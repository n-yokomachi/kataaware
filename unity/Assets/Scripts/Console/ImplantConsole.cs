using System.Collections.Generic;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace HalfAware
{
    /// <summary>
    /// TAB で開くコンソール。主人公の眼球インプラントが視界に重ねて出す画面（設計書 1 節、案 A 端末）。
    /// 頭の行（いまの日時と場所、「TAB　閉じる」）、4 つのボタン、大きなログの枠。
    ///
    /// **場面をまたいで残る一つの物。** 場面ごとの HUD には作らず、遊び始めに一つだけ作って
    /// DontDestroyOnLoad で持ち越す。見た目もここで組む（シーンにもプレハブにも置かない）。
    /// ログの中身は場面ごと（<see cref="ConsoleLog"/>）。
    ///
    /// **開いている間は場面を止める。** Time.timeScale を 0 にして演出の秒を止め、
    /// 歩く・見回す・調べる・字幕送りは PlayerController が、場面の進行は SceneFlow と
    /// DiveDirector が <see cref="IsOpen"/> を見て止まる。コンソール自身は秒を使わない
    /// （知らせを消すのだけ unscaled の秒で数える）。
    /// 音は止めない。部屋の空気や雨が途切れると、視界に重ねた画面ではなく一時停止に見える
    /// </summary>
    public sealed class ImplantConsole : MonoBehaviour
    {
        // ---- 見た目 ------------------------------------------------------------
        //
        // 案 A の CSS を手本にしているが、寸法は粗い画面（UiLens、既定で画面の 0.75）の 1 画素 = Dot で決める。
        // CSS の px のまま縮めると、札や名前の字が粗い画面の中で縦 5〜6 画素になって読めない。
        // **字はいちばん小さいものでも 11 Dot**（粗い画面の中で縦 10 画素ほど）、線は 1 Dot 以上にする。
        // 余白は案 A より二回りほど広く取る。詰まって見える所を作らない（2026-09-27 オーナー、二度の指摘）。
        // 字は 11 Dot のまま、粗さを上げて（Dot を小さくして）画面の上では小さく見せる。
        // 色は CSS のまま。薄い色の重ねはリニアの色空間だと明るく出るので、Tint・Veil で濃さを合わせる

        /// <summary>粗い画面の 1 画素。1280×720 のキャンバスで、既定の粗さ（0.75 で 720×405 相当）のとき</summary>
        const float Dot = 1280f / 720f;
        public const int SortingOrder = 500;

        static readonly Color Accent = Rgb(0x7f, 0xe3, 0xec, 1f);
        static readonly Color Ink = Rgb(0x06, 0x11, 0x14, 1f);
        static readonly Color ButtonText = Rgb(0xcf, 0xf7, 0xfa, 1f);
        static readonly Color RowText = Rgb(0xb9, 0xd9, 0xdc, 1f);
        static readonly Color Dim = Veil(Rgb(3, 10, 14, 0.74f));
        static readonly Color Scan = Tint(Rgb(120, 230, 240, 0.035f));
        static readonly Color PanelLine = Tint(Rgb(120, 220, 230, 0.45f));
        static readonly Color ButtonLine = Tint(Rgb(127, 227, 236, 0.55f));
        static readonly Color LogLine = Tint(Rgb(127, 227, 236, 0.3f));
        static readonly Color TagFill = Tint(Rgb(127, 227, 236, 0.7f));
        static readonly Color Track = Tint(Rgb(127, 227, 236, 0.15f));
        static readonly Color BoxFill = Veil(Rgb(3, 10, 14, 0.92f));
        static readonly Color Clear = new Color(0f, 0f, 0f, 0f);

        /// <summary>画面の縁から枠まで（上下・左右）。画面に対する割合</summary>
        const float InsetY = 0.12f;
        const float InsetX = 0.14f;
        const float Line = 1f * Dot;
        const float HookSize = 12f * Dot;
        const float HookLine = 2f * Dot;

        /// <summary>枠の内側の余白。頭の行・ボタン・ログの枠の左右と、頭の行の上</summary>
        const float Inner = 22f * Dot;
        const float HeadTop = 16f * Dot;
        const float HeadSide = Inner;
        const float HeadFont = 11f * Dot;
        const float HeadHeight = 14f * Dot;
        /// <summary>頭の行の字の間。0.08 em</summary>
        const float HeadSpacing = 8f;

        const float ButtonTop = HeadTop + HeadHeight + 16f * Dot;
        const float ButtonHeight = 28f * Dot;
        const float ButtonGap = 16f * Dot;
        const float ButtonFont = 11f * Dot;
        const float ButtonSpacing = 12f;

        const float LogTop = ButtonTop + ButtonHeight + 20f * Dot;
        const float LogSide = Inner;
        const float LogBottom = Inner;
        const float PadTop = 16f * Dot;
        const float PadRight = 26f * Dot;
        const float PadBottom = 16f * Dot;
        const float PadLeft = 18f * Dot;
        /// <summary>枠の上のこの割合で、古い行が薄れて消える</summary>
        const float FadeBand = 0.22f;

        const float RowFont = 11f * Dot;
        const float RowLine = 17f * Dot;
        const float RowGap = 10f * Dot;
        /// <summary>行の送り。TMP の Noto は素で 1.45 em。折り返した行のあいだも少し開ける</summary>
        const float RowSpacing = 8f;
        const float TagFont = 11f * Dot;
        const float TagHeight = 15f * Dot;
        const float TagPad = 5f * Dot;
        const float CellGap = 18f * Dot;
        const float WhoMin = 4.5f * 11f * Dot;

        /// <summary>スクロールバーの溝。ログの枠の右の余白の中に立てる</summary>
        const float BarRight = LogSide + 12f * Dot;
        const float BarTop = LogTop + PadTop;
        const float BarBottom = LogBottom + PadBottom;
        const float BarWidth = 2f * Dot;
        /// <summary>つまみを掴める幅。見える溝は細いので、当たりだけ太くする</summary>
        const float BarGrip = 16f * Dot;

        const float BoxWidth = 210f * Dot;
        const float BoxRow = 22f * Dot;
        const float BoxPad = 12f * Dot;

        public const string CloseHint = "TAB　閉じる";
        public const string ListTitle = "場面　　数字・E で飛ぶ";
        public const string HereMark = "いま";

        /// <summary>知らせを出しておく秒。unscaled</summary>
        const float NoteSeconds = 2.4f;
        /// <summary>車輪 1 刻みで送る行数</summary>
        const float WheelRows = 3f;

        // ---- 状態 ------------------------------------------------------------

        static ImplantConsole instance;
        static string placeOverride;

        /// <summary>開いているか。場面の側はこれを見て止まる</summary>
        public static bool IsOpen { get; private set; }

        public static ImplantConsole Instance { get { return instance; } }

        readonly ConsoleMenu menu = new ConsoleMenu();
        readonly LogScroll scroll = new LogScroll();
        readonly List<RowView> rows = new List<RowView>();
        readonly List<ButtonView> buttons = new List<ButtonView>();
        readonly List<ButtonView> places = new List<ButtonView>();

        RectTransform root;
        RawImage scan;
        TMP_Text headLeft;
        TMP_Text headRight;
        TMP_Text note;
        RectTransform viewport;
        RectTransform content;
        RectTransform thumb;
        RectTransform box;
        Texture2D scanTexture;
        /// <summary>塗りつぶしの上に置く暗い字の書体の色づけ。線を太らせて、粗い画面でも地に溶けないようにする</summary>
        Material heavy;
        /// <summary>暗い字の線をどれだけ太らせるか。SDF の縁を外へ寄せる量</summary>
        const float HeavyDilate = 0.3f;
        float noteUntil;
        int shown = -1;
        float keptScale = 1f;
        CursorLockMode keptLock;
        bool keptVisible = true;

        public ConsoleMenu Menu { get { return menu; } }
        public LogScroll Scroll { get { return scroll; } }

        static Color Rgb(int r, int g, int b, float a)
        {
            return new Color(r / 255f, g / 255f, b / 255f, a);
        }

        /// <summary>
        /// 暗い地に重ねる薄い明るい色の濃さを、CSS（sRGB で重ねる）と揃える。
        /// リニアで重ねると、暗い地の上では濃さが 1/2.2 乗に効いて明るく出る
        /// </summary>
        public static Color Tint(Color c)
        {
            if (QualitySettings.activeColorSpace == ColorSpace.Linear) c.a = Mathf.Pow(c.a, 2.2f);
            return c;
        }

        /// <summary>
        /// 景色に重ねる暗い層の濃さを、CSS と揃える。
        /// リニアで重ねると、黒の層は透けて見える（残る明るさが 1/2.2 乗に効く）
        /// </summary>
        public static Color Veil(Color c)
        {
            if (QualitySettings.activeColorSpace == ColorSpace.Linear) c.a = 1f - Mathf.Pow(1f - c.a, 2.2f);
            return c;
        }

        static string Here { get { return SceneManager.GetActiveScene().name; } }

        // ---- 作る ------------------------------------------------------------

        /// <summary>再生を始めるたびに静的な状態を戻す。ドメインを読み直さない設定でも前の再生を持ち越さない</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Forget()
        {
            instance = null;
            placeOverride = null;
            IsOpen = false;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            Ensure();
        }

        /// <summary>遊んでいる間の一つを返す。無ければ作って場面をまたいで残す</summary>
        public static ImplantConsole Ensure()
        {
            if (instance != null) return instance;
            var made = Make();
            DontDestroyOnLoad(made.gameObject);
            made.Guide();
            // HUD と同じ粗い画面で描く。HUD より上に重ねる
            UiLens.Adopt(made.GetComponent<Canvas>(), SortingOrder);
            return made;
        }

        /// <summary>
        /// 組むだけ。持ち越しも入力の受け口も付けない。
        /// エディタで撮るときはこれで作って、撮り終えたら消す
        /// </summary>
        public static ImplantConsole Make()
        {
            var go = new GameObject("ImplantConsole", typeof(RectTransform));
            go.layer = 5;
            var console = go.AddComponent<ImplantConsole>();
            console.Build();
            if (instance == null) instance = console;
            return console;
        }

        /// <summary>
        /// マウスの受け口。場面には EventSystem を置いていないので、無ければここに持つ。
        /// 入力の割り当ては UI の既定のもの（指す・押す・車輪）
        /// </summary>
        void Guide()
        {
            if (FindFirstObjectByType<EventSystem>() != null) return;
            var go = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            go.transform.SetParent(transform, false);
            go.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
        }

        void OnEnable()
        {
            SceneManager.sceneLoaded += Arrived;
        }

        void OnDisable()
        {
            SceneManager.sceneLoaded -= Arrived;
        }

        void OnDestroy()
        {
            Release();
        }

        /// <summary>
        /// 開いていれば閉じて、作った絵を捨てる。エディタでは OnDestroy が呼ばれないので、
        /// 撮り終えて消す前にこれを呼ぶ
        /// </summary>
        public void Release()
        {
            if (instance == this)
            {
                if (IsOpen) Close();
                instance = null;
            }
            if (scanTexture != null)
            {
                if (Application.isPlaying) Destroy(scanTexture);
                else DestroyImmediate(scanTexture);
                scanTexture = null;
            }
            if (heavy == null) return;
            if (Application.isPlaying) Destroy(heavy);
            else DestroyImmediate(heavy);
            heavy = null;
        }

        /// <summary>場面を移った。記憶の中の日時は前の場面のものなので捨てる</summary>
        void Arrived(Scene scene, LoadSceneMode mode)
        {
            if (mode != LoadSceneMode.Single) return;
            placeOverride = null;
            if (IsOpen) Close();
        }

        /// <summary>頭の行の左を差し替える。null で場面の既定（<see cref="ConsolePlace.For"/>）へ戻す</summary>
        public static void SetPlace(string line)
        {
            placeOverride = line;
            if (instance != null && instance.headLeft != null) instance.Head();
        }

        void Build()
        {
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = SortingOrder;
            // HUD と同じ拡縮。画面の大きさが変わっても字と余白の釣り合いが崩れない
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            gameObject.AddComponent<GraphicRaycaster>();

            var font = TMP_Settings.defaultFontAsset;
            if (font != null && font.material != null)
            {
                heavy = new Material(font.material);
                heavy.name = "ConsoleHeavy";
                heavy.hideFlags = HideFlags.DontSave;
                if (heavy.HasProperty(ShaderUtilities.ID_FaceDilate)) heavy.SetFloat(ShaderUtilities.ID_FaceDilate, HeavyDilate);
            }

            root = Rect(transform, "Screen");
            Stretch(root, 0f, 0f, 0f, 0f);
            // 暗い層は当たりを持つ。開いている間、下の HUD へクリックを通さない
            Fill(root, "Dim", Dim, true);
            scan = Scanlines(root);

            var panel = Rect(root, "Panel");
            panel.anchorMin = new Vector2(InsetX, InsetY);
            panel.anchorMax = new Vector2(1f - InsetX, 1f - InsetY);
            panel.offsetMin = Vector2.zero;
            panel.offsetMax = Vector2.zero;
            Border(panel, PanelLine, Line);
            Hooks(panel);

            headLeft = Text(panel, "Place", HeadFont, Accent, TextAlignmentOptions.TopLeft);
            Top(headLeft.rectTransform, HeadSide, HeadSide, HeadTop, HeadHeight);
            headLeft.characterSpacing = HeadSpacing;
            headRight = Text(panel, "Close", HeadFont, Accent, TextAlignmentOptions.TopRight);
            Top(headRight.rectTransform, HeadSide, HeadSide, HeadTop, HeadHeight);
            headRight.characterSpacing = HeadSpacing;
            headRight.text = Mono(CloseHint);
            note = Text(panel, "Note", HeadFont, ButtonText, TextAlignmentOptions.Top);
            Top(note.rectTransform, HeadSide, HeadSide, HeadTop, HeadHeight);

            Buttons(panel);
            Log(panel);
            Bar(panel);
            List();

            root.gameObject.SetActive(false);
        }

        /// <summary>細い走査線。1 px の線と 2 px の隙間の繰り返し。1 本の絵を繰り返して貼る</summary>
        RawImage Scanlines(RectTransform parent)
        {
            scanTexture = new Texture2D(1, 3, TextureFormat.RGBA32, false);
            scanTexture.name = "ConsoleScan";
            scanTexture.hideFlags = HideFlags.DontSave;
            scanTexture.filterMode = FilterMode.Point;
            scanTexture.wrapMode = TextureWrapMode.Repeat;
            scanTexture.SetPixels(new[] { Clear, Clear, Scan });
            scanTexture.Apply();
            var go = new GameObject("Scan", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
            go.layer = 5;
            go.transform.SetParent(parent, false);
            var raw = go.GetComponent<RawImage>();
            raw.texture = scanTexture;
            raw.raycastTarget = false;
            Stretch(raw.rectTransform, 0f, 0f, 0f, 0f);
            return raw;
        }

        void Buttons(RectTransform panel)
        {
            var row = Rect(panel, "Buttons");
            Top(row, HeadSide, HeadSide, ButtonTop, ButtonHeight);
            var n = ConsoleMenu.Labels.Length;
            for (var i = 0; i < n; i++)
            {
                var b = Rect(row, "Button" + i);
                b.anchorMin = new Vector2(i / (float)n, 0f);
                b.anchorMax = new Vector2((i + 1) / (float)n, 1f);
                // 隙間はボタンの両側から半分ずつ削る。端のボタンは外側を削らない
                b.offsetMin = new Vector2(i == 0 ? 0f : ButtonGap / 2f, 0f);
                b.offsetMax = new Vector2(i == n - 1 ? 0f : -ButtonGap / 2f, 0f);
                var view = new ButtonView();
                view.fill = b.gameObject.AddComponent<Image>();
                view.fill.color = Clear;
                view.lines = Border(b, ButtonLine, Line);
                view.label = Text(b, "Label", ButtonFont, ButtonText, TextAlignmentOptions.Center);
                Stretch(view.label.rectTransform, 0f, 0f, 0f, 0f);
                view.label.characterSpacing = ButtonSpacing;
                // 塗りつぶしの上の暗い字は、粗い画面だと線が 1 画素に満たずに地の色へ溶ける。太くして残す
                view.label.fontStyle = FontStyles.Bold;
                Heavy(view.label);
                view.label.text = ConsoleMenu.Labels[i];
                var index = i;
                var hit = b.gameObject.AddComponent<ConsolePointer>();
                hit.Entered = () => { menu.Hover(index); Paint(); };
                hit.Clicked = () => { menu.Hover(index); Press(); };
                buttons.Add(view);
            }
        }

        void Log(RectTransform panel)
        {
            var frame = Rect(panel, "Log");
            Stretch(frame, LogSide, LogSide, LogTop, LogBottom);
            Border(frame, LogLine, Line);
            viewport = Rect(frame, "Viewport");
            Stretch(viewport, PadLeft, PadRight, PadTop, PadBottom);
            viewport.gameObject.AddComponent<RectMask2D>();
            content = Rect(viewport, "Rows");
            content.anchorMin = new Vector2(0f, 0f);
            content.anchorMax = new Vector2(1f, 0f);
            content.pivot = new Vector2(0.5f, 0f);
            content.sizeDelta = Vector2.zero;
        }

        void Bar(RectTransform panel)
        {
            // 当たりは太く、見えるのは細い溝だけ
            var grip = Rect(panel, "ScrollBar");
            grip.anchorMin = new Vector2(1f, 0f);
            grip.anchorMax = new Vector2(1f, 1f);
            grip.pivot = new Vector2(0.5f, 0.5f);
            grip.offsetMin = new Vector2(-BarRight - BarGrip / 2f, BarBottom);
            grip.offsetMax = new Vector2(-BarRight + BarGrip / 2f, -BarTop);
            var hitArea = grip.gameObject.AddComponent<Image>();
            hitArea.color = Clear;
            var track = Fill(grip, "Track", Track, false);
            track.rectTransform.anchorMin = new Vector2(0.5f, 0f);
            track.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            track.rectTransform.sizeDelta = new Vector2(BarWidth, 0f);
            var knob = Fill(track.rectTransform, "Thumb", Accent, false);
            thumb = knob.rectTransform;
            thumb.offsetMin = Vector2.zero;
            thumb.offsetMax = Vector2.zero;
            var hit = grip.gameObject.AddComponent<ConsolePointer>();
            hit.Held = p => { scroll.DragTo(p.y - scroll.Thumb / 2f); Paint(); };
        }

        /// <summary>デバッグの場面の一覧。ログの枠の中の左上に重ねる</summary>
        void List()
        {
            var frame = (RectTransform)viewport.parent;
            box = Rect(frame, "Scenes");
            box.anchorMin = new Vector2(0f, 1f);
            box.anchorMax = new Vector2(0f, 1f);
            box.pivot = new Vector2(0f, 1f);
            var count = SceneMenu.Count;
            var height = BoxPad * 2f + HeadHeight + BoxPad + BoxRow * count;
            box.anchoredPosition = new Vector2(PadLeft, -PadTop);
            box.sizeDelta = new Vector2(BoxWidth, height);
            var bg = box.gameObject.AddComponent<Image>();
            bg.color = BoxFill;
            Border(box, ButtonLine, Line);
            var title = Text(box, "Title", HeadFont, Accent, TextAlignmentOptions.TopLeft);
            Top(title.rectTransform, BoxPad, BoxPad, BoxPad, HeadHeight);
            title.characterSpacing = HeadSpacing;
            title.text = ListTitle;
            for (var i = 0; i < count; i++)
            {
                var r = Rect(box, "Scene" + (i + 1));
                Top(r, BoxPad, BoxPad, BoxPad * 2f + HeadHeight + BoxRow * i, BoxRow);
                var view = new ButtonView();
                view.fill = r.gameObject.AddComponent<Image>();
                view.fill.color = Clear;
                view.label = Text(r, "Label", RowFont, ButtonText, TextAlignmentOptions.Left);
                Stretch(view.label.rectTransform, 7f * Dot, 7f * Dot, 0f, 0f);
                view.label.text = Mono((i + 1).ToString()) + "　" + SceneMenu.Titles[i];
                view.label.fontStyle = FontStyles.Bold;
                Heavy(view.label);
                view.mark = Text(r, "Here", TagFont, Accent, TextAlignmentOptions.Right);
                Stretch(view.mark.rectTransform, 7f * Dot, 7f * Dot, 0f, 0f);
                var row = i;
                var hit = r.gameObject.AddComponent<ConsolePointer>();
                hit.Entered = () => { menu.HoverRow(row); Paint(); };
                hit.Clicked = () => { menu.HoverRow(row); Jump(menu.RowTarget(Here)); };
                places.Add(view);
            }
            box.gameObject.SetActive(false);
        }

        // ---- 開け閉め ----------------------------------------------------------

        /// <summary>開く。場面の秒を止め、カーソルを出してロックを外す。ログは必ず最新から</summary>
        public void Open()
        {
            if (IsOpen) return;
            IsOpen = true;
            keptScale = Time.timeScale;
            Time.timeScale = 0f;
            keptLock = Cursor.lockState;
            keptVisible = Cursor.visible;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            Show(0f, false);
        }

        /// <summary>閉じる。止めた秒とカーソルを開く前へ戻す</summary>
        public void Close()
        {
            if (!IsOpen) return;
            IsOpen = false;
            root.gameObject.SetActive(false);
            Time.timeScale = keptScale;
            Cursor.lockState = keptLock;
            Cursor.visible = keptVisible;
            var events = EventSystem.current;
            if (events != null) events.SetSelectedGameObject(null);
        }

        /// <summary>
        /// 開いた形に並べる。back は最新からさかのぼる量、listing はデバッグの一覧を出すか。
        /// 秒もカーソルも触らないので、エディタで撮るときはこれだけ呼ぶ
        /// </summary>
        public void Show(float back, bool listing)
        {
            menu.Reset();
            if (listing)
            {
                menu.Hover((int)ConsoleAction.Debug);
                menu.Decide(Here);
            }
            noteUntil = 0f;
            note.text = string.Empty;
            root.gameObject.SetActive(true);
            Canvas.ForceUpdateCanvases();
            Head();
            Rows();
            scroll.Newest();
            scroll.By(back);
            Paint();
        }

        // ---- 入力 ------------------------------------------------------------

        void Update()
        {
            var keys = Keyboard.current;
            if (!IsOpen)
            {
                if (keys != null && keys.tabKey.wasPressedThisFrame) Open();
                return;
            }
            // 開いている間に行が増えることは無いはずだが、増えたら並べ直す
            if (ConsoleLog.Here().Count != shown) Rows();
            if (keys != null && Keys(keys)) return;
            var mouse = Mouse.current;
            if (mouse != null && !menu.Listing)
            {
                var wheel = mouse.scroll.ReadValue().y;
                if (wheel > 0.01f) scroll.By(Step() * WheelRows);       // 手前に回すと古い方へ
                else if (wheel < -0.01f) scroll.By(-Step() * WheelRows);
            }
            if (noteUntil > 0f && Time.unscaledTime > noteUntil)
            {
                noteUntil = 0f;
                note.text = string.Empty;
            }
            Paint();
            // 押したボタンやつまみが選ばれたままだと、矢印の入力を uGUI が横取りする
            var events = EventSystem.current;
            if (events != null && events.currentSelectedGameObject != null) events.SetSelectedGameObject(null);
        }

        /// <summary>鍵盤。閉じたら true</summary>
        bool Keys(Keyboard keys)
        {
            if (keys.tabKey.wasPressedThisFrame) { Close(); return true; }
            if (keys.escapeKey.wasPressedThisFrame && !menu.Back()) { Close(); return true; }
            var digit = Digit(keys);
            if (digit > 0 && Jump(SceneMenu.Target(digit, Here))) return true;
            if (keys.leftArrowKey.wasPressedThisFrame || keys.aKey.wasPressedThisFrame) menu.Move(-1);
            if (keys.rightArrowKey.wasPressedThisFrame || keys.dKey.wasPressedThisFrame) menu.Move(1);
            var up = keys.upArrowKey.wasPressedThisFrame || keys.wKey.wasPressedThisFrame;
            var down = keys.downArrowKey.wasPressedThisFrame || keys.sKey.wasPressedThisFrame;
            if (menu.Listing)
            {
                if (up) menu.MoveRow(-1);
                if (down) menu.MoveRow(1);
            }
            else
            {
                if (up) scroll.By(Step());
                if (down) scroll.By(-Step());
                if (keys.pageUpKey.wasPressedThisFrame) scroll.By(scroll.View * 0.9f);
                if (keys.pageDownKey.wasPressedThisFrame) scroll.By(-scroll.View * 0.9f);
            }
            var decide = keys.eKey.wasPressedThisFrame || keys.enterKey.wasPressedThisFrame
                || keys.numpadEnterKey.wasPressedThisFrame;
            if (!decide) return false;
            // 一覧を出している間の決定は、選んでいる場面へ飛ぶ
            if (menu.Listing) return Jump(menu.RowTarget(Here));
            Press();
            return false;
        }

        /// <summary>1〜9 の数字。押されていなければ 0。一覧に並んだ数字で、いつでも場面を移れる</summary>
        static int Digit(Keyboard k)
        {
            for (var i = 0; i < 9; i++)
                if (k[Key.Digit1 + i].wasPressedThisFrame || k[Key.Numpad1 + i].wasPressedThisFrame) return i + 1;
            return 0;
        }

        /// <summary>1 行ぶんの送り</summary>
        static float Step()
        {
            return RowLine + RowGap;
        }

        /// <summary>選んでいるボタンを決める。中身の無いボタンは、まだ使えないことだけ知らせる</summary>
        void Press()
        {
            var action = menu.Decide(Here);
            var said = ConsoleMenu.Note(action);
            if (said != null)
            {
                note.text = said;
                noteUntil = Time.unscaledTime + NoteSeconds;
            }
            Paint();
        }

        /// <summary>場面へ飛ぶ。閉じて秒を戻してから読む。移り先が無ければ何もしない</summary>
        bool Jump(string target)
        {
            if (string.IsNullOrEmpty(target)) return false;
            Close();
            SceneManager.LoadScene(target);
            return true;
        }

        // ---- 並べる ----------------------------------------------------------

        /// <summary>頭の行の左。記憶の中なら DiveDirector が渡した行</summary>
        void Head()
        {
            headLeft.text = Mono(placeOverride ?? ConsolePlace.For(Here));
        }

        /// <summary>英数字の並びだけ等幅にする。Noto Sans JP の数字は幅が揃わない</summary>
        static string Mono(string text)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;
            return Regex.Replace(text, @"[0-9A-Za-z][0-9A-Za-z/:\. ]*[0-9A-Za-z]|[0-9A-Za-z]",
                m => "<mspace=0.62em>" + m.Value + "</mspace>");
        }

        /// <summary>ログの行を並べ直す。最新をいちばん下に置き、古い方を上へ積む</summary>
        void Rows()
        {
            var entries = ConsoleLog.Here().Entries;
            while (rows.Count < entries.Count) rows.Add(MakeRow(rows.Count));
            var width = viewport.rect.width;
            var y = 0f;
            for (var k = 0; k < rows.Count; k++)
            {
                var row = rows[k];
                if (k >= entries.Count)
                {
                    if (row.rect.gameObject.activeSelf) row.rect.gameObject.SetActive(false);
                    continue;
                }
                if (!row.rect.gameObject.activeSelf) row.rect.gameObject.SetActive(true);
                var e = entries[entries.Count - 1 - k];
                var h = row.Set(e, k == 0, width);
                row.rect.anchoredPosition = new Vector2(0f, y);
                row.rect.sizeDelta = new Vector2(0f, h);
                row.bottom = y;
                row.height = h;
                y += h + RowGap;
            }
            shown = entries.Count;
            scroll.Fit(entries.Count > 0 ? y - RowGap : 0f, viewport.rect.height);
        }

        RowView MakeRow(int index)
        {
            var view = new RowView();
            view.rect = Rect(content, "Row" + index);
            view.rect.anchorMin = new Vector2(0f, 0f);
            view.rect.anchorMax = new Vector2(1f, 0f);
            view.rect.pivot = new Vector2(0.5f, 0f);
            view.group = view.rect.gameObject.AddComponent<CanvasGroup>();
            view.group.blocksRaycasts = false;
            var tag = Fill(view.rect, "Tag", TagFill, false);
            view.tag = tag.rectTransform;
            view.tag.anchorMin = new Vector2(0f, 1f);
            view.tag.anchorMax = new Vector2(0f, 1f);
            view.tag.pivot = new Vector2(0f, 1f);
            view.tagText = Text(view.tag, "Text", TagFont, Ink, TextAlignmentOptions.Center);
            view.tagText.fontStyle = FontStyles.Bold;
            Heavy(view.tagText);
            Stretch(view.tagText.rectTransform, 0f, 0f, 0f, 0f);
            view.who = Text(view.rect, "Who", RowFont, Accent, TextAlignmentOptions.TopLeft);
            view.text = Text(view.rect, "Text", RowFont, RowText, TextAlignmentOptions.TopLeft);
            view.who.lineSpacing = RowSpacing;
            view.text.lineSpacing = RowSpacing;
            view.text.textWrappingMode = TextWrappingModes.Normal;
            return view;
        }

        /// <summary>選び・さかのぼり・薄れ・つまみ・一覧を、いまの状態に塗る</summary>
        void Paint()
        {
            for (var i = 0; i < buttons.Count; i++) buttons[i].Paint(i == menu.Index);
            content.anchoredPosition = new Vector2(0f, -scroll.Back);
            var view = scroll.View;
            for (var k = 0; k < shown && k < rows.Count; k++)
            {
                var row = rows[k];
                // 枠の上の縁からどれだけ下にあるか。上の FadeBand のあいだで薄れて消える
                var middle = row.bottom + row.height * 0.5f - scroll.Back;
                var fromTop = view > 0f ? (view - middle) / view : 1f;
                row.group.alpha = Mathf.Clamp01(fromTop / FadeBand);
            }
            thumb.anchorMin = new Vector2(0f, scroll.ThumbBottom);
            thumb.anchorMax = new Vector2(1f, scroll.ThumbBottom + scroll.Thumb);
            box.gameObject.SetActive(menu.Listing);
            if (menu.Listing)
            {
                for (var i = 0; i < places.Count; i++)
                {
                    places[i].Paint(i == menu.Row);
                    places[i].mark.text = SceneMenu.Scenes[i] == Here ? HereMark : string.Empty;
                    places[i].mark.color = i == menu.Row ? Ink : Accent;
                }
            }
            var size = root.rect.size;
            if (size.x > 0f && size.y > 0f) scan.uvRect = new Rect(0f, 0f, 1f, size.y / (3f * Dot));
        }

        // ---- 部品 ------------------------------------------------------------

        sealed class ButtonView
        {
            public Image fill;
            public Image[] lines;
            public TMP_Text label;
            public TMP_Text mark;

            /// <summary>選んでいる物は塗りつぶし、字を暗くする</summary>
            public void Paint(bool on)
            {
                fill.color = on ? Accent : Clear;
                label.color = on ? Ink : ButtonText;
            }
        }

        sealed class RowView
        {
            public RectTransform rect;
            public CanvasGroup group;
            public RectTransform tag;
            public TMP_Text tagText;
            public TMP_Text who;
            public TMP_Text text;
            public float bottom;
            public float height;

            /// <summary>1 行を入れて、その高さを返す。札・名前・文を左から並べ、文だけ折り返す</summary>
            public float Set(LogEntry e, bool newest, float width)
            {
                tagText.text = LogEntry.Tag(e.kind);
                // 札の幅は三字の札に揃える。二字の札で名前の列が左へずれないように
                var tagWidth = TagFont * 3f + TagPad * 2f;
                tag.sizeDelta = new Vector2(tagWidth, TagHeight);
                tag.anchoredPosition = new Vector2(0f, -(RowLine - TagHeight) * 0.5f);
                who.text = e.who;
                var whoWidth = Mathf.Max(WhoMin, string.IsNullOrEmpty(e.who) ? 0f : who.GetPreferredValues(e.who).x);
                var whoLeft = tagWidth + CellGap;
                Place(who.rectTransform, whoLeft, whoWidth, RowLine);
                var textLeft = whoLeft + whoWidth + CellGap;
                var textWidth = Mathf.Max(1f, width - textLeft);
                text.color = newest ? Color.white : RowText;
                text.text = Ruby.Expand(e.text);
                var h = Mathf.Max(RowLine, text.GetPreferredValues(text.text, textWidth, 0f).y);
                Place(text.rectTransform, textLeft, textWidth, h);
                return h;
            }

            static void Place(RectTransform r, float left, float width, float height)
            {
                r.anchorMin = new Vector2(0f, 1f);
                r.anchorMax = new Vector2(0f, 1f);
                r.pivot = new Vector2(0f, 1f);
                r.anchoredPosition = new Vector2(left, 0f);
                r.sizeDelta = new Vector2(width, height);
            }
        }

        /// <summary>塗りつぶしの上に置く暗い字を太らせる</summary>
        void Heavy(TMP_Text text)
        {
            if (heavy != null) text.fontSharedMaterial = heavy;
        }

        static RectTransform Rect(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = 5;
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        /// <summary>親いっぱいから、左・右・上・下をそれだけ引いた形</summary>
        static void Stretch(RectTransform r, float left, float right, float top, float bottom)
        {
            r.anchorMin = Vector2.zero;
            r.anchorMax = Vector2.one;
            r.pivot = new Vector2(0.5f, 0.5f);
            r.offsetMin = new Vector2(left, bottom);
            r.offsetMax = new Vector2(-right, -top);
        }

        /// <summary>親の上の縁から top 下げた、高さ height の帯。左右は left・right だけ引く</summary>
        static void Top(RectTransform r, float left, float right, float top, float height)
        {
            r.anchorMin = new Vector2(0f, 1f);
            r.anchorMax = new Vector2(1f, 1f);
            r.pivot = new Vector2(0.5f, 1f);
            r.offsetMin = new Vector2(left, -top - height);
            r.offsetMax = new Vector2(-right, -top);
        }

        static Image Fill(Transform parent, string name, Color color, bool blocks)
        {
            var r = Rect(parent, name);
            Stretch(r, 0f, 0f, 0f, 0f);
            var img = r.gameObject.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = blocks;
            return img;
        }

        static TMP_Text Text(Transform parent, string name, float size, Color color, TextAlignmentOptions align)
        {
            var r = Rect(parent, name);
            var t = r.gameObject.AddComponent<TextMeshProUGUI>();
            t.fontSize = size;
            t.color = color;
            t.alignment = align;
            t.raycastTarget = false;
            t.richText = true;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.text = string.Empty;
            return t;
        }

        /// <summary>内側に引く細い線。上・下・左・右の 4 本</summary>
        static Image[] Border(RectTransform r, Color color, float width)
        {
            var lines = new Image[4];
            for (var i = 0; i < 4; i++)
            {
                lines[i] = Fill(r, "Line" + i, color, false);
                var t = lines[i].rectTransform;
                var horizontal = i < 2;
                t.anchorMin = horizontal ? new Vector2(0f, i == 0 ? 1f : 0f) : new Vector2(i == 2 ? 0f : 1f, 0f);
                t.anchorMax = horizontal ? new Vector2(1f, i == 0 ? 1f : 0f) : new Vector2(i == 2 ? 0f : 1f, 1f);
                t.pivot = horizontal ? new Vector2(0.5f, i == 0 ? 1f : 0f) : new Vector2(i == 2 ? 0f : 1f, 0.5f);
                t.anchoredPosition = Vector2.zero;
                t.sizeDelta = horizontal ? new Vector2(0f, width) : new Vector2(width, 0f);
            }
            return lines;
        }

        /// <summary>四隅の鉤。枠の角から少し外へ出す</summary>
        static void Hooks(RectTransform panel)
        {
            for (var c = 0; c < 4; c++)
            {
                var x = c % 2 == 0 ? 0f : 1f;
                var y = c < 2 ? 1f : 0f;
                var hook = Rect(panel, "Hook" + c);
                hook.anchorMin = new Vector2(x, y);
                hook.anchorMax = new Vector2(x, y);
                hook.pivot = new Vector2(x, y);
                var outward = Line;
                hook.anchoredPosition = new Vector2(x == 0f ? -outward : outward, y == 0f ? -outward : outward);
                hook.sizeDelta = new Vector2(HookSize, HookSize);
                var across = Fill(hook, "Across", Accent, false).rectTransform;
                across.anchorMin = new Vector2(0f, y);
                across.anchorMax = new Vector2(1f, y);
                across.pivot = new Vector2(0.5f, y);
                across.anchoredPosition = Vector2.zero;
                across.sizeDelta = new Vector2(0f, HookLine);
                var down = Fill(hook, "Down", Accent, false).rectTransform;
                down.anchorMin = new Vector2(x, 0f);
                down.anchorMax = new Vector2(x, 1f);
                down.pivot = new Vector2(x, 0.5f);
                down.anchoredPosition = Vector2.zero;
                down.sizeDelta = new Vector2(HookLine, 0f);
            }
        }
    }
}
