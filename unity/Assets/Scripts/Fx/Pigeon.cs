using UnityEngine;

namespace HalfAware
{
    /// <summary>
    /// 記憶の中の鳩を一羽動かす。地面ではついばむ・首を振って歩く・首を傾げる・羽を震わせるを
    /// 一羽ずつずらして繰り返し、主や歩く人が寄ってくると歩いて離れるか、短く飛んで降りる。
    /// 飛び立つ出来事（「鳩がいっせいに飛んだ」）では羽ばたいて斜めに上がり、空へ散る。
    ///
    /// **出来事の秒は <see cref="Mover"/> が持つ。** DiveDirector が人と同じ口（<see cref="Mover.Play(float, float)"/>）で
    /// 記憶の時計を渡すので、鳩の Mover はそれを <see cref="Cue"/> へ回すだけで、自分では置かない。
    /// Mover の一本目の線が飛び立ち（<see cref="Leg"/>）、二本目の線が空から餌へ降りてくる所（記憶 10 の門の前）。
    ///
    /// **地面の暮らしは自分の時計で動かす。** 合図を持つ鳩の Mover には、合図が来るまで 0 秒しか渡らないので、
    /// その時計ではついばみが止まる。有効になった瞬間から自分で数える。思い出して戻ったときに
    /// ついばみの頭が揃わなくても困らない。飛び立ちと降りてくる所は記憶の時計の方で決まる。
    ///
    /// **PS1 らしく、人と同じ 12 こまで段々に動かす**（<see cref="PersonMotion.Fps"/>）。
    /// 形は骨ではなく、姿勢ごとに焼いた mesh（<see cref="poses"/>）の差し替え。羽ばたきは翼の三枚の切り替え。
    /// こまのあいだは位置も形も止める。鳩ごとにこまの頭をずらす（<see cref="lag"/>）。
    ///
    /// 位置は Take のローカル（鳩の親の Doves は Take と同じ置き場）。
    /// エディタでは Mover から渡った秒の所へ根を置くだけで、形は替えない（確かめの撮影の後で根を戻せば、シーンは汚れない）
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class Pigeon : MonoBehaviour
    {
        /// <summary>焼いた形の並び。<see cref="poses"/> の番号</summary>
        public enum Shape { Stand, StepBack, StepFore, Peck, Cock, Ruffle, FlapUp, FlapMid, FlapDown }

        /// <summary>Mover の一本目の線を、この鳩がどう飛ぶか</summary>
        public enum Leg
        {
            /// <summary>飛び立たない。線は見ない</summary>
            Stays,
            /// <summary>羽ばたいて斜めに上がり、線の先からさらに遠くへ散って消える</summary>
            Leaves,
            /// <summary>短く飛んで線の先へ降り、そこでまた地面の暮らしに戻る（撒いた餌へ寄ってくる）</summary>
            Lands,
        }

        enum Act { None, Peck, Walk, Look, Stand, Ruffle, Flee, Hop }

        public const float Fps = PersonMotion.Fps;
        public const float Tick = PersonMotion.Tick;
        /// <summary>歩く速さ。m/s</summary>
        public const float WalkSpeed = 0.30f;
        /// <summary>逃げて歩く速さ。m/s</summary>
        public const float FleeSpeed = 0.65f;
        /// <summary>主がこれより近ければ、止まっていても短く飛んで離れる。m</summary>
        public const float TooClose = 0.45f;
        /// <summary>主や人が歩いて寄ってくると、これより近くで歩いて離れる。m</summary>
        public const float Wary = 1.2f;
        /// <summary>寄ってくると見なす速さ。m/s。立ち止まって話している人には逃げない</summary>
        public const float Coming = 0.22f;
        /// <summary>飛び立ってから、これだけの秒で見えなくする。遠い空の点を出し続けない</summary>
        public const float GoneAfter = 5f;
        /// <summary>一こまで向きを変える上限。度</summary>
        const float TurnPerTick = 40f;
        /// <summary>一フレームで進めるこまの上限</summary>
        const int MostTicks = 3;

        [Tooltip("姿勢ごとの形。Shape の並び")]
        [SerializeField] Mesh[] poses = new Mesh[0];
        [Tooltip("Mover の一本目の線の飛び方")]
        [SerializeField] Leg leg = Leg.Stays;
        [Tooltip("ついばむ・歩くの並びを決める種。鳩ごとに違える")]
        [SerializeField] int seed;
        [Tooltip("こまの頭のずらし。0〜1 こま")]
        [SerializeField] float lag;
        [Tooltip("始まりの向き。度。Take のローカル")]
        [SerializeField] float yaw;
        [Tooltip("居場所。Mover を持たない（飛び立たない）鳩だけが使う。Take のローカル")]
        [SerializeField] Vector3 spot;
        [Tooltip("居場所のまわりを歩き回る半径。m")]
        [SerializeField] float roam = 0.45f;
        [Tooltip("入らない楕円（池）。x・z が中心、z・w が x と z の半径。半径が 0 なら無い")]
        [SerializeField] Vector4 pool;
        [Tooltip("出ない四角（公園の柵の内）。x 最小・x 最大・z 最小・z 最大。幅が 0 なら無い")]
        [SerializeField] Vector4 yard;

        /// <summary>
        /// 主の目。null ならメインカメラ。エディタの確かめで、一時のカメラを主の代わりに立てるときに使う
        /// </summary>
        public static Transform Watcher;

        Mover mover;
        MeshFilter filter;
        Renderer look;
        Transform[] people = new Transform[0];
        /// <summary>人と主の、前に見た所と速さ（m/s）。こまの頭ごとに、前に見てから経った秒で測り直す</summary>
        Vector3[] peopleWas = new Vector3[0];
        Vector3[] peopleVel = new Vector3[0];
        Vector3 watcherAt;
        Vector3 watcherVel;
        bool watcherKnown;
        bool watcherSeen;
        float sinceLook;

        /// <summary>Mover から渡った記憶の時計。一本目の秒と、二本目の合図からの秒（合図の前は負）</summary>
        float legT;
        float legAfter = -1f;
        bool fresh = true;
        float clock;
        int ticks;
        System.Random rng;

        Vector3 pos;
        Vector3 home;
        float heading;
        float pitch;
        Shape shape;
        bool gone;

        Act act;
        int actLeft;
        int actStep;
        Vector3 target;
        float speed;
        /// <summary>短く飛ぶときの出どころと行き先、秒</summary>
        Vector3 hopFrom, hopTo;
        float hopSpan, hopRise, hopClock;
        int calm;

        /// <summary>どこで線の上にいるか</summary>
        enum Phase { Ground, Away, Landing, Settled, Arriving, Back }
        Phase phase = Phase.Ground;
        /// <summary>飛び立った所。飛び立つ瞬間の立ち位置</summary>
        Vector3 lift;

        public Leg Flight { get { return leg; } }
        public Shape Current { get { return shape; } }
        public Vector3 Ground { get { return pos; } }
        public bool Gone { get { return gone; } }
        public string Doing { get { return phase == Phase.Ground || phase == Phase.Settled || phase == Phase.Back ? act.ToString() : phase.ToString(); } }
        public int Ticks { get { return ticks; } }

        Mover Lines { get { if (mover == null) mover = GetComponent<Mover>(); return mover; } }

        void OnEnable()
        {
            fresh = true;
            gone = false;
            if (look == null) look = GetComponent<Renderer>();
            if (look != null && Application.isPlaying) look.enabled = true;
            Gather();
        }

        /// <summary>
        /// 寄ってくると逃げる相手を拾う。人は同じ記憶の中の者だけ。座っている人も入れるが、動いていなければ逃げない。
        /// エディタでは OnEnable が呼ばれないので、頭から数え直すとき（<see cref="Begin"/>）にも拾う
        /// </summary>
        void Gather()
        {
            var take = GetComponentInParent<Take>(true);
            var found = take != null ? take.GetComponentsInChildren<PersonMotion>(true) : new PersonMotion[0];
            people = new Transform[found.Length];
            for (var i = 0; i < found.Length; i++) people[i] = found[i].transform;
            peopleWas = new Vector3[people.Length];
            peopleVel = new Vector3[people.Length];
            gathered = true;
        }
        bool gathered;

        void Update()
        {
            if (Application.isPlaying) Step(Time.deltaTime);
        }

        /// <summary>
        /// Mover から記憶の時計を受け取る。一本目の秒 t と、二本目の合図からの秒 after（合図の前は負）。
        /// 再生中は受け取るだけで、置くのはこまの頭（<see cref="Step"/>）。
        /// エディタでは、その秒の所へ根だけ置く（立った形のまま）
        /// </summary>
        public void Cue(float t, float after)
        {
            // 時計が戻った（有効になり直した、同じ人へ戻った）なら頭から
            if (t < legT - 1e-3f) fresh = true;
            legT = t;
            legAfter = after;
            if (!Application.isPlaying) Still();
        }

        /// <summary>エディタで見せる所。記憶の時計の秒の所へ根を置く。形は替えない</summary>
        public void Still()
        {
            if (fresh) Begin();
            Apply(false, Where());
        }

        /// <summary>一フレーム分。dt 秒ぶん数え、こまの頭が来たら動かす。エディタの確かめからも呼ぶ</summary>
        public void Step(float dt)
        {
            if (fresh) { Begin(); clock = lag * Tick; }
            clock += dt;
            sinceLook += dt;
            var n = PersonMotion.TicksIn(clock);
            if (n > 0)
            {
                clock -= n * Tick;
                Look();
                for (var i = 0; i < Mathf.Min(n, MostTicks); i++) Advance();
            }
            Apply(true, phase);
        }

        // ---- 始まり ------------------------------------------------------------

        /// <summary>頭から。記憶の時計の今の所で、地面にいるか、空にいるか、降りた後かを決めて置く</summary>
        void Begin()
        {
            fresh = false;
            if (!gathered) Gather();
            rng = new System.Random(seed * 7919 + 17);
            ticks = 0;
            calm = 0;
            act = Act.None;
            actLeft = 0;
            heading = yaw;
            pitch = 0f;
            shape = Shape.Stand;
            gone = false;
            watcherKnown = false;
            sinceLook = 0f;
            for (var i = 0; i < people.Length; i++)
            {
                peopleWas[i] = people[i] != null ? Local(people[i].position) : Vector3.zero;
                peopleVel[i] = Vector3.zero;
            }
            var m = Lines;
            home = m != null ? m.From : spot;
            pos = home;
            lift = home;
            phase = Where();
            float s;
            switch (phase)
            {
                case Phase.Settled: home = m.To; pos = home; break;
                case Phase.Back: home = m.Next; pos = home; break;
                case Phase.Away:
                    s = Along();
                    if ((s - 1f) * m.Span > GoneAfter) gone = true;
                    break;
            }
        }

        // ---- 線の上のどこか --------------------------------------------------------

        /// <summary>記憶の時計の今の所で、線のどこにいるか</summary>
        Phase Where()
        {
            var m = Lines;
            if (m == null) return Phase.Ground;
            var second = m.NextCue < 0 ? legT : legAfter;
            if (m.Returns && second >= m.NextAt)
                return second < m.NextAt + m.NextSpan ? Phase.Arriving : Phase.Back;
            if (leg == Leg.Stays || legT < m.At) return Phase.Ground;
            if (leg == Leg.Lands) return legT < m.At + m.Span ? Phase.Landing : Phase.Settled;
            return Phase.Away;
        }

        /// <summary>一本目の線の進み。0〜1、飛び立ちの後はそれを越える</summary>
        float Along()
        {
            var m = Lines;
            return m.Span > 0f ? (legT - m.At) / m.Span : 1f;
        }

        // ---- こま -----------------------------------------------------------------

        void Advance()
        {
            ticks++;
            var was = phase;
            phase = Where();
            var m = Lines;
            if (phase != was)
            {
                // 地面から飛び立つ瞬間の立ち位置から上がる
                if (phase == Phase.Away || phase == Phase.Landing) { lift = pos; gone = false; }
                if (phase == Phase.Settled) { home = m.To; pos = home; act = Act.None; heading = Toward(lift, m.To); }
                if (phase == Phase.Back) { home = m.Next; pos = home; act = Act.None; heading = Toward(Sky(), m.Next); gone = false; }
                if (phase == Phase.Arriving) gone = false;
            }
            switch (phase)
            {
                case Phase.Away: Away(); break;
                case Phase.Landing: Landing(); break;
                case Phase.Arriving: Arriving(); break;
                default: Live(); break;
            }
        }

        /// <summary>羽ばたいて斜めに上がり、線の先からさらに遠くへ散る</summary>
        void Away()
        {
            var m = Lines;
            var s = Along();
            var e = (s - 1f) * m.Span;
            if (e > GoneAfter) { gone = true; return; }
            heading = Toward(lift, Aim());
            // 上がるあいだは三枚を休まず回す。上がり切ったら二回打って少し滑る
            shape = s < 1f || (ticks % 7) < 4 ? Flap(ticks) : Shape.FlapMid;
            pitch = s < 1f ? Mathf.Lerp(-38f, -14f, s) : -8f;
        }

        /// <summary>短く飛んで線の先へ降りる（撒いた餌へ）</summary>
        void Landing()
        {
            var m = Lines;
            var k = Mathf.Clamp01(Along());
            heading = Toward(lift, m.To);
            shape = k > 0.8f ? (ticks % 2 == 0 ? Shape.FlapUp : Shape.FlapDown) : Flap(ticks);
            pitch = k < 0.2f ? -30f : k > 0.8f ? -32f : -6f;
        }

        /// <summary>空から線の先（撒いた餌）へ降りてくる。滑って来て、足元の手前で羽を立てて止まる</summary>
        void Arriving()
        {
            var m = Lines;
            var k = Arrival();
            heading = Toward(Sky(), m.Next);
            if (k < 0.6f) { shape = (ticks % 6) < 2 ? Flap(ticks) : Shape.FlapMid; pitch = 6f; }
            else { shape = ticks % 2 == 0 ? Shape.FlapUp : Shape.FlapDown; pitch = -34f; }
        }

        float Arrival()
        {
            var m = Lines;
            var second = m.NextCue < 0 ? legT : legAfter;
            return m.NextSpan > 0f ? Mathf.Clamp01((second - m.NextAt) / m.NextSpan) : 1f;
        }

        /// <summary>
        /// 降りてくる前にいる空の所。元いた所（飛び立った居場所）の方から来る。
        /// 飛んで行った方から戻すと、門の前で撒く主の背の側から降りてきて、寄ってくる所が見えない
        /// </summary>
        Vector3 Sky()
        {
            var m = Lines;
            var back = m.From - m.Next;
            back.y = 0f;
            if (back.sqrMagnitude < 1e-4f) back = Vector3.forward;
            return m.Next + back.normalized * 6f + Vector3.up * 4f;
        }

        static Shape Flap(int tick)
        {
            switch (tick % 3)
            {
                case 0: return Shape.FlapUp;
                case 1: return Shape.FlapDown;
                default: return Shape.FlapMid;
            }
        }

        // ---- 地面の暮らし --------------------------------------------------------------

        void Live()
        {
            pitch = 0f;
            if (act != Act.Hop) Startle();
            if (act == Act.None || actLeft <= 0) Choose();
            actLeft--;
            actStep++;
            switch (act)
            {
                case Act.Peck:
                    // 下げて二こま、上げて一〜二こま。ときどき半歩前へ
                    var beat = actStep % 4;
                    shape = beat == 1 || beat == 2 ? Shape.Peck : Shape.Stand;
                    if (beat == 3 && rng.NextDouble() < 0.25) { pos += Ahead() * 0.025f; shape = Shape.StepFore; }
                    break;
                case Act.Walk:
                case Act.Flee:
                    Walk();
                    break;
                case Act.Look:
                    shape = actStep <= 2 ? Shape.Stand : Shape.Cock;
                    if (actStep <= 2) heading += target.x;
                    break;
                case Act.Stand:
                    shape = Shape.Stand;
                    break;
                case Act.Ruffle:
                    shape = actStep < 7 ? (actStep % 2 == 0 ? Shape.Ruffle : Shape.Stand) : actStep < 9 ? Shape.Ruffle : Shape.Stand;
                    break;
                case Act.Hop:
                    Hop();
                    break;
            }
        }

        /// <summary>次にすることを選ぶ。ついばむのがいちばん多い</summary>
        void Choose()
        {
            actStep = 0;
            var r = rng.NextDouble();
            if (r < 0.38) { act = Act.Peck; actLeft = 16 + rng.Next(32); }
            else if (r < 0.68)
            {
                act = Act.Walk;
                speed = WalkSpeed * (0.8f + (float)rng.NextDouble() * 0.4f);
                // 居場所のまわりへ。今いる所から近すぎない所
                for (var i = 0; i < 6; i++)
                {
                    var a = (float)rng.NextDouble() * Mathf.PI * 2f;
                    var d = roam * Mathf.Sqrt((float)rng.NextDouble());
                    target = Keep(home + new Vector3(Mathf.Cos(a) * d, 0f, Mathf.Sin(a) * d));
                    if (Flat(target - pos) > 0.15f) break;
                }
                actLeft = Mathf.CeilToInt(Flat(target - pos) / (speed * Tick)) + 4;
            }
            else if (r < 0.82)
            {
                act = Act.Look;
                actLeft = 8 + rng.Next(12);
                // 向き直る量。最初の二こまで回す
                target = new Vector3(((float)rng.NextDouble() * 2f - 1f) * 35f, 0f, 0f);
            }
            else if (r < 0.92) { act = Act.Stand; actLeft = 6 + rng.Next(12); }
            else { act = Act.Ruffle; actLeft = 12; }
        }

        /// <summary>首を振って歩く。頭を突き出して二こま、引いて二こま（逃げるときは一こまずつ）。体は毎こま進む</summary>
        void Walk()
        {
            var to = target - pos;
            to.y = 0f;
            var want = Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg;
            var off = Mathf.Abs(Mathf.DeltaAngle(heading, want));
            heading = Mathf.MoveTowardsAngle(heading, want, TurnPerTick * (act == Act.Flee ? 2f : 1f));
            var run = act == Act.Flee ? FleeSpeed : speed;
            if (to.magnitude < 0.02f) { act = Act.None; actLeft = 0; shape = Shape.Stand; return; }
            // 向きが大きく違えば、その場で向き直ってから歩き出す
            if (off > 70f) { shape = Shape.Stand; return; }
            pos = Keep(pos + to.normalized * Mathf.Min(to.magnitude, run * Tick));
            var beat = act == Act.Flee ? actStep % 2 : (actStep / 2) % 2;
            shape = beat == 0 ? Shape.StepFore : Shape.StepBack;
        }

        /// <summary>短く飛んで降りる。羽ばたいて小さな弧を描き、最後の二こまは羽を立てて止まる</summary>
        void Hop()
        {
            hopClock += Tick;
            var k = Mathf.Clamp01(hopClock / hopSpan);
            heading = Toward(hopFrom, hopTo);
            if (k >= 1f)
            {
                pos = hopTo;
                act = Act.Stand;
                actLeft = 4 + rng.Next(6);
                actStep = 0;
                shape = Shape.Stand;
                pitch = 0f;
                return;
            }
            shape = k > 0.72f ? (ticks % 2 == 0 ? Shape.FlapUp : Shape.FlapDown) : Flap(ticks);
            pitch = k < 0.25f ? -26f : k > 0.72f ? -30f : -4f;
        }

        /// <summary>
        /// 寄ってくる者を見る。主が足元まで来たら短く飛んで離れ、歩いて寄ってくる主や人からは歩いて離れる。
        /// 立ち止まっている人のそばでは逃げない
        /// </summary>
        void Startle()
        {
            if (calm > 0) calm--;
            if (watcherSeen && Threat(watcherAt, watcherVel, true)) return;
            for (var i = 0; i < people.Length; i++)
                if (people[i] != null && Threat(peopleWas[i], peopleVel[i], false)) return;
        }

        /// <summary>
        /// 主と人の今の所と速さを測る。こまの頭ごとに、飛んでいるあいだも測り続ける。
        /// 地面にいるときだけ測っていた頃は、飛んで戻ってきた鳩が、飛び立つ前の主の所から今の所までを
        /// 一こまで寄ってきたと見て、撒いた主の足元でもないのに降りた途端に飛び退いた
        /// </summary>
        void Look()
        {
            var span = Mathf.Max(sinceLook, 1e-3f);
            sinceLook = 0f;
            var eye = Watcher != null ? Watcher : (Camera.main != null ? Camera.main.transform : null);
            watcherSeen = eye != null;
            if (watcherSeen)
            {
                var at = Local(eye.position);
                watcherVel = watcherKnown ? (at - watcherAt) / span : Vector3.zero;
                watcherAt = at;
                watcherKnown = true;
            }
            for (var i = 0; i < people.Length; i++)
            {
                if (people[i] == null) continue;
                var at = Local(people[i].position);
                peopleVel[i] = (at - peopleWas[i]) / span;
                peopleWas[i] = at;
            }
        }

        /// <summary>一人ぶん見る。<paramref name="vel"/> はその人の速さ（m/s）。逃げ出したら true</summary>
        bool Threat(Vector3 at, Vector3 vel, bool player)
        {
            var from = pos - at;
            from.y = 0f;
            var d = from.magnitude;
            if (d > Wary) return false;
            var move = vel;
            move.y = 0f;
            var v = move.magnitude;
            var coming = v > Coming && Vector3.Dot(move, from) > 0f;
            var close = d < TooClose && (player || v > Coming);
            if (!close && (!coming || calm > 0 || act == Act.Flee)) return false;
            var away = d > 1e-3f ? from / d : Ahead() * -1f;
            // まっすぐ後ろではなく、少し横へ逸れる
            var swing = ((float)rng.NextDouble() * 2f - 1f) * 35f;
            away = Quaternion.Euler(0f, swing, 0f) * away;
            if (close || (coming && d < TooClose + 0.25f))
            {
                hopFrom = pos;
                hopTo = Keep(pos + away * (1.4f + (float)rng.NextDouble() * 0.8f));
                hopSpan = 0.55f + (float)rng.NextDouble() * 0.25f;
                hopRise = 0.3f + (float)rng.NextDouble() * 0.2f;
                hopClock = 0f;
                act = Act.Hop;
                actLeft = 999;
                actStep = 0;
                Settle(hopTo);
                calm = 8;
                return true;
            }
            act = Act.Flee;
            target = Keep(pos + away * (0.8f + (float)rng.NextDouble() * 0.5f));
            actLeft = Mathf.CeilToInt(Flat(target - pos) / (FleeSpeed * Tick)) + 2;
            actStep = 0;
            Settle(target);
            calm = 6;
            return true;
        }

        /// <summary>逃げた先へ居場所を寄せる。戻ってきて同じ足元をまた歩かないように</summary>
        void Settle(Vector3 to)
        {
            home = Vector3.Lerp(home, to, 0.7f);
        }

        // ---- 置く ------------------------------------------------------------------

        /// <summary>
        /// 根を今の所へ置く。再生中は形も替える（<paramref name="live"/>）。
        /// 線の上は記憶の時計で決まるので、こまの頭の秒で置く
        /// </summary>
        void Apply(bool live, Phase now)
        {
            var at = pos;
            var m = Lines;
            var p = pitch;
            var h = heading;
            if (m != null)
            {
                switch (now)
                {
                    case Phase.Away: at = AwayAt(Along()); if (!live) { h = Toward(lift, Aim()); p = -20f; } break;
                    case Phase.Landing: at = Arc(lift, m.To, Mathf.Clamp01(Along()), Rise(lift, m.To)); if (!live) h = Toward(lift, m.To); break;
                    case Phase.Arriving: at = ArriveAt(Arrival()); if (!live) h = Toward(Sky(), m.Next); break;
                }
            }
            if (now == Phase.Ground || now == Phase.Settled || now == Phase.Back)
                if (act == Act.Hop) at = Arc(hopFrom, hopTo, Mathf.Clamp01(hopClock / hopSpan), hopRise);
            transform.localPosition = at;
            transform.localRotation = Quaternion.Euler(p, h, 0f);
            if (!live) return;
            if (filter == null) filter = GetComponent<MeshFilter>();
            if (look == null) look = GetComponent<Renderer>();
            var i = (int)shape;
            if (filter != null && i < poses.Length && poses[i] != null && filter.sharedMesh != poses[i]) filter.sharedMesh = poses[i];
            if (look != null && look.enabled == gone) look.enabled = !gone;
        }

        /// <summary>飛び立ちの線の上。s は 0〜1 で線の先まで、それを越えると先へ散る</summary>
        Vector3 AwayAt(float s)
        {
            var m = Lines;
            var to = Aim();
            if (s <= 1f)
            {
                s = Mathf.Max(0f, s);
                var across = Mathf.Pow(s, 1.3f);
                var up = 1f - (1f - s) * (1f - s);
                return new Vector3(Mathf.Lerp(lift.x, to.x, across), Mathf.Lerp(lift.y, to.y, up), Mathf.Lerp(lift.z, to.z, across));
            }
            var e = (s - 1f) * m.Span;
            var dir = to - lift;
            dir.y = 0f;
            dir = dir.sqrMagnitude > 1e-4f ? dir.normalized : Vector3.forward;
            return to + dir * (4.5f * e + 0.8f * e * e) + Vector3.up * (1.6f * e);
        }

        /// <summary>
        /// 飛び立ちの線の先。寄られて居場所から動いていた鳩は、動いた分だけ線の先もずらす。
        /// 線の先を居場所から決めたままだと、逃げた先から主の頭の上を越えて戻るように飛ぶ
        /// </summary>
        Vector3 Aim()
        {
            var m = Lines;
            var to = m.To;
            to.x += lift.x - m.From.x;
            to.z += lift.z - m.From.z;
            return to;
        }

        Vector3 ArriveAt(float k)
        {
            var m = Lines;
            var sky = Sky();
            var across = 1f - Mathf.Pow(1f - k, 1.5f);
            var p = Vector3.Lerp(sky, m.Next, across);
            p.y = m.Next.y + (sky.y - m.Next.y) * Mathf.Pow(1f - k, 1.4f);
            return p;
        }

        static Vector3 Arc(Vector3 a, Vector3 b, float k, float rise)
        {
            return Vector3.Lerp(a, b, k) + Vector3.up * (rise * 4f * k * (1f - k));
        }

        /// <summary>短く飛ぶ弧の高さ。遠いほど高く</summary>
        static float Rise(Vector3 a, Vector3 b)
        {
            return Mathf.Clamp(Flat(b - a) * 0.22f, 0.4f, 1.8f);
        }

        // ---- 道具 ------------------------------------------------------------------

        /// <summary>池へ入らず、柵の外へ出ない所へ寄せる</summary>
        Vector3 Keep(Vector3 p)
        {
            if (pool.z > 0f && pool.w > 0f)
            {
                var dx = (p.x - pool.x) / pool.z;
                var dz = (p.z - pool.y) / pool.w;
                var e = dx * dx + dz * dz;
                if (e < 1f)
                {
                    var k = 1.02f / Mathf.Sqrt(Mathf.Max(e, 1e-4f));
                    p.x = pool.x + (p.x - pool.x) * k;
                    p.z = pool.y + (p.z - pool.y) * k;
                }
            }
            if (yard.y > yard.x && yard.w > yard.z)
            {
                p.x = Mathf.Clamp(p.x, yard.x, yard.y);
                p.z = Mathf.Clamp(p.z, yard.z, yard.w);
            }
            return p;
        }

        Vector3 Local(Vector3 world)
        {
            return transform.parent != null ? transform.parent.InverseTransformPoint(world) : world;
        }

        Vector3 Ahead()
        {
            return Quaternion.Euler(0f, heading, 0f) * Vector3.forward;
        }

        static float Toward(Vector3 from, Vector3 to)
        {
            var d = to - from;
            return Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
        }

        static float Flat(Vector3 v)
        {
            v.y = 0f;
            return v.magnitude;
        }
    }
}
