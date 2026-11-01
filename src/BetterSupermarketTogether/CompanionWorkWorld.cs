using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Newtonsoft.Json.Linq;
using StarterAssets;
using UnityEngine;
using UnityEngine.AI;

namespace BetterSupermarketTogether;

// Runtime target IDs are tied to object identity for this shop session, never sibling indices.
internal sealed class CompanionWorkWorld : ICompanionWorkWorld
{
    private sealed class Goal
    {
        internal Data_Container Shelf;
        internal int Pair, Product;
        internal CompanionRestockQuota Quota;
    }
    private readonly Dictionary<Data_Container,string> _ids = new Dictionary<Data_Container,string>();
    private readonly List<Goal> _goals = new List<Goal>();
    private NPC_Info _employee;
    private CompanionStep _step;
    private int _nextId=1, _expectedRole, _followPreviousRole;
    private bool _handoff, _following, _oldStopped, _externalOverride;
    private bool _externalEvent;
    internal bool ChangingNativeRole { get; private set; }
    private long _baseline;
    private float _lastProgress, _followBlocked, _nextFollow, _followProgress;
    private Vector3 _followPosition;
    private int _remaining;
    internal string SelectedShelf { get; private set; }
    internal NPC_Info Employee => _employee;
    private NPC_Manager Manager => NPC_Manager.Instance;
    private Transform Shelves => Manager?.shelvesOBJ != null ? Manager.shelvesOBJ.transform : null;
    internal void Bind(NPC_Info employee) { _employee=employee; }
    internal bool Targeted => _step?.Kind=="restock";
    internal static bool HasTarget(NPC_Info employee)
    {
        var world=BetterSupermarketPlugin.Instance?.Companion?.WorkWorld;
        return world?.Matches(employee)==true && world.Targeted;
    }
    internal static bool Accepts(NPC_Info employee,Data_Container shelf,int pair,int product)
    {
        var world=BetterSupermarketPlugin.Instance?.Companion?.WorkWorld;
        return world?.Matches(employee)!=true || world.Allows(shelf,pair,product);
    }
    internal bool Matches(NPC_Info employee) => employee!=null && employee==_employee;
    public bool Safe
    {
        get
        {
            if (_employee==null) return false;
            bool known=RestockAssignments.TryGetWaitTarget(_employee,out int wait);
            return CompanionToolPolicy.CanHandoff(_employee.taskPriority,_employee.state,_employee.equippedItem,
                _employee.playingCoroutine && known,wait);
        }
    }
    private string Id(Data_Container shelf)
    {
        if (!_ids.TryGetValue(shelf,out string id)) { id="S"+_nextId++;_ids.Add(shelf,id); }
        return id;
    }
    private Data_Container Find(string id) => _ids.FirstOrDefault(x=>x.Value==id && x.Key!=null && x.Key.transform.parent==Shelves).Key;
    private static string Name(int product)
    {
        string name=LocalizationManager.instance?.GetLocalizationString("product"+product);
        return string.IsNullOrWhiteSpace(name) || name=="LocError" ? "#"+product : CompanionProtocol.Limit(name,80);
    }
    private Dictionary<int,int> Stock()
    {
        var result=new Dictionary<int,int>();
        if (Manager?.storageOBJ==null) return result;
        foreach(Transform child in Manager.storageOBJ.transform)
        {
            var a=child.GetComponent<Data_Container>()?.productInfoArray;
            if(a==null)continue;
            for(int p=0;p+1<a.Length;p+=2)if(a[p]>=0 && a[p+1]>0)
                result[a[p]]=(result.TryGetValue(a[p],out int count)?count:0)+a[p+1];
        }
        return result;
    }
    private List<JObject> Rows(string query)
    {
        var rows=new List<JObject>();var root=Shelves;var stock=Stock();
        if(root==null)return rows;
        foreach(Transform child in root)
        {
            var shelf=child.GetComponent<Data_Container>();var a=shelf?.productInfoArray;
            if(a==null)continue;
            string id=Id(shelf);
            for(int p=0;p+1<a.Length;p+=2)
            {
                int product=a[p];if(product<0 || a[p+1]<0)continue;
                string name=Name(product);
                if(!string.IsNullOrWhiteSpace(query) && !id.Equals(query,StringComparison.OrdinalIgnoreCase) &&
                    product.ToString()!=query && name.IndexOf(query,StringComparison.OrdinalIgnoreCase)<0)continue;
                int capacity=Manager.GetMaxProductsPerRow(child.GetSiblingIndex(),product);
                var pos=child.position;
                rows.Add(new JObject { ["shelf_id"]=id,["row"]=p/2+1,["product_id"]=product,["name"]=name,
                    ["count"]=a[p+1],["capacity"]=capacity,["warehouse_units"]=stock.TryGetValue(product,out int count)?count:0,
                    ["position"]=new JArray(Math.Round(pos.x,1),Math.Round(pos.y,1),Math.Round(pos.z,1)) });
            }
        }
        return rows;
    }
    internal JObject Inspect(string query,int offset)
    {
        var rows=Rows(query);
        return new JObject { ["query"]=query ?? "",["offset"]=offset,["total"]=rows.Count,
            ["rows"]=new JArray(rows.Skip(offset).Take(20)),["has_more"]=offset+20<rows.Count,
            ["selected_shelf"]=SelectedShelf };
    }
    internal JObject Snapshot()
    {
        var rows=Rows(null);int customers=0,free=0;
        if(Manager?.checkoutOBJ!=null)foreach(Transform child in Manager.checkoutOBJ.transform)
        {
            var c=child.GetComponent<Data_Container>();if(c==null || c.isCheckoutClosed)continue;
            if(!c.isOccupiedByNPCCashier)free++;
            if(c.checkoutQueue!=null)customers+=c.checkoutQueue.Count(x=>x);
        }
        return new JObject { ["ordinary_shelf_rows"]=rows.Count,
            ["deficient_rows"]=rows.Count(x=>(int)x["capacity"]>(int)x["count"]),
            ["checkout_customers"]=customers,["unassigned_open_checkouts"]=free,
            ["ground_boxes"]=Manager?.boxesOBJ!=null?Manager.boxesOBJ.transform.childCount:0,
            ["broken_furniture"]=Manager?.brokenFurnitureList?.Count ?? 0,
            ["pending_orders"]=OrderPackaging.Instance?.ordersData?.Count(x=>!string.IsNullOrEmpty(x)) ?? 0,
            ["selected_shelf"]=SelectedShelf,
            ["selected_rows"]=new JArray(rows.Where(x=>(string)x["shelf_id"]==SelectedShelf).Take(20)),
            ["restock_candidates"]=new JArray(rows.Where(x=>(int)x["capacity"]>(int)x["count"])
                .OrderBy(x=>(double)(int)x["count"]/Math.Max(1,(int)x["capacity"])).Take(20)),
            ["catalog_hint"]="inspect_shop(query, offset) searches names, exact product IDs or shelf IDs; 20 rows/page. IDs expire when the shop reloads. Ordinary retail shelves only." };
    }
    internal ModText.Message PointAtShelf()
    {
        var player=FirstPersonController.Instance;
        var camera=player!=null?player.GetComponentInChildren<Camera>():null;
        if(camera==null)camera=Camera.main;
        if(camera==null || !Physics.Raycast(camera.transform.position,camera.transform.forward,out var hit,15f,~0,QueryTriggerInteraction.Ignore))
            return "準星沒有指向商品貨架。";
        var shelf=hit.collider.GetComponentInParent<Data_Container>();
        if(shelf==null || shelf.transform.parent!=Shelves)return "請指向一般商品貨架。";
        SelectedShelf=Id(shelf);
        return new ModText.Message("已選取貨架 {0} · {1}",SelectedShelf,string.Join(", ",Rows(SelectedShelf).Select(x=>(string)x["name"]).Distinct().Take(5)));
    }
    internal ModText.Message Validate(List<CompanionStep> steps)
    {
        Rows(null); // Register all current shelf identities before accepting any plan.
        foreach(var step in steps.Where(x=>x.Kind=="restock"))
        {
            if(step.Shelf!=null && Find(step.Shelf)==null)return new ModText.Message("貨架不存在或已移除：{0}",step.Shelf);
            if(!Rows(null).Any(x=>(int)x["capacity"]>0 && (step.Shelf==null || (string)x["shelf_id"]==step.Shelf) && (!step.Product.HasValue || (int)x["product_id"]==step.Product.Value)))
                return "找不到已標示指定商品的貨架。";
        }
        return null;
    }
    public ModText.Message Start(CompanionStep step)
    {
        if(_employee==null || Manager==null || !Manager.isServer)return "員工目前不可用。";
        _step=step;_handoff=false;_externalOverride=false;_goals.Clear();
        int role=step.Kind=="follow"?0:step.Kind=="restock"?2:Array.IndexOf(CompanionProtocol.Roles,step.Role);
        _followPreviousRole=_employee.taskPriority;
        if(step.Kind=="restock")
        {
            var error=Validate(new List<CompanionStep>{step});if(error!=null)return error;
            foreach(Transform child in Shelves)
            {
                var shelf=child.GetComponent<Data_Container>();if(shelf==null || step.Shelf!=null && Id(shelf)!=step.Shelf)continue;
                var a=shelf.productInfoArray;if(a==null)continue;
                for(int p=0;p+1<a.Length;p+=2)
                {
                    if(a[p]<0 || a[p+1]<0 || step.Product.HasValue && a[p]!=step.Product.Value)continue;
                    int capacity=Manager.GetMaxProductsPerRow(child.GetSiblingIndex(),a[p]);
                    if(capacity>a[p+1])_goals.Add(new Goal {Shelf=shelf,Pair=p,Product=a[p],Quota=new CompanionRestockQuota(a[p+1],capacity)});
                }
            }
            _remaining=_goals.Sum(x=>x.Quota.Needed);_lastProgress=Time.time;
            var stock=Stock();
            if(_goals.Count>0 && !_goals.Any(x=>stock.TryGetValue(x.Product,out int count) && count>0))
                return "指定目標沒有可用倉庫庫存。";
        }
        if(!SetRole(role))return "遊戲未接受職務變更。";
        _expectedRole=role;_baseline=Experience();
        if(step.Kind=="restock" || step.Kind=="follow")
        {
            _employee.StopCoroutinesFromManager();_employee.state=0;RestockAssignments.ForgetTask(_employee);
            var agent=_employee.GetComponent<NavMeshAgent>();
            if(agent!=null && agent.enabled && agent.isOnNavMesh)agent.ResetPath();
        }
        if(step.Kind=="follow")
        {
            var agent=_employee.GetComponent<NavMeshAgent>();
            if(agent==null || !agent.enabled || !agent.isOnNavMesh)return "員工不在可行走區域。";
            _oldStopped=agent.isStopped;_following=true;_followBlocked=0;_nextFollow=0;
            _followProgress=Time.time;_followPosition=_employee.transform.position;
        }
        return null;
    }
    internal bool SetRole(int role)
    {
        if(_employee==null || Manager==null || !Manager.isServer)return false;
        if(_employee.taskPriority==role)return true;
        var handler=AccessTools.Method(typeof(NPC_Manager),"UserCode_CmdChangeEmployeePriority__Int32__Int32");
        int index=BetterSupermarketPlugin.Instance?.Companion?.BoundIndex ?? -1;
        if(handler==null || index<0 || _employee.employeeDismissed || Manager.employeesArray==null || index>=Manager.employeesArray.Length ||
            Manager.employeesArray[index]==null || Manager.employeesArray[index].GetComponent<NPC_Info>()!=_employee)return false;
        ChangingNativeRole=true;
        try { handler.Invoke(Manager,new object[]{index,role}); }
        finally { ChangingNativeRole=false; }
        if(_employee.taskPriority==role)RestockAssignments.ForgetTask(_employee);
        return _employee.taskPriority==role;
    }
    private long Experience()
    {
        if(_employee==null)return 0;
        switch(_employee.taskPriority)
        {
            case 1:return _employee.cashierExperience;
            case 2:return _employee.restockerExperience;
            case 3:return _employee.storageExperience;
            case 4:return _employee.securityExperience;
            case 5:return _employee.technicianExperience;
            case 6:return _employee.orderingExperience;
            case 7:return _employee.manufacturingExperience;
            default:return 0;
        }
    }
    public CompanionProgress Poll(CompanionStep step,double elapsed,out ModText.Message detail)
    {
        detail=null;
        if(_employee==null || _employee.employeeDismissed){detail="員工目前不可用。";return CompanionProgress.Failed;}
        if(_employee.taskPriority!=_expectedRole){if(!_externalOverride)_externalEvent=true;_externalOverride=true;detail="職務已由其他介面更改。";return CompanionProgress.Failed;}
        if(step.Kind=="work")
        {
            if(step.Completion=="duration" && elapsed>=step.Seconds || step.Completion=="one_cycle" && Experience()>_baseline)return CompanionProgress.Complete;
            if(step.Completion=="one_cycle" && elapsed>900){detail="十五分鐘內沒有確認完成一輪工作。";return CompanionProgress.Failed;}
            return CompanionProgress.Running;
        }
        if(step.Kind=="follow")
        {
            if(step.Seconds>0 && elapsed>=step.Seconds)return CompanionProgress.Complete;
            if(!Follow()){detail="無法到達房主所在位置。";return CompanionProgress.Failed;}
            return CompanionProgress.Running;
        }
        foreach(var goal in _goals)
        {
            var a=goal.Shelf!=null?goal.Shelf.productInfoArray:null;
            if(goal.Shelf==null || goal.Shelf.transform.parent!=Shelves || a==null || goal.Pair+1>=a.Length || a[goal.Pair]!=goal.Product)
            {detail="目標貨架已移除或商品標籤改變。";return CompanionProgress.Failed;}
            int capacity=Manager.GetMaxProductsPerRow(goal.Shelf.transform.GetSiblingIndex(),goal.Product);
            if(capacity<=0 || a[goal.Pair+1]<0){detail="目標商品列已失效。";return CompanionProgress.Failed;}
            goal.Quota.Observe(a[goal.Pair+1],capacity);
        }
        int remaining=_goals.Sum(x=>x.Quota.Remaining);
        if(remaining<_remaining){_remaining=remaining;_lastProgress=Time.time;}
        if(remaining==0)return CompanionProgress.Complete;
        detail=new ModText.Message("尚需補貨：{0}",remaining);
        if(Time.time-_lastProgress>180 || elapsed>900)
        {detail="補貨停滯：請檢查庫存、路徑及占用。";return CompanionProgress.Failed;}
        return CompanionProgress.Running;
    }
    private bool Follow()
    {
        float now=Time.time;if(now<_nextFollow)return true;_nextFollow=now+.5f;
        var player=FirstPersonController.Instance;var agent=_employee.GetComponent<NavMeshAgent>();
        bool ready=player!=null && agent!=null && agent.enabled && agent.isOnNavMesh;
        if(ready)
        {
            var route=new EmployeeNavigation(_employee.gameObject);
            if(route.IsNear(player.transform.position,3.5f)){agent.isStopped=true;agent.ResetPath();_followBlocked=0;_followProgress=now;return true;}
            if(Vector3.SqrMagnitude(_employee.transform.position-_followPosition)>.04f){_followPosition=_employee.transform.position;_followProgress=now;}
            if(now-_followProgress>45)return false;
            Vector3 target=player.transform.position-player.transform.forward*2f;
            if(route.MoveTo(target) || route.MoveTo(player.transform.position)){_followBlocked=0;return true;}
        }
        if(_followBlocked==0)_followBlocked=now;
        return now-_followBlocked<30;
    }
    public void PrepareHandoff() { _handoff=true; }
    public void Release()
    {
        if(_following && _employee!=null)
        {
            var agent=_employee.GetComponent<NavMeshAgent>();
            if(agent!=null && agent.enabled && agent.isOnNavMesh)
            {
                if(_employee.taskPriority==_expectedRole && !_externalOverride)agent.ResetPath();
                agent.isStopped=_oldStopped;
            }
            if(!_externalOverride && _employee.taskPriority==0 && BetterSupermarketPlugin.IsHost)SetRole(_followPreviousRole);
        }
        _following=false;_step=null;_handoff=false;_goals.Clear();
    }
    public void Rest() { if(!_externalOverride && Safe)SetRole(0); }
    internal bool TakeExternalEvent() { bool value=_externalEvent;_externalEvent=false;return value; }
    internal void ExternalChange()
    {
        _externalOverride=true;_externalEvent=false;
        if(_following && _employee!=null)
        {
            var agent=_employee.GetComponent<NavMeshAgent>();
            if(agent!=null && agent.enabled && agent.isOnNavMesh){agent.ResetPath();agent.isStopped=_oldStopped;}
            _following=false;
        }
    }
    internal bool BeforeNative(NPC_Info employee)
    {
        if(!Matches(employee) || _step==null || employee.employeeDismissed || Manager.employeesOnRiot)return true;
        if(employee.taskPriority!=_expectedRole)
        {
            if(!_externalOverride)_externalEvent=true;_externalOverride=true;
            if(_following)
            {
                var agent=employee.GetComponent<NavMeshAgent>();
                if(agent!=null && agent.enabled && agent.isOnNavMesh)agent.isStopped=_oldStopped;
                _following=false;
            }
            return true;
        }
        return !(_following || _handoff && Safe);
    }
    internal bool Allows(Data_Container shelf,int pair,int product) => !Targeted || !_handoff && _goals.Any(x=>x.Shelf==shelf && x.Pair==pair && x.Product==product && !x.Quota.Done);
    internal void Delivered(Data_Container shelf,int pair,int quantity)
    {
        if(!Targeted || quantity<=0)return;
        var goal=_goals.FirstOrDefault(x=>x.Shelf==shelf && x.Pair==pair);
        if(goal!=null) {goal.Quota.Add(quantity);_lastProgress=Time.time;}
    }
    internal void ClearTargets() { _ids.Clear();_nextId=1;SelectedShelf=null; }
}

