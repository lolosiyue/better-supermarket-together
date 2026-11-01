using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using NQuaternion = System.Numerics.Quaternion;

namespace BetterSupermarketTogether;

// Retarget evaluated bone poses, including native Animation Rigging/hand IK. The
// original Animator keeps running; generic animation curves never target VRM paths.
internal sealed class CompanionPoseBridge
{
    private sealed class Link
    {
        internal Transform Source, Target;
        internal Quaternion SourceBind, TargetBind;
    }
    private readonly Transform _root, _visual;
    private readonly Animator _animator;
    private readonly AnimatorCullingMode _originalCulling;
    private readonly List<Link> _links = new List<Link>();
    private readonly Quaternion _alignment;
    private readonly Transform _sourceHips, _targetHips;
    private readonly Vector3 _sourceHipBind, _targetHipBind;
    private readonly Vector3 _sourceFeet, _targetFeet;
    private readonly float _positionScale;
    internal int Count => _links.Count;

    internal static Dictionary<string, Transform> Map(Animator animator, IEnumerable<string> roles, bool upperChest)
    {
        var transforms = animator.GetComponentsInChildren<Transform>(true);
        var result = new Dictionary<string, Transform>();
        foreach (string role in roles)
        {
            Transform bone = null;
            if (animator.isHuman && animator.avatar != null && animator.avatar.isValid)
            {
                string unityName = role;
                if (role.EndsWith("ThumbMetacarpal")) unityName = role.Replace("Metacarpal", "Proximal");
                else if (role.EndsWith("ThumbProximal")) unityName = role.Replace("Proximal", "Intermediate");
                unityName = char.ToUpperInvariant(unityName[0]) + unityName.Substring(1);
                if (Enum.TryParse(unityName, out HumanBodyBones humanBone)) bone = animator.GetBoneTransform(humanBone);
            }
            if (bone == null)
            {
                string name = CompanionRigPolicy.Find(role, transforms.Select(x => x.name), upperChest);
                if (name != null) bone = transforms.First(x => x.name == name);
            }
            if (bone != null) result[role] = bone;
        }
        return result;
    }

    internal CompanionPoseBridge(Animator source, Transform visual, Dictionary<string, Transform> target)
    {
        _animator = source; _root = source.transform; _visual = visual;
        var mapped = Map(source, target.Keys, target.ContainsKey("upperChest"));
        var missing = CompanionRigPolicy.Required.Where(x => !mapped.ContainsKey(x) || !target.ContainsKey(x)).ToArray();
        if (missing.Length != 0) throw new InvalidOperationException("Missing mapped bones: " + string.Join(",", missing));
        var rest = new Dictionary<Transform, Matrix4x4>();
        foreach (var renderer in source.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            if (renderer.sharedMesh == null) continue;
            var poses = renderer.sharedMesh.bindposes; var bones = renderer.bones;
            for (int i = 0; i < Math.Min(poses.Length,bones.Length); i++)
                if (bones[i] != null && !rest.ContainsKey(bones[i]))
                    rest[bones[i]] = _root.worldToLocalMatrix * renderer.transform.localToWorldMatrix * poses[i].inverse;
        }
        foreach (string role in CompanionRigPolicy.Required)
            if (!rest.ContainsKey(mapped[role])) throw new InvalidOperationException("Missing native bind pose: " + role);
        var targetRest = target.ToDictionary(x => x.Key, x => visual.worldToLocalMatrix * x.Value.localToWorldMatrix);
        Func<string,Vector3> s = key => Position(rest[mapped[key]]);
        Func<string,Vector3> t = key => Position(targetRest[key]);
        Quaternion sourceFrame = Frame(s), targetFrame = Frame(t);
        _alignment = sourceFrame * Quaternion.Inverse(targetFrame);
        float sourceHeight = Vector3.Dot(s("head") - (s("leftFoot") + s("rightFoot"))*.5f, sourceFrame * Vector3.up);
        float targetHeight = Vector3.Dot(t("head") - (t("leftFoot") + t("rightFoot"))*.5f, targetFrame * Vector3.up);
        if (sourceHeight <= .01f || targetHeight <= .01f) throw new InvalidOperationException("Invalid rig height");
        _positionScale = targetHeight / sourceHeight;
        visual.rotation = _root.rotation * _alignment;
        float scale = sourceHeight * Mathf.Abs(_root.lossyScale.y) / targetHeight / Mathf.Max(.001f,Mathf.Abs(visual.parent.lossyScale.y));
        visual.localScale = Vector3.one * scale;
        // Match foot height while retaining the employee's world position and yaw.
        _sourceFeet = (s("leftFoot") + s("rightFoot"))*.5f;
        _targetFeet = (t("leftFoot") + t("rightFoot"))*.5f;
        visual.position = _root.TransformPoint(_sourceFeet) - visual.TransformVector(_targetFeet);
        foreach (var pair in target)
        {
            if (!mapped.TryGetValue(pair.Key,out var bone) || !rest.TryGetValue(bone,out var bind)) continue;
            Quaternion targetBind = targetRest[pair.Key].rotation;
            string child = AimChild(pair.Key, target.Keys);
            if (child != null && mapped.ContainsKey(child) && rest.ContainsKey(mapped[child]))
            {
                Vector3 td = t(child) - t(pair.Key), sd = Quaternion.Inverse(_alignment) * (s(child) - s(pair.Key));
                if (td.sqrMagnitude > 1e-8f && sd.sqrMagnitude > 1e-8f) targetBind = Quaternion.FromToRotation(td, sd) * targetBind;
            }
            _links.Add(new Link { Source=bone, Target=pair.Value, SourceBind=bind.rotation, TargetBind=targetBind });
        }
        _links.Sort((a,b) => Depth(a.Target).CompareTo(Depth(b.Target)));
        _sourceHips=mapped["hips"]; _targetHips=target["hips"];
        _sourceHipBind=s("hips"); _targetHipBind=t("hips");
        _originalCulling=source.cullingMode; source.cullingMode=AnimatorCullingMode.AlwaysAnimate;
    }

