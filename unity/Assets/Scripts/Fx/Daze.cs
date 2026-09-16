using UnityEngine;

namespace HalfAware
{
    /// <summary>
    /// 眩暈の強さ。ぼかしと輪郭の揺れを 0〜1 で持つ。
    /// Hold で保ち、Decay で時間をかけて 0 にする。見え方は段階 4 の後処理が受け持つ
    /// </summary>
    public sealed class Daze
    {
        float fromBlur;
        float fromWobble;
        float total;
        float left;

        public float Blur { get; private set; }
        public float Wobble { get; private set; }

        public bool IsClear => Blur <= 0f && Wobble <= 0f;

        /// <summary>この強さのまま保つ。Tick では減らない</summary>
        public void Hold(float blur, float wobble)
        {
            total = 0f;
            left = 0f;
            Blur = blur;
            Wobble = wobble;
        }

        /// <summary>この強さから seconds 秒かけて 0 にする。seconds が 0 以下なら即 0</summary>
        public void Decay(float blur, float wobble, float seconds)
        {
            if (seconds <= 0f)
            {
                Clear();
                return;
            }
            fromBlur = blur;
            fromWobble = wobble;
            total = seconds;
            left = seconds;
            Blur = blur;
            Wobble = wobble;
        }

        public void Clear()
        {
            Hold(0f, 0f);
        }

        public void Tick(float dt)
        {
            if (total <= 0f) return;
            left = Mathf.Max(0f, left - dt);
            var k = left / total;
            Blur = fromBlur * k;
            Wobble = fromWobble * k;
            if (left <= 0f) total = 0f;
        }
    }
}
