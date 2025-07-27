using System.Collections.Generic;
using System.Runtime.InteropServices;
using Dissonance;
using Mirror;
using RemoteAdmin;
using UnityEngine;
using UnityEngine.Networking;

public class Scp939PlayerScript : NetworkBehaviour
{
	public bool iAm939;

	public bool sameClass;

	public LayerMask normalVision;

	public LayerMask scpVision;

	public Camera visionCamera;

	public Behaviour[] visualEffects;

	public LayerMask attackMask;

	public AudioClip[] attackSounds;

	public float attackDistance;

	[SyncVar]
	public float speedMultiplier;

	private Camera PlayerCameraGameObject;

	public static List<Scp939PlayerScript> instances;

	[SyncVar]
	public bool usingHumanChat;

	private bool prevuhc;

	private KeyCode speechKey;

	private float cooldown;

	private static int kCmdCmdChangeHumanchatThing;

	private static int kCmdCmdShoot;

	private static int kRpcRpcShoot;

	public float NetworkspeedMultiplier
	{
		get
		{
			return speedMultiplier;
		}
		[param: In]
		set
		{
			SetSyncVar(value, ref speedMultiplier, 1u);
		}
	}

	public bool NetworkusingHumanChat
	{
		get
		{
			return usingHumanChat;
		}
		[param: In]
		set
		{
			SetSyncVar(value, ref usingHumanChat, 2u);
		}
	}

	private void Start()
	{
		if (base.isLocalPlayer)
		{
			speechKey = NewInput.GetKey("939's speech");
			instances = new List<Scp939PlayerScript>();
			PlayerCameraGameObject = GetComponent<Scp049PlayerScript>().PlayerCameraGameObject.GetComponent<Camera>();
		}
	}

	public void Init(int classID, Class c)
	{
		sameClass = c.team == Team.SCP;
		iAm939 = c.fullName.Contains("939");
		if (iAm939 && !instances.Contains(this))
		{
			instances.Add(this);
		}
		if (base.isLocalPlayer)
		{
			Behaviour[] array = visualEffects;
			foreach (Behaviour behaviour in array)
			{
				behaviour.enabled = iAm939;
			}
			PlayerCameraGameObject.renderingPath = ((!iAm939) ? RenderingPath.DeferredShading : RenderingPath.VertexLit);
			PlayerCameraGameObject.cullingMask = ((!iAm939) ? normalVision : scpVision);
			visionCamera.gameObject.SetActive(iAm939);
			visionCamera.fieldOfView = PlayerCameraGameObject.fieldOfView;
			visionCamera.farClipPlane = PlayerCameraGameObject.farClipPlane;
		}
	}

	private void Update()
	{
		CheckInstances();
		CheckForInput();
		ServersideCode();
		ClientsideMisc();
	}

	private void ServersideCode()
	{
		if (NetworkServer.active)
		{
			if (cooldown >= 0f)
			{
				cooldown -= Time.deltaTime;
			}
			if (speedMultiplier > 1f)
			{
				NetworkspeedMultiplier = speedMultiplier - Time.deltaTime / 3f;
			}
			if (speedMultiplier < 1f)
			{
				NetworkspeedMultiplier = 1f;
			}
		}
	}

	private void ClientsideMisc()
	{
		if (base.isLocalPlayer)
		{
			if (iAm939)
			{
				FirstPersonController.speedMultiplier939 = speedMultiplier;
			}
			else
			{
				FirstPersonController.speedMultiplier939 = 1f;
			}
		}
	}

	private void CheckForInput()
	{
		if (base.isLocalPlayer)
		{
			if (iAm939 && Input.GetKey(NewInput.GetKey("Shoot")))
			{
				Shoot();
			}
			bool flag = Input.GetKey(speechKey) && iAm939;
			if (prevuhc != flag)
			{
				prevuhc = flag;
				CmdChangeHumanchatThing(flag);
				VoiceBroadcastTrigger.is939Speaking = flag;
			}
		}
	}

	[Command]
	private void CmdChangeHumanchatThing(bool newValue)
	{
		NetworkusingHumanChat = iAm939 && newValue;
	}

	private void Shoot()
	{
		RaycastHit hitInfo;
		if (Physics.Raycast(new Ray(PlayerCameraGameObject.transform.position, PlayerCameraGameObject.transform.forward), out hitInfo, attackDistance))
		{
			Scp939PlayerScript componentInParent = hitInfo.collider.GetComponentInParent<Scp939PlayerScript>();
			if (componentInParent != null && !componentInParent.sameClass)
			{
				CmdShoot(componentInParent.gameObject);
			}
		}
	}

	[Command]
	private void CmdShoot(GameObject target)
	{
		if (iAm939 && Vector3.Distance(target.transform.position, base.transform.position) < attackDistance * 1.2f && cooldown <= 0f)
		{
			cooldown = 1f;
			GetComponent<PlayerStats>().HurtPlayer(new PlayerStats.HitInfo(Random.Range(50, 80), GetComponent<NicknameSync>().myNick + " (" + GetComponent<CharacterClassManager>().SteamId + ")", DamageTypes.Scp939, GetComponent<QueryProcessor>().PlayerId), target);
			GetComponent<CharacterClassManager>().RpcPlaceBlood(target.transform.position, 0, 2f);
			RpcShoot();
		}
	}

	[ClientRpc]
	private void RpcShoot()
	{
		Animator component = GetComponent<CharacterClassManager>().myModel.GetComponent<Animator>();
		component.GetComponent<AudioSource>().PlayOneShot(attackSounds[Random.Range(0, attackSounds.Length)]);
		if (base.isLocalPlayer)
		{
			Hitmarker.Hit(1.5f);
		}
		else
		{
			component.SetTrigger("Attack");
		}
	}

	private void CheckInstances()
	{
		if (!(base.name == "Host"))
		{
			return;
		}
		foreach (Scp939PlayerScript instance in instances)
		{
			if (instance == null || !instance.iAm939)
			{
				instances.Remove(instance);
				break;
			}
		}
	}
}
