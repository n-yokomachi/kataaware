using UnityEngine;

namespace HalfAware
{
    /// <summary>
    /// 場面 1 の机のモニターに映る主人公を、煙草を吸っている最中の姿にする（<see cref="TerminalReflection"/> が映している間だけ）。
    /// オーナー、2026-10-05「今の正面顔だとやっぱりちょっと不自然なんだよな。。。煙草を吸っている最中にして、手を口元に添えつつ、
    /// 煙草の煙で顔や髪が良く見えない、みたいな感じにできるかな」。
    ///
    /// **腕を上げるのは映り込みの写しだけ。** 場面の主人公の右腕は肘掛けに置いたまま（一人称のカメラに腕が映らない）。
    /// 映り込みの写し（<see cref="TerminalReflection"/> の体の写し）の右腕は、場面の主人公のボーンとは別のボーン（<see cref="bones"/>。
    /// 上腕から指先まで。根 <see cref="root"/> を場面の主人公の右の鎖骨のボーンに重ねる）で動かし、右手を口元へ運んで、
    /// 人差し指と中指で挟んだ煙草を唇の右寄りに当てる形（<see cref="lips"/>）と、唇のすぐ脇に持つ形（<see cref="aside"/>）を行き来する。
    /// 手は口元の右に置き、口元の左のほくろには掛けない。形は組み立て（PlaceProtagonist の映り込み）が解いて書く。
    ///
    /// 間合い（<see cref="At"/>）は <see cref="Period"/> 秒で一巡り。煙草を唇へ運んで吸い（火が強まる）、脇へ離して、口から吐く。
    /// 煙は三つ。煙草の先から細く立つ一筋、数秒おきに口から吐いてふわっと広がる煙、顔と髪の前に漂う薄い煙（吐いた煙の名残）。
    /// 顔と髪は煙越しにぼんやりとしか見えない。粒は口元の煙（<see cref="SmokePuffs"/>）と同じマテリアル。音は足さない。
    ///
    /// 写しの右腕・煙草・煙は、映り込みのカメラが撮る間だけ点ける（<see cref="Shoot"/>）。煙の粒は顔の前にあるので、点けたままだと一人称の視界に入る。
    /// 煙の値は定数で持ち、場面ファイルには持たせない（作るのは初めて要る時。<see cref="SmokePuffs"/> の一筋と同じ。複数の担当が場面を保存し直すので、古い値が残らないよう）
    /// </summary>
    [DefaultExecutionOrder(30)]
    public sealed class MirrorSmoking : MonoBehaviour
    {
        [Tooltip("場面の主人公の右の鎖骨のボーン。写しの右腕の根を、ここに重ねる")]
        [SerializeField] Transform source;
        [Tooltip("場面の主人公の体の根。向きの基準")]
        [SerializeField] Transform body;
        [Tooltip("写しの右腕の根")]
        [SerializeField] Transform root;
        [Tooltip("写しの右腕のボーン（上腕・前腕・手・指）")]
        [SerializeField] Transform[] bones = new Transform[0];
        [Tooltip("煙草を唇に当てた形。ボーンごとの、親から見た向き")]
        [SerializeField] Quaternion[] lips = new Quaternion[0];
        [Tooltip("煙草を唇のすぐ脇に持った形。ボーンごとの、親から見た向き")]
        [SerializeField] Quaternion[] aside = new Quaternion[0];
        [Tooltip("煙草（フィルター・紙・灰・火）。映り込みのカメラが撮る間だけ点ける")]
        [SerializeField] Renderer[] props = new Renderer[0];
        [Tooltip("煙草の火の芯（灰の縁の内の、いちばん明るい橙）")]
        [SerializeField] Renderer ember;
        [Tooltip("煙草の火の縁（芯と灰の縁のあいだの、暗い赤）")]
        [SerializeField] Renderer emberEdge;
        [Tooltip("火のにじみのマテリアル（煙の粒の絵を足し合わせで重ねる）")]
        [SerializeField] Material glow;
        [Tooltip("煙草の火の先。一筋の煙はここから立つ")]
        [SerializeField] Transform tip;
        [Tooltip("場面の主人公の頭のボーン")]
        [SerializeField] Transform head;
        [Tooltip("唇の間（吐いた煙の出る所）の、頭のボーンから見た位置")]
        [SerializeField] Vector3 mouth;
        [Tooltip("煙の粒のマテリアル（口元の煙と同じ）")]
        [SerializeField] Material smoke;

