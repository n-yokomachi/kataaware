using System.Collections.Generic;
using UnityEngine;

namespace HalfAware
{
    /// <summary>
    /// 歩いて調べる場面 1 つ分の進行。毎フレーム、対象の選択 → 印 → 調べる → 字幕 → 完了の判定の順に進める。
    /// 字幕の表示中は E とクリックを字幕の送りにだけ使い、調べる操作は受け付けない。
    /// 必須の対象をすべて調べ、字幕も出ていなければ完了。この段では中央に「（仮）続く」を出して止まる
    /// </summary>
    public sealed class SceneFlow : MonoBehaviour
    {
        public const string ToBeContinued = "（仮）続く";

        [SerializeField] PlayerController player;
        [SerializeField] HudView hud;
        [Tooltip("ラジアン。視線からこの角度以内の対象だけ選ぶ")]
        [SerializeField] float maxAngle = InteractionPicker.MaxAngle;

        readonly SubtitleQueue subtitles = new SubtitleQueue();
        List<IInteractable> items;
        SceneProgress progress;
        bool pendingInteract;

        public SceneProgress Progress => progress;
        public bool Completed { get; private set; }

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
        }

        /// <summary>次のフレームで調べる操作を 1 回起こす。E キーの代わりに、再生中の動作確認から SendMessage で呼ぶ</summary>
        public void PressInteract() => pendingInteract = true;

        void Update()
        {
            if (Completed) return;
            var interact = player.InteractPressed || pendingInteract;
            pendingInteract = false;
            if (subtitles.IsTalking && interact)
            {
                subtitles.Advance();
                interact = false;
            }
            IInteractable selected = null;
            if (!subtitles.IsTalking)
            {
                var eye = player.Eye;
                selected = InteractionPicker.Select(eye.position, eye.forward, items, progress.Done, maxAngle);
            }
            hud.SetPrompt(selected != null ? "E  " + selected.Label : null);
            if (selected != null && interact) subtitles.Enqueue(progress.Examine(selected));
            hud.SetSubtitle(subtitles.Current);
            if (progress.IsComplete && !subtitles.IsTalking) Complete();
        }

        void Complete()
        {
            Completed = true;
            hud.SetPrompt(null);
            hud.SetSubtitle(null);
            hud.SetCenter(ToBeContinued);
        }
    }
}
