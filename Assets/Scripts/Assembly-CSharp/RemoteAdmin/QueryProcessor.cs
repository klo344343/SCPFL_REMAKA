using AntiFaker;
using Cryptography;
using GameConsole;
using Mirror;
using Org.BouncyCastle.Security;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Principal;
using UnityEngine;
using UnityEngine.Networking;
using static PlayerList;

namespace RemoteAdmin
{
    public class QueryProcessor : NetworkBehaviour
    {
        public AntiFakeCommands Afc;

        public RemoteAdminCryptographicManager CryptoManager;

        public GameConsoleTransmission GCT;

        public static SecureRandom SecureRandom;

        public static QueryProcessor Localplayer;

        private ServerRoles _roles;

        public static bool Lockdown;

        private int _toBanType;

        private static int _idIterator;

        public const int HashIterations = 250;

        internal int PasswordTries;

        internal int SignaturesCounter;

        private int _signaturesCounter;

        internal byte[] Key;

        internal byte[] Salt;

        internal byte[] ClientSalt;

        private byte[] _key;

        private byte[] _salt;

        private byte[] _clientSalt;

        private float _lastPlayerlistRequest;

        private string _toBan;

        private string _toBanNick;

        private string _toBanSteamId;

        private string _prevSalt;

        [SyncVar(hook = nameof(OnServerRandomChanged))]
        private string serverRandom;

        public static string ServerStaticRandom;

        [SyncVar(hook = nameof(OnPlayerIdChanged))]
        private int playerId;

        [SyncVar(hook = nameof(OnOverridePasswordEnabledChanged))]
        private bool overridePasswordEnabled;

        public bool OnlineMode;

        internal bool PasswordSent;

        private bool _gameplayData;

        private bool _gdDirty;

        private string ipAddress;

        private NetworkConnection conns;

        private static Dictionary<string, int> _003C_003Ef__switch_0024map5;
        public string ServerRandom
        {
            get => serverRandom;
            set
            {
                if (isServer)
                    serverRandom = value;
            }
        }
        public int PlayerId
        {
            get => playerId;
            set
            {
                if (isServer)
                    playerId = value;
            }
        }
        public bool OverridePasswordEnabled
        {
            get => overridePasswordEnabled;
            set
            {
                if (isServer)
                    overridePasswordEnabled = value;
            }
        }
        private void OnServerRandomChanged(string oldValue, string newValue)
        {
            if (!isServer && _prevSalt != newValue)
            {
                _prevSalt = newValue;
                GameConsole.Console.singleton.AddLog("Obtained server round random: " + newValue, Color.gray);
            }
        }
        private void OnPlayerIdChanged(int oldValue, int newValue)
        {
            SetId(newValue);
        }
        public bool GameplayData
        {
            get
            {
                return _gameplayData;
            }
            set
            {
                _gameplayData = value;
                _gdDirty = true;
            }
        }
        private void OnOverridePasswordEnabledChanged(bool oldValue, bool newValue)
        {
            SetOverridePasswordEnabled(newValue);
        }

        private void SetOverridePasswordEnabled(bool b)
        {
            overridePasswordEnabled = b;
        }

        private void SetId(int id)
        {
            PlayerId = id;
        }

        public void RefreshPlayerList()
        {
            if (base.isLocalPlayer && _roles.LocalRemoteAdmin && UIController.singleton.opened && _lastPlayerlistRequest > 0.2f)
            {
                _lastPlayerlistRequest = 0f;
                CmdSendQuery("REQUEST_DATA PLAYER_LIST SILENT");
            }
        }

        public static void StaticRefreshPlayerList()
        {
            if (Localplayer != null)
            {
                Localplayer.RefreshPlayerList();
            }
        }

        private void Start()
        {
            OnlineMode = GetComponent<CharacterClassManager>().OnlineMode;
            _roles = GetComponent<ServerRoles>();
            Afc = GetComponent<AntiFakeCommands>();
            CryptoManager = GetComponent<RemoteAdminCryptographicManager>();
            GCT = GetComponent<GameConsoleTransmission>();
            SecureRandom ??= new SecureRandom();
            SignaturesCounter = 0;
            _signaturesCounter = 0;
            if (NetworkServer.active)
            {
                conns = base.connectionToClient;
                ipAddress = conns.identity.connectionToClient.address;
                overridePasswordEnabled = ServerStatic.PermissionsHandler.OverrideEnabled;
                if (string.IsNullOrEmpty(ServerStaticRandom))
                {
                    byte[] array;
                    using (RandomNumberGenerator randomNumberGenerator = new RNGCryptoServiceProvider())
                    {
                        array = new byte[32];
                        randomNumberGenerator.GetBytes(array);
                    }
                    ServerStaticRandom = Convert.ToBase64String(array);
                    ServerConsole.AddLog("Generated round random salt: " + ServerStaticRandom);
                }
                if (string.IsNullOrEmpty(ServerRandom))
                {
                    ServerRandom = ServerStaticRandom;
                }
                _idIterator++;
                SetId(_idIterator);
            }
            if (base.isLocalPlayer)
            {
                Localplayer = this;
                InvokeRepeating("RefreshPlayerList", 2f, 1f);
            }
        }

        private void Update()
        {
            if (base.isLocalPlayer && _lastPlayerlistRequest < 1f)
            {
                _lastPlayerlistRequest += Time.deltaTime;
            }
            if (_gdDirty)
            {
                _gdDirty = false;
                if (NetworkServer.active)
                {
                    TargetSyncGameplayData(base.connectionToClient, _gameplayData);
                }
            }
        }

        [Command(channel = 2)]
        public void CmdRequestSalt(byte[] clSalt)
        {
            if (!ServerStatic.PermissionsHandler.OverrideEnabled)
            {
                GetComponent<CharacterClassManager>().TargetConsolePrint(base.connectionToClient, "Password authentication is disabled on this server!", "magenta");
                return;
            }
            if (_clientSalt == null)
            {
                if (clSalt == null)
                {
                    GetComponent<CharacterClassManager>().TargetConsolePrint(base.connectionToClient, "Please generate and send your salt!", "red");
                    return;
                }
                if (clSalt.Length < 32)
                {
                    GetComponent<CharacterClassManager>().TargetConsolePrint(base.connectionToClient, "Generated salt is too short. Please generate longer salt and try again!", "red");
                    return;
                }
                _clientSalt = clSalt;
                if (_key == null && _salt != null)
                {
                    _key = ServerStatic.PermissionsHandler.DerivePassword(_salt, _clientSalt);
                }
                GetComponent<CharacterClassManager>().TargetConsolePrint(base.connectionToClient, "Your salt " + Convert.ToBase64String(clSalt) + " has been accepted by the server.", "cyan");
            }
            if (_salt != null)
            {
                TargetSaltGenerated(base.connectionToClient, _salt);
                return;
            }
            byte[] array;
            using (RandomNumberGenerator randomNumberGenerator = new RNGCryptoServiceProvider())
            {
                array = new byte[32];
                randomNumberGenerator.GetBytes(array);
            }
            _salt = array;
            _key = ServerStatic.PermissionsHandler.DerivePassword(_salt, _clientSalt);
            TargetSaltGenerated(base.connectionToClient, _salt);
        }

        [TargetRpc(channel = 2)]
        public void TargetSaltGenerated(NetworkConnection conn, byte[] salt)
        {
            if (salt.Length < 32)
            {
                GameConsole.Console.singleton.AddLog(string.Concat("Rejected salt ", salt, " because it's too short!"), Color.red);
                return;
            }
            GameConsole.Console.singleton.AddLog("Obtained server's salt " + Convert.ToBase64String(salt) + " from server.", Color.cyan);
            Salt = salt;
        }

        [Command(channel = 15)]
        public void CmdSendPassword(byte[] authSignature)
        {
            bool b = false;
            if (_roles.RemoteAdmin)
            {
                b = true;
                PasswordTries = 0;
            }
            else
            {
                if (_salt == null || _clientSalt == null)
                {
                    GetComponent<CharacterClassManager>().TargetConsolePrint(base.connectionToClient, "Can't verify your remote admin password - please generate salt first!", "red");
                    return;
                }
                if (_clientSalt.Length < 16)
                {
                    GetComponent<CharacterClassManager>().TargetConsolePrint(base.connectionToClient, "Generated salt is too short. Please rejoin the server and try again!", "red");
                    return;
                }
                if (VerifyHmacSignature("Login", -1, authSignature, false))
                {
                    PasswordTries = 0;
                    UserGroup overrideGroup = ServerStatic.PermissionsHandler.OverrideGroup;
                    if (overrideGroup != null)
                    {
                        ServerConsole.AddLog("Assigned group " + overrideGroup.BadgeText + " to " + GetComponent<NicknameSync>().myNick + " - override password.");
                        _roles.SetGroup(overrideGroup, true);
                        b = true;
                    }
                    else
                    {
                        GetComponent<CharacterClassManager>().TargetConsolePrint(base.connectionToClient, "Non-existing group is assigned for override password!", "red");
                    }
                }
                else
                {
                    PasswordTries++;
                    ServerConsole.AddLog("Rejected override password sent by " + GetComponent<NicknameSync>().myNick + " (" + GetComponent<CharacterClassManager>().SteamId + ").");
                    ServerLogs.AddLog(ServerLogs.Modules.Permissions, "Rejected override password sent by " + GetComponent<NicknameSync>().myNick + " (" + GetComponent<CharacterClassManager>().SteamId + ").", ServerLogs.ServerLogType.RemoteAdminActivity_Misc);
                }
            }
            if (PasswordTries >= 3)
            {
                ServerLogs.AddLog(ServerLogs.Modules.Permissions, GetComponent<NicknameSync>().myNick + " (" + GetComponent<CharacterClassManager>().SteamId + ") has been kicked from the server for sending too many invalid override passwords.", ServerLogs.ServerLogType.RemoteAdminActivity_Misc);
                ServerConsole.Disconnect(base.connectionToClient, "You have been kicked for too many Remote Admin login attempts.");
            }
            else
            {
                TargetReplyPassword(base.connectionToClient, b);
            }
        }

        [TargetRpc(channel = 14)]
        private void TargetReplyPassword(NetworkConnection conn, bool b)
        {
            UnityEngine.Object.FindObjectOfType<UIController>().awaitingLogin = (b ? 2 : 0);
        }

        [Server]
        private void TargetReply(NetworkConnection conn, string content, bool isSuccess, bool logInConsole, string overrideDisplay)
        {
            if (!NetworkServer.active)
            {
                Debug.LogWarning("[Server] function 'System.Void RemoteAdmin.QueryProcessor::TargetReply(UnityEngine.Networking.NetworkConnection,System.String,System.Boolean,System.Boolean,System.String)' called on client");
            }
            else if (CryptoManager.EncryptionKey == null)
            {
                if (ServerStatic.IsDedicated && base.isLocalPlayer)
                {
                    ServerConsole.AddLog("[RA output] " + content);
                }
                else if (!CryptoManager.ExchangeRequested)
                {
                    TargetReplyPlain(conn, content, isSuccess, logInConsole, overrideDisplay);
                }
                else
                {
                    TargetReplyPlain(conn, "ERROR#ECDHE exchange was requested, please complete it on client side.", false, true, string.Empty);
                }
            }
            else
            {
                TargetReplyEncrypted(conn, AES.AesGcmEncrypt(Utf8.GetBytes(content), CryptoManager.EncryptionKey, SecureRandom), isSuccess, logInConsole, overrideDisplay);
            }
        }

        [TargetRpc(channel = 15)]
        public void TargetReplyPlain(NetworkConnection conn, string content, bool isSuccess, bool logInConsole, string overrideDisplay)
        {
            if (CryptoManager.EncryptionKey != null || CryptoManager.ExchangeRequested)
            {
                GameConsole.Console.singleton.AddLog("Rejected plaintext reply to remote admin request (encryption required, error 1).", Color.magenta);
            }
            else if (OnlineMode)
            {
                GameConsole.Console.singleton.AddLog("Rejected plaintext reply to remote admin request (encryption required, error 2).", Color.magenta);
            }
            else
            {
                ProcessReply(content, isSuccess, logInConsole, overrideDisplay, false);
            }
        }

        [TargetRpc(channel = 15)]
        public void TargetReplyEncrypted(NetworkConnection conn, byte[] content, bool isSuccess, bool logInConsole, string overrideDisplay)
        {
            if (CryptoManager.EncryptionKey == null)
            {
                GameConsole.Console.singleton.AddLog("Rejected encrypted reply to remote admin request (ECHDE exchange required).", Color.magenta);
                return;
            }
            string content2;
            try
            {
                byte[] data = AES.AesGcmDecrypt(content, CryptoManager.EncryptionKey);
                content2 = Utf8.GetString(data);
            }
            catch
            {
                GetComponent<CharacterClassManager>().TargetConsolePrint(base.connectionToClient, "Decryption or verification of remote admin response failed.", "magenta");
                return;
            }
            ProcessReply(content2, isSuccess, logInConsole, overrideDisplay, true);
        }

        private void ProcessReply(string content, bool isSuccess, bool logInConsole, string overrideDisplay, bool secure)
        {
            string text = content.Remove(content.IndexOf("#", StringComparison.Ordinal));
            content = content.Remove(0, content.IndexOf("#", StringComparison.Ordinal) + 1);
            if (logInConsole)
            {
                TextBasedRemoteAdmin.AddLog(((!secure) ? "<color=red>[UNECRYPTED] </color>" : "<color=green>[ENCRYPTED AND VERIFIED] </color>") + ((!isSuccess) ? "<color=orange>" : "<color=white>") + "[" + text + "] " + content + "</color>");
            }
            switch (text)
            {
                case "BigQR":
                    LargeDataPrinter.Display(content, true);
                    return;
                case "BigQR-NR":
                    LargeDataPrinter.Display(content, false);
                    return;
                case "StaffTokenReply":
                    StaffAuthTokenResponse(content, secure);
                    return;
                case "StaffPlayerListReply":
                    StaffPlayerListResponse(content);
                    return;
                case "PlayerInfoQR":
                    if (content.Length <= 30)
                    {
                        PlayerInfoQR.Display(content);
                    }
                    return;
            }
            if (!(overrideDisplay != "void"))
            {
                return;
            }
            if (overrideDisplay == string.Empty)
            {
                switch (text)
                {
                    case "HELP":
                        Application.OpenURL("https://docs.google.com/document/d/1nj6fNULwc7Kx3fNnt5Gh2YTIqg8jS5d_Z0fDXpTimAw/edit?usp=sharing");
                        return;
                    case "REQUEST_DATA:PLAYER_LIST":
                        PlayerRequest.singleton.ResponsePlayerList(content, isSuccess, GameplayData);
                        return;
                    case "REQUEST_DATA:PLAYER":
                        PlayerRequest.singleton.ResponsePlayerSpecific(content, isSuccess);
                        return;
                    case "LOGOUT":
                        {
                            UIController uIController = UnityEngine.Object.FindObjectOfType<UIController>();
                            if (uIController.root_root.activeSelf)
                            {
                                uIController.ChangeConsoleStage();
                            }
                            uIController.loggedIn = false;
                            return;
                        }
                }
                int num = 0;
                SubmenuSelector.SubMenu[] menus = SubmenuSelector.singleton.menus;
                foreach (SubmenuSelector.SubMenu subMenu in menus)
                {
                    if (subMenu.commandTemplate.StartsWith(text))
                    {
                        DisplayDataOnScreen.singleton.Show(num, ((!isSuccess) ? "<color=red>" : "<color=green>") + content + "</color>");
                    }
                    num++;
                }
                return;
            }
            int num2 = 0;
            SubmenuSelector.SubMenu[] menus2 = SubmenuSelector.singleton.menus;
            foreach (SubmenuSelector.SubMenu subMenu2 in menus2)
            {
                if (subMenu2.commandTemplate == overrideDisplay)
                {
                    DisplayDataOnScreen.singleton.Show(num2, ((!isSuccess) ? "<color=red>" : "<color=green>") + content + "</color>");
                }
                num2++;
            }
        }

