using UnityEngine;

namespace HalfAware
{
    /// <summary>
    /// 蝶番で開く車のドア。**自分では動かない。** 段取り（DriveDirector）が
    /// 0（閉）から 1（開）までの割合を毎フレーム渡し、ここはそれを板の角へ直す。
    ///
    /// <see cref="Wipers"/> と同じ構えで、外から値を渡せば再生していなくても板が動く。
    /// 絵を撮るときもエディタで開き具合を見るときも、<see cref="Set"/> を直に呼べばよい。
    ///
    /// **付けるのは蝶番の側の入れ物で、板そのものではない。** 板は蝶番の子に置いてあり、
    /// この入れ物が回ると板が前の縁を軸にして振れる。板を直に回すと、板の真ん中が
    /// 軸になって前半分が車体へ潜る。
    ///
    /// <see cref="side"/> は蝶番の付いている側で、運転席（+x）が 1、助手席が -1。
    /// どちらの側でも「後ろの縁が外へ出る」向きに開く。y 回りの回転は +z を +x へ倒すので、
    /// 軸より後ろ（-z）にある縁を +x へ出すには負の角が要る。そこで側の符号を掛けて反す
    /// </summary>
    [DefaultExecutionOrder(-14)]
    public sealed class CarDoor : MonoBehaviour
    {
        /// <summary>
        /// 開ききったときの角。度。
        ///
        /// 実物のドアは 70 度近くまで開くが、この場面では開いたドアの脇を通って
        /// 乗り込むので、開きすぎると板が目の中を薙いでいく。板の丈は 1.64 m あり、
        /// 55 度で後ろの縁が車の中心から 2.31 m のところまで出る。
        /// これ以上開けても「開いている」ことの読めかたは変わらない
        /// </summary>
        public const float Swing = 55f;

        [Tooltip("開ききったときの角。度")]
        [SerializeField] float swing = Swing;
        [Tooltip("蝶番の付いている側。運転席（+x）が 1、助手席が -1")]
        [SerializeField] float side = 1f;
        [Tooltip("開き具合。0 が閉、1 が開ききり")]
        [SerializeField, Range(0f, 1f)] float open;

        /// <summary>開き具合。0 が閉、1 が開ききり</summary>
        public float Open
        {
            get { return open; }
            set { Set(value); }
        }

        /// <summary>今の板の角。度。閉じているときが 0</summary>
        public float Angle { get { return swing * open; } }

        void OnEnable()
        {
            Set(open);
        }

        /// <summary>
        /// 板を開き具合 amount のところへ据える。0 が閉、1 が開ききり。
        /// 範囲の外は丸める。段取りの側で緩急を付けた値をそのまま渡せる
        /// </summary>
        public void Set(float amount)
        {
            open = Mathf.Clamp01(amount);
            var way = side < 0f ? -1f : 1f;
            transform.localRotation = Quaternion.Euler(0f, -way * swing * open, 0f);
        }
    }
}
