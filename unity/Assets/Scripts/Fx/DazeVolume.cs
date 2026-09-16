using UnityEngine;

namespace HalfAware
{
    /// <summary>
    /// 眩暈の強さをシーンに置く。SceneFlow が Hold と Decay を呼び、段階 4 の後処理がここから数値を読む。
    /// SceneFlow より後に Update が走るよう実行順を後ろに置く
    /// </summary>
    [DefaultExecutionOrder(10)]
    public sealed class DazeVolume : MonoBehaviour
    {
        readonly Daze daze = new Daze();

        public float Blur => daze.Blur;
        public float Wobble => daze.Wobble;
        public bool IsClear => daze.IsClear;

        public void Hold(float blur, float wobble) => daze.Hold(blur, wobble);

        public void Decay(float blur, float wobble, float seconds) => daze.Decay(blur, wobble, seconds);

        public void Clear() => daze.Clear();

        void Update() => daze.Tick(Time.deltaTime);
    }
}
