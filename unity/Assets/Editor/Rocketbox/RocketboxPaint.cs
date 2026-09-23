using System;
using System.Collections.Generic;
using UnityEngine;

namespace HalfAware.EditorTools.Rocketbox
{
    /// <summary>
    /// Rocketbox の女大 14 の縮めたテクスチャ（512）に、組み立てのときに手を入れる。
    /// 値はすべて <see cref="Look"/> にあり、後から直せる。
    ///
    /// - 髪を黒に（頭のテクスチャの頭皮の髪と、透けの絵の髪の房。毛筋の明暗は残す）
    /// - カーディガンを黒に（ニットの目の明暗は残す）
    /// - 目は元の色のまま（虹彩は目の玉の部品で、左右の目が頭のテクスチャの同じ一枚の目の絵を使う）
    /// - 黒子（主人公は本人の左 = −x の目の下か口の下、片割れは右 = +x）
    /// - 美人への控えめな手入れ（肌のむら、目元、眉、唇、頬から顎の陰り）
    ///
    /// 場所の見分けは、テクスチャの画素ごとに模型の束ねた姿勢での位置（<see cref="Surface"/>）を求め、
    /// 目・口・鼻・眉などの場所は顔の骨（Bip01 LEye など）からの距離で決める。
    /// 顔のテクスチャは左右で別の絵（使い回していない）なので、黒子は絵に直に描ける
    /// </summary>
    public static class RocketboxPaint
    {
        [Serializable]
        public sealed class Look
        {
            [Header("髪")]
            public Color hairShadow = new Color(0.016f, 0.015f, 0.018f);
            public Color hairShine = new Color(0.215f, 0.205f, 0.225f);
            [Tooltip("元の髪の明度をこの範囲で 0〜1 に伸ばし、影から艶へ塗る")]
            public float hairLo = 0.06f, hairHi = 0.52f, hairGamma = 1.25f;
            [Tooltip("頭のテクスチャで髪と見なす明るさ。ぼかした明度が cut より暗ければ髪、fade より明るければ肌")]
            public float hairCut = 0.38f, hairFade = 0.50f;
            [Tooltip("眉を髪に合わせて暗くする量（0〜1）")]
            public float browDarken = 0.40f;
            [Tooltip("頭のテクスチャで髪と見なす一番低い所（目の高さから下へ m）。これより下の暗い所は胸元の影などとして外す。長い髪の人は大きく")]
            public float hairLowest = 0.17f;

            [Header("カーディガン（ニットの服を黒に。服を元のままにする人は false）")]
            public bool blackenKnit = true;
            public Color knitShadow = new Color(0.012f, 0.012f, 0.014f);
            public Color knitShine = new Color(0.085f, 0.087f, 0.098f);
            public float knitLo = 0.22f, knitHi = 0.68f, knitGamma = 1.10f;

            [Header("服の塗り直し（パンツ・靴・中のトップス）。元の明暗を影の色から明るい色へ写す（色は sRGB）")]
            public bool recolourPants;
            public Color pantsShadow = new Color(0.010f, 0.010f, 0.012f), pantsShine = new Color(0.070f, 0.070f, 0.078f);
            public bool recolourShoes;
            public Color shoeShadow = new Color(0.008f, 0.008f, 0.009f), shoeShine = new Color(0.100f, 0.098f, 0.100f);
            public bool recolourTop;
            public Color topShadow = new Color(0.035f, 0.035f, 0.038f), topShine = new Color(0.200f, 0.200f, 0.210f);
            [Tooltip("パンツ: この高さ（m）より下（足首より上）は全部パンツ。その上は pantsHue の色の所だけ（負なら暗い所だけ）")]
            public float pantsAllBelow = 0.84f;
            public float pantsHue = 192f;
            [Tooltip("パンツ（スカート）の上の端の高さ（m）と、明るさの上限（明るい裾のシャツを外す）")]
            public float pantsTop = 1.0f, pantsValMax = 1.01f;
            [Tooltip("中のトップス: 色相（度）とその幅、彩度の下限、明るさの上限、高さの範囲（m）、左右の幅（m、手首の輪を外す）")]
            public float topHue = 20f, topHueWidth = 20f, topSatMin = 0.40f, topValMax = 0.27f;
            public float topLow = 1.00f, topHigh = 1.55f, topHalfWidth = 0.30f;
            [Tooltip("中のトップスの塗りから外す UV の四角（別の島の小物。女大 11 のベルト）。幅 0 なら外さない")]
            public Rect topKeepUv;
            [Tooltip("体のテクスチャで胸の開きに見える肌（胸の高さで真ん中の肌）も中のトップスとして塗る（襟ぐりを浅くするとき）")]
            public bool topNeckSkin;

            [Header("丸首のシャツ（頭のテクスチャの首から下の肌を、白い丸首のシャツとして塗る）")]
            public bool shirt;
            public Color shirtColour = new Color(0.90f, 0.90f, 0.88f);
            [Tooltip("襟ぐりの高さ。頭の骨（Bip01 Head）から下へ、前の真ん中と後ろ（m）")]
            public float neckFront = 0.096f, neckBack = 0.072f;
            [Tooltip("襟ぐりの丸み。前の真ん中から横へ x（m）離れると、x² × この値だけ上がる")]
            public float neckRound = 6f;
            [Tooltip("シャツの明暗を元の肌から取らず一色にし、縦の編み目（幅 shirtRibPitch m）を shirtRib の強さで描く（まわりの服の布に揃えるとき）")]
            public bool shirtFlat;
            public float shirtRib, shirtRibPitch = 0.006f;

            [Tooltip("首から下の肌でない所（女大 14 の中のトップスとネックレス）を肌の色で塗る（胸元を見せる服の体に載せるとき）")]
            public bool chestSkin;
            [Tooltip("体の手の肌だけでなく、腕や肩の肌も頭の肌に揃える（肩や腕を出す服の体のとき）")]
            public bool matchSkinAll;

            [Header("口")]
            [Tooltip("顎の骨（Bip01 MJaw）を閉じる向きへ回す角度（度）。元の模型は口が少し開いていて、斜めから歯が見える")]
            public float jawClose;

            [Header("黒子")]
            [Tooltip("直径（m）。0 なら描かない")]
            public float moleDiameter = 0.005f;
            [Tooltip("目の玉の中心から外へ（m）と下へ（m）")]
            public float moleOut = 0.008f, moleDown = 0.0145f;
            [Tooltip("黒子を口の下に置く（目の下の代わり）。下唇の骨から左右へ moleLipOut、下へ moleLipDown（m）。口の端の少し下の外（下唇の縁から 4〜5 mm 下）")]
            public bool moleUnderLip;
            public float moleLipOut = 0.024f, moleLipDown = 0.003f;
            public Color moleColour = new Color(0.22f, 0.13f, 0.10f);

            [Header("手入れ（beauty 0 = 手を入れない、0.5 = 弱め、1 = 強め。下の値は強めのときの量）")]
            public float beauty = 0.5f;
            [Tooltip("肌の色むらを大きくぼかした色へ寄せる量")]
            public float evenChroma = 0.75f;
            [Tooltip("肌の細かいむら（1〜4 画素）を均す量")]
            public float evenLuma = 0.45f;
            [Tooltip("上瞼の際を暗くする量")]
            public float lashLine = 0.60f;
            public Color lashColour = new Color(0.07f, 0.045f, 0.045f);
            [Tooltip("眉をさらに暗く、はっきりさせる量")]
            public float browDefine = 0.25f;
            public Color lipColour = new Color(0.68f, 0.36f, 0.37f);
            public float lipTint = 0.40f;
            [Tooltip("頬の下から顎の脇を暗くして顔を細く見せる量")]
            public float contour = 0.07f;
            [Tooltip("顎の骨（Bip01 MJaw）を左右に縮める量。顎から頬の下が細くなる。テクスチャでなく形の手入れで、組み立てのときに骨へ掛ける")]
            public float jawSlim = 0.10f;

            /// <summary>顎の骨の左右の倍率</summary>
            public float JawScale { get { return 1f - Mathf.Clamp01(beauty) * jawSlim; } }

            [Header("顔を寄せる（手入れの上に重ねる。0 と 1 倍で何もしない。色は顔のテクスチャへ、形は骨へ）")]
            [Tooltip("年齢の出る影（ほうれい線・目の下のくまとたるみ・額のしわ）を持ち上げる量（0〜1）")]
            public float ageSoften;
            [Tooltip("肌のむらを整える量（手入れの均しに足す。0〜1）")]
            public float skinEven;
            [Tooltip("頬と唇の血色（0〜1）")]
            public float blush;
            public Color blushColour = new Color(0.90f, 0.72f, 0.74f);
            [Tooltip("上瞼の際を締め、目尻の先へ伸ばして切れ長に（0〜1）")]
            public float catEye;
            [Tooltip("眉を整える（縁を締めて少し濃く。0〜1）")]
            public float browTidy;
            [Tooltip("鼻筋の脇を暗く、真ん中を明るくして細く見せる（0〜1）")]
            public float noseSlim;
            [Tooltip("頬骨の下のわずかな影（0〜1）")]
            public float cheekShade;
            [Tooltip("唇の輪郭をはっきり（0〜1）")]
            public float lipLine;
            [Tooltip("目の玉の骨の倍率（1 で元のまま）")]
            public float eyeScale = 1f;
            [Tooltip("瞼の骨を目の玉の中心から外へ動かす割合（0.06 で 6 %）")]
            public float lidOpen;
            [Tooltip("鼻の骨の倍率（1 で元のまま）")]
            public float noseScale = 1f;

            [Header("鼻を目立たなくする（0 で何もしない。Nose(1) が中、Nose(2) が強）")]
            [Tooltip("鼻のまわりの陰影（鼻筋の明るさ・小鼻の横の影・鼻の穴の暗さ）を、まわり 1 cm の肌の明るさへ寄せる割合（顔のテクスチャ）")]
            public float noseShade;
            [Tooltip("鼻の高さ（頬の内側の面からの出っ張り）を縮める割合（頭の面の頂点を動かす）")]
            public float noseFlatten;
            [Tooltip("小鼻の幅を縮める割合（頭の面の頂点を動かす）")]
            public float noseNarrow;
            [Tooltip("鼻のまわりの小さな暗い所（鼻の穴・鼻先の下の影・小鼻の脇の溝）を、まわり 5 mm の平均の色へ寄せる割合。320×180 で暗い画素が一つ拾われて鼻の穴が点に見えるのを抑える")]
            public float noseDark;
            [Tooltip("鼻のまわりだけ、テクスチャを縮める前にぼかす半径（画素）。0 でぼかさない")]
            public float noseBlur;

            /// <summary>鼻を目立たなくする強さの段（0 で何もしない、1 が中、2 が強）。陰影・高さ・小鼻の幅の三つをまとめて入れる</summary>
            public void Nose(int level)
            {
                noseShade = level >= 2 ? 0.75f : level == 1 ? 0.45f : 0f;
                noseFlatten = level >= 2 ? 0.45f : level == 1 ? 0.25f : 0f;
                noseNarrow = level >= 2 ? 0.15f : level == 1 ? 0.08f : 0f;
            }
            [Tooltip("顎を縦に縮める割合（顎から下を短く。0.06 で 6 %）")]
            public float chinShort;

            [Header("艶（頭のマテリアルの Specular の絵。RGB = 照り返しの強さ、A = 滑らかさ）")]
            [Tooltip("肌の照り返しの強さ（線形の値）。髪はここを 0 にする")]
            public float skinSpecular = 0.04f;
            public float skinSmoothness = 0.22f;
            [Tooltip("髪の滑らかさ。髪は照り返しを 0 にするので、見た目はほとんど変わらない")]
            public float hairSmoothness = 0.10f;
            [Tooltip("目の玉の滑らかさ（主光を目に映すため）")]
            public float eyeSmoothness = 0.55f;

