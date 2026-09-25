using UnityEngine;

namespace HalfAware
{
    /// <summary>
    /// 何曲かを順に流して輪にする曲の並びの、位置の計算（<see cref="StallRadio"/>）。
    /// 並べた曲を一本につないだ全体の中の位置（秒）を、何曲目の何秒かへ直す。
    /// 最後の曲の後は最初の曲へ戻るので、全体より先の位置や負の位置は輪で折り返す。
    /// 長さが 0 の曲（読めなかった曲）は飛ばす
    /// </summary>
    public static class Playlist
    {
        /// <summary>全体の長さ（秒）。長さが 0 以下の曲は数えない</summary>
        public static float Total(float[] lengths)
        {
            var total = 0f;
            if (lengths == null) return total;
            foreach (var l in lengths) if (l > 0f) total += l;
            return total;
        }

        /// <summary>
        /// 全体の中の position（秒）が、何曲目（index）の何秒（time）か。
        /// 流せる曲が一つも無ければ false（index は -1、time は 0）
        /// </summary>
        public static bool Locate(float position, float[] lengths, out int index, out float time)
        {
            index = -1;
            time = 0f;
            var total = Total(lengths);
            if (total <= 0f) return false;
            var at = Mathf.Repeat(position, total);
            for (var i = 0; i < lengths.Length; i++)
            {
                var l = lengths[i];
                if (l <= 0f) continue;
                if (at < l)
                {
                    index = i;
                    time = at;
                    return true;
                }
                at -= l;
            }
            // 丸めの誤差で末尾を越えたときは、最後に流せる曲の終わり
            for (var i = lengths.Length - 1; i >= 0; i--)
            {
                if (lengths[i] <= 0f) continue;
                index = i;
                time = lengths[i];
                return true;
            }
            return false;
        }

        /// <summary>index の次に流す曲。最後の後は最初へ戻る。長さが 0 以下の曲は飛ばす。流せる曲が無ければ -1</summary>
        public static int Next(int index, float[] lengths)
        {
            if (lengths == null || lengths.Length == 0) return -1;
            for (var step = 1; step <= lengths.Length; step++)
            {
                var i = ((index + step) % lengths.Length + lengths.Length) % lengths.Length;
                if (lengths[i] > 0f) return i;
            }
            return -1;
        }
    }
}
