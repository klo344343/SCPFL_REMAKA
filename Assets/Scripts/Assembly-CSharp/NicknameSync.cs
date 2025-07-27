using System.Runtime.InteropServices;
using System.Text;
using GameConsole;
using Mirror;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

public class NicknameSync : NetworkBehaviour
{
	public LayerMask raycastMask;

	private Transform spectCam;

	private ServerRoles _role;

	private Text n_text;

	private float transparency;

	private bool _nickSet;

	public float viewRange;

	[SyncVar(hook = nameof(SetNick))]
	public string myNick;

    private void SetNick(string oldValue, string newValue)
    {
        newValue = newValue.Replace("<", "＜");
        newValue = newValue.Replace(">", "＞");
        myNick = newValue;
    }

    private void Start()
	{
		_role = GetComponent<ServerRoles>();
		if (!base.isLocalPlayer)
		{
			return;
		}
		string n;
		if (ServerStatic.IsDedicated)
		{
			n = "Dedicated Server";
		}
		else
		{
			if (SteamManager.Running)
			{
				n = ((SteamManager.GetPersonaName(0uL) != null) ? SteamManager.GetPersonaName(0uL) : "Player");
			}
			else
			{
				Console.singleton.AddLog("Steam has been not initialized!", new Color32(byte.MaxValue, 0, 0, byte.MaxValue));
				if (PlayerPrefs.HasKey("nickname"))
				{
					n = PlayerPrefs.GetString("nickname");
				}
				else
				{
					string text = "Player " + SystemInfo.deviceName;
					PlayerPrefs.SetString("nickname", text);
					n = text;
				}
			}
			CmdSetNick(n);
		}
		CmdSetNick(n);
		spectCam = GetComponent<Scp049PlayerScript>().PlayerCameraGameObject.transform;
		n_text = GameObject.Find("Nickname Text").GetComponent<Text>();
	}

	private void Update()
	{
		if (!base.isLocalPlayer)
		{
			return;
		}
		bool flag = false;
		RaycastHit hitInfo = default;
		CharacterClassManager component = GetComponent<CharacterClassManager>();
		if (component.curClass != 2 && Physics.Raycast(new Ray(spectCam.position, spectCam.forward), out hitInfo, viewRange, raycastMask))
		{
			NicknameSync component2 = hitInfo.transform.GetComponent<NicknameSync>();
			if (component2 != null && !component2.isLocalPlayer)
			{
				CharacterClassManager component3 = component2.GetComponent<CharacterClassManager>();
				flag = true;
				if (component3.curClass >= 0 && component3.curClass != 2 && !TutorialManager.status)
				{
					n_text.color = component3.klasy[component3.curClass].classColor;
					n_text.text = component2._role.GetColoredRoleString() + "\n";
					n_text.text += component2.myNick;
					Text text = n_text;
					text.text = text.text + "\n" + component3.klasy[component3.curClass].fullName;
					if (component3.curClass == 7)
					{
						n_text.text = string.Empty;
					}
				}
				try
				{
					if (component3.curClass >= 0 && component3.klasy[component3.curClass].team == Team.MTF && component.klasy[component.curClass].team == Team.MTF)
					{
						int num = 0;
						int num2 = 0;
						switch (component3.curClass)
						{
						case 4:
						case 11:
							num2 = 200;
							break;
						case 13:
							num2 = 100;
							break;
						case 12:
							num2 = 300;
							break;
						}
						switch (component.curClass)
						{
						case 4:
						case 11:
							num = 200;
							break;
						case 13:
							num = 100;
							break;
						case 12:
							num = 300;
							break;
						}
						Text text2 = n_text;
						text2.text = text2.text + " (" + GameObject.Find("Host").GetComponent<NineTailedFoxUnits>().GetNameById(component3.ntfUnit) + ")\n\n<b>";
						num -= component.ntfUnit;
						num2 -= component3.ntfUnit;
						if (num > num2)
						{
							n_text.text += TranslationReader.Get("Legancy_Interfaces", 0);
						}
						else if (num2 > num)
						{
							n_text.text += TranslationReader.Get("Legancy_Interfaces", 1);
						}
						else if (num2 == num)
						{
							n_text.text += TranslationReader.Get("Legancy_Interfaces", 2);
						}
						n_text.text += "</b>";
					}
				}
				catch
				{
					MonoBehaviour.print("Error");
				}
			}
		}
		transparency += Time.deltaTime * (float)((!flag) ? (-3) : 3);
		if (flag)
		{
			float max = (viewRange - Vector3.Distance(base.transform.position, hitInfo.point)) / viewRange;
			transparency = Mathf.Clamp(transparency, 0f, max);
		}
		transparency = Mathf.Clamp01(transparency);
		CanvasRenderer component4 = n_text.GetComponent<CanvasRenderer>();
		component4.SetAlpha(transparency);
	}

