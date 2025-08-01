using System;
using System.Runtime.CompilerServices;

namespace UnityEngine.PostProcessing
{
	[Serializable]
	public class BuiltinDebugViewsModel : PostProcessingModel
	{
		public enum Mode
		{
			None = 0,
			Depth = 1,
			Normals = 2,
			MotionVectors = 3,
			AmbientOcclusion = 4,
			EyeAdaptation = 5,
			FocusPlane = 6,
			PreGradingLog = 7,
			LogLut = 8,
			UserLut = 9
		}

		[Serializable]
		public struct DepthSettings
		{
			[Range(0f, 1f)]
			[Tooltip("Scales the camera far plane before displaying the depth map.")]
			public float scale;

			public static DepthSettings defaultSettings
			{
				[CompilerGenerated]
				get
				{
					return new DepthSettings
					{
						scale = 1f
					};
				}
			}
		}

		[Serializable]
		public struct MotionVectorsSettings
		{
			[Range(0f, 1f)]
			[Tooltip("Opacity of the source render.")]
			public float sourceOpacity;

			[Range(0f, 1f)]
			[Tooltip("Opacity of the per-pixel motion vector colors.")]
			public float motionImageOpacity;

			[Min(0f)]
			[Tooltip("Because motion vectors are mainly very small vectors, you can use this setting to make them more visible.")]
			public float motionImageAmplitude;

			[Tooltip("Opacity for the motion vector arrows.")]
			[Range(0f, 1f)]
			public float motionVectorsOpacity;

			[Range(8f, 64f)]
			[Tooltip("The arrow density on screen.")]
			public int motionVectorsResolution;

			[Min(0f)]
			[Tooltip("Tweaks the arrows length.")]
			public float motionVectorsAmplitude;

			public static MotionVectorsSettings defaultSettings
			{
				[CompilerGenerated]
				get
				{
					return new MotionVectorsSettings
					{
						sourceOpacity = 1f,
						motionImageOpacity = 0f,
						motionImageAmplitude = 16f,
						motionVectorsOpacity = 1f,
						motionVectorsResolution = 24,
						motionVectorsAmplitude = 64f
					};
				}
			}
		}

		[Serializable]
		public struct Settings
		{
			public Mode mode;

			public DepthSettings depth;

			public MotionVectorsSettings motionVectors;

			public static Settings defaultSettings
			{
				[CompilerGenerated]
				get
				{
					return new Settings
					{
						mode = Mode.None,
						depth = DepthSettings.defaultSettings,
						motionVectors = MotionVectorsSettings.defaultSettings
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

		public bool willInterrupt
		{
			[CompilerGenerated]
			get
			{
				return !IsModeActive(Mode.None) && !IsModeActive(Mode.EyeAdaptation) && !IsModeActive(Mode.PreGradingLog) && !IsModeActive(Mode.LogLut) && !IsModeActive(Mode.UserLut);
			}
		}

		public override void Reset()
		{
			settings = Settings.defaultSettings;
		}

		public bool IsModeActive(Mode mode)
		{
			return m_Settings.mode == mode;
		}
	}
}
