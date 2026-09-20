using UnityEngine;

namespace HalfAware
{
    /// <summary>
    /// 遠景に立つ一本の木を、走った距離のごく一部だけ動かす。
    ///
    /// **環に乗せてはいけない。** 沿道の物はどれも 180 m で一周する環に乗っていて
    /// （<see cref="RoadRing"/>）、乗せた木は必ず 180 m ごとに戻ってくる。
    /// 16 秒に一度おなじ木が同じところへ現れれば、遠景ではなく回り舞台になる。
    ///
    /// かといって空の板のように動かさずに置くと、今度は貼り付いた書き割りになる。
    /// 実際の遠景は、走ってもほんの少しだけ場所が変わる。視差は距離に反比例するので、
    /// 1 km 先の木は 100 m 先の木の 1/10 しか動かない。
    /// ここは**その少しだけを直に与える**。木そのものは霧が届く 100〜200 m のところへ
    /// 置いて、動きだけを <see cref="parallax"/> で遠くのものにする。
    /// 霧の濃さ（帯 4 で 0.0080）では 250 m より先は何も見えないので、
    /// 本当に 1 km 先へ置くことはできない。
    ///
    /// 走った距離は <see cref="DriveWorld"/> が持っている。親を辿って拾うので、
    /// この component は <c>Drive</c> の下のどこかに居ればよい。繋ぎ込みは要らない。
    ///
    /// 実行順を -20 に置くのは <see cref="DriveWorld"/> と同じ理由で、
    /// 道と沿道が並び直すのと同じフレームのうちに木も置き直すため
    /// </summary>
    [DefaultExecutionOrder(-20)]
    public sealed class FarTree : MonoBehaviour
    {
        [Tooltip("走った距離のうち、z へ与える割合。1/20 なら 20 m 走って 1 m 動く。" +
            "小さいほど遠くのものに見えるが、0 にすると空に貼り付いた書き割りになる")]
        [SerializeField] float parallax = 1f / 20f;

        [Tooltip("出てくる z。m。ここから手前へ向かって動く")]
        [SerializeField] float from = 148f;

        [Tooltip("ここまで来たら from へ戻す z。m。**背中まで通り過ぎた先に取ること。**" +
            "画面に残っているところで戻すと、木が跳ぶのがそのまま見える")]
        [SerializeField] float until = -212f;

        DriveWorld world;

        void OnEnable()
        {
            world = GetComponentInParent<DriveWorld>();
            if (world == null) world = FindAnyObjectByType<DriveWorld>();
            if (world == null) { Debug.LogWarning("FarTree: DriveWorld が見つからない。木は動かない", this); return; }
            Place(world.Travelled);
        }

        void Update()
        {
            if (world != null) Place(world.Travelled);
        }

        /// <summary>
        /// 走った距離から z を決める。戻す幅で畳むので、一巡しても並びは繋がったまま。
        /// 外から呼べるようにしてあるのは、再生せずに絵を撮って視差を確かめるため
        /// </summary>
        public void Place(float travelled)
        {
            var span = from - until;
            if (span <= 0.001f) return;
            var gone = travelled * parallax;
            gone -= Mathf.Floor(gone / span) * span;
            var at = transform.localPosition;
            transform.localPosition = new Vector3(at.x, at.y, from - gone);
        }
    }
}
