using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace BetterSupermarketTogether;

// Maps VRM 1.0 preset expressions (morphTargetBinds) onto the loaded blend shapes.
internal sealed class CompanionFaceRig
{
    private struct Bind { public int Slot, Index; public float Weight; }
    private readonly Dictionary<string, List<Bind>> _presets = new Dictionary<string, List<Bind>>();
    private readonly List<SkinnedMeshRenderer> _renderers = new List<SkinnedMeshRenderer>();
    private readonly List<int> _indices = new List<int>();
    private readonly Dictionary<long, int> _slots = new Dictionary<long, int>();
    private float[] _values = new float[0];
    internal int Presets => _presets.Count;

    internal CompanionFaceRig(JToken expressions, Dictionary<int, List<SkinnedMeshRenderer>> byNode)
    {
        if (!(expressions?["preset"] is JObject presets)) return;
        foreach (var preset in presets)
        {
            if (!(preset.Value?["morphTargetBinds"] is JArray binds)) continue;
            var list = new List<Bind>();
            foreach (var bind in binds)
            {
                int node = (int?)bind["node"] ?? -1, index = (int?)bind["index"] ?? -1;
                float weight = Mathf.Clamp01((float?)bind["weight"] ?? 0);
                if (index < 0 || weight <= 0 || !byNode.TryGetValue(node, out var renderers)) continue;
                foreach (var renderer in renderers)
                {
                    if (renderer == null || renderer.sharedMesh == null || index >= renderer.sharedMesh.blendShapeCount) continue;
                    list.Add(new Bind { Slot = Slot(renderer, index), Index = index, Weight = weight });
                }
            }
            if (list.Count > 0) _presets[preset.Key] = list;
        }
        _values = new float[_renderers.Count];
    }

    private int Slot(SkinnedMeshRenderer renderer, int index)
    {
        long key = ((long)renderer.GetInstanceID() << 32) | (uint)index;
        if (_slots.TryGetValue(key, out int slot)) return slot;
        slot = _renderers.Count; _renderers.Add(renderer); _indices.Add(index); _slots[key] = slot;
        return slot;
    }

    // Sum every preset's contribution per blend shape, then write each shape once.
    internal void Apply(IReadOnlyDictionary<string, double> weights)
    {
        System.Array.Clear(_values, 0, _values.Length);
        foreach (var weight in weights)
        {
            if (weight.Value <= 0 || !_presets.TryGetValue(weight.Key, out var binds)) continue;
            foreach (var bind in binds) _values[bind.Slot] += (float)weight.Value * bind.Weight;
        }
        for (int i = 0; i < _renderers.Count; i++)
            if (_renderers[i] != null) _renderers[i].SetBlendShapeWeight(_indices[i], Mathf.Clamp01(_values[i]) * 100f);
    }
}