            [Header("体の肌の色を頭に揃える（頭と体が別の人のとき）")]
            public bool matchSkin;

            [Header("別の人の髪を載せるとき（顔の人の頭のテクスチャ）")]
            [Tooltip("頭皮を兼ねた髪の殻を、毛筋の明暗の無い影の色一色で塗る（別の人の髪の下地にし、殻の外に見えても目立たせない）")]
            public bool flatHair;
            [Header("ワンピース（上の服と借りたスカートを同じ布の色に。布の明暗は元のまま）")]
            public bool dress;
            public Color dressShadow = new Color(0.56f, 0.53f, 0.48f), dressShine = new Color(0.95f, 0.93f, 0.88f);
            [Header("ネックレス（首の付け根の両脇から胸の真ん中へ垂れる鎖と、丸い飾り）")]
            public bool necklace;
            public Color necklaceColour = new Color(0.74f, 0.74f, 0.76f);
            [Tooltip("鎖の幅（m）と、飾りの半径（m）")]
            public float necklaceWidth = 0.0016f, pendantRadius = 0.0042f;
            [Tooltip("目の高さから、首の付け根の両脇の鎖までと胸の真ん中の鎖までの下がり（m）。スポーツ 02 のタンクトップの襟ぐりの真ん中は目の 22 cm 下")]
            public float necklaceBack = 0.14f, necklaceFront = 0.195f;
            [Tooltip("首の付け根の両脇の、真ん中からの左右の位置（m）と、前の鎖の下がり方（1 で V、2 で U）")]
            public float necklaceSide = 0.058f, necklaceCurve = 1.25f;
            [Tooltip("髪を塗らずに元の色のまま。一色で塗る所（顔の人の頭皮・殻の耳まわりとこめかみ）は naturalHairInk")]
            public bool naturalHair;
            public Color naturalHairInk = new Color(0.12f, 0.08f, 0.06f);
            /// <summary>
            /// 顔の人の頭の絵の画素ごとの、髪の人の殻の不透明さ（<see cref="RocketboxHairSwap.HairCover"/>）。null なら使わない。
            /// 顔の人が絵に描いた前髪のうち、殻が透ける所（髪の人では額の肌の所）を肌で埋めるのに使う
            /// </summary>
            [System.NonSerialized] public float[] hairCover;
            [Tooltip("眉より上の額とこめかみで、元の前髪の影が焼き込まれて暗い肌を、額の真ん中の明るさまで上げる量（0〜1）")]
            public float liftForehead;
            [Tooltip("揃える強さ（0〜1）")]
            public float skinMatch = 1f;

            public Look Clone() { return (Look)MemberwiseClone(); }
        }

        // ---- 模型の上の位置 ---------------------------------------------------

        /// <summary>テクスチャの画素ごとの、模型の根の中の位置（束ねた姿勢）</summary>
        public sealed class Surface
        {
            public int N;
            public Vector3[] P;
            public bool[] On;

