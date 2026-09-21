using UnityEditor;
using UnityEngine;

namespace HalfAware.EditorTools
{
    /// <summary>
    /// 場面 4 の電車。
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
        // ---- 電車 ------------------------------------------------------------

        /// <summary>車両の半幅</summary>
        const float CarHalf = 1.3f;
        /// <summary>車両の半分の長さ。一両ぶんで 8 m</summary>
        const float CarLong = 4f;
        /// <summary>天井</summary>
        const float CarHigh = 2.3f;
        /// <summary>
        /// 吊り革の棒の高さと、中心からの寄り。
        /// **棒は目より高く通す。** 1.95 に下げていたときは、輪がちょうど 1.58 の目の前に垂れて、
        /// 掴まっている人の視界を黒い四角が半分塞いでいた
        /// </summary>
        public const float StrapY = 2.05f;
        public const float StrapX = 0.55f;
        /// <summary>座席の座面</summary>
        public const float SeatY = 0.42f;
        /// <summary>座席の中心の x。両側にある</summary>
        public const float SeatX = 1.02f;


        // ---- 電車 --------------------------------------------------------------

        /// <summary>
        /// 記憶 4・11 の舞台。一両ぶんの箱に座席二列と吊り革。
        /// 窓の外は光る帯で済ませる。流れるのは <c>WindowStream</c> の受け持ちで、
        /// 出来るまではこの帯が止まったまま光っている
        /// </summary>
        static void Train(Transform place)
        {
            var deck = new Bank { Texel = 0.5f };
            deck.FaceY(0f, -CarHalf, CarHalf, -CarLong, CarLong, 1);
            Emit(place, "TrainFloor", deck, "Floor", true);

            var shell = new Bank { Texel = 0.4f };
            // 側壁。窓の帯を抜く
            var holes = new System.Collections.Generic.List<Vector4>
            {
                new Vector4(-CarLong + 0.4f, CarLong - 0.4f, 1.0f, 1.8f),
            };
            shell.FaceXHoles(-CarHalf, -CarLong, CarLong, 0f, CarHigh, 1, holes);
            shell.FaceXHoles(CarHalf, -CarLong, CarLong, 0f, CarHigh, -1, holes);
            shell.FaceZ(-CarLong, -CarHalf, CarHalf, 0f, CarHigh, 1);
            shell.FaceZ(CarLong, -CarHalf, CarHalf, 0f, CarHigh, -1);
            Emit(place, "TrainShell", shell, "Wall", true);

            var roof = new Bank { Texel = 0.4f };
            roof.FaceY(CarHigh, -CarHalf, CarHalf, -CarLong, CarLong, -1);
            Emit(place, "TrainRoof", roof, "Ceiling");

            var seat = new Bank { Texel = 0.5f };
            for (var i = 0; i < 2; i++)
            {
                var x = (i == 0 ? -1f : 1f) * SeatX;
                seat.Box(new Vector3(x, SeatY, 0f), new Vector3(0.52f, 0.08f, CarLong * 2f - 1.2f));
                seat.Box(new Vector3(x + (i == 0 ? -0.22f : 0.22f), 0.78f, 0f),
                    new Vector3(0.08f, 0.64f, CarLong * 2f - 1.2f));
            }
            Emit(place, "TrainSeat", seat, "Cloth");

            var pole = new Bank { Texel = 0.5f };
            for (var i = 0; i < 2; i++)
            {
                var x = (i == 0 ? -1f : 1f) * StrapX;
                pole.Box(new Vector3(x, StrapY, 0f), new Vector3(0.04f, 0.04f, CarLong * 2f - 0.8f));
                for (var k = 0; k < 9; k++)
                {
                    var z = -3.2f + k * 0.8f;
                    pole.Box(new Vector3(x, StrapY - 0.12f, z), new Vector3(0.02f, 0.24f, 0.02f));
                    pole.Box(new Vector3(x, StrapY - 0.30f, z), new Vector3(0.11f, 0.13f, 0.02f));
                }
            }
            Emit(place, "TrainPole", pole, "Rail");

            // 窓の外。夕方の街の灯りは一色の帯で足りる
            var stream = Glow("GlowStream", new Color(0.86f, 0.80f, 0.62f), 0.45f);
            for (var i = 0; i < 2; i++)
            {
                var x = (i == 0 ? -1f : 1f) * (CarHalf + 0.03f);
                Pane(place, "TrainWindow" + i, new Vector3(x, 1.4f, 0f), new Vector2(CarLong * 2f - 0.8f, 0.8f),
                    i == 0 ? Vector3.right : Vector3.left, stream);
            }

            Lamp(place, "Fluorescent", LightType.Point, new Vector3(0f, CarHigh - 0.15f, 0f),
                Vector3.zero, new Color(0.90f, 0.94f, 1f), 4.5f, 11f);
            Pane(place, "TrainTube", new Vector3(0f, CarHigh - 0.03f, 0f), new Vector2(0.22f, CarLong * 2f - 1.4f),
                Vector3.down, Glow("GlowTube", new Color(0.94f, 0.97f, 1f), 1.6f));
        }
    }
}
