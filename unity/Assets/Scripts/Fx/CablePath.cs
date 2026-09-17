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
    }
}
