using System;
using System.Collections.Generic;
using UnityEngine;

namespace HalfAware.EditorTools.Rocketbox
{
    /// <summary>
    /// 場面 4 の記憶の人の塗り替え（設計メモ 5 節）。縮めた写しに、組み立てのときに描き込む。値は <see cref="RocketboxMemory"/> が持つ。
    ///
    /// - 髪: 頭のテクスチャの髪の所と、髪の房（透けの絵）を、毛筋の明暗を残したまま影の色から艶の色へ塗る（<see cref="HairTone"/>）
    /// - 上の服: 色相で選んだ所を、明暗を残したまま塗る（<see cref="TopTone"/>）
    ///
    /// 頭のテクスチャの髪の見分けは、画素の模型の上の位置（<see cref="RocketboxPaint.Surface"/>）と色で決める。
    /// 色は、頭の天辺（確かに髪の所）と頬（確かに肌の所）の色を見本にして、どちらに近いかで分ける（<see cref="Classes"/>）。
    /// 金髪は色みが肌に近く、肌らしさ（<see cref="RocketboxMobPaint.Skinness"/>）の一つの見本だけでは分けられなかった（女子 01）。
    /// 顔の真ん中（目・鼻・口・眉・髭）と、首より下（後ろで束ねた髪は除く）は塗らない
    /// </summary>
    public static class RocketboxMemoryPaint
    {
        /// <summary>髪の塗り。元の明るさを、髪の画素の明るさの 5〜95 % の幅で 0〜1 に伸ばし、影（shadow）から艶（shine）へ塗る。色は sRGB</summary>
        public sealed class HairTone
        {
            public Color shadow, shine;
            public float gamma = 1f;
        }

        /// <summary>
        /// 上の服の塗り。色相 hue（度）の ±hueWidth、彩度 satMin より上の画素のうち、模型の背の above より上（束ねた姿勢）の所を、
        /// 明暗を残して shadow から shine へ塗る。背で切るのは、靴の同じ色の飾りを塗らないため（女子 01 の靴の中はピンク）
        /// </summary>
        public sealed class TopTone
        {
            public float hue, hueWidth, satMin;
            public float above = 0.45f;
            public Color shadow, shine;
        }

        /// <summary>頭の天辺として髪の見本にする高さ（目の高さから上へ m）。主人公の頭のテクスチャの塗り（RocketboxPaint.Head）と同じ</summary>
        const float CrownFrom = 0.095f;
        /// <summary>頭の天辺として、はっきり肌の色でなければ髪にする高さ（目の高さから上へ m）の始まり</summary>
        const float CrownFade = 0.080f;
        /// <summary>顔の真ん中の幅の半分（m）の下限。この内の、眉の上端より下は塗らない（目・鼻・口・眉・髭）。眉の外の端が外にある人は、そこまで広げる</summary>
        const float FaceHalf = 0.048f;
        /// <summary>髪の房のうちまつ毛と見なす、目からの距離（m）</summary>
        const float LashReach = 0.024f;
        /// <summary>顔の横（頭の骨より前）で髪と見なす一番低い所（目の高さから下へ m）。耳たぶとこめかみの髪の下の端のあたり</summary>
        const float SideLowest = 0.06f;
        /// <summary>色に関わらず髪にする生え際（<see cref="Head"/> の hairline）を、こめかみでどれだけ下げるか（m）</summary>
        const float TempleDrop = 0.022f;
        /// <summary>耳と見なす楕円体の半径（m。左右・上下・前後）。真ん中は <see cref="Ears"/></summary>
        static readonly Vector3 EarSize = new Vector3(0.030f, 0.036f, 0.030f);

        static bool InEar(Vector3 p, Vector3 ear)
        {
            var d = p - ear;
            return Sq(d.x / EarSize.x) + Sq(d.y / EarSize.y) + Sq(d.z / EarSize.z) < 1f;
        }

        static float Sq(float x) { return x * x; }