    internal bool Apply()
    {
        if (_animator == null || _root == null || _visual == null || _sourceHips == null || _targetHips == null) return false;
        Quaternion inverseRoot=Quaternion.Inverse(_root.rotation);
        _visual.rotation = _root.rotation * _alignment;
        _visual.position = _root.TransformPoint(_sourceFeet) - _visual.TransformVector(_targetFeet);
        foreach (var link in _links)
        {
            if (link.Source == null || link.Target == null) return false;
            Quaternion q=FromNumerics(CompanionRigPolicy.Rotation(ToNumerics(inverseRoot*link.Source.rotation),ToNumerics(link.SourceBind),ToNumerics(_alignment),ToNumerics(link.TargetBind)));
            link.Target.rotation=_visual.rotation*q;
        }
        Vector3 movement=_root.InverseTransformPoint(_sourceHips.position)-_sourceHipBind;
        _targetHips.position=_visual.TransformPoint(_targetHipBind+Quaternion.Inverse(_alignment)*movement*_positionScale);
        return true;
    }

    internal void Restore() { if (_animator != null) _animator.cullingMode=_originalCulling; }
    private static Vector3 Position(Matrix4x4 m) => m.GetColumn(3);
    private static Quaternion Frame(Func<string,Vector3> p)
    {
        Vector3 up=(p("head")-p("hips")).normalized;
        Vector3 right=(p("rightUpperLeg")-p("leftUpperLeg")).normalized;
        return Quaternion.LookRotation(Vector3.Cross(right,up).normalized,up);
    }
    private static int Depth(Transform t) { int n=0; while(t.parent!=null) { n++;t=t.parent; } return n; }
    private static NQuaternion ToNumerics(Quaternion q) => new NQuaternion(q.x,q.y,q.z,q.w);
    private static Quaternion FromNumerics(NQuaternion q) => new Quaternion(q.X,q.Y,q.Z,q.W);
    private static string AimChild(string role,ICollection<string> roles)
    {
        switch(role)
        {
            case "hips": return "spine";
            case "spine": return roles.Contains("chest")?"chest":"head";
            case "chest": return roles.Contains("upperChest")?"upperChest":roles.Contains("neck")?"neck":"head";
            case "upperChest": return roles.Contains("neck")?"neck":"head";
            case "neck": return "head";
        }
        if(role.EndsWith("Shoulder")) return role.Replace("Shoulder","UpperArm");
        if(role.EndsWith("UpperArm")) return role.Replace("UpperArm","LowerArm");
        if(role.EndsWith("LowerArm")) return role.Replace("LowerArm","Hand");
        if(role.EndsWith("UpperLeg")) return role.Replace("UpperLeg","LowerLeg");
        if(role.EndsWith("LowerLeg")) return role.Replace("LowerLeg","Foot");
        return null;
    }
}
