using BetterSupermarketTogether;
using Newtonsoft.Json.Linq;

internal static class PlanningChecks
{
    internal static void Run(Action<bool,string> check)
    {
        bool Parse(string json,bool automatic=false) => CompanionStep.TryParse(JObject.Parse(json),automatic,out _);
        const string finite="{steps:[{kind:'restock',product_id:17,shelf_id:'S4'},{kind:'work',role:'checkout',completion:'continuous'}]}";
        check(Parse(finite),"targeted restock then continuous checkout is a valid ordered plan");
        check(Parse("{steps:[{kind:'follow',duration_seconds:30},{kind:'restock',shelf_id:'S2'}]}"),"timed follow can precede a shelf task");
        check(Parse("{steps:[{kind:'follow'}]}"),"explicit continuous follow is accepted for a player");
        foreach(string json in new[]{
            "{steps:[]}","{steps:[{kind:'work',role:'checkout',completion:'continuous'},{kind:'follow'}]}",
            "{steps:[{kind:'work',role:'rest',completion:'one_cycle'}]}","{steps:[{kind:'restock',shelf_id:'2'}]}",
            "{steps:[{kind:'restock',shelf_id:'S2; code'}]}","{steps:[{kind:'restock',product_id:-1}]}",
            "{steps:[{kind:'work',role:'purchase',completion:'one_cycle'}]}","{steps:[{kind:'follow',employee:2}]}",
            "{steps:[{kind:'follow',duration_seconds:0}]}","{steps:[{kind:'work',role:'repair',completion:'duration',duration_seconds:4}]}",
            "{steps:[{kind:'restock',product_id:1.5}]}","{steps:[{kind:'work',role:'checkout',completion:'duration',duration_seconds:1801}]}"})
            check(!Parse(json),"reject ambiguous, unsupported or unbounded plan: "+json);
        check(!Parse("{steps:[{kind:'restock',product_id:"+new string('9',100)+"}]}"),"oversized plan integers fail safely");
        check(!Parse("{steps:["+string.Join(",",Enumerable.Repeat("{kind:'restock'}",7))+"]}"),"plan is bounded to six steps");
        check(!Parse(finite,true) && !Parse("{steps:[{kind:'follow',duration_seconds:30}]}",true),"autonomy cannot create continuous or follow tasks");
        check(Parse("{steps:[{kind:'restock'},{kind:'work',role:'repair',completion:'one_cycle'}]}",true),"autonomy accepts finite shop work");
        check(CompanionToolPolicy.Allowed("inspect_shop",true) && !CompanionToolPolicy.Allowed("remember",true) &&
            !CompanionToolPolicy.Allowed("forget_memory",true) && !CompanionToolPolicy.Allowed("set_autonomy",true) && !CompanionToolPolicy.Allowed("cancel_plan",true),"autonomous authority is enforced independently of model instructions");
        check(!CompanionToolPolicy.CanHandoff(2,4,1,false,0) && !CompanionToolPolicy.CanHandoff(7,-1,5,true,3),"carried stock and production ingredients prevent handoff");
        check(!CompanionToolPolicy.CanHandoff(6,2,0,false,0) && !CompanionToolPolicy.CanHandoff(7,2,0,false,0),"committed order and production tasks are not interrupted before equipment appears");
        check(CompanionToolPolicy.CanHandoff(6,-1,0,true,0) && CompanionToolPolicy.CanHandoff(7,-1,0,true,1),"known idle waits are safe handoff boundaries");
        check(!CompanionToolPolicy.CanHandoff(2,-1,0,false,0),"an orphaned wait is not assumed safe");
        check(!CompanionToolPolicy.CanHandoff(4,-1,0,true,2),"security does not abandon an active thief chase during a hit wait");
        var quota=new CompanionRestockQuota(2,10);
        check(quota.Remaining==8,"targeted restock captures the initial shelf deficit");
        quota.Add(3);quota.Observe(3,10);
        check(quota.Remaining==5 && !quota.Done,"customer consumption cannot erase confirmed delivery progress");
        quota.Add(-1);check(quota.Remaining==5,"invalid delivery quantity does not manufacture progress");
        quota.Add(5);quota.Observe(7,10);check(quota.Done,"delivering the original deficit finishes a finite goal despite continuing sales");
        quota=new CompanionRestockQuota(2,10);quota.Observe(10,10);quota.Observe(9,10);
        check(quota.Done && quota.Remaining==0,"a shelf filled by another worker remains an achieved objective");
        check(new CompanionRestockQuota(10,10).Done,"already full shelf requires no artificial work");

        JObject Call(string id,string name,string args) => new JObject{["id"]=id,["type"]="function",["function"]=new JObject{["name"]=name,["arguments"]=args}};
        var budget=new CompanionToolBudget();
        check(budget.Accept(new JArray(Call("i1","inspect_shop","{query:'milk',offset:0}")),false),"first tool round can inspect before choosing a target");
        check(budget.Accept(new JArray(Call("p1","set_plan",finite)),false) && budget.Controls==1,"a later tool round can queue one ordered plan");
        check(!budget.Accept(new JArray(Call("p2","assign_work","{role:'repair'}")),false),"second control action is rejected across tool rounds");
        check(!budget.Accept(new JArray(Call("i1","inspect_shop","{query:'milk',offset:0}")),false),"replayed call ID is rejected across tool rounds");
        var mixed=new CompanionToolBudget();
        check(!mixed.Accept(new JArray(Call("a","assign_work","{role:'checkout'}"),Call("b","cancel_plan","{}")),false) && mixed.Calls==0,"invalid batch is rejected atomically before any execution");
        check(!new CompanionToolBudget().Accept(new JArray(Call("a","set_plan","{steps:[{kind:'follow'}]}")),true),"autonomous follow is rejected by dispatch budget");
        check(!new CompanionToolBudget().Accept(new JArray(Call("a","remember","{fact:'invented'}")),true),"autonomous memory writes are rejected by dispatch budget");
        var correction=new CompanionToolBudget();
        var invalid=new JArray(Call("bad","set_plan","{steps:[{kind:'restock',completion:'one_cycle'}]}"));
        check(!correction.Accept(invalid,false) && correction.TryCorrection(invalid,1) && correction.Calls==0,"invalid plan can receive correction feedback without executing anything");
        check(!correction.TryCorrection(invalid,2),"at most one format correction is allowed per turn");
        check(correction.Accept(new JArray(Call("fixed","set_plan","{steps:[{kind:'restock'}]}")),false),"corrected plan is validated before execution");
        check(!new CompanionToolBudget().TryCorrection(new JArray(new JObject{["id"]="missing-function"}),1),"malformed tool envelopes are not echoed as executable history");
        check(!CompanionToolBudget.CanReportRejection(new JArray(new JObject{["type"]=new JObject(),["id"]="bad-type"})),"non-string tool envelope type cannot crash format recovery");
        check(!new CompanionToolBudget().TryCorrection(invalid,3),"format repair cannot exceed the tool-round budget");
        var bounded=new CompanionToolBudget();
        for(int i=0;i<8;i++)check(bounded.Accept(new JArray(Call("id"+i,"inspect_shop","{query:'',offset:0}")),false),"bounded inspection budget accepts call "+(i+1));
        check(!bounded.Accept(new JArray(Call("extra","inspect_shop","{query:'',offset:0}")),false),"tool count budget caps a turn at eight calls");

        CompanionStep A()=>new CompanionStep{Kind="restock",Product=17,Shelf="S4"};
        CompanionStep B()=>new CompanionStep{Kind="work",Role="checkout",Completion="continuous"};
        var world=new FakeWorld();var runner=new CompanionPlanRunner();
        runner.Replace(new List<CompanionStep>{A(),B()},false);
        world.Safe=false;runner.Tick(0,world);
        check(world.Started.Count==0 && runner.Owns,"queued replacement waits while employee is carrying goods");
        world.Safe=true;runner.Tick(1,world);
        check(world.Started.SequenceEqual(new[]{"restock"}) && runner.Active.Kind=="restock","first step starts at a safe boundary");
        world.Progress=CompanionProgress.Complete;world.Safe=false;runner.Tick(2,world);runner.Tick(3,world);
        check(world.Started.Count==1 && runner.HandingOff,"completed A waits for box return before starting B");
        world.Safe=true;world.Progress=CompanionProgress.Running;runner.Tick(4,world);
        check(world.Started.SequenceEqual(new[]{"restock","work"}) && runner.Active.Role=="checkout","B starts only after A completes and handoff is safe");
        runner.Cancel();world.Safe=false;runner.Tick(5,world);
        check(runner.Owns && world.Rests==0,"cancel never forces a busy employee to drop cargo");
        world.Safe=true;runner.Tick(6,world);
        check(!runner.Owns && world.Rests==1,"cancel rests after safe handoff and clears remaining steps");

        world=new FakeWorld();runner=new CompanionPlanRunner();runner.Replace(new List<CompanionStep>{A(),B()},false);runner.Tick(0,world);
        world.Progress=CompanionProgress.Failed;runner.Tick(1,world);runner.Tick(2,world);
        check(world.Started.Count==1 && !runner.Owns && runner.Phase==CompanionPlanPhase.Failed,"failed A stops the sequence instead of executing B");
        world=new FakeWorld {StartError="shelf removed"};runner=new CompanionPlanRunner();runner.Replace(new List<CompanionStep>{A(),B()},false);runner.Tick(0,world);
        check(world.Started.Count==1 && !runner.Owns && runner.Phase==CompanionPlanPhase.Failed && runner.Status.Render(true).Contains("shelf removed"),"stale target detected at start prevents all later steps");
        world=new FakeWorld();runner=new CompanionPlanRunner();runner.Replace(new List<CompanionStep>{A()},true);runner.Tick(0,world);
        world.Safe=false;runner.Replace(new List<CompanionStep>{B()},false);runner.Tick(1,world);
        check(world.Started.Count==1 && !runner.Automatic,"new player plan supersedes autonomous plan but waits for safety");
        world.Safe=true;runner.Tick(2,world);
        check(world.Started.Count==2 && runner.Active.Role=="checkout","replacement starts after old cargo is handled");
        runner.Reset(world);check(!runner.Owns && runner.Active==null,"shop/binding reset releases all queued work");
        world=new FakeWorld();runner=new CompanionPlanRunner();runner.Replace(new List<CompanionStep>{A()},false);runner.Tick(0,world);
        world.Progress=CompanionProgress.Complete;runner.Tick(1,world);runner.Tick(2,world);
        check(!runner.Owns && world.Rests==1,"final finite step rests instead of continuing unrelated native work");
        world=new FakeWorld {ThrowPoll=true};runner.Replace(new List<CompanionStep>{A(),B()},false);runner.Tick(0,world);runner.Tick(1,world);
        check(!runner.Owns && world.Started.Count==1 && runner.Phase==CompanionPlanPhase.Failed,"runtime observation exception stops plan without advancing");

        var gate=new CompanionAutonomyGate();
        check(!gate.TryBegin(0,"a",false,120,2),"disabled or busy autonomy does not consume a decision");
        check(gate.TryBegin(0,"a",true,120,2),"enabled idle autonomy can evaluate initial shop state");
        check(!gate.TryBegin(60,"b",true,120,2),"changed state cannot bypass cooldown");
        check(!gate.TryBegin(120,"a",true,120,2),"unchanged state does not trigger repeated API requests");
        check(gate.TryBegin(120,"b",true,120,2) && !gate.TryBegin(240,"c",true,120,2),"hourly decision cap is enforced");
        check(gate.TryBegin(3600,"c",true,120,2),"rolling-hour budget expires old decisions");
        gate.Reset();check(gate.TryBegin(0,"a",true,120,20),"new room resets decision state");
        gate.Failure(0);check(!gate.TryBegin(119,"a",true,120,20) && gate.TryBegin(120,"a",true,120,20),"first API failure retries only after backoff even with same state");
        gate.Failure(120);check(!gate.TryBegin(359,"a",true,120,20) && gate.TryBegin(360,"a",true,120,20),"repeated API failure doubles retry delay");
        gate.Success();gate.Changed();check(gate.TryBegin(480,"a",true,120,20),"plan outcome can trigger fresh evaluation after cooldown");
    }

    private sealed class FakeWorld : ICompanionWorkWorld
    {
        public bool Safe {get;set;}=true;
        internal readonly List<string> Started=new();
        internal CompanionProgress Progress=CompanionProgress.Running;
        internal ModText.Message StartError;
        internal int Rests;
        internal bool ThrowPoll;
        public ModText.Message Start(CompanionStep step){Started.Add(step.Kind);return StartError;}
        public CompanionProgress Poll(CompanionStep step,double elapsed,out ModText.Message detail){detail=step.Label;if(ThrowPoll)throw new Exception("fake observation failure");return Progress;}
        public void PrepareHandoff(){}
        public void Release(){}
        public void Rest(){Rests++;}
    }
}
