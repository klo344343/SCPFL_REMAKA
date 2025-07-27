using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using Cryptography;
using GameConsole;
using MEC;
using Mirror;
using Org.BouncyCastle.Crypto;
using UnityEngine;
using UnityEngine.Networking;
using ThreadPriority = System.Threading.ThreadPriority;

public class ServerConsole : MonoBehaviour, IDisposable
{
	public static ServerConsole singleton;

	public static int LogId;

	public static int Cycle;

	public static int Port;

	private static bool _disposing;

	public static Process ConsoleId;

	public static string Session;

	public static string Password;

	public static string Ip;

	public static AsymmetricKeyParameter Publickey;

	private static bool _accepted = true;

	public static bool Update;

	public static bool FriendlyFire = false;

	public static bool WhiteListEnabled = false;

	private static readonly Queue<string> PrompterQueue = new Queue<string>();

	private bool _errorSent;

	public Thread CheckProcessThread;

	public Thread QueueThread;

    public void Dispose()
    {
        _disposing = true;
        string sessionPath = $"SCPSL_Data/Dedicated/{Session}";
        if (Directory.Exists(sessionPath))
            Directory.Delete(sessionPath, true);
    }
    private void Awake() => singleton = this;
    private void Start()
    {
        if (!ServerStatic.IsDedicated) return;
        LogId = 0;
        _accepted = true;

        if (string.IsNullOrEmpty(Session))
            Session = "default";

        string path = $"SCPSL_Data/Dedicated/{Session}";
        if (Directory.Exists(path))
        {
            if (Environment.GetCommandLineArgs().Contains("-nodedicateddelete"))
            {
                foreach (var file in Directory.GetFiles(path))
                    File.Delete(file);
            }
            else
                Directory.Delete(path, true);
        }
        Directory.CreateDirectory(path);

        FileSystemWatcher watcher = new FileSystemWatcher(path) { NotifyFilter = NotifyFilters.FileName };
        watcher.Created += (s, e) =>
        {
            if (e.Name.Contains("cs") && e.Name.Contains("mapi"))
                new Thread(() => ReadLog(e.FullPath)).Start();
        };
        watcher.EnableRaisingEvents = true;

        QueueThread = new Thread(Prompt)
        {
            Priority = ThreadPriority.Lowest,
            IsBackground = true,
            Name = "Dedicated server console output"
        };
        QueueThread.Start();

        if (ServerStatic.ProcessIdPassed)
        {
            CheckProcessThread = new Thread(CheckProcess)
            {
                Priority = ThreadPriority.Lowest,
                IsBackground = true,
                Name = "Dedicated server console running check"
            };
            CheckProcessThread.Start();
        }
    }

    private static void ReadLog(string path)
    {
        try
        {
            if (!File.Exists(path)) return;

            string name = Path.GetFileName(path);
            string log = File.ReadAllText(path);
            if (log.Contains("terminator"))
                log = log[..log.LastIndexOf("terminator", StringComparison.Ordinal)];

            string result = EnterCommand(log);
            File.Delete(path);

            if (!string.IsNullOrEmpty(result))
                AddLog(result);
        }
        catch (Exception ex)
        {
            UnityEngine.Debug.LogException(ex);
        }
    }

    private void CheckProcess()
    {
        while (!_disposing)
        {
            Thread.Sleep(4000);
            if (ConsoleId == null || ConsoleId.HasExited)
            {
                DisposeStatic();
                TerminateProcess();
            }
        }
    }

    private void Prompt()
    {
        while (!_disposing)
        {
            if (PrompterQueue.Count == 0 || !_accepted)
            {
                Thread.Sleep(25);
                continue;
            }
            string msg = PrompterQueue.Dequeue();
            if (!_errorSent || !msg.Contains("Could not update the session - Server is not verified."))
            {
                _errorSent = true;
                File.WriteAllText($"SCPSL_Data/Dedicated/{Session}/sl{LogId++}.mapi", msg);
            }
        }
    }

    public void OnDestroy() => Dispose();
    public void OnApplicationQuit() => Dispose();
    public static void DisposeStatic() => singleton.Dispose();

