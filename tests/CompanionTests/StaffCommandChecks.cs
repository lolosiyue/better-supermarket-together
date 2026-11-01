using BetterSupermarketTogether;
using Newtonsoft.Json.Linq;

internal static class StaffCommandChecks
{
    private sealed class Worker
    {
        internal int Role;
        internal long Progress;
        internal bool Safe=true;
    }
    private sealed class World : ICompanionStaffWorld
    {
        public bool Authorized { get; set; }=true;
        public bool CanRestore { get; set; }=true;
        internal readonly Dictionary<string,Worker> Workers=new() { ["E1"]=new(),["E2"]=new() };
        internal int Assignments;
        internal bool Reject;
        public bool Available(string id,out int role,out bool safe,out string label)
        {
            role=0;safe=false;label=id;
            if(!Workers.TryGetValue(id,out var w))return false;
            role=w.Role;safe=w.Safe;return true;
        }
        public long Progress(string id,int role) => Workers.TryGetValue(id,out var worker)?worker.Progress:0;
        public bool Assign(string id,int role,bool restoring=false)
        {
            if(Reject)return false;
            Assignments++;Workers[id].Role=role;return true;
        }
    }
    private static JObject Call(string name,string args,string id="staff") => new() { ["id"]=id,["type"]="function",
        ["function"]=new JObject { ["name"]=name,["arguments"]=args } };
    private static List<CompanionStaffOrder> Permanent(params (string id,string role)[] rows) => rows.Select(x=>new CompanionStaffOrder{Id=x.id,Role=x.role,Completion="continuous"}).ToList();
    internal static void Run(Action<bool,string> check)
    {
        const string valid="{assignments:[{staff_id:'E1',role:'checkout'},{staff_id:'E2',role:'restock'}]}";
        check(CompanionProtocol.ValidateCall(Call("assign_staff",valid),out _,out _),"staff batch accepts different native roles");
        foreach(string json in new[]{"{assignments:[]}","{assignments:[{staff_id:'E1',role:'purchase'}]}",
            "{assignments:[{staff_id:'E1',role:'checkout'},{staff_id:'E1',role:'restock'}]}",
            "{assignments:[{staff_id:1,role:'checkout'}]}","{assignments:[{staff_id:'E1',role:'checkout',employee_number:2}]}",
            "{assignments:[{staff_id:'E1',role:'checkout'}],all:true}","{assignments:[{staff_id:'E1;drop',role:'checkout'}]}",
            "{assignments:[{staff_id:'E1',role:'checkout',role:'rest'}]}"})
            check(!CompanionProtocol.ValidateCall(Call("assign_staff",json),out _,out _),"invalid staff command rejected: "+json);
        var large=new JObject { ["assignments"]=new JArray(Enumerable.Range(1,65).Select(i=>new JObject{["staff_id"]="E"+i,["role"]="rest"})) };
        check(!CompanionStaffOrder.TryParse(large,out _),"staff batch is bounded to 64 employees");
        check(CompanionProtocol.ValidateCall(Call("inspect_staff","{query:'Alex',offset:20}"),out _,out _) &&
            !CompanionProtocol.ValidateCall(Call("inspect_staff","{query:'',offset:-1}"),out _,out _),"staff roster pagination validates bounds");
        check(!new CompanionToolBudget().Accept(new JArray(Call("assign_staff",valid)),true) &&
            !new CompanionToolBudget().Accept(new JArray(Call("cancel_staff","{}")),true),"bound-employee autonomy cannot dispatch or cancel other staff");
        check(new CompanionToolBudget().Accept(new JArray(Call("assign_staff",valid)),false),"player staffing request is one control action");
        check(!new CompanionToolBudget().Accept(new JArray(Call("assign_staff",valid),Call("assign_work","{role:'rest'}","self")),false),"mixed staff/self mutations reject entire batch");
        var budget=new CompanionToolBudget();
        check(budget.Accept(new JArray(Call("inspect_staff","{query:'',offset:0}","roster")),false) &&
            budget.Accept(new JArray(Call("assign_staff",valid)),false),"staff inspection can precede one dispatch");

        var q=new CompanionStaffQueue();var w=new World();w.Workers["E1"].Safe=false;
        q.Enqueue(Permanent(("E1","checkout"),("E2","restock")),0,w);q.Tick(0,w);
        check(q.Count==1 && w.Workers["E1"].Role==0 && w.Workers["E2"].Role==2,"busy employee waits while safe colleague receives role");
        w.Workers["E1"].Safe=true;q.Tick(1,w);
        check(q.Count==0 && w.Workers["E1"].Role==1 && w.Assignments==2,"deferred staff role executes at safe boundary");
        var notices=q.Drain().ToArray();
        check(notices.Length==2 && notices.All(x=>x.Source.Contains("已確認")) && q.Recent.Count()==2,"only confirmed dispatches produce success and enter next context");
        check(ModText.Message.TryNotice(notices[0].ToNotice(),out var reloaded) && reloaded.WireText==notices[0].WireText,"staff outcome survives localized notice persistence");

        q=new();w=new();q.Enqueue(Permanent(("E1","checkout"),("E99","restock")),0,w);q.Tick(0,w);
        check(q.Count==0 && w.Assignments==0,"unknown target rejects entire batch before mutation");
        q.Enqueue(Permanent(("E1","checkout")),0,w);w.Workers.Remove("E1");q.Tick(1,w);
        check(q.Count==0 && w.Assignments==0 && q.Drain().Single().Source.Contains("取消"),"removed or replaced employee cancels pending assignment");
        q=new();w=new();q.Enqueue(Permanent(("E1","checkout")),0,w);w.Workers["E1"].Role=3;q.Tick(1,w);
        check(q.Count==0 && w.Assignments==0 && w.Workers["E1"].Role==3,"external role change wins over pending command");
        q=new();w=new();q.Enqueue(Permanent(("E1","checkout")),0,w);q.Cancel("E1");q.Tick(1,w);
        check(q.Count==0 && w.Assignments==0,"native UI same-role override also cancels via explicit hook");
        q=new();w=new();w.Authorized=false;q.Enqueue(Permanent(("E1","checkout")),0,w);
        check(q.Count==0,"unauthorized enqueue changes nothing");
        w.Authorized=true;q.Enqueue(Permanent(("E1","checkout")),0,w);w.Authorized=false;q.Tick(1,w);
        check(q.Count==1 && w.Assignments==0,"authority loss blocks queued mutation");q.Tick(180,w);
        check(q.Count==0 && q.Drain().Single().WireText.Contains("three minutes"),"pending command expires without interrupting work");
        q=new();w=new();w.Reject=true;q.Enqueue(Permanent(("E1","checkout")),0,w);q.Tick(0,w);
        check(q.Count==0 && q.Drain().Single().Source.Contains("失敗"),"unconfirmed native role change reports failure");
        q=new();w=new();q.Enqueue(Permanent(("E1","checkout")),0,w);q.Enqueue(Permanent(("E1","storage")),1,w);q.Tick(1,w);
        check(w.Assignments==1 && w.Workers["E1"].Role==3,"new player assignment replaces pending assignment for same employee");
        q.Enqueue(Permanent(("E2","restock")),2,w);q.CancelAll();q.Tick(3,w);
        check(w.Assignments==1 && w.Workers["E1"].Role==3,"cancel pending preserves already-applied native jobs");
        q.Reset();check(q.Count==0 && !q.Drain().Any() && !q.Recent.Any(),"shop reset clears pending commands and event history");
        CompanionStaffOrder.TryParse(JObject.Parse(valid),out var defaults);
        check(defaults.All(x=>x.Completion=="one_cycle"),"staff dispatch defaults to one work cycle");
        CompanionStaffOrder.TryParse(JObject.Parse("{assignments:[{staff_id:'E1',role:'rest'}]}"),out var rest);
        check(rest[0].Completion=="duration" && rest[0].Seconds==120,"temporary rest defaults to two minutes");
        foreach(string args in new[]{"{assignments:[{staff_id:'E1',role:'rest',completion:'one_cycle'}]}",
            "{assignments:[{staff_id:'E1',role:'restock',completion:'duration'}]}",
            "{assignments:[{staff_id:'E1',role:'restock',completion:'continuous',duration_seconds:10}]}",
            "{assignments:[{staff_id:'E1',role:'restock',completion:'duration',duration_seconds:0}]}"})
            check(!CompanionProtocol.ValidateCall(Call("assign_staff",args),out _,out _),"reject malformed temporary staff duration: "+args);
        List<CompanionStaffOrder> Temporary(string role="checkout",string completion="one_cycle",int seconds=0) =>
            new(){new CompanionStaffOrder {Id="E1",Role=role,Completion=completion,Seconds=seconds}};
        q=new();w=new();w.Workers["E1"].Role=3;w.Workers["E1"].Progress=100;
        q.Enqueue(Temporary(),0,w);q.Tick(0,w);q.Tick(1,w);
        check(q.Count==1 && !q.Hold("E1") && w.Workers["E1"].Role==1,"temporary role stays active until real progress");
        w.Workers["E1"].Progress++;w.Workers["E1"].Safe=false;q.Tick(2,w);
        check(q.Hold("E1") && q.Phase("E1")=="returning" && w.Workers["E1"].Role==1,"completed work waits until carried goods are safe");
        w.Workers["E1"].Safe=true;q.Tick(3,w);
        check(q.Count==0 && w.Workers["E1"].Role==3,"completed temporary dispatch restores original storage role");
        check(q.Drain().Any(x=>x.Source.Contains("已恢復")),"restoration reports a confirmed return");
        q=new();w=new();w.Workers["E1"].Role=3;q.Enqueue(Temporary(),0,w);q.Tick(0,w);
        q.Enqueue(Temporary("restock"),1,w);q.Tick(1,w);w.Workers["E1"].Progress++;q.Tick(2,w);
        check(w.Workers["E1"].Role==3 && q.Count==0,"replacement temporary dispatch retains the initial original role");
        q=new();w=new();w.Workers["E1"].Role=3;q.Enqueue(Temporary(),0,w);q.Tick(0,w);w.Workers["E1"].Role=5;q.Tick(1,w);
        check(q.Count==0 && w.Workers["E1"].Role==5,"manual reassignment overrides automatic restoration");
        q=new();w=new();w.Workers["E1"].Role=3;q.Enqueue(Temporary(),0,w);q.Tick(0,w);q.CancelAll();w.Authorized=false;q.Tick(1,w);
        check(q.Count==0 && w.Workers["E1"].Role==3,"disabling or cancelling new commands still allows safe restoration");
        q=new();w=new();w.Workers["E1"].Role=3;q.Enqueue(Temporary(),0,w);q.Tick(0,w);q.Tick(900,w);
        check(q.Count==0 && w.Workers["E1"].Role==3,"no-progress work times out and returns without claiming completion");
        q=new();w=new();w.Workers["E1"].Role=3;q.Enqueue(Temporary("checkout","duration",1800),0,w);q.Tick(0,w);q.Tick(900,w);
        check(q.Count==1 && w.Workers["E1"].Role==1,"explicit 30-minute duty is not cut short by cycle timeout");
        q.Tick(1800,w);check(q.Count==0 && w.Workers["E1"].Role==3,"duration completion restores original role");
        q=new();w=new();w.Workers["E1"].Role=3;q.Enqueue(Temporary(),0,w);q.Tick(0,w);w.Reject=true;q.CancelAll();
        q.Tick(1,w);q.Tick(2,w);q.Tick(3,w);q.Tick(5,w);
        check(q.Count==0 && w.Workers["E1"].Role==1 && q.Drain().Any(x=>x.Source.Contains("恢復原職失敗")),"failed restores retry with a bound and report failure honestly");

    }
}
