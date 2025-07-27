using System.Runtime.CompilerServices;
using UnityEngine;

namespace UnityEngine.PostProcessing
{
    public static class GraphicsUtils
    {
        private static Texture2D s_WhiteTexture;
        private static Mesh s_Quad;

        public static bool isLinearColorSpace => QualitySettings.activeColorSpace == ColorSpace.Linear;

        public static bool supportsDX11 => SystemInfo.graphicsShaderLevel >= 50 && SystemInfo.supportsComputeShaders;

        public static Texture2D whiteTexture
        {
            get
            {
                if (s_WhiteTexture == null)
                {
                    s_WhiteTexture = new Texture2D(1, 1, TextureFormat.ARGB32, false);
                    s_WhiteTexture.SetPixel(0, 0, Color.white);
                    s_WhiteTexture.Apply();
                }
                return s_WhiteTexture;
            }
        }

        public static Mesh quad
        {
            get
            {
                if (s_Quad == null)
                {
                    s_Quad = CreateQuadMesh();
                }
                return s_Quad;
            }
        }

        private static Mesh CreateQuadMesh()
        {
            var mesh = new Mesh
            {
                vertices = new[]
                {
                    new Vector3(-1f, -1f, 0f),
                    new Vector3(1f, 1f, 0f),
                    new Vector3(1f, -1f, 0f),
                    new Vector3(-1f, 1f, 0f)
                },
                uv = new[]
                {
                    new Vector2(0f, 0f),
                    new Vector2(1f, 1f),
                    new Vector2(1f, 0f),
                    new Vector2(0f, 1f)
                },
                triangles = new[] { 0, 1, 2, 1, 0, 3 }
            };

            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        public static void Blit(Material material, int pass)
        {
            GL.PushMatrix();
            GL.LoadOrtho();
            material.SetPass(pass);
            GL.Begin(GL.QUADS);
            GL.TexCoord2(0f, 0f);
            GL.Vertex3(0f, 0f, 0.1f);
            GL.TexCoord2(1f, 0f);
            GL.Vertex3(1f, 0f, 0.1f);
            GL.TexCoord2(0f, 1f);
            GL.Vertex3(0f, 1f, 0.1f);
            GL.TexCoord2(1f, 1f);
            GL.Vertex3(1f, 1f, 0.1f);
            GL.End();
            GL.PopMatrix();
        }

        public static void ClearAndBlit(Texture source, RenderTexture destination, Material material, int pass, bool clearColor = true, bool clearDepth = false)
        {
            var activeRT = RenderTexture.active;
            RenderTexture.active = destination;
            GL.Clear(false, clearColor, Color.clear);
            GL.PushMatrix();
            GL.LoadOrtho();

            material.SetTexture("_MainTex", source);
            material.SetPass(pass);

            GL.Begin(GL.QUADS);
            GL.TexCoord2(0f, 0f);
            GL.Vertex3(0f, 0f, 0.1f);
            GL.TexCoord2(1f, 0f);
            GL.Vertex3(1f, 0f, 0.1f);
            GL.TexCoord2(0f, 1f);
            GL.Vertex3(0f, 1f, 0.1f);
            GL.TexCoord2(1f, 1f);
            GL.Vertex3(1f, 1f, 0.1f);
            GL.End();

            GL.PopMatrix();
            RenderTexture.active = activeRT;
        }

        public static void Destroy(Object obj)
        {
            if (obj != null)
            {
                if (Application.isPlaying)
                {
                    Object.Destroy(obj);
                }
                else
                {
                    Object.DestroyImmediate(obj);
                }
            }
        }

        public static void Dispose()
        {
            Destroy(s_Quad);
            Destroy(s_WhiteTexture);

            s_Quad = null;
            s_WhiteTexture = null;
        }
    }
}