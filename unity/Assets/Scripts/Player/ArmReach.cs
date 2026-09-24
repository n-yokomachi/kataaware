using UnityEngine;

namespace HalfAware
{
    /// <summary>
    /// 二つの関節の腕（または脚）を、先の骨が狙った所へ届くように曲げる。
    /// 肩（付け根）は動かさず、上腕と前腕の向きだけを決める。肘の出る向きは pole（肘を寄せたい所）で決める。
    ///
    /// 骨の向きは、今の向きから最小の回しで寄せる（FromToRotation）ので、腕のひねりは今の形のまま残る。
    /// 届かない所を狙うと、腕を伸ばしきって狙いの方へ向けたところで止まる
    /// </summary>
    public static class ArmReach
    {
        /// <summary>伸ばしきらない余り。まっすぐ伸びきると肘の向きが決まらず、こまごとに跳ねる</summary>
        public const float Slack = 0.999f;

        static readonly HumanBodyBones[] Spine = { HumanBodyBones.Spine, HumanBodyBones.Chest, HumanBodyBones.UpperChest };
        static readonly float[] SpineShare = { 0.4f, 0.3f, 0.3f };

        /// <summary>
        /// 肘の位置。肩 a から狙い target へ、上腕の長さ upper・前腕の長さ lower で届くとき、
        /// 肘は a と target を結ぶ線から pole の側へ出る
        /// </summary>
        public static Vector3 Elbow(Vector3 a, float upper, float lower, Vector3 target, Vector3 pole)
        {
            var to = target - a;
            var reach = to.magnitude;
            if (reach < 1e-6f) return a + Vector3.down * upper;
            var dir = to / reach;
            var d = Mathf.Clamp(reach, Mathf.Abs(upper - lower) + 1e-4f, (upper + lower) * Slack);
            // 肩から肘への線が、肩から狙いへの線となす角の余弦（余弦定理）
            var cos = Mathf.Clamp((upper * upper + d * d - lower * lower) / (2f * upper * d), -1f, 1f);
            var sin = Mathf.Sqrt(1f - cos * cos);
            var side = pole - a;
            side -= dir * Vector3.Dot(side, dir);
            if (side.sqrMagnitude < 1e-8f)
            {
                // pole が線の上にあるときは、線に直交する向きをひとつ選ぶ
                side = Vector3.Cross(dir, Mathf.Abs(dir.y) < 0.9f ? Vector3.up : Vector3.right);
            }
            side.Normalize();
            return a + dir * (upper * cos) + side * (upper * sin);
        }

        /// <summary>
        /// 上腕 upperBone・前腕 lowerBone・手 end を、手が target へ届くように曲げる。weight 0 で何もしない、1 で届かせる。
        /// endRotation を渡せば手の向きも寄せる（null なら手は前腕について回るだけ）
        /// </summary>
        public static void Solve(Transform upperBone, Transform lowerBone, Transform end, Vector3 target, Vector3 pole,
            float weight, Quaternion? endRotation = null)
        {
            if (upperBone == null || lowerBone == null || end == null || weight <= 0f) return;
            weight = Mathf.Min(1f, weight);
            var a = upperBone.position;
            var b = lowerBone.position;
            var c = end.position;
            var upper = Vector3.Distance(a, b);
            var lower = Vector3.Distance(b, c);
            var keepUpper = upperBone.rotation;
            var keepLower = lowerBone.rotation;

            var elbow = Elbow(a, upper, lower, target, pole);
            upperBone.rotation = Quaternion.FromToRotation(b - a, elbow - a) * keepUpper;
            // 上腕を回したので、前腕と手の位置が動いている。回した後の位置から前腕を寄せる
            b = lowerBone.position;
            c = end.position;
            lowerBone.rotation = Quaternion.FromToRotation(c - b, target - b) * lowerBone.rotation;

            if (weight < 1f)
            {
                upperBone.rotation = Quaternion.Slerp(keepUpper, upperBone.rotation, weight);
                lowerBone.rotation = Quaternion.Slerp(keepLower, lowerBone.rotation, weight);
            }
            // 手は前腕について回った向きから、狙いの向きへ寄せる
            if (endRotation.HasValue) end.rotation = Quaternion.Slerp(end.rotation, endRotation.Value, weight);
        }

