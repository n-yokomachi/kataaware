using System.Collections;
using UnityEngine;

namespace HalfAware
{
    /// <summary>
    /// 売り買いの買い手が、主人公の視界の左右の外から歩いて入ってきて、卓の向こうで止まり、去る時も歩いて左右の外へ出ていく
    /// （オーナー、2026-09-30「場面2の買い手も歩いて近づいてくるようにして。ただ、画面奥から登場させると時間かかるから、画面の左右から出てくる感じ」）。
    ///
    /// 動かすのは人の根だけ。足の運び（歩き・立ち）は <see cref="PersonMotion"/> が根の進みから決め、歩いている間は体を進む向きへ、
    /// 止まったら根の向き（卓の方、主人公の方）へ回す。止まったら、その人の立ち姿（片脚に預ける・腕を下ろす）を
    /// <see cref="PersonMotion.Pose"/> で掛けていき、去る時は外してから歩き出す。腕を組む人は、組んだまま歩いてくる（<see cref="keepStance"/>。
    /// 下ろした腕から組んだ腕へ骨の向きを寄せると、途中で手と前腕が腹に沈む）。
    ///
    /// - **道筋**（<see cref="path"/>）は、視界の外の入り口 → 露店の柱の外 → 止まる所。去る時は同じ道筋を逆に戻る（来た側へ出る）。
    ///   次の買い手は反対の側から来るので、擦れ違わない
    /// - **止まる前は歩みを緩める**（<see cref="slowFor"/> m で <see cref="slowest"/> 倍まで）。歩幅も詰まる（PersonMotion）
    /// - **足音は歩きの足の着地に揃える**（<see cref="BuyerSteps"/>）。着地は歩きの動きの周期のどこか（<see cref="contacts"/>、組み立てで実測）で、
    ///   周期がそこを越えたこまで、その足の所で鳴らす。止まった時は足を揃える一歩を小さく鳴らす
    /// - 時間で進むので、主人公が見回して買い手を見失っても、着く時と台詞の出方は変わらない
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BuyerWalk : MonoBehaviour
    {
        [Tooltip("道筋。親のローカル。頭が視界の外の入り口、尻が止まる所")]
        [SerializeField] Vector3[] path = new Vector3[0];
        [Tooltip("歩く速さ。m/s")]
        [SerializeField] float speed = 1.3f;
        [Tooltip("止まる前に歩みを緩め始める、止まる所までの m")]
        [SerializeField] float slowFor = 0.8f;
        [Tooltip("止まる直前の速さの割合")]
        [SerializeField] float slowest = 0.45f;
        [Tooltip("止まってから立ち姿を掛けきるまでの秒")]
        [SerializeField] float settleSeconds = 0.6f;
        [Tooltip("去る時に立ち姿を外す秒。外してから歩き出す")]
        [SerializeField] float looseSeconds = 0.35f;

        [Header("体")]
        [SerializeField] PersonMotion motion;
        [Tooltip("立ち姿の形の番号（PersonMotion の posed / poses）。-1 なら掛けない")]
        [SerializeField] int stance;
        [Tooltip("歩く間も立ち姿のまま（腕を組んだまま歩いてきて、そのまま去る）。下ろした腕から組んだ腕へ移る途中は、手と前腕が腹に沈む")]
        [SerializeField] bool keepStance;

        [Header("足音")]
        [SerializeField] BuyerSteps steps;
        [Tooltip("ヒールで歩く")]
        [SerializeField] bool heels;
        [SerializeField] Transform leftFoot;
        [SerializeField] Transform rightFoot;
        [Tooltip("着地する周期の位置（0〜1）。左の足、右の足の順。組み立てで歩きの動きから実測する")]
        [SerializeField] float[] contacts = { 0f, 0.5f };
        [Tooltip("止まった時に足を揃える一歩の大きさ（ふつうの一歩に対して）")]
        [SerializeField] float settleStep = 0.6f;

        float lastPhase;
        bool walking;
        float sinceStep;

        /// <summary>歩いている（入ってくる・出ていく途中）</summary>
        public bool Walking { get { return walking; } }

        /// <summary>鳴らした歩数。動作確認から読む</summary>
        public int Stepped { get; private set; }

        public Vector3[] Path { get { return path; } }
        public float Speed { get { return speed; } }

        /// <summary>入り口から止まるまでの秒（緩めるぶんを含む。立ち姿を掛ける秒は含まない）</summary>
        public float ArriveSeconds()
        {
            return Seconds(Length(path), speed, slowFor, slowest);
        }

        // ---- 決まり（試験から呼ぶ） ------------------------------------------------

        /// <summary>道筋の長さ。m</summary>
        public static float Length(Vector3[] points)
        {
            var n = 0f;
            for (var i = 1; i < points.Length; i++) n += Vector3.Distance(points[i - 1], points[i]);
            return n;
        }

        /// <summary>道筋の頭から distance 進んだ所と、そこで進む向き。道筋の外は端に留める</summary>
        public static Vector3 Along(Vector3[] points, float distance, out Vector3 heading)
        {
            heading = Vector3.forward;
            if (points == null || points.Length == 0) return Vector3.zero;
            if (points.Length == 1) return points[0];
            var left = Mathf.Max(0f, distance);
            for (var i = 1; i < points.Length; i++)
            {
                var span = Vector3.Distance(points[i - 1], points[i]);
                if (span > 1e-5f) heading = (points[i] - points[i - 1]) / span;
                if (left <= span || i == points.Length - 1)
                    return span > 1e-5f ? Vector3.Lerp(points[i - 1], points[i], Mathf.Clamp01(left / span)) : points[i];
                left -= span;
            }
            return points[points.Length - 1];
        }

        /// <summary>止まる所まで remaining m ある時の速さ。slowFor の内で slowest 倍までなだらかに緩める</summary>
        public static float SpeedAt(float remaining, float speed, float slowFor, float slowest)
        {
            if (slowFor <= 0f || remaining >= slowFor) return speed;
            var k = Mathf.Clamp01(remaining / slowFor);
            return speed * Mathf.Lerp(slowest, 1f, k * k * (3f - 2f * k));
        }

        /// <summary>歩きの周期が was から now へ進む間に、at（0〜1）を越えたか。周期の頭へ戻るのも数える</summary>
        public static bool Crossed(float was, float now, float at)
        {
            if (Mathf.Approximately(was, now)) return false;
            if (now < was) now += 1f;
            return (at > was && at <= now) || (at + 1f > was && at + 1f <= now);
        }

        /// <summary>歩いてかかる秒（緩めるぶんを含む）。演出の目安と試験に使う</summary>
        public static float Seconds(float length, float speed, float slowFor, float slowest)
        {
            if (speed <= 0f) return 0f;
            const float step = 0.01f;
            var t = 0f;
            for (var d = 0f; d < length; d += step) t += step / SpeedAt(length - d, speed, slowFor, slowest);
            return t;
        }

        // ---- 流れ -----------------------------------------------------------------

        /// <summary>視界の外の入り口から歩いてきて、止まる所で止まり、立ち姿を掛けきるまで</summary>
        public IEnumerator Enter()
        {
            Place(0f);
            gameObject.SetActive(true);
            SetStance(keepStance ? 1f : 0f);
            yield return Walk(path, true);
            // 止まった。足を揃える一歩（直前に着地していなければ）
            if (sinceStep > 0.25f) Stepped += Play(transform.position, settleStep);
            if (!keepStance)
                for (var t = 0f; t < settleSeconds; t += Time.deltaTime)
                {
                    SetStance(t / settleSeconds);
                    yield return null;
                }
            SetStance(1f);
        }

        /// <summary>立ち姿を外して、来た道を戻り、視界の外へ出たら伏せる</summary>
        public IEnumerator Leave()
        {
            if (!keepStance)
            {
                for (var t = 0f; t < looseSeconds; t += Time.deltaTime)
                {
                    SetStance(1f - t / looseSeconds);
                    yield return null;
                }
                SetStance(0f);
            }
            var back = (Vector3[])path.Clone();
            System.Array.Reverse(back);
            yield return Walk(back, false);
            gameObject.SetActive(false);
        }

        /// <summary>止まる所に、立ち姿で立たせる（思い出して売り買いの途中から始める時）</summary>
        public void Stand()
        {
            Place(Length(path));
            gameObject.SetActive(true);
            SetStance(1f);
            if (motion != null) motion.Refresh();
        }

        /// <summary>
        /// エディタで一こまを置く（確かめの絵）。distance は道筋の頭からの m。負なら止まる所に立ち姿で立たせる。
        /// 歩きの周期は進んだ距離から決め、体は進む向きへ向ける
        /// </summary>
        public void Preview(float distance)
        {
            if (motion == null || motion.Body == null) return;
            if (distance < 0f)
            {
                Place(Length(path));
                motion.Body.localRotation = Quaternion.identity;
                motion.Pose(stance, stance >= 0 ? 1f : 0f);
                motion.Sample(motion.Idle, 0f);
                return;
            }
            Vector3 heading;
            var at = Along(path, distance, out heading);
            transform.localPosition = at;
            var world = transform.parent != null ? transform.parent.TransformDirection(heading) : heading;
            world.y = 0f;
            if (world.sqrMagnitude > 1e-6f) motion.Body.rotation = Quaternion.LookRotation(world, Vector3.up);
            var stride = motion.Strides != null && motion.Strides.Length > 0 ? motion.Strides[motion.Strides.Length - 1] : 1.5f;
            motion.Pose(keepStance ? stance : -1, keepStance ? 1f : 0f);
            if (motion.Walk != null) motion.Sample(motion.Walk, Mathf.Repeat(distance / stride, 1f) * motion.Walk.length);
        }

        IEnumerator Walk(Vector3[] points, bool arriving)
        {
            var length = Length(points);
            var gone = 0f;
            walking = true;
            lastPhase = motion != null ? motion.Phase : 0f;
            sinceStep = 1f;
            try
            {
                while (gone < length)
                {
                    // 入ってくる時は止まる前に緩める。出ていく時は緩めない（視界の外へ抜ける）
                    var v = arriving ? SpeedAt(length - gone, speed, slowFor, slowest) : speed;
                    gone = Mathf.Min(length, gone + v * Time.deltaTime);
                    Vector3 heading;
                    transform.localPosition = Along(points, gone, out heading);
                    yield return null;
                }
            }
            finally
            {
                walking = false;
            }
        }

        void LateUpdate()
        {
            sinceStep += Time.deltaTime;
            if (!walking || motion == null) return;
            var now = motion.Phase;
            if (motion.Current == PersonMotion.Gait.Walk && contacts != null)
            {
                if (contacts.Length > 0 && leftFoot != null && Crossed(lastPhase, now, contacts[0])) Stepped += Play(leftFoot.position, 1f);
                if (contacts.Length > 1 && rightFoot != null && Crossed(lastPhase, now, contacts[1])) Stepped += Play(rightFoot.position, 1f);
            }
            lastPhase = now;
        }

        int Play(Vector3 at, float loud)
        {
            sinceStep = 0f;
            if (steps == null) return 1;
            at.y = transform.position.y;
            steps.Step(at, heels, loud);
            return 1;
        }

        void Place(float distance)
        {
            Vector3 heading;
            transform.localPosition = Along(path, distance, out heading);
        }

        void SetStance(float weight)
        {
            if (motion == null || stance < 0) return;
            motion.Pose(stance, Mathf.Clamp01(weight));
        }
    }
}
