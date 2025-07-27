using UnityEngine;

[AddComponentMenu("Camera Filter Pack/Old Film/Cutting 1")]
[ExecuteInEditMode]
public class CameraFilterPack_OldFilm_Cutting1 : MonoBehaviour
{
	public Shader SCShader;

	private float TimeX = 1f;

	[Range(0f, 10f)]
	public float Speed = 1f;

	[Range(0f, 2f)]
	public float Luminosity = 1.5f;

	[Range(0f, 1f)]
	public float Vignette = 1f;

	[Range(0f, 2f)]
	public float Negative;

	private Material SCMaterial;

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
		Texture2 = Resources.Load("CameraFilterPack_OldFilm1") as Texture2D;
		SCShader = Shader.Find("CameraFilterPack/OldFilm_Cutting1");
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
			material.SetFloat("_Value", Luminosity);
			material.SetFloat("_Value2", 1f - Vignette);
			material.SetFloat("_Value3", Negative);
			material.SetFloat("_Speed", Speed);
			material.SetTexture("_MainTex2", Texture2);
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
