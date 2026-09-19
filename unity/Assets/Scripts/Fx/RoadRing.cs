using UnityEngine;

namespace HalfAware
{
    /// <summary>
    /// 道のタイルを環にして流す計算。
    ///
    /// 車は原点に置いたままで、道の方を手前（-z）へ送る。後ろへ抜けたタイルは
    /// 環の一番先へ回すので、何分走っても座標は原点の周りに留まる。
    /// 実寸で何 km も敷くと座標が大きくなり、揺れや影が粗くなるため。
    ///
    /// MonoBehaviour の外に出してあるのは、隙間や重なりをテストで確かめたいため
    /// </summary>
    public static class RoadRing
    {
        /// <summary>z を [behind, behind + span) に畳む</summary>
        public static float Wrap(float z, float span, float behind)
        {
            if (span <= 0f) return behind;
            var t = (z - behind) % span;
            if (t < 0f) t += span;
            return behind + t;
        }

        /// <summary>
        /// i 枚目のタイルの z。travelled は走った距離。
        /// behind は車の後ろのどこまでタイルを残すか（負の値）
        /// </summary>
        public static float Slot(int i, int n, float length, float travelled, float behind)
        {
            if (n <= 0 || length <= 0f) return behind;
            return Wrap(i * length - travelled, n * length, behind);
        }

        /// <summary>
        /// 前方 ahead まで途切れず敷くのに要る枚数。
        /// 環の長さが前後の見える範囲を覆えばよい
        /// </summary>
        public static int Needed(float ahead, float length, float behind)
        {
            if (length <= 0f) return 0;
            return Mathf.Max(2, Mathf.CeilToInt((ahead - behind) / length));
        }
    }
}
