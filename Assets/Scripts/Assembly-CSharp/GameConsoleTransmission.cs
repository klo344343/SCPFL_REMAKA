using System;
using Cryptography;
using GameConsole;
using Mirror;
using Org.BouncyCastle.Security;
using RemoteAdmin;
using UnityEngine;
using UnityEngine.Networking;

public class GameConsoleTransmission : NetworkBehaviour
{
	public RemoteAdminCryptographicManager CryptoManager;

	public QueryProcessor Processor;

	public GameConsole.Console Console;

	public static SecureRandom SecureRandom;

	private static int kTargetRpcTargetPrintOnConsole;

	private static int kCmdCmdCommandToServer;

	private void Start()
	{
		CryptoManager = GetComponent<RemoteAdminCryptographicManager>();
		Processor = GetComponent<QueryProcessor>();
		if (SecureRandom == null)
		{
			SecureRandom = new SecureRandom();
		}
		if (base.isLocalPlayer)
		{
			Console = GameConsole.Console.singleton;
		}
	}

	[Server]
	public void SendToClient(NetworkConnection connection, string text, string color)
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Void GameConsoleTransmission::SendToClient(UnityEngine.Networking.NetworkConnection,System.String,System.String)' called on client");
			return;
		}
		byte[] bytes = Utf8.GetBytes(color + "#" + text);
		if (CryptoManager.EncryptionKey == null)
		{
			TargetPrintOnConsole(connection, bytes, false);
		}
		else
		{
			TargetPrintOnConsole(connection, AES.AesGcmEncrypt(bytes, CryptoManager.EncryptionKey, SecureRandom), true);
		}
	}

	[TargetRpc(channel = 15)]
	public void TargetPrintOnConsole(NetworkConnection connection, byte[] data, bool encrypted)
	{
		string empty = string.Empty;
		if (!encrypted)
		{
			empty = Utf8.GetString(data);
		}
		else
		{
			if (CryptoManager.EncryptionKey == null)
			{
				Console.AddLog("Can't process encrypted message from server before completing ECDHE exchange.", Color.magenta);
				return;
			}
			try
			{
				byte[] data2 = AES.AesGcmDecrypt(data, CryptoManager.EncryptionKey);
				empty = Utf8.GetString(data2);
			}
			catch
			{
				GetComponent<CharacterClassManager>().TargetConsolePrint(base.connectionToClient, "Decryption or verification of encrypted message failed.", "magenta");
				return;
			}
		}
		string text = empty.Remove(empty.IndexOf("#", StringComparison.Ordinal));
		empty = empty.Remove(0, empty.IndexOf("#", StringComparison.Ordinal) + 1);
		Console.AddLog(((!encrypted) ? "[UNENCRYPTED FROM SERVER] " : "[FROM SERVER] ") + empty, ProcessColor(text));
	}

	[Client]
	public void SendToServer(string command)
	{
		if (!NetworkClient.active)
		{
			Debug.LogWarning("[Client] function 'System.Void GameConsoleTransmission::SendToServer(System.String)' called on server");
			return;
		}
		byte[] bytes = Utf8.GetBytes(command);
		if (CryptoManager.EncryptionKey == null)
		{
			CmdCommandToServer(bytes, false);
		}
		else
		{
			CmdCommandToServer(AES.AesGcmEncrypt(bytes, CryptoManager.EncryptionKey, SecureRandom), true);
		}
	}

	[Command(channel = 15)]
	public void CmdCommandToServer(byte[] data, bool encrypted)
	{
		string empty = string.Empty;
		if (!encrypted)
		{
			if (CryptoManager.EncryptionKey != null || CryptoManager.ExchangeRequested)
			{
				SendToClient(base.connectionToClient, "Please use encrypted connection to send commands.", "magenta");
				return;
			}
			empty = Utf8.GetString(data);
		}
		else
		{
			if (CryptoManager.EncryptionKey == null)
			{
				SendToClient(base.connectionToClient, "Can't process encrypted message from server before completing ECDHE exchange.", "magenta");
				return;
			}
			try
			{
				byte[] data2 = AES.AesGcmDecrypt(data, CryptoManager.EncryptionKey);
				empty = Utf8.GetString(data2);
			}
			catch
			{
				SendToClient(base.connectionToClient, "Decryption or verification of encrypted message failed.", "magenta");
				return;
			}
		}
		Processor.ProcessGameConsoleQuery(empty, encrypted);
	}

	public Color ProcessColor(string name)
	{
		Color grey = Color.grey;
		switch (name)
		{
		case "red":
			return Color.red;
		case "cyan":
			return Color.cyan;
		case "blue":
			return Color.blue;
		case "magenta":
			return Color.magenta;
		case "white":
			return Color.white;
		case "green":
			return Color.green;
		case "yellow":
			return Color.yellow;
		case "black":
			return Color.black;
		default:
			return Color.grey;
		}
	}
}
