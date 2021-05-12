Shader "UnityPIC/ParaboloidInterpolation"
{
	Properties
	{
		_UPIC_MinIntensity ("Min Intensity", Float) = 0.0
		_UPIC_MaxIntensity ("Max Intensity", Float) = 1.0
		_UPIC_Shift ("Shift", Vector) = (0, 0, 0, 0)

		//[PerRendererData]
		//_UPIC_Center ("Center", Vector) = (0, 0, 0, 0)

		//[PerRendererData]
		//_UPIC_Radius ("Radius", Float) = 1.0

		//[PerRendererData]
		//_UPIC_RenderMask ("Render Mask", Int) = 0
	}

	SubShader
	{
		Tags { "RenderType"="Opaque" }
		LOD 100

		Pass
		{
			CGPROGRAM

			//#pragma require geometry
			#pragma vertex vert
			//#pragma geometry geom
			#pragma fragment frag

			#include "UnityCG.cginc"
			#include "./UnityPIC.cginc"

			struct v2g
			{
				float4 vertex : SV_POSITION;
				fixed3 col : COLOR;
			};

			//struct g2f
			//{
			//	float4 vertex : SV_POSITION;
			//	float2 point_uv : TEXCOORD0;
			//	fixed3 col : COLOR;
			//};

			struct fout 
			{
				fixed3 col : SV_TARGET;
				float depth : DEPTH;
			};

			uniform StructuredBuffer<PosColIntCls20> _UPIC_Points;

			//float4 _UPIC_Center;
			float4 _UPIC_Shift;
			//float _UPIC_Radius;

			float _UPIC_MinIntensity;
			float _UPIC_MaxIntensity;

			//int _UPIC_RenderMask;

			v2g vert (uint id : SV_VertexID)
			{
				v2g o;

				PosColIntCls20 p = _UPIC_Points[id];

				o.vertex = UnityObjectToClipPos(p.vertex - _UPIC_Shift);// *((partition_id(p.vertex, _UPIC_Center) & _UPIC_RenderMask) > 0);
				o.col.rgb = normalize_intensity(p.intensity(), _UPIC_MinIntensity, _UPIC_MaxIntensity);

				return o;
			}

			//[maxvertexcount(4)]
			//void geom(point v2g i[1], inout TriangleStream<g2f> ostream) {
			//	g2f o;

			//	o.vertex = i[0].vertex;
			//	o.col = i[0].col;

			//	if (abs(o.vertex.x) < o.vertex.w && abs(o.vertex.y) < o.vertex.w) {
			//		DEFAULT_POINT_POS_UV_TO_STREAM(o, vertex, point_uv, ostream, 2.0 / 1000 * o.vertex.w);
			//	}
			//}

			fout frag(v2g i)
			{
				//float d = sqrt(dot(i.point_uv, i.point_uv));

				//if (d > 1.0f)
					//discard;

				fout o;

				o.col = i.col;
				//o.col = 1.0f;
				//o.depth = depth((i.vertex.w + d * _UPIC_Radius) * _ProjectionParams.w);
				o.depth = i.vertex.z;
				//o.depth = 1.0f - (i.vertex.w - (1.0f - d) * _UPIC_Radius) * _ProjectionParams.w; 

				return o;
			}

			ENDCG
		}
	}
}
