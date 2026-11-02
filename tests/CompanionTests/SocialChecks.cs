using BetterSupermarketTogether;
internal static class SocialChecks
{
    internal static void Run(Action<bool,string> check)
    {
        var social=new CompanionSocialPolicy();
        check(social.Encounter(0,true,true),"nearby encounter permits one nod");
        check(!social.Encounter(100,true,true),"standing nearby never repeats greeting");
        social.Encounter(101,false,true);
        check(social.Encounter(102,true,true),"new encounter after cooldown can greet");
        social.Encounter(103,false,true);
        check(!social.Encounter(104,true,true),"rapid crossings respect greeting cooldown");
        social.Encounter(200,false,true);
        check(!social.Encounter(201,true,false),"blocked sight or busy context does not greet");
        check(!social.Encounter(202,true,true),"missed greeting is not queued to interrupt later");
        social.Observe(0,false,true,0);
        for(int i=1;i<=35;i++)social.Observe(i,false,true,0);
        social.Observe(36,true,false,0);social.Observe(40,true,false,0);
        check(!social.Relax(40,true),"busy state without confirmed work earns no completion gesture");
        social.Observe(41,false,true,1);social.Observe(42,true,false,1);
        check(!social.Relax(44,true) && social.Relax(45,true),"actual work plus idle settling permits relaxation");
        check(!social.Relax(46,true),"one work bout produces only one relaxation");
        var shortWork=new CompanionSocialPolicy();shortWork.Observe(0,false,true,0);shortWork.Observe(1,false,true,1);
        shortWork.Observe(2,true,false,1);shortWork.Observe(10,true,false,1);
        check(!shortWork.Relax(10,true),"one brief job does not trigger busy-work relaxation");
        var p=CompanionBehaviorPreferences.Resolve(new[]{"行為偏好｜路過點頭：關","行為偏好｜忙完放鬆：關","行為偏好｜固定休息位置：關"});
        check(!p.Nod && !p.Relax && !p.RestCorner,"new social behaviors support explicit preferences");
        string folder=Path.Combine(Path.GetTempPath(),"bst-rest-"+Guid.NewGuid());string file=Path.Combine(folder,"memory.json");
        try
        {
            var m=new CompanionMemory{RestCorner=new[]{1f,2f,3f}};m.Save(file);
            check(CompanionMemory.Load(file).RestCorner.SequenceEqual(m.RestCorner),"rest position persists per shop");
            m.RestCorner=new[]{float.NaN,2f,3f};m.Save(file);
            check(CompanionMemory.Load(file).RestCorner==null,"invalid rest coordinates fall back without rejecting ordinary memories");
            check(new CompanionMemory().RestCorner==null,"new shop has no inherited rest position");
        }
        finally{Directory.Delete(folder,true);}
    }
}
