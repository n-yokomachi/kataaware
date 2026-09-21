using System;
using UnityEngine;

namespace HalfAware
{
    /// <summary>ぼやけ方。老眼は近くが、近視は遠くがぼやける</summary>
    public enum Blur { Sharp, Near, Far }

    /// <summary>記憶の中で見える人ひとり。板が出る相手と、板から飛ぶ先</summary>
    [Serializable]
    public struct Seen
    {
        [Tooltip("Take の下の GameObject の名前")]
        public string name;
        [Tooltip("飛び先。一覧での番号（0 始まり）")]
        public int target;
    }

    /// <summary>記憶一つ分の値。場所と人の形はシーン（Take）が持ち、ここは数と文字だけ</summary>
    [Serializable]
    public struct DiveEntry
    {
        [Tooltip("右上と板に出す行。場面 3 の列と同じ書式")]
        public string row;
        [Tooltip("場所の id。DiveIds.Places のどれか")]
        public string place;
        [Tooltip("秒")]
        public float length;
        [Tooltip("目の高さ。m")]
        public float eyeHeight;
        public Blur blur;
        [Tooltip("ぼやけの強さ。0〜1")]
        public float blurAmount;
        [Tooltip("体の動きの速さ。基準 1")]
        public float speed;
        [Tooltip("色味。Volume の Color Filter に入れる")]
        public Color tint;
        [Tooltip("耳の詰まり。0 で素、1 で低域だけ")]
        public float muffle;
        public bool heartbeat;
        public Seen[] seen;
    }

    /// <summary>場面 4 の記憶の一覧。設計書 6 節。書き出し（WriteDiveRoster）で作る</summary>
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
