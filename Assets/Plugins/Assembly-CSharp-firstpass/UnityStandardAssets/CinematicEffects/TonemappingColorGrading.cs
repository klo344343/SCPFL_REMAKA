using System;
using UnityEngine;

namespace UnityStandardAssets.CinematicEffects
{
	[ExecuteInEditMode]
	[RequireComponent(typeof(Camera))]
	[AddComponentMenu("Image Effects/Cinematic/Tonemapping and Color Grading")]
	[ImageEffectAllowedInSceneView]
	public class TonemappingColorGrading : MonoBehaviour
	{
		[AttributeUsage(AttributeTargets.Field)]
		public class SettingsGroup : Attribute
		{
		}

		public class IndentedGroup : PropertyAttribute
		{
		}

		public class ChannelMixer : PropertyAttribute
		{
		}

		public class ColorWheelGroup : PropertyAttribute
		{
			public int minSizePerWheel = 60;

			public int maxSizePerWheel = 150;

			public ColorWheelGroup()
			{
			}

			public ColorWheelGroup(int minSizePerWheel, int maxSizePerWheel)
			{
				this.minSizePerWheel = minSizePerWheel;
				this.maxSizePerWheel = maxSizePerWheel;
			}
		}

		public class Curve : PropertyAttribute
		{
			public Color color = Color.white;

			public Curve()
			{
			}

			public Curve(float r, float g, float b, float a)
			{
				color = new Color(r, g, b, a);
			}
		}

		[Serializable]
		public struct EyeAdaptationSettings
		{
			public bool enabled;

			[Min(0f)]
			[Tooltip("Midpoint Adjustment.")]
			public float middleGrey;

			[Tooltip("The lowest possible exposure value; adjust this value to modify the brightest areas of your level.")]
			public float min;

			[Tooltip("The highest possible exposure value; adjust this value to modify the darkest areas of your level.")]
			public float max;

			[Min(0f)]
			[Tooltip("Speed of linear adaptation. Higher is faster.")]
			public float speed;

			[Tooltip("Displays a luminosity helper in the GameView.")]
			public bool showDebug;

			public static EyeAdaptationSettings defaultSettings
			{
				get
				{
					return new EyeAdaptationSettings
					{
						enabled = false,
						showDebug = false,
						middleGrey = 0.5f,
						min = -3f,
						max = 3f,
						speed = 1.5f
					};
				}
			}
		}

		public enum Tonemapper
		{
			ACES = 0,
			Curve = 1,
			Hable = 2,
			HejlDawson = 3,
			Photographic = 4,
			Reinhard = 5,
			Neutral = 6
		}

		[Serializable]
		public struct TonemappingSettings
		{
			public bool enabled;

			[Tooltip("Tonemapping technique to use. ACES is the recommended one.")]
			public Tonemapper tonemapper;

			[Min(0f)]
			[Tooltip("Adjusts the overall exposure of the scene.")]
			public float exposure;

			[Tooltip("Custom tonemapping curve.")]
			public AnimationCurve curve;

			[Range(-0.1f, 0.1f)]
			public float neutralBlackIn;

			[Range(1f, 20f)]
			public float neutralWhiteIn;

			[Range(-0.09f, 0.1f)]
			public float neutralBlackOut;

			[Range(1f, 19f)]
			public float neutralWhiteOut;

			[Range(0.1f, 20f)]
			public float neutralWhiteLevel;

			[Range(1f, 10f)]
			public float neutralWhiteClip;

			public static TonemappingSettings defaultSettings
			{
				get
				{
					return new TonemappingSettings
					{
						enabled = false,
						tonemapper = Tonemapper.Neutral,
						exposure = 1f,
						curve = CurvesSettings.defaultCurve,
						neutralBlackIn = 0.02f,
						neutralWhiteIn = 10f,
						neutralBlackOut = 0f,
						neutralWhiteOut = 10f,
						neutralWhiteLevel = 5.3f,
						neutralWhiteClip = 10f
					};
				}
			}
		}

		[Serializable]
		public struct LUTSettings
		{
			public bool enabled;

			[Tooltip("Custom lookup texture (strip format, e.g. 256x16).")]
			public Texture texture;

			[Range(0f, 1f)]
			[Tooltip("Blending factor.")]
			public float contribution;

