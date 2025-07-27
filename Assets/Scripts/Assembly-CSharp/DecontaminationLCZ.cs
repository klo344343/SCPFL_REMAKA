using System;
using System.Collections.Generic;
using System.Linq;
using MEC;
using Mirror;
using UnityEngine;
using UnityEngine.Networking;

public class DecontaminationLCZ : NetworkBehaviour
{
	[Serializable]
	public class Announcement
	{
		public AudioClip clip;

		public float startTime;

		public string options;
	}

	public float time;

	private bool smDisableDecontamination;

	public List<Announcement> announcements = new List<Announcement>();

	private MTFRespawn mtfrespawn;

	private AlphaWarheadController alphaController;

	private PlayerStats ps;

	private CharacterClassManager ccm;

	private int curAnm;

	private static int kRpcRpcPlayAnnouncement;

	public int GetCurAnnouncement()
	{
		return curAnm;
	}

	private void Start()
	{
		ccm = GetComponent<CharacterClassManager>();
		ps = GetComponent<PlayerStats>();
		mtfrespawn = GetComponent<MTFRespawn>();
		alphaController = GetComponent<AlphaWarheadController>();
		smDisableDecontamination = ConfigFile.ServerConfig.GetBool("disable_decontamination");
	}

	private void Update()
	{
		if (!smDisableDecontamination && base.isLocalPlayer && base.name == "Host")
		{
			DoServersideStuff();
		}
	}

	private IEnumerator<float> _KillPlayersInLCZ()
	{
		Lift[] array = UnityEngine.Object.FindObjectsOfType<Lift>();
		foreach (Lift lift in array)
		{
			lift.Lock();
		}
		Door[] array2 = UnityEngine.Object.FindObjectsOfType<Door>();
		foreach (Door door in array2)
		{
			door.CloseDecontamination();
		}
		while (this != null)
		{
			yield return Timing.WaitForSeconds(0.25f);
			GameObject[] players = PlayerManager.singleton.players;
			GameObject[] array3 = players;
			foreach (GameObject gameObject in array3)
			{
				if (!(gameObject == null))
				{
					float y = gameObject.transform.position.y;
					if (y < 100f && y > -100f)
					{
						PlayerStats component = gameObject.GetComponent<PlayerStats>();
						ps.HurtPlayer(new PlayerStats.HitInfo((!component.ccm.IsHuman()) ? 10 : 2, "DECONT", DamageTypes.Decont, 0), gameObject);
					}
				}
			}
		}
	}

	[ServerCallback]
	private void DoServersideStuff()
	{
		if (!NetworkServer.active || curAnm >= announcements.Count || alphaController.inProgress || !ccm.roundStarted || !(mtfrespawn.respawnCooldown <= 0f))
		{
			return;
		}
		time += Time.deltaTime;
		if (time / 60f > announcements[curAnm].startTime)
		{
			RpcPlayAnnouncement(curAnm, GetOption("global", curAnm));
			AlphaWarheadController alphaWarheadController = alphaController;
			alphaWarheadController.timeToDetonation = alphaWarheadController.timeToDetonation + (announcements[curAnm].clip.length + 10f);
			PlayerManager.localPlayer.GetComponent<MTFRespawn>().SetDecontCooldown(announcements[curAnm].clip.length + 10f);
			if (GetOption("checkpoints", curAnm))
			{
				Invoke("CallOpenDoors", 10f);
			}
			curAnm++;
		}
	}

	private bool GetOption(string optionName, int curAnm)
	{
		string[] source = announcements[curAnm].options.Split(',');
		return source.Any((string item) => item == optionName);
	}

	private void CallOpenDoors()
	{
		DecontaminationSpeaker.OpenDoors();
	}

	[ClientRpc]
	private void RpcPlayAnnouncement(int id, bool global)
	{
		DecontaminationSpeaker.PlaySound(announcements[id].clip, global);
		if (GetOption("decontstart", id))
		{
			if (NetworkServer.active)
			{
				Timing.RunCoroutine(_KillPlayersInLCZ(), Segment.Update);
			}
			DecontaminationGas.TurnOn();
		}
	}
}
