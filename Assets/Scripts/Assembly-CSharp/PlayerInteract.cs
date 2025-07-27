using System.Linq;
using GameConsole;
using MEC;
using Mirror;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

public class PlayerInteract : NetworkBehaviour
{
	public GameObject playerCamera;

	public LayerMask mask;

	public float raycastMaxDistance;

	private CharacterClassManager _ccm;

	private ServerRoles _sr;

	private Inventory _inv;

	private string uiToggleKey = "numlock";

	private bool enableUiToggle;

	private static int kCmdCmdUse914;

	private static int kCmdCmdUseGenerator;

	private static int kCmdCmdChange914knob;

	private static int kRpcRpcUse914;

	private static int kCmdCmdUseWorkStation_Place;

	private static int kCmdCmdUseWorkStation_Take;

	private static int kCmdCmdUsePanel;

	private static int kRpcRpcLeverSound;

	private static int kCmdCmdUseElevator;

	private static int kCmdCmdSwitchAWButton;

	private static int kCmdCmdDetonateWarhead;

	private static int kCmdCmdOpenDoor;

	private static int kRpcRpcDenied;

	private static int kCmdCmdContain106;

	private static int kRpcRpcContain106;

	private void Start()
	{
		_ccm = GetComponent<CharacterClassManager>();
		_sr = GetComponent<ServerRoles>();
		_inv = GetComponent<Inventory>();
		if (base.isLocalPlayer)
		{
			enableUiToggle = ConfigFile.ServerConfig.GetBool("enable_ui_toggle");
			uiToggleKey = ConfigFile.ServerConfig.GetString("ui_toggle_key", "numlock");
		}
	}

	private void Update()
	{
		if (!base.isLocalPlayer)
		{
			return;
		}
		if (enableUiToggle && Input.GetKeyDown(uiToggleKey))
		{
			Console.singleton.AddLog("UI toggled. Press " + uiToggleKey + " to toggle it.", Color.yellow);
			GameObject gameObject = GameObject.Find("Player Crosshair Canvas");
			if (gameObject != null)
			{
				gameObject.GetComponent<Canvas>().enabled = !gameObject.GetComponent<Canvas>().enabled;
			}
			GameObject gameObject2 = GameObject.Find("Player Canvas");
			if (gameObject2 != null)
			{
				gameObject2.GetComponent<Canvas>().enabled = !gameObject2.GetComponent<Canvas>().enabled;
			}
		}
		RaycastHit hitInfo;
		if (!Input.GetKeyDown(NewInput.GetKey("Interact")) || GetComponent<CharacterClassManager>().curClass == 2 || !Physics.Raycast(playerCamera.transform.position, playerCamera.transform.forward, out hitInfo, raycastMaxDistance, mask))
		{
			return;
		}
		if (hitInfo.transform.GetComponentInParent<Door>() != null)
		{
			CmdOpenDoor(hitInfo.transform.GetComponentInParent<Door>().gameObject);
		}
		else if (hitInfo.transform.CompareTag("AW_Button"))
		{
			if (_inv.curItem != 0 && _inv.availableItems[Mathf.Clamp(_inv.curItem, 0, _inv.availableItems.Length - 1)].permissions.Any((string item) => item == "CONT_LVL_3"))
			{
				CmdSwitchAWButton();
				return;
			}
			GameObject.Find("Keycard Denied Text").GetComponent<Text>().enabled = true;
			Invoke("DisableDeniedText", 1f);
		}
		else if (hitInfo.transform.CompareTag("AW_Detonation"))
		{
			if (AlphaWarheadOutsitePanel.nukeside.enabled && !AlphaWarheadController.host.inProgress)
			{
				CmdDetonateWarhead();
			}
		}
		else if (hitInfo.transform.CompareTag("AW_Panel"))
		{
			CmdUsePanel(hitInfo.transform.name);
		}
		else if (hitInfo.transform.CompareTag("914_use"))
		{
			CmdUse914();
		}
		else if (hitInfo.transform.CompareTag("914_knob"))
		{
			CmdChange914knob();
		}
		else if (hitInfo.transform.CompareTag("ElevatorButton"))
		{
			Lift[] array = Object.FindObjectsOfType<Lift>();
			foreach (Lift lift in array)
			{
				Lift.Elevator[] elevators = lift.elevators;
				for (int num2 = 0; num2 < elevators.Length; num2++)
				{
					Lift.Elevator elevator = elevators[num2];
					if (ChckDis(elevator.door.transform.position))
					{
						CmdUseElevator(lift.transform.gameObject);
					}
				}
			}
		}
		else if (hitInfo.collider.name.StartsWith("EPS_"))
		{
			CmdUseGenerator(hitInfo.collider.name, hitInfo.collider.GetComponentInParent<Generator079>().gameObject);
		}
		else if (hitInfo.transform.CompareTag("FemurBreaker"))
		{
			CmdContain106();
		}
		else if (hitInfo.collider.CompareTag("WS"))
		{
			hitInfo.collider.GetComponentInParent<WorkStation>().UseButton(hitInfo.collider.GetComponent<Button>());
		}
	}