			public static LUTSettings defaultSettings
			{
				get
				{
					return new LUTSettings
					{
						enabled = false,
						texture = null,
						contribution = 1f
					};
				}
			}
		}

		[Serializable]
		public struct ColorWheelsSettings
		{
			[ColorUsage(false)]
			public Color shadows;

			[ColorUsage(false)]
			public Color midtones;

			[ColorUsage(false)]
			public Color highlights;

			public static ColorWheelsSettings defaultSettings
			{
				get
				{
					return new ColorWheelsSettings
					{
						shadows = Color.white,
						midtones = Color.white,
						highlights = Color.white
					};
				}
			}
		}

		[Serializable]
		public struct BasicsSettings
		{
			[Range(-2f, 2f)]
			[Tooltip("Sets the white balance to a custom color temperature.")]
			public float temperatureShift;

			[Range(-2f, 2f)]
			[Tooltip("Sets the white balance to compensate for a green or magenta tint.")]
			public float tint;

			[Space]
			[Range(-0.5f, 0.5f)]
			[Tooltip("Shift the hue of all colors.")]
			public float hue;

			[Range(0f, 2f)]
			[Tooltip("Pushes the intensity of all colors.")]
			public float saturation;

			[Range(-1f, 1f)]
			[Tooltip("Adjusts the saturation so that clipping is minimized as colors approach full saturation.")]
			public float vibrance;

			[Range(0f, 10f)]
			[Tooltip("Brightens or darkens all colors.")]
			public float value;

			[Space]
			[Range(0f, 2f)]
			[Tooltip("Expands or shrinks the overall range of tonal values.")]
			public float contrast;

			[Range(0.01f, 5f)]
			[Tooltip("Contrast gain curve. Controls the steepness of the curve.")]
			public float gain;

			[Range(0.01f, 5f)]
			[Tooltip("Applies a pow function to the source.")]
			public float gamma;

			public static BasicsSettings defaultSettings
			{
				get
				{
					return new BasicsSettings
					{
						temperatureShift = 0f,
						tint = 0f,
						contrast = 1f,
						hue = 0f,
						saturation = 1f,
						value = 1f,
						vibrance = 0f,
						gain = 1f,
						gamma = 1f
					};
				}
			}
		}

		[Serializable]
		public struct ChannelMixerSettings
		{
			public int currentChannel;

			public Vector3[] channels;

			public static ChannelMixerSettings defaultSettings
			{
				get
				{
					return new ChannelMixerSettings
					{
						currentChannel = 0,
						channels = new Vector3[3]
						{
							new Vector3(1f, 0f, 0f),
							new Vector3(0f, 1f, 0f),
							new Vector3(0f, 0f, 1f)
						}
					};
				}
			}
		}

		[Serializable]
		public struct CurvesSettings
		{
			[Curve]
			public AnimationCurve master;

			[Curve(1f, 0f, 0f, 1f)]
			public AnimationCurve red;

			[Curve(0f, 1f, 0f, 1f)]
			public AnimationCurve green;

			[Curve(0f, 1f, 1f, 1f)]
			public AnimationCurve blue;

			public static CurvesSettings defaultSettings
			{
				get
				{
					return new CurvesSettings
					{
						master = defaultCurve,
						red = defaultCurve,
						green = defaultCurve,
						blue = defaultCurve
					};
				}
			}

			public static AnimationCurve defaultCurve
			{
				get
				{
					return new AnimationCurve(new Keyframe(0f, 0f, 1f, 1f), new Keyframe(1f, 1f, 1f, 1f));
				}
			}
		}

		public enum ColorGradingPrecision
		{
			Normal = 0x10,
			High = 0x20
		}

		[Serializable]
		public struct ColorGradingSettings
		{
			public bool enabled;

			[Tooltip("Internal LUT precision. \"Normal\" is 256x16, \"High\" is 1024x32. Prefer \"Normal\" on mobile devices.")]
			public ColorGradingPrecision precision;

			[Space]
			[ColorWheelGroup]
			public ColorWheelsSettings colorWheels;

			[Space]
			[IndentedGroup]
			public BasicsSettings basics;

			[Space]
			[ChannelMixer]
			public ChannelMixerSettings channelMixer;

			[Space]
			[IndentedGroup]
			public CurvesSettings curves;

