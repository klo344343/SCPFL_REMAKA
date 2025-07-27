using System;
using System.Collections.Generic;
using Dissonance.Networking;
using LiteNetLib;
using Mirror;
using Mirror.LiteNetLib4Mirror;
using UnityEngine;

namespace Dissonance.Integrations.UNet_HLAPI
{
    [HelpURL("https://placeholder-software.co.uk/dissonance/docs/Basics/Quick-Start-UNet-HLAPI/")]
    public class HlapiCommsNetwork : BaseCommsNetwork<HlapiServer, HlapiClient, HlapiConn, Unit, Unit>
    {
        public byte UnreliableChannel = 1;
        public byte ReliableSequencedChannel = 0;

        public ushort TypeCode = 18385;

        private readonly Datastructures.ConcurrentPool<byte[]> _loopbackBuffers = new Datastructures.ConcurrentPool<byte[]>(8, () => new byte[1024]);
        private readonly List<ArraySegment<byte>> _loopbackQueue = new();

        public struct EmptyMessage : NetworkMessage { }

        protected override HlapiServer CreateServer(Unit details) => new HlapiServer(this);
        protected override HlapiClient CreateClient(Unit details) => new HlapiClient(this);

        protected override void Update()
        {
            if (base.IsInitialized)
            {
                bool isNetworkActive = NetworkManager.singleton != null && NetworkManager.singleton.isNetworkActive;
                bool isServerActive = NetworkServer.active;
                bool isClientActive = NetworkClient.active;
                // ��������� �������� isReady ��� �������
                bool clientConnected = isClientActive && NetworkClient.connection != null && NetworkClient.connection.isReady;

                if (isNetworkActive && (isServerActive || clientConnected))
                {
                    if (base.Mode.IsServerEnabled() != isServerActive || (base.Mode.IsClientEnabled() != clientConnected && !ServerStatic.IsDedicated))
                    {
                        if (ServerStatic.IsDedicated)
                        {
                            Log.Info($"Dissonance: ������������ � ����� ����������� �������.");
                            RunAsDedicatedServer(Unit.None);
                        }
                        else if (isServerActive && clientConnected)
                        {
                            Log.Info($"Dissonance: ������������ � ����� ����� (������ + ������).");
                            RunAsHost(Unit.None, Unit.None);
                        }
                        else if (isServerActive)
                        {
                            Log.Info($"Dissonance: ������������ � ����� �������.");
                            RunAsDedicatedServer(Unit.None);
                        }
                        else if (clientConnected)
                        {
                            Log.Info($"Dissonance: ������������ � ����� �������.");
                            RunAsClient(Unit.None);
                        }
                    }
                }
                else if (base.Mode != NetworkMode.None)
                {
                    Log.Info($"Dissonance: ��������� ��-�� ���������� ���� Mirror.");
                    Stop();
                    _loopbackQueue.Clear();
                }

                for (int i = 0; i < _loopbackQueue.Count; i++)
                {
                    base.Client?.NetworkReceivedPacket(_loopbackQueue[i]);
                    _loopbackBuffers.Put(_loopbackQueue[i].Array);
                }

                _loopbackQueue.Clear();
            }

            base.Update();
        }

        protected override void Initialize()
        {
            if (Transport.active == null)
            {
                throw Log.CreateUserErrorException("Mirror Transport �� ���������� ��� ���������.",
                    "���������, ��� ��������� Mirror (��������, LiteNetLib4MirrorTransport) �������� �� ����� NetworkManager.",
                    "https://dissonance.readthedocs.io/en/latest/Basics/Quick-Start-UNet-HLAPI/",
                    "NO_TRANSPORT_ACTIVE");
            }

            if (Transport.active is LiteNetLib4MirrorTransport lnlTransport)
            {
                if (UnreliableChannel >= lnlTransport.channels.Length)
                    throw Log.CreateUserErrorException($"����������� '����������' ����� ({UnreliableChannel}) ��������� ��� ���������! " +
                        $"�������� �������: {lnlTransport.channels.Length}.",
                        "��, ��������, ���������� �������� ����� ������ � ���������� HLAPI Comms Network.",
                        "https://dissonance.readthedocs.io/en/latest/Basics/Quick-Start-UNet-HLAPI/",
                        "B19B4916-8709-490B-8152-A646CCAD788E");

                if (lnlTransport.channels[UnreliableChannel] != DeliveryMethod.Unreliable)
                    throw Log.CreateUserErrorException($"����������� '����������' ����� ({UnreliableChannel}) ����� ��� QoS '{lnlTransport.channels[UnreliableChannel]}', ��������� 'Unreliable'.",
                        "���������, ��� ����� ������ � ���������� ����� QoS � LiteNetLib4MirrorTransport.",
                        "https://dissonance.readthedocs.io/en/latest/Basics/Quick-Start-UNet-HLAPI/",
                        "24ee53b1-7517-4672-8a4a-64a3e3c87ef6");

                if (ReliableSequencedChannel >= lnlTransport.channels.Length)
                    throw Log.CreateUserErrorException($"����������� '��������' ����� ({ReliableSequencedChannel}) ��������� ��� ���������! " +
                        $"�������� �������: {lnlTransport.channels.Length}.",
                        "��, ��������, ���������� �������� ����� ������ � ���������� HLAPI Comms Network.",
                        "https://dissonance.readthedocs.io/en/latest/Basics/Quick-Start-UNet-HLAPI/",
                        "5F5F2875-ECC8-433D-B0CB-97C151B8094D");

                var reliable = lnlTransport.channels[ReliableSequencedChannel];
                if (reliable != DeliveryMethod.ReliableSequenced && reliable != DeliveryMethod.ReliableOrdered)
                    throw Log.CreateUserErrorException($"����������� '�������� ����������������' ����� ({ReliableSequencedChannel}) ����� ��� QoS '{reliable}', ��������� 'ReliableSequenced' ��� 'ReliableOrdered'.",
                        "���������, ��� ����� ������ � ���������� ����� QoS � LiteNetLib4MirrorTransport.",
                        "https://dissonance.readthedocs.io/en/latest/Basics/Quick-Start-UNet-HLAPI/",
                        "035773ec-aef3-477a-8eeb-c234d416171c");
            }
            else
            {
                throw Log.CreateUserErrorException("�������� ��������� Mirror �� �������� LiteNetLib4MirrorTransport.",
                    "���������, ��� ��� NetworkManager ���������� LiteNetLib4MirrorTransport ��� ��������� ������� Dissonance.",
                    "https://dissonance.readthedocs.io/en/latest/Basics/Quick-Start-UNet-HLAPI/",
                    "WRONG_TRANSPORT_TYPE");
            }

            // �������: ��������������� ���������� ��� �������
            NetworkServer.RegisterHandler<EmptyMessage>(OnServerNullMessageReceived);

            // �������: ��������������� ���������� ��� �������
            NetworkClient.RegisterHandler<EmptyMessage>(OnClientNullMessageReceived);

            base.Initialize();
        }

