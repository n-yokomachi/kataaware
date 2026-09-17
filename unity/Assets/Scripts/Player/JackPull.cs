using UnityEngine;

namespace HalfAware
{
    /// <summary>
    /// 右手首のジャックを左手で抜く。座位の姿勢の上に曲げを重ねて動かし、
    /// 掴んだところでジャックを左手へ預け、抜き終わったら肘掛けへ置く。
    /// 抜いている間は調べる操作を止める
    /// </summary>
    [DefaultExecutionOrder(25)]
    public sealed class JackPull : MonoBehaviour
    {
        [SerializeField] SceneFlow flow;
        [Tooltip("座位の姿勢。ここへ曲げを重ねる")]
        [SerializeField] SeatedPose pose;
        [Tooltip("この id を調べたら抜き始める")]
        [SerializeField] string id = "jack";
        [Tooltip("手首に刺さっているジャック")]
        [SerializeField] Transform jack;
        [Tooltip("掴んでいる間ジャックを預ける骨。左の手首")]
        [SerializeField] Transform leftHand;
        [Tooltip("抜いた後にジャックを置く場所。肘掛けの差込口の脇")]
        [SerializeField] Transform parked;
        [Tooltip("左手を右手首へ伸ばす曲げ")]
        [SerializeField] SeatedPose.BoneTurn[] reach = new SeatedPose.BoneTurn[0];
        [Tooltip("引き抜いて手を退ける曲げ")]
        [SerializeField] SeatedPose.BoneTurn[] lift = new SeatedPose.BoneTurn[0];

        SeatedPose.BoneTurn[] scratch;
        float elapsed = -1f;
        bool held;
        bool letGo;

        /// <summary>抜いている最中か。動作確認から読む</summary>
        public bool Pulling { get { return elapsed >= 0f && !PullTimeline.Done(elapsed); } }

        /// <summary>抜き終わったか。動作確認から読む</summary>
        public bool Pulled { get; private set; }

        void OnEnable()
        {
            if (flow != null) flow.Examined += OnExamined;
            scratch = new SeatedPose.BoneTurn[reach.Length + lift.Length];
        }

        void OnDisable()
        {
            if (flow != null) flow.Examined -= OnExamined;
        }

        void OnExamined(IInteractable item)
        {
            if (item == null || item.Id != id || elapsed >= 0f) return;
            elapsed = 0f;
            // 抜き終わるまでは他を調べさせない
            if (flow != null) flow.Freeze(PullTimeline.Total);
        }

        void Update()
        {
            if (elapsed < 0f || Pulled) return;
            elapsed += Time.deltaTime;
            Apply(elapsed);
            if (PullTimeline.Held(elapsed) && !held) Grip();
            if (PullTimeline.LetGo(elapsed) && !letGo) Park();
            if (!PullTimeline.Done(elapsed)) return;
            Pulled = true;
            if (pose != null) { pose.Extra = null; pose.ExtraWeight = 0f; }
        }

        /// <summary>2 つの曲げを重みつきで 1 本にまとめ、座位の上へ重ねる</summary>
        void Apply(float t)
        {
            if (pose == null) return;
            var a = PullTimeline.Reach(t);
            var b = PullTimeline.Lift(t);
            var n = 0;
            for (var i = 0; i < reach.Length; i++)
            {
                var turn = reach[i];
                turn.degrees *= a;
                scratch[n++] = turn;
            }
            for (var i = 0; i < lift.Length; i++)
            {
                var turn = lift[i];
                turn.degrees *= b;
                scratch[n++] = turn;
            }
            pose.Extra = scratch;
            pose.ExtraWeight = 1f;
        }

        /// <summary>掴んだ。ここからジャックは左手について動く</summary>
        void Grip()
        {
            held = true;
            if (jack == null || leftHand == null) return;
            jack.SetParent(leftHand, true);
        }

        /// <summary>抜き終わって肘掛けへ置く</summary>
        void Park()
        {
            letGo = true;
            if (jack == null || parked == null) return;
            jack.SetParent(parked, false);
            jack.localPosition = Vector3.zero;
            jack.localRotation = Quaternion.identity;
            jack.localScale = Vector3.one;
        }
    }
}
