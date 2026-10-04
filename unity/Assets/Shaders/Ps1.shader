// 減色とディザ。URP の後処理の後に挿し、拡大される前の低解像度のまま打つ。
// 型は二つで、設定の「フィルター」（ScreenFilter）がグローバルの値 _HaFilter で選ぶ（0 が標準、1 が減色＋ディザ）。
// - 標準: 色ごとに _Levels 段へ刻み、4×4 の規則的な点で段のあいだを埋める
// - 減色＋ディザ: 色の組（52 色）の近い二色を、画面に貼り付いた青色雑音の点で混ぜる（ScreenFilter.hlsl）。
//   強さ（色の寄せ・点の濃さ）もグローバルの値 _HaFilterTint・_HaFilterDots で受ける（設定の「減色の強さ」「ディザの強さ」）
Shader "HalfAware/Ps1"
{
    Properties
    {
        _Levels ("色の段階数", Range(2, 64)) = 32
        _Dither ("ディザの強さ", Range(0, 1)) = 1
        _Amount ("効き具合。0 で素通し", Range(0, 1)) = 1
        [NoScaleOffset] _HaNoise ("減色＋ディザの点の模様（青色雑音 64×64）", 2D) = "gray" {}
        [NoScaleOffset] _HaLut ("減色＋ディザの近い二色の表", 2D) = "black" {}
        [NoScaleOffset] _HaPalette ("減色＋ディザの色の組", 2D) = "black" {}
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off Cull Off

        Pass
        {
            Name "Ps1"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "ScreenFilter.hlsl"

            float _Levels;
            float _Dither;
            float _Amount;
            // グローバル。マテリアルには持たせない（ScreenFilter.Use が書く）
            float _HaFilter;

            // 4x4 の規則的なディザ。0/16 から 15/16 までを散らした並び
            static const float4x4 BAYER = float4x4(
                 0.0,  8.0,  2.0, 10.0,
                12.0,  4.0, 14.0,  6.0,
                 3.0, 11.0,  1.0,  9.0,
                15.0,  7.0, 13.0,  5.0) / 16.0;

            float4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float4 src = SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_PointClamp, input.texcoord, _BlitMipLevel);
                if (_Amount <= 0.0) return src;

                UNITY_BRANCH
                if (_HaFilter > 0.5)
                {
                    // 低解像度の画素の座標で点の模様を引く。64 画素ごとに敷き詰め、画面に貼り付く
                    float3 picked = HaDither(src.rgb, uint2(input.positionCS.xy));
                    return float4(lerp(src.rgb, picked, saturate(_Amount)), src.a);
                }

                // 量子化はガンマ側で行う。Linear のまま刻むと暗部だけ段差が粗くなる
                float3 gamma = LinearToSRGB(saturate(src.rgb));
                uint2 p = uint2(input.positionCS.xy) & 3;
                // ディザ 0 のときは四捨五入（0.5）、1 のときは画素ごとの閾値
                float offset = lerp(0.5, BAYER[p.y][p.x], saturate(_Dither));
                float steps = max(2.0, _Levels) - 1.0;
                float3 stepped = floor(gamma * steps + offset) / steps;

                float3 result = SRGBToLinear(saturate(stepped));
                return float4(lerp(src.rgb, result, saturate(_Amount)), src.a);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
