using UnityEngine;

namespace HalfAware
{
    /// <summary>
    /// 記憶の中の人の、部位ごとの色。
    ///
    /// **マテリアルは全員で一枚を使い回し、色は MaterialPropertyBlock で人ごとに持たせる。**
    /// 十六人 × 十前後の部位をマテリアルのアセットにすると百を超え、組み直すたびに増減する。
    /// 色は組み立てが <see cref="DiveCast"/> から写してここへ並べる。
    ///
    /// エディタでも色が見えるよう、有効になるたびに当て直す。
    /// MaterialPropertyBlock はシーンに保存されないので、ここが当てないと白いままになる
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class PersonTint : MonoBehaviour
    {
        [Tooltip("DiveCast の id。同じ人はどの記憶でも同じ色")]
        [SerializeField] string person;
        [Tooltip("塗るレンダラー。slots・colors・gloss と同じ並び")]
        [SerializeField] Renderer[] renderers = new Renderer[0];
        [Tooltip("レンダラーの中のマテリアルの番号")]
        [SerializeField] int[] slots = new int[0];
        [SerializeField] Color[] colors = new Color[0];
        [SerializeField] float[] gloss = new float[0];

        static MaterialPropertyBlock block;
        static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        static readonly int Smoothness = Shader.PropertyToID("_Smoothness");

        public string Person { get { return person; } }
        public int Count { get { return renderers.Length; } }
        public Renderer RendererAt(int i) { return renderers[i]; }
        public int SlotAt(int i) { return slots[i]; }
        public Color ColorAt(int i) { return colors[i]; }

        void OnEnable()
        {
            Apply();
        }

        /// <summary>組み立てから並べる。並べ終えたら当てる</summary>
        public void Set(string id, Renderer[] r, int[] s, Color[] c, float[] g)
        {
            person = id;
            renderers = r;
            slots = s;
            colors = c;
            gloss = g;
            Apply();
        }

        public void Apply()
        {
            if (renderers == null) return;
            if (block == null) block = new MaterialPropertyBlock();
            for (var i = 0; i < renderers.Length; i++)
            {
                var r = renderers[i];
                if (r == null || i >= slots.Length || i >= colors.Length) continue;
                block.Clear();
                block.SetColor(BaseColor, colors[i]);
                block.SetFloat(Smoothness, i < gloss.Length ? gloss[i] : 0.1f);
                r.SetPropertyBlock(block, slots[i]);
            }
        }
    }
}
