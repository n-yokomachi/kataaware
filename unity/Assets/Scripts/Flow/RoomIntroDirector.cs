using System.Collections;
using UnityEngine;
using UnityEngine.Serialization;

namespace HalfAware
{
    /// <summary>
    /// 場面 1 固有の演出。
    ///
    /// **冒頭**（オーナー、2026-09-28）: 黒のまま呼吸の輪を鳴らし（5 秒ほど）、ゆっくり瞬くように明ける（<see cref="WakeBlink"/>）。
    /// 明けきってから SceneFlow の起き上がり（椅子に背を預けて天井を見ていた姿勢から座位へ）を始めさせ（<see cref="SceneFlow.OpeningHeld"/>）、
    /// 起き上がり終えてから最初の独白を流す。暗いうちと瞬きの間は天井を見た形のまま。
    /// 呼吸はジャックが手首から抜けたところで、短く薄れさせて止める（ブツッと切らない）。
    ///
    /// **煙草**（オーナー、2026-09-28）: 調べたら（SceneFlow が目を煙草へ向ける）、箱から一本取る音を鳴らし、同時に SceneFlow が
    /// 原稿の 1 ページ（煙草を取った時の文、RoomScript の cigarette）を出す。読み終えて送り、箱の音も鳴り終わっていたら、その向きのまま
    /// ジッポを開けて火を点け（点いた瞬間に煙草に火が移った音。煙もそこから）、閉じる（<see cref="Cigarette"/>・<see cref="SmokeBeats"/>）。
    /// ジッポの音と火が移った音が鳴りきってから座り始めの向きへゆっくり向き直して、二服吸う。二服目を吐いたところで一度だけ画面を黒く覆い、
    /// 場所と時刻のカードを出して、薄れさせて明ける。吸い終わるまで見回しも移動も受け付けない。
    ///
    /// **ジャケット**: 吸い終わったら、座ったまま椅子の右の卓に置いたジャケットを着る。着る音を鳴らし、正面へ向き直してから、
    /// 音の終わりの少し前に体へ着せて卓のジャケットを消す（着る動きは作らない）。着た後の独白で締め、読み終えたら立ち上がる（SceneFlow の standAfter）。
    /// SceneFlow とは Examined / Say / Freeze / OpeningHeld / Talking だけで繋ぐ。
    ///
    /// **文面（最初の独白・カード・着た後の独白）は台詞の原稿 docs/scenario/01-room.md から写す**（<c>HalfAware/Apply the scenario (room)</c>）。
    /// ここの既定の値は空にしてあり、文面を二か所で持たない。
    ///
    /// **思い出した時**（<see cref="ISceneMemory"/>）は、黒と瞬きと最初の独白を出さず、ジャックがまだなら呼吸を鳴らしておく。
    /// ジャケットを着た後なら着た形に置く。煙草は場に残る物が無いので、調べ済みの印だけでよい
    /// </summary>
    public sealed class RoomIntroDirector : MonoBehaviour, ISceneMemory
    {
        /// <summary>停止に足す余裕。停止が先に切れて、演出の途中で調べられるのを防ぐ</summary>
        public const float FreezeMargin = 0.25f;

        [Header("冒頭の呼吸と明け。遊びながら詰められるよう Inspector に出してある")]
        [Tooltip("呼吸の輪を鳴らす音源（2D）。無くても明けは出る")]
        [SerializeField] AudioSource breath;
        [Tooltip("呼吸の大きさ。自室の空気の音（RoomTone、0.5）と並べて耳で決める")]
        [SerializeField, Range(0f, 1f)] float breathVolume = 0.5f;
        [Tooltip("ジャックが手首から抜けてから、呼吸が消えきるまでの秒")]
        [SerializeField] float breathFadeSeconds = 1.2f;
        [Tooltip("ジャックを抜くしぐさ。抜けたところで呼吸を止める。無ければジャックを調べたところで止める")]
        [SerializeField] JackPull jackPull;
        [Tooltip("この id を調べ済みなら、思い出した時に呼吸を鳴らさない")]
        [SerializeField] string jackId = RoomIds.Jack;
        [Tooltip("黒のまま呼吸だけを聞かせる秒")]
        [SerializeField] float darkSeconds = 5f;
        [Tooltip("瞬き: 開きかけるのにかける秒")]
        [SerializeField] float peekSeconds = 0.9f;
        [Tooltip("瞬き: 開きかけて、どこまで明けるか。0〜1。黒の層を 1 − この値まで薄くする")]
        [SerializeField, Range(0f, 1f)] float glimpse = 0.35f;
        [Tooltip("瞬き: 開きかけたまま止める秒")]
        [SerializeField] float glimpseSeconds = 0.25f;
        [Tooltip("瞬き: 閉じるのにかける秒")]
        [SerializeField] float shutSeconds = 0.55f;
        [Tooltip("瞬き: 閉じてから明け始めるまでの一拍。秒")]
        [SerializeField] float pauseSeconds = 0.8f;
        [Tooltip("瞬き: 明けきるのにかける秒")]
        [SerializeField] float openSeconds = 2.6f;

