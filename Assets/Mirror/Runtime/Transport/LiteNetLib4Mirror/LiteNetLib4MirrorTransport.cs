using System;
using System.Collections.Generic;
using LiteNetLib;
using LiteNetLib.Utils;
using LiteNetLib4Mirror.Open.Nat;
using UnityEngine;
using Mirror; // <--- ADD THIS USING DIRECTIVE for 'Channels'

namespace Mirror.LiteNetLib4Mirror
{
    public class LiteNetLib4MirrorTransport : Transport, ISegmentTransport
    {
        public static LiteNetLib4MirrorTransport Singleton;

#if UNITY_EDITOR
        [Header("Connection settings")]
#endif
        public string clientAddress = "127.0.0.1";
#if UNITY_EDITOR
        [Rename("Server IPv4 Bind Address")]
#endif
        public string serverIPv4BindAddress = "0.0.0.0";
#if !DISABLE_IPV6
#if UNITY_EDITOR
        [Rename("Server IPv6 Bind Address")]
#endif
        public string serverIPv6BindAddress = "::";
#endif
        public ushort port = 7777;
#if UNITY_EDITOR
        [Rename("Use UPnP")]
#endif
        public bool useUpnP = true;
        public ushort maxConnections = 20;
#if !DISABLE_IPV6
#if UNITY_EDITOR
        [Rename("IPv6 Enabled")]
#endif
        public bool ipv6Enabled = true;
#endif

#if UNITY_EDITOR
        [ArrayRename("Channel")]
#endif
        public DeliveryMethod[] channels =
        {
            DeliveryMethod.ReliableOrdered,
            DeliveryMethod.Unreliable,
            DeliveryMethod.Sequenced,
            DeliveryMethod.ReliableSequenced,
            DeliveryMethod.ReliableUnordered
        };

#if UNITY_EDITOR
        [Header("Connection additional auth code (optional)")]
#endif
        public string authCode;

        /// <summary>Library logic update (and send) period in milliseconds</summary>
#if UNITY_EDITOR
        [Header("LiteNetLib settings")]
        [Tooltip("Library logic update (and send) period in milliseconds")]
#endif
        public int updateTime = 15;
        /// <summary>Interval for latency detection and checking connection</summary>
#if UNITY_EDITOR
        [Tooltip("Interval for latency detection and checking connection")]
#endif
        public int pingInterval = 1000;
        /// <summary>If client or server doesn't receive any packet from remote peer during this time then connection will be closed (including library internal keepalive packets)</summary>
#if UNITY_EDITOR
        [Tooltip("If client or server doesn't receive any packet from remote peer during this time then connection will be closed (including library internal keepalive packets)")]
#endif
        public int disconnectTimeout = 5000;
        /// <summary>Delay between connection attempts</summary>
#if UNITY_EDITOR
        [Tooltip("Delay between connection attempts")]
#endif
        public int reconnectDelay = 500;
        /// <summary>Maximum connection attempts before client stops and call disconnect event.</summary>
#if UNITY_EDITOR
        [Tooltip("Maximum connection attempts before client stops and call disconnect event.")]
#endif
        public int maxConnectAttempts = 10;

        /// <summary>Simulate packet loss by dropping random amount of packets. (Works only in DEBUG mode)</summary>
#if UNITY_EDITOR
        [Header("Debug connection tests")]
        [Tooltip("Simulate packet loss by dropping random amount of packets. (Works only in DEBUG mode)")]
#endif
        public bool simulatePacketLoss;
        /// <summary>Chance of packet loss when simulation enabled. Value in percents.</summary>
#if UNITY_EDITOR
        [Range(0, 100)]
        [Tooltip("Chance of packet loss when simulation enabled. Value in percents.")]
#endif
        public int simulationPacketLossChance = 10;
        /// <summary>Simulate latency by holding packets for random time. (Works only in DEBUG mode)</summary>
#if UNITY_EDITOR
        [Tooltip("Simulate latency by holding packets for random time. (Works only in DEBUG mode)")]
#endif
        public bool simulateLatency;
        /// <summary>Minimum simulated latency</summary>
#if UNITY_EDITOR
        [Tooltip("Minimum simulated latency")]
#endif
        public int simulationMinLatency = 30;
        /// <summary>Maximum simulated latency</summary>
#if UNITY_EDITOR
        [Tooltip("Maximum simulated latency")]
#endif
        public int simulationMaxLatency = 100;

#if UNITY_EDITOR
        [Header("Error events")]
#endif
        public UnityEventError onClientSocketError;
        public UnityEventIntError onServerSocketError;

        internal static bool Polling;
        private static readonly NetDataWriter ConnectWriter = new NetDataWriter();

        #region Overridable methods
        protected internal virtual void GetConnectData(NetDataWriter writer)
        {
            writer.Put(GetConnectKey());
        }

        protected internal virtual void ProcessConnectionRequest(ConnectionRequest request)
        {
            if (LiteNetLib4MirrorCore.Host.PeersCount >= maxConnections)
            {
                request.Reject();
            }
            else if (request.AcceptIfKey(LiteNetLib4MirrorServer.Code) == null)
            {
                Debug.LogWarning("Client tried to join with an invalid auth code! Current code:" + LiteNetLib4MirrorServer.Code);
            }
        }

        protected internal virtual void OnConncetionRefused(DisconnectInfo disconnectinfo)
        {

        }
        #endregion

