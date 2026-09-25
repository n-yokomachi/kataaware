using UnityEngine;

namespace HalfAware
{
    /// <summary>
    /// 体の肌の一点に貼り付く置き所。その所の肌と同じ重みで骨の動きを混ぜ（線形の混ぜ）、毎こま自分をそこへ置く。
    /// 手首の差込口（インプラント）と、挿さっているジャックがこの子になる。
    ///
    /// 手首に近い肌は前腕の骨にも手の骨にも付いていて、どちらか一つの骨に付けた物は、手首をひねると肌から 6 mm ずれた
    /// （抜くしぐさで右の手首を目の前へ出すと、前腕の骨に付けた差込口の輪が肌に沈んだ）。
    /// 置き方（骨・重み・骨ごとの置き方）は組み立て（BuildProps.WristPort）が体の肌から測って書く
    /// </summary>
    [DefaultExecutionOrder(30)]
    public sealed class SkinPoint : MonoBehaviour
    {
        [Tooltip("混ぜる骨")]
        [SerializeField] Transform[] bones = new Transform[0];
        [Tooltip("骨ごとの重み（和が 1）")]
        [SerializeField] float[] weights = new float[0];
        [Tooltip("骨ごとの、この点の置き方（骨の枠で見た、束ねた姿勢の置き方）")]
        [SerializeField] Matrix4x4[] offsets = new Matrix4x4[0];

        /// <summary>骨・重み・骨ごとの置き方を書く。組み立てから呼ぶ</summary>
        public void Set(Transform[] bones, float[] weights, Matrix4x4[] offsets)
        {
            this.bones = bones;
            this.weights = weights;
            this.offsets = offsets;
        }

        /// <summary>今の骨の向きから、肌の上の置き方を求めて自分を置く</summary>
        public void Follow()
        {
            Vector3 position;
            Quaternion rotation;
            if (Place(out position, out rotation)) transform.SetPositionAndRotation(position, rotation);
        }

        /// <summary>今の骨の向きから求めた、肌の上の位置と向き。骨が無ければ false</summary>
        public bool Place(out Vector3 position, out Quaternion rotation)
        {
            position = transform.position;
            rotation = transform.rotation;
            var n = Mathf.Min(bones.Length, Mathf.Min(weights.Length, offsets.Length));
            if (n == 0) return false;
            var m = new Matrix4x4();
            for (var i = 0; i < n; i++)
            {
                if (bones[i] == null || weights[i] <= 0f) continue;
                var b = bones[i].localToWorldMatrix * offsets[i];
                for (var k = 0; k < 16; k++) m[k] += b[k] * weights[i];
            }
            var forward = (Vector3)m.GetColumn(2);
            var up = (Vector3)m.GetColumn(1);
            if (forward.sqrMagnitude < 1e-12f || up.sqrMagnitude < 1e-12f) return false;
            position = m.GetColumn(3);
            rotation = Quaternion.LookRotation(forward, up);
            return true;
        }

        void LateUpdate()
        {
            Follow();
        }

        /// <summary>
        /// c が肌に貼り付いた物（この置き所の下にある物。差込口の輪と穴）か、体に着せた服（<see cref="Garment"/>。ジャケット）の一部か。
        /// どれも体の骨で動く SkinnedMeshRenderer なので、体の肌を集めるところ（肌の頂点・骨の曲がりの基準など）ではこれで除く
        /// </summary>
        public static bool Rides(Component c)
        {
            return c != null && (c.GetComponentInParent<SkinPoint>(true) != null || c.GetComponentInParent<Garment>(true) != null);
        }

        /// <summary>root の下の体の肌のレンダラー。頭の影だけを落とす物（HeadShadow）と、肌に貼り付いた物・着せた服（<see cref="Rides"/>）を除いた最初のもの</summary>
        public static SkinnedMeshRenderer BodyOf(Component root)
        {
            if (root == null) return null;
            foreach (var smr in root.GetComponentsInChildren<SkinnedMeshRenderer>())
                if (smr.shadowCastingMode != UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly && !Rides(smr)) return smr;
            return null;
        }
    }
}
