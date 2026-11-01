using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;

namespace BetterSupermarketTogether;

// A host-side, idle-only break. It owns navigation briefly, never the employee's job.
internal sealed class CompanionRiceBreak
{
    private sealed class Choice
    {
        internal Data_Container Shelf;
        internal int Product;
        internal Vector3 Destination;
    }
    private readonly CompanionAvatar _avatar;
    private readonly System.Random _random=new System.Random();
    private CompanionRicePolicy _gate=new CompanionRicePolicy();
    private NPC_Info _employee;
    private NavMeshAgent _agent;
    private EmployeeNavigation _navigation;
    private Choice _choice;
    private bool _oldStopped,_eating;
    private float _started;
    internal bool Active => _employee!=null;
    internal bool Owns(NPC_Info employee) => Active && employee==_employee;
    internal CompanionRiceBreak(CompanionAvatar avatar) { _avatar=avatar; }

    internal ModText.Message Tick(NPC_Info employee,bool eligible)
    {
        float now=Time.time;int day=GameData.Instance.gameDay;
        _gate.Initialize(now,_random.NextDouble());
        if(Active)
        {
            if(!eligible || employee!=_employee || !Idle(employee,true) || !_avatar.Ready || !Valid(_choice)) { Abort();return null; }
            if(_eating)
            {
                if(now-_started>=8)Abort();
                return null;
            }
            if(now-_started>45 || !_navigation.Ready) { Abort();return null; }
            if(!_navigation.IsAt(_choice.Destination))return null;
            _agent.isStopped=true;_agent.ResetPath();
            if(!_avatar.BeginRicePose(_choice.Product)) { Abort();return null; }
            bool ate=CompanionRicePolicy.ConsumeOne(()=>Count(_choice),()=>_choice.Shelf.NPCGetsItemFromRow(_choice.Product));
            if(!ate) { Abort();return null; }
            _gate.Ate(now,day,_random.NextDouble());_eating=true;_started=now;
            string name=LocalizationManager.instance?.GetLocalizationString("product"+_choice.Product);
            if(string.IsNullOrEmpty(name) || name=="LocError")name="#"+_choice.Product;
            return new ModText.Message("（彩蛋）DeepSeek 娘偷偷吃了一份{0}，貨架庫存 -1。",name);
        }
        if(!eligible || !_gate.Due(now,day))return null;
        _gate.RetryLater(now);
        if(!Idle(employee) || !_avatar.Ready)return null;
        var navigation=new EmployeeNavigation(employee.gameObject);
        if(!navigation.Ready)return null;
        var choices=FindChoices(navigation);
        if(choices.Count==0)return null;
        // Uniform by stocked rice variety, then pick the closest reachable shelf of that variety.
        var products=choices.Select(x=>x.Product).Distinct().ToArray();
        int product=products[_random.Next(products.Length)];
        var choice=choices.Where(x=>x.Product==product).OrderBy(x=>Vector3.SqrMagnitude(x.Destination-employee.transform.position)).First();
        _employee=employee;_choice=choice;_navigation=navigation;_agent=employee.GetComponent<NavMeshAgent>();
        _oldStopped=_agent.isStopped;_started=now;
        // Only a known rest-state wait can reach this point.
        if(employee.playingCoroutine)employee.StopCoroutinesFromManager();
        employee.state=0;
        if(!_navigation.MoveTo(choice.Destination))Abort();
        return null;
    }
    private static bool Idle(NPC_Info employee,bool ownsNavigation=false)
    {
        if(employee==null || !employee.isEmployee || employee.employeeDismissed || employee.taskPriority!=0 || employee.equippedItem!=0)return false;
        bool known=RestockAssignments.TryGetWaitTarget(employee,out int wait);
        var agent=employee.GetComponent<NavMeshAgent>();
        bool moving=!ownsNavigation && (agent==null || !agent.enabled || !agent.isOnNavMesh || agent.pathPending ||
            agent.velocity.sqrMagnitude>.01f || agent.hasPath && agent.remainingDistance>agent.stoppingDistance+.1f);
        return CompanionRicePolicy.Idle(employee.taskPriority,employee.state,employee.equippedItem,employee.playingCoroutine,known,wait,moving);
    }
    private static List<Choice> FindChoices(EmployeeNavigation navigation)
    {
        var result=new List<Choice>();var manager=NPC_Manager.Instance;var catalog=ProductListing.Instance?.productsData;
        if(manager?.shelvesOBJ==null || catalog==null)return result;
        foreach(Transform child in manager.shelvesOBJ.transform)
        {
            var shelf=child.GetComponent<Data_Container>();
            if(shelf==null || !shelf.isServer || shelf.productInfoArray==null || shelf.standSpot==null)continue;
            Vector3 destination=shelf.standSpot.transform.position;
            for(int row=0;row+1<shelf.productInfoArray.Length;row+=2)
            {
                int id=shelf.productInfoArray[row];
                if(id<0 || id>=catalog.Length || catalog[id]?.productPrefab==null || shelf.productInfoArray[row+1]<=0 ||
                    !CompanionRicePolicy.IsRice(id,catalog[id].productPrefab.name) || !navigation.CanReach(destination))continue;
                result.Add(new Choice {Shelf=shelf,Product=id,Destination=destination});
            }
        }
        return result;
    }
    private static bool Valid(Choice choice) => choice?.Shelf!=null && choice.Shelf.isServer && NPC_Manager.Instance?.shelvesOBJ!=null &&
        choice.Shelf.transform.parent==NPC_Manager.Instance.shelvesOBJ.transform && choice.Shelf.standSpot!=null &&
        Vector3.SqrMagnitude(choice.Shelf.standSpot.transform.position-choice.Destination)<.01f;
    private static int Count(Choice choice)
    {
        if(!Valid(choice))return 0;
        int count=0;var rows=choice.Shelf.productInfoArray;
        if(rows!=null)for(int i=0;i+1<rows.Length;i+=2)if(rows[i]==choice.Product)count+=Math.Max(0,rows[i+1]);
        return count;
    }
    internal void Abort()
    {
        _avatar.EndRicePose();
        if(_agent!=null && _agent.enabled && _agent.isOnNavMesh)
        {
            _agent.ResetPath();_agent.isStopped=_oldStopped;
        }
        _employee=null;_agent=null;_navigation=null;_choice=null;_eating=false;
    }
    internal void Reset() { Abort();_gate=new CompanionRicePolicy(); }
}
