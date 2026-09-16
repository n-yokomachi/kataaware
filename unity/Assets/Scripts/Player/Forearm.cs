using UnityEngine;

namespace HalfAware
{
    /// <summary>
    /// カメラの子に付ける前腕。下を向いたときだけ出す。主観視点に手を描かない方針の例外。
    /// ジャックを抜いたら、ケーブルつきの腕からケーブルなしの腕へ替える。
    /// 腕が見えていないあいだはジャックを選べない。上を向いたまま抜けてしまうのを防ぐ
    /// </summary>
    [DefaultExecutionOrder(-5)]
    public sealed class Forearm : MonoBehaviour
    {
        /// <summary>この id を調べるとジャックが抜けたことにする</summary>
        public const string JackId = "jack";

        [SerializeField] PlayerController player;
        [Tooltip("抜いたことを受け取る。無くても腕の出し入れは動く")]
        [SerializeField] SceneFlow flow;
        [Tooltip("ジャックが刺さっている前腕")]
        [SerializeField] GameObject withJack;
        [Tooltip("抜いた後の前腕")]
        [SerializeField] GameObject withoutJack;
        [Tooltip("抜く操作の対象。腕が出ていないあいだは選べないようにする")]
        [SerializeField] GameObject jackItem;
        [Tooltip("この角度より下を向くと出る。度。正が下向き")]
        [SerializeField] float showBelow = ForearmView.DefaultShowBelow;
        [Tooltip("出ている間は、この角度ぶん戻るまで消えない。度")]
        [SerializeField] float hysteresis = ForearmView.DefaultHysteresis;

        bool showing;
        bool pulled;

        /// <summary>今このフレームで腕が出ているか。動作確認から読む</summary>
        public bool Showing => showing;

        /// <summary>ジャックを抜いた後かどうか。動作確認から読む</summary>
        public bool Pulled => pulled;

        void OnEnable()
        {
            if (flow != null) flow.Examined += OnExamined;
            Apply();
        }

        void OnDisable()
        {
            if (flow != null) flow.Examined -= OnExamined;
        }

        void OnExamined(IInteractable item)
        {
            if (item != null && item.Id == JackId) pulled = true;
        }

        void LateUpdate()
        {
            if (player == null) return;
            showing = ForearmView.ShouldShow(player.Pitch, showing, showBelow, hysteresis);
            Apply();
        }

        void Apply()
        {
            if (withJack != null) withJack.SetActive(showing && !pulled);
            if (withoutJack != null) withoutJack.SetActive(showing && pulled);
            if (jackItem != null) jackItem.SetActive(showing && !pulled);
        }
    }
}