    public static void AddLog(string q)
    {

        if (ServerStatic.IsDedicated)
            PrompterQueue.Enqueue(q);
        else
            GameConsole.Console.singleton.AddLog(q, Color.grey);
    }

    public static string GetClientInfo(NetworkConnectionToClient conn)
    {
        GameObject obj = GameConsole.Console.FindConnectedRoot(conn);
        string nick = obj.GetComponent<NicknameSync>().myNick;
        string steamId = obj.GetComponent<CharacterClassManager>().SteamId;
        return $"{nick} ( {steamId} | {conn.address} )";
    }

    public static string GetClientInfo(GameObject obj)
    {
        var conn = obj.GetComponent<NetworkBehaviour>().connectionToClient;
        return $"{obj.GetComponent<NicknameSync>().myNick} ( {obj.GetComponent<CharacterClassManager>().SteamId} | {conn.address} )";
    }

    public static void Disconnect(GameObject player, string message)
    {
        if (player == null) return;
        var net = player.GetComponent<NetworkBehaviour>();
        var conn = net.connectionToClient;
        if (conn != null && conn.isReady)
        {
            var ccm = player.GetComponent<CharacterClassManager>();
            if (ccm != null)
                ccm.DisconnectClient(conn, message);
            else
                conn.Disconnect();
        }
    }

    public static void Disconnect(NetworkConnectionToClient conn, string message)
    {
        GameObject obj = GameConsole.Console.FindConnectedRoot(conn);
        if (obj != null)
            Disconnect(obj, message);
        else
            conn.Disconnect();
    }

    private static void ColorText(string text)
	{
		UnityEngine.Debug.Log(string.Format("<color={0}>{1}</color>", GetColor(text), text), null);
	}

	private static string GetColor(string text)
	{
		int num = 9;
		if (text.Contains("LOGTYPE"))
		{
			try
			{
				string text2 = text.Remove(0, text.IndexOf("LOGTYPE", StringComparison.Ordinal) + 7);
				num = int.Parse((!text2.Contains("-")) ? text2 : text2.Remove(0, 1));
			}
			catch
			{
				num = 9;
			}
		}
		string empty = string.Empty;
        return num switch
        {
            0 => "#000",
            1 => "#183487",
            2 => "#0b7011",
            3 => "#0a706c",
            4 => "#700a0a",
            5 => "#5b0a40",
            6 => "#aaa800",
            7 => "#afafaf",
            8 => "#5b5b5b",
            9 => "#0055ff",
            10 => "#10ce1a",
            11 => "#0fc7ce",
            12 => "#ce0e0e",
            13 => "#c70dce",
            14 => "#ffff07",
            15 => "#e0e0e0",
            _ => "#fff",
        };
    }

	internal static string EnterCommand(string cmds)
	{
		string result = "Command accepted.";
		string[] array = cmds.ToUpper().Split(' ');
		if (array.Length <= 0)
		{
			return result;
		}
		switch (array[0])
		{
		case "FORCESTART":
		{
			bool flag = false;
			GameObject gameObject = GameObject.Find("Host");
			if (gameObject != null)
			{
				CharacterClassManager component = gameObject.GetComponent<CharacterClassManager>();
				if (component != null && component.isLocalPlayer && component.isServer && !component.roundStarted)
				{
					component.ForceRoundStart();
					flag = true;
				}
			}
			result = ((!flag) ? "Failed to force start.LOGTYPE14" : "Forced round start.");
			break;
		}
		case "CONFIG":
			if (File.Exists(ConfigFile.ConfigPath))
			{
				Application.OpenURL(ConfigFile.ConfigPath);
			}
			else
			{
				result = "Config file not found!";
			}
			break;
		default:
			result = GameConsole.Console.singleton.TypeCommand(cmds);
			break;
		}
		return result;
	}

	public void RunServer()
	{
		Timing.RunCoroutine(_RefreshSession(), Segment.Update);
	}

	public void RunRefreshPublicKey()
	{
		Timing.RunCoroutine(_RefreshPublicKey(), Segment.Update);
	}

	public void RunRefreshCentralServers()
	{
		Timing.RunCoroutine(_RefreshCentralServers(), Segment.Update);
	}

