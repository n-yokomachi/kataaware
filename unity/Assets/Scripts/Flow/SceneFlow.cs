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
    /// 二択は画面の真ん中の札（<see cref="HudView.SetChoice"/>）に出し、そのあいだはカーソルを出して札をマウスでも選べるようにする。
    /// 止まっている間は調べる操作と進行が止まる。見回しと移動は止めない（場面 1 の停止はすべて座っている間に起きる）。
    /// 必須の対象をすべて調べ、字幕も出ておらず、止まってもいなければ暗転して「続く」を出す。
    ///
    /// **調べたら、その物を画面の真ん中へ持ってくる。** 調べる操作は視線から 40 度の内の物を拾うので、
    /// 調べた物が真ん中から外れていることがある。調べたら目をその物へ回し（<see cref="PlayerController.Face"/>）、
    /// その物の字幕・二択・その物が起こした止まりが済むまで見回しと歩きを封じる（<see cref="PlayerController.HoldLook"/>・
    /// <see cref="PlayerController.HoldMove"/>）。目は 1 秒かけてなめらかに回す（<see cref="PlayerController.FaceSeconds"/>）。
    /// 演出がもともと目を動かす所（端末の前へ座る、露店の内側へ回る）は、演出の側が勝つ
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
        readonly ChoicePointer pointer = new ChoicePointer();
        /// <summary>二択を開いたフレーム。札はまだ出ておらず、カーソルの位置も古いので、マウスは次のフレームから見る</summary>
        bool opened;
        /// <summary>調べている物。その字幕・二択・止まりが済むまで見回しを封じる。調べていなければ null</summary>
        IInteractable attending;
        /// <summary>その物の文（と、調べたその時に演出が足した文）を積み終えた所。<see cref="SubtitleQueue.Passed"/> がここへ届けば読み終えた</summary>
        int attendLines;
        /// <summary>その物を調べたその時に掛かった止まりの終わり</summary>
        float attendUntil;
        /// <summary>場面の演出の、状態を取り出す・当てる口。初めて使う時に拾う</summary>
        List<ISceneMemory> memories;
        /// <summary>直近の自由に動ける所の写し。まだ一度も来ていなければ null</summary>
        SceneMemo kept;
        /// <summary>前のフレームも自由に動ける所だったか。来たばかりのフレームで写しを取り直す</summary>
        bool keptLast;
        /// <summary>写しを取った時の調べ済みの数。増えたら取り直す</summary>
        int keptDone = -1;
        /// <summary>思い出して来た。場面の頭の見出しを出さない</summary>
        bool resumed;

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
            // 思い出して来た時は見出しを出さない。当て終えた形を黒から明ける
            if (!resumed && !string.IsNullOrEmpty(openingCard))
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
            hud.SetPrompt(selected != null ? HudView.Prompt(selected.Label) : null);
            if (selected != null && interact)
            {
                // 目は先に向け始める。同じフレームで演出が見回しを預かるか向きを書き換えたら、そちらが勝つ
                player.Face(selected.Position);
                player.HoldLook(this);
                player.HoldMove(this);
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
                attending = selected;
                Attended();
            }
            // 調べた先の演出が Freeze を呼ぶので、止まっているかは調べた後に見直す
            var frozenNow = Frozen;
            Unattend();
            ReleaseDaze();
            Wake();
            // 独白を読み終えてから腰を上げる。喋りながら立ち上がらせない
            Stand(frozenNow || subtitles.IsTalking || choice != null);
            // 二択は字幕の枠から出して札に浮かべる。そのあいだ字幕は下げる。
            // 送れるのは止まっていない間だけ。そのときだけ「E　送る」を添える
            hud.SetSubtitle(choice != null ? null : subtitles.Current, SubtitleKind.Line, !frozenNow);
            hud.SetChoice(choice);
            if (progress.IsComplete && !subtitles.IsTalking && choice == null && !frozenNow && !Held) StartCoroutine(Complete());
            Keep();
        }

        // ---- 記憶する・思い出す（設計書 5 節） ------------------------------------
        //
        // 記憶するは、押した時の場面の中の状態を残す。台詞・二択・止まり・演出の途中で押した時は、その直前の自由に動ける所を残す。
        // 自由に動けるフレームごとに写しを持っておき（Keep）、押した時に自由に動けなければそれを渡す（Kept）

        /// <summary>
        /// いまが自由に動ける所か。字幕・二択・止まりのどれも無く、場面を閉じ始めてもおらず、調べている物も無く、
        /// 目覚めと立ち上がりの途中でもなく、どの演出も途中でない（<see cref="ISceneMemory.Settled"/>）。
        /// <see cref="Held"/> はここでは見ない。押さえている演出（路地裏の売り買い・車内の帯）が、自分の区切りを Settled で言う
        /// </summary>
        public bool Calm
        {
            get
            {
                if (Completed || subtitles.IsTalking || choice != null || asking != null || Frozen) return false;
                if (attending != null) return false;
                if (wakeUp != null && !wakeUp.Done) return false;
                if (standUp != null && standUp.Rising) return false;
                return SceneMemory.Settled(Memories);
            }
        }

        List<ISceneMemory> Memories
        {
            get
            {
                if (memories == null) memories = SceneMemory.Find();
                return memories;
            }
        }

        /// <summary>
        /// フレームの終わりに、自由に動ける所なら写しを持つ。来たばかりのフレームと、調べ済みが増えたフレームは丸ごと取り直し、
        /// それ以外は立ち位置と向きだけ書き直す（毎フレーム演出ごとの状態を JSON にしない）
        /// </summary>
        void Keep()
        {
            if (!Calm)
            {
                keptLast = false;
                return;
            }
            if (!keptLast || kept == null || keptDone != progress.Done.Count) kept = Capture();
            else SceneMemory.Hold(player, kept);
            keptLast = true;
        }

        /// <summary>
        /// いまを区切りとして写しを取る。自由に動けるフレームの無い演出（路地裏の売り買いの、買い手と買い手の間）が、
        /// ここからなら続けられるという所で呼ぶ
        /// </summary>
        public void Checkpoint()
        {
            kept = Capture();
            keptLast = false;
        }

        /// <summary>いまの状態を丸ごと取り出す。動作確認からも呼ぶ</summary>
        public SceneMemo Capture()
        {
            Ensure();
            var memo = new SceneMemo();
            SceneMemory.Hold(player, memo);
            var ids = new List<string>(progress.Done);
            ids.Sort(string.CompareOrdinal);
            memo.done = ids.ToArray();
            memo.parts = SceneMemory.Capture(Memories);
            keptDone = progress.Done.Count;
            return memo;
        }

        /// <summary>
        /// 記憶するが書く状態。いま自由に動けるならその場で取り、そうでなければ直前の自由に動ける所の写し。
        /// 場面に入ってからまだ一度も自由に動ける所へ来ていなければ null（場面の頭を書く）
        /// </summary>
        public SceneMemo Kept()
        {
            if (player != null && Calm) kept = Capture();
            return kept;
        }

        /// <summary>
        /// 思い出した時に、残した状態を当てる。シーンを読んだ直後、最初のフレームを出す前に呼ぶ（<see cref="SceneMemory.Resume"/>）。
        /// 調べ済みの物を戻し、目覚めと入った時の眩暈は出さずに済ませ、立ち上がった後なら立った形にし、
        /// 演出ごとの状態を当ててから、残した立ち位置へ置く。音も字幕も出さない。黒から明けるのは Start
        /// </summary>
        public void Restore(SceneMemo memo)
        {
            if (memo == null || player == null) return;
            Ensure();
            resumed = true;
            if (memo.done != null)
                foreach (var id in memo.done)
                    if (!string.IsNullOrEmpty(id)) progress.Done.Add(id);
            // 目覚めは出さない
            wakeUp = null;
            player.CanLook = true;
            // 立ち上がった後なら、立った形。椅子は押し下げ、椅子のコライダーを入れる
            if (standAfter.Length > 0 && progress.Done.Contains(standAfter))
            {
                if (standUp != null) standUp.Finish();
                if (seatedPose != null) seatedPose.Seated = false;
                if (chair != null) chair.position = chairSpot - chair.forward * chairPushBack;
                if (chairBlocker != null) chairBlocker.SetActive(true);
            }
            // 入った時の眩暈は出さない。調べるまで保つ眩暈（場面 1 のジャック）は、まだ調べていなければ保ったまま
            if (daze != null && (dazeUntil.Length == 0 || progress.Done.Contains(dazeUntil)))
            {
                dazeReleased = true;
                daze.Clear();
            }
            SceneMemory.Restore(Memories, memo);
            SceneMemory.Place(player, memo);
            kept = memo;
            keptLast = false;
            keptDone = progress.Done.Count;
        }

        /// <summary>
        /// 調べる対象と、演出の状態を取り出す・当てる口を、シーンを探さずに渡す。テストから呼ぶ
        /// </summary>
        public void Use(IEnumerable<IInteractable> things, IEnumerable<ISceneMemory> parts)
        {
            items = new List<IInteractable>(things);
            progress = new SceneProgress(items);
            memories = new List<ISceneMemory>(parts);
            memories.Sort((a, b) => string.CompareOrdinal(a.MemoryKey, b.MemoryKey));
            if (player != null) seatedSpot = player.transform.position;
            if (chair != null) chairSpot = chair.position;
        }

        /// <summary>調べる対象を拾ってあるか。エディタで当てて撮る時は Awake が鳴らないので、ここで拾う</summary>
        void Ensure()
        {
            if (items != null) return;
            items = new List<IInteractable>(FindObjectsByType<Interactable>(FindObjectsInactive.Include, FindObjectsSortMode.InstanceID));
            progress = new SceneProgress(items);
            if (player != null) seatedSpot = player.transform.position;
            if (chair != null) chairSpot = chair.position;
        }

        /// <summary>
        /// 二択を出しているあいだ。左右で選び、調べる操作（E）で決める。
        /// マウスでは、札に入ると選び、札の上で押して離すと決める（<see cref="ChoicePointer"/>）。
        /// 止まっている間はマウスでも決めない。
        /// 「はい」なら済んだことにして続きの文を出し、「いいえ」なら何もせず閉じる
        /// </summary>
        void Ask(bool interact)
        {
            choice.Tilt(player.ChoiceStep);
            if (opened) opened = false;
            else pointer.Step(hud.ChoiceAt(player.Pointer), player.PointerPressed, player.PointerReleased);
            if (pointer.Entered >= 0) choice.Hover(pointer.Entered);
            if (pointer.Picked >= 0 && !Frozen)
            {
                choice.Hover(pointer.Picked);
                interact = true;
            }
            if (!interact) return;
            var accepted = choice.Accepted;
            var item = asking;
            ConsoleLog.Picked(choice.Question, choice.Label);
            CloseChoice();
            if (!accepted) return;
            var said = progress.Confirm(item);
            subtitles.Enqueue(said);
            ConsoleLog.Examined(item.Label, said);
            if (Examined != null) Examined(item);
            // 「はい」の後の文と、それで演出が掛けた止まりも、調べている間に入れる
            if (attending == item) Attended();
        }

        /// <summary>
        /// 調べている物の文と止まりを、いま積まれている所まで伸ばす。調べたその時（と「はい」を選んだその時）に、
        /// 演出が足した文と掛けた止まりまで含める。そのあと演出が別に積む文（路地裏の買い手の台詞など）は含めない
        /// </summary>
        void Attended()
        {
            attendLines = subtitles.Queued;
            attendUntil = Mathf.Max(attendUntil, frozenUntil);
        }

        /// <summary>調べている物の文を読み終え、二択も閉じ、止まりも過ぎたら、見回しを返す</summary>
        void Unattend()
        {
            if (attending == null) return;
            if (StillAttending(subtitles.Passed, attendLines, choice != null, Time.time, attendUntil)) return;
            Forget();
        }

        /// <summary>
        /// 調べている間か。その物の文（積み終えた所 lines まで）を送り終えていない、二択を出している、
        /// その物が起こした止まり（until まで）が続いている、のどれかなら true。見回しを封じる間
        /// </summary>
        public static bool StillAttending(int passed, int lines, bool choosing, float now, float until)
        {
            return passed < lines || choosing || now < until;
        }

        /// <summary>
        /// 調べている物を手放し、見回しと歩きを返す。歩きは封じを解くだけで、座っている間や演出が止めている間
        /// （<see cref="PlayerController.CanMove"/> が false）はそのまま
        /// </summary>
        void Forget()
        {
            attending = null;
            attendUntil = 0f;
            if (player == null) return;
            player.FreeLook(this);
            player.FreeMove(this);
        }

        void OnDisable()
        {
            Forget();
        }

        /// <summary>
        /// 二択を開く。左右の入力が歩きに化けないよう、そのあいだは足を止める。
        /// カーソルを出してロックを外し、見回しを止める（<see cref="PlayerController.Pointing"/>）
        /// </summary>
        void OpenChoice()
        {
            choice = new Choice(asking.Question);
            pointer.Reset();
            opened = true;
            player.CanMove = false;
            player.Pointing = true;
        }

        /// <summary>二択を閉じる。カーソルはロックして隠す</summary>
        void CloseChoice()
        {
            choice = null;
            asking = null;
            pointer.Reset();
            player.Pointing = false;
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
            Forget();
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
