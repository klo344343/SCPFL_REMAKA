using UnityEngine;

[ExecuteInEditMode]
[AddComponentMenu("Camera Filter Pack/3D/Matrix")]
public class CameraFilterPack_3D_Matrix : MonoBehaviour
{
	public Shader SCShader;

	private float TimeX = 1f;

	private Material SCMaterial;

	public bool _Visualize;

	[Range(0f, 100f)]
	public float _FixDistance = 1f;

	[Range(-5f, 5f)]
	public float LightIntensity = 1f;

	[Range(0f, 6f)]
	public float MatrixSize = 1f;

	[Range(-4f, 4f)]
	public float MatrixSpeed = 1f;

	[Range(0f, 1f)]
	public float Fade = 1f;

	public Color _MatrixColor = new Color(0f, 1f, 0f, 1f);

	public static Color ChangeColorRGB;

	private Texture2D Texture2;

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
		Texture2 = Resources.Load("CameraFilterPack_3D_Matrix1") as Texture2D;
		SCShader = Shader.Find("CameraFilterPack/3D_Matrix");
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
			material.SetFloat("_DepthLevel", Fade);
			material.SetFloat("_FixDistance", _FixDistance);
			material.SetFloat("_MatrixSize", MatrixSize);
			material.SetColor("_MatrixColor", _MatrixColor);
			material.SetFloat("_MatrixSpeed", MatrixSpeed * 2f);
			material.SetFloat("_Visualize", _Visualize ? 1 : 0);
			material.SetFloat("_LightIntensity", LightIntensity);
			material.SetTexture("_MainTex2", Texture2);
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
