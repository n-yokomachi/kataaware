using UnityEngine;

namespace HalfAware
{
    /// <summary>場面 2 の、プレイヤーのいる所。通り・小路・ヤード</summary>
    public enum AlleyPlace
    {
        /// <summary>グレビル・ストリート（小路とヤードの外は、みな通り）</summary>
        Street,
        /// <summary>通りから西へ折れる小路</summary>
        Lane,
        /// <summary>小路の先のヤード（蚤の市）</summary>
        Yard,
    }

    /// <summary>
    /// 場面 2 の場所ごとの音の大きさ。雑踏の音（<see cref="CrowdNoise"/>）と、ヤードのスピーカーの曲（<see cref="StallRadio"/>）が使う。
    /// 小路とヤードの範囲は組み立て（BuildAlley の定数）がシーンへ書き込み、ここは渡された範囲で決めるだけ
    /// </summary>
    public static class AlleySound
    {
        /// <summary>at がどこか。ヤードと小路の境はヤードに数える</summary>
        public static AlleyPlace Where(Vector3 at, Bounds lane, Bounds yard)
        {
            if (yard.Contains(at)) return AlleyPlace.Yard;
            if (lane.Contains(at)) return AlleyPlace.Lane;
            return AlleyPlace.Street;
        }

        /// <summary>場所ごとの値を一つ選ぶ</summary>
        public static float Level(AlleyPlace place, float street, float lane, float yard)
        {
            switch (place)
            {
                case AlleyPlace.Yard: return yard;
                case AlleyPlace.Lane: return lane;
                default: return street;
            }
        }

        /// <summary>
        /// 小路のどこまで入ったか。通りの側の端で 0、ヤードの口で 1。
        /// ヤードの中は 1、通りは 0。小路の長さは範囲の長い辺で測り、ヤードの範囲までの距離から出す
        /// </summary>
        public static float Depth(Vector3 at, Bounds lane, Bounds yard)
        {
            var place = Where(at, lane, yard);
            if (place == AlleyPlace.Yard) return 1f;
            if (place == AlleyPlace.Street) return 0f;
            var length = Mathf.Max(lane.size.x, lane.size.z);
            if (length <= 0f) return 1f;
            return 1f - Mathf.Clamp01(Mathf.Sqrt(yard.SqrDistance(at)) / length);
        }

        /// <summary>
        /// 今の値を狙いの値へ寄せる。境で急に変わると嘘に聞こえるので、seconds かけて差の 6 割ほどを詰める（指数で寄せる）。
        /// 行き過ぎない。seconds が 0 以下なら一度に狙いの値にする
        /// </summary>
        public static float Ease(float current, float target, float dt, float seconds)
        {
            if (seconds <= 0f) return target;
            return Mathf.Lerp(current, target, 1f - Mathf.Exp(-Mathf.Max(0f, dt) / seconds));
        }
    }
}
