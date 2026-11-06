using BetterSupermarketTogether;

internal static class FaceChecks
{
    internal static void Run(Action<bool,string> check)
    {
        const double dt=1/60.0;
        var face=new CompanionFacePolicy(new Random(7));
        int blinks=0;double peak=0,longest=0,run=0;bool closed=false;
        for(double t=0;t<60;t+=dt)
        {
            double blink=face.Update(t,dt)["blink"];peak=Math.Max(peak,blink);
            if(blink>0)run+=dt;else{longest=Math.Max(longest,run);run=0;}
            if(blink>.99 && !closed){blinks++;closed=true;}if(blink<.5)closed=false;
        }
        check(blinks>=10 && blinks<=36 && peak>.99,"she blinks naturally every few seconds: "+blinks+" in a minute");
        check(longest>.15 && longest<.4,"a blink is quick, about a fifth of a second");
        var tired=new CompanionFacePolicy(new Random(7)){Tired=true};
        double tiredLongest=0;run=0;
        for(double t=0;t<60;t+=dt){if(tired.Update(t,dt)["blink"]>0)run+=dt;else{tiredLongest=Math.Max(tiredLongest,run);run=0;}}
        check(tiredLongest>longest*1.3,"tired blinks are slower");

        face=new CompanionFacePolicy(new Random(1));
        face.SetBase("happy",.35);
        IReadOnlyDictionary<string,double> w=null!;
        for(double t=0;t<1;t+=dt)w=face.Update(t,dt);
        check(Math.Abs(w["happy"]-.35)<1e-6 && w["sad"]==0,"resting face follows mood");
        face.Express(1,"sad",.6,2);
        for(double t=1;t<1.5;t+=dt)w=face.Update(t,dt);
        check(Math.Abs(w["sad"]-.6)<1e-6 && w["happy"]==0,"a reaction replaces the resting face quickly");
        for(double t=1.5;t<4;t+=dt)w=face.Update(t,dt);
        check(w["sad"]==0 && Math.Abs(w["happy"]-.35)<1e-6,"after the reaction she returns to her resting face");
        face.Express(4,"HAPPY",1,5);face.Express(4,"unknown",1,5);
        bool blinkSeen=false;
        for(double t=4;t<9;t+=dt){w=face.Update(t,dt);if(t>4.5 && w["blink"]>0)blinkSeen=true;}
        check(!blinkSeen,"a full smile (closed happy eyes) suppresses blinking");

        face=new CompanionFacePolicy(new Random(3));
        face.SetBase("happy",1);
        double open=0,maxEmotion=0;
        face.Speak(0,2);
        for(double t=0;t<2;t+=dt){w=face.Update(t,dt);open=Math.Max(open,CompanionFacePolicy.Visemes.Max(v=>w[v]));if(t>1)maxEmotion=Math.Max(maxEmotion,w["happy"]);}
        check(open>.2 && maxEmotion<=CompanionFacePolicy.TalkingEmotionCap+1e-9 && face.Talking(1.9),"talking moves the mouth and softens the emotion");
        var shapes=new HashSet<string>();face.Speak(2,3);
        for(double t=2;t<5;t+=dt){w=face.Update(t,dt);foreach(var v in CompanionFacePolicy.Visemes)if(w[v]>.3)shapes.Add(v);}
        check(shapes.Count>=3,"speech cycles through several mouth shapes");
        for(double t=5;t<5.5;t+=dt)w=face.Update(t,dt);
        check(!face.Talking(5.5) && CompanionFacePolicy.Visemes.All(v=>w[v]==0),"the mouth closes when she stops talking");
        face.Reset();w=face.Update(6,dt);
        check(w["happy"]==0 && CompanionFacePolicy.Visemes.All(v=>w[v]==0),"reset returns to a neutral face");

        check(CompanionChatPolicy.TakeEmotion("[happy] 補好了啦",out var e)=="補好了啦" && e=="happy","leading expression tag sets the face and is hidden");
        check(CompanionChatPolicy.TakeEmotion("［SAD］唉\n[sad]真的",out e)=="唉\n真的" && e=="sad","full-width and repeated tags are removed");
        check(CompanionChatPolicy.TakeEmotion("[neutral] ok",out e)=="ok" && e==null,"neutral tag means no expression");
        check(CompanionChatPolicy.TakeEmotion("好 [test] 喔",out e)=="好 [test] 喔" && e==null && CompanionChatPolicy.TakeEmotion(null,out e)=="","unknown brackets are kept and null is safe");
    }
}
