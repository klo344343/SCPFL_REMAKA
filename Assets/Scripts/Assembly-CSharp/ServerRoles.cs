using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Cryptography;
using GameConsole;
using MEC;
using Mirror;
using Org.BouncyCastle.Crypto;
using RemoteAdmin;
using UnityEngine;
using UnityEngine.Networking;

public class ServerRoles : NetworkBehaviour
{
	[Serializable]
	public class NamedColor
	{
		public string Name;

		public Gradient SpeakingColorIn;

		public Gradient SpeakingColorOut;

		public string ColorHex;

		public bool Restricted;
	}

	[Serializable]
	public enum AccessMode
	{
		LocalAccess = 1,
		GlobalAccess = 2,
		PasswordOverride = 3
	}

	public NamedColor CurrentColor;

	public NamedColor[] NamedColors;

	public Dictionary<string, string> FirstVerResult;

	internal AsymmetricKeyParameter PublicKey;

	public bool AuthroizeBadge;

	public bool BypassMode;

	public bool LocalRemoteAdmin;

	internal bool OverwatchPermitted;

	internal bool OverwatchEnabled;

	internal bool AmIInOverwatch;

	private bool _requested;

	private bool _badgeRequested;

	private bool _authRequested;

	internal string PrevBadge;

	private string _globalBadgeUnconfirmed;

	private string _prevColor;

	private string _prevText;

	private string _prevBadge;

	private string _badgeUserChallenge;

	private string _authChallenge;

	private string _badgeChallenge;

	private string _bgc;

	private string _bgt;

	public bool GlobalSet;

	public bool BadgeCover;

	public string FixedBadge;

	public int GlobalBadgeType;

	public bool RemoteAdmin;

	public bool Staff;

	public bool BypassStaff;

	public bool RaEverywhere;

	public ulong Permissions;

	public string HiddenBadge;

	public bool DoNotTrack;

	public AccessMode RemoteAdminMode;

    [SyncVar(hook = nameof(OnColorChanged))]
    public string MyColor = "default";

    [SyncVar(hook = nameof(OnTextChanged))]
    public string MyText = "";

    [SyncVar(hook = nameof(OnBadgeChanged))]
    public string GlobalBadge = "";

    private void OnColorChanged(string oldValue, string newValue)
    {
        SetColor(newValue);
    }

    private void OnTextChanged(string oldValue, string newValue)
    {
        SetText(newValue);
    }

    private void OnBadgeChanged(string oldValue, string newValue)
    {
		SetBadgeUpdate(newValue);
    }
    public void SetText(string i)
    {
        MyText = i;
        NamedColor namedColor = NamedColors.FirstOrDefault((NamedColor row) => row.Name == MyColor);
        if (namedColor != null)
        {
            CurrentColor = namedColor;
        }
    }
    public void SetColor(string newColor)
    {
        MyColor = newColor;
        var namedColor = NamedColors.FirstOrDefault(row => row.Name == newColor);
        if (namedColor == null && newColor != "default")
        {
            SetColor("default");
        }
        else
        {
            CurrentColor = namedColor;
        }
    }
    public void SetBadgeUpdate(string i)
    {
        GlobalBadge = i;
    }

	public void Start()
	{
		if (base.isLocalPlayer && GameConsole.Console.RequestDNT)
		{
			GameConsole.Console.singleton.AddLog("Sending \"Do not track\" request to the game server...", Color.grey);
			CmdDoNotTrack();
		}
	}

	[TargetRpc(channel = 2)]
	public void TargetSetHiddenRole(NetworkConnection connection, string role)
	{
		if (!base.isServer)
		{
			if (string.IsNullOrEmpty(role))
			{
				GlobalSet = false;
				SetColor("default");
				SetText(string.Empty);
				FixedBadge = string.Empty;
				SetText(string.Empty);
			}
			else
			{
				GlobalSet = true;
				SetColor("silver");
				FixedBadge = role.Replace("[", string.Empty).Replace("]", string.Empty).Replace("<", string.Empty)
					.Replace(">", string.Empty) + " (hidden)";
				SetText(FixedBadge);
			}
		}
	}

	[ClientRpc(channel = 2)]
	public void RpcResetFixed()
	{
		FixedBadge = string.Empty;
	}

