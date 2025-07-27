using System.Collections.Generic;
using Dissonance.Integrations.UNet_HLAPI;
using MEC;
using Mirror;
using RemoteAdmin;
using UnityEngine;
using UnityEngine.Networking;

public class MicroHID_GFX : NetworkBehaviour
{
	public Animator anim;

	public GameObject cam;

	public bool count;

	public bool serverUnlocked;

	private float damageGiven;

	private InventoryDisplay invdis;

	public bool onFire;

	private HlapiPlayer plyid;

	private PlayerManager pmng;

	public Light[] progress;

	public float range;

	public float serverTime;

	public AudioSource shotSource;

	public ParticleSystem teslaFX;

	private static int kCmdCmdHurtPlayersInRange;

	private static int kCmdCmdUse;

	private static int kRpcRpcSyncAnim;

	private void Start()
	{
		pmng = PlayerManager.singleton;
		invdis = Object.FindObjectOfType<InventoryDisplay>();
		plyid = GetComponent<HlapiPlayer>();
	}

	private void Update()
	{
		if (!ServerStatic.IsDedicated && base.isLocalPlayer && Input.GetKeyDown(NewInput.GetKey("Shoot")) && GetComponent<Inventory>().curItem == 16 && !onFire && Inventory.inventoryCooldown <= 0f && GetComponent<Inventory>().items[GetComponent<Inventory>().GetItemIndex()].durability > 0f && !Cursor.visible)
		{
			onFire = true;
			CmdUse();
			Timing.RunCoroutine(_PlayAnimation(), Segment.Update);
		}
		if (count && base.isServer)
		{
			serverTime += Time.deltaTime;
			if (serverTime > 8.5f)
			{
				serverUnlocked = true;
			}
			if (serverTime > 16f)
			{
				serverUnlocked = false;
				count = false;
				serverTime = 0f;
			}
		}
	}

	private IEnumerator<float> _PlayAnimation()
	{
		damageGiven = 0f;
		anim.SetTrigger("Shoot");
		shotSource.Play();
		Light[] array = progress;
		foreach (Light light in array)
		{
			light.intensity = 0f;
		}
		GlowLight(0);
		yield return Timing.WaitForSeconds(2.2f);
		GlowLight(1);
		yield return Timing.WaitForSeconds(2.2f);
		GlowLight(2);
		yield return Timing.WaitForSeconds(2.2f);
		GlowLight(3);
		GlowLight(5);
		yield return Timing.WaitForSeconds(2.2f);
		GlowLight(4);
		yield return Timing.WaitForSeconds(0.6f);
		teslaFX.Play();
		GameObject[] players = pmng.players;
		for (int j = 0; j < 20; j++)
		{
			GameObject[] array2 = players;
			foreach (GameObject gameObject in array2)
			{
				RaycastHit hitInfo;
				if (gameObject != null && Vector3.Dot(cam.transform.forward, (cam.transform.position - gameObject.transform.position).normalized) < -0.92f && Physics.Raycast(cam.transform.position, (gameObject.transform.position - cam.transform.position).normalized, out hitInfo, range) && hitInfo.transform.name == gameObject.name)
				{
					Hitmarker.Hit(2.3f);
					CmdHurtPlayersInRange(gameObject);
				}
			}
			yield return Timing.WaitForSeconds(0.25f);
		}
		onFire = false;
		Light[] array3 = progress;
		foreach (Light light2 in array3)
		{
			light2.intensity = 0f;
		}
	}

	[Command(channel = 11)]
	private void CmdHurtPlayersInRange(GameObject ply)
	{
		if (GetComponent<Inventory>().curItem == 16 && serverUnlocked && Vector3.Distance(GetComponent<PlyMovementSync>().CurrentPosition, ply.transform.position) < range && GetComponent<WeaponManager>().GetShootPermission(ply.GetComponent<CharacterClassManager>()))
		{
			bool flag = ply.GetComponent<CharacterClassManager>().klasy[ply.GetComponent<CharacterClassManager>().curClass].team == Team.SCP;
			if (GetComponent<PlayerStats>().HurtPlayer(new PlayerStats.HitInfo(Random.Range(200, 300), string.Empty, DamageTypes.Tesla, GetComponent<QueryProcessor>().PlayerId), ply) && flag)
			{
				PlayerManager.localPlayer.GetComponent<PlayerStats>().TargetAchieve(base.connectionToClient, "zap");
			}
		}
	}

	[Command(channel = 2)]
	private void CmdUse()
	{
		if (count)
		{
			return;
		}
		Inventory component = GetComponent<Inventory>();
		if (component.curItem != 16 || component.GetItemInHand().durability == 0f)
		{
			return;
		}
		serverTime = 0f;
		count = true;
		if (component.GetItemIndex() >= 0 && component.items[component.GetItemIndex()].id == 16)
		{
			component.items.ModifyDuration(component.GetItemIndex(), 0f);
		}
		else
		{
			for (int i = 0; i < component.items.Count; i++)
			{
				if (component.items[i].id == 16)
				{
					component.items.ModifyDuration(i, 0f);
					break;
				}
			}
		}
		RpcSyncAnim();
	}

	[ClientRpc(channel = 1)]
	private void RpcSyncAnim()
	{
		if (!base.isLocalPlayer)
		{
			GetComponent<AnimationController>().PlaySound("HID_Shoot", true);
			GetComponent<AnimationController>().DoAnimation("Shoot");
		}
	}

	private void GlowLight(int id)
	{
		float targetIntensity;
		switch (id)
		{
		case 5:
			targetIntensity = 50f;
			break;
		case 4:
			targetIntensity = 6f;
			break;
		default:
			targetIntensity = 3f;
			break;
		}
		Timing.RunCoroutine(_SetLightState(targetIntensity, progress[id], (id != 5) ? 2f : 50f), Segment.FixedUpdate);
	}

	private static IEnumerator<float> _SetLightState(float targetIntensity, Light light, float speed)
	{
		while (light.intensity < targetIntensity)
		{
			light.intensity += 0.02f * speed;
			yield return 0f;
		}
	}
}
