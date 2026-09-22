using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace HalfAware.EditorTools
{
    /// <summary>
    /// 場面 4 の団地。手すりの外の景色と隣の棟。
    ///
    /// **手すりの向こうは背景ではなく距離の作り。** 高さの違う物を手前から奥へ重ねないと、
    /// 一枚の書き割りになって、廊下を歩いても景色が動かない。
    /// 電柱・電線・駐輪場・物干し台・低い棟・隣の棟を、別々の奥行きに置く。
    ///
    /// 寸法は場所のローカル。鍵打ち（<c>BuildDiveTakes.cs</c>）が同じ数を見るので、
    /// 印になる点は <c>BuildDiveEstate.cs</c> の const に置いて両方から見る
    /// </summary>
    public static partial class BuildDive
    {
        // ---- 手すりの外 --------------------------------------------------------

        /// <summary>
        /// 手すりの向こう。電柱と電線、駐輪場の屋根、物干し台、低い棟、室外機の列。
        ///
        /// **高さの違う物を手前から奥へ重ねる。** 隣の棟の書き割りだけでは、
        /// 手すりの外が一枚の絵になって距離が出ない。
        /// 電線は目の高さのすぐ下を横切るので、廊下を歩くあいだ景色がいちばん大きく動く
        /// </summary>
        static void EstateYard(EstateBanks b)
        {
            // 電柱。三本を等間隔で、廊下の目の高さより少し上まで
            var poles = new[] { -4.5f, 8.5f, 21.5f };
            foreach (var x in poles)
            {
                b.Gear.Box(new Vector3(x, 4.6f, -3.2f), new Vector3(0.24f, 9.2f, 0.24f));
                for (var i = 0; i < 2; i++)
                    b.Gear.Box(new Vector3(x, 8.30f + i * 0.55f, -3.2f), new Vector3(0.09f, 0.09f, 1.7f));
                b.Gear.Box(new Vector3(x, 6.30f, -3.2f), new Vector3(0.52f, 0.62f, 0.44f));
            }
            // 電線。撓みが無いと物差しを渡したように見える
            var hang = new[] { -14f, -4.5f, 8.5f, 21.5f, 25f };
            for (var i = 0; i + 1 < hang.Length; i++)
            {
                for (var w = 0; w < 3; w++)
                {
                    var y = 8.24f + (w - 1) * 0.28f;
                    var z = -3.2f + (w - 1) * 0.62f;
                    EstateWire(b.Gear, new Vector3(hang[i], y, z), new Vector3(hang[i + 1], y, z), 0.34f, 0.035f);
                }
            }
            // もう一本を奥へ渡す。線が一面だけだと、手前と奥の区別が付かない
            EstateWire(b.Gear, new Vector3(-14f, 9.10f, 1.4f), new Vector3(25f, 8.90f, 1.4f), 0.9f, 0.035f);

            // 駐輪場。屋根が水平に一枚あると、地面との間に距離が生まれる
            b.Gear.Box(new Vector3(5.3f, 2.34f, -8.5f), new Vector3(8.2f, 0.09f, 2.6f));
            for (var i = 0; i < 16; i++)
                b.Shade.Box(new Vector3(1.45f + i * 0.51f, 2.40f, -8.5f), new Vector3(0.05f, 0.05f, 2.6f));
            for (var i = 0; i < 6; i++)
                b.Gear.Box(new Vector3(1.5f + (i % 3) * 3.8f, 1.17f, i < 3 ? -9.6f : -7.4f),
                    new Vector3(0.12f, 2.34f, 0.12f));
            for (var i = 0; i < 7; i++) EstateBike(b, 2.0f + i * 1.05f, 0f, -8.4f);

            // 物干し台。地面の上に洗濯物が一枚あると、そこが暮らしの庭だと分かる
            for (var i = 0; i < 2; i++)
                b.Gear.Box(new Vector3(13.0f + i * 2.2f, 0.90f, -5.0f), new Vector3(0.10f, 1.80f, 0.10f));
            b.Gear.Box(new Vector3(14.1f, 1.76f, -5.0f), new Vector3(2.4f, 0.05f, 0.05f));
            for (var i = 0; i < 4; i++)
                b.Linen.Box(new Vector3(13.3f + i * 0.54f, 1.42f, -5.0f), new Vector3(0.42f, 0.62f, 0.02f));

            // 低い棟。隣の棟より手前に低い塊があると、奥行きが二段になる
            b.Wall.Box(new Vector3(16.0f, 1.70f, -11.4f), new Vector3(8.0f, 3.40f, 4.4f));
            b.Wall.Box(new Vector3(16.0f, 3.52f, -11.4f), new Vector3(8.4f, 0.24f, 4.8f));
            for (var i = 0; i < 4; i++)
                b.Shade.FaceZ(-9.18f, 13.1f + i * 1.9f, 14.1f + i * 1.9f, 1.10f, 2.30f, -1);
            // 室外機の列。低い棟の脇に並べる。同じ箱が等間隔で並ぶ形がもう一つ増える
            for (var i = 0; i < 5; i++)
            {
                var x = 10.6f + i * 0.98f;
                b.Gear.Box(new Vector3(x, 0.32f, -9.0f), new Vector3(0.80f, 0.58f, 0.36f));
                b.Shade.Box(new Vector3(x, 0.32f, -8.81f), new Vector3(0.62f, 0.40f, 0.02f));
            }
        }

        /// <summary>
        /// 撓んだ線を一本。真っ直ぐな棒では電線に見えないので、
        /// 途中の点を拾って短い棒で繋ぐ
        /// </summary>
        static void EstateWire(Bank b, Vector3 from, Vector3 to, float sag, float thick)
        {
            const int span = 6;
            var last = from;
            for (var i = 1; i <= span; i++)
            {
                var k = i / (float)span;
                var at = Vector3.Lerp(from, to, k);
                at.y -= sag * 4f * k * (1f - k);
                var dir = at - last;
                if (dir.sqrMagnitude > 1e-6f)
                    b.Box((last + at) * 0.5f, new Vector3(thick, thick, dir.magnitude),
                        Quaternion.LookRotation(dir, Vector3.up));
                last = at;
            }
        }

        // ---- 隣の棟 ------------------------------------------------------------

        /// <summary>
        /// 隣の棟。窓の四角が等間隔に並ぶだけの書き割り。
        ///
        /// **灯りを当てない。** 面を Unlit で持たせると、朝日が回らない側でも値が決まり、
        /// 夜明けの薄い空を背にした輪郭がそのまま出る。当たりも影も持たせない。
        /// バルコニーの帯と屋上の塔屋を重ねるのは、窓の列だけでは面が平らに沈むため
        /// </summary>
        static void EstateBlockWall(Transform place, EstateBanks b)
        {
            var slabs = new Bank { Texel = 0.4f };
            slabs.FaceZ(BlockFace, -13.5f, 24.5f, 0f, 15.6f, -1);
            NoShadow(EstateEmit(place, "EstateBlock", slabs,
                Glow("EstateBlock", new Color(0.50f, 0.53f, 0.60f), 0.40f), false));

            var panes = new Bank { Texel = 0.4f };
            // 階ごとの水平の帯。窓の列だけだと面が平らすぎて、棟の高さが出ない
            for (var j = 0; j < 5; j++)
                panes.FaceZ(BlockFace - 0.12f, -13.5f, 24.5f, 1.22f + j * Floor, 1.38f + j * Floor, -1);
            for (var i = 0; i < 14; i++)
            {
                for (var j = 0; j < 5; j++)
                {
                    var x = -11.2f + i * 2.6f;
                    var y = 1.6f + j * Floor;
                    // 数枚だけ灯りが入っている。全部暗いと廃墟に、全部明るいと看板に見える
                    var into = (i * 5 + j * 3) % 13 == 0 ? b.Lit : panes;
                    into.FaceZ(BlockFace - 0.16f, x - 0.72f, x + 0.72f, y, y + 1.15f, -1);
                    // バルコニーの手すり。**窓より明るい側に置く。**
                    // 暗い帯にすると窓と繋がって、棟ぜんたいが黒い塊になる。
                    // 朝日の当たる手すりが明るく、その下の影が暗いと、階の帯が横へ通る
                    b.Far.Box(new Vector3(x, y - 0.42f, BlockFace - 0.42f), new Vector3(1.90f, 0.86f, 0.10f));
                    panes.Box(new Vector3(x, y - 0.84f, BlockFace - 0.22f), new Vector3(1.94f, 0.08f, 0.44f));
                    // 竿と洗濯物。明るい手すりの前に暗い形が下がると、棟に人が住んでいることになる
                    if ((i + j) % 3 != 0) continue;
                    panes.Box(new Vector3(x, y + 0.02f, BlockFace - 0.50f), new Vector3(1.70f, 0.05f, 0.05f));
                    for (var k = 0; k < 3; k++)
                        panes.Box(new Vector3(x - 0.5f + k * 0.5f, y - 0.26f, BlockFace - 0.50f),
                            new Vector3(0.36f, 0.52f, 0.02f));
                }
            }
            // 屋上。立ち上がりと塔屋と水槽。棟の頭が切り落とされて見えないように
            panes.Box(new Vector3(5.5f, 15.85f, BlockFace - 0.18f), new Vector3(38f, 0.5f, 0.36f));
            panes.Box(new Vector3(-2.5f, 16.9f, BlockFace - 0.6f), new Vector3(4.2f, 2.6f, 1.2f));
            panes.Box(new Vector3(12.5f, 18.1f, BlockFace - 0.6f), new Vector3(3.0f, 1.8f, 1.2f));
            for (var i = 0; i < 4; i++)
                panes.Box(new Vector3(11.4f + (i % 2) * 2.2f, 16.7f, BlockFace - 0.3f - (i / 2) * 0.6f),
                    new Vector3(0.16f, 3.0f, 0.16f));
            NoShadow(EstateEmit(place, "EstateBlockPane", panes, Mat("Ceiling"), false));

            // さらに奥の棟。手前の棟に隠れて頭だけが出る。
            // 遠いほど薄く見えるので、手前の棟より明るい色にして距離を出す
            b.Far.FaceZ(14f, -13f, 24f, 9f, 19.5f, -1);
            for (var i = 0; i < 12; i++)
                b.Far.FaceZ(13.96f, -11.5f + i * 3f, -10.2f + i * 3f, 16.4f, 17.5f, -1);
        }
    }
}
