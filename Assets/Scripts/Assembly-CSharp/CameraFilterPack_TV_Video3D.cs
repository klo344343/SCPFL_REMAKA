using UnityEngine;

[AddComponentMenu("Camera Filter Pack/TV/Video3D")]
[ExecuteInEditMode]
public class CameraFilterPack_TV_Video3D : MonoBehaviour
{
	public Shader SCShader;

	private float TimeX = 1f;

	private Material SCMaterial;

	private Material material
	{
		get
		{
			if (SCMaterial == null)
			{
				SCMaterial = new Material(SCShader)
				{
					hideFlags = HideFlags.HideAndDontSave
				};
			}
			return SCMaterial;
		}
	}

	private void Start()
	{
		SCShader = Shader.Find("CameraFilterPack/TV_Video3D");
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
