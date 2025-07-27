using Mirror;
using System;
using System.Collections.Generic;
using UnityEngine.Rendering;

namespace UnityEngine.PostProcessing
{
	[ImageEffectAllowedInSceneView]
	[RequireComponent(typeof(Camera))]
	[DisallowMultipleComponent]
	[ExecuteInEditMode]
	[AddComponentMenu("Effects/Post-Processing Behaviour", -1)]
	public class PostProcessingBehaviour : MonoBehaviour
	{
		public Func<Vector2, Matrix4x4> jitteredMatrixFunc;

		private AmbientOcclusionComponent m_AmbientOcclusion;

		private BloomComponent m_Bloom;

		private Camera m_Camera;

		private ChromaticAberrationComponent m_ChromaticAberration;

		private ColorGradingComponent m_ColorGrading;

		private Dictionary<Type, KeyValuePair<CameraEvent, CommandBuffer>> m_CommandBuffers;

		private List<PostProcessingComponentBase> m_Components;

		private Dictionary<PostProcessingComponentBase, bool> m_ComponentStates;

		private PostProcessingContext m_Context;

		private BuiltinDebugViewsComponent m_DebugViews;

		private DepthOfFieldComponent m_DepthOfField;

		private DitheringComponent m_Dithering;

		private EyeAdaptationComponent m_EyeAdaptation;

		private FogComponent m_FogComponent;

		private FxaaComponent m_Fxaa;

		private GrainComponent m_Grain;

		private MaterialFactory m_MaterialFactory;

		private MotionBlurComponent m_MotionBlur;

		private PostProcessingProfile m_PreviousProfile;

		private bool m_RenderingInSceneView;

		private RenderTextureFactory m_RenderTextureFactory;

		private ScreenSpaceReflectionComponent m_ScreenSpaceReflection;

		private TaaComponent m_Taa;

		private UserLutComponent m_UserLut;

		private VignetteComponent m_Vignette;

		public PostProcessingProfile profile;

		private readonly List<PostProcessingComponentBase> m_ComponentsToEnable = new List<PostProcessingComponentBase>();

		private readonly List<PostProcessingComponentBase> m_ComponentsToDisable = new List<PostProcessingComponentBase>();

		private void OnEnable()
		{
			m_CommandBuffers = new Dictionary<Type, KeyValuePair<CameraEvent, CommandBuffer>>();
			m_MaterialFactory = new MaterialFactory();
			m_RenderTextureFactory = new RenderTextureFactory();
			m_Context = new PostProcessingContext();
			m_Components = new List<PostProcessingComponentBase>();
			m_DebugViews = AddComponent(new BuiltinDebugViewsComponent());
			m_AmbientOcclusion = AddComponent(new AmbientOcclusionComponent());
			m_ScreenSpaceReflection = AddComponent(new ScreenSpaceReflectionComponent());
			m_FogComponent = AddComponent(new FogComponent());
			m_MotionBlur = AddComponent(new MotionBlurComponent());
			m_Taa = AddComponent(new TaaComponent());
			m_EyeAdaptation = AddComponent(new EyeAdaptationComponent());
			m_DepthOfField = AddComponent(new DepthOfFieldComponent());
			m_Bloom = AddComponent(new BloomComponent());
			m_ChromaticAberration = AddComponent(new ChromaticAberrationComponent());
			m_ColorGrading = AddComponent(new ColorGradingComponent());
			m_UserLut = AddComponent(new UserLutComponent());
			m_Grain = AddComponent(new GrainComponent());
			m_Vignette = AddComponent(new VignetteComponent());
			m_Dithering = AddComponent(new DitheringComponent());
			m_Fxaa = AddComponent(new FxaaComponent());
			m_ComponentStates = new Dictionary<PostProcessingComponentBase, bool>();
			foreach (PostProcessingComponentBase component in m_Components)
			{
				m_ComponentStates.Add(component, false);
			}
			base.useGUILayout = false;
		}

