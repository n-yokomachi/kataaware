using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

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
        /// <summary>場面の頭で黒から明ける秒数</summary>
        public const float FadeInSeconds = 1.2f;
        /// <summary>座位から立位へ目線を上げる秒数</summary>
        public const float StandSeconds = 0.6f;

        [SerializeField] PlayerController player;
        [SerializeField] HudView hud;
        [Tooltip("眩暈の強さの容れ物。無ければ眩暈を使わない")]
        [SerializeField] DazeVolume daze;
        [Tooltip("ラジアン。視線からこの角度以内の対象だけ選ぶ")]
        [SerializeField] float maxAngle = InteractionPicker.MaxAngle;
        [Tooltip("必須をすべて終えたら読むシーンの名前。空なら「続く」で止まる")]
        [SerializeField] string nextScene = "";

        [Header("場面の頭と終わり")]
        [Tooltip("黒いうちに出す見出し。空なら出さず、そのまま明ける")]
        [SerializeField, TextArea] string openingCard = "";
        [Tooltip("見出しを出しておく秒数")]
        [SerializeField] float cardSeconds = 2.4f;
        [Tooltip("見出しから明けるまでの秒数")]
        [SerializeField] float cardLiftSeconds = 1.8f;
        [Tooltip("出て行くときに 1 度鳴らす音。扉の開け閉めなど")]
        [SerializeField] AudioSource exitSound;
        [Tooltip("その音を鳴らしてから暗転するまでの秒数。扉が閉まる瞬間に合わせる")]
        [SerializeField] float exitSoundSeconds = 1.45f;
        [Tooltip("次の場面へ渡す前に暗転するか。見出しを挟んで切り替えるときに使う")]
        [SerializeField] bool cutToBlack;
        [Tooltip("暗転してから次の場面を読むまでの秒数。音の余韻はここで鳴り切る")]
        [SerializeField] float blackHoldSeconds = 0.55f;

        [Header("座って始める")]
        [Tooltip("座っているときの目線の高さ")]
        [SerializeField] float seatEyeHeight = 1.1f;
        [Tooltip("この id を調べると立ち上がって移動できるようになる。空なら最初から立っている")]
        [SerializeField] string standAfter = "";
        [Tooltip("立ち上がって足を下ろす場所。椅子の中に立ち尽くさないよう、ここへ滑らせる")]
        [SerializeField] Transform standSpot;
        [Tooltip("立ってから効かせる当たり。座っている間は椅子に当たらないよう切っておく")]
        [SerializeField] GameObject chairBlocker;
        [Tooltip("立つときに後ろへ押す椅子。椅子と机のあいだに立つ幅を空ける")]
        [SerializeField] Transform chair;
        [Tooltip("椅子を押し下げる距離。椅子の後ろ向きに。メートル")]
        [SerializeField] float chairPushBack = 0.24f;

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
        List<IInteractable> items;
        SceneProgress progress;
        StandUp standUp;
        WakeUp wakeUp;
        SeatedPose seatedPose;
        bool pendingInteract;
        float frozenUntil;
        bool dazeReleased;
        Vector3 seatedSpot;
        Vector3 chairSpot;
        Choice choice;
        IInteractable asking;
        int lastStep;

        /// <summary>
        /// 対象を調べたその時。前提が揃っていて、その対象の文を出し始めたときに呼ぶ。
        /// 二択を持つ対象でも、文を読む前（二択に答える前）に来る。前提が未達で文だけ出たときは呼ばない
        /// </summary>
        public event Action<IInteractable> Examining;

        /// <summary>対象を調べて済んだ直後。前提が未達で文だけ出たときは呼ばない</summary>
        public event Action<IInteractable> Examined;

        public SceneProgress Progress => progress;

        /// <summary>場面固有の演出が、向きを変えたり見回しを止めたりするのに使う</summary>
        public PlayerController Player => player;
        public bool Completed { get; private set; }

        /// <summary>調べる操作と進行が止まっているか。見回しは止めない</summary>
        public bool Frozen => Time.time < frozenUntil;

        /// <summary>字幕を出している最中か</summary>
        public bool Talking => subtitles.IsTalking;

        /// <summary>二択を出している最中か</summary>
        public bool Choosing => choice != null;

        /// <summary>
        /// いま出している行。出していなければ null。
        /// 台詞の途中でしぐさを入れたい演出が、どこまで進んだかを見るのに使う
        /// </summary>
        public string CurrentLine => subtitles.Current;

        /// <summary>
        /// 必須を済ませたあとも続きの演出がある場面で、そのあいだ場面を閉じるのを止める。
        /// Freeze と違って調べる操作は止めないので、字幕は送れる。
        /// 必須がひとつしかなく、それを済ませてから長い芝居が続く場面（路地裏の売り買い）で使う
        /// </summary>
        public bool Held { get; set; }

        void Awake()
        {
            if (player == null || hud == null)
            {
                Debug.LogError("SceneFlow: player か hud が未接続", this);
                enabled = false;
                return;
            }
            // 切ってある対象も拾う。場面 3 のジャックやモニターのように伏せた状態で始まる対象があるので、
            // ここで漏らすと必須の数え上げが狂って場面が早く終わってしまう
            items = new List<IInteractable>(FindObjectsByType<Interactable>(FindObjectsInactive.Include, FindObjectsSortMode.InstanceID));
            if (items.Count == 0) Debug.LogWarning("SceneFlow: 調べる対象が 1 つも見つからない", this);
            progress = new SceneProgress(items);
            seatedSpot = player.transform.position;
            if (chair != null) chairSpot = chair.position;
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

        /// <summary>
        /// 場面の頭は黒から明ける。前の場面から切り替わった直後の目の慣れを兼ねる。
        /// 見出しがあるときは、黒いうちにそれを出してから明ける
        /// </summary>
        IEnumerator Start()
        {
            if (!string.IsNullOrEmpty(openingCard))
            {
                // 幕は見出しの下に敷く層。暗転の層とは別に持つ
                hud.SetFade(0f);
                hud.SetCurtain(true);
                hud.SetCenter(openingCard);
                yield return new WaitForSeconds(cardSeconds);
                hud.SetCenter(null);
                yield return hud.CurtainTo(0f, cardLiftSeconds);
                hud.SetCurtain(false);
                yield break;
            }
            hud.SetFade(1f);
            yield return hud.FadeTo(0f, FadeInSeconds);
        }

        /// <summary>次のフレームで調べる操作を 1 回起こす。E キーの代わりに、再生中の動作確認から SendMessage で呼ぶ</summary>
        public void PressInteract() => pendingInteract = true;

        /// <summary>字幕を積む。場面固有の演出から呼ぶ。ログにも残す（名前があれば会話、無ければ独白）</summary>
        public void Say(IReadOnlyList<string> lines)
        {
            subtitles.Enqueue(lines);
            ConsoleLog.Said(lines);
        }

        /// <summary>seconds 秒のあいだ、調べる操作と進行を止める。すでに止まっているときは長い方を採る</summary>
        public void Freeze(float seconds) => frozenUntil = Mathf.Max(frozenUntil, Time.time + seconds);

        void Update()
        {
            // エディタで再生したままスクリプトを組み直すと、Awake を通さずに Update だけが続く。
            // 覚えていたものは消えているので、拾い直せるものはここで拾い直す。
            // これが無いと、例外がフレームごとに流れてログが読めなくなる
            if (items == null)
            {
                items = new List<IInteractable>(FindObjectsByType<Interactable>(FindObjectsInactive.Include, FindObjectsSortMode.InstanceID));
                progress = new SceneProgress(items);
                Debug.LogWarning("SceneFlow: 再生中に組み直されたので、調べる対象を拾い直した", this);
            }
            // TAB のコンソールを開いている間は、場面の進行を丸ごと止める。
            // 字幕と印は HudView が伏せ、秒はコンソールが止めている
            if (ImplantConsole.IsOpen) return;
            if (Completed) return;
            var frozen = Frozen;
            var interact = (player.InteractPressed || pendingInteract) && !frozen;
            // 止まっている間に届いた PressInteract は捨てずに持ち越す。実キー入力はその場限りなので落ちる
            if (!frozen) pendingInteract = false;
            if (subtitles.IsTalking && interact)
            {
                subtitles.Advance();
                interact = false;
                // 文を読み終えたところで二択を出す
                if (!subtitles.IsTalking && asking != null && choice == null) OpenChoice();
            }
            if (choice != null)
            {
                // 二択を出している間は、見回す以外は受け付けない
                Ask(interact);
                interact = false;
            }
            IInteractable selected = null;
            if (choice == null && !subtitles.IsTalking && !frozen)
            {
                var eye = player.Eye;
                selected = InteractionPicker.Select(eye.position, eye.forward, items, progress.Done, maxAngle);
            }
            hud.SetPrompt(selected != null ? "E  " + selected.Label : null);
            if (selected != null && interact)
            {
                // 前提が揃っているかは、済んだことにする前に見る
                var ready = InteractionPicker.UnmetPrerequisite(selected, progress.Done) == null;
                var said = progress.Examine(selected);
                subtitles.Enqueue(said);
                ConsoleLog.Examined(selected.Label, said);
                if (ready && Examining != null) Examining(selected);
                // 二択を持つ対象は、文を読み終えてから問う
                asking = selected.Asks ? selected : null;
                if (asking != null && !subtitles.IsTalking) OpenChoice();
                if (progress.Done.Contains(selected.Id) && Examined != null) Examined(selected);
            }
            // 調べた先の演出が Freeze を呼ぶので、止まっているかは調べた後に見直す
            var frozenNow = Frozen;
            ReleaseDaze();
            Wake();
            // 独白を読み終えてから腰を上げる。喋りながら立ち上がらせない
            Stand(frozenNow || subtitles.IsTalking || choice != null);
            // 送れるのは、二択でなく、止まってもいない間だけ。そのときだけ「E　送る」を添える
            hud.SetSubtitle(choice != null ? choice.Compose() : subtitles.Current,
                choice == null ? SubtitleKind.Line : SubtitleKind.Choice, choice == null && !frozenNow);
            if (progress.IsComplete && !subtitles.IsTalking && choice == null && !frozenNow && !Held) StartCoroutine(Complete());
        }

        /// <summary>
        /// 二択を出しているあいだ。左右で選び、調べる操作で決める。
        /// 「はい」なら済んだことにして続きの文を出し、「いいえ」なら何もせず閉じる
        /// </summary>
        void Ask(bool interact)
        {
            var step = player.ChoiceStep;
            if (step != 0 && step != lastStep) choice.Move(step);
            lastStep = step;
            if (!interact) return;
            var accepted = choice.Accepted;
            var item = asking;
            ConsoleLog.Picked(choice.Question, accepted ? Choice.Yes : Choice.No);
            CloseChoice();
            if (!accepted) return;
            var said = progress.Confirm(item);
            subtitles.Enqueue(said);
            ConsoleLog.Examined(item.Label, said);
            if (Examined != null) Examined(item);
        }

        /// <summary>二択を開く。左右の入力が歩きに化けないよう、そのあいだは足を止める</summary>
        void OpenChoice()
        {
            choice = new Choice(asking.Question);
            lastStep = 0;
            player.CanMove = false;
        }

        void CloseChoice()
        {
            choice = null;
            asking = null;
            lastStep = 0;
            player.CanMove = standUp == null || standUp.Standing;
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
            // 椅子は後ろへ下がる。押しのけないと机とのあいだに立てない
            if (chair == null) return;
            chair.position = chairSpot - chair.forward * (chairPushBack * k);
        }

        IEnumerator Complete()
        {
            Completed = true;
            player.CanMove = false;
            hud.SetPrompt(null);
            hud.SetSubtitle(null);
            // 出がけの音。扉を閉めてから暗転へ移る
            if (exitSound != null)
            {
                exitSound.Play();
                if (exitSoundSeconds > 0f) yield return new WaitForSeconds(exitSoundSeconds);
            }
            // 場面のつなぎは暗転を挟まない。止まるときだけ黒く落とす
            if (!SceneExit.Continues(nextScene))
            {
                yield return hud.FadeTo(1f, FadeSeconds);
                hud.SetCenter(ToBeContinued);
                yield break;
            }
            // 見出しを挟んで渡すときだけ、先に黒く落とす。
            // 薄れさせると扉の閉まる音と合わないので、そこは切り替える
            if (cutToBlack)
            {
                hud.SetFade(1f);
                if (blackHoldSeconds > 0f) yield return new WaitForSeconds(blackHoldSeconds);
            }
            else yield return null;
            SceneManager.LoadScene(SceneExit.Target(nextScene));
        }
    }
}