        [Header("煙草とカード。遊びながら詰められるよう Inspector に出してある")]
        [Tooltip("カードを出したまま止まっている秒数。読む時間（2026-09-28 にオーナーの指示で 1.95 秒の 1.5 倍に延ばした）")]
        [SerializeField] float holdSeconds = 2.925f;
        [Tooltip("ジッポの音と火が移った音が鳴りきってから、座り始めの向きへ向き直すのにかける秒数")]
        [SerializeField] float aimSeconds = 2.5f;
        [Tooltip("向き直してから吸い始めるまでの一拍。秒")]
        [SerializeField] float aimSettleSeconds = 0.4f;
        [Tooltip("カードから明けるのにかける秒数。切り替えずに薄れさせて戻す")]
        [SerializeField] float liftSeconds = 1.4f;
        [Tooltip("明けきってから独白までにおくひと呼吸。秒")]
        [SerializeField] float settleSeconds = 1.1f;

        [SerializeField] SceneFlow flow;
        [SerializeField] HudView hud;
        [Tooltip("ジッポの音と息、そして煙。無くても場面は進む")]
        [SerializeField] Cigarette cigarette;
        [Tooltip("この id を調べたら煙草の演出を始める")]
        [SerializeField] string cigaretteId = RoomIds.Cigarette;
        [Tooltip("起き上がり終えてから言う独白。原稿の「冒頭」")]
        [SerializeField] string[] firstLines = new string[0];
        [Tooltip("煙草の箱から一本取る音（PackPull.wav）。煙草を調べた時に、原稿の 1 ページと一緒に口元の音源（voice）で鳴らす")]
        [SerializeField] AudioClip packPull;
        [Tooltip("二服目を吐いたところで出す、場所と時刻のカード。原稿の「暗転のカード」。空なら文字を出さずに黒くなるだけ")]
        [SerializeField, TextArea] string card = "";

        [Header("ジャケット")]
        [Tooltip("この id を調べたらジャケットを着る")]
        [SerializeField] string jacketId = RoomIds.Jacket;
        [Tooltip("体に付けたジャケット。着る音の終わりの少し前に着せる")]
        [SerializeField] Garment garment;
        [Tooltip("椅子の右の卓に置いたジャケット。着せたところで消す")]
        [FormerlySerializedAs("draped")]
        [SerializeField] GameObject folded;
        [Tooltip("着る音を鳴らす口元の音源（煙草の息と同じもの）")]
        [SerializeField] AudioSource voice;
        [Tooltip("着る音")]
        [SerializeField] AudioClip jacketOn;
        [Tooltip("音の終わりから、着せ替えるまでさかのぼる秒")]
        [SerializeField] float swapBeforeEnd = 0.6f;
        [Tooltip("着る音のあいだに正面へ向き直すのにかける秒数。卓を見たまま着せ替えを見せない")]
        [SerializeField] float jacketAimSeconds = 1.6f;
        [Tooltip("着た後の独白")]
        [FormerlySerializedAs("afterSmokeLines")]
        [SerializeField] string[] afterJacketLines = new string[0];

        bool opening;
        bool smoking;
        /// <summary>箱の音の長さ。Web では鳴らした直後に長さが 0 になるので、鳴らす前に読んで持っておく（SoundLoad.Seconds）</summary>
        float packPullSeconds;
        bool dressing;
        bool breathing;
        bool fading;
        /// <summary>座って始めたときの体の向き。煙草のあいだはここへ戻す</summary>
        float seatedYaw;

        /// <summary>何服吸うか</summary>
        public int Drags { get { return SmokeBeats.Drags; } }

