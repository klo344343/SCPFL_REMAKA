using System;
using System.Collections.Generic;
using Dissonance.Audio.Codecs;
using Dissonance.Datastructures;
using Dissonance.Extensions;
using Dissonance.Networking;
using NAudio.Wave;

namespace Dissonance.Audio.Playback
{
    internal class DecoderPipeline : IDecoderPipeline, IVolumeProvider, IRemoteChannelProvider
    {
        private static readonly Log Log = Logs.Create(LogCategory.Playback, typeof(DecoderPipeline).Name);

        private readonly Action<DecoderPipeline> _completionHandler;

        private readonly TransferBuffer<VoicePacket> _inputBuffer;

        private readonly ConcurrentPool<byte[]> _bytePool;

        private readonly ConcurrentPool<List<RemoteChannel>> _channelListPool;

        private readonly BufferedDecoder _source;

        private readonly ISampleSource _output;

        private volatile bool _prepared;

        private volatile bool _complete;

        private bool _sourceClosed;

        private readonly TimeSpan _frameDuration;

        private DateTime? _firstFrameArrival;

        private uint _firstFrameSeq;

        float IVolumeProvider.TargetVolume
        {
            get
            {
                return ((VolumeProvider != null) ? VolumeProvider.TargetVolume : 1f) * PlaybackOptions.AmplitudeMultiplier;
            }
        }

        public int BufferCount
        {
            get
            {
                return _source.BufferCount + _inputBuffer.EstimatedUnreadCount;
            }
        }

        public TimeSpan BufferTime
        {
            get
            {
                return TimeSpan.FromTicks(BufferCount * _frameDuration.Ticks);
            }
        }

        public float PacketLoss
        {
            get
            {
                return _source.PacketLoss;
            }
        }

        public PlaybackOptions PlaybackOptions
        {
            get
            {
                return _source.LatestPlaybackOptions;
            }
        }

        public WaveFormat OutputFormat
        {
            get
            {
                return _output.WaveFormat;
            }
        }

        public IVolumeProvider VolumeProvider { get; set; }

        public DecoderPipeline([NotNull] IVoiceDecoder decoder, uint frameSize, [NotNull] Action<DecoderPipeline> completionHandler, bool softClip = true)
        {
            if (decoder == null)
            {
                throw new ArgumentNullException("decoder");
            }

            _completionHandler = completionHandler ?? throw new ArgumentNullException("completionHandler");
            _inputBuffer = new TransferBuffer<VoicePacket>(32);
            _bytePool = new ConcurrentPool<byte[]>(12, () => new byte[(int)frameSize * decoder.Format.Channels * 4]);
            _channelListPool = new ConcurrentPool<List<RemoteChannel>>(12, () => new List<RemoteChannel>());
            _frameDuration = TimeSpan.FromSeconds((double)frameSize / (double)decoder.Format.SampleRate);
            _firstFrameArrival = null;
            _firstFrameSeq = 0u;
            BufferedDecoder source = new BufferedDecoder(decoder, frameSize, decoder.Format, RecycleFrame);
            VolumeRampedFrameSource source2 = new VolumeRampedFrameSource(source, this);
            FrameToSampleConverter frameToSampleConverter = new FrameToSampleConverter(source2);
            ISampleSource source3 = frameToSampleConverter;
            if (softClip)
            {
                source3 = new SoftClipSampleSource(frameToSampleConverter);
            }
            Resampler output = new Resampler(source3);
            _source = source;
            _output = output;
        }

        private void RecycleFrame(VoicePacket packet)
        {
            if (packet.EncodedAudioFrame.Array != null) 
            {
                _bytePool.Put(packet.EncodedAudioFrame.Array);
            }
            if (packet.Channels != null)
            {
                packet.Channels.Clear();
                _channelListPool.Put(packet.Channels);
            }
        }

        public void Prepare(SessionContext context)
        {
            _output.Prepare(context);
            _prepared = true;
        }

        public bool Read(ArraySegment<float> samples)
        {
            FlushTransferBuffer();
            bool flag = _output.Read(samples);
            if (flag)
            {
                _completionHandler(this);
            }
            return flag;
        }

        public float Push(VoicePacket packet, DateTime now)
        {
            List<RemoteChannel> list = null;
            if (packet.Channels != null)
            {
                list = _channelListPool.Get();
                list.Clear();
                list.AddRange(packet.Channels);
            }

            byte[] buffer = _bytePool.Get();
            if (buffer.Length < packet.EncodedAudioFrame.Count)
            {
                Log.Error($"Pooled buffer size ({buffer.Length}) is smaller than packet size ({packet.EncodedAudioFrame.Count})! Allocating new buffer.");
                buffer = new byte[packet.EncodedAudioFrame.Count]; 
            }

            Buffer.BlockCopy(packet.EncodedAudioFrame.Array, packet.EncodedAudioFrame.Offset, buffer, 0, packet.EncodedAudioFrame.Count);

            ArraySegment<byte> encodedAudioFrame = new ArraySegment<byte>(buffer, 0, packet.EncodedAudioFrame.Count);

            VoicePacket item = new VoicePacket(packet.SenderPlayerId, packet.PlaybackOptions.Priority, packet.PlaybackOptions.AmplitudeMultiplier, packet.PlaybackOptions.IsPositional, encodedAudioFrame, packet.SequenceNumber, list);
            if (!_inputBuffer.TryWrite(item))
            {
                Log.Warn("Failed to write an encoded audio packet into the input transfer buffer");
                _bytePool.Put(buffer);
                if (list != null)
                {
                    list.Clear();
                    _channelListPool.Put(list);
                }
            }
            if (!_prepared)
            {
                FlushTransferBuffer();
            }
            if (!_firstFrameArrival.HasValue)
            {
                _firstFrameArrival = now;
                _firstFrameSeq = packet.SequenceNumber;
                return 0f;
            }
            DateTime dateTime = _firstFrameArrival.Value + TimeSpan.FromTicks(_frameDuration.Ticks * (packet.SequenceNumber - _firstFrameSeq));
            return (float)(now - dateTime).TotalSeconds;
        }

        public void Stop()
        {
            _complete = true;
        }

        public void Reset()
        {
            _output.Reset();
            _firstFrameArrival = null;
            _prepared = false;
            _complete = false;
            _sourceClosed = false;
            VolumeProvider = null;
            while (_inputBuffer.Read(out VoicePacket item))
            {
                RecycleFrame(item);
            }
        }

        public void FlushTransferBuffer()
        {
            VoicePacket item;
            while (_inputBuffer.Read(out item))
            {
                _source.Push(item);
            }
            if (_complete && !_sourceClosed)
            {
                _sourceClosed = true;
                _source.Stop();
            }
        }

        public void GetRemoteChannels(List<RemoteChannel> output)
        {
            output.Clear();
            _source.GetRemoteChannels(output);
        }
    }
}