        [Client]
        public void CmdSendQuery(string query)
        {
            if (!NetworkClient.active)
            {
                Debug.LogWarning("[Client] function 'System.Void RemoteAdmin.QueryProcessor::CmdSendQuery(System.String)' called on server");
                return;
            }
            if (string.IsNullOrEmpty(ServerRandom))
            {
                GameConsole.Console.singleton.AddLog("Failed to send command - empty ServerRandom.", Color.magenta);
                return;
            }
            if (ServerRandom.Length < 32)
            {
                GameConsole.Console.singleton.AddLog("Failed to send command - too short ServerRandom.", Color.magenta);
                return;
            }
            SignaturesCounter++;
            if (CryptoManager.EncryptionKey == null)
            {
                if (PasswordSent || CentralAuth.GlobalBadgeIssued)
                {
                    CmdSendQuery(query, SignaturesCounter, SignRequest(query));
                }
                else
                {
                    GameConsole.Console.singleton.AddLog("Failed to send remote admin command - ECDHE exchange not performed.", Color.magenta);
                }
            }
            else
            {
                CmdSendEncryptedQuery(AES.AesGcmEncrypt(Utf8.GetBytes(query + ":[:COUNTER:]:" + SignaturesCounter), CryptoManager.EncryptionKey, SecureRandom));
            }
            if (!query.Contains("SILENT"))
            {
                TextBasedRemoteAdmin.AddLog("<color=purple>[USER-INPUT] " + query + "</color>");
            }
        }

        [Command(channel = 15)]
        public void CmdSendEncryptedQuery(byte[] query)
        {
            if (!_roles.RemoteAdmin)
            {
                GetComponent<CharacterClassManager>().TargetConsolePrint(base.connectionToClient, "You are not logged in to remote admin panel!", "red");
                return;
            }
            if (CryptoManager.EncryptionKey == null)
            {
                GetComponent<CharacterClassManager>().TargetConsolePrint(base.connectionToClient, "Please complete ECDHE exchange before sending encrypted remote admin requests.", "magenta");
                return;
            }
            string text;
            try
            {
                byte[] data = AES.AesGcmDecrypt(query, CryptoManager.EncryptionKey);
                text = Utf8.GetString(data);
            }
            catch
            {
                GetComponent<CharacterClassManager>().TargetConsolePrint(base.connectionToClient, "Decryption or verification of remote admin request failed.", "magenta");
                return;
            }
            if (!text.Contains(":[:COUNTER:]:"))
            {
                GetComponent<CharacterClassManager>().TargetConsolePrint(base.connectionToClient, "Remote admin request doesn't contain a signatures counter.", "magenta");
                return;
            }
            int num = text.LastIndexOf(":[:COUNTER:]:", StringComparison.Ordinal);
            string s = text.Substring(num + 13);
            int result;
            if (!int.TryParse(s, out result))
            {
                GetComponent<CharacterClassManager>().TargetConsolePrint(base.connectionToClient, "Remote admin request contains non-integer signatures counter.", "magenta");
                return;
            }
            if (result <= _signaturesCounter)
            {
                GetComponent<CharacterClassManager>().TargetConsolePrint(base.connectionToClient, "Remote admin request contains smaller signatures counter than previous request.", "magenta");
                return;
            }
            _signaturesCounter = result;
            ProcessQuery(text.Substring(0, num));
        }

        [Command(channel = 15)]
        public void CmdSendQuery(string query, int counter, byte[] signature)
        {
            if (string.IsNullOrEmpty(ServerRandom))
            {
                GetComponent<CharacterClassManager>().TargetConsolePrint(base.connectionToClient, "Remote Admin error - ServerRandom is empty or null.", "magenta");
            }
            else if (_roles.RemoteAdmin)
            {
                if (CryptoManager.ExchangeRequested)
                {
                    GetComponent<CharacterClassManager>().TargetConsolePrint(base.connectionToClient, "ECDHE exchange was requested, please use encrypted channel for remote admin commands.", "magenta");
                }
                else if (VerifyRequestSignature(query, counter, signature))
                {
                    ProcessQuery(query);
                }
                else
                {
                    GetComponent<CharacterClassManager>().TargetConsolePrint(base.connectionToClient, "Signature verification of request \"" + query + "\" failed!", "magenta");
                }
            }
            else
            {
                GetComponent<CharacterClassManager>().TargetConsolePrint(base.connectionToClient, "You are not logged in to remote admin panel!", "red");
            }
        }

