using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace HalfAware
{
    /// <summary>
    /// タイトルの画面（設計書 5 節、案 1「インプラントの起動」）。
    ///
    /// 黒い画面に、眼球インプラントが立ち上がる短い表示が一行ずつ流れ、そのあと背景が明けて、
    /// コンソールと同じ青緑の枠・題（HALF AWARE／かたあはれ）・ボタン（はじめる・思い出す）が出る。制作の表記は出さない。
    ///
    /// **背景は前もって撮った絵。** クリアの印があれば朝の村、無ければいちばん新しいセーブの場面、セーブが無ければ自室
    /// （<see cref="TitleBackdrops.Pick"/>）。タイトルのために重い場面を読まない。絵は組み立ての道具
    /// （HalfAware/Shoot the title backgrounds）で撮り直す。
    ///
    /// 背景は画面の解像度の Canvas に最近傍で引き伸ばして敷き、暗く沈める（明るい朝の村は沈め方を弱める）。
    /// 枠・字・ボタンはコンソールと同じ粗い画面（<see cref="UiLens"/>）で描く。
    /// 見た目はここで組む（シーンに置くのはこの部品と、絵・音・書体の参照だけ）
    /// </summary>
    public sealed class TitleScreen : MonoBehaviour
    {
        public const string SceneName = SaveFlow.TitleScene;

        // ---- 文言 --------------------------------------------------------------

        public const string Implant = "OCULAR IMPLANT";
        public const string Version = "v4.12";
        public const string Sight = "視覚補助";
        public const string SightOk = "正常";
        public const string Memory = "記憶領域";
        public const string Checking = "照合中…";
        /// <summary>
        /// 項目と値のあいだの罫。案は細い罫（U+2500）だが、Noto Sans JP の細い罫は線の太さが 0.04 em しかなく、
        /// 粗い画面では 1 画素に満たずに消える。太い罫（U+2501、0.12 em）にして 1 画素の線で残す
        /// </summary>
        public const string Rule = "━━";
        public const string Name = "HALF AWARE";
        public const string Kana = "かたあはれ";
        public const string Begin = "はじめる";
        public const string Back = "戻る";
        public const string ListTitle = "思い出す　　E で読む";

        /// <summary>ボタンの字。並びの順</summary>
        public static readonly string[] Labels = { Begin, ConsoleMenu.Recall };

        // ---- 見た目 ------------------------------------------------------------
        //
        // 案 1 の CSS を手本に、位置は画面に対する割合のまま、寸法はコンソールと同じ粗い画面の 1 画素（Dot）で決める。
        // 字はいちばん小さいものでも 11 Dot（粗い画面の中で縦 10 画素ほど）

        const float Dot = ImplantConsole.Dot;
        /// <summary>粗い画面の中での重なりの順。コンソール（500）より下</summary>
        public const int SortingOrder = 0;
        /// <summary>背景の Canvas の重なりの順。粗い画面を重ねる層（<see cref="UiLens.ShowOrder"/>）より下</summary>
        public const int BackdropOrder = -100;

        const float FrameInset = 0.05f;
        const float BootLeft = 0.06f;
        const float BootTop = 0.08f;
        const float NameTop = 0.36f;
        const float KanaTop = 0.46f;
        const float MenuTop = 0.60f;
        const float ListTop = 0.56f;

        const float Line = 1f * Dot;
        const float HookSize = 12f * Dot;
        const float HookLine = 2f * Dot;
        const float BootFont = 11f * Dot;
        const float BootStep = 18f * Dot;
        /// <summary>0.06 em</summary>
        const float BootSpacing = 6f;
        const float NameFont = 22f * Dot;
        /// <summary>題の字間。案の 0.45 em から詰めて、朝の村の画角でアーチの口の内に収める（0.22 em）</summary>
        public const float NameSpacing = 22f;
        /// <summary>読みの字。明朝の細い線が粗い画面で切れないよう、いちばん小さい字（11 Dot）より一回り上げる</summary>
        const float KanaFont = 13f * Dot;
        const float KanaSpacing = 60f;
        const float ButtonWidth = 165f * Dot;
        const float ButtonHeight = 28f * Dot;
        const float ButtonGap = 10f * Dot;
        const float ButtonFont = 12f * Dot;
        const float ButtonSpacing = 20f;
        const float BoxWidth = 280f * Dot;
        const float BoxRow = 22f * Dot;
        const float BoxPad = 12f * Dot;
        const float HeadHeight = 14f * Dot;
        const float RowFont = 11f * Dot;
        const float RowInset = 7f * Dot;

        // 色は使う時に作る。静的な値の初期化で色空間を訊くと、シーンから読まれた時（部品の生成の途中）に Unity が止める。
        // ImplantConsole の色も、ここの静的な初期化からは触らない（あちらの初期化も色空間を訊く）
        static Color FrameLine { get { return ImplantConsole.Tint(ImplantConsole.Rgb(127, 227, 236, 0.25f)); } }
        static Color BootText { get { return ImplantConsole.Tint(ImplantConsole.Rgb(127, 227, 236, 0.75f)); } }
        const string BootBright = "#cff7fa";
        static Color NameColor { get { return ImplantConsole.Rgb(0xe9, 0xfb, 0xfc, 1f); } }
        static Color KanaColor { get { return ImplantConsole.Tint(ImplantConsole.Rgb(207, 247, 250, 0.7f)); } }
        static Color ButtonFill { get { return ImplantConsole.Veil(ImplantConsole.Rgb(3, 10, 14, 0.35f)); } }
        static Color Glow { get { return ImplantConsole.Rgb(127, 227, 236, 0.35f); } }
        static Color KanaShade { get { return ImplantConsole.Rgb(0, 0, 0, 0.8f); } }
        static Color Shade { get { return ImplantConsole.Rgb(2, 8, 11, 1f); } }
        const float OffAlpha = 0.35f;

        // ---- 時間（unscaled の秒） ----------------------------------------------

        const float FirstLine = 0.45f;
        const float LineGap = 0.4f;
        const float LastLineGap = 0.55f;
        const float RevealAfter = 0.5f;
        const float RevealSeconds = 1.1f;
        /// <summary>明け始めてからボタンが出るまで</summary>
        const float ButtonsAfter = 0.5f;
        const float LeaveSeconds = 0.6f;

        public const float DefaultVolume = 0.3f;

        // ---- シーンに置く物 -----------------------------------------------------

        [Tooltip("背景の絵。TitleBackdrop の並び（自室・路地裏・潜る・車内・村の朝・村の夕方）")]
        [SerializeField] Texture2D[] backdrops = new Texture2D[TitleBackdrops.Count];
        [Tooltip("背景の場所の環境音。TitleBackdrop の並び。無い所は無音")]
        [SerializeField] AudioClip[] sounds = new AudioClip[TitleBackdrops.Count];
        [Tooltip("環境音の大きさ。小さく流す")]
        [SerializeField, Range(0f, 1f)] float volume = DefaultVolume;
        [SerializeField] AudioSource sound;
        [Tooltip("題の書体（しっぽり明朝）")]
        [SerializeField] TMP_FontAsset mincho;
        [Tooltip("背景が潜る（場面 4）のときの、起動の表示の日時と場所。組み立てが記憶の一覧から拾う")]
        [SerializeField] string divingLine = "";

        // ---- 状態 ------------------------------------------------------------

        enum Phase { Boot, Menu, Recall, Leaving }

        Phase phase = Phase.Boot;
        int index;
        int row;
        bool canRecall;
        TitleBackdrop backdrop;
        readonly SaveData[] slots = new SaveData[SaveStore.All.Length];

        Canvas backCanvas;
        Canvas lensCanvas;
        RectTransform root;
        RawImage picture;
        Image cover;
        RawImage scan;
        CanvasGroup chrome;
        CanvasGroup menuGroup;
        RectTransform list;
        readonly List<TMP_Text> bootLines = new List<TMP_Text>();
        readonly List<Row> buttons = new List<Row>();
        readonly List<Row> rows = new List<Row>();

        Texture2D dimTexture;
        Texture2D scanTexture;
        Material heavy;
        Material glow;
        Material shade;

        /// <summary>いま出している背景</summary>
        public TitleBackdrop Backdrop { get { return backdrop; } }

        /// <summary>背景を敷く Canvas。画面の解像度で描く</summary>
        public Canvas BackdropCanvas { get { return backCanvas; } }

        /// <summary>枠・字・ボタンの Canvas。粗い画面で描く</summary>
        public Canvas ScreenCanvas { get { return lensCanvas; } }

        // ---- 始まり ------------------------------------------------------------

        void Start()
        {
            // コンソールから目を閉じて来たときも、秒とカーソルはここで揃える
            Time.timeScale = 1f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            Guide();
            Compose(SaveStore.Cleared, SaveStore.Newest());
            UiLens.Adopt(lensCanvas, SortingOrder);
            Play();
            StartCoroutine(Boot());
        }

        void OnDestroy()
        {
            Release();
        }

        /// <summary>マウスの受け口。コンソールが持っていればそれを使い、無ければここに持つ</summary>
        void Guide()
        {
            if (FindFirstObjectByType<EventSystem>() != null) return;
            var go = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            go.transform.SetParent(transform, false);
            go.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
        }

        /// <summary>
        /// 背景を選んで、画面を組む。起動の表示はまだ出さない。
        /// エディタで撮るときはこれで組み、<see cref="Finish"/> で出し切ってから撮り、<see cref="Release"/> して消す
        /// </summary>
        public void Compose(bool cleared, SaveData newest)
        {
            backdrop = TitleBackdrops.Pick(cleared, newest);
            for (var i = 0; i < slots.Length; i++) slots[i] = SaveStore.Read(SaveStore.All[i]);
            canRecall = false;
            for (var i = 0; i < slots.Length; i++) canRecall |= slots[i] != null;
            BuildBackdrop();
            BuildScreen(TitleBackdrops.BootPlace(cleared, newest, divingLine));
            Hide();
        }

        /// <summary>作った絵とマテリアルを捨てる。エディタでは OnDestroy が呼ばれないので、撮り終えて消す前にこれを呼ぶ</summary>
        public void Release()
        {
            Drop(ref dimTexture);
            Drop(ref scanTexture);
            Drop(ref heavy);
            Drop(ref glow);
            Drop(ref shade);
        }

        static void Drop<T>(ref T made) where T : UnityEngine.Object
        {
            if (made == null) return;
            if (Application.isPlaying) Destroy(made);
            else DestroyImmediate(made);
            made = null;
        }

        void Play()
        {
            if (sound == null) return;
            var clip = sounds != null && (int)backdrop < sounds.Length ? sounds[(int)backdrop] : null;
            if (clip == null) return;
            sound.clip = clip;
            sound.loop = true;
            sound.spatialBlend = 0f;
            sound.volume = 0f;
            sound.Play();
        }

        // ---- 流れ ------------------------------------------------------------

        /// <summary>起動の前。黒い画面で、何も出ていない</summary>
        void Hide()
        {
            phase = Phase.Boot;
            cover.color = Color.black;
            chrome.alpha = 0f;
            chrome.blocksRaycasts = false;
            menuGroup.alpha = 0f;
            for (var i = 0; i < bootLines.Count; i++) bootLines[i].gameObject.SetActive(false);
            list.gameObject.SetActive(false);
        }

        /// <summary>インプラントの起動。表示を一行ずつ流し、背景を明けて、枠と題とボタンを出す</summary>
        IEnumerator Boot()
        {
            for (var i = 0; i < bootLines.Count; i++)
            {
                yield return new WaitForSecondsRealtime(i == 0 ? FirstLine : i == bootLines.Count - 1 ? LastLineGap : LineGap);
                bootLines[i].gameObject.SetActive(true);
            }
            yield return new WaitForSecondsRealtime(RevealAfter);
            var start = Time.unscaledTime;
            while (true)
            {
                var t = Mathf.Clamp01((Time.unscaledTime - start) / RevealSeconds);
                Reveal(t, Mathf.Clamp01((Time.unscaledTime - start - ButtonsAfter) / (RevealSeconds - ButtonsAfter)));
                if (t >= 1f && Time.unscaledTime - start >= RevealSeconds) break;
                yield return null;
            }
            Finish();
        }

        /// <summary>明けの途中。t は背景と枠と題、buttonsT はボタン</summary>
        void Reveal(float t, float buttonsT)
        {
            Crop();
            cover.color = new Color(0f, 0f, 0f, 1f - t);
            chrome.alpha = t;
            menuGroup.alpha = buttonsT;
            if (sound != null && sound.clip != null) sound.volume = volume * t;
        }

        /// <summary>起動を出し切って、ボタンを選べる形にする。途中でキーかクリックが来たら飛ばしてここへ</summary>
        public void Finish()
        {
            StopAllCoroutines();
            for (var i = 0; i < bootLines.Count; i++) bootLines[i].gameObject.SetActive(true);
            Reveal(1f, 1f);
            chrome.blocksRaycasts = true;
            phase = Phase.Menu;
            index = 0;
            Paint();
        }

        /// <summary>
        /// 思い出すの枠を開いた形にする。エディタで撮るときに使う
        /// </summary>
        public void OpenList()
        {
            if (!canRecall) return;
            phase = Phase.Recall;
            row = NextRow(-1, 1);
            Paint();
        }

        void CloseList()
        {
            phase = Phase.Menu;
            Paint();
        }

        /// <summary>画面を閉じて、黒く落としてから then を呼ぶ（場面を読む）</summary>
        void Leave(Action then)
        {
            phase = Phase.Leaving;
            chrome.blocksRaycasts = false;
            StartCoroutine(Leaving(then));
        }

        IEnumerator Leaving(Action then)
        {
            var start = Time.unscaledTime;
            var from = sound != null ? sound.volume : 0f;
            while (true)
            {
                var t = Mathf.Clamp01((Time.unscaledTime - start) / LeaveSeconds);
                cover.color = new Color(0f, 0f, 0f, t);
                chrome.alpha = 1f - t;
                for (var i = 0; i < bootLines.Count; i++) bootLines[i].alpha = 1f - t;
                if (sound != null) sound.volume = from * (1f - t);
                if (t >= 1f) break;
                yield return null;
            }
            then();
        }

        // ---- 入力 ------------------------------------------------------------

        void Update()
        {
            var keys = Keyboard.current;
            var mouse = Mouse.current;
            if (phase == Phase.Boot)
            {
                // 起動の表示は、キーかクリックで飛ばせる
                var any = (keys != null && keys.anyKey.wasPressedThisFrame)
                    || (mouse != null && mouse.leftButton.wasPressedThisFrame);
                if (any) Finish();
                return;
            }
            if (phase == Phase.Leaving) return;
            if (keys != null)
            {
                var up = keys.upArrowKey.wasPressedThisFrame || keys.wKey.wasPressedThisFrame;
                var down = keys.downArrowKey.wasPressedThisFrame || keys.sKey.wasPressedThisFrame;
                var decide = keys.eKey.wasPressedThisFrame || keys.enterKey.wasPressedThisFrame
                    || keys.numpadEnterKey.wasPressedThisFrame;
                if (phase == Phase.Menu)
                {
                    if (up) Move(-1);
                    if (down) Move(1);
                    if (decide) Decide();
                }
                else if (phase == Phase.Recall)
                {
                    if (up) row = NextRow(row, -1);
                    if (down) row = NextRow(row, 1);
                    if (keys.escapeKey.wasPressedThisFrame) CloseList();
                    else if (decide) Pick();
                }
            }
            if (phase == Phase.Leaving) return;
            Paint();
            // 押したボタンが選ばれたままだと、矢印の入力を uGUI が横取りする
            var events = EventSystem.current;
            if (events != null && events.currentSelectedGameObject != null) events.SetSelectedGameObject(null);
        }

        /// <summary>ボタンの上下。押せないボタン（セーブが無い時の思い出す）は飛ばす</summary>
        void Move(int step)
        {
            var next = Mathf.Clamp(index + step, 0, Labels.Length - 1);
            if (Usable(next)) index = next;
        }

        bool Usable(int button)
        {
            return button == 0 || canRecall;
        }

        void Decide()
        {
            if (index == 0) Leave(SaveFlow.StartNew);
            else if (canRecall) OpenList();
        }

        /// <summary>思い出すの行を決める。いちばん下は戻る</summary>
        void Pick()
        {
            if (row == slots.Length)
            {
                CloseList();
                return;
            }
            if (!RowUsable(row)) return;
            var slot = SaveStore.All[row];
            if (!SaveFlow.CanResume(slot)) return;
            Leave(() => SaveFlow.Resume(slot));
        }

        /// <summary>思い出すの行を選べるか。空きは選べない。戻るはいつでも</summary>
        bool RowUsable(int r)
        {
            if (r == slots.Length) return true;
            return r >= 0 && r < slots.Length && slots[r] != null;
        }

        int NextRow(int from, int step)
        {
            for (var r = from + step; r >= 0 && r <= slots.Length; r += step)
                if (RowUsable(r)) return r;
            return RowUsable(from) ? from : slots.Length;
        }

        // ---- 塗る ------------------------------------------------------------

        /// <summary>
        /// 背景の絵を、縦横比を保って画面いっぱいに切る。画面が横に長ければ上下を、縦に長ければ左右を切る
        /// </summary>
        void Crop()
        {
            if (picture == null || picture.texture == null) return;
            var size = ((RectTransform)backCanvas.transform).rect.size;
            if (size.x <= 0f || size.y <= 0f) return;
            var pictureAspect = picture.texture.width / (float)picture.texture.height;
            var screenAspect = size.x / size.y;
            if (screenAspect >= pictureAspect)
            {
                var h = pictureAspect / screenAspect;
                picture.uvRect = new Rect(0f, (1f - h) * 0.5f, 1f, h);
            }
            else
            {
                var w = screenAspect / pictureAspect;
                picture.uvRect = new Rect((1f - w) * 0.5f, 0f, w, 1f);
            }
        }

        void Paint()
        {
            Crop();
            var menu = phase == Phase.Menu;
            menuGroup.gameObject.SetActive(phase != Phase.Recall);
            for (var i = 0; i < buttons.Count; i++)
            {
                var on = menu && i == index;
                var b = buttons[i];
                b.fill.color = on ? ImplantConsole.Accent : ButtonFill;
                b.label.color = on ? ImplantConsole.Ink : ImplantConsole.ButtonText;
                b.group.alpha = Usable(i) ? 1f : OffAlpha;
            }
            list.gameObject.SetActive(phase == Phase.Recall);
            if (phase == Phase.Recall)
                for (var r = 0; r < rows.Count; r++)
                {
                    var on = r == row;
                    var usable = RowUsable(r);
                    rows[r].fill.color = on ? ImplantConsole.Accent : ImplantConsole.Clear;
                    rows[r].label.color = on ? ImplantConsole.Ink : usable ? ImplantConsole.ButtonText : ImplantConsole.Faded;
                    rows[r].mark.color = on ? ImplantConsole.Ink : usable ? ImplantConsole.RowText : ImplantConsole.Faded;
                }
            var size = root.rect.size;
            if (size.x > 0f && size.y > 0f) scan.uvRect = new Rect(0f, 0f, 1f, size.y / (3f * Dot));
        }

        // ---- 組む ------------------------------------------------------------

        sealed class Row
        {
            public Image fill;
            public TMP_Text label;
            public TMP_Text mark;
            public CanvasGroup group;
        }

        /// <summary>背景の Canvas。絵を最近傍で画面いっぱいに（縦横比を保って切って）敷き、暗く沈め、黒い幕をかぶせる</summary>
        void BuildBackdrop()
        {
            var go = new GameObject("Backdrop", typeof(RectTransform));
            go.transform.SetParent(transform, false);
            backCanvas = go.AddComponent<Canvas>();
            backCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            backCanvas.sortingOrder = BackdropOrder;

            var pic = new GameObject("Picture", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
            pic.transform.SetParent(go.transform, false);
            picture = pic.GetComponent<RawImage>();
            picture.raycastTarget = false;
            picture.texture = backdrops != null && (int)backdrop < backdrops.Length ? backdrops[(int)backdrop] : null;
            picture.color = picture.texture != null ? Color.white : Color.black;
            ImplantConsole.Stretch(picture.rectTransform, 0f, 0f, 0f, 0f);

            dimTexture = MakeDim(TitleBackdrops.Bright(backdrop));
            var dim = new GameObject("Dim", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
            dim.transform.SetParent(go.transform, false);
            var dimImage = dim.GetComponent<RawImage>();
            dimImage.texture = dimTexture;
            dimImage.raycastTarget = false;
            ImplantConsole.Stretch(dimImage.rectTransform, 0f, 0f, 0f, 0f);

            cover = ImplantConsole.Fill(go.transform, "Cover", Color.black, false);
        }

        /// <summary>
        /// 背景を沈める暗さ。案の CSS の楕円のグラデーション（中心は横の真ん中、上から 55%）。
        /// u は左から、v は上から（0〜1）。ふつうは 0.45 から縁の 0.82 へ。
        /// 明るい背景は、題の後ろだけ少し沈め（0.42）、その外は 0.18 まで明るく残し、縁で 0.62
        /// </summary>
        public static float DimAt(float u, float v, bool bright)
        {
            var du = (u - 0.5f) / 0.5f;
            var dv = (v - 0.55f) / 0.55f;
            var t = Mathf.Sqrt(du * du + dv * dv) / Mathf.Sqrt(2f);
            if (!bright) return Mathf.Lerp(0.45f, 0.82f, Mathf.Clamp01(t / 0.75f));
            if (t < 0.45f) return Mathf.Lerp(0.42f, 0.18f, t / 0.45f);
            return Mathf.Lerp(0.18f, 0.62f, Mathf.Clamp01((t - 0.45f) / 0.4f));
        }

        static Texture2D MakeDim(bool bright)
        {
            const int w = 128;
            const int h = 72;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.name = "TitleDim";
            tex.hideFlags = HideFlags.DontSave;
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            var px = new Color[w * h];
            for (var y = 0; y < h; y++)
                for (var x = 0; x < w; x++)
                {
                    var c = Shade;
                    c.a = DimAt((x + 0.5f) / w, 1f - (y + 0.5f) / h, bright);
                    px[y * w + x] = ImplantConsole.Veil(c);
                }
            tex.SetPixels(px);
            tex.Apply();
            return tex;
        }

        /// <summary>枠・起動の表示・題・ボタン・思い出すの枠。粗い画面で描く Canvas</summary>
        void BuildScreen(string place)
        {
            var go = new GameObject("Screen", typeof(RectTransform));
            go.layer = UiLens.UiLayer;
            go.transform.SetParent(transform, false);
            lensCanvas = go.AddComponent<Canvas>();
            lensCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            lensCanvas.sortingOrder = SortingOrder;
            // コンソールと同じ拡縮。画面の大きさが変わっても字と余白の釣り合いが崩れない
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            go.AddComponent<GraphicRaycaster>();
            Materials();

            root = ImplantConsole.Rect(go.transform, "Root");
            ImplantConsole.Stretch(root, 0f, 0f, 0f, 0f);
            scan = Scanlines(root);
            BuildBoot(root, place);

            var c = ImplantConsole.Rect(root, "Chrome");
            ImplantConsole.Stretch(c, 0f, 0f, 0f, 0f);
            chrome = c.gameObject.AddComponent<CanvasGroup>();
            Frame(c);
            Titles(c);
            BuildMenu(c);
            BuildList(c);
        }

        /// <summary>塗りつぶしの上の暗い字を太らせる色づけと、題の淡い光</summary>
        void Materials()
        {
            var font = TMP_Settings.defaultFontAsset;
            if (font != null && font.material != null)
            {
                heavy = new Material(font.material);
                heavy.name = "TitleHeavy";
                heavy.hideFlags = HideFlags.DontSave;
                if (heavy.HasProperty(ShaderUtilities.ID_FaceDilate)) heavy.SetFloat(ShaderUtilities.ID_FaceDilate, 0.3f);
            }
            if (mincho != null && mincho.material != null)
            {
                // 題は青緑の淡い光、読みは暗い影（案の text-shadow）。明るい朝の花の前でも字の縁が溶けないように
                glow = Underlaid(mincho.material, "TitleGlow", Glow, 0.8f, 0.6f);
                shade = Underlaid(mincho.material, "TitleShade", KanaShade, 0.6f, 0.5f);
            }
        }

        static Material Underlaid(Material from, string label, Color color, float softness, float dilate)
        {
            var m = new Material(from);
            m.name = label;
            m.hideFlags = HideFlags.DontSave;
            if (!m.HasProperty(ShaderUtilities.ID_UnderlayColor)) return m;
            m.EnableKeyword(ShaderUtilities.Keyword_Underlay);
            m.SetColor(ShaderUtilities.ID_UnderlayColor, color);
            m.SetFloat(ShaderUtilities.ID_UnderlaySoftness, softness);
            m.SetFloat(ShaderUtilities.ID_UnderlayDilate, dilate);
            return m;
        }

        RawImage Scanlines(RectTransform parent)
        {
            scanTexture = new Texture2D(1, 3, TextureFormat.RGBA32, false);
            scanTexture.name = "TitleScan";
            scanTexture.hideFlags = HideFlags.DontSave;
            scanTexture.filterMode = FilterMode.Point;
            scanTexture.wrapMode = TextureWrapMode.Repeat;
            scanTexture.SetPixels(new[] { ImplantConsole.Clear, ImplantConsole.Clear, ImplantConsole.Scan });
            scanTexture.Apply();
            var go = new GameObject("Scan", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
            go.layer = UiLens.UiLayer;
            go.transform.SetParent(parent, false);
            var raw = go.GetComponent<RawImage>();
            raw.texture = scanTexture;
            raw.raycastTarget = false;
            ImplantConsole.Stretch(raw.rectTransform, 0f, 0f, 0f, 0f);
            return raw;
        }

        /// <summary>起動の表示。左上に四行。英数字は等幅、値だけ明るく</summary>
        void BuildBoot(RectTransform parent, string place)
        {
            var lines = new[]
            {
                ImplantConsole.Mono(Implant) + "　" + Bright(ImplantConsole.Mono(Version)),
                Sight + " " + Rule + " " + Bright(SightOk),
                Memory + " " + Rule + " " + Checking,
                ImplantConsole.Mono(place),
            };
            for (var i = 0; i < lines.Length; i++)
            {
                var t = ImplantConsole.Text(parent, "Boot" + i, BootFont, BootText, TextAlignmentOptions.TopLeft);
                var r = t.rectTransform;
                r.anchorMin = new Vector2(BootLeft, 1f - BootTop);
                r.anchorMax = new Vector2(1f - BootLeft, 1f - BootTop);
                r.pivot = new Vector2(0f, 1f);
                r.anchoredPosition = new Vector2(0f, -BootStep * i);
                r.sizeDelta = new Vector2(0f, BootStep);
                t.characterSpacing = BootSpacing;
                t.text = lines[i];
                bootLines.Add(t);
            }
        }

        static string Bright(string text)
        {
            return "<color=" + BootBright + ">" + text + "</color>";
        }
        /// <summary>青緑の細い枠。鉤は左上と右下だけ（案 1）</summary>
        void Frame(RectTransform parent)
        {
            var frame = ImplantConsole.Rect(parent, "Frame");
            frame.anchorMin = new Vector2(FrameInset, FrameInset);
            frame.anchorMax = new Vector2(1f - FrameInset, 1f - FrameInset);
            frame.offsetMin = Vector2.zero;
            frame.offsetMax = Vector2.zero;
            ImplantConsole.Border(frame, FrameLine, Line);
            Hook(frame, 0f, 1f);
            Hook(frame, 1f, 0f);
        }

        static void Hook(RectTransform frame, float x, float y)
        {
            var hook = ImplantConsole.Rect(frame, "Hook");
            hook.anchorMin = new Vector2(x, y);
            hook.anchorMax = new Vector2(x, y);
            hook.pivot = new Vector2(x, y);
            hook.anchoredPosition = new Vector2(x == 0f ? -Line : Line, y == 0f ? -Line : Line);
            hook.sizeDelta = new Vector2(HookSize, HookSize);
            var across = ImplantConsole.Fill(hook, "Across", ImplantConsole.Accent, false).rectTransform;
            across.anchorMin = new Vector2(0f, y);
            across.anchorMax = new Vector2(1f, y);
            across.pivot = new Vector2(0.5f, y);
            across.anchoredPosition = Vector2.zero;
            across.sizeDelta = new Vector2(0f, HookLine);
            var down = ImplantConsole.Fill(hook, "Down", ImplantConsole.Accent, false).rectTransform;
            down.anchorMin = new Vector2(x, 0f);
            down.anchorMax = new Vector2(x, 1f);
            down.pivot = new Vector2(x, 0.5f);
            down.anchoredPosition = Vector2.zero;
            down.sizeDelta = new Vector2(HookLine, 0f);
        }

        /// <summary>題（HALF AWARE）と読み（かたあはれ）。明朝で真ん中に控えめに</summary>
        void Titles(RectTransform parent)
        {
            var heading = Centered(parent, "Name", NameFont, NameColor, NameTop);
            heading.characterSpacing = NameSpacing;
            heading.text = Name;
            var kana = Centered(parent, "Kana", KanaFont, KanaColor, KanaTop);
            kana.characterSpacing = KanaSpacing;
            kana.text = Kana;
            if (mincho != null)
            {
                heading.font = mincho;
                kana.font = mincho;
            }
            if (glow != null) heading.fontSharedMaterial = glow;
            if (shade != null) kana.fontSharedMaterial = shade;
        }

        static TMP_Text Centered(RectTransform parent, string label, float size, Color color, float top)
        {
            var t = ImplantConsole.Text(parent, label, size, color, TextAlignmentOptions.Top);
            var r = t.rectTransform;
            r.anchorMin = new Vector2(0f, 1f - top);
            r.anchorMax = new Vector2(1f, 1f - top);
            r.pivot = new Vector2(0.5f, 1f);
            r.anchoredPosition = Vector2.zero;
            r.sizeDelta = new Vector2(0f, size * 1.6f);
            return t;
        }

        /// <summary>はじめる・思い出す。縦に二つ。選んでいる方を塗りつぶす</summary>
        void BuildMenu(RectTransform parent)
        {
            var m = ImplantConsole.Rect(parent, "Menu");
            m.anchorMin = new Vector2(0.5f, 1f - MenuTop);
            m.anchorMax = new Vector2(0.5f, 1f - MenuTop);
            m.pivot = new Vector2(0.5f, 1f);
            m.anchoredPosition = Vector2.zero;
            m.sizeDelta = new Vector2(ButtonWidth, ButtonHeight * Labels.Length + ButtonGap * (Labels.Length - 1));
            menuGroup = m.gameObject.AddComponent<CanvasGroup>();
            for (var i = 0; i < Labels.Length; i++)
            {
                var b = ImplantConsole.Rect(m, "Button" + i);
                ImplantConsole.Top(b, 0f, 0f, (ButtonHeight + ButtonGap) * i, ButtonHeight);
                var view = new Row();
                view.group = b.gameObject.AddComponent<CanvasGroup>();
                view.fill = b.gameObject.AddComponent<Image>();
                view.fill.color = ButtonFill;
                ImplantConsole.Border(b, ImplantConsole.ButtonLine, Line);
                view.label = ImplantConsole.Text(b, "Label", ButtonFont, ImplantConsole.ButtonText, TextAlignmentOptions.Center);
                ImplantConsole.Stretch(view.label.rectTransform, 0f, 0f, 0f, 0f);
                view.label.characterSpacing = ButtonSpacing;
                view.label.fontStyle = FontStyles.Bold;
                if (heavy != null) view.label.fontSharedMaterial = heavy;
                view.label.text = Labels[i];
                var which = i;
                var hit = b.gameObject.AddComponent<ConsolePointer>();
                hit.Entered = () =>
                {
                    if (phase != Phase.Menu || !Usable(which)) return;
                    index = which;
                    Paint();
                };
                hit.Clicked = () =>
                {
                    if (phase != Phase.Menu || !Usable(which)) return;
                    index = which;
                    Decide();
                };
                buttons.Add(view);
            }
        }

        /// <summary>思い出すの枠。自動・1・2・3 と、戻る。空きは薄くして押せない</summary>
        void BuildList(RectTransform parent)
        {
            var count = slots.Length + 1;
            list = ImplantConsole.Rect(parent, "Recall");
            list.anchorMin = new Vector2(0.5f, 1f - ListTop);
            list.anchorMax = new Vector2(0.5f, 1f - ListTop);
            list.pivot = new Vector2(0.5f, 1f);
            list.anchoredPosition = Vector2.zero;
            list.sizeDelta = new Vector2(BoxWidth, BoxPad * 2f + HeadHeight + BoxPad + BoxRow * count);
            var bg = list.gameObject.AddComponent<Image>();
            bg.color = ImplantConsole.BoxFill;
            ImplantConsole.Border(list, ImplantConsole.ButtonLine, Line);
            var title = ImplantConsole.Text(list, "Title", BootFont, ImplantConsole.Accent, TextAlignmentOptions.TopLeft);
            ImplantConsole.Top(title.rectTransform, BoxPad, BoxPad, BoxPad, HeadHeight);
            title.characterSpacing = 8f;
            title.text = ListTitle;
            for (var i = 0; i < count; i++)
            {
                var r = ImplantConsole.Rect(list, "Slot" + i);
                ImplantConsole.Top(r, BoxPad, BoxPad, BoxPad * 2f + HeadHeight + BoxRow * i, BoxRow);
                var view = new Row();
                view.fill = r.gameObject.AddComponent<Image>();
                view.fill.color = ImplantConsole.Clear;
                view.label = ImplantConsole.Text(r, "Label", RowFont, ImplantConsole.ButtonText, TextAlignmentOptions.Left);
                ImplantConsole.Stretch(view.label.rectTransform, RowInset, RowInset, 0f, 0f);
                view.label.fontStyle = FontStyles.Bold;
                view.mark = ImplantConsole.Text(r, "Written", RowFont, ImplantConsole.RowText, TextAlignmentOptions.Right);
                ImplantConsole.Stretch(view.mark.rectTransform, RowInset, RowInset, 0f, 0f);
                view.mark.fontStyle = FontStyles.Bold;
                if (heavy != null)
                {
                    view.label.fontSharedMaterial = heavy;
                    view.mark.fontSharedMaterial = heavy;
                }
                if (i < slots.Length)
                {
                    var d = slots[i];
                    var slot = SaveStore.All[i];
                    view.label.text = ImplantConsole.Mono(SaveStore.NameOf(slot)) + "　"
                        + (d != null ? StageMap.TitleOf(d.stage) : SaveStore.Empty);
                    view.mark.text = d != null ? ImplantConsole.Mono(d.written) : string.Empty;
                }
                else view.label.text = Back;
                var which = i;
                var hit = r.gameObject.AddComponent<ConsolePointer>();
                hit.Entered = () =>
                {
                    if (phase != Phase.Recall || !RowUsable(which)) return;
                    row = which;
                    Paint();
                };
                hit.Clicked = () =>
                {
                    if (phase != Phase.Recall || !RowUsable(which)) return;
                    row = which;
                    Pick();
                };
                rows.Add(view);
            }
        }
    }
}
