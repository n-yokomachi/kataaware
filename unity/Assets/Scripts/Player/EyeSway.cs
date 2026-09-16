using UnityEngine;

namespace HalfAware
{
    /// <summary>
    /// 眩暈のあいだ、目の位置と向きをゆっくり漂わせる。
    /// 自分で変換を触らず、ずれの値を PlayerController に渡して同じフレームのうちに書かせる。
    /// 上乗せしていく形にすると、書き直されない間にずれが溜まって視界が回り続けるため。
    /// 調べる対象の選択は揺れる前の向きで行われるため、狙いにくくはならない
    /// </summary>
    [DefaultExecutionOrder(-20)]
    public sealed class EyeSway : MonoBehaviour
    {
        [Tooltip("漂いの強さを読む先。無ければ動かない")]
        [SerializeField] DazeVolume daze;
        [Tooltip("ずれを渡す先")]
        [SerializeField] PlayerController player;
        [Tooltip("メートル。いちばん強いときの目の位置のずれ")]
        [SerializeField] float shift = 0.03f;
        [Tooltip("度。いちばん強いときの向きのずれ")]
        [SerializeField] float tilt = 0.9f;

        readonly Sway sway = new Sway();

        void Awake()
        {
            if (player == null) Debug.LogError("EyeSway: player が未接続", this);
        }

        /// <summary>切ったら漂いを残さない</summary>
        void OnDisable()
        {
            if (player == null) return;
            player.EyeOffset = Vector3.zero;
            player.EyeTilt = Vector2.zero;
        }

        void Update()
        {
            if (player == null) return;
            sway.Tick(Time.time, daze != null ? daze.Wobble : 0f, shift, tilt);
            player.EyeOffset = sway.Offset;
            player.EyeTilt = sway.Tilt;
        }
    }
}
