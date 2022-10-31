Shader "Unlit/TestDrawIndirectShader"
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
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
            };

            struct v2f
            {
                float4 color : Color;
                float4 vertex : SV_POSITION;
            };

            struct MeshInstanceProperty
            {
                matrix mat;
                float4 color;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            StructuredBuffer<MeshInstanceProperty> meshBuffer;

            v2f vert (appdata v, uint instanceID: SV_InstanceID)
            {
                v2f o;
                float4 pos = mul(meshBuffer[instanceID].mat, v.vertex);
                o.vertex = UnityObjectToClipPos(pos);
                o.color = meshBuffer[instanceID].color;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                return i.color;
            }
            ENDCG
        }
    }
}
