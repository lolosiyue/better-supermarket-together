using System;

namespace BetterSupermarketTogether;

internal sealed class CompanionRicePolicy
{
    private bool _scheduled;
    private double _next;
    private int _lastDay=int.MinValue;
    internal void Initialize(double now,double random)
    {
        if(!_scheduled)Schedule(now,random);
    }
    private void Schedule(double now,double random)
    {
        _scheduled=true;_next=now+1200+1200*Math.Max(0,Math.Min(1,random));
    }
    internal bool Due(double now,int day) => _scheduled && now>=_next && day!=_lastDay;
    internal void RetryLater(double now) { _next=Math.Max(_next,now+120); }
    internal void Ate(double now,int day,double random) { _lastDay=day;Schedule(now,random); }
    internal static bool Idle(int role,int state,int equipped,bool playing,bool knownWait,int waitTarget,bool moving) =>
        role==0 && equipped==0 && !moving && ((state==0 || state==1) && !playing || state==-1 && playing && knownWait && waitTarget==0);
    internal static bool IsRice(int product,string prefabName)
    {
        string name=(prefabName??"").Replace("(Clone)","").Trim();
        return product==3 && name=="3_Rice" || product==33 && name=="33_RiceBasmati" || product==34 && name=="34_RiceLongGrain";
    }
    // Count changes are authoritative even if an RPC throws after the server decrement.
    internal static bool ConsumeOne(Func<int> count,Action remove)
    {
        int before=count();if(before<=0)return false;
        try { remove(); } catch(Exception) { }
        return count()==before-1;
    }
}
