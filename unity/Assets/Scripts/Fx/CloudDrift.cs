using UnityEngine;

namespace HalfAware
{
    /// <summary>
    /// 低く垂れた雲を流す。板は動かさず、絵の位置だけをずらす。
    /// 見上げたときにゆっくり動いていれば、止まった書き割りには見えない
    /// </summary>
    [DefaultExecutionOrder(-20)]
    public sealed class CloudDrift : MonoBehaviour
    {
        [Tooltip("1 秒あたりに絵をずらす量。絵 1 枚ぶんを 1 とする")]
        [SerializeField] Vector2 speed = new Vector2(0.004f, 0.0015f);

        Material mat;
        Vector2 at;

        void OnEnable()
        {
            var r = GetComponent<Renderer>();
            if (r == null) return;
            // 板ごとに別の絵を持たせる。共有のままだと全部の層が一緒に動く
            mat = Application.isPlaying ? r.material : r.sharedMaterial;
            if (mat != null) at = mat.GetTextureOffset("_BaseMap");
        }

        void Update()
        {
            if (mat == null) return;
            at += speed * Time.deltaTime;
            at.x -= Mathf.Floor(at.x);
            at.y -= Mathf.Floor(at.y);
            mat.SetTextureOffset("_BaseMap", at);
        }
    }
}