        /// <summary>ジッポの音と火が移った音が鳴りきってから吸い始めるまでに挟む、向き直す間。秒</summary>
        public float Turn { get { return aimSeconds + aimSettleSeconds; } }

        /// <summary>火を点け始めて（1 ページを送ってから）吸い終わるまでの秒数</summary>
        public float SmokeSeconds { get { return SmokeBeats.Total(Drags, Turn); } }

        /// <summary>冒頭の黒と瞬きの間合い</summary>
        public WakeBlink Blink
        {
            get { return new WakeBlink(darkSeconds, peekSeconds, glimpse, glimpseSeconds, shutSeconds, pauseSeconds, openSeconds); }
        }

        /// <summary>冒頭の黒と瞬きの途中か。動作確認から読む</summary>
        public bool Opening { get { return opening; } }

        /// <summary>呼吸を鳴らしているか（薄れている途中も含む）。動作確認から読む</summary>
        public bool Breathing { get { return breathing; } }

        /// <summary>
        /// 明けを預かる。Start より前に立てておかないと、SceneFlow の Start が黒から明けを始めてしまう。
        /// 思い出した時は Restore が下ろす（Awake の後、Start の前に呼ばれる）
        /// </summary>
        void Awake()
        {
            if (flow != null && hud != null) flow.OpeningHeld = true;
            // Web: 箱の音は煙草を調べたその時に鳴らす。長さを読み込みの前に読んで持ち、展開を始めておく（SoundLoad）
            SoundLoad.Seconds(packPull, ref packPullSeconds);
            SoundLoad.Warm(packPull);
        }

        void OnEnable()
        {
            if (flow == null || hud == null)
            {
                Debug.LogError("RoomIntroDirector: flow か hud が未接続", this);
                enabled = false;
                return;
            }
            flow.Examined += OnExamined;
        }

        void OnDisable()
        {
            if (flow != null)
            {
                flow.Examined -= OnExamined;
                // 止められた場合は finally が走らないので、ここでも見回しと起き上がりを戻す
                if (flow.Player != null) flow.Player.CanLook = true;
                flow.OpeningHeld = false;
            }
            StopAllCoroutines();
            opening = false;
            smoking = false;
            dressing = false;
            StopBreath();
            if (hud == null) return;
            hud.SetCenter(null);
            hud.SetCurtain(false);
            if (cigarette != null) cigarette.Stop();
        }

        /// <summary>
        /// 冒頭は場面の始めに 1 度だけ。切って入れ直してもやり直さない。
        /// 思い出した時は黒と瞬きと最初の独白を出さず、ジャックがまだなら呼吸だけ鳴らす
        /// </summary>
        void Start()
        {
            if (!enabled) return;
            if (resumed)
            {
                if (!JackDone) Breathe();
                return;
            }
            if (flow.Player != null) seatedYaw = flow.Player.Yaw;
            Breathe();
            StartCoroutine(Open());
        }

        /// <summary>
        /// 黒のまま呼吸を聞かせ、瞬くように明ける。明けきったら起き上がりを始めさせ、起き上がり終えたら最初の独白。
        /// 秒は場面の秒なので、コンソールを開いている間は止まる（呼吸の音は止めない。部屋の空気の音と同じ）
        /// </summary>
        IEnumerator Open()
        {
            opening = true;
            var blink = Blink;
            try
            {
                for (var t = 0f; !blink.Done(t); t += Time.deltaTime)
                {
                    hud.SetFade(blink.Level(t));
                    // 暗いうちに調べられないよう、明けきるまで止めておく
                    flow.Freeze(FreezeMargin);
                    yield return null;
                }
                hud.SetFade(0f);
            }
            finally
            {
                opening = false;
                flow.OpeningHeld = false;
            }
            // 起き上がり終えてから独白。起き上がっている間も調べさせない
            while (flow.Waking)
            {
                flow.Freeze(FreezeMargin);
                yield return null;
            }
            if (flow.Completed) yield break;
            flow.Say(firstLines);
        }

        // ---- 呼吸 ------------------------------------------------------------

        /// <summary>ジャックを調べ済みか</summary>
        bool JackDone { get { return flow != null && flow.Progress != null && flow.Progress.Done.Contains(jackId); } }

