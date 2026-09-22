using System;
using UnityEngine;

namespace HalfAware
{
    /// <summary>
    /// 空の色を方角から出す式。組み立てのときに正距円筒の絵へ焼き、
    /// <c>Skybox/Panoramic</c> に貼って空にする（設計書 9.1 節「空と光」）。
    ///
    /// 決まりは四つ。
    /// <list type="bullet">
    /// <item>上へ行くほど濃く、地平へ下りるほど白む</item>
    /// <item>日のある方角だけ、地平の近くが暖かく明るい</item>
    /// <item>日は空に見える。影を落とす灯りの向きと同じ所に置く</item>
    /// <item>雲は地平の少し上に、横に流れる薄い帯で</item>
    /// </list>
    ///
    /// 色はどれも linear で受け取り、linear で返す。絵に焼く側が sRGB へ直す。
    /// 実行時には呼ばない。純粋な計算なので、ここに置いて試験から見る
    /// </summary>
    public static class SkyPaint
    {
        /// <summary>空の見え方。色は linear、角度は度</summary>
        [Serializable]
        public struct Look
        {
            [Tooltip("天頂の色")] public Color zenith;
            [Tooltip("仰角 30 度あたりの色")] public Color middle;
            [Tooltip("地平の色。霞の色もこれに揃える")] public Color horizon;
            [Tooltip("仰角の sin がこの値で、地平の白みが 1/e まで薄れる")] public float whiteDepth;

            [Tooltip("日の方角の地平に乗る暖かい色")] public Color warm;
            [Tooltip("暖かさの強さ。0〜1")] public float warmth;
            [Tooltip("暖かさが地平から上へ届く高さ。仰角の sin")] public float warmDepth;
            [Tooltip("方角の開き。大きいほど日の方角へ絞れる")] public float warmFocus;

            [Tooltip("日へ向かう向き。単位ベクトル")] public Vector3 sun;
            [Tooltip("日の周りの明るみの色")] public Color glow;
            [Tooltip("日のすぐ周りの明るみの強さと幅")] public float glowNear, glowNearWidth;
            [Tooltip("日の周りに広く掛かる明るみの強さと幅")] public float glowWide, glowWideWidth;
            [Tooltip("日の円の色")] public Color disc;
            [Tooltip("日の円の半径と、縁のぼかし")] public float discRadius, discSoft;

            [Tooltip("日の側の雲の色")] public Color cloudLit;
            [Tooltip("日の反対側の雲の色")] public Color cloudShade;
            [Tooltip("雲の濃さの上限。0〜1")] public float cloudCover;
            [Tooltip("雲の帯の下端と上端。仰角の sin")] public float cloudLow, cloudHigh;
            [Tooltip("雲の模様の種")] public int cloudSeed;
        }

        // ---- 絵の座標と方角 ----------------------------------------------------

        /// <summary>
        /// 正距円筒の絵の uv を方角に。<c>Skybox/Panoramic</c>（Latitude Longitude、360 度）と
        /// 同じ取り方で、u = 0.5 が +x、u = 0.25 が +z、v = 1 が真上。
        /// あちらの式は u = 0.5 - atan2(z, x) / 2π、v = 1 - acos(y) / π
        /// </summary>
        public static Vector3 Direction(float u, float v)
        {
            var lon = (0.5f - u) * 2f * Mathf.PI;
            var lat = (1f - v) * Mathf.PI;
            var s = Mathf.Sin(lat);
            return new Vector3(s * Mathf.Cos(lon), Mathf.Cos(lat), s * Mathf.Sin(lon));
        }

        /// <summary><see cref="Direction"/> の逆。u は 0〜1 に収める</summary>
        public static Vector2 Uv(Vector3 dir)
        {
            var d = dir.normalized;
            var u = 0.5f - Mathf.Atan2(d.z, d.x) / (2f * Mathf.PI);
            u -= Mathf.Floor(u);
            var v = 1f - Mathf.Acos(Mathf.Clamp(d.y, -1f, 1f)) / Mathf.PI;
            return new Vector2(u, v);
        }

        // ---- 色 ------------------------------------------------------------------

