Shader "Hidden/OutlineEdgeDetect"
{
    Properties
    {
        _OutlineColor ("Outline Color", Color) = (0, 0, 0, 1)
        _OutlineThickness ("Outline Thickness", Range(0, 5)) = 2.0
        _DepthThreshold ("Depth Threshold", Range(0, 10)) = 1.5
        _NormalThreshold ("Normal Threshold", Range(0, 2)) = 0.4
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        LOD 100
        ZWrite Off ZTest Always Cull Off

        Pass
        {
            Name "OutlineEdgeDetect"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareNormalsTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            float4 _OutlineColor;
            float _OutlineThickness;
            float _DepthThreshold;
            float _NormalThreshold;

            // Roberts Cross edge detection on depth
            float DepthEdge(float2 uv, float2 texelSize)
            {
                float halfThick = _OutlineThickness * 0.5;

                float d00 = SampleSceneDepth(uv + float2(-halfThick, -halfThick) * texelSize);
                float d11 = SampleSceneDepth(uv + float2( halfThick,  halfThick) * texelSize);
                float d01 = SampleSceneDepth(uv + float2(-halfThick,  halfThick) * texelSize);
                float d10 = SampleSceneDepth(uv + float2( halfThick, -halfThick) * texelSize);

                float ld00 = LinearEyeDepth(d00, _ZBufferParams);
                float ld11 = LinearEyeDepth(d11, _ZBufferParams);
                float ld01 = LinearEyeDepth(d01, _ZBufferParams);
                float ld10 = LinearEyeDepth(d10, _ZBufferParams);

                float depthDiff1 = ld11 - ld00;
                float depthDiff2 = ld10 - ld01;
                float depthEdge = sqrt(depthDiff1 * depthDiff1 + depthDiff2 * depthDiff2);

                float centerDepth = LinearEyeDepth(SampleSceneDepth(uv), _ZBufferParams);
                float scaledThreshold = _DepthThreshold * centerDepth * 0.05;

                return step(scaledThreshold, depthEdge);
            }

            // Roberts Cross edge detection on normals
            float NormalEdge(float2 uv, float2 texelSize)
            {
                float halfThick = _OutlineThickness * 0.5;

                float3 n00 = SampleSceneNormals(uv + float2(-halfThick, -halfThick) * texelSize);
                float3 n11 = SampleSceneNormals(uv + float2( halfThick,  halfThick) * texelSize);
                float3 n01 = SampleSceneNormals(uv + float2(-halfThick,  halfThick) * texelSize);
                float3 n10 = SampleSceneNormals(uv + float2( halfThick, -halfThick) * texelSize);

                float3 normalDiff1 = n11 - n00;
                float3 normalDiff2 = n10 - n01;
                float normalEdge = sqrt(dot(normalDiff1, normalDiff1) + dot(normalDiff2, normalDiff2));

                return step(_NormalThreshold, normalEdge);
            }

            half4 frag(Varyings input) : SV_Target
            {
                float2 uv = input.texcoord;
                float2 texelSize = _BlitTexture_TexelSize.xy;

                // Compute edges
                float depthEdge = DepthEdge(uv, texelSize);
                float normalEdge = NormalEdge(uv, texelSize);

                // Combine: either depth OR normal edge triggers outline
                float edge = saturate(max(depthEdge, normalEdge));

                // Sample original scene color
                half4 sceneColor = SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, uv);

                // Blend outline color over scene
                half4 result = lerp(sceneColor, half4(_OutlineColor.rgb, 1.0), edge * _OutlineColor.a);

                return result;
            }
            ENDHLSL
        }
    }
}