        // �������: ��������� ���������� ��� ���������, ���������� �� �������.
        // �� ��������� NetworkConnectionToClient.
        private void OnServerNullMessageReceived(NetworkConnectionToClient conn, EmptyMessage msg)
        {
            if (Logs.GetLogLevel(LogCategory.Network) <= LogLevel.Trace)
                Log.Debug("������������ Dissonance �������� ��������� �� ������� (Mirror ����������)");
        }

        // �������: ��������� ���������� ��� ���������, ���������� �� �������.
        // �� ��������� NetworkConnection.
        private void OnClientNullMessageReceived(EmptyMessage msg)
        {
            if (Logs.GetLogLevel(LogCategory.Network) <= LogLevel.Trace)
                Log.Debug("������������ Dissonance �������� ��������� �� ������� (Mirror ����������)");
        }


        internal bool PreprocessPacketToClient(ArraySegment<byte> packet, HlapiConn destination)
        {
            if (base.Server == null)
                throw Log.CreatePossibleBugException("��������������� ��������� ���������� ������ ��������, �� ���� ���� �� �������� ��������", "8f9dc0a0-1b48-4a7f-9bb6-f767b2542ab1");

            if (base.Client == null)
                return false;

            if (NetworkClient.connection != null && NetworkClient.connection != destination.Connection)
                return false;

            byte[] buffer = _loopbackBuffers.Get();
            packet.CopyTo(buffer);
            _loopbackQueue.Add(new ArraySegment<byte>(buffer, 0, packet.Count));

            return true;
        }

        internal bool PreprocessPacketToServer(ArraySegment<byte> packet)
        {
            if (base.Client == null)
                throw Log.CreatePossibleBugException("��������������� ��������� ����������� ������ ��������, �� ���� ���� �� �������� ��������", "dd75dce4-e85c-4bb3-96ec-3a3636cc4fbe");

            if (base.Server == null)
                return false;

            base.Server.NetworkReceivedPacket(new HlapiConn(NetworkClient.connection), packet);
            return true;
        }

        internal ArraySegment<byte> CopyToArraySegment(NetworkReader msg, ArraySegment<byte> segment)
        {
            if (msg == null)
                throw new ArgumentNullException(nameof(msg));

            byte[] array = segment.Array ?? throw new ArgumentNullException(nameof(segment));

            uint num = msg.ReadUInt();
            if (num > segment.Count)
                throw Log.CreatePossibleBugException("������ ������ ������ ������� ���", "A7387195-BF3D-4796-A362-6C64BB546445");

            for (int i = 0; i < num; i++)
                array[segment.Offset + i] = msg.ReadByte();

            return new ArraySegment<byte>(array, segment.Offset, (int)num);
        }

        internal int CopyPacketToNetworkWriter(ArraySegment<byte> packet, NetworkWriter writer)
        {
            if (writer == null)
                throw new ArgumentNullException(nameof(writer));

            byte[] array = packet.Array ?? throw new ArgumentNullException(nameof(packet));

            writer.WriteUShort(TypeCode);
            writer.WriteUInt((uint)packet.Count);
            writer.WriteBytes(array, packet.Offset, packet.Count);
            return writer.Position;
        }
    }
}