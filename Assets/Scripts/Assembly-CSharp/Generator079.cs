using Mirror;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using TMPro;
using Unity;
using UnityEngine;
using UnityEngine.Networking;

public class Generator079 : NetworkBehaviour
{
	private Animator anim;

	public Animator tabletAnim;

	public float startDuration = 90f;

	private float doorAnimationCooldown;

	private float tabletAnimCooldown;

	private float deniedCooldown;

	private float localTime;

	private bool prevConn;

	private AudioSource asource;

	public MeshRenderer keycardRenderer;

	public MeshRenderer wmtRenderer;

	public MeshRenderer epsenRenderer;

	public MeshRenderer epsdisRenderer;

	public MeshRenderer cancel1Rend;

	public MeshRenderer cancel2Rend;

	public Material matLocked;

	public Material matUnlocked;

	public Material matDenied;

	public Material matLedBlack;

	public Material matLetGreen;

	public Material matLetBlue;

	public Material cancel1MatDis;

	public Material cancel2MatDis;

	public Material cancel1MatEn;

	public Material cancel2MatEn;

	public AudioClip clipOpen;

	public AudioClip clipClose;

	public AudioClip beepSound;

	public AudioClip unlockSound;

	public AudioClip clipConnect;

	public AudioClip clipDisconnect;

	public AudioClip clipCounter;

	public Transform tabletEjectionPoint;

	public TextMeshProUGUI countdownText;

	public TextMeshProUGUI warningText;

	public Transform localArrow;

	public Transform totalArrow;

	public float localVoltage;

    public string curRoom;

	public static List<Generator079> generators;

	public static Generator079 mainGenerator;

	private string prevMin;

	private bool prevReady;

	private bool prevFinish;

	private bool prevUnlocked;

    [SyncVar(hook = nameof(SetTotal))] 
    public int totalVoltage;

    [SyncVar(hook = nameof(SetOffset))] 
    public Offset position;

    [SyncVar(hook = nameof(SetTime))] 
    public float remainingPowerup;

    [SyncVar]
    public bool isDoorOpen;

    [SyncVar]
    public bool isDoorUnlocked;

    [SyncVar]
    public bool isTabletConnected;

	public void SetTotal(int oldValue, int newValue)
	{
		totalVoltage = newValue;
	}

	public void SetOffset(Offset oldValue, Offset newValue)
	{
		position = newValue;
	}

	private void SetTime(float oldValue, float newValue)
	{
		remainingPowerup = newValue;
		if (Mathf.Abs(newValue - localTime) > 1f)
		{
			localTime = newValue;
		}
	}

	private void Awake()
	{
		if (!base.name.Contains("("))
		{
			mainGenerator = this;
		}
		asource = GetComponent<AudioSource>();
		anim = GetComponent<Animator>();
		generators.Clear();
	}

	private void Start()
	{
		if (NetworkServer.active)
		{
			float num = (remainingPowerup = startDuration);
			localTime = num;
		}
		generators.Add(this);
	}

	private void Update()
	{
		if (tabletAnimCooldown >= -1f)
		{
			tabletAnimCooldown -= Time.deltaTime;
		}
		if (base.transform.position != position.position || base.transform.rotation != Quaternion.Euler(position.rotation) || string.IsNullOrEmpty(curRoom))
		{
			base.transform.position = position.position;
			base.transform.rotation = Quaternion.Euler(position.rotation);
			RaycastHit hitInfo;
			if (Physics.Raycast(new Ray(base.transform.position - base.transform.forward, Vector3.down), out hitInfo, 50f, Interface079.singleton.roomDetectionMask))
			{
				Transform parent = hitInfo.transform;
				while (parent != null && !parent.transform.name.ToUpper().Contains("ROOT"))
				{
					parent = parent.transform.parent;
				}
				if (parent != null)
				{
					curRoom = parent.transform.name;
				}
			}
		}
		anim.SetBool("isOpen", isDoorOpen);
		float[] array = new float[6] { -57f, -38f, -22f, 0f, 22f, 38f };
		localArrow.transform.localRotation = Quaternion.Lerp(localArrow.transform.localRotation, Quaternion.Euler(0f, Mathf.Lerp(-40f, 40f, localVoltage), 0f), Time.deltaTime * 2f);
		totalArrow.transform.localRotation = Quaternion.Lerp(totalArrow.transform.localRotation, Quaternion.Euler(0f, array[Mathf.Clamp(mainGenerator.totalVoltage, 0, 5)], 0f), Time.deltaTime * 2f);
		if (doorAnimationCooldown >= 0f)
		{
			doorAnimationCooldown -= Time.deltaTime;
		}
		if (deniedCooldown >= 0f)
		{
			deniedCooldown -= Time.deltaTime;
			if (deniedCooldown < 0f)
			{
				keycardRenderer.material = ((!isDoorUnlocked) ? matLocked : matUnlocked);
			}
		}
	}

