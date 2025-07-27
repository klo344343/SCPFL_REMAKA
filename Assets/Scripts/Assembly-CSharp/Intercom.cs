using System.Collections.Generic;
using System.Runtime.InteropServices;
using MEC;
using Mirror;
using RemoteAdmin;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

public class Intercom : NetworkBehaviour
{
	private CharacterClassManager ccm;

	private Transform area;

	public float triggerDistance;

	private float speechTime;

	private float cooldownAfter;

	public float speechRemainingTime;

	public float remainingCooldown;

	public bool speaking;

	public Text txt;

	[SyncVar]
	public GameObject speaker;

	public bool Muted;

	public static bool AdminSpeaking;

	public static bool LastState;

	public static Intercom host;

	public GameObject start_sound;

	public GameObject stop_sound;

	private bool intercomSupported = true;

	private string content = string.Empty;

	private bool inUse;

	private bool isTransmitting;

	private void SetSpeaker(GameObject go)
	{
		speaker = go;
	}

	private void Log(string s)
	{
	}

	private IEnumerator<float> _StartTransmitting(GameObject sp)
	{
		if (!intercomSupported)
		{
			yield break;
		}
		speaking = true;
		if (sp.GetComponent<CharacterClassManager>().IntercomMuted || sp.GetComponent<CharacterClassManager>().Muted || MuteHandler.QueryPersistantMute(sp.GetComponent<CharacterClassManager>().SteamId))
		{
			Muted = true;
			remainingCooldown = 3f;
			while (remainingCooldown >= 0f)
			{
				remainingCooldown -= Time.deltaTime;
				yield return 0f;
			}
			Muted = false;
			speaking = false;
			inUse = false;
			yield break;
		}
		RpcPlaySound(true, sp.GetComponent<QueryProcessor>().PlayerId);
		Log("Beep beep!");
		yield return Timing.WaitForSeconds(2f);
		SetSpeaker(sp);
		Log("Speaker set!");
		bool wasAdmin = AdminSpeaking;
		if (AdminSpeaking)
		{
			while (speaker != null)
			{
				yield return 0f;
			}
		}
		else if (sp.GetComponent<ServerRoles>().BypassMode)
		{
			Log("Timer NOT set (bypass mode)! IsNull: " + (speaker == null) + " AllowSpeak:" + ServerAllowToSpeak());
			speechRemainingTime = -77f;
			while (speaker != null && sp.GetComponent<Intercom>().ServerAllowToSpeak())
			{
				yield return 0f;
			}
		}
		else
		{
			speechRemainingTime = speechTime;
			Log("Timer set! IsNull: " + (speaker == null) + " AllowSpeak:" + ServerAllowToSpeak());
			while (speechRemainingTime > 0f && speaker != null && sp.GetComponent<Intercom>().ServerAllowToSpeak())
			{
				speechRemainingTime -= Timing.DeltaTime;
				yield return 0f;
			}
		}
		Log("Unlinking the current speaker!");
		if (speaker != null)
		{
			SetSpeaker(null);
		}
		Log("Beeeeep!");
		RpcPlaySound(false, 0);
		speaking = false;
		if (!wasAdmin)
		{
			remainingCooldown = cooldownAfter;
			while (remainingCooldown >= 0f)
			{
				remainingCooldown -= Time.deltaTime;
				yield return 0f;
			}
		}
		if (!speaking)
		{
			inUse = false;
		}
	}

	private void Start()
	{
		if (NonFacilityCompatibility.currentSceneSettings.voiceChatSupport != NonFacilityCompatibility.SceneDescription.VoiceChatSupportMode.FullySupported)
		{
			intercomSupported = false;
			return;
		}
		txt = GameObject.Find("IntercomMonitor").GetComponent<Text>();
		ccm = GetComponent<CharacterClassManager>();
		area = GameObject.Find("IntercomSpeakingZone").transform;
		speechTime = ConfigFile.ServerConfig.GetInt("intercom_max_speech_time", 20);
		cooldownAfter = ConfigFile.ServerConfig.GetInt("intercom_cooldown", 180);
		Timing.RunCoroutine(_FindHost());
		Timing.RunCoroutine(_CheckForInput());
		if (base.isLocalPlayer && base.isServer)
		{
			InvokeRepeating("RefreshText", 5f, 7f);
		}
	}

