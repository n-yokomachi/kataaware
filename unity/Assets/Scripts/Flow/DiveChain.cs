using System;
using System.Collections.Generic;
using UnityEngine;

namespace HalfAware
{
    /// <summary>
    /// 渡り歩きの決まり。いま誰の記憶か、次は誰か、`切断` がどれだけ大きいか。
    ///
    /// 最初の一人は列の一番上。プレイヤーが板で選べばそこへ、選ばずに尽きれば
    /// 端末が列の順に、列が尽きれば一覧から無作為に選ぶ。端末は一度潜った人を飛ばすが、
    /// プレイヤーは板で戻ってよい。
    ///
    /// `切断` は初め四割の大きさで押せず、人を渡るごとに大きくなって、
    /// threshold 人で `潜る` と同じ大きさになり、そこから押せる
    /// </summary>
    public sealed class DiveChain
    {
        /// <summary>`切断` の初めの大きさ。`潜る` を 1 として</summary>
        public const float CutStart = 0.4f;

        readonly int count;
        readonly int[] listed;
        readonly int threshold;
        readonly System.Random random;
        readonly HashSet<int> visited = new HashSet<int>();

        public int Current { get; private set; }
        public int Hops { get; private set; }

        public DiveChain(int count, int[] listed, int threshold, System.Random random)
        {
            this.count = count;
            this.listed = listed;
            this.threshold = Mathf.Max(1, threshold);
            this.random = random;
            Current = listed.Length > 0 ? listed[0] : 0;
            visited.Add(Current);
        }

        public bool CanCut { get { return Hops >= threshold; } }

        public float CutSize { get { return Mathf.Lerp(CutStart, 1f, Mathf.Clamp01((float)Hops / threshold)); } }

        /// <summary>板で選んだ先へ</summary>
        public void Hop(int target)
        {
            Current = Mathf.Clamp(target, 0, count - 1);
            visited.Add(Current);
            Hops++;
        }

        /// <summary>尽きたので端末が選ぶ</summary>
        public void Next()
        {
            for (var i = 0; i < listed.Length; i++)
                if (!visited.Contains(listed[i])) { Hop(listed[i]); return; }
            var rest = new List<int>();
            for (var i = 0; i < count; i++) if (!visited.Contains(i)) rest.Add(i);
            if (rest.Count == 0) { Hop(random.Next(count)); return; }
            Hop(rest[random.Next(rest.Count)]);
        }
    }
}
