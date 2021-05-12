Shader "UnityPIC/CB_Unlit"
{
	Properties
	{
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
				fixed4 color : COLOR;
			};

			struct fout
			{
				fixed4 col : SV_TARGET;
				float depth : DEPTH;
			};

			uniform StructuredBuffer<PosCol16> _Vertices;

			v2f vert (uint id : SV_VertexID)
			{
				PosCol16 vert = _Vertices[id];

				v2f o;

				o.vertex = UnityObjectToClipPos(vert.vertex);
				o.color = vert.get_color();

				return o;
			}

			fout frag (v2f i)
			{
				fout o;

				o.col = i.color;
				o.depth = i.vertex.z;

				return o;
			}
			ENDCG
		}
	}
}
