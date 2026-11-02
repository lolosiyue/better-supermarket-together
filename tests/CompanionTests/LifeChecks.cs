using BetterSupermarketTogether;
using Newtonsoft.Json.Linq;
internal static class LifeChecks
{
    internal static void Run(Action<bool,string> check)
    {
        var gate=new CompanionLifePolicy();
        gate.Observe(0,true);
        check(!gate.HelpDue(15),"help waits through initial cooldown");
        check(gate.HelpDue(30),"settled idle companion can help");
        gate.HelpAttempt(30);
        check(!gate.HelpDue(209) && gate.HelpDue(210),"help attempts have three minute cooldown");
        gate.Observe(211,false);gate.Observe(212,true);
        check(!gate.HelpDue(226) && gate.HelpDue(227),"new native activity resets idle settling");
        check(gate.GestureDue(227),"settled idle allows gestures");
        gate.GestureAttempt(227,1);
        check(!gate.GestureDue(346) && gate.GestureDue(347),"gesture interval upper bound is two minutes");
        gate.Defer(348);
        check(!gate.HelpDue(1000) && !gate.GestureDue(1000),"player intervention requires fresh idle observation");
        gate.Reset();gate.Observe(500,true);
        check(!gate.HelpDue(529) && gate.HelpDue(530),"shop reset does not inherit stale cooldown clock");
        check(CompanionLifePolicy.Idle(0,1,0,false,false,0,false),"native rest state may gesture");
        check(CompanionLifePolicy.Idle(3,-1,0,true,true,0,false),"known uncommitted storage wait can be considered");
        check(!CompanionLifePolicy.Idle(3,1,0,false,false,0,false),"committed empty-handed box pickup is not idle");
        check(!CompanionLifePolicy.Idle(1,3,0,false,false,0,false),"cashier at assigned checkout is not borrowed");
        check(!CompanionLifePolicy.Idle(4,1,0,false,false,0,false) && !CompanionLifePolicy.Idle(7,1,0,false,false,0,false),"security patrol and production remain dedicated");
        check(!CompanionLifePolicy.Idle(0,1,1,false,false,0,false),"carried items prevent idle gestures");
        check(!CompanionLifePolicy.Idle(0,1,0,false,false,0,true),"walking prevents idle gestures");
        check(!CompanionLifePolicy.Idle(3,-1,0,true,false,0,false),"unknown wait cannot be borrowed");
        check(!CompanionLifePolicy.Idle(5,-1,0,true,true,2,false),"repair coroutine is not idle");
        check(!CompanionStep.TryParse(JObject.Parse("{steps:[{kind:'restock',rowPair:0,oneDelivery:true}]}"),false,out _),"internal proactive fields cannot expand API tool schema");
    }
}
