using UnityEngine;

namespace HalfAware
{
    /// <summary>
    /// 場面 2 の歩く人（オーナー、2026-09-30「モブに動きがなさ過ぎて流石に違和感がある」）。決まった道筋（<see cref="points"/>）を
    /// 頭から尻まで歩き、尻から頭へ戻って、それを繰り返す。道の途中の点で立ち止まって、店や出店を覗く（<see cref="waits"/> 秒、<see cref="faces"/> の向きへ）。
    ///
    /// 動かすのは人の根だけ。足の運び（歩き・立ち）は <see cref="PersonMotion"/> が根の進みから決める。
    ///
    /// - **折り返しは主人公から見えにくい所で。** 道の両端は通りの外れや小路の奥に置き、立ち止まる秒の無い端に着いたら、
    ///   その人が画面に写っていない時まで待ってから向きを変える（<see cref="turnHidden"/>）。店や出店を覗いて立ち止まる端は、覗き終えたら向きを変える
    /// - **主人公の前に来たら立ち止まる。** 進む先の <see cref="yield"/> m の内に主人公がいれば、居なくなるまで待つ（道探しはしない）。
    ///   歩く人どうしは、先に置いた人を後の人が待つ（互いに待って動けなくならない）
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CrowdWalk : MonoBehaviour
    {
        [Tooltip("道筋。親（Alley/Crowd/Walkers）のローカル。頭から尻へ歩き、尻から頭へ戻る")]
        [SerializeField] Vector3[] points = new Vector3[0];
        [Tooltip("その点で立ち止まる秒。points と同じ数。0 なら止まらずに通る")]
        [SerializeField] float[] waits = new float[0];
        [Tooltip("立ち止まっている間に向く向き。度（親のローカル）。NaN なら歩いてきた向きのまま")]
        [SerializeField] float[] faces = new float[0];
        [Tooltip("歩く速さ。m/s")]
        [SerializeField] float speed = 1.2f;
        [Tooltip("始まりの位置。道筋の頭から尻を 0〜1、尻から頭を 1〜2 で数える")]
        [SerializeField] float start;
        [Tooltip("両端では、写っていない時まで待ってから向きを変える")]
        [SerializeField] bool turnHidden = true;
        [Tooltip("進む先のこの距離の内に主人公がいれば立ち止まる。m")]
        [SerializeField] float yield = 1.5f;
        [Tooltip("主人公の体。空なら PlayerController を拾う")]
        [SerializeField] Transform player;
        [Tooltip("写っているかを見る描く物")]
        [SerializeField] Renderer[] seen = new Renderer[0];

        /// <summary>今いる区間（i から i+1、戻りは逆向き）と、その中の進み（m）</summary>
        /// <summary>場面の中の歩く人。先に有効になった人ほど前。後の人が前の人を待つ</summary>
        static readonly System.Collections.Generic.List<CrowdWalk> all = new System.Collections.Generic.List<CrowdWalk>();

        int leg;
        bool back;
        float along;
        float waited;
        bool waiting;

        public int Leg { get { return leg; } }
        public bool Back { get { return back; } }
        public bool Waiting { get { return waiting; } }

        // ---- 決まり（試験から呼ぶ） ------------------------------------------------

        /// <summary>道筋の長さ。m</summary>
        public static float Length(Vector3[] path)
        {
            var n = 0f;
            for (var i = 1; i < path.Length; i++) n += Vector3.Distance(path[i - 1], path[i]);
            return n;
        }

        /// <summary>
        /// 主人公が進む先の reach m の内にいて、道を塞いでいるか。横へ 0.9 m より離れていれば塞いでいない
        /// </summary>
        public static bool Blocked(Vector3 at, Vector3 heading, Vector3 other, float reach)
        {
            heading.y = 0f;
            if (heading.sqrMagnitude < 1e-6f) return false;
            heading.Normalize();
            var d = other - at;
            d.y = 0f;
            var ahead = Vector3.Dot(d, heading);
            if (ahead < 0f || ahead > reach) return false;
            var side = (d - heading * ahead).magnitude;
            return side < 0.9f;
        }

        // ---- 流れ -----------------------------------------------------------------

        void Awake()
        {
            if (player == null)
            {
                var p = FindFirstObjectByType<PlayerController>();
                if (p != null) player = p.transform;
            }
            Seek(start);
        }

        void OnEnable()
        {
            if (!all.Contains(this)) all.Add(this);
        }

        void OnDisable()
        {
            all.Remove(this);
        }

        /// <summary>道筋を頭から尻を 0〜1、戻りを 1〜2 として、u の所へ置く</summary>
        public void Seek(float u)
        {
            if (points.Length < 2) return;
            u = Mathf.Repeat(u, 2f);
            back = u >= 1f;
            var k = back ? u - 1f : u;
            var total = Length(points);
            var want = k * total;
            leg = 0;
            along = 0f;
            for (var i = 0; i < points.Length - 1; i++)
            {
                var len = Vector3.Distance(points[i], points[i + 1]);
                if (want <= len || i == points.Length - 2)
                {
                    leg = back ? points.Length - 2 - i : i;
                    along = back ? len - Mathf.Min(want, len) : Mathf.Min(want, len);
                    break;
                }
                want -= len;
            }
            Place();
        }

        void Update()
        {
            if (points.Length < 2) return;
            var dt = Time.deltaTime;
            if (waiting)
            {
                waited -= dt;
                var end = !back ? leg + 1 : leg;
                var at = end == 0 || end == points.Length - 1;
                var browse = end < waits.Length && waits[end] > 0f;
                if (waited > 0f || (at && turnHidden && !browse && Visible())) return;
                waiting = false;
                // 端では向きを変えて戻る
                if (end == points.Length - 1 && !back) { back = true; along = Vector3.Distance(points[leg], points[leg + 1]); }
                else if (end == 0 && back) { back = false; along = 0f; }
                else
                {
                    // 途中の点。次の区間へ
                    if (!back) { leg++; along = 0f; }
                    else { leg--; along = Vector3.Distance(points[leg], points[leg + 1]); }
                }
                return;
            }
            var a = points[leg];
            var b = points[leg + 1];
            var span = Vector3.Distance(a, b);
            var heading = (back ? a - b : b - a);
            heading = transform.parent != null ? transform.parent.TransformDirection(heading) : heading;
            // 根も進む向きへ向けておく。主人公に道を塞がれて立ち止まった時に、体が前を向いたままでいる
            var flat = new Vector3(heading.x, 0f, heading.z);
            if (flat.sqrMagnitude > 1e-6f) transform.rotation = Quaternion.LookRotation(flat, Vector3.up);
            if (player != null && Blocked(transform.position, heading, player.position, yield)) return;
            // 前の歩く人が進む先にいれば待つ
            var me = all.IndexOf(this);
            for (var i = 0; i < me; i++)
                if (all[i] != null && Blocked(transform.position, heading, all[i].transform.position, 1.2f)) return;
            var step = speed * dt;
            if (!back)
            {
                along += step;
                if (along >= span) { along = span; Arrive(leg + 1); }
            }
            else
            {
                along -= step;
                if (along <= 0f) { along = 0f; Arrive(leg); }
            }
            Place();
        }

        /// <summary>点 i に着いた。止まる秒があれば止まり、端なら止まって向きを変える</summary>
        void Arrive(int i)
        {
            var wait = i < waits.Length ? waits[i] : 0f;
            var end = i == 0 || i == points.Length - 1;
            if (wait <= 0f && !end)
            {
                if (!back) { leg++; along = 0f; }
                else { leg--; along = Vector3.Distance(points[leg], points[leg + 1]); }
                return;
            }
            waiting = true;
            waited = wait;
            if (i < faces.Length && !float.IsNaN(faces[i]))
            {
                var parent = transform.parent != null ? transform.parent.rotation : Quaternion.identity;
                transform.rotation = parent * Quaternion.Euler(0f, faces[i], 0f);
            }
        }

        /// <summary>根を今の所へ置く。体の向きは PersonMotion が歩いた向きから決め、立ち止まっている間は根の向きへ戻す</summary>
        void Place()
        {
            var a = points[leg];
            var b = points[leg + 1];
            var span = Vector3.Distance(a, b);
            var p = span > 1e-4f ? Vector3.Lerp(a, b, along / span) : a;
            transform.localPosition = p;
        }

        bool Visible()
        {
            foreach (var r in seen) if (r != null && r.isVisible) return true;
            return false;
        }
    }
}
