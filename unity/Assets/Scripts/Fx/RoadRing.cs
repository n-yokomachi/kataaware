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
    /// 返すのは mesh の原点の z なので、タイルの mesh は z 方向にちょうど length だけ伸び、
    /// 原点が手前（-z）の端にあることが要る。Needed はその置き方を前提に枚数を数えている。
    /// 中央に置くと前の端が length の半分だけ手前に寄り、奥の端に置くと丸ごと一枚ぶん足りず、
    /// タイル 1 枚ぶんの周期で地平に穴が空く。
    ///
    /// 毎回 travelled から出し直すのは、1 枚ずつ動かすとタイルごとに誤差が溜まって
    /// 隣との間に隙間が開くため。共通の数から出せば、誤差が出ても道全体が同じだけずれるので
    /// 継ぎ目は割れない。
    ///
    /// なお継ぎ目が割れないのは length が 2 の冪と相性の良い数のときで、
    /// 20 / 環 180 / 後ろ 30 では計算に丸めが一切入らない。
    /// 17.3 のような半端な数にすると 10 分ほどで 1 mm の隙間が開く
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
        /// 前方 ahead まで途切れず敷くのに要る枚数。behind は負の値で渡す。
        ///
        /// 前の端は必ず ahead まで届く。後ろは環がずれるぶん最大でタイル 1 枚ぶん縮むが、
        /// 運転席から後ろは見えないので構わない
        /// </summary>
        public static int Needed(float ahead, float length, float behind)
        {
            if (length <= 0f) return 0;
            return Mathf.Max(2, Mathf.CeilToInt((ahead - behind) / length));
        }
    }
}