        // ---- 間合い ------------------------------------------------------------
        /// <summary>一巡りの秒。唇へ運んで吸い、離して吐き、脇に持って一息つく</summary>
        public const float Period = 6.4f;
        /// <summary>唇に届く時刻（一巡りの頭から）。ここまでに脇から唇へ運ぶ</summary>
        public const float LipsAt = 0.8f;
        /// <summary>唇から離し始める時刻。唇に当てている間が吸っている間（火が強まる）</summary>
        public const float LeaveAt = 2.2f;
        /// <summary>脇に戻りきる時刻</summary>
        public const float AsideAt = 2.9f;
        /// <summary>吐き始める時刻と、吐き終える時刻。離しながら吐く</summary>
        public const float BreathFrom = 2.6f;
        public const float BreathTo = 4.1f;
        /// <summary>
        /// 映り込みが出始めた時の時刻（一巡りの中）。吐いた煙が顔の前を流れている所から始め、
        /// 1 秒ほどで唇へ運び始める。前の一巡りの煙は、出始める前の <see cref="History"/> 秒を流しておいて、もう漂っている形から
        /// </summary>
        public const float StartAt = 5.0f;
        /// <summary>出始める前に流しておく秒。煙の粒の寿命より長く</summary>
        public const float History = 9f;
        /// <summary>前もって流すときの一こまの秒</summary>
        const float Step = 1f / 20f;

        // ---- 手の揺れ（わずか）。脇に持っている間だけ。唇に当てている間は当てたまま
        /// <summary>上腕を体の上下の軸まわりに振る角（度、片側）と速さ（周/秒）</summary>
        const float SwayDegrees = 1.6f;
        const float SwayRate = 0.21f;
        /// <summary>手首を前後に振る角（度、片側）と速さ（周/秒）</summary>
        const float WristDegrees = 3.5f;
        const float WristRate = 0.34f;

        // ---- 火
        /// <summary>
        /// 火の色。吸っていない時（EmberDim）と、吸っていちばん強まった時（EmberHot）。火は自分で光るので、灯りを受けないマテリアル（URP の Unlit）の色で持つ
        /// </summary>
        public static readonly Color EmberDim = new Color(0.62f, 0.14f, 0.04f);
        public static readonly Color EmberHot = new Color(1.00f, 0.48f, 0.12f);
        /// <summary>
        /// 火のにじみ（灰の縁の外へ、赤〜橙が少しにじむ光）。大きさ（m）と、吸っていない時・吸っていちばん強まった時の色（α で濃さ）。
        /// 火の芯の円だけでは、平らな橙の円盤に見えた
        /// </summary>
        const float HaloSize = 0.024f;
        static readonly Color HaloDim = new Color(0.90f, 0.22f, 0.05f, 0.8f);
        static readonly Color HaloHot = new Color(1.00f, 0.45f, 0.12f, 1f);
        /// <summary>吸っている間の、火の明るさの倍</summary>
        const float DragGlow = 2.2f;

        // ---- 煙草の先から立つ一筋
        const float WispRate = 30f;
        const float WispRise = 0.11f;
        const float WispLife = 2.8f;
        /// <summary>出る所の粒の大きさ（m）と、寿命の終わりの太り方（倍）</summary>
        const float WispSize = 0.015f;
        const float WispSpread = 6f;
        const float WispAlpha = 0.85f;
        /// <summary>流れ（体の右・前へ。m/秒）。顔の前を、体の左（ほくろの側）へゆっくり寄る</summary>
        static readonly Vector2 WispDrift = new Vector2(-0.018f, 0.004f);

