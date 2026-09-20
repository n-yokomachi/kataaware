using UnityEngine;

namespace HalfAware
{
    /// <summary>
    /// モニターの画面。消灯から起動して、中に開いた窓ごとに文字を流す。
    ///
    /// **地と文字を別の面に分ける。** 画面の面そのものに帯のテクスチャを貼ると、
    /// 消えている間も灰色の文字が見えてしまう。地の色に帯が掛かるので、
    /// 暗いなりに行が読めてしまうため。地は帯を貼らない一枚板にして、
    /// 文字は手前へ浮かせた小さな面（<see cref="Pane"/>）に持たせ、
    /// 消えている間はその面ごと伏せる。
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
        /// <summary>画面の中に開いた窓ひとつ。窓ごとに字の大きさも流れる速さも違う</summary>
        [System.Serializable]
        public struct Pane
        {
            [Tooltip("窓の面")]
            public Renderer face;
            [Tooltip("帯のテクスチャの割り当て。**小さいほど字が大きく映る**")]
            public Vector2 tiling;
            [Tooltip("流れる速さの倍率。窓ごとに変えて、揃って動かないようにする")]
            public float speed;
            [Tooltip("初めのずれ。窓ごとに変えて、同じ行が並ばないようにする")]
            public float phase;
        }

        [Tooltip("画面の地。机に並んだぶんを全部渡す")]
        [SerializeField] Renderer[] backs = new Renderer[0];
        [Tooltip("画面の中の窓")]
        [SerializeField] Pane[] panes = new Pane[0];
        [Tooltip("消えているときの地の色")]
        [SerializeField] Color off = new Color(0.035f, 0.040f, 0.045f);
        [Tooltip("点いているときの地の色。窓の外に出る")]
        [SerializeField] Color back = new Color(0.026f, 0.070f, 0.040f);
        [Tooltip("文字の色")]
        [SerializeField] Color glow = new Color(0.32f, 0.80f, 0.46f);
        [Tooltip("文字を流す速さ。UV/秒。行の高さは UV で 0.04 なので、0.06 なら 1 秒に 1.5 行進む")]
        [SerializeField] float scrollSpeed = 0.06f;

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
            if (block == null) block = new MaterialPropertyBlock();
            var lit = level > 0.001f;
            Ground(lit ? Color.Lerp(off, back, level) : off);
            Windows(level, lit);
        }

        /// <summary>画面の地。帯を貼っていないので、色を渡すだけで一様に染まる</summary>
        void Ground(Color tone)
        {
            if (backs == null) return;
            for (var i = 0; i < backs.Length; i++)
            {
                var face = backs[i];
                if (face == null) continue;
                face.GetPropertyBlock(block);
                block.SetColor(BaseColor, tone);
                block.SetColor(Emission, tone);
                face.SetPropertyBlock(block);
            }
        }

        /// <summary>
        /// 画面の中の窓。
        ///
        /// **消えている間はレンダラーごと伏せる。** 色を黒にするだけでは、
        /// 地との僅かな差で窓の四角が浮き、消えているはずの画面に枠が見える。
        ///
        /// 流す向きは下。ST のずれを増やすと、いま見えている行より上にあった行を
        /// 拾うようになるので、絵は下へ送られる。上へ送りたいなら符号を返す
        /// </summary>
        void Windows(float level, bool lit)
        {
            if (panes == null) return;
            var tone = Color.Lerp(Color.black, glow, level);
            for (var i = 0; i < panes.Length; i++)
            {
                var pane = panes[i];
                if (pane.face == null) continue;
                if (pane.face.enabled != lit) pane.face.enabled = lit;
                if (!lit) continue;
                pane.face.GetPropertyBlock(block);
                block.SetColor(BaseColor, tone);
                block.SetColor(Emission, tone);
                // 流すのは縦だけ。横へずらすと行が切れて読めない絵になる
                block.SetVector(BaseMapST,
                    new Vector4(pane.tiling.x, pane.tiling.y, 0f, pane.phase + offset * pane.speed));
                pane.face.SetPropertyBlock(block);
            }
        }
    }
}
