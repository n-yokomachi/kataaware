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
    /// いつ出していつ消すかは DiveDirector が決める。板は言われたとおりに出るだけ。
    ///
    /// **潜れない人の板**（<see cref="Lock"/>、場面 6 の女性）は、二行目を薄い色の「潜れない」一つにする。
    /// **画面の真ん中の下の案内を避ける**（<see cref="AvoidPrompt"/>）。案内の帯に掛かれば、帯の上へ持ち上げる
    /// </summary>
    public sealed class HoloPanel : MonoBehaviour
    {
        public const string Dive = "潜る";
        public const string Cut = "切断";

        /// <summary>
        /// 板を描く層。**板は後処理（記憶ごとのぼやけ・色味・光のにじみ）の後に描く**（2026-09-27）。
        /// 板は主の端末が描くもので、借りた目の出来には従わない（設計書 5 節）。景色と一緒に描いていた頃は、
        /// 年寄りの近くぼやけ（1.6 m から）と目の疲れのぼやけが、板の字まで溶かした。板は透けるので深さを書かず、
        /// ぼやけは板の後ろの景色の深さで掛かっていた。
        /// この層は描き手（PC_Renderer・Mobile_Renderer）の不透明と半透明の描画から外し、
        /// RenderObjects の「Holo」が後処理の後・減色（Ps1）の前に描く。後処理の後には深さが無いので、板は景色の上に出る。
        /// 眩暈（Daze、後処理の前）も板には掛からない
        /// </summary>
        public const string LayerName = "Holo";

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
        // これ以上伸ばすと板の実寸が 2.5 m を超えて、廊下の壁を突き抜けたところが欠ける。
        // それより遠い人の板は、同じ向きのまま目の側へ引いて、この大きさで置く（Beside）
        [SerializeField] float most = 2.60f;

        Transform host;
        /// <summary>相手の体の高さと半幅。Show のときに一度だけ測る</summary>
        float tall = 1.7f;
        float half = 0.25f;
        /// <summary>板そのものの幅。m。寄せ幅を出すのに要る。Pane の大きさが唯一の出どころ</summary>
        float wide = 0.92f;
        /// <summary>板そのものの丈。m。物に埋まっていないか測るのに要る</summary>
        float high = 0.30f;
        /// <summary>いま出している側。1 が主の右、-1 が左。毎フレーム選び直すと左右に飛ぶ</summary>
        float side = 1f;
        /// <summary>主の体。板が目の近くまで引かれたとき、自分の当たりを物と数えないため</summary>
        Collider mine;
        readonly Collider[] caught = new Collider[8];
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
            if (pane != null) { wide = pane.localScale.x; high = pane.localScale.y; }
            if (eye != null) mine = eye.GetComponentInParent<Collider>();
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
            locked = null;
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
            locked = null;
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

        /// <summary>
        /// 潜れない人の板にする。二行目を、潜る・切断の代わりに薄い色の text（<see cref="Locked"/>）一つにする。▶ は付けない。
        /// 次に <see cref="Show"/> か <see cref="Hide"/> を呼ぶまで続く
        /// </summary>
        public void Lock(string text)
        {
            locked = text;
            index = 0;
            Paint();
        }

        /// <summary>潜れない人の板の二行目（場面 6 の庭の記憶の女性。記憶を失くした主人公なので潜れない）</summary>
        public const string Locked = "潜れない";

        /// <summary>潜れない板にしているか</summary>
        public bool IsLocked { get { return !string.IsNullOrEmpty(locked); } }

        string locked;

        /// <summary>人が動けば板も一緒に動くので、人を動かし終えた後に置き直す</summary>
        void LateUpdate()
        {
            Place();
        }

        /// <summary>いま置き直す。再生せずに撮るときに呼ぶ（LateUpdate が回らないので）</summary>
        public void PlaceNow()
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
            // 寄せるのは目から見た真横。
            // 相手の向きで寄せる側を決めると、横を向いた人では板が顔の前か後ろへ回り込み、
            // 相手の周りを歩くと左右が入れ替わる瞬間に板が飛ぶ
            var flat = eye.right;
            flat.y = 0f;
            if (flat.sqrMagnitude < 1e-6f) flat = host.right;
            flat.Normalize();

            // **いま出している側を先に試す。** 毎フレーム左右を選び直すと、
            // 壁際を歩くあいだ板が右と左を行き来して読めない
            if (Settle(at, flat, side)) { AvoidPrompt(); return; }
            if (Settle(at, flat, -side)) { side = -side; AvoidPrompt(); return; }
            Pull(at, flat);
            AvoidPrompt();
        }

        /// <summary>
        /// 肩 at の which の側の置き場と大きさ。
        ///
        /// **見かけの大きさを揃える。** 寄られると画面の半分を覆い、
        /// 離れると行が読めなくなる。目からの距離に比例させれば、どちらも起きない。
        /// 測るのは肩までの距離。板の位置から測ると、寄せ幅と大きさが互いを押し合う。
        /// 板の内側の縁が体に掛からないところまで出す。体の幅は相手ごとに、板の幅は遠近で変わるので、どちらも数に入れる。
        ///
        /// **遠い人の板も縮めない。** 上限（<see cref="most"/>、3.2 m）より遠い人の脇にそのまま置くと、板は距離なりに縮み、
        /// 7 m 先の公園の少女では粗い画面で数画素になって、街灯の陰で読めなかった（2026-09-27、「ソフィアから次の人に飛ぶことができない」）。
        /// 遠い人の脇の置き場を、目から見た向きのまま目の側へ引き、上限の大きさで置く。画面の上では同じ所（相手の肩の脇）に、
        /// 3.2 m の人と同じ見かけの大きさで出る
        /// </summary>
        void Beside(Vector3 at, Vector3 flat, float which, out Vector3 pos, out float span)
        {
            var want = Mathf.Max(least, (at - eye.position).magnitude * perMetre);
            span = Mathf.Min(want, most);
            pos = at + flat * which * (half + gap + wide * want * 0.5f);
            if (want <= most) return;
            pos = eye.position + (pos - eye.position) * (most / want);
        }

        /// <summary>その側へ出せるなら出して true</summary>
        bool Settle(Vector3 at, Vector3 flat, float which)
        {
            Vector3 pos;
            float span;
            Beside(at, flat, which, out pos, out span);
            if (!Clear(pos, span)) return false;
            Put(pos, span);
            return true;
        }

        /// <summary>
        /// 左右どちらも塞がっているときに、目の側へ引いてくる。
        ///
        /// **相手の脇に留めるより、読めることを採る。** 廊下や部屋の中では、
        /// 肩の脇に出した板が壁や箪笥に食い込んで、行が半分欠けた（オーナーの差し戻し）。
        /// 引いた先で距離に合わせて大きさを取り直すので、見かけの大きさは変わらない
        /// </summary>
        void Pull(Vector3 at, Vector3 flat)
        {
            Vector3 want;
            float span;
            Beside(at, flat, side, out want, out span);
            var back = eye.position - want;
            var reach = back.magnitude;
            if (reach > 1e-4f)
            {
                back /= reach;
                for (var k = 1; k <= Tries; k++)
                {
                    var pos = want + back * (reach * k / (Tries + 1f));
                    var near = Mathf.Clamp((pos - eye.position).magnitude * perMetre, least, most);
                    if (!Clear(pos, near)) continue;
                    Put(pos, near);
                    return;
                }
            }
            // どこも空いていなければ目のすぐ前に出す。埋まって読めないよりはまし
            var edge = want - eye.position;
            var head = edge.sqrMagnitude > 1e-6f ? edge.normalized : eye.forward;
            Put(eye.position + head * Close, Mathf.Clamp(Close * perMetre, least, most));
        }

        /// <summary>目から見えて、なおかつ物に食い込んでいないか</summary>
        bool Clear(Vector3 pos, float span)
        {
            var rot = Facing(pos);
            var box = new Vector3(wide * span * 0.5f, high * span * 0.5f, Thin);
            // 記憶ごとの見えない囲い（Ignore Raycast の層）は板を押し退けない。DiveDirector の光線と同じ層だけを見る
            var n = Physics.OverlapBoxNonAlloc(pos, box, caught, rot, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            for (var i = 0; i < n; i++)
                if (caught[i] != null && caught[i] != mine) return false;

            var away = pos - eye.position;
            var reach = away.magnitude - Thin;
            if (reach <= 0f) return true;
            RaycastHit hit;
            if (!Physics.Raycast(eye.position, away / (reach + Thin), out hit, reach, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                return true;
            return hit.collider == mine;
        }

        void Put(Vector3 pos, float span)
        {
            transform.position = pos;
            transform.rotation = Facing(pos);
            transform.localScale = Vector3.one * span;
        }

        // ---- 画面の下の案内を避ける ------------------------------------------------
        //
        // 板は肩の脇に浮くので、目を留めた人を画面の真ん中に置くと、板の二行目がちょうど画面の真ん中の下の案内
        // （「E/(左クリック)　この人の記憶へ潜る」など、HudView の Prompt）の高さに来て、字が重なって読めなかった（2026-09-28）。
        // 置いた後で板の画面の上の四角を測り、案内の出る帯に掛かれば、帯の上の縁の上まで目から見た上へ持ち上げる

        /// <summary>
        /// 案内の出る帯。画面の割合（左下が 0）。HudView の案内（Prompt）は、基準 1280×720 の真ん中から 40 下に、幅 800・高さ 40 で置く
        /// </summary>
        public static readonly Rect PromptBand = new Rect(0.5f - 400f / 1280f, 0.5f - 60f / 720f, 800f / 1280f, 40f / 720f);

        /// <summary>帯から離す余白。画面の高さの割合</summary>
        public const float PromptMargin = 0.012f;

        /// <summary>
        /// 板の画面の上の四角 board が帯 band に掛かっていれば、板を上へどれだけ持ち上げれば帯の上の縁（と余白）を越えるか。画面の高さの割合。
        /// 掛かっていなければ 0
        /// </summary>
        public static float Lift(Rect board, Rect band, float margin)
        {
            if (board.xMax <= band.xMin || board.xMin >= band.xMax) return 0f;
            if (board.yMax <= band.yMin - margin || board.yMin >= band.yMax + margin) return 0f;
            return band.yMax + margin - board.yMin;
        }

        /// <summary>置いた板が案内の帯に掛かっていれば、目から見た上へ持ち上げる</summary>
        void AvoidPrompt()
        {
            if (lens == null && eye != null) lens = eye.GetComponent<Camera>();
            if (lens == null) return;
            var span = transform.localScale.x;
            var half = new Vector2(wide * span * 0.5f, high * span * 0.5f);
            var min = new Vector2(float.MaxValue, float.MaxValue);
            var max = new Vector2(float.MinValue, float.MinValue);
            for (var i = 0; i < 4; i++)
            {
                var corner = transform.position + transform.right * (i % 2 == 0 ? -half.x : half.x) + transform.up * (i < 2 ? -half.y : half.y);
                var v = lens.WorldToViewportPoint(corner);
                if (v.z <= 0f) return;
                min = Vector2.Min(min, v);
                max = Vector2.Max(max, v);
            }
            var lift = Lift(Rect.MinMaxRect(min.x, min.y, max.x, max.y), PromptBand, PromptMargin);
            if (lift <= 0f) return;
            // 画面の高さの割合を、板の所の深さでの長さへ
            var depth = Vector3.Dot(transform.position - lens.transform.position, lens.transform.forward);
            var tall = 2f * depth * Mathf.Tan(lens.fieldOfView * 0.5f * Mathf.Deg2Rad);
            transform.position += lens.transform.up * (lift * tall);
            transform.rotation = Facing(transform.position);
        }

        Camera lens;

        /// <summary>目から離れる向きへ forward を置く。Quad も 3D の字も -z から見て表だから</summary>
        Quaternion Facing(Vector3 pos)
        {
            var away = pos - eye.position;
            if (away.sqrMagnitude < 1e-6f) return transform.rotation;
            return Quaternion.LookRotation(away.normalized, Vector3.up);
        }

        /// <summary>板の厚みの半分。面には厚みが無いので、測るときだけ持たせる</summary>
        const float Thin = 0.02f;
        /// <summary>目の側へ引くときに試す刻みの数</summary>
        const int Tries = 6;
        /// <summary>最後の逃げ場。目からこれだけ前へ置く</summary>
        const float Close = 0.7f;

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
            if (IsLocked) return "<color=#" + ColorUtility.ToHtmlStringRGB(Dead) + ">" + locked + "</color>";
            var cut = "<size=" + Mathf.RoundToInt(Mathf.Clamp01(size) * 100f) + "%>"
                + (index == 1 ? Choice.Cursor : "") + Cut + "</size>";
            if (!Ready) cut = "<color=#" + ColorUtility.ToHtmlStringRGB(Dead) + ">" + cut + "</color>";
            return (index == 0 ? Choice.Cursor : "") + Dive + "　　　" + cut;
        }
    }
}
