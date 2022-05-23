Shader "Voxel/Diffuse"
{
    Properties {}
    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
        }
        Pass
        {
            Tags
            {
                "LightMode" = "ForwardBase"
            }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fwdbase

            #include "UnityCG.cginc"
            #include "AutoLight.cginc"
            #include "Lighting.cginc"

            struct Attributes
            {
                float4 vertex : POSITION;
                half3 normal : NORMAL;
                half4 color : COLOR0;
            };

            struct Varyings
            {
                float4 pos : SV_POSITION;
                half4 color : COLOR0;
                half3 worldNormal : TEXCOORD0;
                LIGHTING_COORDS(1, 2)
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;

                float3 worldPos = mul(unity_ObjectToWorld, IN.vertex).xyz;

                OUT.pos = UnityObjectToClipPos(IN.vertex);
                OUT.color = IN.color;
                OUT.worldNormal = UnityObjectToWorldNormal(IN.normal);
                TRANSFER_VERTEX_TO_FRAGMENT(OUT);

                return OUT;
            }

            fixed4 frag(Varyings IN) : SV_Target
            {
                half3 worldNormal = normalize(IN.worldNormal);
                half nDotL = saturate(dot(worldNormal, _WorldSpaceLightPos0));
                half atten = nDotL * LIGHT_ATTENUATION(IN);

                half3 diffuseTerm = IN.color.rgb;
                half3 directDiffuse = diffuseTerm * atten * _LightColor0;
                half3 indirectDiffuse = diffuseTerm * ShadeSH9(half4(worldNormal,1));
                
                return half4(directDiffuse + indirectDiffuse, 1.0);
            }
            ENDHLSL
        }
    }

    // Fallback to VertexLit to get it's shadow caster pass.
    Fallback "VertexLit"
}
