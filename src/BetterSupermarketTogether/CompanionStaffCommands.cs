using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace BetterSupermarketTogether;

internal sealed class CompanionStaffOrder
{
    internal string Id, Role, Completion="one_cycle";
    internal int Seconds;
    internal static bool TryParse(JObject args,out List<CompanionStaffOrder> orders)
    {
        orders=null;
        if(args?.Count!=1 || args["assignments"] is not JArray rows || rows.Count<1 || rows.Count>64)return false;
        var parsed=new List<CompanionStaffOrder>();var ids=new HashSet<string>();
        foreach(var row in rows)
        {
            if(row is not JObject obj || obj.Properties().Any(p=>p.Name!="staff_id" && p.Name!="role" && p.Name!="completion" && p.Name!="duration_seconds") || obj["staff_id"]?.Type!=JTokenType.String || obj["role"]?.Type!=JTokenType.String)return false;
            string id=(string)obj["staff_id"],role=(string)obj["role"];
            if(id.Length<2 || id.Length>16 || id[0]!='E' || !id.Skip(1).All(c=>c>='0'&&c<='9') ||
                !ids.Add(id) || !CompanionProtocol.Roles.Contains(role))return false;
            string completion=role=="rest"?"duration":"one_cycle";int seconds=role=="rest"?120:0;
            if(obj["completion"]!=null)
            {
                if(obj["completion"].Type!=JTokenType.String)return false;
                completion=(string)obj["completion"];
            }
            if(completion!="one_cycle" && completion!="duration" && completion!="continuous" || role=="rest" && completion=="one_cycle")return false;
            if(obj["duration_seconds"]!=null)
            {
                if(completion!="duration" || obj["duration_seconds"].Type!=JTokenType.Integer)return false;
                try { long value=(long)obj["duration_seconds"];if(value<5 || value>1800)return false;seconds=(int)value; }
                catch(OverflowException) { return false; }
            }
            else if(completion=="duration" && role!="rest")return false;
            parsed.Add(new CompanionStaffOrder { Id=id,Role=role,Completion=completion,Seconds=seconds });
        }
        orders=parsed;return true;
    }
}

internal interface ICompanionStaffWorld
{
    bool Authorized { get; }
    bool CanRestore { get; }
    bool Available(string id,out int role,out bool safe,out string label);
    bool Assign(string id,int role,bool restoring=false);
    long Progress(string id,int role);
}

// A finite dispatch owns its original role until completion and a safe return boundary.
internal sealed class CompanionStaffQueue
{
    private sealed class Pending
    {
        internal string Id,Label,Completion;
        internal int OriginalRole,ExpectedRole,Role,Seconds,RestoreFailures;
        internal long Baseline;
        internal double Expires,StartedAt,RetryAt;
        internal bool Started,Returning;
    }
    private readonly Dictionary<string,Pending> _pending=new Dictionary<string,Pending>();
    private readonly Queue<ModText.Message> _events=new Queue<ModText.Message>();
    private readonly Queue<ModText.Message> _recent=new Queue<ModText.Message>();
    internal IEnumerable<ModText.Message> Recent => _recent;
    internal int Count => _pending.Count;
    internal int WaitingCount => _pending.Values.Count(x=>!x.Started || x.Returning);
    internal bool Contains(string id) => _pending.ContainsKey(id);
    internal bool Hold(string id) => _pending.TryGetValue(id,out var p) && (!p.Started || p.Returning);
    internal string DesiredRole(string id) => _pending.TryGetValue(id,out var p)?CompanionProtocol.Roles[p.Returning?p.OriginalRole:p.Role]:null;
    internal string Phase(string id) => !_pending.TryGetValue(id,out var p)?null:p.Returning?"returning":p.Started?"working":"waiting";

    internal ModText.Message Enqueue(List<CompanionStaffOrder> orders,double now,ICompanionStaffWorld world)
    {
        if(!world.Authorized)return "員工派工未接受：需要房主權限、有效同伴及非罷工狀態。";
        var staged=new List<Pending>();
        foreach(var order in orders)
        {
            if(!world.Available(order.Id,out int role,out _,out string label))
                return new ModText.Message("員工派工未接受：{0} 已失效或是同伴本人，請重新查閱名單。",order.Id);
            int original=role;
            if(_pending.TryGetValue(order.Id,out var previous) && role==previous.ExpectedRole)original=previous.OriginalRole;
            staged.Add(new Pending { Id=order.Id,Label=label,OriginalRole=original,ExpectedRole=role,
                Role=Array.IndexOf(CompanionProtocol.Roles,order.Role),Completion=order.Completion,Seconds=order.Seconds,Expires=now+180 });
        }
        if(_pending.Keys.Union(staged.Select(p=>p.Id)).Count()>64)return "待派工員工已達上限，請先等候或取消。";
        foreach(var p in staged)_pending[p.Id]=p;
        return new ModText.Message("已排入 {0} 名員工的派工；預設完成一輪後恢復原職。",staged.Count);
    }

