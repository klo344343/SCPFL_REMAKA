using Mirror;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.Serialization;

public class Scp079PlayerScript : NetworkBehaviour
{
	[Serializable]
	public class Level079
	{
		public string label;

		public int unlockExp;

		[Space]
		public float manaPerSecond;

		public float maxMana;

		public float powerdrainDuration;

		public void LoadConfig(int id)
		{
		}
	}

	[Serializable]
	public class Ability079
	{
		public string label;

		[FormerlySerializedAs("manaCost")]
		public float mana;

		public int requiredAccessTier = 1;
	}

	public Level079[] levels;

	public Ability079[] abilities;

	public Ability079[] expEarnWays;

	public float[] generatorAuxRegenerationFactor;

	public static List<Scp079PlayerScript> instances;

	public static Camera079[] allCameras;

	private ServerRoles roles;

	public GameObject PlayerCameraGameObjectera;

	[SyncVar]
	private string curSpeaker;

    public SyncList<string> lockedDoors = new SyncList<string>();

    [SyncVar]
	private int curLvl;

	[SyncVar]
	private float curExp;

	[SyncVar]
	private float curMana = 100f;

	[SyncVar]
	public float maxMana = 100f;

	public Camera079 currentCamera;

	public bool sameClass;

	public bool iAm079;

	public List<Scp079Interaction> interactionHistory;

	public string currentZone;

	public string currentRoom;

	public Camera079[] nearestCameras;

	public List<Scp079Interactable> nearbyInteractables = new List<Scp079Interactable>();

	private float ucpTimer;

	public int Lvl
	{
		get
		{
			return curLvl;
		}
		private set
		{
			NetworkcurLvl = Mathf.Clamp(value, 0, levels.Length - 1);
		}
	}

	public string Speaker
	{
		get
		{
			return curSpeaker;
		}
		private set
		{
			NetworkcurSpeaker = value;
		}
	}

	public float Exp
	{
		get
		{
			return curExp;
		}
		private set
		{
			NetworkcurExp = value;
		}
	}

	public float Mana
	{
		get
		{
			return curMana;
		}
		private set
		{
			NetworkcurMana = value;
		}
	}

	public string NetworkcurSpeaker
	{
		get
		{
			return curSpeaker;
		}
		set
		{
			SetSyncVar(value, ref curSpeaker, 1u);
		}
	}

	public int NetworkcurLvl
	{
		get
		{
			return curLvl;
		}
		set
		{
			SetSyncVar(value, ref curLvl, 4u);
		}
	}

	public float NetworkcurExp
	{
		get
		{
			return curExp;
		}
		set
		{
			SetSyncVar(value, ref curExp, 8u);
		}
	}

	public float NetworkcurMana
	{
		get
		{
			return curMana;
		}
		set
		{
			SetSyncVar(value, ref curMana, 16u);
		}
	}

	public float NetworkmaxMana
	{
		get
		{
			return maxMana;
		}
		set
		{
			SetSyncVar(value, ref maxMana, 32u);
		}
	}

	private void Start()
	{
		roles = GetComponent<ServerRoles>();
		if (base.isLocalPlayer || NetworkServer.active)
		{
			allCameras = UnityEngine.Object.FindObjectsOfType<Camera079>();
		}
		if (base.isLocalPlayer)
		{
			Interface079.lply = this;
		}
		if (!NetworkServer.active)
		{
			return;
		}
		Ability079[] array = abilities;
		foreach (Ability079 ability in array)
		{
			int num = ConfigFile.ServerConfig.GetInt("scp079_ability_" + ability.label.Replace(" ", "_").ToLower(), -1);
			if (num <= 0)
			{
			}
		}
		for (int j = 0; j < levels.Length; j++)
		{
			levels[j].LoadConfig(j);
		}
		OnExpChange();
	}

	private void Update()
	{
		RefreshInstances();
		UpdateCameraPosition();
		ServerUpdateMana();
	}

