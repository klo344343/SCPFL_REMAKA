using System;
using System.Runtime.CompilerServices;

namespace UnityEngine.PostProcessing
{
	[Serializable]
	public class GrainModel : PostProcessingModel
	{
		[Serializable]
		public struct Settings
		{
			[Tooltip("Enable the use of colored grain.")]
			public bool colored;

			[Tooltip("Grain strength. Higher means more visible grain.")]
			[Range(0f, 1f)]
			public float intensity;

			[Tooltip("Grain particle size.")]
			[Range(0.3f, 3f)]
			public float size;

			[Range(0f, 1f)]
			[Tooltip("Controls the noisiness response curve based on scene luminance. Lower values mean less noise in dark areas.")]
			public float luminanceContribution;

			public static Settings defaultSettings
			{
				[CompilerGenerated]
				get
				{
					return new Settings
					{
						colored = true,
						intensity = 0.5f,
						size = 1f,
						luminanceContribution = 0.8f
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
