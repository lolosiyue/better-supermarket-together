using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Rendering;

namespace BetterSupermarketTogether;

// Deliberately supports the pinned bundled VRM only. It is not a general VRM importer:
// base textures + skinning + evaluated native bone poses, without MToon, springs or expressions.
[DefaultExecutionOrder(10000)]
internal sealed class CompanionAvatar : MonoBehaviour
{
    private GameObject _visual;
    private Animator _source;
    private CompanionPoseBridge _bridge;
    private readonly CompanionAvatarRetry _retry = new CompanionAvatarRetry();
    private string _modelPath, _lastDiagnostic;
    private readonly List<UnityEngine.Object> _resources = new List<UnityEngine.Object>();
    private readonly List<Renderer> _hidden = new List<Renderer>();
    internal string Status { get; private set; } = "使用原有員工外觀。";
    private NPC_Info _employee;
    private Coroutine _load;

    internal void Bind(NPC_Info employee, string path)
    {
        if (employee == null) { if (_employee != null || _visual != null || _load != null) Clear(); return; }
        if (_employee != employee || _modelPath != path) { Clear(); _employee = employee; _modelPath = path; }
        if (_retry.Ready && _source == null) { Release(); _retry.Reset(); }
        if (_load != null || !_retry.Due(Time.unscaledTime)) return;
        if (!File.Exists(path)) { Status="找不到 DeepSeek 娘模型；會自動重試。"; _retry.Failed(Time.unscaledTime,5); return; }
        var candidates = employee.GetComponentsInChildren<Animator>(true)
            .Where(x => _visual == null || !x.transform.IsChildOf(_visual.transform))
            .Select(x => new { Animator=x, Bones=CompanionPoseBridge.Map(x, CompanionRigPolicy.Required, false) })
            .OrderByDescending(x => x.Bones.Count).ToArray();
        var candidate = candidates.FirstOrDefault();
        string[] missing = candidate == null ? CompanionRigPolicy.Required : CompanionRigPolicy.Required.Where(x=>!candidate.Bones.ContainsKey(x)).ToArray();
        if (missing.Length != 0 || candidate.Animator.GetComponentsInChildren<SkinnedMeshRenderer>(true).All(x=>x.sharedMesh==null || x.bones.Length==0))
        {
            Status="等待員工骨架載入／配對；會自動重試。";
            string diagnostic="animators="+candidates.Length+" missing="+string.Join(",",missing);
            if (_lastDiagnostic!=diagnostic) { _lastDiagnostic=diagnostic; BetterSupermarketPlugin.Log.LogInfo("Companion avatar waiting: "+diagnostic); }
            _retry.Failed(Time.unscaledTime); return;
        }
        _source=candidate.Animator;
        _load = StartCoroutine(Load(path));
    }

    private IEnumerator Load(string path)
    {
        Status = "正在載入 DeepSeek 娘外觀……";
        yield return null;
        VrmData data = null;
        Exception failure = null;
        try { if (new FileInfo(path).Length != 33616628) throw new IOException("Unsupported model size"); data = new VrmData(File.ReadAllBytes(path)); }
        catch (Exception ex) { failure = ex; }
        if (failure != null) { Failed(failure); yield break; }
        var routine = Build(data);
        while (true)
        {
            bool more = false;
            try { more = routine.MoveNext(); }
            catch (Exception ex) { failure = ex; }
            if (failure != null) { Failed(failure); yield break; }
            if (!more) break;
            yield return routine.Current;
        }
        _load = null;
        _retry.Succeeded();
        Status = "DeepSeek 娘外觀已載入（原生骨架動作同步；房主本機可見）。";
        BetterSupermarketPlugin.Log.LogInfo("Companion avatar ready: source="+(_source.isHuman?"Humanoid":"Generic")+" mappedBones="+_bridge.Count);
    }

