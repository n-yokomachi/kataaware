using UnityEngine;

namespace HalfAware
{
    /// <summary>
    /// モニターの画面。消灯から起動して、文字に見える帯を流す。
    ///
    /// **面のマテリアルはこの場面のために複製したものを当てる。**
    /// 場面 1 と共有すると、こちらで灯した画面が向こうでも灯る。
    /// 複製したマテリアルでは emission を有効にしておく。
    /// 切ってあると <see cref="MaterialPropertyBlock"/> から色を渡しても効かない。
    ///
    /// **面は 1 枚ではなく机に並んだ 5 枚をまとめて持つ。** 5 枚でひとつの作業机なので、
    /// 点くときは 5 枚とも点く。1 枚だけ灯すと、残りの 4 枚が壊れているように見える
    /// </summary>
    public sealed class TerminalScreen : MonoBehaviour
    {
        [Tooltip("画面の面。ここのマテリアルを触る。机に並んだぶんを全部渡す")]
        [SerializeField] Renderer[] faces = new Renderer[0];
        [Tooltip("消えているときの色")]
        [SerializeField] Color off = new Color(0.035f, 0.040f, 0.045f);
        [Tooltip("点いているときの色")]
        [SerializeField] Color glow = new Color(0.32f, 0.80f, 0.46f);
        [Tooltip("文字を流す速さ。UV/秒。行の高さは UV で 0.04 なので、0.06 なら 1 秒に 1.5 行進む")]
        [SerializeField] float scrollSpeed = 0.06f;
        [Tooltip("帯のテクスチャの、面に映す割り当て。**小さいほど字が大きく映る**")]
        [SerializeField] Vector2 tiling = new Vector2(1.0f, 0.5f);

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
            if (faces == null) return;
            var lit = Color.Lerp(off, glow, level);
            for (var i = 0; i < faces.Length; i++)
            {
                var face = faces[i];
                if (face == null) continue;
                face.GetPropertyBlock(block);
                block.SetColor(BaseColor, lit);
                block.SetColor(Emission, lit * level);
                // **テクスチャを丸ごと映さない。** 描画は 427×240 に renderScale 0.333 が
                // 掛かるので、画面 1 枚は実寸で 30×22 px ほどしかない。256 px の帯を
                // 25 行とも詰めると 1 行が 1 px を切り、行が潰れてただの緑の板になる。
                // 半分だけ切り出すと 1 行に 2 px 残り、文字の並びに見える。
                // **横は 1 を超えない。** 超えると横に繋がって、継ぎ目が縦の筋に出る。
                // 流すのは縦だけ。横へずらすと行が切れて読めない絵になる
                block.SetVector(BaseMapST, new Vector4(tiling.x, tiling.y, 0f, -offset));
                face.SetPropertyBlock(block);
            }
        }
    }
}
