using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace HalfAware
{
    /// <summary>
    /// 設定の枠の見た目と操作（設計書 1 節・5 節）。コンソール（<see cref="ImplantConsole"/>）とタイトルの画面（<see cref="TitleScreen"/>）が
    /// 同じ物を使う。置き場と、開け閉め・一つ前に戻る（Esc・右クリック・TAB）は呼び手が決める。
    ///
    /// 行は <see cref="SettingsList"/> の表の順に並べる: 小見出し（字と細い線）、つまみの行（項目の名の升・つまみ・倍率や割合の字）、
    /// 選ぶ行（項目の名の升・左右の三角・選んでいる物の名）、既定に戻す、（タイトルの画面だけ）戻る。
    /// いま効いていない行（<see cref="SettingRow.Live"/>）は、出したまま名・値・つまみを暗くする（行の並びも枠の高さも変えない）。
    /// 寸法はコンソールと同じ粗い画面の 1 画素（Dot）で、字はいちばん小さい 11 Dot。
    ///
    /// 操作: マウスは行に重ねると選び、つまみを掴んで動かす・溝を押すとそこへ飛ぶ。選ぶ行は左右の三角を押すとその向きへ一つ、
    /// ほかの所を押すと次の物へ（最後の次は最初へ）。既定に戻す・戻るは押すと効く。
    /// 鍵盤は上下で行（小見出しは飛ばす）、左右でつまみを一刻みずつ・選ぶ行を一つずつ（押し続けると <see cref="HoldRepeat"/> で続けて）、
    /// E・Enter で既定に戻す・戻る・選ぶ行の次の物（<see cref="Keys"/>）。
    /// 値は動かすとすぐ効き、残すのは <see cref="GameSettings.Tick"/>（動きが止まってから 0.5 秒）と呼び手の <see cref="GameSettings.Commit"/>
    /// </summary>
    public sealed class SettingsPanel
    {
        const float Dot = ImplantConsole.Dot;

        /// <summary>枠の幅。つまみの行に、項目の名・つまみ・倍率を並べる（記憶する・思い出すの枠の 280 Dot より広い）</summary>
        public const float Width = 320f * Dot;
        /// <summary>つまみの行の、項目の名の升の幅。選んでいる時はここを塗る（つまみは塗らずに青緑のまま見せる）</summary>
        const float DialName = 84f * Dot;
        /// <summary>つまみの行の右の、倍率の字の幅</summary>
        const float DialValue = 48f * Dot;
        /// <summary>名の升と、つまみの当たりのあいだ</summary>
        const float DialGap = 10f * Dot;
        /// <summary>つまみ。ボタンの選んだ色で塗った縦長の升</summary>
        const float KnobWidth = 6f * Dot;
        const float KnobHeight = 14f * Dot;
        /// <summary>溝の太さ。1 Dot だと薄い色の線が粗い画面で途切れるので 2 Dot</summary>
        const float GrooveHeight = 2f * Dot;
        /// <summary>既定（1 倍）の所に立てる目盛りの高さ</summary>
        const float TickHeight = 8f * Dot;
        /// <summary>小見出し（先頭を除く）と「既定に戻す」の行の上に空ける</summary>
        const float SectionGap = 4f * Dot;
        /// <summary>
        /// 小見出しの行の高さ。選べる行（<see cref="BoxRow"/> 22 Dot）より詰める。小見出しが二つになり（カメラ・画面）、
        /// 22 Dot のままではタイトルの画面で枠が題の読みから下の線まで収まらなかった（2026-10-05）
        /// </summary>
        const float HeadingRow = 14f * Dot;
        /// <summary>小見出しの字と、その後ろの細い線のあいだ</summary>
        const float RuleGap = 8f * Dot;
        /// <summary>選ぶ行の左右の三角の当たりの幅。三角の字は 8 Dot</summary>
        const float ArrowCell = 18f * Dot;
        const float ArrowFont = 8f * Dot;
        const string LeftArrow = "◀";
        const string RightArrow = "▶";

        const float BoxRow = ImplantConsole.BoxRow;
        const float BoxPad = ImplantConsole.BoxPad;
        const float RowInset = ImplantConsole.RowInset;
        const float HeadHeight = ImplantConsole.HeadHeight;

        // 色は使う時に作る。タイトルの画面はシーンから読まれる部品なので、静的な値の初期化で色空間を訊かない
        static Color Groove { get { return ImplantConsole.Tint(ImplantConsole.Rgb(127, 227, 236, 0.35f)); } }
        /// <summary>
        /// 効いていないつまみの行の、つまみと塗り。溝と同じ濃さの青緑を、枠の地の上に重ねた色を、透けない色で持つ
        /// （溝の色のまま透かすと、つまみの下の既定の目盛りが透けて見えた）
        /// </summary>
        static Color DimKnob
        {
            get
            {
                var c = Color.Lerp(ImplantConsole.Rgb(3, 10, 14, 1f), ImplantConsole.Accent, 0.35f);
                c.a = 1f;
                return c;
            }
        }

        readonly SettingsList list;
        readonly HoldRepeat nudge = new HoldRepeat();
        readonly List<RowView> views = new List<RowView>();
        RectTransform box;
        Material heavy;

        /// <summary>「戻る」の行を押した・E で決めた時に呼ぶ（タイトルの画面が枠を閉じる）</summary>
        public Action Back;

        public SettingsPanel(SettingsList list)
        {
            this.list = list;
        }

        public SettingsList List { get { return list; } }

        /// <summary>組んだ枠。呼び手が置き場（anchor・pivot・位置）と出し入れを決める</summary>
        public RectTransform Box { get { return box; } }

        /// <summary>行の高さ。小見出しは詰め、ほかは <see cref="BoxRow"/></summary>
        public static float RowHeight(SettingRow row)
        {
            return row != null && row.Kind == SettingKind.Heading ? HeadingRow : BoxRow;
        }

        /// <summary>行 index の上の縁。枠の上の縁から。小見出し（先頭を除く）と既定に戻すの上は少し空ける</summary>
        public static float RowTop(SettingsList list, int index)
        {
            var top = BoxPad * 2f + HeadHeight;
            for (var i = 0; i < index; i++) top += RowHeight(list.RowAt(i));
            for (var i = 1; i <= index; i++)
            {
                var kind = list.RowAt(i).Kind;
                if (kind == SettingKind.Heading || kind == SettingKind.Reset) top += SectionGap;
            }
            return top;
        }

        /// <summary>枠の高さ</summary>
        public static float Height(SettingsList list)
        {
            return RowTop(list, list.Count - 1) + RowHeight(list.RowAt(list.Count - 1)) + BoxPad;
        }

        /// <summary>
        /// 枠を組む。parent の左上を軸に置いておく（呼び手が置き場を決め直す）。
        /// title は見出しの行、heavy は塗りの上の暗い字を太らせる色づけ（無ければ素の書体）。閉じた形で返す
        /// </summary>
        public RectTransform Build(Transform parent, string title, Material heavy)
        {
            this.heavy = heavy;
            box = ImplantConsole.Rect(parent, "Settings");
            box.anchorMin = new Vector2(0f, 1f);
            box.anchorMax = new Vector2(0f, 1f);
            box.pivot = new Vector2(0f, 1f);
            box.sizeDelta = new Vector2(Width, Height(list));
            var bg = box.gameObject.AddComponent<Image>();
            bg.color = ImplantConsole.BoxFill;
            ImplantConsole.Border(box, ImplantConsole.ButtonLine, ImplantConsole.Line);
            var head = ImplantConsole.Text(box, "Title", ImplantConsole.HeadFont, ImplantConsole.Accent, TextAlignmentOptions.TopLeft);
            ImplantConsole.Top(head.rectTransform, BoxPad, BoxPad, BoxPad, HeadHeight);
            head.characterSpacing = ImplantConsole.HeadSpacing;
            head.text = title;
            for (var i = 0; i < list.Count; i++)
            {
                var r = ImplantConsole.Rect(box, "Row" + i);
                ImplantConsole.Top(r, BoxPad, BoxPad, RowTop(list, i), RowHeight(list.RowAt(i)));
                views.Add(MakeRow(r, list.RowAt(i), i));
            }
            box.gameObject.SetActive(false);
            return box;
        }

        /// <summary>一行を組む。小見出しは選べないので当たりを持たない</summary>
        RowView MakeRow(RectTransform r, SettingRow row, int index)
        {
            var view = new RowView();
            view.row = row;
            if (row.Kind == SettingKind.Heading)
            {
                view.label = ImplantConsole.Text(r, "Label", ImplantConsole.HeadFont, ImplantConsole.Accent, TextAlignmentOptions.Left);
                ImplantConsole.Stretch(view.label.rectTransform, RowInset, RowInset, 0f, 0f);
                view.label.characterSpacing = ImplantConsole.HeadSpacing;
                view.label.text = row.Label;
                // 字の後ろから右の端まで細い線を引いて、項目の区切りに見せる
                var width = view.label.GetPreferredValues(row.Label).x;
                var rule = ImplantConsole.Fill(r, "Rule", Groove, false).rectTransform;
                rule.anchorMin = new Vector2(0f, 0.5f);
                rule.anchorMax = new Vector2(1f, 0.5f);
                rule.pivot = new Vector2(0.5f, 0.5f);
                rule.offsetMin = new Vector2(RowInset + width + RuleGap, -ImplantConsole.Line);
                rule.offsetMax = new Vector2(-RowInset, 0f);
                return view;
            }
            var hit = r.gameObject.AddComponent<ConsolePointer>();
            hit.Entered = () => { list.HoverRow(index); Paint(); };
            hit.Clicked = () => Press(index);
            if (row.Kind == SettingKind.Choice) return MakeChoice(r, view, index);
            if (row.Kind != SettingKind.Dial)
            {
                // 既定に戻す・戻る。記憶する・思い出すの行と同じく、選んでいる時は行ごと塗る
                view.fill = r.gameObject.AddComponent<Image>();
                view.fill.color = ImplantConsole.Clear;
                view.label = Label(r, row.Label);
                ImplantConsole.Stretch(view.label.rectTransform, RowInset, RowInset, 0f, 0f);
                return view;
            }
            // 行のどこにカーソルを重ねても選ぶ
            var area = r.gameObject.AddComponent<Image>();
            area.color = ImplantConsole.Clear;
            var cell = ImplantConsole.Rect(r, "Name");
            cell.anchorMin = new Vector2(0f, 0f);
            cell.anchorMax = new Vector2(0f, 1f);
            cell.pivot = new Vector2(0f, 0.5f);
            cell.offsetMin = Vector2.zero;
            cell.offsetMax = new Vector2(DialName, 0f);
            view.fill = cell.gameObject.AddComponent<Image>();
            view.fill.color = ImplantConsole.Clear;
            view.fill.raycastTarget = false;
            view.label = Label(cell, row.Label);
            ImplantConsole.Stretch(view.label.rectTransform, RowInset, RowInset, 0f, 0f);

            // つまみの当たり。溝より両脇につまみの半分ずつ広い（端の値でもつまみが当たりからはみ出さない）
            var track = ImplantConsole.Rect(r, "Track");
            track.anchorMin = Vector2.zero;
            track.anchorMax = Vector2.one;
            track.pivot = new Vector2(0.5f, 0.5f);
            track.offsetMin = new Vector2(DialName + DialGap, 0f);
            track.offsetMax = new Vector2(-DialValue, 0f);
            var trackHit = track.gameObject.AddComponent<Image>();
            trackHit.color = ImplantConsole.Clear;
            var groove = ImplantConsole.Fill(track, "Groove", Groove, false).rectTransform;
            groove.anchorMin = new Vector2(0f, 0.5f);
            groove.anchorMax = new Vector2(1f, 0.5f);
            groove.pivot = new Vector2(0.5f, 0.5f);
            groove.offsetMin = new Vector2(KnobWidth / 2f, -GrooveHeight / 2f);
            groove.offsetMax = new Vector2(-KnobWidth / 2f, GrooveHeight / 2f);
            // 既定の所の目盛り。塗りとつまみの下に置く
            var tick = ImplantConsole.Fill(groove, "Default", ImplantConsole.ButtonLine, false).rectTransform;
            var at = row.Dial.Fraction(row.Dial.Default);
            tick.anchorMin = new Vector2(at, 0.5f);
            tick.anchorMax = new Vector2(at, 0.5f);
            tick.pivot = new Vector2(0.5f, 0.5f);
            tick.anchoredPosition = Vector2.zero;
            tick.sizeDelta = new Vector2(ImplantConsole.Line, TickHeight);
            // 溝の左の端からつまみまでの塗り
            view.doneFill = ImplantConsole.Fill(groove, "Done", ImplantConsole.Accent, false);
            view.done = view.doneFill.rectTransform;
            view.knobFill = ImplantConsole.Fill(groove, "Knob", ImplantConsole.Accent, false);
            view.knob = view.knobFill.rectTransform;
            view.knob.pivot = new Vector2(0.5f, 0.5f);
            view.knob.sizeDelta = new Vector2(KnobWidth, KnobHeight);

            view.value = ImplantConsole.Text(r, "Value", ImplantConsole.RowFont, ImplantConsole.ButtonText, TextAlignmentOptions.Right);
            var vr = view.value.rectTransform;
            vr.anchorMin = new Vector2(1f, 0f);
            vr.anchorMax = new Vector2(1f, 1f);
            vr.pivot = new Vector2(1f, 0.5f);
            vr.offsetMin = new Vector2(-DialValue, 0f);
            vr.offsetMax = new Vector2(-RowInset, 0f);
            view.value.fontStyle = FontStyles.Bold;
            if (heavy != null) view.value.fontSharedMaterial = heavy;

            // 掴んで動かす・溝の上を押すとそこへ飛ぶ
            var slide = track.gameObject.AddComponent<ConsolePointer>();
            slide.Held = p => Slide(index, p.x, track.rect.width);
            return view;
        }

        /// <summary>
        /// 選ぶ行を組む。左に項目の名の升（つまみの行と同じ）、右の広い所の両端に三角、真ん中に選んでいる物の名。
        /// 三角はつまみの溝の両端と同じ所に置き、押すとその向きへ一つ動かす。端では薄くする
        /// </summary>
        RowView MakeChoice(RectTransform r, RowView view, int index)
        {
            var area = r.gameObject.AddComponent<Image>();
            area.color = ImplantConsole.Clear;
            var cell = ImplantConsole.Rect(r, "Name");
            cell.anchorMin = new Vector2(0f, 0f);
            cell.anchorMax = new Vector2(0f, 1f);
            cell.pivot = new Vector2(0f, 0.5f);
            cell.offsetMin = Vector2.zero;
            cell.offsetMax = new Vector2(DialName, 0f);
            view.fill = cell.gameObject.AddComponent<Image>();
            view.fill.color = ImplantConsole.Clear;
            view.fill.raycastTarget = false;
            view.label = Label(cell, view.row.Label);
            ImplantConsole.Stretch(view.label.rectTransform, RowInset, RowInset, 0f, 0f);

            var pick = ImplantConsole.Rect(r, "Pick");
            pick.anchorMin = Vector2.zero;
            pick.anchorMax = Vector2.one;
            pick.pivot = new Vector2(0.5f, 0.5f);
            pick.offsetMin = new Vector2(DialName + DialGap, 0f);
            pick.offsetMax = new Vector2(-RowInset, 0f);

            view.value = ImplantConsole.Text(pick, "Value", ImplantConsole.RowFont, ImplantConsole.ButtonText, TextAlignmentOptions.Center);
            ImplantConsole.Stretch(view.value.rectTransform, ArrowCell, ArrowCell, 0f, 0f);
            view.value.fontStyle = FontStyles.Bold;
            if (heavy != null) view.value.fontSharedMaterial = heavy;
            view.value.raycastTarget = false;

            view.left = Arrow(pick, "Left", LeftArrow, 0f, index, -1);
            view.right = Arrow(pick, "Right", RightArrow, 1f, index, 1);
            return view;
        }

        /// <summary>選ぶ行の三角。side は 0 で左の端、1 で右の端。押すと step の向きへ一つ</summary>
        TMP_Text Arrow(RectTransform parent, string name, string glyph, float side, int index, int step)
        {
            var cell = ImplantConsole.Rect(parent, name);
            cell.anchorMin = new Vector2(side, 0f);
            cell.anchorMax = new Vector2(side, 1f);
            cell.pivot = new Vector2(side, 0.5f);
            cell.sizeDelta = new Vector2(ArrowCell, 0f);
            cell.anchoredPosition = Vector2.zero;
            var hit = cell.gameObject.AddComponent<Image>();
            hit.color = ImplantConsole.Clear;
            var press = cell.gameObject.AddComponent<ConsolePointer>();
            press.Clicked = () => Step(index, step);
            var t = ImplantConsole.Text(cell, "Glyph", ArrowFont, ImplantConsole.Accent,
                side < 0.5f ? TextAlignmentOptions.Left : TextAlignmentOptions.Right);
            ImplantConsole.Stretch(t.rectTransform, 0f, 0f, 0f, 0f);
            t.raycastTarget = false;
            t.text = glyph;
            return t;
        }

        /// <summary>行の字。塗りつぶしの上でも地に溶けないよう、太くして太らせた色づけを当てる</summary>
        TMP_Text Label(RectTransform parent, string text)
        {
            var t = ImplantConsole.Text(parent, "Label", ImplantConsole.RowFont, ImplantConsole.ButtonText, TextAlignmentOptions.Left);
            t.fontStyle = FontStyles.Bold;
            if (heavy != null) t.fontSharedMaterial = heavy;
            t.text = text;
            return t;
        }

        // ---- 操作 ------------------------------------------------------------

        /// <summary>
        /// 鍵盤の一フレームぶん。上下（W・S・矢印）で行、左右（A・D・矢印）でつまみを一刻みずつ（押し続けると続けて）、
        /// E・Enter で既定に戻す・戻る。戻るの行で決めたら true（呼び手が枠を閉じる）。
        /// Esc・右クリック・TAB は呼び手が読む。now は unscaled の秒
        /// </summary>
        public bool Keys(Keyboard keys, float now)
        {
            if (keys == null) return false;
            if (keys.upArrowKey.wasPressedThisFrame || keys.wKey.wasPressedThisFrame) list.MoveRow(-1);
            if (keys.downArrowKey.wasPressedThisFrame || keys.sKey.wasPressedThisFrame) list.MoveRow(1);
            var left = keys.leftArrowKey.wasPressedThisFrame || keys.aKey.wasPressedThisFrame;
            var right = keys.rightArrowKey.wasPressedThisFrame || keys.dKey.wasPressedThisFrame;
            var held = (keys.rightArrowKey.isPressed || keys.dKey.isPressed ? 1 : 0)
                - (keys.leftArrowKey.isPressed || keys.aKey.isPressed ? 1 : 0);
            var step = nudge.Step((right ? 1 : 0) - (left ? 1 : 0), held, now);
            if (step != 0) list.Nudge(step);
            var back = false;
            if (keys.eKey.wasPressedThisFrame || keys.enterKey.wasPressedThisFrame || keys.numpadEnterKey.wasPressedThisFrame)
                back = Decide();
            Paint();
            return back;
        }

        /// <summary>押し続けを忘れる。枠を閉じている間に呼ぶ（開く前から押していた鍵で動かさない）</summary>
        public void Rest()
        {
            nudge.Release();
        }

        /// <summary>選んでいる行を決める。既定に戻すなら全部の値を既定へ戻す。選ぶ行なら次の物へ。戻るなら true</summary>
        public bool Decide()
        {
            if (list.Reset()) return false;
            var choice = list.Choice;
            if (choice != null)
            {
                choice.Cycle();
                return false;
            }
            return list.AtBack;
        }

        /// <summary>行を押す（クリック・撮影）。選べない行（小見出し）なら何もしない</summary>
        public void Press(int row)
        {
            list.HoverRow(row);
            if (list.Row != row) return;
            var back = Decide();
            Paint();
            if (back && Back != null) Back();
        }

        /// <summary>選ぶ行の三角を押した。その行を選び、step の向きへ一つ動かす（両端で止まる）</summary>
        public void Step(int row, int step)
        {
            list.HoverRow(row);
            if (list.Row != row || list.Choice == null) return;
            list.Nudge(step);
            Paint();
        }

        /// <summary>
        /// つまみの当たりを押した・掴んで動かした。x は当たりの中の割合（左 0・右 1）、width は当たりの幅。
        /// その行を選び、押した所の値（刻みへ揃える）にする
        /// </summary>
        void Slide(int row, float x, float width)
        {
            list.HoverRow(row);
            var dial = list.Dial;
            if (list.Row != row || dial == null) return;
            dial.Value = dial.AtFraction(ConsoleSettings.TrackAt(x, width, KnobWidth / 2f));
            Paint();
        }

        // ---- 塗る ------------------------------------------------------------

        /// <summary>選びと値を塗る。つまみの位置と倍率の字もここで入れ直す</summary>
        public void Paint()
        {
            for (var i = 0; i < views.Count; i++) views[i].Paint(i == list.Row);
        }

        /// <summary>
        /// 一行の塗り。既定に戻す・戻るは、記憶する・思い出すの行と同じく行ごと塗る。
        /// つまみの行は、選んでいる時に項目の名の升だけ塗り、倍率の字を白に。つまみは青緑のまま見せる（行ごと塗ると、つまみが塗りに溶ける）。
        /// 選ぶ行もつまみの行と同じく名の升だけ塗り、選んでいる物の名を白に。三角は青緑で、その向きへもう動けない端では溝の色に薄める。
        /// いま効いていないつまみの行は、名と値を選べない行の字の色（思い出すの空きと同じ）に、塗りとつまみを溝と同じ濃さの色に落とす。
        /// 選んでいる時は名の升を薄く塗る（効いている行の塗りより一段暗い）
        /// </summary>
        sealed class RowView
        {
            public SettingRow row;
            public Image fill;
            public TMP_Text label;
            public RectTransform done;
            public RectTransform knob;
            public Image doneFill;
            public Image knobFill;
            public TMP_Text value;
            public TMP_Text left;
            public TMP_Text right;
            /// <summary>最後に塗った時、効いていない行として暗くしたか</summary>
            public bool dim;

            public void Paint(bool on)
            {
                if (row.Kind == SettingKind.Heading) return;
                var live = row.Live;
                dim = !live;
                fill.color = on ? (live ? ImplantConsole.Accent : ImplantConsole.ButtonLine) : ImplantConsole.Clear;
                label.color = on ? ImplantConsole.Ink : live ? ImplantConsole.ButtonText : ImplantConsole.Faded;
                if (row.Kind == SettingKind.Choice)
                {
                    var c = row.Choice.Value;
                    value.text = row.Choice.Text(c);
                    value.color = on ? Color.white : ImplantConsole.ButtonText;
                    left.color = c > 0 ? ImplantConsole.Accent : Groove;
                    right.color = c < row.Choice.Count - 1 ? ImplantConsole.Accent : Groove;
                    return;
                }
                if (row.Kind != SettingKind.Dial) return;
                var v = row.Dial.Value;
                var t = row.Dial.Fraction(v);
                done.anchorMin = Vector2.zero;
                done.anchorMax = new Vector2(t, 1f);
                done.offsetMin = Vector2.zero;
                done.offsetMax = Vector2.zero;
                knob.anchorMin = new Vector2(t, 0.5f);
                knob.anchorMax = new Vector2(t, 0.5f);
                knob.anchoredPosition = Vector2.zero;
                value.text = ImplantConsole.Mono(row.Dial.Text(v));
                value.color = !live ? ImplantConsole.Faded : on ? Color.white : ImplantConsole.ButtonText;
                doneFill.color = live ? ImplantConsole.Accent : DimKnob;
                knobFill.color = live ? ImplantConsole.Accent : DimKnob;
            }
        }

        /// <summary>確かめ用。行 row のつまみの位置（溝の中の割合）。つまみの行でなければ -1</summary>
        public float KnobAt(int row)
        {
            if (row < 0 || row >= views.Count || views[row].knob == null) return -1f;
            return views[row].knob.anchorMin.x;
        }

        /// <summary>確かめ用。行 row の字（倍率の字があればそれ、無ければ行の名）</summary>
        public string TextAt(int row)
        {
            if (row < 0 || row >= views.Count) return null;
            var v = views[row];
            return v.value != null ? v.value.text : v.label != null ? v.label.text : null;
        }

        /// <summary>確認用。行 row を、いま効いていない行として暗くして出しているか（最後に塗った時）</summary>
        public bool DimAt(int row)
        {
            return row >= 0 && row < views.Count && views[row].dim;
        }
    }
}
