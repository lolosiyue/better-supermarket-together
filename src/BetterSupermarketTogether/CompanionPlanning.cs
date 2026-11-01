using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace BetterSupermarketTogether;

internal sealed class CompanionStep
{
    internal string Kind, Role, Completion, Shelf;
    internal int? Product;
    internal int Seconds;
    internal bool Continuous => Kind == "follow" ? Seconds == 0 : Kind == "work" && Completion == "continuous";
    internal ModText.Message Label
    {
        get
        {
            if(Kind=="restock")return new ModText.Message("補貨：{0}{1}",Product.HasValue?new ModText.Message("商品 #{0}",Product.Value):new ModText.Message("所有商品"),
                Shelf!=null?(object)new ModText.Message("，貨架 {0}",Shelf):"");
            if(Kind=="follow")return Seconds>0?new ModText.Message("跟隨房主 {0} 秒",Seconds):new ModText.Message("持續跟隨房主");
            var role=new ModText.Message(CompanionProtocol.RoleLabels[Array.IndexOf(CompanionProtocol.Roles,Role)]);
            return Completion=="continuous"?new ModText.Message("{0}（持續）",role):Completion=="one_cycle"?new ModText.Message("{0}（完成一輪）",role):
                new ModText.Message("{0}（限時 {1} 秒）",role,Seconds);
        }
    }

    internal static bool TryParse(JObject args, bool automatic, out List<CompanionStep> steps)
    {
        steps = null;
        if (args?.Count != 1 || args["steps"] is not JArray array || array.Count < 1 || array.Count > 6) return false;
        var parsed = new List<CompanionStep>();
        foreach (var token in array)
        {
            if (token is not JObject obj || obj["kind"]?.Type != JTokenType.String) return false;
            var step = new CompanionStep { Kind=(string)obj["kind"] };
            string[] allowed;
            if (step.Kind == "work")
            {
                allowed = new[] { "kind", "role", "completion", "duration_seconds" };
                if (obj["role"]?.Type != JTokenType.String || !CompanionProtocol.Roles.Contains((string)obj["role"]) ||
                    obj["completion"]?.Type != JTokenType.String) return false;
                step.Role=(string)obj["role"]; step.Completion=(string)obj["completion"];
                if (step.Completion != "continuous" && step.Completion != "one_cycle" && step.Completion != "duration") return false;
                if (step.Role == "rest" && step.Completion == "one_cycle") return false;
                if (step.Completion == "duration")
                {
                    if (!Integer(obj["duration_seconds"],5,1800,out step.Seconds)) return false;
                }
                else if (obj["duration_seconds"] != null) return false;
            }
            else if (step.Kind == "restock")
            {
                allowed = new[] { "kind", "product_id", "shelf_id" };
                if (obj["product_id"] != null)
                {
                    if (!Integer(obj["product_id"],0,99999,out int product)) return false;
                    step.Product=product;
                }
                if (obj["shelf_id"] != null)
                {
                    if (obj["shelf_id"]?.Type != JTokenType.String) return false;
                    string shelf=(string)obj["shelf_id"];
                    if (shelf.Length < 2 || shelf.Length > 10 || shelf[0] != 'S' || !shelf.Skip(1).All(c=>c>='0'&&c<='9')) return false;
                    step.Shelf=shelf;
                }
            }
            else if (step.Kind == "follow")
            {
                allowed = new[] { "kind", "duration_seconds" };
                if (automatic || obj["duration_seconds"] != null && !Integer(obj["duration_seconds"],5,1800,out step.Seconds)) return false;
            }
            else return false;
            if (obj.Properties().Any(p=>!allowed.Contains(p.Name)) || automatic && step.Continuous) return false;
            parsed.Add(step);
        }
        if (parsed.Take(parsed.Count-1).Any(s=>s.Continuous)) return false;
        steps=parsed; return true;
    }
    private static bool Integer(JToken token,int min,int max,out int value)
    {
        value=0;
        if (token?.Type != JTokenType.Integer) return false;
        try { long n=(long)token; if(n<min||n>max)return false; value=(int)n; return true; }
        catch (OverflowException) { return false; }
    }
}

internal enum CompanionProgress { Running, Complete, Failed }
internal enum CompanionPlanPhase { Idle, Waiting, Running, Stopping, Completed, Failed }

