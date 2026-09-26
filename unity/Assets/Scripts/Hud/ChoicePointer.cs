namespace HalfAware
{
    /// <summary>
    /// 札へのマウス。毎フレーム、カーソルの下の札の番号（外なら -1）と、左ボタンを押したか・離したかを渡す。
    ///
    /// - 札に入ったら、その札を選ぶ（<see cref="Entered"/>）。札の上にいるだけでは選び直さないので、
    ///   左右のキーで選びを動かしても、マウスを動かさない限り戻されない
    /// - 同じ札の上で押して離したら、その札に決める（<see cref="Picked"/>）。押してから外へずらして離せば決めない
    /// - 出した直後にカーソルが札の上にあっても、それだけでは選ばない。動かして入り直した時か、押した時だけ
    /// - 出す前から押していたボタンを、出てから離しても決めない。字幕を送ったクリックで二択まで決めないため
    /// </summary>
    public sealed class ChoicePointer
    {
        /// <summary>出してまだ一度も見ていない</summary>
        const int Unknown = int.MinValue;

        int over = Unknown;
        int pressed = -1;

        /// <summary>このフレームで入った札。無ければ -1</summary>
        public int Entered { get; private set; }

        /// <summary>このフレームで決めた札。無ければ -1</summary>
        public int Picked { get; private set; }

        public ChoicePointer()
        {
            Reset();
        }

        /// <summary>二択を出し直すたびに呼ぶ</summary>
        public void Reset()
        {
            over = Unknown;
            pressed = -1;
            Entered = -1;
            Picked = -1;
        }

        /// <summary>1 フレームぶん。at はカーソルの下の札、down と up はこのフレームで左ボタンを押した・離したか</summary>
        public void Step(int at, bool down, bool up)
        {
            Entered = -1;
            Picked = -1;
            if (over == Unknown) over = at;
            else if (at != over)
            {
                over = at;
                if (at >= 0) Entered = at;
            }
            if (down) pressed = at;
            if (!up) return;
            if (at >= 0 && at == pressed) Picked = at;
            pressed = -1;
        }
    }
}