        /// <summary>その方角の空の色。linear</summary>
        public static Color At(Vector3 dir, Look look)
        {
            var d = dir.normalized;
            var up = d.y;
            var sun = look.sun.normalized;

            // 上下の移り。地平の白みは地平のすぐ上で急に薄れ、その上はゆっくり濃くなる
            var lift = Mathf.Max(up, 0f);
            var c = Color.Lerp(look.horizon, look.middle, 1f - Mathf.Exp(-lift / Mathf.Max(look.whiteDepth, 1e-4f)));
            c = Color.Lerp(c, look.zenith, Smooth(0.35f, 1f, lift));

            // 日の方角の地平だけを暖める。方角の寄りは水平に潰して測る。
            // 日そのものが高くても、暖かいのは地平の近くだけ
            var flat = new Vector2(d.x, d.z);
            var flatSun = new Vector2(sun.x, sun.z);
            var toward = 0f;
            if (flat.sqrMagnitude > 1e-8f && flatSun.sqrMagnitude > 1e-8f)
                toward = Mathf.Max(0f, Vector2.Dot(flat.normalized, flatSun.normalized));
            var warm = Mathf.Pow(toward, Mathf.Max(look.warmFocus, 1f))
                * Mathf.Exp(-lift / Mathf.Max(look.warmDepth, 1e-4f)) * look.warmth;
            c = Color.Lerp(c, look.warm, Mathf.Clamp01(warm));

            // 雲。日の側ほど明るく暖かく
            var cover = Cloud(d, look);
            if (cover > 0f)
            {
                var facing = Mathf.Clamp01(Vector3.Dot(d, sun) * 0.5f + 0.5f);
                var cloud = Color.Lerp(look.cloudShade, look.cloudLit, facing * facing);
                c = Color.Lerp(c, cloud, cover);
            }

            // 日の周りの明るみ。近くの鋭い明るみと、広く掛かる明るみの二つを重ねる
            var angle = Vector3.Angle(d, sun);
            var bloom = look.glowNear * Mathf.Exp(-angle / Mathf.Max(look.glowNearWidth, 1e-3f))
                + look.glowWide * Mathf.Exp(-angle / Mathf.Max(look.glowWideWidth, 1e-3f));
            c += look.glow * bloom;

            // 日の円
            var edge = 1f - Smooth(look.discRadius, look.discRadius + Mathf.Max(look.discSoft, 1e-3f), angle);
            c = Color.Lerp(c, look.disc, edge);

            // 地平の下は地平の色のまま少し沈める。地面と書き割りに隠れて、ほとんど見えない
            if (up < 0f) c = Color.Lerp(c, look.horizon * 0.86f, Smooth(0f, 0.12f, -up));
            c.a = 1f;
            return c;
        }

        /// <summary>
        /// その方角の雲の濃さ。0〜1。地平の少し上の帯の中だけに出る。
        ///
        /// **模様は横へ伸ばす。** 経度を円周に置いて三次元の値の雑音を拾うので、
        /// 絵の左右の継ぎ目で模様が切れない
        /// </summary>
        public static float Cloud(Vector3 dir, Look look)
        {
            if (look.cloudCover <= 0f) return 0f;
            var d = dir.normalized;
            var up = d.y;
            if (up <= look.cloudLow || up >= look.cloudHigh) return 0f;
            var span = look.cloudHigh - look.cloudLow;
            // 帯の中ほどが濃く、上下の端で消える
            var band = Smooth(look.cloudLow, look.cloudLow + span * 0.3f, up)
                * (1f - Smooth(look.cloudHigh - span * 0.45f, look.cloudHigh, up));
            var flat = new Vector2(d.x, d.z);
            if (flat.sqrMagnitude < 1e-8f) return 0f;
            flat.Normalize();
            // 円周の半径が横の細かさ、高さの倍率が縦の細かさ。縦を詰めて横に流す
            var p = new Vector3(flat.x * 7f, up * 70f, flat.y * 7f);
            var n = 0f;
            var amp = 0.5f;
            var total = 0f;
            for (var o = 0; o < 4; o++)
            {
                n += Noise(p, look.cloudSeed + o * 131) * amp;
                total += amp;
                p = new Vector3(p.x * 2.03f, p.y * 1.7f, p.z * 2.03f);
                amp *= 0.5f;
            }
            n /= total;
            return Smooth(0.50f, 0.74f, n) * band * look.cloudCover;
        }

        // ---- 道具 ----------------------------------------------------------------

        /// <summary>smoothstep。a と b の間で 0 から 1 へ寝かせて上がる</summary>
        public static float Smooth(float a, float b, float x)
        {
            if (Mathf.Approximately(a, b)) return x < a ? 0f : 1f;
            var t = Mathf.Clamp01((x - a) / (b - a));
            return t * t * (3f - 2f * t);
        }

        /// <summary>三次元の値の雑音。0〜1。格子の点ごとに決まった値を置き、間を寝かせて繋ぐ</summary>
        public static float Noise(Vector3 p, int seed)
        {
            var ix = Mathf.FloorToInt(p.x);
            var iy = Mathf.FloorToInt(p.y);
            var iz = Mathf.FloorToInt(p.z);
            var fx = p.x - ix;
            var fy = p.y - iy;
            var fz = p.z - iz;
            fx = fx * fx * (3f - 2f * fx);
            fy = fy * fy * (3f - 2f * fy);
            fz = fz * fz * (3f - 2f * fz);
            var a = Mathf.Lerp(Hash(ix, iy, iz, seed), Hash(ix + 1, iy, iz, seed), fx);
            var b = Mathf.Lerp(Hash(ix, iy + 1, iz, seed), Hash(ix + 1, iy + 1, iz, seed), fx);
            var c = Mathf.Lerp(Hash(ix, iy, iz + 1, seed), Hash(ix + 1, iy, iz + 1, seed), fx);
            var e = Mathf.Lerp(Hash(ix, iy + 1, iz + 1, seed), Hash(ix + 1, iy + 1, iz + 1, seed), fx);
            return Mathf.Lerp(Mathf.Lerp(a, b, fy), Mathf.Lerp(c, e, fy), fz);
        }

        static float Hash(int x, int y, int z, int seed)
        {
            unchecked
            {
                var h = (uint)(x * 374761393 + y * 668265263 + z * 1442695041 + seed * 1274126177);
                h = (h ^ (h >> 13)) * 1274126177u;
                h ^= h >> 16;
                return (h & 0xffffff) / 16777215f;
            }
        }
    }
}