        /// <summary>
        /// 左右の耳の真ん中。目の高さの 2 cm 下のまわりで、頭の骨の前後 3〜5 cm の内の、いちばん外へ出た所から 1.2 cm 内へ入り、
        /// 高さは目の高さの 2.8 cm 下（耳は目の高さのすぐ上から 6 cm ほど下まで）。耳の骨は無いので、頭の面の形から拾う
        /// </summary>
        static Vector3[] Ears(RocketboxPaint.Surface s, float eyeY, float headZ)
        {
            var o = new Vector3[2];
            var best = new[] { float.MinValue, float.MinValue };
            for (var i = 0; i < s.P.Length; i++)
            {
                if (!s.On[i]) continue;
                var p = s.P[i];
                if (Mathf.Abs(p.y - (eyeY - 0.02f)) > 0.025f || p.z < headZ - 0.03f || p.z > headZ + 0.05f) continue;
                for (var k = 0; k < 2; k++)
                {
                    var outward = k == 0 ? p.x : -p.x;
                    if (outward > best[k]) { best[k] = outward; o[k] = p; }
                }
            }
            o[0].x -= 0.012f;
            o[1].x += 0.012f;
            o[0].y = o[1].y = eyeY - 0.028f;
            return o;
        }

        // ---- 頭のテクスチャ ----------------------------------------------------------

