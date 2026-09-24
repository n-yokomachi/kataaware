using UnityEngine;

namespace HalfAware
{
    /// <summary>
    /// 右手首のジャックを左手で抜く。座った形（<see cref="SeatedPose"/>）の上で、腕を二関節の IK（<see cref="ArmReach"/>）で動かす。
    /// まず右の手首を目の前へ出して刺さっているところを見せ、左手で掴み、ジャックの向きへ抜き、
    /// ケーブルが張って見えるところまで前へ出し、肘掛けの置き場まで運んで置く。最後に両手を戻す。
    /// 視線はジャックを追うので、抜けた瞬間もケーブルも画面から外れない。
    /// 抜いている間は調べる操作も見回しも止める。
    ///
    /// **掴む瞬間にずれない。** 左手の狙いは、掴むまでは右手首のジャックの今の位置から毎こま決める
    /// （左手の置き所 <see cref="grip"/> をジャックに重ねる手の位置）。掴んだ瞬間にジャックを置き所へ移しても、
    /// 置き所とジャックは重なっているので跳ねない。置くときも、置き場に重なってから移す。
    ///
    /// 狙いの値（右の手首の行き先、見せる所、肘を寄せる所）は、どれも体の根から見た値で、組み立て（PlaceProtagonist）が
    /// 椅子に合わせて書く。後からインスペクタで直せる
    /// </summary>
    [DefaultExecutionOrder(25)]
    public sealed class JackPull : MonoBehaviour
    {
        [SerializeField] SceneFlow flow;
        [Tooltip("座った形。これが骨を当てた後に腕を曲げ直す")]
        [SerializeField] SeatedPose pose;
        [Tooltip("この id を調べたら抜き始める")]
        [SerializeField] string id = "jack";
        [Tooltip("手首に刺さっているジャック")]
        [SerializeField] Transform jack;
        [Tooltip("掴んでいる間ジャックを預ける置き所。左手の JackHold")]
        [SerializeField] Transform grip;
        [Tooltip("抜いた後にジャックを置く場所。右の肘掛けの内の縁の後ろ寄り")]
        [SerializeField] Transform parked;
        [Tooltip("抜ける音。無くても動く")]
        [SerializeField] AudioSource source;
        [SerializeField] AudioClip unplug;

        [Header("右手（刺さっているところを見せる）")]
        [Tooltip("右の手首（手の骨）の行き先。体の根から見た位置")]
        [SerializeField] Vector3 lookWrist = new Vector3(0.10f, 0.95f, 0.35f);
        [Tooltip("そのときの右手の向き。体の根から見た向き")]
        [SerializeField] Quaternion lookHand = Quaternion.identity;
        [Tooltip("右肘を寄せる所。体の根から見た位置")]
        [SerializeField] Vector3 rightElbowPole = new Vector3(0.45f, 0.6f, 0f);

        [Header("左手（掴んで抜き、見せて、置く）")]
        [Tooltip("左肘を寄せる所。体の根から見た位置")]
        [SerializeField] Vector3 leftElbowPole = new Vector3(-0.45f, 0.6f, 0f);
        [Tooltip("抜く長さ。ジャックの向きへ m")]
        [SerializeField] float pullDistance = 0.06f;
        [Tooltip("抜いたジャックを見せる所。体の根から見た位置")]
        [SerializeField] Vector3 showAt = new Vector3(0.02f, 1.05f, 0.40f);
        [Tooltip("見せるときのジャックの向き。体の根から見た向き")]
        [SerializeField] Quaternion showRotation = Quaternion.identity;
        [Tooltip("掴む手の形。左手の指の骨の、親から見た向き。掴む所に着いてから寄せる")]
        [SerializeField] SeatedPose.Bone[] pinch = new SeatedPose.Bone[0];
        [Tooltip("掴む前に開いておく手の形（親指と人差し指の間を広く）。伸ばす間に寄せる")]
        [SerializeField] SeatedPose.Bone[] open = new SeatedPose.Bone[0];
        [Tooltip("掴む前に、開いた手をジャックの尻の側（軸の向き）へ浮かせておく距離（m）。そこから軸に沿って下ろしてジャックを指の間に入れ、指を閉じる")]
        [SerializeField] float approach = 0.06f;

        [Tooltip("置き場へ下ろす前に、いったん運ぶ高さ（置き場の真上 m）。上げたままの右腕の下を通す")]
        [SerializeField] float placeLift = 0.10f;
        [Tooltip("そのとき置き場より後ろへ寄せる距離（m）")]
        [SerializeField] float placeBack = 0f;

