using AntiFaker;
using Mirror;
using UnityEngine;
using UnityEngine.Networking;

public class FallDamage : NetworkBehaviour
{
	public bool isGrounded = true;

	public bool isCloseToGround = true;

	private LayerMask groundMask;

	public static LayerMask staticGroundMask;

	[SerializeField]
	private float groundMaxDistance = 1.3f;

	public AudioClip sound;

	public AudioSource sfxsrc;

	internal float previousHeight;

	public AnimationCurve damageOverDistance;

	private CharacterClassManager ccm;

	public string zone;

	private static Vector3 posG;

	private static Vector3 posCG;

	public static readonly Vector3 GroundCheckSize;

	public static readonly Vector3 CloseGroundCheckSize;

	private static int kTargetRpcTargetAchieve;

	private static int kRpcRpcDoSound;

	private void Start()
	{
		ccm = GetComponent<CharacterClassManager>();
		groundMask = GetComponent<AntiFakeCommands>().mask;
		staticGroundMask = groundMask;
	}

	private void Update()
	{
		if (!base.isLocalPlayer)
		{
			return;
		}
		CalculateGround();
		RaycastHit hitInfo;
		if (Physics.Raycast(new Ray(base.transform.position, Vector3.down), out hitInfo, groundMaxDistance, groundMask) && zone != hitInfo.transform.root.name)
		{
			zone = hitInfo.transform.root.name;
			if (zone.Contains("Heavy"))
			{
				SoundtrackManager.singleton.mainIndex = 1;
			}
			else if (zone.Contains("Out"))
			{
				SoundtrackManager.singleton.mainIndex = 2;
			}
			else
			{
				SoundtrackManager.singleton.mainIndex = 0;
			}
		}
	}

	public void CalculateGround()
	{
		if (TutorialManager.status || ccm.curClass < 0 || ccm.curClass == 2)
		{
			return;
		}
		bool flag = CheckIfGrounded(base.transform.position);
		isCloseToGround = flag || CheckIfCloseToGround();
		if (flag == isGrounded)
		{
			return;
		}
		isGrounded = flag;
		if (!(ccm.aliveTime < 5f) && NetworkServer.active)
		{
			if (isGrounded)
			{
				OnTouchdown();
			}
			else
			{
				OnLoseContactWithGround();
			}
		}
	}

	public static bool CheckIfGrounded(Vector3 pos)
	{
		pos.y -= 0.8f;
		posG = pos;
		posCG = pos;
		posCG.y -= 0.8f;
		return Physics.OverlapBox(pos, GroundCheckSize, new Quaternion(0f, 0f, 0f, 0f), staticGroundMask).Length != 0;
	}

	public static bool CheckIfCloseToGround()
	{
		return Physics.OverlapBox(posCG, CloseGroundCheckSize, new Quaternion(0f, 0f, 0f, 0f), staticGroundMask).Length != 0;
	}

	public static bool CheckUnsafePosition(Vector3 pos)
	{
		return Physics.OverlapBox(pos, new Vector3(0.6f, 1.2f, 0.6f), new Quaternion(0f, 0f, 0f, 0f), staticGroundMask).Length == 0 && Physics.Raycast(pos, Vector3.down, 10f, staticGroundMask);
	}

	private void OnLoseContactWithGround()
	{
		previousHeight = base.transform.position.y;
	}

	private void OnTouchdown()
	{
		float num = damageOverDistance.Evaluate(previousHeight - base.transform.position.y);
		if (num > 5f && ccm.klasy[ccm.curClass].team != Team.SCP && !ccm.GodMode)
		{
			if ((float)GetComponent<PlayerStats>().Health - num <= 0f)
			{
				TargetAchieve(ccm.connectionToClient);
			}
			RpcDoSound(base.transform.position, num);
			ccm.RpcPlaceBlood(base.transform.position, 0, Mathf.Clamp(num / 30f, 0.8f, 2f));
			GetComponent<PlayerStats>().HurtPlayer(new PlayerStats.HitInfo(Mathf.Abs(num), "WORLD", DamageTypes.Falldown, 0), base.gameObject);
		}
	}

	[TargetRpc]
	private void TargetAchieve(NetworkConnection conn)
	{
		AchievementManager.Achieve("gravity");
	}

	[ClientRpc]
	private void RpcDoSound(Vector3 pos, float dmg)
	{
		sfxsrc.PlayOneShot(sound);
	}
}
