Shader "Custom/ToonLit"
{
    Properties
    {
        // Base color & texture (same names as URP Lit for compatibility)
        _BaseColor ("Base Color", Color) = (1, 1, 1, 1)
        _BaseMap ("Base Map (RGB)", 2D) = "white" {}

        // Toon shadow band
        _ShadowColor ("Shadow Color", Color) = (0.2, 0.15, 0.1, 1)
        _ShadowThreshold ("Shadow Threshold", Range(0, 1)) = 0.3
        _ShadowSmoothness ("Shadow Edge Smoothness", Range(0.001, 0.1)) = 0.02
        _MidThreshold ("Mid-tone Threshold", Range(0, 1)) = 0.65
        _MidSmoothness ("Mid-tone Edge Smoothness", Range(0.001, 0.1)) = 0.03

        // Specular highlight band
        _SpecularColor ("Specular Color", Color) = (1, 1, 1, 1)
        _SpecularSize ("Specular Size", Range(0, 1)) = 0.1
        _Glossiness ("Glossiness", Range(1, 256)) = 32

        // Rim light
        _RimColor ("Rim Color", Color) = (1, 1, 1, 1)
        _RimAmount ("Rim Amount", Range(0, 1)) = 0.6
        _RimThreshold ("Rim Threshold", Range(0, 1)) = 0.1

        // Emission
        _EmissionColor ("Emission Color", Color) = (0, 0, 0, 1)

        // Normal map (same name as URP Lit)
        _BumpMap ("Normal Map", 2D) = "bump" {}
        _BumpScale ("Normal Scale", Float) = 1.0

        // Occlusion
        _OcclusionMap ("Occlusion Map", 2D) = "white" {}
        _OcclusionStrength ("Occlusion Strength", Range(0, 1)) = 1.0

        // Hatching
        _HatchingDensity ("Hatching Line Density", Float) = 25
        _HatchingIntensity ("Hatching Intensity", Range(0, 1)) = 0
        _HatchingAngle ("Hatching Angle (degrees)", Float) = 45

        // Metallic / Smoothness (read by some existing code)
        _Metallic ("Metallic", Range(0, 1)) = 0
        _Smoothness ("Smoothness", Range(0, 1)) = 0.5

        // Transparency support
        _Surface ("Surface Type", Float) = 0
        _Blend ("Blend Mode", Float) = 0
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 2
        [Enum(Off, 0, On, 1)] _ZWrite ("Z Write", Float) = 1
        _SrcBlend ("Src Blend", Float) = 1
        _DstBlend ("Dst Blend", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Geometry"
        }

        // ======= FORWARD PASS =======
        Pass
        {
            Name "ToonForward"
            Tags { "LightMode" = "UniversalForward" }

            Cull [_Cull]
            ZWrite [_ZWrite]
            Blend [_SrcBlend] [_DstBlend]

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            // URP keywords
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _SHADOWS_SOFT
            #pragma multi_compile_fog
            #pragma multi_compile _ _NORMALMAP
            #pragma multi_compile _ _EMISSION

            // SRP Batcher compatibility
            #pragma prefer_hlslcc gles
            #pragma exclude_renderers d3d11_9x

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            // SRP Batcher CBUFFER
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _BaseMap_ST;
                float4 _ShadowColor;
                float _ShadowThreshold;
                float _ShadowSmoothness;
                float _MidThreshold;
                float _MidSmoothness;
                float4 _SpecularColor;
                float _SpecularSize;
                float _Glossiness;
                float4 _RimColor;
                float _RimAmount;
                float _RimThreshold;
                float4 _EmissionColor;
                float _BumpScale;
                float _OcclusionStrength;
                float _HatchingDensity;
                float _HatchingIntensity;
                float _HatchingAngle;
                float _Metallic;
                float _Smoothness;
                float _Surface;
                float _Cull;
                float _ZWrite;
                float _SrcBlend;
                float _DstBlend;
                float _Blend;
            CBUFFER_END

            TEXTURE2D(_BaseMap);        SAMPLER(sampler_BaseMap);
            TEXTURE2D(_BumpMap);        SAMPLER(sampler_BumpMap);
            TEXTURE2D(_OcclusionMap);   SAMPLER(sampler_OcclusionMap);

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float3 normalOS     : NORMAL;
                float4 tangentOS    : TANGENT;
                float2 uv           : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
                float2 uv           : TEXCOORD0;
                float3 positionWS   : TEXCOORD1;
                float3 normalWS     : TEXCOORD2;
                float3 tangentWS    : TEXCOORD3;
                float3 bitangentWS  : TEXCOORD4;
                float4 shadowCoord  : TEXCOORD5;
                float fogFactor     : TEXCOORD6;
                float4 screenPos    : TEXCOORD7;
            };

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;

                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS, input.tangentOS);

                output.positionCS = vertexInput.positionCS;
                output.positionWS = vertexInput.positionWS;
                output.normalWS = normalInput.normalWS;
                output.tangentWS = normalInput.tangentWS;
                output.bitangentWS = normalInput.bitangentWS;
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.shadowCoord = GetShadowCoord(vertexInput);
                output.fogFactor = ComputeFogFactor(vertexInput.positionCS.z);
                output.screenPos = ComputeScreenPos(vertexInput.positionCS);

                return output;
            }

            // Procedural crosshatch pattern
            float Hatching(float2 screenUV, float shadowAmount)
            {
                // Rotate UVs for diagonal lines
                float angleRad = _HatchingAngle * 3.14159 / 180.0;
                float cosA = cos(angleRad);
                float sinA = sin(angleRad);
                float2 rotUV = float2(
                    screenUV.x * cosA - screenUV.y * sinA,
                    screenUV.x * sinA + screenUV.y * cosA
                );

                // Primary hatch lines
                float line1 = step(0.55, frac(rotUV.x * _HatchingDensity));

                // Cross-hatch (perpendicular set) for deeper shadows
                float2 rotUV2 = float2(
                    screenUV.x * cosA + screenUV.y * sinA,
                    -screenUV.x * sinA + screenUV.y * cosA
                );
                float line2 = step(0.55, frac(rotUV2.x * _HatchingDensity * 0.8));

                // Blend based on shadow depth
                float hatch = line1 * saturate(shadowAmount * 2.0);
                hatch += line2 * saturate(shadowAmount * 2.0 - 0.5) * 0.7; // cross only in deeper shadow

                return hatch * _HatchingIntensity;
            }

            half4 frag(Varyings input) : SV_Target
            {
                // Sample base texture
                half4 texColor = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                half4 baseColor = texColor * _BaseColor;

                // Normal mapping
                float3 normalWS = normalize(input.normalWS);
                #ifdef _NORMALMAP
                {
                    float3 normalTS = UnpackNormalScale(
                        SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, input.uv), _BumpScale);
                    float3x3 TBN = float3x3(
                        normalize(input.tangentWS),
                        normalize(input.bitangentWS),
                        normalWS);
                    normalWS = normalize(mul(normalTS, TBN));
                }
                #endif

                // AO
                half occlusion = SAMPLE_TEXTURE2D(_OcclusionMap, sampler_OcclusionMap, input.uv).r;
                occlusion = lerp(1.0, occlusion, _OcclusionStrength);

                // Main light
                Light mainLight = GetMainLight(input.shadowCoord);
                float3 lightDir = normalize(mainLight.direction);
                float3 lightColor = mainLight.color;
                float shadow = mainLight.distanceAttenuation * mainLight.shadowAttenuation;

                // View direction
                float3 viewDir = normalize(_WorldSpaceCameraPos.xyz - input.positionWS);

                // === CEL SHADING ===
                float NdotL = dot(normalWS, lightDir);
                float lightAmount = NdotL * shadow;

                // 3-band toon ramp
                float shadowBand = smoothstep(_ShadowThreshold - _ShadowSmoothness,
                                              _ShadowThreshold + _ShadowSmoothness, lightAmount);
                float midBand = smoothstep(_MidThreshold - _MidSmoothness,
                                           _MidThreshold + _MidSmoothness, lightAmount);

                // Interpolate between shadow → mid → lit
                half3 toonColor = lerp(_ShadowColor.rgb, baseColor.rgb * 0.75, shadowBand);
                toonColor = lerp(toonColor, baseColor.rgb, midBand);

                // Apply light color
                toonColor *= lightColor;

                // === SPECULAR BAND ===
                float3 halfVec = normalize(lightDir + viewDir);
                float NdotH = dot(normalWS, halfVec);
                float specular = pow(max(0, NdotH), _Glossiness);
                float specBand = smoothstep(1.0 - _SpecularSize - 0.01, 1.0 - _SpecularSize + 0.01, specular)
                               * shadow * step(0.01, _SpecularSize);
                toonColor += _SpecularColor.rgb * specBand * _Smoothness;

                // === RIM LIGHT ===
                float NdotV = dot(normalWS, viewDir);
                float rimDot = 1.0 - saturate(NdotV);
                float rimIntensity = smoothstep(_RimAmount - 0.01, _RimAmount + 0.01, rimDot)
                                   * smoothstep(-0.1, 0.3, NdotL); // rim only on lit side
                toonColor += _RimColor.rgb * rimIntensity * _RimThreshold;

                // === ADDITIONAL LIGHTS (simplified toon) ===
                #ifdef _ADDITIONAL_LIGHTS
                {
                    uint pixelLightCount = GetAdditionalLightsCount();
                    for (uint i = 0u; i < pixelLightCount; ++i)
                    {
                        Light addLight = GetAdditionalLight(i, input.positionWS);
                        float addNdotL = dot(normalWS, normalize(addLight.direction));
                        float addShadow = addLight.distanceAttenuation * addLight.shadowAttenuation;
                        float addBand = smoothstep(0.0, 0.05, addNdotL * addShadow);
                        toonColor += baseColor.rgb * addLight.color * addBand * 0.5;
                    }
                }
                #endif

                // === HATCHING ===
                float2 screenUV = input.screenPos.xy / input.screenPos.w;
                screenUV.x *= _ScreenParams.x / _ScreenParams.y; // aspect correction
                float shadowAmount = 1.0 - shadowBand; // 1 = deep shadow, 0 = lit
                float hatch = Hatching(screenUV, shadowAmount);
                toonColor -= hatch * 0.08; // darken in hatched areas

                // Apply AO
                toonColor *= occlusion;

                // === EMISSION ===
                #ifdef _EMISSION
                    toonColor += _EmissionColor.rgb;
                #endif

                // Fog
                toonColor = MixFog(toonColor, input.fogFactor);

                return half4(toonColor, baseColor.a);
            }
            ENDHLSL
        }

        // ======= SHADOW CASTER =======
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull [_Cull]

            HLSLPROGRAM
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
            #pragma multi_compile_shadowcaster

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _BaseMap_ST;
                float4 _ShadowColor;
                float _ShadowThreshold;
                float _ShadowSmoothness;
                float _MidThreshold;
                float _MidSmoothness;
                float4 _SpecularColor;
                float _SpecularSize;
                float _Glossiness;
                float4 _RimColor;
                float _RimAmount;
                float _RimThreshold;
                float4 _EmissionColor;
                float _BumpScale;
                float _OcclusionStrength;
                float _HatchingDensity;
                float _HatchingIntensity;
                float _HatchingAngle;
                float _Metallic;
                float _Smoothness;
                float _Surface;
                float _Cull;
                float _ZWrite;
                float _SrcBlend;
                float _DstBlend;
                float _Blend;
            CBUFFER_END

            struct ShadowAttributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
            };

            struct ShadowVaryings
            {
                float4 positionCS : SV_POSITION;
            };

            float3 _LightDirection;

            ShadowVaryings ShadowVert(ShadowAttributes input)
            {
                ShadowVaryings output;
                float3 posWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                posWS = ApplyShadowBias(posWS, normalWS, _LightDirection);
                output.positionCS = TransformWorldToHClip(posWS);

                #if UNITY_REVERSED_Z
                    output.positionCS.z = min(output.positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #else
                    output.positionCS.z = max(output.positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #endif

                return output;
            }

            half4 ShadowFrag(ShadowVaryings input) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }

        // ======= DEPTH ONLY =======
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask R
            Cull [_Cull]

            HLSLPROGRAM
            #pragma vertex DepthVert
            #pragma fragment DepthFrag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _BaseMap_ST;
                float4 _ShadowColor;
                float _ShadowThreshold;
                float _ShadowSmoothness;
                float _MidThreshold;
                float _MidSmoothness;
                float4 _SpecularColor;
                float _SpecularSize;
                float _Glossiness;
                float4 _RimColor;
                float _RimAmount;
                float _RimThreshold;
                float4 _EmissionColor;
                float _BumpScale;
                float _OcclusionStrength;
                float _HatchingDensity;
                float _HatchingIntensity;
                float _HatchingAngle;
                float _Metallic;
                float _Smoothness;
                float _Surface;
                float _Cull;
                float _ZWrite;
                float _SrcBlend;
                float _DstBlend;
                float _Blend;
            CBUFFER_END

            struct DepthAttributes
            {
                float4 positionOS : POSITION;
            };

            struct DepthVaryings
            {
                float4 positionCS : SV_POSITION;
            };

            DepthVaryings DepthVert(DepthAttributes input)
            {
                DepthVaryings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }

            half4 DepthFrag(DepthVaryings input) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }

        // ======= DEPTH NORMALS =======
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }

            ZWrite On
            Cull [_Cull]

            HLSLPROGRAM
            #pragma vertex DepthNormalsVert
            #pragma fragment DepthNormalsFrag
            #pragma multi_compile _ _NORMALMAP

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _BaseMap_ST;
                float4 _ShadowColor;
                float _ShadowThreshold;
                float _ShadowSmoothness;
                float _MidThreshold;
                float _MidSmoothness;
                float4 _SpecularColor;
                float _SpecularSize;
                float _Glossiness;
                float4 _RimColor;
                float _RimAmount;
                float _RimThreshold;
                float4 _EmissionColor;
                float _BumpScale;
                float _OcclusionStrength;
                float _HatchingDensity;
                float _HatchingIntensity;
                float _HatchingAngle;
                float _Metallic;
                float _Smoothness;
                float _Surface;
                float _Cull;
                float _ZWrite;
                float _SrcBlend;
                float _DstBlend;
                float _Blend;
            CBUFFER_END

            TEXTURE2D(_BumpMap); SAMPLER(sampler_BumpMap);

            struct DNAttributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float4 tangentOS  : TANGENT;
                float2 uv         : TEXCOORD0;
            };

            struct DNVaryings
            {
                float4 positionCS  : SV_POSITION;
                float3 normalWS    : TEXCOORD0;
                float3 tangentWS   : TEXCOORD1;
                float3 bitangentWS : TEXCOORD2;
                float2 uv          : TEXCOORD3;
            };

            DNVaryings DepthNormalsVert(DNAttributes input)
            {
                DNVaryings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.tangentWS = TransformObjectToWorldDir(input.tangentOS.xyz);
                output.bitangentWS = cross(output.normalWS, output.tangentWS) * input.tangentOS.w;
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return output;
            }

            half4 DepthNormalsFrag(DNVaryings input) : SV_Target
            {
                float3 normalWS = normalize(input.normalWS);

                #ifdef _NORMALMAP
                {
                    float3 normalTS = UnpackNormalScale(
                        SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, input.uv), _BumpScale);
                    float3x3 TBN = float3x3(
                        normalize(input.tangentWS),
                        normalize(input.bitangentWS),
                        normalWS);
                    normalWS = normalize(mul(normalTS, TBN));
                }
                #endif

                return half4(normalWS * 0.5 + 0.5, 0);
            }
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Lit"
}
