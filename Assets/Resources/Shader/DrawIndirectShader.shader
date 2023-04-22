Shader "Unlit/TestDrawIndirectShader"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
    }
    SubShader
    {
        Tags { 
            "RenderType"="Opaque" 
            "IgnoreProjector" = "True"
            "DisableBatching" = "True"
        }
        LOD 100

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma enable_d3d11_debug_symbols
            #pragma use_dxc
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv: TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            StructuredBuffer<float3> posBuffer;

            v2f vert (appdata v, uint instanceID: SV_InstanceID)
            {
                v2f o;
                float4 pos = v.vertex;
                float3 worldPos = posBuffer[instanceID];
                pos.xyz += worldPos;
                o.vertex = UnityWorldToClipPos(pos);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }


            fixed4 frag(v2f i) : SV_Target
            {
                //fixed4 col = tex2D(_MainTex, i.uv);
                fixed4 col = fixed4(1.0, 0, 0, 1.0);
                return col;
            }
            ENDCG
        }
    }
}
