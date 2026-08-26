Shader "GameArea/PolyhedronEdgeHighlight"
{
    // Highlights ALL hard edges of an arbitrary convex (or non-convex) polyhedron mesh,
    // with no extra vertex data, no geometry shader (unsupported on Metal, relevant on
    // macOS/iOS builds) and no dependency on a specific mesh topology.
    //
    // TECHNIQUE
    // ---------
    // A "polyhedron edge" is, by definition, a place where two flat faces with DIFFERENT
    // normals meet. If the mesh uses hard/flat face normals (i.e. vertices are NOT
    // shared/welded across a face boundary - the standard way faceted "hard surface" or
    // STL-derived low-poly meshes are authored, and the same assumption the existing
    // HexBlockEdge.shader relies on via `dot(normalWS, up)`), then the interpolated
    // world-space normal is constant across the interior of each face and jumps sharply
    // exactly on the shared edge between two faces.
    //
    // We measure that jump per-pixel with screen-space partial derivatives:
    //     fwidth(normalWS) = |ddx(normalWS)| + |ddy(normalWS)|
    // This is ~0 in the interior of a face and spikes to a large value on the 1-2 pixels
    // that straddle an edge, giving a free, distance-correct, anti-aliasable edge mask
    // with zero CPU/import-time work and zero extra vertex channels.
    //
    // NOTE: if a mesh instead uses smoothed/averaged normals across edges, normalWS will
    // NOT jump at the edge and this technique will not find anything to highlight there -
    // in that case either flip the mesh/model's normals to hard/faceted, or use a
    // baked-barycentric wireframe technique instead (needs an import-time vertex split,
    // out of scope for a shader-only change).

    Properties
    {
        [MainTexture] _BaseMap("Base Map", 2D) = "white" {}
        [MainColor] _BaseColor("Base Color", Color) = (0.8, 0.8, 0.8, 1)

        _EdgeColor("Edge Color", Color) = (1, 0.9, 0.2, 1)
        _EdgeThreshold("Edge Threshold", Range(0.001, 2.0)) = 0.35
        _EdgeFeather("Edge Feather", Range(0.001, 1.0)) = 0.2
        _EdgeWidthMultiplier("Edge Width Multiplier", Range(0.1, 8.0)) = 1.5
        _EdgeIntensity("Edge Intensity", Range(0, 3)) = 1
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
                half4  _EdgeColor;
                float  _EdgeThreshold;
                float  _EdgeFeather;
                float  _EdgeWidthMultiplier;
                float  _EdgeIntensity;
                float  _PulseSpeed;
                float  _PulseAmount;
                float  _Smoothness;
                float  _Metallic;
            CBUFFER_END

            #ifdef DOTS_INSTANCING_ON
            UNITY_DOTS_INSTANCING_START(MaterialPropertyMetadata)
                UNITY_DOTS_INSTANCED_PROP(float4, _BaseColor)
                UNITY_DOTS_INSTANCED_PROP(float4, _EdgeColor)
                UNITY_DOTS_INSTANCED_PROP(float,  _EdgeThreshold)
                UNITY_DOTS_INSTANCED_PROP(float,  _EdgeFeather)
                UNITY_DOTS_INSTANCED_PROP(float,  _EdgeWidthMultiplier)
                UNITY_DOTS_INSTANCED_PROP(float,  _EdgeIntensity)
                UNITY_DOTS_INSTANCED_PROP(float,  _PulseSpeed)
                UNITY_DOTS_INSTANCED_PROP(float,  _PulseAmount)
                UNITY_DOTS_INSTANCED_PROP(float,  _Smoothness)
                UNITY_DOTS_INSTANCED_PROP(float,  _Metallic)
            UNITY_DOTS_INSTANCING_END(MaterialPropertyMetadata)

            #define _BaseColor           UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float4, _BaseColor)
            #define _EdgeColor           UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float4, _EdgeColor)
            #define _EdgeThreshold       UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float,  _EdgeThreshold)
            #define _EdgeFeather         UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float,  _EdgeFeather)
            #define _EdgeWidthMultiplier UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float,  _EdgeWidthMultiplier)
            #define _EdgeIntensity       UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float,  _EdgeIntensity)
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

                // --- Edge detection via screen-space normal discontinuity ---
                // Constant (~0) inside a flat face, spikes on the pixels straddling a
                // hard edge between two differently-oriented faces.
                half3 dN = fwidth(normalWS);
                half edgeSignal = (dN.x + dN.y + dN.z) * _EdgeWidthMultiplier;

                half edgeMask = smoothstep(_EdgeThreshold - _EdgeFeather,
                                            _EdgeThreshold + _EdgeFeather,
                                            edgeSignal);

                // Optional pulsing glow so edges read clearly even in a static screenshot.
                half pulse = 1.0 + _PulseAmount * sin(_Time.y * _PulseSpeed);
                half edgeStrength = edgeMask * _EdgeIntensity * pulse;

                half3 finalRGB = lerp(diffuse, _EdgeColor.rgb, saturate(edgeStrength) * _EdgeColor.a);
                finalRGB += _EdgeColor.rgb * max(0.0, edgeStrength - 1.0); // extra glow above full mask

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
