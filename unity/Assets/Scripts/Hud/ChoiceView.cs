using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HalfAware
{
    /// <summary>
    /// 二択の札（設計書 2 節、案 3「視線の先に浮かぶ札」）。字幕の枠には出さず、画面の真ん中に浮かべる。
    ///
    /// - 問いを出し、その下に細い線を引く
    /// - 選ぶ物は、四隅の括弧で囲んだ札にして横に並べる。選んでいる札は薄い青緑で塗り、字と括弧を明るくする
    /// - 問いと札を、半ば透ける暗い板で囲む。板にはコンソールと同じ青緑の細い枠と、左上と右下の鉤
    ///
    /// **見た目はここで組む。** シーンにもプレハブにも置かない。<see cref="HudView"/> が初めて二択を出すときに、
    /// HUD の Canvas の中に作る。HUD と一緒に粗い画面（<see cref="UiLens"/>）で描かれ、
    /// コンソールを開いている間は HUD と一緒に伏せる。寸法と当たりは <see cref="ChoiceLayout"/>。
    /// 色は案の CSS のまま。薄い色の重ねはコンソールと同じく <see cref="ImplantConsole.Tint"/>・<see cref="ImplantConsole.Veil"/> で濃さを合わせる
    /// </summary>
    public sealed class ChoiceView
    {
        public const string RootName = "Choice";

        static readonly Color Accent = Rgb(0x7f, 0xe3, 0xec, 1f);
        static readonly Color Pale = Rgb(0xcf, 0xf7, 0xfa, 1f);
        static readonly Color Fill = ImplantConsole.Veil(Rgb(3, 10, 14, 0.62f));
        static readonly Color Edge = ImplantConsole.Tint(Rgb(127, 227, 236, 0.28f));
        static readonly Color Rule = ImplantConsole.Tint(Rgb(127, 227, 236, 0.35f));
        static readonly Color Bracket = ImplantConsole.Tint(Rgb(127, 227, 236, 0.7f));
        static readonly Color Chosen = ImplantConsole.Tint(Rgb(127, 227, 236, 0.22f));
        static readonly Color Clear = new Color(0f, 0f, 0f, 0f);

        /// <summary>板の鉤の一辺</summary>
        const float HookSize = 8f * ChoiceLayout.Dot;
        /// <summary>札の括弧の腕の長さ。CSS の 6 px</summary>
        const float Arm = 5f * ChoiceLayout.Dot;
        const float Line = 1f * ChoiceLayout.Dot;

        readonly RectTransform root;
        readonly List<CardView> cards = new List<CardView>();
        TMP_Text question;
        RectTransform rule;
        ChoiceLayout.Frame frame;
        /// <summary>いま並べている問い。同じ問いのあいだは並べ直さない</summary>
        Choice laid;
        int painted = -1;

        /// <summary>板。HUD の Canvas の子</summary>
        public RectTransform Root { get { return root; } }

        /// <summary>いまの並び。矩形は板の中の座標</summary>
        public ChoiceLayout.Frame Frame { get { return frame; } }

        /// <summary>i 番の札。並べていなければ null</summary>
        public RectTransform Card(int i)
        {
            return i >= 0 && i < cards.Count && cards[i].rect.gameObject.activeSelf ? cards[i].rect : null;
        }

        public bool Visible
        {
            get { return root.gameObject.activeSelf; }
            set { if (root.gameObject.activeSelf != value) root.gameObject.SetActive(value); }
        }

        static Color Rgb(int r, int g, int b, float a)
        {
            return new Color(r / 255f, g / 255f, b / 255f, a);
        }

        ChoiceView(RectTransform root)
        {
            this.root = root;
        }

        /// <summary>parent（HUD の Canvas）の中に板を組む。伏せた状態で返す</summary>
        public static ChoiceView Build(RectTransform parent)
        {
            var r = Rect(parent, RootName);
            r.anchorMin = new Vector2(0.5f, 0.5f);
            r.anchorMax = new Vector2(0.5f, 0.5f);
            r.pivot = new Vector2(0.5f, 0.5f);
            r.anchoredPosition = new Vector2(0f, ChoiceLayout.Lift);
            var view = new ChoiceView(r);
            view.Make();
            view.Visible = false;
            return view;
        }

        void Make()
        {
            var bg = root.gameObject.AddComponent<Image>();
            bg.color = Fill;
            bg.raycastTarget = false;
            Border(root, Edge, Line);
            Hook(root, 0f, 1f);
            Hook(root, 1f, 0f);
            question = Text(root, "Question", ChoiceLayout.QuestionFont, Pale);
            question.characterSpacing = ChoiceLayout.QuestionSpacing;
            rule = Solid(root, "Rule", Rule).rectTransform;
        }

        /// <summary>
        /// choice を出す。問いが変わった時だけ並べ直し、選びが変わった時だけ塗り直す。
        /// 毎フレーム呼んでよい。null なら何もしない（伏せるのは <see cref="Visible"/>）
        /// </summary>
        public void Show(Choice choice)
        {
            if (choice == null) return;
            if (choice != laid) Lay(choice);
            if (choice.Index != painted) Paint(choice.Index);
        }

        void Lay(Choice choice)
        {
            // 伏せたままの板の中で作った字は、Awake を通らず幅を正しく測れない。並べる間だけ起こす
            var hidden = !root.gameObject.activeSelf;
            if (hidden) root.gameObject.SetActive(true);
            try
            {
                Arrange(choice);
            }
            finally
            {
                if (hidden) root.gameObject.SetActive(false);
            }
        }

        void Arrange(Choice choice)
        {
            laid = choice;
            painted = -1;
            question.text = Ruby.Expand(choice.Question);
            while (cards.Count < choice.Count) cards.Add(MakeCard(cards.Count));
            var widths = new float[choice.Count];
            for (var i = 0; i < cards.Count; i++)
            {
                var on = i < choice.Count;
                if (cards[i].rect.gameObject.activeSelf != on) cards[i].rect.gameObject.SetActive(on);
                if (!on) continue;
                cards[i].label.text = Ruby.Expand(choice.Options[i]);
                widths[i] = cards[i].label.GetPreferredValues(cards[i].label.text).x;
            }
            var q = string.IsNullOrEmpty(question.text) ? 0f : question.GetPreferredValues(question.text).x;
            frame = ChoiceLayout.Lay(q, widths);
            root.sizeDelta = frame.size;
            Place(question.rectTransform, frame.question);
            Place(rule, frame.line);
            for (var i = 0; i < choice.Count; i++) Place(cards[i].rect, frame.cards[i]);
        }

        void Paint(int index)
        {
            painted = index;
            for (var i = 0; i < cards.Count; i++) cards[i].Paint(i == index);
        }

        /// <summary>
        /// 画面の座標 screen の下にある札の番号。無ければ -1。
        /// 粗い画面で描いているので、座標をそちらへ直してから、Canvas を描くカメラで板の中の座標へ移す
        /// </summary>
        public int At(Vector2 screen)
        {
            if (laid == null || !root.gameObject.activeInHierarchy) return -1;
            var canvas = root.GetComponentInParent<Canvas>();
            if (canvas == null) return -1;
            canvas = canvas.rootCanvas;
            var overlay = canvas.renderMode == RenderMode.ScreenSpaceOverlay;
            var cam = overlay ? null : canvas.worldCamera;
            var at = overlay ? screen : UiLens.ToLens(screen);
            Vector2 local;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(root, at, cam, out local)) return -1;
            return ChoiceLayout.Hit(frame, local);
        }

        // ---- 部品 ------------------------------------------------------------

        sealed class CardView
        {
            public RectTransform rect;
            public Image fill;
            public Image[] brackets;
            public TMP_Text label;

            public void Paint(bool on)
            {
                fill.color = on ? Chosen : Clear;
                label.color = on ? Color.white : Pale;
                for (var i = 0; i < brackets.Length; i++) brackets[i].color = on ? Accent : Bracket;
            }
        }

        CardView MakeCard(int index)
        {
            var c = new CardView();
            c.rect = Rect(root, "Card" + index);
            c.fill = c.rect.gameObject.AddComponent<Image>();
            c.fill.color = Clear;
            c.fill.raycastTarget = false;
            c.brackets = Brackets(c.rect);
            c.label = Text(c.rect, "Label", ChoiceLayout.CardFont, Pale);
            c.label.characterSpacing = ChoiceLayout.CardSpacing;
            Stretch(c.label.rectTransform);
            c.Paint(false);
            return c;
        }

        /// <summary>札の左右の括弧。縦の線と、上下へ内側に伸ばす短い腕</summary>
        static Image[] Brackets(RectTransform card)
        {
            var list = new Image[6];
            for (var side = 0; side < 2; side++)
            {
                var x = side == 0 ? 0f : 1f;
                var up = Solid(card, "Bracket" + side, Bracket);
                var r = up.rectTransform;
                r.anchorMin = new Vector2(x, 0f);
                r.anchorMax = new Vector2(x, 1f);
                r.pivot = new Vector2(x, 0.5f);
                r.anchoredPosition = Vector2.zero;
                r.sizeDelta = new Vector2(Line, 0f);
                list[side * 3] = up;
                for (var end = 0; end < 2; end++)
                {
                    var y = end == 0 ? 1f : 0f;
                    var arm = Solid(card, "Arm" + side + end, Bracket);
                    var a = arm.rectTransform;
                    a.anchorMin = new Vector2(x, y);
                    a.anchorMax = new Vector2(x, y);
                    a.pivot = new Vector2(x, y);
                    a.anchoredPosition = Vector2.zero;
                    a.sizeDelta = new Vector2(Arm, Line);
                    list[side * 3 + 1 + end] = arm;
                }
            }
            return list;
        }

        /// <summary>板の角の鉤。枠の線に重ねる。x・y は角（0 が左・下、1 が右・上）</summary>
        static void Hook(RectTransform panel, float x, float y)
        {
            var hook = Rect(panel, "Hook" + x + y);
            hook.anchorMin = new Vector2(x, y);
            hook.anchorMax = new Vector2(x, y);
            hook.pivot = new Vector2(x, y);
            hook.anchoredPosition = Vector2.zero;
            hook.sizeDelta = new Vector2(HookSize, HookSize);
            var across = Solid(hook, "Across", Accent).rectTransform;
            across.anchorMin = new Vector2(0f, y);
            across.anchorMax = new Vector2(1f, y);
            across.pivot = new Vector2(0.5f, y);
            across.anchoredPosition = Vector2.zero;
            across.sizeDelta = new Vector2(0f, Line);
            var down = Solid(hook, "Down", Accent).rectTransform;
            down.anchorMin = new Vector2(x, 0f);
            down.anchorMax = new Vector2(x, 1f);
            down.pivot = new Vector2(x, 0.5f);
            down.anchoredPosition = Vector2.zero;
            down.sizeDelta = new Vector2(Line, 0f);
        }

        /// <summary>内側に引く細い線。上・下・左・右の 4 本</summary>
        static void Border(RectTransform r, Color color, float width)
        {
            for (var i = 0; i < 4; i++)
            {
                var t = Solid(r, "Line" + i, color).rectTransform;
                var horizontal = i < 2;
                t.anchorMin = horizontal ? new Vector2(0f, i == 0 ? 1f : 0f) : new Vector2(i == 2 ? 0f : 1f, 0f);
                t.anchorMax = horizontal ? new Vector2(1f, i == 0 ? 1f : 0f) : new Vector2(i == 2 ? 0f : 1f, 1f);
                t.pivot = horizontal ? new Vector2(0.5f, i == 0 ? 1f : 0f) : new Vector2(i == 2 ? 0f : 1f, 0.5f);
                t.anchoredPosition = Vector2.zero;
                t.sizeDelta = horizontal ? new Vector2(0f, width) : new Vector2(width, 0f);
            }
        }

        /// <summary>板の中の矩形（板の真ん中が原点）に置く</summary>
        static void Place(RectTransform r, Rect box)
        {
            r.anchorMin = new Vector2(0.5f, 0.5f);
            r.anchorMax = new Vector2(0.5f, 0.5f);
            r.pivot = Vector2.zero;
            r.anchoredPosition = box.min;
            r.sizeDelta = box.size;
        }

        static void Stretch(RectTransform r)
        {
            r.anchorMin = Vector2.zero;
            r.anchorMax = Vector2.one;
            r.pivot = new Vector2(0.5f, 0.5f);
            r.offsetMin = Vector2.zero;
            r.offsetMax = Vector2.zero;
        }

        /// <summary>UI のレイヤーに置く。HUD の Canvas を粗い画面へ渡した後に作るので、自分で置く</summary>
        static RectTransform Rect(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = UiLens.UiLayer;
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        static Image Solid(Transform parent, string name, Color color)
        {
            var r = Rect(parent, name);
            var img = r.gameObject.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        static TMP_Text Text(Transform parent, string name, float size, Color color)
        {
            var r = Rect(parent, name);
            var t = r.gameObject.AddComponent<TextMeshProUGUI>();
            if (t.font == null) t.font = TMP_Settings.defaultFontAsset;
            t.fontSize = size;
            t.color = color;
            t.alignment = TextAlignmentOptions.Center;
            t.raycastTarget = false;
            t.richText = true;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.text = string.Empty;
            return t;
        }
    }
}
