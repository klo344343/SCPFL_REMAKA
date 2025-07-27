using Mirror;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Networking;

public class FlashEffect : NetworkBehaviour
{
	public CameraFilterPack_Colors_Brightness e1;

	public CameraFilterPack_TV_Vignetting e2;

	private float curP;

	[SyncVar]
	public bool sync_blind;

	public static bool isBlind;

	public bool Networksync_blind
	{
		get
		{
			return sync_blind;
		}
		[param: In]
		set
		{
			SetSyncVar(value, ref sync_blind, 1u);
		}
	}

	[Command]
	private void CmdBlind(bool value)
	{
		Networksync_blind = value;
	}

	public void Play(float power)
	{
		if (GetComponent<CharacterClassManager>().IsHuman())
		{
			curP = power;
		}
	}

	private void Update()
	{
		if (base.isLocalPlayer)
		{
			if (curP > 0f)
			{
				curP -= Time.deltaTime / 3f;
				e1.enabled = true;
				e2.enabled = true;
				e1._Brightness = Mathf.Clamp(curP * 1.25f + 1f, 1f, 2.5f);
				e2.Vignetting = Mathf.Clamp01(curP);
				e2.VignettingFull = Mathf.Clamp01(curP);
				e2.VignettingDirt = Mathf.Clamp01(curP);
			}
			else
			{
				curP = 0f;
				e1.enabled = false;
				e2.enabled = false;
			}
			isBlind = curP > 1f;
			if (isBlind != sync_blind)
			{
				CmdBlind(isBlind);
			}
		}
	}
}
