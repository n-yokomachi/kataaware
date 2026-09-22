using TMPro;
using UnityEngine;

namespace HalfAware
{
    /// <summary>
    /// 人の脇に浮く板。一行目にその人の行、二行目に `潜る　　　切断`。
    ///
    /// **`E` は板に書かない。** 鍵の案内は画面の下の `E ○○` が持っている
    /// （場面 1・2・3・8 と同じ場所・同じ書式で <see cref="DiveDirector"/> が出す）。
    /// 板にも書くと、一つの操作に `E` が二つ出て、どちらを押す話なのか読めなくなる。
    /// 板に残すのは、誰の脇に出ているかと、`切断` がどれだけ育ったかだけ。
    ///
    /// **Quad も 3D の TextMeshPro も法線が -z。** どちらも -z の側から見たときに
    /// 表が見えるので、目の方へ向けるには forward を目から離す向きに置く。
    /// 表を向けるつもりで目の方へ forward を向けると、板も字もまとめて裏になって消える
    /// （場面 3 の窓で踏んだ）。
    ///
    /// いつ出していつ消すかは DiveDirector が決める。板は言われたとおりに出るだけ
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
        [Tooltip("肩の高さ。相手の背丈に対する割合")]
        [SerializeField] float shoulder = 0.82f;
        // 体の縁と板の縁のあいだ。狭いのは、寄るほど体の幅が角度を食って
        // 板の外の縁が画面の右へはみ出すため。0.05 で 1.65 m まで収まる
        [Tooltip("相手の体の縁と板の縁のあいだ。m")]
        [SerializeField] float gap = 0.05f;
        [Tooltip("目からの距離 1 m あたりの板の大きさ。遠近で見かけの大きさを揃える。" +
            "0.82 で、板の丈が画面の縦のおよそ 19 %（427×240 で 42 px）になる")]
        [SerializeField] float perMetre = 0.82f;
        [Tooltip("大きさの下限と上限")]
        [SerializeField] float least = 0.45f;
        // **上限はそのまま「見かけの大きさが揃う距離」。** 2.20 だと 2.7 m から先は
        // 板ごと縮んで字が潰れ、部屋の向こう側の人が読めなかった。2.60 で 3.2 m まで伸びる。
        // これ以上伸ばすと板の実寸が 2.5 m を超えて、廊下の壁を突き抜けたところが欠ける
        [SerializeField] float most = 2.60f;

        Transform host;
        /// <summary>相手の体の高さと半幅。Show のときに一度だけ測る</summary>
        float tall = 1.7f;
        float half = 0.25f;
        /// <summary>板そのものの幅。m。寄せ幅を出すのに要る。Pane の大きさが唯一の出どころ</summary>
        float wide = 0.92f;
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
            var pane = transform.Find("Pane");
            if (pane != null) wide = pane.localScale.x;
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
            Measure(beside);
            line = Brief(string.IsNullOrEmpty(targetRow) ? row : targetRow);
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

        /// <summary>
        /// 相手の体を測る。背丈も幅も模型と縮尺でまちまちで、
        /// 高さを決め打ちにすると子どもの脇では頭の上へ大きく浮く
        /// </summary>
        void Measure(Transform beside)
        {
            tall = 1.7f;
            half = 0.25f;
            if (beside == null) return;
            var parts = beside.GetComponentsInChildren<Renderer>();
            if (parts.Length == 0) return;
            var box = parts[0].bounds;
            for (var i = 1; i < parts.Length; i++) box.Encapsulate(parts[i].bounds);
            tall = Mathf.Max(0.4f, box.size.y);
            half = Mathf.Max(0.1f, Mathf.Max(box.size.x, box.size.z) * 0.5f);
        }

        /// <summary>
        /// 肩の脇へ置く。設計書 5 節の「肩の脇」。
        ///
        /// **頭の上には置かない。** 二 m ほどまで寄ると、頭の上の板が右上の行
        /// （いま潜っている人の行）と重なり、端末の緑どうしが二重になって
        /// どちらも読めなかった（オーナーの差し戻し）
        /// </summary>
        void Place()
        {
            if (host == null || eye == null) return;
            var at = host.position + Vector3.up * (tall * shoulder);
            var away = at - eye.position;
            if (away.sqrMagnitude < 1e-6f) return;
            // **見かけの大きさを揃える。** 寄られると画面の半分を覆い、
            // 離れると行が読めなくなる。目からの距離に比例させれば、どちらも起きない。
            // 測るのは肩までの距離。板の位置から測ると、寄せ幅と大きさが互いを押し合う
            var span = Mathf.Clamp(away.magnitude * perMetre, least, most);
            // 寄せるのは目から見た真横で、いつも主の右。
            // 相手の向きで寄せる側を決めると、横を向いた人では板が顔の前か後ろへ回り込み、
            // 相手の周りを歩くと左右が入れ替わる瞬間に板が飛ぶ。
            // いつも同じ側に出るなら、どこを見れば読めるかが決まっている
            var flat = eye.right;
            flat.y = 0f;
            if (flat.sqrMagnitude < 1e-6f) flat = host.right;
            flat.Normalize();
            // 板の内側の縁が体に掛からないところまで出す。
            // 体の幅は相手ごとに、板の幅は遠近で変わるので、どちらも数に入れる
            transform.position = at + flat * (half + gap + wide * span * 0.5f);
            transform.rotation = Quaternion.LookRotation(
                (transform.position - eye.position).normalized, Vector3.up);
            transform.localScale = Vector3.one * span;
        }

        /// <summary>
        /// 板に出すぶんだけ切り出す。行は「性別　年齢　『名前』　日付 時刻」の形だが、
        /// 板は相手の目の前に浮く小さな面で、二十数文字を流し込むと一字が数 px になって潰れる。
        /// 日付と時刻は右上の行が出しているので、板は名前までで足りる
        /// </summary>
        static string Brief(string row)
        {
            if (string.IsNullOrEmpty(row)) return "";
            var shut = row.IndexOf('』');
            return shut < 0 ? row : row.Substring(0, shut + 1);
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
            return (index == 0 ? Choice.Cursor : "") + Dive + "　　　" + cut;
        }
    }
}
