Shader "GameArea/PolyhedronFaceHighlight"
{
    // Highlights a single flat face of an arbitrary convex polyhedron mesh.
    //
    // The face to highlight is identified by its OBJECT-SPACE normal direction
    // (_HighlightNormalOS), not by a face-index vertex channel, so this works with
    // any mesh that has no baked per-face ID data (e.g. imported FBX/STL platforms).
    //
    // The actual per-pixel face normal is reconstructed from screen-space derivatives
    // of the world position (ddx/ddy), so the result is correct even if the mesh uses
    // smoothed vertex normals across face edges - only the true flat-face geometry matters.
    //
    // Usage from ECS/DOTS: write per-instance value of _HighlightNormalOS
    // (URPMaterialPropertyBaseColor-style override) with the object-space normal of the
    // face you want highlighted (e.g. (0,1,0,0) for a face facing local +Y), and it will
    // stay correct as the entity rotates because the comparison happens in world space
    // after transforming _HighlightNormalOS by the instance's object-to-world matrix.

    Properties
    {
        [MainTexture] _BaseMap("Base Map", 2D) = "white" {}
        [MainColor] _BaseColor("Base Color", Color) = (0.8, 0.8, 0.8, 1)

        _HighlightColor("Highlight Color", Color) = (1, 0.85, 0.2, 1)
        [Normal] _HighlightNormalOS("Highlighted Face Normal (Object Space)", Vector) = (0, 1, 0, 0)
        _HighlightThreshold("Highlight Threshold", Range(0.5, 0.999)) = 0.97
        _HighlightFeather("Highlight Feather", Range(0.001, 0.2)) = 0.03
        _HighlightIntensity("Highlight Intensity", Range(0, 3)) = 1
        _PulseSpeed("Pulse Speed", Range(0, 10)) = 0
        _PulseAmount("Pulse Amount", Range(0, 1)) = 0

        _Smoothness("Smoothness", Range(0,1)) = 0.5
        _Metallic("Metallic", Range(0,1)) = 0
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _SHADOWS_SOFT
            #pragma multi_compile_instancing
            #pragma instancing_options renderinglayer
            #pragma multi_compile _ DOTS_INSTANCING_ON
            #pragma target 4.5

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4  _BaseColor;
                half4  _HighlightColor;
                float4 _HighlightNormalOS;
                float  _HighlightThreshold;
                float  _HighlightFeather;
                float  _HighlightIntensity;
                float  _PulseSpeed;
                float  _PulseAmount;
                float  _Smoothness;
                float  _Metallic;
            CBUFFER_END

            #ifdef DOTS_INSTANCING_ON
            UNITY_DOTS_INSTANCING_START(MaterialPropertyMetadata)
                UNITY_DOTS_INSTANCED_PROP(float4, _BaseColor)
                UNITY_DOTS_INSTANCED_PROP(float4, _HighlightColor)
                UNITY_DOTS_INSTANCED_PROP(float4, _HighlightNormalOS)
                UNITY_DOTS_INSTANCED_PROP(float,  _HighlightThreshold)
                UNITY_DOTS_INSTANCED_PROP(float,  _HighlightFeather)
                UNITY_DOTS_INSTANCED_PROP(float,  _HighlightIntensity)
                UNITY_DOTS_INSTANCED_PROP(float,  _PulseSpeed)
                UNITY_DOTS_INSTANCED_PROP(float,  _PulseAmount)
                UNITY_DOTS_INSTANCED_PROP(float,  _Smoothness)
                UNITY_DOTS_INSTANCED_PROP(float,  _Metallic)
            UNITY_DOTS_INSTANCING_END(MaterialPropertyMetadata)

            #define _BaseColor           UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float4, _BaseColor)
            #define _HighlightColor      UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float4, _HighlightColor)
            #define _HighlightNormalOS   UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float4, _HighlightNormalOS)
            #define _HighlightThreshold  UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float,  _HighlightThreshold)
            #define _HighlightFeather    UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float,  _HighlightFeather)
            #define _HighlightIntensity  UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float,  _HighlightIntensity)
            #define _PulseSpeed          UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float,  _PulseSpeed)
            #define _PulseAmount         UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float,  _PulseAmount)
            #define _Smoothness          UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float,  _Smoothness)
            #define _Metallic            UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float,  _Metallic)
            #endif

            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                half3  normalWS   : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

                VertexPositionInputs posInputs  = GetVertexPositionInputs(IN.positionOS.xyz);
                VertexNormalInputs   normInputs = GetVertexNormalInputs(IN.normalOS);

                OUT.positionCS = posInputs.positionCS;
                OUT.positionWS = posInputs.positionWS;
                OUT.normalWS   = normInputs.normalWS;
                OUT.uv         = TRANSFORM_TEX(IN.uv, _BaseMap);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(IN);

                half4 baseSample = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv) * _BaseColor;

                half3 normalWS = normalize(IN.normalWS);
                Light mainLight = GetMainLight();
                half NdotL = saturate(dot(normalWS, mainLight.direction));
                half3 diffuse = baseSample.rgb * (0.35 + 0.65 * NdotL * mainLight.color);

                // Reconstruct the true flat-face normal from screen-space derivatives of
                // the world position. This is independent of vertex-normal smoothing and
                // correctly identifies which planar face a pixel belongs to.
                half3 faceNormalWS = normalize(cross(ddy(IN.positionWS), ddx(IN.positionWS)));
                if (dot(faceNormalWS, normalWS) < 0.0)
                {
                    faceNormalWS = -faceNormalWS;
                }

                // Transform the target face normal from object space to world space,
                // taking the per-instance object-to-world matrix (rotation) into account.
                half3 highlightNormalWS = normalize(TransformObjectToWorldNormal(_HighlightNormalOS.xyz, true));

                half faceMatch = dot(faceNormalWS, highlightNormalWS);
                half highlightMask = smoothstep(_HighlightThreshold - _HighlightFeather,
                                                 _HighlightThreshold + _HighlightFeather,
                                                 faceMatch);

                // Optional pulsing glow so the highlighted face reads clearly even on a static image.
                half pulse = 1.0 + _PulseAmount * sin(_Time.y * _PulseSpeed);
                half highlightStrength = highlightMask * _HighlightIntensity * pulse;

                half3 finalRGB = lerp(diffuse, _HighlightColor.rgb, saturate(highlightStrength) * _HighlightColor.a);
                finalRGB += _HighlightColor.rgb * max(0.0, highlightStrength - 1.0); // extra glow when boosted above 1

                // Specular
                half3 viewDir = normalize(GetWorldSpaceViewDir(IN.positionWS));
                half3 h = normalize(mainLight.direction + viewDir);
                half spec = pow(saturate(dot(normalWS, h)), 64.0 * _Smoothness + 1.0);
                finalRGB += spec * 0.15 * lerp(half3(0.04, 0.04, 0.04), baseSample.rgb, _Metallic);

                return half4(finalRGB, baseSample.a);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex vertShadow
            #pragma fragment fragShadow
            #pragma multi_compile_instancing
            #pragma multi_compile _ DOTS_INSTANCING_ON
            #pragma target 4.5

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            #ifdef DOTS_INSTANCING_ON
            UNITY_DOTS_INSTANCING_START(MaterialPropertyMetadata)
            UNITY_DOTS_INSTANCING_END(MaterialPropertyMetadata)
            #endif

            struct Attr
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Vary
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            float3 _LightDirection;

            Vary vertShadow(Attr IN)
            {
                Vary OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);
                float3 posWS  = TransformObjectToWorld(IN.positionOS.xyz);
                float3 normWS = TransformObjectToWorldNormal(IN.normalOS);
                float4 posCS  = TransformWorldToHClip(ApplyShadowBias(posWS, normWS, _LightDirection));
                #if UNITY_REVERSED_Z
                    posCS.z = min(posCS.z, UNITY_NEAR_CLIP_VALUE);
                #else
                    posCS.z = max(posCS.z, UNITY_NEAR_CLIP_VALUE);
                #endif
                OUT.positionCS = posCS;
                return OUT;
            }

            half4 fragShadow(Vary IN) : SV_Target { return 0; }
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
