Shader "SyntyStudios/Zombies"
{
    Properties
    {
        [MainTexture] _Texture("Texture", 2D) = "white" {}
        _Blood("Blood", 2D) = "white" {}
        _BloodColor("BloodColor", Color) = (0.6470588, 0.2569204, 0.2569204, 0)
        _BloodAmount("BloodAmount", Range(0, 1)) = 0
        _Emissive("Emissive", 2D) = "white" {}
        [HDR] _EmissiveColor("Emissive Color", Color) = (0, 0, 0, 0)
        _Glossiness("Glossiness", Range(0, 1)) = 0.5
        _SpecularHighlights("Specular Highlights", Float) = 1
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
            "RenderPipeline" = "UniversalPipeline"
            "IsEmissive" = "true"
        }
        LOD 200
        Cull Back

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForwardOnly" }

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex Vert
            #pragma fragment Frag

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _LIGHT_COOKIES
            #pragma multi_compile _ _LIGHT_LAYERS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fragment _ _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _Texture_ST;
                float4 _Blood_ST;
                float4 _Emissive_ST;
                half4 _BloodColor;
                half4 _EmissiveColor;
                half _BloodAmount;
                half _Glossiness;
                half _SpecularHighlights;
            CBUFFER_END

            TEXTURE2D(_Texture);
            SAMPLER(sampler_Texture);
            TEXTURE2D(_Blood);
            SAMPLER(sampler_Blood);
            TEXTURE2D(_Emissive);
            SAMPLER(sampler_Emissive);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 tangentOS : TANGENT;
                float2 uv : TEXCOORD0;
                float2 uv2 : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float2 uv : TEXCOORD0;
                float2 uv2 : TEXCOORD1;
                float3 positionWS : TEXCOORD2;
                half3 normalWS : TEXCOORD3;
                half fogFactor : TEXCOORD4;
            #if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
                float4 shadowCoord : TEXCOORD5;
            #endif
            #ifdef _ADDITIONAL_LIGHTS_VERTEX
                half3 vertexLighting : TEXCOORD6;
            #endif
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;

                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS, input.tangentOS);

                output.positionCS = vertexInput.positionCS;
                output.positionWS = vertexInput.positionWS;
                output.normalWS = NormalizeNormalPerVertex(normalInput.normalWS);
                output.uv = input.uv;
                output.uv2 = input.uv2;

            #if defined(_FOG_FRAGMENT)
                output.fogFactor = vertexInput.positionVS.z;
            #else
                output.fogFactor = ComputeFogFactor(vertexInput.positionCS.z);
            #endif

            #if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
                output.shadowCoord = GetShadowCoord(vertexInput);
            #endif

            #ifdef _ADDITIONAL_LIGHTS_VERTEX
                output.vertexLighting = VertexLighting(vertexInput.positionWS, normalInput.normalWS);
            #endif

                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                half2 uvBase = TRANSFORM_TEX(input.uv, _Texture);
                half2 uvBlood = TRANSFORM_TEX(input.uv2, _Blood);
                half2 uvEmissive = TRANSFORM_TEX(input.uv, _Emissive);

                half4 baseSample = SAMPLE_TEXTURE2D(_Texture, sampler_Texture, uvBase);
                half4 bloodSample = SAMPLE_TEXTURE2D(_Blood, sampler_Blood, uvBlood);
                half4 bloodBlend = lerp(half4(0, 0, 0, 0), bloodSample, _BloodAmount);

                half3 albedo = lerp(baseSample.rgb, _BloodColor.rgb, bloodBlend.rgb);
                half3 emission = SAMPLE_TEXTURE2D(_Emissive, sampler_Emissive, uvEmissive).rgb * _EmissiveColor.rgb;

                half3 normalWS = NormalizeNormalPerPixel(input.normalWS);
                half3 viewDirWS = SafeNormalize(GetWorldSpaceViewDir(input.positionWS));
                half smoothnessPower = exp2(10.0h * _Glossiness + 1.0h);
                half specIntensity = 0.2h * _SpecularHighlights;
                half4 specularParams = half4(specIntensity, specIntensity, specIntensity, 1.0h);

                float4 shadowCoord;
            #if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
                shadowCoord = input.shadowCoord;
            #elif defined(MAIN_LIGHT_CALCULATE_SHADOWS)
                shadowCoord = TransformWorldToShadowCoord(input.positionWS);
            #else
                shadowCoord = float4(0, 0, 0, 0);
            #endif

                half3 diffuseLighting = SampleSH(normalWS);
                half3 specularLighting = half3(0, 0, 0);

                Light mainLight = GetMainLight(shadowCoord);
                half3 mainAttenuatedColor = mainLight.color * (mainLight.distanceAttenuation * mainLight.shadowAttenuation);
                diffuseLighting += LightingLambert(mainAttenuatedColor, mainLight.direction, normalWS);
                specularLighting += LightingSpecular(mainAttenuatedColor, mainLight.direction, normalWS, viewDirWS, specularParams, smoothnessPower);

            #ifdef _ADDITIONAL_LIGHTS_VERTEX
                diffuseLighting += input.vertexLighting;
            #endif

            #if defined(_ADDITIONAL_LIGHTS)
                uint pixelLightCount = GetAdditionalLightsCount();
                LIGHT_LOOP_BEGIN(pixelLightCount)
                    Light light = GetAdditionalLight(lightIndex, input.positionWS);
                    half3 attenuatedColor = light.color * (light.distanceAttenuation * light.shadowAttenuation);
                    diffuseLighting += LightingLambert(attenuatedColor, light.direction, normalWS);
                    specularLighting += LightingSpecular(attenuatedColor, light.direction, normalWS, viewDirWS, specularParams, smoothnessPower);
                LIGHT_LOOP_END
            #endif

                half3 color = albedo * diffuseLighting + specularLighting + emission;
                half fogCoord = InitializeInputDataFog(float4(input.positionWS, 1.0), input.fogFactor);
                color = MixFog(color, fogCoord);

                return half4(color, 1.0h);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #include "Packages/com.unity.render-pipelines.universal/Shaders/ShadowCasterPass.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask R

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex DepthOnlyVertex
            #pragma fragment DepthOnlyFragment
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/Shaders/DepthOnlyPass.hlsl"
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
