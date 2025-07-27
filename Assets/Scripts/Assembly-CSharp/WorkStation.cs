using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using MEC;
using Mirror;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class WorkStation : NetworkBehaviour
{
	[Serializable]
	public class WorkStationScreenGroup
	{
		[Serializable]
		public class WorkStationScreen
		{
			public string label;

			public GameObject screenObject;
		}

		private string curScreen;

		public WorkStationScreen[] screens;

		private WorkStation station;

		public void SetWorkstation(WorkStation s)
		{
			station = s;
		}

		public void SetScreenByName(string _label)
		{
			if (!(curScreen == _label))
			{
				curScreen = _label;
				if (!station.GetComponent<AudioSource>().isPlaying)
				{
					station.GetComponent<AudioSource>().PlayOneShot(station.beepClip);
				}
				WorkStationScreen[] array = screens;
				foreach (WorkStationScreen workStationScreen in array)
				{
					workStationScreen.screenObject.SetActive(workStationScreen.label == _label);
				}
			}
		}
	}

	private static WorkStation updateRoot;

	private float animationCooldown;

	public Animator animator;

	public AudioClip beepClip;

	private Button[] buttons;

	private string currentGroup = "unconnected";

	[SyncVar]
	public bool isTabletConnected;

	private Transform localply;

	public float maxDistance = 3f;

	private MeshRenderer[] meshRenderers;

	public AudioClip powerOnClip;

	public AudioClip powerOffClip;

	private bool prevConn;

	private int prevGun;

	public WorkStationScreenGroup screenGroup;

	public GameObject ui_place;

	public GameObject ui_take;

	public GameObject ui_using;

	public GameObject ui_notablet;
    [SyncVar]
    private GameObject playerConnected;

    [SyncVar(hook = nameof(SetPosition))]
    public Offset position;
    private void SetPosition(Offset oldValue, Offset newValue)
    {
        position = newValue;
    }

    public bool NetworkisTabletConnected
	{
		get
		{
			return isTabletConnected;
		}
		[param: In]
		set
		{
			SetSyncVar(value, ref isTabletConnected, 1u);
		}
	}

	private void Start()
	{
		updateRoot = this;
		Timing.RunCoroutine(_Update(), Segment.Update);
		Invoke("UnmuteSource", 10f);
		meshRenderers = GetComponentsInChildren<MeshRenderer>(true);
	}

	private void UnmuteSource()
	{
		GetComponent<AudioSource>().mute = false;
	}

	private void Update()
	{
		if (base.transform.localPosition != position.position)
		{
			base.transform.SetLocalPositionAndRotation(position.position, Quaternion.Euler(position.rotation));
        }
		CheckConnectionChange();
		screenGroup.SetScreenByName(currentGroup);
		if (animationCooldown >= 0f)
		{
			animationCooldown -= Time.deltaTime;
		}
		if (ServerStatic.IsDedicated)
		{
			return;
		}
		if (localply == null)
		{
			if (PlayerManager.localPlayer != null)
			{
				localply = PlayerManager.localPlayer.transform;
			}
		}
		else
		{
			if (!isTabletConnected || !(animationCooldown < 0f))
			{
				return;
			}
			float num = Vector3.Distance(base.transform.position, localply.position);
			if (num < maxDistance)
			{
				bool flag = false;
				for (int i = 0; i < localply.GetComponent<WeaponManager>().weapons.Length; i++)
				{
					if (localply.GetComponent<WeaponManager>().weapons[i].inventoryID == localply.GetComponent<Inventory>().curItem)
					{
						flag = true;
					}
				}
				if (flag)
				{
					if (currentGroup == "mainmenu")
					{
						ChangeScreen("slots");
						GetComponent<WorkStationUpgrader>().RefreshSlotSelector();
						return;
					}
					int itemUniq = localply.GetComponent<Inventory>().itemUniq;
					if (itemUniq != prevGun)
					{
						prevGun = itemUniq;
						ChangeScreen("slots");
						GetComponent<WorkStationUpgrader>().RefreshSlotSelector();
					}
				}
				else if (currentGroup == "mods" || currentGroup == "slots")
				{
					ChangeScreen("mainmenu");
				}
			}
			else if (currentGroup == "mods" || currentGroup == "slots")
			{
				ChangeScreen("mainmenu");
			}
		}
	}

	public void ChangeScreen(string scene)
	{
		currentGroup = scene;
	}

	public void UseButton(Button button)
	{
		if (!PlayerManager.localPlayer.GetComponent<CharacterClassManager>().IsHuman())
		{
			return;
		}
		Button[] array = buttons;
		foreach (Button button2 in array)
		{
			if (button2 == button)
			{
				GetComponent<AudioSource>().PlayOneShot(beepClip);
				button2.onClick.Invoke();
			}
		}
	}

	private IEnumerator<float> _Update()
	{
		screenGroup.SetWorkstation(this);
		buttons = GetComponentsInChildren<Button>();
		yield return Timing.WaitForSeconds(1f);
		while (PlayerManager.localPlayer == null)
		{
			yield return 0f;
		}
		PlayerInteract interact = PlayerManager.localPlayer.GetComponent<PlayerInteract>();
		while (this != null && updateRoot == this)
		{
			ui_place.SetActive(false);
			ui_take.SetActive(false);
			ui_using.SetActive(false);
			ui_notablet.SetActive(false);
			GameObject lastInteraction = null;
			RaycastHit hit;
			if (interact.playerCamera != null && Physics.Raycast(interact.playerCamera.transform.position, interact.playerCamera.transform.forward, out hit, interact.raycastMaxDistance, interact.mask))
			{
				lastInteraction = hit.collider.gameObject;
			}
			if (lastInteraction != null && lastInteraction.name == "workbench")
			{
				WorkStation station = lastInteraction.GetComponentInParent<WorkStation>();
				if (station == null)
				{
					Debug.LogError(lastInteraction.name + " is null.");
					yield return 0f;
					continue;
				}
				if (station.animationCooldown <= 0f)
				{
					if (station.isTabletConnected)
					{
						if (station.CanTake(interact.gameObject))
						{
							ui_take.SetActive(true);
							TextMeshProUGUI component = ui_take.GetComponent<TextMeshProUGUI>();
							if (component.text.Contains("{KEYBINDING_INTERACTION}"))
							{
								component.text = component.text.Replace("{KEYBINDING_INTERACTION}", string.Concat("<color=yellow>", NewInput.GetKey("Interact"), "</color>"));
							}
							if (Input.GetKeyDown(NewInput.GetKey("Interact")))
							{
								interact.CmdUseWorkStation_Take(station.gameObject);
							}
						}
						else if (interact.GetComponent<Inventory>().items.Count < 8)
						{
							ui_using.SetActive(true);
						}
					}
					else if (station.CanPlace(interact.gameObject))
					{
						ui_place.SetActive(true);
						TextMeshProUGUI component2 = ui_place.GetComponent<TextMeshProUGUI>();
						if (component2.text.Contains("{KEYBINDING_INTERACTION}"))
						{
							component2.text = component2.text.Replace("{KEYBINDING_INTERACTION}", string.Concat("<color=yellow>", NewInput.GetKey("Interact"), "</color>"));
						}
						if (Input.GetKeyDown(NewInput.GetKey("Interact")))
						{
							interact.CmdUseWorkStation_Place(station.gameObject);
						}
					}
					else
					{
						ui_notablet.SetActive(true);
					}
				}
			}
			yield return 0f;
		}
	}

	private void CheckConnectionChange()
	{
		if (prevConn != isTabletConnected)
		{
			prevConn = isTabletConnected;
			Timing.RunCoroutine((!prevConn) ? _OnTabletDisconnected() : _OnTabletConnected(), Segment.FixedUpdate);
		}
	}

	private IEnumerator<float> _OnTabletConnected()
	{
		GetComponent<AudioSource>().PlayOneShot(powerOnClip);
		animationCooldown = 6.5f;
		animator.SetBool("Connected", true);
		for (int i = 0; i < 50; i++)
		{
			yield return 0f;
		}
		currentGroup = "connecting";
		while (animationCooldown > 0f)
		{
			yield return 0f;
		}
		currentGroup = "mainmenu";
	}

	private IEnumerator<float> _OnTabletDisconnected()
	{
		GetComponent<AudioSource>().PlayOneShot(powerOffClip);
		animationCooldown = 3.5f;
		animator.SetBool("Connected", false);
		currentGroup = "closingsession";
		while (animationCooldown > 0f)
		{
			yield return 0f;
		}
		currentGroup = "unconnected";
	}

	public bool CanPlace(GameObject tabletOwner)
	{
		CharacterClassManager component = tabletOwner.GetComponent<CharacterClassManager>();
		if (playerConnected != null || isTabletConnected || (component != null && component.klasy[component.curClass].team == Team.SCP))
		{
			return false;
		}
		return HasInInventory(tabletOwner);
	}

	private bool HasInInventory(GameObject tabletOwner)
	{
		foreach (Inventory.SyncItemInfo item in tabletOwner.GetComponent<Inventory>().items)
		{
			if (item.id == 19)
			{
				return true;
			}
		}
		return false;
	}

	public bool CanTake(GameObject taker)
	{
		CharacterClassManager component = taker.GetComponent<CharacterClassManager>();
		if (taker != playerConnected && playerConnected != null && Vector3.Distance(playerConnected.transform.position, base.transform.position) < 10f)
		{
			return false;
		}
		if (component != null && component.klasy[component.curClass].team == Team.SCP)
		{
			return false;
		}
		return taker.GetComponent<Inventory>().items.Count < 8;
	}

	public void UnconnectTablet(GameObject taker)
	{
		if (CanTake(taker) && !(animationCooldown > 0f))
		{
			Inventory component = taker.GetComponent<Inventory>();
			component.AddNewItem(19);
			playerConnected = null;
			NetworkisTabletConnected = false;
			animationCooldown = 3.5f;
		}
	}

	public void ConnectTablet(GameObject tabletOwner)
	{
		if (!CanPlace(tabletOwner) || animationCooldown > 0f)
		{
			return;
		}
		Inventory component = tabletOwner.GetComponent<Inventory>();
		foreach (Inventory.SyncItemInfo item in component.items)
		{
			if (item.id == 19)
			{
				component.items.Remove(item);
				NetworkisTabletConnected = true;
				animationCooldown = 6.5f;
				playerConnected = tabletOwner;
				break;
			}
		}
	}
}