            /// <summary>三角を UV に並べて、画素の中心の位置を重心の座標で埋める。縁は 2 画素だけ外へ延ばす</summary>
            public static Surface Of(Vector3[] verts, Vector2[] uv, int[] tris, int n)
            {
                var s = new Surface { N = n, P = new Vector3[n * n], On = new bool[n * n] };
                for (var t = 0; t < tris.Length; t += 3)
                {
                    int ia = tris[t], ib = tris[t + 1], ic = tris[t + 2];
                    Vector2 a = uv[ia] * n, b = uv[ib] * n, c = uv[ic] * n;
                    var x0 = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(a.x, Mathf.Min(b.x, c.x))));
                    var x1 = Mathf.Min(n - 1, Mathf.CeilToInt(Mathf.Max(a.x, Mathf.Max(b.x, c.x))));
                    var y0 = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(a.y, Mathf.Min(b.y, c.y))));
                    var y1 = Mathf.Min(n - 1, Mathf.CeilToInt(Mathf.Max(a.y, Mathf.Max(b.y, c.y))));
                    var den = (b.y - c.y) * (a.x - c.x) + (c.x - b.x) * (a.y - c.y);
                    if (Mathf.Abs(den) < 1e-9f) continue;
                    for (var y = y0; y <= y1; y++)
                        for (var x = x0; x <= x1; x++)
                        {
                            float px = x + 0.5f, py = y + 0.5f;
                            var l1 = ((b.y - c.y) * (px - c.x) + (c.x - b.x) * (py - c.y)) / den;
                            var l2 = ((c.y - a.y) * (px - c.x) + (a.x - c.x) * (py - c.y)) / den;
                            var l3 = 1f - l1 - l2;
                            if (l1 < -0.001f || l2 < -0.001f || l3 < -0.001f) continue;
                            var k = y * n + x;
                            s.P[k] = verts[ia] * l1 + verts[ib] * l2 + verts[ic] * l3;
                            s.On[k] = true;
                        }
                }
                for (var pass = 0; pass < 2; pass++) s.Grow();
                return s;
            }

            void Grow()
            {
                var on = (bool[])On.Clone();
                for (var y = 0; y < N; y++)
                    for (var x = 0; x < N; x++)
                    {
                        var k = y * N + x;
                        if (On[k]) continue;
                        var sum = Vector3.zero;
                        var cnt = 0;
                        for (var dy = -1; dy <= 1; dy++)
                            for (var dx = -1; dx <= 1; dx++)
                            {
                                int xx = x + dx, yy = y + dy;
                                if (xx < 0 || yy < 0 || xx >= N || yy >= N) continue;
                                var j = yy * N + xx;
                                if (!On[j]) continue;
                                sum += P[j];
                                cnt++;
                            }
                        if (cnt == 0) continue;
                        P[k] = sum / cnt;
                        on[k] = true;
                    }
                On = on;
            }
        }

        /// <summary>顔の骨の位置（模型の根の中、束ねた姿勢）</summary>
        public sealed class Anchors
        {
            public Vector3 eyeL, eyeR, blinkTopL, blinkTopR, browInL, browInR, browOutL, browOutR;
            public Vector3 upperLip, lowerLip, mouthL, mouthR, nose, cheekL, cheekR, head;

            public static Anchors Of(Transform root)
            {
                var d = new Dictionary<string, Transform>();
                foreach (var t in root.GetComponentsInChildren<Transform>(true)) d[t.name] = t;
                Func<string, Vector3> at = n =>
                {
                    Transform t;
                    if (!d.TryGetValue(n, out t)) throw new InvalidOperationException("顔の骨が無い: " + n);
                    return root.InverseTransformPoint(t.position);
                };
                return new Anchors
                {
                    eyeL = at("Bip01 LEye"), eyeR = at("Bip01 REye"),
                    blinkTopL = at("Bip01 LEyeBlinkTop"), blinkTopR = at("Bip01 REyeBlinkTop"),
                    browInL = at("Bip01 LInnerEyebrow"), browInR = at("Bip01 RInnerEyebrow"),
                    browOutL = at("Bip01 LOuterEyebrow"), browOutR = at("Bip01 ROuterEyebrow"),
                    upperLip = at("Bip01 MUpperLip"), lowerLip = at("Bip01 MBottomLip"),
                    mouthL = at("Bip01 LMouthCorner"), mouthR = at("Bip01 RMouthCorner"),
                    nose = at("Bip01 MNose"), cheekL = at("Bip01 LCheek"), cheekR = at("Bip01 RCheek"),
                    head = at("Bip01 Head"),
                };
            }
        }

        // ---- 頭 ---------------------------------------------------------------

        /// <summary>頭に手を入れた結果と、黒子を置いた所</summary>
        public sealed class HeadResult
        {
            public Color[] Px;
            /// <summary>黒子の中心（模型の根の中）と UV。黒子が無ければ UV は負</summary>
            public Vector3 Mole;
            public Vector2 MoleUv = new Vector2(-1f, -1f);
            /// <summary>印の絵（R = 黒子、G = 虹彩、B = 他の顔の部品、地は灰）。<see cref="HalfAware.EditorTools.Study.FaceStudy"/> で測るのに使う</summary>
            public Color32[] Mask;
            /// <summary>頭のマテリアルの Specular の絵（sRGB の RGB = 照り返しの強さ、A = 滑らかさ）</summary>
            public Color32[] Spec;
            /// <summary>頭のテクスチャで髪と見なした重み（0〜1、N×N）。撮り比べで髪の長さを測るのに使う</summary>
            public float[] Hair;
            public int N;
            public string Note = "";
        }

        /// <summary>
        /// 頭のテクスチャ（顔・頭皮の髪・首と胸・口の中・目の玉）に手を入れる。
        /// twin なら黒子を本人の右（+x）の目の下へ
        /// </summary>
        /// <param name="irisUv">目の玉の絵の虹彩の中心（UV）。塗らない。印の絵で虹彩を測るのにだけ使う</param>
        public static HeadResult Head(Color[] src, Surface s, Anchors a, Look k, bool twin, Vector2 irisUv, float irisRadius)
        {
            var n = s.N;
            var px = (Color[])src.Clone();
            var H = new float[n * n];
            var S = new float[n * n];
            var V = new float[n * n];
            for (var i = 0; i < px.Length; i++) Color.RGBToHSV(src[i], out H[i], out S[i], out V[i]);
            var scale = n / 512f;
            var Vb = Blur(V, s.On, n, Mathf.Max(1, Mathf.RoundToInt(1 * scale)));
            var Vl = Blur(V, s.On, n, Mathf.Max(2, Mathf.RoundToInt(6 * scale)));
            var eyeY = (a.eyeL.y + a.eyeR.y) * 0.5f;

            // 場所の重み
            var hair = new float[n * n];
            var skin = new float[n * n];
            var brow = new float[n * n];
            for (var i = 0; i < px.Length; i++)
            {
                // UV の島の外（詰め物）は、暗ければ髪と同じに塗る（ミップマップで島の縁へ滲むため）
                if (!s.On[i]) { hair[i] = Smooth(k.hairFade, k.hairCut, Vb[i]); continue; }
                var p = s.P[i];
                var guarded = InEye(p, a.eyeL) || InEye(p, a.eyeR) || InMouth(p, a) || InNose(p, a);
                // 髪は顎の高さより上だけ（胸元の影を髪と取り違えない）
                var above = Smooth(eyeY - k.hairLowest - 0.02f, eyeY - k.hairLowest + 0.02f, p.y);
                hair[i] = guarded ? 0f : Smooth(k.hairFade, k.hairCut, Vb[i]) * above;
                // 頭の天辺（額の生え際より上）は明るい毛筋も髪
                hair[i] = Mathf.Max(hair[i], Smooth(eyeY + 0.080f, eyeY + 0.095f, p.y));
                var bw = BrowZone(p, a);
                if (bw > 0f)
                {
                    var dark = (Vl[i] - V[i]) / Mathf.Max(0.05f, Vl[i]);
                    brow[i] = bw * Smooth(0.02f, 0.10f, dark) * (1f - hair[i]);
                }
                var faceAndNeck = Smooth(eyeY - 0.24f, eyeY - 0.20f, p.y);
                var skinCol = Smooth(0.40f, 0.52f, Vb[i]) * Band(S[i], 0.18f, 0.24f, 0.62f, 0.70f) * HueNear(H[i] * 360f, 20f, 16f, 10f);
                skin[i] = guarded || InEyeWide(p, a.eyeL) || InEyeWide(p, a.eyeR) ? 0f : (1f - hair[i]) * faceAndNeck * skinCol * (1f - Mathf.Clamp01(bw * 2f));
            }
            // 別の人の髪を載せるとき（一色で塗るとき）は、髪の中の明るい毛筋（幅 6 画素まで）も髪にする。
            // 明るい毛筋は暗さで髪と見なされず、別の人の髪の分け目から明るい茶の筋になって覗いた
            if (k.flatHair)
            {
                hair = MinMax(MinMax(hair, n, 3, true), n, 3, false);
                // 太い毛筋も、まわり（半径 6 画素）の多くが髪なら髪にする
                var wide = Blur(hair, s.On, n, Mathf.Max(2, Mathf.RoundToInt(6 * scale)));
                for (var i = 0; i < hair.Length; i++) hair[i] = Mathf.Max(hair[i], Smooth(0.50f, 0.65f, wide[i]));
            }
            hair = Blur(hair, s.On, n, 1);

            // 顔の人が絵に描いた前髪のうち、髪の人の殻が透ける所（髪の人では額の肌の所）は、髪にせず額の肌で埋める
            // （女大 14 の前髪は額の右上に描かれていて、女大 08 の殻が透ける所で黒い塊に見えた）
            var fringe = new List<int>();
            var fringeTop = Mathf.Max(Mathf.Max(a.browInL.y, a.browOutL.y), Mathf.Max(a.browInR.y, a.browOutR.y));
            if (k.hairCover != null && k.hairCover.Length == hair.Length)
            {
                for (var i = 0; i < px.Length; i++)
                {
                    if (!s.On[i] || hair[i] < 0.05f || k.hairCover[i] >= 0.5f || brow[i] > 0f) continue;
                    var p = s.P[i];
                    // 眉（眉の上端より 1.2 cm 上まで）は触らない
                    if (p.y < fringeTop + 0.012f || p.z < a.head.z + 0.01f) continue;
                    hair[i] = 0f;
                    fringe.Add(i);
                }
            }

            var b = Mathf.Clamp01(k.beauty);

            // 1. 肌のむら（手入れ）。明るさの形は残し、色のむらと細かい斑だけを均す
            if (b > 0f || k.skinEven > 0f)
            {
                var Y = new float[n * n];
                var Cb = new float[n * n];
                var Cr = new float[n * n];
                for (var i = 0; i < px.Length; i++) ToYCC(px[i], out Y[i], out Cb[i], out Cr[i]);
                var rBig = Mathf.Max(2, Mathf.RoundToInt(10 * scale));
                var Cbb = Blur(Cb, s.On, n, rBig);
                var Crb = Blur(Cr, s.On, n, rBig);
                var Y1 = Blur(Y, s.On, n, 1);
                var Y4 = Blur(Y, s.On, n, Mathf.Max(2, Mathf.RoundToInt(4 * scale)));
                for (var i = 0; i < px.Length; i++)
                {
                    if (skin[i] <= 0f) continue;
                    var wc = Mathf.Clamp01(b * k.evenChroma + k.skinEven * 0.5f) * skin[i];
                    var wl = Mathf.Clamp01(b * k.evenLuma + k.skinEven * 0.4f) * skin[i];
                    var y = Mathf.Lerp(Y[i], Y4[i] + (Y[i] - Y1[i]), wl);
                    var cb = Mathf.Lerp(Cb[i], Cbb[i], wc);
                    var cr = Mathf.Lerp(Cr[i], Crb[i], wc);
                    px[i] = FromYCC(y, cb, cr, px[i].a);
                }
            }

            // 1b. 鼻のまわりの陰影を、まわり 1 cm の肌の明るさへ寄せる（色は残し、明るさだけ）
            if (k.noseShade > 0f)
            {
                var L = new float[n * n];
                for (var i = 0; i < px.Length; i++) L[i] = Lum(px[i]);
                var Lb = Blur(L, s.On, n, Mathf.Max(3, Mathf.RoundToInt(12 * scale)));
                for (var i = 0; i < px.Length; i++)
                {
                    if (!s.On[i] || hair[i] > 0.1f) continue;
                    var p = s.P[i];
                    if (InEyeWide(p, a.eyeL) || InEyeWide(p, a.eyeR)) continue;
                    var w = NoseZone(p, a);
                    if (w <= 0f) continue;
                    // 鼻の穴の暗い画素を大きく持ち上げると橙に浮くので、明るくするのは 1.35 倍まで
                    var g = Mathf.Clamp(Mathf.Lerp(1f, Lb[i] / Mathf.Max(0.02f, L[i]), Mathf.Clamp01(k.noseShade) * w), 0.6f, 1.35f);
                    var c = px[i];
                    px[i] = new Color(Mathf.Clamp01(c.r * g), Mathf.Clamp01(c.g * g), Mathf.Clamp01(c.b * g), c.a);
                }
            }

            // 1c. 鼻のまわりの小さな暗い所を、まわり 5 mm の平均の色へ寄せる（明るさだけでなく色も。暗い画素を持ち上げるだけだと橙に浮いた）。
            //     そのあと、鼻のまわりだけ少しぼかす（縮めたときに一つの暗い画素が拾われないように）
            if (k.noseDark > 0f || k.noseBlur > 0f)
            {
                var R = new float[n * n];
                var G = new float[n * n];
                var Bl = new float[n * n];
                for (var i = 0; i < px.Length; i++) { R[i] = px[i].r; G[i] = px[i].g; Bl[i] = px[i].b; }
                var r6 = Mathf.Max(2, Mathf.RoundToInt(6 * scale));
                var Rb = Blur(R, s.On, n, r6);
                var Gb = Blur(G, s.On, n, r6);
                var Bb = Blur(Bl, s.On, n, r6);
                var zone = new float[n * n];
                for (var i = 0; i < px.Length; i++)
                {
                    if (!s.On[i] || hair[i] > 0.1f) continue;
                    var p = s.P[i];
                    if (InEyeWide(p, a.eyeL) || InEyeWide(p, a.eyeR) || InMouth(p, a)) continue;
                    zone[i] = NoseZone(p, a);
                }
                if (k.noseDark > 0f)
                    for (var i = 0; i < px.Length; i++)
                    {
                        if (zone[i] <= 0f) continue;
                        var c = px[i];
                        var mean = new Color(Rb[i], Gb[i], Bb[i], c.a);
                        var l = Lum(c);
                        var lm = Mathf.Max(0.02f, Lum(mean));
                        var t = Mathf.Clamp01(k.noseDark) * zone[i] * Smooth(0f, 0.25f, (lm - l) / lm);
                        if (t > 0f) px[i] = Color.Lerp(c, mean, t);
                    }
                if (k.noseBlur > 0f)
                {
                    var rb = Mathf.Max(1, Mathf.RoundToInt(k.noseBlur * scale));
                    for (var i = 0; i < px.Length; i++) { R[i] = px[i].r; G[i] = px[i].g; Bl[i] = px[i].b; }
                    Rb = Blur(R, s.On, n, rb);
                    Gb = Blur(G, s.On, n, rb);
                    Bb = Blur(Bl, s.On, n, rb);
                    for (var i = 0; i < px.Length; i++)
                    {
                        if (zone[i] <= 0f) continue;
                        var c = px[i];
                        px[i] = Color.Lerp(c, new Color(Rb[i], Gb[i], Bb[i], c.a), zone[i]);
                    }
                }
            }

            // 2. 髪を黒に（別の人の髪を載せるときは、毛筋の無い影の色一色）
            for (var i = 0; i < px.Length; i++)
            {
                if (hair[i] <= 0f) continue;
                // 元の色の髪は塗らない（一色で塗る頭皮だけ、元の髪の暗い色で塗る）
                if (k.naturalHair && !k.flatHair) continue;
                var ink = k.flatHair ? (k.naturalHair ? k.naturalHairInk : k.hairShadow) : HairRamp(V[i], k);
                ink.a = px[i].a;
                // 一色で塗るときは、髪と見なす度合いが半ばの所（元の明るい毛筋）も塗り切る。
                // 別の人の髪の殻は分け目で透けて、顔の人の頭皮がそこから覗き、毛筋が明るい茶の筋や、生え際の先の茶色の点に見えたため
                px[i] = Color.Lerp(px[i], ink, k.flatHair ? Smooth(0.05f, 0.22f, hair[i]) : hair[i]);
            }

            // 2a. 肌にした前髪の所を、まわりの肌からならして埋める（縁のとなりの肌の平均の色から始めて、となりの平均を 800 回。髪の画素は混ぜない）
            if (fringe.Count > 0)
            {
                var inFringe = new HashSet<int>(fringe);
                var refs = new List<Color>();
                foreach (var i in fringe)
                {
                    int x = i % n, y = i / n;
                    foreach (var j in new[] { x > 0 ? i - 1 : -1, x < n - 1 ? i + 1 : -1, y > 0 ? i - n : -1, y < n - 1 ? i + n : -1 })
                        if (j >= 0 && s.On[j] && !inFringe.Contains(j) && hair[j] < 0.1f) refs.Add(px[j]);
                }
                if (refs.Count > 0)
                {
                    var start = new Color(0f, 0f, 0f, 0f);
                    foreach (var c0 in refs) start += c0;
                    start /= refs.Count;
                    foreach (var i in fringe)
                    {
                        var c = start;
                        c.a = px[i].a;
                        px[i] = c;
                    }
                    var inFill = new HashSet<int>(fringe);
                    for (var it = 0; it < 800; it++)
                    {
                        var next = new Dictionary<int, Color>();
                        foreach (var i in fringe)
                        {
                            int x = i % n, y = i / n;
                            var acc = new Color(0f, 0f, 0f, 0f);
                            var cnt = 0;
                            foreach (var j in new[] { x > 0 ? i - 1 : -1, x < n - 1 ? i + 1 : -1, y > 0 ? i - n : -1, y < n - 1 ? i + n : -1 })
                            {
                                if (j < 0 || !s.On[j] || (!inFill.Contains(j) && hair[j] > 0.3f)) continue;
                                acc += px[j];
                                cnt++;
                            }
                            if (cnt > 0) next[i] = acc / cnt;
                        }
                        foreach (var kv in next)
                        {
                            var c = kv.Value;
                            c.a = px[kv.Key].a;
                            px[kv.Key] = c;
                        }
                    }
                }
            }

            // 2b. 元の前髪の影で暗い額とこめかみの肌を、額の真ん中の明るさまで上げる（別の人の髪を載せるとき）
            if (k.liftForehead > 0f)
            {
                var browTop = Mathf.Max(Mathf.Max(a.browInL.y, a.browOutL.y), Mathf.Max(a.browInR.y, a.browOutR.y));
                var refs = new List<float>();
                for (var i = 0; i < px.Length; i++)
                {
                    if (!s.On[i] || hair[i] > 0.1f) continue;
                    var p = s.P[i];
                    if (Mathf.Abs(p.x) < 0.02f && p.y > browTop + 0.008f && p.y < browTop + 0.035f && p.z > a.eyeL.z) refs.Add(Lum(px[i]));
                }
                if (refs.Count > 0)
                {
                    refs.Sort();
                    var lref = refs[refs.Count / 2];
                    for (var i = 0; i < px.Length; i++)
                    {
                        if (!s.On[i] || hair[i] >= 0.5f) continue;
                        var p = s.P[i];
                        var zone = Smooth(browTop - 0.006f, browTop + 0.004f, p.y) * Smooth(a.head.z - 0.01f, a.head.z + 0.02f, p.z) * (1f - hair[i] * 2f);
                        if (zone <= 0f) continue;
                        var l = Lum(px[i]);
                        if (l >= lref * 0.97f || l < 0.05f) continue;
                        var gain = Mathf.Min(1.8f, lref / l);
                        var g = Mathf.Lerp(1f, gain, zone * Mathf.Clamp01(k.liftForehead));
                        var c = px[i];
                        px[i] = new Color(Mathf.Clamp01(c.r * g), Mathf.Clamp01(c.g * g), Mathf.Clamp01(c.b * g), c.a);
                    }
                }
            }

            // 3. 眉。髪に合わせて暗く（常に）、手入れでさらにはっきり
            var browAmount = Mathf.Clamp01(k.browDarken + b * k.browDefine);
            for (var i = 0; i < px.Length; i++)
            {
                if (brow[i] <= 0f) continue;
                // 眉を整える（browTidy）: 縁の薄い所を落とし、芯を少し濃く
                var w = Mathf.Lerp(brow[i], Smooth(0.25f, 0.55f, brow[i]), Mathf.Clamp01(k.browTidy)) * Mathf.Clamp01(browAmount + 0.2f * k.browTidy);
                var c = px[i];
                var grey = c.r * 0.30f + c.g * 0.59f + c.b * 0.11f;
                var dark = Color.Lerp(c, new Color(grey, grey, grey), 0.35f) * 0.38f;
                px[i] = Color.Lerp(c, new Color(dark.r, dark.g, dark.b, c.a), w);
            }

            // 4. 目元（手入れ）。瞼の開きの縁（元の絵で暗い赤の所）の上側を暗くする
            if (b > 0f && k.lashLine > 0f)
            {
                foreach (var eye in new[] { a.eyeL, a.eyeR })
                {
                    var open = new float[n * n];
                    for (var i = 0; i < px.Length; i++)
                    {
                        if (!s.On[i] || !InEye(s.P[i], eye)) continue;
                        var h = H[i] * 360f;
                        var red = (h < 16f || h > 340f) && S[i] > 0.33f && V[i] < 0.66f;
                        open[i] = red ? 1f : 0f;
                    }
                    var near = Blur(open, s.On, n, Mathf.Max(1, Mathf.RoundToInt(2 * scale)));
                    for (var i = 0; i < px.Length; i++)
                    {
                        if (near[i] <= 0f || open[i] > 0.5f) continue;
                        var p = s.P[i];
                        if (!InEye(p, eye)) continue;
                        var up = Smooth(eye.y - 0.0015f, eye.y + 0.0020f, p.y);
                        var lower = 0.30f * (1f - up);
                        var w = Mathf.Clamp01(Mathf.Clamp01(near[i] * 2.2f) * (up + lower) * b * k.lashLine * (1f + k.catEye));
                        px[i] = Color.Lerp(px[i], new Color(k.lashColour.r, k.lashColour.g, k.lashColour.b, px[i].a), w);
                    }
                }
            }

            // 5. 唇（手入れ）。口の周りで元の赤みが肌より強い所へ色を差す
            if (b > 0f && k.lipTint > 0f)
            {
                var c0 = new Vector3(0f, (a.upperLip.y + a.lowerLip.y) * 0.5f, 0f);
                var half = Mathf.Abs(a.mouthL.x - a.mouthR.x) * 0.5f + 0.003f;
                var lipLum = Lum(k.lipColour);
                for (var i = 0; i < px.Length; i++)
                {
                    if (!s.On[i]) continue;
                    var p = s.P[i];
                    if (p.z < a.mouthL.z - 0.012f) continue;
                    var e = Mathf.Sqrt(Sq(p.x / half) + Sq((p.y - c0.y) / 0.0115f));
                    var zone = Smooth(1.0f, 0.70f, e);
                    if (zone <= 0f) continue;
                    var c = src[i];
                    var red = (c.r - c.g) / Mathf.Max(0.05f, c.r);
                    var lip = zone * Smooth(0.26f, 0.31f, red);
                    if (lip <= 0f) continue;
                    var l = Lum(px[i]);
                    var tint = k.lipColour * (l / Mathf.Max(0.05f, lipLum));
                    tint.a = px[i].a;
                    px[i] = Color.Lerp(px[i], tint, lip * b * k.lipTint);
                }
            }

            // 6. 頬の下から顎の脇の陰り（手入れ）
            if (b > 0f && k.contour > 0f)
            {
                for (var i = 0; i < px.Length; i++)
                {
                    if (!s.On[i] || hair[i] > 0.5f) continue;
                    var p = s.P[i];
                    var ax = Mathf.Abs(p.x);
                    var side = Smooth(0.038f, 0.062f, ax) * Smooth(0.090f, 0.068f, ax);
                    var height = Smooth(a.lowerLip.y - 0.030f, a.lowerLip.y - 0.005f, p.y) * Smooth(a.cheekL.y + 0.002f, a.cheekL.y - 0.012f, p.y);
                    var front = Smooth(a.head.z - 0.01f, a.head.z + 0.02f, p.z);
                    var w = side * height * front * b * k.contour;
                    if (w <= 0f) continue;
                    var c = px[i];
                    px[i] = new Color(c.r * (1f - w), c.g * (1f - w * 1.05f), c.b * (1f - w * 1.05f), c.a);
                }
            }

            // 6c. 顔を寄せる（手入れの上に重ねる。どれも 0 で何もしない）
            FaceTouch(px, s, a, k, hair, brow, src, n);

            // 6b. 丸首のシャツ
            if (k.shirt) Shirt(px, s, a, k);
            if (k.chestSkin) ChestSkin(px, s, a, hair);
            // 6c. ネックレス（元の鎖を消した後に描き直す）
            Necklace(px, s, a, k);

            // 7. 虹彩は塗らない。印の絵のために、瞳の外から虹彩の縁までの輪だけを覚える
            var irisMask = new bool[n * n];
            {
                var cx = irisUv.x * n;
                var cy = irisUv.y * n;
                var R = irisRadius * n;
                var reach = Mathf.CeilToInt(R + 2);
                for (var y = Mathf.Max(0, (int)cy - reach); y <= Mathf.Min(n - 1, (int)cy + reach); y++)
                    for (var x = Mathf.Max(0, (int)cx - reach); x <= Mathf.Min(n - 1, (int)cx + reach); x++)
                    {
                        var r = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(cx, cy));
                        if (r < R && r > R * 0.33f) irisMask[y * n + x] = true;
                    }
            }

            // 8. 黒子
            var res = new HeadResult();
            var moleMask = new bool[n * n];
            if (k.moleDiameter > 0f)
            {
                var eye = twin ? a.eyeR : a.eyeL;
                var outward = Mathf.Sign(eye.x);
                var target = k.moleUnderLip
                    ? new Vector2(outward * k.moleLipOut, a.lowerLip.y - k.moleLipDown)
                    : new Vector2(eye.x + outward * k.moleOut, eye.y - k.moleDown);
                var frontZ = k.moleUnderLip ? a.lowerLip.z - 0.03f : eye.z - 0.01f;
                // 狙った所（目の下か口の下）の肌の上で、一番近く、一番手前の画素
                var best = -1;
                var bestScore = float.MaxValue;
                for (var i = 0; i < px.Length; i++)
                {
                    if (!s.On[i]) continue;
                    var p = s.P[i];
                    if (p.z < frontZ) continue;
                    var d = Vector2.Distance(new Vector2(p.x, p.y), target);
                    if (d > 0.003f) continue;
                    var score = d - p.z * 0.5f;
                    if (score < bestScore) { bestScore = score; best = i; }
                }
                if (best < 0) throw new InvalidOperationException("黒子を置く肌が見つからない");
                var centre = s.P[best];
                var rad = k.moleDiameter * 0.5f;
                for (var i = 0; i < px.Length; i++)
                {
                    if (!s.On[i]) continue;
                    var d = Vector3.Distance(s.P[i], centre) / rad;
                    if (d >= 1.15f) continue;
                    var w = Mathf.Clamp01((1.15f - d) / 0.3f);
                    var c = Color.Lerp(k.moleColour * 0.85f, k.moleColour * 1.10f, Mathf.Clamp01(d));
                    c.a = px[i].a;
                    px[i] = Color.Lerp(px[i], c, w);
                    if (d < 1f) moleMask[i] = true;
                }
                res.Mole = centre;
                res.MoleUv = new Vector2((best % n + 0.5f) / n, (best / n + 0.5f) / n);
            }

            // 印の絵: R = 黒子、G = 虹彩、B = 眉・唇・目の開き・口の中（他の部品）、地は灰
            var mask = new Color32[n * n];
            for (var i = 0; i < mask.Length; i++)
            {
                if (moleMask[i]) mask[i] = new Color32(255, 38, 38, 255);
                else if (irisMask[i]) mask[i] = new Color32(38, 255, 38, 255);
                else if (s.On[i] && (brow[i] > 0.3f || InEye(s.P[i], a.eyeL) || InEye(s.P[i], a.eyeR) || InMouth(s.P[i], a))) mask[i] = new Color32(38, 38, 255, 255);
                else mask[i] = new Color32(38, 38, 38, 255);
            }
            res.Mask = mask;
            res.Hair = hair;
            res.Spec = SpecMap(hair, s, irisUv, irisRadius, k);
            res.N = n;
            res.Px = px;
            res.Note = string.Format("手入れ {0:0.00}、黒子 {1:0.0} mm（{2}）", b, k.moleDiameter * 1000f, k.moleUnderLip ? (twin ? "口の右下" : "口の左下") : (twin ? "右目の下" : "左目の下"));
            return res;
        }

        static bool InEye(Vector3 p, Vector3 eye)
        {
            return Mathf.Abs(p.x - eye.x) < 0.021f && Mathf.Abs(p.y - eye.y) < 0.0125f && p.z > eye.z - 0.012f;
        }

        static bool InEyeWide(Vector3 p, Vector3 eye)
        {
            return Mathf.Abs(p.x - eye.x) < 0.024f && Mathf.Abs(p.y - eye.y) < 0.015f && p.z > eye.z - 0.015f;
        }

        static bool InMouth(Vector3 p, Anchors a)
        {
            var half = Mathf.Abs(a.mouthL.x - a.mouthR.x) * 0.5f + 0.008f;
            return Mathf.Abs(p.x) < half && p.y > a.lowerLip.y - 0.012f && p.y < a.upperLip.y + 0.008f && p.z > a.mouthL.z - 0.040f;
        }

        /// <summary>
        /// ネックレスを描く（<see cref="Look.necklace"/>）。鎖は首の付け根の両脇（真ん中から necklaceSide、目の necklaceBack 下）から、
        /// 重さで胸の真ん中（目の necklaceFront 下）へ下がる V に近い U の形（左右の位置の necklaceCurve 乗で下がる）。後ろは首の付け根に沿って同じ高さで回る。
        /// 飾りは鎖の一番低い所に吊るした丸（半径 pendantRadius、縁を少し暗く）。
        /// 頭の面と胸元の面（別の絵）のどちらにも同じ位置で描くので、首の付け根で面が替わっても途切れない
        /// </summary>
        public static void Necklace(Color[] px, Surface s, Anchors a, Look k)
        {
            if (!k.necklace) return;
            var eyeY = (a.eyeL.y + a.eyeR.y) * 0.5f;
            var eyeZ = (a.eyeL.z + a.eyeR.z) * 0.5f;
            var zN = eyeZ - 0.087f;
            var ySide = eyeY - k.necklaceBack;
            var yFront = eyeY - k.necklaceFront;
            var xs = k.necklaceSide;
            // 飾りの真ん中: 胸の真ん中で、鎖の一番低い所から (0.5 mm + 半径) 下の、いちばん前の点
            var cy = yFront - 0.0005f - k.pendantRadius;
            var centre = Vector3.zero;
            var best = float.MinValue;
            for (var i = 0; i < px.Length; i++)
            {
                if (!s.On[i]) continue;
                var p = s.P[i];
                if (Mathf.Abs(p.x) > 0.003f || Mathf.Abs(p.y - cy) > 0.0015f || p.z < zN) continue;
                if (p.z > best) { best = p.z; centre = p; }
            }
            var hasPendant = best > float.MinValue;
            var half = k.necklaceWidth * 0.5f;
            for (var i = 0; i < px.Length; i++)
            {
                if (!s.On[i]) continue;
                var p = s.P[i];
                if (Mathf.Abs(p.x) > xs + 0.02f || p.y > ySide + 0.01f || p.y < yFront - 0.02f) continue;
                float d;
                if (p.z > zN + 0.015f && Mathf.Abs(p.x) <= xs)
                {
                    // 前: 左右の位置で下がる
                    var t = Mathf.Pow(Mathf.Abs(p.x) / xs, k.necklaceCurve);
                    d = Mathf.Abs(p.y - Mathf.Lerp(yFront, ySide, t));
                }
                else
                {
                    // 横と後ろ: 首の付け根に沿って同じ高さ（首のまわり 8 cm まで）
                    if (new Vector2(p.x, p.z - zN).magnitude > 0.08f) continue;
                    d = Mathf.Abs(p.y - ySide);
                }
                var chain = Smooth(half + 0.0003f, half - 0.0003f, d);
                var rim = 0f;
                if (hasPendant)
                {
                    var dp = (p - centre).magnitude;
                    chain = Mathf.Max(chain, Smooth(k.pendantRadius + 0.0003f, k.pendantRadius - 0.0003f, dp));
                    rim = Smooth(k.pendantRadius + 0.0008f, k.pendantRadius + 0.0002f, dp) * (1f - chain);
                }
                if (chain <= 0f && rim <= 0f) continue;
                var c = px[i];
                if (rim > 0f) c = Color.Lerp(c, c * 0.7f, rim * 0.6f);
                // 銀の灰。肌の明るさで少し陰を付ける（明るすぎて襟の縁取りに見えないように）
                var silver = k.necklaceColour * Mathf.Lerp(0.85f, 1.0f, Mathf.Clamp01(Lum(px[i]) / 0.6f));
                silver.a = c.a;
                px[i] = Color.Lerp(c, silver, chain);
            }
        }

        /// <summary>
        /// 鼻の陰影を寄せる所の重み（0〜1）。鼻筋（目の高さ）から鼻の下（鼻の骨の 1.2〜2 cm 下）まで、真ん中から 1.4 cm は全部、2.6 cm で 0。
        /// 顔の前だけ（鼻の骨の 3.5〜5 cm 奥より前）
        /// </summary>
        public static float NoseZone(Vector3 p, Anchors a)
        {
            var eyeY = (a.eyeL.y + a.eyeR.y) * 0.5f;
            var across = Smooth(0.026f, 0.014f, Mathf.Abs(p.x));
            var along = Smooth(eyeY + 0.004f, eyeY - 0.008f, p.y) * Smooth(a.nose.y - 0.020f, a.nose.y - 0.012f, p.y);
            var front = Smooth(a.nose.z - 0.050f, a.nose.z - 0.035f, p.z);
            return across * along * front;
        }

        static bool InNose(Vector3 p, Anchors a)
        {
            return Mathf.Abs(p.x) < 0.022f && p.y > a.nose.y - 0.020f && p.y < a.nose.y + 0.006f && p.z > a.nose.z - 0.040f;
        }

        /// <summary>眉の帯。内と外の眉の骨を結ぶ線の上下 9 mm。重みは 0〜1</summary>
        static float BrowZone(Vector3 p, Anchors a)
        {
            var left = p.x < 0f;
            var inner = left ? a.browInL : a.browInR;
            var outer = left ? a.browOutL : a.browOutR;
            var ax = Mathf.Abs(p.x);
            float ix = Mathf.Abs(inner.x), ox = Mathf.Abs(outer.x);
            if (ax < ix - 0.014f || ax > ox + 0.016f) return 0f;
            if (p.z < outer.z - 0.02f) return 0f;
            var t = Mathf.Clamp01((ax - ix) / (ox - ix));
            var lineY = Mathf.Lerp(inner.y, outer.y, t);
            var dy = p.y - lineY;
            if (dy < -0.010f || dy > 0.010f) return 0f;
            return Smooth(0.010f, 0.006f, Mathf.Abs(dy)) * Smooth(ix - 0.014f, ix - 0.008f, ax) * Smooth(ox + 0.016f, ox + 0.008f, ax);
        }

        // ---- 体 ---------------------------------------------------------------

        /// <summary>体のテクスチャ（服・手・靴）のカーディガンを黒に。重みも返す（撮り比べの測りに使う）</summary>
        public static Color[] Body(Color[] src, Surface s, Look k, out float[] knit)
        {
            var n = s.N;
            var px = (Color[])src.Clone();
            var H = new float[n * n];
            var S = new float[n * n];
            var V = new float[n * n];
            for (var i = 0; i < px.Length; i++) Color.RGBToHSV(src[i], out H[i], out S[i], out V[i]);
            Outfit(px, s, H, S, V, k);
            if (!k.blackenKnit)
            {
                knit = new float[n * n];
                return px;
            }
            var w = new float[n * n];
            for (var i = 0; i < px.Length; i++)
            {
                // ニットは色相 35 度前後・彩度 0.18 前後で揃っている。デニムは青、手は彩度が高い、靴は暗い
                var hue = HueNear(H[i] * 360f, 35.5f, 9f, 5f);
                var sat = Band(S[i], 0.07f, 0.11f, 0.28f, 0.33f);
                var val = Smooth(0.10f, 0.16f, V[i]);
                // 島の外（詰め物）は色だけで決める。靴は膝より下にあるので、島の中は高さでも外す
                var high = s.On[i] ? Smooth(0.45f, 0.60f, s.P[i].y) : 1f;
                w[i] = hue * sat * val * high;
            }
            var all = new bool[n * n];
            for (var i = 0; i < all.Length; i++) all[i] = true;
            w = Blur(w, all, n, 1);
            for (var i = 0; i < px.Length; i++)
            {
                if (w[i] <= 0f) continue;
                var t = Mathf.Pow(Mathf.Clamp01((V[i] - k.knitLo) / (k.knitHi - k.knitLo)), k.knitGamma);
                var c = Color.Lerp(k.knitShadow, k.knitShine, t);
                c.a = px[i].a;
                px[i] = Color.Lerp(px[i], c, Mathf.Clamp01(w[i] * 1.15f));
            }
            knit = w;
            return px;
        }

        /// <summary>
        /// パンツ（デニム。色相 190 度前後の青で、脛から腰まで）、靴（足首より下）、中のトップス（胸の高さの暗い茶）を塗り直す。
        /// 場所は UV の島の中は束ねた姿勢の高さと色で決め、島の外（詰め物）はとなりの島の中から広げる。
        /// 色は元の明るさ（その所の 5〜95 % の幅）を影の色から明るい色へ写す（布の皺や縫い目の明暗を残す）
        /// </summary>
        static void Outfit(Color[] px, Surface s, float[] H, float[] S, float[] V, Look k)
        {
            if (!k.recolourPants && !k.recolourShoes && !k.recolourTop) return;
            var n = s.N;
            var pants = new float[n * n];
            var shoes = new float[n * n];
            var top = new float[n * n];
            var neckSkin = new float[n * n];
            for (var i = 0; i < px.Length; i++)
            {
                if (!s.On[i]) { pants[i] = shoes[i] = top[i] = -1f; continue; }
                var y = s.P[i].y;
                // 脛から腿（上着の裾より下）は全部パンツ（明るい縫い目も含める）。腰はパンツの色（女大 14 はデニムの青、負なら暗い所）だけ
                var cloth = k.pantsHue >= 0f
                    ? HueNear(H[i] * 360f, k.pantsHue, 25f, 12f) * Band(S[i], 0.06f, 0.09f, 0.45f, 0.55f)
                    : Smooth(0.34f, 0.26f, V[i]) * Smooth(0.30f, 0.22f, S[i]);
                var ax = Mathf.Abs(s.P[i].x);
                cloth *= Smooth(k.pantsValMax + 0.04f, k.pantsValMax - 0.04f, V[i]);
                pants[i] = Smooth(0.10f, 0.14f, y) * Mathf.Max(Smooth(k.pantsAllBelow + 0.02f, k.pantsAllBelow - 0.02f, y), cloth * Smooth(k.pantsTop, k.pantsTop - 0.05f, y) * Smooth(0.26f, 0.22f, ax));
                shoes[i] = Smooth(0.18f, 0.15f, y) * (1f - pants[i]);
                // 中のトップス: 色相と彩度と明るさで見分ける（女大 14 は胸の V の開きから見える、彩度の高い暗い茶。ニットは彩度 0.18）
                var u = (i % n + 0.5f) / n;
                var v = (i / n + 0.5f) / n;
                if (k.topKeepUv.width > 0f && k.topKeepUv.Contains(new Vector2(u, v))) { top[i] = 0f; continue; }
                top[i] = Smooth(k.topLow, k.topLow + 0.05f, y) * Smooth(k.topHigh, k.topHigh - 0.05f, y) * Smooth(k.topHalfWidth + 0.02f, k.topHalfWidth - 0.02f, ax)
                    * HueNear(H[i] * 360f, k.topHue, k.topHueWidth, 8f)
                    * Smooth(k.topSatMin - 0.05f, k.topSatMin + 0.05f, S[i]) * Smooth(k.topValMax + 0.03f, k.topValMax - 0.03f, V[i]);
                // 胸の開き: 肌も、開きの縁の暗い線も布にする（縁の線が暗い点の列に見えた）
                if (k.topNeckSkin) neckSkin[i] = Smooth(1.18f, 1.23f, y) * Smooth(0.14f, 0.10f, ax) * Smooth(-0.02f, 0.02f, s.P[i].z) * (1f - top[i]);
            }
            if (k.recolourPants) Recolour(px, V, Spread(pants, n), k.pantsShadow, k.pantsShine);
            if (k.recolourShoes) Recolour(px, V, Spread(shoes, n), k.shoeShadow, k.shoeShine);
            if (k.recolourTop)
            {
                var vt = V;
                if (k.topNeckSkin)
                {
                    // 胸の開きの肌は、布の真ん中の明るさで塗る（肌の明るさのまま写すと、まわりの布より明るい面になった）
                    var vs = new List<float>();
                    for (var i = 0; i < px.Length; i++) if (top[i] > 0.5f) vs.Add(V[i]);
                    vs.Sort();
                    var mid = vs.Count > 0 ? vs[vs.Count / 2] : 0.3f;
                    vt = (float[])V.Clone();
                    for (var i = 0; i < px.Length; i++)
                    {
                        if (neckSkin[i] <= 0f) continue;
                        vt[i] = Mathf.Lerp(V[i], mid, neckSkin[i]);
                        top[i] = Mathf.Max(top[i], neckSkin[i]);
                    }
                }
                Recolour(px, vt, Spread(top, n), k.topShadow, k.topShine);
            }
        }

        /// <summary>縦横 (2r+1) 画素の四角の中の最大（max）か最小。絵の外は数えない</summary>
        public static float[] MinMax(float[] a, int n, int r, bool max)
        {
            var tmp = new float[a.Length];
            var o = new float[a.Length];
            for (var y = 0; y < n; y++)
                for (var x = 0; x < n; x++)
                {
                    var v = a[y * n + x];
                    for (var k = Mathf.Max(0, x - r); k <= Mathf.Min(n - 1, x + r); k++) v = max ? Mathf.Max(v, a[y * n + k]) : Mathf.Min(v, a[y * n + k]);
                    tmp[y * n + x] = v;
                }
            for (var y = 0; y < n; y++)
                for (var x = 0; x < n; x++)
                {
                    var v = tmp[y * n + x];
                    for (var k = Mathf.Max(0, y - r); k <= Mathf.Min(n - 1, y + r); k++) v = max ? Mathf.Max(v, tmp[k * n + x]) : Mathf.Min(v, tmp[k * n + x]);
                    o[y * n + x] = v;
                }
            return o;
        }

        /// <summary>
        /// 首から下の肌（頭のテクスチャの首と胸の上）を、白い丸首のシャツとして塗る。襟ぐりは首の付け根で丸く、前が低く後ろが高い。
        /// 明暗は元の肌の明るさから取り、襟ぐりのすぐ下を少し暗くしてリブに見せる。ネックレスもシャツの下に消える
        /// </summary>
        static void Shirt(Color[] px, Surface s, Anchors a, Look k)
        {
            var line = new float[px.Length];
            var lums = new List<float>();
            for (var i = 0; i < px.Length; i++)
            {
                if (!s.On[i]) continue;
                var p = s.P[i];
                var front = Smooth(a.head.z - 0.01f, a.head.z + 0.06f, p.z);
                line[i] = Mathf.Lerp(a.head.y - k.neckBack, a.head.y - k.neckFront, front) + k.neckRound * p.x * p.x;
                if (p.y < line[i] - 0.01f) lums.Add(Lum(px[i]));
            }
            if (lums.Count == 0) return;
            lums.Sort();
            var median = Mathf.Max(0.05f, lums[lums.Count / 2]);
            for (var i = 0; i < px.Length; i++)
            {
                if (!s.On[i]) continue;
                var p = s.P[i];
                var w = Smooth(line[i] + 0.0012f, line[i] - 0.0012f, p.y);
                if (w <= 0f) continue;
                var shade = k.shirtFlat
                    ? 1f + k.shirtRib * Mathf.Sin(p.x / Mathf.Max(0.001f, k.shirtRibPitch) * Mathf.PI * 2f)
                    : Mathf.Clamp(Lum(px[i]) / median, 0.80f, 1.06f);
                var rib = 1f - 0.12f * Smooth(0.005f, 0.002f, line[i] - p.y);
                var c = k.shirtColour * (shade * rib);
                c.a = px[i].a;
                px[i] = Color.Lerp(px[i], c, w);
            }
        }

        /// <summary>
        /// 首の付け根（頭の骨から 8 cm 下）より下で肌でない所を、首元の肌の色で塗る。明暗は元の明るさの比から取る
        /// </summary>
        static void ChestSkin(Color[] px, Surface s, Anchors a, float[] hair)
        {
            var top = a.head.y - 0.08f;
            Vector3 sum = Vector3.zero;
            var cnt = 0;
            var lums = new List<float>();
            for (var i = 0; i < px.Length; i++)
            {
                if (!s.On[i] || hair[i] > 0.1f) continue;
                var p = s.P[i];
                if (p.y > top + 0.03f || p.y < top - 0.03f || p.z < a.head.z + 0.02f) continue;
                if (SkinLike(px[i]) < 0.5f) continue;
                sum += new Vector3(px[i].r, px[i].g, px[i].b);
                lums.Add(Lum(px[i]));
                cnt++;
            }
            if (cnt == 0) return;
            var skin = sum / cnt;
            lums.Sort();
            var median = Mathf.Max(0.05f, lums[lums.Count / 2]);
            var fill = new List<int>();
            for (var i = 0; i < px.Length; i++)
            {
                if (!s.On[i] || hair[i] > 0.1f) continue;
                var p = s.P[i];
                var w = Smooth(top + 0.004f, top - 0.004f, p.y) * (1f - SkinLike(px[i]));
                if (w <= 0f) continue;
                var shade = Mathf.Clamp(Lum(px[i]) / median, 0.75f, 1.05f);
                // 暗い服の明るさは肌より低いので、明暗は弱めに写す
                shade = Mathf.Lerp(1f, shade, 0.3f);
                var c = new Color(skin.x * shade, skin.y * shade, skin.z * shade, px[i].a);
                px[i] = Color.Lerp(px[i], c, Mathf.Clamp01(w * 1.5f));
                if (w > 0.05f) fill.Add(i);
            }
            // 首に掛かったネックレスの鎖（首の付け根より上の、銀色の細い線）: 彩度が低く明るい画素を、下のならしでまわりの肌で埋める
            // （胸元だけを塗ると、首の付け根の上に白い線が残った）
            var fillSet = new HashSet<int>(fill);
            for (var i = 0; i < px.Length; i++)
            {
                if (!s.On[i] || hair[i] > 0.1f || fillSet.Contains(i)) continue;
                var p = s.P[i];
                if (p.y < top - 0.004f || p.y > a.head.y - 0.02f) continue;
                float h, sat, v;
                Color.RGBToHSV(px[i], out h, out sat, out v);
                if (sat > 0.20f || v < 0.40f) continue;
                fill.Add(i);
            }
            // 塗った所を、まわりの肌からならす（となりの平均を 400 回。まわりの肌の色が中まで届く）。一色のままだと、元の肌との境が角張った前掛けの形に見えた
            var n = s.N;
            for (var it = 0; it < 400; it++)
            {
                var next = new Dictionary<int, Color>();
                foreach (var i in fill)
                {
                    int x = i % n, y = i / n;
                    var acc = new Color(0f, 0f, 0f, 0f);
                    var cnt2 = 0;
                    foreach (var j in new[] { x > 0 ? i - 1 : -1, x < n - 1 ? i + 1 : -1, y > 0 ? i - n : -1, y < n - 1 ? i + n : -1 })
                    {
                        if (j < 0 || !s.On[j]) continue;
                        acc += px[j];
                        cnt2++;
                    }
                    if (cnt2 > 0) next[i] = acc / cnt2;
                }
                foreach (var kv in next)
                {
                    var c = kv.Value;
                    c.a = px[kv.Key].a;
                    px[kv.Key] = c;
                }
            }
        }

        /// <summary>
        /// 顔を寄せる（<see cref="Look"/> の ageSoften・blush・catEye・noseSlim・cheekShade・lipLine）。場所は顔の骨からの距離で決める。
        /// - 年齢の出る影: 目の下（くまとたるみ）・ほうれい線・額のしわの所で、細かい暗がり（1 画素でぼかした明るさが 6 画素でぼかした明るさより暗い分）を持ち上げる
        /// - 血色: 頬の高い所に薄い赤み、唇の赤みを少し強く
        /// - 切れ長: 目尻の先へ上がる短い線を上瞼の際の色で描く
        /// - 鼻筋: 鼻筋の両脇を少し暗く、真ん中を少し明るく
        /// - 頬骨の下: 耳の前から口の端へ下がる帯を少し暗く
        /// - 唇の輪郭: 唇の縁のすぐ外を少し暗く
        /// </summary>
        static void FaceTouch(Color[] px, Surface s, Anchors a, Look k, float[] hair, float[] brow, Color[] src, int n)
        {
            if (k.ageSoften <= 0f && k.blush <= 0f && k.catEye <= 0f && k.noseSlim <= 0f && k.cheekShade <= 0f && k.lipLine <= 0f) return;
            var scale = n / 512f;
            var eyeY = (a.eyeL.y + a.eyeR.y) * 0.5f;
            var browTop = Mathf.Max(Mathf.Max(a.browInL.y, a.browOutL.y), Mathf.Max(a.browInR.y, a.browOutR.y));
            var front = new float[px.Length];
            for (var i = 0; i < px.Length; i++) front[i] = s.On[i] ? Smooth(a.head.z - 0.01f, a.head.z + 0.03f, s.P[i].z) * (1f - Mathf.Clamp01(hair[i] * 2f)) : 0f;

            if (k.ageSoften > 0f)
            {
                var Y = new float[px.Length];
                for (var i = 0; i < px.Length; i++) Y[i] = Lum(px[i]);
                var y1 = Blur(Y, s.On, n, Mathf.Max(1, Mathf.RoundToInt(1 * scale)));
                var y6 = Blur(Y, s.On, n, Mathf.Max(2, Mathf.RoundToInt(6 * scale)));
                // 目の下のくまは広いので、頬の高い所（頬の骨から 1.2 cm 以内）の明るさの中ほどまで持ち上げる
                var cheekLums = new List<float>();
                for (var i = 0; i < px.Length; i++)
                {
                    if (front[i] <= 0.5f) continue;
                    var p = s.P[i];
                    if (Vector2.Distance(new Vector2(p.x, p.y), new Vector2(a.cheekL.x, a.cheekL.y)) < 0.012f || Vector2.Distance(new Vector2(p.x, p.y), new Vector2(a.cheekR.x, a.cheekR.y)) < 0.012f)
                        cheekLums.Add(y1[i]);
                }
                cheekLums.Sort();
                var cheekRef = cheekLums.Count > 0 ? cheekLums[cheekLums.Count / 2] : 0f;
                for (var i = 0; i < px.Length; i++)
                {
                    if (front[i] <= 0f) continue;
                    var p = s.P[i];
                    var zone = 0f;
                    var under = 0f;
                    foreach (var eye in new[] { a.eyeL, a.eyeR })
                    {
                        var e = Mathf.Sqrt(Sq((p.x - eye.x) / 0.019f) + Sq((p.y - (eye.y - 0.013f)) / 0.009f));
                        under = Mathf.Max(under, Smooth(1f, 0.5f, e) * Smooth(eye.y - 0.004f, eye.y - 0.008f, p.y));
                    }
                    zone = under;
                    foreach (var side in new[] { -1f, 1f })
                    {
                        var corner = side < 0f ? a.mouthL : a.mouthR;
                        var from = new Vector2(side * 0.016f, a.nose.y - 0.003f);
                        var to = new Vector2(corner.x + side * 0.006f, corner.y - 0.002f);
                        var d = SegDist(new Vector2(p.x, p.y), from, to);
                        zone = Mathf.Max(zone, Smooth(0.009f, 0.003f, d));
                    }
                    zone = Mathf.Max(zone, Smooth(browTop + 0.006f, browTop + 0.014f, p.y) * Smooth(eyeY + 0.075f, eyeY + 0.06f, p.y) * Smooth(0.05f, 0.04f, Mathf.Abs(p.x)));
                    zone *= front[i] * (1f - brow[i]);
                    if (zone <= 0f) continue;
                    var lift = Mathf.Max(0f, y6[i] - y1[i]) * 1.4f * k.ageSoften * zone
                        + Mathf.Max(0f, cheekRef * 0.97f - y1[i]) * 0.8f * k.ageSoften * under * front[i];
                    var g = Mathf.Min(1.3f, (Y[i] + lift) / Mathf.Max(0.05f, Y[i]));
                    var c = px[i];
                    px[i] = new Color(Mathf.Clamp01(c.r * g), Mathf.Clamp01(c.g * g), Mathf.Clamp01(c.b * g), c.a);
                }
            }

            // 唇の所（元の赤みが肌より強い所）
            var lip = new float[px.Length];
            {
                var cy = (a.upperLip.y + a.lowerLip.y) * 0.5f;
                var half = Mathf.Abs(a.mouthL.x - a.mouthR.x) * 0.5f + 0.003f;
                for (var i = 0; i < px.Length; i++)
                {
                    if (!s.On[i]) continue;
                    var p = s.P[i];
                    if (p.z < a.mouthL.z - 0.012f) continue;
                    var e = Mathf.Sqrt(Sq(p.x / half) + Sq((p.y - cy) / 0.0115f));
                    var zone = Smooth(1.0f, 0.70f, e);
                    if (zone <= 0f) continue;
                    var c = src[i];
                    lip[i] = zone * Smooth(0.26f, 0.31f, (c.r - c.g) / Mathf.Max(0.05f, c.r));
                }
            }

            for (var i = 0; i < px.Length; i++)
            {
                if (!s.On[i]) continue;
                var p = s.P[i];
                var c = px[i];
                // 血色: 頬の高い所と唇
                if (k.blush > 0f && front[i] > 0f)
                {
                    var w = 0f;
                    foreach (var cheek in new[] { a.cheekL, a.cheekR })
                    {
                        var e = Mathf.Sqrt(Sq((p.x - cheek.x * 1.12f) / 0.020f) + Sq((p.y - (cheek.y - 0.004f)) / 0.013f));
                        w = Mathf.Max(w, Smooth(1f, 0.2f, e));
                    }
                    w = w * front[i] * k.blush * 0.5f + lip[i] * k.blush * 0.35f;
                    c = Color.Lerp(c, new Color(c.r * k.blushColour.r / 0.9f, c.g * k.blushColour.g / 0.9f, c.b * k.blushColour.b / 0.9f, c.a), Mathf.Clamp01(w));
                }
                // 鼻筋
                if (k.noseSlim > 0f && front[i] > 0f)
                {
                    var along = Smooth(eyeY - 0.002f, eyeY - 0.008f, p.y) * Smooth(a.nose.y + 0.000f, a.nose.y + 0.006f, p.y);
                    var side = Smooth(0.0045f, 0.0015f, Mathf.Abs(Mathf.Abs(p.x) - 0.0095f));
                    var mid = Smooth(0.0035f, 0.001f, Mathf.Abs(p.x));
                    var g = 1f - 0.12f * k.noseSlim * side * along + 0.05f * k.noseSlim * mid * along;
                    c = new Color(Mathf.Clamp01(c.r * g), Mathf.Clamp01(c.g * g), Mathf.Clamp01(c.b * g), c.a);
                }
                // 頬骨の下
                if (k.cheekShade > 0f && front[i] > 0f)
                {
                    var w = 0f;
                    foreach (var side in new[] { -1f, 1f })
                    {
                        var corner = side < 0f ? a.mouthL : a.mouthR;
                        var d = SegDist(new Vector2(p.x, p.y), new Vector2(side * 0.060f, eyeY - 0.030f), new Vector2(corner.x + side * 0.012f, corner.y + 0.012f));
                        w = Mathf.Max(w, Smooth(0.008f, 0.002f, d));
                    }
                    var g = 1f - 0.09f * k.cheekShade * w * front[i];
                    c = new Color(c.r * g, c.g * (g - 0.005f * w), c.b * (g - 0.005f * w), c.a);
                }
                px[i] = c;
            }

            // 唇の輪郭: 唇の縁のすぐ外を少し暗く
            if (k.lipLine > 0f)
            {
                var near = Blur(lip, s.On, n, Mathf.Max(1, Mathf.RoundToInt(2 * scale)));
                for (var i = 0; i < px.Length; i++)
                {
                    var w = Smooth(0.08f, 0.35f, near[i]) * (1f - Smooth(0.35f, 0.7f, lip[i]));
                    if (w <= 0f) continue;
                    var g = 1f - 0.22f * k.lipLine * w;
                    var c = px[i];
                    px[i] = new Color(c.r * g, c.g * g * 0.98f, c.b * g * 0.98f, c.a);
                }
            }

            // 切れ長: 目尻の先へ上がる短い線
            if (k.catEye > 0f)
            {
                foreach (var eye in new[] { a.eyeL, a.eyeR })
                {
                    var outward = Mathf.Sign(eye.x);
                    var from = new Vector2(eye.x + outward * 0.0115f, eye.y + 0.0012f);
                    var to = new Vector2(eye.x + outward * 0.0185f, eye.y + 0.0042f);
                    for (var i = 0; i < px.Length; i++)
                    {
                        if (!s.On[i]) continue;
                        var p = s.P[i];
                        if (p.z < eye.z - 0.012f) continue;
                        var q = new Vector2(p.x, p.y);
                        var t = Mathf.Clamp01(Vector2.Dot(q - from, to - from) / (to - from).sqrMagnitude);
                        var d = SegDist(q, from, to);
                        var width = Mathf.Lerp(0.0011f, 0.0003f, t);
                        var w = Smooth(width + 0.0004f, width, d) * k.catEye;
                        if (w <= 0f) continue;
                        px[i] = Color.Lerp(px[i], new Color(k.lashColour.r, k.lashColour.g, k.lashColour.b, px[i].a), Mathf.Clamp01(w));
                    }
                }
            }
        }

        static float SegDist(Vector2 p, Vector2 a, Vector2 b)
        {
            var ab = b - a;
            var t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(1e-12f, ab.sqrMagnitude));
            return Vector2.Distance(p, a + ab * t);
        }

        /// <summary>負の値（島の外）を、となりの値の平均で 4 回まで広げる。届かない所は 0</summary>
        static float[] Spread(float[] w, int n)
        {
            for (var pass = 0; pass < 4; pass++)
            {
                var next = (float[])w.Clone();
                for (var y = 0; y < n; y++)
                    for (var x = 0; x < n; x++)
                    {
                        var i = y * n + x;
                        if (w[i] >= 0f) continue;
                        float sum = 0f;
                        var cnt = 0;
                        if (x > 0 && w[i - 1] >= 0f) { sum += w[i - 1]; cnt++; }
                        if (x < n - 1 && w[i + 1] >= 0f) { sum += w[i + 1]; cnt++; }
                        if (y > 0 && w[i - n] >= 0f) { sum += w[i - n]; cnt++; }
                        if (y < n - 1 && w[i + n] >= 0f) { sum += w[i + n]; cnt++; }
                        if (cnt > 0) next[i] = sum / cnt;
                    }
                w = next;
            }
            for (var i = 0; i < w.Length; i++) if (w[i] < 0f) w[i] = 0f;
            return w;
        }

        /// <summary>
        /// 服（肌でない所）を一つの布の色に塗る（<see cref="Look.dress"/>）。場所は束ねた姿勢の高さ fromY〜toY。
        /// 明るさは元の服の明るさの 5〜95 % を布の陰から明るい所へ広げる（元の服が白でも黒でも、同じ布に見える）。
        /// 青い下着（色相 150〜225°）は、まわりの服の真ん中の明るさにしてから塗る（胸元に濃い斑が残らないように）。
        /// pad なら UV の島の外（詰め物）も布の明るい所の色にする（縁に元の黒が滲まないように）
        /// </summary>
        public static void Dress(Color[] px, Surface s, Look k, float fromY, float toY, bool pad)
        {
            var n = s.N;
            var V = new float[px.Length];
            var w = new float[px.Length];
            var blue = new bool[px.Length];
            var vs = new List<float>();
            for (var i = 0; i < px.Length; i++)
            {
                float h, sat, v;
                Color.RGBToHSV(px[i], out h, out sat, out v);
                V[i] = v;
                if (!s.On[i]) continue;
                var y = s.P[i].y;
                if (y < fromY || y > toY) continue;
                // 足とサンダル（15 cm より下で、テクスチャの上の帯）は塗らない
                if (pad && y < 0.15f && (i / n + 0.5f) / n > 0.6f) continue;
                w[i] = Smooth(0.55f, 0.25f, SkinLike(px[i]));
                blue[i] = w[i] > 0.5f && h * 360f > 150f && h * 360f < 225f && sat > 0.2f;
                if (w[i] > 0.5f && !blue[i]) vs.Add(v);
            }
            if (vs.Count == 0) return;
            vs.Sort();
            var mid = vs[vs.Count / 2];
            for (var i = 0; i < px.Length; i++) if (blue[i]) V[i] = mid;
            Recolour(px, V, w, k.dressShadow, k.dressShine);
            if (pad)
                for (var i = 0; i < px.Length; i++)
                    if (!s.On[i])
                    {
                        var c = Color.Lerp(k.dressShadow, k.dressShine, 0.6f);
                        c.a = px[i].a;
                        px[i] = c;
                    }
        }

        static void Recolour(Color[] px, float[] V, float[] w, Color shadow, Color shine)
        {
            var vs = new List<float>();
            for (var i = 0; i < px.Length; i++) if (w[i] > 0.5f) vs.Add(V[i]);
            if (vs.Count == 0) return;
            vs.Sort();
            var lo = vs[(int)(vs.Count * 0.05f)];
            var hi = Mathf.Max(lo + 0.02f, vs[(int)(vs.Count * 0.95f)]);
            for (var i = 0; i < px.Length; i++)
            {
                if (w[i] <= 0f) continue;
                var c = Color.Lerp(shadow, shine, Mathf.Clamp01((V[i] - lo) / (hi - lo)));
                c.a = px[i].a;
                px[i] = Color.Lerp(px[i], c, Mathf.Clamp01(w[i]));
            }
        }

        // ---- 髪の房（透けの絵） -----------------------------------------------

        /// <summary>透けの絵（髪の房とまつ毛）を黒に。α は元のまま。透けた地も黒くして縁に肌色が滲まないようにする</summary>
        public static Color[] Hair(Color[] src, Look k)
        {
            var px = (Color[])src.Clone();
            for (var i = 0; i < px.Length; i++)
            {
                var c = src[i];
                float h, sat, v;
                Color.RGBToHSV(c, out h, out sat, out v);
                var o = k.naturalHair ? (c.a < 0.02f ? k.naturalHairInk : c) : (c.a < 0.02f ? k.hairShadow : HairRamp(v, k));
                o.a = c.a;
                px[i] = o;
            }
            return px;
        }

        /// <summary>
        /// 頭の Specular の絵。髪は照り返しを 0 にする（黒く塗った髪は拡散の光がほとんど無く、
        /// 横の強い光では照り返しだけが灰色に残って、長い髪の面がフードのように光るため）。
        /// 肌は 0.04、目の玉は滑らかにして主光を映す
        /// </summary>
        static Color32[] SpecMap(float[] hair, Surface s, Vector2 irisUv, float irisRadius, Look k)
        {
            var n = s.N;
            var o = new Color32[n * n];
            var skinSpec = (byte)Mathf.RoundToInt(Mathf.LinearToGammaSpace(k.skinSpecular) * 255f);
            var ball = irisRadius * 3.2f;
            for (var y = 0; y < n; y++)
                for (var x = 0; x < n; x++)
                {
                    var i = y * n + x;
                    var h = Mathf.Clamp01(hair[i]);
                    var eye = Smooth(ball, ball * 0.8f, Vector2.Distance(new Vector2((x + 0.5f) / n, (y + 0.5f) / n), irisUv));
                    var smooth = Mathf.Lerp(Mathf.Lerp(k.skinSmoothness, k.hairSmoothness, h), k.eyeSmoothness, eye);
                    var spec = (byte)Mathf.RoundToInt(skinSpec * (1f - h * (1f - eye)));
                    o[i] = new Color32(spec, spec, spec, (byte)Mathf.RoundToInt(Mathf.Clamp01(smooth) * 255f));
                }
            return o;
        }

        /// <summary>
        /// 体のテクスチャの肌（手）の色を、頭のテクスチャの首元の肌の色に揃える（頭と体が別の人のとき）。
        /// 首元は首の付け根から胸の上（目の高さから 11〜21 cm 下）の前側で髪でない所、手は束ねた姿勢で左右 38 cm より外の肌の色の所。
        /// 線形の光の平均の比を、肌の色の所にだけ掛ける。揃える前と後の平均（sRGB）と色差を返す
        /// </summary>
        public static Color[] MatchSkin(Color[] body, Surface bodyMap, Color[] head, float[] headHair, Surface headMap, Anchors a, Look k, out string note)
        {
            var o = (Color[])body.Clone();
            var eyeY = (a.eyeL.y + a.eyeR.y) * 0.5f;
            Vector3 hs = Vector3.zero; var hn = 0;
            for (var i = 0; i < head.Length; i++)
            {
                if (!headMap.On[i] || headHair[i] > 0.1f) continue;
                var p = headMap.P[i];
                if (p.y > eyeY - 0.11f || p.y < eyeY - 0.21f || p.z < a.head.z + 0.02f) continue;
                if (SkinLike(head[i]) < 0.5f) continue;
                hs += Lin(head[i]);
                hn++;
            }
            Vector3 bs = Vector3.zero; var bn = 0;
            var w = new float[body.Length];
            for (var i = 0; i < body.Length; i++)
            {
                if (!bodyMap.On[i]) continue;
                if (!k.matchSkinAll && Mathf.Abs(bodyMap.P[i].x) < 0.38f) continue;
                w[i] = SkinLike(body[i]);
                if (w[i] < 0.5f) continue;
                bs += Lin(body[i]);
                bn++;
            }
            if (hn == 0 || bn == 0)
            {
                note = "肌の色を揃えられない（首元 " + hn + "・手 " + bn + " 画素）";
                return o;
            }
            hs /= hn;
            bs /= bn;
            var gain = new Vector3(hs.x / bs.x, hs.y / bs.y, hs.z / bs.z);
            gain = Vector3.Lerp(Vector3.one, gain, Mathf.Clamp01(k.skinMatch));
            Vector3 after = Vector3.zero;
            for (var i = 0; i < body.Length; i++)
            {
                if (w[i] <= 0f) continue;
                var l = Lin(body[i]);
                var m = new Vector3(l.x * gain.x, l.y * gain.y, l.z * gain.z);
                var c = new Color(Mathf.LinearToGammaSpace(Mathf.Clamp01(m.x)), Mathf.LinearToGammaSpace(Mathf.Clamp01(m.y)), Mathf.LinearToGammaSpace(Mathf.Clamp01(m.z)), body[i].a);
                o[i] = Color.Lerp(body[i], c, w[i]);
                if (w[i] >= 0.5f) after += Lin(o[i]);
            }
            after /= bn;
            note = string.Format("首元の肌 {0}、手 {1} → {2}（Lab の色差 {3:0.0} → {4:0.0}、比 R {5:0.00} G {6:0.00} B {7:0.00}）",
                Srgb(hs), Srgb(bs), Srgb(after), DeltaE(hs, bs), DeltaE(hs, after), gain.x, gain.y, gain.z);
            return o;
        }

        /// <summary>
        /// 肌の色を、体の人の首元の肌を基に揃える。体の人の頭のテクスチャ（refHead）の首元の肌の平均を、顔の人の首元の肌の平均へ寄せる比を求め、
        /// その一つの比を body の肌らしい画素の全部に掛ける。体の人の体と頭のテクスチャ（胸元）に同じ比を掛けるので、二枚の間に色の差ができない
        /// （<see cref="MatchSkin"/> は絵ごとに肌の平均を取るので、体の人の胸元を頭の面で作ると、胸の上と下で色がずれた）
        /// </summary>
        public static Color[] MatchSkinBy(Color[] body, Surface bodyMap, Color[] head, float[] headHair, Surface headMap, Anchors a,
            Color[] refHead, Surface refMap, Anchors refA, Look k, out string note)
        {
            var o = (Color[])body.Clone();
            Vector3 hs, rs;
            var hn = NeckSkin(head, headHair, headMap, a, out hs);
            var rn = NeckSkin(refHead, null, refMap, refA, out rs);
            if (hn == 0 || rn == 0)
            {
                note = "肌の色を揃えられない（顔の人の首元 " + hn + "・体の人の首元 " + rn + " 画素）";
                return o;
            }
            var gain = new Vector3(hs.x / rs.x, hs.y / rs.y, hs.z / rs.z);
            gain = Vector3.Lerp(Vector3.one, gain, Mathf.Clamp01(k.skinMatch));
            var n = 0;
            for (var i = 0; i < body.Length; i++)
            {
                if (!bodyMap.On[i]) continue;
                if (!k.matchSkinAll && Mathf.Abs(bodyMap.P[i].x) < 0.38f) continue;
                var w = SkinLike(body[i]);
                if (w <= 0f) continue;
                var l = Lin(body[i]);
                var m = new Vector3(l.x * gain.x, l.y * gain.y, l.z * gain.z);
                var c = new Color(Mathf.LinearToGammaSpace(Mathf.Clamp01(m.x)), Mathf.LinearToGammaSpace(Mathf.Clamp01(m.y)), Mathf.LinearToGammaSpace(Mathf.Clamp01(m.z)), body[i].a);
                o[i] = Color.Lerp(body[i], c, w);
                if (w >= 0.5f) n++;
            }
            note = string.Format("顔の人の首元の肌 {0}、体の人の首元の肌 {1}（Lab の色差 {2:0.0}、比 R {3:0.00} G {4:0.00} B {5:0.00}）を肌の画素 {6} に掛けた",
                Srgb(hs), Srgb(rs), DeltaE(hs, rs), gain.x, gain.y, gain.z, n);
            return o;
        }

        /// <summary>首元（目の 11〜21 cm 下の前）の肌の平均（線形の光）。hair が null なら髪の所を除かない。数を返す</summary>
        static int NeckSkin(Color[] head, float[] hair, Surface map, Anchors a, out Vector3 mean)
        {
            var eyeY = (a.eyeL.y + a.eyeR.y) * 0.5f;
            mean = Vector3.zero;
            var n = 0;
            for (var i = 0; i < head.Length; i++)
            {
                if (!map.On[i] || (hair != null && hair[i] > 0.1f)) continue;
                var p = map.P[i];
                if (p.y > eyeY - 0.11f || p.y < eyeY - 0.21f || p.z < a.head.z + 0.02f) continue;
                if (SkinLike(head[i]) < 0.5f) continue;
                mean += Lin(head[i]);
                n++;
            }
            if (n > 0) mean /= n;
            return n;
        }

        /// <summary>肌らしさ（0〜1）。色相 5〜35 度、彩度 0.2〜0.65、明度 0.3 以上</summary>
        static float SkinLike(Color c)
        {
            float h, sat, v;
            Color.RGBToHSV(c, out h, out sat, out v);
            return HueNear(h * 360f, 20f, 15f, 6f) * Band(sat, 0.16f, 0.22f, 0.62f, 0.70f) * Smooth(0.25f, 0.35f, v);
        }

        static Vector3 Lin(Color c)
        {
            return new Vector3(Mathf.GammaToLinearSpace(c.r), Mathf.GammaToLinearSpace(c.g), Mathf.GammaToLinearSpace(c.b));
        }

        static string Srgb(Vector3 lin)
        {
            return string.Format("({0:0}, {1:0}, {2:0})", Mathf.LinearToGammaSpace(lin.x) * 255f, Mathf.LinearToGammaSpace(lin.y) * 255f, Mathf.LinearToGammaSpace(lin.z) * 255f);
        }

        /// <summary>CIE76 の色差（線形の sRGB から Lab へ、D65）</summary>
        public static float DeltaE(Vector3 a, Vector3 b)
        {
            var la = Lab(a);
            var lb = Lab(b);
            return Vector3.Distance(la, lb);
        }

        static Vector3 Lab(Vector3 rgb)
        {
            var X = 0.4124f * rgb.x + 0.3576f * rgb.y + 0.1805f * rgb.z;
            var Y = 0.2126f * rgb.x + 0.7152f * rgb.y + 0.0722f * rgb.z;
            var Z = 0.0193f * rgb.x + 0.1192f * rgb.y + 0.9505f * rgb.z;
            System.Func<float, float> f = t => t > 0.008856f ? Mathf.Pow(t, 1f / 3f) : 7.787f * t + 16f / 116f;
            float fx = f(X / 0.95047f), fy = f(Y), fz = f(Z / 1.08883f);
            return new Vector3(116f * fy - 16f, 500f * (fx - fy), 200f * (fy - fz));
        }

        static Color HairRamp(float v, Look k)
        {
            var t = Mathf.Pow(Mathf.Clamp01((v - k.hairLo) / (k.hairHi - k.hairLo)), k.hairGamma);
            return Color.Lerp(k.hairShadow, k.hairShine, t);
        }

        // ---- 道具 -------------------------------------------------------------

        /// <summary>覆われた画素だけで平均する箱のぼかし（縦横に分けて和で）</summary>
        public static float[] Blur(float[] v, bool[] on, int n, int r)
        {
            var num = new float[n * n];
            var den = new float[n * n];
            var tn = new float[n * n];
            var td = new float[n * n];
            for (var i = 0; i < v.Length; i++)
            {
                if (!on[i]) continue;
                num[i] = v[i];
                den[i] = 1f;
            }
            // 横
            for (var y = 0; y < n; y++)
            {
                float sn = 0f, sd = 0f;
                for (var x = -r; x < n + r; x++)
                {
                    var add = x + r;
                    if (add >= 0 && add < n) { sn += num[y * n + add]; sd += den[y * n + add]; }
                    var sub = x - r - 1;
                    if (sub >= 0 && sub < n) { sn -= num[y * n + sub]; sd -= den[y * n + sub]; }
                    if (x >= 0 && x < n) { tn[y * n + x] = sn; td[y * n + x] = sd; }
                }
            }
            // 縦
            var o = new float[n * n];
            for (var x = 0; x < n; x++)
            {
                float sn = 0f, sd = 0f;
                for (var y = -r; y < n + r; y++)
                {
                    var add = y + r;
                    if (add >= 0 && add < n) { sn += tn[add * n + x]; sd += td[add * n + x]; }
                    var sub = y - r - 1;
                    if (sub >= 0 && sub < n) { sn -= tn[sub * n + x]; sd -= td[sub * n + x]; }
                    if (y >= 0 && y < n) o[y * n + x] = sd > 0.5f ? sn / sd : v[y * n + x];
                }
            }
            for (var i = 0; i < o.Length; i++) if (!on[i]) o[i] = v[i];
            return o;
        }

        /// <summary>from から to へ 0 から 1 へ滑らかに（from &gt; to なら下がる）</summary>
        public static float Smooth(float from, float to, float x)
        {
            var t = Mathf.Clamp01((x - from) / (to - from));
            return t * t * (3f - 2f * t);
        }

        static float Band(float x, float a0, float a1, float b1, float b0)
        {
            return Smooth(a0, a1, x) * Smooth(b0, b1, x);
        }

        static float HueNear(float h, float centre, float full, float soft)
        {
            var d = Mathf.Abs(Mathf.DeltaAngle(h, centre));
            return Smooth(full + soft, full, d);
        }

        static float Sq(float x) { return x * x; }

        public static float Lum(Color c) { return c.r * 0.299f + c.g * 0.587f + c.b * 0.114f; }

        static void ToYCC(Color c, out float y, out float cb, out float cr)
        {
            y = 0.299f * c.r + 0.587f * c.g + 0.114f * c.b;
            cb = -0.168736f * c.r - 0.331264f * c.g + 0.5f * c.b;
            cr = 0.5f * c.r - 0.418688f * c.g - 0.081312f * c.b;
        }

        static Color FromYCC(float y, float cb, float cr, float a)
        {
            return new Color(
                Mathf.Clamp01(y + 1.402f * cr),
                Mathf.Clamp01(y - 0.344136f * cb - 0.714136f * cr),
                Mathf.Clamp01(y + 1.772f * cb), a);
        }
    }
}