	private void LateUpdate()
	{
		if (Mathf.Abs(localTime - remainingPowerup) > 1.3f || remainingPowerup == 0f)
		{
			localTime = remainingPowerup;
		}
		if (prevConn && tabletAnimCooldown <= 0f && localTime > 0f)
		{
			if (NetworkServer.active && remainingPowerup > 0f)
			{
				remainingPowerup = remainingPowerup - Time.deltaTime;
				if (remainingPowerup < 0f)
				{
					remainingPowerup = 0f;
				}
				localTime = remainingPowerup;
			}
			localTime -= Time.deltaTime;
			if (localTime < 0f)
			{
				localTime = 0f;
			}
			float num = localTime;
			int num2 = 0;
			while (num >= 60f)
			{
				num -= 60f;
				num2++;
			}
			string[] array = (Mathf.Round(num * 100f) / 100f).ToString("00.00").Split('.');
			if (array.Length >= 2)
			{
				if (array[0] != prevMin)
				{
					prevMin = array[0];
					if (tabletAnimCooldown < -0.5f)
					{
						asource.PlayOneShot(clipCounter);
					}
				}
				countdownText.text = num2.ToString("00") + ":" + array[0] + ":" + array[1];
				warningText.enabled = true;
			}
		}
		else
		{
			countdownText.text = ((!(localTime > 0f)) ? "ENGAGED" : string.Empty);
			warningText.enabled = false;
			if (NetworkServer.active && prevConn && localTime <= 0f && isTabletConnected)
			{
				int num3 = 0;
				foreach (Generator079 generator in generators)
				{
					if (generator.localTime <= 0f)
					{
						num3++;
					}
				}
				remainingPowerup = 0f;
				localTime = 0f;
				EjectTablet();
				RpcNotify(num3);
				if (num3 < 5)
				{
					PlayerManager.localPlayer.GetComponent<MTFRespawn>().RpcPlayCustomAnnouncement("SCP079RECON" + num3, false);
				}
				else
				{
					Recontainer079.BeginContainment();
				}
				mainGenerator.totalVoltage = num3;
			}
			if (!prevConn && NetworkServer.active && tabletAnimCooldown < 0f && remainingPowerup < startDuration - 1f && remainingPowerup > 0f)
			{
				remainingPowerup = remainingPowerup + Time.deltaTime;
			}
		}
		localVoltage = 1f - Mathf.InverseLerp(0f, startDuration, localTime);
		CheckTabletConnectionStatus();
		CheckFinish();
		Unlock();
	}

	public void Interact(GameObject person, string command)
	{
		if (command.StartsWith("EPS_DOOR"))
		{
			OpenClose(person);
		}
		else
		{
			if (command.StartsWith("EPS_TABLET"))
			{
				if (isTabletConnected || !isDoorOpen || !(localTime > 0f))
				{
					return;
				}
				Inventory component = person.GetComponent<Inventory>();
				{
					foreach (Inventory.SyncItemInfo item in component.items)
					{
						if (item.id == 19)
						{
							component.items.Remove(item);
							isTabletConnected = true;
							break;
						}
					}
					return;
				}
			}
			if (command.StartsWith("EPS_CANCEL"))
			{
				EjectTablet();
			}
			else
			{
				Debug.LogError("Unknown command: " + command);
			}
		}
	}

