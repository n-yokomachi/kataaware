using UnityEngine;

namespace HalfAware
{
    /// <summary>
    /// ジャケットを着る・脱ぐ間合い。着る音（<c>JacketOn.wav</c>）を鳴らし始めてからの秒で数える。
    /// 着る・脱ぐの動きは作らず、音の終わりの少し前に、体の服と置いたジャケットを入れ替える
    /// （場面 1 は右の卓に置いたジャケットを着る、場面 3 は玄関先のコートハンガーに掛ける）
    /// </summary>
    public readonly struct JacketBeats
    {
        /// <summary>音の長さ。秒</summary>
        public readonly float Sound;
        /// <summary>音の終わりから、入れ替える時刻までさかのぼる秒</summary>
        public readonly float SwapBefore;

        public JacketBeats(float sound, float swapBefore)
        {
            Sound = Mathf.Max(0f, sound);
            SwapBefore = Mathf.Max(0f, swapBefore);
        }

        /// <summary>入れ替える時刻。音の中に収める</summary>
        public float SwapAt => Mathf.Max(0f, Sound - SwapBefore);

        /// <summary>t 秒目までに入れ替えたか</summary>
        public bool Swapped(float t) => t >= SwapAt;

        /// <summary>着るとき、t 秒目に体がジャケットを着ているか</summary>
        public bool WornPuttingOn(float t) => Swapped(t);

        /// <summary>脱ぐとき、t 秒目に体がジャケットを着ているか</summary>
        public bool WornTakingOff(float t) => !Swapped(t);
    }
}
