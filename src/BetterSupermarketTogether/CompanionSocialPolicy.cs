using System;
namespace BetterSupermarketTogether;
internal sealed class CompanionSocialPolicy
{
    private bool _observed,_near,_confirmed;
    private double _last,_busy,_idleSince=-1,_lastWork,_nodAt,_relaxAt;
    private long _experience;
    internal void Observe(double now,bool idle,bool working,long experience)
    {
        if(!_observed){_observed=true;_last=now;_experience=experience;return;}
        double delta=Math.Max(0,Math.Min(1,now-_last));_last=now;
        if(experience>_experience){_confirmed=true;_lastWork=now;}
        _experience=experience;
        if(working){_busy+=delta;_lastWork=now;}
        if(idle){if(_idleSince<0)_idleSince=now;}else _idleSince=-1;
        if(now-_lastWork>120){_busy=0;_confirmed=false;}
    }
    internal bool Encounter(double now,bool near,bool allowed)
    {
        bool entered=near && !_near;_near=near;
        if(!entered || !allowed || now<_nodAt)return false;
        _nodAt=now+90;return true;
    }
    internal bool Relax(double now,bool allowed)
    {
        if(!allowed || !_confirmed || _busy<30 || _idleSince<0 || now-_idleSince<3 || now<_relaxAt)return false;
        _busy=0;_confirmed=false;_relaxAt=now+300;return true;
    }
}
