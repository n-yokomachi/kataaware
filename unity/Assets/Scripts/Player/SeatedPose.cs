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

        Vector3 standingPosition;

        readonly Dictionary<string, Transform> bones = new Dictionary<string, Transform>();
        readonly Dictionary<Transform, Quaternion> rest = new Dictionary<Transform, Quaternion>();
        bool ready;

        /// <summary>true の間だけ座位の姿勢を当てる</summary>
        public bool Seated { get; set; } = true;

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
                transform.localPosition = standingPosition;
                foreach (var pair in rest) pair.Key.localRotation = pair.Value;
                return;
            }
            transform.localPosition = standingPosition + seatedOffset;
            var root = body != null ? body : transform;
            foreach (var t in turns)
            {
                Transform b;
                if (!bones.TryGetValue(t.bone, out b)) continue;
                Quaternion r;
                if (!rest.TryGetValue(b, out r)) continue;
                b.localRotation = r;
                var axis = t.axis == Axis.Right ? root.right : t.axis == Axis.Up ? root.up : root.forward;
                b.rotation = Quaternion.AngleAxis(t.degrees, axis) * b.rotation;
            }
        }
    }
}
