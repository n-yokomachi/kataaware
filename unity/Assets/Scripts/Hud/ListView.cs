using TMPro;
using UnityEngine;

namespace HalfAware
{
    /// <summary>
    /// リストの枠（オーナー、2026-09-28「モニターの抽出条件や、メモリのリスト、売買のメモなど、リストで表示する項目は、
    /// 選択画面のように画面中央に枠で表示するように」）。
    ///
    /// 字幕のページのうち、並びになっているもの（<see cref="ListFormat.IsList"/>）は、字幕の窓に出さず、画面の真ん中の枠に表で出す。
    /// 枠は二択の札（<see cref="ChoiceView"/>）と同じ板（暗い地・青緑の細い枠・左上と右下の鉤）で、同じ所に浮かべ、
    /// HUD と一緒に粗い画面（<see cref="UiLens"/>）で描く。表の列の揃えは <see cref="ListFormat"/> のまま（行ごとには寄せず、枠を表の幅に合わせる。枠の幅の下限より狭い表は、表の塊のまま真ん中へ寄せる）。
    /// 送れる時だけ、枠の中の右下に送りの印（「E/」＋左クリックのアイコン、<see cref="HudView.Advance"/>）を出す。
    ///
    /// **見た目はここで組む。** シーンにもプレハブにも置かない。<see cref="HudView"/> が初めてリストのページを出すときに、
    /// HUD の Canvas の中に作る。コンソールを開いている間は HUD と一緒に伏せる。寸法は <see cref="ListLayout"/>
    /// </summary>
    public sealed class ListView
    {
        public const string RootName = "List";

        /// <summary>送りの印の色。字幕の送りの印と同じ（白を 0.6 の濃さで）</summary>
        static readonly Color HintColor = ImplantConsole.Tint(new Color(1f, 1f, 1f, 0.6f));

        readonly RectTransform root;
        TMP_Text table;
        TMP_Text hint;
        ListLayout.Frame frame;
        /// <summary>いま並べている文。同じ文のあいだは並べ直さない</summary>
        string laid;

        /// <summary>板。HUD の Canvas の子</summary>
        public RectTransform Root { get { return root; } }

        /// <summary>いまの並び。矩形は板の中の座標</summary>
        public ListLayout.Frame Frame { get { return frame; } }

        /// <summary>表の字。動作確認から読む</summary>
        public TMP_Text Table { get { return table; } }

        /// <summary>送りの印を出しているか。動作確認から読む</summary>
        public bool Advancing { get { return hint != null && hint.gameObject.activeSelf; } }

        public bool Visible
        {
            get { return root.gameObject.activeSelf; }
            set { if (root.gameObject.activeSelf != value) root.gameObject.SetActive(value); }
        }

        ListView(RectTransform root)
        {
            this.root = root;
        }

        /// <summary>parent（HUD の Canvas）の中に枠を組む。伏せた状態で返す</summary>
        public static ListView Build(RectTransform parent)
        {
            var r = ChoiceView.Board(parent, RootName);
            r.anchoredPosition = new Vector2(0f, ListLayout.Lift);
            var view = new ListView(r);
            view.Make();
            view.Visible = false;
            return view;
        }

        void Make()
        {
            table = ChoiceView.Text(root, "Table", ListLayout.RowFont, ChoiceView.Pale);
            // 列は pos で置くので、左に寄せる
            table.alignment = TextAlignmentOptions.TopLeft;
            hint = ChoiceView.Text(root, "Hint", ListLayout.HintFont, HintColor);
            hint.alignment = TextAlignmentOptions.BottomRight;
            hint.text = HudView.Advance;
            hint.gameObject.SetActive(false);
        }

        /// <summary>
        /// text（並びになっている文）を表に組んで出す。文が変わった時だけ並べ直す。毎フレーム呼んでよい。
        /// advance が true の時だけ送りの印を出す（演出で止まっている間は false）。伏せるのは <see cref="Visible"/>
        /// </summary>
        public void Show(string text, bool advance)
        {
            if (text == null) return;
            if (text != laid) Lay(text);
            if (hint.gameObject.activeSelf != advance) hint.gameObject.SetActive(advance);
        }

        void Lay(string text)
        {
            // 伏せたままの板の中で作った字は、Awake を通らず幅を正しく測れない。並べる間だけ起こす
            var hidden = !root.gameObject.activeSelf;
            if (hidden) root.gameObject.SetActive(true);
            try
            {
                Arrange(text);
            }
            finally
            {
                if (hidden) root.gameObject.SetActive(false);
            }
        }

        void Arrange(string text)
        {
            laid = text;
            // ルビは列を組んでから書式に直す。先に直すと、列の幅がタグを字数に数えてしまう
            var composed = Ruby.Expand(ListFormat.Compose(text, ListLayout.RoomEm, false));
            // ルビ（傍点）のある表は行を少し開ける。そのままだと下の行のルビが上の行の字にかぶる
            table.lineSpacing = Ruby.Has(text) ? Ruby.ExtraLineSpacing : 0f;
            table.text = composed;
            var size = table.GetPreferredValues(composed);
            var mark = hint.GetPreferredValues(HudView.Advance);
            frame = ListLayout.Lay(size.x, size.y, mark.x);
            root.sizeDelta = frame.size;
            ChoiceView.Place(table.rectTransform, frame.table);
            ChoiceView.Place(hint.rectTransform, frame.hint);
        }
    }
}
