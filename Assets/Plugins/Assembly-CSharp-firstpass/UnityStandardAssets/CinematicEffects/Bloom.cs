using System;
using UnityEngine;

namespace UnityStandardAssets.CinematicEffects
{
	[ExecuteInEditMode]
	[RequireComponent(typeof(Camera))]
	[AddComponentMenu("Image Effects/Cinematic/Bloom")]
	[ImageEffectAllowedInSceneView]
	public class Bloom : MonoBehaviour
	{
		[Serializable]
		public struct Settings
		{
			[SerializeField]
			[Tooltip("Filters out pixels under this level of brightness.")]
			public float threshold;

			[SerializeField]
			[Range(0f, 1f)]
			[Tooltip("Makes transition between under/over-threshold gradual.")]
			public float softKnee;

			[SerializeField]
			[Range(1f, 7f)]
			[Tooltip("Changes extent of veiling effects in a screen resolution-independent fashion.")]
			public float radius;

			[SerializeField]
			[Tooltip("Blend factor of the result image.")]
			public float intensity;

			[SerializeField]
			[Tooltip("Controls filter quality and buffer resolution.")]
			public bool highQuality;

			[SerializeField]
			[Tooltip("Reduces flashing noise with an additional filter.")]
			public bool antiFlicker;

			public float thresholdGamma
			{
				get
				{
					return Mathf.Max(0f, threshold);
				}
				set
				{
					threshold = value;
				}
			}

			public float thresholdLinear
			{
				get
				{
					return Mathf.GammaToLinearSpace(thresholdGamma);
				}
				set
				{
					threshold = Mathf.LinearToGammaSpace(value);
				}
			}

			public static Settings defaultSettings
			{
				get
				{
					return new Settings
					{
						threshold = 0.9f,
						softKnee = 0.5f,
						radius = 2f,
						intensity = 0.7f,
						highQuality = true,
						antiFlicker = false
					};
				}
			}
		}

		[SerializeField]
		public Settings settings = Settings.defaultSettings;

		[SerializeField]
		[HideInInspector]
		private Shader m_Shader;

		private Material m_Material;

		private const int kMaxIterations = 16;

		private RenderTexture[] m_blurBuffer1 = new RenderTexture[16];

		private RenderTexture[] m_blurBuffer2 = new RenderTexture[16];

		public Shader shader
		{
			get
			{
				if (m_Shader == null)
				{
					m_Shader = Shader.Find("Hidden/Image Effects/Cinematic/Bloom");
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

		private void OnEnable()
		{
			if (!ImageEffectHelper.IsSupported(shader, true, false, this))
			{
				base.enabled = false;
			}
		}

		private void OnDisable()
		{
			if (m_Material != null)
			{
				UnityEngine.Object.DestroyImmediate(m_Material);
			}
			m_Material = null;
		}

		private void OnRenderImage(RenderTexture source, RenderTexture destination)
		{
			bool isMobilePlatform = Application.isMobilePlatform;
			int num = source.width;
			int num2 = source.height;
			if (!settings.highQuality)
			{
				num /= 2;
				num2 /= 2;
			}
			RenderTextureFormat format = ((!isMobilePlatform) ? RenderTextureFormat.DefaultHDR : RenderTextureFormat.Default);
			float num3 = Mathf.Log(num2, 2f) + settings.radius - 8f;
			int num4 = (int)num3;
			int num5 = Mathf.Clamp(num4, 1, 16);
			float thresholdLinear = settings.thresholdLinear;
			material.SetFloat("_Threshold", thresholdLinear);
			float num6 = thresholdLinear * settings.softKnee + 1E-05f;
			Vector3 vector = new Vector3(thresholdLinear - num6, num6 * 2f, 0.25f / num6);
			material.SetVector("_Curve", vector);
			bool flag = !settings.highQuality && settings.antiFlicker;
			material.SetFloat("_PrefilterOffs", (!flag) ? 0f : (-0.5f));
			material.SetFloat("_SampleScale", 0.5f + num3 - (float)num4);
			material.SetFloat("_Intensity", Mathf.Max(0f, settings.intensity));
			RenderTexture temporary = RenderTexture.GetTemporary(num, num2, 0, format);
			Graphics.Blit(source, temporary, material, settings.antiFlicker ? 1 : 0);
			RenderTexture renderTexture = temporary;
			for (int i = 0; i < num5; i++)
			{
				m_blurBuffer1[i] = RenderTexture.GetTemporary(renderTexture.width / 2, renderTexture.height / 2, 0, format);

				int passes = (i != 0) ? 4 : ((!settings.antiFlicker) ? 2 : 3);

				if (material.passCount == 1)
					passes = 1;

                Graphics.Blit(renderTexture, m_blurBuffer1[i], material, passes);
				renderTexture = m_blurBuffer1[i];
			}
			for (int num7 = num5 - 2; num7 >= 0; num7--)
			{
				RenderTexture renderTexture2 = m_blurBuffer1[num7];
				material.SetTexture("_BaseTex", renderTexture2);
				m_blurBuffer2[num7] = RenderTexture.GetTemporary(renderTexture2.width, renderTexture2.height, 0, format);

				int passes = (!settings.highQuality) ? 5 : 6;

				if (material.passCount == 1)
					passes = 1;

                Graphics.Blit(renderTexture, m_blurBuffer2[num7], material, passes);
				renderTexture = m_blurBuffer2[num7];
			}
			material.SetTexture("_BaseTex", source);

			int passeS = (!settings.highQuality) ? 7 : 8;

			if (material.passCount == 1)
				passeS = 1;

            Graphics.Blit(renderTexture, destination, material, passeS);
			for (int j = 0; j < 16; j++)
			{
				if (m_blurBuffer1[j] != null)
				{
					RenderTexture.ReleaseTemporary(m_blurBuffer1[j]);
				}
				if (m_blurBuffer2[j] != null)
				{
					RenderTexture.ReleaseTemporary(m_blurBuffer2[j]);
				}
				m_blurBuffer1[j] = null;
				m_blurBuffer2[j] = null;
			}
			RenderTexture.ReleaseTemporary(temporary);
		}
	}
}
