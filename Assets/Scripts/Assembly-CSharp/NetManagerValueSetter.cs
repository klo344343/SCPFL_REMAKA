using Mirror;
using UnityEngine;
using UnityEngine.Networking;

public class NetManagerValueSetter : MonoBehaviour
{
	private CustomNetworkManager _singleton;

	private void Start()
	{
		_singleton = NetworkManager.singleton.GetComponent<CustomNetworkManager>();
	}

	public void ChangeIP(string ip)
	{
		_singleton.networkAddress = ip;
		CustomNetworkManager.ConnectionIp = ip;
	}

	public void ChangePort(int port)
	{
        ServerConsole.Port = port;
	}

	public void JoinGame()
	{
		_singleton.StartClient();
	}

	public void HostGame()
	{
		_singleton.StartHost();
	}

	public void Disconnect()
	{
		_singleton.StopHost();
	}
}