		private void Start()
		{
			NetworkIdentity networkIdentity = GetComponent<NetworkIdentity>();
			if (networkIdentity == null)
			{
				networkIdentity = GetComponentInChildren<NetworkIdentity>();
			}
			if (networkIdentity != null && !networkIdentity.isLocalPlayer)
			{
				Object.Destroy(this);
			}
		}

		private void OnPreCull()
		{
			m_Camera = GetComponent<Camera>();
			if (profile == null || m_Camera == null)
			{
				return;
			}
			PostProcessingContext postProcessingContext = m_Context.Reset();
			postProcessingContext.profile = profile;
			postProcessingContext.renderTextureFactory = m_RenderTextureFactory;
			postProcessingContext.materialFactory = m_MaterialFactory;
			postProcessingContext.camera = m_Camera;
			m_DebugViews.Init(postProcessingContext, profile.debugViews);
			m_AmbientOcclusion.Init(postProcessingContext, profile.ambientOcclusion);
			m_ScreenSpaceReflection.Init(postProcessingContext, profile.screenSpaceReflection);
			m_FogComponent.Init(postProcessingContext, profile.fog);
			m_MotionBlur.Init(postProcessingContext, profile.motionBlur);
			m_Taa.Init(postProcessingContext, profile.antialiasing);
			m_EyeAdaptation.Init(postProcessingContext, profile.eyeAdaptation);
			m_DepthOfField.Init(postProcessingContext, profile.depthOfField);
			m_Bloom.Init(postProcessingContext, profile.bloom);
			m_ChromaticAberration.Init(postProcessingContext, profile.chromaticAberration);
			m_ColorGrading.Init(postProcessingContext, profile.colorGrading);
			m_UserLut.Init(postProcessingContext, profile.userLut);
			m_Grain.Init(postProcessingContext, profile.grain);
			m_Vignette.Init(postProcessingContext, profile.vignette);
			m_Dithering.Init(postProcessingContext, profile.dithering);
			m_Fxaa.Init(postProcessingContext, profile.antialiasing);
			if (m_PreviousProfile != profile)
			{
				DisableComponents();
				m_PreviousProfile = profile;
			}
			CheckObservers();
			DepthTextureMode depthTextureMode = postProcessingContext.camera.depthTextureMode;
			foreach (PostProcessingComponentBase component in m_Components)
			{
				if (component.active)
				{
					depthTextureMode |= component.GetCameraFlags();
				}
			}
			postProcessingContext.camera.depthTextureMode = depthTextureMode;
			if (!m_RenderingInSceneView && m_Taa.active && !profile.debugViews.willInterrupt)
			{
				m_Taa.SetProjectionMatrix(jitteredMatrixFunc);
			}
		}

		private void OnPreRender()
		{
			if (!(profile == null))
			{
				TryExecuteCommandBuffer(m_DebugViews);
				TryExecuteCommandBuffer(m_AmbientOcclusion);
				TryExecuteCommandBuffer(m_ScreenSpaceReflection);
				TryExecuteCommandBuffer(m_FogComponent);
				if (!m_RenderingInSceneView)
				{
					TryExecuteCommandBuffer(m_MotionBlur);
				}
			}
		}

		private void OnPostRender()
		{
			if (!(profile == null) && !(m_Camera == null) && !m_RenderingInSceneView && m_Taa.active && !profile.debugViews.willInterrupt)
			{
				m_Context.camera.ResetProjectionMatrix();
			}
		}

