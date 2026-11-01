using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace BetterSupermarketTogether;

internal static class CompanionPortrait
{
    // Capture the authored model before retargeting, once per avatar load.
    internal static Sprite Render(GameObject visual, Transform head, Transform hips, List<UnityEngine.Object> resources)
    {
        GameObject copy = null, cameraObject = null;
        RenderTexture target = null;
        Texture2D texture = null;
        RenderTexture previous = RenderTexture.active;
        try
        {
            Vector3 headLocal = visual.transform.InverseTransformPoint(head.position);
            float torso = Vector3.Distance(headLocal, visual.transform.InverseTransformPoint(hips.position));
            copy = UnityEngine.Object.Instantiate(visual);
            copy.transform.SetParent(null, false);
            copy.transform.SetPositionAndRotation(new Vector3(0, -10000, 0), Quaternion.identity);
            copy.transform.localScale = Vector3.one;
            foreach (Transform part in copy.GetComponentsInChildren<Transform>(true)) part.gameObject.layer = 30;
            copy.SetActive(true);
            cameraObject = new GameObject("DeepSeek portrait camera");
            var camera = cameraObject.AddComponent<Camera>();
            camera.enabled = false;
            camera.orthographic = true;
            camera.orthographicSize = Mathf.Max(.18f, torso * .55f);
            camera.aspect = 1;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.12f, .19f, .28f, 1);
            camera.cullingMask = 1 << 30;
            camera.nearClipPlane = .01f;
            camera.farClipPlane = 4;
            var center = copy.transform.position + headLocal + Vector3.up * torso * .05f;
            // VRM +Z front becomes -Z in our importer.
            camera.transform.position = center + Vector3.back * 2;
            camera.transform.LookAt(center, Vector3.up);
            target = RenderTexture.GetTemporary(256, 256, 24, RenderTextureFormat.ARGB32);
            camera.targetTexture = target;
            camera.Render();
            RenderTexture.active = target;
            texture = new Texture2D(256, 256, TextureFormat.RGBA32, false);
            texture.ReadPixels(new Rect(0, 0, 256, 256), 0, 0);
            texture.Apply(false, true);
            var sprite = Sprite.Create(texture, new Rect(0, 0, 256, 256), new Vector2(.5f, .5f));
            resources.Add(texture);
            resources.Add(sprite);
            texture = null;
            return sprite;
        }
        catch (Exception error)
        {
            BetterSupermarketPlugin.Log.LogWarning("Companion portrait unavailable: " + error.Message);
            return null;
        }
        finally
        {
            RenderTexture.active = previous;
            if (cameraObject != null) { cameraObject.GetComponent<Camera>().targetTexture = null; UnityEngine.Object.Destroy(cameraObject); }
            if (copy != null) { copy.SetActive(false); UnityEngine.Object.Destroy(copy); }
            if (target != null) RenderTexture.ReleaseTemporary(target);
            if (texture != null) UnityEngine.Object.Destroy(texture);
        }
    }

    internal static void Attach(Transform row, int index)
    {
        var image = row?.Find("EmployeeImageBCK/EmployeeImage")?.GetComponent<Image>();
        if (image == null) return;
        var slot = image.GetComponent<CompanionPortraitSlot>() ?? image.gameObject.AddComponent<CompanionPortraitSlot>();
        slot.Set(image, index);
    }

    internal static void AttachRows(GameObject parent, NPC_Manager manager)
    {
        if (parent == null || manager?.hiredEmployeesData == null) return;
        int count = manager.hiredEmployeesData.Length;
        // Native Destroy is deferred: old rows can coexist until the frame ends.
        int start = parent.transform.childCount - count;
        if (start < 0) return;
        for (int i = 0; i < count; i++)
            if (!string.IsNullOrWhiteSpace(manager.hiredEmployeesData[i])) Attach(parent.transform.GetChild(start + i), i);
    }
}

internal sealed class CompanionPortraitSlot : MonoBehaviour
{
    private Image _image;
    private Sprite _original, _applied;
    private int _index;
    private float _next;
    internal void Set(Image image, int index)
    {
        Restore();
        _image = image; _index = index; _original = image.sprite;
        Refresh();
    }
    private void LateUpdate()
    {
        if (Time.unscaledTime < _next) return;
        _next = Time.unscaledTime + .2f;
        Refresh();
    }
    private void Refresh()
    {
        if (_image == null) return;
        if (!ReferenceEquals(_image.sprite, _applied)) _original = _image.sprite;
        var companion = BetterSupermarketPlugin.Instance?.Companion;
        var manager = NPC_Manager.Instance;
        bool bound = BetterSupermarketPlugin.EnablePlugin && BetterSupermarketPlugin.IsHost &&
            companion != null && companion.BoundIndex == _index && companion.BoundEmployee != null &&
            !companion.BoundEmployee.employeeDismissed && manager?.employeesArray != null &&
            _index >= 0 && _index < manager.employeesArray.Length &&
            manager.employeesArray[_index] == companion.BoundEmployee.gameObject;
        Sprite portrait = bound ? companion.Portrait : null;
        _image.sprite = portrait != null ? portrait : _original;
        _applied = portrait;
    }
    private void Restore()
    {
        if (_image != null && !ReferenceEquals(_applied, null) && ReferenceEquals(_image.sprite, _applied)) _image.sprite = _original;
        _applied = null;
    }
    private void OnDisable() => Restore();
    private void OnDestroy() => Restore();
}

[HarmonyPatch(typeof(EmployeesDataGeneration), "SpawnUIAssignment")]
internal static class CompanionAssignmentPortraitPatch
{
    private static void Postfix(EmployeesDataGeneration __instance) => CompanionPortrait.AttachRows(__instance.assignmentParentOBJ, __instance.GetComponent<NPC_Manager>());
}

[HarmonyPatch(typeof(EmployeesDataGeneration), "SpawnUIDismiss")]
internal static class CompanionDismissPortraitListPatch
{
    private static void Postfix(EmployeesDataGeneration __instance) => CompanionPortrait.AttachRows(__instance.dismissParentOBJ, __instance.GetComponent<NPC_Manager>());
}

[HarmonyPatch(typeof(EmployeesDataGeneration), "SetUIDismissDataInPanel")]
internal static class CompanionDetailPortraitPatch
{
    private static void Postfix(EmployeesDataGeneration __instance, int __0)
    {
        if (__instance.dismissDataPanel != null) CompanionPortrait.Attach(__instance.dismissDataPanel.transform, __0);
    }
}
