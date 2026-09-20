using UnityEngine;

namespace HalfAware
{
    /// <summary>
    /// モニターの画面。消灯から起動して、文字に見える帯を流す。
    ///
    /// **面のマテリアルはこの場面のために複製したものを当てる。**
    /// 場面 1 と共有すると、こちらで灯した画面が向こうでも灯る。
    /// 複製したマテリアルでは emission を有効にしておく。
    /// 切ってあると <see cref="MaterialPropertyBlock"/> から色を渡しても効かない
    /// </summary>
    public sealed class TerminalScreen : MonoBehaviour
    {
        [Tooltip("画面の面。ここのマテリアルを触る")]
        [SerializeField] Renderer face;
        [Tooltip("消えているときの色")]
        [SerializeField] Color off = new Color(0.035f, 0.040f, 0.045f);
        [Tooltip("点いているときの色")]
        [SerializeField] Color glow = new Color(0.32f, 0.80f, 0.46f);
        [Tooltip("文字を流す速さ。UV/秒")]
        [SerializeField] float scrollSpeed = 0.22f;

        static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        static readonly int Emission = Shader.PropertyToID("_EmissionColor");
        // ずれは _BaseMap ではなくその ST へ渡す。テクスチャ側の id を足しても別物には当たらない
        static readonly int BaseMapST = Shader.PropertyToID("_BaseMap_ST");

        MaterialPropertyBlock block;
        float booted = -1f;
        float offset;
        bool scrolling;

        /// <summary>もう灯っているか</summary>
        public bool Lit { get { return booted >= 0f && ScreenBoot.Lit(booted); } }

        void Awake()
        {
            block = new MaterialPropertyBlock();
            Paint(0f);
        }

        /// <summary>灯す。二度瞬いてから明るさが上がる</summary>
        public void Boot()
        {
            if (booted >= 0f) return;
            booted = 0f;
        }

        /// <summary>文字を流すか</summary>
        public void Scroll(bool on) { scrolling = on; }

        void Update()
        {
            if (booted >= 0f && !ScreenBoot.Lit(booted)) booted += Time.deltaTime;
            if (scrolling) offset += scrollSpeed * Time.deltaTime;
            Paint(booted < 0f ? 0f : ScreenBoot.Level(booted));
        }

        void Paint(float level)
        {
            if (face == null) return;
            face.GetPropertyBlock(block);
            var lit = Color.Lerp(off, glow, level);
            block.SetColor(BaseColor, lit);
            block.SetColor(Emission, lit * level);
            // 流すのは縦だけ。横へずらすと行が切れて読めない絵になる
            block.SetVector(BaseMapST, new Vector4(1f, 1f, 0f, -offset));
            face.SetPropertyBlock(block);
        }
    }
}
