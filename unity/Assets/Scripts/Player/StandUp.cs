using UnityEngine;

namespace HalfAware
{
    /// <summary>
    /// 座位から立位への目線の移り変わり。released が立ったら上がり始め、seconds かけて立位の高さになる。
    /// 止まっている間は上がり始めない。一度始まったら止まらない
    /// </summary>
    public sealed class StandUp
    {
        readonly float seat;
        readonly float standing;
        readonly float seconds;
        /// <summary>立ち上がり始めてからの秒数。負のあいだはまだ始まっていない</summary>
        float elapsed = -1f;

        public StandUp(float seatEyeHeight, float standingEyeHeight, float seconds)
        {
            seat = seatEyeHeight;
            standing = standingEyeHeight;
            this.seconds = seconds;
            EyeHeight = seatEyeHeight;
        }

        public float EyeHeight { get; private set; }

        /// <summary>立ち終わったか。true になったら移動を許してよい</summary>
        public bool Standing { get; private set; }

        public void Tick(float dt, bool released, bool frozen)
        {
            if (Standing) return;
            if (elapsed < 0f)
            {
                if (frozen || !released) return;
                elapsed = 0f;
            }
            elapsed += dt;
            var k = seconds <= 0f ? 1f : Mathf.Min(1f, elapsed / seconds);
            EyeHeight = seat + (standing - seat) * k;
            if (k >= 1f)
            {
                EyeHeight = standing;
                Standing = true;
            }
        }
    }
}
