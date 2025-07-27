Shader "Hidden/Post FX/Uber Shader" {
	Properties {
		_MainTex ("Texture", 2D) = "white" {}
		_AutoExposure ("", 2D) = "" {}
		_BloomTex ("", 2D) = "" {}
		_Bloom_DirtTex ("", 2D) = "" {}
		_GrainTex ("", 2D) = "" {}
		_LogLut ("", 2D) = "" {}
		_UserLut ("", 2D) = "" {}
		_Vignette_Mask ("", 2D) = "" {}
		_ChromaticAberration_Spectrum ("", 2D) = "" {}
		_DitheringTex ("", 2D) = "" {}
	}
	//DummyShaderTextExporter
	SubShader{
		Tags { "RenderType"="Opaque" }
		LOD 200

		Pass
		{
			HLSLPROGRAM
			#pragma vertex vert
			#pragma fragment frag

			float4x4 unity_MatrixMVP;

			struct Vertex_Stage_Input
			{
				float3 pos : POSITION;
			};

			struct Vertex_Stage_Output
			{
				float4 pos : SV_POSITION;
			};

			Vertex_Stage_Output vert(Vertex_Stage_Input input)
			{
				Vertex_Stage_Output output;
				output.pos = mul(unity_MatrixMVP, float4(input.pos, 1.0));
				return output;
			}

			Texture2D<float4> _MainTex;
			SamplerState sampler_MainTex;

			struct Fragment_Stage_Input
			{
				float2 uv : TEXCOORD0;
			};

			float4 frag(Fragment_Stage_Input input) : SV_TARGET
			{
				return _MainTex.Sample(sampler_MainTex, float2(input.uv.x, input.uv.y));
			}

			ENDHLSL
		}
	}
}