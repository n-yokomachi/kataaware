using UnityEngine;

namespace HalfAware
{
    /// <summary>
    /// 酔って視点が漂う動き。時間と強さから、目の位置のずれと上下左右の傾きを返す。
    /// 周期の合わない正弦波を重ねるので、見ていて繰り返しに気づかない。
    /// 画面を回す動き（ロール）は持たない。遊ぶ側がいちばん酔うため
    /// </summary>
    public sealed class Sway
    {
        /// <summary>目の位置に足すずれ。左右と上下だけで、前後には動かさない</summary>
        public Vector3 Offset { get; private set; }

        /// <summary>向きに足す傾き。x が上下、y が左右。度</summary>
        public Vector2 Tilt { get; private set; }

        /// <summary>
        /// strength は 0 で止まり、1 でいちばん強い。負の値は 0 として扱う。
        /// shift はメートル、tilt は度で、どちらもいちばん強いときの振れ幅。
        /// 毎フレーム渡すので、Inspector で振れ幅を変えるとその場で効く
        /// </summary>
        public void Tick(float time, float strength, float shift, float tilt)
        {
            var s = Mathf.Max(0f, strength);
            var x = Mathf.Sin(time * 0.37f) * 0.6f + Mathf.Sin(time * 0.83f + 2.1f) * 0.4f;
            var y = Mathf.Sin(time * 0.53f + 1.7f) * 0.7f + Mathf.Sin(time * 1.13f) * 0.3f;
            Offset = new Vector3(x, y, 0f) * (shift * s);
            Tilt = new Vector2(
                Mathf.Sin(time * 0.41f + 0.9f),
                Mathf.Sin(time * 0.29f + 2.3f)) * (tilt * s);
        }
    }
}
