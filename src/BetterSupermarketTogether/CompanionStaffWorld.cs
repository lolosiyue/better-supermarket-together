using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Newtonsoft.Json.Linq;

namespace BetterSupermarketTogether;

internal sealed class CompanionStaffWorld : ICompanionStaffWorld
{
    private sealed class Entry
    {
        internal string Id;
        internal int Index;
        internal NPC_Info Employee;
    }
    private readonly Dictionary<string,Entry> _entries=new Dictionary<string,Entry>();
    private int _nextId=1;
    internal readonly CompanionStaffQueue Queue=new CompanionStaffQueue();
    private NPC_Manager Manager => NPC_Manager.Instance;
    private DeepSeekCompanion Companion => BetterSupermarketPlugin.Instance?.Companion;
    internal bool ChangingNativeRole { get; private set; }
    public bool Authorized => BetterSupermarketPlugin.EnablePlugin && BetterSupermarketPlugin.IsHost &&
        Manager!=null && Manager.isServer && Manager.initialEmployeesSpawnIsFinished && !Manager.employeesOnRiot && Companion?.BoundEmployee!=null;
    public bool CanRestore => BetterSupermarketPlugin.IsHost && Manager!=null && Manager.isServer && Manager.initialEmployeesSpawnIsFinished && !Manager.employeesOnRiot;

    private bool Valid(Entry entry) => entry.Employee!=null && entry.Employee.isEmployee && !entry.Employee.employeeDismissed &&
        Manager?.employeesArray!=null && entry.Index>=0 && entry.Index<Manager.employeesArray.Length &&
        Manager.employeesArray[entry.Index]!=null && Manager.employeesArray[entry.Index].GetComponent<NPC_Info>()==entry.Employee;
    private static bool Safe(NPC_Info employee)
    {
        bool known=RestockAssignments.TryGetWaitTarget(employee,out int wait);
        return CompanionToolPolicy.CanHandoff(employee.taskPriority,employee.state,employee.equippedItem,
            employee.playingCoroutine && known,wait);
    }
    private Entry Register(int index,NPC_Info employee)
    {
        var existing=_entries.Values.FirstOrDefault(x=>x.Index==index && x.Employee==employee);
        if(existing!=null)return existing;
        var entry=new Entry { Id="E"+_nextId++,Index=index,Employee=employee };
        _entries.Add(entry.Id,entry);return entry;
    }
    internal JObject Inspect(string query,int offset)
    {
        var rows=new List<JObject>();
        // Drop stale objects without reusing their session IDs.
        foreach(var e in _entries.Values.Where(x=>!Valid(x) && !Queue.Contains(x.Id)).ToArray())_entries.Remove(e.Id);
        if(Manager?.employeesArray!=null)
        for(int i=0;i<Manager.employeesArray.Length;i++)
        {
            var obj=Manager.employeesArray[i];var employee=obj!=null?obj.GetComponent<NPC_Info>():null;
            if(employee==null || !employee.isEmployee || employee.employeeDismissed)continue;
            var entry=Register(i,employee);
            if(!string.IsNullOrWhiteSpace(query) && query!=entry.Id && query!=(i+1).ToString() &&
                (employee.NPCName??"").IndexOf(query,StringComparison.OrdinalIgnoreCase)<0)continue;
            rows.Add(new JObject { ["staff_id"]=entry.Id,["employee_number"]=i+1,["name"]=CompanionProtocol.Limit(employee.NPCName,80),
                ["role"]=CompanionProtocol.Roles[Math.Max(0,Math.Min(7,employee.taskPriority))],
                ["bound_companion"]=employee==Companion?.BoundEmployee,["state"]=employee.state,["equipped_item"]=employee.equippedItem,
                ["safe_to_reassign"]=Safe(employee),["pending_role"]=Queue.DesiredRole(entry.Id),["dispatch_phase"]=Queue.Phase(entry.Id) });
        }
        return new JObject { ["total"]=rows.Count,["offset"]=offset,["employees"]=new JArray(rows.Skip(offset).Take(20)),
            ["has_more"]=offset+20<rows.Count,["pending_assignments"]=Queue.WaitingCount,["active_temporary_assignments"]=Queue.Count-Queue.WaitingCount,["recent_dispatch_results"]=new JArray(Queue.Recent.Select(x=>x.WireText)),
            ["hint"]="employee_number is the same 1-based number shown in the employee UI. Use staff_id from the latest roster for assign_staff. Exclude bound_companion; duplicate names require an employee number. Only hired staff, never customers. Role assignment does not confirm completion of work." };
    }
    public bool Available(string id,out int role,out bool safe,out string label)
    {
        role=0;safe=false;label=id;
        if(!_entries.TryGetValue(id,out var entry) || !Valid(entry) || entry.Employee==Companion?.BoundEmployee)return false;
        role=entry.Employee.taskPriority;safe=Safe(entry.Employee);
        label=(entry.Index+1)+" · "+CompanionProtocol.Limit(entry.Employee.NPCName,80);return true;
    }
    public bool Assign(string id,int role,bool restoring=false)
    {
        if(role<0 || role>=CompanionProtocol.Roles.Length || !(restoring?CanRestore:Authorized) || !Available(id,out int current,out bool safe,out _) || !safe)return false;
        var entry=_entries[id];
        if(Manager.priorityArray==null || entry.Index>=Manager.priorityArray.Length)return false;
        if(current==role)return true;
        var handler=AccessTools.Method(typeof(NPC_Manager),"UserCode_CmdChangeEmployeePriority__Int32__Int32");
        if(handler==null)return false;
        ChangingNativeRole=true;
        try { handler.Invoke(Manager,new object[] {entry.Index,role}); }
        finally { ChangingNativeRole=false; }
        bool confirmed=Valid(entry) && entry.Employee.taskPriority==role && Manager.priorityArray[entry.Index]==role;
        if(confirmed)RestockAssignments.ForgetTask(entry.Employee);
        return confirmed;
    }
    internal bool BeforeNative(NPC_Info employee)
    {
        if(!CanRestore || employee==null || employee==Companion?.BoundEmployee)return true;
        return !_entries.Values.Any(x=>x.Employee==employee && Valid(x) && Queue.Hold(x.Id) && Safe(employee));
    }
    public long Progress(string id,int role)
    {
        if(!_entries.TryGetValue(id,out var entry) || !Valid(entry))return 0;
        var employee=entry.Employee;
        switch(role)
        {
            case 1:return employee.cashierExperience;
            case 2:return employee.restockerExperience;
            case 3:return employee.storageExperience;
            case 4:return employee.securityExperience;
            case 5:return employee.technicianExperience;
            case 6:return employee.orderingExperience;
            case 7:return employee.manufacturingExperience;
            default:return 0;
        }
    }
    internal void ExternalChange(int index)
    {
        if(ChangingNativeRole)return;
        foreach(var entry in _entries.Values.Where(x=>index<0 || x.Index==index))Queue.Cancel(entry.Id);
    }
    internal void Reset() { Queue.Reset();_entries.Clear(); /* IDs never recycle during this plugin lifetime. */ }
}