[HarmonyPatch(typeof(NPC_Manager),"UserCode_CmdChangeEmployeePriority__Int32__Int32")]
internal static class CompanionExternalPriority
{
    private static void Prefix(int __0) { BetterSupermarketPlugin.Instance?.Companion?.ExternalRoleChange(__0); }
}

[HarmonyPatch(typeof(NPC_Manager),"UserCode_CmdChangeAllEmployeePriorities__Int32")]
internal static class CompanionExternalAllPriorities
{
    private static void Prefix()
    {
        var companion=BetterSupermarketPlugin.Instance?.Companion;
        if(companion!=null)companion.ExternalRoleChange(companion.BoundIndex);
    }
}

[HarmonyPatch(typeof(NPC_Manager),"EmployeeNPCControl")]
internal static class CompanionEmployeeScope
{
    internal static NPC_Info Current;
    [HarmonyPriority(Priority.First)]
    private static bool Prefix(NPC_Manager __instance,int __0,out NPC_Info __state)
    {
        __state=Current;Current=null;
        if(!BetterSupermarketPlugin.EnablePlugin || !__instance.isServer || __instance.employeeParentOBJ==null || __0<0 || __0>=__instance.employeeParentOBJ.transform.childCount)return true;
        Current=__instance.employeeParentOBJ.transform.GetChild(__0).GetComponent<NPC_Info>();
        return BetterSupermarketPlugin.Instance?.Companion?.WorkWorld.BeforeNative(Current) ?? true;
    }
    private static void Postfix(NPC_Info __state) { Current=__state; }
    private static Exception Finalizer(Exception __exception,NPC_Info __state) { Current=__state;return __exception; }
}

[HarmonyPatch(typeof(Data_Container),"EmployeeAddsItemToRow")]
internal static class CompanionPlacementResult
{
    private static void Postfix(Data_Container __instance,int rowIndex,int quantity)
    {
        var world=BetterSupermarketPlugin.Instance?.Companion?.WorkWorld;
        if(world?.Matches(CompanionEmployeeScope.Current)==true)world.Delivered(__instance,rowIndex,quantity);
    }
}