	[Command(channel = 2)]
	public void CmdRequestBadge(string token)
	{
		if (!_requested)
		{
			_requested = true;
			Timing.RunCoroutine(_RequestRoleFromServer(token), Segment.FixedUpdate);
		}
	}

	[Command(channel = 2)]
	public void CmdDoNotTrack()
	{
		SetDoNotTrack();
	}

	public void SetDoNotTrack()
	{
		if (!DoNotTrack)
		{
			DoNotTrack = true;
			if (!string.IsNullOrEmpty(GetComponent<NicknameSync>().myNick))
			{
				LogDNT();
			}
			if (!base.isLocalPlayer)
			{
				GetComponent<GameConsoleTransmission>().SendToClient(base.connectionToClient, "Your \"Do not track\" request has been received.", "green");
			}
		}
	}

	public void LogDNT()
	{
		ServerLogs.AddLog(ServerLogs.Modules.Networking, "Player with nickname " + GetComponent<NicknameSync>().myNick + ", SteamID " + GetComponent<CharacterClassManager>().SteamId + " connected from IP " + base.connectionToClient.address + " sent Do Not Track signal.", ServerLogs.ServerLogType.ConnectionUpdate);
	}

	[ServerCallback]
	public void RefreshPermissions(bool disp = false)
	{
		if (NetworkServer.active)
		{
			UserGroup userGroup = ServerStatic.PermissionsHandler.GetUserGroup(GetComponent<CharacterClassManager>().SteamId);
			if (userGroup != null)
			{
				SetGroup(userGroup, false, false, disp);
			}
		}
	}

	[ServerCallback]
	public void SetGroup(UserGroup group, bool ovr, bool byAdmin = false, bool disp = false)
	{
		if (!NetworkServer.active)
		{
			return;
		}
		if (group == null)
		{
			if (!RaEverywhere || Permissions != ServerStatic.PermissionsHandler.FullPerm)
			{
				RemoteAdmin = false;
				Permissions = 0uL;
				RemoteAdminMode = AccessMode.LocalAccess;
				SetColor("default");
				SetText(string.Empty);
				BadgeCover = false;
				if (!string.IsNullOrEmpty(PrevBadge))
				{
					SetBadgeUpdate(PrevBadge);
				}
				TargetCloseRemoteAdmin(base.connectionToClient);
				GetComponent<CharacterClassManager>().TargetConsolePrint(base.connectionToClient, "Your local permissions has been revoked by server administrator.", "red");
			}
			return;
		}
		GetComponent<CharacterClassManager>().TargetConsolePrint(base.connectionToClient, byAdmin ? "Updating your group on server (set by server administrator)..." : "Updating your group on server (local permissions)...", "cyan");
		BadgeCover = group.Cover;
		if (!OverwatchPermitted && ServerStatic.PermissionsHandler.IsPermitted(group.Permissions, PlayerPermissions.Overwatch))
		{
			OverwatchPermitted = true;
		}
		if (group.Permissions != 0 && Permissions != ServerStatic.PermissionsHandler.FullPerm && ServerStatic.PermissionsHandler.IsRaPermitted(group.Permissions))
		{
			RemoteAdmin = true;
			Permissions = group.Permissions;
			RemoteAdminMode = ((!ovr) ? AccessMode.LocalAccess : AccessMode.PasswordOverride);
			GetComponent<QueryProcessor>().PasswordTries = 0;
			TargetOpenRemoteAdmin(base.connectionToClient, ovr);
			GetComponent<CharacterClassManager>().TargetConsolePrint(base.connectionToClient, byAdmin ? "Your remote admin access has been granted (set by server administrator)." : "Your remote admin access has been granted (local permissions).", "cyan");
			if (ServerStatic.PermissionsHandler.IsPermitted(Permissions, PlayerPermissions.ViewHiddenBadges))
			{
				GameObject[] players = PlayerManager.singleton.players;
				foreach (GameObject gameObject in players)
				{
					ServerRoles component = gameObject.GetComponent<ServerRoles>();
					if (!string.IsNullOrEmpty(component.HiddenBadge))
					{
						component.TargetSetHiddenRole(base.connectionToClient, component.HiddenBadge);
					}
				}
				GetComponent<CharacterClassManager>().TargetConsolePrint(base.connectionToClient, "Hidden badges have been displayed for you (if there are any).", "gray");
			}
		}
		else if (!RaEverywhere && Permissions != ServerStatic.PermissionsHandler.FullPerm)
		{
			RemoteAdmin = false;
			Permissions = 0uL;
			RemoteAdminMode = AccessMode.LocalAccess;
			TargetCloseRemoteAdmin(base.connectionToClient);
		}
		ServerLogs.AddLog(ServerLogs.Modules.Permissions, "User with nickname " + GetComponent<NicknameSync>().myNick + " and SteamID " + GetComponent<CharacterClassManager>().SteamId + " has been assigned to group " + group.BadgeText + " (local permissions).", ServerLogs.ServerLogType.RemoteAdminActivity_Misc);
		if (group.BadgeColor == "none")
		{
			return;
		}
		if (group.HiddenByDefault && !disp)
		{
			BadgeCover = false;
			if (string.IsNullOrEmpty(MyText))
			{
				GlobalSet = false;
				MyText = string.Empty;
				MyColor = string.Empty;
				HiddenBadge = group.BadgeText;
				RefreshHiddenTag();
				TargetSetHiddenRole(base.connectionToClient, group.BadgeText);
				if (!byAdmin)
				{
					GetComponent<CharacterClassManager>().TargetConsolePrint(base.connectionToClient, "Your role has been granted, but it's hidden. Use \"showtag\" command in the game console to show your server badge.", "yellow");
				}
				else
				{
					GetComponent<CharacterClassManager>().TargetConsolePrint(base.connectionToClient, "Your role has been granted to you (set by server administrator), but it's hidden. Use \"showtag\" command in the game console to show your server badge.", "cyan");
				}
			}
			return;
		}
		GlobalSet = false;
		HiddenBadge = string.Empty;
		RpcResetFixed();
		MyText = group.BadgeText;
		MyColor = group.BadgeColor;
		if (!byAdmin)
		{
			GetComponent<CharacterClassManager>().TargetConsolePrint(base.connectionToClient, "Your role \"" + group.BadgeText + "\" with color " + group.BadgeColor + " has been granted to you (local permissions).", "cyan");
		}
		else
		{
			GetComponent<CharacterClassManager>().TargetConsolePrint(base.connectionToClient, "Your role \"" + group.BadgeText + "\" with color " + group.BadgeColor + " has been granted to you (set by server administrator).", "cyan");
		}
	}

