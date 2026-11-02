using BetterSupermarketTogether;
internal static class BehaviorPreferenceChecks
{
    internal static void Run(Action<bool,string> check)
    {
        var facts=new List<string>{"我喜歡白飯", "不要偷吃", "少做小動作"};
        var p=CompanionBehaviorPreferences.Resolve(facts);
        check(!p.Rice && p.IntervalScale==3 && p.Help,"supported saved preferences affect effective behavior");
        check(CompanionBehaviorPreferences.Resolve(new[]{"我不想你不要偷吃", "如果缺貨才不要主動幫忙"}).Rice,"negated or conditional prose is not guessed");
        check(CompanionBehaviorPreferences.Store(facts,"行為偏好｜偷吃白飯：開",out var saved) && !facts.Contains("不要偷吃") && facts.Contains("我喜歡白飯"),"update replaces conflicting setting without deleting ordinary facts");
        check(CompanionBehaviorPreferences.Resolve(facts).Rice,"new setting takes effect");
        facts.Add("行為偏好｜整理裙擺：關");
        p=CompanionBehaviorPreferences.Resolve(facts);
        check(p.Gestures.SequenceEqual(new[]{0,1}),"individual skirt preference preserves other gestures");
        facts.Add("行為偏好｜小動作頻率：關");
        check(CompanionBehaviorPreferences.Resolve(facts).Gestures.Length==0,"global gesture off wins over individual defaults");
        int index=facts.Count-1;
        CompanionBehaviorPreferences.Forget(facts,index);
        check(!facts.Contains("少做小動作") && CompanionBehaviorPreferences.Resolve(facts).IntervalScale==1,"forget removes older conflicting aliases instead of resurrecting them");
        p=CompanionBehaviorPreferences.Resolve(facts,false,false,false);
        check(!p.Help && !p.Rice && p.Gestures.Length==0,"configuration master switches override remembered enable");
        var full=Enumerable.Range(0,23).Select(i=>"fact "+i).Append("不要偷吃").ToList();
        check(CompanionBehaviorPreferences.Store(full,"行為偏好｜偷吃白飯：開",out _) && full.Count==24,"full memory permits replacing existing preference");
        check(!CompanionBehaviorPreferences.Store(full,"行為偏好｜主動幫忙：關",out _) && full.Count==24,"full memory rejects new preference atomically");
        check(!CompanionBehaviorPreferences.Parse("行為偏好｜小動作頻率：無限",out _,out _) && !CompanionBehaviorPreferences.Parse("行為偏好｜購買商品：開",out _,out _),"unsupported values and capabilities never become controls");
        var gate=new CompanionLifePolicy();gate.Observe(0,true);gate.GestureAttempt(60,0,3);
        check(!gate.GestureDue(239) && gate.GestureDue(240),"fewer gestures use at least three minute opportunity interval");
        var path=Path.Combine(Path.GetTempPath(),"bst-behavior-"+Guid.NewGuid()+"/memory.json");
        try
        {
            var memory=new CompanionMemory{Facts=facts};memory.Save(path);
            var loaded=CompanionMemory.Load(path);
            check(CompanionBehaviorPreferences.Resolve(loaded.Facts).Signature==CompanionBehaviorPreferences.Resolve(facts).Signature,"effective behavior survives existing UTF8 memory save/load");
            loaded.Facts.Clear();
            check(CompanionBehaviorPreferences.Resolve(loaded.Facts).Skirt,"clear memory restores default behaviors");
            check(CompanionBehaviorPreferences.Resolve(new CompanionMemory().Facts).Rice,"another shop does not inherit preferences");
        }
        finally{Directory.Delete(Path.GetDirectoryName(path),true);}
    }
}
