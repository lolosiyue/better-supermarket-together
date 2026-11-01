using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace BetterSupermarketTogether;

// Bounded, world-space secondary motion for the bundled VRM's authored chains.
// This deliberately retains movement inertia instead of locking simulation to spring centers.
internal sealed class CompanionSecondaryMotion
{
    private sealed class Guard
    {
        internal Transform Bone;
        internal Vector3 Offset, Tail;
        internal float Radius;
    }
    private sealed class Joint
    {
        internal Transform Bone;
        internal Quaternion Rest;
        internal Vector3 Axis, Current, Previous, Gravity;
        internal float Stiffness, Drag, Radius, Limit;
        internal Guard[] Guards;
    }
    private readonly List<Joint> _joints = new List<Joint>();
    private readonly Transform _root;
    private Vector3 _lastPosition;
    private Quaternion _lastRotation;
    private float _previousStep=1f/60;
    private bool _ready;
    internal int Count => _joints.Count;

    internal CompanionSecondaryMotion(JToken extension,Transform[] nodes,Transform root)
    {
        _root=root;
        if(!(extension?["springs"] is JArray springs))return;
        var guards=new List<Guard>();
        foreach(var c in (JArray)extension["colliders"] ?? new JArray())
        {
            JToken shape=c["shape"]?["sphere"] ?? c["shape"]?["capsule"];
            if(shape==null)continue;
            Vector3 offset=Vector(shape["offset"]);
            guards.Add(new Guard { Bone=nodes[(int)c["node"]],Offset=offset,
                Tail=shape["tail"]==null?offset:Vector(shape["tail"]),Radius=(float)(shape["radius"]??0) });
        }
        var used=new HashSet<int>();
        foreach(var spring in springs)
        {
            var selected=new List<Guard>();
            foreach(var group in (JArray)spring["colliderGroups"] ?? new JArray())
                foreach(var index in (JArray)extension["colliderGroups"][(int)group]["colliders"])
                    selected.Add(guards[(int)index]);
            var joints=(JArray)spring["joints"];
            string name=(string)spring["name"]??"";
            // End markers define the preceding segment; rotating them cannot deform a chain.
            for(int i=0;i+1<joints.Count;i++)
            {
                var j=joints[i]; int index=(int)j["node"];
                if(!used.Add(index))continue;
                Transform bone=nodes[index],child=nodes[(int)joints[i+1]["node"]];
                Vector3 axis=bone.InverseTransformPoint(child.position);
                if(axis.sqrMagnitude<1e-10f)continue;
                _joints.Add(new Joint { Bone=bone,Rest=bone.localRotation,Axis=axis,
                    Gravity=Vector(j["gravityDir"])*(float)(j["gravityPower"]??0),
                    Stiffness=(float)(j["stiffness"]??1),Drag=Mathf.Clamp01((float)(j["dragForce"]??.5f)),
                    Radius=(float)(j["hitRadius"]??0),Guards=selected.ToArray(),
                    Limit=(name.StartsWith("skirt")?35:name=="whale_tail"?65:45)*Mathf.Deg2Rad });
            }
        }
    }

    internal void Apply(float dt)
    {
        if(_root==null || dt<=0)return;
        bool reset=!_ready || dt>.15f || Vector3.Distance(_root.position,_lastPosition)>1.5f || Quaternion.Angle(_root.rotation,_lastRotation)>100;
        _lastPosition=_root.position; _lastRotation=_root.rotation;
        if(reset)
        {
            foreach(var j in _joints)
            {
                j.Bone.localRotation=j.Rest;
                j.Current=j.Previous=j.Bone.TransformPoint(j.Axis);
            }
            _previousStep=1f/60; _ready=true; return;
        }
        int steps=Mathf.CeilToInt(dt/(1f/60)); float step=dt/steps;
        for(int n=0;n<steps;n++)
        {
            foreach(var j in _joints)
            {
                Transform bone=j.Bone;
                bone.localRotation=j.Rest;
                Vector3 origin=bone.position, rest=bone.TransformVector(j.Axis);
                float length=rest.magnitude;
                if(length<1e-6f)continue;
                float scale=Scale(bone);
                Vector3 velocity=(j.Current-j.Previous)*(step/_previousStep)*Mathf.Pow(1-j.Drag,step*60);
                Vector3 next=j.Current+velocity+(rest.normalized*j.Stiffness+j.Gravity)*scale*step;
                Vector3 direction=next-origin;
                if(direction.sqrMagnitude<1e-10f)direction=rest;
                direction=Vector3.RotateTowards(rest.normalized,direction.normalized,j.Limit,0);
                next=origin+direction*length;
                // Alternating sphere/capsule and length constraints; bones never stretch.
                for(int pass=0;pass<3;pass++)
                {
                    foreach(var guard in j.Guards)
                    {
                        Vector3 a=guard.Bone.TransformPoint(guard.Offset), b=guard.Bone.TransformPoint(guard.Tail);
                        Vector3 segment=b-a;
                        Vector3 nearest=a+segment*(segment.sqrMagnitude>1e-10f?Mathf.Clamp01(Vector3.Dot(next-a,segment)/segment.sqrMagnitude):0);
                        float radius=guard.Radius*Scale(guard.Bone)+j.Radius*scale;
                        Vector3 away=next-nearest;
                        if(away.sqrMagnitude<radius*radius)
                            next=nearest+(away.sqrMagnitude>1e-10f?away.normalized:rest.normalized)*radius;
                    }
                    direction=next-origin;
                    next=origin+(direction.sqrMagnitude>1e-10f?direction.normalized:rest.normalized)*length;
                }
                j.Previous=j.Current; j.Current=next;
                bone.rotation=Quaternion.FromToRotation(rest,next-origin)*bone.rotation;
            }
            _previousStep=step;
        }
    }
    private static float Scale(Transform t)
    {
        var s=t.lossyScale; return Mathf.Max(Mathf.Abs(s.x),Mathf.Abs(s.y),Mathf.Abs(s.z));
    }
    private static Vector3 Vector(JToken t) => t is JArray?new Vector3((float)t[0],(float)t[1],-(float)t[2]):Vector3.zero;
}
