using Mirror;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Networking;

public class VersionCheck : NetworkBehaviour
{

	private string clientVersion = string.Empty;

	private bool isChecked;

    [SyncVar(hook = nameof(SyncVersion))]
    public string serverVersion = string.Empty;

    private void SyncVersion(string oldValue, string newValue)
    {
        serverVersion = newValue;
    }

    private void Start()
	{
		clientVersion = CustomNetworkManager.CompatibleVersions[0];
		if (NetworkServer.active)
		{
            serverVersion = clientVersion;
		}
	}

	private void Update()
	{
		if (!isChecked && base.name == "Host" && !string.IsNullOrEmpty(serverVersion))
		{
			isChecked = true;
			if (serverVersion != clientVersion)
			{
				CustomNetworkManager customNetworkManager = Object.FindObjectOfType<CustomNetworkManager>();
				customNetworkManager.StopClient();
				customNetworkManager.ShowLog(16, clientVersion, serverVersion);
			}
		}
	}
}