	[Server]
	public void AddExperience(float amount)
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Void Scp079PlayerScript::AddExperience(System.Single)' called on client");
			return;
		}
		Exp += amount;
		OnExpChange();
	}

	[Server]
	public void ForceLevel(int levelToForce, bool notifyUser)
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Void Scp079PlayerScript::ForceLevel(System.Int32,System.Boolean)' called on client");
			return;
		}
		Lvl = levelToForce;
		if (notifyUser)
		{
			if (base.name == "Host")
			{
				Interface079.singleton.NotifyNewLevel(levelToForce);
			}
			else
			{
				TargetLevelChanged(base.connectionToClient, levelToForce);
			}
		}
	}

	[Server]
	public void ResetAll()
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Void Scp079PlayerScript::ResetAll()' called on client");
			return;
		}
		interactionHistory.Clear();
		NetworkcurLvl = 0;
		NetworkcurExp = 0f;
		NetworkcurMana = 0f;
		RpcSwitchCamera(Interface079.singleton.defaultCamera.transform.position, false);
	}

	[Server]
	private void OnExpChange()
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Void Scp079PlayerScript::OnExpChange()' called on client");
			return;
		}
		while (curLvl < levels.Length - 1 && curExp >= (float)levels[curLvl + 1].unlockExp)
		{
			int num = (Lvl = curLvl + 1);
			Exp -= levels[num].unlockExp;
			NetworkmaxMana = levels[num].maxMana;
			if (base.name == "Host")
			{
				Interface079.singleton.NotifyNewLevel(num);
			}
			else
			{
				TargetLevelChanged(base.connectionToClient, num);
			}
		}
	}

	[TargetRpc]
	private void TargetLevelChanged(NetworkConnection conn, int newLvl)
	{
		Interface079.singleton.NotifyNewLevel(newLvl);
	}

	[Server]
	public void AddInteractionToHistory(GameObject go, string cmd, bool addMana)
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Void Scp079PlayerScript::AddInteractionToHistory(UnityEngine.GameObject,System.String,System.Boolean)' called on client");
		}
		else
		{
			if (go == null)
			{
				return;
			}
			Scp079Interactable component = go.GetComponent<Scp079Interactable>();
			if (component != null)
			{
				if (addMana)
				{
					RpcGainExp(ExpGainType.GeneralInteractions, (int)component.type);
				}
				interactionHistory.Add(new Scp079Interaction
				{
					activationTime = Time.realtimeSinceStartup,
					interactable = component,
					command = cmd
				});
			}
		}
	}

	[Command]
	public void CmdInteract(string command, GameObject target)
	{
		if (!iAm079)
		{
			return;
		}
		Interface079.Log("Received command: " + command);
		if (!command.Contains(":"))
		{
			return;
		}
		string[] array = command.Split(':');
		RefreshCurrentRoom();
		if (!CheckInteractableLegitness(currentRoom, currentZone, target, true))
		{
			return;
		}
		List<string> list = ConfigFile.ServerConfig.GetStringList("scp079_door_blacklist") ?? new List<string>();
		switch (array[0])
		{
		case "DOOR":
		{
			if (AlphaWarheadController.host.inProgress)
			{
				break;
			}
			if (target == null)
			{
				Interface079.Log("This command requires a target");
				break;
			}
			Door component = target.GetComponent<Door>();
			if (component == null)
			{
				break;
			}
			int? num2 = ((list != null) ? new int?(list.Count) : ((int?)null));
			if (num2.HasValue && num2.GetValueOrDefault() > 0)
			{
				bool? flag3 = ((list != null) ? new bool?(list.Contains(component.DoorName)) : ((bool?)null));
				if (flag3.HasValue && flag3.Value)
				{
					Interface079.Log("DOOR ACCESS DENIED BY SERVER");
					break;
				}
			}
			float manaFromLabel = GetManaFromLabel("Door Interaction " + ((!string.IsNullOrEmpty(component.permissionLevel)) ? component.permissionLevel : "DEFAULT"), abilities);
			if (manaFromLabel > curMana)
			{
				Interface079.Log("NOT ENOUGH MANA");
				RpcNotEnoughMana(manaFromLabel, curMana);
			}
			else if (component != null && component.ChangeState079())
			{
				Mana -= manaFromLabel;
				AddInteractionToHistory(target, array[0], true);
				Interface079.Log("DOOR STATE CHANGED");
			}
			else
			{
				Interface079.Log("DOOR STATE CHANGE FAILED");
			}
			break;
		}
		case "DOORLOCK":
		{
			if (AlphaWarheadController.host.inProgress)
			{
				break;
			}
			if (target == null)
			{
				Interface079.Log("This command requires a target");
				break;
			}
			Door component = target.GetComponent<Door>();
			if (component == null)
			{
				break;
			}
			int? num = ((list != null) ? new int?(list.Count) : ((int?)null));
			if (num.HasValue && num.GetValueOrDefault() > 0)
			{
				bool? flag2 = ((list != null) ? new bool?(list.Contains(component.DoorName)) : ((bool?)null));
				if (flag2.HasValue && flag2.Value)
				{
					Interface079.Log("DOOR ACCESS DENIED BY SERVER");
				}
			}
			if (component.sound_checkpointWarning != null)
			{
				break;
			}
			float manaFromLabel = 15f;
			if (manaFromLabel > curMana)
			{
				RpcNotEnoughMana(manaFromLabel, curMana);
			}
			else if (!component.Locked)
			{
				string item = component.transform.parent.name + "/" + component.transform.name;
				if (!lockedDoors.Contains(item))
				{
					lockedDoors.Add(item);
				}
				component.LockBy079();
				AddInteractionToHistory(component.gameObject, array[0], true);
				Mana -= manaFromLabel / 3f;
			}
			break;
		}
		case "SPEAKER":
		{
			string speaker = currentZone + "/" + currentRoom + "/Scp079Speaker";
			GameObject gameObject3 = GameObject.Find(speaker);
			float manaFromLabel = GetManaFromLabel("Speaker Start", abilities);
			if (manaFromLabel * 1.5f > curMana)
			{
				RpcNotEnoughMana(manaFromLabel, curMana);
			}
			else if (gameObject3 != null)
			{
				Mana -= manaFromLabel;
				Speaker = speaker;
				AddInteractionToHistory(gameObject3, array[0], true);
			}
			break;
		}
		case "STOPSPEAKER":
			Speaker = string.Empty;
			break;
		case "ELEVATORTELEPORT":
		{
			float manaFromLabel = GetManaFromLabel("Elevator Teleport", abilities);
			if (manaFromLabel > curMana)
			{
				RpcNotEnoughMana(manaFromLabel, curMana);
				break;
			}
			Camera079 camera = null;
			foreach (Scp079Interactable nearbyInteractable in nearbyInteractables)
			{
				if (nearbyInteractable.type == Scp079Interactable.InteractableType.ElevatorTeleport)
				{
					camera = nearbyInteractable.optionalObject.GetComponent<Camera079>();
				}
			}
			if (camera != null)
			{
				RpcSwitchCamera(camera.transform.position, false);
				Mana -= manaFromLabel;
				AddInteractionToHistory(target, array[0], true);
			}
			break;
		}
		case "ELEVATORUSE":
		{
			float manaFromLabel = GetManaFromLabel("Elevator Use", abilities);
			if (manaFromLabel > curMana)
			{
				RpcNotEnoughMana(manaFromLabel, curMana);
				break;
			}
			string text = string.Empty;
			if (array.Length > 1)
			{
				text = array[1];
			}
			Lift[] array2 = UnityEngine.Object.FindObjectsOfType<Lift>();
			foreach (Lift lift in array2)
			{
				if (lift.elevatorName == text && lift.UseLift())
				{
					Mana -= manaFromLabel;
					bool flag = false;
					Lift.Elevator[] elevators = lift.elevators;
					for (int k = 0; k < elevators.Length; k++)
					{
						Lift.Elevator elevator = elevators[k];
						AddInteractionToHistory(elevator.door.GetComponentInParent<Scp079Interactable>().gameObject, array[0], !flag);
						flag = true;
					}
				}
			}
			break;
		}
		case "TESLA":
		{
			float manaFromLabel = GetManaFromLabel("Tesla Gate Burst", abilities);
			if (manaFromLabel > curMana)
			{
				RpcNotEnoughMana(manaFromLabel, curMana);
				break;
			}
			GameObject gameObject3 = GameObject.Find(currentZone + "/" + currentRoom + "/Gate");
			if (gameObject3 != null)
			{
				TeslaGate component2 = gameObject3.GetComponent<TeslaGate>();
				component2.RpcInstantBurst();
				AddInteractionToHistory(gameObject3, array[0], true);
				Mana -= manaFromLabel;
			}
			break;
		}
		case "LOCKDOWN":
		{
			if (AlphaWarheadController.host.inProgress)
			{
				break;
			}
			float manaFromLabel = GetManaFromLabel("Room Lockdown", abilities);
			if (manaFromLabel > curMana)
			{
				RpcNotEnoughMana(manaFromLabel, curMana);
				break;
			}
			GameObject gameObject = GameObject.Find(currentZone + "/" + currentRoom);
			if (gameObject != null)
			{
				List<Scp079Interactable> list2 = new List<Scp079Interactable>();
				Scp079Interactable[] allInteractables = Interface079.singleton.allInteractables;
				foreach (Scp079Interactable scp079Interactable in allInteractables)
				{
					foreach (Scp079Interactable.ZoneAndRoom currentZonesAndRoom in scp079Interactable.currentZonesAndRooms)
					{
						if (currentZonesAndRoom.currentRoom == currentRoom && currentZonesAndRoom.currentZone == currentZone && scp079Interactable.transform.position.y - 100f < currentCamera.transform.position.y && !list2.Contains(scp079Interactable))
						{
							list2.Add(scp079Interactable);
						}
					}
				}
				GameObject gameObject2 = null;
				foreach (Scp079Interactable item2 in list2)
				{
					switch (item2.type)
					{
					case Scp079Interactable.InteractableType.Door:
						if (item2.GetComponent<Door>().Destroyed)
						{
							Interface079.Log("One of the doors has been destroyed /// Lockdown command");
							return;
						}
						break;
					case Scp079Interactable.InteractableType.Lockdown:
						gameObject2 = item2.gameObject;
						break;
					}
				}
				if (list2.Count == 0 || gameObject2 == null)
				{
					Interface079.Log("No interactables or icon not found /// Lockdown command");
					break;
				}
				foreach (Scp079Interactable item3 in list2)
				{
					if (item3.type == Scp079Interactable.InteractableType.Door)
					{
						Door component = item3.GetComponent<Door>();
						if (component.Locked || component.scp079Lockdown > -2.5f)
						{
							return;
						}
						if (component.IsOpen)
						{
							component.ChangeState();
						}
						component.scp079Lockdown = 10f;
					}
				}
				RpcFlickerLights(currentRoom, currentZone);
				AddInteractionToHistory(gameObject2, array[0], true);
				Mana -= GetManaFromLabel("Room Lockdown", abilities);
			}
			else
			{
				Interface079.Log("Current room not found /// Lockdown command");
			}
			break;
		}
		}
	}

	[Command]
	public void CmdResetDoors()
	{
		lockedDoors.Clear();
	}

	[Server]
	public List<Scp079Interaction> ReturnRecentHistory(float timeLimit, Scp079Interactable.InteractableType[] filter = null)
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Collections.Generic.List`1<Scp079Interaction> Scp079PlayerScript::ReturnRecentHistory(System.Single,Scp079Interactable/InteractableType[])' called on client");
			return null;
		}
		List<Scp079Interaction> list = new List<Scp079Interaction>();
		foreach (Scp079Interaction item in interactionHistory)
		{
			float num = Time.realtimeSinceStartup - item.activationTime;
			if (!(num <= timeLimit))
			{
				continue;
			}
			bool flag = filter == null;
			if (filter != null)
			{
				foreach (Scp079Interactable.InteractableType interactableType in filter)
				{
					if (item.interactable.type == interactableType)
					{
						flag = true;
					}
				}
			}
			if (flag)
			{
				list.Add(item);
			}
		}
		return list;
	}

	public Scp079Interactable.ZoneAndRoom GetOtherRoom()
	{
		Scp079Interactable.ZoneAndRoom result = new Scp079Interactable.ZoneAndRoom
		{
			currentRoom = "::NONE::",
			currentZone = "::NONE::"
		};
        RaycastHit hitInfo;
		if (Physics.Raycast(new Ray(base.transform.position, Vector3.down), out hitInfo, 100f, Interface079.singleton.roomDetectionMask))
		{
			Transform parent = hitInfo.transform;
			while (parent != null && !parent.transform.name.ToUpper().Contains("ROOT"))
			{
				parent = parent.transform.parent;
			}
			if (parent != null)
			{
				result.currentRoom = parent.transform.name;
				result.currentZone = parent.transform.parent.name;
			}
		}
		return result;
	}

	public void RefreshCurrentRoom()
	{
		int num = 0;
		try
		{
			currentRoom = "::NONE::";
			currentZone = "::NONE::";
			num = 5;
			RaycastHit hitInfo;
			if (Physics.Raycast(new Ray(currentCamera.transform.position, Vector3.down), out hitInfo, 100f, Interface079.singleton.roomDetectionMask))
			{
				num = 1;
				Transform parent = hitInfo.transform;
				while (parent != null && !parent.transform.name.ToUpper().Contains("ROOT"))
				{
					parent = parent.transform.parent;
				}
				num = 2;
				if (parent != null)
				{
					currentRoom = parent.transform.name;
					currentZone = parent.transform.parent.name;
				}
			}
			num = 3;
			nearbyInteractables.Clear();
			if (Interface079.singleton.allInteractables != null)
			{
				Scp079Interactable[] allInteractables = Interface079.singleton.allInteractables;
				foreach (Scp079Interactable scp079Interactable in allInteractables)
				{
					if (scp079Interactable != null && Vector3.Distance(scp079Interactable.transform.position, currentCamera.transform.position) < (float)((!(currentZone == "Outside")) ? 40 : 250) && scp079Interactable.IsVisible(currentZone, currentRoom))
					{
						nearbyInteractables.Add(scp079Interactable);
					}
				}
			}
			Interface079.singleton.cameraNormal.farClipPlane = ((!(currentZone == "Outside")) ? 47 : 300);
			Interface079.singleton.cameraNormal.nearClipPlane = ((!(currentZone == "Outside")) ? 0.01f : 0.05f);
			if (Interface079.singleton.mapEnabled && currentZone == "Outside")
			{
				Interface079.singleton.SwitchMode();
			}
		}
		catch
		{
			Interface079.Log("ERROR CODE " + num);
		}
	}

	private bool CheckInteractableLegitness(string cr, string cz, GameObject tr, bool allowNull)
	{
		if (tr == null)
		{
			return allowNull;
		}
		Scp079Interactable component = tr.GetComponent<Scp079Interactable>();
		if (component == null)
		{
			Interface079.Log("THIS GAMEOBJECT IS NOT AN INTERACTABLE PREFAB");
			return false;
		}
		foreach (Scp079Interactable.ZoneAndRoom currentZonesAndRoom in component.currentZonesAndRooms)
		{
			if (currentZonesAndRoom.currentRoom == cr && currentZonesAndRoom.currentZone == cz)
			{
				return true;
			}
		}
		Interface079.Log("NO MATCH. ITEM NAME: '" + tr.name + "' DEBUG CODE: " + component.currentZonesAndRooms.Count);
		foreach (Scp079Interactable.ZoneAndRoom currentZonesAndRoom2 in component.currentZonesAndRooms)
		{
			Interface079.Log(currentZonesAndRoom2.currentRoom + "!=" + cr + ":" + currentZonesAndRoom2.currentZone + "!=" + cz);
		}
		return false;
	}

	public float GetManaFromLabel(string label, Ability079[] array)
	{
		if (roles.BypassMode && array == abilities)
		{
			return 0f;
		}
		foreach (Ability079 ability in array)
		{
			if (ability.label == label)
			{
				if (ability.requiredAccessTier <= curLvl + 1)
				{
					return ability.mana;
				}
				return float.PositiveInfinity;
			}
		}
		Interface079.Log(label + " is not a correct label.");
		return float.PositiveInfinity;
	}

	public void Init(int classID, Class c)
	{
		if (!iAm079 && base.isLocalPlayer && classID == 7)
		{
			Interface079.singleton.RefreshInteractables();
		}
		sameClass = c.team == Team.SCP;
		iAm079 = classID == 7;
		if (base.isLocalPlayer)
		{
			PlayerCameraGameObjectera.SetActive(!iAm079);
			Interface079.singleton.gameObject.SetActive(iAm079);
			FirstPersonController.disableJumping = iAm079;
		}
		if (iAm079)
		{
			if (!instances.Contains(this))
			{
				instances.Add(this);
			}
			if (NetworkServer.active && (currentCamera == null || currentCamera.transform.position == Vector3.zero))
			{
				RpcSwitchCamera(Interface079.singleton.defaultCamera.transform.position, false);
			}
		}
		else if (base.isLocalPlayer)
		{
			CursorManager.singleton.is079 = false;
		}
	}

	[ServerCallback]
	private void ServerUpdateMana()
	{
		if (!NetworkServer.active || !NetworkServer.active || !iAm079)
		{
			return;
		}
		if (!string.IsNullOrEmpty(Speaker))
		{
			Mana -= GetManaFromLabel("Speaker Update", abilities) * Time.deltaTime;
			if (Mana <= 0f)
			{
				Speaker = string.Empty;
			}
		}
		else if (lockedDoors.Count > 0)
		{
			Mana -= GetManaFromLabel("Door Lock", abilities) * Time.deltaTime * (float)lockedDoors.Count;
			if (Mana <= 0f)
			{
				lockedDoors.Clear();
			}
		}
		else
		{
			Mana = Mathf.Clamp(curMana + levels[curLvl].manaPerSecond * Time.deltaTime * generatorAuxRegenerationFactor[Generator079.mainGenerator.totalVoltage], 0f, levels[curLvl].maxMana);
		}
	}

    private void RefreshInstances()
    {
        for (int i = instances.Count - 1; i >= 0; i--)
        {
            var instance = instances[i];
            if (instance == null || !instance.iAm079)
                instances.RemoveAt(i);
        }
    }


    [Command]
	public void CmdSwitchCamera(Vector3 cameraPosition, bool lookatRotation)
	{
		if (!iAm079)
		{
			return;
		}
		float num = CalculateCameraSwitchCost(cameraPosition);
		if (num > curMana)
		{
			RpcNotEnoughMana(num, curMana);
			return;
		}
		Camera079[] array = allCameras;
		foreach (Camera079 camera in array)
		{
			if (Vector3.Distance(camera.transform.position, cameraPosition) < 1f)
			{
				RpcSwitchCamera(camera.transform.position, lookatRotation);
				Mana -= num;
				currentCamera = camera;
				break;
			}
		}
	}

	public float CalculateCameraSwitchCost(Vector3 cameraPosition)
	{
		if (currentCamera == null)
		{
			return 0f;
		}
		if (roles.BypassMode)
		{
			return 0f;
		}
		float num = Vector3.Distance(cameraPosition, currentCamera.transform.position);
		return num * GetManaFromLabel("Camera Switch", abilities) / 10f;
	}

	public Camera079 GetCameraByPos(Vector3 camPos)
	{
		Camera079 result = null;
		Camera079[] array = allCameras;
		foreach (Camera079 camera in array)
		{
			if (Vector3.Distance(camera.transform.position, camPos) < 1f)
			{
				result = camera;
			}
		}
		return result;
	}

	[ClientRpc]
	private void RpcFlickerLights(string cr, string cz)
	{
		List<Scp079Interactable> list = new List<Scp079Interactable>();
		Scp079Interactable[] allInteractables = Interface079.singleton.allInteractables;
		foreach (Scp079Interactable scp079Interactable in allInteractables)
		{
			foreach (Scp079Interactable.ZoneAndRoom currentZonesAndRoom in scp079Interactable.currentZonesAndRooms)
			{
				if (currentZonesAndRoom.currentRoom == cr && currentZonesAndRoom.currentZone == cz && scp079Interactable.transform.position.y - 100f < currentCamera.transform.position.y && !list.Contains(scp079Interactable))
				{
					list.Add(scp079Interactable);
				}
			}
		}
		foreach (Scp079Interactable item in list)
		{
			if (item.type == Scp079Interactable.InteractableType.Light)
			{
				item.GetComponent<FlickerableLight>().EnableFlickering(8f);
			}
		}
	}

	[ClientRpc]
	private void RpcSwitchCamera(Vector3 camPos, bool lookatRotation)
	{
		Camera079 camera = null;
		Camera079[] array = allCameras;
		foreach (Camera079 camera2 in array)
		{
			if (camera2.transform.position == camPos)
			{
				camera = camera2;
			}
		}
		if (camera == null && base.isLocalPlayer)
		{
			Interface079.Log("ERROR: NO CAMERA FOUND AT " + camPos);
			Interface079.singleton.transitionInProgress = 0f;
		}
		currentCamera = camera;
		if (!base.isLocalPlayer)
		{
			return;
		}
		if (camera != null)
		{
			Interface079.Log("NEW CAMERA: " + camera.cameraName);
		}
		Interface079.singleton.transitionInProgress = 0f;
		if (Interface079.singleton.mapEnabled)
		{
			Interface079.singleton.SwitchMode();
		}
		RefreshCurrentRoom();
		if (!lookatRotation)
		{
			return;
		}
		currentCamera.head.transform.rotation = Interface079.singleton.prevCamRotation;
		float minRot = currentCamera.minRot;
		float maxRot = currentCamera.maxRot;
		float[] array2 = new float[3];
		for (int j = -1; j <= 1; j++)
		{
			float num = currentCamera.head.localRotation.eulerAngles.y + (float)(360 * j);
			if (num < minRot)
			{
				array2[j + 1] = Mathf.Abs(minRot - num);
			}
			if (num > maxRot)
			{
				array2[j + 1] = Mathf.Abs(num - maxRot);
			}
		}
		float num2 = float.PositiveInfinity;
		int num3 = 0;
		for (int k = 0; k < 3; k++)
		{
			if (array2[k] < num2)
			{
				num2 = array2[k];
				num3 = k - 1;
			}
		}
		currentCamera.curRot = currentCamera.head.localRotation.eulerAngles.y + (float)(360 * num3);
		currentCamera.curPitch = currentCamera.head.localRotation.eulerAngles.x;
	}

	private void UpdateCameraPosition()
	{
		if (base.isLocalPlayer && iAm079 && currentCamera != null)
		{
			ucpTimer += Time.deltaTime;
			if (ucpTimer > 0.15f)
			{
				CmdUpdateCameraPosition(new Vector3(currentCamera.transform.position.x, currentCamera.curRot, currentCamera.curPitch));
				ucpTimer = 0f;
			}
		}
	}

	[Command]
	private void CmdUpdateCameraPosition(Vector3 data)
	{
		if (currentCamera != null && currentCamera.transform.position.x == data.x)
		{
			RpcUpdateCameraPostion(data);
		}
	}

	[ClientRpc]
	private void RpcUpdateCameraPostion(Vector3 data)
	{
		if (!base.isLocalPlayer && currentCamera != null && currentCamera.transform.position.x == data.x)
		{
			currentCamera.UpdatePosition(data.y, data.z);
		}
	}

	[ClientRpc]
	private void RpcNotEnoughMana(float required, float cur)
	{
		if (base.isLocalPlayer && iAm079)
		{
			if (Interface079.singleton.transitionInProgress > 0f)
			{
				Interface079.singleton.transitionInProgress = 0f;
			}
			Interface079.Log("Not enough mana " + cur + "/" + required);
			Interface079.singleton.NotifyNotEnoughMana((int)required);
		}
	}

	[ClientRpc]
	public void RpcGainExp(ExpGainType type, int details)
	{
		switch (type)
		{
		case ExpGainType.KillAssist:
		{
			Team team = GetComponent<CharacterClassManager>().klasy[details].team;
			int num3 = 6;
			float num4;
			switch (team)
			{
			case Team.CDP:
				num4 = GetManaFromLabel("Class-D Kill Assist", expEarnWays);
				num3 = 7;
				break;
			case Team.CHI:
				num4 = GetManaFromLabel("Chaos Kill Assist", expEarnWays);
				num3 = 8;
				break;
			case Team.MTF:
				num4 = GetManaFromLabel("MTF Kill Assist", expEarnWays);
				num3 = 9;
				break;
			case Team.RSC:
				num4 = GetManaFromLabel("Scientist Kill Assist", expEarnWays);
				num3 = 10;
				break;
			case Team.SCP:
				num4 = GetManaFromLabel("SCP Kill Assist", expEarnWays);
				num3 = 11;
				break;
			default:
				num4 = 0f;
				break;
			}
			num3--;
			try
			{
				Interface079.singleton.NotifyMoreExp(TranslationReader.Get("SCP079", 4) + " " + TranslationReader.Get("SCP079", num3) + "(+" + num4 + " EXP)");
			}
			catch
			{
				Interface079.Log("KillAssist error, 'RpcGainExp' in Scp079PlayerScript.cs");
			}
			if (NetworkServer.active)
			{
				AddExperience(num4);
			}
			break;
		}
		case ExpGainType.GeneralInteractions:
		{
			float num = 0f;
			switch ((Scp079Interactable.InteractableType)details)
			{
			case Scp079Interactable.InteractableType.Door:
				num = GetManaFromLabel("Door Interaction", expEarnWays);
				break;
			case Scp079Interactable.InteractableType.Lockdown:
				num = GetManaFromLabel("Lockdown Activation", expEarnWays);
				break;
			case Scp079Interactable.InteractableType.Tesla:
				num = GetManaFromLabel("Tesla Gate Activation", expEarnWays);
				break;
			case Scp079Interactable.InteractableType.ElevatorUse:
				num = GetManaFromLabel("Elevator Use", expEarnWays);
				break;
			}
			if (num != 0f)
			{
				float num2 = 1f / Mathf.Clamp(levels[curLvl].manaPerSecond / 1.5f, 1f, 7f);
				num = Mathf.Round(num * num2 * 10f) / 10f;
				try
				{
					Interface079.singleton.NotifyMoreExp(TranslationReader.Get("SCP079", 12) + " (+" + num + " EXP)");
				}
				catch
				{
					Interface079.Log("KillAssist error, 'RpcGainExp' in Scp079PlayerScript.cs");
				}
				if (NetworkServer.active)
				{
					AddExperience(num);
				}
			}
			break;
		}
		case ExpGainType.AdminCheat:
			try
			{
				Interface079.singleton.NotifyMoreExp(TranslationReader.Get("SCP079", 4) + " Admin cheat (+" + details + " EXP)");
				if (NetworkServer.active)
				{
					AddExperience(details);
				}
				break;
			}
			catch
			{
				Interface079.Log("AdminCheat error, 'RpcGainExp' in Scp079PlayerScript.cs");
				break;
			}
		}
	}
}
