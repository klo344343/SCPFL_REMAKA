using Mirror;
using UnityEngine;

public class FootstepSync : NetworkBehaviour
{
	private AnimationController controller;

	private CharacterClassManager ccm;

	private Scp939_VisionController visionController;

	private void Start()
	{
		visionController = GetComponent<Scp939_VisionController>();
		ccm = GetComponent<CharacterClassManager>();
		controller = GetComponent<AnimationController>();
	}

	public void SyncFoot(bool run)
	{
		if (base.isLocalPlayer)
		{
			CmdSyncFoot(run);
			AudioClip[] stepClips = ccm.klasy[ccm.curClass].stepClips;
			controller.walkSource.PlayOneShot(stepClips[Random.Range(0, stepClips.Length)], (!run) ? 0.6f : 1f);
		}
	}

	public void SyncWalk()
	{
		SyncFoot(false);
	}

	public void SyncRun()
	{
		if (ccm.klasy[ccm.curClass].team != Team.SCP || ccm.klasy[ccm.curClass].fullName.Contains("939"))
		{
			SyncFoot(true);
		}
		else
		{
			SyncFoot(false);
		}
	}

	public void SetLoundness(Team t, bool is939)
	{
		if (t != Team.SCP || is939)
		{
			switch (t)
			{
			case Team.CHI:
				break;
			case Team.RSC:
			case Team.CDP:
				controller.runSource.maxDistance = 20f;
				controller.walkSource.maxDistance = 10f;
				return;
			default:
				controller.runSource.maxDistance = 30f;
				controller.walkSource.maxDistance = 15f;
				return;
			}
		}
		controller.runSource.maxDistance = 50f;
		controller.walkSource.maxDistance = 50f;
	}

	[Command(channel = 1)]
	private void CmdSyncFoot(bool run)
	{
		visionController.MakeNoise(controller.runSource.maxDistance * ((!run) ? 0.4f : 0.7f));
		RpcSyncFoot(run);
	}

	[ClientRpc(channel = 1)]
	private void RpcSyncFoot(bool run)
	{
		if (!base.isLocalPlayer && ccm != null)
		{
			AudioClip[] stepClips = ccm.klasy[ccm.curClass].stepClips;
			if (run || ccm.klasy[ccm.curClass].team == Team.SCP)
			{
				controller.runSource.PlayOneShot(stepClips[Random.Range(0, stepClips.Length)]);
			}
			else
			{
				controller.walkSource.PlayOneShot(stepClips[Random.Range(0, stepClips.Length)]);
			}
		}
	}
}
