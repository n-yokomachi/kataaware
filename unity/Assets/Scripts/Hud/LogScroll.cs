using UnityEngine;

namespace HalfAware
{
    /// <summary>
    /// ログの枠のさかのぼり。最新の行を枠のいちばん下に置き、上へ送ると古い行が出てくる。
    /// 量は枠の中の高さ（キャンバスの単位）で持つ。0 が最新で、<see cref="Max"/> がいちばん古い行の頭。
    /// 右の細いスクロールバーのつまみの大きさと位置もここで決める
    /// </summary>
    public sealed class LogScroll
    {
        /// <summary>行を全部並べた高さ</summary>
        public float Content { get; private set; }

        /// <summary>枠の中の見える高さ</summary>
        public float View { get; private set; }

        /// <summary>最新からさかのぼった量。0 で最新の行が枠の下の縁に付く</summary>
        public float Back { get; private set; }

        /// <summary>さかのぼれる限り。中身が枠に収まれば 0</summary>
        public float Max { get { return Mathf.Max(0f, Content - View); } }

        /// <summary>いちばん新しいところにいるか</summary>
        public bool AtNewest { get { return Back <= 0f; } }

        /// <summary>中身と枠の高さを入れ直す。さかのぼった量は範囲に収め直す</summary>
        public void Fit(float content, float view)
        {
            Content = Mathf.Max(0f, content);
            View = Mathf.Max(0f, view);
            Back = Mathf.Clamp(Back, 0f, Max);
        }

        /// <summary>最新へ戻す。開いた時は必ずここから</summary>
        public void Newest()
        {
            Back = 0f;
        }

        /// <summary>amount だけ送る。正で古い方へ、負で新しい方へ。両端で止まる</summary>
        public void By(float amount)
        {
            Back = Mathf.Clamp(Back + amount, 0f, Max);
        }

        /// <summary>つまみの長さ。枠に対する割合。中身が収まっていれば 1</summary>
        public float Thumb
        {
            get
            {
                if (Content <= View || Content <= 0f) return 1f;
                return Mathf.Clamp01(View / Content);
            }
        }

        /// <summary>つまみの下の縁の位置。溝の下の端が 0、上の端が 1 - Thumb</summary>
        public float ThumbBottom
        {
            get { return Max <= 0f ? 0f : Back / Max * (1f - Thumb); }
        }

        /// <summary>つまみを掴んで動かす。下の縁を溝の割合で渡す</summary>
        public void DragTo(float bottom)
        {
            var room = 1f - Thumb;
            if (room <= 0f || Max <= 0f) { Back = 0f; return; }
            Back = Mathf.Clamp01(bottom / room) * Max;
        }
    }
}
