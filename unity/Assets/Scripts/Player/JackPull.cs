using UnityEngine;

namespace HalfAware
{
    /// <summary>
    /// 右手首のジャックを左手で抜く。座位の姿勢の上に曲げを重ねて動かす。
    /// まず手首へ視線を落として刺さっているところを見せ、掴んでから引き抜き、
    /// ケーブルが張って見えるところまで前へ出す。見せ終わったら肘掛けへ置く。
    /// 視線はジャックを追うので、抜けた瞬間もケーブルも画面から外れない。
    /// 抜いている間は調べる操作も見回しも止める
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
        [Tooltip("掴んだジャックの置き所。左手のここへ移す。無ければ掴んだ姿勢のまま預ける")]
        [SerializeField] Transform grip;
        [Tooltip("抜いた後にジャックを置く場所。肘掛けの差込口の脇")]
        [SerializeField] Transform parked;
        [Tooltip("抜ける音。無くても動く")]
        [SerializeField] AudioSource source;
        [SerializeField] AudioClip unplug;
        [Tooltip("右の手首を持ち上げて、刺さっているジャックを目の前へ出す曲げ")]
        [SerializeField] SeatedPose.BoneTurn[] look = new SeatedPose.BoneTurn[0];
        [Tooltip("左手を右手首へ伸ばす曲げ")]
        [SerializeField] SeatedPose.BoneTurn[] reach = new SeatedPose.BoneTurn[0];
        [Tooltip("引き抜いて手を退ける曲げ")]
        [SerializeField] SeatedPose.BoneTurn[] lift = new SeatedPose.BoneTurn[0];
        [Tooltip("抜いたジャックを前へ出す曲げ。ケーブルが見えるところまで")]
        [SerializeField] SeatedPose.BoneTurn[] show = new SeatedPose.BoneTurn[0];

        SeatedPose.BoneTurn[] scratch;
        float elapsed = -1f;
        float fromYaw;
        float fromPitch;
        float aimYaw;
        float aimPitch;
        bool aimed;
        bool held;
        bool letGo;
        bool sounded;
        bool tookLook;

        /// <summary>抜いている最中か。動作確認から読む</summary>
        public bool Pulling { get { return elapsed >= 0f && !PullTimeline.Done(elapsed); } }

        /// <summary>もう手首から抜けているか。刺さっている印を消すのに使う</summary>
        public bool Unplugged { get { return elapsed >= 0f && PullTimeline.Out(elapsed); } }

        /// <summary>抜き終わったか。動作確認から読む</summary>
        public bool Pulled { get; private set; }

        void OnEnable()
        {
            if (flow != null) flow.Examined += OnExamined;
            scratch = new SeatedPose.BoneTurn[look.Length + reach.Length + lift.Length + show.Length];
        }

        void OnDisable()
        {
            if (flow != null) flow.Examined -= OnExamined;
            Release();
        }

        void OnExamined(IInteractable item)
        {
            if (item == null || item.Id != id || elapsed >= 0f) return;
            elapsed = 0f;
            var player = flow != null ? flow.Player : null;
            if (player != null)
            {
                fromYaw = player.Yaw;
                fromPitch = player.Pitch;
                // 抜き終わるまでは見回しも受け付けない。視線はこちらで運ぶ
                player.CanLook = false;
                tookLook = true;
            }
            // 抜き終わるまでは他を調べさせない
            if (flow != null) flow.Freeze(PullTimeline.Total);
        }

        void Update()
        {
            if (elapsed < 0f || Pulled) return;
            elapsed += Time.deltaTime;
            Apply(elapsed);
            Aim(elapsed);
            if (PullTimeline.Held(elapsed) && !held) Grip();
            if (PullTimeline.Out(elapsed) && !sounded) Sound();
            if (PullTimeline.LetGo(elapsed) && !letGo) Park();
            if (!PullTimeline.Done(elapsed)) return;
            Pulled = true;
            if (pose != null) { pose.Extra = null; pose.ExtraWeight = 0f; }
            Release();
        }

        /// <summary>段ごとの曲げを重みつきで 1 本にまとめ、座位の上へ重ねる</summary>
        void Apply(float t)
        {
            if (pose == null) return;
            var n = 0;
            // 手首を持ち上げるのは視線を落とすのと同じ間合いで。見せる段はこの 2 つで出来ている
            Blend(look, PullTimeline.Aim(t), ref n);
            Blend(reach, PullTimeline.Reach(t), ref n);
            Blend(lift, PullTimeline.Lift(t), ref n);
            Blend(show, PullTimeline.Show(t), ref n);
            pose.Extra = scratch;
            pose.ExtraWeight = 1f;
        }

        void Blend(SeatedPose.BoneTurn[] turns, float weight, ref int n)
        {
            for (var i = 0; i < turns.Length; i++)
            {
                var turn = turns[i];
                turn.degrees *= weight;
                scratch[n++] = turn;
            }
        }

        /// <summary>
        /// 視線をジャックへ寄せる。ジャックは掴めば左手について動くので、
        /// 追っているだけで抜ける瞬間も、前へ出したケーブルも画面に入る。
        /// 手を離した後は追わない。ジャックは肘掛けへ移るので、追うと視線が右下へ飛ぶ
        /// </summary>
        void Aim(float t)
        {
            var player = flow != null ? flow.Player : null;
            if (player == null || player.Eye == null) return;
            if (PullTimeline.Follows(t) && jack != null)
            {
                var to = jack.position - player.Eye.position;
                if (to.sqrMagnitude > 1e-6f)
                {
                    to.Normalize();
                    aimYaw = Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg;
                    aimPitch = -Mathf.Asin(Mathf.Clamp(to.y, -1f, 1f)) * Mathf.Rad2Deg;
                    aimed = true;
                }
            }
            if (!aimed) return;
            // 離した後は最後の狙いを保ったまま、戻しの間に元の向きへ帰る
            var k = PullTimeline.Aim(t);
            player.Yaw = Mathf.LerpAngle(fromYaw, aimYaw, k);
            player.Pitch = Mathf.Lerp(fromPitch, aimPitch, k);
        }

        /// <summary>掴んだ。ここからジャックは左手について動く</summary>
        void Grip()
        {
            held = true;
            if (jack == null) return;
            if (grip != null)
            {
                // 決めた持ち方へ移す。掴んだ角度まかせにすると手のひらの陰に入って見えない
                jack.SetParent(grip, false);
                jack.localPosition = Vector3.zero;
                jack.localRotation = Quaternion.identity;
                var s = grip.lossyScale.x;
                jack.localScale = Vector3.one / (Mathf.Approximately(s, 0f) ? 1f : s);
                return;
            }
            if (leftHand != null) jack.SetParent(leftHand, true);
        }

        /// <summary>抜けた音。引き抜く段の頭で鳴らす</summary>
        void Sound()
        {
            sounded = true;
            if (source == null || unplug == null) return;
            source.PlayOneShot(unplug);
        }

        /// <summary>見せ終わって肘掛けへ置く</summary>
        void Park()
        {
            letGo = true;
            if (jack == null || parked == null) return;
            jack.SetParent(parked, false);
            jack.localPosition = Vector3.zero;
            jack.localRotation = Quaternion.identity;
            jack.localScale = Vector3.one;
        }

        /// <summary>見回しを返す。途中で切られても視線が固まったままにならないよう、ここへ集めてある</summary>
        void Release()
        {
            if (!tookLook) return;
            tookLook = false;
            var player = flow != null ? flow.Player : null;
            if (player != null) player.CanLook = true;
        }
    }
}
