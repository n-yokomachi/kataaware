using UnityEngine;

namespace HalfAware
{
    /// <summary>
    /// 眩暈の強さをシーンに置く。SceneFlow が Hold と Decay を呼び、毎フレーム シェーダのグローバル変数へ入れる。
    /// SceneFlow より後に Update が走るよう実行順を後ろに置く
    /// </summary>
    [DefaultExecutionOrder(10)]
    public sealed class DazeVolume : MonoBehaviour
    {
        static readonly int BlurId = Shader.PropertyToID("_DazeBlur");
        static readonly int WobbleId = Shader.PropertyToID("_DazeWobble");

        readonly Daze daze = new Daze();

        public float Blur => daze.Blur;
        public float Wobble => daze.Wobble;
        public bool IsClear => daze.IsClear;

        public void Hold(float blur, float wobble) => daze.Hold(blur, wobble);

        public void Decay(float blur, float wobble, float seconds) => daze.Decay(blur, wobble, seconds);

        public void Clear() => daze.Clear();

        void OnEnable() => Push();

        /// <summary>グローバル変数は再生を抜けても残るので、消えるときに 0 へ戻す。戻さないとシーンビューが眩暈のままになる</summary>
        void OnDisable()
        {
            Shader.SetGlobalFloat(BlurId, 0f);
            Shader.SetGlobalFloat(WobbleId, 0f);
        }

        void Update()
        {
            daze.Tick(Time.deltaTime);
            Push();
        }

        void Push()
        {
            Shader.SetGlobalFloat(BlurId, daze.Blur);
            Shader.SetGlobalFloat(WobbleId, daze.Wobble);
        }
    }
}
