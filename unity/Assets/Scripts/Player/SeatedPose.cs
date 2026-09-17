using System;
using System.Collections.Generic;
using UnityEngine;

namespace HalfAware
{
    /// <summary>
    /// 座位の姿勢。同梱の動作に座りが無いので、骨を直接曲げて作る。
    /// 角度は骨それぞれの軸ではなく、体から見た軸（右・上・前）で指定する。
    /// 取り込んだ骨組みは足首が脚の子ではなく根の直下にあるため、
    /// そのままでは脚を曲げても靴が置き去りになる。始めに繋ぎ直して解く
    /// </summary>
    [DefaultExecutionOrder(20)]
    public sealed class SeatedPose : MonoBehaviour
    {
        /// <summary>体から見た回転軸</summary>
        public enum Axis
        {
            /// <summary>右。前後に振る</summary>
            Right,
            /// <summary>上。内外に捻る</summary>
            Up,
            /// <summary>前。左右に開く</summary>
            Forward,
        }

        [Serializable]
        public struct BoneTurn
        {
            public string bone;
            public Axis axis;
            [Tooltip("度。安静の姿勢からの差")]
            public float degrees;
        }

        [Tooltip("体の根。骨はこの下から名前で探す")]
        [SerializeField] Transform body;
        [Tooltip("足首の骨。脛の子へ繋ぎ直す")]
        [SerializeField] string[] refootBones = { "Foot.L", "Foot.R" };
        [Tooltip("繋ぎ先の脛")]
        [SerializeField] string[] shinBones = { "LowerLeg.L", "LowerLeg.R" };
        [SerializeField] BoneTurn[] turns;
        [Tooltip("座ると腰が座面まで下がる。その沈み込み。体の座標で指定する")]
        [SerializeField] Vector3 seatedOffset = new Vector3(0f, -0.51f, 0f);
        [Tooltip("立っている間だけ動かす。座っている間は骨をこちらが握るので止める")]
        [SerializeField] Animator animator;

        /// <summary>繋ぎ直す前の親と、その下での置き方。立つときに戻す</summary>
        struct Hung
        {
            public Transform child;
            public Transform parent;
            public Vector3 position;
            public Quaternion rotation;
        }

        Vector3 standingPosition;

        readonly Dictionary<string, Transform> bones = new Dictionary<string, Transform>();
        readonly Dictionary<Transform, Quaternion> rest = new Dictionary<Transform, Quaternion>();
        readonly List<Hung> refooted = new List<Hung>();
        readonly HashSet<Transform> cleared = new HashSet<Transform>();
        bool ready;
        bool handedOver;

        /// <summary>true の間だけ座位の姿勢を当てる</summary>
        public bool Seated { get; set; } = true;

        /// <summary>座位の上に重ねる曲げ。場面の演出が差し込む。null なら何も重ねない</summary>
        public BoneTurn[] Extra { get; set; }

        /// <summary>重ねる曲げの強さ。0 で無し、1 で指定のまま</summary>
        public float ExtraWeight { get; set; }

        void Awake()
        {
            var root = body != null ? body : transform;
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                if (!bones.ContainsKey(t.name)) bones.Add(t.name, t);
            }
            // 足首を脛の下へ。世界の位置は保つので見た目は変わらない
            var n = Mathf.Min(refootBones.Length, shinBones.Length);
            for (var i = 0; i < n; i++)
            {
                Transform foot, shin;
                if (!bones.TryGetValue(refootBones[i], out foot)) continue;
                if (!bones.TryGetValue(shinBones[i], out shin)) continue;
                if (foot.parent == shin) continue;
                refooted.Add(new Hung
                {
                    child = foot,
                    parent = foot.parent,
                    position = foot.localPosition,
                    rotation = foot.localRotation,
                });
                foot.SetParent(shin, true);
            }
            foreach (var t in turns)
            {
                Transform b;
                if (!bones.TryGetValue(t.bone, out b)) continue;
                if (!rest.ContainsKey(b)) rest.Add(b, b.localRotation);
            }
            standingPosition = transform.localPosition;
            ready = true;
        }

        void LateUpdate()
        {
            if (!ready) return;
            if (!Seated)
            {
                if (!handedOver)
                {
                    // 座位を解いた一度だけ骨を戻し、そこから先は動きに任せる
                    transform.localPosition = standingPosition;
                    foreach (var pair in rest) pair.Key.localRotation = pair.Value;
                    // 足首は元の親へ返す。歩きの動作は足首を根の直下として付けているので、
                    // 繋いだままだと足の曲げが当たらない
                    foreach (var h in refooted)
                    {
                        h.child.SetParent(h.parent, false);
                        h.child.localPosition = h.position;
                        h.child.localRotation = h.rotation;
                    }
                    if (animator != null)
                    {
                        // 繋ぎ替えた先を見に行かせるため、道筋を取り直させてから回す
                        animator.enabled = true;
                        animator.Rebind();
                    }
                    handedOver = true;
                }
                return;
            }
            handedOver = false;
            if (animator != null && animator.enabled) animator.enabled = false;
            transform.localPosition = standingPosition + seatedOffset;
            var root = body != null ? body : transform;
            // 同じ骨に複数の軸を指定できるよう、安静へ戻すのは最初の 1 回だけにする
            cleared.Clear();
            foreach (var t in turns)
            {
                Transform b;
                if (!bones.TryGetValue(t.bone, out b)) continue;
                Quaternion r;
                if (!rest.TryGetValue(b, out r)) continue;
                if (cleared.Add(b)) b.localRotation = r;
                Turn(b, t, root, 1f);
            }
            // 重ねる分は安静へ戻さない。座位の上へ足していく
            if (Extra == null || Mathf.Approximately(ExtraWeight, 0f)) return;
            foreach (var t in Extra)
            {
                Transform b;
                if (!bones.TryGetValue(t.bone, out b)) continue;
                Turn(b, t, root, ExtraWeight);
            }
        }

        /// <summary>体から見た軸まわりに曲げる。骨それぞれの軸ではないので、左右で符号が揃う</summary>
        static void Turn(Transform b, BoneTurn t, Transform root, float weight)
        {
            var axis = t.axis == Axis.Right ? root.right : t.axis == Axis.Up ? root.up : root.forward;
            b.rotation = Quaternion.AngleAxis(t.degrees * weight, axis) * b.rotation;
        }
    }
}