        /// <summary>
        /// 頭のテクスチャの髪を塗る。px は元の色（n×n）、s は画素の位置、a は顔の骨の位置。hairLowest は髪と見なす一番低い所（目の高さから下へ m）。
        /// hairline が正なら、目の高さからその分より上は色に関わらず髪にする（生え際の細い金髪が肌の色に近く、黒く塗ると額の上に肌の色の抜けが残った。女子 01）。
        /// 戻り値は塗った色。weight に画素ごとの髪の重み（0〜1）を返す
        /// </summary>
        public static Color[] Head(Color[] px, RocketboxPaint.Surface s, RocketboxPaint.Anchors a, HairTone tone, float hairLowest, float hairline, out float[] weight, out string note)
        {
            var n = s.N;
            var eyeY = (a.eyeL.y + a.eyeR.y) * 0.5f;
            var browTop = Mathf.Max(Mathf.Max(a.browInL.y, a.browOutL.y), Mathf.Max(a.browInR.y, a.browOutR.y));
            var headZ = a.head.z;
            var cls = Classes(px, s, a, eyeY);
            var ears = Ears(s, eyeY, headZ);
            // 顔の真ん中の幅。眉の外の端より 1.2 cm 外まで（灰の眉の外の端が髪と見分けられず、白く塗られた。男大 03）
            var faceHalf = Mathf.Max(FaceHalf, Mathf.Max(Mathf.Abs(a.browOutL.x), Mathf.Abs(a.browOutR.x)) + 0.012f);
            var w = new float[px.Length];
            var front = new float[px.Length];
            for (var i = 0; i < px.Length; i++)
            {
                if (!s.On[i]) continue;
                var p = s.P[i];
                // 目の玉・口の中・歯は頭の中に入った別の島。顔の骨のそばは塗らない
                if (Near(p, a.eyeL, 0.022f) || Near(p, a.eyeR, 0.022f) || InMouth(p, a)) continue;
                // 顔の真ん中は眉の上端の 1 cm 上まで塗らない（眉・髭・唇の暗い所を髪と取り違えない）
                if (Mathf.Abs(p.x) < faceHalf && p.z > headZ && p.y < browTop + 0.010f) continue;
                // 髪と見なす一番低い所。前（頭の骨より前）は耳たぶの高さ（目の高さの SideLowest 下）まで、
                // 後ろ（頭の骨より 4 cm 後ろ）は hairLowest まで（うなじと後ろで束ねた髪）。顎と首の影を髪と取り違えない
                var back = RocketboxPaint.Smooth(headZ + 0.01f, headZ - 0.04f, p.z);
                front[i] = 1f - back;
                var lowest = eyeY - Mathf.Lerp(SideLowest, hairLowest, back);
                var above = RocketboxPaint.Smooth(lowest - 0.02f, lowest + 0.02f, p.y);
                if (above <= 0f) continue;
                var hairness = cls.Hair(px[i]);
                // 頭の後ろの丸みより後ろ（頭の骨より 9〜11 cm 後ろ）は、束ねた髪か後ろ頭の髪しか無い。明るさを見ず色みだけで分ける
                // （束ねた金髪の明るい毛筋が肌の明るさに近く、黒く塗った中に橙の筋で残った。髪留めは色みで肌の側に分かれる。女子 01）
                var beyond = RocketboxPaint.Smooth(headZ - 0.09f, headZ - 0.11f, p.z);
                if (beyond > 0f)
                {
                    // 黄から銅の色み、暗い色、色みの無い色は髪。はっきり桃の色み（髪留めのシュシュ）は髪にしない
                    float yellow, pink;
                    Tint(px[i], out yellow, out pink);
                    hairness = Mathf.Lerp(hairness, Mathf.Max(hairness, Mathf.Max(cls.HairChroma(px[i]), yellow)) * (1f - pink), beyond);
                }
                // 耳の窪みの影は暗く、明るさでは髪に寄る（男大 03 の灰の髪は色みも肌に近い）。耳の中は、色みでも髪の所だけ
                if (InEar(p, ears[0]) || InEar(p, ears[1]))
                    hairness = Mathf.Min(RocketboxPaint.Smooth(0.90f, 0.99f, hairness), RocketboxPaint.Smooth(0.70f, 0.95f, cls.HairChroma(px[i])));
                // 頭の天辺は、はっきり肌の色でなければ髪（明るい毛筋も髪にする）。額が高い人の額の上の肌は塗らない
                var crown = RocketboxPaint.Smooth(eyeY + CrownFade, eyeY + CrownFrom, p.y) * RocketboxPaint.Smooth(0.05f, 0.30f, hairness);
                if (hairline > 0f)
                {
                    // 生え際は額の真ん中で高く、こめかみ（真ん中から 5.5 cm より外）で TempleDrop だけ低い
                    var line = eyeY + hairline - TempleDrop * RocketboxPaint.Smooth(0.02f, 0.055f, Mathf.Abs(p.x));
                    crown = Mathf.Max(crown, RocketboxPaint.Smooth(line - 0.008f, line + 0.008f, p.y));
                }
                w[i] = Mathf.Max(crown, hairness) * NotAccent(px[i]) * above;
            }
            // 生え際の細い毛の一本ずつ（幅 2 画素まで）は髪にしない（削ってから太らせる）。金髪を黒く塗ると、額に黒い点々として散った（女子 01）。
            // そのあと毛筋の縁の一画素のぎざぎざをならす
            w = RocketboxPaint.MinMax(RocketboxPaint.MinMax(w, n, 1, false), n, 1, true);
            w = RocketboxPaint.Blur(w, s.On, n, 1);
            weight = w;
            var o = Matte(px, w, front, s, tone, out note);
            // UV の島の外の詰め物は元の髪の色のままなので、島の縁の色で埋め直す（ミップマップで島の縁へ滲むため）
            Bleed(o, s.On, n);
            note = string.Format("髪と見なした画素 {0}（重み 0.5 以上）。{1}", Count(w, 0.5f), note);
            return o;
        }

        /// <summary>髪と肌の境で、まわりの髪と肌の明るさを測る半径（画素）</summary>
        const int EdgeReach = 4;