	[Command(channel = 4)]
	private void CmdUse914()
	{
		if (!Scp914.singleton.working && ChckDis(GameObject.FindGameObjectWithTag("914_use").transform.position))
		{
			RpcUse914();
		}
	}

	[Command(channel = 4)]
	private void CmdUseGenerator(string command, GameObject go)
	{
		if (!(go == null) && !(go.GetComponent<Generator079>() == null))
		{
			if (ChckDis(go.transform.position))
			{
				go.GetComponent<Generator079>().Interact(base.gameObject, command);
			}
			else
			{
				Debug.Log("Command aborted");
			}
		}
	}

	[Command(channel = 4)]
	private void CmdChange914knob()
	{
		if (!Scp914.singleton.working && ChckDis(GameObject.FindGameObjectWithTag("914_use").transform.position))
		{
			Scp914.singleton.ChangeKnobStatus();
		}
	}

	[ClientRpc(channel = 4)]
	private void RpcUse914()
	{
		Scp914.singleton.StartRefining();
	}

	[Command(channel = 4)]
	public void CmdUseWorkStation_Place(GameObject station)
	{
		if (ChckDis(station.transform.position))
		{
			station.GetComponent<WorkStation>().ConnectTablet(base.gameObject);
		}
	}

	[Command(channel = 4)]
	public void CmdUseWorkStation_Take(GameObject station)
	{
		if (ChckDis(station.transform.position))
		{
			station.GetComponent<WorkStation>().UnconnectTablet(base.gameObject);
		}
	}

	[Command(channel = 4)]
	private void CmdUsePanel(string n)
	{
		AlphaWarheadNukesitePanel nukeside = AlphaWarheadOutsitePanel.nukeside;
		if (ChckDis(nukeside.transform.position))
		{
			if (n.Contains("cancel"))
			{
				AlphaWarheadController.host.CancelDetonation(base.gameObject);
				ServerLogs.AddLog(ServerLogs.Modules.Warhead, "Player " + GetComponent<NicknameSync>().myNick + " (" + GetComponent<CharacterClassManager>().SteamId + ") cancelled the Alpha Warhead detonation.", ServerLogs.ServerLogType.GameEvent);
			}
			else if (n.Contains("lever") && nukeside.AllowChangeLevelState())
			{
				nukeside.Enabled = !nukeside.enabled;
                RpcPlayLeverSound();
				ServerLogs.AddLog(ServerLogs.Modules.Warhead, "Player " + GetComponent<NicknameSync>().myNick + " (" + GetComponent<CharacterClassManager>().SteamId + ") set the Alpha Warhead status to " + nukeside.enabled + ".", ServerLogs.ServerLogType.GameEvent);
			}
		}
	}

    [ClientRpc(channel = 4)]
    private void RpcPlayLeverSound()
    {
		AlphaWarheadOutsitePanel.nukeside.lever.GetComponent<AudioSource>().Play();
	}

	[Command(channel = 4)]
	private void CmdUseElevator(GameObject elevator)
	{
		Lift.Elevator[] elevators = elevator.GetComponent<Lift>().elevators;
		for (int i = 0; i < elevators.Length; i++)
		{
			Lift.Elevator elevator2 = elevators[i];
			if (ChckDis(elevator2.door.transform.position))
			{
				elevator.GetComponent<Lift>().UseLift();
			}
		}
	}

	[Command(channel = 4)]
	private void CmdSwitchAWButton()
	{
		GameObject gameObject = GameObject.Find("OutsitePanelScript");
		if (ChckDis(gameObject.transform.position) && _inv.availableItems[_inv.curItem].permissions.Any((string item) => item == "CONT_LVL_3"))
		{
			gameObject.GetComponentInParent<AlphaWarheadOutsitePanel>().SetKeycardState(true);
		}
	}

	[Command(channel = 4)]
	private void CmdDetonateWarhead()
	{
		GameObject gameObject = GameObject.Find("OutsitePanelScript");
		if (ChckDis(gameObject.transform.position) && AlphaWarheadOutsitePanel.nukeside.enabled && gameObject.GetComponent<AlphaWarheadOutsitePanel>().KeycardEntered)
		{
			AlphaWarheadController.host.StartDetonation();
			ServerLogs.AddLog(ServerLogs.Modules.Warhead, "Player " + GetComponent<NicknameSync>().myNick + " (" + GetComponent<CharacterClassManager>().SteamId + ") started the Alpha Warhead detonation.", ServerLogs.ServerLogType.GameEvent);
		}
	}

