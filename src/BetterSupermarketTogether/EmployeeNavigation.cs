using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.AI;

namespace BetterSupermarketTogether;

internal sealed class EmployeeNavigation
{
    private sealed class History
    {
        internal readonly Dictionary<Vector3, float> Blocked = new();
        internal Vector3 Destination, Position;
        internal int State = int.MinValue;
        internal float ProgressTime, CheckTime, PathTime;
    }

    private static readonly ConditionalWeakTable<NPC_Info, History> Histories = new();
    private readonly NPC_Info _employee;
    private readonly NavMeshAgent _agent;
    private readonly NavMeshPath _path = new();
    private readonly Dictionary<Vector3, bool> _reachable = new();

    internal EmployeeNavigation(GameObject requester)
    {
        _employee = requester != null ? requester.GetComponent<NPC_Info>() : null;
        _agent = requester != null ? requester.GetComponent<NavMeshAgent>() : null;
    }

    internal bool Ready => _agent != null && _agent.enabled && _agent.isOnNavMesh;

    private NavMeshQueryFilter Filter => new() { agentTypeID = _agent.agentTypeID, areaMask = _agent.areaMask };

    internal bool CanReach(Vector3 destination)
    {
        if (!Ready || IsBlocked(_employee, destination)) return false;
        if (_reachable.TryGetValue(destination, out bool result)) return result;
        result = TrySample(destination, out var point) &&
            NavMesh.CalculatePath(_agent.nextPosition, point, Filter, _path) && _path.status == NavMeshPathStatus.PathComplete;
        _reachable[destination] = result;
        return result;
    }

    internal bool CanTravel(Vector3 source, Vector3 destination)
    {
        return CanReach(source) && CanReach(destination) && TrySample(source, out var start) &&
            TrySample(destination, out var end) && NavMesh.CalculatePath(start, end, Filter, _path) &&
            _path.status == NavMeshPathStatus.PathComplete;
    }

    internal bool TrySample(Vector3 destination, out Vector3 point)
    {
        point = destination;
        if (!Ready || !NavMesh.SamplePosition(destination, out var hit, 1.5f, Filter)) return false;
        point = hit.position;
        return true;
    }

    internal bool MoveTo(Vector3 destination)
    {
        if (!CanReach(destination) || !TrySample(destination, out var point)) return false;
        _agent.isStopped = false;
        return _agent.SetDestination(point);
    }

    internal bool IsAt(Vector3 destination)
    {
        if (!Ready || _agent.pathPending || _agent.pathStatus != NavMeshPathStatus.PathComplete || !TrySample(destination, out var point)) return false;
        float reach = Mathf.Max(1.25f, _agent.stoppingDistance + 0.3f);
        if (Vector3.Distance(_agent.transform.position, point) > reach ||
            _agent.hasPath && (_agent.remainingDistance > _agent.stoppingDistance + 0.3f || _agent.velocity.sqrMagnitude > 0.01f)) return false;
        // A point can be nearby across a wall while its walkable route is much longer.
        if (!NavMesh.CalculatePath(_agent.nextPosition, point, Filter, _path) || _path.status != NavMeshPathStatus.PathComplete) return false;
        float distance = 0f;
        var corners = _path.corners;
        for (int i = 1; i < corners.Length; i++) distance += Vector3.Distance(corners[i - 1], corners[i]);
        return distance <= reach;
    }

    internal bool IsNear(Vector3 destination,float range)
    {
        if(!Ready || Vector3.Distance(_agent.transform.position,destination)>range || !TrySample(destination,out var point) ||
            !NavMesh.CalculatePath(_agent.nextPosition,point,Filter,_path) || _path.status!=NavMeshPathStatus.PathComplete)return false;
        float length=0;var corners=_path.corners;
        for(int i=1;i<corners.Length;i++)length+=Vector3.Distance(corners[i-1],corners[i]);
        return length<=range;
    }

    internal static void Block(NPC_Info employee, Vector3 destination)
    {
        var history = Histories.GetValue(employee, _ => new History());
        if (history.Blocked.Count > 32) history.Blocked.Clear();
        history.Blocked[destination] = Time.unscaledTime + 30f;
        history.State = int.MinValue;
    }

    private static bool IsBlocked(NPC_Info employee, Vector3 destination)
    {
        if (employee == null || !Histories.TryGetValue(employee, out var history)) return false;
        foreach (var entry in history.Blocked)
            if (entry.Value > Time.unscaledTime && Vector3.SqrMagnitude(entry.Key - destination) < 0.25f) return true;
        return false;
    }

    internal static bool IsStalled(NPC_Info employee, NavMeshAgent agent)
    {
        var history = Histories.GetValue(employee, _ => new History());
        float now = Time.unscaledTime;
        if (now < history.CheckTime) return false;
        history.CheckTime = now + 1f;
        var destination = agent.destination;
        if (history.State != employee.state || Vector3.SqrMagnitude(destination - history.Destination) > 0.25f)
        {
            history.State = employee.state;
            history.Destination = destination;
            history.Position = employee.transform.position;
            history.ProgressTime = history.PathTime = now;
            return false;
        }
        if (Vector3.SqrMagnitude(employee.transform.position - history.Position) > 0.04f)
        {
            history.Position = employee.transform.position;
            history.ProgressTime = now;
        }
        if (agent.pathPending) return now - history.PathTime > 15f;
        if (agent.pathStatus != NavMeshPathStatus.PathComplete) return now - history.PathTime > 3f;
        if (Vector3.Distance(employee.transform.position, destination) <= Mathf.Max(1.25f, agent.stoppingDistance + 0.3f))
        {
            history.ProgressTime = now;
            return false;
        }
        return now - history.ProgressTime > 30f;
    }

    internal static void ClearProgress(NPC_Info employee)
    {
        if (employee != null && Histories.TryGetValue(employee, out var history)) history.State = int.MinValue;
    }
}