        /// <summary>
        /// 頭のテクスチャの髪を塗る。確かに髪の画素（重みが大きく、髪の艶より暗い）は明るさで影から艶へ塗り（<see cref="Ramp"/> と同じ）、
        /// 髪と肌の境の画素（生え際の細い毛の下に肌が透ける所）は、髪と肌の混ざりとして、髪の分の色だけを塗り替える:
        /// まわりの髪と肌の明るさから髪の割合を出し、まわりの髪の元の色から塗った色への差を、その割合だけ足す。
        /// 境の画素を自分の明るさで塗ると、肌の明るさが艶の明るさに写って、生え際が白い縁取りと、その内の塗り残しの暗い筋になった。
        /// 艶より明るい画素を境として扱うのは、顔の側（front、頭の骨より前で 1）の生え際だけ。後ろで束ねた髪の明るい毛筋は艶の色で塗り切る
        /// （髪留めのそばの金髪の毛筋が、黒い髪の中に橙の筋で残った。女子 01）
        /// </summary>
        static Color[] Matte(Color[] px, float[] w, float[] front, RocketboxPaint.Surface s, HairTone tone, out string note)
        {
            var n = s.N;
            var o = (Color[])px.Clone();
            float lo, hi;
            if (!Range(px, w, out lo, out hi))
            {
                note = "塗る画素が無い";
                return o;
            }
            System.Func<float, Color> ink = l => Color.Lerp(tone.shadow, tone.shine, Mathf.Pow(Mathf.Clamp01((l - lo) / (hi - lo)), tone.gamma));
            var len = px.Length;
            var lum = new float[len];
            var hs = new float[len];
            var ss = new float[len];
            var hL = new float[len];
            var hR = new float[len];
            var hG = new float[len];
            var hB = new float[len];
            var sL = new float[len];
            for (var i = 0; i < len; i++)
            {
                lum[i] = RocketboxPaint.Lum(px[i]);
                hs[i] = w[i] >= 0.6f && lum[i] <= hi ? 1f : 0f;
                ss[i] = s.On[i] && w[i] <= 0.02f ? 1f : 0f;
                hL[i] = lum[i] * hs[i];
                hR[i] = px[i].r * hs[i];
                hG[i] = px[i].g * hs[i];
                hB[i] = px[i].b * hs[i];
                sL[i] = lum[i] * ss[i];
            }
            var hsD = RocketboxPaint.Blur(hs, s.On, n, EdgeReach);
            var ssD = RocketboxPaint.Blur(ss, s.On, n, EdgeReach);
            hL = RocketboxPaint.Blur(hL, s.On, n, EdgeReach);
            hR = RocketboxPaint.Blur(hR, s.On, n, EdgeReach);
            hG = RocketboxPaint.Blur(hG, s.On, n, EdgeReach);
            hB = RocketboxPaint.Blur(hB, s.On, n, EdgeReach);
            sL = RocketboxPaint.Blur(sL, s.On, n, EdgeReach);
            var edges = 0;
            for (var i = 0; i < len; i++)
            {
                if (w[i] <= 0f) continue;
                var pure = Color.Lerp(px[i], ink(lum[i]), Mathf.Clamp01(w[i]));
                // 艶より明るい画素は、まわりに肌がある所（生え際）だけ境として扱う。髪の中の明るい毛筋は、艶の色で塗り切る
                var skinNear = RocketboxPaint.Smooth(0.05f, 0.30f, ssD[i]) * front[i];
                var core = RocketboxPaint.Smooth(0.6f, 0.95f, w[i]) * (1f - RocketboxPaint.Smooth(hi, hi + 0.10f, lum[i]) * skinNear);
                if (core >= 0.999f || hsD[i] < 1e-3f)
                {
                    o[i] = new Color(pure.r, pure.g, pure.b, px[i].a);
                    continue;
                }
                edges++;
                var hairL = hL[i] / hsD[i];
                var hairC = new Color(hR[i] / hsD[i], hG[i] / hsD[i], hB[i] / hsD[i]);
                // 髪の割合: 肌の明るさから髪の明るさへ、どこまで寄っているか。まわりに肌が無ければ重みのまま
                var share = Mathf.Clamp01(w[i]);
                if (ssD[i] > 1e-3f)
                {
                    var skinL = sL[i] / ssD[i];
                    share = Mathf.Clamp01((skinL - lum[i]) / Mathf.Max(0.02f, skinL - hairL)) * RocketboxPaint.Smooth(0f, 0.5f, w[i]);
                }
                var shift = ink(hairL) - hairC;
                var edge = new Color(Mathf.Clamp01(px[i].r + shift.r * share), Mathf.Clamp01(px[i].g + shift.g * share), Mathf.Clamp01(px[i].b + shift.b * share), px[i].a);
                o[i] = Color.Lerp(edge, pure, core);
                o[i].a = px[i].a;
            }
            note = string.Format(System.Globalization.CultureInfo.InvariantCulture, "元の明るさ {0:0.00}〜{1:0.00} を塗った。髪と肌の境として塗った画素 {2}", lo, hi, edges);
            return o;
        }

