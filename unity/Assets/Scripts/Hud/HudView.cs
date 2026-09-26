using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace HalfAware
{
    /// <summary>
    /// 字幕（画面の下から上へ薄れる黒の地に、名前の行と台詞の行）、印（中央）、中央の文字、暗転、幕。
    /// 見せるだけで、何をいつ出すかは SceneFlow と場面固有の演出が決める。
    /// 煙は画面を覆う層ではなく世界の粒で描くので、ここには無い（SmokePuffs）。
    /// 暗転と幕の層は、繋がっていなければ何もしない。
    ///
    /// 遊んでいる間は、この Canvas を粗い画面（<see cref="UiLens"/>）で描く。
    /// 中央の文字（冒頭のカード・「続く」）だけは粗くせず、別の Canvas に分けてくっきり描く。
    /// TAB のコンソールを開いている間は、字幕・印・中央の文字を伏せる
    /// </summary>
    public sealed class HudView : MonoBehaviour
    {
        [Tooltip("字幕の地。下から上へ薄れる黒のグラデーション。高さは入っている行数で伸びる")]
        [SerializeField] GameObject subtitleBand;
        [SerializeField] TMP_Text subtitleText;
        [Tooltip("話した人の名前の行。台詞と色を変える。独白では出さない")]
        [SerializeField] TMP_Text subtitleName;
        [Tooltip("送れる時だけ右下に出す「E　送る ▼」")]
        [SerializeField] TMP_Text subtitleHint;
        [Tooltip("字幕 1 行ぶんの高さ。地はこの倍数で伸びる")]
        [SerializeField] float subtitleRowHeight = 44f;
        [Tooltip("地の上下の余白をあわせた高さ。名前の行を含む")]
        [SerializeField] float subtitlePadding = 34f;
        [Tooltip("地の上の縁から台詞の行の頭まで。名前の行を含む。0 なら台詞の行を動かさない")]
        [SerializeField] float subtitleHead = 0f;
        [SerializeField] TMP_Text promptText;
        [SerializeField] TMP_Text centerText;
        [Tooltip("画面全体の黒い層。暗転に使う")]
        [SerializeField] Image fadeLayer;
        [Tooltip("画面全体を覆う黒い幕。クレジットのカードを載せる")]
        [SerializeField] Image curtainLayer;
        [Tooltip("画面の角へ向かって白く溶ける膜。場面 4 だけが使う。無い場面では null")]
        [SerializeField] ScreenHaze hazeLayer;

        [Header("流れる行（SetPassing）")]
        [Tooltip("送らずに消える行の地の濃さ。E で送る字幕より薄くする。場面 4 の一行目だけが使う")]
        [SerializeField] float passingAlpha = 0.45f;

        float baseFontSize;
        TextAlignmentOptions listlessAlignment = TextAlignmentOptions.TopLeft;
        /// <summary>地のふだんの色を覚えたか。組み立てたままの色を、流れる行のあとで戻す</summary>
        bool framed;
        Color bandColor;
        /// <summary>出すように言われているか。コンソールを開いている間は、言われていても伏せる</summary>
        bool subtitleOn;
        bool promptOn;
        bool centerOn;
        bool hidden;

        void Awake()
        {
            if (subtitleText != null)
            {
                baseFontSize = subtitleText.fontSize;
                listlessAlignment = subtitleText.alignment;
            }
            if (Application.isPlaying)
            {
                // 見出しは粗くしない。先に別の Canvas へ移してから、残りを粗い画面へ渡す
                Unblur(centerText);
                UiLens.Adopt(GetComponent<Canvas>(), 0);
            }
            Frame();
            SetSubtitle(null);
            SetPrompt(null);
            SetCenter(null);
            Cover(fadeLayer);
            SetFade(0f);
            SetCurtain(false);
            SetHaze(0f);
        }

        /// <summary>
        /// text を、くっきり描く Canvas へ移す。HUD と同じ拡縮で、粗い画面の一つ上に重ねる。
        /// 場面の物なので、場面を移れば一緒に消える
        /// </summary>
        public static Canvas Unblur(TMP_Text text)
        {
            if (text == null) return null;
            var from = text.canvas != null ? text.canvas.GetComponent<CanvasScaler>() : null;
            var go = new GameObject("HudCard", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            if (go.scene != text.gameObject.scene) SceneManager.MoveGameObjectToScene(go, text.gameObject.scene);
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = UiLens.ShowOrder + 1;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = from != null ? from.referenceResolution : new Vector2(1280f, 720f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = from != null ? from.matchWidthOrHeight : 0.5f;
            text.transform.SetParent(go.transform, false);
            return canvas;
        }

        /// <summary>コンソールを開いたら伏せ、閉じたら戻す。言われていた状態は覚えたまま</summary>
        void LateUpdate()
        {
            if (hidden == ImplantConsole.IsOpen) return;
            hidden = ImplantConsole.IsOpen;
            Sync();
        }

        void Sync()
        {
            if (subtitleBand != null) subtitleBand.SetActive(subtitleOn && !hidden);
            if (promptText != null) promptText.gameObject.SetActive(promptOn && !hidden);
            if (centerText != null) centerText.gameObject.SetActive(centerOn && !hidden);
        }

        /// <summary>
        /// 角の白い膜の強さ。0 で消える。
        /// 膜を持たない場面で呼ばれても黙って何もしない
        /// </summary>
        public void SetHaze(float amount)
        {
            if (hazeLayer == null) return;
            hazeLayer.Amount = amount;
            var on = amount > 1e-4f;
            if (hazeLayer.gameObject.activeSelf != on) hazeLayer.gameObject.SetActive(on);
        }

        /// <summary>
        /// 画面の実寸を拡大率で割って丸めるので、キャンバスは画面より 1 ピクセルほど
        /// 小さくなることがある。幕をぴったり張ると端に地が覗くので、少し外へはみ出させる
        /// </summary>
        const float Overscan = 96f;

        /// <summary>
        /// 幕を画面いっぱい、四方へ大きくはみ出させて張る。
        /// 暗転で端に地が覗くのは目に付くうえ、はみ出させて困ることは何も無いので、
        /// ぎりぎりを狙わずに十分な余りを取る
        /// </summary>
        static void Cover(Image layer)
        {
            if (layer == null) return;
            var rect = layer.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(-Overscan, -Overscan);
            rect.offsetMax = new Vector2(Overscan, Overscan);
        }

        /// <summary>
        /// null で地ごと隠す。入っている行数に合わせて地を伸ばし、
        /// 長いものは字を小さくして収める。台詞は E で送れるものとして「E　送る」を添える
        /// </summary>
        public void SetSubtitle(string text)
        {
            SetSubtitle(text, SubtitleKind.Line);
        }

        /// <summary>
        /// 二択は表に組まず、地の真ん中へ寄せる。
        /// 「はい　いいえ」を左に寄せると、どちらを選んでいるかが目で追いにくい
        /// </summary>
        public void SetSubtitle(string text, SubtitleKind kind)
        {
            Show(text, kind, false, kind == SubtitleKind.Line);
        }

        /// <summary>advance が true の時だけ、右下に「E　送る ▼」を出す。送れない間（演出で止まっている間）は false</summary>
        public void SetSubtitle(string text, SubtitleKind kind, bool advance)
        {
            Show(text, kind, false, advance);
        }

        /// <summary>
        /// 送らずに流れて消える行。地を薄くし、「E　送る」も出さずに、E で送る字幕と見分ける。null で隠す。
        ///
        /// **場面 4 の一行目（名を呼ぶ声）だけが使う。** 記憶に入った瞬間に出て、
        /// 決まった秒で勝手に消える。E で送る字幕と同じ地で出すと、
        /// 送り待ちに見えて E を押させてしまう
        /// </summary>
        public void SetPassing(string text)
        {
            Show(text, SubtitleKind.Line, true, false);
        }

        void Show(string text, SubtitleKind kind, bool passing, bool advance)
        {
            subtitleOn = text != null;
            Sync();
            subtitleText.text = text ?? string.Empty;
            if (subtitleName != null) subtitleName.text = string.Empty;
            if (subtitleHint != null) subtitleHint.gameObject.SetActive(false);
            if (text == null) return;
            // 一行に入る幅は地の幅と字の大きさで決まるので、どちらも先に決める。
            // エディタで Awake を通さずに呼ばれると字の大きさが 0 のままで、一行に二文字ずつ割れる
            if (baseFontSize <= 0f) baseFontSize = subtitleText.fontSize;
            Shape(passing);
            // 並びになっているものは表に組む。そうでない長い 1 行は割ってウインドウに収める
            var list = kind == SubtitleKind.Line && ListFormat.IsList(text);
            // 「名前「台詞」」は名前の行と台詞の行に分け、鉤括弧を外す。独白は名前の行を出さない
            var who = string.Empty;
            var said = text;
            if (kind == SubtitleKind.Line && !list) Speech.Split(text, out who, out said);
            if (subtitleName != null) subtitleName.text = Ruby.Expand(who);
            if (subtitleHint != null) subtitleHint.gameObject.SetActive(advance && !passing && kind == SubtitleKind.Line);
            // 1 行に入る幅はウインドウの実寸から。全角 1 文字で半角 2 つぶん
            var fits = Mathf.Max(SubtitleBox.BaseRows * 2, Mathf.FloorToInt(RoomEm(1f) * 2f) - 1);
            var shown = list ? said : SubtitleBox.Wrap(said, fits);
            var rows = SubtitleBox.Rows(shown);
            var scale = SubtitleBox.FontScale(shown);
            // 列を揃えるため表は左寄せにして、表ごと地の真ん中へ寄せる
            // ルビは折り返してから書式に直す。
            // 先に直すと、折り返しがタグを字数に数えてしまう
            subtitleText.text = Ruby.Expand(list ? ListFormat.Compose(said, RoomEm(scale)) : shown);
            // ルビのある文は行を少し開ける。
            // そのままだと下の行のルビが上の行の字にかぶる
            subtitleText.lineSpacing = shown.IndexOf(Ruby.Head) >= 0 ? Ruby.ExtraLineSpacing : 0f;
            subtitleText.alignment =
                kind == SubtitleKind.Choice ? TextAlignmentOptions.Top :
                list ? TextAlignmentOptions.TopLeft : listlessAlignment;
            // 字を小さくしたぶん 1 行も低くなる。地の高さも同じだけ詰める
            var body = subtitleRowHeight * rows * scale;
            var band = subtitleBand.GetComponent<RectTransform>();
            if (band != null)
            {
                var size = band.sizeDelta;
                size.y = subtitlePadding + body;
                band.sizeDelta = size;
            }
            // 台詞の行は、地の上の縁から名前の行のぶん下げて置く。
            // 独白でも同じ所に置き、会話と独白が続いても行が跳ねないようにする
            if (subtitleHead > 0f)
            {
                var line = subtitleText.rectTransform;
                line.anchoredPosition = new Vector2(line.anchoredPosition.x, -subtitleHead);
                line.sizeDelta = new Vector2(line.sizeDelta.x, body);
            }
            subtitleText.fontSize = baseFontSize * scale;
        }

        /// <summary>
        /// 地のふだんの色を覚える。**組み立てた色をそのまま正とする。**
        /// エディタで Awake を通さずに呼ばれたときのために、初めて出すときにも呼ぶ
        /// </summary>
        void Frame()
        {
            if (framed || subtitleBand == null || subtitleText == null) return;
            var shade = subtitleBand.GetComponent<Image>();
            bandColor = shade != null ? shade.color : Color.black;
            framed = true;
        }

        /// <summary>地を、流れる行の薄さか、ふだんの濃さにする</summary>
        void Shape(bool passing)
        {
            Frame();
            if (!framed) return;
            var shade = subtitleBand.GetComponent<Image>();
            if (shade == null) return;
            var col = bandColor;
            if (passing) col.a = passingAlpha;
            shade.color = col;
        }

        /// <summary>地に入る横幅を em で。表の列数と寄せ方をこれで決める</summary>
        float RoomEm(float scale)
        {
            var size = baseFontSize * scale;
            if (size <= 0f) return 0f;
            return subtitleText.rectTransform.rect.width / size;
        }

        /// <summary>null で隠す</summary>
        public void SetPrompt(string text)
        {
            promptOn = text != null;
            Sync();
            promptText.text = text ?? string.Empty;
        }

        /// <summary>null で隠す</summary>
        public void SetCenter(string text)
        {
            centerOn = text != null;
            Sync();
            centerText.text = Ruby.Expand(text ?? string.Empty);
        }

        /// <summary>いまの黒い層の濃さ。層が繋がっていなければ 0。暗転に合わせて音を絞る演出が読む</summary>
        public float Fade => fadeLayer != null ? fadeLayer.color.a : 0f;

        /// <summary>黒い層の濃さ。0 で透明、1 で真っ黒</summary>
        public void SetFade(float alpha)
        {
            Cover(fadeLayer);
            SetAlpha(fadeLayer, alpha);
        }

        /// <summary>seconds 秒かけて黒い層の濃さを変える</summary>
        public IEnumerator FadeTo(float alpha, float seconds)
        {
            Cover(fadeLayer);
            return Ramp(fadeLayer, alpha, seconds);
        }

        /// <summary>seconds 秒かけて黒い幕の濃さを変える。切り替えずに明けたいときに使う</summary>
        public IEnumerator CurtainTo(float alpha, float seconds)
        {
            return Ramp(curtainLayer, alpha, seconds);
        }

        static IEnumerator Ramp(Image layer, float alpha, float seconds)
        {
            if (layer == null || seconds <= 0f)
            {
                SetAlpha(layer, alpha);
                yield break;
            }
            var from = layer.color.a;
            for (var t = 0f; t < seconds; t += Time.deltaTime)
            {
                SetAlpha(layer, Mathf.Lerp(from, alpha, t / seconds));
                yield return null;
            }
            SetAlpha(layer, alpha);
        }

        /// <summary>
        /// 黒い幕。true で画面を覆い、false で消す。動きは付けず、そのまま切り替える。
        /// 濃さもここで戻すので、CurtainTo で薄くした後に覆い直しても透けない
        /// </summary>
        public void SetCurtain(bool covered)
        {
            if (curtainLayer == null) return;
            Cover(curtainLayer);
            SetAlpha(curtainLayer, covered ? 1f : 0f);
        }

        static void SetAlpha(Image layer, float alpha)
        {
            if (layer == null) return;
            var color = layer.color;
            color.a = alpha;
            layer.color = color;
            layer.gameObject.SetActive(alpha > 0f);
        }
    }
}
