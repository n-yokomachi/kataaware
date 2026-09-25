using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace HalfAware
{
    /// <summary>
    /// 記憶の中の人を動かす。立っている人は立ちの動き、歩く人は歩きの動き、座っている人は座った形。
    ///
    /// **実行時に骨で動かす。** 何こまかを焼いた mesh を差し替える作りは、一体一こまで 0.8〜0.9 MB あり、
    /// 歩く人だけで二十こま × 十人を超えるとリポジトリが百 MB 単位で増える。人ごとに骨の縮尺
    /// （年齢と体つき）が違うので、焼いた形は人の間で使い回せない。骨で動かせば、模型は一人ずつのプレハブ
    /// （Rocketbox の Humanoid）、動きは女と男で三本ずつの Humanoid の動きを使い回すので、増えるのはシーンの参照だけで済む。
    ///
    /// **PS1 らしく、こまを落として段々に動かす。** 一秒に <see cref="Fps"/> こまだけ骨を置き直し、
    /// そのあいだは姿勢も位置も止める。<see cref="Mover"/> が根を滑らかに運んでも、
    /// 模型（<see cref="body"/>）の位置はこまの頭でしか追いつかない。姿勢だけ段々で位置が滑らかだと、
    /// 着いている足がこまのあいだに前へ滑って、こまの頭で後ろへ跳ね戻る。
    ///
    /// **足の運びは進んだ距離で進める。** 歩きの周期は時計ではなく、根が進んだ m を一周期の進み
    /// （<see cref="strides"/>、組み立てで実測）に対する比で進める。Mover の速さがどうであれ、
    /// 着いている足は床に止まって見える。遅く歩く人は、歩きの動きを立ちの動きへ寄せて歩幅を詰め、
    /// その歩幅で測り直した一周期の進みを使う。速く駆ける人は走りの動きに替える。
    ///
    /// **自分の時計は有効になった瞬間から数える。** 同じ記憶へ戻ると DiveDirector は Take を
    /// 伏せてから起こし直すので、そこで動きも頭から始まる。人ごとにこまの頭を少しずらすのは
    /// （<see cref="lag"/>）、全員が同じフレームで一斉に動くと一枚の絵が跳ねて見えるから。
    /// ずらし幅は組み立てで決めた定数なので、入り直しても同じ所から始まる。
    ///
    /// **骨の縮尺と曲げは、動きを置いたあとに毎こま上書きする**（<see cref="Settle"/>）。
    /// 動きは全部の骨の位置・向き・縮尺を持っているので、置き直すたびに素の値へ戻る。
    /// 上書きは縮尺と向きをどちらも絶対の値で書くので、何度重ねても同じ形になる。
    /// **骨を identity へ戻すことはしない**（皮が裂ける。Models/LICENSES.md）
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class PersonMotion : MonoBehaviour
    {
        public enum Gait { Stand, Walk, Run }

        /// <summary>一秒のこま数</summary>
        public const float Fps = 12f;
        public const float Tick = 1f / Fps;
        /// <summary>これより遅ければ立っている。m/s</summary>
        public const float StandBelow = 0.04f;
        /// <summary>走りに移る速さ。その人の自然な歩きの速さに対する比</summary>
        public const float RunEnter = 1.7f;
        /// <summary>走りから歩きへ戻る速さ。移る速さより低くして、境目で行き来しないようにする</summary>
        public const float RunLeave = 1.35f;
        /// <summary>
        /// 歩幅の度合いの段。<see cref="strides"/> の並び。1 は歩きの動きそのまま、
        /// 小さいほど立ちの動きへ寄せて歩幅を詰める
        /// </summary>
        public static readonly float[] Reaches = { 0.35f, 0.5f, 0.65f, 0.8f, 1f };
        /// <summary>一こまで向きを変える上限。度</summary>
        public const float TurnPerTick = 35f;
        /// <summary>一フレームで進めるこまの上限。処理が詰まって何秒も飛んだときに、周期を何周も回さない</summary>
        const int MostTicks = 3;

        [SerializeField] Animator animator;
        [Tooltip("模型の根。こまの頭でだけ根の位置へ追いつかせる")]
        [SerializeField] Transform body;
        [SerializeField] AnimationClip idle;
        [SerializeField] AnimationClip walk;
        [SerializeField] AnimationClip run;
        [Tooltip("座っている。歩かない")]
        [SerializeField] bool seated;
        [Tooltip("模型の根の置き場。人の根のローカル。靴の裏が床に来る高さ")]
        [SerializeField] Vector3 seat;
        [Tooltip("歩きの一周期で進む m。Reaches の段ごと。組み立てで実測する")]
        [SerializeField] float[] strides = new float[0];
        [Tooltip("走りの一周期で進む m")]
        [SerializeField] float runStride = 2.4f;
        [Tooltip("こまの頭のずらし。0〜1 こま")]
        [SerializeField] float lag;

        [Header("骨")]
        [Tooltip("縮尺を上書きする骨。年齢と体つき")]
        [SerializeField] Transform[] sized = new Transform[0];
        [SerializeField] Vector3[] sizes = new Vector3[0];
        [Tooltip("前へ曲げる骨。背の丸みと、座った背の傾き")]
        [SerializeField] Transform[] bent = new Transform[0];
        [Tooltip("度。模型の右を軸に、正が前")]
        [SerializeField] float[] bends = new float[0];
        [Tooltip("向きを据える骨。座った脚、組んだ腕。模型の根から見た向き")]
        [SerializeField] Transform[] held = new Transform[0];
        [SerializeField] Quaternion[] holds = new Quaternion[0];
        [Tooltip("足首を脛の先へ付け直す骨。足が脛の子でない模型（Quaternius）のためで、Rocketbox の人には入れない")]
        [SerializeField] Transform[] feet = new Transform[0];
        [SerializeField] Transform[] ankles = new Transform[0];

        [Header("持ち物")]
        [Tooltip("骨に付いて動く持ち物。人の根の子に置き、こまの頭ごとに骨の所へ据え直す")]
        [SerializeField] Transform[] carried = new Transform[0];
        [SerializeField] Transform[] carriers = new Transform[0];
        [Tooltip("持ち物の置き場。骨のローカル")]
        [SerializeField] Vector3[] carryAt = new Vector3[0];
        [SerializeField] Quaternion[] carryTurn = new Quaternion[0];

        PlayableGraph graph;
        AnimationMixerPlayable mixer;
        AnimationClipPlayable[] inputs = new AnimationClipPlayable[0];
        /// <summary>有効になったばかり。根の置き場は Mover の OnEnable の後で読む</summary>
        bool fresh;
        float clock;
        float idleTime;
        float phase;
        Gait gait;
        float reach = 1f;
        Vector3 last;
        Vector3 moved;
        float travelled;
        Vector3 heldAt;
        float heldYaw;
        int ticks;

        public Transform Body { get { return body; } }
        public AnimationClip Idle { get { return idle; } }
        public AnimationClip Walk { get { return walk; } }
        public AnimationClip Run { get { return run; } }
        public bool Seated { get { return seated; } }
        public Vector3 Seat { get { return seat; } }
        public float[] Strides { get { return strides; } }
        public float RunStride { get { return runStride; } }
        public Gait Current { get { return gait; } }
        public float Phase { get { return phase; } }
        public float Reach { get { return reach; } }
        public float IdleTime { get { return idleTime; } }
        /// <summary>有効になってから置き直した回数</summary>
        public int Ticks { get { return ticks; } }

        /// <summary>その人の自然な歩きの速さ。m/s。歩きの動きのままの歩幅で一周期に進む m と、一周期の秒の比</summary>
        public float Natural
        {
            get
            {
                if (walk == null || strides == null || strides.Length == 0 || walk.length <= 0f) return 1f;
                return strides[strides.Length - 1] / walk.length;
            }
        }

        // ---- 決まり（試験から呼ぶ） ------------------------------------------------

        /// <summary>速さから、立つ・歩く・走るを選ぶ。走りから戻る速さは移る速さより低い</summary>
        public static Gait Choose(float speed, float natural, Gait was, bool canRun)
        {
            if (speed < StandBelow) return Gait.Stand;
            if (!canRun || natural <= 0f) return Gait.Walk;
            if (was == Gait.Run) return speed > natural * RunLeave ? Gait.Run : Gait.Walk;
            return speed > natural * RunEnter ? Gait.Run : Gait.Walk;
        }

        /// <summary>
        /// 歩幅の度合い。自然な速さより遅く歩く人は、歩数を落とすだけでなく歩幅も詰める。
        /// 平方根にするのは、人が遅く歩くとき歩幅と歩数の両方を少しずつ落とすから
        /// </summary>
        public static float ReachOf(float speed, float natural)
        {
            if (natural <= 0f) return 1f;
            return Mathf.Clamp(Mathf.Sqrt(Mathf.Max(0f, speed) / natural), Reaches[0], 1f);
        }

        /// <summary>歩幅の度合いでの一周期の進み。段のあいだは直線で繋ぐ</summary>
        public static float StrideAt(float[] table, float at)
        {
            if (table == null || table.Length == 0) return 1f;
            if (table.Length != Reaches.Length) return table[table.Length - 1];
            if (at <= Reaches[0]) return table[0];
            for (var i = 1; i < Reaches.Length; i++)
            {
                if (at > Reaches[i]) continue;
                var k = (at - Reaches[i - 1]) / (Reaches[i] - Reaches[i - 1]);
                return Mathf.Lerp(table[i - 1], table[i], k);
            }
            return table[table.Length - 1];
        }

        /// <summary>溜まった秒から、置き直すこまの数。こまに満たない端は次へ持ち越す</summary>
        public static int TicksIn(float clock)
        {
            return clock < Tick ? 0 : Mathf.FloorToInt(clock / Tick + 1e-4f);
        }

        // ---- 流れ -----------------------------------------------------------------

        /// <summary>
        /// 再生中は頭から流す。エディタでは立ちの動きの頭の一こまを置いて見せるだけで、動かさない。
        /// 伏せたら骨を模型の素の値へ戻す（<see cref="Unpose"/>）
        /// </summary>
        void OnEnable()
        {
            if (Application.isPlaying) Restart();
            else Still();
        }

        void OnDisable()
        {
            if (Application.isPlaying) Close();
            else Unpose();
        }

        void Update()
        {
            if (Application.isPlaying) Step(Time.deltaTime);
        }

        void LateUpdate()
        {
            if (Application.isPlaying) Late();
        }

        /// <summary>エディタで見せる形。立ちの動きの頭の一こまに、年齢と体つきと座り方を重ねたもの</summary>
        public void Still()
        {
            if (body != null && idle != null) Sample(idle, 0f);
        }

        /// <summary>
        /// 骨を模型の素の値へ戻す。エディタで記憶を伏せたときに呼ばれる。
        /// 置いた形のままシーンを保存すると、骨の数だけプレハブの上書きが残ってシーンが何 MB も太る
        /// </summary>
        public void Unpose()
        {
#if UNITY_EDITOR
            if (body == null) return;
            foreach (var t in body.GetComponentsInChildren<Transform>(true))
            {
                if (t == body) continue;
                var source = UnityEditor.PrefabUtility.GetCorrespondingObjectFromSource(t);
                if (source == null) continue;
                t.localPosition = source.localPosition;
                t.localRotation = source.localRotation;
                t.localScale = source.localScale;
            }
#endif
        }

        /// <summary>
        /// 頭から流し直す。有効になるたびに呼ばれる。エディタで確かめるときは直に呼ぶ
        /// </summary>
        public void Restart()
        {
            Open();
            idleTime = idle != null ? lag * idle.length : 0f;
            phase = 0f;
            gait = Gait.Stand;
            reach = 1f;
            clock = lag * Tick;
            travelled = 0f;
            moved = Vector3.zero;
            ticks = 0;
            fresh = true;
            Hold();
            Place();
            Evaluate();
        }

        public void Close()
        {
            if (graph.IsValid()) graph.Destroy();
            inputs = new AnimationClipPlayable[0];
        }

        /// <summary>一フレーム分。dt 秒ぶん時計を進め、こまの頭が来たら骨を置き直す</summary>
        public void Step(float dt)
        {
            if (fresh) return;
            var now = transform.position;
            var step = now - last;
            step.y = 0f;
            last = now;
            moved += step;
            travelled += step.magnitude;

            clock += dt;
            var n = TicksIn(clock);
            if (n == 0) return;
            clock -= n * Tick;
            Advance(Mathf.Min(n, MostTicks) * Tick, Mathf.Min(n, MostTicks));
            Place();
            Evaluate();
        }

        /// <summary>フレームの終わり。根が動いても模型はこまの頭の置き場に留める</summary>
        public void Late()
        {
            if (fresh)
            {
                // Mover は同じ瞬間に開始位置へ戻っているので、ここで初めて根の置き場を読む
                fresh = false;
                Hold();
                Place();
                Evaluate();
                return;
            }
            Place();
        }

        /// <summary>根の今の置き場を、模型を据える所として掴む</summary>
        void Hold()
        {
            last = transform.position;
            heldAt = transform.TransformPoint(seat);
            heldYaw = transform.eulerAngles.y;
        }

        /// <summary>こま数ぶん進める。span はそのこま数の秒</summary>
        void Advance(float span, int n)
        {
            var speed = seated ? 0f : travelled / span;
            var natural = Natural;
            gait = walk == null || seated ? Gait.Stand : Choose(speed, natural, gait, run != null);
            switch (gait)
            {
                case Gait.Walk:
                    reach = ReachOf(speed, natural);
                    phase = Mathf.Repeat(phase + travelled / Mathf.Max(0.05f, StrideAt(strides, reach)), 1f);
                    idleTime += span;
                    break;
                case Gait.Run:
                    reach = 1f;
                    phase = Mathf.Repeat(phase + travelled / Mathf.Max(0.05f, runStride), 1f);
                    break;
                default:
                    reach = 1f;
                    idleTime += span;
                    break;
            }

            // 歩いているあいだは進む向きへ体を回す。止まったら据えた向きへ戻す。
            // 根の向きは変えないので、立ち位置の向き（会話の相手を向く向き）はそのまま
            var toward = transform.eulerAngles.y;
            if (gait != Gait.Stand && moved.sqrMagnitude > 1e-6f)
                toward = Mathf.Atan2(moved.x, moved.z) * Mathf.Rad2Deg;
            heldYaw = Mathf.MoveTowardsAngle(heldYaw, toward, TurnPerTick * n);
            heldAt = transform.TransformPoint(seat);
            travelled = 0f;
            moved = Vector3.zero;
        }

        /// <summary>模型をこまの頭の置き場に据える。こまのあいだは根が動いてもここに留める</summary>
        void Place()
        {
            if (body == null) return;
            body.position = heldAt;
            body.rotation = Quaternion.Euler(0f, heldYaw, 0f);
        }

        void Open()
        {
            if (graph.IsValid()) graph.Destroy();
            if (animator == null || idle == null) return;
            graph = PlayableGraph.Create(name + ".Motion");
            graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            var output = AnimationPlayableOutput.Create(graph, "Motion", animator);
            var clips = new[] { idle, walk, run };
            mixer = AnimationMixerPlayable.Create(graph, clips.Length);
            inputs = new AnimationClipPlayable[clips.Length];
            for (var i = 0; i < clips.Length; i++)
            {
                if (clips[i] == null) continue;
                inputs[i] = AnimationClipPlayable.Create(graph, clips[i]);
                inputs[i].SetApplyFootIK(false);
                graph.Connect(inputs[i], 0, mixer, i);
            }
            output.SetSourcePlayable(mixer);
            graph.Play();
        }

        /// <summary>今の歩き方と周期で骨を置き、年齢・体つき・座り方で上書きする</summary>
        void Evaluate()
        {
            if (!graph.IsValid()) return;
            var wIdle = 0f;
            var wWalk = 0f;
            var wRun = 0f;
            switch (gait)
            {
                case Gait.Walk: wWalk = reach; wIdle = 1f - reach; break;
                case Gait.Run: wRun = 1f; break;
                default: wIdle = 1f; break;
            }
            Set(0, idle, wIdle, idle != null ? Mathf.Repeat(idleTime, idle.length) : 0f);
            Set(1, walk, wWalk, walk != null ? phase * walk.length : 0f);
            Set(2, run, wRun, run != null ? phase * run.length : 0f);
            graph.Evaluate(0f);
            Settle();
            ticks++;
        }

        void Set(int i, AnimationClip clip, float weight, float time)
        {
            if (clip == null || i >= inputs.Length || !inputs[i].IsValid())
            {
                if (i < mixer.GetInputCount()) mixer.SetInputWeight(i, 0f);
                return;
            }
            mixer.SetInputWeight(i, weight);
            inputs[i].SetTime(time);
        }

        /// <summary>
        /// 動きを置いたあとの上書き。縮尺 → 背の曲げ → 据えた向き → 足首の順。
        /// 据えた腕は背を曲げたあとに置くので、背の丸みにつられない。
        /// 足首は脚が決まったあとで脛の先へ付け直す
        /// </summary>
        public void Settle()
        {
            for (var i = 0; i < sized.Length && i < sizes.Length; i++)
                if (sized[i] != null) sized[i].localScale = sizes[i];
            if (body != null)
            {
                var right = body.right;
                for (var i = 0; i < bent.Length && i < bends.Length; i++)
                    if (bent[i] != null) bent[i].rotation = Quaternion.AngleAxis(bends[i], right) * bent[i].rotation;
                var frame = body.rotation;
                for (var i = 0; i < held.Length && i < holds.Length; i++)
                    if (held[i] != null) held[i].rotation = frame * holds[i];
            }
            for (var i = 0; i < feet.Length && i < ankles.Length; i++)
                if (feet[i] != null && ankles[i] != null) feet[i].position = ankles[i].position;
            for (var i = 0; i < carried.Length && i < carriers.Length && i < carryAt.Length && i < carryTurn.Length; i++)
            {
                if (carried[i] == null || carriers[i] == null) continue;
                carried[i].SetPositionAndRotation(carriers[i].TransformPoint(carryAt[i]), carriers[i].rotation * carryTurn[i]);
            }
        }

        /// <summary>
        /// 持ち物を骨に付ける。組み立てから、形を置いたところで呼ぶ。今の置き場を骨から見た置き場として覚える。
        ///
        /// **骨の子にはしない。** DiveDirector は人の最初のレンダラーの広がりで頭と胸を狙うが、
        /// 模型によっては骨組みがレンダラーより先に並ぶ（W_Formal）。骨の子にした袋が最初に見つかり、
        /// 袋の広がりで頭を狙って、壁越しでなくても選べなくなった。持ち物は人の根の子に置き、
        /// 模型より後ろに並べる
        /// </summary>
        public void Carry(Transform item, Transform bone)
        {
            if (item == null || bone == null) return;
            var n = carried.Length;
            System.Array.Resize(ref carried, n + 1);
            System.Array.Resize(ref carriers, n + 1);
            System.Array.Resize(ref carryAt, n + 1);
            System.Array.Resize(ref carryTurn, n + 1);
            carried[n] = item;
            carriers[n] = bone;
            carryAt[n] = bone.InverseTransformPoint(item.position);
            carryTurn[n] = Quaternion.Inverse(bone.rotation) * item.rotation;
        }

        /// <summary>
        /// エディタで一こまを置く。確かめ用。再生していなくても、組み立てのあとの絵や
        /// 測りに使う。clip を time 秒で置き、上書きまで済ませる
        /// </summary>
        public void Sample(AnimationClip clip, float time)
        {
            if (clip == null || body == null) return;
            clip.SampleAnimation(body.gameObject, time);
            Settle();
        }
    }
}