	[ServerCallback]
	public void RefreshHiddenTag()
	{
		if (!NetworkServer.active)
		{
			return;
		}
		PermissionsHandler handler = ServerStatic.PermissionsHandler;
		IEnumerable<GameObject> enumerable = PlayerManager.singleton.players.Where((GameObject player) => handler.IsPermitted(player.GetComponent<ServerRoles>().Permissions, PlayerPermissions.ViewHiddenBadges) || player.GetComponent<ServerRoles>().Staff);
		foreach (GameObject item in enumerable)
		{
			TargetSetHiddenRole(item.GetComponent<ServerRoles>().connectionToClient, HiddenBadge);
		}
	}

	private IEnumerator<float> _RequestRoleFromServer(string token)
	{
		Dictionary<string, string> dictionary = CentralAuth.ValidateBadgeRequest(token, GetComponent<CharacterClassManager>().SteamId, GetComponent<NicknameSync>().myNick);
		if (dictionary != null)
		{
			_globalBadgeUnconfirmed = token;
			StartServerChallenge(1);
		}
		yield break;
	}

	public string GetColoredRoleString(bool newLine = false)
	{
		if (string.IsNullOrEmpty(MyColor) || string.IsNullOrEmpty(MyText) || CurrentColor == null)
		{
			return string.Empty;
		}
		if ((CurrentColor.Restricted || MyText.Contains("[") || MyText.Contains("]") || MyText.Contains("<") || MyText.Contains(">")) && !AuthroizeBadge)
		{
			return string.Empty;
		}
		NamedColor namedColor = NamedColors.FirstOrDefault((NamedColor row) => row.Name == MyColor);
		if (namedColor != null)
		{
			return ((!newLine) ? string.Empty : "\n") + "<color=#" + namedColor.ColorHex + ">" + MyText + "</color>";
		}
		return string.Empty;
	}

