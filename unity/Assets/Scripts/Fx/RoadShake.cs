using UnityEngine;

namespace HalfAware
{
    /// <summary>
    /// 路面から車体へ伝わる揺れ。走った距離と路面の粗さから、目の位置に足すずれを返す。
    ///
    /// 位相を時間ではなく距離から取るのは、揺れているのが時計ではなく路面の凹凸だから。
    /// 同じ凹凸でも速く走るほど速く踏むので、速さを上げれば揺れは細かくなり、
    /// 止まれば位相も止まって車内は静かになる。時間を位相にすると、
    /// 信号待ちの車がアイドリングどころか走行中と同じだけ跳ねることになる。
    ///
    /// 周期の合わない正弦波を重ねるので、いちばん長い周期そのものは見ていて気づかない。
    /// 上下を左右より大きく振るのは、車は横に振れるより縦に跳ねるため。
    /// 前後には動かさない。目が計器盤へ寄ったり離れたりすると乗り物酔いに近くなる。
    ///
    /// MonoBehaviour の外に出してあるのは、振れ幅と粗さの掛かり方をテストで確かめたいため
    /// （<see cref="Sway"/> や <see cref="RoadRing"/> と同じ構え）
    /// </summary>
    public sealed class RoadShake
    {
        /// <summary>左右は上下の何割で振るか</summary>
        public const float SideShare = 0.45f;

        /// <summary>目の位置に足すずれ。左右と上下だけで、前後には動かさない</summary>
        public Vector3 Offset { get; private set; }

        /// <summary>
        /// travelled は走った距離でメートル。rough は路面の粗さで、1 が舗装。
        /// shift は粗さ 1 のときの振れ幅でメートル、rate は距離に掛ける位相の速さ。
        /// 粗さも振れ幅も負なら 0 として扱う。
        ///
        /// 毎フレーム渡すので、Inspector で振れ幅を変えるとその場で効く。
        /// 状態を持たないので、同じ距離を渡せば何度でも同じずれが返る
        /// </summary>
        public void Tick(float travelled, float rough, float shift, float rate)
        {
            var r = Mathf.Max(0f, rough);
            var p = travelled * Mathf.Max(0f, rate);
            // 係数の比を割り切れない数にして、重ねた波が元の周期へ戻らないようにする
            var up = Mathf.Sin(p) * 0.62f + Mathf.Sin(p * 2.3f + 1.3f) * 0.38f;
            var side = Mathf.Sin(p * 1.7f + 2.4f) * 0.7f + Mathf.Sin(p * 3.1f + 0.6f) * 0.3f;
            Offset = new Vector3(side * SideShare, up, 0f) * (Mathf.Max(0f, shift) * r);
        }
    }
}
