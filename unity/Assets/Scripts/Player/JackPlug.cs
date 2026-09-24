using UnityEngine;

namespace HalfAware
{
    /// <summary>
    /// 肘掛けに置いたジャックを左手で取り、右手首へ挿す。座った形（<see cref="SeatedPose"/>）の上で、
    /// 腕を二関節の IK（<see cref="ArmReach"/>）で動かす。
    /// まず右の手首を持ち上げて差込口を出し（肘掛けを空ける）、左手で置き場のジャックを掴み、置き場の上へ持ち上げてから
    /// 差込口の前へ運んで挿す。視線は差込口を追うので、挿さる瞬間は画面に入る。置き場（目より後ろ）は見ない。
    /// 挿している間は調べる操作も見回しも止める。
    ///
    /// <see cref="JackPull"/>（場面 1 で抜く側）の裏返し。掴む瞬間と挿さる瞬間は、置き所と行き先が重なってから
    /// ジャックを移すので跳ねない
    /// </summary>
    [DefaultExecutionOrder(25)]
    public sealed class JackPlug : MonoBehaviour
    {
        [SerializeField] SceneFlow flow;
        [Tooltip("座った形。これが骨を当てた後に腕を曲げ直す")]
        [SerializeField] SeatedPose pose;
        [Tooltip("この id を調べたら挿し始める")]
        [SerializeField] string id = ConnectIds.Jack;
        [Tooltip("置いてあるジャック（右の肘掛けの内の縁の後ろ寄り）")]
        [SerializeField] Transform jack;
        [Tooltip("掴んでいる間ジャックを預ける置き所。左手の JackHold")]
        [SerializeField] Transform grip;
        [Tooltip("挿さったジャックの行き先。右の手首の JackSocket")]
        [SerializeField] Transform socket;
        [Tooltip("挿さる音。無くても動く")]
        [SerializeField] AudioSource source;
        [SerializeField] AudioClip plug;

        [Header("右手（差込口を出す）")]
        [Tooltip("右の手首（手の骨）の行き先。体の根から見た位置")]
        [SerializeField] Vector3 liftWrist = new Vector3(0.10f, 0.95f, 0.35f);
        [Tooltip("そのときの右手の向き。体の根から見た向き")]
        [SerializeField] Quaternion liftHand = Quaternion.identity;
        [Tooltip("右肘を寄せる所。体の根から見た位置")]
        [SerializeField] Vector3 rightElbowPole = new Vector3(0.45f, 0.6f, 0f);

        [Header("左手（取って、運んで、挿す）")]
        [Tooltip("左肘を寄せる所。体の根から見た位置")]
        [SerializeField] Vector3 leftElbowPole = new Vector3(-0.45f, 0.6f, 0f);
        [Tooltip("挿す前に差込口の手前で止める距離。ジャックの向きへ m")]
        [SerializeField] float pushDistance = 0.06f;
        [Tooltip("掴む手の形。左手の指の骨の、親から見た向き。掴む所に着いてから寄せる")]
        [SerializeField] SeatedPose.Bone[] pinch = new SeatedPose.Bone[0];
        [Tooltip("掴む前に開いておく手の形（親指と人差し指の間を広く）。伸ばす間に寄せる")]
        [SerializeField] SeatedPose.Bone[] open = new SeatedPose.Bone[0];
        [Tooltip("掴む前に、開いた手をジャックの尻の側（軸の向き）へ浮かせておく距離（m）。そこから軸に沿って下ろしてジャックを指の間に入れ、指を閉じる")]
        [SerializeField] float approach = 0.06f;
        [Tooltip("掴む手を、ジャックの軸まわりに回しておく角（度）。ジャックは丸いので、どの角で掴んでも見た目は変わらない。" +
            "左腕のひねりと手首の曲げが人の腕の範囲に収まる角を、組み立てが流れを通して試して選ぶ")]
        [SerializeField] float holdRoll;
        [Tooltip("差込口での、掴む手の軸まわりの角（度）。差込口へ運ぶ間に holdRoll から移す。" +
            "指の間でジャックを転がすのと同じで、ジャックは丸いので見た目は変わらない")]
        [SerializeField] float seatRoll;

