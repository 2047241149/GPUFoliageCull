Shader "Instance/NewSurfaceShader"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _MainTex ("Albedo (RGB)", 2D) = "white" {}
        _Glossiness ("Smoothness", Range(0,1)) = 0.5
        _Metallic ("Metallic", Range(0,1)) = 0.0
        _Cutoff("Cutoff", float) = 0.5
        _Scale("Scale", float) = 1
    }
    SubShader
    {
        Tags{
				"Queue" = "Geometry+200"
				"IgnoreProjector" = "True"
                "DisableBatching" = "True"
        }
        Cull Off
        LOD 200

        CGPROGRAM

        #pragma surface surf Lambert     addshadow exclude_path:deferred
        #pragma multi_compile_instancing
        #pragma instancing_options procedural:setup

        // Use shader model 3.0 target, to get nicer looking lighting
        #pragma target 3.0
        
        struct appdata
        {
            float4 vertex : POSITION;
            float2 texcoord : TEXCOORD0;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };
        
        struct Input
        {
            float2 uv_MainTex;
        };

        half _Glossiness;
        half _Metallic;
        fixed4 _Color;
        fixed _Cutoff;
        float _Scale;
        sampler2D _MainTex;

        #ifdef UNITY_PROCEDURAL_INSTANCING_ENABLED
        StructuredBuffer<float3> posBuffer;
        #endif

        void setup()
        {
            #ifdef UNITY_PROCEDURAL_INSTANCING_ENABLED
            float3 position = posBuffer[unity_InstanceID];
            unity_ObjectToWorld._12_22_32_42 = float4(0, 1, 0, 0);
            unity_ObjectToWorld._14_24_34_44 = float4(position.xyz,1);
            unity_ObjectToWorld._11_22_33 = float3(_Scale, _Scale, _Scale);

            unity_WorldToObject = unity_ObjectToWorld;
            unity_WorldToObject._14_24_34 *= -1;
            unity_WorldToObject._11_22_33 = 1.0f / unity_WorldToObject._11_22_33;
            #endif
        }

        void surf (Input IN, inout SurfaceOutput o)
        {
            // Albedo comes from a texture tinted by color
            fixed4 c = tex2D (_MainTex, IN.uv_MainTex) * _Color;
            o.Albedo = c.rgb;
            clip(c.a - _Cutoff);
            o.Alpha = 1.0;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
