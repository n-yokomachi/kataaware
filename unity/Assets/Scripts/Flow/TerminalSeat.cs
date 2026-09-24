using UnityEngine;

namespace HalfAware
{
    /// <summary>
    /// 場面 1 の端末を調べたら、椅子に座った目の高さの正面（モニターの方）へ視点を移し、体も椅子に座らせて、
    /// モニターの映り込み（<see cref="TerminalReflection"/>）を見せる。独白を読み終え、映り込みが消えてから、
    /// 体を立たせて元の所と向きへ戻す（流れは <see cref="SeatVisit"/>）。
    ///
    /// 移す・読む・戻す間は、見回しと歩きを止める。移す・戻す間は調べる操作と字幕送りも止める
    /// （着く前に 3 行目へ送られて、映り込みが動いている途中で出ないように）。
    /// 立って調べたときだけ移す。座ったまま（場面の頭）調べたときは何もしない
    /// </summary>
    [DefaultExecutionOrder(-5)]
    public sealed class TerminalSeat : MonoBehaviour
    {
        [SerializeField] SceneFlow flow;
        [Tooltip("座った形。移す間だけ掛ける")]
        [SerializeField] SeatedPose pose;
        [Tooltip("モニターの映り込み。消えきってから戻す")]
        [SerializeField] TerminalReflection reflection;
        [Tooltip("端末の調べる対象の id")]
        [SerializeField] string terminalId = "terminal";

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
            if (flow != null) flow.Examined += OnExamined;
        }

        void OnDisable()
        {
            if (flow != null) flow.Examined -= OnExamined;
        }

        void OnExamined(IInteractable item)
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
            if (visit == null || !visit.Busy || flow == null || flow.Player == null) return;
            visit.Tick(Time.deltaTime, flow.Talking, reflection != null && reflection.Level > 0f);
            if (visit.Frozen(flow.Talking)) flow.Freeze(ConnectDirector.FreezeMargin);
            Place();
            if (!visit.Busy) Finish();
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