        [Header("腕のひねり")]
        [Tooltip("手のひらのひねりのうち、前腕の骨（肘の所）へ移す割合。残りは手首に残る。" +
            "この体には前腕のひねりの骨が無いので、手の骨だけをひねると手首の肌が絞られて細く潰れる。半分ずつ持たせると、肘と手首の潰れがどちらも目に付かない")]
        [Range(0f, 1f)]
        [SerializeField] float twistShare = ArmReach.TwistShare;

        [Tooltip("運ぶ途中に通す所。置き場から見た、体の根の向きの枠でのずれ（m）。置き場の上へ持ち上げてから差込口へ回す。" +
            "置き場に寝かせたジャックを取るようにしてからは、前の値（上へ 18 cm・前へ 6 cm）ではジャックの尻が胸に掠ったので、低く前へ寄せた")]
        [SerializeField] Vector3 carryVia = new Vector3(-0.10f, 0.11f, 0.13f);

        [Tooltip("挿し終えて離した指を、ジャックの尻の側へ抜く距離（m）")]
        [SerializeField] float releaseLift = 0.07f;

        [Header("上体（置き場へ手を届かせる）")]
        [Tooltip("置き場（右の肘掛けの内の縁）は左肩から遠いので、取る間は上体を少し寄せる。右へ倒す角・前へ倒す角・左肩を前へ出すひねり（度）")]
        [SerializeField] Vector3 reachLean = new Vector3(0f, 6f, 10f);

        Transform upperR, lowerR, handR, upperL, lowerL, handL;
        ArmReach.Rest restR, restL;
        Transform startParent;
        Vector3 startPosition;
        Quaternion startRotation;
        Vector3 startScale = Vector3.one;
        Transform[] pinchBones;
        Transform[] openBones;
        Vector3 picked;
        Quaternion pickedRotation;
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
            Bind();
        }

        void OnDisable()
        {
            if (flow != null) flow.Examined -= OnExamined;
            Release();
        }

        /// <summary>骨を探す。エディタで撮るときにも呼ぶ</summary>
        public void Bind()
        {
            var an = pose != null ? pose.Animator : null;
            if (an == null) return;
            upperR = an.GetBoneTransform(HumanBodyBones.RightUpperArm);
            lowerR = an.GetBoneTransform(HumanBodyBones.RightLowerArm);
            handR = an.GetBoneTransform(HumanBodyBones.RightHand);
            upperL = an.GetBoneTransform(HumanBodyBones.LeftUpperArm);
            lowerL = an.GetBoneTransform(HumanBodyBones.LeftLowerArm);
            handL = an.GetBoneTransform(HumanBodyBones.LeftHand);
            var skin = an.GetComponentInChildren<SkinnedMeshRenderer>();
            restR = ArmReach.RestOf(skin, upperR, lowerR, handR);
            restL = ArmReach.RestOf(skin, upperL, lowerL, handL);
            pinchBones = new Transform[pinch.Length];
            for (var i = 0; i < pinch.Length; i++) pinchBones[i] = an.GetBoneTransform(pinch[i].bone);
            openBones = new Transform[open.Length];
            for (var i = 0; i < open.Length; i++) openBones[i] = an.GetBoneTransform(open[i].bone);
            if (jack != null && startParent == null)
            {
                startParent = jack.parent;
                startPosition = jack.localPosition;
                startRotation = jack.localRotation;
                startScale = jack.localScale;
            }
        }

        /// <summary>エディタで流れを頭からやり直すための戻し。ジャックを置き場へ戻し、掴む・挿さるの印を消す。再生中は使わない</summary>
        public void ResetForStudy()
        {
            held = false;
            seated = false;
            sounded = false;
            if (jack == null || startParent == null) return;
            jack.SetParent(startParent, false);
            jack.localPosition = startPosition;
            jack.localRotation = startRotation;
            jack.localScale = startScale;
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
            Step(elapsed);
            if (PlugTimeline.In(elapsed) && !sounded) Sound();
            if (!PlugTimeline.Done(elapsed)) return;
            Done = true;
            Release();
        }

        void LateUpdate()
        {
            if (elapsed < 0f || Done) return;
            Apply(elapsed);
            Aim(elapsed);
        }

        /// <summary>掴む・挿さるの切り替え。エディタで一こまずつ撮るときにも呼ぶ</summary>
        public void Step(float t)
        {
            if (PlugTimeline.Held(t) && !held) Grip();
            if (PlugTimeline.In(t) && !seated) Seat();
        }

