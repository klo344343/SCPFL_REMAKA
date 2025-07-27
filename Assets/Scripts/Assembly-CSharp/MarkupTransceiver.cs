using System.Collections.Generic;
using GameConsole;
using Mirror;
using RemoteAdmin;
using UnityEngine;
using UnityEngine.Networking;

public class MarkupTransceiver : NetworkBehaviour
{
	[ServerCallback]
	public void Transmit(string code, int[] playerIDs)
	{
		if (NetworkServer.active)
		{
			NetworkConnection[] targets = GetTargets(playerIDs);
			foreach (NetworkConnection target in targets)
			{
				TargetRpcReceiveData(target, code);
			}
		}
	}

	[ServerCallback]
	public void RequestStyleDownload(string url, int[] playerIDs)
	{
		if (NetworkServer.active)
		{
			NetworkConnection[] targets = GetTargets(playerIDs);
			foreach (NetworkConnection conn in targets)
			{
				TargetRpcDownloadStyle(conn, url);
			}
		}
	}

	[TargetRpc]
	private void TargetRpcDownloadStyle(NetworkConnection conn, string url)
	{
		if (Console.DisableSLML || Console.DisableRemoteSLML)
		{
			Console.singleton.AddLog("Rejected REMOTE SLML from the game server - disabled by user.", Color.gray);
		}
		else
		{
			MarkupReader.singleton.AddStyleFromURL(url);
		}
	}

	public NetworkConnection[] GetTargets(int[] playerIDs)
	{
		List<NetworkConnection> list = new List<NetworkConnection>();
		GameObject[] players = PlayerManager.singleton.players;
		foreach (GameObject gameObject in players)
		{
			QueryProcessor component = gameObject.GetComponent<QueryProcessor>();
			foreach (int num in playerIDs)
			{
				if (component.PlayerId == num)
				{
					list.Add(component.connectionToClient);
				}
			}
		}
		return list.ToArray();
	}

	[TargetRpc]
	private void TargetRpcReceiveData(NetworkConnection target, string code)
	{
		if (Console.DisableSLML)
		{
			Console.singleton.AddLog("Rejected SLML from the game server - disabled by user.", Color.gray);
		}
		else
		{
			MarkupWriter.singleton.ReadTag(code);
		}
	}
}
