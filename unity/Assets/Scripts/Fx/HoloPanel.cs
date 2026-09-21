using TMPro;
using UnityEngine;

namespace HalfAware
{
    /// <summary>
    /// 人の脇に浮く板。一行目にその人の行、二行目に `E 潜る　　　切断`。
    ///
    /// **Quad も 3D の TextMeshPro も法線が -z。** どちらも -z の側から見たときに
    /// 表が見えるので、目の方へ向けるには forward を目から離す向きに置く。
    /// 表を向けるつもりで目の方へ forward を向けると、板も字もまとめて裏になって消える
    /// （場面 3 の窓で踏んだ）。
    ///
    /// 出るまでと消えるまでの半秒は DiveDirector が数える。板は言われたとおりに出るだけ。
    /// 位置も色も秒数もオーナーが決めるので、ここの値は仮置き
    /// </summary>
    public sealed class HoloPanel : MonoBehaviour
    {
        public const string Dive = "潜る";
        public const string Cut = "切断";

        /// <summary>押せないあいだの `切断` の色。端末の緑から彩りを抜いたもの</summary>
        static readonly Color Dead = new Color(0.46f, 0.50f, 0.47f);

        [Tooltip("主の目。板はここへ表を向ける")]
        [SerializeField] Transform eye;
        [Tooltip("一行目。その人の行")]
        [SerializeField] TMP_Text rowText;
        [Tooltip("二行目。操作")]
        [SerializeField] TMP_Text actionText;
        [Tooltip("肩の脇へどれだけ寄せるか。m")]
        [SerializeField] float side = 0.35f;
        [Tooltip("足元からの高さ。m")]
        [SerializeField] float height = 1.4f;

        Transform host;
        string line = "";
        float size = DiveChain.CutStart;
        int index;

        /// <summary>いま出ているか</summary>
        public bool Showing { get { return host != null; } }

        /// <summary>`切断` が `潜る` と同じ大きさになったか</summary>
        public bool Ready { get { return size >= 1f - 1e-4f; } }

        /// <summary>選んでいる方。0 が `潜る`、1 が `切断`</summary>
        public int Index { get { return index; } }

        /// <summary>二行目に出している文字列</summary>
        public string Action { get { return Compose(); } }

        void Awake()
        {
            Paint();
        }

        /// <summary>
        /// beside の脇に出す。一行目に出すのは飛び先の行で、
        /// 板は「この人へ潜るか」を訊くものだから。飛び先が無いときだけ、
        /// いま借りている体の行で代える
        /// </summary>
        public void Show(Transform beside, string row, string targetRow)
        {
            host = beside;
            line = string.IsNullOrEmpty(targetRow) ? (row ?? "") : targetRow;
            if (!gameObject.activeSelf) gameObject.SetActive(true);
            Place();
            Paint();
        }

        public void Hide()
        {
            host = null;
            index = 0;
            if (gameObject.activeSelf) gameObject.SetActive(false);
        }

        /// <summary>二行目の `切断` の大きさ。0.4 で灰色、1 で緑</summary>
        public void Grow(float size)
        {
            this.size = Mathf.Clamp(size, DiveChain.CutStart, 1f);
            if (!Ready) index = 0;
            Paint();
        }

        /// <summary>
        /// 0 で `潜る`、1 で `切断`。大きさが揃うまでは呼ばれても `潜る` のまま。
        /// 選べるように見せると、押しても何も起きない操作を覚えさせてしまう
        /// </summary>
        public void Select(int index)
        {
            this.index = Ready && index == 1 ? 1 : 0;
            Paint();
        }

        /// <summary>人が動けば板も一緒に動くので、人を動かし終えた後に置き直す</summary>
        void LateUpdate()
        {
            Place();
        }

        void Place()
        {
            if (host == null) return;
            transform.position = host.position + host.right * side + Vector3.up * height;
            if (eye == null) return;
            var away = transform.position - eye.position;
            if (away.sqrMagnitude < 1e-6f) return;
            transform.rotation = Quaternion.LookRotation(away.normalized, Vector3.up);
        }

        void Paint()
        {
            if (rowText != null) rowText.text = line;
            if (actionText != null) actionText.text = Compose();
        }

        string Compose()
        {
            var cut = "<size=" + Mathf.RoundToInt(Mathf.Clamp01(size) * 100f) + "%>"
                + (index == 1 ? Choice.Cursor : "") + Cut + "</size>";
            if (!Ready) cut = "<color=#" + ColorUtility.ToHtmlStringRGB(Dead) + ">" + cut + "</color>";
            return "E " + (index == 0 ? Choice.Cursor : "") + Dive + "　　　" + cut;
        }
    }
}