        [Header("上体（置き場へ手を届かせる）")]
        [Tooltip("置き場（右の肘掛けの内の縁）は左肩から遠いので、置く間は上体を少し寄せる。右へ倒す角・前へ倒す角・左肩を前へ出すひねり（度）")]
        [SerializeField] Vector3 placeLean = new Vector3(0f, 6f, 10f);

        Transform upperR, lowerR, handR, upperL, lowerL, handL;
        Transform[] pinchBones;
        Transform[] openBones;
        Transform restParent;
        Vector3 restPosition;
        Quaternion restRotation;
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
            Bind();
        }

        void OnDisable()
        {
            if (flow != null) flow.Examined -= OnExamined;
            Release();
        }

        /// <summary>骨を探し、刺さっているジャックの置き方を覚える。エディタで撮るときにも呼ぶ</summary>
        public void Bind()
        {
            var an = pose != null ? pose.Animator : null;
            if (an != null)
            {
                upperR = an.GetBoneTransform(HumanBodyBones.RightUpperArm);
                lowerR = an.GetBoneTransform(HumanBodyBones.RightLowerArm);
                handR = an.GetBoneTransform(HumanBodyBones.RightHand);
                upperL = an.GetBoneTransform(HumanBodyBones.LeftUpperArm);
                lowerL = an.GetBoneTransform(HumanBodyBones.LeftLowerArm);
                handL = an.GetBoneTransform(HumanBodyBones.LeftHand);
                pinchBones = new Transform[pinch.Length];
                for (var i = 0; i < pinch.Length; i++) pinchBones[i] = an.GetBoneTransform(pinch[i].bone);
                openBones = new Transform[open.Length];
                for (var i = 0; i < open.Length; i++) openBones[i] = an.GetBoneTransform(open[i].bone);
            }
            if (jack != null && restParent == null)
            {
                restParent = jack.parent;
                restPosition = jack.localPosition;
                restRotation = jack.localRotation;
            }
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
            if (PullTimeline.Held(elapsed) && !held) Grip();
            if (PullTimeline.Out(elapsed) && !sounded) Sound();
            if (PullTimeline.LetGo(elapsed) && !letGo) Park();
            if (!PullTimeline.Done(elapsed)) return;
            Pulled = true;
            Release();
        }

        void LateUpdate()
        {
            if (elapsed < 0f || Pulled) return;
            Apply(elapsed);
            Aim(elapsed);
        }

