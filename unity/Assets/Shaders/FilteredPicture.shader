// タイトルの画面の背景の絵（前もって撮った 320×180）を敷く。設定の「フィルター」が減色＋ディザのときは、
// 3D の絵に Ps1 のパスが掛けるのと同じ色の組と点の模様、同じ強さで減色する（ScreenFilter.hlsl）。標準のときは絵をそのまま出す
// （絵は標準の加工で撮ってある）。点の模様は絵の画素の座標で引くので、一つの点がゲームの低解像度の 1 画素と同じ大きさになる。
// 最近傍で引き伸ばすのは絵の側（filterMode = Point）。マテリアルは Resources/ScreenFilterPicture（TitleScreen が読む）
Shader "HalfAware/FilteredPicture"
{
    Properties
    {
        [PerRendererData] _MainTex ("絵", 2D) = "black" {}
        [NoScaleOffset] _HaNoise ("点の模様（青色雑音 64×64）", 2D) = "gray" {}
        [NoScaleOffset] _HaLut ("近い二色の表", 2D) = "black" {}
        [NoScaleOffset] _HaPalette ("色の組", 2D) = "black" {}
    }
    SubShader
    {
        Tags { "Queue" = "Transparent" "IgnoreProjector" = "True" "RenderType" = "Transparent" "PreviewType" = "Plane" }
        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            // テクスチャを添え字で直に読む（ScreenFilter.hlsl）。WebGL2 の texelFetch
            #pragma target 3.5
            #include "UnityCG.cginc"
            #include "ScreenFilter.hlsl"

            struct appdata
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float4 _MainTex_TexelSize;
            // グローバル。ScreenFilter.Use が書く
            float _HaFilter;

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.color = v.color;
                return o;
            }

            float4 frag(v2f i) : SV_Target
            {
                float4 c = tex2D(_MainTex, i.uv) * i.color;
                UNITY_BRANCH
                if (_HaFilter > 0.5)
                {
                    // 点は絵の画素ごとに一つ
                    c.rgb = HaDither(c.rgb, uint2(max(floor(i.uv * _MainTex_TexelSize.zw), 0.0)));
                }
                return c;
            }
            ENDCG
        }
    }
}
