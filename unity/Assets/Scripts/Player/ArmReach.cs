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
            var p = Vector3.Lerp(end.position, target, weight);
            var r = Quaternion.Slerp(end.rotation, endRotation, weight);
            Solve(upperBone, lowerBone, end, p, pole, 1f, r);
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
