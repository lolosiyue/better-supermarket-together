using HarmonyLib;
using UnityEngine;
using UnityEngine.AI;

namespace BetterSupermarketTogether;

[HarmonyPatch(typeof(CleanerRobot), "FixedUpdate")]
internal static class CleanerRobotBehaviorPatch
{
	internal static Vector3 LastTrashPosition = Vector3.zero;

	internal static bool HasTrashPosition;

	private static bool Prefix(CleanerRobot __instance, GameObject ___originDesk, ref int ___state, ref int ___currentCounter, Transform ___trashParentTransform, NavMeshAgent ___agent, ref GameObject ___currentChasedTrash)
	{
		if (!BetterSupermarketPlugin.EnablePlugin || !BetterSupermarketPlugin.CleanerPatrolEnabled)
		{
			return true;
		}
		if (!___agent.enabled)
		{
			return false;
		}
		if (___currentCounter < 20)
		{
			___currentCounter++;
			return false;
		}
		___currentCounter = 0;
		if (!___originDesk)
		{
			if ((bool)___currentChasedTrash)
			{
				___currentChasedTrash.GetComponent<TrashSpawn>().cleanerIsAfterIt = false;
			}
			GameData.Instance.GetComponent<NetworkSpawner>().CmdDestroyBox(__instance.gameObject);
			return false;
		}
		if (___state != 2)
		{
			GameObject gameObject = NearestUnclaimedTrash(___trashParentTransform, __instance.transform.position);
			if ((bool)gameObject)
			{
				___currentChasedTrash = gameObject;
				gameObject.GetComponent<TrashSpawn>().cleanerIsAfterIt = true;
				Vector3 position = gameObject.transform.position;
				if (NavMesh.SamplePosition(position, out var hit, 1f, -1) && hit.distance > 0.02f)
				{
					position = hit.position;
				}
				___state = 2;
				___agent.destination = position;
				LastTrashPosition = gameObject.transform.position;
				HasTrashPosition = true;
				return false;
			}
		}
		if (___agent.pathPending || !(___agent.remainingDistance <= ___agent.stoppingDistance) || (___agent.hasPath && ___agent.velocity.sqrMagnitude != 0f))
		{
			return false;
		}
		if (___state == 2)
		{
			if ((bool)___currentChasedTrash)
			{
				___currentChasedTrash.GetComponent<TrashSpawn>().ClearTrashFromCleanerRobot();
				___currentChasedTrash = null;
			}
			___state = 0;
		}
		else
		{
			GameData instance = GameData.Instance;
			if (instance != null && instance.timeOfDay > 22.5f)
			{
				Vector3 position2 = ___originDesk.transform.position;
				if (NavMesh.SamplePosition(position2, out var hit2, 1f, -1) && hit2.distance > 0.02f)
				{
					position2 = hit2.position;
				}
				if (Vector3.Distance(__instance.transform.position, position2) > 1f)
				{
					___agent.destination = position2;
				}
				___state = 0;
			}
			else
			{
				Vector3 vector = (HasTrashPosition ? LastTrashPosition : ___originDesk.transform.position);
				Vector3 vector2 = Random.insideUnitCircle.normalized * Random.Range(3f, BetterSupermarketPlugin.CleanerPatrolRadius);
				if (NavMesh.SamplePosition(vector + new Vector3(vector2.x, 0f, vector2.y), out var hit3, 3f, -1))
				{
					___agent.destination = hit3.position;
				}
				___state = 0;
			}
		}
		return false;
	}

	private static GameObject NearestUnclaimedTrash(Transform trashParent, Vector3 from)
	{
		int childCount = trashParent.transform.childCount;
		float num = 500f;
		GameObject result = null;
		for (int i = 0; i < childCount; i++)
		{
			Transform child = trashParent.transform.GetChild(i);
			if (!child.GetComponent<TrashSpawn>().cleanerIsAfterIt)
			{
				float num2 = Vector3.Distance(from, child.position);
				if (num2 < num)
				{
					num = num2;
					result = child.gameObject;
				}
			}
		}
		return result;
	}
}
