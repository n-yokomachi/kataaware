using UnityEngine;

namespace HalfAware
{
    /// <summary>帯の今の段取り</summary>
    public enum DriveBeat
    {
        /// <summary>黙って走っている。きっかけを調べるまでここから動かない</summary>
        Running,
        /// <summary>独白が出ている。送り切るまでここから動かない</summary>
        Talking,
        /// <summary>送り切ったあとの余韻。また黙って走る</summary>
        Afterglow,
        /// <summary>黒。裏で次の帯を並べる</summary>
        Black,
        /// <summary>黒から浮かび上がっている</summary>
        FadingIn,
    }

    /// <summary>
    /// 帯の段取り。走る → 独白 → 余韻 → 黒 → 明ける → 走る。
    ///
    /// 走りと独白には終わりの時間を持たせない。きっかけを調べるまで、
    /// そして独白を送り切るまで、いくら時間が経っても次へ行かない。
    /// 時間で動くのは余韻から先の三つだけで、そこが演出の長さにあたる。
    ///
    /// MonoBehaviour の外に出してあるのは、これを丸ごとテストで確かめたいため
    /// </summary>
    public sealed class BandClock
    {
        float since;
        bool swapped;

        public DriveBeat Beat { get; private set; }

        /// <summary>きっかけの対象を調べた。独白が始まる</summary>
        public void Trigger()
        {
            if (Beat != DriveBeat.Running) return;
            Beat = DriveBeat.Talking;
            since = 0f;
        }

        /// <summary>独白を送り切った。ここから余韻</summary>
        public void Spoken()
        {
            if (Beat != DriveBeat.Talking) return;
            Beat = DriveBeat.Afterglow;
            since = 0f;
        }

        /// <summary>
        /// 黒へ入った最初の一度だけ true を返す。呼んだら下ろす。
        /// 景色の入れ替えは黒のあいだにやりたいが、毎フレーム並べ直したくない
        /// </summary>
        public bool TakeSwap()
        {
            if (!swapped) return false;
            swapped = false;
            return true;
        }

        /// <summary>
        /// 時を進める。Running と Talking では何も起きない。
        /// 秒数が 0 でも詰まらないよう、1 回の Tick で先まで進めることがある
        /// </summary>
        public void Tick(float dt, DriveBand band)
        {
            if (Beat == DriveBeat.Running || Beat == DriveBeat.Talking) return;
            since += dt;
            // 秒数が 0 のときに 1 フレーム 1 段ずつ進むと、途中の段が見えてしまう。
            // 越えた分をそのまま次へ持ち越して、その場で最後まで進める。
            // 進む先は 3 段しか無いので、この数を使い切ることは無い
            for (var guard = 0; guard < 4; guard++)
            {
                if (Beat == DriveBeat.Afterglow)
                {
                    if (since < band.afterglow) return;
                    since -= Spent(band.afterglow);
                    Beat = DriveBeat.Black;
                    swapped = true;
                    continue;
                }
                if (Beat == DriveBeat.Black)
                {
                    if (since < band.black) return;
                    since -= Spent(band.black);
                    Beat = DriveBeat.FadingIn;
                    continue;
                }
                if (Beat == DriveBeat.FadingIn)
                {
                    if (since < band.fadeIn) return;
                    since = 0f;
                    Beat = DriveBeat.Running;
                    return;
                }
                return;
            }
        }

        /// <summary>
        /// その段に使った秒数。負の数を打たれても次の段に貸しを作らない。
        /// 再生しながら Inspector を触る前提なので、打ち間違いはそのまま通る
        /// </summary>
        static float Spent(float seconds)
        {
            return seconds > 0f ? seconds : 0f;
        }

        /// <summary>
        /// 今どれだけ黒いか。0 で素通し、1 で真っ黒。
        /// 黒へは切り替えで入るので、Afterglow の 0 から Black の 1 へ一息に跳ぶ
        /// </summary>
        public float Dark(DriveBand band)
        {
            if (Beat == DriveBeat.Black) return 1f;
            if (Beat != DriveBeat.FadingIn) return 0f;
            if (band.fadeIn <= 0f) return 0f;
            return Mathf.Clamp01(1f - since / band.fadeIn);
        }

        /// <summary>
        /// 頭から組み直す。場面に入って最初の帯を並べるときだけ呼ぶ。
        /// 帯を跨ぐときには呼ばない。黒と明けはこの時計が自分で進めるので、
        /// そこで組み直すと暗転が 1 フレームで終わってしまう
        /// </summary>
        public void Reset()
        {
            Beat = DriveBeat.Running;
            since = 0f;
            swapped = false;
        }
    }
}
