using UnityEngine;

namespace HalfAware
{
    /// <summary>
    /// 煙草の煙。吸っている間だけ粒を出し、止めた後も出ている分は消えるまで流れる。
    /// 出どころは口元に置き、粒は世界の座標で動かす。首を振っても煙は置き去りになる。
    ///
    /// **場面 1 は吸い終えても煙を立て続ける**（<see cref="Linger"/>。オーナー、2026-09-29「タバコ吸った後だけど、しばらくは煙草の煙を出し続けるようにして」）。
    /// 吸い終えたら口元の煙を止め、肘掛けに置いた右手の指先（煙草）の一点から昇る一筋に替える（<see cref="MakeWisp"/>）。
    /// 口元の燻る煙のままでは、机の上の薄い靄にしか見えなかった。一筋は空気の流れに乗って机の方（前）へ流れ、昇りながら広がって薄れ、
    /// その途中で正面のモニターの前を横切る（オーナー「ここまであからさまにしなくていい。煙の量は変えず、でも流れによってモニターにかぶるように」）。
    /// 流れはゆっくり揺らいで向きと強さが少しずつ変わる（<see cref="Draft"/>）ので、映り込みの顔や画面の文字にかかるのは時々・部分的に。
    /// わざと顔の前に留めはしない。前に試した、モニターの前に別に漂わせる煙はやめた。
    /// 一筋は吸い終えた後に立ち続ける間だけで、吸っている間（場面 1・5・8）の口元の煙は変えない。
    /// モニターを済ませたら、出す数と粒の大きさを <see cref="Fade"/> の秒で細くして止める（ふっと消さない。出ていた粒は寿命まで流れて、顔と文字がはっきり見えてくる）
    /// </summary>
    public sealed class SmokePuffs : MonoBehaviour
    {
        [SerializeField] ParticleSystem puffs;
        [Tooltip("ひと息で吐き出す粒の数。薄いものを数多く重ねて煙の塊にする")]
        [SerializeField] int blowCount = 90;
        [Tooltip("吐いた煙を撒く広さ。メートル")]
        [SerializeField] float blowSpread = 0.13f;

        // ---- 吸い終えた後の一筋（場面 1。右手の指先から）。場面ファイルに持たせない（複数の担当が場面を保存し直すので、古い値が残らないよう定数にする）
        /// <summary>一筋の粒を出す数。毎秒。粒が重なって筋に見えるだけ出す</summary>
        const float WispRate = 26f;
        /// <summary>昇る速さ。m/秒（粒ごとにこの 0.85〜1.15 倍）</summary>
        const float WispRise = 0.15f;
        /// <summary>粒の寿命。秒。昇る速さとの積がおおよその高さ、流れる速さとの積がおおよその流れる先</summary>
        const float WispLife = 5.5f;
        /// <summary>
        /// 空気の流れ。体の内側（x）と前（y）へ流す速さ。m/秒。指先（机の手前の右）から、昇りながら机とモニターの方へ流れ、
        /// 寿命の間に 0.7 m ほど前へ進んで、目の高さの辺りでモニターの前を横切る
        /// </summary>
        static readonly Vector2 WispDraft = new Vector2(0.04f, 0.13f);
        /// <summary>流れの向きの揺らぎ。度（片側）。ゆっくり左右へ振れる</summary>
        const float WispWander = 28f;
        /// <summary>流れの強さの揺らぎ。1 に対する割合（片側）</summary>
        const float WispGust = 0.3f;
        /// <summary>流れが揺らぐ速さ。一巡りがおおよそこの逆数の秒</summary>
        const float WispWanderRate = 0.05f;
        /// <summary>出る所の粒の大きさ。m。昇るほど WispSpread 倍まで太る</summary>
        const float WispSize = 0.026f;
        /// <summary>昇りきった所の太り方。出る所の大きさに対する倍。流れながら広がって薄れる</summary>
        const float WispSpread = 6f;
        /// <summary>横の揺れの強さ。昇るほど強くなる</summary>
        const float WispSway = 0.11f;
        /// <summary>粒の濃さ（不透明さ）。口元の燻る煙（0.17〜0.26）より明るく</summary>
        const float WispAlpha = 0.6f;
        /// <summary>指先の骨から、煙草の火の所までの高さ。m</summary>
        const float WispLift = 0.02f;

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
        /// <summary>吸い終えた後の一筋。初めて要る時に作る</summary>
        ParticleSystem wisp;
        /// <summary>一筋を出す所（右手の指先の骨）。無ければ口元から出す</summary>
        Transform wispAnchor;
        /// <summary>吸い終えた後の一筋を立てているか（口元の煙から替わった後）</summary>
        bool afterOn;

        /// <summary>今このとき煙を出しているか。動作確認から読む</summary>
        public bool Emitting { get { return until > 0f && (lingering || fadeStart >= 0f || Time.time < until); } }

        /// <summary>吸い終えた後も立て続けているか（細くしている途中は含まない）。動作確認から読む</summary>
        public bool Lingering { get { return lingering; } }