			[Space]
			[Tooltip("Use dithering to try and minimize color banding in dark areas.")]
			public bool useDithering;

			[Tooltip("Displays the generated LUT in the top left corner of the GameView.")]
			public bool showDebug;

			public static ColorGradingSettings defaultSettings
			{
				get
				{
					return new ColorGradingSettings
					{
						enabled = false,
						useDithering = false,
						showDebug = false,
						precision = ColorGradingPrecision.Normal,
						colorWheels = ColorWheelsSettings.defaultSettings,
						basics = BasicsSettings.defaultSettings,
						channelMixer = ChannelMixerSettings.defaultSettings,
						curves = CurvesSettings.defaultSettings
					};
				}
			}

			internal void Reset()
			{
				curves = CurvesSettings.defaultSettings;
			}
		}

		private enum Pass
		{
			LutGen = 0,
			AdaptationLog = 1,
			AdaptationExpBlend = 2,
			AdaptationExp = 3,
			TonemappingOff = 4,
			TonemappingACES = 5,
			TonemappingCurve = 6,
			TonemappingHable = 7,
			TonemappingHejlDawson = 8,
			TonemappingPhotographic = 9,
			TonemappingReinhard = 10,
			TonemappingNeutral = 11,
			AdaptationDebug = 12
		}

		[SerializeField]
		[SettingsGroup]
		private EyeAdaptationSettings m_EyeAdaptation = EyeAdaptationSettings.defaultSettings;

		[SerializeField]
		[SettingsGroup]
		private TonemappingSettings m_Tonemapping = TonemappingSettings.defaultSettings;

		[SerializeField]
		[SettingsGroup]
		private ColorGradingSettings m_ColorGrading = ColorGradingSettings.defaultSettings;

		[SerializeField]
		[SettingsGroup]
		private LUTSettings m_Lut = LUTSettings.defaultSettings;

		private Texture2D m_IdentityLut;

		private RenderTexture m_InternalLut;

		private Texture2D m_CurveTexture;

		private Texture2D m_TonemapperCurve;

		private float m_TonemapperCurveRange;

		[SerializeField]
		private Shader m_Shader;

		private Material m_Material;

		private bool m_Dirty = true;

		private bool m_TonemapperDirty = true;

		private RenderTexture m_SmallAdaptiveRt;

		private RenderTextureFormat m_AdaptiveRtFormat;

		public EyeAdaptationSettings eyeAdaptation
		{
			get
			{
				return m_EyeAdaptation;
			}
			set
			{
				m_EyeAdaptation = value;
			}
		}

		public TonemappingSettings tonemapping
		{
			get
			{
				return m_Tonemapping;
			}
			set
			{
				m_Tonemapping = value;
				SetTonemapperDirty();
			}
		}

		public ColorGradingSettings colorGrading
		{
			get
			{
				return m_ColorGrading;
			}
			set
			{
				m_ColorGrading = value;
				SetDirty();
			}
		}

		public LUTSettings lut
		{
			get
			{
				return m_Lut;
			}
			set
			{
				m_Lut = value;
			}
		}

		private Texture2D identityLut
		{
			get
			{
				if (m_IdentityLut == null || m_IdentityLut.height != lutSize)
				{
					UnityEngine.Object.DestroyImmediate(m_IdentityLut);
					m_IdentityLut = GenerateIdentityLut(lutSize);
				}
				return m_IdentityLut;
			}
		}

		private RenderTexture internalLutRt
		{
			get
			{
				if (m_InternalLut == null || !m_InternalLut.IsCreated() || m_InternalLut.height != lutSize)
				{
					UnityEngine.Object.DestroyImmediate(m_InternalLut);
					m_InternalLut = new RenderTexture(lutSize * lutSize, lutSize, 0, RenderTextureFormat.ARGB32)
					{
						name = "Internal LUT",
						filterMode = FilterMode.Bilinear,
						anisoLevel = 0,
						hideFlags = HideFlags.DontSave
					};
				}
				return m_InternalLut;
			}
		}

		private Texture2D curveTexture
		{
			get
			{
				if (m_CurveTexture == null)
				{
					m_CurveTexture = new Texture2D(256, 1, TextureFormat.ARGB32, false, true)
					{
						name = "Curve texture",
						wrapMode = TextureWrapMode.Clamp,
						filterMode = FilterMode.Bilinear,
						anisoLevel = 0,
						hideFlags = HideFlags.DontSave
					};
				}
				return m_CurveTexture;
			}
		}

