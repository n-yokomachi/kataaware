// 何も描かない面。一人称のカメラに主人公の頭（顔・髪・首より上）を映さないために、体のレンダラーの頭の面の組へ当てる。
//
// **影も落とさない。** ShadowCaster の段を持たないので、この面は影にも入らない。頭の影は、
// 頭の面の組だけを残した別のレンダラー（影だけを落とす形）が受け持つ。
//
// 頂点は画面の外（切り取りの箱の外）へ置くので、画素は一つも塗られない。深度も書かない
Shader "HalfAware/Hidden"
{
    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Name "Hidden"
            Tags { "LightMode" = "UniversalForward" }
            ColorMask 0
            ZWrite Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; };

            Varyings Vert(Attributes input)
            {
                Varyings o;
                o.positionCS = float4(2.0, 2.0, 2.0, 1.0);
                return o;
            }

            half4 Frag(Varyings input) : SV_Target { return 0; }
            ENDHLSL
        }
    }
}
