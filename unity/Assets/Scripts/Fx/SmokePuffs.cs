using UnityEngine;

namespace HalfAware
{
    /// <summary>
    /// 煙草の煙。吸っている間だけ粒を出し、止めた後も出ている分は消えるまで流れる。
    /// 出どころは口元に置き、粒は世界の座標で動かす。首を振っても煙は置き去りになる。
    ///
    /// **場面 1 は吸い終えても煙を立て続ける**（<see cref="Linger"/>。オーナー、2026-09-29「タバコ吸った後だけど、しばらくは煙草の煙を出し続けるようにして」）。
    /// 吸い終えたら口元の煙を止め、二つに替える（口元の燻る煙のままでは、机の上の薄い靄にしか見えなかった）。
    /// - 一筋（<see cref="MakeWisp"/>）: 肘掛けに置いた右手の指先（煙草）の一点から細く昇り、昇るほど揺れて太り、薄れて消える
    /// - 漂う煙（<see cref="MakeHaze"/>）: 正面のモニターの前に大きく薄い粒をゆっくり漂わせ、画面の文字と映り込みの顔に**ある程度**かぶせる
    ///   （オーナー「モニターの文字や映り込みの顔をある程度隠すために煙をかぶせてほしい」。顔の輪郭・ほくろ・目がところどころ紛れる程度で、
    ///   全部は隠さない。粒が流れて、隠れる所が少しずつ変わる）。字幕の窓と真ん中の枠は UI なので煙の外
    /// どちらも吸い終えた後に立ち続ける間だけで、吸っている間（場面 1・5・8）の口元の煙は変えない。
    /// モニターを済ませたら、出す数と粒の大きさを <see cref="Fade"/> の秒で細くして止める（ふっと消さない。出ていた粒は寿命まで流れて、顔と文字がはっきり見えてくる）
    /// </summary>
    public sealed class SmokePuffs : MonoBehaviour
    {
        [SerializeField] ParticleSystem puffs;
        [Tooltip("ひと息で吐き出す粒の数。薄いものを数多く重ねて煙の塊にする")]
        [SerializeField] int blowCount = 90;
        [Tooltip("吐いた煙を撒く広さ。メートル")]
        [SerializeField] float blowSpread = 0.13f;

        [Header("吸い終えた後の一筋（場面 1。右手の指先から）")]
        [Tooltip("一筋の粒を出す数。毎秒。粒が重なって筋に見えるだけ出す")]
        [SerializeField] float wispRate = 26f;
        [Tooltip("昇る速さ。m/秒（粒ごとにこの 0.85〜1.15 倍）")]
        [SerializeField] float wispRise = 0.15f;
        [Tooltip("粒の寿命。秒。昇る速さとの積がおおよその高さ")]
        [SerializeField] float wispLife = 5f;
        [Tooltip("昇りながら体の内側（x）と前（y）へ流れる速さ。m/秒。指先は目の右下のすぐ近くで、まっすぐ昇ると視界の右の縁にしか入らない。正面の視界へ寄せる")]
        [SerializeField] Vector2 wispDrift = new Vector2(0.05f, 0.06f);
        [Tooltip("出る所の粒の大きさ。m。昇るほど wispSpread 倍まで太る")]
        [SerializeField] float wispSize = 0.026f;
        [Tooltip("昇りきった所の太り方。出る所の大きさに対する倍")]
        [SerializeField] float wispSpread = 4f;
        [Tooltip("横の揺れの強さ。昇るほど強くなる")]
        [SerializeField] float wispSway = 0.09f;
        [Tooltip("粒の濃さ（不透明さ）。口元の燻る煙（0.17〜0.26）より明るく")]
        [SerializeField, Range(0f, 1f)] float wispAlpha = 0.6f;
        [Tooltip("指先の骨から、煙草の火の所までの高さ。m")]
        [SerializeField] float wispLift = 0.02f;

