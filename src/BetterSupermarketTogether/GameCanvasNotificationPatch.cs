using HarmonyLib;

namespace BetterSupermarketTogether;

[HarmonyPatch(typeof(GameCanvas), "Update")]
internal static class GameCanvasNotificationPatch
{
	private static void Postfix(ref bool ___inCooldown)
	{
        if (!BetterSupermarketPlugin.IsHost) { BetterSupermarketPlugin.PendingNotification = null; return; }
		string pendingNotification = BetterSupermarketPlugin.PendingNotification;
		if (pendingNotification != null)
		{
			BetterSupermarketPlugin.PendingNotification = null;
			___inCooldown = false;
			if (GameCanvas.Instance != null)
			{
				GameCanvas.Instance.CreateCanvasNotification(pendingNotification);
			}
		}
	}
}
