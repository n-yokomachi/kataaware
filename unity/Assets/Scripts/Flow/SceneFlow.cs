using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace HalfAware
{
    /// <summary>
    /// 歩いて調べる場面 1 つ分の進行。毎フレーム、対象の選択 → 印 → 調べる → 字幕 → 完了の判定の順に進める。
    /// 字幕の表示中は E とクリックを字幕の送りにだけ使い、調べる操作は受け付けない。
    /// 止まっている間は調べる操作と進行が止まる。見回しと移動は止めない（場面 1 の停止はすべて座っている間に起きる）。
    /// 必須の対象をすべて調べ、字幕も出ておらず、止まってもいなければ暗転して「続く」を出す
    /// </summary>
    public sealed class SceneFlow : MonoBehaviour
    {
        public const string ToBeContinued = "（仮）続く";
        /// <summary>暗転にかける秒数</summary>
        public const float FadeSeconds = 1.5f;
        /// <summary>座位から立位へ目線を上げる秒数</summary>
        public const float StandSeconds = 0.6f;

        [SerializeField] PlayerController player;
        [SerializeField] HudView hud;
        [Tooltip("眩暈の強さの容れ物。無ければ眩暈を使わない")]
        [SerializeField] DazeVolume daze;
        [Tooltip("ラジアン。視線からこの角度以内の対象だけ選ぶ")]
        [SerializeField] float maxAngle = InteractionPicker.MaxAngle;

        [Header("座って始める")]
        [Tooltip("座っているときの目線の高さ")]
        [SerializeField] float seatEyeHeight = 1.1f;
        [Tooltip("この id を調べると立ち上がって移動できるようになる。空なら最初から立っている")]
        [SerializeField] string standAfter = "";
        [Tooltip("立ち上がって足を下ろす場所。椅子の中に立ち尽くさないよう、ここへ滑らせる")]
        [SerializeField] Transform standSpot;
        [Tooltip("立ってから効かせる当たり。座っている間は椅子に当たらないよう切っておく")]
        [SerializeField] GameObject chairBlocker;

        [Header("目覚めの起き上がり")]
        [Tooltip("座位の目線からどれだけ下から始めるか。メートル")]
        [SerializeField] float wakeDrop = WakeUp.DefaultDrop;
        [Tooltip("始まりの伏し目の角度。度")]
        [SerializeField] float wakeStartPitch = WakeUp.DefaultStartPitch;
        [Tooltip("起き上がりにかける秒数。0 なら起き上がりを入れない")]
        [SerializeField] float wakeSeconds = WakeUp.DefaultSeconds;
        [Tooltip("自分の体。座位と立位の姿勢を切り替える")]
        [SerializeField] GameObject body;

        [Header("入ったときの眩暈")]
        [SerializeField] float dazeBlur = 1f;
        [SerializeField] float dazeWobble = 1f;
        [Tooltip("秒。解放してからこの時間で 0 になる")]
        [SerializeField] float dazeSeconds = 5f;
        [Tooltip("この id を調べるまで眩暈を保つ。空なら入った時点から消え始める")]
        [SerializeField] string dazeUntil = "";

        readonly SubtitleQueue subtitles = new SubtitleQueue();
        readonly MessageLog log = new MessageLog();
        List<IInteractable> items;
        SceneProgress progress;
        StandUp standUp;
        WakeUp wakeUp;
        SeatedPose seatedPose;
        bool pendingInteract;
        float frozenUntil;
        bool dazeReleased;
        bool logOpen;
        Vector3 seatedSpot;

        /// <summary>対象を調べて済んだ直後。前提が未達で文だけ出たときは呼ばない</summary>
        public event Action<IInteractable> Examined;

        public SceneProgress Progress => progress;

        /// <summary>これまでに出した文の控え。Tab で開く</summary>
        public MessageLog Log => log;

        /// <summary>控えを開いている間。調べる操作は受け付けない</summary>
        public bool LogOpen => logOpen;

        /// <summary>場面固有の演出が、向きを変えたり見回しを止めたりするのに使う</summary>
        public PlayerController Player => player;
        public bool Completed { get; private set; }

        /// <summary>調べる操作と進行が止まっているか。見回しは止めない</summary>
        public bool Frozen => Time.time < frozenUntil;

        /// <summary>字幕を出している最中か</summary>
        public bool Talking => subtitles.IsTalking;

        void Awake()
        {
            if (player == null || hud == null)
            {
                Debug.LogError("SceneFlow: player か hud が未接続", this);
                enabled = false;
                return;
            }
            // 切ってある対象も拾う。前腕のジャックは伏せた状態で始まるので、
            // ここで漏らすと必須の数え上げが狂って場面が早く終わってしまう
            items = new List<IInteractable>(FindObjectsByType<Interactable>(FindObjectsInactive.Include, FindObjectsSortMode.InstanceID));
            if (items.Count == 0) Debug.LogWarning("SceneFlow: 調べる対象が 1 つも見つからない", this);
            progress = new SceneProgress(items);
            seatedSpot = player.transform.position;
            if (chairBlocker != null) chairBlocker.SetActive(false);
            if (standAfter.Length > 0)
            {
                standUp = new StandUp(seatEyeHeight, PlayerController.StandingEyeHeight, StandSeconds);
                player.CanMove = false;
                player.EyeHeight = seatEyeHeight;
                // 座っている間は体を据えて首だけ振る
                player.HeadYawLimit = HeadTurn.DefaultLimit;
                if (body != null)
                {
                    seatedPose = body.GetComponent<SeatedPose>();
                    if (seatedPose != null) seatedPose.Seated = true;
                }
                wakeUp = new WakeUp(seatEyeHeight, wakeDrop, wakeStartPitch, wakeSeconds);
                if (!wakeUp.Done)
                {
                    player.EyeHeight = wakeUp.EyeHeight;
                    player.Pitch = wakeUp.Pitch;
                    player.CanLook = false;
                }
            }
            if (daze != null && dazeUntil.Length > 0) daze.Hold(dazeBlur, dazeWobble);
        }

        /// <summary>次のフレームで調べる操作を 1 回起こす。E キーの代わりに、再生中の動作確認から SendMessage で呼ぶ</summary>
        public void PressInteract() => pendingInteract = true;

        /// <summary>字幕を積む。場面固有の演出から呼ぶ。控えにも残す</summary>
        public void Say(IReadOnlyList<string> lines)
        {
            subtitles.Enqueue(lines);
            log.AddRange(lines);
        }

        /// <summary>seconds 秒のあいだ、調べる操作と進行を止める。すでに止まっているときは長い方を採る</summary>
        public void Freeze(float seconds) => frozenUntil = Mathf.Max(frozenUntil, Time.time + seconds);

        void Update()
        {
            if (Completed) return;
            if (player.LogPressed) logOpen = !logOpen;
            hud.SetLog(logOpen ? log.Compose(HudView.LogLines) : null);
            var frozen = Frozen || logOpen;
            var interact = (player.InteractPressed || pendingInteract) && !frozen;
            // 止まっている間に届いた PressInteract は捨てずに持ち越す。実キー入力はその場限りなので落ちる
            if (!frozen) pendingInteract = false;
            if (subtitles.IsTalking && interact)
            {
                subtitles.Advance();
                interact = false;
            }
            IInteractable selected = null;
            if (!subtitles.IsTalking && !frozen)
            {
                var eye = player.Eye;
                selected = InteractionPicker.Select(eye.position, eye.forward, items, progress.Done, maxAngle);
            }
            hud.SetPrompt(selected != null ? "E  " + selected.Label : null);
            if (selected != null && interact)
            {
                var said = progress.Examine(selected);
                subtitles.Enqueue(said);
                log.AddRange(said);
                if (progress.Done.Contains(selected.Id) && Examined != null) Examined(selected);
            }
            // 調べた先の演出が Freeze を呼ぶので、止まっているかは調べた後に見直す
            var frozenNow = Frozen;
            ReleaseDaze();
            Wake();
            // 独白を読み終えてから腰を上げる。喋りながら立ち上がらせない
            Stand(frozenNow || subtitles.IsTalking);
            // 控えを開いている間は字幕を伏せる。控えの上に重なって読みにくい
            hud.SetSubtitle(logOpen ? null : subtitles.Current);
            if (progress.IsComplete && !subtitles.IsTalking && !frozenNow) StartCoroutine(Complete());
        }

        /// <summary>dazeUntil の対象を調べたら、保っていた眩暈を消し始める</summary>
        void ReleaseDaze()
        {
            if (daze == null || dazeReleased) return;
            if (dazeUntil.Length > 0 && !progress.Done.Contains(dazeUntil)) return;
            dazeReleased = true;
            daze.Decay(dazeBlur, dazeWobble, dazeSeconds);
        }

        /// <summary>始まりの起き上がり。終わるまで見回しを預かる</summary>
        void Wake()
        {
            if (wakeUp == null || wakeUp.Done) return;
            wakeUp.Tick(Time.deltaTime);
            player.EyeHeight = wakeUp.EyeHeight;
            player.Pitch = wakeUp.Pitch;
            if (!wakeUp.Done) return;
            player.CanLook = true;
        }

        /// <summary>standAfter の対象を調べたら、止まってもおらず字幕も出ていない間に目線を上げて移動を許す</summary>
        void Stand(bool frozen)
        {
            if (standUp == null || standUp.Standing) return;
            if (wakeUp != null && !wakeUp.Done) return;   // 起き上がりが先
            standUp.Tick(Time.deltaTime, progress.Done.Contains(standAfter), frozen);
            player.EyeHeight = standUp.EyeHeight;
            player.CanMove = standUp.Standing;
            StepOffTheChair();
            if (!standUp.Standing) return;
            // 立ち上がりきってから、立位の姿勢に戻して首の制限を解く
            if (seatedPose != null) seatedPose.Seated = false;
            if (player.HeadYawLimit > 0f) player.ReleaseHead();
            // 椅子から離れきってから当たりを入れる。座ったまま入れると押し出される
            if (chairBlocker != null) chairBlocker.SetActive(true);
        }

        /// <summary>
        /// 立ち上がる間に、腰を下ろしていた場所から足を下ろす場所へ滑らせる。
        /// 椅子の中に立ち尽くすと、下を向いたとき体が椅子を突き抜けて見える
        /// </summary>
        void StepOffTheChair()
        {
            if (standSpot == null || standUp == null) return;
            var k = StandUp.Progress(standUp.EyeHeight, seatEyeHeight, PlayerController.StandingEyeHeight);
            var at = Vector3.Lerp(seatedSpot, standSpot.position, k);
            player.transform.position = new Vector3(at.x, player.transform.position.y, at.z);
        }

        IEnumerator Complete()
        {
            Completed = true;
            player.CanMove = false;
            hud.SetPrompt(null);
            hud.SetSubtitle(null);
            yield return hud.FadeTo(1f, FadeSeconds);
            hud.SetCenter(ToBeContinued);
        }
    }
}