        [Header("吸い終えた後にモニターの前を漂う煙（場面 1）")]
        [Tooltip("漂う粒を出す数。毎秒")]
        [SerializeField] float hazeRate = 12f;
        [Tooltip("漂う粒の寿命。秒（この 0.8〜1.2 倍）")]
        [SerializeField] float hazeLife = 7f;
        [Tooltip("漂う粒の大きさ。m（この 0.8〜1.3 倍）。目から 0.5〜0.8 m 先なので、一粒で画面の一部を覆う")]
        [SerializeField] float hazeSize = 0.32f;
        [Tooltip("漂う粒の濃さ（不透明さ）。重なった所だけ濃くなり、全部は隠さない")]
        [SerializeField, Range(0f, 1f)] float hazeAlpha = 0.55f;
        [Tooltip("漂わせる箱の真ん中。体（Player の根）から見て、右・上・前。m。座った目の少し下、モニターの手前")]
        [SerializeField] Vector3 hazeCentre = new Vector3(0f, 1.12f, 0.85f);
        [Tooltip("漂わせる箱の広さ。右・上・前。m。正面のモニターの幅を覆う")]
        [SerializeField] Vector3 hazeBox = new Vector3(1.0f, 0.45f, 0.3f);
        [Tooltip("漂う速さ（横と上下のゆっくりした流れ）。m/秒")]
        [SerializeField] float hazeDrift = 0.03f;

        float until = -1f;
        /// <summary>時刻が来ても止めずに立て続けるか</summary>
        bool lingering;
        /// <summary>細くしている途中の、始めた時刻。細くしていなければ負</summary>
        float fadeStart = -1f;
        float fadeSeconds;
        /// <summary>細くしている煙と、細くし始めた時の出す数と粒の大きさ（二つの定数の間）。止めたら戻す</summary>
        ParticleSystem[] fading = new ParticleSystem[0];
        float[] baseRate = new float[0];
        float[] baseSizeMin = new float[0];
        float[] baseSizeMax = new float[0];
        /// <summary>吸い終えた後の一筋と漂う煙。初めて要る時に作る</summary>
        ParticleSystem wisp;
        ParticleSystem haze;
        /// <summary>一筋を出す所（右手の指先の骨）。無ければ口元から出す</summary>
        Transform wispAnchor;
        /// <summary>吸い終えた後の煙（一筋と漂う煙）を立てているか（口元の煙から替わった後）</summary>
        bool afterOn;

        /// <summary>今このとき煙を出しているか。動作確認から読む</summary>
        public bool Emitting { get { return until > 0f && (lingering || fadeStart >= 0f || Time.time < until); } }

        /// <summary>吸い終えた後も立て続けているか（細くしている途中は含まない）。動作確認から読む</summary>
        public bool Lingering { get { return lingering; } }

        /// <summary>細くしている途中か。動作確認から読む</summary>
        public bool Fading { get { return fadeStart >= 0f; } }

        /// <summary>吸い終えた後の一筋と漂う煙を立てているか。動作確認から読む</summary>
        public bool Wisping { get { return afterOn; } }

