using UnityEngine;

[ExecuteInEditMode]
[AddComponentMenu("Camera Filter Pack/Distortion/Dissipation")]
public class CameraFilterPack_Distortion_Dissipation : MonoBehaviour
{
	public Shader SCShader;

	private float TimeX = 1f;

	private Material SCMaterial;

	[Range(0f, 2.99f)]
	public float Dissipation = 1f;

	[Range(0f, 16f)]
	private readonly float Colors = 11f;

	[Range(-1f, 1f)]
	private readonly float Green_Mod = 1f;

	[Range(0f, 10f)]
	private readonly float Value4 = 1f;

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
		SCShader = Shader.Find("CameraFilterPack/Distortion_Dissipation");
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
			material.SetFloat("_Value", Dissipation);
			material.SetFloat("_Value2", Colors);
			material.SetFloat("_Value3", Green_Mod);
			material.SetFloat("_Value4", Value4);
			material.SetVector("_ScreenResolution", new Vector4(sourceTexture.width, sourceTexture.height, 0f, 0f));
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
