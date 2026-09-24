using UnityEngine;

namespace HalfAware
{
    /// <summary>
    /// 端末を調べたとき、座った正面の視点へ移して読ませ、読み終えたら元の所へ戻す流れ（どの段にいて、どこまで寄ったか）。
    /// 置き方の計算は持たない。寄った度合い（<see cref="Seat"/>）を、呼ぶ側が元の所と座った所の間に当てる。
    ///
    /// 段は 離れている → 移している（goSeconds）→ 読んでいる → 戻している（backSeconds）→ 離れている。
    /// 読んでいる段は、字幕を出している間と、映り込みが消えきるまで続く（消えてから戻る）。
    /// 移す・戻すは両端をなだらかにする（酔わないよう、動き出しと止まり際を遅く）
    /// </summary>
    public sealed class SeatVisit
    {
        public enum Phase { Away, Going, Reading, Leaving }

        readonly float goSeconds;
        readonly float backSeconds;
        float elapsed;

        public SeatVisit(float goSeconds, float backSeconds)
        {
            this.goSeconds = goSeconds;
            this.backSeconds = backSeconds;
        }

        /// <summary>今の段</summary>
        public Phase Now { get; private set; } = Phase.Away;

        /// <summary>移すか戻すか読んでいる最中か</summary>
        public bool Busy => Now != Phase.Away;

        /// <summary>見回しと歩きを止める間（移す・読む・戻す）</summary>
        public bool Locked => Now != Phase.Away;

        /// <summary>
        /// 調べる操作と字幕送りを止める間。移す・戻す間と、読んでいる段で字幕が切れた後（映り込みが消えるまで）。
        /// talking は字幕を出しているか
        /// </summary>
        public bool Frozen(bool talking)
        {
            return Now == Phase.Going || Now == Phase.Leaving || (Now == Phase.Reading && !talking);
        }

        /// <summary>座った所の側へ寄った度合い。0 が元の所、1 が座った正面</summary>
        public float Seat
        {
            get
            {
                switch (Now)
                {
                    case Phase.Going: return Ease(goSeconds > 0f ? elapsed / goSeconds : 1f);
                    case Phase.Reading: return 1f;
                    case Phase.Leaving: return 1f - Ease(backSeconds > 0f ? elapsed / backSeconds : 1f);
                    default: return 0f;
                }
            }
        }

        /// <summary>移し始める。離れている時だけ働く</summary>
        public void Start()
        {
            if (Now != Phase.Away) return;
            Now = Phase.Going;
            elapsed = 0f;
        }

        /// <summary>dt 秒進める。talking は字幕を出しているか、shown は映り込みがまだ見えているか</summary>
        public void Tick(float dt, bool talking, bool shown)
        {
            switch (Now)
            {
                case Phase.Going:
                    elapsed += dt;
                    if (elapsed >= goSeconds) { Now = Phase.Reading; elapsed = 0f; }
                    break;
                case Phase.Reading:
                    if (!talking && !shown) { Now = Phase.Leaving; elapsed = 0f; }
                    break;
                case Phase.Leaving:
                    elapsed += dt;
                    if (elapsed >= backSeconds) { Now = Phase.Away; elapsed = 0f; }
                    break;
            }
        }

        /// <summary>両端をなだらかにした 0〜1</summary>
        public static float Ease(float x)
        {
            return Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(x));
        }
    }
}