        /// <summary>島の外（on が偽）の画素を、いちばん近い島の画素の色で埋める（縦横の隣を順に広げる）</summary>
        static void Bleed(Color[] px, bool[] on, int n)
        {
            var done = (bool[])on.Clone();
            var front = new Queue<int>();
            for (var i = 0; i < px.Length; i++) if (done[i]) front.Enqueue(i);
            while (front.Count > 0)
            {
                var i = front.Dequeue();
                int x = i % n, y = i / n;
                foreach (var j in new[] { x > 0 ? i - 1 : -1, x < n - 1 ? i + 1 : -1, y > 0 ? i - n : -1, y < n - 1 ? i + n : -1 })
                {
                    if (j < 0 || done[j]) continue;
                    done[j] = true;
                    px[j] = px[i];
                    front.Enqueue(j);
                }
            }
        }

        /// <summary>
        /// 髪の房の透けの絵を塗る。まつ毛（目から <see cref="LashReach"/> 以内の房）と、髪留め（色の鮮やかな所）は元のまま。
        /// 透けた地の色は塗った髪の平均にする（縁に元の地の色が滲まないように）。s は髪の房の三角の画素の位置（大きさは絵と同じ）
        /// </summary>
        public static Color[] Strands(Color[] px, int w, int h, RocketboxPaint.Surface s, RocketboxPaint.Anchors a, HairTone tone, out string note)
        {
            var wt = new float[px.Length];
            var lashes = 0;
            for (var i = 0; i < px.Length; i++)
            {
                if (px[i].a < 0.02f) continue;
                var x = i % w;
                var y = i / w;
                var k = SurfaceIndex(s, w, h, x, y);
                if (k >= 0 && s.On[k] && (Near(s.P[k], a.eyeL, LashReach) || Near(s.P[k], a.eyeR, LashReach))) { lashes++; continue; }
                wt[i] = NotAccent(px[i]);
            }
            string rampNote;
            var o = Ramp(px, wt, tone, out rampNote);
            // 透けた地（α がほぼ 0）は、塗った髪の平均の色に
            var sum = Vector3.zero;
            var cnt = 0f;
            for (var i = 0; i < o.Length; i++)
            {
                if (wt[i] <= 0f || px[i].a < 0.5f) continue;
                sum += new Vector3(o[i].r, o[i].g, o[i].b) * wt[i];
                cnt += wt[i];
            }
            if (cnt > 0f)
            {
                var mean = sum / cnt;
                for (var i = 0; i < o.Length; i++)
                    if (px[i].a < 0.02f) o[i] = new Color(mean.x, mean.y, mean.z, px[i].a);
            }
            note = string.Format("髪の房: 塗った画素 {0}、まつ毛として残した画素 {1}。{2}", Count(wt, 0.5f), lashes, rampNote);
            return o;
        }

