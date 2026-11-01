using System.Numerics;
using BetterSupermarketTogether;

internal static class MotionChecks
{
    internal static void Run(Action<bool,string> check)
    {
        var root=Vector3.Zero; var joint=new Vector3(.6f,.8f,0); var end=new Vector3(1.2f,0,0);
        var goal=new Vector3(.8f,.2f,.3f);
        bool ok=CompanionMotionMath.Solve(root,joint,end,goal,out var bend,out var tip);
        check(ok && Vector3.Distance(tip,goal)<1e-5f,"reachable grip reaches native hand target");
        check(MathF.Abs(Vector3.Distance(root,bend)-1)<1e-5f && MathF.Abs(Vector3.Distance(bend,tip)-1)<1e-5f,"grip correction preserves both bone lengths");
        CompanionMotionMath.Solve(root,joint,end,new Vector3(8,0,0),out bend,out tip);
        check(tip.X<2 && tip.X>1.99f && bend.Y>=0,"unreachable grip clamps without stretching or elbow reversal");
        check(!CompanionMotionMath.Solve(root,root,end,goal,out _,out _),"zero-length bones safely skip IK");
        check(!CompanionMotionMath.Solve(root,joint,end,root,out _,out _),"goal at limb root safely skips IK");
        var random=new Random(31); bool lengths=true,finite=true;
        for(int i=0;i<1000;i++)
        {
            goal=new Vector3((float)random.NextDouble()*6-3,(float)random.NextDouble()*6-3,(float)random.NextDouble()*6-3);
            CompanionMotionMath.Solve(root,joint,end,goal,out bend,out tip);
            lengths &= MathF.Abs(bend.Length()-1)<1e-4f && MathF.Abs(Vector3.Distance(bend,tip)-1)<1e-4f;
            finite &= float.IsFinite(bend.X+bend.Y+bend.Z+tip.X+tip.Y+tip.Z);
        }
        check(lengths && finite,"1000 varied foot/grip goals remain finite and preserve limb lengths");
        CompanionMotionMath.Solve(root,Vector3.UnitY,Vector3.UnitY*2,new Vector3(0,1.5f,0),out bend,out tip);
        check(float.IsFinite(bend.X+bend.Y+bend.Z) && Vector3.Distance(tip,new Vector3(0,1.5f,0))<1e-5f,"straight limb has stable fallback bend plane");
    }
}
