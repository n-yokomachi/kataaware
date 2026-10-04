// 同じ顔の三人（主人公・片割れ・場面 6 の過去の主人公）の、リアル系のアニメ寄りの肌と髪（Assets/Editor/Rocketbox/RocketboxAnimeFace.cs）。
// 頭・髪の房・体・胸元・膝から下のマテリアルが使う。
//
// 一つのマテリアルの中で、部位の絵（_MaskMap。R = 肌（0.5 で体の肌、1 で顔の肌）、G = 髪、B = 艶）が画素ごとに陰影の付け方を選ぶ。
// 体の肌（腕・脚・首から下）は陰を深く、URP の Lit に近く当てる（_Body*。暗い部屋で一人称の腕が光って見えないように）。
// どれでもない所（服・靴・腕時計）は、URP の Lit の拡散と同じ当て方（照り返しは無し）。
//
// - 肌: 影を柔らかい段（ランプ）にする。光の側は向きで少し落ち、明暗の境（_RampCentre ± _RampSoft）で
//   赤みのある影の色（_SkinShade）へ落ちる。境のすぐ影の側に、血の色の帯（_SkinTerminator）を薄く足す。
//   光から背けた所（法線と光の向きの内積が _BackFade より小さい所）へ向けて暗くなる
//   （自室の天井の灯りや路地裏のように四方に灯りがある所で、後ろの灯りが顔の前を照らさないように）。
//   リムは下を向いた面（顎の下・首）には回さない。
//   照り返しは描かない（テカりを抑える）。縁に光を回す（リム）。リムの色は、その場の灯りと環境光の色
// - 髪: 肌と同じ段で、影の色は暗く冷たい（_HairShade）。縁に地の色によらない照り（_HairSheen）。光の帯（天使の輪）を、毛の流れ（頭の上から下へ）の向きの
//   Kajiya-Kay の照り返しで描く。帯の向きは見る向きから決め（灯りの向きによらず、同じ所に輪が出る）、明るさは、
//   その場の灯りと環境光の明るさで決める。茶色の髪（片割れ）は、地の色・影・帯・照りの色をマテリアルで暖かくする
// - 艶（B）: 目の玉と唇。灯りの照り返しを小さく描く
//
// 環境光は球面調和を、法線の向きと向きによらない平均の間で混ぜて平らにする（_AmbientFlat）。
// 主灯の影と、追加の灯り（点光源・スポット。Forward+ の輪も）を受ける。霧を掛ける。影を落とす pass と深さの pass を持つ。
// WebGL2（GLES3）で通る（AnimeSkinTests）。テクスチャの読みは二つ、灯りの輪は URP の Lit と同じ作り
Shader "HalfAware/AnimeSkin"
{
    Properties
    {
        _BaseMap ("絵", 2D) = "white" {}
        _BaseColor ("色", Color) = (1, 1, 1, 1)
        _MaskMap ("部位の絵（R 肌（0.5 で体の肌、1 で顔の肌）・G 髪・B 艶。線形）", 2D) = "black" {}
        [Toggle(_ALPHATEST_ON)] _AlphaClip ("α で抜く", Float) = 0
        _Cutoff ("α を抜く閾値", Range(0, 1)) = 0.45
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("描かない面", Float) = 2

        [Header(Ramp)]
        _RampCentre ("明暗の境の位置（法線と光の向きの内積）", Range(-1, 1)) = 0.06
        _RampSoft ("明暗の境のぼかし（内積の幅の半分）", Range(0.01, 1)) = 0.45
        _BackFade ("光が届かなくなる内積（光から背けた所）", Range(-1, 0)) = -0.35
        _LitFloor ("光の側の、光と直角の面の明るさ", Range(0, 1)) = 0.15
        _ShadowStrength ("灯りの影の濃さ（灯りの影の強さに掛ける）", Range(0, 1)) = 1
        _AmbientFlat ("環境光の平らさ", Range(0, 1)) = 0.3
        _AmbientGain ("環境光の強さ", Range(0, 2)) = 1

        [Header(Skin)]
        _SkinShade ("肌の影の色（光の色と地の色に掛ける）", Color) = (0.62, 0.45, 0.44, 1)
        _SkinAmbientWarm ("肌の環境光を暖かい色へ寄せる量（空の青い環境光で影が灰に寄らないように）", Range(0, 1)) = 0.5
        _SkinAmbientColor ("肌の環境光の色（明るさを保って掛ける）", Color) = (1.18, 0.95, 0.88, 1)

        [Header(Body skin)]
        _BodyShade ("体の肌の影の色", Color) = (0.38, 0.28, 0.27, 1)
        _BodyRampCentre ("体の肌の明暗の境の位置", Range(-1, 1)) = 0.0
        _BodyRampSoft ("体の肌の明暗の境のぼかし", Range(0.01, 1)) = 0.4
        _BodyBackFade ("体の肌に光が届かなくなる内積", Range(-1, 0)) = -0.1
        _BodyLitFloor ("体の肌の、光と直角の面の明るさ", Range(0, 1)) = 0.0
        _BodyTerminator ("体の肌の明暗の境の血の色の強さ", Range(0, 1)) = 0.15
        _BodyRim ("体の肌のリムの強さ", Range(0, 2)) = 0.06
        _BodyAmbientFlat ("体の肌の環境光の平らさ", Range(0, 1)) = 0.1
        _SkinTerminator ("明暗の境の血の色", Color) = (0.55, 0.12, 0.08, 1)
        _TerminatorStrength ("明暗の境の血の色の強さ", Range(0, 1)) = 0.35
        _SkinLift ("肌の光の側の明るさ", Range(0.5, 1.5)) = 1.0
        _RimColor ("リムの色", Color) = (1, 0.92, 0.88, 1)
        _RimPower ("リムの細さ", Range(1, 8)) = 3.5
        _RimStrength ("リムの強さ", Range(0, 2)) = 0.3

        [Header(Hair)]
        _HairShade ("髪の影の色", Color) = (0.42, 0.42, 0.50, 1)
        _HairTint ("髪の地の色に掛ける色（黒い髪を青みへ）", Color) = (0.80, 0.86, 1.0, 1)
        _RingColor ("光の帯の色", Color) = (0.55, 0.62, 0.82, 1)
        _RingStrength ("光の帯の強さ", Range(0, 3)) = 0.32
        _RingLift ("光の帯を出す見かけの光の高さ", Range(-1, 2)) = 0.55
        _RingShift ("光の帯の上下のずらし", Range(-1, 1)) = 0.1
        _RingWidth ("光の帯の幅", Range(0.01, 0.6)) = 0.10
        _RingBreak ("光の帯を毛筋で切る量", Range(0, 1)) = 0.85
        _RingAmbient ("光の帯に環境光を数える割合", Range(0, 4)) = 1.6
        _HairRim ("髪のリムの強さ", Range(0, 2)) = 0.35
        _HairSheen ("髪の縁の照り（地の色によらない。黒い髪の輪郭を読ませる）", Color) = (0.24, 0.28, 0.40, 1)
        _SheenPower ("髪の縁の照りの細さ", Range(1, 8)) = 3

        [Header(Gloss)]
        _GlossPower ("艶の鋭さ", Range(4, 256)) = 64
        _GlossStrength ("艶の強さ", Range(0, 2)) = 0.6
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "RenderPipeline" = "UniversalPipeline" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        TEXTURE2D(_BaseMap);
        SAMPLER(sampler_BaseMap);
        TEXTURE2D(_MaskMap);
        SAMPLER(sampler_MaskMap);

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4 _BaseColor;
            half _Cutoff;
            half _RampCentre;
            half _RampSoft;
            half _BackFade;
            half _LitFloor;
            half _ShadowStrength;
            half _AmbientFlat;
            half _AmbientGain;
            half4 _SkinShade;
            half _SkinAmbientWarm;
            half4 _SkinAmbientColor;
            half4 _BodyShade;
            half _BodyRampCentre;
            half _BodyRampSoft;
            half _BodyBackFade;
            half _BodyLitFloor;
            half _BodyTerminator;
            half _BodyRim;
            half _BodyAmbientFlat;
            half4 _SkinTerminator;
            half _TerminatorStrength;
            half _SkinLift;
            half4 _RimColor;
            half _RimPower;
            half _RimStrength;
            half4 _HairShade;
            half4 _HairTint;
            half4 _RingColor;
            half _RingStrength;
            half _RingLift;
            half _RingShift;
            half _RingWidth;
            half _RingBreak;
            half _RingAmbient;
            half _HairRim;
            half4 _HairSheen;
            half _SheenPower;
            half _GlossPower;
            half _GlossStrength;
        CBUFFER_END
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma shader_feature_local_fragment _ALPHATEST_ON
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
                float fogCoord : TEXCOORD3;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes v)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                VertexPositionInputs p = GetVertexPositionInputs(v.positionOS.xyz);
                o.positionWS = p.positionWS;
                o.positionCS = p.positionCS;
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.uv = TRANSFORM_TEX(v.uv, _BaseMap);
                o.fogCoord = ComputeFogFactor(p.positionCS.z);
                return o;
            }

            // 部位ごとの重み（肌・髪・それ以外）
            struct Parts
            {
                half skin;
                half face;
                half hair;
                half cloth;
                half gloss;
                // 肌の段の値（顔の肌と体の肌の間を face で混ぜた物）
                half centre;
                half soft;
                half back;
                half floor;
                half terminator;
                half3 shade;
            };

            // 一つの灯りの、拡散と艶。lightSum には、リムと光の帯の明るさに使う灯りの量を足す
            half3 Shade(Light light, half3 albedo, float3 n, float3 v, Parts k, inout half3 lightSum)
            {
                half3 colour = light.color * light.distanceAttenuation;
                half ndl = dot(n, light.direction);
                half shadow = lerp(1.0h, light.shadowAttenuation, _ShadowStrength);
                // 段: 光の側は 1、境の影の側は 0。影に入った所も 0 へ
                half ramp = smoothstep(k.centre - k.soft, k.centre + k.soft, ndl) * shadow;
                // 光の届く量: 光へ向いた所は 1、光から back（内積）より背けた所は 0。
                // 影の色の明るさをそのまま背けた所まで広げると、後ろの灯り（自室の天井の灯り・路地裏の街灯）が顔の前を照らした
                half reach = smoothstep(k.back, 0.2h, ndl);
                // 光の側も向きで落とす（段だけにすると、斜めの面が真正面と同じ明るさで白く飛び、横からの強い日で顔が鼻筋を境に二色に割れた）
                half lit = lerp(k.floor, 1.0h, saturate(ndl));
                // 境の帯（影の側に寄せる）: ramp が 0.15〜0.6 の所で山
                half band = saturate(ramp * (1.0h - ramp) * 4.0h) * reach;

                // 影の色の側も、主灯の影（ほかの物が落とす影）では暗くする。影の外の明るさだけを段にすると、
                // 壁や天井の影の中（自室の平行光、村の家の陰）でも影の色の明るさが残り、暗い所で肌が光って見えた
                half3 skin = albedo * lerp(k.shade * shadow, lit * _SkinLift.xxx, ramp) * reach;
                skin += albedo * _SkinTerminator.rgb * band * k.terminator;
                half hramp = smoothstep(_RampCentre - _RampSoft, _RampCentre + _RampSoft, ndl) * shadow;
                half hreach = smoothstep(_BackFade, 0.2h, ndl);
                half hlit = lerp(_LitFloor, 1.0h, saturate(ndl));
                half3 hair = albedo * lerp(_HairShade.rgb * shadow, hlit.xxx, hramp) * hreach;
                half3 cloth = albedo * saturate(ndl) * shadow;
                half3 diffuse = skin * k.skin + hair * k.hair + cloth * k.cloth;

                // 艶（目の玉と唇）
                float3 h = SafeNormalize(light.direction + v);
                half spec = pow(saturate(dot(n, h)), _GlossPower) * k.gloss * _GlossStrength * shadow * step(0.0h, ndl);

                // リム・光の帯・髪の縁の照りに数える灯りも、ほかの物の影の中では数えない（家の陰で髪の光の帯だけが光った）
                lightSum += colour * saturate(ndl * 0.5h + 0.5h) * shadow;
                return colour * (diffuse + spec.xxx);
            }

            half4 Frag(Varyings i) : SV_Target
            {
                half4 tex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv) * _BaseColor;
            #if defined(_ALPHATEST_ON)
                clip(tex.a - _Cutoff);
            #endif
                half4 mask = SAMPLE_TEXTURE2D(_MaskMap, sampler_MaskMap, i.uv);
                Parts k;
                // R: 0.5 で体の肌（腕・脚・首から下）、1 で顔の肌。体の肌は陰を深く、URP の Lit に近く（暗い部屋で腕が光って見えないように）
                k.skin = saturate(mask.r * 2.0h);
                k.face = saturate(mask.r * 2.0h - 1.0h);
                k.centre = lerp(_BodyRampCentre, _RampCentre, k.face);
                k.soft = lerp(_BodyRampSoft, _RampSoft, k.face);
                k.back = lerp(_BodyBackFade, _BackFade, k.face);
                k.floor = lerp(_BodyLitFloor, _LitFloor, k.face);
                k.terminator = lerp(_BodyTerminator, _TerminatorStrength, k.face);
                k.shade = lerp(_BodyShade.rgb, _SkinShade.rgb, k.face);
                k.hair = saturate(mask.g) * (1.0h - k.skin);
                k.cloth = saturate(1.0h - k.skin - k.hair);
                k.gloss = saturate(mask.b);
                half3 albedo = tex.rgb * lerp(half3(1.0h, 1.0h, 1.0h), _HairTint.rgb, k.hair);

                float3 n = normalize(i.normalWS);
                float3 v = GetWorldSpaceNormalizeViewDir(i.positionWS);

                InputData inputData = (InputData)0;
                inputData.positionWS = i.positionWS;
                inputData.normalWS = n;
                inputData.viewDirectionWS = v;
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(i.positionCS);
                inputData.shadowCoord = TransformWorldToShadowCoord(i.positionWS);
                half4 shadowMask = half4(1, 1, 1, 1);

                // 環境光。法線の向きの値と、向きによらない平均（表と裏の平均）の間
                half3 shN = SampleSH(n);
                half3 shFlat = (shN + SampleSH(-n)) * 0.5h;
                half flatness = lerp(_AmbientFlat, lerp(_BodyAmbientFlat, _AmbientFlat, k.face), k.skin);
                half3 ambient = lerp(shN, shFlat, flatness) * _AmbientGain;
                // 肌の環境光は、明るさを保って暖かい色へ寄せる（村の朝の青い空の環境光で、影の側の肌が灰に寄った）
                ambient = lerp(ambient, Luminance(ambient) * _SkinAmbientColor.rgb, _SkinAmbientWarm * k.skin);
                // 肌と髪の環境光は影の色を少し通す（影の側の色を揃える）
                half3 ambTint = lerp(half3(1.0h, 1.0h, 1.0h), lerp(k.shade, _HairShade.rgb, k.hair / max(k.skin + k.hair, 1e-3h)) * 0.35h + 0.65h, k.skin + k.hair);
                half3 colour = albedo * ambient * ambTint;

                half3 lightSum = 0;
                Light main = GetMainLight(inputData.shadowCoord, i.positionWS, shadowMask);
                colour += Shade(main, albedo, n, v, k, lightSum);

            #if defined(_ADDITIONAL_LIGHTS)
                uint lightCount = GetAdditionalLightsCount();
            #if USE_CLUSTER_LIGHT_LOOP
                [loop] for (uint lightIndex = 0; lightIndex < min(URP_FP_DIRECTIONAL_LIGHTS_COUNT, MAX_VISIBLE_LIGHTS); lightIndex++)
                {
                    CLUSTER_LIGHT_LOOP_SUBTRACTIVE_LIGHT_CHECK
                    Light light = GetAdditionalLight(lightIndex, i.positionWS, shadowMask);
                    colour += Shade(light, albedo, n, v, k, lightSum);
                }
            #endif
                LIGHT_LOOP_BEGIN(lightCount)
                    Light light = GetAdditionalLight(lightIndex, i.positionWS, shadowMask);
                    colour += Shade(light, albedo, n, v, k, lightSum);
                LIGHT_LOOP_END
            #endif

                half nv = saturate(dot(n, v));
                // リム: 縁に、その場の灯りと環境光の色を回す
                // 下を向いた面（顎の下・首）にはリムを回さない（顎と首の境が光って顎の線が消えるため）
                half rim = pow(1.0h - nv, _RimPower) * smoothstep(-0.35h, 0.25h, n.y);
                half3 around = lightSum + shFlat * 1.5h;
                colour += _RimColor.rgb * around * rim * (lerp(_BodyRim, _RimStrength, k.face) * k.skin + _HairRim * k.hair) * albedo * 2.0h;

                // 髪の縁の照り: 黒い髪は地の色がほぼ黒で、リムを地の色に掛けると消える。縁は地の色によらず、その場の灯りと環境光の色で照らす
                // （URP の Lit の髪が、環境の照り返しで縁を灰青に見せていたのと同じ役。映り込みで横の髪が消えないように）
                colour += _HairSheen.rgb * pow(1.0h - nv, _SheenPower) * (lightSum + shFlat * 1.5h) * k.hair;

                // 光の帯（天使の輪）。毛の流れは、頭の上から下へ、面に沿った向き
                if (k.hair > 0.001h)
                {
                    float3 up = float3(0, 1, 0);
                    float3 down = -up - n * dot(-up, n);
                    float len = length(down);
                    float3 t = len > 1e-3 ? down / len : float3(1, 0, 0);
                    t = normalize(t + n * _RingShift);
                    float3 hv = normalize(v + up * _RingLift);
                    half th = dot(t, hv);
                    half ring = smoothstep(_RingWidth, 0.0h, abs(th));
                    // 毛筋で切る（絵の明るさの細かい差）と、縁で消す
                    half strand = smoothstep(0.002h, 0.03h, Luminance(tex.rgb));
                    ring *= lerp(1.0h, strand, _RingBreak) * smoothstep(0.08h, 0.35h, nv);
                    half3 ringLight = lightSum + shFlat * _RingAmbient;
                    colour += _RingColor.rgb * ring * _RingStrength * ringLight * k.hair;
                }

                colour = MixFog(colour, i.fogCoord);
                return half4(colour, 1.0h);
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
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
            #pragma shader_feature_local_fragment _ALPHATEST_ON
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            struct ShadowIn
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct ShadowOut
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            ShadowOut ShadowVert(ShadowIn v)
            {
                ShadowOut o;
                UNITY_SETUP_INSTANCE_ID(v);
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
            #if defined(_ALPHATEST_ON)
                clip(SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv).a * _BaseColor.a - _Cutoff);
            #endif
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
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex DepthVert
            #pragma fragment DepthFrag
            #pragma shader_feature_local_fragment _ALPHATEST_ON
            #pragma multi_compile_instancing

            struct DepthIn
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct DepthOut
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            DepthOut DepthVert(DepthIn v)
            {
                DepthOut o;
                UNITY_SETUP_INSTANCE_ID(v);
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = TRANSFORM_TEX(v.uv, _BaseMap);
                return o;
            }

            half DepthFrag(DepthOut i) : SV_Target
            {
            #if defined(_ALPHATEST_ON)
                clip(SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv).a * _BaseColor.a - _Cutoff);
            #endif
                return i.positionCS.z;
            }
            ENDHLSL
        }
    }
}
