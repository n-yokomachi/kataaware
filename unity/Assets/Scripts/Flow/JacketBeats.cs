using UnityEngine;

namespace HalfAware
{
    /// <summary>
    /// ジャケットを着る・脱ぐ間合い。着る音（<c>JacketOn.wav</c>）を鳴らし始めてからの秒で数える。
    /// 着る・脱ぐの動きは作らず、音の終わりの少し前に、体の服と肘掛けのジャケットを入れ替える。
    /// 場面 3 では、脱いでから（音が鳴り終わってから）椅子に腰を下ろす
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

        /// <summary>
        /// 脱いでから腰を下ろすとき、t 秒目の下ろし具合（0 が立った所、1 が座った所）。
        /// 音が鳴り終わってから sitSeconds かけて、両端をなだらかにして下ろす
        /// </summary>
        public float Sit(float t, float sitSeconds)
        {
            if (sitSeconds <= 0f) return t >= Sound ? 1f : 0f;
            return Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - Sound) / sitSeconds));
        }

        /// <summary>脱いで座り終える時刻</summary>
        public float Seated(float sitSeconds) => Sound + Mathf.Max(0f, sitSeconds);
    }
}
