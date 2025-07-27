Shader "Outline Shaders/Outline 3.1" {
	Properties {
		_OutlineColor ("Outline Color", Vector) = (1,1,1,1)
		_Outline ("Outline width", Range(0, 5)) = 0.005
		_IntensityBase ("Alpha Base", Float) = 1
		_IntensityMax ("Alpha Max", Float) = 0
		_MaxIntensityDist ("Max Alpha Distance", Float) = 30
	}
	//DummyShaderTextExporter
	SubShader{
		Tags { "RenderType" = "Opaque" }
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

			float4 frag(Vertex_Stage_Output input) : SV_TARGET
			{
				return float4(1.0, 1.0, 1.0, 1.0); // RGBA
			}

			ENDHLSL
		}
	}
}