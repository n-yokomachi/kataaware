// 前照灯の照らし。路面に落ちる跡と、雨や靄の中を通る空中の帯を、
// **一つの光の場から**描く。
//
// 前は路面に寝かせた板一枚に絵（DriveBeam.png）を貼っていた。光そのものが空中に
// 無いので、夜の路面に橙色の染みが浮いているようにしか見えず、四角くぼんやり広がる
// 縁には切れ目も芯も無かった。「前方のオレンジ色の光源は何？」と訊かれたのはそのとおりで、
// 灯りが照らしているとは読めない絵だった。
//
// ここでは灯りの側を式で持つ。灯り一つを、レンズの座から前へ伸びる円錐として置き、
// **画素の物体座標をその円錐に当てて明るさを出す。** 路面の板も空中の板も同じ式を引くので、
// 路面の跡は「円錐が舗装を切ったところ」そのものになり、空中の帯とは自動で辻褄が合う。
// 絵を持たないので、板の寸法を変えても絵を描き直す必要が無い。
//
// **実際の下向きの前照灯には上端の切れがある。** 軸をわずかに伏せて、その少し上で
// すっぱり落とすと、路面の跡は手前が明るく、ある距離ではっきり終わる。この境が
// 前照灯を前照灯に見せる。四角い板をぼかしただけでは何度強さを詰めても出てこない。
//
// **灯りは二つ。** 左右のレンズの座から別々に円錐を立てて足す。手前では二つの芯が
// 並んで見え、遠くでひとつに溶ける。実際の車の照らしもそう見える。
//
// URP の Unlit は使えない。あちらは必ず霧を色として混ぜる（MixFog）ので、加算で
// 重ねる板では霧の色が板の形のまま浮く。HalfAware/RoadGlow と同じく、
// 霧は色として混ぜず、遠いほど灯りが減る方で掛ける。距離は画素ごとに世界座標から測る
Shader "HalfAware/Headlamp"
{
    Properties
    {
        // 強さ。景色ごとに DriveBand.sky.beam が MaterialPropertyBlock で差し替える
        [HDR] _BaseColor ("強さ", Color) = (1, 1, 1, 1)
        // 灯りの色。**Color ではなく Vector で持つ。** 線形のまま渡したいので、
        // 色として宣言して SetColor の変換に晒さない
        _Warm ("灯りの色。線形", Vector) = (1, 0.955, 0.875, 1)
        _Lamp ("灯り (x, y, z, 伏せる正接)", Vector) = (0.715, 1.085, 2.52, 0.05)
        _Cut ("切れ (上端, ぼかし, 横の開き, 下の開き)", Vector) = (0.014, 0.015, 0.115, 0.105)
        _Shape ("形 (下の張り出し, 届く距離, 芯, 芯の広がり)", Vector) = (1.6, 17, 1.2, 0.04)
        _Air ("空中 (漏れ, 開き, 強さ, 向きの効き)", Vector) = (0.045, 1.7, 0.3, 0.6)
        _Gain ("ぜんたいの強さ", Float) = 1.1
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent" }

        Pass
        {
            Name "Headlamp"
            Blend One One
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            // exp(-x) を exp2 で書くための log2(e)
            #define LAMP_E 1.4426950h

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                float4 _Warm;
                float4 _Lamp;
                float4 _Cut;
                float4 _Shape;
                float4 _Air;
                float _Gain;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 positionOS : TEXCOORD2;
            };

            // 灯り一つぶんの明るさ。lx はレンズの x、air は 0 が路面・1 が空中。
            //
            // 角は正接で測る。灯りからの前後の距離 f で割った横ずれと上ずれが、
            // そのまま円錐の中での向きになるので、距離が変わっても形が保たれる。
            // これが「画面では手前も奥も同じ幅に見える」という前照灯の見え方にあたる
            float Lobe(float3 p, float lx, float air)
            {
                float3 d = p - float3(lx, _Lamp.y, _Lamp.z);
                float f = d.z;
                // 灯りより後ろは照らされない
                if (f < 0.5) return 0.0;
                float inv = rcp(f);
                float side = d.x * inv;
                // 軸からの上向き。0 が軸の上、正が上。_Lamp.w のぶん軸が伏せてある
                float up = d.y * inv + _Lamp.w;

                // 下の広がり。空中は雨や靄に散るぶんだけ広く取る
                float sy = _Cut.w * lerp(1.0, _Air.y, air);
                float low = saturate(-up / sy);
                // 下ほど横へ張り出す。実際の前照灯も足元がいちばん広い
                float open = _Cut.z * (1.0 + _Shape.x * low) * lerp(1.0, 1.35, air);

                // **上端の切れ。ここが前照灯の要。** 路面はすっぱり終わらせる
                float lid = smoothstep(_Cut.x, _Cut.x - _Cut.y, up);
                // 切れ目の上へ漏れるぶん。空中だけ。
                //
                // **漏らすのはほとんど芯だけ。** 広がりごと漏らしていたときは、
                // 切れ目の上下が同じ明るさで繋がって境が消えた。芯だけを通せば、
                // 切れ目の上に残るのは灯り二つぶんの細い筋になる。
                // 霧の中の前照灯を横から見たときに立つ筋がこれで、
                // 境を潰さずに「空中に光がある」ことだけを足せる
                float over = exp2(-max(0.0, up - _Cut.x + _Cut.y) / max(1e-4, _Air.x)) * 0.62 * air;
                float wide = max(lid, over * 0.35);
                float hot = max(lid, over);

                float below = exp2(-low * low * LAMP_E);
                float t = side / open;
                float across = exp2(-t * t * LAMP_E);
                // 芯。灯り一つぶんの明るい真ん中。**幅は角で持つ。**
                //
                // 横の開き（open）に対する割合にすると、芯の幅が距離に連れて
                // ほとんど変わらず、二つの山が最後まで溶けなかった。角で持てば
                // 芯の差し渡しは距離に比例して広がり、灯りの間隔（2 * _Lamp.x）は
                // 変わらないので、手前では二つ、遠くではひとつに見える
                float c = side / max(0.005, _Shape.w);
                float core = exp2(-c * c * LAMP_E);
                // 遠いほど弱る
                float ratio = f / max(0.5, _Shape.y);
                float far = rcp(1.0 + ratio * ratio);
                return below * far * (wide * across + _Shape.z * hot * core);
            }

            Varyings Vert(Attributes v)
            {
                Varyings o;
                o.positionOS = v.positionOS.xyz;
                o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.uv = v.uv;
                return o;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                // uv は絵の座標ではない。x が板の種（0 路面 / 1 空中）、
                // y が板ごとの重み（空中の板は間隔のぶんだけ重く数える）
                float air = i.uv.x;
                float k = Lobe(i.positionOS, -_Lamp.x, air) + Lobe(i.positionOS, _Lamp.x, air);
                k *= i.uv.y * _Gain;

                if (air > 0.5)
                {
                    k *= _Air.z;
                    // 空中の板は光の筋を横から切った面なので、真横から見ると
                    // 紙一枚の線になる。向きで落として、脇を向いたときに
                    // 空中へ明るい線が引かれないようにする
                    float3 n = normalize(TransformObjectToWorldDir(float3(0, 0, -1)));
                    float3 v = normalize(_WorldSpaceCameraPos - i.positionWS);
                    k *= pow(saturate(abs(dot(n, v))), _Air.w);
                }

                // 照らしは一様ではない。粒を薄く掛ける。車に据えた板なので
                // 粒も車と一緒に動く。流れるのは路面の方
                float g = sin(i.positionOS.x * 1.7 + i.positionOS.z * 0.93)
                        * sin(i.positionOS.z * 0.51 - i.positionOS.x * 1.13);
                k = max(0.0, k) * (0.86 + 0.28 * (g * 0.5 + 0.5));

                // 強いところほど色が抜ける。灯りの芯はどの色でも白い
                half3 hue = lerp(_Warm.rgb, float3(1, 1, 1), saturate(k * k * 0.55));
                half3 c = k * hue * _BaseColor.rgb * _BaseColor.a;

                // 霧の向こうの灯りは減るだけで、霧の色を帯びはしない。
                // unity_FogParams は x が二乗の掛かり、y が一乗の掛かり
                float dist = length(i.positionWS - _WorldSpaceCameraPos);
                #if defined(FOG_EXP2)
                    half through = exp2(-(unity_FogParams.x * dist) * (unity_FogParams.x * dist));
                #elif defined(FOG_EXP)
                    half through = exp2(-unity_FogParams.y * dist);
                #else
                    half through = 1.0h;
                #endif
                return half4(c * through, 1);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
