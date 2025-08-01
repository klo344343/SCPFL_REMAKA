using System;
using NAudio.Wave;

namespace Dissonance.Audio.Codecs.Opus
{
	internal class OpusDecoder : IVoiceDecoder, IDisposable
	{
		private readonly WaveFormat _format;

		private OpusNative.OpusDecoder _decoder;

		public WaveFormat Format
		{
			get
			{
				return _format;
			}
		}

		public OpusDecoder([NotNull] WaveFormat format, bool fec = true)
		{
			if (format == null)
			{
				throw new ArgumentNullException("format");
			}
			_format = format;
			_decoder = new OpusNative.OpusDecoder(format.SampleRate, format.Channels)
			{
				EnableForwardErrorCorrection = fec
			};
		}

		public void Dispose()
		{
			if (_decoder != null)
			{
				_decoder.Dispose();
			}
			_decoder = null;
		}

		public void Reset()
		{
			_decoder.Reset();
		}

		public int Decode(EncodedBuffer input, ArraySegment<float> output)
		{
			return _decoder.DecodeFloats(input, output);
		}
	}
}
