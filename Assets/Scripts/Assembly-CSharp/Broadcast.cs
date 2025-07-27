using System;
using System.Collections.Generic;
using GameConsole;
using Mirror;
using UnityEngine;
using UnityEngine.Networking;

public class Broadcast : NetworkBehaviour
{
	public static Queue<BroadcastMessage> Messages;
	private void Start()
	{
		if (base.isLocalPlayer)
		{
			Messages = new Queue<BroadcastMessage>();
		}
	}

	[TargetRpc(channel = 2)]
	public void TargetAddElement(NetworkConnection conn, string data, uint time, bool monospaced)
	{
		AddElement(data, time, monospaced);
	}

	[ClientRpc(channel = 2)]
	public void RpcAddElement(string data, uint time, bool monospaced)
	{
		AddElement(data, time, monospaced);
	}

	[TargetRpc(channel = 2)]
	public void TargetClearElements(NetworkConnection conn)
	{
		Messages.Clear();
		BroadcastAssigner.MessageDisplayed = false;
	}

	[ClientRpc(channel = 2)]
	public void RpcClearElements()
	{
		Messages.Clear();
		BroadcastAssigner.MessageDisplayed = false;
	}

	public static void AddElement(string data, uint time, bool monospaced)
	{
		if (time >= 1 && Messages.Count <= 25 && !string.IsNullOrEmpty(data) && data.Length <= 3072)
		{
			if (time > 300)
			{
				time = 10u;
			}
			Messages.Enqueue(new BroadcastMessage(data.Replace("\\n", Environment.NewLine), time, monospaced));
			BroadcastAssigner.Displaying = true;
			if (GameConsole.Console.singleton != null)
			{
				GameConsole.Console.singleton.AddLog("[BROADCAST FROM SERVER] " + data.Replace("<", "[").Replace(">", "]") + ", time: " + time + ", monospace: " + ((!monospaced) ? "NO" : "YES"), Color.grey);
			}
		}
	}
}
