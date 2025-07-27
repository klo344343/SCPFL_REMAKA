using System.Collections.Generic;
using Dissonance.Integrations.UNet_HLAPI;
using MEC;
using Mirror;
using RemoteAdmin;
using UnityEngine;
using UnityEngine.Networking;

public class Scp049_2PlayerScript : NetworkBehaviour
{
	[Header("Player Properties")]
	public Transform PlayerCameraGameObject;

	public Animator animator;

	public bool iAm049_2;

	public bool sameClass;

	[Header("Attack")]
	public float distance = 2.4f;

	public int damage = 60;

	[Header("Boosts")]
	public AnimationCurve multiplier;

	private static int kCmdCmdHurtPlayer;

	private static int kCmdCmdShootAnim;

	private static int kRpcRpcShootAnim;

	private void Start()
	{
		if (base.isLocalPlayer)
		{
			Timing.RunCoroutine(_UpdateInput(), Segment.FixedUpdate);
		}
	}

	public void Init(int classID, Class c)
	{
		sameClass = c.team == Team.SCP;
		iAm049_2 = classID == 10;
		animator.gameObject.SetActive(base.isLocalPlayer && iAm049_2);
	}

	private IEnumerator<float> _UpdateInput()
	{
		while (this != null)
		{
			if (iAm049_2 && Input.GetKey(NewInput.GetKey("Shoot")))
			{
				float mt = multiplier.Evaluate(GetComponent<PlayerStats>().GetHealthPercent());
				CmdShootAnim();
				animator.SetTrigger("Shoot");
				animator.speed = mt;
				yield return Timing.WaitForSeconds(0.65f / mt);
				Attack();
				yield return Timing.WaitForSeconds(1f / mt);
			}
			yield return 0f;
		}
	}

	private void Attack()
	{
		RaycastHit hitInfo;
		if (Physics.Raycast(PlayerCameraGameObject.transform.position, PlayerCameraGameObject.transform.forward, out hitInfo, distance))
		{
			Scp049_2PlayerScript scp049_2PlayerScript = hitInfo.transform.GetComponent<Scp049_2PlayerScript>();
			if (scp049_2PlayerScript == null)
			{
				scp049_2PlayerScript = hitInfo.transform.GetComponentInParent<Scp049_2PlayerScript>();
			}
			if (scp049_2PlayerScript != null && !scp049_2PlayerScript.sameClass)
			{
				Hitmarker.Hit();
				CmdHurtPlayer(hitInfo.transform.gameObject, GetComponent<HlapiPlayer>().PlayerId);
			}
		}
	}

	[Command(channel = 2)]
	private void CmdHurtPlayer(GameObject ply, string id)
	{
		if (Vector3.Distance(GetComponent<PlyMovementSync>().CurrentPosition, ply.transform.position) <= distance * 1.5f && iAm049_2)
		{
			Vector3 position = ply.transform.position;
			GetComponent<PlayerStats>().HurtPlayer(new PlayerStats.HitInfo(damage, GetComponent<NicknameSync>().myNick + " (" + GetComponent<CharacterClassManager>().SteamId + ")", DamageTypes.Scp0492, GetComponent<QueryProcessor>().PlayerId), ply);
			GetComponent<CharacterClassManager>().RpcPlaceBlood(position, 0, (ply.GetComponent<CharacterClassManager>().curClass != 2) ? 0.5f : 1.3f);
		}
	}

	[Command(channel = 1)]
	private void CmdShootAnim()
	{
		RpcShootAnim();
	}

	[ClientRpc]
	private void RpcShootAnim()
	{
		GetComponent<AnimationController>().DoAnimation("Shoot");
	}
}