	public string GetUncoloredRoleString()
	{
		if (string.IsNullOrEmpty(MyColor) || string.IsNullOrEmpty(MyText) || CurrentColor == null)
		{
			return string.Empty;
		}
		if ((CurrentColor.Restricted || MyText.Contains("[") || MyText.Contains("]") || MyText.Contains("<") || MyText.Contains(">")) && !AuthroizeBadge)
		{
			return string.Empty;
		}
		NamedColor namedColor = NamedColors.FirstOrDefault((NamedColor row) => row.Name == MyColor);
		if (namedColor != null)
		{
			return MyText;
		}
		return string.Empty;
	}

	public Color GetColor()
	{
		if (string.IsNullOrEmpty(MyColor) || MyColor == "default" || CurrentColor == null)
		{
			return Color.white;
		}
		if ((CurrentColor.Restricted || MyText.Contains("[") || MyText.Contains("]") || MyText.Contains("<") || MyText.Contains(">")) && !AuthroizeBadge)
		{
			return Color.white;
		}
		NamedColor namedColor = NamedColors.FirstOrDefault((NamedColor row) => row.Name == MyColor);
		return (namedColor != null) ? namedColor.SpeakingColorIn.Evaluate(1f) : Color.white;
	}

	public Gradient[] GetGradient()
	{
		NamedColor namedColor = NamedColors.FirstOrDefault((NamedColor row) => row.Name == MyColor);
		return (namedColor == null) ? null : new Gradient[2] { namedColor.SpeakingColorIn, namedColor.SpeakingColorOut };
	}

	private void Update()
	{
		if (CurrentColor == null)
		{
			return;
		}
		if (!string.IsNullOrEmpty(FixedBadge) && MyText != FixedBadge)
		{
			SetText(FixedBadge);
			SetColor("silver");
			return;
		}
		if (!string.IsNullOrEmpty(FixedBadge) && CurrentColor.Name != "silver")
		{
			SetColor("silver");
			return;
		}
		if (GlobalBadge != _prevBadge)
		{
			_prevBadge = GlobalBadge;
			if (string.IsNullOrEmpty(GlobalBadge))
			{
				_bgc = string.Empty;
				_bgt = string.Empty;
				AuthroizeBadge = false;
				_prevColor += ".";
				_prevText += ".";
				return;
			}
			GameConsole.Console.singleton.AddLog("Validating global badge of user " + GetComponent<NicknameSync>().myNick, Color.gray);
			Dictionary<string, string> dictionary = CentralAuth.ValidateBadgeRequest(GlobalBadge, GetComponent<CharacterClassManager>().SteamId, GetComponent<NicknameSync>().myNick);
			if (dictionary == null)
			{
				GameConsole.Console.singleton.AddLog("Validation of global badge of user " + GetComponent<NicknameSync>().myNick + " failed - invalid digital signature.", Color.red);
				_bgc = string.Empty;
				_bgt = string.Empty;
				AuthroizeBadge = false;
				_prevColor += ".";
				_prevText += ".";
				return;
			}
			GameConsole.Console.singleton.AddLog("Validation of global badge of user " + GetComponent<NicknameSync>().myNick + " complete - badge signed by central server " + dictionary["Issued by"] + ".", Color.grey);
			_bgc = dictionary["Badge color"];
			_bgt = dictionary["Badge text"];
			MyColor = dictionary["Badge color"];
			MyText = dictionary["Badge text"];
			AuthroizeBadge = true;
		}
		if (!(_prevColor == MyColor) || !(_prevText == MyText))
		{
			if (CurrentColor.Restricted && (MyText != _bgt || MyColor != _bgc))
			{
				GameConsole.Console.singleton.AddLog("TAG FAIL 1 - " + MyText + " - " + _bgt + " /-/ " + MyColor + " - " + _bgc, Color.gray);
				AuthroizeBadge = false;
				MyColor = string.Empty;
				MyText = string.Empty;
				_prevColor = string.Empty;
				_prevText = string.Empty;
				PlayerList.UpdatePlayerRole(base.gameObject);
			}
			else if ((MyText != _bgt && (MyText.Contains("[") || MyText.Contains("]"))) || MyText.Contains("<") || MyText.Contains(">"))
			{
				GameConsole.Console.singleton.AddLog("TAG FAIL 2 - " + MyText + " - " + _bgt + " /-/ " + MyColor + " - " + _bgc, Color.gray);
				AuthroizeBadge = false;
				MyColor = string.Empty;
				MyText = string.Empty;
				_prevColor = string.Empty;
				_prevText = string.Empty;
				PlayerList.UpdatePlayerRole(base.gameObject);
			}
			else
			{
				_prevColor = MyColor;
				_prevText = MyText;
				_prevBadge = GlobalBadge;
				PlayerList.UpdatePlayerRole(base.gameObject);
			}
		}
	}