        internal void InitializeTransport()
        {
            if (Singleton == null)
            {
                Singleton = this;
                LiteNetLib4MirrorCore.State = LiteNetLib4MirrorCore.States.Idle;
            }
        }

        private static string GetConnectKey()
        {
            return LiteNetLib4MirrorUtils.ToBase64(Application.productName + Application.companyName + Application.unityVersion + LiteNetLib4MirrorCore.TransportVersion + Singleton.authCode);
        }

        #region Unity Functions
        private void Awake()
        {
            InitializeTransport();
        }

        private void LateUpdate()
        {
            if (Polling)
            {
                LiteNetLib4MirrorCore.Host.PollEvents();
            }
        }

        private void OnDestroy()
        {
            LiteNetLib4MirrorCore.StopTransport();
            if (LiteNetLib4MirrorUtils.LastForwardedPort != 0)
            {
                NatDiscoverer.ReleaseAll();
                LiteNetLib4MirrorUtils.LastForwardedPort = 0;
            }
        }
        #endregion

        #region Transport Overrides
        public override bool Available()
        {
            return Application.platform != RuntimePlatform.WebGLPlayer;
        }

        public override bool ClientConnected()
        {
            return LiteNetLib4MirrorClient.IsConnected();
        }

        public override void ClientConnect(string address)
        {
            clientAddress = address;
            ConnectWriter.Reset();
            GetConnectData(ConnectWriter);
            LiteNetLib4MirrorClient.ConnectClient(ConnectWriter);
        }

        // CORRECTED: Changed signature to match Transport.ClientSend
        public override void ClientSend(ArraySegment<byte> data, int channelId)
        {
            byte channel = (byte)(channelId < channels.Length ? channelId : 0);
            LiteNetLib4MirrorClient.Send(channels[channel], data.Array, data.Offset, data.Count, channel);
        }

        public override void ClientDisconnect()
        {
            if (!LiteNetLib4MirrorServer.IsActive()) // This logic might need review depending on your desired behavior (client disconnect when server is also running?)
            {
                LiteNetLib4MirrorCore.StopTransport();
            }
        }

        public override bool ServerActive()
        {
            return LiteNetLib4MirrorServer.IsActive();
        }

        public override void ServerStart()
        {
            LiteNetLib4MirrorServer.StartServer(GetConnectKey());
        }

        // REMOVED: This signature is not an override in Mirror.Transport
        // public override bool ServerSend(List<int> connectionIds, int channelId, ArraySegment<byte> data)
        // {
        //     byte channel = (byte)(channelId < channels.Length ? channelId : 0);
        //     bool success = true;
        //     foreach (int id in connectionIds)
        //     {
        //         success &= LiteNetLib4MirrorServer.Send(id, channels[0], data.Array, data.Offset, data.Count, channel);
        //     }
        //     return success;
        // }

        // This should be a helper method, not an override, if you need it.
        // It's already correctly defined without 'override' above.
        // If it's meant to be ISegmentTransport.ServerSend, ensure the interface is correctly implemented.
        public bool ServerSend(int connectionId, int channelId, ArraySegment<byte> data) // This is likely for ISegmentTransport, not a Transport override
        {
            byte channel = (byte)(channelId < channels.Length ? channelId : 0);
            return LiteNetLib4MirrorServer.Send(connectionId, channels[channel], data.Array, data.Offset, data.Count, channel);
        }


        // CORRECTED: Changed signature to match Transport.ServerSend
        // This is the required override for sending to a single client
        public override void ServerSend(int connectionId, ArraySegment<byte> data, int channelId)
        {
            byte channel = (byte)(channelId < channels.Length ? channelId : 0);
            LiteNetLib4MirrorServer.Send(connectionId, channels[channel], data.Array, data.Offset, data.Count, channel);
        }

        // CORRECTED: Changed return type to void
        public override void ServerDisconnect(int connectionId)
        {
            if (connectionId != 0) // Assuming connectionId 0 is a special case or invalid
            {
                LiteNetLib4MirrorServer.Disconnect(connectionId);
            }
        }

        public override void ServerStop()
        {
            LiteNetLib4MirrorCore.StopTransport();
        }

        public override string ServerGetClientAddress(int connectionId)
        {
            return LiteNetLib4MirrorServer.GetClientAddress(connectionId);
        }

        // CORRECTED: Added implementation for ServerUri()
        public override Uri ServerUri()
        {
            // Assuming your server is binding to serverIPv4BindAddress and port
            // You might need to adjust this if you support IPv6 as the primary bind.
            return new Uri($"kcp://{serverIPv4BindAddress}:{port}"); // Or "udp" if LiteNetLib is purely UDP
        }

        public override void Shutdown()
        {
            LiteNetLib4MirrorCore.StopTransport();
        }

        public override int GetMaxPacketSize(int channelId = Channels.Reliable) // Should be resolved with 'using Mirror;'
        {
            // Ensure channelId is within bounds of your channels array
            if (channelId < 0 || channelId >= channels.Length)
            {
                Debug.LogError($"Invalid channelId {channelId}. Using default channel 0.");
                channelId = 0;
            }
            return LiteNetLib4MirrorCore.GetMaxPacketSize(channels[channelId]);
        }

        public override string ToString()
        {
            return LiteNetLib4MirrorCore.GetState();
        }

        public bool ClientSend(int channelId, ArraySegment<byte> data)
        {
            byte channel = (byte)(channelId < channels.Length ? channelId : 0);
            return LiteNetLib4MirrorClient.Send(channels[0], data.Array, data.Offset, data.Count, channel);
        }
        #endregion
    }
}