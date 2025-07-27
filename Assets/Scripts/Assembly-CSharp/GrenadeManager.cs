using System.Collections.Generic;
using System.Runtime.InteropServices;
using MEC;
using Mirror;
using RemoteAdmin;
using UnityEngine;
using UnityEngine.Networking;

public class GrenadeManager : NetworkBehaviour
{
	public GrenadeSettings[] availableGrenades;

	public static List<Grenade> grenadesOnScene;

	private Inventory inv;

	public static bool flashfire;

	private bool isThrowing;

	private int throwInteger;

	internal static int GrenadeChainLimit;

	internal static int GrenadeChainLengthLimit;

	[SyncVar]
	private bool _syncFlashfire;
	public bool Network_syncFlashfire
	{
		get
		{
			return _syncFlashfire;
		}
		set
		{
			SetSyncVar(value, ref _syncFlashfire, 1u);
		}
	}

	private void Start()
	{
		if (NetworkServer.active)
		{
			Network_syncFlashfire = ConfigFile.ServerConfig.GetBool("friendly_flash");
			GrenadeChainLimit = ConfigFile.ServerConfig.GetInt("grenade_chain_limit", 10);
			GrenadeChainLengthLimit = ConfigFile.ServerConfig.GetInt("grenade_chain_length_limit", 4);
		}
		inv = GetComponent<Inventory>();
		if (base.isLocalPlayer)
		{
			grenadesOnScene = new List<Grenade>();
		}
	}

	private void Update()
	{
		if (base.isLocalPlayer)
		{
			CheckForInput();
		}
		if (base.name == "Host")
		{
			flashfire = _syncFlashfire;
		}
	}

	private void CheckForInput()
	{
		bool keyDown = Input.GetKeyDown(NewInput.GetKey("Shoot"));
		bool keyDown2 = Input.GetKeyDown(NewInput.GetKey("Zoom"));
		if (isThrowing || (!keyDown && !keyDown2) || !(Inventory.inventoryCooldown <= 0f) || Cursor.visible)
		{
			return;
		}
		for (int i = 0; i < availableGrenades.Length; i++)
		{
			if (availableGrenades[i].inventoryID == inv.curItem)
			{
				isThrowing = true;
				Timing.RunCoroutine(_ThrowGrenade(i, keyDown2), Segment.FixedUpdate);
				break;
			}
		}
	}