	[ServerCallback]
	public void StartServerChallenge(int selector)
	{
		if (NetworkServer.active && (selector != 0 || string.IsNullOrEmpty(_authChallenge)) && (selector != 1 || string.IsNullOrEmpty(_badgeChallenge)) && selector <= 1 && selector >= 0)
		{
			byte[] array;
			using (RandomNumberGenerator randomNumberGenerator = new RNGCryptoServiceProvider())
			{
				array = new byte[32];
				randomNumberGenerator.GetBytes(array);
			}
			string text = Convert.ToBase64String(array);
			if (selector == 0)
			{
				_authChallenge = "auth-" + text;
				TargetSignServerChallenge(base.connectionToClient, _authChallenge);
			}
			else
			{
				_badgeChallenge = "badge-server-" + text;
				TargetSignServerChallenge(base.connectionToClient, _badgeChallenge);
			}
		}
	}

	[TargetRpc(channel = 2)]
	public void TargetSignServerChallenge(NetworkConnection target, string challenge)
	{
		if (challenge.StartsWith("auth-"))
		{
			if (_authRequested)
			{
				return;
			}
			_authRequested = true;
		}
		else
		{
			if (!challenge.StartsWith("badge-server-") || _badgeRequested)
			{
				return;
			}
			_badgeRequested = true;
		}
		string response = ECDSA.Sign(challenge, GameConsole.Console.SessionKeys.Private);
		GameConsole.Console.singleton.AddLog("Signed " + challenge + " for server.", Color.cyan);
		CmdServerSignatureComplete(challenge, response, ECDSA.KeyToString(GameConsole.Console.SessionKeys.Public), GameConsole.Console.HideBadge);
	}