        // ---- 吐いた煙
        /// <summary>吐いている間に出す数（毎秒）</summary>
        const float BreathRate = 85f;
        /// <summary>
        /// 吐く向き（体の右・上・前）。手の反対（体の左）の斜め上へ吐き、顔の前を横切って髪の方へ昇らせる。
        /// まっすぐ前へ吐くと、映り込みのカメラへ向かって来るだけで、口の前に丸い塊が浮いた
        /// </summary>
        static readonly Vector3 BreathAim = new Vector3(-0.5f, 0.4f, 0.7f);
        /// <summary>吐いた勢い（m/秒）と、その抜け方（この速さを越えた分を、こまごとに削る割合）。15 cm ほど出て止まり、そこから広がって昇る</summary>
        static readonly Vector2 BreathSpeed = new Vector2(0.22f, 0.40f);
        const float BreathLimit = 0.05f;
        const float BreathDampen = 0.12f;
        static readonly Vector2 BreathRise = new Vector2(0.03f, 0.07f);
        static readonly Vector2 BreathLife = new Vector2(4.0f, 5.5f);
        static readonly Vector2 BreathSize = new Vector2(0.04f, 0.06f);
        const float BreathGrow = 3.5f;
        static readonly Vector2 BreathAlpha = new Vector2(0.30f, 0.45f);

        // ---- 顔と髪の前に漂う煙（吐いた煙の名残）
        const float HazeRate = 10f;
        /// <summary>漂わせる箱の大きさ（体の右・上・前。m）と、箱の真ん中の唇からの離れ（m）</summary>
        static readonly Vector3 HazeBox = new Vector3(0.22f, 0.34f, 0.05f);
        static readonly Vector3 HazeAt = new Vector3(0.0f, 0.07f, 0.05f);
        static readonly Vector2 HazeLife = new Vector2(5f, 7.5f);
        static readonly Vector2 HazeSize = new Vector2(0.14f, 0.24f);
        static readonly Vector2 HazeAlpha = new Vector2(0.28f, 0.42f);

        static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

        ParticleSystem wisp;
        ParticleSystem breath;
        ParticleSystem haze;
        /// <summary>火のにじみ。粒一つを火の先に置く（いつもカメラの方を向く）</summary>
        ParticleSystem halo;
        MaterialPropertyBlock block;
        float clock;
        bool playing;

        /// <summary>一巡りの中の、手と火と息の具合</summary>
        public struct Beat
        {
            /// <summary>煙草を唇に当てている度合い（1 で唇、0 で脇）</summary>
            public float lips;
            /// <summary>火の明るさの倍（1 で吸っていない時）</summary>
            public float glow;
            /// <summary>吐いている度合い（0〜1）。吐いた煙を出す数の割合</summary>
            public float breath;
        }

        /// <summary>時刻 t（秒）の、手と火と息の具合。<see cref="Period"/> 秒で繰り返す</summary>
        public static Beat At(float t)
        {
            var u = Mathf.Repeat(t, Period);
            var b = new Beat();
            if (u < LipsAt) b.lips = Mathf.SmoothStep(0f, 1f, u / LipsAt);
            else if (u < LeaveAt) b.lips = 1f;
            else if (u < AsideAt) b.lips = 1f - Mathf.SmoothStep(0f, 1f, (u - LeaveAt) / (AsideAt - LeaveAt));
            // 吸っている間（唇に届いてから離すまで）に強まり、離すと戻る
            var rise = Mathf.Clamp01((u - LipsAt) / 0.4f);
            var fall = 1f - Mathf.Clamp01((u - LeaveAt) / 0.8f);
            b.glow = 1f + (DragGlow - 1f) * Mathf.SmoothStep(0f, 1f, Mathf.Min(rise, fall));
            if (u >= BreathFrom && u < BreathTo)
            {
                var k = (u - BreathFrom) / (BreathTo - BreathFrom);
                // 吐き始めに強く、終わりへ細る
                b.breath = Mathf.Sin(Mathf.PI * Mathf.Pow(k, 0.6f));
            }
            return b;
        }

        /// <summary>今の時刻（秒。一巡りの中で <see cref="StartAt"/> から数える）。動作確認から読む</summary>
        public float Clock => clock;

        /// <summary>写しの右腕のボーン（動作確認から読む）</summary>
        public Transform[] Bones => bones;

        /// <summary>煙草の火の先（動作確認から読む）</summary>
        public Transform Tip => tip;

        /// <summary>唇の間の、世界の位置</summary>
        public Vector3 Mouth => head != null ? head.TransformPoint(mouth) : transform.position;

