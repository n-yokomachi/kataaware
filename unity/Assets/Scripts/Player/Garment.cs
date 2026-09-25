using UnityEngine;

namespace HalfAware
{
    /// <summary>
    /// 体に着せた服（主人公の革のライダースジャケット）。体と同じ骨で動くレンダラーを持ち、<see cref="Worn"/> で着る・脱ぐを切り替える。
    /// 脱いだ時はレンダラーを消すだけで、骨も形もそのまま残す（着る・脱ぐの動きは作らない）。
    /// 体の肌を探す所（<see cref="SkinPoint.BodyOf"/> など）は、この下のレンダラーを体の肌に数えない
    /// </summary>
    public sealed class Garment : MonoBehaviour
    {
        [Tooltip("着ているか。切ると服のレンダラーを消す")]
        [SerializeField] bool worn = true;
        [Tooltip("服のレンダラー（体と同じ骨で動く）")]
        [SerializeField] Renderer[] renderers = new Renderer[0];

        /// <summary>着ているか。変えるとすぐにレンダラーを点ける・消す</summary>
        public bool Worn
        {
            get { return worn; }
            set
            {
                worn = value;
                Apply();
            }
        }

        /// <summary>服のレンダラー（動作確認から読む）</summary>
        public Renderer[] Renderers => renderers;

        /// <summary>服のレンダラーと、着ているかを決める。組み立てから呼ぶ</summary>
        public void Set(Renderer[] renderers, bool worn)
        {
            this.renderers = renderers;
            this.worn = worn;
            Apply();
        }

        void OnEnable()
        {
            Apply();
        }

        void OnValidate()
        {
            Apply();
        }

        void Apply()
        {
            if (renderers == null) return;
            foreach (var r in renderers)
                if (r != null) r.enabled = worn;
        }
    }
}
