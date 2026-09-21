using UnityEditor;
using UnityEngine;

namespace HalfAware.EditorTools
{
    /// <summary>
    /// 場面 4 の公園。
    ///
    /// **一目でどこか分かることを条件に組む。** 記憶は他人の頭から抜いてきた絵なので
    /// 隅々まで見えている必要はないが、床と壁だけの箱では何の場所か読めない。
    /// 等間隔に並ぶものを一つ入れると、その場所らしさがいちばん早く伝わる。
    ///
    /// 寸法は場所のローカル。鍵打ち（<c>BuildDiveTakes.cs</c>）が同じ数を見るので、
    /// 目印になる点は const にして両方から見る
    /// </summary>
    public static partial class BuildDive
    {
        // ---- 公園 ------------------------------------------------------------

        /// <summary>左のベンチ。記憶 2 のアルベルトが掛けている</summary>
        public static readonly Vector3 BenchA = new Vector3(0f, 0f, 0f);
        /// <summary>右のベンチ。記憶 9 のローザが餌の袋を畳んでいる</summary>
        public static readonly Vector3 BenchB = new Vector3(2.6f, 0f, 0f);
        /// <summary>門。ベンチから真っ直ぐ +z</summary>
        public const float GateZ = 9.4f;
        const float GateHalf = 1.3f;
        /// <summary>池の縁の中心と半径。鳩はこの手前に集まる</summary>
        public static readonly Vector3 Pond = new Vector3(-4.2f, 0f, 4.6f);
        const float PondR = 2.8f;
        /// <summary>鳩の群れの真ん中</summary>
        public static readonly Vector3 Flock = new Vector3(0.6f, 0f, 3.6f);


        // ---- 公園 --------------------------------------------------------------

        /// <summary>
        /// 記憶 2・3・9・10 の舞台。ベンチ二つから門まで真っ直ぐ歩けるようにしてある。
        /// 池は縁の低い壁だけで、水は暗い面を一枚。遠くの木は板で済ませる
        /// </summary>
        static void Park(Transform place)
        {
            var dirt = new Bank { Texel = 0.25f };
            dirt.FaceY(0f, -9f, 9f, -9f, 13f, 1);
            Emit(place, "ParkGround", dirt, "Ground", true);

            var wood = new Bank { Texel = 0.5f };
            Bench(wood, BenchA);
            Bench(wood, BenchB);
            Emit(place, "ParkBench", wood, "Timber");

            var stone = new Bank { Texel = 0.4f };
            // 池の縁。ベンチの側だけ弧を描いていればよい
            for (var i = 0; i < 12; i++)
            {
                var a = Mathf.Deg2Rad * (150f + i * 12f);
                var b2 = Mathf.Deg2Rad * (150f + (i + 1) * 12f);
                var p = Pond + new Vector3(Mathf.Cos(a) * PondR, 0f, Mathf.Sin(a) * PondR);
                var q = Pond + new Vector3(Mathf.Cos(b2) * PondR, 0f, Mathf.Sin(b2) * PondR);
                var mid = (p + q) * 0.5f;
                var run = (q - p);
                stone.Box(new Vector3(mid.x, 0.16f, mid.z), new Vector3(0.36f, 0.32f, run.magnitude + 0.04f),
                    Quaternion.LookRotation(run.normalized, Vector3.up));
            }
            stone.Box(new Vector3(-GateHalf, 1.2f, GateZ), new Vector3(0.35f, 2.4f, 0.35f));
            stone.Box(new Vector3(GateHalf, 1.2f, GateZ), new Vector3(0.35f, 2.4f, 0.35f));
            Emit(place, "ParkStone", stone, "Wall", true);

            var pool = new Bank { Texel = 0.2f };
            pool.FaceY(0.06f, Pond.x - PondR, Pond.x + PondR, Pond.z - PondR * 0.4f, Pond.z + PondR, 1);
            Emit(place, "ParkWater", pool, "Water");

            // 遠くの木。板を並べるだけで、近づけないところに置く
            var far = new Bank { Texel = 0.2f };
            for (var i = 0; i < 9; i++)
            {
                var x = -8f + i * 2f;
                far.Box(new Vector3(x, 2.6f, -7.5f), new Vector3(2.2f, 5.2f, 0.12f));
            }
            for (var i = 0; i < 5; i++)
                far.Box(new Vector3(-8.4f, 2.6f, -4f + i * 3.4f), new Vector3(0.12f, 5.2f, 2.2f));
            NoShadow(Emit(place, "ParkTrees", far, "Leaf"));

            // 鳩は場所ではなく記憶の側に置く（BuildDiveTakes.Doves）。
            // 飛び立つ秒が記憶ごとに違うので、場所に置くと四つの記憶で同じ瞬間に飛ぶことになる

            Ring(place, "ParkFence", new Vector2(-9f, 9f), new Vector2(-9f, 13f), 3.5f);

            // 午後の低い日。**門の側（+z）から差す。** 門へ歩く人はこれで逆光になり、
            // 遠景の板の影も場所の外へ伸びる。逆向きに当てていたときは、板の影が
            // 十数メートルの帯になってベンチから池までを丸ごと夜にしていた
            var sun = Lamp(place, "Afternoon", LightType.Directional, new Vector3(0f, 12f, 0f),
                new Vector3(26f, 188f, 0f), new Color(1f, 0.92f, 0.76f), 2.2f, 10f);
            sun.shadows = LightShadows.Soft;
        }

        static void Bench(Bank b, Vector3 at)
        {
            b.Box(at + new Vector3(0f, 0.45f, 0f), new Vector3(1.8f, 0.07f, 0.46f));
            b.Box(at + new Vector3(0f, 0.72f, -0.24f), new Vector3(1.8f, 0.42f, 0.06f));
            for (var i = 0; i < 2; i++)
                b.Box(at + new Vector3((i == 0 ? -1f : 1f) * 0.78f, 0.22f, 0f), new Vector3(0.08f, 0.44f, 0.42f));
        }
    }
}