		private Texture2D tonemapperCurve
		{
			get
			{
				if (m_TonemapperCurve == null)
				{
					TextureFormat textureFormat = TextureFormat.RGB24;
					if (SystemInfo.SupportsTextureFormat(TextureFormat.RFloat))
					{
						textureFormat = TextureFormat.RFloat;
					}
					else if (SystemInfo.SupportsTextureFormat(TextureFormat.RHalf))
					{
						textureFormat = TextureFormat.RHalf;
					}
					m_TonemapperCurve = new Texture2D(256, 1, textureFormat, false, true)
					{
						name = "Tonemapper curve texture",
						wrapMode = TextureWrapMode.Clamp,
						filterMode = FilterMode.Bilinear,
						anisoLevel = 0,
						hideFlags = HideFlags.DontSave
					};
				}
				return m_TonemapperCurve;
			}
		}

		public Shader shader
		{
			get
			{
				if (m_Shader == null)
				{
					m_Shader = Shader.Find("Hidden/TonemappingColorGrading");
				}
				return m_Shader;
			}
		}

		public Material material
		{
			get
			{
				if (m_Material == null)
				{
					m_Material = ImageEffectHelper.CheckShaderAndCreateMaterial(shader);
				}
				return m_Material;
			}
		}

		public bool isGammaColorSpace
		{
			get
			{
				return QualitySettings.activeColorSpace == ColorSpace.Gamma;
			}
		}

		public int lutSize
		{
			get
			{
				return (int)colorGrading.precision;
			}
		}

		public bool validRenderTextureFormat { get; private set; }

		public bool validUserLutSize { get; private set; }

		public void SetDirty()
		{
			m_Dirty = true;
		}

		public void SetTonemapperDirty()
		{
			m_TonemapperDirty = true;
		}

		private void OnEnable()
		{
			if (!ImageEffectHelper.IsSupported(shader, false, true, this))
			{
				base.enabled = false;
				return;
			}
			SetDirty();
			SetTonemapperDirty();
		}

		private void OnDisable()
		{
			if (m_Material != null)
			{
				UnityEngine.Object.DestroyImmediate(m_Material);
			}
			if (m_IdentityLut != null)
			{
				UnityEngine.Object.DestroyImmediate(m_IdentityLut);
			}
			if (m_InternalLut != null)
			{
				UnityEngine.Object.DestroyImmediate(internalLutRt);
			}
			if (m_SmallAdaptiveRt != null)
			{
				UnityEngine.Object.DestroyImmediate(m_SmallAdaptiveRt);
			}
			if (m_CurveTexture != null)
			{
				UnityEngine.Object.DestroyImmediate(m_CurveTexture);
			}
			if (m_TonemapperCurve != null)
			{
				UnityEngine.Object.DestroyImmediate(m_TonemapperCurve);
			}
			m_Material = null;
			m_IdentityLut = null;
			m_InternalLut = null;
			m_SmallAdaptiveRt = null;
			m_CurveTexture = null;
			m_TonemapperCurve = null;
		}

		private void OnValidate()
		{
			SetDirty();
			SetTonemapperDirty();
		}

		private static Texture2D GenerateIdentityLut(int dim)
		{
			Color[] array = new Color[dim * dim * dim];
			float num = 1f / ((float)dim - 1f);
			for (int i = 0; i < dim; i++)
			{
				for (int j = 0; j < dim; j++)
				{
					for (int k = 0; k < dim; k++)
					{
						array[i + j * dim + k * dim * dim] = new Color((float)i * num, Mathf.Abs((float)k * num), (float)j * num, 1f);
					}
				}
			}
			Texture2D texture2D = new Texture2D(dim * dim, dim, TextureFormat.RGB24, false, true);
			texture2D.name = "Identity LUT";
			texture2D.filterMode = FilterMode.Bilinear;
			texture2D.anisoLevel = 0;
			texture2D.hideFlags = HideFlags.DontSave;
			Texture2D texture2D2 = texture2D;
			texture2D2.SetPixels(array);
			texture2D2.Apply();
			return texture2D2;
		}

		private float StandardIlluminantY(float x)
		{
			return 2.87f * x - 3f * x * x - 0.27509508f;
		}