    internal void Tick(double now,ICompanionStaffWorld world)
    {
        foreach(var p in _pending.Values.ToArray())
        {
            if(!world.Available(p.Id,out int role,out bool safe,out _))
            { Finish(p,"員工已離職、被替換或綁定已改變");continue; }
            if(role!=p.ExpectedRole)
            { Finish(p,"職務已由其他介面變更");continue; }
            if(p.Returning) { Return(p,now,safe,world);continue; }
            if(!p.Started)
            {
                if(now>=p.Expires)
                {
                    if(p.ExpectedRole==p.OriginalRole)Finish(p,"等待安全交接超過三分鐘");
                    else BeginReturn(p,"等待安全交接超過三分鐘");
                    continue;
                }
                if(!world.Authorized || !safe)continue;
                bool assigned=false;
                long baseline=world.Progress(p.Id,p.Role);
                try { assigned=world.Assign(p.Id,p.Role); } catch(Exception) { }
                if(!assigned)
                {
                    _events.Enqueue(new ModText.Message("員工 {0} 派工失敗：{1}。",p.Label,new ModText.Message("遊戲未確認職務變更")));
                    if(p.ExpectedRole!=p.OriginalRole)p.Returning=true;else _pending.Remove(p.Id);
                    continue;
                }
                p.ExpectedRole=p.Role;p.Started=true;p.StartedAt=now;p.Baseline=baseline;
                _events.Enqueue(new ModText.Message("員工 {0} 已確認改為{1}；實際工作由遊戲執行。",p.Label,new ModText.Message(CompanionProtocol.RoleLabels[p.Role])));
                if(p.Completion=="continuous")_pending.Remove(p.Id);
                continue;
            }
            bool done=p.Completion=="duration"?now-p.StartedAt>=p.Seconds:world.Progress(p.Id,p.Role)>p.Baseline;
            if(done || p.Completion=="one_cycle" && now-p.StartedAt>=900)
            {
                BeginReturn(p,done?"臨時工作已完成":"十五分鐘內沒有確認工作進度");
                Return(p,now,safe,world);
            }
        }
    }
    private void BeginReturn(Pending p,string reason)
    {
        p.Returning=true;
        _events.Enqueue(new ModText.Message("員工 {0}：{1}，等待安全交接後恢復原職。",p.Label,new ModText.Message(reason)));
    }
    private void Return(Pending p,double now,bool safe,ICompanionStaffWorld world)
    {
        if(!world.CanRestore || !safe || now<p.RetryAt)return;
        bool restored=false;
        try { restored=world.Assign(p.Id,p.OriginalRole,true); } catch(Exception) { }
        if(restored)
        {
            _pending.Remove(p.Id);
            _events.Enqueue(new ModText.Message("員工 {0} 已恢復原本職責：{1}。",p.Label,new ModText.Message(CompanionProtocol.RoleLabels[p.OriginalRole])));
        }
        else if(++p.RestoreFailures>=3)
        {
            _pending.Remove(p.Id);
            _events.Enqueue(new ModText.Message("員工 {0} 恢復原職失敗，請在員工介面檢查。",p.Label));
        }
        else p.RetryAt=now+2;
    }
    private void Finish(Pending p,string reason)
    {
        _pending.Remove(p.Id);
        _events.Enqueue(new ModText.Message("員工 {0} 派工已取消：{1}。",p.Label,new ModText.Message(reason)));
    }
    internal void Cancel(string id)
    {
        if(_pending.TryGetValue(id,out var p))Finish(p,"職務已由其他介面變更");
    }
    internal void CancelAll()
    {
        foreach(var p in _pending.Values.ToArray())
        {
            if(!p.Started && p.ExpectedRole==p.OriginalRole)_pending.Remove(p.Id);
            else if(!p.Returning)BeginReturn(p,"玩家已取消臨時工作");
        }
    }
    internal IEnumerable<ModText.Message> Drain()
    {
        while(_events.Count>0)
        {
            var message=_events.Dequeue();_recent.Enqueue(message);
            while(_recent.Count>16)_recent.Dequeue();
            yield return message;
        }
    }
    internal void Reset() { _pending.Clear();_events.Clear();_recent.Clear(); }
}
