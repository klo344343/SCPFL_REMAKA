using UnityEngine;

[ExecuteInEditMode]
[AddComponentMenu("Camera Filter Pack/Broken/Simple")]
public class CameraFilterPack_Broken_Simple : MonoBehaviour
{
	public Shader SCShader;

	private float TimeX = 1f;

	private Material SCMaterial;

	[Range(0f, 1f)]
	public float __Speed = 1f;

	[Range(0f, 1f)]
	public float _Broke1 = 1f;

	[Range(0f, 1f)]
	public float _Broke2 = 1f;

	[Range(0f, 1f)]
	public float _PosX = 0.5f;

	[Range(0f, 1f)]
	public float _PosY = 0.5f;

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
		SCShader = Shader.Find("CameraFilterPack/CameraFilterPack_Broken_Simple");
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
			material.SetFloat("_Speed", __Speed);
			material.SetFloat("Broke1", _Broke1);
			material.SetFloat("Broke2", _Broke2);
			material.SetFloat("PosX", _PosX);
			material.SetFloat("PosY", _PosY);
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
