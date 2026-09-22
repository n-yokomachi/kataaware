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

    /// <summary>
    /// 記憶の中で交わされる一行と、それが出る場所の点。
    /// 顔は見せないので、誰が喋っているかは声の向きとこの文字列の名前でしか伝わらない。
    /// 話者と鉤括弧は line に含める。
    ///
    /// **秒では出さない。** 記憶の頭からの秒で流していた版は、何をすれば進むのか
    /// 読めないと差し戻された（設計書 2 節）。行はプレイヤーが点へ入ったときに出る
    /// </summary>
    [Serializable]
    public struct Said
    {
        [Tooltip("話者と鉤括弧つきの一行")]
        public string line;
        [Tooltip("この行が出る点。場所のローカル。主の足元で測る")]
        public Vector3 where;
        [Tooltip("点の届く半径。m。0 以下なら点を置いていない")]
        public float radius;

        /// <summary>点を置いてあるか。置いていない行は、一行目のほかは出ないまま残る</summary>
        public bool Placed { get { return radius > 0f; } }

        /// <summary>足元が at にあるとき、この点の中にいるか。at は場所のローカル</summary>
        public bool Holds(Vector3 at)
        {
            return radius > 0f && (at - where).sqrMagnitude <= radius * radius;
        }
    }

    /// <summary>記憶一つ分の値。場所と人の形はシーン（Take）が持ち、ここは数と文字だけ</summary>
    [Serializable]
    public struct DiveEntry
    {
        [Tooltip("右上と板に出す行。場面 3 の列と同じ書式")]
        public string row;
        [Tooltip("場所の id。DiveIds.Places のどれか")]
        public string place;
        [Tooltip("鍵打ちと人の動きが一巡する秒。記憶はここで終わらない")]
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
        [Tooltip("場所の点で出す会話。設計書 7 節")]
        public Said[] said;

        /// <summary>
        /// いま出す行の番号。spoken 行まで出した状態で、主の足元が at にあるときの答え。
        /// 出す行が無ければ -1。at は場所のローカル。
        ///
        /// **点は順に armed になる。** 見るのは次の一つだけなので、先の点の中を
        /// 通り抜けても順番は飛ばない。
        ///
        /// 一行目は名前を呼ばれる声で、記憶に入った瞬間に出る。二行目から先は
        /// 点を置いていなければ出ないまま残る（団地の四本のほかはまだ置いていない）
        /// </summary>
        public static int Due(Said[] said, int spoken, Vector3 at)
        {
            if (said == null || spoken < 0 || spoken >= said.Length) return -1;
            if (spoken == 0) return 0;
            return said[spoken].Holds(at) ? spoken : -1;
        }
    }
}
