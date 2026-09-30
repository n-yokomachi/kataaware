using UnityEngine;

namespace HalfAware
{
    /// <summary>
    /// 場面 1 の端末（モニター）を、煙草を吸い終えた後に座ったまま調べる。調べたその時（<see cref="SceneFlow.Examining"/>）に、
    /// 座った正面（モニターの方、seatYaw・seatPitch）へ視線を向き直し始め、独白とモニターの映り込み（<see cref="TerminalReflection"/>）、
    /// 「スリープを解除する」の二択と、「はい」の後の解除の文までを読ませる。どれも終わって映り込みが消えたら、見回しを返す。
    /// 座ったままなので、立たず、立ち上がる音も鳴らさない。「いいえ」なら二択を閉じたところで見回しを返す（流れは <see cref="SeatVisit"/>。戻す段は 0 秒）。
    ///
    /// 2026-09-29 まではジャケットを着て立った後に調べ、立った所から椅子へ座らせて、読み終えたら立たせて戻していた。
    /// モニターをジャケットの前に移し（<see cref="RoomIds.After"/>。オーナー「モニター調べるためにもう一度座り直すっていうのをなくす」）、
    /// 立った所から座らせる動きは無くした。座った正面の値（seatSpot・seatEyeHeight・chairSeated）は確かめの撮影が使う。
    ///
    /// 向き直す・読む間は、見回しと歩きを止める。向き直す間は調べる操作と字幕送りも止める
    /// （向き直す前に 2 ページ目へ送られて、映り込みが動いている途中で出ないように）。二択を出している間は止めない。
    /// 座った形は変えない（右手は煙草を持って肘掛けに置いたまま。前の、立った所から座らせた時の、両手を腿に置いた二つ目の形は使わない）。
    ///
    /// **「はい」でスリープを解除したら、机のモニター 5 枚を起動する**（<see cref="TerminalScreen"/>、オーナー、2026-09-28
    /// 「モニターのスリープを解除したときに、別の場面の時と同じようにコンソール的なものを表示して」）。場面 3 でジャックを繋いだ時と同じ、
    /// 二度瞬いてから明るくなって、窓ごとに文字が流れる形。映り込みが消えきってから点ける。点いた後は場面 1 の終わりまで点いたまま。
    /// 思い出した時（<see cref="ISceneMemory"/>）は、解除した後なら瞬かずに点いた形で始める
    /// </summary>
    [DefaultExecutionOrder(-5)]
    public sealed class TerminalSeat : MonoBehaviour, ISceneMemory
    {
        [SerializeField] SceneFlow flow;
        [Tooltip("座った形（確かめの撮影が使う。遊ぶ間は形を変えない）")]
        [SerializeField] SeatedPose pose;
        [Tooltip("モニターの映り込み。消えきってから戻す")]
        [SerializeField] TerminalReflection reflection;
        [Tooltip("端末の調べる対象の id")]
        [SerializeField] string terminalId = "terminal";
        [Tooltip("机のモニター 5 枚の画面。スリープを解除した（「はい」）ところで起動する")]
        [SerializeField] TerminalScreen screen;

        [Header("座った正面")]
        [Tooltip("腰を下ろす場所。Player の足元の位置。座って始める所と同じ（確かめの撮影が使う）")]
        [SerializeField] Vector3 seatSpot;
        [Tooltip("座ったときの体の向き。度")]
        [SerializeField] float seatYaw;
        [Tooltip("座ったときの見下ろし。度（負で見上げる）")]
        [SerializeField] float seatPitch;
        [Tooltip("座ったときの目線の高さ（確かめの撮影が使う）")]
        [SerializeField] float seatEyeHeight = 1.1f;

        [Header("椅子（確かめの撮影が使う。遊ぶ間は動かさない）")]
        [Tooltip("椅子")]
        [SerializeField] Transform chair;
        [Tooltip("座っているときの椅子の位置")]
        [SerializeField] Vector3 chairSeated;
        [Tooltip("歩き回る間だけ点ける椅子のコライダー")]
        [SerializeField] GameObject chairBlocker;

        [Header("秒数（仮置き）")]
        [Tooltip("座った正面へ向き直すのにかける秒数")]
        [SerializeField] float goSeconds = 1.8f;

        SeatVisit visit;
        /// <summary>「はい」を選んで、映り込みが消えきるのを待っている</summary>
        bool waking;
        float fromYaw;
        float fromPitch;

