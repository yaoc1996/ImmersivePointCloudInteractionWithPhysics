Shader "UnityPIC/SceneIntegration"
{
	Properties
	{
		_MainTex ("Texture", 2D) = "white" {}
	}
	SubShader
	{
		Tags { "RenderType"="Opaque" }
		LOD 100

		Pass
		{
			ZTest Always
			//Blend One OneMinusSrcAlpha

			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag

			#include "UnityCG.cginc"
			#include "./UnityPIC.cginc"

			struct appdata
			{
				float4 vertex : POSITION;
			};

			struct v2f
			{
				float4 vertex : SV_POSITION;
			};

			struct fout 
			{
				fixed4 col : SV_TARGET;
			};

			uniform sampler2D _MainTex;
			uniform sampler2D _CameraDepthTexture;

			uniform sampler2D _UPIC_SceneColor;
			uniform sampler2D _UPIC_SceneDepth;

			v2f vert (appdata v)
			{
				v2f o;
				o.vertex = UnityObjectToClipPos(v.vertex);
				return o;
			}

			fout frag (v2f i)
			{
				float2 uv = (floor(i.vertex.xy) + 0.5f) / _ScreenParams.xy;

#ifdef UNITY_UV_STARTS_AT_TOP
				uv.y = 1.0f - uv.y;
#endif 

				fout o;

				float scn_depth = tex2Dlod(_UPIC_SceneDepth, float4(uv, 0, 0)).r;
				float cam_depth = tex2Dlod(_CameraDepthTexture, float4(uv, 0, 0)).r;

				//if (cam_depth < scn_depth) {
					o.col.rgb = tex2Dlod(_UPIC_SceneColor, float4(uv, 0, 0)).rgb;
					o.col.a = 1.0f;
				//}
				//else {
				//	o.col = tex2Dlod(_MainTex, float4(uv, 0, 0));
				//}

				//o.col = cam_depth;

				return o;
			}
			ENDCG
		}
	}
}