        private void OnRenderImage(RenderTexture source, RenderTexture destination)
        {
            if (profile == null || m_Camera == null)
            {
                Graphics.Blit(source, destination);
                return;
            }

            bool effectsApplied = false;
            bool fxaaActive = m_Fxaa.active;
            bool taaActive = m_Taa.active && !m_RenderingInSceneView;
            bool dofActive = m_DepthOfField.active && !m_RenderingInSceneView;

            Material uberMaterial = m_MaterialFactory.Get("Hidden/Post FX/Uber Shader");
            uberMaterial.shaderKeywords = null;

            RenderTexture currentRT = source;

            if (taaActive)
            {
                RenderTexture taaRT = m_RenderTextureFactory.Get(currentRT);
                m_Taa.Render(currentRT, taaRT); // Убрали параметр defaultPass
                currentRT = taaRT;
            }

            Texture autoExposure = GraphicsUtils.whiteTexture;
            if (m_EyeAdaptation.active)
            {
                effectsApplied = true;
                autoExposure = m_EyeAdaptation.Prepare(currentRT, uberMaterial);
            }
            uberMaterial.SetTexture("_AutoExposure", autoExposure);

            if (dofActive)
            {
                effectsApplied = true;
                m_DepthOfField.Prepare(currentRT, uberMaterial, taaActive,
                                      m_Taa.jitterVector,
                                      m_Taa.model.settings.taaSettings.motionBlending);
            }

            if (m_Bloom.active)
            {
                effectsApplied = true;
                m_Bloom.Prepare(currentRT, uberMaterial, autoExposure);
            }

            effectsApplied |= TryPrepareUberImageEffect(m_ChromaticAberration, uberMaterial);
            effectsApplied |= TryPrepareUberImageEffect(m_ColorGrading, uberMaterial);
            effectsApplied |= TryPrepareUberImageEffect(m_Vignette, uberMaterial);
            effectsApplied |= TryPrepareUberImageEffect(m_UserLut, uberMaterial);

            Material fxaaMaterial = fxaaActive ? m_MaterialFactory.Get("Hidden/Post FX/FXAA") : null;
            if (fxaaActive)
            {
                fxaaMaterial.shaderKeywords = null;
                TryPrepareUberImageEffect(m_Grain, fxaaMaterial);
                TryPrepareUberImageEffect(m_Dithering, fxaaMaterial);

                if (effectsApplied)
                {
                    RenderTexture tempRT = m_RenderTextureFactory.Get(currentRT);
                    Graphics.Blit(currentRT, tempRT, uberMaterial, 0); // Используем проход 0
                    currentRT = tempRT;
                }

                m_Fxaa.Render(currentRT, destination); // Убрали параметр defaultPass
            }
            else
            {
                effectsApplied |= TryPrepareUberImageEffect(m_Grain, uberMaterial);
                effectsApplied |= TryPrepareUberImageEffect(m_Dithering, uberMaterial);

                if (effectsApplied)
                {
                    if (!GraphicsUtils.isLinearColorSpace)
                        uberMaterial.EnableKeyword("UNITY_COLORSPACE_GAMMA");

                    Graphics.Blit(currentRT, destination, uberMaterial, 0);
                }
            }

            if (!effectsApplied && !fxaaActive)
            {
                Graphics.Blit(currentRT, destination);
            }

            m_RenderTextureFactory.ReleaseAll();
        }

        private void OnGUI()
		{
			if (Event.current.type == EventType.Repaint && !(profile == null) && !(m_Camera == null))
			{
				if (m_EyeAdaptation.active && profile.debugViews.IsModeActive(BuiltinDebugViewsModel.Mode.EyeAdaptation))
				{
					m_EyeAdaptation.OnGUI();
				}
				else if (m_ColorGrading.active && profile.debugViews.IsModeActive(BuiltinDebugViewsModel.Mode.LogLut))
				{
					m_ColorGrading.OnGUI();
				}
				else if (m_UserLut.active && profile.debugViews.IsModeActive(BuiltinDebugViewsModel.Mode.UserLut))
				{
					m_UserLut.OnGUI();
				}
			}
		}

		private void OnDisable()
		{
			foreach (KeyValuePair<CameraEvent, CommandBuffer> value in m_CommandBuffers.Values)
			{
				m_Camera.RemoveCommandBuffer(value.Key, value.Value);
				value.Value.Dispose();
			}
			m_CommandBuffers.Clear();
			if (profile != null)
			{
				DisableComponents();
			}
			m_Components.Clear();
			m_MaterialFactory.Dispose();
			m_RenderTextureFactory.Dispose();
			GraphicsUtils.Dispose();
		}

