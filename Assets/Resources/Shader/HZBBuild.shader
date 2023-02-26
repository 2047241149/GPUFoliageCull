Shader "Custom/HZB"
{
    Properties
    {
        _MainTex ("Depth Texture", 2D) = "white" {}
        _InvSize("Inverse Tex Size", Vector) = (0, 0, 0, 0) //x,y = (1/MipMapSize.x, 1/MipMapSize.y), zw = (0, 0
    }
    SubShader
    {
        // No culling or depth
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            float get_hzb(sampler2D tex, float2 uv, float invSize)
            {
                float4 depth;
                float2 uv0 = uv + float2(-0.25f, -0.25f) * invSize;
                float2 uv1 = uv + float2(0.25f, -0.25f) * invSize;
                float2 uv2 = uv + float2(-0.25f, 0.25f) * invSize;
                float2 uv3 = uv + float2(0.25f, 0.25f) * invSize;

                depth.x = tex2D(tex, uv0);
                depth.y = tex2D(tex, uv1);
                depth.z = tex2D(tex, uv2);
                depth.w = tex2D(tex, uv3);

                #if defined(UNITY_REVERSED_Z)
                return min(min(depth.x, depth.y), min(depth.z, depth.w));
                #else
                return max(max(depth.x, depth.y), max(depth.z, depth.w));
                #endif
            }

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }
            
            sampler2D _MainTex;
            float4 _InvSize;

            fixed4 frag (v2f i) : SV_Target
            {
				float2 invSize = _InvSize.xy;
				float2 inUV = i.uv;

				float depth = get_hzb(_MainTex, inUV, invSize);
                return depth;
            }
            ENDCG
        }
    }
}
