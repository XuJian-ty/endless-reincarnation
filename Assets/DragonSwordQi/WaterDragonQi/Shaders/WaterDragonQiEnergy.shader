Shader "DragonSwordQi/WaterDragonQi/Energy"
{
    Properties
    {
        _BodyColor ("Body Color", Color) = (0.08, 0.58, 1.00, 0.30)
        _CoreColor ("Core Color", Color) = (0.74, 0.95, 1.00, 1.00)
        _GlowColor ("Glow Color", Color) = (0.88, 0.99, 1.00, 1.00)
        _Opacity ("Opacity", Range(0, 2)) = 1
        _LayerAlpha ("Layer Alpha", Range(0, 2)) = 1
        _RevealFront ("Reveal Front", Range(-0.2, 1.2)) = 0
        _RevealWidth ("Reveal Width", Range(0.01, 0.5)) = 0.08
        _FlowSpeed ("Flow Speed", Float) = 2
        _FlowScale ("Flow Scale", Float) = 3
        _FlowPhase ("Flow Phase", Float) = 0
        _WaveAmplitude ("Wave Amplitude", Float) = 0.1
        _WaveFrequency ("Wave Frequency", Float) = 10
        _TwistAmount ("Twist Amount", Float) = 12
        _FresnelPower ("Fresnel Power", Float) = 3
        _NoiseStrength ("Noise Strength", Range(0, 1)) = 0.2
        _EmissionStrength ("Emission Strength", Float) = 1.4
        _CoreBias ("Core Bias", Float) = 1.0
        _ShellBias ("Shell Bias", Float) = 1.0
        _Cull ("Cull", Float) = 2
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline"="UniversalPipeline"
            "RenderType"="Transparent"
            "Queue"="Transparent"
        }

        Cull [_Cull]
        ZWrite Off
        Blend SrcAlpha One

        Pass
        {
            Name "ForwardUnlit"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float4 color : TEXCOORD2;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _BodyColor;
                float4 _CoreColor;
                float4 _GlowColor;
                float _Opacity;
                float _LayerAlpha;
                float _RevealFront;
                float _RevealWidth;
                float _FlowSpeed;
                float _FlowScale;
                float _FlowPhase;
                float _WaveAmplitude;
                float _WaveFrequency;
                float _TwistAmount;
                float _FresnelPower;
                float _NoiseStrength;
                float _EmissionStrength;
                float _CoreBias;
                float _ShellBias;
                float _Cull;
            CBUFFER_END

            float Hash21(float2 p)
            {
                float3 p3 = frac(float3(p.x, p.y, p.x) * 0.1031);
                p3 += dot(p3, p3.yzx + 33.33);
                return frac((p3.x + p3.y) * p3.z);
            }

            float ValueNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float a = Hash21(i);
                float b = Hash21(i + float2(1.0, 0.0));
                float c = Hash21(i + float2(0.0, 1.0));
                float d = Hash21(i + float2(1.0, 1.0));
                float2 u = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
            }

            float Fbm(float2 p)
            {
                float value = 0.0;
                float amplitude = 0.5;
                [unroll] for (int i = 0; i < 4; i++)
                {
                    value += amplitude * ValueNoise(p);
                    p *= 2.03;
                    amplitude *= 0.5;
                }
                return value;
            }

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                float t = saturate(IN.positionOS.x);
                float wavePhase = t * _WaveFrequency + _FlowPhase;
                float waveA = sin(wavePhase) * _WaveAmplitude;
                float waveB = sin(wavePhase * 1.37 + 1.7) * _WaveAmplitude * 0.52;
                float twist = ((t - 0.5) * _TwistAmount + sin(wavePhase * 0.73) * _WaveAmplitude * 0.45) * 0.0174532924;
                float s, c;
                sincos(twist, s, c);

                float2 yz = IN.positionOS.yz;
                yz = float2(yz.x * c - yz.y * s, yz.x * s + yz.y * c);
                yz += float2(waveA, waveB) * (0.48 + IN.color.b * 0.34);

                float3 positionOS = IN.positionOS.xyz;
                positionOS.y = yz.x;
                positionOS.z = yz.y;
                positionOS.x += waveA * 0.04;

                VertexPositionInputs positionInputs = GetVertexPositionInputs(positionOS);
                VertexNormalInputs normalInputs = GetVertexNormalInputs(IN.normalOS);
                OUT.positionHCS = positionInputs.positionCS;
                OUT.positionWS = positionInputs.positionWS;
                OUT.normalWS = normalInputs.normalWS;
                OUT.color = IN.color;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float3 normalWS = normalize(IN.normalWS);
                float3 viewWS = normalize(_WorldSpaceCameraPos.xyz - IN.positionWS);

                float fresnel = pow(1.0 - saturate(dot(normalWS, viewWS)), max(0.5, _FresnelPower));
                float flowNoise = Fbm(IN.positionWS.xz * _FlowScale + float2(_Time.y * _FlowSpeed, _FlowPhase));
                float shell = saturate(IN.color.b);
                float radial = saturate(IN.color.g);
                float coreMask = pow(saturate(1.0 - radial), max(0.5, _CoreBias));
                float shellMask = pow(radial, max(0.5, _ShellBias));
                float reveal = 1.0 - smoothstep(_RevealFront - _RevealWidth, _RevealFront + _RevealWidth, IN.color.r);
                reveal = saturate(reveal);

                float flowMask = saturate(flowNoise * 0.82 + fresnel * 0.92 + shellMask * 0.3);
                float alpha = reveal * _LayerAlpha * _Opacity * (0.18 + fresnel * 0.74 + flowNoise * _NoiseStrength);

                float3 body = _BodyColor.rgb;
                float3 core = _CoreColor.rgb;
                float3 glow = _GlowColor.rgb;

                float3 color = lerp(body, core, coreMask);
                color = lerp(color, glow, flowMask * 0.64 + shellMask * 0.26);
                color *= _EmissionStrength * (0.88 + fresnel * 0.72 + flowNoise * 0.28);

                return half4(color, alpha);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
