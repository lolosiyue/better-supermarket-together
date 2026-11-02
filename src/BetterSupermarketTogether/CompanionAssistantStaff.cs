using System.Collections.Generic;
using System.Linq;
using UnityEngine;
namespace BetterSupermarketTogether;
internal sealed class CompanionAssistantStaff
{
    private sealed class Job {internal NPC_Info Employee;internal Data_Container Shelf;internal CompanionWorkWorld World;internal CompanionPlanRunner Plan=new CompanionPlanRunner();}
    private readonly List<Job> _jobs=new List<Job>();
    internal bool ChangingRole=>_jobs.Any(j=>j.World.ChangingNativeRole);
    internal int Count=>_jobs.Count;
    internal CompanionWorkWorld World(NPC_Info employee)=>_jobs.FirstOrDefault(j=>j.Employee!=null && j.Employee==employee)?.World;
    internal string Dispatch(CompanionWorkWorld source,CompanionStep target,DeepSeekCompanion companion)
    {
        var manager=NPC_Manager.Instance;
        if(manager?.employeesArray==null || manager.employeesOnRiot)return "員工目前不可用。";
        // Register/inspect native staff once; never steal an existing temporary assignment.
        var shelf=source.ResolveShelf(target.Shelf);
        if(shelf==null || shelf.standSpot==null)return "貨架已移除。";
        RestockAssignments.Collect(manager,companion.BoundEmployee.gameObject,false,out var claimed,out _);
        if(_jobs.Any(j=>j.Shelf==shelf) || claimed.Contains(shelf.transform.GetSiblingIndex()))return "已有員工正在處理該貨架，沒有重複派人。";
        var candidates=manager.employeesArray.Where(x=>x!=null).Select(x=>x.GetComponent<NPC_Info>())
            .Where(e=>e!=null && e!=companion.BoundEmployee && e.taskPriority==0 && CompanionLife.Idle(e) && World(e)==null)
            .OrderBy(e=>(e.transform.position-companion.BoundEmployee.transform.position).sqrMagnitude);
        foreach(var employee in candidates)
        {
            int index=System.Array.IndexOf(manager.employeesArray,employee.gameObject);
            var roster=(Newtonsoft.Json.Linq.JArray)companion.StaffWorld.Inspect((index+1).ToString(),0)["employees"];
            if(!new EmployeeNavigation(employee.gameObject).CanReach(shelf.standSpot.transform.position))continue;
            var row=roster.FirstOrDefault(x=>(int)x["employee_number"]==index+1);
            if(row==null || row["pending_role"]?.Type!=Newtonsoft.Json.Linq.JTokenType.Null)continue;
            var world=new CompanionWorkWorld();world.Bind(employee);
            var step=world.ImportTarget(source,target);if(step==null || world.Validate(new List<CompanionStep>{step})!=null)continue;
            var job=new Job{Employee=employee,World=world,Shelf=shelf};_jobs.Add(job);
            job.Plan.Replace(new List<CompanionStep>{step},false);job.Plan.Tick(Time.time,world);
            if(job.Plan.Phase==CompanionPlanPhase.Failed){_jobs.Remove(job);return job.Plan.Status.ToString();}
            return "已派員工 "+(index+1)+" 補指定貨架；完成後恢復原職。這是派工確認，尚未完成補貨。";
        }
        return "目前沒有可接手的空閒休息員工；未更改其他員工職責。";
    }
    internal void Tick(bool enabled)
    {
        foreach(var job in _jobs.ToArray())
        {
            if(job.Employee==null || job.Employee.employeeDismissed || NPC_Manager.Instance?.employeesArray==null || !NPC_Manager.Instance.employeesArray.Contains(job.Employee.gameObject)){job.Plan.Reset(job.World);_jobs.Remove(job);continue;}
            if(!enabled && !job.Plan.HandingOff)job.Plan.Cancel();
            job.Plan.Tick(Time.time,job.World);
            if(!job.Plan.Owns)_jobs.Remove(job);
        }
    }
    internal void External(int index)
    {
        var manager=NPC_Manager.Instance;
        foreach(var job in _jobs.ToArray())
            if(index<0 || manager?.employeesArray!=null && index<manager.employeesArray.Length && manager.employeesArray[index]==job.Employee.gameObject)
            {job.World.ExternalChange();job.Plan.Reset(job.World);_jobs.Remove(job);}
    }
    internal void Cancel(){foreach(var job in _jobs)job.Plan.Cancel();}
    internal void Reset(){foreach(var job in _jobs)job.Plan.Reset(job.World);_jobs.Clear();}
}
