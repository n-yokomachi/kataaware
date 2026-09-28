using System.Collections.Generic;
using UnityEngine;

namespace HalfAware
{
    /// <summary>
    /// エンディングの目の向きと見回しの限り（2026-09-28 オーナー「カメラ回転できるようにしよう。左側は片割れの姿が見えないくらいの位置までに制限で」）。
    /// **向きと限りの値はここの一か所。** 組み立て（BuildEnding）は、このクラスの既定の値をそのまま場面へ書く。
    ///
    /// 角度は度。左右（x）は車の前（+z）が 0 で右回り、上下（y）は下が正（<see cref="PlayerController.Pitch"/> と同じ）。
    /// <list type="bullet">
    /// <item>右・上・下は角度の限り。限りの手前 <see cref="soft"/> 度から重くなり、限りで止まる（ばねのように戻さない）</item>
    /// <item>左は、助手席の片割れの体のどこも画面に入らない所まで（<see cref="Clearance"/>）。片割れの体の点が画面の縁の外に
    ///   <see cref="margin"/> 度より近づく向きへは回らない。左へ回して下を向くと膝が下の縁から入るので、その組み合わせも止まる。
    ///   画面の縦横比で横の画角が変わるので、角度では持たずに、その時のカメラの画角で調べる</item>
    /// <item>左にも角度の限り（<see cref="left"/>）を置く。片割れの点が読めなかった時の止め</item>
    /// </list>
    /// </summary>
    [System.Serializable]
    public sealed class EndingView
    {
        [Tooltip("既定の向き（前を見ている向き）。明けた時と、片割れの体へ向いて戻った時にこの向きになる。" +
            "右の柱が画面の横の 6 割ほどに来て、その右が運転席の窓の外")]
        public Vector2 front = new Vector2(17f, 6f);
        [Tooltip("右の限り（左右）。運転席の窓の外を見渡せる所まで")]
        public float right = 76f;
        [Tooltip("左の限り（左右）。片割れの体で止まる所より左に置く、角度の止め")]
        public float left = -40f;
        [Tooltip("上の限り（上下。負が上）。屋根の内張りで画面の上半分が埋まる手前")]
        public float up = -14f;
        [Tooltip("下の限り（上下）。計器盤で画面の下半分が埋まる手前")]
        public float down = 30f;
        [Tooltip("限りの手前で重くなり始める幅。度")]
        public float soft = 10f;
        [Tooltip("限りの際での、回る速さの割合（0〜1）")]
        public float heavy = 0.25f;
        [Tooltip("片割れの体の点と画面の縁のあいだに残す角度。度。車体の揺れで目が動く分を見込む")]
        public float margin = 3f;

        /// <summary>組み立てが場面へ書く既定の値</summary>
        public static EndingView Default { get { return new EndingView(); } }

        /// <summary>角度の限り（右・上・下・左の止め）の内へ収める</summary>
        public Vector2 Clamp(Vector2 a)
        {
            return new Vector2(Mathf.Clamp(a.x, left, right), Mathf.Clamp(a.y, up, down));
        }

        /// <summary>
        /// at から turn だけ回した向き。角度の限りの手前 <see cref="soft"/> 度から重くし、限りで止める。
        /// 片割れの点（points、eye と同じ座標）があれば、片割れが画面の縁に近づく向きへの回りも重くして、
        /// 縁から <see cref="margin"/> 度の所で止める。aspect はカメラの横÷縦、fov は縦の画角
        /// </summary>
        public Vector2 Step(Vector2 at, Vector2 turn, Vector3 eye, IList<Vector3> points, float aspect, float fov)
        {
            var next = new Vector2(Axis(at.x, turn.x, left, right), Axis(at.y, turn.y, up, down));
            if (points == null || points.Count == 0) return next;
            var before = Clearance(at, eye, points, aspect, fov);
            var after = Clearance(next, eye, points, aspect, fov);
            if (after >= before) return next;
            // 片割れへ近づく向き。残りの角度で重くし、縁で止める
            var k = Mathf.Lerp(heavy, 1f, Mathf.Clamp01((before - margin) / soft));
            next = at + (next - at) * k;
            if (Clearance(next, eye, points, aspect, fov) >= margin) return next;
            if (before < margin) return at;
            float lo = 0f, hi = 1f;
            for (var i = 0; i < 8; i++)
            {
                var mid = (lo + hi) * 0.5f;
                if (Clearance(at + (next - at) * mid, eye, points, aspect, fov) >= margin) lo = mid;
                else hi = mid;
            }
            return at + (next - at) * lo;
        }

        /// <summary>一つの軸を、限りの手前で重くしながら回す</summary>
        float Axis(float at, float turn, float lo, float hi)
        {
            if (turn > 0f)
            {
                var room = hi - at;
                turn *= Mathf.Lerp(heavy, 1f, Mathf.Clamp01(room / soft));
                return Mathf.Min(at + turn, Mathf.Max(hi, at));
            }
            if (turn < 0f)
            {
                var room = at - lo;
                turn *= Mathf.Lerp(heavy, 1f, Mathf.Clamp01(room / soft));
                return Mathf.Max(at + turn, Mathf.Min(lo, at));
            }
            return at;
        }

        /// <summary>
        /// 向き angles のカメラ（目は eye、横÷縦 aspect、縦の画角 fov）で、points のどれかが画面の縁の外に何度残っているか。
        /// 負なら画面の内に入っている。いちばん縁に近い点の値
        /// </summary>
        public static float Clearance(Vector2 angles, Vector3 eye, IList<Vector3> points, float aspect, float fov)
        {
            var back = Quaternion.Inverse(Quaternion.Euler(angles.y, angles.x, 0f));
            var halfV = fov * 0.5f;
            var halfH = Mathf.Atan(Mathf.Tan(halfV * Mathf.Deg2Rad) * aspect) * Mathf.Rad2Deg;
            var best = 180f;
            for (var i = 0; i < points.Count; i++)
            {
                var l = back * (points[i] - eye);
                var ax = Mathf.Atan2(Mathf.Abs(l.x), l.z) * Mathf.Rad2Deg - halfH;
                var ay = Mathf.Atan2(Mathf.Abs(l.y), l.z) * Mathf.Rad2Deg - halfV;
                var c = Mathf.Max(ax, ay);
                if (c < best) best = c;
            }
            return best;
        }
    }
}
