using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using Dissonance.Integrations.UNet_HLAPI;
using kcp2k;
using MEC;
using Mirror;
using Mono.Nat;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Mirror.LiteNetLib4Mirror;
using UnityEngine.Networking;
using System.Net.Sockets;

public class CustomNetworkManager : NetworkManager
{
    [Serializable]
    public class DisconnectLog
    {
        [Serializable]
        public class LogButton
        {
            public ConnInfoButton[] actions;
        }

        [Multiline] public string msg_en;
        public LogButton button;
        public bool autoHideOnSceneLoad;
    }
    private readonly DisconnectLog[] logs = new DisconnectLog[15];
    public static CustomNetworkManager Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindObjectOfType<CustomNetworkManager>();
            }
            return _instance;
        }
    }
    private static CustomNetworkManager _instance;

    public GameObject popup;
    public GameObject createpop;
    public RectTransform contSize;
    public Text content;

    private static readonly QueryServer _queryserver;
    private List<INatDevice> _mappedDevices;
    private readonly GameConsole.Console _console;
    private int _curLogId;
    private readonly int _queryPort;
    private int _expectedGameFilesVersion;
    private bool _queryEnabled;
    private bool _configLoaded;
    private bool _activated;

    public bool reconnect;
    public string disconnectMessage = string.Empty;
    public static string Ip = string.Empty;
    public static string ConnectionIp;

    public int GameFilesVersion;
    public static string[] CompatibleVersions;
    public static readonly bool isPrivateBeta;
    public static readonly bool isStreamingAllowed = true;
    public static bool Modded;
    private int reservedSlots;

    public int ReservedMaxPlayers => maxConnections - reservedSlots;

    private void Awake()
    {
        if (_instance == null) _instance = this;
        if (Transport.active == null) Transport.active = GetComponent<LiteNetLib4MirrorTransport>();
    }

    private void Start()
    {
        LoadConfigs();
        SceneManager.sceneLoaded += OnLevelFinishedLoading;
    }

    private void Update()
    {
        if (popup.activeSelf && Input.GetKey(KeyCode.Escape))
            ClickButton();
    }

    private void SetCompatibleVersions()
    {
        CompatibleVersions = new[] { "8.0.1 (Revision III)" };
        _expectedGameFilesVersion = 3;
    }

    private void LoadConfigs()
    {
        if (_configLoaded) return;
        _configLoaded = true;

        SetCompatibleVersions();
        ConfigFile.HosterPolicy = File.Exists("hoster_policy.txt")
            ? new YamlConfig("hoster_policy.txt")
            : new YamlConfig();

        if (!ServerStatic.IsDedicated)
        {
            ConfigFile.ServerConfig = ConfigFile.ReloadGameConfig(
                Path.Combine(FileManager.GetAppFolder(), "config_gameplay.txt")
            );
        }
    }

    public override void OnClientDisconnect()
    {
        SteamManager.CancelTicket();
        base.OnClientDisconnect();
    }

    public override void OnServerConnect(NetworkConnectionToClient conn)
    {
        base.OnServerConnect(conn);

        var banResult = BanHandler.QueryBan(null, conn.address);
        if (banResult.Value != null)
        {
            ServerConsole.Disconnect(conn, "You are banned from this server.");
            return;
        }

        if (numPlayers > ReservedMaxPlayers || ConfigFile.ServerConfig.GetBool("reserved_slots_simulate_full"))
        {
            string cleanIp = ReservedSlot.TrimIPAddress(conn.address);
            if (!ReservedSlot.ContainsIP(cleanIp))
            {
                ServerConsole.Disconnect(conn, "Reserved Slots - Server is Full.");
            }
        }
    }

    public override void OnServerDisconnect(NetworkConnectionToClient conn)
    {
        base.OnServerDisconnect(conn);
        HlapiServer.OnServerDisconnect(conn);
        conn.Disconnect();
    }

    private void OnLevelFinishedLoading(Scene scene, LoadSceneMode mode)
    {
        if (!_activated && scene.name.Contains("menu", StringComparison.OrdinalIgnoreCase))
        {
            _activated = true;
            networkAddress = "none";
            StartClient();
            networkAddress = "localhost";
            StopClient();
        }

        if (reconnect)
        {
            ShowLog(14);
            Invoke(nameof(Reconnect), 3f);
        }
    }

    private void Reconnect()
    {
        if (reconnect)
        {
            reconnect = false;
            StartClient();
        }
    }

    public void ShowLog(int id, string your = "", string server = "")
    {
        _curLogId = id;
        popup.SetActive(true);

        string message = TranslationReader.Get("Connection_Errors", id);
        if (!string.IsNullOrEmpty(your) && !string.IsNullOrEmpty(server))
            message = message.Replace("[your]", your).Replace("[server]", server);

        if (!string.IsNullOrEmpty(disconnectMessage))
            message = $"{message.Split(new[] { Environment.NewLine }, StringSplitOptions.None)[0]}{Environment.NewLine}{disconnectMessage}";

        content.text = message;
        disconnectMessage = string.Empty;
    }

    public void ClickButton()
    {
        foreach (var button in logs[_curLogId].button.actions)
            button.UseButton();
    }

    public void CreateMatch()
    {
        LoadConfigs();
        ShowLog(13);
        createpop.SetActive(false);
        NetworkServer.Shutdown();

        ServerConsole.Port = GetFreePort();
        SetupTransportPort(ServerConsole.Port);

        reservedSlots = Mathf.Max(ConfigFile.ServerConfig.GetInt("reserved_slots", 0), 0);
        maxConnections = ConfigFile.ServerConfig.GetInt("max_players", 20) + reservedSlots;

        int playerLimit = ConfigFile.HosterPolicy.GetInt("players_limit", -1);
        if (playerLimit > 0 && maxConnections > playerLimit)
        {
            maxConnections = playerLimit;
            ServerConsole.AddLog($"Players limit exceeded. Max players set to {playerLimit}");
        }

        _queryEnabled = ConfigFile.ServerConfig.GetBool("enable_query");
        if (ConfigFile.ServerConfig.GetBool("forward_ports", true))
            UpnpStart();

        Timing.RunCoroutine(CreateLobby());
    }

    private void SetupTransportPort(int port)
    {
        switch (Transport.active)
        {
            case TelepathyTransport telepathy:
                telepathy.port = (ushort)port;
                break;
            case KcpTransport kcp:
                kcp.Port = (ushort)port;
                break;
            case LiteNetLib4MirrorTransport liteNet:
                liteNet.port = (ushort)port;
                break;
            default:
                ServerConsole.AddLog("Unknown transport type. Port not configured.");
                break;
        }
    }

    private IEnumerator<float> CreateLobby()
    {
        if (GameFilesVersion != _expectedGameFilesVersion)
        {
            ServerConsole.AddLog("Version mismatch. Aborting server startup.");
            yield break;
        }

        if (ConfigFile.HosterPolicy.GetString("server_ip", "none") != "none")
        {
            Ip = ConfigFile.HosterPolicy.GetString("server_ip");
        }
        else if (ConfigFile.ServerConfig.GetBool("online_mode", true) && ServerStatic.IsDedicated)
        {
            if (ConfigFile.ServerConfig.GetString("server_ip", "auto") != "auto")
            {
                Ip = ConfigFile.ServerConfig.GetString("server_ip");
            }
            else
            {
                using UnityWebRequest www = UnityWebRequest.Get(CentralServer.StandardUrl + "ip.php");
                yield return Timing.WaitUntilDone(www.SendWebRequest());

                if (www.result != UnityWebRequest.Result.Success)
                {
                    ServerConsole.AddLog($"IP fetch error: {www.error}");
                    yield break;
                }

                Ip = www.downloadHandler.text.TrimEnd('.');
            }
        }
        else
        {
            Ip = "127.0.0.1";
        }

        if (Transport.active is LiteNetLib4MirrorTransport liteNetTransport)
        {
            string bindIp = GetBindAddress();
            liteNetTransport.serverIPv4BindAddress = bindIp;
            StartHost();
        }

        if (!ConfigFile.ServerConfig.GetBool("online_mode", true))
            ServerConsole.AddLog("Server hidden: online_mode disabled");
    }

    private string GetBindAddress()
    {
        string bindIp = ConfigFile.HosterPolicy.GetString("bind_ip", "ANY");
        if (bindIp.Equals("ANY", StringComparison.OrdinalIgnoreCase))
            return "0.0.0.0";

        return ConfigFile.ServerConfig.GetString("bind_ip", bindIp);
    }

    public int GetFreePort()
    {
        int[] ports = ConfigFile.ServerConfig.GetIntList("port_queue").Count > 0
            ? ConfigFile.ServerConfig.GetIntList("port_queue").ToArray()
            : new[] { 7777, 7778, 7779 };

        foreach (int port in ports)
        {
            if (IsPortAvailable(port))
            {
                ServerConsole.AddLog($"Using port: {port}");
                return port;
            }
        }

        return 7777;
    }

    private bool IsPortAvailable(int port)
    {
        TcpListener tester = null;
        try
        {
            tester = new TcpListener(System.Net.IPAddress.Any, port);
            tester.Start();
            return true;
        }
        catch
        {
            ServerConsole.AddLog($"Port {port} busy");
            return false;
        }
        finally
        {
            tester?.Stop();
        }
    }

    private void UpnpStart()
    {
        _mappedDevices ??= new List<INatDevice>();
        NatUtility.DeviceFound += DeviceFound;
        NatUtility.DeviceLost += DeviceLost;
        NatUtility.StartDiscovery();
    }

    private void DeviceFound(object sender, DeviceEventArgs args)
    {
        try
        {
            var device = args.Device;
            _mappedDevices.Add(device);
            device.CreatePortMap(new Mapping(Protocol.Udp, ServerConsole.Port, ServerConsole.Port));

            if (_queryEnabled)
                device.CreatePortMap(new Mapping(Protocol.Tcp, _queryPort, _queryPort));
        }
        catch (Exception e)
        {
            ServerConsole.AddLog($"UPNP error: {e.Message}");
        }
    }

    private void DeviceLost(object sender, DeviceEventArgs args)
    {
        INatDevice device = args.Device;
        try { device.DeletePortMap(new Mapping(Protocol.Udp, ServerConsole.Port, ServerConsole.Port)); } catch { }
        _mappedDevices.Remove(device);
    }
}