	[Server]
	public void ChangeIntoGrenade(Pickup pickup, int id, int ti_pid, int ti_int, Vector3 dir, Vector3 pos, int chain)
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Void GrenadeManager::ChangeIntoGrenade(Pickup,System.Int32,System.Int32,System.Int32,UnityEngine.Vector3,UnityEngine.Vector3,System.Int32)' called on client");
			return;
		}
		pickup.Delete();
		RpcThrowGrenade(id, ti_pid, ti_int, dir, true, pos, false, chain);
	}

	private IEnumerator<float> _ThrowGrenade(int gId, bool slow)
	{
		GetComponent<MicroHID_GFX>().onFire = true;
		inv.availableItems[inv.curItem].firstpersonModel.GetComponent<Animator>().SetBool("Throw", true);
		for (int i = 1; (float)i <= availableGrenades[gId].throwAnimationDuration * 50f; i++)
		{
			yield return 0f;
		}
		throwInteger++;
		float throwForce = ((!slow) ? 1f : 0.5f) * availableGrenades[gId].throwForce;
		Grenade g = Object.Instantiate(availableGrenades[gId].grenadeInstance).GetComponent<Grenade>();
		g.id = GetComponent<QueryProcessor>().PlayerId + ":" + throwInteger;
		grenadesOnScene.Add(g);
		g.SyncMovement(availableGrenades[gId].GetStartPos(base.gameObject), (GetComponent<Scp049PlayerScript>().PlayerCameraGameObject.transform.forward + Vector3.up / 4f).normalized * throwForce, Quaternion.Euler(availableGrenades[gId].startRotation), availableGrenades[gId].angularVelocity);
		CmdThrowGrenade(gId, throwInteger, GetComponent<Scp049PlayerScript>().PlayerCameraGameObject.transform.forward, slow);
		inv.availableItems[inv.curItem].firstpersonModel.GetComponent<Animator>().SetBool("Throw", false);
		GetComponent<MicroHID_GFX>().onFire = false;
		inv.SetCurItem(-1);
		isThrowing = false;
	}

	[Command]
	private void CmdThrowGrenade(int id, int ti, Vector3 direction, bool slowThrow)
	{
		for (int i = 0; i < inv.items.Count; i++)
		{
			if (inv.items[i].id == availableGrenades[id].inventoryID)
			{
				RpcThrowGrenade(id, GetComponent<QueryProcessor>().PlayerId, ti, direction.normalized, false, Vector3.zero, slowThrow, 0);
				inv.items.RemoveAt(i);
				break;
			}
		}
	}

	[ClientRpc]
	private void RpcThrowGrenade(int id, int ti_pid, int ti_int, Vector3 dir, bool isEnvironmentallyTriggered, Vector3 optionalParam, bool slowThrow, int chain)
	{
		Timing.RunCoroutine(_RpcThrowGrenade(id, ti_pid, ti_int, dir, isEnvironmentallyTriggered, optionalParam, slowThrow, chain), Segment.FixedUpdate);
	}

	private IEnumerator<float> _RpcThrowGrenade(int id, int ti_pid, int ti_int, Vector3 dir, bool isEnvironmentallyTriggered, Vector3 optionalParamenter, bool slowThrow, int chain)
	{
		Grenade g = null;
		if (!base.isLocalPlayer || isEnvironmentallyTriggered)
		{
			g = Object.Instantiate(availableGrenades[id].grenadeInstance).GetComponent<Grenade>();
			g.id = ((!isEnvironmentallyTriggered) ? string.Empty : "SERVER_") + ti_pid + ":" + ti_int;
			g.chain = chain;
			grenadesOnScene.Add(g);
			float num = ((!slowThrow) ? 1f : 0.5f) * availableGrenades[id].throwForce;
			if (isEnvironmentallyTriggered)
			{
				g.SyncMovement(optionalParamenter, dir, Quaternion.Euler(Vector3.zero), Vector3.zero);
			}
			else
			{
				g.SyncMovement(base.transform.position + Vector3.up * 0.8380203f, (dir + Vector3.up / 4f).normalized * num * dir.magnitude, Quaternion.Euler(availableGrenades[id].startRotation), availableGrenades[id].angularVelocity);
			}
		}
		else
		{
			foreach (Grenade item in grenadesOnScene)
			{
				if (item.id == ti_pid + ":" + ti_int)
				{
					g = item;
				}
			}
		}
		if (!NetworkServer.active)
		{
			yield break;
		}
		for (float i = 1f; i <= availableGrenades[id].timeUnitilDetonation * 50f; i += 1f)
		{
			if (g != null)
			{
				RpcUpdate(g.id, g.transform.position, g.transform.rotation, g.transform.GetComponent<Rigidbody>().velocity, g.transform.GetComponent<Rigidbody>().angularVelocity);
			}
			yield return 0f;
		}
		if (g != null)
		{
			RpcExplode(g.id, ti_pid);
		}
	}

	[ClientRpc]
	private void RpcExplode(string id, int playerID)
	{
		foreach (Grenade item in grenadesOnScene)
		{
			if (item.id == id)
			{
				item.Explode(playerID);
				break;
			}
		}
	}

	[ClientRpc]
	private void RpcUpdate(string id, Vector3 pos, Quaternion rot, Vector3 vel, Vector3 angVel)
	{
		if (NetworkServer.active)
		{
			return;
		}
		foreach (Grenade item in grenadesOnScene)
		{
			if (item.id == id)
			{
				item.SyncMovement(pos, vel, rot, angVel);
			}
		}
	}
}
