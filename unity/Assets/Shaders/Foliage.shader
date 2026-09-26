// 村の庭の花と葉の札。絵（VillageFlora.png のアトラス）が色と形を持ち、α で抜く。
//
// **札は裏表を描き、法線は裏返さない。** 札は交差させて株にしているので、どちらの面も
// 見える。URP の Lit を両面にすると裏の面で法線が逆を向き、日の側の株の半分が暗く沈んで
// 札の継ぎ目が見える。法線は組み立て（Bank.AtlasCard）が上へ倒して渡すので、そのまま使う。
//
// 光は日（影を受ける）と環境光だけ。回り込み（_Wrap）で日の裏も沈みきらないようにし、
// 夕方の低い日に透ける花びらの明るみを _Glow で足す。霧は URP の霧（ExponentialSquared）を掛ける。
// 影を落とす pass も持つ。低い日の長い影が芝に落ちないと、花の縁が芝の上に浮いて見える
Shader "HalfAware/Foliage"
{
    Properties
    {
        _BaseMap ("花と葉のアトラス", 2D) = "white" {}
        _BaseColor ("色", Color) = (1, 1, 1, 1)
        _Cutoff ("α を抜く閾値", Range(0, 1)) = 0.5
        _Wrap ("光の回り込み", Range(0, 1)) = 0.5
        _Glow ("日に透ける明るみ", Range(0, 1)) = 0.25
        _Shade ("根元の陰り。uv1.y が 0 の所でこれだけ暗くする", Range(0, 1)) = 0.35
    }

    SubShader
    {
        Tags { "RenderType" = "TransparentCutout" "Queue" = "AlphaTest" "RenderPipeline" = "UniversalPipeline" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        TEXTURE2D(_BaseMap);
        SAMPLER(sampler_BaseMap);

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4 _BaseColor;
            half _Cutoff;
            half _Wrap;
            half _Glow;
            half _Shade;
        CBUFFER_END
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                float2 root : TEXCOORD1;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
                float fogCoord : TEXCOORD3;
                float high : TEXCOORD4;
            };

            Varyings Vert(Attributes v)
            {
                Varyings o;
                float3 world = TransformObjectToWorld(v.positionOS.xyz);
                o.positionWS = world;
                o.positionCS = TransformWorldToHClip(world);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.uv = TRANSFORM_TEX(v.uv, _BaseMap);
                o.fogCoord = ComputeFogFactor(o.positionCS.z);
                o.high = v.root.y;
                return o;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                half4 tex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv);
                clip(tex.a - _Cutoff);
                half3 n = normalize(i.normalWS);
                float4 shadowCoord = TransformWorldToShadowCoord(i.positionWS);
                Light sun = GetMainLight(shadowCoord);
                half ndl = dot(n, sun.direction);
                half wrapped = saturate((ndl + _Wrap) / (1.0 + _Wrap));
                half lit = sun.shadowAttenuation * sun.distanceAttenuation;
                half3 albedo = tex.rgb * _BaseColor.rgb;
                // 根元ほど暗い。株の中へ光が届かない
                half deep = lerp(1.0 - _Shade, 1.0, saturate(i.high));
                half3 col = albedo * deep * (SampleSH(n) + sun.color * lit * (wrapped + _Glow * (1.0 - wrapped)));
                col = MixFog(col, i.fogCoord);
                return half4(col, 1.0);
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
            Cull Off

            HLSLPROGRAM
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            struct ShadowIn
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct ShadowOut
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            ShadowOut ShadowVert(ShadowIn v)
            {
                ShadowOut o;
                float3 world = TransformObjectToWorld(v.positionOS.xyz);
                float3 normal = TransformObjectToWorldNormal(v.normalOS);
            #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                float3 toLight = normalize(_LightPosition - world);
            #else
                float3 toLight = _LightDirection;
            #endif
                float4 cs = TransformWorldToHClip(ApplyShadowBias(world, normal, toLight));
            #if UNITY_REVERSED_Z
                cs.z = min(cs.z, UNITY_NEAR_CLIP_VALUE);
            #else
                cs.z = max(cs.z, UNITY_NEAR_CLIP_VALUE);
            #endif
                o.positionCS = cs;
                o.uv = TRANSFORM_TEX(v.uv, _BaseMap);
                return o;
            }

            half4 ShadowFrag(ShadowOut i) : SV_Target
            {
                clip(SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv).a - _Cutoff);
                return 0;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R
            Cull Off

            HLSLPROGRAM
            #pragma vertex DepthVert
            #pragma fragment DepthFrag

            struct DepthIn
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct DepthOut
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            DepthOut DepthVert(DepthIn v)
            {
                DepthOut o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = TRANSFORM_TEX(v.uv, _BaseMap);
                return o;
            }

            half DepthFrag(DepthOut i) : SV_Target
            {
                clip(SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv).a - _Cutoff);
                return 0;
            }
            ENDHLSL
        }
    }

    Fallback Off
}
