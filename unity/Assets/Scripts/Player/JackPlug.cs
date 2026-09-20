using UnityEngine;

namespace HalfAware
{
    /// <summary>
    /// 肘掛けに置いたジャックを左手で取り、右手首へ挿す。座位の姿勢の上に曲げを重ねて動かす。
    /// まず肘掛けへ視線を落として置いてあるところを見せ、掴んで手首の前へ運び、挿す。
    /// 視線はジャックを追うので、挿さる瞬間も画面から外れない。
    /// 挿している間は調べる操作も見回しも止める。
    ///
    /// <see cref="JackPull"/>（場面 1 で抜く側）の裏返し
    /// </summary>
    [DefaultExecutionOrder(25)]
    public sealed class JackPlug : MonoBehaviour
    {
        [SerializeField] SceneFlow flow;
        [Tooltip("座位の姿勢。ここへ曲げを重ねる")]
        [SerializeField] SeatedPose pose;
        [Tooltip("この id を調べたら挿し始める")]
        [SerializeField] string id = ConnectIds.Jack;
        [Tooltip("肘掛けに置いてあるジャック")]
        [SerializeField] Transform jack;
        [Tooltip("掴んでいる間ジャックを預ける置き所。左手の JackHold")]
        [SerializeField] Transform grip;
        [Tooltip("挿さったジャックの行き先。右の手首")]
        [SerializeField] Transform socket;
        [Tooltip("挿さる音。無くても動く")]
        [SerializeField] AudioSource source;
        [SerializeField] AudioClip plug;
        [Tooltip("肘掛けのジャックへ視線を落とす曲げ")]
        [SerializeField] SeatedPose.BoneTurn[] look = new SeatedPose.BoneTurn[0];
        [Tooltip("左手を肘掛けへ伸ばす曲げ")]
        [SerializeField] SeatedPose.BoneTurn[] reach = new SeatedPose.BoneTurn[0];
        [Tooltip("掴んだジャックを右手首の前へ運ぶ曲げ")]
        [SerializeField] SeatedPose.BoneTurn[] carry = new SeatedPose.BoneTurn[0];
        [Tooltip("挿し込む曲げ")]
        [SerializeField] SeatedPose.BoneTurn[] push = new SeatedPose.BoneTurn[0];

        SeatedPose.BoneTurn[] scratch;
        float elapsed = -1f;
        float fromYaw;
        float fromPitch;
        float aimYaw;
        float aimPitch;
        bool aimed;
        bool held;
        bool seated;
        bool sounded;
        bool tookLook;

        /// <summary>挿している最中か</summary>
        public bool Plugging { get { return elapsed >= 0f && !PlugTimeline.Done(elapsed); } }

        /// <summary>もう手首に挿さっているか。モニターを灯す合図に使う</summary>
        public bool Plugged { get { return elapsed >= 0f && PlugTimeline.In(elapsed); } }

        /// <summary>しぐさが終わったか</summary>
        public bool Done { get; private set; }

        void OnEnable()
        {
            if (flow != null) flow.Examined += OnExamined;
            scratch = new SeatedPose.BoneTurn[look.Length + reach.Length + carry.Length + push.Length];
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
                // 挿し終わるまでは見回しも受け付けない。視線はこちらで運ぶ
                player.CanLook = false;
                tookLook = true;
            }
            // 挿し終わるまでは他を調べさせない
            if (flow != null) flow.Freeze(PlugTimeline.Total);
        }

        void Update()
        {
            if (elapsed < 0f || Done) return;
            elapsed += Time.deltaTime;
            Apply(elapsed);
            Aim(elapsed);
            if (PlugTimeline.Held(elapsed) && !held) Grip();
            if (PlugTimeline.In(elapsed) && !seated) Seat();
            if (PlugTimeline.In(elapsed) && !sounded) Sound();
            if (!PlugTimeline.Done(elapsed)) return;
            Done = true;
            if (pose != null) { pose.Extra = null; pose.ExtraWeight = 0f; }
            Release();
        }

        /// <summary>段ごとの曲げを重みつきで 1 本にまとめ、座位の上へ重ねる</summary>
        void Apply(float t)
        {
            if (pose == null) return;
            var n = 0;
            Blend(look, PlugTimeline.Aim(t), ref n);
            Blend(reach, PlugTimeline.Reach(t), ref n);
            Blend(carry, PlugTimeline.Carry(t), ref n);
            Blend(push, PlugTimeline.Push(t), ref n);
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

        /// <summary>視線をジャックへ寄せる。掴めば左手について動くので、挿さる瞬間も画面に入る</summary>
        void Aim(float t)
        {
            var player = flow != null ? flow.Player : null;
            if (player == null || player.Eye == null) return;
            if (PlugTimeline.Follows(t) && jack != null)
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
            // 手を戻す間は最後の狙いを保ったまま、元の向きへ帰る
            var k = PlugTimeline.Aim(t);
            player.Yaw = Mathf.LerpAngle(fromYaw, aimYaw, k);
            player.Pitch = Mathf.Lerp(fromPitch, aimPitch, k);
        }

        /// <summary>掴んだ。ここからジャックは左手について動く</summary>
        void Grip()
        {
            held = true;
            if (jack == null || grip == null) return;
            // 決めた持ち方へ移す。掴んだ角度まかせにすると手のひらの陰に入って見えない
            jack.SetParent(grip, false);
            jack.localPosition = Vector3.zero;
            jack.localRotation = Quaternion.identity;
            var s = grip.lossyScale.x;
            jack.localScale = Vector3.one / (Mathf.Approximately(s, 0f) ? 1f : s);
        }

        /// <summary>挿さった。ジャックは手首のものになる</summary>
        void Seat()
        {
            seated = true;
            if (jack == null || socket == null) return;
            jack.SetParent(socket, false);
            jack.localPosition = Vector3.zero;
            jack.localRotation = Quaternion.identity;
            jack.localScale = Vector3.one;
        }

        /// <summary>挿さる音。挿さった頭で鳴らす</summary>
        void Sound()
        {
            sounded = true;
            if (source == null || plug == null) return;
            source.PlayOneShot(plug);
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
