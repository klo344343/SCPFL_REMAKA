using System.Collections.Generic;
using MEC;
using Mirror;
using UnityEngine;
using UnityEngine.Networking;

public class TeslaGate : NetworkBehaviour
{
	public Vector3 localPosition;

	public Vector3 localRotation;

	public Vector3 sizeOfKiller;

	public float sizeOfTrigger;

	public GameObject[] killers;

	public AudioSource source;

	public AudioClip clip_warmup;

	public AudioClip clip_shock;

	public LayerMask killerMask;

	public bool showGizmos;

	private bool inProgress;

	private bool next079burst;

	public GameObject particles;

	private void ServerSideCode()
	{
		if (!inProgress && PlayersInRange(false).Length > 0)
		{
			RpcPlayAnimation();
		}
	}

	private void ClientSideCode()
	{
		transform.SetLocalPositionAndRotation(localPosition, Quaternion.Euler(localRotation));
        GetComponent<Renderer>().enabled = true;
	}

	[ClientRpc]
	private void RpcPlayAnimation()
	{
		Timing.RunCoroutine(_PlayAnimation(), Segment.FixedUpdate);
	}

	[ClientRpc]
	public void RpcInstantBurst()
	{
		next079burst = true;
		if (!inProgress)
		{
			Timing.RunCoroutine(_PlayAnimation(), Segment.FixedUpdate);
		}
	}

	private IEnumerator<float> _PlayAnimation()
	{
		inProgress = true;
		source.PlayOneShot(clip_warmup);
		for (int i = 1; i <= ((!next079burst) ? 30 : 5); i++)
		{
			yield return 0f;
		}
		source.PlayOneShot(clip_shock);
		particles.SetActive(true);
		bool wasIn079 = next079burst;
		for (int j = 1; j <= 30; j++)
		{
			PlayerStats[] array = PlayersInRange(true);
			foreach (PlayerStats playerStats in array)
			{
				if (playerStats.isLocalPlayer)
				{
					playerStats.CmdTesla();
					AchievementManager.Achieve("electrocuted");
				}
			}
			yield return 0f;
		}
		particles.SetActive(false);
		for (int l = 1; l < ((!next079burst) ? 20 : 5); l++)
		{
			yield return 0f;
		}
		if (wasIn079)
		{
			next079burst = false;
		}
		inProgress = false;
	}

	private PlayerStats[] PlayersInRange(bool hurtRange)
	{
		List<PlayerStats> list = new List<PlayerStats>();
		if (hurtRange)
		{
			GameObject[] array = killers;
			foreach (GameObject gameObject in array)
			{
				Collider[] array2 = Physics.OverlapBox(gameObject.transform.position + Vector3.up * (sizeOfKiller.y / 2f), sizeOfKiller / 2f, default(Quaternion), killerMask);
				Collider[] array3 = array2;
				foreach (Collider collider in array3)
				{
					PlayerStats componentInParent = collider.GetComponentInParent<PlayerStats>();
					if (componentInParent != null && componentInParent.ccm.curClass != 2)
					{
						list.Add(componentInParent);
					}
				}
			}
		}
		else
		{
			GameObject[] players = PlayerManager.singleton.players;
			foreach (GameObject gameObject2 in players)
			{
				if (Vector3.Distance(base.transform.position, gameObject2.transform.position) < sizeOfTrigger && gameObject2.GetComponent<CharacterClassManager>().curClass != 2)
				{
					list.Add(gameObject2.GetComponent<PlayerStats>());
				}
			}
		}
		return list.ToArray();
	}

	private void OnDrawGizmosSelected()
	{
		if (showGizmos)
		{
			Gizmos.color = new Color(1f, 0f, 0f, 0.2f);
			GameObject[] array = killers;
			foreach (GameObject gameObject in array)
			{
				Gizmos.DrawCube(gameObject.transform.position + Vector3.up * (sizeOfKiller.y / 2f), sizeOfKiller);
			}
			Gizmos.color = new Color(1f, 1f, 0f, 0.2f);
			Gizmos.DrawSphere(base.transform.position, sizeOfTrigger);
		}
	}

	private void Update()
	{
		if (NetworkServer.active)
		{
			ServerSideCode();
		}
		else
		{
			ClientSideCode();
		}
	}
}