        /// <summary>細くしている途中か。動作確認から読む</summary>
        public bool Fading { get { return fadeStart >= 0f; } }

        /// <summary>吸い終えた後の一筋を立てているか。動作確認から読む</summary>
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
            fading = afterOn ? new[] { wisp } : new[] { puffs };
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

        /// <summary>口元の煙を止めて、一筋を立てる。settled なら、もう流れている形から始める</summary>
        void StartAfter(bool settled)
        {
            if (afterOn) return;
            var w = MakeWisp(wispAnchor);
            if (puffs != null) puffs.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            Breeze(Time.time);
            // 思い出した時は寿命ひと回りぶん進めて、流れている形から。吸い終えた時は出る所から少しずつ
            if (settled) w.Simulate(w.main.startLifetime.constantMax, true, true);
            w.Play();
            afterOn = true;
        }

        /// <summary>
        /// 時刻 t の空気の流れ。x は流れの向きのずれ（度。体の内側と前の間の向きから、上から見て右回りに正）、y は強さの倍率。
        /// ゆっくり揺らいで、少しずつ変わる（Perlin の雑音。同じ t なら同じ値）
        /// </summary>
        public static Vector2 Draft(float t)
        {
            // 雑音の面を斜めに切って読む（軸に沿って読むと、格子の点を決まった間隔で通って同じ値に戻る）
            var k = t * WispWanderRate;
            var turn = (Mathf.PerlinNoise(k, k * 0.61f + 0.37f) - 0.5f) * 2f * WispWander;
            var gust = 1f + (Mathf.PerlinNoise(k * 0.73f + 5.1f, k + 11.3f) - 0.5f) * 2f * WispGust;
            return new Vector2(turn, gust);
        }

        /// <summary>
        /// 一筋を時刻 t の空気の流れに乗せる。流れは出ている粒のすべてに効く（空気が動けば、昇った煙もまとめて流れる）。
        /// 遊ぶ間は毎こま、エディタで撮るときは撮る前に呼ぶ
        /// </summary>
        public void Breeze(float t)
        {
            if (wisp == null) return;
            var d = Draft(t);
            var along = Quaternion.AngleAxis(d.x, Vector3.up) * Body(new Vector3(-WispDraft.x, 0f, WispDraft.y), false) * d.y;
            var velocity = wisp.velocityOverLifetime;
            velocity.x = new ParticleSystem.MinMaxCurve(along.x - 0.006f, along.x + 0.006f);
            velocity.z = new ParticleSystem.MinMaxCurve(along.z - 0.006f, along.z + 0.006f);
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
            var ps = NewSystem("SmokeWisp", at, at.position + Vector3.up * WispLift);

            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(WispLife * 0.85f, WispLife * 1.1f);
            // 大きさは昇りきった所の大きさで持ち、昇るほど太る曲線（出る所で 1/WispSpread）を掛ける
            main.startSize = new ParticleSystem.MinMaxCurve(WispSize * WispSpread * 0.8f, WispSize * WispSpread * 1.2f);
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(0.90f, 0.89f, 0.87f, WispAlpha), new Color(0.84f, 0.83f, 0.81f, WispAlpha * 0.8f));
            main.maxParticles = 200;

            var emission = ps.emission;
            emission.rateOverTime = WispRate;

            // 一点から出す
            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.002f;

            // 昇る。粒ごとに速さを少し違えて、筋が途切れず伸びるように。横（空気の流れ）は Breeze が毎こま決める
            var velocity = ps.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.x = new ParticleSystem.MinMaxCurve(-0.006f, 0.006f);
            velocity.y = new ParticleSystem.MinMaxCurve(WispRise * 0.85f, WispRise * 1.15f);
            velocity.z = new ParticleSystem.MinMaxCurve(-0.006f, 0.006f);

            // 横の揺れ。出る所では細くまっすぐ、昇るほど大きく揺れる
            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = new ParticleSystem.MinMaxCurve(WispSway, AnimationCurve.EaseInOut(0f, 0.05f, 1f, 1f));
            noise.frequency = 0.9f;
            noise.scrollSpeed = 0.35f;
            noise.damping = true;
            noise.octaveCount = 1;
            noise.quality = ParticleSystemNoiseQuality.Medium;

            // 流れながら広がる
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 1f / Mathf.Max(1f, WispSpread), 1f, 1f));

            // 出てすぐ濃くなり、流れて広がるにつれ薄れて消える
            AlphaOverLife(ps, 0.08f, 0.6f, 0.55f);
            wisp = ps;
            Breeze(Time.time);
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
            // 吸い終えたら、口元の煙から指先の一筋へ替える。立てている間は空気の流れを揺らがせる
            if (lingering && until >= 0f && Time.time >= until && !afterOn) StartAfter(false);
            if (afterOn) Breeze(Time.time);
            if (until < 0f || lingering || Time.time < until) return;
            Cancel();
        }
    }
}
