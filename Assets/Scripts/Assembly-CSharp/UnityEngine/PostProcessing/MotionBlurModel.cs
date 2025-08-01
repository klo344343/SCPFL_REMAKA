using System;
using System.Runtime.CompilerServices;

namespace UnityEngine.PostProcessing
{
	[Serializable]
	public class MotionBlurModel : PostProcessingModel
	{
		[Serializable]
		public struct Settings
		{
			[Tooltip("The angle of rotary shutter. Larger values give longer exposure.")]
			[Range(0f, 360f)]
			public float shutterAngle;

			[Range(4f, 32f)]
			[Tooltip("The amount of sample points, which affects quality and performances.")]
			public int sampleCount;

			[Range(0f, 1f)]
			[Tooltip("The strength of multiple frame blending. The opacity of preceding frames are determined from this coefficient and time differences.")]
			public float frameBlending;

			public static Settings defaultSettings
			{
				[CompilerGenerated]
				get
				{
					return new Settings
					{
						shutterAngle = 270f,
						sampleCount = 10,
						frameBlending = 0f
					};
				}
			}
		}

		[SerializeField]
		private Settings m_Settings = Settings.defaultSettings;

		public Settings settings
		{
			get
			{
				return m_Settings;
			}
			set
			{
				m_Settings = value;
			}
		}

		public override void Reset()
		{
			m_Settings = Settings.defaultSettings;
		}
	}
}