        [ServerCallback]
        internal void ProcessQuery(string q)
        {
            if (!NetworkServer.active)
            {
                return;
            }
            string[] query = q.Split(' ');
            string myNick = GetComponent<NicknameSync>().myNick;
            int failures;
            int successes;
            string error;
            bool replySent;
            switch (query[0].ToUpper())
            {
                case "HELLO":
                    TargetReply(base.connectionToClient, query[0].ToUpper() + "#Hello World!", true, true, string.Empty);
                    break;
                case "HELP":
                    TargetReply(base.connectionToClient, query[0].ToUpper() + "#This should be useful!", true, true, string.Empty);
                    break;
                case "CASSIE":
                    if (CheckPermissions(query[0], PlayerPermissions.FacilityManagement, string.Empty))
                    {
                        if (query.Length > 1)
                        {
                            ServerLogs.AddLog(ServerLogs.Modules.Administrative, myNick + " ran the cassie command (parameters: " + q.Remove(0, 7) + ").", ServerLogs.ServerLogType.RemoteAdminActivity_GameChanging);
                            PlayerManager.localPlayer.GetComponent<MTFRespawn>().RpcPlayCustomAnnouncement(q.Remove(0, 7), false);
                        }
                        else
                        {
                            TargetReply(base.connectionToClient, query[0].ToUpper() + "#To run this program, type at least 2 arguments! (some parameters are missing)", false, true, string.Empty);
                        }
                    }
                    break;
                case "BROADCAST":
                case "BC":
                case "ALERT":
                case "BROADCASTMONO":
                case "BCMONO":
                case "ALERTMONO":
                    if (CheckPermissions(query[0], PlayerPermissions.FacilityManagement, string.Empty))
                    {
                        if (query.Length < 2)
                        {
                            TargetReply(base.connectionToClient, query[0].ToUpper() + "#To run this program, type at least 2 arguments! (some parameters are missing)", false, true, string.Empty);
                        }
                        uint result2 = 0u;
                        if (!uint.TryParse(query[1], out result2) || result2 < 1)
                        {
                            TargetReply(base.connectionToClient, query[0].ToUpper() + "#First argument must be a positive integer.", false, true, string.Empty);
                        }
                        string text6 = q.Substring(query[0].Length + query[1].Length + 2);
                        ServerLogs.AddLog(ServerLogs.Modules.Administrative, myNick + " ran the broadcast command (duration: " + query[1] + " seconds) with text \"" + text6 + "\" players.", ServerLogs.ServerLogType.RemoteAdminActivity_GameChanging);
                        GetComponent<Broadcast>().RpcAddElement(text6, result2, query[0].ToLower().Contains("mono"));
                        TargetReply(base.connectionToClient, query[0].ToUpper() + "#Broadcast sent.", false, true, string.Empty);
                    }
                    break;
                case "CLEARBC":
                case "BCCLEAR":
                case "CLEARALERT":
                case "ALERTCLEAR":
                    if (CheckPermissions(query[0], PlayerPermissions.FacilityManagement, string.Empty))
                    {
                        ServerLogs.AddLog(ServerLogs.Modules.Administrative, myNick + " ran the cleared all broadcasts.", ServerLogs.ServerLogType.RemoteAdminActivity_GameChanging);
                        GetComponent<Broadcast>().RpcClearElements();
                        TargetReply(base.connectionToClient, query[0].ToUpper() + "#All broadcasts cleared.", false, true, string.Empty);
                    }
                    break;
                case "BAN":
                    if (query.Length >= 3)
                    {
                        ServerLogs.AddLog(ServerLogs.Modules.Administrative, myNick + " ran the ban command (duration: " + query[2] + " min) on " + query[1] + " players.", ServerLogs.ServerLogType.RemoteAdminActivity_GameChanging);
                        StandardizedQueryModel1(query[0], query[1], query[2], out failures, out successes, out error, out replySent);
                        if (replySent)
                        {
                            break;
                        }
                        if (failures == 0)
                        {
                            string text5 = "Banned";
                            int result;
                            if (int.TryParse(query[2], out result))
                            {
                                text5 = ((result <= 0) ? "Kicked" : "Banned");
                            }
                            TargetReply(base.connectionToClient, query[0] + "#Done! " + text5 + " " + successes + " player(s)!", true, true, string.Empty);
                        }
                        else
                        {
                            TargetReply(base.connectionToClient, query[0] + "#The proccess has occured an issue! Failures: " + failures + "\nLast error log:\n" + error, false, true, string.Empty);
                        }
                    }
                    else
                    {
                        TargetReply(base.connectionToClient, query[0].ToUpper() + "#To run this program, type at least 3 arguments! (some parameters are missing)", false, true, string.Empty);
                    }
                    break;
                case "GBAN-KICK":
                    if (GetComponent<ServerRoles>().RaEverywhere || (GetComponent<ServerRoles>().Staff && CheckPermissions(query[0].ToUpper(), PlayerPermissions.KickingAndShortTermBanning, string.Empty)) || CheckPermissions(query[0].ToUpper(), PlayerPermissions.PermissionsManagement, string.Empty))
                    {
                        if (query.Length != 2)
                        {
                            TargetReply(base.connectionToClient, query[0].ToUpper() + "#To run this program, type exactly 1 argument! (some parameters are missing)", false, true, string.Empty);
                            break;
                        }
                        ServerLogs.AddLog(ServerLogs.Modules.Administrative, myNick + " globally banned and kicked " + query[1] + " player.", ServerLogs.ServerLogType.RemoteAdminActivity_GameChanging);
                        StandardizedQueryModel1(query[0], query[1], "0", out failures, out successes, out error, out replySent);
                    }
                    break;
                case "SUDO":
                case "RCON":
                    {
                        if (!CheckPermissions(query[0].ToUpper(), PlayerPermissions.ServerConsoleCommands, string.Empty))
                        {
                            break;
                        }
                        if (query.Length < 2)
                        {
                            TargetReply(base.connectionToClient, query[0].ToUpper() + "#To run this program, type at least 1 argument! (some parameters are missing)", false, true, string.Empty);
                            break;
                        }
                        string text4 = query.Skip(1).Aggregate(string.Empty, (string text20, string arg) => text20 + arg + " ");
                        text4 = text4.Substring(0, text4.Length - 1);
                        ServerLogs.AddLog(ServerLogs.Modules.Administrative, myNick + " executed command as server console: " + text4 + " player.", ServerLogs.ServerLogType.RemoteAdminActivity_GameChanging);
                        ServerConsole.EnterCommand(text4);
                        TargetReply(base.connectionToClient, query[0] + "#Command \"" + text4 + "\" executed in server console!", true, true, string.Empty);
                        break;
                    }
                case "SETGROUP":
                    if (!CheckPermissions(query[0].ToUpper(), PlayerPermissions.SetGroup, string.Empty))
                    {
                        break;
                    }
                    if (query.Length >= 3)
                    {
                        ServerLogs.AddLog(ServerLogs.Modules.Permissions, myNick + " ran the setgroup command (new group: " + query[2] + " min) on " + query[1] + " players.", ServerLogs.ServerLogType.RemoteAdminActivity_GameChanging);
                        StandardizedQueryModel1(query[0], query[1], query[2], out failures, out successes, out error, out replySent);
                        if (!replySent)
                        {
                            if (failures == 0)
                            {
                                TargetReply(base.connectionToClient, query[0] + "#Done! The request affected " + successes + " player(s)!", true, true, string.Empty);
                            }
                            else
                            {
                                TargetReply(base.connectionToClient, query[0] + "#The proccess has occured an issue! Failures: " + failures + "\nLast error log:\n" + error, false, true, string.Empty);
                            }
                        }
                    }
                    else
                    {
                        TargetReply(base.connectionToClient, query[0].ToUpper() + "#To run this program, type at least 3 arguments! (some parameters are missing)", false, true, string.Empty);
                    }
                    break;
                case "PM":
                    {
                        if (!CheckPermissions(query[0].ToUpper(), PlayerPermissions.PermissionsManagement, string.Empty))
                        {
                            break;
                        }
                        Dictionary<string, UserGroup> allGroups = ServerStatic.PermissionsHandler.GetAllGroups();
                        List<string> allPermissions2 = ServerStatic.PermissionsHandler.GetAllPermissions();
                        if (query.Length == 1)
                        {
                            TargetReply(base.connectionToClient, query[0].ToUpper() + "#Permissions manager help:\nSyntax: " + query[0] + " action\n\nAvailable actions:\ngroups - lists all groups\ngroup info <group name> - prints group info\ngroup grant <group name> <permission name> - grants permission to a group\ngroup revoke <group name> <permission name> - revokes permission from a group\ngroup setcolor <group name> <color name> - sets group color\ngroup settag <group name> <tag> - sets group tag\ngroup enablecover <group name> - enables badge cover for group\ngroup disablecover <group name> - disables badge cover for group\n\nusers - lists all privileged users\nsetgroup <SteamID 64> <group name> - sets membership of user (use group name \"-1\" to remove user from group)\nreload - reloads permission file\n\n\"< >\" are only used to indicate the arguments, don't put them\nMore commands will be added in the next versions of the game", true, true, string.Empty);
                        }
                        else if (query[1].ToLower() == "groups")
                        {
                            int num3 = 0;
                            string text11 = "\n";
                            string text12 = string.Empty;
                            for (int num4 = 0; num4 < 5; num4++)
                            {
                                text11 += "-";
                            }
                            text11 += " BN1 BN2 BN3 FSE FSP FWR GIV EWA ERE ERO SGR GMD OVR FCM PLM PRM SCC VHB CFG";
                            foreach (KeyValuePair<string, UserGroup> item in allGroups)
                            {
                                string text3 = text12;
                                text12 = text3 + "\n" + num3 + " - " + item.Key;
                                string text13 = num3.ToString();
                                for (int num5 = text13.Length; num5 < 5; num5++)
                                {
                                    text13 += " ";
                                }
                                foreach (string item2 in allPermissions2)
                                {
                                    text13 = text13 + "  " + ((!ServerStatic.PermissionsHandler.IsPermitted(item.Value.Permissions, item2)) ? " " : "X") + " ";
                                }
                                num3++;
                                text11 = text11 + "\n" + text13;
                            }
                            TargetReply(base.connectionToClient, query[0].ToUpper() + "#All defined groups: " + text11 + "\n" + text12, true, true, string.Empty);
                        }
                        else if (query[1].ToLower() == "group" && query.Length == 2)
                        {
                            TargetReply(base.connectionToClient, query[0].ToUpper() + "#Unknown action. Run " + query[0] + " to get list of all actions.", false, true, string.Empty);
                        }
                        else if (query[1].ToLower() == "group" && query.Length > 2)
                        {
                            if (query[2].ToLower() == "info" && query.Length == 4)
                            {
                                KeyValuePair<string, UserGroup> keyValuePair = allGroups.FirstOrDefault((KeyValuePair<string, UserGroup> gr) => gr.Key == query[3]);
                                if (keyValuePair.Key == null)
                                {
                                    TargetReply(base.connectionToClient, query[0].ToUpper() + "#Group can't be found.", false, true, string.Empty);
                                    break;
                                }
                                TargetReply(base.connectionToClient, query[0].ToUpper() + "#Details of group " + keyValuePair.Key + "\nTag text: " + keyValuePair.Value.BadgeText + "\nTag color: " + keyValuePair.Value.BadgeColor + "\nPermissions: " + keyValuePair.Value.Permissions + "\nCover: " + ((!keyValuePair.Value.Cover) ? "NO" : "YES") + "\nHidden by default: " + ((!keyValuePair.Value.HiddenByDefault) ? "NO" : "YES"), true, true, string.Empty);
                            }
                            else if ((query[2].ToLower() == "grant" || query[2].ToLower() == "revoke") && query.Length == 5)
                            {
                                if (allGroups.FirstOrDefault((KeyValuePair<string, UserGroup> gr) => gr.Key == query[3]).Key == null)
                                {
                                    TargetReply(base.connectionToClient, query[0].ToUpper() + "#Group can't be found.", false, true, string.Empty);
                                    break;
                                }
                                if (!allPermissions2.Contains(query[4]))
                                {
                                    TargetReply(base.connectionToClient, query[0].ToUpper() + "#Permission can't be found.", false, true, string.Empty);
                                    break;
                                }
                                Dictionary<string, string> stringDictionary = ServerStatic.RolesConfig.GetStringDictionary("Permissions");
                                List<string> list = null;
                                foreach (string key in stringDictionary.Keys)
                                {
                                    if (!(key != query[4]))
                                    {
                                        list = YamlConfig.ParseCommaSeparatedString(stringDictionary[key]).ToList();
                                    }
                                }
                                if (list == null)
                                {
                                    TargetReply(base.connectionToClient, query[0].ToUpper() + "#Permission can't be found in the config.", false, true, string.Empty);
                                    break;
                                }
                                if (list.Contains(query[3]) && query[2].ToLower() == "grant")
                                {
                                    TargetReply(base.connectionToClient, query[0].ToUpper() + "#Group already has that permission.", false, true, string.Empty);
                                    break;
                                }
                                if (!list.Contains(query[3]) && query[2].ToLower() == "revoke")
                                {
                                    TargetReply(base.connectionToClient, query[0].ToUpper() + "#Group already doesn't have that permission.", false, true, string.Empty);
                                    break;
                                }
                                if (query[2].ToLower() == "grant")
                                {
                                    list.Add(query[3]);
                                }
                                else
                                {
                                    list.Remove(query[3]);
                                }
                                list.Sort();
                                string text14 = "[";
                                foreach (string item3 in list)
                                {
                                    if (text14 != "[")
                                    {
                                        text14 += ", ";
                                    }
                                    text14 += item3;
                                }
                                text14 += "]";
                                ServerStatic.RolesConfig.SetStringDictionaryItem("Permissions", query[4], text14);
                                ServerStatic.PermissionsHandler = new PermissionsHandler(ref ServerStatic.RolesConfig);
                                TargetReply(base.connectionToClient, query[0].ToUpper() + "#Permissions updated.", true, true, string.Empty);
                            }
                            else if (query[2].ToLower() == "setcolor" && query.Length == 5)
                            {
                                if (allGroups.FirstOrDefault((KeyValuePair<string, UserGroup> gr) => gr.Key == query[3]).Key == null)
                                {
                                    TargetReply(base.connectionToClient, query[0].ToUpper() + "#Group can't be found.", false, true, string.Empty);
                                    break;
                                }
                                ServerStatic.RolesConfig.SetString(query[3] + "_color", query[4]);
                                ServerStatic.PermissionsHandler = new PermissionsHandler(ref ServerStatic.RolesConfig);
                                TargetReply(base.connectionToClient, query[0].ToUpper() + "#Group color updated.", true, true, string.Empty);
                            }
                            else if (query[2].ToLower() == "settag" && query.Length == 5)
                            {
                                if (allGroups.FirstOrDefault((KeyValuePair<string, UserGroup> gr) => gr.Key == query[3]).Key == null)
                                {
                                    TargetReply(base.connectionToClient, query[0].ToUpper() + "#Group can't be found.", false, true, string.Empty);
                                    break;
                                }
                                ServerStatic.RolesConfig.SetString(query[3] + "_badge", query[4]);
                                ServerStatic.PermissionsHandler = new PermissionsHandler(ref ServerStatic.RolesConfig);
                                TargetReply(base.connectionToClient, query[0].ToUpper() + "#Group tag updated.", true, true, string.Empty);
                            }
                            else if (query[2].ToLower() == "enablecover" && query.Length == 4)
                            {
                                if (allGroups.FirstOrDefault((KeyValuePair<string, UserGroup> gr) => gr.Key == query[3]).Key == null)
                                {
                                    TargetReply(base.connectionToClient, query[0].ToUpper() + "#Group can't be found.", false, true, string.Empty);
                                    break;
                                }
                                ServerStatic.RolesConfig.SetString(query[3] + "_cover", "true");
                                ServerStatic.PermissionsHandler = new PermissionsHandler(ref ServerStatic.RolesConfig);
                                TargetReply(base.connectionToClient, query[0].ToUpper() + "#Enabled cover for group " + query[3] + ".", true, true, string.Empty);
                            }
                            else if (query[2].ToLower() == "disablecover" && query.Length == 4)
                            {
                                if (allGroups.FirstOrDefault((KeyValuePair<string, UserGroup> gr) => gr.Key == query[3]).Key == null)
                                {
                                    TargetReply(base.connectionToClient, query[0].ToUpper() + "#Group can't be found.", false, true, string.Empty);
                                    break;
                                }
                                ServerStatic.RolesConfig.SetString(query[3] + "_cover", "false");
                                ServerStatic.PermissionsHandler = new PermissionsHandler(ref ServerStatic.RolesConfig);
                                TargetReply(base.connectionToClient, query[0].ToUpper() + "#Enabled cover for group " + query[3] + ".", true, true, string.Empty);
                            }
                            else
                            {
                                TargetReply(base.connectionToClient, query[0].ToUpper() + "#Unknown action. Run " + query[0] + " to get list of all actions.", false, true, string.Empty);
                            }
                        }
                        else if (query[1].ToLower() == "users")
                        {
                            Dictionary<string, string> stringDictionary2 = ServerStatic.RolesConfig.GetStringDictionary("Members");
                            string text15 = "Players with assigned groups:";
                            foreach (KeyValuePair<string, string> item4 in stringDictionary2)
                            {
                                string text3 = text15;
                                text15 = text3 + "\n" + item4.Key + " - " + item4.Value;
                            }
                            TargetReply(base.connectionToClient, query[0].ToUpper() + "#" + text15, true, true, string.Empty);
                        }
                        else if (query[1].ToLower() == "setgroup" && query.Length == 4)
                        {
                            string empty = string.Empty;
                            if (query[3] == "-1")
                            {
                                empty = null;
                            }
                            else
                            {
                                KeyValuePair<string, UserGroup> keyValuePair2 = allGroups.FirstOrDefault((KeyValuePair<string, UserGroup> gr) => gr.Key == query[3]);
                                if (keyValuePair2.Key == null)
                                {
                                    TargetReply(base.connectionToClient, query[0].ToUpper() + "#Group can't be found.", false, true, string.Empty);
                                    break;
                                }
                                empty = keyValuePair2.Key;
                            }
                            ServerStatic.RolesConfig.SetStringDictionaryItem("Members", query[2], empty);
                            ServerStatic.PermissionsHandler = new PermissionsHandler(ref ServerStatic.RolesConfig);
                            TargetReply(base.connectionToClient, query[0].ToUpper() + "#User permissions updated. If user is online, please use \"setgroup\" command to change it now (without this command, new role will be applied during next round).", true, true, string.Empty);
                        }
                        else if (query[1].ToLower() == "reload")
                        {
                            ServerStatic.RolesConfig.Reload();
                            ServerStatic.PermissionsHandler = new PermissionsHandler(ref ServerStatic.RolesConfig);
                            TargetReply(base.connectionToClient, query[0].ToUpper() + "#Permission file reloaded.", true, true, string.Empty);
                        }
                        else
                        {
                            TargetReply(base.connectionToClient, query[0].ToUpper() + "#Unknown action. Run " + query[0] + " to get list of all actions.", false, true, string.Empty);
                        }
                        break;
                    }
                case "SLML_STYLE":
                case "SLML_TAG":
                    if (query.Length >= 3)
                    {
                        ServerLogs.AddLog(ServerLogs.Modules.Logger, myNick + " Requested a download of " + query[2] + " on " + query[1] + " players' computers.", ServerLogs.ServerLogType.RemoteAdminActivity_Misc);
                        StandardizedQueryModel1(query[0], query[1], query[2], out failures, out successes, out error, out replySent);
                        if (!replySent)
                        {
                            if (failures == 0)
                            {
                                TargetReply(base.connectionToClient, query[0] + "#Done! " + successes + " player(s) affected!", true, true, string.Empty);
                            }
                            else
                            {
                                TargetReply(base.connectionToClient, query[0] + "#The proccess has occured an issue! Failures: " + failures + "\nLast error log:\n" + error, false, true, string.Empty);
                            }
                        }
                    }
                    else
                    {
                        TargetReply(base.connectionToClient, query[0].ToUpper() + "#To run this program, type at least 3 arguments! (some parameters are missing)", false, true, string.Empty);
                    }
                    break;
                case "UNBAN":
                    if (!CheckPermissions(query[0].ToUpper(), PlayerPermissions.LongTermBanning, string.Empty))
                    {
                        break;
                    }
                    if (query.Length == 3)
                    {
                        ServerLogs.AddLog(ServerLogs.Modules.Administrative, myNick + " ran the unban " + query[1] + " command on " + query[2] + ".", ServerLogs.ServerLogType.RemoteAdminActivity_Misc);
                        switch (query[1].ToLower())
                        {
                            case "id":
                            case "steamid":
                                BanHandler.RemoveBan(query[2], BanHandler.BanType.Steam);
                                TargetReply(base.connectionToClient, query[0] + "#Done!", true, true, string.Empty);
                                break;
                            case "ip":
                            case "address":
                                BanHandler.RemoveBan(query[2], BanHandler.BanType.IP);
                                TargetReply(base.connectionToClient, query[0] + "#Done!", true, true, string.Empty);
                                break;
                            default:
                                TargetReply(base.connectionToClient, query[0].ToUpper() + "#Correct syntax is: unban id SteamIdHere OR unban ip IpAddressHere", false, true, string.Empty);
                                break;
                        }
                    }
                    else
                    {
                        TargetReply(base.connectionToClient, query[0].ToUpper() + "#Correct syntax is: unban id SteamIdHere OR unban ip IpAddressHere", false, true, string.Empty);
                    }
                    break;
                case "GROUPS":
                    {
                        string text19 = "Groups defined on this server:";
                        Dictionary<string, UserGroup> allGroups2 = ServerStatic.PermissionsHandler.GetAllGroups();
                        ServerRoles.NamedColor[] namedColors = GetComponent<ServerRoles>().NamedColors;
                        foreach (KeyValuePair<string, UserGroup> permentry in allGroups2)
                        {
                            try
                            {
                                string text3 = text19;
                                text19 = text3 + "\n" + permentry.Key + " (" + permentry.Value.Permissions + ") - <color=#" + namedColors.FirstOrDefault((ServerRoles.NamedColor x) => x.Name == permentry.Value.BadgeColor).ColorHex + ">" + permentry.Value.BadgeText + "</color> in color " + permentry.Value.BadgeColor;
                            }
                            catch
                            {
                                string text3 = text19;
                                text19 = text3 + "\n" + permentry.Key + " (" + permentry.Value.Permissions + ") - " + permentry.Value.BadgeText + " in color " + permentry.Value.BadgeColor;
                            }
                            if (ServerStatic.PermissionsHandler.IsPermitted(permentry.Value.Permissions, PlayerPermissions.KickingAndShortTermBanning))
                            {
                                text19 += " BN1";
                            }
                            if (ServerStatic.PermissionsHandler.IsPermitted(permentry.Value.Permissions, PlayerPermissions.BanningUpToDay))
                            {
                                text19 += " BN2";
                            }
                            if (ServerStatic.PermissionsHandler.IsPermitted(permentry.Value.Permissions, PlayerPermissions.LongTermBanning))
                            {
                                text19 += " BN3";
                            }
                            if (ServerStatic.PermissionsHandler.IsPermitted(permentry.Value.Permissions, PlayerPermissions.ForceclassSelf))
                            {
                                text19 += " FSE";
                            }
                            if (ServerStatic.PermissionsHandler.IsPermitted(permentry.Value.Permissions, PlayerPermissions.ForceclassToSpectator))
                            {
                                text19 += " FSP";
                            }
                            if (ServerStatic.PermissionsHandler.IsPermitted(permentry.Value.Permissions, PlayerPermissions.ForceclassWithoutRestrictions))
                            {
                                text19 += " FWR";
                            }
                            if (ServerStatic.PermissionsHandler.IsPermitted(permentry.Value.Permissions, PlayerPermissions.GivingItems))
                            {
                                text19 += " GIV";
                            }
                            if (ServerStatic.PermissionsHandler.IsPermitted(permentry.Value.Permissions, PlayerPermissions.WarheadEvents))
                            {
                                text19 += " EWA";
                            }
                            if (ServerStatic.PermissionsHandler.IsPermitted(permentry.Value.Permissions, PlayerPermissions.RespawnEvents))
                            {
                                text19 += " ERE";
                            }
                            if (ServerStatic.PermissionsHandler.IsPermitted(permentry.Value.Permissions, PlayerPermissions.RoundEvents))
                            {
                                text19 += " ERO";
                            }
                            if (ServerStatic.PermissionsHandler.IsPermitted(permentry.Value.Permissions, PlayerPermissions.SetGroup))
                            {
                                text19 += " SGR";
                            }
                            if (ServerStatic.PermissionsHandler.IsPermitted(permentry.Value.Permissions, PlayerPermissions.GameplayData))
                            {
                                text19 += " GMD";
                            }
                            if (ServerStatic.PermissionsHandler.IsPermitted(permentry.Value.Permissions, PlayerPermissions.Overwatch))
                            {
                                text19 += " OVR";
                            }
                            if (ServerStatic.PermissionsHandler.IsPermitted(permentry.Value.Permissions, PlayerPermissions.FacilityManagement))
                            {
                                text19 += " FCM";
                            }
                            if (ServerStatic.PermissionsHandler.IsPermitted(permentry.Value.Permissions, PlayerPermissions.PlayersManagement))
                            {
                                text19 += " PLM";
                            }
                            if (ServerStatic.PermissionsHandler.IsPermitted(permentry.Value.Permissions, PlayerPermissions.PermissionsManagement))
                            {
                                text19 += " PRM";
                            }
                            if (ServerStatic.PermissionsHandler.IsPermitted(permentry.Value.Permissions, PlayerPermissions.ServerConsoleCommands))
                            {
                                text19 += " SCC";
                            }
                            if (ServerStatic.PermissionsHandler.IsPermitted(permentry.Value.Permissions, PlayerPermissions.ViewHiddenBadges))
                            {
                                text19 += " VHB";
                            }
                            if (ServerStatic.PermissionsHandler.IsPermitted(permentry.Value.Permissions, PlayerPermissions.ServerConfigs))
                            {
                                text19 += " CFG";
                            }
                        }
                        TargetReply(base.connectionToClient, query[0].ToUpper() + "#" + text19, true, true, string.Empty);
                        break;
                    }
                case "FORCESTART":
                    {
                        if (!CheckPermissions(query[0].ToUpper(), PlayerPermissions.RoundEvents, "ServerEvents"))
                        {
                            break;
                        }
                        bool flag3 = false;
                        GameObject gameObject8 = GameObject.Find("Host");
                        if (gameObject8 != null)
                        {
                            CharacterClassManager component5 = gameObject8.GetComponent<CharacterClassManager>();
                            if (component5 != null && component5.isLocalPlayer && component5.isServer && !component5.roundStarted)
                            {
                                ServerLogs.AddLog(ServerLogs.Modules.Administrative, myNick + " roced round start.", ServerLogs.ServerLogType.RemoteAdminActivity_GameChanging);
                                component5.ForceRoundStart();
                                flag3 = true;
                            }
                        }
                        TargetReply(base.connectionToClient, query[0] + "#" + ((!flag3) ? "Failed to force start." : "Done! Forced round start."), flag3, true, "ServerEvents");
                        break;
                    }
                case "SC":
                case "CONFIG":
                case "SETCONFIG":
                    if (!CheckPermissions(query[0].ToUpper(), PlayerPermissions.ServerConfigs, "ServerConfigs"))
                    {
                        break;
                    }
                    if (query.Length >= 3)
                    {
                        if (query.Length > 3)
                        {
                            string text17 = query[2];
                            for (int num8 = 3; num8 < query.Length; num8++)
                            {
                                text17 = text17 + " " + query[num8];
                            }
                            query = new string[3]
                            {
                            query[0],
                            query[1],
                            text17
                            };
                        }
                        ServerLogs.AddLog(ServerLogs.Modules.Administrative, myNick + " ran the setconfig command (" + query[1] + ": " + query[2] + ").", ServerLogs.ServerLogType.RemoteAdminActivity_GameChanging);
                        switch (query[1])
                        {
                            case "friendly_fire":
                                {
                                    bool result6;
                                    if (bool.TryParse(query[2], out result6))
                                    {
                                        ServerConsole.FriendlyFire = result6;
                                        WeaponManager[] array6 = UnityEngine.Object.FindObjectsOfType<WeaponManager>();
                                        foreach (WeaponManager weaponManager in array6)
                                        {
                                            weaponManager.friendlyFire = result6;
                                        }
                                        TargetReply(base.connectionToClient, query[0].ToUpper() + "#Done! Config [" + query[1] + "] has been set to [" + result6 + "]!", true, true, "ServerConfigs");
                                    }
                                    else
                                    {
                                        TargetReply(base.connectionToClient, query[0].ToUpper() + "#" + query[1] + " has invalid value, " + query[2] + " is not a valid bool!", false, true, "ServerConfigs");
                                    }
                                    break;
                                }
                            case "player_list_title":
                                {
                                    string text18 = ((!string.IsNullOrEmpty(query[2])) ? query[2] : ConfigFile.ServerConfig.GetString("server_name", "Player List"));
                                    UnityEngine.Object.FindObjectOfType<PlayerList>().NetworksyncServerName = text18;
                                    TargetReply(base.connectionToClient, query[0].ToUpper() + "#Done! Config [" + query[1] + "] has been set to [" + text18 + "]!", true, true, "ServerConfigs");
                                    break;
                                }
                            case "pd_refresh_exit":
                                {
                                    bool result7;
                                    if (bool.TryParse(query[2], out result7))
                                    {
                                        UnityEngine.Object.FindObjectOfType<PocketDimensionTeleport>().RefreshExit = result7;
                                        TargetReply(base.connectionToClient, query[0].ToUpper() + "#Done! Config [" + query[1] + "] has been set to [" + result7 + "]!", true, true, "ServerConfigs");
                                    }
                                    else
                                    {
                                        TargetReply(base.connectionToClient, query[0].ToUpper() + "#" + query[1] + " has invalid value, " + query[2] + " is not a valid bool!", false, true, "ServerConfigs");
                                    }
                                    break;
                                }
                            case "spawn_protect_disable":
                                {
                                    if (bool.TryParse(query[2], out bool result5))
                                    {
                                        CharacterClassManager[] array5 = UnityEngine.Object.FindObjectsOfType<CharacterClassManager>();
                                        foreach (CharacterClassManager characterClassManager2 in array5)
                                        {
                                            characterClassManager2.EnableSP = !result5;
                                        }
                                        TargetReply(base.connectionToClient, query[0].ToUpper() + "#Done! Config [" + query[1] + "] has been set to [" + result5 + "]!", true, true, "ServerConfigs");
                                    }
                                    else
                                    {
                                        TargetReply(base.connectionToClient, query[0].ToUpper() + "#" + query[1] + " has invalid value, " + query[2] + " is not a valid bool!", false, true, "ServerConfigs");
                                    }
                                    break;
                                }
                            case "spawn_protect_time":
                                {
                                    int result4;
                                    if (int.TryParse(query[2], NumberStyles.Any, CultureInfo.InvariantCulture, out result4))
                                    {
                                        CharacterClassManager[] array4 = UnityEngine.Object.FindObjectsOfType<CharacterClassManager>();
                                        foreach (CharacterClassManager characterClassManager in array4)
                                        {
                                            characterClassManager.SProtectedDuration = result4;
                                        }
                                        TargetReply(base.connectionToClient, query[0].ToUpper() + "#Done! Config [" + query[1] + "] has been set to [" + result4 + "]!", true, true, "ServerConfigs");
                                    }
                                    else
                                    {
                                        TargetReply(base.connectionToClient, query[0].ToUpper() + "#" + query[1] + " has invalid value, " + query[2] + " is not a valid integer!", false, true, "ServerConfigs");
                                    }
                                    break;
                                }
                            default:
                                TargetReply(base.connectionToClient, query[0].ToUpper() + "#Invalid config " + query[1], false, true, "ServerConfigs");
                                break;
                        }
                    }
                    else
                    {
                        TargetReply(base.connectionToClient, query[0].ToUpper() + "#To run this program, type at least 3 arguments! (some parameters are missing)", false, true, "ServerConfigs");
                    }
                    break;
                case "FC":
                case "FORCECLASS":
                    if (query.Length >= 3)
                    {
                        int result3 = 0;
                        if (!int.TryParse(query[2], out result3) || result3 < 0 || result3 >= GetComponent<CharacterClassManager>().klasy.Length)
                        {
                            TargetReply(base.connectionToClient, query[0].ToUpper() + "#Invalid class ID.", false, true, string.Empty);
                            break;
                        }
                        string fullName = GetComponent<CharacterClassManager>().klasy[result3].fullName;
                        GameObject gameObject9 = GameObject.Find("Host");
                        if (gameObject9 == null)
                        {
                            TargetReply(base.connectionToClient, query[0].ToUpper() + "#Please start round before using this command.", false, true, string.Empty);
                            break;
                        }
                        CharacterClassManager component6 = gameObject9.GetComponent<CharacterClassManager>();
                        if (component6 == null || !component6.isLocalPlayer || !component6.isServer || !component6.roundStarted)
                        {
                            TargetReply(base.connectionToClient, query[0].ToUpper() + "#Please start round before using this command.", false, true, string.Empty);
                            break;
                        }
                        bool flag4 = query[1] == PlayerId.ToString() || query[1] == PlayerId + ".";
                        bool flag5 = result3 == 2;
                        if ((flag4 && flag5 && !CheckPermissions(query[0].ToUpper(), new PlayerPermissions[3]
                        {
                        PlayerPermissions.ForceclassWithoutRestrictions,
                        PlayerPermissions.ForceclassToSpectator,
                        PlayerPermissions.ForceclassSelf
                        }, string.Empty)) || (flag4 && !flag5 && !CheckPermissions(query[0].ToUpper(), new PlayerPermissions[2]
                        {
                        PlayerPermissions.ForceclassWithoutRestrictions,
                        PlayerPermissions.ForceclassSelf
                        }, string.Empty)) || (!flag4 && flag5 && !CheckPermissions(query[0].ToUpper(), new PlayerPermissions[2]
                        {
                        PlayerPermissions.ForceclassWithoutRestrictions,
                        PlayerPermissions.ForceclassToSpectator
                        }, string.Empty)) || (!flag4 && !flag5 && !CheckPermissions(query[0].ToUpper(), new PlayerPermissions[1] { PlayerPermissions.ForceclassWithoutRestrictions }, string.Empty)))
                        {
                            break;
                        }
                        if (query[0].ToLower() == "role")
                        {
                            ServerLogs.AddLog(ServerLogs.Modules.ClassChange, myNick + " ran the role command (ID: " + query[2] + " - " + fullName + ") on " + query[1] + " players.", ServerLogs.ServerLogType.RemoteAdminActivity_GameChanging);
                        }
                        else
                        {
                            ServerLogs.AddLog(ServerLogs.Modules.ClassChange, myNick + " ran the forceclass command (ID: " + query[2] + " - " + fullName + ") on " + query[1] + " players.", ServerLogs.ServerLogType.RemoteAdminActivity_GameChanging);
                        }
                        StandardizedQueryModel1(query[0], query[1], query[2], out failures, out successes, out error, out replySent);
                        if (!replySent)
                        {
                            if (failures == 0)
                            {
                                TargetReply(base.connectionToClient, query[0] + "#Done! The request affected " + successes + " player(s)!", true, true, string.Empty);
                            }
                            else
                            {
                                TargetReply(base.connectionToClient, query[0] + "#The proccess has occured an issue! Failures: " + failures + "\nLast error log:\n" + error, false, true, string.Empty);
                            }
                        }
                    }
                    else
                    {
                        TargetReply(base.connectionToClient, query[0].ToUpper() + "#To run this program, type at least 3 arguments! (some parameters are missing)", false, true, string.Empty);
                    }
                    break;
                case "HEAL":
                    if (!CheckPermissions(query[0].ToUpper(), PlayerPermissions.PlayersManagement, string.Empty))
                    {
                        break;
                    }
                    if (query.Length >= 2)
                    {
                        int num2 = ((query.Length >= 3 && int.TryParse(query[2], out num2)) ? num2 : 0);
                        ServerLogs.AddLog(ServerLogs.Modules.Administrative, myNick + " ran the heal command on " + query[1] + " players.", ServerLogs.ServerLogType.RemoteAdminActivity_GameChanging);
                        StandardizedQueryModel1(query[0], query[1], num2.ToString(), out failures, out successes, out error, out replySent);
                        if (!replySent)
                        {
                            if (failures == 0)
                            {
                                TargetReply(base.connectionToClient, query[0] + "#Done! The request affected " + successes + " player(s)!", true, true, string.Empty);
                            }
                            else
                            {
                                TargetReply(base.connectionToClient, query[0] + "#The proccess has occured an issue! Failures: " + failures + "\nLast error log:\n" + error, false, true, "AdminTools");
                            }
                        }
                    }
                    else
                    {
                        TargetReply(base.connectionToClient, query[0].ToUpper() + "#To run this program, type at least 2 arguments! (some parameters are missing)", false, true, string.Empty);
                    }
                    break;
                case "HP":
                case "SETHP":
                    if (!CheckPermissions(query[0].ToUpper(), PlayerPermissions.PlayersManagement, string.Empty))
                    {
                        break;
                    }
                    if (query.Length >= 3)
                    {
                        ServerLogs.AddLog(ServerLogs.Modules.Administrative, myNick + " ran the sethp command on " + query[1] + " players (HP: " + query[2] + ").", ServerLogs.ServerLogType.RemoteAdminActivity_GameChanging);
                        StandardizedQueryModel1(query[0], query[1], query[2], out failures, out successes, out error, out replySent);
                        if (!replySent)
                        {
                            if (failures == 0)
                            {
                                TargetReply(base.connectionToClient, query[0] + "#Done! The request affected " + successes + " player(s)!", true, true, string.Empty);
                            }
                            else
                            {
                                TargetReply(base.connectionToClient, query[0] + "#The proccess has occured an issue! Failures: " + failures + "\nLast error log:\n" + error, false, true, string.Empty);
                            }
                        }
                    }
                    else
                    {
                        TargetReply(base.connectionToClient, query[0].ToUpper() + "#To run this program, type at least 3 arguments! (some parameters are missing)", false, true, string.Empty);
                    }
                    break;
                case "OVR":
                case "OVERWATCH":
                    if (!CheckPermissions(query[0].ToUpper(), PlayerPermissions.Overwatch, string.Empty))
                    {
                        break;
                    }
                    if (query.Length >= 2)
                    {
                        if (query.Length == 2)
                        {
                            query = new string[3]
                            {
                            query[0],
                            query[1],
                            string.Empty
                            };
                        }
                        ServerLogs.AddLog(ServerLogs.Modules.ClassChange, myNick + " ran the overwatch command (new status: " + ((!(query[2] == string.Empty)) ? query[2] : "TOGGLE") + ") on " + query[1] + " players.", ServerLogs.ServerLogType.RemoteAdminActivity_GameChanging);
                        StandardizedQueryModel1("OVERWATCH", query[1], query[2], out failures, out successes, out error, out replySent);
                        if (!replySent)
                        {
                            if (failures == 0)
                            {
                                TargetReply(base.connectionToClient, "OVERWATCH#Done! The request affected " + successes + " player(s)!", true, true, "AdminTools");
                                break;
                            }
                            TargetReply(base.connectionToClient, "OVERWATCH#The proccess has occured an issue! Failures: " + failures + "\nLast error log:\n" + error, false, true, "AdminTools");
                        }
                    }
                    else
                    {
                        TargetReply(base.connectionToClient, query[0].ToUpper() + "#To run this program, type at least 2 arguments! (some parameters are missing)", false, true, "AdminTools");
                    }
                    break;
                case "GOD":
                    if (!CheckPermissions(query[0].ToUpper(), PlayerPermissions.PlayersManagement, string.Empty))
                    {
                        break;
                    }
                    if (query.Length >= 2)
                    {
                        if (query.Length == 2)
                        {
                            query = new string[3]
                            {
                            query[0],
                            query[1],
                            string.Empty
                            };
                        }
                        ServerLogs.AddLog(ServerLogs.Modules.Administrative, myNick + " ran the god command (new status: " + ((!(query[2] == string.Empty)) ? query[2] : "TOGGLE") + ") on " + query[1] + " players.", ServerLogs.ServerLogType.RemoteAdminActivity_GameChanging);
                        StandardizedQueryModel1("GOD", query[1], query[2], out failures, out successes, out error, out replySent);
                        if (!replySent)
                        {
                            if (failures == 0)
                            {
                                TargetReply(base.connectionToClient, "OVERWATCH#Done! The request affected " + successes + " player(s)!", true, true, "AdminTools");
                                break;
                            }
                            TargetReply(base.connectionToClient, "OVERWATCH#The proccess has occured an issue! Failures: " + failures + "\nLast error log:\n" + error, false, true, "AdminTools");
                        }
                    }
                    else
                    {
                        TargetReply(base.connectionToClient, query[0].ToUpper() + "#To run this program, type at least 2 arguments! (some parameters are missing)", false, true, "AdminTools");
                    }
                    break;
                case "MUTE":
                case "UNMUTE":
                case "IMUTE":
                case "IUNMUTE":
                    if (!CheckPermissions(query[0].ToUpper(), new PlayerPermissions[3]
                    {
                    PlayerPermissions.BanningUpToDay,
                    PlayerPermissions.LongTermBanning,
                    PlayerPermissions.PlayersManagement
                    }, string.Empty))
                    {
                        break;
                    }
                    if (query.Length == 2)
                    {
                        ServerLogs.AddLog(ServerLogs.Modules.Administrative, myNick + " ran the " + query[0].ToLower() + " command on " + query[1] + " players.", ServerLogs.ServerLogType.RemoteAdminActivity_Misc);
                        StandardizedQueryModel1(query[0].ToUpper(), query[1], null, out failures, out successes, out error, out replySent);
                        if (!replySent)
                        {
                            if (failures == 0)
                            {
                                TargetReply(base.connectionToClient, query[0].ToUpper() + "#Done! The request affected " + successes + " player(s)!", true, true, "PlayersManagement");
                            }
                            else
                            {
                                TargetReply(base.connectionToClient, query[0].ToUpper() + "#The proccess has occured an issue! Failures: " + failures + "\nLast error log:\n" + error, false, true, "PlayersManagement");
                            }
                        }
                    }
                    else
                    {
                        TargetReply(base.connectionToClient, query[0].ToUpper() + "#To run this program, type exactly 2 arguments!", false, true, "PlayersManagement");
                    }
                    break;
                case "INTERCOM-TIMEOUT":
                    if (CheckPermissions(query[0].ToUpper(), new PlayerPermissions[5]
                    {
                    PlayerPermissions.BanningUpToDay,
                    PlayerPermissions.LongTermBanning,
                    PlayerPermissions.RoundEvents,
                    PlayerPermissions.FacilityManagement,
                    PlayerPermissions.PlayersManagement
                    }, "ServerEvents"))
                    {
                        if (!Intercom.host.speaking)
                        {
                            TargetReply(base.connectionToClient, query[0].ToUpper() + "#Intercom is not being used.", false, true, "ServerEvents");
                            break;
                        }
                        if (Intercom.host.speechRemainingTime == -77f)
                        {
                            TargetReply(base.connectionToClient, query[0].ToUpper() + "#Intercom is being used by player with bypass mode enabled.", false, true, "ServerEvents");
                            break;
                        }
                        Intercom.host.speechRemainingTime = -1f;
                        ServerLogs.AddLog(ServerLogs.Modules.Administrative, myNick + " timeouted the intercom speaker.", ServerLogs.ServerLogType.RemoteAdminActivity_Misc);
                        TargetReply(base.connectionToClient, query[0].ToUpper() + "#Done! Intercom speaker timeouted.", true, true, "ServerEvents");
                    }
                    break;
                case "INTERCOM-RESET":
                    if (CheckPermissions(query[0].ToUpper(), new PlayerPermissions[3]
                    {
                    PlayerPermissions.RoundEvents,
                    PlayerPermissions.FacilityManagement,
                    PlayerPermissions.PlayersManagement
                    }, "ServerEvents"))
                    {
                        if (Intercom.host.remainingCooldown <= 0f)
                        {
                            TargetReply(base.connectionToClient, query[0].ToUpper() + "#Intercom is already ready to use.", false, true, "ServerEvents");
                            break;
                        }
                        Intercom.host.remainingCooldown = -1f;
                        ServerLogs.AddLog(ServerLogs.Modules.Administrative, myNick + " reset the intercom cooldown.", ServerLogs.ServerLogType.RemoteAdminActivity_Misc);
                        TargetReply(base.connectionToClient, query[0].ToUpper() + "#Done! Intercom cooldown reset.", true, true, "ServerEvents");
                    }
                    break;
                case "SPEAK":
                case "ICOM":
                    if (!CheckPermissions(query[0].ToUpper(), PlayerPermissions.FacilityManagement, "ServerEvents"))
                    {
                        break;
                    }
                    if (!Intercom.AdminSpeaking)
                    {
                        if (Intercom.host.speaking)
                        {
                            TargetReply(base.connectionToClient, query[0].ToUpper() + "#Intercom is being used by someone else.", false, true, "ServerEvents");
                            break;
                        }
                        Intercom.AdminSpeaking = true;
                        Intercom.host.RequestTransmission(GetComponent<Intercom>().gameObject);
                        ServerLogs.AddLog(ServerLogs.Modules.Administrative, myNick + " requested admin usage of the intercom.", ServerLogs.ServerLogType.RemoteAdminActivity_Misc);
                        TargetReply(base.connectionToClient, query[0].ToUpper() + "#Done! Intercom voice granted.", true, true, "ServerEvents");
                    }
                    else
                    {
                        Intercom.AdminSpeaking = false;
                        Intercom.host.RequestTransmission(null);
                        ServerLogs.AddLog(ServerLogs.Modules.Administrative, myNick + " ended admin intercom session.", ServerLogs.ServerLogType.RemoteAdminActivity_Misc);
                        TargetReply(base.connectionToClient, query[0].ToUpper() + "#Done! Admin intercom session ended.", true, true, "ServerEvents");
                    }
                    break;
                case "BM":
                case "BYPASS":
                    if (!CheckPermissions(query[0].ToUpper(), PlayerPermissions.FacilityManagement, "AdminTools"))
                    {
                        break;
                    }
                    if (query.Length >= 2)
                    {
                        if (query.Length == 2)
                        {
                            query = new string[3]
                            {
                            query[0],
                            query[1],
                            string.Empty
                            };
                        }
                        ServerLogs.AddLog(ServerLogs.Modules.Administrative, myNick + " ran the bypass mode command (new status: " + ((!(query[2] == string.Empty)) ? query[2] : "TOGGLE") + ") on " + query[1] + " players.", ServerLogs.ServerLogType.RemoteAdminActivity_GameChanging);
                        StandardizedQueryModel1("BYPASS", query[1], query[2], out failures, out successes, out error, out replySent);
                        if (!replySent)
                        {
                            if (failures == 0)
                            {
                                TargetReply(base.connectionToClient, "BYPASS#Done! The request affected " + successes + " player(s)!", true, true, "AdminTools");
                                break;
                            }
                            TargetReply(base.connectionToClient, "BYPASS#The proccess has occured an issue! Failures: " + failures + "\nLast error log:\n" + error, false, true, "AdminTools");
                        }
                    }
                    else
                    {
                        TargetReply(base.connectionToClient, query[0].ToUpper() + "#To run this program, type at least 2 arguments! (some parameters are missing)", false, true, "AdminTools");
                    }
                    break;
                case "BRING":
                    if (!CheckPermissions(query[0].ToUpper(), PlayerPermissions.PlayersManagement, "AdminTools"))
                    {
                        break;
                    }
                    if (query.Length == 2)
                    {
                        if (GetComponent<CharacterClassManager>().curClass == 2 || GetComponent<CharacterClassManager>().curClass < 0)
                        {
                            TargetReply(base.connectionToClient, "BRING#Command disabled when you are spectator!", false, true, "AdminTools");
                            break;
                        }
                        ServerLogs.AddLog(ServerLogs.Modules.Administrative, myNick + " ran the bring command on " + query[1] + " players.", ServerLogs.ServerLogType.RemoteAdminActivity_GameChanging);
                        StandardizedQueryModel1("BRING", query[1], string.Empty, out failures, out successes, out error, out replySent);
                        if (!replySent)
                        {
                            if (failures == 0)
                            {
                                TargetReply(base.connectionToClient, "BRING#Done! The request affected " + successes + " player(s)!", true, true, "AdminTools");
                                break;
                            }
                            TargetReply(base.connectionToClient, "BRING#The proccess has occured an issue! Failures: " + failures + "\nLast error log:\n" + error, false, true, "AdminTools");
                        }
                    }
                    else
                    {
                        TargetReply(base.connectionToClient, query[0].ToUpper() + "#To run this program, type exactly 2 arguments!", false, true, "AdminTools");
                    }
                    break;
                case "GOTO":
                    if (!CheckPermissions(query[0].ToUpper(), PlayerPermissions.PlayersManagement, "AdminTools"))
                    {
                        break;
                    }
                    if (query.Length == 2)
                    {
                        if (GetComponent<CharacterClassManager>().curClass == 2 || GetComponent<CharacterClassManager>().curClass < 0)
                        {
                            TargetReply(base.connectionToClient, "GOTO#Command disabled when you are spectator!", false, true, "AdminTools");
                            break;
                        }
                        int id = 0;
                        if (!int.TryParse(query[1], out id))
                        {
                            TargetReply(base.connectionToClient, "GOTO#Player ID must be an integer.", false, true, "AdminTools");
                            break;
                        }
                        if (query[1].Contains("."))
                        {
                            TargetReply(base.connectionToClient, "GOTO#Goto command requires exact one selected player.", false, true, "AdminTools");
                            break;
                        }
                        GameObject gameObject10 = PlayerManager.singleton.players.FirstOrDefault((GameObject pl) => pl.GetComponent<QueryProcessor>().PlayerId == id);
                        if (gameObject10 == null)
                        {
                            TargetReply(base.connectionToClient, "GOTO#Can't find requested player.", false, true, "AdminTools");
                            break;
                        }
                        if (gameObject10.GetComponent<CharacterClassManager>().curClass == 2 || gameObject10.GetComponent<CharacterClassManager>().curClass < -1)
                        {
                            TargetReply(base.connectionToClient, "GOTO#Requested player is a spectator!", false, true, "AdminTools");
                            break;
                        }
                        ServerLogs.AddLog(ServerLogs.Modules.Administrative, myNick + " ran the goto command on " + query[1] + " players.", ServerLogs.ServerLogType.RemoteAdminActivity_GameChanging);
                        GetComponent<PlyMovementSync>().SetPosition(gameObject10.GetComponent<PlyMovementSync>().CurrentPosition);
                        TargetReply(base.connectionToClient, "GOTO#Done!", true, true, "AdminTools");
                    }
                    else
                    {
                        TargetReply(base.connectionToClient, query[0].ToUpper() + "#To run this program, type exactly 2 arguments!", false, true, "AdminTools");
                    }
                    break;
                case "LD":
                case "LOCKDOWN":
                    {
                        if (!CheckPermissions(query[0].ToUpper(), PlayerPermissions.FacilityManagement, "AdminTools"))
                        {
                            break;
                        }
                        if (!Lockdown)
                        {
                            ServerLogs.AddLog(ServerLogs.Modules.Administrative, myNick + " enabled the lockdown.", ServerLogs.ServerLogType.RemoteAdminActivity_GameChanging);
                            Door[] array2 = UnityEngine.Object.FindObjectsOfType<Door>();
                            foreach (Door door in array2)
                            {
                                if (!door.Locked)
                                {
                                    door.lockdown = true;
                                    door.UpdateLock();
                                }
                            }
                            Lockdown = true;
                            TargetReply(base.connectionToClient, query[0] + "#Lockdown enabled!", true, true, "AdminTools");
                            break;
                        }
                        ServerLogs.AddLog(ServerLogs.Modules.Administrative, myNick + " disabled the lockdown.", ServerLogs.ServerLogType.RemoteAdminActivity_GameChanging);
                        Door[] array3 = UnityEngine.Object.FindObjectsOfType<Door>();
                        foreach (Door door2 in array3)
                        {
                            if (door2.lockdown)
                            {
                                door2.lockdown = false;
                                door2.UpdateLock();
                            }
                        }
                        Lockdown = false;
                        TargetReply(base.connectionToClient, query[0] + "#Lockdown disabled!", true, true, "AdminTools");
                        break;
                    }
                case "O":
                case "OPEN":
                    if (CheckPermissions(query[0].ToUpper(), PlayerPermissions.FacilityManagement, "DoorsManagement"))
                    {
                        if (query.Length != 2)
                        {
                            TargetReply(base.connectionToClient, query[0].ToUpper() + "#Syntax of this program: " + query[0].ToUpper() + " DoorName", false, true, string.Empty);
                        }
                        else
                        {
                            ProcessDoorQuery("OPEN", query[1]);
                        }
                    }
                    break;
                case "C":
                case "CLOSE":
                    if (CheckPermissions(query[0].ToUpper(), PlayerPermissions.FacilityManagement, "DoorsManagement"))
                    {
                        if (query.Length != 2)
                        {
                            TargetReply(base.connectionToClient, query[0].ToUpper() + "#Syntax of this program: " + query[0].ToUpper() + " DoorName", false, true, string.Empty);
                        }
                        else
                        {
                            ProcessDoorQuery("CLOSE", query[1]);
                        }
                    }
                    break;
                case "L":
                case "LOCK":
                    if (CheckPermissions(query[0].ToUpper(), PlayerPermissions.FacilityManagement, "DoorsManagement"))
                    {
                        if (query.Length != 2)
                        {
                            TargetReply(base.connectionToClient, query[0].ToUpper() + "#Syntax of this program: " + query[0].ToUpper() + " DoorName", false, true, string.Empty);
                        }
                        else
                        {
                            ProcessDoorQuery("LOCK", query[1]);
                        }
                    }
                    break;
                case "UL":
                case "UNLOCK":
                    if (CheckPermissions(query[0].ToUpper(), PlayerPermissions.FacilityManagement, "DoorsManagement"))
                    {
                        if (query.Length != 2)
                        {
                            TargetReply(base.connectionToClient, query[0].ToUpper() + "#Syntax of this program: " + query[0].ToUpper() + " DoorName", false, true, string.Empty);
                        }
                        else
                        {
                            ProcessDoorQuery("UNLOCK", query[1]);
                        }
                    }
                    break;
                case "DESTROY":
                    if (CheckPermissions(query[0].ToUpper(), PlayerPermissions.FacilityManagement, "DoorsManagement"))
                    {
                        if (query.Length != 2)
                        {
                            TargetReply(base.connectionToClient, query[0].ToUpper() + "#Syntax of this program: " + query[0].ToUpper() + " DoorName", false, true, string.Empty);
                        }
                        else
                        {
                            ProcessDoorQuery("DESTROY", query[1]);
                        }
                    }
                    break;
                case "DOORTP":
                case "DTP":
                    if (!CheckPermissions(query[0].ToUpper(), PlayerPermissions.PlayersManagement, "DoorsManagement"))
                    {
                        break;
                    }
                    if (query.Length != 3)
                    {
                        TargetReply(base.connectionToClient, query[0].ToUpper() + "#Syntax of this program: " + query[0].ToUpper() + " PlayerIDs DoorName", false, true, string.Empty);
                        break;
                    }
                    ServerLogs.AddLog(ServerLogs.Modules.Administrative, myNick + " ran the DoorTp command (Door: " + query[2] + ") on " + query[1] + " players.", ServerLogs.ServerLogType.RemoteAdminActivity_GameChanging);
                    StandardizedQueryModel1("DOORTP", query[1], query[2], out failures, out successes, out error, out replySent);
                    if (!replySent)
                    {
                        if (failures == 0)
                        {
                            TargetReply(base.connectionToClient, query[0] + "#Done! The request affected " + successes + " player(s)!", true, true, "DoorsManagement");
                        }
                        else
                        {
                            TargetReply(base.connectionToClient, query[0] + "#The proccess has occured an issue! Failures: " + failures + "\nLast error log:\n" + error, false, true, "DoorsManagement");
                        }
                    }
                    break;
                case "DL":
                case "DOORS":
                case "DOORLIST":
                    if (CheckPermissions(query[0].ToUpper(), PlayerPermissions.FacilityManagement, "AdminTools"))
                    {
                        string text16 = "List of named doors in the facility:\n";
                        Door[] source = UnityEngine.Object.FindObjectsOfType<Door>();
                        List<string> list2 = (from item in source
                                              where !string.IsNullOrEmpty(item.DoorName)
                                              select item.DoorName + " - " + ((!item.IsOpen) ? "<color=orange>CLOSED</color>" : "<color=green>OPENED</color>") + ((!item.Locked) ? string.Empty : " <color=red>[LOCKED]</color>") + ((!string.IsNullOrEmpty(item.permissionLevel)) ? " <color=blue>[CARD REQUIRED]</color>" : string.Empty)).ToList();
                        list2.Sort();
                        text16 += list2.Aggregate((string text20, string adding) => text20 + "\n" + adding);
                        TargetReply(base.connectionToClient, query[0] + "#" + text16, true, true, string.Empty);
                    }
                    break;
                case "GIVE":
                    if (!CheckPermissions(query[0].ToUpper(), PlayerPermissions.GivingItems, string.Empty))
                    {
                        break;
                    }
                    if (query.Length >= 3)
                    {
                        ServerLogs.AddLog(ServerLogs.Modules.Administrative, myNick + " ran the give command (ID: " + query[2] + ") on " + query[1] + " players.", ServerLogs.ServerLogType.RemoteAdminActivity_GameChanging);
                        StandardizedQueryModel1(query[0], query[1], query[2], out failures, out successes, out error, out replySent);
                        if (!replySent)
                        {
                            if (failures == 0)
                            {
                                TargetReply(base.connectionToClient, query[0] + "#Done! The request affected " + successes + " player(s)!", true, true, string.Empty);
                            }
                            else
                            {
                                TargetReply(base.connectionToClient, query[0] + "#The proccess has occured an issue! Failures: " + failures + "\nLast error log:\n" + error, false, true, string.Empty);
                            }
                        }
                    }
                    else
                    {
                        TargetReply(base.connectionToClient, query[0].ToUpper() + "#To run this program, type at least 3 arguments! (some parameters are missing)", false, true, string.Empty);
                    }
                    break;
                case "REQUEST_DATA":
                    if (query.Length >= 2)
                    {
                        switch (query[1].ToUpper())
                        {
                            case "PLAYER_LIST":
                                try
                                {
                                    bool gameplayData = CheckPermissions(query[0].ToUpper(), PlayerPermissions.GameplayData, string.Empty, false);
                                    bool isStaffRequest = q.ToUpper().Contains("STAFF");
                                    bool silent = query.Length >= 3 && query[2].ToUpper() == "SILENT";

                                    string resultText = "\n";

                                    foreach (var kvp in NetworkServer.connections)
                                    {
                                        NetworkConnectionToClient connection = kvp.Value;
                                        GameObject playerObj = GameConsole.Console.FindConnectedRoot(connection);
                                        if (playerObj == null)
                                            continue;

                                        var serverRoles = playerObj.GetComponent<ServerRoles>();
                                        var queryProcessor = playerObj.GetComponent<QueryProcessor>();
                                        var nickSync = playerObj.GetComponent<NicknameSync>();

                                        if (!isStaffRequest)
                                        {
                                            string prefix = string.Empty;
                                            try
                                            {
                                                if (serverRoles != null)
                                                {
                                                    if (serverRoles.RaEverywhere) prefix = "[~] ";
                                                    else if (serverRoles.Staff) prefix = "[@] ";
                                                    else if (serverRoles.RemoteAdmin) prefix = "[RA] ";
                                                }
                                            }
                                            catch
                                            {
                                            }

                                            string nicknameClean = nickSync?.myNick.Replace("\n", string.Empty).Replace("<", string.Empty).Replace(">", string.Empty) ?? "(no nick)";
                                            string overwatchTag = (serverRoles != null && serverRoles.OverwatchEnabled) ? "<OVRM>" : string.Empty;

                                            resultText += $"{prefix}({queryProcessor?.PlayerId ?? -1}) {nicknameClean}{overwatchTag}\n";
                                        }
                                        else
                                        {
                                            string playerIdStr = queryProcessor?.PlayerId.ToString() ?? "N/A";
                                            string nick = nickSync?.myNick ?? "(no nick)";
                                            resultText += $"{playerIdStr};{nick}\n";
                                        }
                                    }

                                    if (!isStaffRequest)
                                        TargetReply(base.connectionToClient, $"{query[0].ToUpper()}:PLAYER_LIST#{resultText}", true, !silent, string.Empty);
                                    else
                                        TargetReply(base.connectionToClient, $"StaffPlayerListReply#{resultText}", true, !silent, string.Empty);
                                }
                                catch (Exception ex)
                                {
                                    TargetReply(base.connectionToClient, $"{query[0].ToUpper()}:PLAYER_LIST#An unexpected problem has occurred!\nMessage: {ex.Message}\nStackTrace: {ex.StackTrace}\nAt: {ex.Source}", false, true, string.Empty);
                                    throw;
                                }
                                break;
                            case "PLAYER":
                            case "SHORT-PLAYER":
                                {
                                    if (query.Length < 3)
                                    {
                                        TargetReply(base.connectionToClient, query[0].ToUpper() + ":PLAYER#Please specify the PlayerId!", false, true, string.Empty);
                                        break;
                                    }

                                    if (query[1].ToUpper() == "PLAYER" && !GetComponent<ServerRoles>().Staff && !CheckPermissions(query[0].ToUpper(), new[] { PlayerPermissions.LongTermBanning, PlayerPermissions.BanningUpToDay }, string.Empty))
                                        break;

                                    try
                                    {
                                        GameObject target = null;
                                        NetworkConnection conn = null;
                                        string playerId = query[2].Split('.')[0];

                                        foreach (var connection in NetworkServer.connections.Values)
                                        {
                                            GameObject go = GameConsole.Console.FindConnectedRoot(connection);
                                            if (go != null && go.GetComponent<QueryProcessor>().PlayerId.ToString() == playerId)
                                            {
                                                target = go;
                                                conn = connection;
                                                break;
                                            }
                                        }

                                        if (target == null)
                                        {
                                            TargetReply(base.connectionToClient, query[0].ToUpper() + ":PLAYER#Player with id " + playerId + " not found!", false, true, string.Empty);
                                            break;
                                        }

                                        bool hasGameplayPerms = CheckPermissions(query[0].ToUpper(), PlayerPermissions.GameplayData, string.Empty, false);
                                        var ccm = target.GetComponent<CharacterClassManager>();
                                        var sr = target.GetComponent<ServerRoles>();
                                        var qp = target.GetComponent<QueryProcessor>();
                                        var nick = target.GetComponent<NicknameSync>().myNick;
                                        var stats = target.GetComponent<PlayerStats>();

                                        ServerLogs.AddLog(ServerLogs.Modules.DataAccess, $"{myNick} ({PlayerId}) accessed IP address of player {qp.PlayerId} ({nick}).", ServerLogs.ServerLogType.RemoteAdminActivity_GameChanging);

                                        string info = $"<color=white>Nickname: {nick}\nPlayer ID: {qp.PlayerId}\nIP: {(conn == null ? "null" : (query[1].ToUpper() == "PLAYER" ? conn.identity.connectionToClient.address : "[REDACTED]"))}\nSteam ID: {(string.IsNullOrEmpty(ccm.SteamId) ? "(none)" : ccm.SteamId)}\nServer role: {sr.GetColoredRoleString()}";

                                        if (!string.IsNullOrEmpty(sr.HiddenBadge)) info += $"\n<color=#DC143C>Hidden role: </color>{sr.HiddenBadge}";
                                        if (sr.RaEverywhere) info += "\nActive flag: <color=#BCC6CC>Studio GLOBAL Staff (management or security team)</color>";
                                        if (sr.Staff) info += "\nActive flag: Studio Staff";
                                        if (ccm.Muted) info += "\nActive flag: <color=#F70D1A>SERVER MUTED</color>";
                                        else if (ccm.IntercomMuted) info += "\nActive flag: <color=#F70D1A>INTERCOM MUTED</color>";
                                        if (ccm.GodMode) info += "\nActive flag: <color=#659EC7>GOD MODE</color>";
                                        if (sr.DoNotTrack) info += "\nActive flag: <color=#BFFF00>DO NOT TRACK</color>";
                                        if (sr.BypassMode) info += "\nActive flag: <color=#BFFF00>BYPASS MODE</color>";
                                        if (sr.RemoteAdmin) info += "\nActive flag: <color=#43C6DB>REMOTE ADMIN AUTHENTICATED</color>";
                                        if (sr.OverwatchEnabled) info += "\nActive flag: <color=#008080>OVERWATCH MODE</color>";

                                        if (hasGameplayPerms)
                                        {
                                            info += $"\nClass: {(ccm.curClass < 0 || ccm.curClass >= ccm.klasy.Length ? "None" : ccm.klasy[ccm.curClass].fullName)}";
                                            info += $"\nHP: {stats.HealthToString()}";
                                        }
                                        else
                                        {
                                            info += "\nClass: <color=#D4AF37>INSUFFICIENT PERMISSIONS</color>";
                                            info += "\nHP: <color=#D4AF37>INSUFFICIENT PERMISSIONS</color>";
                                            info += "\n<color=#D4AF37>* GameplayData permission required</color>";
                                        }

                                        info += "</color>";

                                        TargetReply(base.connectionToClient, query[0].ToUpper() + ":PLAYER#" + info, true, true, "PlayerInfo");
                                        TargetReply(base.connectionToClient, "PlayerInfoQR#" + (string.IsNullOrEmpty(ccm.SteamId) ? "(no SteamID64)" : ccm.SteamId), true, false, "PlayerInfo");
                                    }
                                    catch (Exception ex)
                                    {
                                        TargetReply(base.connectionToClient, query[0].ToUpper() + "#An unexpected problem has occurred!\nMessage: " + ex.Message + "\nStackTrace: " + ex.StackTrace + "\nAt: " + ex.Source, false, true, "PlayerInfo");
                                        throw;
                                    }
                                    break;
                                }
                            case "AUTH":
                                {
                                    if (!GetComponent<ServerRoles>().Staff && !CheckPermissions(query[0].ToUpper(), new[] { PlayerPermissions.LongTermBanning, PlayerPermissions.BanningUpToDay }, string.Empty))
                                        break;

                                    if (query.Length < 3)
                                    {
                                        TargetReply(base.connectionToClient, query[0].ToUpper() + ":PLAYER#Please specify the PlayerId!", false, true, string.Empty);
                                        break;
                                    }

                                    try
                                    {
                                        GameObject target = null;
                                        string playerId = query[2].Split('.')[0];

                                        foreach (var conn in NetworkServer.connections.Values)
                                        {
                                            GameObject go = GameConsole.Console.FindConnectedRoot(conn);
                                            if (go != null && go.GetComponent<QueryProcessor>().PlayerId.ToString() == playerId)
                                            {
                                                target = go;
                                                break;
                                            }
                                        }

                                        if (target == null)
                                        {
                                            TargetReply(base.connectionToClient, query[0].ToUpper() + ":PLAYER#Player with id " + playerId + " not found!", false, true, string.Empty);
                                            break;
                                        }

                                        var ccm = target.GetComponent<CharacterClassManager>();
                                        if (string.IsNullOrEmpty(ccm.AuthToken))
                                        {
                                            TargetReply(base.connectionToClient, query[0].ToUpper() + ":PLAYER#Can't obtain auth token. Is server using offline mode or you selected the host?", false, true, "PlayerInfo");
                                            break;
                                        }

                                        ServerLogs.AddLog(ServerLogs.Modules.DataAccess, $"{myNick} ({PlayerId}) accessed authentication token of player {target.GetComponent<QueryProcessor>().PlayerId} ({target.GetComponent<NicknameSync>().myNick}).", ServerLogs.ServerLogType.RemoteAdminActivity_GameChanging);

                                        string token = ccm.AuthToken;
                                        if (!q.ToUpper().Contains("STAFF"))
                                        {
                                            string reply = $"<color=white>Authentication token of player {target.GetComponent<NicknameSync>().myNick} ({target.GetComponent<QueryProcessor>().PlayerId}):\n{token}</color>";
                                            TargetReply(base.connectionToClient, query[0].ToUpper() + ":PLAYER#" + reply, true, true, "null");
                                            TargetReply(base.connectionToClient, "BigQR#" + token, true, false, "null");
                                        }
                                        else
                                        {
                                            TargetReply(base.connectionToClient, "StaffTokenReply#" + token, true, false, "null");
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        TargetReply(base.connectionToClient, query[0].ToUpper() + "#An unexpected problem has occurred!\nMessage: " + ex.Message + "\nStackTrace: " + ex.StackTrace + "\nAt: " + ex.Source, false, true, "PlayerInfo");
                                        throw;
                                    }
                                    break;
                                }
                        }
                    }
                    else
                    {
                        TargetReply(base.connectionToClient, query[0].ToUpper() + "#To run this program, type at least 2 arguments! (some parameters are missing)", false, true, "PlayerInfo");
                    }
                    break;
                case "CONTACT":
                    TargetReply(base.connectionToClient, query[0].ToUpper() + "#Contact email address: " + ConfigFile.ServerConfig.GetString("contact_email", string.Empty), false, true, string.Empty);
                    break;
                case "LOGOUT":
                    if (_roles.RemoteAdminMode == ServerRoles.AccessMode.PasswordOverride)
                    {
                        ServerLogs.AddLog(ServerLogs.Modules.Administrative, myNick + " logged out from the Remote Admin.", ServerLogs.ServerLogType.RemoteAdminActivity_Misc);
                        _roles.RemoteAdmin = false;
                        _roles.TargetCloseRemoteAdmin(base.connectionToClient);
                        if (!_roles.GlobalSet)
                        {
                            _roles.SetText(string.Empty);
                            _roles.SetColor("default");
                        }
                        PasswordTries = 0;
                        TargetReply(base.connectionToClient, query[0].ToUpper() + "#Logged out!", true, true, string.Empty);
                    }
                    else
                    {
                        TargetReply(base.connectionToClient, query[0].ToUpper() + "#You can't log out, when you are not using override password!", true, true, string.Empty);
                    }
                    break;
                case "SERVER_EVENT":
                    if (query.Length >= 2)
                    {
                        ServerLogs.AddLog(ServerLogs.Modules.Administrative, myNick + " forced a server event: " + query[1].ToUpper(), ServerLogs.ServerLogType.RemoteAdminActivity_GameChanging);
                        GameObject gameObject = GameObject.Find("Host");
                        MTFRespawn component = gameObject.GetComponent<MTFRespawn>();
                        AlphaWarheadController component2 = gameObject.GetComponent<AlphaWarheadController>();
                        bool flag = true;
                        switch (query[1].ToUpper())
                        {
                            case "FORCE_CI_RESPAWN":
                                if (!CheckPermissions(query[0].ToUpper(), PlayerPermissions.RespawnEvents, "ServerEvents"))
                                {
                                    return;
                                }
                                component.nextWaveIsCI = true;
                                component.timeToNextRespawn = 0.1f;
                                break;
                            case "FORCE_MTF_RESPAWN":
                                if (!CheckPermissions(query[0].ToUpper(), PlayerPermissions.RespawnEvents, "ServerEvents"))
                                {
                                    return;
                                }
                                component.nextWaveIsCI = false;
                                component.timeToNextRespawn = 0.1f;
                                break;
                            case "DETONATION_START":
                                if (!CheckPermissions(query[0].ToUpper(), PlayerPermissions.WarheadEvents, "ServerEvents"))
                                {
                                    return;
                                }
                                component2.InstantPrepare();
                                component2.StartDetonation();
                                break;
                            case "DETONATION_CANCEL":
                                if (!CheckPermissions(query[0].ToUpper(), PlayerPermissions.WarheadEvents, "ServerEvents"))
                                {
                                    return;
                                }
                                component2.CancelDetonation();
                                break;
                            case "DETONATION_INSTANT":
                                if (!CheckPermissions(query[0].ToUpper(), PlayerPermissions.WarheadEvents, "ServerEvents"))
                                {
                                    return;
                                }
                                component2.InstantPrepare();
                                component2.StartDetonation();
                                component2.timeToDetonation = 5f;
                                break;
                            case "TERMINATE_UNCONN":
                                {
                                    if (!CheckPermissions(query[0].ToUpper(), PlayerPermissions.RoundEvents, "ServerEvents"))
                                        return;
                                    foreach (var conn in NetworkServer.connections.Values)
                                    {
                                        if (conn is NetworkConnectionToClient validConn
                                            && GameConsole.Console.FindConnectedRoot(validConn) == null
                                            && validConn != null
                                            && validConn.isReady)
                                        {
                                            CustomNetworkManager.Instance.OnServerConnect(validConn);
                                        }
                                    }

                                    break;
                                }
                            case "ROUND_RESTART":
                                {
                                    if (!CheckPermissions(query[0].ToUpper(), PlayerPermissions.RoundEvents, "ServerEvents"))
                                    {
                                        return;
                                    }
                                    GameObject[] array = GameObject.FindGameObjectsWithTag("Player");
                                    foreach (GameObject gameObject2 in array)
                                    {
                                        PlayerStats component3 = gameObject2.GetComponent<PlayerStats>();
                                        if (component3.isLocalPlayer && component3.isServer)
                                        {
                                            component3.Roundrestart();
                                        }
                                    }
                                    break;
                                }
                            default:
                                flag = false;
                                break;
                        }
                        if (flag)
                        {
                            TargetReply(base.connectionToClient, query[0].ToUpper() + "#Started event: " + query[1].ToUpper(), true, true, "ServerEvents");
                        }
                        else
                        {
                            TargetReply(base.connectionToClient, query[0].ToUpper() + "#Incorrect event! (Doesn't exist)", false, true, "ServerEvents");
                        }
                    }
                    else
                    {
                        TargetReply(base.connectionToClient, query[0].ToUpper() + "#To run this program, type at least 2 arguments! (some parameters are missing)", false, true, string.Empty);
                    }
                    break;
                case "HIDETAG":
                    _roles.HiddenBadge = _roles.MyText;
                    _roles.SetBadgeUpdate(string.Empty);
                    _roles.SetText(string.Empty);
                    _roles.SetColor("default");
                    _roles.GlobalSet = false;
                    _roles.RefreshHiddenTag();
                    TargetReply(base.connectionToClient, query[0].ToUpper() + "#Tag hidden!", true, true, string.Empty);
                    PasswordTries = 0;
                    break;
                case "SHOWTAG":
                    _roles.HiddenBadge = string.Empty;
                    _roles.RpcResetFixed();
                    _roles.RefreshPermissions(true);
                    TargetReply(base.connectionToClient, query[0].ToUpper() + "#Local tag refreshed!", true, true, string.Empty);
                    break;
                case "GTAG":
                case "GLOBALTAG":
                    if (string.IsNullOrEmpty(_roles.PrevBadge))
                    {
                        TargetReply(base.connectionToClient, query[0].ToUpper() + "#You don't have global tag.", false, true, string.Empty);
                        break;
                    }
                    _roles.HiddenBadge = string.Empty;
                    _roles.RpcResetFixed();
                    _roles.SetBadgeUpdate(_roles.PrevBadge);
                    _roles.GlobalSet = true;
                    TargetReply(base.connectionToClient, query[0].ToUpper() + "#Global tag refreshed!", true, true, string.Empty);
                    break;
                case "PERM":
                    {
                        ulong permissions = GetComponent<ServerRoles>().Permissions;
                        string text = "Your permissions:";
                        List<string> allPermissions = ServerStatic.PermissionsHandler.GetAllPermissions();
                        foreach (string item5 in allPermissions)
                        {
                            string text2 = ((!ServerStatic.PermissionsHandler.IsRaPermitted(ServerStatic.PermissionsHandler.GetPermissionValue(item5))) ? string.Empty : "*");
                            string text3 = text;
                            text = text3 + "\n" + item5 + text2 + " (" + ServerStatic.PermissionsHandler.GetPermissionValue(item5) + "): " + ((!ServerStatic.PermissionsHandler.IsPermitted(permissions, item5)) ? "NO" : "YES");
                        }
                        TargetReply(base.connectionToClient, query[0].ToUpper() + "#" + text, true, true, string.Empty);
                        break;
                    }
                default:
                    TargetReply(base.connectionToClient, "SYSTEM#Unknown command!", false, true, string.Empty);
                    break;
            }
        }

        private void ProcessDoorQuery(string command, string door)
        {
            if (!CheckPermissions(command.ToUpper(), PlayerPermissions.FacilityManagement, string.Empty))
            {
                return;
            }
            if (string.IsNullOrEmpty(door))
            {
                TargetReply(base.connectionToClient, command + "#Please select door first.", false, true, "DoorsManagement");
                return;
            }
            bool flag = false;
            door = door.ToUpper();
            int num = 0;
            switch (command)
            {
                case "OPEN":
                    num = 1;
                    break;
                case "LOCK":
                    num = 2;
                    break;
                case "UNLOCK":
                    num = 3;
                    break;
                case "DESTROY":
                    num = 4;
                    break;
                default:
                    num = 0;
                    break;
            }
            Door[] array = UnityEngine.Object.FindObjectsOfType<Door>();
            foreach (Door door2 in array)
            {
                if (!(door2.DoorName.ToUpper() != door) || (!(door != "**") && !(door2.permissionLevel == "UNACCESSIBLE")) || (!(door != "!*") && string.IsNullOrEmpty(door2.DoorName)) || (!(door != "*") && !string.IsNullOrEmpty(door2.DoorName) && !(door2.permissionLevel == "UNACCESSIBLE")))
                {
                    switch (num)
                    {
                        case 0:
                            door2.SetStateWithSound(false);
                            break;
                        case 1:
                            door2.SetStateWithSound(true);
                            break;
                        case 2:
                            door2.commandlock = true;
                            door2.UpdateLock();
                            break;
                        case 3:
                            door2.commandlock = false;
                            door2.UpdateLock();
                            break;
                        case 4:
                            door2.SetDestroyed(true);
                            break;
                    }
                    flag = true;
                }
            }
            TargetReply(base.connectionToClient, command + "#" + ((!flag) ? ("Can't find door " + door + ".") : ("Door " + door + " " + command.ToLower() + "ed.")), flag, true, "DoorsManagement");
            if (flag)
            {
                ServerLogs.AddLog(ServerLogs.Modules.Administrative, GetComponent<NicknameSync>().myNick + " " + command.ToLower() + ((!command.ToLower().EndsWith("e")) ? "ed" : "d") + " door " + door + ".", ServerLogs.ServerLogType.RemoteAdminActivity_GameChanging);
            }
        }

        private void StandardizedQueryModel1(string programName, string playerIds, string xValue, out int failures, out int successes, out string error, out bool replySent)
        {
            error = string.Empty;
            failures = 0;
            successes = 0;
            replySent = false;
            programName = programName.ToUpper();
            if (!int.TryParse(xValue, out int result) && !programName.StartsWith("SLML"))
            {
                switch (programName)
                {
                    case "SETGROUP":
                    case "OVERWATCH":
                    case "BYPASS":
                    case "HEAL":
                    case "GOD":
                    case "BRING":
                    case "MUTE":
                    case "UNMUTE":
                    case "IMUTE":
                    case "IUNMUTE":
                    case "DOORTP":
                        break;
                    default:
                        replySent = true;
                        TargetReply(base.connectionToClient, programName + "#The third parameter has to be an integer!", false, true, string.Empty);
                        return;
                }
            }
            List<int> list = new List<int>();
            try
            {
                string[] source = playerIds.Split('.');
                list.AddRange(source.Where((string item) => !string.IsNullOrEmpty(item)).Select(int.Parse));
                UserGroup userGroup = null;
                Vector3 vector = Vector3.down;
                if (programName == "BAN")
                {
                    replySent = true;
                    if (result < 0)
                    {
                        result = 0;
                    }
                    if ((result == 0 && !CheckPermissions(programName, new PlayerPermissions[3]
                    {
                        PlayerPermissions.KickingAndShortTermBanning,
                        PlayerPermissions.BanningUpToDay,
                        PlayerPermissions.LongTermBanning
                    }, string.Empty)) || (result > 0 && result <= 60 && !CheckPermissions(programName, PlayerPermissions.KickingAndShortTermBanning, string.Empty)) || (result > 60 && result <= 1440 && !CheckPermissions(programName, PlayerPermissions.BanningUpToDay, string.Empty)) || (result > 1440 && !CheckPermissions(programName, PlayerPermissions.LongTermBanning, string.Empty)))
                    {
                        return;
                    }
                    replySent = false;
                }
                else if (programName.StartsWith("SLML"))
                {
                    MarkupTransceiver markupTransceiver = UnityEngine.Object.FindObjectOfType<MarkupTransceiver>();
                    if (programName.Contains("_STYLE"))
                    {
                        markupTransceiver.RequestStyleDownload(xValue, list.ToArray());
                    }
                    else if (programName.Contains("_TAG"))
                    {
                        markupTransceiver.Transmit(xValue, list.ToArray());
                    }
                }
                else
                {
                    switch (programName)
                    {
                        case "SETGROUP":
                            if (xValue != "-1")
                            {
                                userGroup = ServerStatic.PermissionsHandler.GetGroup(xValue);
                                if (userGroup == null)
                                {
                                    replySent = true;
                                    TargetReply(base.connectionToClient, programName + "#Requested group doesn't exist! Use group \"-1\" to remove user group.", false, true, string.Empty);
                                    return;
                                }
                            }
                            break;
                        case "DOORTP":
                            {
                                xValue = xValue.ToUpper();
                                Door door = UnityEngine.Object.FindObjectsOfType<Door>().FirstOrDefault((Door dr) => dr.DoorName.ToUpper() == xValue);
                                if (door == null)
                                {
                                    replySent = true;
                                    TargetReply(base.connectionToClient, programName + "#Can't find door " + xValue + ".", false, true, "DoorsManagement");
                                    return;
                                }
                                vector = door.transform.position;
                                vector.y += 2.5f;
                                for (int num = 0; num < 21; num++)
                                {
                                    if (num == 0)
                                    {
                                        vector.x += 1.5f;
                                    }
                                    else if (num < 3)
                                    {
                                        vector.x += 1f;
                                    }
                                    else if (num == 4)
                                    {
                                        vector = door.transform.position;
                                        vector.y += 2.5f;
                                        vector.z += 1.5f;
                                    }
                                    else if (num < 10 && num % 2 == 0)
                                    {
                                        vector.z += 1f;
                                    }
                                    else if (num < 10)
                                    {
                                        vector.x += 1f;
                                    }
                                    else if (num == 10)
                                    {
                                        vector = door.transform.position;
                                        vector.y += 2.5f;
                                        vector.x -= 1.5f;
                                    }
                                    else if (num < 13)
                                    {
                                        vector.x -= 1f;
                                    }
                                    else if (num == 14)
                                    {
                                        vector = door.transform.position;
                                        vector.y += 2.5f;
                                        vector.z -= 1.5f;
                                    }
                                    else if (num % 2 == 0)
                                    {
                                        vector.z -= 1f;
                                    }
                                    else
                                    {
                                        vector.x -= 1f;
                                    }
                                    if (FallDamage.CheckUnsafePosition(vector))
                                    {
                                        break;
                                    }
                                    if (num == 20)
                                    {
                                        vector = Vector3.zero;
                                    }
                                }
                                if (vector == Vector3.zero)
                                {
                                    replySent = true;
                                    TargetReply(base.connectionToClient, programName + "#Can't find safe place to teleport to door " + xValue + ".", false, true, "DoorsManagement");
                                    return;
                                }
                                break;
                            }
                    }
                }
                bool isVerified = ServerStatic.PermissionsHandler.IsVerified;
                string myNick = GetComponent<NicknameSync>().myNick;
                Vector3 position = GetComponent<PlyMovementSync>().CurrentPosition;
                foreach (int item in list)
                {
                    try
                    {
                        GameObject[] players = PlayerManager.singleton.players;
                        foreach (GameObject gameObject in players)
                        {
                            if (item != gameObject.GetComponent<QueryProcessor>().PlayerId)
                            {
                                continue;
                            }
                            if (programName != null)
                            {
                                if (_003C_003Ef__switch_0024map5 == null)
                                {
                                    Dictionary<string, int> dictionary = new Dictionary<string, int>(19);
                                    dictionary.Add("BAN", 0);
                                    dictionary.Add("GBAN-KICK", 1);
                                    dictionary.Add("FC", 2);
                                    dictionary.Add("FORCECLASS", 2);
                                    dictionary.Add("ROLE", 3);
                                    dictionary.Add("GIVE", 4);
                                    dictionary.Add("SETGROUP", 5);
                                    dictionary.Add("HEAL", 6);
                                    dictionary.Add("HP", 7);
                                    dictionary.Add("SETHP", 7);
                                    dictionary.Add("MUTE", 8);
                                    dictionary.Add("UNMUTE", 9);
                                    dictionary.Add("IMUTE", 10);
                                    dictionary.Add("IUNMUTE", 11);
                                    dictionary.Add("DOORTP", 12);
                                    dictionary.Add("BRING", 13);
                                    dictionary.Add("OVERWATCH", 14);
                                    dictionary.Add("GOD", 15);
                                    dictionary.Add("BYPASS", 16);
                                    _003C_003Ef__switch_0024map5 = dictionary;
                                }
                                int value;
                                if (_003C_003Ef__switch_0024map5.TryGetValue(programName, out value))
                                {
                                    switch (value)
                                    {
                                        case 0:
                                            if (isVerified && gameObject.GetComponent<ServerRoles>().BypassStaff)
                                            {
                                                GetComponent<BanPlayer>().BanUser(gameObject, 0, string.Empty, myNick);
                                                break;
                                            }
                                            if (result == 0 && ConfigFile.ServerConfig.GetBool("broadcast_kicks"))
                                            {
                                                GetComponent<Broadcast>().RpcAddElement(ConfigFile.ServerConfig.GetString("broadcast_kick_text", "%nick% has been kicked from this server.").Replace("%nick%", gameObject.GetComponent<NicknameSync>().myNick), (uint)ConfigFile.ServerConfig.GetInt("broadcast_kick_duration", 5), false);
                                            }
                                            else if (result != 0 && ConfigFile.ServerConfig.GetBool("broadcast_bans", true))
                                            {
                                                GetComponent<Broadcast>().RpcAddElement(ConfigFile.ServerConfig.GetString("broadcast_ban_text", "%nick% has been banned from this server.").Replace("%nick%", gameObject.GetComponent<NicknameSync>().myNick), (uint)ConfigFile.ServerConfig.GetInt("broadcast_ban_duration", 5), false);
                                            }
                                            GetComponent<BanPlayer>().BanUser(gameObject, result, string.Empty, myNick);
                                            break;
                                        case 1:
                                            GetComponent<BanPlayer>().BanUser(gameObject, 0, string.Empty, myNick);
                                            break;
                                        case 2:
                                            GetComponent<CharacterClassManager>().SetPlayersClass(result, gameObject);
                                            break;
                                        case 3:
                                            GetComponent<CharacterClassManager>().SetPlayersClass(result, gameObject, true);
                                            break;
                                        case 4:
                                            gameObject.GetComponent<Inventory>().AddNewItem(result);
                                            break;
                                        case 5:
                                            gameObject.GetComponent<ServerRoles>().SetGroup(userGroup, false, true);
                                            break;
                                        case 6:
                                            {
                                                PlayerStats component = gameObject.GetComponent<PlayerStats>();
                                                if (xValue != null && result > 0)
                                                {
                                                    component.HealHPAmount(result);
                                                }
                                                else
                                                {
                                                    component.SetHPAmount(component.ccm.klasy[component.ccm.curClass].maxHP);
                                                }
                                                break;
                                            }
                                        case 7:
                                            gameObject.GetComponent<PlayerStats>().SetHPAmount(result);
                                            break;
                                        case 8:
                                            MuteHandler.IssuePersistantMute(gameObject.GetComponent<CharacterClassManager>().SteamId);
                                            gameObject.GetComponent<CharacterClassManager>().Muted = true;
                                            break;
                                        case 9:
                                            MuteHandler.RevokePersistantMute(gameObject.GetComponent<CharacterClassManager>().SteamId);
                                            gameObject.GetComponent<CharacterClassManager>().Muted = false;
                                            break;
                                        case 10:
                                            MuteHandler.IssuePersistantMute("ICOM-" + gameObject.GetComponent<CharacterClassManager>().SteamId);
                                            gameObject.GetComponent<CharacterClassManager>().IntercomMuted = true;
                                            break;
                                        case 11:
                                            MuteHandler.RevokePersistantMute("ICOM-" + gameObject.GetComponent<CharacterClassManager>().SteamId);
                                            gameObject.GetComponent<CharacterClassManager>().IntercomMuted = false;
                                            break;
                                        case 12:
                                            gameObject.GetComponent<PlyMovementSync>().SetPosition(vector);
                                            break;
                                        case 13:
                                            if (gameObject.GetComponent<CharacterClassManager>().curClass == 2 || gameObject.GetComponent<CharacterClassManager>().curClass == -1)
                                            {
                                                failures++;
                                                continue;
                                            }
                                            gameObject.GetComponent<PlyMovementSync>().SetPosition(position);
                                            break;
                                        case 14:
                                            if (string.IsNullOrEmpty(xValue))
                                            {
                                                gameObject.GetComponent<ServerRoles>().CmdToggleOverwatch();
                                                break;
                                            }
                                            if (xValue == "1" || xValue.ToLower() == "true" || xValue.ToLower() == "enable" || xValue.ToLower() == "on")
                                            {
                                                gameObject.GetComponent<ServerRoles>().CmdSetOverwatchStatus(true);
                                                break;
                                            }
                                            if (xValue == "0" || xValue.ToLower() == "false" || xValue.ToLower() == "disable" || xValue.ToLower() == "off")
                                            {
                                                gameObject.GetComponent<ServerRoles>().CmdSetOverwatchStatus(false);
                                                break;
                                            }
                                            replySent = true;
                                            TargetReply(base.connectionToClient, programName + "#Invalid option " + xValue + " - leave null for toggle or use 1/0, true/false, enable/disable or on/off.", false, true, "AdminTools");
                                            return;
                                        case 15:
                                            if (string.IsNullOrEmpty(xValue))
                                            {
                                                gameObject.GetComponent<CharacterClassManager>().GodMode = !gameObject.GetComponent<CharacterClassManager>().GodMode;
                                                break;
                                            }
                                            if (xValue == "1" || xValue.ToLower() == "true" || xValue.ToLower() == "enable" || xValue.ToLower() == "on")
                                            {
                                                gameObject.GetComponent<CharacterClassManager>().GodMode = true;
                                                break;
                                            }
                                            if (xValue == "0" || xValue.ToLower() == "false" || xValue.ToLower() == "disable" || xValue.ToLower() == "off")
                                            {
                                                gameObject.GetComponent<CharacterClassManager>().GodMode = false;
                                                break;
                                            }
                                            replySent = true;
                                            TargetReply(base.connectionToClient, programName + "#Invalid option " + xValue + " - leave null for toggle or use 1/0, true/false, enable/disable or on/off.", false, true, "AdminTools");
                                            return;
                                        case 16:
                                            if (string.IsNullOrEmpty(xValue))
                                            {
                                                gameObject.GetComponent<ServerRoles>().BypassMode = !gameObject.GetComponent<ServerRoles>().BypassMode;
                                                break;
                                            }
                                            if (xValue == "1" || xValue.ToLower() == "true" || xValue.ToLower() == "enable" || xValue.ToLower() == "on")
                                            {
                                                gameObject.GetComponent<ServerRoles>().BypassMode = true;
                                                break;
                                            }
                                            if (xValue == "0" || xValue.ToLower() == "false" || xValue.ToLower() == "disable" || xValue.ToLower() == "off")
                                            {
                                                gameObject.GetComponent<ServerRoles>().BypassMode = false;
                                                break;
                                            }
                                            replySent = true;
                                            TargetReply(base.connectionToClient, programName + "#Invalid option " + xValue + " - leave null for toggle or use 1/0, true/false, enable/disable or on/off.", false, true, "AdminTools");
                                            return;
                                    }
                                }
                            }
                            successes++;
                        }
                    }
                    catch (Exception ex)
                    {
                        failures++;
                        error = ex.Message + "\nStackTrace:\n" + ex.StackTrace;
                    }
                }
            }
            catch (Exception ex2)
            {
                replySent = true;
                TargetReply(base.connectionToClient, programName + "#An unexpected problem has occurred!\nMessage: " + ex2.Message + "\nStackTrace: " + ex2.StackTrace + "\nAt: " + ex2.Source + "\nMost likely the PlayerId array was not in the correct format.", false, true, string.Empty);
                throw;
            }
        }

        internal void ProcessGameConsoleQuery(string query, bool encrypted)
        {
            GCT.SendToClient(base.connectionToClient, "Command not found.", "red");
        }

        internal bool CheckPermissions(string queryZero, PlayerPermissions[] perm, string replyScreen = "", bool reply = true)
        {
            if (ServerStatic.IsDedicated && base.isLocalPlayer && base.isServer)
            {
                return true;
            }
            if (ServerStatic.PermissionsHandler.IsPermitted(GetComponent<ServerRoles>().Permissions, perm))
            {
                return true;
            }
            if (!reply)
            {
                return false;
            }
            string text = perm.Aggregate(string.Empty, (string current, PlayerPermissions p) => current + "\n- " + p);
            text.Remove(text.Length - 3);
            TargetReply(base.connectionToClient, queryZero + "#You don't have permissions to execute this command.\nYou need at least one of following permissions: " + text, false, true, replyScreen);
            return false;
        }

        internal bool CheckPermissions(string queryZero, PlayerPermissions perm, string replyScreen = "", bool reply = true)
        {
            if (ServerStatic.IsDedicated && base.isLocalPlayer && base.isServer)
            {
                return true;
            }
            if (ServerStatic.PermissionsHandler.IsPermitted(GetComponent<ServerRoles>().Permissions, perm))
            {
                return true;
            }
            if (reply)
            {
                TargetReply(base.connectionToClient, queryZero + "#You don't have permissions to execute this command.\nMissing permission: " + perm, false, true, replyScreen);
            }
            return false;
        }

        public bool VerifyRequestSignature(string message, int counter, byte[] signature, bool validateCounter = true)
        {
            return (GetComponent<ServerRoles>().RemoteAdminMode != ServerRoles.AccessMode.PasswordOverride) ? VerifyEcdsaSignature(message, counter, signature, validateCounter) : VerifyHmacSignature(message, counter, signature, validateCounter);
        }

        public byte[] SignRequest(string message, int counter = -2)
        {
            return (GetComponent<ServerRoles>().RemoteAdminMode != ServerRoles.AccessMode.PasswordOverride) ? EcdsaSign(message, counter) : HmacSign(message, counter);
        }

        public bool VerifyHmacSignature(string message, int counter, byte[] signature, bool validateCounter = true)
        {
            if (counter <= _signaturesCounter)
            {
                if (validateCounter)
                {
                    return false;
                }
            }
            else
            {
                _signaturesCounter = counter;
            }
            return OverridePasswordEnabled && Sha.Sha512Hmac(Utf8.GetBytes(message + ":[:COUNTER:]:" + counter + ":[:SALT:]:" + ServerRandom), _key).SequenceEqual(signature);
        }

        public bool VerifyEcdsaSignature(string message, int counter, byte[] signature, bool validateCounter = true)
        {
            if (counter <= _signaturesCounter)
            {
                if (validateCounter)
                {
                    return false;
                }
            }
            else
            {
                _signaturesCounter = counter;
            }
            return ECDSA.VerifyBytes(message + ":[:COUNTER:]:" + counter + ":[:SALT:]:" + ServerRandom, signature, GetComponent<ServerRoles>().PublicKey);
        }

        public byte[] EcdsaSign(string message, int counter = -2)
        {
            if (counter == -2)
            {
                counter = SignaturesCounter;
            }
            return ECDSA.SignBytes(message + ":[:COUNTER:]:" + counter + ":[:SALT:]:" + ServerRandom, GameConsole.Console.SessionKeys.Private);
        }

        public byte[] HmacSign(string message, int counter = -2)
        {
            if (counter == -2)
            {
                counter = SignaturesCounter;
            }
            return Sha.Sha512Hmac(Utf8.GetBytes(message + ":[:COUNTER:]:" + counter + ":[:SALT:]:" + ServerRandom), Key);
        }

        public static byte[] DerivePassword(string password, byte[] serversalt, byte[] clientsalt)
        {
            byte[] salt = Sha.Sha512(Convert.ToBase64String(serversalt) + Convert.ToBase64String(clientsalt));
            return PBKDF2.Pbkdf2HashBytes(password, salt, 250, 512);
        }

        internal void RequestGlobalBan(string key, int keytype)
        {
            _toBan = key;
            _toBanType = keytype;
            CmdSendQuery("REQUEST_DATA PLAYER_LIST STAFF");
        }

        internal void StaffPlayerListResponse(string data)
        {
            if (string.IsNullOrEmpty(_toBan) || !string.IsNullOrEmpty(_toBanNick))
            {
                return;
            }
            string[] array = data.Split('\n');
            string text = "-1";
            string text2 = string.Empty;
            string[] array2 = array;
            foreach (string text3 in array2)
            {
                try
                {
                    int num = text3.IndexOf(";", StringComparison.Ordinal);
                    if (num != -1)
                    {
                        string text4 = text3.Substring(0, num);
                        string text5 = text3.Substring(num + 1);
                        if (_toBanType == 0 && text4 == _toBan)
                        {
                            text = text4;
                            text2 = text5;
                            break;
                        }
                        if (_toBanType == 1 && string.Equals(text5, _toBan, StringComparison.CurrentCultureIgnoreCase))
                        {
                            text = text4;
                            text2 = text5;
                            break;
                        }
                    }
                }
                catch (Exception ex)
                {
                    GameConsole.Console.singleton.AddLog("Error while processing online list for global banning: " + ex.GetType().FullName, Color.red);
                }
            }
            if (text == "-1")
            {
                GameConsole.Console.singleton.AddLog("Requested player can't be found!", Color.red);
            }
            else
            {
                GameConsole.Console.singleton.AddLog("Requesting authentication token of player " + text2 + "(" + text + ").", Color.cyan);
                _toBan = text;
                _toBanNick = text2;
                CmdSendQuery("REQUEST_DATA AUTH " + text + " STAFF");
            }
            _toBanType = 0;
        }

        internal void StaffAuthTokenResponse(string auth, bool secure)
        {
            if (string.IsNullOrEmpty(_toBan) || string.IsNullOrEmpty(_toBanNick))
            {
                return;
            }
            string text = CentralAuth.ValidateForGlobalBanning(auth, _toBanNick);
            if (text == "-1")
            {
                GameConsole.Console.singleton.AddLog("Aborting global banning....", Color.red);
                _toBan = string.Empty;
                _toBanNick = string.Empty;
                _toBanSteamId = string.Empty;
                _toBanType = 0;
                return;
            }
            _toBanSteamId = text;
            GameConsole.Console.singleton.AddLog("==== GLOBAL BANNING FINAL STEP ====", Color.cyan);
            if (secure)
            {
                GameConsole.Console.singleton.AddLog("Token obtained over encrypted connection.", Color.cyan);
            }
            else
            {
                GameConsole.Console.singleton.AddLog("Token obtained over **UNENCRYPTED** connection.", Color.yellow);
            }
            GameConsole.Console.singleton.AddLog("Nick: " + _toBanNick, Color.cyan);
            GameConsole.Console.singleton.AddLog("ID on this server: " + _toBan, Color.cyan);
            GameConsole.Console.singleton.AddLog("SteamID64: " + _toBanSteamId, Color.cyan);
            GameConsole.Console.singleton.AddLog(string.Empty, Color.cyan);
            GameConsole.Console.singleton.AddLog("To confirm ban please execute \"CONFIRM\" command.", Color.cyan);
            GameConsole.Console.singleton.AddLog("==== GLOBAL BANNING FINAL STEP ====", Color.cyan);
            _toBanNick = string.Empty;
        }

        internal void ConfirmGlobalBanning()
        {
            StartCoroutine(IssueGlobalBan());
        }

        private IEnumerator IssueGlobalBan()
        {
            if (string.IsNullOrEmpty(_toBanSteamId))
            {
                GameConsole.Console.singleton.AddLog("You don't have any pending global ban request to confirm.", Color.yellow);
                yield break;
            }
            GameConsole.Console.singleton.AddLog("Issuing global ban for " + _toBanSteamId, Color.cyan);
            WWWForm form = new WWWForm();
            form.AddField("token", FileManager.ReadAllLines(FileManager.GetAppFolder(ServerStatic.ShareNonConfigs) + "StaffAPI.txt")[0]);
            form.AddField("action", "ban");
            form.AddField("steamid", _toBanSteamId);
            using (WWW www = new WWW(CentralServer.MasterUrl + "globalbanning.php", form))
            {
                yield return www;
                if (!string.IsNullOrEmpty(www.error))
                {
                    GameConsole.Console.singleton.AddLog("Error during global ban issuance: " + www.error, Color.red);
                }
                else if (www.text == "Banned")
                {
                    GameConsole.Console.singleton.AddLog("Global ban issued, kicking player from server...", Color.cyan);
                    CmdSendQuery("GBAN-KICK " + _toBan);
                    GameConsole.Console.singleton.AddLog("==== GLOBAL BANNING CONFIRMATION ====", Color.green);
                    GameConsole.Console.singleton.AddLog("ID on this server: " + _toBan, Color.green);
                    GameConsole.Console.singleton.AddLog("SteamID64: " + _toBanSteamId, Color.green);
                    GameConsole.Console.singleton.AddLog(string.Empty, Color.green);
                    GameConsole.Console.singleton.AddLog("Player has been globally banned.", Color.green);
                    GameConsole.Console.singleton.AddLog("Request to kick this player has been sent to game server.", Color.green);
                    GameConsole.Console.singleton.AddLog("==== GLOBAL BANNING CONFIRMATION ====", Color.green);
                    _toBanSteamId = string.Empty;
                    _toBan = string.Empty;
                    _toBanNick = string.Empty;
                    _toBanType = 0;
                }
                else
                {
                    GameConsole.Console.singleton.AddLog("Server error during global ban issuance: " + www.text, Color.red);
                }
            }
            yield return null;
        }

        [TargetRpc(channel = 2)]
        public void TargetSyncGameplayData(NetworkConnection conn, bool gd)
        {
            _gameplayData = gd;
        }

        private void OnDestroy()
        {
            if (!NetworkServer.active)
                return;

            if (connectionToClient != null && connectionToClient.isReady)
            {
                CustomNetworkManager.Instance.OnServerConnect(connectionToClient);
            }

            if (ServerLogs.singleton != null)
            {
                var characterClassManager = GetComponent<CharacterClassManager>();
                var nicknameSync = GetComponent<NicknameSync>();

                string steamId = !string.IsNullOrEmpty(characterClassManager?.SteamId)
                    ? characterClassManager.SteamId
                    : "(unavailable)";

                string nickname = nicknameSync != null
                    ? nicknameSync.myNick
                    : "(no nickname)";

                string className = characterClassManager != null && characterClassManager.curClass >= 0 &&
                                   characterClassManager.curClass < characterClassManager.klasy.Length
                    ? characterClassManager.klasy[characterClassManager.curClass].fullName
                    : "NOT SPAWNED";

                string log = $"Player ID {PlayerId} disconnected from IP {ipAddress} with SteamID {steamId} and nickname {nickname}. His last class was {characterClassManager?.curClass} ({className}).";

                ServerLogs.AddLog(ServerLogs.Modules.Networking, log, ServerLogs.ServerLogType.ConnectionUpdate);
            }
        }
    }
}
