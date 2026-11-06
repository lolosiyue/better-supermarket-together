using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Mirror;
using UnityEngine;

namespace BetterSupermarketTogether;

// Bridges the native multiplayer chat: host messages starting with @ go to the companion,
// and replies come back as chat lines instead of only living in the F7 panel.
internal static class CompanionGameChat
{
    private struct Line { public string Speaker, Text; public bool Broadcast, Speech; public float Delay; }
    // Raised when one of her spoken lines actually appears, so the mouth moves in time with it.
    internal static System.Action<string> Spoken;
    private static readonly Queue<Line> Pending = new Queue<Line>();
    private static readonly MethodInfo ReceiveRpc = AccessTools.Method(typeof(PlayerObjectController), "RpcReceiveChatMsg");
    private static float _readyAt = -1;
    private const float LineInterval = .2f;

    // With typing on, each line appears after a pause matching its length, like someone typing.
    internal static void Say(string speaker, string answer, bool broadcast, bool typing)
    {
        var chunks = CompanionChatPolicy.Chunks(answer);
        for (int i = 0; i < chunks.Count; i++)
            Enqueue(speaker, chunks[i], broadcast, typing ? CompanionChatPolicy.TypingDelay(chunks[i], i == 0) : LineInterval, true);
    }

    // Status/errors are only for the host; other players never see them.
    internal static void Notice(string speaker, string text)
    {
        foreach (string chunk in CompanionChatPolicy.Chunks(text)) Enqueue(speaker, chunk, false, LineInterval, false);
    }

    private static void Enqueue(string speaker, string text, bool broadcast, float delay, bool speech)
    {
        if (Pending.Count >= 32) return;
        Pending.Enqueue(new Line { Speaker = CompanionChatPolicy.Plain(speaker).Replace(":", " "), Text = text, Broadcast = broadcast, Delay = delay, Speech = speech });
    }

    internal static void Clear() { Pending.Clear(); _readyAt = -1; }

    // One line at a time (never two Send_Data events in one frame), each after its own delay.
    internal static void Tick()
    {
        if (Pending.Count == 0) return;
        if (_readyAt < 0) _readyAt = Time.unscaledTime + Mathf.Max(LineInterval, Pending.Peek().Delay);
        if (Time.unscaledTime < _readyAt) return;
        _readyAt = -1;
        var line = Pending.Dequeue();
        if (line.Speech) Spoken?.Invoke(line.Text);
        try
        {
            var host = NetworkServer.active ? NetworkServer.localConnection?.identity?.GetComponent<PlayerObjectController>() : null;
            if (line.Broadcast && host != null && ReceiveRpc != null) { ReceiveRpc.Invoke(host, new object[] { line.Speaker, line.Text }); return; }
            var fsm = LobbyController.Instance?.ChatContainerOBJ?.GetComponent<PlayMakerFSM>();
            if (fsm == null) return;
            fsm.FsmVariables.GetFsmString("Message").Value = "<color=#4FB8B0>" + line.Speaker + ":</color> " + line.Text;
            fsm.SendEvent("Send_Data");
        }
        catch (System.Exception error) { BetterSupermarketPlugin.Log.LogWarning("Companion chat line failed: " + error.Message); }
    }

    internal static IEnumerable<string> PlayerNames() =>
        Object.FindObjectsByType<PlayerObjectController>(FindObjectsSortMode.None).Select(x => x.PlayerName).Where(x => !string.IsNullOrWhiteSpace(x));
}

[HarmonyPatch(typeof(PlayerObjectController), "UserCode_CmdSendMessage__String__NetworkConnectionToClient")]
internal static class CompanionGameChatPatch
{
    // Postfix: the host's own line is broadcast first, then the companion answers below it.
    private static void Postfix(string message, NetworkConnectionToClient sender)
    {
        var companion = BetterSupermarketPlugin.Instance?.Companion;
        if (companion == null || !companion.GameChatEnabled || !BetterSupermarketPlugin.IsHost) return;
        if (sender == null || sender != NetworkServer.localConnection) return;
        if (!CompanionChatPolicy.TryParse(message, companion.ChatAliases, CompanionGameChat.PlayerNames(), out string prompt)) return;
        companion.Send(prompt, true);
    }
}