        /// <summary>透けの絵の画素 (x, y)（w×h）が載る、位置の地図（正方形 N×N）の画素。絵が横長（男大 03 の 512×256）でも UV で合わせる</summary>
        static int SurfaceIndex(RocketboxPaint.Surface s, int w, int h, int x, int y)
        {
            var sx = Mathf.Clamp(Mathf.FloorToInt((x + 0.5f) / w * s.N), 0, s.N - 1);
            var sy = Mathf.Clamp(Mathf.FloorToInt((y + 0.5f) / h * s.N), 0, s.N - 1);
            return sy * s.N + sx;
        }

        // ---- 体のテクスチャ ----------------------------------------------------------

        /// <summary>上の服を塗る。px は体のテクスチャの元の色、s は画素の位置（大きさは絵と同じ）、tall は模型の背（束ねた姿勢、m）</summary>
        public static Color[] Body(Color[] px, RocketboxPaint.Surface s, float tall, TopTone top, out string note)
        {
            var w = new float[px.Length];
            var from = tall * top.above;
            for (var i = 0; i < px.Length; i++)
            {
                if (!s.On[i] || s.P[i].y < from) continue;
                float h, sat, v;
                Color.RGBToHSV(px[i], out h, out sat, out v);
                var d = Mathf.Abs(Mathf.DeltaAngle(h * 360f, top.hue));
                w[i] = RocketboxPaint.Smooth(top.hueWidth, top.hueWidth * 0.7f, d) * RocketboxPaint.Smooth(top.satMin - 0.08f, top.satMin, sat);
            }
            var tone = new HairTone { shadow = top.shadow, shine = top.shine, gamma = 1f };
            string rampNote;
            var o = Ramp(px, w, tone, out rampNote);
            // 島の外の詰め物は元の色のままなので、島の縁の色で埋め直す
            Bleed(o, s.On, s.N);
            note = string.Format("上の服として塗った画素 {0}（模型の背の {1:0.00} m より上）。{2}", Count(w, 0.5f), from, rampNote);
            return o;
        }

        // ---- 道具 -------------------------------------------------------------------

        /// <summary>
        /// 重み w の画素を塗る。明るさを、重み 0.5 以上の画素の明るさの 5〜95 % の幅で 0〜1 に伸ばし、影から艶へ。α は元のまま
        /// </summary>
        static Color[] Ramp(Color[] px, float[] w, HairTone tone, out string note)
        {
            var o = (Color[])px.Clone();
            float lo, hi;
            if (!Range(px, w, out lo, out hi))
            {
                note = "塗る画素が無い";
                return o;
            }
            for (var i = 0; i < px.Length; i++)
            {
                if (w[i] <= 0f) continue;
                var t = Mathf.Pow(Mathf.Clamp01((RocketboxPaint.Lum(px[i]) - lo) / (hi - lo)), tone.gamma);
                var ink = Color.Lerp(tone.shadow, tone.shine, t);
                o[i] = Color.Lerp(px[i], ink, Mathf.Clamp01(w[i]));
                o[i].a = px[i].a;
            }
            note = string.Format(System.Globalization.CultureInfo.InvariantCulture, "元の明るさ {0:0.00}〜{1:0.00} を塗った", lo, hi);
            return o;
        }

        /// <summary>重み 0.5 以上（透けの絵では α 0.5 以上も）の画素の明るさの 5 % と 95 % の所。塗る画素が無ければ偽</summary>
        static bool Range(Color[] px, float[] w, out float lo, out float hi)
        {
            var lums = new List<float>();
            for (var i = 0; i < px.Length; i++) if (w[i] >= 0.5f && px[i].a >= 0.5f) lums.Add(RocketboxPaint.Lum(px[i]));
            lo = 0f;
            hi = 1f;
            if (lums.Count == 0) return false;
            lums.Sort();
            lo = lums[Mathf.Clamp(Mathf.RoundToInt(lums.Count * 0.05f), 0, lums.Count - 1)];
            hi = lums[Mathf.Clamp(Mathf.RoundToInt(lums.Count * 0.95f), 0, lums.Count - 1)];
            if (hi - lo < 0.02f) hi = lo + 0.02f;
            return true;
        }

