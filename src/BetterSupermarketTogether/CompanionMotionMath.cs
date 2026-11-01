using System;
using System.Numerics;

namespace BetterSupermarketTogether;

// Engine-independent two-bone solve: preserve segment lengths and the animated bend plane.
internal static class CompanionMotionMath
{
    internal static bool Solve(Vector3 root, Vector3 joint, Vector3 end, Vector3 goal,
        out Vector3 bend, out Vector3 tip)
    {
        bend=joint; tip=end;
        float a=Vector3.Distance(root,joint), b=Vector3.Distance(joint,end);
        Vector3 delta=goal-root;
        if(a<1e-5f || b<1e-5f || delta.LengthSquared()<1e-10f) return false;
        Vector3 axis=Vector3.Normalize(delta);
        float distance=Math.Max(Math.Abs(a-b)+1e-5f,Math.Min(a+b-1e-5f,delta.Length()));
        Vector3 pole=joint-root-axis*Vector3.Dot(joint-root,axis);
        if(pole.LengthSquared()<1e-10f)
        {
            Vector3 fallback=Math.Abs(axis.Y)<.9f?Vector3.UnitY:Vector3.UnitZ;
            pole=fallback-axis*Vector3.Dot(fallback,axis);
        }
        float along=(a*a-b*b+distance*distance)/(2*distance);
        bend=root+axis*along+Vector3.Normalize(pole)*(float)Math.Sqrt(Math.Max(0,a*a-along*along));
        tip=root+axis*distance;
        return true;
    }
}