	[Command(channel = 2)]
	private void CmdSetNick(string n)
	{
		if (base.isLocalPlayer)
		{
			myNick = n;
		}
		else
		{
			if (ConfigFile.ServerConfig.GetBool("online_mode", true) || _nickSet)
			{
				return;
			}
			_nickSet = true;
			if (n == null)
			{
				ServerConsole.AddLog("Banned " + base.connectionToClient.address + " for passing null name.");
				PlayerManager.localPlayer.GetComponent<BanPlayer>().BanUser(base.gameObject, 26297460, string.Empty, "Server");
                myNick = "Null Name";
				return;
			}
			StringBuilder stringBuilder = new();
			char c = '0';
			bool flag = false;
			foreach (char c2 in n)
			{
				if (char.IsLetterOrDigit(c2) || char.IsPunctuation(c2) || char.IsSymbol(c2))
				{
					flag = true;
					stringBuilder.Append(c2);
				}
				else if (char.IsWhiteSpace(c2) && c2 != '\n' && c2 != '\r' && c2 != '\t')
				{
					stringBuilder.Append(c2);
				}
				else if (char.IsHighSurrogate(c2))
				{
					c = c2;
				}
				else if (char.IsLowSurrogate(c2) && char.IsSurrogatePair(c, c2))
				{
					stringBuilder.Append(c);
					stringBuilder.Append(c2);
					flag = true;
				}
			}
			string text = stringBuilder.ToString();
			if (text.Length > 32)
			{
				text = text.Substring(0, 32);
			}
			if (!flag)
			{
				ServerConsole.AddLog("Kicked " + base.connectionToClient.address + " for having an empty name.");
				ServerConsole.Disconnect(base.connectionToClient, "You may not have an empty name.");
				myNick = "Empty Name";
				return;
			}
			text = text.Replace("<", "＜");
			text = text.Replace(">", "＞");
			text = text.Replace("[", "(");
			text = text.Replace("]", ")");
			myNick = text;
			GetComponent<CharacterClassManager>().SyncServerCmdBinding();
		}
	}

	[ServerCallback]
	public void UpdateNickname(string n)
	{
		if (!NetworkServer.active)
		{
			return;
		}
		_nickSet = true;
		if (n == null)
		{
			ServerConsole.AddLog("Banned " + base.connectionToClient.address + " for passing null name.");
			PlayerManager.localPlayer.GetComponent<BanPlayer>().BanUser(base.gameObject, 26297460, string.Empty, "Server");
			myNick = "Null Name";
			return;
		}
		StringBuilder stringBuilder = new StringBuilder();
		char c = '0';
		bool flag = false;
		foreach (char c2 in n)
		{
			if (char.IsLetterOrDigit(c2) || char.IsPunctuation(c2) || char.IsSymbol(c2))
			{
				flag = true;
				stringBuilder.Append(c2);
			}
			else if (char.IsWhiteSpace(c2) && c2 != '\n' && c2 != '\r' && c2 != '\t')
			{
				stringBuilder.Append(c2);
			}
			else if (char.IsHighSurrogate(c2))
			{
				c = c2;
			}
			else if (char.IsLowSurrogate(c2) && char.IsSurrogatePair(c, c2))
			{
				stringBuilder.Append(c);
				stringBuilder.Append(c2);
				flag = true;
			}
		}
		string text = stringBuilder.ToString();
		if (text.Length > 32)
		{
			text = text.Substring(0, 32);
		}
		if (!flag)
		{
			ServerConsole.AddLog("Kicked " + base.connectionToClient.address + " for having an empty name.");
			ServerConsole.Disconnect(base.connectionToClient, "You may not have an empty name.");
			myNick = "Empty Name";
		}
		else
		{
			text = text.Replace("<", "＜");
			text = text.Replace(">", "＞");
			myNick = text;
		}
	}
}