        /// <summary>
        /// 髪留めやリボンなど、鮮やかな色の小物（緑〜青〜紫の色相で彩度 0.25 より上）なら 0。髪（茶・金・赤・灰・黒）なら 1。
        /// 女大 09 のまとめ髪の青緑の髪留めを塗らないため
        /// </summary>
        static float NotAccent(Color c)
        {
            float h, sat, v;
            Color.RGBToHSV(c, out h, out sat, out v);
            var deg = h * 360f;
            var cool = deg > 75f && deg < 345f ? 1f : 0f;
            return 1f - cool * RocketboxPaint.Smooth(0.18f, 0.28f, sat);
        }

        static bool Near(Vector3 p, Vector3 q, float r) { return (p - q).sqrMagnitude < r * r; }

        /// <summary>
        /// 色みの見分け（Lab）。yellow は黄から銅の色み（b が a より十分大きい）か、暗い色か、色みの無い色（髪の色）。
        /// pink は色みのはっきりした明るい桃（a が b より大きい。女子 01 の髪留め）
        /// </summary>
        static void Tint(Color c, out float yellow, out float pink)
        {
            var lab = LabOf(c);
            var lean = lab.z - (0.9f * lab.y + 5f);
            var chroma = Mathf.Sqrt(lab.y * lab.y + lab.z * lab.z);
            // 桃は明るい色だけ（赤みのある暗い茶の髪は桃にしない。女大 02）
            pink = RocketboxPaint.Smooth(0f, 6f, -lean) * RocketboxPaint.Smooth(5f, 9f, chroma) * RocketboxPaint.Smooth(40f, 55f, lab.x);
            yellow = Mathf.Max(RocketboxPaint.Smooth(-2f, 4f, lean), Mathf.Max(RocketboxPaint.Smooth(45f, 35f, lab.x), RocketboxPaint.Smooth(9f, 5f, chroma)));
        }

        /// <summary>口の中（唇の間の奥）。上と下の唇の骨の真ん中から 2.5 cm 以内で、唇より奥</summary>
        static bool InMouth(Vector3 p, RocketboxPaint.Anchors a)
        {
            var mid = (a.upperLip + a.lowerLip) * 0.5f;
            return (p - mid).sqrMagnitude < 0.025f * 0.025f && p.z < mid.z - 0.004f;
        }

        static int Count(float[] w, float min)
        {
            var c = 0;
            foreach (var x in w) if (x >= min) c++;
            return c;
        }

        /// <summary>
        /// 髪と肌の色の見本。髪は頭の天辺（目の高さから <see cref="CrownFrom"/> より上）、肌は頬の骨のまわり 2 cm と額の真ん中（眉の上 1.5〜3 cm）。
        /// Lab の各軸の平均を持ち、二つの見本の広がりを均した物差しで、画素がどちらの平均に近いかで髪らしさを出す。
        /// 広がりを見本ごとに持つと、頭の天辺の揃った髪の見本が狭すぎて、後ろ頭の影になった髪まで肌に分けられた（女子 01）
        /// </summary>
        sealed class Colours
        {
            Vector3 hairMean, skinMean, spread;

            public static Colours Of(List<Vector3> hair, List<Vector3> skin)
            {
                var c = new Colours();
                Vector3 hv, sv;
                Stat(hair, out c.hairMean, out hv);
                Stat(skin, out c.skinMean, out sv);
                // 広がりの下限（明るさ 5、色み 2 の標準偏差）
                c.spread = Vector3.Max((hv + sv) * 0.5f, new Vector3(25f, 4f, 4f));
                return c;
            }

            static void Stat(List<Vector3> xs, out Vector3 mean, out Vector3 var)
            {
                mean = Vector3.zero;
                foreach (var x in xs) mean += x;
                mean /= Mathf.Max(1, xs.Count);
                var = Vector3.zero;
                foreach (var x in xs) { var d = x - mean; var += Vector3.Scale(d, d); }
                var /= Mathf.Max(1, xs.Count);
            }

