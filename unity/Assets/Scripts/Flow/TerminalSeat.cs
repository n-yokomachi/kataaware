using UnityEngine;

namespace HalfAware
{
    /// <summary>
    /// 場面 1 の端末を調べたら、その時（<see cref="SceneFlow.Examining"/>）に、椅子に座った目の高さの正面（モニターの方）へ
    /// 視点を移し始め、体も椅子に座らせる。座ったまま、独白とモニターの映り込み（<see cref="TerminalReflection"/>）、
    /// 「スリープを解除する」の二択と、「はい」の後の解除の文までを読ませ、どれも終わって映り込みが消えてから、
    /// 体を立たせて元の所と向きへ戻す。「いいえ」なら二択を閉じたところで戻る（流れは <see cref="SeatVisit"/>）。
    ///
    /// 移す・読む・戻す間は、見回しと歩きを止める。移す・戻す間は調べる操作と字幕送りも止める
    /// （着く前に 2 ページ目へ送られて、映り込みが動いている途中で出ないように）。二択を出している間は止めない。
    /// 立って調べたときだけ移す。座ったまま（場面の頭）調べたときは何もしない。
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
        [Tooltip("座った形。移す間だけ掛ける")]
        [SerializeField] SeatedPose pose;
        [Tooltip("モニターの映り込み。消えきってから戻す")]
        [SerializeField] TerminalReflection reflection;
        [Tooltip("端末の調べる対象の id")]
        [SerializeField] string terminalId = "terminal";
        [Tooltip("机のモニター 5 枚の画面。スリープを解除した（「はい」）ところで起動する")]
        [SerializeField] TerminalScreen screen;

        [Header("座った正面")]
        [Tooltip("腰を下ろす場所。Player の足元の位置")]
        [SerializeField] Vector3 seatSpot;
        [Tooltip("座ったときの体の向き。度")]
        [SerializeField] float seatYaw;
        [Tooltip("座ったときの見下ろし。度（負で見上げる）")]
        [SerializeField] float seatPitch;
        [Tooltip("座ったときの目線の高さ")]
        [SerializeField] float seatEyeHeight = 1.1f;

        [Header("椅子")]
        [Tooltip("椅子。座る間は机へ寄せ、戻るときに元へ下げる")]
        [SerializeField] Transform chair;
        [Tooltip("座っているときの椅子の位置")]
        [SerializeField] Vector3 chairSeated;
        [Tooltip("歩き回る間だけ点ける椅子のコライダー。座る間は切る")]
        [SerializeField] GameObject chairBlocker;

        [Header("秒数（仮置き）")]
        [Tooltip("座った正面へ移すのにかける秒数")]
        [SerializeField] float goSeconds = 1.8f;
        [Tooltip("元の所へ戻すのにかける秒数")]
        [SerializeField] float backSeconds = 1.6f;

        SeatVisit visit;
        /// <summary>「はい」を選んで、映り込みが消えきるのを待っている</summary>
        bool waking;
        Vector3 fromSpot;
        float fromYaw;
        float fromPitch;
        float fromEye;
        Vector3 fromChair;
        bool blockerWasOn;

        /// <summary>移す・読む・戻すの流れ（動作確認から読む）</summary>
        public SeatVisit Visit => visit;

        void Awake()
        {
            visit = new SeatVisit(goSeconds, backSeconds);
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
            // 立って歩ける時だけ。座ったまま調べたときは、もう正面にいる
            if (player == null || !player.CanMove) return;
            fromSpot = player.transform.position;
            fromYaw = player.Yaw;
            fromPitch = player.Pitch;
            fromEye = player.EyeHeight;
            if (chair != null) fromChair = chair.position;
            blockerWasOn = chairBlocker != null && chairBlocker.activeSelf;
            if (chairBlocker != null) chairBlocker.SetActive(false);
            if (pose != null)
            {
                pose.Seated = true;
                // 端末の前では両手を腿に置いて肩を落とした形（二つ目の形）
                pose.UseAlternate = true;
            }
            visit.Start();
            Place();
        }

        void Update()
        {
            Wake();
            if (visit == null || !visit.Busy || flow == null || flow.Player == null) return;
            visit.Tick(Time.deltaTime, flow.Talking, flow.Choosing, reflection != null && reflection.Level > 0f);
            if (visit.Frozen(flow.Talking, flow.Choosing)) flow.Freeze(ConnectDirector.FreezeMargin);
            Place();
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

        /// <summary>寄った度合いのとおりに、目と体と椅子を置く</summary>
        void Place()
        {
            var player = flow.Player;
            var k = visit.Seat;
            player.CanMove = false;
            player.CanLook = false;
            player.transform.position = Vector3.Lerp(fromSpot, seatSpot, k);
            player.EyeHeight = Mathf.Lerp(fromEye, seatEyeHeight, k);
            player.Yaw = Mathf.LerpAngle(fromYaw, seatYaw, k);
            player.Pitch = Mathf.Lerp(fromPitch, seatPitch, k);
            if (chair != null) chair.position = Vector3.Lerp(fromChair, chairSeated, k);
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

        /// <summary>座った正面へ移す・読む・戻すの途中と、起動を待っている間は残さない</summary>
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

        /// <summary>戻りきったら、立った形に戻して歩きと見回しを返す</summary>
        void Finish()
        {
            var player = flow.Player;
            player.transform.position = fromSpot;
            player.EyeHeight = fromEye;
            player.Yaw = fromYaw;
            player.Pitch = fromPitch;
            if (chair != null) chair.position = fromChair;
            if (pose != null)
            {
                pose.Seated = false;
                pose.UseAlternate = false;
            }
            if (chairBlocker != null) chairBlocker.SetActive(blockerWasOn);
            player.CanMove = true;
            player.CanLook = true;
        }
    }
}
