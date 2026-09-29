using UnityEngine;

namespace HalfAware
{
    /// <summary>
    /// 場面 2 の群衆の、その場の動き（オーナー、2026-09-30「モブに動きがなさ過ぎて流石に違和感がある」）。
    /// 立っている人と座っている人に、息（胸と肩が上下する）・重心の移し（背の左右の傾き）・辺りを見る首の振り・
    /// 小さなしぐさ（手首の端末を指で叩く、煙草を口へ運ぶ）を、人ごとに周期と始まりをずらして付ける。
    ///
    /// **焼いた形を骨で少しだけ曲げる。** 群衆は組み立てで姿勢に曲げて焼いてある（<c>BuildAlleyCrowd</c>）。その焼いた形に、
    /// 腰・胴・首・頭・腕の 14 本の骨（<see cref="Slot"/>）の重みを付けておき（脚と指はそれぞれ腰と手の骨にまとめる）、
    /// 骨を焼いた姿勢から少しだけ回して、形を焼き直す（<see cref="SkinnedMeshRenderer.BakeMesh(Mesh)"/>）。
    /// 焼き直すのは一秒に <see cref="Fps"/> こまだけ（PS1 らしく段々に動く。<see cref="PersonMotion"/> と同じ考え）で、
    /// 近さの段の近い方（LOD0）が画面に写っている人だけ。遠い人・画面の外の人は動かさない（焼いた姿勢のまま）。
    ///
    /// しぐさの形（腕の骨の向き）は組み立てで腕を狙いへ届かせて決めてあり（<see cref="gesture"/>）、ここでは焼いた姿勢との間を行き来するだけ
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CrowdIdle : MonoBehaviour
    {
        /// <summary>一秒のこま数</summary>
        public const float Fps = 10f;
        public const float Tick = 1f / Fps;

        /// <summary>骨の並び。bones と rest と gesture はこの順</summary>
        public enum Slot
        {
            Hips, Spine, Chest, UpperChest, Neck, Head,
            LeftShoulder, LeftUpperArm, LeftLowerArm, LeftHand,
            RightShoulder, RightUpperArm, RightLowerArm, RightHand,
        }

        public const int Slots = 14;

        /// <summary>しぐさ</summary>
        public enum Kind
        {
            /// <summary>しぐさは無い（息・重心・首だけ）</summary>
            None,
            /// <summary>左の手首の端末を右の指で叩く（手首の端末を見る人）</summary>
            Tap,
            /// <summary>右の手の煙草を口へ運ぶ</summary>
            Smoke,
        }

        [Tooltip("形を焼き直す元。描かない（enabled を切る）")]
        [SerializeField] SkinnedMeshRenderer skin;
        [Tooltip("焼き直した形を描く所（近さの段の近い方）")]
        [SerializeField] MeshFilter view;
        [SerializeField] Transform[] bones = new Transform[0];
        [Tooltip("焼いた姿勢の骨の向き（親から見た向き）。Slot の順")]
        [SerializeField] Quaternion[] rest = new Quaternion[0];
        [Tooltip("しぐさの形の骨の向き。Slot の順。しぐさが無ければ空")]
        [SerializeField] Quaternion[] gesture = new Quaternion[0];
        [SerializeField] Kind kind;
        [Tooltip("座っている。重心は移さない")]
        [SerializeField] bool seated;

        [Header("息")]
        [SerializeField] float breathPeriod = 4.2f;
        [Tooltip("胸を反らす角。度")]
        [SerializeField] float breathDegrees = 1.4f;
        [Tooltip("肩を上げる角。度")]
        [SerializeField] float shoulderDegrees = 1.2f;

        [Header("重心の移し")]
        [SerializeField] float swayPeriod = 9f;
        [Tooltip("背を左右へ傾ける角。度")]
        [SerializeField] float swayDegrees = 2.2f;

        [Header("辺りを見る")]
        [SerializeField] float lookPeriod = 7f;
        [Tooltip("首を左右へ振る角。度")]
        [SerializeField] float lookDegrees = 22f;
        [Tooltip("首を上下へ振る角。度")]
        [SerializeField] float nodDegrees = 5f;

        [Header("しぐさ")]
        [SerializeField] float gesturePeriod = 11f;
        [Tooltip("しぐさの形で止める秒")]
        [SerializeField] float gestureHold = 1.4f;
        [Tooltip("しぐさの形へ動く秒")]
        [SerializeField] float gestureMove = 0.7f;

        [Header("人ごとのずらし")]
        [Tooltip("秒。周期の始まりをずらす")]
        [SerializeField] float phase;
        [Tooltip("首の振り先と重心の移し先を決める種")]
        [SerializeField] int seed;
        [Tooltip("これより遠ければ動かさない。m")]
        [SerializeField] float near = 16f;

        Mesh baked;
        Renderer shown;
        float clock;
        float time;

        /// <summary>焼き直した回数。動作確認から読む</summary>
        public int Ticks { get; private set; }

        public Kind Gesture { get { return kind; } }

        // ---- 決まり（試験から呼ぶ） ------------------------------------------------

        /// <summary>息。-1〜1。period 秒の正弦</summary>
        public static float Breath(float t, float period)
        {
            return period <= 0f ? 0f : Mathf.Sin(t * 2f * Mathf.PI / period);
        }

        /// <summary>
        /// 止まっては移る振れ。-1〜1。period 秒ごとに種 seed から次の先を引き、周期の終わりの move 割で滑らかに移る。
        /// 人は首も重心も、ずっと揺れ続けるのではなく、ある向きで止まっていて時々移す
        /// </summary>
        public static float Shift(float t, float period, int seed, float move)
        {
            if (period <= 0f) return 0f;
            var k = Mathf.FloorToInt(t / period);
            var u = t / period - k;
            var a = Hash(seed, k);
            var b = Hash(seed, k + 1);
            move = Mathf.Clamp(move, 0.01f, 1f);
            var w = Mathf.Clamp01((u - (1f - move)) / move);
            return Mathf.Lerp(a, b, w * w * (3f - 2f * w));
        }

        /// <summary>種と番号から -1〜1 の数（毎回同じ）</summary>
        public static float Hash(int seed, int k)
        {
            unchecked
            {
                var h = (uint)(seed * 73856093) ^ (uint)(k * 19349663) ^ 0x9E3779B9u;
                h ^= h >> 16;
                h *= 0x7FEB352Du;
                h ^= h >> 15;
                h *= 0x846CA68Bu;
                h ^= h >> 16;
                return (h & 0xFFFFFF) / (float)0xFFFFFF * 2f - 1f;
            }
        }

        /// <summary>
        /// しぐさの効き。0〜1。period 秒ごとに、move 秒で形へ動き、hold 秒止め、move 秒で戻る。残りは焼いた姿勢のまま
        /// </summary>
        public static float Envelope(float t, float period, float move, float hold)
        {
            if (period <= 0f) return 0f;
            var u = Mathf.Repeat(t, period);
            move = Mathf.Max(0.01f, move);
            float w;
            if (u < move) w = u / move;
            else if (u < move + hold) w = 1f;
            else if (u < move * 2f + hold) w = 1f - (u - move - hold) / move;
            else w = 0f;
            return w * w * (3f - 2f * w);
        }

        /// <summary>溜まった秒から、焼き直すこまがあるか</summary>
        public static bool Due(float clock)
        {
            return clock >= Tick;
        }

        // ---- 流れ -----------------------------------------------------------------

        void Awake()
        {
            if (view != null) shown = view.GetComponent<Renderer>();
            time = phase;
            // 焼き直すこまの頭を人ごとにずらす。全員が同じフレームで焼き直すと、そのフレームだけ重くなる
            clock = Mathf.Repeat(phase, Tick);
        }

        void OnDestroy()
        {
            if (baked != null) Destroy(baked);
        }

        void Update()
        {
            var dt = Time.deltaTime;
            time += dt;
            clock += dt;
            if (!Due(clock)) return;
            clock = Mathf.Repeat(clock, Tick);
            // 近さの段の近い方が写っている時だけ。遠い段・画面の外では焼いた姿勢のまま
            if (shown == null || !shown.isVisible || skin == null || bones.Length < Slots || rest.Length < Slots) return;
            var cam = Camera.main;
            if (cam != null && (cam.transform.position - transform.position).sqrMagnitude > near * near) return;
            Pose(time);
            if (baked == null)
            {
                baked = new Mesh { name = name + "_Idle" };
                baked.MarkDynamic();
            }
            skin.BakeMesh(baked);
            if (view.sharedMesh != baked) view.sharedMesh = baked;
            Ticks++;
        }

        /// <summary>骨を t 秒の形に置く。焼いた姿勢へ戻してから、しぐさ・息・重心・首の順に重ねる</summary>
        public void Pose(float t)
        {
            for (var i = 0; i < Slots && i < bones.Length; i++)
                if (bones[i] != null) bones[i].localRotation = rest[i];
            if (kind != Kind.None && gesture.Length >= Slots)
            {
                var w = Envelope(t, gesturePeriod, gestureMove, gestureHold);
                if (w > 0f)
                    for (var i = (int)Slot.LeftShoulder; i < Slots; i++)
                        if (bones[i] != null) bones[i].localRotation = Quaternion.Slerp(rest[i], gesture[i], w);
            }
            var right = transform.right;
            var up = transform.up;
            var fwd = transform.forward;
            var breath = Breath(t, breathPeriod);
            var sway = seated ? 0f : Shift(t + 3.1f, swayPeriod, seed + 17, 0.35f) * swayDegrees;
            // 重心を移すと、腰の上で背が傾き、頭は起こして前を見たままにする
            Turn(Slot.Spine, fwd, sway);
            // 吸うと胸が起きて肩が上がる
            Turn(Slot.Chest, right, -breath * breathDegrees);
            Turn(Slot.LeftShoulder, fwd, -breath * shoulderDegrees);
            Turn(Slot.RightShoulder, fwd, breath * shoulderDegrees);
            // 辺りを見る。手首の端末を見ている人は、端末から大きくは目を離さない
            var look = kind == Kind.Tap ? 0.2f : 1f;
            var yaw = Shift(t, lookPeriod, seed, 0.3f) * lookDegrees * look;
            var nod = Shift(t + 1.7f, lookPeriod * 1.3f, seed + 5, 0.3f) * nodDegrees * look;
            // 首の骨は回さない（Rocketbox の骨組みは鎖骨が首の子で、首を回すと腕ごと振れる）。振るのは頭だけ
            Turn(Slot.Head, up, yaw);
            Turn(Slot.Head, right, nod);
            Turn(Slot.Head, fwd, -sway * 0.8f);
        }

        void Turn(Slot slot, Vector3 axis, float degrees)
        {
            var b = bones[(int)slot];
            if (b == null || Mathf.Abs(degrees) < 1e-4f) return;
            b.rotation = Quaternion.AngleAxis(degrees, axis) * b.rotation;
        }
    }
}
