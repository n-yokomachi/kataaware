using UnityEngine;

namespace HalfAware
{
    /// <summary>
    /// 目覚めの起き上がり。椅子に体を預けて天井を見ていた姿勢から、座位の高さと水平な視線へ戻る。
    /// 場面の始まりに一度だけ動き、終わったら二度と動かない。
    /// 両端をなだらかにして、頭を起こす感じにする
    /// </summary>
    public sealed class WakeUp
    {
        /// <summary>背を預けているぶん、座位の目線よりどれだけ低く始めるか。メートル</summary>
        public const float DefaultDrop = 0.18f;

        /// <summary>始まりの視線の角度。度。正が下向きなので、天井を仰ぐぶん負にする</summary>
        public const float DefaultStartPitch = -50f;

        /// <summary>起き上がりにかける秒数</summary>
        public const float DefaultSeconds = 2.2f;

        /// <summary>刻みを足し込むと端数が残るので、この分だけ手前で終わりとみなす</summary>
        const float Tolerance = 1e-4f;

        readonly float seat;
        readonly float drop;
        readonly float startPitch;
        readonly float seconds;
        float elapsed;

        public WakeUp(float seatEyeHeight, float drop, float startPitch, float seconds)
        {
            seat = seatEyeHeight;
            this.drop = drop;
            this.startPitch = startPitch;
            this.seconds = seconds;
            EyeHeight = seat - drop;
            Pitch = startPitch;
            Done = seconds <= 0f;
            if (Done)
            {
                EyeHeight = seat;
                Pitch = 0f;
            }
        }

        /// <summary>今の目線の高さ</summary>
        public float EyeHeight { get; private set; }

        /// <summary>今の上下の向き。度。正が下向き</summary>
        public float Pitch { get; private set; }

        /// <summary>起き上がり終えたか。true になったら見回しを返してよい</summary>
        public bool Done { get; private set; }

        public void Tick(float dt)
        {
            if (Done) return;
            elapsed += dt;
            var k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / seconds));
            EyeHeight = seat - drop * (1f - k);
            Pitch = startPitch * (1f - k);
            if (elapsed < seconds - Tolerance) return;
            EyeHeight = seat;
            Pitch = 0f;
            Done = true;
        }
    }
}