        /// <summary>
        /// 細くし始めてから elapsed 秒の、出す数の割合。1 から始めて seconds 秒で 0。両端をなだらかにする（ふっと消さない）
        /// </summary>
        public static float Thin(float elapsed, float seconds)
        {
            if (seconds <= 0f) return 0f;
            return 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / seconds));
        }

        /// <summary>細くする間の粒の大きさの倍率。出す数の割合 thin が 0 に近づくほど半分まで細る</summary>
        public static float Size(float thin)
        {
            return Mathf.Lerp(0.5f, 1f, Mathf.Clamp01(thin));
        }

        /// <summary>吸っている間の判定。始めた時刻から seconds 秒だけ出す</summary>
        public static bool Lit(float now, float startedAt, float seconds)
        {
            if (startedAt < 0f) return false;
            return now >= startedAt && now < startedAt + seconds;
        }

        /// <summary>seconds 秒のあいだ煙を立てる。<see cref="Linger"/> を掛けてあれば、時刻が来ても止めない</summary>
        public void Begin(float seconds)
        {
            Unfade();
            until = Time.time + seconds;
            if (puffs == null) return;
            puffs.Clear();
            puffs.Play();
        }

        /// <summary>
        /// 時刻が来ても止めずに立て続ける（場面 1。モニターを済ませるまで）。まだ立てていなければ、立て始めた時から効く。
        /// 時刻が来たら（吸い終えたら）、口元の煙を止めて、anchor（右手の指先）の一筋とモニターの前を漂う煙に替える。anchor が無ければ一筋は口元から
        /// </summary>
        public void Linger(Transform anchor = null)
        {
            lingering = true;
            wispAnchor = anchor;
        }

        /// <summary>
        /// 吸い終えた後の、立ち続けている形で始める（場面 1 を思い出した時。煙草を吸い終えてモニターがまだの所）。
        /// 吐いた煙の塊も口元の煙も出さず、一筋と漂う煙を、もう漂っている形（寿命ひと回りぶん進めた形）から始める
        /// </summary>
        public void Smolder(Transform anchor = null)
        {
            Unfade();
            lingering = true;
            wispAnchor = anchor;
            until = Mathf.Max(0.001f, Time.time);
            StartAfter(true);
        }

        /// <summary>立てている煙を、seconds 秒で出す数と粒の大きさを細くしてから止める。出ていた粒は寿命まで流れる</summary>
        public void Fade(float seconds)
        {
            if (until < 0f || fadeStart >= 0f) return;
            lingering = false;
            fadeStart = Time.time;
            fadeSeconds = Mathf.Max(0f, seconds);
            fading = afterOn ? new[] { wisp, haze } : new[] { puffs };
            baseRate = new float[fading.Length];
            baseSizeMin = new float[fading.Length];
            baseSizeMax = new float[fading.Length];
            for (var i = 0; i < fading.Length; i++)
            {
                if (fading[i] == null) continue;
                baseRate[i] = fading[i].emission.rateOverTimeMultiplier;
                baseSizeMin[i] = fading[i].main.startSize.constantMin;
                baseSizeMax[i] = fading[i].main.startSize.constantMax;
            }
        }

        /// <summary>口元の煙を止めて、一筋と漂う煙を立てる。settled なら、もう漂っている形から始める</summary>
        void StartAfter(bool settled)
        {
            if (afterOn) return;
            var w = MakeWisp(wispAnchor);
            var h = MakeHaze();
            if (puffs != null) puffs.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            foreach (var ps in new[] { w, h })
            {
                if (ps == null) continue;
                // 思い出した時は寿命ひと回りぶん進めて、漂っている形から。吸い終えた時は出る所から少しずつ
                if (settled) ps.Simulate(ps.main.startLifetime.constantMax, true, true);
                ps.Play();
            }
            afterOn = true;
        }

        /// <summary>
        /// 吸い終えた後の一筋を作る（まだ無ければ）。anchor（右手の指先の骨）の少し上から出し、付いて動く。無ければ口元から。
        /// 一点から出し、粒ごとに少し違う速さで昇らせ、昇るほど強くなる横の揺れ（ノイズ）で揺らし、太らせ、薄れさせる。
        /// 粒は世界の座標で動かす（手が動いても昇った煙は置き去り）。マテリアルは口元の煙と同じ。
        /// エディタで撮るときにも呼ぶ（その時は呼んだ側が撮った後に消す）
        /// </summary>
        public ParticleSystem MakeWisp(Transform anchor)
        {
            if (wisp != null) return wisp;
            var at = anchor != null ? anchor : transform;
            var ps = NewSystem("SmokeWisp", at, at.position + Vector3.up * wispLift);

            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(wispLife * 0.85f, wispLife * 1.1f);
            // 大きさは昇りきった所の大きさで持ち、昇るほど太る曲線（出る所で 1/wispSpread）を掛ける
            main.startSize = new ParticleSystem.MinMaxCurve(wispSize * wispSpread * 0.8f, wispSize * wispSpread * 1.2f);
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(0.90f, 0.89f, 0.87f, wispAlpha), new Color(0.84f, 0.83f, 0.81f, wispAlpha * 0.8f));
            main.maxParticles = 200;

            var emission = ps.emission;
            emission.rateOverTime = wispRate;

            // 一点から出す
            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.002f;

            // 昇る。粒ごとに速さを少し違えて、筋が途切れず伸びるように。体の内側と前へ少し流し、正面の視界へ寄せる
            var drift = Body(new Vector3(-wispDrift.x, 0f, wispDrift.y), false);
            var velocity = ps.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.x = new ParticleSystem.MinMaxCurve(drift.x - 0.004f, drift.x + 0.004f);
            velocity.y = new ParticleSystem.MinMaxCurve(wispRise * 0.85f, wispRise * 1.15f);
            velocity.z = new ParticleSystem.MinMaxCurve(drift.z - 0.004f, drift.z + 0.004f);

            // 横の揺れ。出る所では細くまっすぐ、昇るほど大きく揺れる
            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = new ParticleSystem.MinMaxCurve(wispSway, AnimationCurve.EaseInOut(0f, 0.05f, 1f, 1f));
            noise.frequency = 0.9f;
            noise.scrollSpeed = 0.35f;
            noise.damping = true;
            noise.octaveCount = 1;
            noise.quality = ParticleSystemNoiseQuality.Medium;

            // 昇るほど太る
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 1f / Mathf.Max(1f, wispSpread), 1f, 1f));

            // 出てすぐ濃くなり、昇るにつれ薄れて消える
            AlphaOverLife(ps, 0.08f, 0.7f, 0.6f);
            wisp = ps;
            return ps;
        }

        /// <summary>
        /// 吸い終えた後にモニターの前を漂う煙を作る（まだ無ければ）。体（Player の根）の前の箱（hazeCentre・hazeBox）の中に、
        /// 大きく薄い粒をゆっくり出し、低い周波数の揺れで流す。粒はゆっくり膨らみ、現れて消える。
        /// エディタで撮るときにも呼ぶ（その時は呼んだ側が撮った後に消す）
        /// </summary>
        public ParticleSystem MakeHaze()
        {
            if (haze != null) return haze;
            var body = transform.root;
            var ps = NewSystem("SmokeHaze", body, body.position + Body(hazeCentre, true));
            // 箱は体の向きに合わせる
            ps.transform.rotation = Quaternion.LookRotation(Flat(body.forward), Vector3.up);

            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(hazeLife * 0.8f, hazeLife * 1.2f);
            main.startSize = new ParticleSystem.MinMaxCurve(hazeSize * 0.8f, hazeSize * 1.3f);
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(0.86f, 0.85f, 0.83f, hazeAlpha), new Color(0.80f, 0.79f, 0.77f, hazeAlpha * 0.7f));
            main.maxParticles = 120;

            var emission = ps.emission;
            emission.rateOverTime = hazeRate;

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = hazeBox;

            // ゆっくり流れる。横と上下へ少しずつ、粒ごとに違う向きへ
            var velocity = ps.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.x = new ParticleSystem.MinMaxCurve(-hazeDrift, hazeDrift);
            velocity.y = new ParticleSystem.MinMaxCurve(-hazeDrift * 0.3f, hazeDrift * 0.6f);
            velocity.z = new ParticleSystem.MinMaxCurve(-hazeDrift * 0.5f, hazeDrift * 0.5f);

            // 大きくゆっくりうねる
            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = 0.05f;
            noise.frequency = 0.25f;
            noise.scrollSpeed = 0.12f;
            noise.damping = true;
            noise.octaveCount = 1;
            noise.quality = ParticleSystemNoiseQuality.Medium;

            // ゆっくり膨らむ
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.75f, 1f, 1.2f));

            // ゆっくり現れて、ゆっくり消える（ぱっと出たり消えたりしない）
            AlphaOverLife(ps, 0.25f, 1f, 0.7f);
            haze = ps;
            return ps;
        }

        /// <summary>吸い終えた後の煙の、共通の作り（世界の座標で動かす・止めてある・口元の煙と同じマテリアル）</summary>
        ParticleSystem NewSystem(string name, Transform parent, Vector3 at)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = at;
            go.transform.rotation = Quaternion.identity;
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true;
            main.playOnAwake = false;
            main.duration = 5f;
            main.startSpeed = 0f;
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = 0f;
            var emission = ps.emission;
            emission.enabled = true;
            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            var from = puffs != null ? puffs.GetComponent<ParticleSystemRenderer>() : null;
            if (from != null)
            {
                renderer.sharedMaterial = from.sharedMaterial;
                renderer.sortingFudge = from.sortingFudge;
            }
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
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

        /// <summary>体（Player の根）から見た右・上・前の量 v を、世界の向きへ直す。withUp でなければ上下は 0</summary>
        Vector3 Body(Vector3 v, bool withUp)
        {
            var body = transform.root;
            var right = Flat(body.right);
            var ahead = Flat(body.forward);
            return right * v.x + ahead * v.z + (withUp ? Vector3.up * v.y : Vector3.zero);
        }

        /// <summary>立てている煙を seconds 秒だけ長くする。時刻表を止めている間（場面 1 の 1 ページを読んでいる間）に呼ぶ</summary>
        public void Extend(float seconds)
        {
            if (until < 0f || seconds <= 0f) return;
            until += seconds;
        }

        /// <summary>
        /// ひと息ぶんを吐き出す。細く燻る煙とは別に、前へ向けて大きな塊をまとめて出す
        /// </summary>
        public void Blow()
        {
            if (puffs == null) return;
            // 出どころは上を向けてあるので、吐く向きは親（カメラ）の正面から取る。
            // まっすぐ前へ出して、少し上へ抜けていく
            var face = transform.parent != null ? transform.parent.forward : transform.forward;
            // 口から前へ出るのは最初だけ。すぐ上へ向かうので、初速も上を強くする
            var ahead = (face * 0.80f + Vector3.up * 0.60f).normalized;
            for (var i = 0; i < blowCount; i++)
            {
                // 口から出るほど細く速く、先へ行くほど広がって遅い。一息の形にする
                var along = (float)i / Mathf.Max(1, blowCount - 1);
                var spread = Random.insideUnitSphere * blowSpread * (0.25f + along);
                var p = new ParticleSystem.EmitParams();
                p.position = transform.position + face * (along * 0.12f) + spread * 0.6f;
                p.velocity = ahead * Random.Range(0.34f, 0.62f) * (1.15f - along * 0.5f) + spread * 0.7f;
                p.startSize = Random.Range(0.13f, 0.24f) * (0.75f + along * 0.6f);
                p.startLifetime = Random.Range(3.8f, 6.0f);
                // 1 粒は薄く。重なったところだけ濃くなる
                p.startColor = new Color(0.82f, 0.81f, 0.78f, Random.Range(0.26f, 0.40f));
                p.rotation = Random.Range(0f, 360f);
                puffs.Emit(p, 1);
            }
        }

        /// <summary>水平にならした向き。真上・真下なら前（z）</summary>
        static Vector3 Flat(Vector3 v)
        {
            v.y = 0f;
            return v.sqrMagnitude > 1e-6f ? v.normalized : Vector3.forward;
        }

        /// <summary>止める。すでに出た粒はそのまま流れて消える</summary>
        public void Cancel()
        {
            until = -1f;
            lingering = false;
            Unfade();
            if (puffs != null) puffs.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            if (wisp != null) wisp.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            if (haze != null) haze.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            afterOn = false;
        }

        /// <summary>細くしていたら、出す数と粒の大きさを戻す</summary>
        void Unfade()
        {
            if (fadeStart >= 0f)
                for (var i = 0; i < fading.Length; i++)
                {
                    var ps = fading[i];
                    if (ps == null) continue;
                    var emission = ps.emission;
                    emission.rateOverTimeMultiplier = baseRate[i];
                    var main = ps.main;
                    main.startSize = new ParticleSystem.MinMaxCurve(baseSizeMin[i], baseSizeMax[i]);
                }
            fadeStart = -1f;
            fading = new ParticleSystem[0];
        }

        void Update()
        {
            if (fadeStart >= 0f)
            {
                var thin = Thin(Time.time - fadeStart, fadeSeconds);
                // 大きさは二つの定数で持っているので、倍率ではなく定数ごと作り直す（倍率は片方にしか効かない）
                var k = Size(thin);
                for (var i = 0; i < fading.Length; i++)
                {
                    var ps = fading[i];
                    if (ps == null) continue;
                    var emission = ps.emission;
                    emission.rateOverTimeMultiplier = baseRate[i] * thin;
                    var main = ps.main;
                    main.startSize = new ParticleSystem.MinMaxCurve(baseSizeMin[i] * k, baseSizeMax[i] * k);
                }
                if (thin > 0f) return;
                Cancel();
                return;
            }
            // 吸い終えたら、口元の煙から指先の一筋とモニターの前を漂う煙へ替える
            if (lingering && until >= 0f && Time.time >= until && !afterOn) StartAfter(false);
            if (until < 0f || lingering || Time.time < until) return;
            Cancel();
        }
    }
}
