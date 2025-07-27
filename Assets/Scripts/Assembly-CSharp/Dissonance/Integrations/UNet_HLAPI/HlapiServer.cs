using System;
using System.Collections.Generic;
using Dissonance.Networking;
using Dissonance.Networking.Server;
using Mirror;

namespace Dissonance.Integrations.UNet_HLAPI
{
    public struct RawPacketMessage : NetworkMessage
    {
        public byte[] Data;
    }

    public class HlapiServer : BaseServer<HlapiServer, HlapiClient, HlapiConn>
    {
        public static HlapiServer _instance;

        [Dissonance.NotNull]
        private readonly HlapiCommsNetwork _network;

        private readonly byte[] _receiveBuffer = new byte[1024];
        private readonly List<NetworkConnectionToClient> _addedConnections = new();

        public HlapiServer([Dissonance.NotNull] HlapiCommsNetwork network)
        {
            _network = network ?? throw new ArgumentNullException(nameof(network));
            _instance = this;
        }

        public override void Connect()
        {
            NetworkServer.RegisterHandler<RawPacketMessage>(OnMessageReceivedHandler, false);
            base.Connect();
        }

        private void OnMessageReceivedHandler(NetworkConnectionToClient conn, RawPacketMessage msg)
        {
            if (msg.Data == null || msg.Data.Length == 0)
                return;

            var segment = new ArraySegment<byte>(msg.Data, 0, msg.Data.Length);
            NetworkReceivedPacket(new HlapiConn(conn), segment);
        }

        protected override void AddClient([Dissonance.NotNull] ClientInfo<HlapiConn> client)
        {
            base.AddClient(client);
            if (client.PlayerName != _network.PlayerName)
            {
                _addedConnections.Add((NetworkConnectionToClient)client.Connection.Connection);
            }
        }

        public override void Disconnect()
        {
            base.Disconnect();
            NetworkServer.RegisterHandler<RawPacketMessage>(NullMessageReceivedHandlerMirror, false);
        }

        internal static void NullMessageReceivedHandlerMirror(NetworkConnectionToClient conn, RawPacketMessage msg)
        {
        }

        protected override void ReadMessages()
        {
        }

        public static void OnServerDisconnect(NetworkConnectionToClient connection)
        {
            _instance?.OnServerDisconnect(new HlapiConn(connection));
        }

        private void OnServerDisconnect(HlapiConn conn)
        {
            int idx = _addedConnections.IndexOf((NetworkConnectionToClient)conn.Connection);
            if (idx >= 0)
            {
                _addedConnections.RemoveAt(idx);
                ClientDisconnected(conn);
            }
        }

        public override ServerState Update()
        {
            for (int i = _addedConnections.Count - 1; i >= 0; i--)
            {
                var conn = _addedConnections[i];
                if (!conn.isAuthenticated || !conn.isReady || !NetworkServer.connections.ContainsKey(conn.connectionId))
                {
                    ClientDisconnected(new HlapiConn(conn));
                    _addedConnections.RemoveAt(i);
                }
            }

            return base.Update();
        }

        protected override void SendReliable(HlapiConn connection, ArraySegment<byte> packet)
        {
            if (!Send(packet, connection, _network.ReliableSequencedChannel))
            {
                FatalError("Failed to send reliable packet (Mirror send error)");
            }
        }

        protected override void SendUnreliable(HlapiConn connection, ArraySegment<byte> packet)
        {
            Send(packet, connection, _network.UnreliableChannel);
        }

        private bool Send(ArraySegment<byte> packet, HlapiConn connection, byte channel)
        {
            if (_network.PreprocessPacketToClient(packet, connection))
                return true;

            var conn = connection.Connection;
            if (conn == null || !conn.isReady)
                return false;

            byte[] data = new byte[packet.Count];
            Buffer.BlockCopy(packet.Array!, packet.Offset, data, 0, packet.Count);

            conn.Send(new RawPacketMessage { Data = data }, channel);
            return true;
        }
    }
}
