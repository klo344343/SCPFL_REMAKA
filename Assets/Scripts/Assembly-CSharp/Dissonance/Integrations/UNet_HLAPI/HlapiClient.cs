using Dissonance.Networking;
using Mirror;
using System;
using UnityEngine;

namespace Dissonance.Integrations.UNet_HLAPI
{
    public struct DissonanceNetworkMessage : NetworkMessage
    {
        public byte[] Data;
        public int Offset;
        public int Count;

        public readonly ArraySegment<byte> Payload => new(Data, Offset, Count);
    }

    public class HlapiClient : BaseClient<HlapiServer, HlapiClient, HlapiConn>
    {
        private readonly HlapiCommsNetwork _network;
        private readonly NetworkWriter _sendWriter;
        private readonly byte[] _receiveBuffer = new byte[1024];

        public HlapiClient(HlapiCommsNetwork network)
            : base((ICommsNetworkState)network)
        {
            _network = network ?? throw new ArgumentNullException(nameof(network));
            _sendWriter = new NetworkWriter();
        }

        public override void Connect()
        {
            if (!_network.Mode.IsServerEnabled())
            {
                NetworkClient.RegisterHandler<DissonanceNetworkMessage>(OnMessageReceivedHandler, false);
            }
            Connected();
        }

        public override void Disconnect()
        {
            if (!_network.Mode.IsServerEnabled() && NetworkClient.active)
            {
                NetworkClient.UnregisterHandler<DissonanceNetworkMessage>();
            }
            base.Disconnect();
        }

        private void OnMessageReceivedHandler(DissonanceNetworkMessage message)
        {
            NetworkReceivedPacket(message.Payload);
        }

        protected override void ReadMessages()
        {
            // Mirror handles message reading automatically
        }

        protected override void SendReliable(ArraySegment<byte> packet)
        {
            Send(packet, Channels.Reliable);
        }

        protected override void SendUnreliable(ArraySegment<byte> packet)
        {
            Send(packet, Channels.Unreliable);
        }

        private void Send(ArraySegment<byte> packet, int channel)
        {
            if (_network.PreprocessPacketToServer(packet))
                return;

            var message = new DissonanceNetworkMessage
            {
                Data = new byte[packet.Count],
                Offset = 0,
                Count = packet.Count
            };
            Buffer.BlockCopy(packet.Array, packet.Offset, message.Data, 0, packet.Count);

            NetworkClient.Send(message, channel);
        }
    }
}