        /// <summary>
        /// 手を、今の所（座った形の手）から狙い（target・endRotation）へ、weight の割合だけ寄せた所へ届かせる。
        /// 骨の向きを混ぜる <see cref="Solve"/> と違い、手は今の所と狙いを結ぶまっすぐな道を通る
        /// （向きを混ぜると、手は肩のまわりの弧を通り、途中で物を横切ることがある）
        /// </summary>
        public static void Move(Transform upperBone, Transform lowerBone, Transform end, Vector3 target, Quaternion endRotation, Vector3 pole, float weight)
        {
            if (upperBone == null || lowerBone == null || end == null || weight <= 0f) return;
            weight = Mathf.Min(1f, weight);
            if (weight >= 1f)
            {
                Solve(upperBone, lowerBone, end, target, pole, 1f, endRotation);
                return;
            }
            // 手の向きは、世界の向きではなく手首の角（前腕から見た手の向き）で混ぜる。世界の向きで混ぜると、
            // 腕が体の前を回り込む途中で手首が 70〜90 度反った。手首の角で混ぜれば、途中の曲げは両端の曲げの間に収まる。
            // 前腕のひねり（肘の所）も両端の間を混ぜる。最小の回しで寄せるだけだと、腕を回す途中でひねりがずれていく
            var keepUpper = upperBone.rotation;
            var keepLower = lowerBone.rotation;
            var keepEnd = end.rotation;
            var startLower = lowerBone.localRotation;
            var startWrist = end.localRotation;
            var from = end.position;
            Solve(upperBone, lowerBone, end, target, pole, 1f, endRotation);
            var endWrist = end.localRotation;
            var endRoll = RollFrom(startLower, lowerBone, end);
            upperBone.rotation = keepUpper;
            lowerBone.rotation = keepLower;
            end.rotation = keepEnd;
            Solve(upperBone, lowerBone, end, Vector3.Lerp(from, target, weight), pole, 1f);
            Roll(lowerBone, end, endRoll * weight - RollFrom(startLower, lowerBone, end));
            end.localRotation = Quaternion.Slerp(startWrist, endWrist, weight);
        }

        /// <summary>前腕の骨の、start（親から見た向き）からの自分の軸まわりのひねり。度</summary>
        static float RollFrom(Quaternion start, Transform lower, Transform end)
        {
            return TwistAngle(Quaternion.Inverse(start) * lower.localRotation, end.localPosition);
        }

        /// <summary>前腕の骨を自分の軸（肘から手首への線）まわりに degrees だけ回す。肘と手首の位置は動かない</summary>
        static void Roll(Transform lower, Transform end, float degrees)
        {
            if (Mathf.Abs(degrees) < 1e-3f) return;
            lower.rotation = Quaternion.AngleAxis(degrees, (end.position - lower.position).normalized) * lower.rotation;
        }

        /// <summary>
        /// 背を傾ける。背骨・胸・上の胸へ 4:3:3 に分けて、体の根から見た軸で回す。
        /// right は右へ倒す角、forward は前へ倒す角、twist は左の肩を前へ出す向きのひねり（どれも度）。
        /// 手が肩から遠い所へ届かないとき、腕を伸ばす前に上体ごと寄せるのに使う
        /// </summary>
        public static void Lean(Animator an, Transform body, float right, float forward, float twist, float weight)
        {
            if (an == null || body == null || weight <= 0f) return;
            for (var i = 0; i < Spine.Length; i++)
            {
                var t = an.GetBoneTransform(Spine[i]);
                if (t == null) continue;
                var k = weight * SpineShare[i];
                t.rotation = Quaternion.AngleAxis(forward * k, body.right)
                    * Quaternion.AngleAxis(-right * k, body.forward)
                    * Quaternion.AngleAxis(twist * k, body.up)
                    * t.rotation;
            }
        }

