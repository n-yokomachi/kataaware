using UnityEngine;

namespace HalfAware
{
    /// <summary>
    /// BGM の声一つ。いまの曲の呼び名と、フェードの大きさ（0〜1）だけを持つ。音源には触らない（<see cref="MusicBed"/> が写す）。
    ///
    /// **フェードは電力を保つ形。** 大きさの二乗（電力）を、半分の余弦（両端が緩い）で from から to へ運ぶ。
    /// 0 から 1 なら sin、1 から 0 なら cos の四分の一になり、入る曲と出る曲の電力の和はいつも 1（クロスフェードで中が凹まない）。
    /// 途中から向きを変えても、今の大きさからそのまま続く
    /// </summary>
    public sealed class MusicVoice
    {
        /// <summary>フェードの残りがこれを切ったら着いたことにする。秒</summary>
        const float Settle = 1e-4f;

        float from;
        float to;
        float elapsed;
        float seconds;

        /// <summary>流している曲。何も流していなければ <see cref="MusicCue.None"/></summary>
        public MusicCue Cue { get; private set; }

        /// <summary>いまのフェードの大きさ。0〜1。曲ごとの大きさ（<see cref="MusicTable.Track.volume"/>）は掛けていない</summary>
        public float Gain { get; private set; }

        /// <summary>フェードの行き先。0 なら消えていく途中</summary>
        public float Target { get { return to; } }

        /// <summary>何か流しているか（消えていく途中も含む）</summary>
        public bool Active { get { return Cue != MusicCue.None; } }

        /// <summary>フェードの途中か</summary>
        public bool Fading { get { return elapsed < seconds; } }

        /// <summary>フェードの残りの秒</summary>
        public float Left { get { return Mathf.Max(0f, seconds - elapsed); } }

        /// <summary>曲を当てたばかりで、音源をまだ鳴らしていない。<see cref="MusicBed"/> が鳴らしたら下ろす</summary>
        public bool Fresh { get; set; }

        /// <summary>曲の読み込み（Web の展開）を待っている。待つ間はフェードを進めない（<see cref="SoundLoad"/>）</summary>
        public bool Waiting { get; set; }

        /// <summary>曲を当てる。大きさ 0 から、まだ鳴らしていない形で</summary>
        public void Begin(MusicCue cue)
        {
            Cue = cue;
            Gain = 0f;
            from = 0f;
            to = 0f;
            elapsed = 0f;
            seconds = 0f;
            Fresh = true;
            Waiting = false;
        }

        /// <summary>今の大きさから target へ、seconds 秒で運ぶ。0 秒以下ならその場で</summary>
        public void FadeTo(float target, float seconds)
        {
            from = Gain;
            to = Mathf.Clamp01(target);
            elapsed = 0f;
            this.seconds = Mathf.Max(0f, seconds);
            if (this.seconds <= 0f) Gain = to;
        }

        /// <summary>何も流していない形へ戻す</summary>
        public void Clear()
        {
            Cue = MusicCue.None;
            Gain = 0f;
            from = 0f;
            to = 0f;
            elapsed = 0f;
            seconds = 0f;
            Fresh = false;
            Waiting = false;
        }

        /// <summary>dt 秒進める。消えきったら何も流していない形へ戻して true を返す</summary>
        public bool Tick(float dt)
        {
            if (!Active) return false;
            if (elapsed < seconds)
            {
                elapsed = Mathf.Min(seconds, elapsed + Mathf.Max(0f, dt));
                // 刻みを足した秒は丸めの分だけ届かないことがある。残りが 0.1ms を切ったら着いたことにする
                if (seconds - elapsed < Settle) elapsed = seconds;
                Gain = Curve(from, to, elapsed / seconds);
            }
            if (to > 0f || elapsed < seconds) return false;
            Clear();
            return true;
        }

        /// <summary>from から to へ、t（0〜1）の所の大きさ。電力（二乗）を半分の余弦で運ぶ</summary>
        public static float Curve(float from, float to, float t)
        {
            var k = (1f - Mathf.Cos(Mathf.PI * Mathf.Clamp01(t))) * 0.5f;
            var power = from * from + (to * to - from * from) * k;
            return Mathf.Sqrt(Mathf.Max(0f, power));
        }
    }

