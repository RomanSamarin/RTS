Shader "Lords of the Valley/Water"
{
    Properties
    {
        _ShallowColor ("Shallows", Color) = (0.20, 0.66, 0.60, 1)
        _DeepColor ("Deep Water", Color) = (0.035, 0.23, 0.34, 1)
        _FoamColor ("Shore Foam", Color) = (0.78, 0.91, 0.86, 1)
        _DepthRange ("Color Depth (m)", Range(0.1, 16)) = 4
        _FoamDepth ("Foam Depth (m)", Range(0.02, 2)) = 0.38
        _WaveScale ("Wave Scale", Range(0.05, 3)) = 0.55
        _WaveStrength ("Wave Strength", Range(0, 1)) = 0.28
        _FlowSpeed ("Flow Speed", Range(0, 2)) = 0.55
        _EastBank ("Flat Map Bank (X, Width, Extension)", Vector) = (0, 0, 0, 0)
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent-50" "RenderType" = "Transparent" }

        Pass
        {
            Name "Water"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex WaterVertex
            #pragma fragment WaterFragment
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _ShallowColor;
                float4 _DeepColor;
                float4 _FoamColor;
                float _DepthRange;
                float _FoamDepth;
                float _WaveScale;
                float _WaveStrength;
                float _FlowSpeed;
                float4 _EastBank;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half fog : TEXCOORD1;
                half up : TEXCOORD2;
            };

            Varyings WaterVertex(Attributes input)
            {
                Varyings output;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionWS.x += _EastBank.z * step(_EastBank.x - max(1.0, _EastBank.y), output.positionWS.x);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.fog = ComputeFogFactor(output.positionCS.z);
                output.up = TransformObjectToWorldNormal(input.normalOS).y;
                return output;
            }

            // Fade ripples as their wavelength approaches a pixel at RTS zoom distances.
            float ResolvedWave(float phase)
            {
                return 1.0 - smoothstep(0.45, 1.8, fwidth(phase));
            }

            half4 WaterFragment(Varyings input) : SV_Target
            {
                clip(input.up - 0.5);
                float2 screenUV = GetNormalizedScreenSpaceUV(input.positionCS);
                float rawDepth = SampleSceneDepth(screenUV);
                #if UNITY_REVERSED_Z
                    bool hasBottom = rawDepth > 0.000001;
                    float deviceDepth = rawDepth;
                #else
                    bool hasBottom = rawDepth < 0.999999;
                    float deviceDepth = lerp(UNITY_NEAR_CLIP_VALUE, 1.0, rawDepth);
                #endif

                float3 bottom = ComputeWorldSpacePosition(screenUV, deviceDepth, UNITY_MATRIX_I_VP);
                float depth = hasBottom ? max(0.0, input.positionWS.y - bottom.y) : _DepthRange * 4.0;
                // The flat prototype map ends at a vertical edge, with no sloped seabed.
                if (_EastBank.y > 0.0)
                {
                    float bankDepth = max(0.0, _EastBank.x - input.positionWS.x) * _DepthRange / _EastBank.y;
                    depth = min(depth, bankDepth);
                }
                float depthBlend = 1.0 - exp2(-2.0 * depth / _DepthRange);
                float2 p = input.positionWS.xz * _WaveScale;
                float t = _Time.y * _FlowSpeed;

                float phaseA = dot(p, float2(1.1, 0.52)) - t;
                float phaseB = dot(p, float2(-0.38, 1.72)) + t * 0.81;
                float phaseC = dot(p, float2(3.62, -2.09)) - t * 1.65 + sin(phaseB) * 0.56;
                float phaseD = dot(p, float2(6.17, 4.7)) + t * 2.41 + sin(phaseA) * 0.9;
                float2 slope =
                    float2(0.91, 0.43) * cos(phaseA) * ResolvedWave(phaseA) * 0.42 +
                    float2(-0.22, 0.98) * cos(phaseB) * ResolvedWave(phaseB) * 0.27 +
                    float2(0.87, -0.50) * cos(phaseC) * ResolvedWave(phaseC) * 0.24 +
                    float2(0.80, 0.60) * cos(phaseD) * ResolvedWave(phaseD) * 0.13;
                float3 normal = normalize(float3(slope.x * _WaveStrength, 1, slope.y * _WaveStrength));
                float3 viewDirection = GetWorldSpaceNormalizeViewDir(input.positionWS);
                float fresnel = Pow4(1.0 - saturate(dot(normal, viewDirection)));

                Light sun = GetMainLight();
                float3 halfway = SafeNormalize(sun.direction + viewDirection);
                float normalHalf = saturate(dot(normal, halfway));
                float glint = (pow(normalHalf, 128.0) * 1.6 + pow(normalHalf, 26.0) * 0.24)
                    * saturate(sun.direction.y);

                float2 refractedUV = saturate(screenUV + normal.xz * 0.015 * saturate(depth));
                float refractedDepth = SampleSceneDepth(refractedUV);
                #if UNITY_REVERSED_Z
                    bool foreground = refractedDepth > rawDepth + 0.0001;
                #else
                    bool foreground = refractedDepth < rawDepth - 0.0001;
                #endif
                if (foreground) refractedUV = screenUV;
                float3 bedColor = SampleSceneColor(refractedUV);
                float3 tint = lerp(_ShallowColor.rgb, _DeepColor.rgb, depthBlend);
                float3 water = lerp(bedColor * float3(0.8, 0.98, 0.95), tint, depthBlend * 0.87 + 0.08);
                float ripple = sin(phaseA) * 0.032 + sin(phaseC) * ResolvedWave(phaseC) * 0.048;
                water *= 0.91 + ripple + saturate(dot(normal, sun.direction)) * 0.15;
                float3 reflectedSky = lerp(float3(0.31, 0.48, 0.57), float3(0.14, 0.34, 0.5),
                    saturate(reflect(-viewDirection, normal).y));
                water = lerp(water, reflectedSky, 0.07 + 0.67 * fresnel);
                water += sun.color * glint;

                float windRipple = dot(p, float2(8.6, -3.5)) + sin(phaseC) * 1.35 + sin(phaseA) - t * 2.8;
                float windPatch = smoothstep(-0.25, 0.8, sin(phaseB * 0.73 + sin(phaseA * 0.62)));
                float rippleCrest = pow(saturate(0.5 + 0.5 * sin(windRipple)), 5.0)
                    * ResolvedWave(windRipple) * windPatch;
                water += reflectedSky * rippleCrest * (0.13 + 0.16 * fresnel);

                // Sunlight patterns move across the submerged ground.
                float2 bed = (hasBottom ? bottom.xz : input.positionWS.xz) * (_WaveScale * 3.5);
                float causticA = dot(bed, float2(1.0, 0.57)) + t * 1.38 + sin(bed.y * 0.63 + sin(bed.x * 0.47)) * 1.8;
                float causticB = dot(bed, float2(-0.71, 1.13)) - t * 1.12 + sin(bed.x * 0.56 + sin(bed.y * 0.38)) * 1.6;
                float caustic = 1.0 - abs(sin(causticA + 0.68 * sin(causticB))
                    * sin(causticB - 0.58 * sin(causticA)));
                float causticFade = smoothstep(0.04, 0.3, depth) * (1.0 - smoothstep(1.0, 7.0, depth));
                water += sun.color * pow(caustic, 7.0) * ResolvedWave(causticA) * ResolvedWave(causticB)
                    * causticFade * 0.16;

                // Moving bands break up along the actual bank, including islands and piers.
                float shore = depth / _FoamDepth;
                float pattern = sin(phaseA * 1.67 + phaseB) * 0.55 + sin(phaseC * 0.85) * 0.26;
                float crest = shore - (0.48 + pattern * 0.21 + sin(phaseB * 0.62 - t) * 0.16);
                float foamAA = max(0.055, fwidth(crest));
                float foamBand = 1.0 - smoothstep(0.07, 0.07 + foamAA, abs(crest));
                float rim = 1.0 - smoothstep(0.04, 0.24 + foamAA, shore);
                float foam = max(foamBand * 0.66, rim * 0.55)
                    * smoothstep(-0.38, 0.34, pattern + 0.18)
                    * (1.0 - smoothstep(0.78, 1.3, shore));
                water = lerp(water, _FoamColor.rgb, foam);

                return half4(MixFog(water, input.fog), 1);
            }
            ENDHLSL
        }
    }
}