        /// <summary>向き直す・読むの流れ（動作確認から読む）</summary>
        public SeatVisit Visit => visit;

        void Awake()
        {
            // 座ったままなので戻す段は無い（0 秒）。読み終えたらその向きのまま見回しを返す
            visit = new SeatVisit(goSeconds, 0f);
        }

        void OnEnable()
        {
            if (flow == null) return;
            flow.Examining += OnExamining;
            flow.Examined += OnExamined;
        }

        void OnDisable()
        {
            if (flow == null) return;
            flow.Examining -= OnExamining;
            flow.Examined -= OnExamined;
        }

        /// <summary>端末は二択を持つので、済んだ（Examined）のは「はい」を選んだ時</summary>
        void OnExamined(IInteractable item)
        {
            if (item == null || item.Id != terminalId || screen == null) return;
            waking = true;
        }

        void OnExamining(IInteractable item)
        {
            if (item == null || item.Id != terminalId || visit == null || visit.Busy) return;
            var player = flow.Player;
            // 座ったまま調べる（前提は煙草で、立てるのはジャケットの後）。立って歩ける時は何もしない
            if (player == null || player.CanMove) return;
            fromYaw = player.Yaw;
            fromPitch = player.Pitch;
            visit.Start();
            Place();
        }

        void Update()
        {
            Wake();
            if (visit == null || !visit.Busy || flow == null || flow.Player == null) return;
            visit.Tick(Time.deltaTime, flow.Talking, flow.Choosing, reflection != null && reflection.Level > 0f);
            if (visit.Frozen(flow.Talking, flow.Choosing)) flow.Freeze(ConnectDirector.FreezeMargin);
            // 戻す段は置き直さない（その向きのまま）
            if (visit.Now == SeatVisit.Phase.Going || visit.Now == SeatVisit.Phase.Reading) Place();
            if (!visit.Busy) Finish();
        }

        /// <summary>
        /// 座っている間は歩かせない。SceneFlow は二択を閉じるときに player.CanMove を立てるので、
        /// 「はい」「いいえ」のどちらでも座ったまま歩き出せてしまう。伏せ直す。
        /// PlayerController は実行順が前なので、次のフレームに間に合う LateUpdate で書く
        /// </summary>
        void LateUpdate()
        {
            if (visit == null || !visit.Busy || flow == null || flow.Player == null) return;
            flow.Player.CanMove = false;
        }

        /// <summary>向き直した度合いのとおりに、視線を置く。座ったままなので、体と目の高さと椅子は動かさない</summary>
        void Place()
        {
            var player = flow.Player;
            var k = visit.Seat;
            player.CanMove = false;
            player.CanLook = false;
            player.Yaw = Mathf.LerpAngle(fromYaw, seatYaw, k);
            player.Pitch = Mathf.Lerp(fromPitch, seatPitch, k);
        }

        /// <summary>
        /// 映り込みが消えきってから画面を起動し、文字を流し始める。映り込みは二択を出したところで消え始めるので、
        /// ふつうは「はい」を選んだ時には消えきっている。すぐに選んだ時だけ、消えきるのを待つ（顔と文字を重ねない）
        /// </summary>
        void Wake()
        {
            if (!waking) return;
            if (reflection != null && reflection.Level > 0f) return;
            waking = false;
            screen.Boot();
            screen.Scroll(true);
        }

        // ---- 記憶する・思い出す ------------------------------------------------

        public string MemoryKey { get { return "room.terminal"; } }

        /// <summary>向き直す・読むの途中と、起動を待っている間は残さない</summary>
        public bool Settled { get { return (visit == null || !visit.Busy) && !waking; } }

        /// <summary>画面が点いたかは調べ済みの印（terminal）から決まるので、自分では残さない</summary>
        public string Capture() { return null; }

        /// <summary>スリープを解除した後なら、瞬かずに点いた形にして文字を流しておく</summary>
        public void Restore(string data)
        {
            waking = false;
            if (flow == null || flow.Progress == null || screen == null) return;
            if (!flow.Progress.Done.Contains(terminalId)) return;
            screen.LightNow();
            screen.Scroll(true);
        }

        /// <summary>読み終えたら、見回しを返す。座ったままなので歩きは返さない（立つのはジャケットの後。SceneFlow）</summary>
        void Finish()
        {
            flow.Player.CanLook = true;
        }
    }
}