	[Command(channel = 2)]
	public void CmdServerSignatureComplete(string challenge, string response, string publickey, bool hide)
	{
		if (FirstVerResult == null)
		{
			FirstVerResult = CentralAuth.ValidateBadgeRequest(_globalBadgeUnconfirmed, GetComponent<CharacterClassManager>().SteamId, GetComponent<NicknameSync>().myNick);
		}
		if (FirstVerResult == null)
		{
			return;
		}
		if (FirstVerResult["Public key"] != Misc.Base64Encode(Sha.HashToString(Sha.Sha256(publickey))))
		{
			GameConsole.Console.singleton.AddLog("Rejected signature of challenge " + challenge + " due to public key hash mismatch.", Color.red);
			GetComponent<CharacterClassManager>().TargetConsolePrint(base.connectionToClient, "Challenge signature rejected due to public key mismatch.", "red");
			return;
		}
		if (PublicKey == null)
		{
			PublicKey = ECDSA.PublicKeyFromString(publickey);
		}
		if (!ECDSA.Verify(challenge, response, PublicKey))
		{
			GameConsole.Console.singleton.AddLog("Rejected signature of challenge " + challenge + " due to signature mismatch.", Color.red);
			GetComponent<CharacterClassManager>().TargetConsolePrint(base.connectionToClient, "Challenge signature rejected due to signature mismatch.", "red");
		}
		else if (challenge.StartsWith("auth-") && challenge == _authChallenge)
		{
			GetComponent<CharacterClassManager>().SteamId = FirstVerResult["Steam ID"];
			GetComponent<NicknameSync>().UpdateNickname(Misc.Base64Decode(FirstVerResult["Nickname"]));
			if (DoNotTrack)
			{
				LogDNT();
			}
			GetComponent<CharacterClassManager>().TargetConsolePrint(base.connectionToClient, "Hi " + Misc.Base64Decode(FirstVerResult["Nickname"]) + "! Your challenge signature has been accepted.", "green");
			GetComponent<RemoteAdminCryptographicManager>().StartExchange();
			RefreshPermissions();
			_authChallenge = string.Empty;
		}
		else
		{
			if (!challenge.StartsWith("badge-server-") || !(challenge == _badgeChallenge))
			{
				return;
			}
			Dictionary<string, string> dictionary = CentralAuth.ValidateBadgeRequest(_globalBadgeUnconfirmed, GetComponent<CharacterClassManager>().SteamId, GetComponent<NicknameSync>().myNick);
			if (dictionary == null)
			{
				ServerConsole.AddLog("Rejected signature of challenge " + challenge + " due to signature mismatch.");
				GetComponent<CharacterClassManager>().TargetConsolePrint(base.connectionToClient, "Challenge signature rejected due to signature mismatch.", "red");
				return;
			}
			PrevBadge = _globalBadgeUnconfirmed;
			if (dictionary["Remote admin"] == "YES" || dictionary["Management"] == "YES" || dictionary["Global banning"] == "YES")
			{
				Staff = true;
			}
			if (dictionary["Management"] == "YES" || dictionary["Global banning"] == "YES")
			{
				RaEverywhere = true;
			}
			if (dictionary["Overwatch mode"] == "YES")
			{
				OverwatchPermitted = true;
			}
			if (dictionary["Remote admin"] == "YES" && ServerStatic.PermissionsHandler.StaffAccess)
			{
				RemoteAdmin = true;
				Permissions = ServerStatic.PermissionsHandler.FullPerm;
				RemoteAdminMode = AccessMode.GlobalAccess;
				GetComponent<QueryProcessor>().PasswordTries = 0;
				TargetOpenRemoteAdmin(base.connectionToClient, false);
				GetComponent<CharacterClassManager>().TargetConsolePrint(base.connectionToClient, "Your remote admin access has been granted (global permissions - staff).", "cyan");
			}
			else if (dictionary["Management"] == "YES" && ServerStatic.PermissionsHandler.ManagersAccess)
			{
				RemoteAdmin = true;
				Permissions = ServerStatic.PermissionsHandler.FullPerm;
				RemoteAdminMode = AccessMode.GlobalAccess;
				GetComponent<QueryProcessor>().PasswordTries = 0;
				TargetOpenRemoteAdmin(base.connectionToClient, false);
				GetComponent<CharacterClassManager>().TargetConsolePrint(base.connectionToClient, "Your remote admin access has been granted (global permissions - management).", "cyan");
			}
			else if (dictionary["Global banning"] == "YES" && ServerStatic.PermissionsHandler.BanningTeamAccess)
			{
				RemoteAdmin = true;
				Permissions = ServerStatic.PermissionsHandler.FullPerm;
				RemoteAdminMode = AccessMode.GlobalAccess;
				GetComponent<QueryProcessor>().PasswordTries = 0;
				TargetOpenRemoteAdmin(base.connectionToClient, false);
				GetComponent<CharacterClassManager>().TargetConsolePrint(base.connectionToClient, "Your remote admin access has been granted (global permissions - banning team).", "cyan");
			}
			if (!BadgeCover || string.IsNullOrEmpty(MyText) || string.IsNullOrEmpty(MyColor))
			{
				if (dictionary["Badge type"] == "3")
				{
					hide = true;
				}
				else if (dictionary["Badge type"] == "4" && ConfigFile.ServerConfig.GetBool("hide_banteam_badges_by_default"))
				{
					hide = true;
				}
				else if (dictionary["Badge type"] == "1" && ConfigFile.ServerConfig.GetBool("hide_staff_badges_by_default"))
				{
					hide = true;
				}
				else if (dictionary["Badge type"] == "2" && ConfigFile.ServerConfig.GetBool("hide_management_badges_by_default"))
				{
					hide = true;
				}
				else if (dictionary["Badge type"] == "0" && ConfigFile.ServerConfig.GetBool("hide_patreon_badges_by_default") && !ServerStatic.PermissionsHandler.IsVerified)
				{
					hide = true;
				}
				int result = 0;
				GlobalSet = true;
				if (int.TryParse(dictionary["Badge type"], out result))
				{
					GlobalBadgeType = result;
				}
				if (hide)
				{
					HiddenBadge = dictionary["Badge text"];
					RefreshHiddenTag();
					GetComponent<CharacterClassManager>().TargetConsolePrint(base.connectionToClient, "Your global badge has been granted, but it's hidden. Use \"gtag\" command in the game console to show your global badge.", "yellow");
				}
				else
				{
					HiddenBadge = string.Empty;
					RpcResetFixed();
					SetBadgeUpdate(_globalBadgeUnconfirmed);
					GetComponent<CharacterClassManager>().TargetConsolePrint(base.connectionToClient, "Your global badge has been granted.", "cyan");
				}
			}
			else
			{
				GetComponent<CharacterClassManager>().TargetConsolePrint(base.connectionToClient, "Your global badge is covered by server badge. Use \"gtag\" command in the game console to show your global badge.", "yellow");
			}
			_badgeChallenge = string.Empty;
			_globalBadgeUnconfirmed = string.Empty;
			if (!Staff)
			{
				return;
			}
			GameObject[] players = PlayerManager.singleton.players;
			foreach (GameObject gameObject in players)
			{
				ServerRoles component = gameObject.GetComponent<ServerRoles>();
				if (!string.IsNullOrEmpty(component.HiddenBadge))
				{
					component.TargetSetHiddenRole(base.connectionToClient, component.HiddenBadge);
				}
			}
			GetComponent<CharacterClassManager>().TargetConsolePrint(base.connectionToClient, "Hidden badges have been displayed for you (if there are any).", "gray");
		}
	}