	private IEnumerator<float> _RefreshCentralServers()
	{
		while (this != null)
		{
			yield return Timing.WaitForSeconds(900f);
			new Thread((ThreadStart)delegate
			{
				CentralServer.RefreshServerList(true);
			}).Start();
		}
	}

	private IEnumerator<float> _RefreshPublicKey()
	{
		string cache = CentralServerKeyCache.ReadCache();
		string cacheHash = string.Empty;
		string lastHash = string.Empty;
		if (!string.IsNullOrEmpty(cache))
		{
			Publickey = ECDSA.PublicKeyFromString(cache);
			cacheHash = Sha.HashToString(Sha.Sha256(ECDSA.KeyToString(Publickey)));
			AddLog("Loaded central server public key from cache.\nSHA256 of public key: " + cacheHash);
		}
		AddLog("Downloading public key from central server...");
		while (this != null)
		{
			using (WWW www = new WWW(form: new WWWForm(), url: CentralServer.StandardUrl + "publickey.php"))
			{
				yield return Timing.WaitUntilDone(www);
				try
				{
					bool flag = false;
					if (!string.IsNullOrEmpty(www.error))
					{
						AddLog("Can't refresh central server public key - " + www.error);
						flag = true;
					}
					if (!flag)
					{
						Publickey = ECDSA.PublicKeyFromString(www.text);
						string text = Sha.HashToString(Sha.Sha256(ECDSA.KeyToString(Publickey)));
						if (text != lastHash)
						{
							lastHash = text;
							AddLog("Downloaded public key from central server.\nSHA256 of public key: " + text);
							if (text != cacheHash)
							{
								CentralServerKeyCache.SaveCache(www.text);
							}
							else
							{
								AddLog("SHA256 of cached key matches, no need to update cache.");
							}
						}
						else
						{
							AddLog("Refreshed public key of central server - key hash not changed.");
						}
					}
				}
				catch (Exception ex)
				{
					AddLog("Can't refresh central server public key - " + ex.Message);
				}
			}
			yield return Timing.WaitForSeconds(360f);
		}
	}

	private IEnumerator<float> _RefreshPublicKeyOnce()
	{
		using (WWW www = new WWW(form: new WWWForm(), url: CentralServer.StandardUrl + "publickey.php"))
		{
			yield return Timing.WaitUntilDone(www);
			try
			{
				if (!string.IsNullOrEmpty(www.error))
				{
					AddLog("Can't refresh central server public key - " + www.error);
					yield break;
				}
				Publickey = ECDSA.PublicKeyFromString(www.text);
				string text = Sha.HashToString(Sha.Sha256(ECDSA.KeyToString(Publickey)));
				AddLog("Downloaded public key from central server.\nSHA256 of public key: " + text);
				CentralServerKeyCache.SaveCache(www.text);
			}
			catch (Exception ex)
			{
				AddLog("Can't refresh central server public key - " + ex.Message);
			}
		}
	}

