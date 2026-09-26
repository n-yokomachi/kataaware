using UnityEngine;

namespace HalfAware
{
    /// <summary>
    /// コンソールの知らせ（「記憶した」「思い出せない」など）の出し入れ。見せ方は持たない（<see cref="ImplantConsole"/>）。
    ///
    /// 出してから <see cref="Seconds"/> 秒たつか、出した後の次の入力（キー・マウスのボタン・車輪）で消す。
    /// 入力は消すだけでなく、そのままふだんどおり効く（閉じる・選ぶを二度押しさせない）。
    /// 秒は unscaled で渡す（コンソールを開いている間は Time.timeScale が 0）
    /// </summary>
    public sealed class ConsoleNote
    {
        /// <summary>出しておく秒</summary>
        public const float Seconds = 1.5f;

        /// <summary>消える前に薄れる秒</summary>
        public const float FadeSeconds = 0.25f;

        float until;
        int frame = -1;

        /// <summary>出している知らせ。出していなければ null</summary>
        public string Text { get; private set; }

        public bool Visible { get { return !string.IsNullOrEmpty(Text); } }

        /// <summary>知らせを出す。now は unscaled の秒、frame はいまのフレーム</summary>
        public void Say(string text, float now, int frame)
        {
            Text = text;
            until = now + Seconds;
            this.frame = frame;
        }

        /// <summary>
        /// 1 フレームぶん。input はこのフレームで何か押したか。時間が来たか、入力があれば消す。
        /// 出したのと同じフレームの入力では消さない（その入力で出したので）
        /// </summary>
        public void Step(float now, int frame, bool input)
        {
            if (!Visible) return;
            if (now >= until || (input && frame != this.frame)) Clear();
        }

        public void Clear()
        {
            Text = null;
        }

        /// <summary>濃さ。終わりの <see cref="FadeSeconds"/> 秒で 1 から 0 へ薄れる</summary>
        public float Alpha(float now)
        {
            if (!Visible) return 0f;
            return Mathf.Clamp01((until - now) / FadeSeconds);
        }
    }
}
