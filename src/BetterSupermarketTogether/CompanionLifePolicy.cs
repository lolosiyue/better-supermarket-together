using System;
namespace BetterSupermarketTogether;
internal sealed class CompanionLifePolicy
{
    private double _idleSince=-1,_helpAt,_gestureAt,_companyAt;
    private bool _initialized;
    internal void Reset(){_initialized=false;_idleSince=-1;_helpAt=_gestureAt=_companyAt=0;}
    internal void Observe(double now,bool idle)
    {
        if(!_initialized){_initialized=true;_helpAt=now+30;_gestureAt=now+60;_companyAt=now+20;}
        if(!idle)_idleSince=-1;
        else if(_idleSince<0)_idleSince=now;
    }
    internal bool HelpDue(double now)=>_idleSince>=0 && now-_idleSince>=15 && now>=_helpAt;
    internal bool GestureDue(double now)=>_idleSince>=0 && now-_idleSince>=10 && now>=_gestureAt;
    internal void HelpAttempt(double now){_helpAt=now+180;}
    internal bool CompanyDue(double now)=>_idleSince>=0 && now-_idleSince>=5 && now>=_companyAt;
    internal void CompanyAttempt(double now){_companyAt=now+20;}
    internal void GestureAttempt(double now,double random,double scale=1){_gestureAt=now+(60+60*Math.Max(0,Math.Min(1,random)))*Math.Max(1,Math.Min(3,scale));}
    internal void Defer(double now){_idleSince=-1;_helpAt=Math.Max(_helpAt,now+180);_gestureAt=Math.Max(_gestureAt,now+60);_companyAt=Math.Max(_companyAt,now+60);}
    internal static bool Idle(int role,int state,int held,bool playing,bool known,int wait,bool moving)
    {
        if(held!=0 || moving || role<0 || role>6 || role==4)return false;
        if(state==-1)return playing && known && wait==0;
        if(playing)return false;
        return role==0 ? state==0 || state==1 : role==2 ? state==0 : state==10;
    }
}
