using UnityEngine;

namespace HalfAware
{
    /// <summary>
    /// 場面 4 の記憶の一覧。設計書 6 節。書き出し（WriteDiveRoster）で作る。
    ///
    /// <see cref="DiveEntry"/> と同じファイルに置けない。Unity はアセットへ結ぶ
    /// ScriptableObject をファイル名で探すので、クラス名と違う名前のファイルに書くと
    /// アセットの m_Script が空のまま作られ、読み込んでも null になる
    /// </summary>
    [CreateAssetMenu(fileName = "DiveRoster", menuName = "HalfAware/Dive Roster")]
    public sealed class DiveRoster : ScriptableObject
    {
        [SerializeField] DiveEntry[] entries = new DiveEntry[0];

        public int Count { get { return entries.Length; } }

        public DiveEntry this[int i] { get { return entries[i]; } }

        /// <summary>一覧の中で閉じているか。飛び先がすべて一覧の中を指しているか</summary>
        public static bool Closed(DiveEntry[] all)
        {
            for (var i = 0; i < all.Length; i++)
            {
                var seen = all[i].seen;
                if (seen == null) continue;
                for (var k = 0; k < seen.Length; k++)
                    if (seen[k].target < 0 || seen[k].target >= all.Length) return false;
            }
            return true;
        }
    }
}