        /// <summary>t 秒目の腕の形を、座った形の上へ当てる。エディタで一こまずつ撮るときにも呼ぶ</summary>
        public void Apply(float t)
        {
            if (pose == null || handR == null || handL == null) return;
            var body = pose.transform;

            // 上体。置く間だけ右の肘掛けへ寄せる
            ArmReach.Lean(pose.Animator, body, placeLean.x, placeLean.y, placeLean.z, PullTimeline.Place(t));

            // 右手。手首を目の前へ出し、ジャックを目へ向ける
            ArmReach.Move(upperR, lowerR, handR, body.TransformPoint(lookWrist), body.rotation * lookHand,
                body.TransformPoint(rightElbowPole), PullTimeline.RightHand(t));

            // 左手
            // 離した後は、離した瞬間の所（離す直前の狙い）から座った形の手へ戻る。
            // 前のこまの手を覚えておくと、こまの間が開いたときに、離す所の手前から戻り始めてしまう
            Vector3 gp, hp;
            Quaternion gr, hr;
            Target(PullTimeline.LetGo(t) ? PullTimeline.LetGoAt - 1e-4f : t, out gp, out gr);
            ArmReach.HandFor(gp, gr, HoldLocal(), HoldRotation(), out hp, out hr);
            var w = PullTimeline.Reach(t);
            ArmReach.Move(upperL, lowerL, handL, hp, hr, body.TransformPoint(leftElbowPole), w);
            Shape(open, openBones, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.3f, 0.8f, w)));
            Shape(pinch, pinchBones, Closing(w));
        }

        /// <summary>指を閉じる強さ。手が掴む所に着いてから（伸ばす強さの終わりの 3 %）閉じる</summary>
        static float Closing(float reach)
        {
            return Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.97f, 1f, reach));
        }

        /// <summary>
        /// 掴む所の手前からの寄せ。伸ばす強さの 85〜97 % で、ジャックの尻の側（手のひらの側）へ浮かせておいた開いた手を、
        /// ジャックの軸に沿って掴む所まで下ろす。ジャックは開いた親指と人差し指の間へ入る。寄せ終わってから指を閉じる
        /// </summary>
        static Vector3 Approach(float reach, Quaternion rotation, float distance)
        {
            var k = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.85f, 0.97f, reach));
            return rotation * (Vector3.forward * (distance * k));
        }

        /// <summary>t 秒目に、ジャック（左手の置き所）が居るべき位置と向き</summary>
        void Target(float t, out Vector3 position, out Quaternion rotation)
        {
            // 刺さっている所。右手が動いても付いて行くよう、手首に刺さっていたときの置き方から毎こま求める
            var parent = restParent != null ? restParent : handR;
            position = parent.TransformPoint(restPosition);
            rotation = parent.rotation * restRotation;
            if (!held)
            {
                // 伸ばす終わりまでは、開いた手をジャックの尻の側へ浮かせておき、軸に沿って下ろしてから指を閉じる
                position += Approach(PullTimeline.Reach(t), rotation, approach);
                return;
            }
            // 抜く。ジャックの向き（手首から外へ）へ
            position += rotation * Vector3.forward * (pullDistance * PullTimeline.Lift(t));
            // 前へ出して見せる
            var body = pose.transform;
            var k = PullTimeline.Show(t);
            position = Vector3.Lerp(position, body.TransformPoint(showAt), k);
            rotation = Quaternion.Slerp(rotation, body.rotation * showRotation, k);
            // 置き場へ運ぶ。置き場の上（placeLift・placeBack）へ運んでから下ろす。右腕はまだ目の前に上げてあり、肘掛けは空いている
            if (parked == null) return;
            k = PullTimeline.Place(t);
            var above = parked.position + Vector3.up * placeLift - pose.transform.forward * placeBack;
            position = k < 0.5f
                ? Vector3.Lerp(position, above, Mathf.SmoothStep(0f, 1f, k * 2f))
                : Vector3.Lerp(above, parked.position, Mathf.SmoothStep(0f, 1f, k * 2f - 1f));
            rotation = Quaternion.Slerp(rotation, parked.rotation, k);
        }

        Vector3 HoldLocal()
        {
            return grip == null ? Vector3.zero : Quaternion.Inverse(handL.rotation) * (grip.position - handL.position);
        }

        Quaternion HoldRotation()
        {
            return grip == null ? Quaternion.identity : Quaternion.Inverse(handL.rotation) * grip.rotation;
        }

        /// <summary>左手の指を、shape の形へ w の割合だけ寄せる</summary>
        static void Shape(SeatedPose.Bone[] shape, Transform[] bones, float w)
        {
            if (w <= 0f || bones == null) return;
            for (var i = 0; i < shape.Length && i < bones.Length; i++)
            {
                var b = bones[i];
                if (b == null) continue;
                b.localRotation = Quaternion.Slerp(b.localRotation, shape[i].rotation, w);
            }
        }

        /// <summary>
        /// 視線をジャックへ寄せる。ジャックは掴めば左手について動くので、
        /// 追っているだけで抜ける瞬間も、前へ出したケーブルも、置くところも画面に入る
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
            if (jack == null || grip == null) return;
            jack.SetParent(grip, false);
            jack.localPosition = Vector3.zero;
            jack.localRotation = Quaternion.identity;
            var s = grip.lossyScale.x;
            jack.localScale = Vector3.one / (Mathf.Approximately(s, 0f) ? 1f : s);
        }

        /// <summary>抜けた音。引き抜く段の頭で鳴らす</summary>
        void Sound()
        {
            sounded = true;
            if (source == null || unplug == null) return;
            source.PlayOneShot(unplug);
        }

        /// <summary>置き場に重なったところで手を離す</summary>
        void Park()
        {
            letGo = true;
            if (jack == null || parked == null) return;
            jack.SetParent(parked, false);
            jack.localPosition = Vector3.zero;
            jack.localRotation = Quaternion.identity;
            var s = parked.lossyScale.x;
            jack.localScale = Vector3.one / (Mathf.Approximately(s, 0f) ? 1f : s);
        }

        /// <summary>見回しを返す。途中で切られても視線が固まったままにならないよう、ここへ集めてある</summary>
        void Release()
        {
            if (!tookLook) return;
            tookLook = false;
            var player = flow != null ? flow.Player : null;
            if (player != null) player.CanLook = true;
        }

        /// <summary>エディタで一こまずつ撮るための、掴む・置くの切り替え。再生中は使わない</summary>
        public void StepForStudy(float t)
        {
            if (PullTimeline.Held(t) && !held) Grip();
            if (PullTimeline.LetGo(t) && !letGo) Park();
        }
    }
}