		private Vector3 CIExyToLMS(float x, float y)
		{
			float num = 1f;
			float num2 = num * x / y;
			float num3 = num * (1f - x - y) / y;
			float x2 = 0.7328f * num2 + 0.4296f * num - 0.1624f * num3;
			float y2 = -0.7036f * num2 + 1.6975f * num + 0.0061f * num3;
			float z = 0.003f * num2 + 0.0136f * num + 0.9834f * num3;
			return new Vector3(x2, y2, z);
		}

		private Vector3 GetWhiteBalance()
		{
			float temperatureShift = colorGrading.basics.temperatureShift;
			float tint = colorGrading.basics.tint;
			float x = 0.31271f - temperatureShift * ((!(temperatureShift < 0f)) ? 0.05f : 0.1f);
			float y = StandardIlluminantY(x) + tint * 0.05f;
			Vector3 vector = new Vector3(0.949237f, 1.03542f, 1.08728f);
			Vector3 vector2 = CIExyToLMS(x, y);
			return new Vector3(vector.x / vector2.x, vector.y / vector2.y, vector.z / vector2.z);
		}

		private static Color NormalizeColor(Color c)
		{
			float num = (c.r + c.g + c.b) / 3f;
			if (Mathf.Approximately(num, 0f))
			{
				return new Color(1f, 1f, 1f, 1f);
			}
			return new Color
			{
				r = c.r / num,
				g = c.g / num,
				b = c.b / num,
				a = 1f
			};
		}

		private void GenerateLiftGammaGain(out Color lift, out Color gamma, out Color gain)
		{
			Color color = NormalizeColor(colorGrading.colorWheels.shadows);
			Color color2 = NormalizeColor(colorGrading.colorWheels.midtones);
			Color color3 = NormalizeColor(colorGrading.colorWheels.highlights);
			float num = (color.r + color.g + color.b) / 3f;
			float num2 = (color2.r + color2.g + color2.b) / 3f;
			float num3 = (color3.r + color3.g + color3.b) / 3f;
			float r = (color.r - num) * 0.1f;
			float g = (color.g - num) * 0.1f;
			float b = (color.b - num) * 0.1f;
			float b2 = Mathf.Pow(2f, (color2.r - num2) * 0.5f);
			float b3 = Mathf.Pow(2f, (color2.g - num2) * 0.5f);
			float b4 = Mathf.Pow(2f, (color2.b - num2) * 0.5f);
			float r2 = Mathf.Pow(2f, (color3.r - num3) * 0.5f);
			float g2 = Mathf.Pow(2f, (color3.g - num3) * 0.5f);
			float b5 = Mathf.Pow(2f, (color3.b - num3) * 0.5f);
			float r3 = 1f / Mathf.Max(0.01f, b2);
			float g3 = 1f / Mathf.Max(0.01f, b3);
			float b6 = 1f / Mathf.Max(0.01f, b4);
			lift = new Color(r, g, b);
			gamma = new Color(r3, g3, b6);
			gain = new Color(r2, g2, b5);
		}

		private void GenCurveTexture()
		{
			AnimationCurve master = colorGrading.curves.master;
			AnimationCurve red = colorGrading.curves.red;
			AnimationCurve green = colorGrading.curves.green;
			AnimationCurve blue = colorGrading.curves.blue;
			Color[] array = new Color[256];
			for (float num = 0f; num <= 1f; num += 0.003921569f)
			{
				float a = Mathf.Clamp(master.Evaluate(num), 0f, 1f);
				float r = Mathf.Clamp(red.Evaluate(num), 0f, 1f);
				float g = Mathf.Clamp(green.Evaluate(num), 0f, 1f);
				float b = Mathf.Clamp(blue.Evaluate(num), 0f, 1f);
				array[(int)Mathf.Floor(num * 255f)] = new Color(r, g, b, a);
			}
			curveTexture.SetPixels(array);
			curveTexture.Apply();
		}

		private bool CheckUserLut()
		{
			validUserLutSize = lut.texture.height == (int)Mathf.Sqrt(lut.texture.width);
			return validUserLutSize;
		}

