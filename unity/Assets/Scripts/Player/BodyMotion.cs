using UnityEngine;

namespace HalfAware
{
    /// <summary>
    /// 歩く速さを体の動きへ渡す。立って動いている間だけ働く。
    /// 座っている間は SeatedPose が骨を握るので、こちらは止まっている
    /// </summary>
    [DefaultExecutionOrder(10)]
    public sealed class BodyMotion : MonoBehaviour
    {
        /// <summary>これより遅ければ止まっているとみなす。m/s</summary>
        public const float StillBelow = 0.12f;

        /// <summary>歩きの一巡が等速で進む速さ。足を滑らせないための基準。m/s</summary>
        public const float NominalWalk = 1.06f;

        /// <summary>足の運びを速める/緩める幅。外れると歩きに見えなくなる</summary>
        public const float SlowestCycle = 0.5f;
        public const float FastestCycle = 2.6f;

        [SerializeField] CharacterController body;
        [SerializeField] Animator animator;

        static readonly int SpeedId = Animator.StringToHash("speed");
        static readonly int CycleId = Animator.StringToHash("cycle");

        /// <summary>水平の速さだけを見る。落下や段差で歩き出さないように</summary>
        public static float GroundSpeed(Vector3 velocity)
        {
            velocity.y = 0f;
            var speed = velocity.magnitude;
            return speed < StillBelow ? 0f : speed;
        }

        /// <summary>足の運びの速さ。進む速さに合わせて一巡の長さを伸縮させ、床を滑らせない</summary>
        public static float Cycle(float groundSpeed)
        {
            return Mathf.Clamp(groundSpeed / NominalWalk, SlowestCycle, FastestCycle);
        }

        void Update()
        {
            if (animator == null || !animator.enabled || body == null) return;
            var speed = GroundSpeed(body.velocity);
            animator.SetFloat(SpeedId, speed);
            animator.SetFloat(CycleId, Cycle(speed));
        }
    }
}