		public void ResetTemporalEffects()
		{
			m_Taa.ResetHistory();
			m_MotionBlur.ResetHistory();
			m_EyeAdaptation.ResetHistory();
		}

		private void CheckObservers()
		{
			foreach (KeyValuePair<PostProcessingComponentBase, bool> componentState in m_ComponentStates)
			{
				PostProcessingComponentBase key = componentState.Key;
				bool flag = key.GetModel().enabled;
				if (flag != componentState.Value)
				{
					if (flag)
					{
						m_ComponentsToEnable.Add(key);
					}
					else
					{
						m_ComponentsToDisable.Add(key);
					}
				}
			}
			for (int i = 0; i < m_ComponentsToDisable.Count; i++)
			{
				PostProcessingComponentBase postProcessingComponentBase = m_ComponentsToDisable[i];
				m_ComponentStates[postProcessingComponentBase] = false;
				postProcessingComponentBase.OnDisable();
			}
			for (int j = 0; j < m_ComponentsToEnable.Count; j++)
			{
				PostProcessingComponentBase postProcessingComponentBase2 = m_ComponentsToEnable[j];
				m_ComponentStates[postProcessingComponentBase2] = true;
				postProcessingComponentBase2.OnEnable();
			}
			m_ComponentsToDisable.Clear();
			m_ComponentsToEnable.Clear();
		}

		private void DisableComponents()
		{
			foreach (PostProcessingComponentBase component in m_Components)
			{
				PostProcessingModel model = component.GetModel();
				if (model != null && model.enabled)
				{
					component.OnDisable();
				}
			}
		}

		private CommandBuffer AddCommandBuffer<T>(CameraEvent evt, string name) where T : PostProcessingModel
		{
			CommandBuffer commandBuffer = new CommandBuffer();
			commandBuffer.name = name;
			CommandBuffer value = commandBuffer;
			KeyValuePair<CameraEvent, CommandBuffer> value2 = new KeyValuePair<CameraEvent, CommandBuffer>(evt, value);
			m_CommandBuffers.Add(typeof(T), value2);
			m_Camera.AddCommandBuffer(evt, value2.Value);
			return value2.Value;
		}

		private void RemoveCommandBuffer<T>() where T : PostProcessingModel
		{
			Type typeFromHandle = typeof(T);
			KeyValuePair<CameraEvent, CommandBuffer> value;
			if (m_CommandBuffers.TryGetValue(typeFromHandle, out value))
			{
				m_Camera.RemoveCommandBuffer(value.Key, value.Value);
				m_CommandBuffers.Remove(typeFromHandle);
				value.Value.Dispose();
			}
		}

		private CommandBuffer GetCommandBuffer<T>(CameraEvent evt, string name) where T : PostProcessingModel
		{
			KeyValuePair<CameraEvent, CommandBuffer> value;
			if (!m_CommandBuffers.TryGetValue(typeof(T), out value))
			{
				return AddCommandBuffer<T>(evt, name);
			}
			if (value.Key != evt)
			{
				RemoveCommandBuffer<T>();
				return AddCommandBuffer<T>(evt, name);
			}
			return value.Value;
		}

		private void TryExecuteCommandBuffer<T>(PostProcessingComponentCommandBuffer<T> component) where T : PostProcessingModel
		{
			if (component.active)
			{
				CommandBuffer commandBuffer = GetCommandBuffer<T>(component.GetCameraEvent(), component.GetName());
				commandBuffer.Clear();
				component.PopulateCommandBuffer(commandBuffer);
			}
			else
			{
				RemoveCommandBuffer<T>();
			}
		}

		private bool TryPrepareUberImageEffect<T>(PostProcessingComponentRenderTexture<T> component, Material material) where T : PostProcessingModel
		{
			if (!component.active)
			{
				return false;
			}
			component.Prepare(material);
			return true;
		}

		private T AddComponent<T>(T component) where T : PostProcessingComponentBase
		{
			m_Components.Add(component);
			return component;
		}
	}
}
