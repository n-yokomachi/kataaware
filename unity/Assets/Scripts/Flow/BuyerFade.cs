using UnityEngine;

namespace HalfAware
{
    /// <summary>
    /// 買い手が浮かび上がるときだけ、透かせるマテリアルに差し替える。
    ///
    /// ふだんは群衆と同じ透かさないマテリアル（solid）で描く。透かせる側のまま置いておくと、深さを書かないので
    /// 顔の奥の面（口の中や後ろ頭の髪の塗り）が顔の上に描かれ、顔が崩れて見えた。
    /// 濃さを上げているあいだ（1 未満）だけ透かせるマテリアル（fade）にして、_BaseColor の α を下げる
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BuyerFade : MonoBehaviour
    {
        [Tooltip("ふだんのマテリアル（透かさない）。面の組の順")]
        [SerializeField] Material[] solid = new Material[0];
        [Tooltip("浮かび上がるあいだのマテリアル（透かせる）。面の組の順")]
        [SerializeField] Material[] fade = new Material[0];

        static readonly int BaseColour = Shader.PropertyToID("_BaseColor");
        MaterialPropertyBlock paint;

        /// <summary>組み立てで二組のマテリアルを渡す</summary>
        public void Bind(Material[] solidMaterials, Material[] fadeMaterials)
        {
            solid = solidMaterials;
            fade = fadeMaterials;
        }

        /// <summary>濃さ。0 で透明、1 で透かさないマテリアルに戻す</summary>
        public void Set(float amount)
        {
            var r = GetComponent<Renderer>();
            if (r == null) return;
            amount = Mathf.Clamp01(amount);
            if (amount >= 1f || fade == null || fade.Length == 0)
            {
                if (solid != null && solid.Length > 0) r.sharedMaterials = solid;
                r.SetPropertyBlock(null);
                return;
            }
            r.sharedMaterials = fade;
            if (paint == null) paint = new MaterialPropertyBlock();
            r.GetPropertyBlock(paint);
            var colour = fade[0] != null ? fade[0].GetColor(BaseColour) : Color.white;
            colour.a *= amount;
            paint.SetColor(BaseColour, colour);
            r.SetPropertyBlock(paint);
        }
    }
}