        // ---- 前腕のひねり ------------------------------------------------------------
        //
        // **ひねりの骨が無い体では、手の骨だけをひねると手首の肌が絞られて細く潰れる**（包み紙の潰れ）。
        // 手首の肌は前腕と手の骨に半分ずつ付いているので、手を前腕に対して 90 度ひねると、
        // 手首の頂点は 45 度ずつ引っ張られて断面が潰れる。人の腕では、手のひらの向き（回内・回外）は
        // 前腕の二本の骨が肘から手首まで少しずつ回して作るので、手首だけがねじれることはない。
        //
        // そこで、手の向きを決めた後に、手のひらのひねりの一部を前腕の骨そのもの（肘の所）へ移す。
        // 手の世界の向きは変えないので、手の置き所もジャックの向きもそのまま

        /// <summary>
        /// 手のひらのひねりのうち、前腕の骨へ移す割合。半分ずつ持たせると、肘の側と手首の側の潰れがどちらも
        /// 立った形の 8 割 5 分より細くならない（場面 1 と 3 の座った形と、抜く・挿すしぐさで測った）
        /// </summary>
        public const float TwistShare = 0.5f;

        /// <summary>前腕と手の、束ねた姿勢（体の模型を組んだときの姿勢）での親から見た向き。ひねりを測る基</summary>
        public struct Rest
        {
            public Quaternion lower;
            public Quaternion hand;
            public bool valid;
        }

        /// <summary>
        /// 束ねた姿勢の向きを、体のメッシュの bindposes から読む。
        /// 束ねた姿勢は腕を横へ伸ばし手のひらを下へ向けた形で、腕を下ろせば手のひらは腿を向く（ひねりの無い真ん中）
        /// </summary>
        public static Rest RestOf(SkinnedMeshRenderer smr, Transform upper, Transform lower, Transform hand)
        {
            var rest = new Rest();
            if (smr == null || smr.sharedMesh == null || upper == null || lower == null || hand == null) return rest;
            var bones = smr.bones;
            var binds = smr.sharedMesh.bindposes;
            int iu = -1, il = -1, ih = -1;
            for (var i = 0; i < bones.Length && i < binds.Length; i++)
            {
                if (bones[i] == upper) iu = i;
                else if (bones[i] == lower) il = i;
                else if (bones[i] == hand) ih = i;
            }
            if (iu < 0 || il < 0 || ih < 0) return rest;
            var ru = binds[iu].inverse.rotation;
            var rl = binds[il].inverse.rotation;
            var rh = binds[ih].inverse.rotation;
            rest.lower = Quaternion.Inverse(ru) * rl;
            rest.hand = Quaternion.Inverse(rl) * rh;
            rest.valid = true;
            return rest;
        }

        /// <summary>q のうち、axis まわりの回し（ひねり）の角。度。-180〜180</summary>
        public static float TwistAngle(Quaternion q, Vector3 axis)
        {
            axis.Normalize();
            var v = new Vector3(q.x, q.y, q.z);
            var p = Vector3.Dot(v, axis);
            var angle = 2f * Mathf.Atan2(p, q.w) * Mathf.Rad2Deg;
            return Mathf.DeltaAngle(0f, angle);
        }

        /// <summary>
        /// 前腕と手のひねり（束ねた姿勢から、前腕の軸まわり）。度。
        /// forearm は前腕の骨が上腕に対してひねった角、hand は手が前腕に対してひねった角。二つの和が手のひらの向きのひねり
        /// </summary>
        public static void Twists(Transform lower, Transform hand, Rest rest, out float forearm, out float wrist)
        {
            forearm = 0f;
            wrist = 0f;
            if (!rest.valid || lower == null || hand == null) return;
            var axis = hand.localPosition;
            if (axis.sqrMagnitude < 1e-10f) return;
            // 前腕: 上腕の枠で、束ねた向きから今の向きへの回しを、束ねた姿勢の前腕の軸まわりで測る
            var dl = Quaternion.Inverse(rest.lower) * lower.localRotation;
            forearm = TwistAngle(dl, axis);
            // 手: 前腕の枠で、束ねた向きから今の向きへの回しを、前腕の軸まわりで測る
            var dh = hand.localRotation * Quaternion.Inverse(rest.hand);
            wrist = TwistAngle(dh, axis);
        }