	[Command(channel = 14)]
	private void CmdOpenDoor(GameObject doorId)
	{
		Door door = doorId.GetComponent<Door>();
		if (!((door.buttons.Count != 0) ? door.buttons.Any((GameObject item) => ChckDis(item.transform.position)) : ChckDis(doorId.transform.position)))
		{
			return;
		}
		Scp096PlayerScript component = GetComponent<Scp096PlayerScript>();
		if (door.destroyedPrefab != null && (!door.IsOpen || door.curCooldown > 0f) && component.iAm096 && component.enraged == Scp096PlayerScript.RageState.Enraged)
		{
			if (!door.Locked || _sr.BypassMode)
			{
				door.SetDestroyed(true);
			}
			return;
		}
		if (_sr.BypassMode)
		{
			door.ChangeState(true);
			return;
		}
		if (door.permissionLevel.ToUpper() == "CHCKPOINT_ACC" && GetComponent<CharacterClassManager>().klasy[GetComponent<CharacterClassManager>().curClass].team == Team.SCP)
		{
			door.ChangeState();
			return;
		}
		try
		{
			if (string.IsNullOrEmpty(door.permissionLevel))
			{
				if (!door.Locked)
				{
					door.ChangeState();
				}
			}
			else if (_inv.availableItems[_inv.curItem].permissions.Any((string item) => item == door.permissionLevel))
			{
				if (!door.Locked)
				{
					door.ChangeState();
				}
				else
				{
					RpcDenied(doorId);
				}
			}
			else
			{
				RpcDenied(doorId);
			}
		}
		catch
		{
			RpcDenied(doorId);
		}
	}

	[ClientRpc(channel = 14)]
	private void RpcDenied(GameObject door)
	{
		Timing.RunCoroutine(door.GetComponent<Door>()._UpdatePosition(), Segment.Update);
	}

	private bool ChckDis(Vector3 pos, float distanceMultiplier = 1f)
	{
		if (TutorialManager.status)
		{
			return true;
		}
		return Vector3.Distance(GetComponent<PlyMovementSync>().CurrentPosition, pos) < raycastMaxDistance * 1.5f;
	}

	[Command(channel = 4)]
	private void CmdContain106()
	{
		if (!Object.FindObjectOfType<LureSubjectContainer>().allowContain || (_ccm.klasy[_ccm.curClass].team == Team.SCP && _ccm.curClass != 3) || !ChckDis(GameObject.FindGameObjectWithTag("FemurBreaker").transform.position) || Object.FindObjectOfType<OneOhSixContainer>().used || _ccm.klasy[_ccm.curClass].team == Team.RIP)
		{
			return;
		}
		bool flag = false;
		GameObject[] players = PlayerManager.singleton.players;
		foreach (GameObject gameObject in players)
		{
			if (gameObject.GetComponent<CharacterClassManager>().GodMode && gameObject.GetComponent<CharacterClassManager>().curClass == 3)
			{
				flag = true;
			}
		}
		if (flag)
		{
			return;
		}
		GameObject[] players2 = PlayerManager.singleton.players;
		foreach (GameObject gameObject2 in players2)
		{
			if (gameObject2.GetComponent<CharacterClassManager>().curClass == 3)
			{
				gameObject2.GetComponent<Scp106PlayerScript>().Contain(_ccm);
			}
		}
		RpcContain106(base.gameObject);
		Object.FindObjectOfType<OneOhSixContainer>().used = true;
	}

	[ClientRpc(channel = 4)]
	private void RpcContain106(GameObject executor)
	{
		Object.Instantiate(GetComponent<Scp106PlayerScript>().screamsPrefab);
		if (executor != base.gameObject)
		{
			return;
		}
		GameObject[] players = PlayerManager.singleton.players;
		foreach (GameObject gameObject in players)
		{
			if (gameObject.GetComponent<CharacterClassManager>().curClass == 3)
			{
				AchievementManager.Achieve("securecontainprotect");
			}
		}
	}

	private void DisableDeniedText()
	{
		GameObject.Find("Keycard Denied Text").GetComponent<Text>().enabled = false;
		HintManager.singleton.AddHint(1);
	}

	private void DisableAlphaText()
	{
		GameObject.Find("Alpha Denied Text").GetComponent<Text>().enabled = false;
		HintManager.singleton.AddHint(2);
	}

	private void DisableLockText()
	{
		GameObject.Find("Lock Denied Text").GetComponent<Text>().enabled = false;
	}
}
