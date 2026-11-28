Shader "Butterfly Rebellion/Creek Water"
{
    Properties
    {
        _ShallowColor ("Shallow Water", Color) = (0.27, 0.57, 0.51, 1)
        _DeepColor ("Deep Water", Color) = (0.055, 0.32, 0.35, 1)
        _SkyTint ("Sky Reflection Tint", Color) = (0.56, 0.73, 0.80, 1)
        _FoamColor ("Foam", Color) = (0.82, 0.91, 0.85, 1)
        _FlowDirection ("Flow Direction (World XZ)", Vector) = (1, 0, 0, 0)
        _FlowSpeed ("Flow Speed (m/s)", Range(0, 3)) = 0.85
        _RippleStrength ("Ripple Strength", Range(0, 0.4)) = 0.17
        _Refraction ("Refraction", Range(0, 0.05)) = 0.014
        _Absorption ("Water Absorption", Range(0.1, 4)) = 1.65
        _FoamWidth ("Foam Width (m)", Range(0.02, 0.4)) = 0.14
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Transparent-50"
        }

        Pass
        {
            Name "Creek Water"
            Tags { "LightMode" = "UniversalForwardOnly" }
            Cull Back
            ZWrite Off

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex WaterVertex
            #pragma fragment WaterFragment
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _ShallowColor;
                half4 _DeepColor;
                half4 _SkyTint;
                half4 _FoamColor;
                float4 _FlowDirection;
                float _FlowSpeed;
                float _RippleStrength;
                float _Refraction;
                float _Absorption;
                float _FoamWidth;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float eyeDepth : TEXCOORD1;
                half fog : TEXCOORD2;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings WaterVertex(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                VertexPositionInputs position = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = position.positionCS;
                output.positionWS = position.positionWS;
                output.eyeDepth = -position.positionVS.z;
                output.fog = ComputeFogFactor(position.positionCS.z);
                return output;
            }

            float SceneEyeDepth(float2 uv)
            {
                float depth = SampleSceneDepth(uv);
                return IsPerspectiveProjection()
                    ? LinearEyeDepth(depth, _ZBufferParams)
                    : LinearDepthToEyeDepth(depth);
            }

            float2 RippleSlope(float2 position, float2 wave, float weight)
            {
                float angle = dot(position, wave);
                float visibility = 1.0 - smoothstep(0.45, 1.5, fwidth(angle));
                return normalize(wave) * (cos(angle) * weight * visibility);
            }

            half4 WaterFragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 screenUV = GetNormalizedScreenSpaceUV(input.positionCS);
                float2 flow = _FlowDirection.xy * rsqrt(max(dot(_FlowDirection.xy, _FlowDirection.xy), 0.0001));
                float2 current = input.positionWS.xz - flow * (_Time.y * _FlowSpeed);
                float2 slope = RippleSlope(current, float2(1.7, 3.2), 0.58)
                             + RippleSlope(current, float2(3.5, -2.1), 0.28)
                             + RippleSlope(current, float2(7.4, 4.9), 0.14);
                half3 normalWS = normalize(float3(slope.x * _RippleStrength, 1.0, slope.y * _RippleStrength));
                half3 viewWS = GetWorldSpaceNormalizeViewDir(input.positionWS);

                // Estimate vertical depth from the distance between the surface and opaque creek bed.
                float viewDepth = max(0.0, SceneEyeDepth(screenUV) - input.eyeDepth);
                float depth = viewDepth * max(viewWS.y, 0.14);
                float coverage = saturate(depth / 0.16);
                float2 normalVS = TransformWorldToViewDir(normalWS).xy;
                float2 offset = normalVS - TransformWorldToViewDir(float3(0, 1, 0)).xy;
                float2 refractedUV = saturate(screenUV + offset * (_Refraction * coverage));
                // Reject samples in front of the surface to keep the banks and bridge from bleeding into the water.
                float refractedDepth = SceneEyeDepth(refractedUV) - input.eyeDepth;
                half3 behind = SampleSceneColor(lerp(screenUV, refractedUV, smoothstep(0.05, 0.30, refractedDepth)));

                half transmission = exp2(-depth * _Absorption);
                half3 tint = lerp(_DeepColor.rgb, _ShallowColor.rgb, transmission);
                half noV = saturate(dot(normalWS, viewWS));
                half grazing = 1.0 - noV;
                half grazingSquared = grazing * grazing;
                half fresnel = 0.02 + 0.98 * grazingSquared * grazingSquared * grazing;
                half3 reflected = GlossyEnvironmentReflection(reflect(-viewWS, normalWS), 0.17, 1.0);

                Light sun = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                half3 halfVector = SafeNormalize(sun.direction + viewWS);
                half glare = pow(saturate(dot(normalWS, halfVector)), 105.0) * saturate(dot(normalWS, sun.direction));
                half crest = smoothstep(0.22, 0.66, dot(slope, float2(0.65, 0.76)));
                half3 sky = max(reflected, _SkyTint.rgb * 0.6);
                half3 reflection = sky * (fresnel + crest * (0.055 + 0.22 * fresnel))
                                 + sun.color * glare * sun.shadowAttenuation * 0.72;

                float foamNoise = 0.5 + 0.3 * sin(dot(current, float2(3.3, 6.1)))
                                    + 0.2 * sin(dot(current, float2(7.9, -5.3)));
                float foamLimit = _FoamWidth * lerp(0.45, 1.3, foamNoise);
                float foamPatch = 0.55 + 0.45 * sin(dot(current, float2(0.63, 1.08)));
                half foam = smoothstep(0.02, 0.05, depth)
                          * (1.0 - smoothstep(foamLimit * 0.55, foamLimit, depth))
                          * smoothstep(0.50, 0.82, foamNoise) * foamPatch * 0.43;

                half opacity = saturate(1.0 - transmission * (1.0 - fresnel));
                half3 water = behind * (1.0 - opacity)
                            + MixFog(tint, input.fog) * opacity
                            + reflection * coverage;
                // The camera's opaque texture is already included in the color above.
                return half4(lerp(water, MixFog(_FoamColor.rgb, input.fog), foam), 1.0);
            }
            ENDHLSL
        }
    }
}