        void Awake()
        {
            Shoot(false);
        }

        void OnDestroy()
        {
            Clear();
        }

        /// <summary>映り込みが出始めた。前の一巡りの煙がもう漂っている形から、吸い始める手前の所で始める</summary>
        public void Begin()
        {
            Rehearse(StartAt);
            foreach (var ps in Systems()) if (ps != null) ps.Play();
            playing = true;
        }

        /// <summary>映り込みが消えた。煙を止めて消す（次に出る時はまた始めから）</summary>
        public void End()
        {
            playing = false;
            foreach (var ps in Systems())
                if (ps != null) ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        /// <summary>映っている間、毎こま進める</summary>
        public void Tick(float dt)
        {
            if (!playing) return;
            clock += Mathf.Max(0f, dt);
            Pose(clock);
            Feed(clock);
        }

        /// <summary>
        /// 時刻 t の形にして、煙を t まで流す。映り込みが出始める <see cref="History"/> 秒前（<see cref="StartAt"/> − History）から、
        /// 一こまずつ手を動かしながら流す（どの t も同じ流れの途中になる。撮り比べで、煙が流れて変わっていくのが並ぶ）。
        /// 煙は止めたまま（再生中は <see cref="Begin"/> が続けて流す）。エディタで撮るときはこれを呼んでから撮る
        /// </summary>
        public void Rehearse(float t)
        {
            Ensure();
            var systems = Systems();
            foreach (var ps in systems) ps.Simulate(0f, true, true);
            var from = Mathf.Min(t, StartAt) - History;
            var n = Mathf.CeilToInt((t - from) / Step);
            for (var i = 0; i < n; i++)
            {
                var at = from + i * Step;
                Pose(at);
                Feed(at);
                foreach (var ps in systems) ps.Simulate(Step, true, false, false);
            }
            clock = t;
            Pose(t);
            Feed(t);
        }

        /// <summary>
        /// 時刻 t の手の形。写しの右腕の根を場面の主人公の右の鎖骨のボーンに重ね、唇に当てた形と脇に持った形を混ぜ、
        /// 脇に持っている間だけ、手をわずかに揺らす
        /// </summary>
        public void Pose(float t)
        {
            if (root != null && source != null) root.SetPositionAndRotation(source.position, source.rotation);
            var n = Mathf.Min(bones.Length, Mathf.Min(lips.Length, aside.Length));
            if (n == 0) return;
            var beat = At(t);
            for (var i = 0; i < n; i++)
                if (bones[i] != null) bones[i].localRotation = Quaternion.Slerp(aside[i], lips[i], beat.lips);
            var free = 1f - beat.lips;
            if (free <= 0f || body == null) return;
            // 上腕を体の上下の軸まわりに、手首を体の左右の軸まわりに。周期をずらして、同じ動きに戻らないように
            var sway = Mathf.Sin(t * SwayRate * 2f * Mathf.PI) * SwayDegrees * free;
            var nod = Mathf.Sin(t * WristRate * 2f * Mathf.PI + 1.3f) * WristDegrees * free;
            if (bones[0] != null) bones[0].rotation = Quaternion.AngleAxis(sway, body.up) * bones[0].rotation;
            if (n > 2 && bones[2] != null) bones[2].rotation = Quaternion.AngleAxis(nod, body.right) * bones[2].rotation;
        }

        /// <summary>時刻 t の火の明るさ、吐いた煙の出す数と出る所、漂う煙の箱の置き場</summary>
        void Feed(float t)
        {
            var beat = At(t);
            var hot = (beat.glow - 1f) / (DragGlow - 1f);
            if (block == null) block = new MaterialPropertyBlock();
            if (ember != null)
            {
                ember.GetPropertyBlock(block);
                block.SetColor(BaseColor, Color.Lerp(EmberDim, EmberHot, hot));
                ember.SetPropertyBlock(block);
            }
            if (emberEdge != null)
            {
                // 縁は芯の色を暗くした赤（芯から灰へ、赤〜橙がにじむ）
                emberEdge.GetPropertyBlock(block);
                var c = Color.Lerp(EmberDim, EmberHot, hot) * 0.55f;
                c.a = 1f;
                block.SetColor(BaseColor, c);
                emberEdge.SetPropertyBlock(block);
            }
            if (halo != null)
            {
                var r = halo.GetComponent<ParticleSystemRenderer>();
                r.GetPropertyBlock(block);
                block.SetColor(BaseColor, Color.Lerp(HaloDim, HaloHot, hot));
                r.SetPropertyBlock(block);
            }
            var facing = Facing();
            var at = Mouth;
            if (breath != null)
            {
                breath.transform.SetPositionAndRotation(at, facing * Quaternion.LookRotation(BreathAim.normalized, Vector3.up));
                var e = breath.emission;
                e.rateOverTimeMultiplier = BreathRate * beat.breath;
            }
            if (haze != null) haze.transform.SetPositionAndRotation(at + facing * HazeAt, facing);
            if (wisp != null)
            {
                var v = wisp.velocityOverLifetime;
                var d = facing * new Vector3(WispDrift.x, 0f, WispDrift.y);
                v.x = new ParticleSystem.MinMaxCurve(d.x - 0.005f, d.x + 0.005f);
                v.z = new ParticleSystem.MinMaxCurve(d.z - 0.005f, d.z + 0.005f);
            }
        }

        /// <summary>体の向き（水平）</summary>
        Quaternion Facing()
        {
            return body != null ? Quaternion.Euler(0f, body.eulerAngles.y, 0f) : Quaternion.identity;
        }

        /// <summary>
        /// 映り込みのカメラが撮る間だけ、煙草と煙を点ける。off で消す（一人称の視界に入れない）。
        /// 写しの体（右腕を含む）は <see cref="TerminalReflection"/> が点ける
        /// </summary>
        public void Shoot(bool on)
        {
            ShowCigarette(on);
            foreach (var ps in Systems())
                if (ps != null && ps != halo) ps.GetComponent<ParticleSystemRenderer>().enabled = on;
        }

        /// <summary>煙草（と火のにじみ）だけを点ける・消す。エディタで手と煙草を寄って撮るとき（煙は出さない）にも呼ぶ</summary>
        public void ShowCigarette(bool on)
        {
            foreach (var r in props) if (r != null) r.enabled = on;
            if (halo != null) halo.GetComponent<ParticleSystemRenderer>().enabled = on;
        }

        /// <summary>作った煙を捨てる（エディタで撮った後にも呼ぶ）</summary>
        public void Clear()
        {
            foreach (var ps in Systems())
                if (ps != null)
                {
                    if (Application.isPlaying) Destroy(ps.gameObject);
                    else DestroyImmediate(ps.gameObject);
                }
            wisp = breath = haze = halo = null;
            playing = false;
        }

        ParticleSystem[] Systems()
        {
            if (wisp == null && breath == null && haze == null && halo == null) return new ParticleSystem[0];
            return new[] { wisp, breath, haze, halo };
        }

        /// <summary>三つの煙を作る（まだ無ければ）。粒は世界の座標で動かす（手が動いても、出た煙は置き去り）</summary>
        void Ensure()
        {
            if (wisp != null && breath != null && haze != null && halo != null) return;
            Clear();

            // 火のにじみ。火の先に粒を一つ、いつまでも置いておく（火の先に付いて動く）
            halo = Make("MirrorEmberHalo", tip != null ? tip : transform, 53);
            {
                var main = halo.main;
                main.simulationSpace = ParticleSystemSimulationSpace.Local;
                main.startLifetime = 1e5f;
                main.startSize = HaloSize;
                main.startColor = Color.white;
                main.maxParticles = 1;
                var emission = halo.emission;
                emission.rateOverTime = 0f;
                emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 1) });
                var shape = halo.shape;
                shape.enabled = false;
                var r = halo.GetComponent<ParticleSystemRenderer>();
                if (glow != null) r.sharedMaterial = glow;
            }