            /// <summary>髪らしさ（0〜1）。均した広がりで測った、二つの平均までの距離の差から</summary>
            public float Hair(Color c)
            {
                var lab = LabOf(c);
                var llr = -0.5f * (Maha(lab, hairMean, spread) - Maha(lab, skinMean, spread));
                return 1f / (1f + Mathf.Exp(-Mathf.Clamp(llr, -30f, 30f)));
            }

            /// <summary>色みだけ（明るさを見ない）で測った髪らしさ。影になった肌は明るさで髪に寄るが、色みは肌のまま</summary>
            public float HairChroma(Color c)
            {
                var lab = LabOf(c);
                lab.x = 0f;
                var flat = new Vector3(1f, spread.y, spread.z);
                var llr = -0.5f * (Maha(lab, new Vector3(0f, hairMean.y, hairMean.z), flat) - Maha(lab, new Vector3(0f, skinMean.y, skinMean.z), flat));
                return 1f / (1f + Mathf.Exp(-Mathf.Clamp(llr, -30f, 30f)));
            }

            static float Maha(Vector3 x, Vector3 m, Vector3 v)
            {
                var d = x - m;
                return d.x * d.x / v.x + d.y * d.y / v.y + d.z * d.z / v.z;
            }
        }

        static Colours Classes(Color[] px, RocketboxPaint.Surface s, RocketboxPaint.Anchors a, float eyeY)
        {
            var browTop = Mathf.Max(Mathf.Max(a.browInL.y, a.browOutL.y), Mathf.Max(a.browInR.y, a.browOutR.y));
            var hair = new List<Vector3>();
            var skin = new List<Vector3>();
            for (var i = 0; i < px.Length; i++)
            {
                if (!s.On[i]) continue;
                var p = s.P[i];
                if (p.y > eyeY + CrownFrom) { if (NotAccent(px[i]) > 0.5f) hair.Add(LabOf(px[i])); continue; }
                var cheek = Near(p, a.cheekL, 0.02f) || Near(p, a.cheekR, 0.02f);
                var brow = Mathf.Abs(p.x) < 0.02f && p.z > a.head.z + 0.03f && p.y > browTop + 0.015f && p.y < browTop + 0.030f;
                if (cheek || brow) skin.Add(LabOf(px[i]));
            }
            if (hair.Count == 0 || skin.Count == 0) throw new InvalidOperationException("髪か肌の見本の画素が無い（髪 " + hair.Count + "、肌 " + skin.Count + "）");
            // 額の高い人は、頭の天辺の見本に額の上の肌が混じる。一度分けてから、肌に分けられた画素を髪の見本から除いて分け直す
            var first = Colours.Of(hair, skin);
            var kept = new List<Vector3>();
            for (var i = 0; i < px.Length; i++)
                if (s.On[i] && s.P[i].y > eyeY + CrownFrom && NotAccent(px[i]) > 0.5f && first.Hair(px[i]) >= 0.5f) kept.Add(LabOf(px[i]));
            return kept.Count > 100 ? Colours.Of(kept, skin) : first;
        }

        /// <summary>sRGB の色を Lab へ</summary>
        static Vector3 LabOf(Color c)
        {
            var l = c.linear;
            var X = 0.4124f * l.r + 0.3576f * l.g + 0.1805f * l.b;
            var Y = 0.2126f * l.r + 0.7152f * l.g + 0.0722f * l.b;
            var Z = 0.0193f * l.r + 0.1192f * l.g + 0.9505f * l.b;
            Func<float, float> f = t => t > 0.008856f ? Mathf.Pow(t, 1f / 3f) : 7.787f * t + 16f / 116f;
            float fx = f(X / 0.95047f), fy = f(Y), fz = f(Z / 1.08883f);
            return new Vector3(116f * fy - 16f, 500f * (fx - fy), 200f * (fy - fz));
        }
    }
}
