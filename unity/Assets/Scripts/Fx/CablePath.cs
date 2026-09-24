using UnityEngine;

namespace HalfAware
{
    /// <summary>
    /// 両端を結ぶケーブルの道筋。端が近づいたぶんだけ下へたるむ。
    /// 張力の計算はしない。見た目が保てば足りる
    /// </summary>
    public static class CablePath
    {
        /// <summary>たるみの深さは、余った長さのこれだけ</summary>
        public const float SagShare = 0.42f;

        /// <summary>
        /// a から b へ into.Length 点を刻む。length はケーブルの長さで、
        /// 端の間が詰まるほど深く垂れる。張りきっていればまっすぐ
        /// </summary>
        public static void Sag(Vector3 a, Vector3 b, float length, Vector3[] into)
        {
            if (into == null || into.Length == 0) return;
            if (into.Length == 1) { into[0] = a; return; }
            var slack = Mathf.Max(0f, length - Vector3.Distance(a, b));
            var depth = slack * SagShare;
            var last = into.Length - 1;
            for (var i = 0; i <= last; i++)
            {
                var t = (float)i / last;
                var p = Vector3.Lerp(a, b, t);
                p.y -= depth * 4f * t * (1f - t);
                into[i] = p;
            }
        }

        /// <summary>
        /// 両端から、それぞれの向き（aOut・bOut）へ stiffness だけまっすぐ出してから曲がる道筋（三次のベジェ）に、
        /// 余った長さのぶんの垂れを重ねる。ジャックの尻からケーブルが軸に沿って出る形になり、
        /// 刺さっている手首の皮膚や、持っている手の指へ寄っていかない。stiffness が 0 なら <see cref="Sag"/> と同じ
        /// </summary>
        public static void Curve(Vector3 a, Vector3 aOut, Vector3 b, Vector3 bOut, float length, float stiffness, Vector3[] into)
        {
            if (into == null || into.Length == 0) return;
            if (stiffness <= 0f || into.Length < 3) { Sag(a, b, length, into); return; }
            var run = Bend(a, aOut, b, bOut, stiffness, into);
            Droop(into, Mathf.Max(0f, length - run) * SagShare);
        }

        /// <summary>
        /// 巻き取り式のケーブル。長さは端の間の道筋に合わせて出し入れされ、いつも slack だけ余って垂れる。
        /// 刺さっているときは短く、抜いて前へ出すと伸びる
        /// </summary>
        public static void Reel(Vector3 a, Vector3 aOut, Vector3 b, Vector3 bOut, float slack, float stiffness, Vector3[] into)
        {
            if (into == null || into.Length == 0) return;
            if (into.Length < 3) { Sag(a, b, Vector3.Distance(a, b) + slack, into); return; }
            Bend(a, aOut, b, bOut, Mathf.Max(0f, stiffness), into);
            Droop(into, Mathf.Max(0f, slack) * SagShare);
        }

        /// <summary>両端から向きへ stiffness だけ出るベジェを刻み、その長さを返す</summary>
        static float Bend(Vector3 a, Vector3 aOut, Vector3 b, Vector3 bOut, float stiffness, Vector3[] into)
        {
            var p1 = a + aOut.normalized * stiffness;
            var p2 = b + bOut.normalized * stiffness;
            var last = into.Length - 1;
            var run = 0f;
            var prev = a;
            for (var i = 0; i <= last; i++)
            {
                var p = Bezier(a, p1, p2, b, (float)i / last);
                into[i] = p;
                run += Vector3.Distance(prev, p);
                prev = p;
            }
            return run;
        }

        /// <summary>両端を残して、真ん中ほど深く depth だけ下げる</summary>
        static void Droop(Vector3[] into, float depth)
        {
            var last = into.Length - 1;
            for (var i = 1; i < last; i++)
            {
                var t = (float)i / last;
                into[i].y -= depth * 4f * t * (1f - t);
            }
        }

        static Vector3 Bezier(Vector3 a, Vector3 b, Vector3 c, Vector3 d, float t)
        {
            var u = 1f - t;
            return u * u * u * a + 3f * u * u * t * b + 3f * u * t * t * c + t * t * t * d;
        }
    }
}
