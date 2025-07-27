using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cryptography;
using GameConsole;
using RemoteAdmin;
using UnityEngine;
using UnityEngine.Networking;
using Mirror;

public class CheaterReport : NetworkBehaviour
{
	private int reportedPlayersAmount;

	private float lastReport;

	private HashSet<int> reportedPlayers;

	internal void Report(int playerId, string reason)
	{
		GameObject gameObject = PlayerManager.singleton.players.FirstOrDefault((GameObject pl) => pl.GetComponent<QueryProcessor>().PlayerId == playerId);
		if (gameObject == null)
		{
			GameConsole.Console.singleton.AddLog("[REPORTING] Can't find player with that PlayerID.", Color.red);
			return;
		}
		gameObject.GetComponent<CharacterClassManager>().CheatReported = true;
		CmdReport(playerId, reason, ECDSA.SignBytes(gameObject.GetComponent<CharacterClassManager>().SteamId + ";" + reason, GameConsole.Console.SessionKeys.Private));
	}

	[Command(channel = 2)]
	internal void CmdReport(int playerId, string reason, byte[] signature)
	{
		float num = Time.time - lastReport;
		if (num < 2f)
		{
			GetComponent<GameConsoleTransmission>().SendToClient(base.connectionToClient, "[REPORTING] Reporting rate limit exceeded (1).", "red");
			return;
		}
		if (num > 60f)
		{
			reportedPlayersAmount = 0;
		}
		if (reportedPlayersAmount > 5)
		{
			GetComponent<GameConsoleTransmission>().SendToClient(base.connectionToClient, "[REPORTING] Reporting rate limit exceeded (2).", "red");
			return;
		}
		if (!ServerStatic.GetPermissionsHandler().IsVerified || string.IsNullOrEmpty(ServerConsole.Password))
		{
			GetComponent<GameConsoleTransmission>().SendToClient(base.connectionToClient, "[REPORTING] Server is not verified - you can't use report feature on this server.", "red");
			return;
		}
		GameObject gameObject = PlayerManager.singleton.players.FirstOrDefault((GameObject pl) => pl.GetComponent<QueryProcessor>().PlayerId == playerId);
		if (gameObject == null)
		{
			GetComponent<GameConsoleTransmission>().SendToClient(base.connectionToClient, "[REPORTING] Can't find player with that PlayerID.", "red");
			return;
		}
		CharacterClassManager reportedCcm = gameObject.GetComponent<CharacterClassManager>();
		CharacterClassManager reporterCcm = GetComponent<CharacterClassManager>();
		if (reportedPlayers == null)
		{
			reportedPlayers = new HashSet<int>();
		}
		if (reportedPlayers.Contains(playerId))
		{
			GetComponent<GameConsoleTransmission>().SendToClient(base.connectionToClient, "[REPORTING] You have already reported that player.", "red");
			return;
		}
		if (string.IsNullOrEmpty(reportedCcm.SteamId))
		{
			GetComponent<GameConsoleTransmission>().SendToClient(base.connectionToClient, "[REPORTING] Failed: SteamID of reported player is null.", "red");
			return;
		}
		if (string.IsNullOrEmpty(reporterCcm.SteamId))
		{
			GetComponent<GameConsoleTransmission>().SendToClient(base.connectionToClient, "[REPORTING] Failed: your SteamID of is null.", "red");
			return;
		}
		if (!ECDSA.VerifyBytes(reportedCcm.SteamId + ";" + reason, signature, GetComponent<ServerRoles>().PublicKey))
		{
			GetComponent<GameConsoleTransmission>().SendToClient(base.connectionToClient, "[REPORTING] Invalid report signature.", "red");
			return;
		}
		lastReport = Time.time;
		reportedPlayersAmount++;
		GameConsole.Console.singleton.AddLog(string.Format("Player {0}({1}) reported player {2}({3}) with reason {4}.", reporterCcm.GetComponent<NicknameSync>().myNick, reporterCcm.SteamId, reportedCcm.GetComponent<NicknameSync>().myNick, reportedCcm.SteamId, reason), Color.gray);
		Thread thread = new Thread((ThreadStart)delegate
		{
			IssueReport(GetComponent<GameConsoleTransmission>(), reportedCcm.AuthToken, reportedCcm.connectionToClient.address, reporterCcm.AuthToken, reporterCcm.connectionToClient.address, ref reason, ref signature, ECDSA.KeyToString(GetComponent<ServerRoles>().PublicKey), playerId);
		});
		thread.Priority = System.Threading.ThreadPriority.Lowest;
		thread.IsBackground = true;
		thread.Name = "Reporting player - " + reportedCcm.SteamId + " by " + reporterCcm.SteamId;
		thread.Start();
	}

	private void IssueReport(GameConsoleTransmission reporter, string reportedAuth, string reportedIp, string reporterAuth, string reporterIp, ref string reason, ref byte[] signature, string reporterPublicKey, int reportedId)
	{
		try
		{
			string data = string.Format("reporterAuth={0}&reporterIp={1}&reportedAuth={2}&reportedIp={3}&reason={4}&signature={5}&reporterKey={6}&token={7}", Misc.Base64Encode(reporterAuth), reporterIp, Misc.Base64Encode(reportedAuth), reportedIp, Misc.Base64Encode(reason), Convert.ToBase64String(signature), Misc.Base64Encode(reporterPublicKey), ServerConsole.Password);
			string text = HttpQuery.Post(CentralServer.StandardUrl + "ingamereport.php", data);
			if (!(reporter == null))
			{
				if (text == "OK")
				{
					reportedPlayers.Add(reportedId);
					reporter.SendToClient(base.connectionToClient, "Player report successfully sent.", "green");
				}
				else
				{
					TargetReportUpdate(base.connectionToClient, reportedId, false);
					reporter.SendToClient(base.connectionToClient, "Error during **PROCESSING** player report:" + Environment.NewLine + text, "red");
				}
			}
		}
		catch (Exception ex)
		{
			GameConsole.Console.singleton.AddLog("[HOST] Error during **SENDING** player report:" + Environment.NewLine + ex.Message, Color.red);
			if (!(reporter == null))
			{
				TargetReportUpdate(base.connectionToClient, reportedId, false);
				reporter.SendToClient(base.connectionToClient, "Error during **SENDING** player report.", "yellow");
			}
		}
	}

	[TargetRpc(channel = 2)]
	private void TargetReportUpdate(NetworkConnection conn, int playerId, bool status)
	{
		GameObject gameObject = GameObject.FindGameObjectsWithTag("Player").FirstOrDefault((GameObject pl) => pl.GetComponent<QueryProcessor>().PlayerId == playerId);
		if (gameObject != null)
		{
			gameObject.GetComponent<CharacterClassManager>().CheatReported = status;
		}
	}
}