	private void RefreshText()
	{
		RpcUpdateText(content);
	}

	private IEnumerator<float> _FindHost()
	{
		while (host == null)
		{
			GameObject h = GameObject.Find("Host");
			if (h != null)
			{
				host = h.GetComponent<Intercom>();
			}
			yield return 0f;
		}
	}

	[ClientRpc]
	public void RpcPlaySound(bool start, int transmitterID)
	{
		if (PlayerManager.localPlayer.GetComponent<QueryProcessor>().PlayerId == transmitterID)
		{
			AchievementManager.Achieve("isthisthingon");
		}
		GameObject obj = Object.Instantiate((!start) ? stop_sound : start_sound);
		Object.Destroy(obj, 10f);
	}

	private void Update()
	{
		if (intercomSupported && base.isLocalPlayer && base.isServer)
		{
			UpdateText();
		}
	}

	private void UpdateText()
	{
		if (Muted)
		{
			content = "YOU ARE MUTED BY ADMIN";
		}
		else if (AdminSpeaking)
		{
			content = "ADMIN IS USING\nTHE INTERCOM NOW";
		}
		else if (remainingCooldown > 0f)
		{
			content = "RESTARTING\n" + Mathf.CeilToInt(remainingCooldown);
		}
		else if (speaker != null)
		{
			if (speechRemainingTime == -77f)
			{
				content = "TRANSMITTING...\nBYPASS MODE";
			}
			else
			{
				content = "TRANSMITTING...\nTIME LEFT - " + Mathf.CeilToInt(speechRemainingTime);
			}
		}
		else
		{
			content = "READY";
		}
		if (content != txt.text)
		{
			RpcUpdateText(content);
		}
		if (AdminSpeaking != LastState)
		{
			LastState = AdminSpeaking;
			RpcUpdateAdminStatus(AdminSpeaking);
		}
	}

	[ClientRpc(channel = 2)]
	private void RpcUpdateText(string t)
	{
		try
		{
			txt.text = t;
		}
		catch
		{
		}
	}

	[ClientRpc(channel = 2)]
	private void RpcUpdateAdminStatus(bool status)
	{
		AdminSpeaking = status;
	}

	public void RequestTransmission(GameObject spk)
	{
		if (spk == null)
		{
			SetSpeaker(null);
		}
		else if ((remainingCooldown <= 0f && !inUse) || (spk.GetComponent<ServerRoles>().BypassMode && !speaking))
		{
			speaking = true;
			remainingCooldown = -1f;
			inUse = true;
			Timing.RunCoroutine(_StartTransmitting(spk), Segment.Update);
		}
	}

	private IEnumerator<float> _CheckForInput()
	{
		if (base.isLocalPlayer)
		{
			while (this != null)
			{
				if (AdminSpeaking)
				{
					yield return 0f;
					continue;
				}
				if (host != null)
				{
					if (ClientAllowToSpeak() && host.speaker == null)
					{
						CmdSetTransmit(true);
					}
					if (!ClientAllowToSpeak() && host.speaker == base.gameObject)
					{
						yield return Timing.WaitForSeconds(1f);
						if (!ClientAllowToSpeak())
						{
							CmdSetTransmit(false);
						}
					}
				}
				yield return 0f;
			}
		}
		else
		{
			yield return 0f;
		}
	}

	private bool ClientAllowToSpeak()
	{
		try
		{
			return Vector3.Distance(base.transform.position, area.position) < triggerDistance && Input.GetKey(NewInput.GetKey("Voice Chat")) && ccm.klasy[ccm.curClass].team != Team.SCP;
		}
		catch
		{
			return false;
		}
	}

	private bool ServerAllowToSpeak()
	{
		return Vector3.Distance(base.transform.position, area.position) < triggerDistance && ccm.klasy[ccm.curClass].team != Team.SCP;
	}

	[Command(channel = 2)]
	private void CmdSetTransmit(bool player)
	{
		if (AdminSpeaking)
		{
			return;
		}
		if (player)
		{
			if (ServerAllowToSpeak())
			{
				host.RequestTransmission(base.gameObject);
			}
		}
		else if (host.speaker == base.gameObject)
		{
			host.RequestTransmission(null);
		}
	}
}