    private void Failed(Exception error)
    {
        _load = null; Release();
        _retry.Failed(Time.unscaledTime,30);
        Status = "模型未能載入，保留原有外觀；稍後會自動重試，詳情見日誌。";
        BetterSupermarketPlugin.Log.LogWarning("Companion avatar load failed: "+error.GetType().Name+": "+error.Message);
    }

    private IEnumerator Build(VrmData d)
    {
        _visual = new GameObject("DeepSeek-chan visual"); _visual.SetActive(false);
        _visual.transform.SetParent(_employee.transform, false);
        var nodes = (JArray)d.Json["nodes"];
        var transforms = new Transform[nodes.Count];
        for (int i = 0; i < nodes.Count; i++)
        {
            var obj = new GameObject((string)nodes[i]["name"] ?? "node_" + i);
            transforms[i] = obj.transform; obj.transform.SetParent(_visual.transform, false);
            transforms[i].localPosition = Vector(nodes[i]["translation"], Vector3.zero, true);
            transforms[i].localScale = Vector(nodes[i]["scale"], Vector3.one, false);
            var q = nodes[i]["rotation"] as JArray;
            transforms[i].localRotation = q == null ? Quaternion.identity : new Quaternion(-(float)q[0], -(float)q[1], (float)q[2], (float)q[3]);
        }
        for (int i = 0; i < nodes.Count; i++) if (nodes[i]["children"] is JArray children)
            foreach (var child in children) transforms[(int)child].SetParent(transforms[i], false);

        var textures = new Texture2D[((JArray)d.Json["images"]).Count];
        for (int i = 0; i < textures.Length; i++)
        {
            int view = (int)d.Json["images"][i]["bufferView"];
            textures[i] = new Texture2D(2,2,TextureFormat.RGBA32,true);
            _resources.Add(textures[i]);
            if (!ImageConversion.LoadImage(textures[i], d.ViewBytes(view), true)) throw new IOException("Invalid texture");
            yield return null;
        }
        var materials = new Material[((JArray)d.Json["materials"]).Count];
        for (int i = 0; i < materials.Length; i++)
        {
            var entry = d.Json["materials"][i];
            string alpha = (string)entry["alphaMode"] ?? "OPAQUE";
            bool scriptable = GraphicsSettings.currentRenderPipeline != null;
            var shader = scriptable
                ? Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Universal Render Pipeline/Lit")
                : Shader.Find(alpha == "MASK" ? "Unlit/Transparent Cutout" : alpha == "BLEND" ? "Unlit/Transparent" : "Unlit/Texture") ?? Shader.Find("Standard");
            if (shader == null) throw new IOException("No supported shader");
            var material = new Material(shader) { name = (string)entry["name"] };
            _resources.Add(material); materials[i] = material;
            int textureIndex = (int)(entry["pbrMetallicRoughness"]?["baseColorTexture"]?["index"] ?? -1);
            if (textureIndex >= 0) material.mainTexture = textures[(int)d.Json["textures"][textureIndex]["source"]];
            var color = entry["pbrMetallicRoughness"]?["baseColorFactor"] as JArray;
            if (color != null)
            {
                var tint = new Color((float)color[0],(float)color[1],(float)color[2],(float)color[3]);
                if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor",tint);
                else if (material.HasProperty("_Color")) material.SetColor("_Color",tint);
            }
            if (material.HasProperty("_Cull")) material.SetInt("_Cull", (int)CullMode.Off);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", .25f);
            if (alpha == "MASK")
            {
                material.EnableKeyword("_ALPHATEST_ON");
                if (material.HasProperty("_AlphaClip")) material.SetFloat("_AlphaClip",1);
                if (material.HasProperty("_Cutoff")) material.SetFloat("_Cutoff", (float)(entry["alphaCutoff"] ?? .5f));
                material.SetOverrideTag("RenderType","TransparentCutout"); material.renderQueue=(int)RenderQueue.AlphaTest;
            }
            else if (alpha == "BLEND" && scriptable)
            {
                if (material.HasProperty("_Surface")) material.SetFloat("_Surface",1);
                if (material.HasProperty("_SrcBlend")) material.SetInt("_SrcBlend",(int)BlendMode.SrcAlpha);
                if (material.HasProperty("_DstBlend")) material.SetInt("_DstBlend",(int)BlendMode.OneMinusSrcAlpha);
                if (material.HasProperty("_ZWrite")) material.SetInt("_ZWrite",0);
                material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                material.SetOverrideTag("RenderType","Transparent"); material.renderQueue=(int)RenderQueue.Transparent;
            }
        }
        for (int n = 0; n < nodes.Count; n++)
        {
            if (nodes[n]["mesh"] == null) continue;
            var skin = d.Json["skins"][(int)nodes[n]["skin"]];
            var bones = ((JArray)skin["joints"]).Select(x => transforms[(int)x]).ToArray();
            var bindPoses = d.Matrices((int)skin["inverseBindMatrices"]);
            foreach (var primitive in d.Json["meshes"][(int)nodes[n]["mesh"]]["primitives"])
            {
                var attributes = primitive["attributes"];
                var mesh = new Mesh { name = "DeepSeek primitive", indexFormat = IndexFormat.UInt32 };
                _resources.Add(mesh);
                mesh.vertices = d.Vectors((int)attributes["POSITION"],true);
                mesh.normals = d.Vectors((int)attributes["NORMAL"],true);
                mesh.uv = d.Uvs((int)attributes["TEXCOORD_0"]);
                mesh.boneWeights = d.Weights((int)attributes["JOINTS_0"],(int)attributes["WEIGHTS_0"]);
                mesh.bindposes = bindPoses;
                mesh.triangles = d.Indices((int)primitive["indices"]);
                mesh.RecalculateBounds();
                var obj = new GameObject("Mesh"); obj.transform.SetParent(transforms[n],false);
                var renderer = obj.AddComponent<SkinnedMeshRenderer>();
                renderer.sharedMesh = mesh; renderer.bones = bones; renderer.rootBone = transforms[(int)d.Json["extensions"]["VRMC_vrm"]["humanoid"]["humanBones"]["hips"]["node"]];
                renderer.sharedMaterial = materials[(int)primitive["material"]];
                renderer.updateWhenOffscreen = true;
            }
            yield return null;
        }
        var human = new Dictionary<string,Transform>();
        foreach (var bone in (JObject)d.Json["extensions"]["VRMC_vrm"]["humanoid"]["humanBones"])
        {
            human.Add(bone.Key,transforms[(int)bone.Value["node"]]);
        }
        _bridge = new CompanionPoseBridge(_source,_visual.transform,human);
        if (!_bridge.Apply()) throw new InvalidOperationException("Native rig changed while loading");
        _visual.SetActive(true);
        foreach (var renderer in _source.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            if (renderer.enabled) { _hidden.Add(renderer); renderer.enabled = false; }
    }

    private void LateUpdate()
    {
        if (_bridge == null || _visual == null || !_visual.activeSelf) return;
        if (!_bridge.Apply()) { Release(); _retry.Reset(); Status="員工模型已變更，正在重新載入外觀。"; }
    }

    private static Vector3 Vector(JToken t,Vector3 fallback,bool reverse) => t is JArray ? new Vector3((float)t[0],(float)t[1],(reverse?-1:1)*(float)t[2]) : fallback;
    internal void Clear() { if (_load != null) StopCoroutine(_load); _load=null; _employee=null; _modelPath=_lastDiagnostic=null; Release(); _retry.Reset(); Status="使用原有員工外觀。"; }
    private void Release()
    {
        _bridge?.Restore(); _bridge=null;
        foreach (var renderer in _hidden) if (renderer != null) renderer.enabled=true;
        _hidden.Clear();
        if (_visual != null) { _visual.SetActive(false); Destroy(_visual); }
        _visual=null; _source=null;
        foreach (var resource in _resources) if (resource != null) Destroy(resource);
        _resources.Clear();
    }
    private void OnDestroy() => Clear();

    private sealed class VrmData
    {
        internal readonly JObject Json;
        private readonly byte[] _bytes;
        private readonly int _binary;
        internal VrmData(byte[] bytes)
        {
            // Hash is assigned from the upstream asset during packaging and checked before parsing.
            using (var sha=SHA256.Create())
            {
                string actual=BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-","").ToLowerInvariant();
                if (actual != BundledModelSha256) throw new IOException("Unsupported model");
            }
            if (BitConverter.ToUInt32(bytes,0)!=0x46546c67 || BitConverter.ToUInt32(bytes,4)!=2) throw new IOException("Invalid GLB");
            int jsonLength=BitConverter.ToInt32(bytes,12);
            Json=JObject.Parse(Encoding.UTF8.GetString(bytes,20,jsonLength));
            _bytes=bytes; _binary=20+jsonLength+8;
        }
        internal byte[] ViewBytes(int index)
        {
            var view=Json["bufferViews"][index]; int length=(int)view["byteLength"];
            var result=new byte[length]; Buffer.BlockCopy(_bytes,_binary+(int)(view["byteOffset"]??0),result,0,length); return result;
        }
        private int Count(int a) => (int)Json["accessors"][a]["count"];
        private float Read(int a,int row,int component,int dimensions)
        {
            var accessor=Json["accessors"][a]; var view=Json["bufferViews"][(int)accessor["bufferView"]];
            int type=(int)accessor["componentType"],size=type==5126?4:type==5123?2:1;
            int offset=_binary+(int)(view["byteOffset"]??0)+(int)(accessor["byteOffset"]??0)+row*(int)(view["byteStride"]??(size*dimensions))+component*size;
            if (type==5126) return BitConverter.ToSingle(_bytes,offset);
            float value=type==5123?BitConverter.ToUInt16(_bytes,offset):_bytes[offset];
            return (bool)(accessor["normalized"]??false)?value/(type==5123?65535f:255f):value;
        }
        internal Vector3[] Vectors(int a,bool reverse) => Enumerable.Range(0,Count(a)).Select(i=>new Vector3(Read(a,i,0,3),Read(a,i,1,3),(reverse?-1:1)*Read(a,i,2,3))).ToArray();
        internal Vector2[] Uvs(int a) => Enumerable.Range(0,Count(a)).Select(i=>new Vector2(Read(a,i,0,2),1-Read(a,i,1,2))).ToArray();
        internal int[] Indices(int a)
        {
            var indices=Enumerable.Range(0,Count(a)).Select(i=>(int)Read(a,i,0,1)).ToArray();
            for(int i=0;i<indices.Length;i+=3) { int x=indices[i]; indices[i]=indices[i+2]; indices[i+2]=x; } return indices;
        }
        internal BoneWeight[] Weights(int joints,int weights) => Enumerable.Range(0,Count(joints)).Select(i=>new BoneWeight {
            boneIndex0=(int)Read(joints,i,0,4),boneIndex1=(int)Read(joints,i,1,4),boneIndex2=(int)Read(joints,i,2,4),boneIndex3=(int)Read(joints,i,3,4),
            weight0=Read(weights,i,0,4),weight1=Read(weights,i,1,4),weight2=Read(weights,i,2,4),weight3=Read(weights,i,3,4) }).ToArray();
        internal Matrix4x4[] Matrices(int a) => Enumerable.Range(0,Count(a)).Select(i=> {
            var m=new Matrix4x4(); for(int k=0;k<16;k++) m[k]=(k/4==2 ^ k%4==2 ? -1:1)*Read(a,i,k,16); return m;
        }).ToArray();
    }
    private const string BundledModelSha256 = "9fa455e48ee2271dc7c2b62f2f2edcd47709406ca89067b6f544979589958f42";
}
