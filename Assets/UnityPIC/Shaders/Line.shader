Shader "UnityPIC/Line"
{
	Properties
	{
		_UPIC_Shift ("Shift", Vector) = (0, 0, 0, 0)
		_UPIC_Color ("Color", Color) = (1, 1, 1, 1)
	}
	SubShader
	{
		Tags { "RenderType"="Opaque" }
		LOD 100

		Pass
		{
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag

			#include "UnityCG.cginc"
			#include "./UnityPIC.cginc"

			struct v2f
			{
				float4 vertex : SV_POSITION;
			};

			struct fout
			{
				fixed4 col : SV_TARGET;
				float depth : DEPTH;
			};

			uniform StructuredBuffer<float3> _UPIC_Edges;
			float4 _UPIC_Shift;
			fixed4 _UPIC_Color;

			v2f vert (uint id : SV_VertexID)
			{
				v2f o;

				o.vertex = UnityObjectToClipPos(_UPIC_Edges[id] - _UPIC_Shift.xyz);

				return o;
			}

			fout frag (v2f i)
			{
				fout o;

				o.col = _UPIC_Color;
				//o.depth = 1 - i.vertex.w * _ProjectionParams.w;
				o.depth = i.vertex.z;

				return o;
			}
			ENDCG
		}
	}
}