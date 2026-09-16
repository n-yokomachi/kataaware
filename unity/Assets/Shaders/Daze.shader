// 眩暈。ぼかしと二重像。強さは DazeVolume がグローバルに入れる _DazeBlur / _DazeWobble を読む
Shader "HalfAware/Daze"
{
    Properties
    {
        _BlurRadius ("ぼかしの半径（UV）", Range(0, 0.02)) = 0.0035
        _DoubleOffset ("二重像のずれ（UV）", Range(0, 0.06)) = 0.02
        _DoubleMix ("二重像の混ざり。0.5 で等分", Range(0, 0.5)) = 0.45
        _Sway ("画面全体の漂いの幅（UV）", Range(0, 0.03)) = 0.006
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off Cull Off

        Pass
        {
            Name "Daze"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            float _BlurRadius;
            float _DoubleOffset;
            float _DoubleMix;
            float _Sway;

            // DazeVolume が毎フレーム入れる。どちらも 0 のときは素通しになる。
            // _DazeWobble は二重像のずれと画面の漂いの強さを兼ねる
            float _DazeBlur;
            float _DazeWobble;

            float4 Tap(float2 uv)
            {
                return SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_LinearClamp, uv, _BlitMipLevel);
            }

            // 3x3 の平均。二重像が溶け合わない程度の弱いぼかしに留める。半径 0 なら同じ点を 9 回読むので素通しになる
            float4 Soften(float2 uv, float r)
            {
                float4 sum = 0.0;
                [unroll]
                for (int i = -1; i <= 1; i++)
                {
                    [unroll]
                    for (int j = -1; j <= 1; j++)
                    {
                        sum += Tap(uv + float2(i, j) * r);
                    }
                }
                return sum / 9.0;
            }

            float4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;
                if (_DazeBlur <= 0.0 && _DazeWobble <= 0.0) return Tap(uv);

                float t = _Time.y;
                // 波打たせず、絵ごとゆっくり漂わせる
                uv += float2(sin(t * 0.61), sin(t * 0.47 + 1.3)) * _Sway * _DazeWobble;

                float r = _DazeBlur * _BlurRadius;
                if (_DazeWobble <= 0.0) return Soften(uv, r);

                // 二重像。ずれの向きがゆっくり回り、焦点が合いそうで合わない
                float2 drift = float2(cos(t * 0.43), sin(t * 0.37)) * _DoubleOffset * _DazeWobble;
                float4 first = Soften(uv + drift, r);
                float4 second = Soften(uv - drift, r);
                return lerp(first, second, _DoubleMix);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