            // 煙草の先から細く立つ一筋。火の先に付いて動く
            wisp = Make("MirrorWisp", tip != null ? tip : transform, 41);
            {
                var main = wisp.main;
                main.startLifetime = new ParticleSystem.MinMaxCurve(WispLife * 0.85f, WispLife * 1.15f);
                main.startSize = new ParticleSystem.MinMaxCurve(WispSize * WispSpread * 0.8f, WispSize * WispSpread * 1.2f);
                main.startColor = new ParticleSystem.MinMaxGradient(
                    new Color(0.90f, 0.89f, 0.87f, WispAlpha), new Color(0.84f, 0.83f, 0.81f, WispAlpha * 0.8f));
                main.maxParticles = 150;
                var emission = wisp.emission;
                emission.rateOverTime = WispRate;
                var shape = wisp.shape;
                shape.enabled = true;
                shape.shapeType = ParticleSystemShapeType.Sphere;
                shape.radius = 0.0015f;
                var velocity = wisp.velocityOverLifetime;
                velocity.enabled = true;
                velocity.space = ParticleSystemSimulationSpace.World;
                velocity.x = new ParticleSystem.MinMaxCurve(-0.005f, 0.005f);
                velocity.y = new ParticleSystem.MinMaxCurve(WispRise * 0.85f, WispRise * 1.15f);
                velocity.z = new ParticleSystem.MinMaxCurve(-0.005f, 0.005f);
                var noise = wisp.noise;
                noise.enabled = true;
                noise.strength = new ParticleSystem.MinMaxCurve(0.05f, AnimationCurve.EaseInOut(0f, 0.05f, 1f, 1f));
                noise.frequency = 2.2f;
                noise.scrollSpeed = 0.4f;
                noise.damping = true;
                noise.octaveCount = 1;
                noise.quality = ParticleSystemNoiseQuality.Medium;
                var size = wisp.sizeOverLifetime;
                size.enabled = true;
                size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 1f / WispSpread, 1f, 1f));
                AlphaOverLife(wisp, 0.06f, 0.55f, 0.5f);
            }

            // 吐いた煙。唇の間から前へ出て、勢いが抜けたところで広がって昇る
            breath = Make("MirrorBreath", transform, 43);
            {
                var main = breath.main;
                main.startLifetime = new ParticleSystem.MinMaxCurve(BreathLife.x, BreathLife.y);
                main.startSpeed = new ParticleSystem.MinMaxCurve(BreathSpeed.x, BreathSpeed.y);
                main.startSize = new ParticleSystem.MinMaxCurve(BreathSize.x, BreathSize.y);
                main.startColor = new ParticleSystem.MinMaxGradient(
                    new Color(0.82f, 0.81f, 0.78f, BreathAlpha.x), new Color(0.76f, 0.75f, 0.73f, BreathAlpha.y));
                main.maxParticles = 400;
                var emission = breath.emission;
                emission.rateOverTime = 0f;
                var shape = breath.shape;
                shape.enabled = true;
                shape.shapeType = ParticleSystemShapeType.Cone;
                shape.angle = 26f;
                shape.radius = 0.01f;
                // 吐いた勢いは口元の煙（SmokePuffs）と同じ抜け方。越えた分をこまごとに削る
                var limit = breath.limitVelocityOverLifetime;
                limit.enabled = true;
                limit.limit = new ParticleSystem.MinMaxCurve(BreathLimit);
                limit.dampen = BreathDampen;
                var velocity = breath.velocityOverLifetime;
                velocity.enabled = true;
                velocity.space = ParticleSystemSimulationSpace.World;
                velocity.x = new ParticleSystem.MinMaxCurve(-0.012f, 0.012f);
                velocity.y = new ParticleSystem.MinMaxCurve(BreathRise.x, BreathRise.y);
                velocity.z = new ParticleSystem.MinMaxCurve(-0.012f, 0.012f);
                var noise = breath.noise;
                noise.enabled = true;
                noise.strength = new ParticleSystem.MinMaxCurve(0.05f);
                noise.frequency = 1.4f;
                noise.scrollSpeed = 0.25f;
                noise.damping = true;
                noise.octaveCount = 1;
                noise.quality = ParticleSystemNoiseQuality.Medium;
                var size = breath.sizeOverLifetime;
                size.enabled = true;
                var grow = new AnimationCurve();
                grow.AddKey(0f, 1f / BreathGrow);
                grow.AddKey(0.35f, 0.6f);
                grow.AddKey(1f, 1f);
                size.size = new ParticleSystem.MinMaxCurve(BreathGrow, grow);
                var rotation = breath.rotationOverLifetime;
                rotation.enabled = true;
                rotation.z = new ParticleSystem.MinMaxCurve(-0.4f, 0.4f);
                AlphaOverLife(breath, 0.08f, 0.6f, 0.5f);
            }

            // 顔と髪の前に漂う煙。吐いた煙の名残で、いつも薄くかかっている
            haze = Make("MirrorHaze", transform, 47);
            {
                var main = haze.main;
                main.startLifetime = new ParticleSystem.MinMaxCurve(HazeLife.x, HazeLife.y);
                main.startSize = new ParticleSystem.MinMaxCurve(HazeSize.x, HazeSize.y);
                main.startColor = new ParticleSystem.MinMaxGradient(
                    new Color(0.82f, 0.81f, 0.78f, HazeAlpha.x), new Color(0.76f, 0.75f, 0.73f, HazeAlpha.y));
                main.maxParticles = 80;
                var emission = haze.emission;
                emission.rateOverTime = HazeRate;
                var shape = haze.shape;
                shape.enabled = true;
                shape.shapeType = ParticleSystemShapeType.Box;
                shape.scale = HazeBox;
                var velocity = haze.velocityOverLifetime;
                velocity.enabled = true;
                velocity.space = ParticleSystemSimulationSpace.World;
                velocity.x = new ParticleSystem.MinMaxCurve(-0.008f, 0.008f);
                velocity.y = new ParticleSystem.MinMaxCurve(0.006f, 0.018f);
                velocity.z = new ParticleSystem.MinMaxCurve(-0.002f, 0.002f);
                // 揺れは小さく（顔の後ろへ回り込まないように）
                var noise = haze.noise;
                noise.enabled = true;
                noise.strength = new ParticleSystem.MinMaxCurve(0.006f);
                noise.frequency = 0.9f;
                noise.scrollSpeed = 0.15f;
                noise.damping = true;
                noise.octaveCount = 1;
                var size = haze.sizeOverLifetime;
                size.enabled = true;
                size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.7f, 1f, 1.3f));
                var rotation = haze.rotationOverLifetime;
                rotation.enabled = true;
                rotation.z = new ParticleSystem.MinMaxCurve(-0.15f, 0.15f);
                AlphaOverLife(haze, 0.25f, 0.8f, 0.6f);
            }
            Feed(clock);
            Shoot(false);
        }

        /// <summary>煙の共通の作り。世界の座標で動かし、止めてあり、口元の煙と同じマテリアル。乱数の種は決めておく（撮るたびに同じ形）</summary>
        ParticleSystem Make(string name, Transform parent, uint seed)
        {
            // 映り込みのカメラだけが撮る層に置く（映り込みには人と煙草と煙だけを映す）
            var go = new GameObject(name) { hideFlags = HideFlags.DontSave, layer = TerminalReflection.MirrorLayer };
            go.transform.SetParent(parent, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.rotation = Quaternion.identity;
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ps.useAutoRandomSeed = false;
            ps.randomSeed = seed;
            var main = ps.main;
            main.loop = true;
            main.playOnAwake = false;
            main.duration = Period;
            main.startSpeed = 0f;
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = 0f;
            // 描くのは映り込みのカメラが撮る間だけ。描かない間も流し続ける
            main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
            var emission = ps.emission;
            emission.enabled = true;
            var r = ps.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Billboard;
            r.alignment = ParticleSystemRenderSpace.View;
            r.sortMode = ParticleSystemSortMode.Distance;
            r.sharedMaterial = smoke;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            r.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            r.enabled = false;
            return ps;
        }

        /// <summary>寿命の間の濃さ。rise の所で濃くなりきり、hold の所で keep まで薄れ、終わりで 0</summary>
        static void AlphaOverLife(ParticleSystem ps, float rise, float keep, float hold)
        {
            var color = ps.colorOverLifetime;
            color.enabled = true;
            var g = new Gradient();
            g.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, rise), new GradientAlphaKey(keep, hold), new GradientAlphaKey(0f, 1f) });
            color.color = new ParticleSystem.MinMaxGradient(g);
        }
    }
}
