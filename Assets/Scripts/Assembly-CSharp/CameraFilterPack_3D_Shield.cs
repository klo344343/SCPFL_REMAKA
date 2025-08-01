using UnityEngine;

[AddComponentMenu("Camera Filter Pack/3D/Shield")]
[ExecuteInEditMode]
public class CameraFilterPack_3D_Shield : MonoBehaviour
{
	public Shader SCShader;

	public bool _Visualize;

	private float TimeX = 1f;

	private Material SCMaterial;

	[Range(0f, 100f)]
	public float _FixDistance = 1.5f;

	[Range(-0.99f, 0.99f)]
	public float _Distance = 0.4f;

	[Range(0f, 0.5f)]
	public float _Size = 0.5f;

	[Range(0f, 1f)]
	public float _FadeShield = 0.75f;

	[Range(-0.2f, 0.2f)]
	public float LightIntensity = 0.025f;

	public bool AutoAnimatedNear;

	[Range(-5f, 5f)]
	public float AutoAnimatedNearSpeed = 0.5f;

	[Range(0f, 10f)]
	public float Speed = 0.2f;

	[Range(0f, 10f)]
	public float Speed_X = 0.2f;

	[Range(0f, 1f)]
	public float Speed_Y = 0.3f;

	[Range(0f, 10f)]
	public float Intensity = 2.4f;

	public static Color ChangeColorRGB;

	private Material material
	{
		get
		{
			if (SCMaterial == null)
			{
				SCMaterial = new Material(SCShader);
				SCMaterial.hideFlags = HideFlags.HideAndDontSave;
			}
			return SCMaterial;
		}
	}

	private void Start()
	{
		SCShader = Shader.Find("CameraFilterPack/3D_Shield");
		if (!SystemInfo.supportsImageEffects)
		{
			base.enabled = false;
		}
	}

	private void OnRenderImage(RenderTexture sourceTexture, RenderTexture destTexture)
	{
		if (SCShader != null)
		{
			TimeX += Time.deltaTime;
			if (TimeX > 100f)
			{
				TimeX = 0f;
			}
			material.SetFloat("_TimeX", TimeX);
			if (AutoAnimatedNear)
			{
				_Distance += Time.deltaTime * AutoAnimatedNearSpeed;
				if (_Distance > 1f)
				{
					_Distance = -1f;
				}
				if (_Distance < -1f)
				{
					_Distance = 1f;
				}
				material.SetFloat("_Near", _Distance);
			}
			else
			{
				material.SetFloat("_Near", _Distance);
			}
			material.SetFloat("_Far", _Size);
			material.SetFloat("_FixDistance", _FixDistance);
			material.SetFloat("_LightIntensity", LightIntensity * 64f);
			material.SetFloat("_Visualize", _Visualize ? 1 : 0);
			material.SetFloat("_FadeShield", _FadeShield);
			material.SetFloat("_Value", Speed);
			material.SetFloat("_Value2", Speed_X);
			material.SetFloat("_Value3", Speed_Y);
			material.SetFloat("_Value4", Intensity);
			float farClipPlane = GetComponent<Camera>().farClipPlane;
			material.SetFloat("_FarCamera", 1000f / farClipPlane);
			material.SetVector("_ScreenResolution", new Vector4(sourceTexture.width, sourceTexture.height, 0f, 0f));
			GetComponent<Camera>().depthTextureMode = DepthTextureMode.Depth;
			Graphics.Blit(sourceTexture, destTexture, material);
		}
		else
		{
			Graphics.Blit(sourceTexture, destTexture);
		}
	}

	private void Update()
	{
	}

	private void OnDisable()
	{
		if ((bool)SCMaterial)
		{
			Object.DestroyImmediate(SCMaterial);
		}
	}
}
