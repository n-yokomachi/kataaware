using System;
using UnityEngine;

namespace HalfAware
{
    /// <summary>
    /// 座った形。立ちと歩きの動き（Animator）を止め、組み立てが椅子に合わせて解いておいた骨の向きを当てる。
    ///
    /// 骨は名前ではなく Humanoid の骨（<see cref="Animator.GetBoneTransform"/>）で探すので、
    /// 体の模型を差し替えても同じ作りで動く。座った形そのもの（どの骨をどちらへ何度）は模型ごとに違うので、
    /// 組み立て（PlaceProtagonist）が椅子の座面・肘掛け・床から解いて <see cref="seated"/> へ書く。
    ///
    /// 形は二つまで持てる。二つ目（<see cref="alternate"/>）は場面 8 の、ハンドルに手を乗せた形に使う。
    /// 腕を動かす演出（<see cref="JackPull"/> など）は、これが骨を当てた後（実行順 25）に腕だけを曲げ直す
    /// </summary>
    [DefaultExecutionOrder(20)]
    public sealed class SeatedPose : MonoBehaviour
    {
        /// <summary>骨一本の、親から見た向き（腰の骨だけは位置も）</summary>
        [Serializable]
        public struct Bone
        {
            public HumanBodyBones bone;
            public Vector3 position;
            public Quaternion rotation;
        }

        [Tooltip("体の Animator。座っている間は止める")]
        [SerializeField] Animator animator;
        [Tooltip("座った形。組み立てが椅子に合わせて解いて書く")]
        [SerializeField] Bone[] seated = new Bone[0];
        [Tooltip("二つ目の座った形（場面 8 のハンドルに手を乗せた形）。無ければ空")]
        [SerializeField] Bone[] alternate = new Bone[0];

        Transform[] bones;
        Transform[] alternateBones;
        bool handedOver;

        /// <summary>true の間だけ座った形を当てる</summary>
        public bool Seated { get; set; } = true;

        /// <summary>二つ目の形を当てるか</summary>
        public bool UseAlternate { get; set; }

        /// <summary>体の Animator。腕の演出が骨を探すのに使う</summary>
        public Animator Animator => animator;

        void Awake()
        {
            Bind();
        }

        /// <summary>骨を探し直す。エディタで撮るときにも呼ぶ</summary>
        public void Bind()
        {
            bones = Find(seated);
            alternateBones = Find(alternate);
        }

        Transform[] Find(Bone[] pose)
        {
            var found = new Transform[pose.Length];
            if (animator == null) return found;
            for (var i = 0; i < pose.Length; i++) found[i] = animator.GetBoneTransform(pose[i].bone);
            return found;
        }

        void LateUpdate()
        {
            if (!Seated)
            {
                if (!handedOver)
                {
                    // 座った形を解いた一度だけ、動きへ骨を返す。道筋を取り直させてから回す
                    if (animator != null)
                    {
                        animator.enabled = true;
                        animator.Rebind();
                    }
                    handedOver = true;
                }
                return;
            }
            handedOver = false;
            if (animator != null && animator.enabled) animator.enabled = false;
            Apply();
        }

        /// <summary>座った形を骨へ当てる。エディタで撮るときにも呼ぶ</summary>
        public void Apply()
        {
            if (bones == null || bones.Length != seated.Length) Bind();
            var alt = UseAlternate && alternate.Length > 0;
            var pose = alt ? alternate : seated;
            var found = alt ? alternateBones : bones;
            for (var i = 0; i < pose.Length; i++)
            {
                var t = found[i];
                if (t == null) continue;
                if (pose[i].bone == HumanBodyBones.Hips) t.localPosition = pose[i].position;
                t.localRotation = pose[i].rotation;
            }
        }
    }
}