        /// <summary>t 秒目の腕の形を、座った形の上へ当てる。エディタで一こまずつ撮るときにも呼ぶ</summary>
        public void Apply(float t)
        {
            if (pose == null || handR == null || handL == null) return;
            var body = pose.transform;

            // 上体。取る間だけ右の肘掛けへ寄せ、運ぶ間に起こす
            ArmReach.Lean(pose.Animator, body, reachLean.x, reachLean.y, reachLean.z, Lean(t));

            // 右手。差込口を出す。視線を落とす間に持ち上げ（肘掛けを空けて、左手が取りに行けるように）、手を戻すときに下ろす
            ArmReach.Move(upperR, lowerR, handR, body.TransformPoint(liftWrist), body.rotation * liftHand,
                body.TransformPoint(rightElbowPole), PlugTimeline.RightHand(t));

            // 左手
            // 離した後は、離した瞬間の所（離す直前の狙い）から座った形の手へ戻る。
            // 前のこまの手を覚えておくと、こまの間が開いたときに、離す所の手前から戻り始めてしまう
            Vector3 gp, hp;
            Quaternion gr, hr;
            Target(PlugTimeline.LetGo(t) ? PlugTimeline.LetGoAt - 1e-4f : t, out gp, out gr);
            // 離した後は、まず開いた指をジャックの尻の側へ抜いてから戻る。そのまま戻すと、開く指が挿さったジャックを横切った
            if (PlugTimeline.LetGo(t)) gp += gr * Vector3.forward * (releaseLift * PlugTimeline.Release(t));
            ArmReach.HandFor(gp, gr * Roll(PlugTimeline.LetGo(t) ? PlugTimeline.LetGoAt - 1e-4f : t), HoldLocal(), HoldRotation(), out hp, out hr);
            var w = PlugTimeline.Reach(t);
            var leftPole = body.TransformPoint(leftElbowPole);
            float k;
            Vector3 ap, bp;
            Quaternion ar, br;
            if (Carrying(t, out k, out ap, out ar, out bp, out br))
                ArmReach.Carry(upperL, lowerL, handL, hp, ap, ar, bp, br, leftPole, k);
            else
                ArmReach.Move(upperL, lowerL, handL, hp, hr, leftPole, w);
            Shape(open, openBones, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.3f, 0.8f, w)));
            Shape(pinch, pinchBones, Closing(w));

            // 手のひらのひねりを前腕と手首に分ける（手の向きと位置は変えない）
            ArmReach.Untwist(lowerR, handR, restR, twistShare);
            ArmReach.Untwist(lowerL, handL, restL, twistShare);
        }

        /// <summary>
        /// 取ったジャックを差込口の手前へ運んでいる途中か。運んでいる間は、両端の手の置き方（取った所と差込口の手前）と、運んだ割合 k を返す
        /// </summary>
        bool Carrying(float t, out float k, out Vector3 positionA, out Quaternion rotationA, out Vector3 positionB, out Quaternion rotationB)
        {
            k = PlugTimeline.Carry(t);
            positionA = positionB = Vector3.zero;
            rotationA = rotationB = Quaternion.identity;
            if (!held || socket == null || PlugTimeline.LetGo(t) || k <= 0f || k >= 1f) return false;
            Hand(picked, pickedRotation, holdRoll, out positionA, out rotationA);
            Hand(socket.position + socket.rotation * Vector3.forward * pushDistance, socket.rotation, seatRoll, out positionB, out rotationB);
            return true;
        }

        /// <summary>ジャックを position・rotation に、軸まわりに roll 度回して掴んでいる手の骨の位置と向き</summary>
        void Hand(Vector3 position, Quaternion rotation, float roll, out Vector3 handPosition, out Quaternion handRotation)
        {
            ArmReach.HandFor(position, rotation * Quaternion.AngleAxis(roll, Vector3.forward), HoldLocal(), HoldRotation(), out handPosition, out handRotation);
        }

        /// <summary>t 秒目に、掴む手をジャックの軸まわりに回しておく回し。取る所の角から、運ぶ間に差込口の角へ移す</summary>
        Quaternion Roll(float t)
        {
            return Quaternion.AngleAxis(Mathf.LerpAngle(holdRoll, seatRoll, PlugTimeline.Carry(t)), Vector3.forward);
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

        /// <summary>上体を寄せる強さ。伸ばす間に上がり、運ぶ間に下がる。手を戻すときには寄せない</summary>
        static float Lean(float t)
        {
            if (PlugTimeline.LetGo(t)) return 0f;
            return t < PlugTimeline.CarryAt ? PlugTimeline.Reach(t) : 1f - PlugTimeline.Carry(t);
        }

        /// <summary>t 秒目に、ジャック（左手の置き所）が居るべき位置と向き</summary>
        void Target(float t, out Vector3 position, out Quaternion rotation)
        {
            if (!held)
            {
                // 置いてある所。掴むまでは毎こま今の位置から。伸ばす終わりまでは、開いた手をジャックの尻の側へ浮かせておき、軸に沿って下ろしてから指を閉じる
                position = jack.position;
                rotation = jack.rotation;
                position += Approach(PlugTimeline.Reach(t), rotation, approach);
                return;
            }
            position = picked;
            rotation = pickedRotation;
            if (socket == null) return;
            // 置き場から、上の通り道（carryVia）を通して差込口の手前へ運ぶ。差込口は右手について動くので毎こま求める。
            // 置き場から差込口へまっすぐ運ぶと、上げてある右の上腕を突き抜けた。肘の下をくぐらせると、左手が右の前腕に当たった
            var above = picked + pose.transform.rotation * carryVia;
            var front = socket.position + socket.rotation * Vector3.forward * pushDistance;
            var k = PlugTimeline.Carry(t);
            position = k < 0.5f
                ? Vector3.Lerp(picked, above, Mathf.SmoothStep(0f, 1f, k * 2f))
                : Vector3.Lerp(above, front, Mathf.SmoothStep(0f, 1f, k * 2f - 1f));
            rotation = Quaternion.Slerp(rotation, socket.rotation, k);
            // 挿し込む
            k = PlugTimeline.Push(t);
            position = Vector3.Lerp(position, socket.position, k);
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
        /// 視線を右の手首の差込口へ寄せる。差込口は右手について上がり、挿さる瞬間も画面に入る。
        /// 置いてあるジャックは追わない。置き場（右の肘掛けの後ろ寄り）は目より後ろにあり、そこを見ると、
        /// 首より上を映さない体の襟ぐりの中が見える。左手は画面の外で取って、差込口の前へ運んでくる
        /// </summary>
        void Aim(float t)
        {
            var player = flow != null ? flow.Player : null;
            if (player == null || player.Eye == null) return;
            var follow = socket != null ? socket : jack;
            if (PlugTimeline.Follows(t) && follow != null)
            {
                var to = follow.position - player.Eye.position;
                if (to.sqrMagnitude > 1e-6f)
                {
                    to.Normalize();
                    aimYaw = Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg;
                    // 下は向けられる限り（PlayerController.PitchDownLimit）まで。越える所は画面の下へ外れる
                    aimPitch = PlayerController.ClampPitch(-Mathf.Asin(Mathf.Clamp(to.y, -1f, 1f)) * Mathf.Rad2Deg);
                    aimed = true;
                }
            }
            if (!aimed) return;
            // **手を戻しても視線は戻さない。** 挿したところを見たまま置く。
            // 元の向きへ引き戻すと、挿し終わりに首だけが勝手に振れる
            var k = PlugTimeline.Aim(t);
            player.Yaw = Mathf.LerpAngle(fromYaw, aimYaw, k);
            player.Pitch = Mathf.Lerp(fromPitch, aimPitch, k);
        }

        /// <summary>掴んだ。ここからジャックは左手について動く。取った所を覚えて、そこから運ぶ</summary>
        void Grip()
        {
            held = true;
            if (jack == null || grip == null) return;
            picked = jack.position;
            pickedRotation = jack.rotation;
            jack.SetParent(grip, false);
            jack.localPosition = Vector3.zero;
            // 手は軸まわりに holdRoll だけ回して掴んでいるので、ジャックはその分を戻して置く（掴む瞬間に回らない）
            jack.localRotation = Quaternion.Inverse(Quaternion.AngleAxis(holdRoll, Vector3.forward));
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