	[TargetRpc]
	internal void TargetOpenRemoteAdmin(NetworkConnection connection, bool password)
	{
		LocalRemoteAdmin = true;
		if (!base.isServer)
		{
			if (password && RemoteAdminMode != AccessMode.PasswordOverride)
			{
				RemoteAdminMode = AccessMode.PasswordOverride;
			}
			else if (!password && RemoteAdminMode == AccessMode.PasswordOverride)
			{
				RemoteAdminMode = AccessMode.LocalAccess;
			}
		}
		UnityEngine.Object.FindObjectOfType<UIController>().ActivateRemoteAdmin();
	}

	[TargetRpc]
	internal void TargetCloseRemoteAdmin(NetworkConnection connection)
	{
		LocalRemoteAdmin = false;
		UnityEngine.Object.FindObjectOfType<UIController>().DeactivateRemoteAdmin();
	}

	[Command(channel = 2)]
	public void CmdSetOverwatchStatus(bool status)
	{
		if (!OverwatchPermitted && status)
		{
			GetComponent<CharacterClassManager>().TargetConsolePrint(base.connectionToClient, "You don't have permissions to enable overwatch mode!", "red");
		}
		else
		{
			SetOverwatchStatus(status);
		}
	}

	[Command(channel = 2)]
	public void CmdToggleOverwatch()
	{
		if (!OverwatchPermitted && !OverwatchEnabled)
		{
			GetComponent<CharacterClassManager>().TargetConsolePrint(base.connectionToClient, "You don't have permissions to enable overwatch mode!", "red");
		}
		else
		{
			SetOverwatchStatus(!OverwatchEnabled);
		}
	}

	public void SetOverwatchStatus(bool status)
	{
		OverwatchEnabled = status;
		CharacterClassManager component = GetComponent<CharacterClassManager>();
		if (status && component.curClass != 2 && component.curClass >= 0)
		{
			component.SetClassID(2);
		}
		TargetSetOverwatch(base.connectionToClient, OverwatchEnabled);
	}

	public void RequestBadge(string token)
	{
		CmdRequestBadge(token);
	}

	[TargetRpc(channel = 2)]
	public void TargetSetOverwatch(NetworkConnection conn, bool s)
	{
		GameConsole.Console.singleton.AddLog("Overwatch status: " + ((!s) ? "DISABLED" : "ENABLED"), Color.green);
		AmIInOverwatch = s;
	}
}
