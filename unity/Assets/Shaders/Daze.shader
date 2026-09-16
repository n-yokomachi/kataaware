// 眩暈。ぼかしと輪郭の揺れ。強さは DazeVolume がグローバルに入れる _DazeBlur / _DazeWobble を読む
Shader "HalfAware/Daze"
{
    Properties
    {
        _BlurRadius ("ぼかしの半径（UV）", Range(0, 0.05)) = 0.012
        _WobbleX ("横の揺れの幅", Range(0, 0.1)) = 0.03
        _WobbleY ("縦の揺れの幅", Range(0, 0.1)) = 0.02
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
            float _WobbleX;
            float _WobbleY;

            // DazeVolume が毎フレーム入れる。0 のときは素通しになる
            float _DazeBlur;
            float _DazeWobble;

            float4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;
                if (_DazeBlur <= 0.0 && _DazeWobble <= 0.0)
                    return SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_LinearClamp, uv, _BlitMipLevel);

                float t = _Time.y;
                uv.x += _DazeWobble * _WobbleX * sin(uv.y * 14.0 + t * 2.5);
                uv.y += _DazeWobble * _WobbleY * sin(uv.x * 11.0 + t * 1.9);

                float r = _DazeBlur * _BlurRadius;
                float4 sum = 0.0;
                [unroll]
                for (int i = -2; i <= 2; i++)
                {
                    [unroll]
                    for (int j = -2; j <= 2; j++)
                    {
                        float2 at = uv + float2(i, j) * r;
                        sum += SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_LinearClamp, at, _BlitMipLevel);
                    }
                }
                return sum / 25.0;
            }
            ENDHLSL
        }
    }

    Fallback Off
}