        /// <summary>
        /// 手のひねりの share の割合を、前腕の骨へ移す（手の世界の向きと位置は変えない）。
        /// 前腕の骨を自分の軸（肘から手首への線）まわりに回すので、肘と手首の位置も動かない
        /// </summary>
        public static void Untwist(Transform lower, Transform hand, Rest rest, float share)
        {
            if (!rest.valid || lower == null || hand == null || share <= 0f) return;
            float forearm, wrist;
            Twists(lower, hand, rest, out forearm, out wrist);
            var total = Mathf.DeltaAngle(0f, forearm + wrist);
            var move = total * share - forearm;
            if (Mathf.Abs(move) < 1e-3f) return;
            var keep = hand.rotation;
            var axis = (hand.position - lower.position).normalized;
            lower.rotation = Quaternion.AngleAxis(move, axis) * lower.rotation;
            hand.rotation = keep;
        }

        /// <summary>
        /// 物を持った手を、二つの置き方（A と B。手の骨の位置と向き）の間を運ぶ途中の所 position へ届かせる。
        /// 手の向きは、両端での手首の角（前腕から見た手の向き）を k で混ぜる。
        ///
        /// **運ぶ途中の手の向きを、世界の向きで混ぜない。** 両端の向きだけを世界の向きで混ぜると、腕が体の前を
        /// 回り込む途中で手首が 100 度を越えて反り、手のひらのひねりも 180 度近くになって腕が極端に細く見えた。
        /// 手首の角と前腕のひねりで混ぜれば、途中の曲げとひねりは両端の間に収まる。持っている物は手について回る
        /// </summary>
        public static void Carry(Transform upperBone, Transform lowerBone, Transform end, Vector3 position,
            Vector3 positionA, Quaternion rotationA, Vector3 positionB, Quaternion rotationB, Vector3 pole, float k)
        {
            if (upperBone == null || lowerBone == null || end == null) return;
            var keepUpper = upperBone.rotation;
            var keepLower = lowerBone.rotation;
            var keepEnd = end.rotation;
            var start = lowerBone.localRotation;
            k = Mathf.Clamp01(k);
            Solve(upperBone, lowerBone, end, positionA, pole, 1f, rotationA);
            var wristA = end.localRotation;
            var rollA = RollFrom(start, lowerBone, end);
            upperBone.rotation = keepUpper;
            lowerBone.rotation = keepLower;
            end.rotation = keepEnd;
            Solve(upperBone, lowerBone, end, positionB, pole, 1f, rotationB);
            var wristB = end.localRotation;
            var rollB = RollFrom(start, lowerBone, end);
            upperBone.rotation = keepUpper;
            lowerBone.rotation = keepLower;
            end.rotation = keepEnd;
            Solve(upperBone, lowerBone, end, position, pole, 1f);
            // 前腕のひねりと手首の角を、両端の間で混ぜる
            Roll(lowerBone, end, Mathf.LerpAngle(rollA, rollB, k) - RollFrom(start, lowerBone, end));
            end.localRotation = Quaternion.Slerp(wristA, wristB, k);
        }

        /// <summary>
        /// 手首の曲げ（ひねりを除いた、手が前腕に対して倒れた角）。度。束ねた姿勢からの角
        /// </summary>
        public static float WristBend(Transform lower, Transform hand, Rest rest)
        {
            if (!rest.valid || lower == null || hand == null) return 0f;
            var axis = hand.localPosition.normalized;
            var dh = hand.localRotation * Quaternion.Inverse(rest.hand);
            // 前腕の軸が手の中でどこへ倒れたか（ひねりはこの角を変えない）
            return Vector3.Angle(axis, dh * axis);
        }

        /// <summary>
        /// 手の中の置き所（手の骨の向きの枠で見た位置 holdLocal・向き holdRotation。縮尺は掛けない m）を、
        /// 世界の worldPosition・worldRotation に重ねるための、手の骨の位置と向き
        /// </summary>
        public static void HandFor(Vector3 worldPosition, Quaternion worldRotation, Vector3 holdLocal, Quaternion holdRotation,
            out Vector3 handPosition, out Quaternion handRotation)
        {
            handRotation = worldRotation * Quaternion.Inverse(holdRotation);
            handPosition = worldPosition - handRotation * holdLocal;
        }
    }
}
