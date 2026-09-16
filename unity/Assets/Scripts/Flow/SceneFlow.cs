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
        bool pendingInteract;
        float frozenUntil;
        bool dazeReleased;

        /// <summary>対象を調べて済んだ直後。前提が未達で文だけ出たときは呼ばない</summary>
        public event Action<IInteractable> Examined;

        public SceneProgress Progress => progress;

        /// <summary>場面固有の演出が、向きを変えたり見回しを止めたりするのに使う</summary>
        public PlayerController Player => player;
        public bool Completed { get; private set; }

        /// <summary>調べる操作と進行が止まっているか。見回しは止めない</summary>
        public bool Frozen => Time.time < frozenUntil;

        void Awake()
        {
            if (player == null || hud == null)
            {
                Debug.LogError("SceneFlow: player か hud が未接続", this);
                enabled = false;
                return;
            }
            items = new List<IInteractable>(FindObjectsByType<Interactable>(FindObjectsSortMode.InstanceID));
            if (items.Count == 0) Debug.LogWarning("SceneFlow: 調べる対象が 1 つも見つからない", this);
            progress = new SceneProgress(items);
            if (standAfter.Length > 0)
            {
                standUp = new StandUp(seatEyeHeight, PlayerController.StandingEyeHeight, StandSeconds);
                player.CanMove = false;
                player.EyeHeight = seatEyeHeight;
            }
            if (daze != null && dazeUntil.Length > 0) daze.Hold(dazeBlur, dazeWobble);
        }

        /// <summary>次のフレームで調べる操作を 1 回起こす。E キーの代わりに、再生中の動作確認から SendMessage で呼ぶ</summary>
        public void PressInteract() => pendingInteract = true;

        /// <summary>字幕を積む。場面固有の演出から呼ぶ</summary>
        public void Say(IReadOnlyList<string> lines) => subtitles.Enqueue(lines);

        /// <summary>seconds 秒のあいだ、調べる操作と進行を止める。すでに止まっているときは長い方を採る</summary>
        public void Freeze(float seconds) => frozenUntil = Mathf.Max(frozenUntil, Time.time + seconds);

        void Update()
        {
            if (Completed) return;
            var frozen = Frozen;
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
                subtitles.Enqueue(progress.Examine(selected));
                if (progress.Done.Contains(selected.Id) && Examined != null) Examined(selected);
            }
            // 調べた先の演出が Freeze を呼ぶので、止まっているかは調べた後に見直す
            var frozenNow = Frozen;
            ReleaseDaze();
            Stand(frozenNow);
            hud.SetSubtitle(subtitles.Current);
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

        /// <summary>standAfter の対象を調べたら、止まっていない間に目線を上げて移動を許す</summary>
        void Stand(bool frozen)
        {
            if (standUp == null || standUp.Standing) return;
            standUp.Tick(Time.deltaTime, progress.Done.Contains(standAfter), frozen);
            player.EyeHeight = standUp.EyeHeight;
            player.CanMove = standUp.Standing;
        }

        IEnumerator Complete()
        {
            Completed = true;
            player.CanMove = false;
            hud.SetPrompt(null);
            hud.SetSubtitle(null);
            hud.CancelSmoke();
            yield return hud.FadeTo(1f, FadeSeconds);
            hud.SetCenter(ToBeContinued);
        }
    }
}