	public void EjectTablet()
	{
		if (isTabletConnected)
		{
			isTabletConnected = false;
			PlayerManager.localPlayer.GetComponent<Inventory>().SetPickup(19, 0f, tabletEjectionPoint.position, tabletEjectionPoint.rotation, 0, 0, 0);
		}
	}

	private void CheckTabletConnectionStatus()
	{
		if (prevConn != isTabletConnected)
		{
			prevConn = isTabletConnected;
			tabletAnimCooldown = 1f;
			tabletAnim.SetBool("b", prevConn);
			asource.PlayOneShot((!prevConn) ? clipDisconnect : clipConnect);
		}
		bool flag = prevConn && tabletAnimCooldown <= 0f;
		if (prevReady != flag)
		{
			prevReady = flag;
			wmtRenderer.material = ((!flag) ? matLedBlack : matLetBlue);
			Material material = ((!(localTime > 0f) || !prevConn || !(tabletAnimCooldown <= 0f)) ? cancel1MatDis : cancel1MatEn);
			cancel1Rend.material = material;
			material = ((!(localTime > 0f) || !prevConn || !(tabletAnimCooldown <= 0f)) ? cancel2MatDis : cancel2MatEn);
			cancel2Rend.material = material;
		}
	}

	private void CheckFinish()
	{
		if (!prevFinish && localTime <= 0f)
		{
			prevFinish = true;
			epsenRenderer.material = matLetGreen;
			epsdisRenderer.material = matLedBlack;
			asource.PlayOneShot(unlockSound);
		}
	}

	private void OpenClose(GameObject person)
	{
		Inventory component = person.GetComponent<Inventory>();
		if (component == null || doorAnimationCooldown > 0f || deniedCooldown > 0f)
		{
			return;
		}
		if (!isDoorUnlocked)
		{
			bool flag = person.GetComponent<ServerRoles>().BypassMode;
			if (component.curItem > 0)
			{
				string[] permissions = component.availableItems[component.curItem].permissions;
				foreach (string text in permissions)
				{
					if (text == "ARMORY_LVL_3")
					{
						flag = true;
					}
				}
			}
			if (flag)
			{
				isDoorUnlocked = true;
				doorAnimationCooldown = 0.5f;
			}
			else
			{
				RpcDenied();
			}
		}
		else
		{
			doorAnimationCooldown = 1.5f;
			isDoorOpen = !isDoorOpen;
			RpcDoSound(isDoorOpen);
		}
	}

	[ClientRpc]
	private void RpcDenied()
	{
		deniedCooldown = 0.5f;
		if (keycardRenderer.material != matUnlocked)
		{
			keycardRenderer.material = matDenied;
		}
		asource.PlayOneShot(beepSound);
	}

	[ClientRpc]
	public void RpcOvercharge()
	{
		FlickerableLight[] array = Object.FindObjectsOfType<FlickerableLight>();
		foreach (FlickerableLight flickerableLight in array)
		{
			Scp079Interactable component = flickerableLight.GetComponent<Scp079Interactable>();
			if (component == null || component.currentZonesAndRooms[0].currentZone == "HeavyRooms")
			{
				flickerableLight.EnableFlickering(10f);
			}
		}
	}

	private void Unlock()
	{
		if (prevUnlocked != isDoorUnlocked)
		{
			prevUnlocked = true;
			asource.PlayOneShot(unlockSound);
			keycardRenderer.material = matUnlocked;
		}
	}

	[ClientRpc]
	private void RpcNotify(int curr)
	{
		if (Interface079.lply != null && Interface079.lply.iAm079)
		{
			Interface079.singleton.AddBigNotification("ENGAGED GENERATORS:\n" + curr + "/5");
		}
	}

	[ClientRpc]
	private void RpcDoSound(bool isOpen)
	{
		asource.PlayOneShot((!isOpen) ? clipClose : clipOpen);
	}
}