        /// <summary>ジャックが手首から抜けたか。抜くしぐさが無ければ、調べたところで抜けたとみなす</summary>
        bool Unplugged { get { return jackPull != null ? jackPull.Unplugged || jackPull.Pulled : JackDone; } }

        /// <summary>呼吸の輪を鳴らし始める</summary>
        void Breathe()
        {
            if (breath == null || breath.clip == null) return;
            breath.loop = true;
            breath.volume = breathVolume;
            breath.Play();
            breathing = true;
            fading = false;
        }

        void StopBreath()
        {
            if (breath != null) breath.Stop();
            breathing = false;
            fading = false;
        }

        /// <summary>ジャックが抜けたら、呼吸を短く薄れさせて止める</summary>
        void Update()
        {
            if (!breathing || breath == null) return;
            if (!fading && Unplugged) fading = true;
            if (!fading) return;
            var step = breathVolume * Time.deltaTime / Mathf.Max(0.01f, breathFadeSeconds);
            breath.volume = Mathf.MoveTowards(breath.volume, 0f, step);
            if (breath.volume > 0f) return;
            StopBreath();
        }

        // ---- 記憶する・思い出す ------------------------------------------------

        bool resumed;

        public string MemoryKey { get { return "room.intro"; } }

        /// <summary>冒頭の明けの間と、吸っている間と、着ている間は残さない</summary>
        public bool Settled { get { return !opening && !smoking && !dressing; } }

        /// <summary>着たかは調べ済みの印（jacket）から、呼吸はジャックの印から決まるので、自分では残さない</summary>
        public string Capture() { return null; }

        /// <summary>
        /// 黒と瞬きと最初の独白を出さない（明けは SceneFlow がいつもどおり黒から明ける）。
        /// ジャケットを調べ済みなら、音も向き直しも無しに着た形へ置く
        /// </summary>
        public void Restore(string data)
        {
            resumed = true;
            if (flow == null) return;
            flow.OpeningHeld = false;
            // 座って始めた向きは、残した向きへ置き直す前に拾う（着る時に正面へ戻す先）
            if (flow.Player != null) seatedYaw = flow.Player.Yaw;
            if (flow.Progress != null && flow.Progress.Done.Contains(jacketId)) Dress();
        }

        void OnExamined(IInteractable item)
        {
            if (item.Id == cigaretteId && !smoking) StartCoroutine(Smoke());
            else if (item.Id == jacketId && !dressing) StartCoroutine(PutOn());
        }

        // ---- 煙草 ------------------------------------------------------------

        /// <summary>
        /// 煙草を取った後は吸い終わるまで自動で進む。停止は段ごとに掛け直し、
        /// 演出の流れと別の時計にしない。そうしないと停止が先に切れて、途中で調べられる
        /// </summary>
        IEnumerator Smoke()
        {
            var player = flow.Player;
            smoking = true;
            try
            {
                // 箱から一本取る音。SceneFlow が調べたその時に出す 1 ページ（原稿）と一緒に鳴らす
                var took = Time.time;
                var pulling = SoundLoad.Seconds(packPull, ref packPullSeconds);
                if (voice != null && packPull != null) voice.PlayOneShot(packPull);
                // SceneFlow が調べた時に目を煙草へ向け始めている。向け終えるまで待ち、その向きのまま火を点ける。
                // 見回しはここで預からない（預かると向ける動きがそこで止まる）。向けている間は SceneFlow が封じている。
                // 調べたその時に掛けた止まりの間は、SceneFlow が見回しを封じ続ける（調べている間に数える）
                flow.Freeze(PlayerController.FaceSeconds + FreezeMargin);
                var faced = Time.time + PlayerController.FaceSeconds + 0.5f;
                while (player != null && player.Facing && Time.time < faced)
                {
                    flow.Freeze(FreezeMargin);
                    yield return null;
                }
                // 吸い終わるまで見回しも受け付けない
                if (player != null) player.CanLook = false;
                // 1 ページを読み終えて送り、箱の音も鳴り終わってから火を点ける。
                // 読んでいる間は止めない（止めると送れない）。送った後、箱の音の残りを待つ間だけ止める
                while (flow.Talking || Time.time < took + pulling)
                {
                    if (!flow.Talking) flow.Freeze(FreezeMargin);
                    yield return null;
                }
                if (flow.Completed) yield break;
                if (cigarette != null) cigarette.Light(Drags, Turn);
                var started = Time.time;
                // ジッポの音と火が移った音が鳴りきってから、座り始めの向きへ戻す
                var turnAt = started + SmokeBeats.TurnAt;
                flow.Freeze(SmokeBeats.TurnAt + aimSeconds + FreezeMargin);
                while (Time.time < turnAt) yield return null;
                yield return AimForward(player, aimSeconds);
                // 二服目を吐いたところで一度だけ黒く覆い、場所と時刻のカードを出す。一服目は暗くしない
                var at = started + SmokeBeats.CardAt(Drags - 1, Drags, Turn);
                // 明ける間も止めておく。暗いうちに調べられないように
                flow.Freeze(at - Time.time + holdSeconds + liftSeconds + FreezeMargin);
                while (Time.time < at) yield return null;
                if (flow.Completed) yield break;
                yield return Show(card);
                // 吐き終わってから、少し置いて次へ。
                // 明けに時刻表より手間取ったときは、明け終わりから数え直す
                var until = SmokeBeats.Settle(Time.time, started + SmokeSeconds, settleSeconds);
                flow.Freeze(until - Time.time + FreezeMargin);
                while (Time.time < until) yield return null;
            }
            finally
            {
                smoking = false;
                if (player != null) player.CanLook = true;
            }
            // 吸い終わりには何も言わない。独白はジャケットを着た後
        }