		private bool CheckSmallAdaptiveRt()
		{
			if (m_SmallAdaptiveRt != null)
			{
				return false;
			}
			m_AdaptiveRtFormat = RenderTextureFormat.ARGBHalf;
			if (SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.RGHalf))
			{
				m_AdaptiveRtFormat = RenderTextureFormat.RGHalf;
			}
			m_SmallAdaptiveRt = new RenderTexture(1, 1, 0, m_AdaptiveRtFormat);
			m_SmallAdaptiveRt.hideFlags = HideFlags.DontSave;
			return true;
		}

		private void OnGUI()
		{
			if (Event.current.type == EventType.Repaint)
			{
				int num = 0;
				if (m_InternalLut != null && colorGrading.enabled && colorGrading.showDebug)
				{
					Graphics.DrawTexture(new Rect(0f, num, lutSize * lutSize, lutSize), internalLutRt);
					num += lutSize;
				}
				if (m_SmallAdaptiveRt != null && eyeAdaptation.enabled && eyeAdaptation.showDebug)
				{
					m_Material.SetPass(12);
					Graphics.DrawTexture(new Rect(0f, num, 256f, 16f), m_SmallAdaptiveRt, m_Material);
				}
			}
		}

		[ImageEffectTransformsToLDR]
		private void OnRenderImage(RenderTexture source, RenderTexture destination)
		{
			material.shaderKeywords = null;
			RenderTexture renderTexture = null;
			RenderTexture[] array = null;
			if (eyeAdaptation.enabled)
			{
				bool flag = CheckSmallAdaptiveRt();
				int num = ((source.width >= source.height) ? source.height : source.width);
				int num2 = num;
				num2 |= num2 >> 1;
				num2 |= num2 >> 2;
				num2 |= num2 >> 4;
				num2 |= num2 >> 8;
				num2 |= num2 >> 16;
				num2 -= num2 >> 1;
				renderTexture = RenderTexture.GetTemporary(num2, num2, 0, m_AdaptiveRtFormat);
				Graphics.Blit(source, renderTexture);
				int num3 = (int)Mathf.Log(renderTexture.width, 2f);
				int num4 = 2;
				array = new RenderTexture[num3];
				for (int i = 0; i < num3; i++)
				{
					array[i] = RenderTexture.GetTemporary(renderTexture.width / num4, renderTexture.width / num4, 0, m_AdaptiveRtFormat);
					num4 <<= 1;
				}
				RenderTexture source2 = array[num3 - 1];
				Graphics.Blit(renderTexture, array[0], material, 1);
				for (int j = 0; j < num3 - 1; j++)
				{
					Graphics.Blit(array[j], array[j + 1]);
					source2 = array[j + 1];
				}
				m_SmallAdaptiveRt.MarkRestoreExpected();
				material.SetFloat("_AdaptationSpeed", Mathf.Max(eyeAdaptation.speed, 0.001f));

				int passeS = (!flag) ? 2 : 3;

				if (material.passCount == 1)
					passeS = 1;

                Graphics.Blit(source2, m_SmallAdaptiveRt, material, passeS);
				material.SetFloat("_MiddleGrey", eyeAdaptation.middleGrey);
				material.SetFloat("_AdaptationMin", Mathf.Pow(2f, eyeAdaptation.min));
				material.SetFloat("_AdaptationMax", Mathf.Pow(2f, eyeAdaptation.max));
				material.SetTexture("_LumTex", m_SmallAdaptiveRt);
				material.EnableKeyword("ENABLE_EYE_ADAPTATION");
			}
			int num5 = 4;
			if (tonemapping.enabled)
			{
				if (tonemapping.tonemapper == Tonemapper.Curve)
				{
					if (m_TonemapperDirty)
					{
						float num6 = 1f;
						if (tonemapping.curve.length > 0)
						{
							num6 = tonemapping.curve[tonemapping.curve.length - 1].time;
							for (float num7 = 0f; num7 <= 1f; num7 += 0.003921569f)
							{
								float num8 = tonemapping.curve.Evaluate(num7 * num6);
								tonemapperCurve.SetPixel(Mathf.FloorToInt(num7 * 255f), 0, new Color(num8, num8, num8));
							}
							tonemapperCurve.Apply();
						}
						m_TonemapperCurveRange = 1f / num6;
						m_TonemapperDirty = false;
					}
					material.SetFloat("_ToneCurveRange", m_TonemapperCurveRange);
					material.SetTexture("_ToneCurve", tonemapperCurve);
				}
				else if (tonemapping.tonemapper == Tonemapper.Neutral)
				{
					float num9 = tonemapping.neutralBlackIn * 20f + 1f;
					float num10 = tonemapping.neutralBlackOut * 10f + 1f;
					float num11 = tonemapping.neutralWhiteIn / 20f;
					float num12 = 1f - tonemapping.neutralWhiteOut / 20f;
					float t = num9 / num10;
					float t2 = num11 / num12;
					float y = Mathf.Max(0f, Mathf.LerpUnclamped(0.57f, 0.37f, t));
					float z = Mathf.LerpUnclamped(0.01f, 0.24f, t2);
					float w = Mathf.Max(0f, Mathf.LerpUnclamped(0.02f, 0.2f, t));
					material.SetVector("_NeutralTonemapperParams1", new Vector4(0.2f, y, z, w));
					material.SetVector("_NeutralTonemapperParams2", new Vector4(0.02f, 0.3f, tonemapping.neutralWhiteLevel, tonemapping.neutralWhiteClip / 10f));
				}
				material.SetFloat("_Exposure", tonemapping.exposure);
				num5 = (int)(num5 + (tonemapping.tonemapper + 1));
			}
			if (colorGrading.enabled)
			{
				if (m_Dirty || !m_InternalLut.IsCreated())
				{
					Color lift;
					Color gamma;
					Color gain;
					GenerateLiftGammaGain(out lift, out gamma, out gain);
					GenCurveTexture();
					material.SetVector("_WhiteBalance", GetWhiteBalance());
					material.SetVector("_Lift", lift);
					material.SetVector("_Gamma", gamma);
					material.SetVector("_Gain", gain);
					material.SetVector("_ContrastGainGamma", new Vector3(colorGrading.basics.contrast, colorGrading.basics.gain, 1f / colorGrading.basics.gamma));
					material.SetFloat("_Vibrance", colorGrading.basics.vibrance);
					material.SetVector("_HSV", new Vector4(colorGrading.basics.hue, colorGrading.basics.saturation, colorGrading.basics.value));
					material.SetVector("_ChannelMixerRed", colorGrading.channelMixer.channels[0]);
					material.SetVector("_ChannelMixerGreen", colorGrading.channelMixer.channels[1]);
					material.SetVector("_ChannelMixerBlue", colorGrading.channelMixer.channels[2]);
					material.SetTexture("_CurveTex", curveTexture);
					internalLutRt.MarkRestoreExpected();

					int passeS = 0;

					if (material.passCount == 1)
						passeS = 1;

					Graphics.Blit(identityLut, internalLutRt, material, passeS);
					m_Dirty = false;
				}
				material.EnableKeyword("ENABLE_COLOR_GRADING");
				if (colorGrading.useDithering)
				{
					material.EnableKeyword("ENABLE_DITHERING");
				}
				material.SetTexture("_InternalLutTex", internalLutRt);
				material.SetVector("_InternalLutParams", new Vector3(1f / (float)internalLutRt.width, 1f / (float)internalLutRt.height, (float)internalLutRt.height - 1f));
			}
			if (lut.enabled && lut.texture != null && CheckUserLut())
			{
				material.SetTexture("_UserLutTex", lut.texture);
				material.SetVector("_UserLutParams", new Vector4(1f / (float)lut.texture.width, 1f / (float)lut.texture.height, (float)lut.texture.height - 1f, lut.contribution));
				material.EnableKeyword("ENABLE_USER_LUT");
			}

			int passes = num5;

			if (material.passCount == 1)
				passes = 1;

            Graphics.Blit(source, destination, material, passes);
			if (eyeAdaptation.enabled)
			{
				for (int k = 0; k < array.Length; k++)
				{
					RenderTexture.ReleaseTemporary(array[k]);
				}
				RenderTexture.ReleaseTemporary(renderTexture);
			}
		}

		public Texture2D BakeLUT()
		{
			Texture2D texture2D = new Texture2D(internalLutRt.width, internalLutRt.height, TextureFormat.RGB24, false, true);
			RenderTexture.active = internalLutRt;
			texture2D.ReadPixels(new Rect(0f, 0f, texture2D.width, texture2D.height), 0, 0);
			RenderTexture.active = null;
			return texture2D;
		}
	}
}
