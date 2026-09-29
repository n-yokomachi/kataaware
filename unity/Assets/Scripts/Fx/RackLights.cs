using UnityEngine;

namespace HalfAware
{
    /// <summary>
    /// 自室のサーバーラックの点滅する灯り。
    ///
    /// 灯りの面（一つの mesh）は点滅の絵（横が灯りの組、縦が時の段）の、組ごとの列の真ん中を引いている。
    /// 段ごとに縦のずれを <see cref="MaterialPropertyBlock"/> で <c>_BaseMap_ST</c> へ渡すだけで、灯りがそれぞれの拍子で瞬く
    /// （光る所の絵も同じ uv を使う）。マテリアルは書き換えないので、ほかの物には移らない。
    /// 絵は <c>HalfAware/Build the room furniture</c>（<c>BuildFurniture</c>）が描く
    /// </summary>
    public sealed class RackLights : MonoBehaviour
    {
        [Tooltip("点滅の絵を貼った灯りの面")]
        [SerializeField] Renderer lights;
        [Tooltip("点滅の絵の縦の段の数")]
        [SerializeField] int rows = 16;
        [Tooltip("一段の長さ。秒")]
        [SerializeField] float step = 0.13f;

        static readonly int BaseMapST = Shader.PropertyToID("_BaseMap_ST");

        MaterialPropertyBlock block;
        float age;
        int shown = -1;

        void OnEnable()
        {
            shown = -1;
            Paint(0);
        }

        void Update()
        {
            age += Time.deltaTime;
            var row = Mathf.FloorToInt(age / Mathf.Max(0.01f, step)) % Mathf.Max(1, rows);
            if (row == shown) return;
            Paint(row);
        }

        void Paint(int row)
        {
            if (lights == null) return;
            shown = row;
            if (block == null) block = new MaterialPropertyBlock();
            lights.GetPropertyBlock(block);
            block.SetVector(BaseMapST, new Vector4(1f, 1f, 0f, (float)row / Mathf.Max(1, rows)));
            lights.SetPropertyBlock(block);
        }
    }
}