        /// <summary>
        /// 座ったまま、右の卓のジャケットを着る。着る音を鳴らし、正面へ向き直してから、
        /// 音の終わりの少し前に体へ着せて卓のジャケットを消す。音が鳴り終わってから独白
        /// </summary>
        IEnumerator PutOn()
        {
            var player = flow.Player;
            dressing = true;
            // Web では、鳴らす時に読み込み始めると鳴り出しが展開の後へ延び、着る音と体へ着せる時がずれる。
            // 展開を待ってから鳴らす。待つ間も止めておく。エディタとスタンドアロンでは待たない（SoundLoad）
            if (!SoundLoad.Ready(jacketOn)) yield return SoundLoad.Wait(jacketOn, () => flow.Freeze(FreezeMargin));
            var beats = new JacketBeats(jacketOn != null ? jacketOn.length : 0f, swapBeforeEnd);
            // 鳴り終わるまで止めておく。途中で他を調べさせない
            flow.Freeze(beats.Sound + FreezeMargin);
            if (player != null) player.CanLook = false;
            try
            {
                if (voice != null && jacketOn != null) voice.PlayOneShot(jacketOn);
                var started = Time.time;
                yield return AimForward(player, jacketAimSeconds);
                while (Time.time - started < beats.SwapAt) yield return null;
                Dress();
                while (Time.time - started < beats.Sound) yield return null;
            }
            finally
            {
                dressing = false;
                if (player != null) player.CanLook = true;
            }
            if (flow.Completed) yield break;
            flow.Say(afterJacketLines);
        }

        /// <summary>体へ着せ、卓のジャケットを消す</summary>
        void Dress()
        {
            if (garment != null) garment.Worn = true;
            if (folded != null) folded.SetActive(false);
        }

        /// <summary>座って始めたときの向きへ、滑らかに戻す。切り替えではなく回して戻すので繋ぎ目が出ない</summary>
        IEnumerator AimForward(PlayerController player, float seconds)
        {
            if (player == null || seconds <= 0f) yield break;
            var fromYaw = player.Yaw;
            var fromPitch = player.Pitch;
            for (var t = 0f; t < seconds; t += Time.deltaTime)
            {
                var k = Mathf.SmoothStep(0f, 1f, t / seconds);
                player.Yaw = Mathf.LerpAngle(fromYaw, seatedYaw, k);
                player.Pitch = Mathf.Lerp(fromPitch, 0f, k);
                yield return null;
            }
            player.Yaw = seatedYaw;
            player.Pitch = 0f;
        }

        /// <summary>画面を黒く覆ってカードを出し、読む時間を置いてから、薄れさせて明ける</summary>
        IEnumerator Show(string text)
        {
            hud.SetCurtain(true);
            if (!string.IsNullOrEmpty(text)) hud.SetCenter(text);
            yield return new WaitForSeconds(holdSeconds);
            hud.SetCenter(null);
            yield return hud.CurtainTo(0f, liftSeconds);
            hud.SetCurtain(false);
        }
    }
}
