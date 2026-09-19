using System;
using System.Collections.Generic;

namespace HalfAware
{
    /// <summary>
    /// 景色の帯 1 つ分。尺に関わる値はここにしか無い。
    /// 秒数はオーナーが実画面を見てから決めるので、組み立て側には仮置きしか入れない
    /// </summary>
    [Serializable]
    public struct DriveBand
    {
        /// <summary>帯の名前。ログと見直しで使う</summary>
        public string name;
        /// <summary>この帯の独白を始める対象の id。調べるまで帯は終わらない</summary>
        public string trigger;
        /// <summary>走る速さ。m/s。タイルの送りと揺れがこの一つから出る</summary>
        public float speed;
        /// <summary>路面の粗さ。1 が舗装、未舗装はもっと大きい。車体の揺れ幅に掛かる</summary>
        public float rough;
        /// <summary>独白を送り切ってから黒へ切り替わるまでの秒数。黙って走る</summary>
        public float afterglow;
        /// <summary>黒のまま置く秒数。仮眠にあたる切れ目だけ長く取る</summary>
        public float black;
        /// <summary>黒から次の帯へ浮かび上がる秒数</summary>
        public float fadeIn;
    }

    /// <summary>
    /// 帯の並び。順送りと、きっかけの id からの引き当てだけを持つ。
    /// 範囲の外を渡されても落ちない。組み立ての途中で帯が空のことがある
    /// </summary>
    public sealed class DriveRoute
    {
        readonly DriveBand[] bands;

        public DriveRoute(IReadOnlyList<DriveBand> from)
        {
            if (from == null) { bands = new DriveBand[0]; return; }
            bands = new DriveBand[from.Count];
            for (var i = 0; i < from.Count; i++) bands[i] = from[i];
        }

        public int Count { get { return bands.Length; } }

        /// <summary>i 番目の帯。範囲の外なら空の帯</summary>
        public DriveBand At(int i)
        {
            return i >= 0 && i < bands.Length ? bands[i] : new DriveBand();
        }

        /// <summary>
        /// i が最後の帯か。帯がひとつも無いうちは最後にしない。
        /// 組み立て途中の場面が、入った瞬間に閉じてしまうのを防ぐ（SceneProgress.IsComplete と同じ構え）
        /// </summary>
        public bool IsLast(int i)
        {
            return bands.Length > 0 && i >= bands.Length - 1;
        }

        /// <summary>そのきっかけの id を持つ帯。どの帯のものでもなければ -1</summary>
        public int BandOf(string trigger)
        {
            if (string.IsNullOrEmpty(trigger)) return -1;
            for (var i = 0; i < bands.Length; i++)
                if (bands[i].trigger == trigger) return i;
            return -1;
        }
    }
}