internal sealed class CompanionRestockQuota
{
    internal int Needed { get; }
    internal int Delivered { get; private set; }
    internal bool Done { get; private set; }
    internal int Remaining => Done?0:Math.Max(0,Needed-Delivered);
    internal CompanionRestockQuota(int count,int capacity) { Needed=Math.Max(0,capacity-count);Done=Needed==0; }
    internal void Add(int quantity) { if(quantity>0)Delivered+=quantity;Done |= Delivered>=Needed; }
    internal void Observe(int count,int capacity) { Done |= capacity>0 && count>=capacity; }
}

internal interface ICompanionWorkWorld
{
    bool Safe { get; }
    ModText.Message Start(CompanionStep step);
    CompanionProgress Poll(CompanionStep step,double elapsed,out ModText.Message detail);
    void PrepareHandoff();
    void Release();
    void Rest();
}

// All transitions use an injected clock/world; the same queue runs in the game and tests.
internal sealed class CompanionPlanRunner
{
    private readonly Queue<CompanionStep> _queue = new Queue<CompanionStep>();
    private List<CompanionStep> _replacement;
    private bool _stopping, _advance, _rest;
    private double _started;
    internal CompanionStep Active { get; private set; }
    internal bool Automatic { get; private set; }
    internal bool Owns => Active != null || _queue.Count != 0 || _replacement != null || _stopping;
    internal bool HandingOff => _stopping || _advance || _replacement != null;
    internal CompanionPlanPhase Phase { get; private set; } = CompanionPlanPhase.Idle;
    internal int EventVersion { get; private set; }
    internal ModText.Message Status { get; private set; } = "待命";
    internal string Summary(bool english) => Status.Render(english)+(Active!=null?"\n"+ModText.FormatFor(english,"目前：{0}",Active.Label):"")+
        (_queue.Count!=0?"\n"+ModText.FormatFor(english,"接續：{0}",string.Join(" → ",_queue.Select(x=>x.Label.Render(english)))):"");
    internal void Replace(List<CompanionStep> steps,bool automatic)
    {
        _replacement=steps.ToList(); Automatic=automatic; _queue.Clear();
        _stopping=true; _advance=false; _rest=false; Phase=CompanionPlanPhase.Waiting;Status="等待手上工作安全交接";
    }
    internal void Cancel()
    {
        _replacement=null;_queue.Clear();_stopping=true;_advance=false;_rest=true;Phase=CompanionPlanPhase.Stopping;Status="安全交接後停止";
    }
    internal void Reset(ICompanionWorkWorld world)
    {
        world.Release();Active=null;_replacement=null;_queue.Clear();_stopping=_advance=_rest=false;Phase=CompanionPlanPhase.Idle;Status="待命";EventVersion=0;
    }
    internal void Tick(double now,ICompanionWorkWorld world)
    {
        if (_stopping || _advance)
        {
            world.PrepareHandoff();
            if (!world.Safe) return;
            world.Release(); Active=null;
            if (_rest) world.Rest();
            _rest=false;_stopping=_advance=false;
            if (_replacement != null) { foreach(var step in _replacement)_queue.Enqueue(step);_replacement=null; }
        }
        if (Active == null)
        {
            if (_queue.Count == 0) return;
            if (!world.Safe) { Phase=CompanionPlanPhase.Waiting;Status="等待手上工作安全交接";return; }
            var next=_queue.Dequeue();
            ModText.Message error;
            try { error=world.Start(next); }
            catch (Exception) { error="原生任務未能開始。"; }
            if (error != null) { _queue.Clear();world.Release();Phase=CompanionPlanPhase.Failed;Status=new ModText.Message("未能完成：{0}",error);EventVersion++;return; }
            Active=next;_started=now;Phase=CompanionPlanPhase.Running;Status="執行中";
        }
        CompanionProgress progress;ModText.Message detail;
        try { progress=world.Poll(Active,now-_started,out detail); }
        catch (Exception) { progress=CompanionProgress.Failed;detail="無法讀取原生任務進度。"; }
        if (progress == CompanionProgress.Running) { Phase=CompanionPlanPhase.Running;Status=detail ?? new ModText.Message("執行中");return; }
        _advance=true;
        Phase=progress==CompanionProgress.Complete?CompanionPlanPhase.Completed:CompanionPlanPhase.Failed;
        Status=new ModText.Message(progress==CompanionProgress.Complete?"已完成：{0}":"未能完成：{0}",detail??Active.Label);EventVersion++;
        if (progress == CompanionProgress.Failed) { _queue.Clear(); _rest=true; }
        else if (_queue.Count==0) _rest=true;
        // Hold new native task selection until the next safe queue transition.
        world.PrepareHandoff();
    }
}

