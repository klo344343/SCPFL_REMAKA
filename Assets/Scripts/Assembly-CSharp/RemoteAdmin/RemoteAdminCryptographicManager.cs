using Cryptography;
using GameConsole;
using Mirror;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Agreement;
using UnityEngine;
using UnityEngine.Networking;

namespace RemoteAdmin
{
	public class RemoteAdminCryptographicManager : NetworkBehaviour
	{
		internal AsymmetricCipherKeyPair EcdhKeys;

		internal ECDHBasicAgreement Exchange;

		internal byte[] EcdhPublicKeySignature;

		internal bool ExchangeRequested;

		internal byte[] EncryptionKey;

		private static int kTargetRpcTargetDiffieHellmanExchange;

		private static int kCmdCmdDiffieHellmanExchange;

		public void Init()
		{
			EcdhKeys = ECDH.GenerateKeys();
			Exchange = ECDH.Init(EcdhKeys);
			ExchangeRequested = true;
		}

		[Server]
		public void StartExchange()
		{
			if (!NetworkServer.active)
			{
				Debug.LogWarning("[Server] function 'System.Void RemoteAdmin.RemoteAdminCryptographicManager::StartExchange()' called on client");
				return;
			}
			if (Exchange == null || EcdhKeys == null)
			{
				Init();
			}
			TargetDiffieHellmanExchange(base.connectionToClient, ECDSA.KeyToString(EcdhKeys.Public));
		}

		[TargetRpc(channel = 2)]
		public void TargetDiffieHellmanExchange(NetworkConnection conn, string publicKey)
		{
			if (EncryptionKey != null)
			{
				Console.singleton.AddLog("Rejected duplicated Elliptic-curve Diffie–Hellman (ECDH) parameters from server.", Color.magenta);
				return;
			}
			if (Exchange == null || EcdhKeys == null)
			{
				Init();
			}
			if (EcdhPublicKeySignature == null)
			{
				EcdhPublicKeySignature = ECDSA.SignBytes(ECDSA.KeyToString(EcdhKeys.Public), Console.SessionKeys.Private);
			}
			EncryptionKey = ECDH.DeriveKey(Exchange, ECDSA.PublicKeyFromString(publicKey));
			CmdDiffieHellmanExchange(ECDSA.KeyToString(EcdhKeys.Public), EcdhPublicKeySignature);
			Console.singleton.AddLog("Completed ECDHE exchange with server.", Color.grey);
		}

		[Command(channel = 2)]
		public void CmdDiffieHellmanExchange(string publicKey, byte[] signature)
		{
			if (EncryptionKey == null && Exchange != null && EcdhKeys != null)
			{
				bool onlineMode = GetComponent<CharacterClassManager>().OnlineMode;
				AsymmetricKeyParameter publicKey2 = GetComponent<ServerRoles>().PublicKey;
				string authToken = GetComponent<CharacterClassManager>().AuthToken;
				if (onlineMode && (publicKey == null || authToken == null))
				{
					GetComponent<CharacterClassManager>().TargetConsolePrint(base.connectionToClient, "Please complete authentication before requesting ECDHE exchange.", "magenta");
				}
				else if (onlineMode && publicKey2 != null && !ECDSA.VerifyBytes(publicKey, signature, publicKey2))
				{
					GetComponent<CharacterClassManager>().TargetConsolePrint(base.connectionToClient, "Exchange parameters signature is invalid!", "magenta");
				}
				else
				{
					EncryptionKey = ECDH.DeriveKey(Exchange, ECDSA.PublicKeyFromString(publicKey));
				}
			}
		}
	}
}
