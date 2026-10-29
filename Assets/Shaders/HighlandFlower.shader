Shader "Game/Environment/HighlandFlower"
{
    Properties
    {
        _WindDirection("Wind Direction", Vector) = (0.82, 0, 0.57, 0)
        _WindStrength("Wind Strength", Range(0, 1)) = 0.18
        _WindSpeed("Wind Speed", Range(0, 8)) = 2.1
        _AmbientStrength("Ambient Strength", Range(0, 1)) = 0.48
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "Queue" = "AlphaTest+5"
            "RenderPipeline" = "UniversalPipeline"
        }
        Cull Off

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #pragma multi_compile_fragment _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _WindDirection;
                float _WindStrength;
                float _WindSpeed;
                float _AmbientStrength;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                half4 color : TEXCOORD2;
                float4 shadowCoord : TEXCOORD3;
                half fogFactor : TEXCOORD4;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float2 windDirection = normalize(_WindDirection.xz + float2(0.0001, 0.0001));
                float wave = sin(dot(positionWS.xz, windDirection) * 0.19 - _Time.y * _WindSpeed);
                positionWS.xz += windDirection * wave * _WindStrength * input.uv.y * input.uv.y;
                output.positionWS = positionWS;
                output.positionCS = TransformWorldToHClip(positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.color = input.color;
                output.shadowCoord = TransformWorldToShadowCoord(positionWS);
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            half4 Frag(Varyings input, bool isFrontFace : SV_IsFrontFace) : SV_Target
            {
                half3 normalWS = normalize(input.normalWS) * (isFrontFace ? 1.0 : -1.0);
                Light mainLight = GetMainLight(input.shadowCoord);
                half diffuse = saturate(dot(normalWS, mainLight.direction) * 0.7 + 0.3);
                half3 ambient = max(SampleSH(normalWS), _AmbientStrength.xxx) * input.color.rgb;
                half3 color = ambient + input.color.rgb * mainLight.color * diffuse * mainLight.shadowAttenuation;
                color = MixFog(color, input.fogFactor);
                return half4(color, 1.0);
            }
            ENDHLSL
        }
    }
}