internal sealed class CompanionAutonomyGate
{
    private readonly Queue<double> _decisions = new Queue<double>();
    private double _next;
    private int _failures;
    private string _fingerprint;
    internal int DecisionsLastHour => _decisions.Count;
    internal int Count(double now) { while(_decisions.Count>0 && now-_decisions.Peek()>=3600)_decisions.Dequeue();return _decisions.Count; }
    internal bool TryBegin(double now,string fingerprint,bool eligible,double interval,int limit)
    {
        while(_decisions.Count>0 && now-_decisions.Peek()>=3600)_decisions.Dequeue();
        if (!eligible || now<_next || _decisions.Count>=limit || fingerprint==_fingerprint) return false;
        _decisions.Enqueue(now);_fingerprint=fingerprint;_next=now+Math.Max(60,interval);return true;
    }
    internal void Success() { _failures=0; }
    internal void Failure(double now)
    {
        _fingerprint=null;_failures=Math.Min(4,_failures+1);_next=now+Math.Min(900,120*Math.Pow(2,_failures-1));
    }
    internal void Changed() { _fingerprint=null; }
    internal void Reset() { _decisions.Clear();_next=0;_failures=0;_fingerprint=null; }
}

internal static class CompanionToolPolicy
{
    internal static bool Movement(string name) => name=="assign_work" || name=="set_plan" || name=="cancel_plan" || name=="set_autonomy";
    internal static bool Allowed(string name,bool automatic) => !automatic || name=="assign_work" || name=="set_plan" || name=="inspect_shop";
    internal static bool CanHandoff(int role,int state,int equipped,bool waiting,int waitTarget)
    {
        if (equipped != 0) return false;
        int effective=state==-1 && waiting ? waitTarget : state;
        if (state==-1 && !waiting) return false;
        if (role==6) return effective==0 || effective==10;
        if (role==7) return effective==0 || effective==1;
        if (role==4) return effective==0 || effective==1 || effective==10;
        if (role==1) return effective==0 || effective==1 || effective==3 || effective==4 || effective==10;
        return true;
    }
}

internal sealed class CompanionToolBudget
{
    private readonly HashSet<string> _ids = new HashSet<string>();
    internal int Calls { get; private set; }
    internal int Controls { get; private set; }
    internal int Corrections { get; private set; }
    internal const string Rejection = "Rejected entire batch; nothing in this batch executed. Follow the exact tool schema. Restock step allows ONLY kind, product_id, shelf_id (no completion); work needs role and completion; follow allows kind and optional duration_seconds. Only one plan/mode change per turn; autonomous mode forbids follow, continuous steps, memories and mode changes. Correct the request or ask the player.";
    internal static bool CanReportRejection(JArray calls) => calls!=null && calls.Count>0 && calls.Count<=4 &&
        calls.All(x=>x is JObject && x["type"]?.Type==JTokenType.String && (string)x["type"]=="function" && x["id"]?.Type==JTokenType.String && !string.IsNullOrWhiteSpace((string)x["id"]) &&
            x["function"] is JObject function && function["name"]?.Type==JTokenType.String && function["arguments"]?.Type==JTokenType.String) &&
        calls.Select(x=>(string)x["id"]).Distinct().Count()==calls.Count;
    internal bool TryCorrection(JArray calls,int round)
    {
        if(Corrections!=0 || round>=3 || Controls!=0 || !CanReportRejection(calls))return false;
        Corrections++;return true;
    }
    internal bool Accept(JArray calls,bool automatic)
    {
        if(calls==null || calls.Count<1 || calls.Count>4 || Calls+calls.Count>8)return false;
        int controls=0;var ids=new HashSet<string>();
        foreach(var token in calls)
        {
            if(token is not JObject call || !CompanionProtocol.ValidateCall(call,out string name,out JObject args) ||
                !CompanionToolPolicy.Allowed(name,automatic) || _ids.Contains((string)call["id"]) || !ids.Add((string)call["id"]))return false;
            if(CompanionToolPolicy.Movement(name))controls++;
            if(automatic && name=="set_plan" && !CompanionStep.TryParse(args,true,out _))return false;
        }
        if(Controls+controls>1)return false;
        Controls+=controls;Calls+=calls.Count;foreach(string id in ids)_ids.Add(id);return true;
    }
}
