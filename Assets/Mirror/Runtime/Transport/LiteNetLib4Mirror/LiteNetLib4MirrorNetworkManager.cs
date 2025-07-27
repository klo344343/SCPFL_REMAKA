using UnityEngine;
using Mirror;

namespace Mirror.LiteNetLib4Mirror
{
    [RequireComponent(typeof(LiteNetLib4MirrorTransport))]
    public class LiteNetLib4MirrorNetworkManager : NetworkManager
    {
        public static new LiteNetLib4MirrorNetworkManager singleton;

        public override void Awake()
        {
            GetComponent<LiteNetLib4MirrorTransport>().InitializeTransport();
            base.Awake();
            singleton = this;
        }

        public void StartClient(string ip, ushort port)
        {
            networkAddress = ip;
            maxConnections = 2;
            LiteNetLib4MirrorTransport.Singleton.clientAddress = ip;
            LiteNetLib4MirrorTransport.Singleton.port = port;
            LiteNetLib4MirrorTransport.Singleton.maxConnections = 2;
            StartClient();
        }

#if DISABLE_IPV6
        public void StartHost(string serverIPv4BindAddress, ushort port, ushort maxPlayers)
#else
        public void StartHost(string serverIPv4BindAddress, string serverIPv6BindAddress, ushort port, ushort maxPlayers)
#endif
        {
            networkAddress = serverIPv4BindAddress;
            maxConnections = maxPlayers;
            LiteNetLib4MirrorTransport.Singleton.clientAddress = serverIPv4BindAddress;
            LiteNetLib4MirrorTransport.Singleton.serverIPv4BindAddress = serverIPv4BindAddress;
#if !DISABLE_IPV6
            LiteNetLib4MirrorTransport.Singleton.serverIPv6BindAddress = serverIPv6BindAddress;
#endif
            LiteNetLib4MirrorTransport.Singleton.port = port;
            LiteNetLib4MirrorTransport.Singleton.maxConnections = maxPlayers;
            StartHost();
        }

#if DISABLE_IPV6
        public void StartServer(string serverIPv4BindAddress, ushort port, ushort maxPlayers)
#else
        public void StartServer(string serverIPv4BindAddress, string serverIPv6BindAddress, ushort port, ushort maxPlayers)
#endif
        {
            networkAddress = serverIPv4BindAddress;
            maxConnections = maxPlayers;
            LiteNetLib4MirrorTransport.Singleton.clientAddress = serverIPv4BindAddress;
            LiteNetLib4MirrorTransport.Singleton.serverIPv4BindAddress = serverIPv4BindAddress;
#if !DISABLE_IPV6
            LiteNetLib4MirrorTransport.Singleton.serverIPv6BindAddress = serverIPv6BindAddress;
#endif
            LiteNetLib4MirrorTransport.Singleton.port = port;
            LiteNetLib4MirrorTransport.Singleton.maxConnections = maxPlayers;
            StartServer();
        }

        public void StartHost(ushort port, ushort maxPlayers)
        {
            networkAddress = "127.0.0.1";
            maxConnections = maxPlayers;
            LiteNetLib4MirrorTransport.Singleton.clientAddress = "127.0.0.1";
            LiteNetLib4MirrorTransport.Singleton.serverIPv4BindAddress = "0.0.0.0";
#if !DISABLE_IPV6
            LiteNetLib4MirrorTransport.Singleton.serverIPv6BindAddress = "::";
#endif
            LiteNetLib4MirrorTransport.Singleton.port = port;
            LiteNetLib4MirrorTransport.Singleton.maxConnections = maxPlayers;
            StartHost();
        }

        public void StartServer(ushort port, ushort maxPlayers)
        {
            networkAddress = "127.0.0.1";
            maxConnections = maxPlayers;
            LiteNetLib4MirrorTransport.Singleton.serverIPv4BindAddress = "0.0.0.0";
#if !DISABLE_IPV6
            LiteNetLib4MirrorTransport.Singleton.serverIPv6BindAddress = "::";
#endif
            LiteNetLib4MirrorTransport.Singleton.port = port;
            LiteNetLib4MirrorTransport.Singleton.maxConnections = maxPlayers;
            StartServer();
        }

        public void DisconnectConnection(NetworkConnection conn, string message = null)
        {
            LiteNetLib4MirrorServer.DisconnectMessage = message;
            conn.Disconnect();
            LiteNetLib4MirrorServer.DisconnectMessage = null;
        }
    }
}