	private IEnumerator<float> _RefreshSession()
	{
		CustomNetworkManager cnm = GetComponent<CustomNetworkManager>();
		string masterServer = CentralServer.MasterUrl + "authenticator.php";
		while (this != null)
		{
			float timeBefore = Time.realtimeSinceStartup;
			Cycle++;
			if (string.IsNullOrEmpty(Password) && Cycle < 15)
			{
				if (Cycle == 5 || Cycle == 12)
				{
					RefreshToken();
				}
			}
			else
			{
				WWWForm form = new WWWForm();
				form.AddField("ip", Ip);
				if (!string.IsNullOrEmpty(Password))
				{
					form.AddField("passcode", Password);
				}
				int plys = 0;
				try
				{
					plys = GameObject.FindGameObjectsWithTag("Player").Length - 1;
				}
				catch
				{
				}
				form.AddField("players", plys + "/" + cnm.ReservedMaxPlayers);
				form.AddField("port", Port);
				form.AddField("version", 2);
				if (Update || Cycle == 10)
				{
					Update = false;
					string value = ConfigFile.ServerConfig.GetString("server_name", "Unnamed server") + ":[:BREAK:]:" + ConfigFile.ServerConfig.GetString("serverinfo_pastebin_id", "7wV681fT") + ":[:BREAK:]:" + CustomNetworkManager.CompatibleVersions[0];
					form.AddField("update", 1);
					form.AddField("info", value);
					form.AddField("privateBeta", CustomNetworkManager.isPrivateBeta.ToString());
					form.AddField("staffRA", ServerStatic.PermissionsHandler.StaffAccess.ToString());
					form.AddField("friendlyFire", FriendlyFire.ToString());
					form.AddField("modded", CustomNetworkManager.Modded.ToString());
					form.AddField("whitelist", WhiteListEnabled.ToString());
				}
				using (WWW www = new WWW(masterServer, form))
				{
					yield return Timing.WaitUntilDone(www);
					if (!string.IsNullOrEmpty(www.error) || www.text != "YES")
					{
						if (!string.IsNullOrEmpty(www.error))
						{
							AddLog("Could not update data on server list - " + www.error + www.text + "LOGTYPE-8");
						}
						else
						{
							if (www.text.StartsWith("New code generated:"))
							{
								ServerStatic.PermissionsHandler.SetServerAsVerified();
								string path = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "/SCP Secret Laboratory/verkey.txt";
								try
								{
									File.Delete(path);
								}
								catch
								{
									AddLog("New password could not be saved.LOGTYPE-8");
								}
								try
								{
									StreamWriter streamWriter = new StreamWriter(path);
									string text = www.text.Remove(0, www.text.IndexOf(":", StringComparison.Ordinal)).Remove(www.text.IndexOf(":", StringComparison.Ordinal));
									while (text.Contains(":"))
									{
										text = text.Replace(":", string.Empty);
									}
									streamWriter.WriteLine(text);
									streamWriter.Close();
									AddLog("New password saved.LOGTYPE-8");
									Update = true;
								}
								catch
								{
									AddLog("New password could not be saved.LOGTYPE-8");
								}
							}
							else if (www.text.Contains(":Restart:"))
							{
								AddLog("Server restart requested by central server.LOGTYPE-8");
								Application.Quit();
							}
							else if (www.text.Contains(":RoundRestart:"))
							{
								AddLog("Round restart requested by central server.LOGTYPE-8");
								GameObject[] array = GameObject.FindGameObjectsWithTag("Player");
								foreach (GameObject gameObject in array)
								{
									PlayerStats component = gameObject.GetComponent<PlayerStats>();
									if (component.isLocalPlayer && component.isServer)
									{
										component.Roundrestart();
									}
								}
							}
							else if (www.text.Contains(":UpdateData:"))
							{
								Update = true;
							}
							else if (www.text.Contains(":RefreshKey:"))
							{
								AddLog("Public key refresh requested by central server.LOGTYPE-8");
								Timing.RunCoroutine(_RefreshPublicKeyOnce(), Segment.Update);
							}
							else if (www.text.Contains(":Message - "))
							{
								string text2 = www.text.Substring(www.text.IndexOf(":Message - ", StringComparison.Ordinal) + 11);
								text2 = text2.Substring(0, text2.IndexOf(":::", StringComparison.Ordinal));
								AddLog("[MESSAGE FROM CENTRAL SERVER] " + text2 + " LOGTYPE-3");
							}
							else if (!www.text.Contains("Server is not verified"))
							{
								AddLog("Could not update data on server list - " + www.error + www.text + "LOGTYPE-8");
							}
							RefreshToken();
						}
					}
				}
			}
			if (Cycle >= 15)
			{
				Cycle = 0;
			}
			yield return Timing.WaitForSeconds(5f - (Time.realtimeSinceStartup - timeBefore));
		}
	}

	public void RefreshToken()
	{
		string path = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "/SCP Secret Laboratory/verkey.txt";
		if (File.Exists(path))
		{
			StreamReader streamReader = new(path);
			string text = streamReader.ReadToEnd();
			if (string.IsNullOrEmpty(Password) && !string.IsNullOrEmpty(text))
			{
				AddLog("Verification token loaded! Server probably will be listed on public list.");
			}
			if (Password != text)
			{
				AddLog("Verification token reloaded.");
				Update = true;
			}
			Password = text;
			ServerStatic.PermissionsHandler.SetServerAsVerified();
			streamReader.Close();
		}
	}

	private static void TerminateProcess()
	{
		ServerStatic.IsDedicated = false;
		Application.Quit();
	}
}