    /// <summary>
    /// BGM の二つの声の段取り（<see cref="MusicBed"/> の中身）。前の声（<see cref="Front"/>）がいまの曲で、
    /// 後ろの声は消えていく曲だけを持つ。MonoBehaviour の外に出してあるのは、秒の動きをテストで確かめたいため。
    ///
    /// - <see cref="Play"/>: 同じ曲なら何もしない（頭へ戻さない）。何も流れていなければ fadeIn 秒で入る。
    ///   ほかの曲が流れていれば、前の曲を後ろへ回して cross 秒で消し、新しい曲を cross 秒で上げる。
    ///   前の曲が消えていく途中（<see cref="FadeOut"/> の後）なら、消えていくのはそのままに、新しい曲は fadeIn 秒で入る
    /// - <see cref="FadeOut"/>: 流れている曲を seconds 秒で消す。消えきったら止まる
    /// - <see cref="Stop"/>: その場で止める。フェードしない
    ///
    /// 新しい曲の読み込みを待つ間（<see cref="MusicVoice.Waiting"/>）は、その曲のフェードも、入れ替わりに消えていく曲のフェードも進めない。
    /// 読み終わるまで前の曲が鳴り続け、無音の隙間を作らない
    /// </summary>
    public sealed class MusicMix
    {
        readonly MusicVoice[] voices = { new MusicVoice(), new MusicVoice() };
        int front;

        /// <summary>いまの曲の声</summary>
        public MusicVoice Front { get { return voices[front]; } }

        /// <summary>消えていく曲の声</summary>
        public MusicVoice Back { get { return voices[1 - front]; } }

        /// <summary>いまの曲の声の番号（0 か 1）。<see cref="MusicBed"/> は同じ番号の音源へ写す</summary>
        public int FrontIndex { get { return front; } }

        /// <summary>i 番の声</summary>
        public MusicVoice Voice(int i) { return voices[i]; }

        /// <summary>いま流している曲。消えていく途中の曲は数えない。何も無ければ <see cref="MusicCue.None"/></summary>
        public MusicCue Current { get { return Front.Active && Front.Target > 0f ? Front.Cue : MusicCue.None; } }

        /// <summary>何か鳴っているか（消えていく途中も含む）</summary>
        public bool Sounding { get { return voices[0].Active || voices[1].Active; } }

        /// <summary>cue を流す。何をしたかを返す（テストで読む）</summary>
        public MusicStart Play(MusicCue cue, float fadeIn, float cross)
        {
            if (cue == MusicCue.None) return MusicStart.Kept;
            var was = Front;
            if (was.Active && was.Cue == cue)
            {
                // 同じ曲。頭へ戻さずに流し続ける。消えていく途中なら上げ直す
                if (was.Target < 1f) was.FadeTo(1f, fadeIn);
                return MusicStart.Kept;
            }
            var crossing = was.Active && was.Target > 0f;
            if (was.Active)
            {
                // 前の曲を後ろへ回す。後ろでまだ消えていく曲があれば、そこで切る（三つは重ねない）
                if (Back.Active) Back.Clear();
                front = 1 - front;
                if (crossing) was.FadeTo(0f, cross);
            }
            Front.Begin(cue);
            Front.FadeTo(1f, crossing ? cross : fadeIn);
            return crossing ? MusicStart.Crossed : MusicStart.Faded;
        }

        /// <summary>流れている曲を seconds 秒で消す。もっと早く消えきる途中なら、そちらのまま。0 秒以下ならその場で止める</summary>
        public void FadeOut(float seconds)
        {
            if (seconds <= 0f) { Stop(); return; }
            foreach (var v in voices)
            {
                if (!v.Active) continue;
                if (v.Target <= 0f && v.Fading && v.Left <= seconds) continue;
                v.FadeTo(0f, seconds);
            }
        }

        /// <summary>その場で止める</summary>
        public void Stop()
        {
            voices[0].Clear();
            voices[1].Clear();
        }

        /// <summary>dt 秒進める</summary>
        public void Tick(float dt)
        {
            // 次の曲の読み込みを待つ間は、入れ替わりに消えていく曲も待たせる
            var hold = Front.Active && Front.Waiting;
            if (!Front.Waiting) Front.Tick(dt);
            if (!Back.Waiting && !hold) Back.Tick(dt);
        }
    }

    /// <summary><see cref="MusicMix.Play"/> が何をしたか</summary>
    public enum MusicStart
    {
        /// <summary>同じ曲が流れていたので、そのまま（か、上げ直した）</summary>
        Kept,
        /// <summary>何も流れていない所から入った</summary>
        Faded,
        /// <summary>ほかの曲から渡った</summary>
        Crossed,
    }
}
