using UnityEngine;

namespace HalfAware
{
    /// <summary>
    /// 眩暈のあいだ、目の位置と向きをゆっくり漂わせる。
    /// 歩行の処理がカメラを置いた後に上乗せするので、毎フレームの最後に働く。
    /// 調べる対象の選択は揺れる前の向きで行われるため、狙いにくくはならない
    /// </summary>
    public sealed class EyeSway : MonoBehaviour
    {
        [Tooltip("漂いの強さを読む先。無ければ動かない")]
        [SerializeField] DazeVolume daze;
        [Tooltip("動かすカメラ。歩行の処理が位置と向きを書いているもの")]
        [SerializeField] Transform eye;
        [Tooltip("メートル。いちばん強いときの目の位置のずれ")]
        [SerializeField] float shift = 0.03f;
        [Tooltip("度。いちばん強いときの向きのずれ")]
        [SerializeField] float tilt = 0.9f;

        readonly Sway sway = new Sway();

        void Awake()
        {
            if (eye == null) Debug.LogError("EyeSway: eye が未接続", this);
        }

        void LateUpdate()
        {
            if (eye == null) return;
            sway.Tick(Time.time, daze != null ? daze.Wobble : 0f, shift, tilt);
            eye.localPosition += sway.Offset;
            eye.localRotation *= Quaternion.Euler(sway.Tilt.x, sway.Tilt.y, 0f);
        }
    }
}
