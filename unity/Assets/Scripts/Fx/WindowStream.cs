using UnityEngine;

namespace HalfAware
{
    /// <summary>
    /// 電車の窓の外を流れる灯り。
    ///
    /// **窓は動かさず、絵だけを送る。** 走っている車両の外を実際に作ると、
    /// 記憶ひとつ 30 秒のために街が一つ要る。窓に貼った帯を横へ送れば、
    /// 止まっていない車両に見えるところまでは届く。
    ///
    /// ずれは <c>_BaseMap</c> ではなくその ST へ、<see cref="MaterialPropertyBlock"/> から渡す。
    /// 窓は同じマテリアルを分け合っているので、マテリアルを直に書き換えると
    /// 八枚とも同じ絵になり、隣り合う窓に同じ建物が並んで書き割りだと分かってしまう。
    /// <see cref="TerminalScreen"/> と同じ手で、あちらは縦へ、こちらは横へ送る。
    ///
    /// 帯は横に繋がるように描いてある（<c>tools/make-window.py</c>・<c>HalfAware/Shoot the train backdrop</c>）ので、
    /// ずれが 1 を越えたところで頭へ戻しても継ぎ目は出ない。
    ///
    /// **電車は窓の外を近く・中・遠くの三つの層に分け、層ごとに一つずつ付けて速さを変える**（設計書 9.1 節「電車の作り込み」）。
    /// 層の帯は車両の両脇に通しで立てた一枚なので、窓ごとのずれ（stagger）は使わない
    /// </summary>
    [DefaultExecutionOrder(-20)]
    public sealed class WindowStream : MonoBehaviour
    {
        [Tooltip("帯を貼った窓の面。ドアの窓も混ぜてよい")]
        [SerializeField] Renderer[] panes = new Renderer[0];
        [Tooltip("帯の繰り返し。x を小さくすると窓一つに映る範囲が狭まり、外の物が大きく見える")]
        [SerializeField] Vector2 tiling = new Vector2(0.55f, 1f);
        [Tooltip("流れる速さ。UV/秒。符号を返すと逆へ走る")]
        [SerializeField] float speed = 0.36f;
        [Tooltip("窓ごとの初めのずれ。隣り合う窓に同じ建物が並ばないように")]
        [SerializeField] float stagger = 0.31f;
        [Tooltip("速さの揺らぎの深さ。一定で送ると、外ではなく絵が動いていることに気づかれる")]
        [SerializeField] float sway = 0.14f;
        [Tooltip("揺らぎの周期。秒")]
        [SerializeField] float swaySpan = 3.4f;

        // ずれは _BaseMap ではなくその ST へ渡す。テクスチャ側の id を渡しても別物には当たらない
        static readonly int BaseMapST = Shader.PropertyToID("_BaseMap_ST");

        MaterialPropertyBlock block;
        float at;
        float age;

        void OnEnable()
        {
            // 記憶ごとに場所を起こし直すので、前の記憶の続きから流れ出さないように頭へ戻す
            at = 0f;
            age = 0f;
            Paint();
        }

        void Update()
        {
            age += Time.deltaTime;
            var pace = 1f + Mathf.Sin(age / Mathf.Max(0.01f, swaySpan) * Mathf.PI * 2f) * sway;
            at += speed * pace * Time.deltaTime;
            // 送り続けると桁が落ちて絵が跳ねる。帯は横に繋がるので、1 で頭へ戻しても切れ目は出ない
            at -= Mathf.Floor(at);
            Paint();
        }

        void Paint()
        {
            if (panes == null) return;
            if (block == null) block = new MaterialPropertyBlock();
            for (var i = 0; i < panes.Length; i++)
            {
                var face = panes[i];
                if (face == null) continue;
                face.GetPropertyBlock(block);
                // 流すのは横だけ。縦へずらすと空と線路際が入れ替わって、外が上下する
                block.SetVector(BaseMapST, new Vector4(tiling.x, tiling.y, at + i * stagger, 0f));
                face.SetPropertyBlock(block);
            }
        }
    }
}
