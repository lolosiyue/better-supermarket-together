using BetterSupermarketTogether;

internal static class PresenceChecks
{
    internal static void Run(Action<bool,string> check)
    {
        var gaze=new CompanionGazePolicy();double t=0;
        void Step(double seconds,bool available,bool working){for(double end=t+seconds;t<end-1e-9;t+=.05)gaze.Update(t,.05,available,working,0);}
        Step(.5,true,false);
        check(gaze.Looking && Math.Abs(gaze.Weight-1)<1e-9,"idle companion turns to meet the player's eyes");
        Step(3.4,true,false);
        check(gaze.Looking,"eye contact is held for several seconds");
        Step(.5,true,false);
        check(!gaze.Looking && gaze.Weight==0,"she glances away naturally after a while");
        Step(1,true,false);Step(.5,true,false);
        check(gaze.Looking && gaze.Weight>.9,"and looks back again");
        Step(.5,false,false);
        check(gaze.Weight==0,"gaze releases when the player leaves view");
        Step(.5,true,true);
        check(Math.Abs(gaze.Weight-.6)<1e-9,"while working she only half-turns for a glance");
        Step(1.5,true,true);
        check(!gaze.Looking,"working glances are brief");

        var stare=new CompanionStarePolicy();
        check(!stare.Observe(0,true) && !stare.Observe(1.9,true) && stare.Observe(2,true),"two seconds of staring triggers a reaction");
        check(!stare.Observe(5,true),"continuous staring does not repeat the reaction");
        stare.Observe(6,false);stare.Observe(6.6,false);
        check(!stare.Observe(7,true) && !stare.Observe(9.5,true),"reaction cooldown prevents spam");
        stare.Observe(70,false);stare.Observe(70.6,false);
        check(!stare.Observe(71,true) && stare.Observe(73,true),"after looking away and the cooldown she reacts again");
        stare.Reset();
        check(!stare.Observe(100,true) && stare.Observe(102,true),"reset re-arms stare reactions");

        var presence=new CompanionPresencePolicy();
        check(presence.Observe(0,true)==0 && presence.Observe(10,true)==0,"normal activity is not a return");
        check(presence.Observe(100,false)==0 && presence.Observe(150,true)==0,"short pauses are not a return");
        check(presence.Observe(400,true)==250,"activity after three idle minutes is a return");
        presence.Reset();
        check(presence.Observe(1000,true)==0,"reset starts a new session baseline");

        var all=CompanionBehaviorPreferences.Resolve(new string[0]);
        check(all.Gaze && all.Company && all.GreetOn && all.StareOn && all.YawnOn,"companionship behaviors default on");
        var off=CompanionBehaviorPreferences.Resolve(new[]{"不要一直看著我","不要跟著我","行為偏好｜迎接我：關","行為偏好｜被注視反應：關","不要打哈欠"});
        check(!off.Gaze && !off.Company && !off.GreetOn && !off.StareOn && !off.YawnOn && off.Nod,"companionship behaviors can be switched off individually");
        var quiet=CompanionBehaviorPreferences.Resolve(new[]{"行為偏好｜小動作頻率：關"});
        check(quiet.Gaze && quiet.Company && !quiet.GreetOn && !quiet.StareOn && !quiet.YawnOn,"gesture master off silences gestures but keeps eye contact and company");
        check(all.Signature!=off.Signature && (bool)off.Snapshot()["look_at_player"]==false,"new preferences are part of signature and snapshot");

        var life=new CompanionLifePolicy();
        life.Observe(0,true);
        check(!life.CompanyDue(10) && life.CompanyDue(20),"after-hours company waits for settled idleness");
        life.CompanyAttempt(20);
        check(!life.CompanyDue(30) && life.CompanyDue(40),"company walks are spaced out");
        life.Defer(40);life.Observe(41,true);
        check(!life.CompanyDue(90) && life.CompanyDue(100),"player requests defer company");
        life.Observe(101,false);
        check(!life.CompanyDue(200),"working cancels company");
    }
}
