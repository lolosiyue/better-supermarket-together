using System;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;
using StarterAssets;

namespace BetterSupermarketTogether;

internal sealed class CompanionLife
{
    private NPC_Info _employee;
    private string _settings;
    private CompanionSocialPolicy _social=new CompanionSocialPolicy();
    private readonly CompanionLifePolicy _gate=new CompanionLifePolicy();
    private readonly System.Random _random=new System.Random();
    internal void Reset() { _employee=null;_settings=null;_social=new CompanionSocialPolicy();_gate.Reset(); }
    internal void Defer() => _gate.Defer(Time.time);
    internal CompanionStep Tick(NPC_Info employee, bool eligible, CompanionBehaviorPreferences preferences, CompanionWorkWorld world, CompanionAvatar avatar)
    {
        if(employee!=_employee){_employee=employee;_settings=null;_social=new CompanionSocialPolicy();_gate.Reset();avatar.StopIdlePose();}
        bool idle=eligible && Idle(employee);
        _gate.Observe(Time.time,idle);
        if(_settings!=preferences.Signature)
        {
            if(_settings!=null || preferences.IntervalScale>1){avatar.StopIdlePose();_gate.GestureAttempt(Time.time,_random.NextDouble(),preferences.IntervalScale);}
            _settings=preferences.Signature;
        }
        bool help=preferences.Help;var kinds=preferences.Gestures;bool gestures=kinds.Length>0;
        bool working=employee!=null && (employee.equippedItem!=0 || employee.state>1 && employee.state!=10);
        _social.Observe(Time.time,idle,working,Experience(employee));
        var player=FirstPersonController.Instance;
        bool near=employee!=null && player!=null && (player.transform.position-employee.transform.position).sqrMagnitude<12.25f;
        bool nodAllowed=eligible && employee!=null && employee.equippedItem==0 && employee.taskPriority!=4 && !employee.employeeDismissed &&
            preferences.Nod && preferences.Frequency!="關" && avatar.CanIdlePose;
        if(nodAllowed && near)
        {
            Vector3 from=employee.transform.position+Vector3.up*1.4f,to=player.transform.position+Vector3.up*1.4f;
            nodAllowed=Vector3.Dot(employee.transform.forward,to-from)>0 && new EmployeeNavigation(employee.gameObject).IsNear(player.transform.position,3.5f) &&
                (!Physics.Linecast(from,to,out var hit,~0,QueryTriggerInteraction.Ignore) || hit.transform.IsChildOf(player.transform));
        }
        bool nod=_social.Encounter(Time.time,near,nodAllowed);
        if(!idle)
        {
            if(!eligible)avatar.StopIdlePose();
            if(nod)avatar.BeginIdlePose(3,player.transform.position+Vector3.up*1.4f);
            return null;
        }
        if(nod)avatar.BeginIdlePose(3,player.transform.position+Vector3.up*1.4f);
        if(_social.Relax(Time.time,preferences.Relax && preferences.Frequency!="關" && avatar.CanIdlePose))avatar.BeginIdlePose(4);
        if(help && employee.taskPriority!=2 && _gate.HelpDue(Time.time))
        {
            _gate.HelpAttempt(Time.time);
            var step=world.FindHelpingTask();
            if(step!=null){avatar.StopIdlePose();return step;}
        }
        if(gestures && avatar.Ready && _gate.GestureDue(Time.time))
        {
            _gate.GestureAttempt(Time.time,_random.NextDouble(),preferences.IntervalScale);
            if(_random.NextDouble()<.65)avatar.BeginIdlePose(kinds[_random.Next(kinds.Length)]);
        }
        if(preferences.Frequency=="關")avatar.StopIdlePose();
        return null;
    }
    private static long Experience(NPC_Info e)=>e==null?0:(long)e.cashierExperience+e.restockerExperience+e.storageExperience+
        e.securityExperience+e.technicianExperience+e.orderingExperience+e.manufacturingExperience;
    internal static bool Idle(NPC_Info employee)
    {
        var manager=NPC_Manager.Instance;
        if(employee==null || manager==null || !employee.isEmployee || employee.employeeDismissed || manager.employeesOnRiot)return false;
        var agent=employee.GetComponent<NavMeshAgent>();
        bool moving=agent==null || !agent.enabled || !agent.isOnNavMesh || agent.pathPending || agent.velocity.sqrMagnitude>.01f ||
            agent.hasPath && agent.remainingDistance>agent.stoppingDistance+.1f;
        bool known=RestockAssignments.TryGetWaitTarget(employee,out int wait);
        if(!CompanionLifePolicy.Idle(employee.taskPriority,employee.state,employee.equippedItem,employee.playingCoroutine,known,wait,moving))return false;
        // Conservative checks: never borrow a cashier assigned to a till or a busy specialist.
        switch(employee.taskPriority)
        {
            case 1:
                if(employee.employeeAssignedCheckoutIndex>=0 || manager.checkoutOBJ==null)return false;
                foreach(Transform child in manager.checkoutOBJ.transform)
                {var c=child.GetComponent<Data_Container>();if(c!=null && !c.isCheckoutClosed && !c.isOccupiedByNPCCashier)return false;}
                return true;
            case 3: return manager.boxesOBJ!=null && manager.boxesOBJ.transform.childCount==0;
            case 5:
                var props=GameData.Instance?.GetComponent<NetworkSpawner>()?.levelPropsOBJ;
                return (manager.brokenFurnitureList?.Count??0)==0 && props!=null && props.transform.childCount>9 && props.transform.GetChild(9).childCount==0;
            case 6: return OrderPackaging.Instance==null || OrderPackaging.Instance.ordersData==null || !OrderPackaging.Instance.ordersData.Any(x=>!string.IsNullOrEmpty(x));
            default:return true;
        }
    }
}
