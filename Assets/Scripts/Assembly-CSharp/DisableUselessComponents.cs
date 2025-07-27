using System.Runtime.InteropServices;
using Mirror;
using RemoteAdmin;
using UnityEngine;
using UnityEngine.Networking;

public class DisableUselessComponents : NetworkBehaviour
{
	private CharacterClassManager _ccm;

	private NicknameSync _ns;

	private bool _added;

	[SerializeField]
	private Behaviour[] uselessComponents;

	[SyncVar(hook = nameof(SetName))]
	private string label = "Player";

	[SyncVar(hook = nameof(SetServer))]
	public bool isDedicated = true;

    private void SetName(string oldValue, string newValue) => label = newValue;
    private void SetServer(bool oldValue, bool newValue) => isDedicated = newValue;

    private void Start()
	{
		_ns = GetComponent<NicknameSync>();
		if (NetworkServer.active)
		{
			CmdSetName((!base.isLocalPlayer) ? "Player" : "Host", base.isLocalPlayer && ServerStatic.IsDedicated);
		}
		_ccm = GetComponent<CharacterClassManager>();
		if (!base.isLocalPlayer)
		{
			Object.DestroyImmediate(GetComponent<FirstPersonController>());
			Behaviour[] array = uselessComponents;
			foreach (Behaviour behaviour in array)
			{
				behaviour.enabled = false;
			}
			Object.Destroy(GetComponent<CharacterController>());
		}
		else
		{
			PlayerManager.localPlayer = base.gameObject;
			PlayerManager.spect = GetComponent<SpectatorManager>();
			GetComponent<FirstPersonController>().enabled = false;
		}
	}

	private void FixedUpdate()
	{
		if (!_added && _ccm.IsVerified && !string.IsNullOrEmpty(_ns.myNick))
		{
			_added = true;
			if (!isDedicated)
			{
				PlayerManager.singleton.AddPlayer(base.gameObject);
			}
			if (NetworkServer.active)
			{
				ServerLogs.AddLog(ServerLogs.Modules.Networking, "Player connected and authenticated from IP " + base.connectionToClient.address + " with SteamID " + ((!string.IsNullOrEmpty(GetComponent<CharacterClassManager>().SteamId)) ? GetComponent<CharacterClassManager>().SteamId : "(unavailable)") + " and nickname " + GetComponent<NicknameSync>().myNick + ". Assigned Player ID: " + GetComponent<QueryProcessor>().PlayerId + ".", ServerLogs.ServerLogType.ConnectionUpdate);
			}
		}
		base.name = label;
	}

	private void OnDestroy()
	{
		if (!base.isLocalPlayer && PlayerManager.singleton != null)
		{
			PlayerManager.singleton.RemovePlayer(base.gameObject);
		}
	}

	[ServerCallback]
	private void CmdSetName(string n, bool b)
	{
		if (NetworkServer.active)
		{
			label = n;
			isDedicated = b;
		}
	}
}
