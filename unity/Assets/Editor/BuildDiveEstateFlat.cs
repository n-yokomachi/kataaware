using System.Collections.Generic;
using UnityEngine;

namespace HalfAware.EditorTools
{
    /// <summary>
    /// 場面 4 の公営住宅。上のメゾネット（三・四階）の躯体。A と B で同じ間取りを使う。
    ///
    /// **二層の通り抜け。** 間口 5.4 m、奥行き 8.6 m。三階はデッキから入り、玄関の廊下の
    /// 西に台所（窓はデッキ側で玄関の脇）、突き当たりに居間（南）。玄関のすぐ先で
    /// 住戸の中の階段が南へ上がり、四階はデッキの上へ 2 m 張り出した表の寝室と、
    /// 浴室、奥の寝室。
    ///
    /// **戸口から覗いて見えるのは廊下と階段まで。** 戸は階段と廊下の境に開けてあり、
    /// 正面に階段が上がっていき、右に廊下が伸びる。居間は廊下の突き当たりの戸口の幅ぶんしか
    /// 見えない。戸を開けて居間が丸見えになるのは集合住宅の住戸ではない。
    ///
    /// **戸口と通路は壁から壁まで 1.0 m。** プレイヤーの体（CharacterController の半径 0.26 と
    /// skinWidth 0.08）は壁から 0.34 離れて止まるので、体の中心が動ける幅は 0.32 残る。
    /// 0.9 m の廊下では 0.22、0.8 m の戸口では 0.12 しかなく、真っ直ぐ入れなかった。
    /// 開いた戸の板は戸口の幅の外（壁の側）へ寄せ、戸口の幅を食わせない。
    ///
    /// 寸法は住戸のローカルで持つ。u は住戸の西の縁（戸境の壁の真ん中）から東へ、
    /// d はデッキ側の面から南（奥）へ。z は <see cref="FlatZ"/> で場所のローカルへ直す
    /// </summary>
    public static partial class BuildDive
    {
        // ---- 住戸の平面（u は西の縁から東へ、d はデッキ側の面から奥へ） ----------

        /// <summary>西の内壁の面</summary>
        const float FlatIn = PartyHalf;                                                     // 0.1
        /// <summary>東の内壁の面</summary>
        const float FlatInner = FlatWide - PartyHalf;                                       // 5.3
        /// <summary>デッキ側の外壁の内側</summary>
        const float FrontIn = FaceSkin;                                                     // 0.25
        /// <summary>南の外壁の内側</summary>
        const float FlatBackIn = FlatDeep - FaceSkin;                                       // 8.35

        // 三階
        /// <summary>台所と廊下の仕切りの真ん中。四階では廊下と浴室の仕切り</summary>
        const float KitchenWall = 3.15f;
        /// <summary>住戸の中の階段と廊下の仕切りの真ん中</summary>
        const float StairLine = 4.25f;
        /// <summary>廊下の西と東の面。幅 1.0 m。四階の廊下も同じ線</summary>
        const float LaneWest = KitchenWall + HallSkin;                                      // 3.2
        const float LaneEast = StairLine - HallSkin;                                        // 4.2
        /// <summary>住戸の中の階段の西の縁。東は戸境の壁で、幅 1.0 m</summary>
        const float TreadWest = StairLine + HallSkin;                                       // 4.3
        /// <summary>住戸の中の階段の最初の蹴上げ。玄関の戸から 1.3 m 奥</summary>
        const float InnerFoot = 1.3f;
        /// <summary>住戸の中の階段の踏み面</summary>
        const float InnerGo = 0.24f;
        /// <summary>住戸の中の階段の蹴上げの数。一階ぶんを 14 段で上がる</summary>
        const int InnerRisers = 14;
        /// <summary>蹴上げ。20 cm で、体の当たりの stepOffset（0.3）の内に収まる</summary>
        const float InnerRise = Floor / InnerRisers;                                        // 0.2
        /// <summary>最後の蹴上げ。ここから南が四階の踊り場</summary>
        const float InnerHead = InnerFoot + InnerGo * (InnerRisers - 1);                    // 4.42
        /// <summary>台所の南の仕切りの真ん中。仕切りに配膳の小窓を抜く</summary>
        const float CookBack = 3.65f;
        /// <summary>居間の北の仕切りの真ん中。廊下はここで居間の戸口に突き当たる</summary>
        const float LoungeWall = 4.65f;
        /// <summary>台所の戸口の両端（d）。玄関の先、廊下の西の壁に開く。幅 1.0 m</summary>
        const float KitchenDoor0 = 1.40f;
        const float KitchenDoor1 = 2.40f;
        /// <summary>配膳の小窓の両端（u）</summary>
        const float HatchWest = 1.2f;
        const float HatchEast = 2.2f;

        // 四階
        /// <summary>表の寝室の北の内壁。デッキの上へ張り出した先</summary>
        const float BedFrontIn = FaceSkin - LandingDeep;                                    // -1.75
        /// <summary>表の寝室の南の仕切りの真ん中</summary>
        const float BedWall = 1.25f;
        /// <summary>浴室の南の仕切りの真ん中</summary>
        const float BathWall = 3.45f;
        /// <summary>踊り場の南の仕切りの真ん中。ここから南が奥の寝室</summary>
        const float LandWall = 5.55f;
        /// <summary>表の寝室の戸口の両端（u）。四階の廊下の突き当たりで、廊下の幅そのまま</summary>
        const float BedDoor0 = LaneWest;                                                    // 3.2
        const float BedDoor1 = LaneEast;                                                    // 4.2
        /// <summary>浴室の戸口の両端（d）。四階の廊下の北の端、表の寝室の戸口のすぐ南</summary>
        const float BathDoor0 = 1.40f;
        const float BathDoor1 = 2.40f;
        /// <summary>奥の寝室の戸口の両端（u）。階段を上がり切った正面。東の脇に戸の板を寄せる隙を残す</summary>
        const float RearDoor0 = 4.25f;
        const float RearDoor1 = 5.25f;

        // 窓（u と、床からの高さ）
        const float KitchenWin0 = 0.8f;
        const float KitchenWin1 = 2.6f;
        const float KitchenSill = 1.0f;
        const float KitchenHead = 2.1f;
        const float LoungeWin0 = 0.6f;
        const float LoungeWin1 = 4.8f;
        const float LoungeSill = 0.8f;
        const float LoungeHead = 2.2f;
        const float FrontWin0 = 0.9f;
        const float FrontWin1 = 3.5f;
        const float RearWin0 = 0.8f;
        const float RearWin1 = 2.8f;
        const float RearWin2 = 3.4f;
        const float RearWin3 = 4.8f;
        const float BedSill = 0.9f;
        const float BedHead = 2.1f;
        /// <summary>メーターの物入れの真ん中（u）。台所の窓と戸口のあいだ</summary>
        const float MeterAt = 3.35f;

        /// <summary>住戸の奥行き d を場所の z へ</summary>
        static float FlatZ(float d)
        {
            return EstateFace - d;
        }

        /// <summary>住戸の中の灯り一つ。at は (u, 絶対の高さ, d)</summary>
        struct FlatLamp
        {
            public string name;
            public Vector3 at;
            public float power;
            public float range;

            public FlatLamp(string name, Vector3 at, float power, float range)
            {
                this.name = name;
                this.at = at;
                this.power = power;
                this.range = range;
            }
        }

        /// <summary>
        /// 住戸の中の灯り。玄関（Hall）、居間（名前なし）、四階の廊下（Up）の三つ。
        ///
        /// **天井に寄せすぎない。** 天井から 0.35 m の点では、天井に白い円が焼けて灯りの形が見える。
        /// 吊り下げの笠の高さまで下ろす。影を落とさない灯りは壁を抜けるので、
        /// 玄関の一つで台所まで、四階の廊下の一つで寝室二つと浴室まで届く。
        /// 四階の灯りは廊下に置く。浴室の中に置いた版では、廊下の側を向いた壁が全部沈んだ
        /// </summary>
        static readonly FlatLamp[] FlatLamps =
        {
            new FlatLamp("Hall", new Vector3(3.9f, WalkRoof - 0.45f, 0.9f), 2.2f, 4.8f),
            new FlatLamp("", new Vector3(2.6f, WalkRoof - 0.60f, 6.4f), 2.4f, 6.5f),
            new FlatLamp("Up", new Vector3(3.8f, UpperRoof - 0.50f, 3.0f), 2.6f, 7.5f),
        };

        /// <summary>
        /// 上のメゾネットの躯体。床・壁・天井・仕切り・住戸の中の階段・窓枠・開いた戸の板。
        ///
        /// <paramref name="wall"/> と <paramref name="carpet"/> を住戸ごとに変える。
        /// 壁は部屋の側だけに面を張り、外壁の外側は <see cref="EstateBlock"/> が張る
        /// </summary>
        static void EstateFlat(EstateBanks b, int unit, Bank wall, Bank carpet)
        {
            var ox = FlatWest + FlatWide * unit;
            const float f3 = EstateTop;
            const float c3 = WalkRoof;
            const float f4 = EstateUpper;
            const float c4 = UpperRoof;

            // ---- 三階の床 ----
            // 台所は陶板、ほかは敷き込みの絨毯。**戸口に段は付けない。** デッキと同じ高さのまま入る
            b.Tile.FaceY(f3, ox + FlatIn, ox + KitchenWall, FlatZ(CookBack), FlatZ(FrontIn), 1);
            carpet.FaceY(f3, ox + KitchenWall, ox + FlatInner, FlatZ(InnerFoot), FlatZ(FrontIn), 1);
            carpet.FaceY(f3, ox + KitchenWall, ox + StairLine, FlatZ(LoungeWall), FlatZ(InnerFoot), 1);
            carpet.FaceY(f3, ox + FlatIn, ox + KitchenWall, FlatZ(LoungeWall), FlatZ(CookBack), 1);
            carpet.FaceY(f3, ox + FlatIn, ox + FlatInner, FlatZ(FlatBackIn), FlatZ(LoungeWall), 1);
            b.Cast.FaceY(f3, ox + DoorAt - DoorHalf, ox + DoorAt + DoorHalf, FlatZ(FrontIn), EstateFace, 1);

            // ---- 三階の外周の壁 ----
            wall.FaceX(ox + FlatIn, FlatZ(FlatBackIn), FlatZ(FrontIn), f3, c3, 1);
            wall.FaceX(ox + FlatInner, FlatZ(FlatBackIn), FlatZ(FrontIn), f3, c3, -1);
            var door = new Vector4(ox + DoorAt - DoorHalf, ox + DoorAt + DoorHalf, f3, f3 + DoorHigh);
            var kitchen = new Vector4(ox + KitchenWin0, ox + KitchenWin1, f3 + KitchenSill, f3 + KitchenHead);
            wall.FaceZHoles(FlatZ(FrontIn), ox + FlatIn, ox + FlatInner, f3, c3, -1, new List<Vector4> { door, kitchen });
            EstateReveal(wall, door, FlatZ(FrontIn), EstateFace, false);
            EstateReveal(wall, kitchen, FlatZ(FrontIn), EstateFace, true);
            var lounge = new Vector4(ox + LoungeWin0, ox + LoungeWin1, f3 + LoungeSill, f3 + LoungeHead);
            wall.FaceZHoles(FlatZ(FlatBackIn), ox + FlatIn, ox + FlatInner, f3, c3, 1, new List<Vector4> { lounge });
            EstateReveal(wall, lounge, BlockBack, FlatZ(FlatBackIn), true);

            // ---- 三階の仕切り ----
            // 台所と廊下。台所の戸口は玄関の先、廊下の西の壁に開ける
            FlatPart(wall, unit, KitchenWall - HallSkin, KitchenWall + HallSkin, FrontIn, KitchenDoor0, f3, c3);
            FlatPart(wall, unit, KitchenWall - HallSkin, KitchenWall + HallSkin, KitchenDoor1, LoungeWall + HallSkin, f3, c3);
            FlatPart(wall, unit, KitchenWall - HallSkin, KitchenWall + HallSkin, KitchenDoor0, KitchenDoor1, f3 + HallHead, c3);
            // 台所の南の仕切りと配膳の小窓。食卓は小窓の向こう
            FlatPart(wall, unit, FlatIn, HatchWest, CookBack - HallSkin, CookBack + HallSkin, f3, c3);
            FlatPart(wall, unit, HatchEast, KitchenWall - HallSkin, CookBack - HallSkin, CookBack + HallSkin, f3, c3);
            FlatPart(wall, unit, HatchWest, HatchEast, CookBack - HallSkin, CookBack + HallSkin, f3, f3 + 0.95f);
            FlatPart(wall, unit, HatchWest, HatchEast, CookBack - HallSkin, CookBack + HallSkin, f3 + 1.55f, c3);
            b.Frame.Box(new Vector3(ox + (HatchWest + HatchEast) * 0.5f, f3 + 0.965f, FlatZ(CookBack)),
                new Vector3(HatchEast - HatchWest + 0.06f, 0.03f, HallSkin * 2f + 0.10f));
            // 居間の北の壁。階段の下を閉じ、廊下の突き当たりに戸口の垂れ壁を渡す
            FlatPart(wall, unit, LaneEast, FlatInner, LoungeWall - HallSkin, LoungeWall + HallSkin, f3, c3);
            FlatPart(wall, unit, LaneWest, LaneEast, LoungeWall - HallSkin, LoungeWall + HallSkin, f3 + HallHead, c3);
            EstateStairWall(b, unit, wall);

            // ---- 住戸の中の階段 ----
            EstateInnerStair(b, unit, carpet);

            // ---- 三階の天井。階段の上だけ抜いて、四階まで吹き抜ける ----
            b.Ceil.FaceY(c3, ox + FlatIn, ox + TreadWest, FlatZ(FlatBackIn), FlatZ(FrontIn), -1);
            b.Ceil.FaceY(c3, ox + TreadWest, ox + FlatInner, FlatZ(InnerFoot), FlatZ(FrontIn), -1);
            b.Ceil.FaceY(c3, ox + TreadWest, ox + FlatInner, FlatZ(FlatBackIn), FlatZ(InnerHead), -1);
            wall.FaceX(ox + FlatInner, FlatZ(InnerHead), FlatZ(InnerFoot), c3, f4, -1);

            // ---- 四階の床 ----
            carpet.FaceY(f4, ox + FlatIn, ox + FlatInner, FlatZ(BedWall), FlatZ(BedFrontIn), 1);
            b.Tile.FaceY(f4, ox + FlatIn, ox + KitchenWall, FlatZ(BathWall), FlatZ(BedWall), 1);
            carpet.FaceY(f4, ox + KitchenWall, ox + StairLine, FlatZ(InnerHead), FlatZ(BedWall), 1);
            carpet.FaceY(f4, ox + KitchenWall, ox + FlatInner, FlatZ(LandWall), FlatZ(InnerHead), 1);
            carpet.FaceY(f4, ox + FlatIn, ox + KitchenWall, FlatZ(LandWall), FlatZ(BathWall), 1);
            carpet.FaceY(f4, ox + FlatIn, ox + FlatInner, FlatZ(FlatBackIn), FlatZ(LandWall), 1);

            // ---- 四階の外周の壁 ----
            wall.FaceX(ox + FlatIn, FlatZ(FlatBackIn), FlatZ(BedFrontIn), f4, c4, 1);
            wall.FaceX(ox + FlatInner, FlatZ(FlatBackIn), FlatZ(BedFrontIn), f4, c4, -1);
            var front = new Vector4(ox + FrontWin0, ox + FrontWin1, f4 + BedSill, f4 + BedHead);
            wall.FaceZHoles(FlatZ(BedFrontIn), ox + FlatIn, ox + FlatInner, f4, c4, -1, new List<Vector4> { front });
            EstateReveal(wall, front, FlatZ(BedFrontIn), WalkFront, true);
            var rear0 = new Vector4(ox + RearWin0, ox + RearWin1, f4 + BedSill, f4 + BedHead);
            var rear1 = new Vector4(ox + RearWin2, ox + RearWin3, f4 + BedSill, f4 + BedHead);
            wall.FaceZHoles(FlatZ(FlatBackIn), ox + FlatIn, ox + FlatInner, f4, c4, 1, new List<Vector4> { rear0, rear1 });
            EstateReveal(wall, rear0, BlockBack, FlatZ(FlatBackIn), true);
            EstateReveal(wall, rear1, BlockBack, FlatZ(FlatBackIn), true);

            // ---- 四階の仕切り ----
            // 表の寝室の南。吹き抜けの上の分は三階の天井まで下ろして、床板の縁を隠す
            FlatPart(wall, unit, FlatIn, BedDoor0, BedWall - HallSkin, BedWall + HallSkin, f4, c4);
            FlatPart(wall, unit, BedDoor1, TreadWest, BedWall - HallSkin, BedWall + HallSkin, f4, c4);
            FlatPart(wall, unit, TreadWest, FlatInner, BedWall - HallSkin, BedWall + HallSkin, c3, c4);
            FlatPart(wall, unit, BedDoor0, BedDoor1, BedWall - HallSkin, BedWall + HallSkin, f4 + HallHead, c4);
            // 廊下の西。浴室の戸口を抜く。南の半分は奥の寝室の張り出しとの境
            FlatPart(wall, unit, KitchenWall - HallSkin, KitchenWall + HallSkin, BedWall + HallSkin, BathDoor0, f4, c4);
            FlatPart(wall, unit, KitchenWall - HallSkin, KitchenWall + HallSkin, BathDoor1, LandWall + HallSkin, f4, c4);
            FlatPart(wall, unit, KitchenWall - HallSkin, KitchenWall + HallSkin, BathDoor0, BathDoor1, f4 + HallHead, c4);
            // 浴室の南
            FlatPart(wall, unit, FlatIn, KitchenWall - HallSkin, BathWall - HallSkin, BathWall + HallSkin, f4, c4);
            // 踊り場の南。奥の寝室の戸口は、階段を上がり切った正面
            FlatPart(wall, unit, KitchenWall + HallSkin, RearDoor0, LandWall - HallSkin, LandWall + HallSkin, f4, c4);
            FlatPart(wall, unit, RearDoor1, FlatInner, LandWall - HallSkin, LandWall + HallSkin, f4, c4);
            FlatPart(wall, unit, RearDoor0, RearDoor1, LandWall - HallSkin, LandWall + HallSkin, f4 + HallHead, c4);
            // 吹き抜けの手すり壁。四階の廊下から階段へ落ちないように、腰の高さまで
            FlatPart(wall, unit, StairLine - HallSkin, StairLine + HallSkin, InnerFoot, InnerHead, c3, f4 + 0.95f);
            b.Set.Box(new Vector3(ox + StairLine, f4 + 0.97f, FlatZ((InnerFoot + InnerHead) * 0.5f)),
                new Vector3(0.14f, 0.05f, InnerHead - InnerFoot + 0.04f));

            b.Ceil.FaceY(c4, ox + FlatIn, ox + FlatInner, FlatZ(FlatBackIn), FlatZ(BedFrontIn), -1);

            // ---- 窓枠 ----
            EstateSash(b, kitchen, EstateFace - 0.10f);
            EstateSash(b, lounge, BlockBack + 0.10f);
            EstateSash(b, front, WalkFront - 0.10f);
            EstateSash(b, rear0, BlockBack + 0.10f);
            EstateSash(b, rear1, BlockBack + 0.10f);
            // 窓台。居間と寝室は内側に白い板を出す
            EstateBoardIn(b, lounge, FlatZ(FlatBackIn), 1);
            EstateBoardIn(b, front, FlatZ(BedFrontIn), -1);
            EstateBoardIn(b, rear0, FlatZ(FlatBackIn), 1);
            EstateBoardIn(b, rear1, FlatZ(FlatBackIn), 1);
            // 外の窓台
            foreach (var hole in new[] { kitchen, lounge, front, rear0, rear1 })
            {
                var outside = hole.Equals(kitchen) ? EstateFace + 0.05f : hole.Equals(front) ? WalkFront + 0.05f : BlockBack - 0.05f;
                b.Cast.Box(new Vector3((hole.x + hole.y) * 0.5f, hole.z - 0.03f, outside),
                    new Vector3(hole.y - hole.x + 0.2f, 0.06f, 0.10f));
            }

            // ---- 開いた戸の板。どれも部屋の側へ 90 度開けて、壁に寄せてある ----
            // **板は戸口の幅の外に置く。** 戸口の縁の線より内へ出すと、板の厚みの分だけ戸口が狭まる
            // 台所。蝶番は南の縁で、板は台所の中へ
            FlatThing(b.Frame, unit, KitchenWall - HallSkin - 0.49f, KitchenDoor1 + 0.02f, f3,
                new Vector3(0.98f, HallHead - 0.02f, 0.04f));
            // 居間。蝶番は東の縁で、板は居間の中へ
            FlatThing(b.Frame, unit, LaneEast + 0.02f, LoungeWall + HallSkin + 0.49f, f3,
                new Vector3(0.04f, HallHead - 0.02f, 0.98f));
            // 表の寝室。蝶番は東の縁。部屋の広い西の側を塞がない
            FlatThing(b.Frame, unit, BedDoor1 + 0.02f, BedWall - HallSkin - 0.49f, f4,
                new Vector3(0.04f, HallHead - 0.02f, 0.98f));

            // 吊り下げの灯り。点の灯りの位置に笠と電球を下げて、明るさの出どころを見せる
            foreach (var lamp in FlatLamps)
            {
                var roof = lamp.at.y > EstateUpper ? c4 : c3;
                var at = new Vector3(ox + lamp.at.x, lamp.at.y, FlatZ(lamp.at.z));
                b.Gear.Box(new Vector3(at.x, (roof + at.y) * 0.5f + 0.08f, at.z), new Vector3(0.015f, roof - at.y - 0.16f, 0.015f));
                b.Frame.Box(at + new Vector3(0f, 0.08f, 0f), new Vector3(0.34f, 0.16f, 0.34f));
                b.Lit.Box(at + new Vector3(0f, -0.02f, 0f), new Vector3(0.12f, 0.06f, 0.12f));
            }
            // 浴室。蝶番は北の縁で、板は表の寝室との仕切りに寄る
            FlatThing(b.Frame, unit, KitchenWall - HallSkin - 0.49f, BathDoor0 - 0.02f, f4,
                new Vector3(0.98f, HallHead - 0.02f, 0.04f));
            // 奥の寝室。蝶番は東の縁で、板は戸境の壁に寄る
            FlatThing(b.Frame, unit, RearDoor1 + 0.02f, LandWall + HallSkin + 0.49f, f4,
                new Vector3(0.04f, HallHead - 0.02f, 0.98f));

            // ---- 幅木 ----
            // 壁の裾に白い線が一本通らないと、壁が塗っただけの面に見える
            EstateSkirt(b, ox + FlatIn + 0.01f, FlatZ(FlatBackIn), FlatZ(LoungeWall + HallSkin), f3, true, 1);
            EstateSkirt(b, ox + FlatInner - 0.01f, FlatZ(FlatBackIn), FlatZ(LoungeWall + HallSkin), f3, true, -1);
            EstateSkirt(b, FlatZ(FlatBackIn) + 0.01f, ox + FlatIn, ox + FlatInner, f3, false, 1);
            EstateSkirt(b, ox + LaneWest + 0.01f, FlatZ(LoungeWall - HallSkin), FlatZ(KitchenDoor1), f3, true, 1);
            EstateSkirt(b, ox + FlatIn + 0.01f, FlatZ(FlatBackIn), FlatZ(BedFrontIn), f4, true, 1);
            EstateSkirt(b, ox + FlatInner - 0.01f, FlatZ(FlatBackIn), FlatZ(LandWall + HallSkin), f4, true, -1);
            EstateSkirt(b, ox + FlatInner - 0.01f, FlatZ(BedWall - HallSkin), FlatZ(BedFrontIn), f4, true, -1);
            EstateSkirt(b, FlatZ(FlatBackIn) + 0.01f, ox + FlatIn, ox + FlatInner, f4, false, 1);
            EstateSkirt(b, FlatZ(BedFrontIn) - 0.01f, ox + FlatIn, ox + FlatInner, f4, false, -1);
        }

        /// <summary>
        /// 住戸の中の階段と廊下の仕切り。
        ///
        /// **下の半分は段と一緒に上がる手すり壁、上の半分は天井まで。** 階段の西は廊下で、
        /// 段を上がるにつれて廊下の床から離れていく。手すり壁が無いと 2 m を超えて落ちる。
        /// 段ごとに段の頭へ揃えた箱を立て、その上に勾配なりの板と笠木を渡す。
        /// 階段の上り口の低い所では、玄関から段と廊下の両方が見える
        /// </summary>
        static void EstateStairWall(EstateBanks b, int unit, Bank wall)
        {
            var ox = FlatWest + FlatWide * unit;
            const float f3 = EstateTop;
            const int low = 7;
            for (var i = 0; i < low; i++)
                FlatPart(wall, unit, StairLine - HallSkin, StairLine + HallSkin,
                    InnerFoot + InnerGo * i, InnerFoot + InnerGo * (i + 1), f3, f3 + InnerRise * (i + 1) + 0.75f);
            FlatPart(wall, unit, StairLine - HallSkin, StairLine + HallSkin,
                InnerFoot + InnerGo * low, LoungeWall - HallSkin, f3, WalkRoof);

            // 勾配なりの板。段鼻を結んだ線から 0.5〜1.0 m 上。箱の頭の段々はこの中に隠れる
            var pitch = InnerRise / InnerGo;
            var d0 = InnerFoot;
            var d1 = InnerFoot + InnerGo * low;
            var y0 = f3 + InnerRise + 0.75f;
            var y1 = y0 + (d1 - d0) * pitch;
            var mid = new Vector3(ox + StairLine, (y0 + y1) * 0.5f, FlatZ((d0 + d1) * 0.5f));
            var dir = new Vector3(0f, y1 - y0, -(d1 - d0));
            var len = dir.magnitude;
            var rot = Quaternion.LookRotation(dir.normalized, Vector3.up);
            var cos = (d1 - d0) / len;
            wall.Box(mid, new Vector3(HallSkin * 2f + 0.02f, 0.5f * cos, len), rot);
            // 笠木。木の手すり
            b.Set.Box(mid + rot * new Vector3(0f, 0.25f * cos + 0.025f, 0f), new Vector3(0.14f, 0.05f, len + 0.04f), rot);
            // 上り口の親柱
            FlatThing(b.Set, unit, StairLine, InnerFoot + 0.05f, f3, new Vector3(0.12f, 1.12f, 0.12f));
        }

        /// <summary>
        /// 住戸の中の階段。玄関のすぐ先から南へ、14 段で四階の踊り場まで上がる。
        /// 絨毯を段ごとに蹴込みと踏み面で張り、段鼻に金物を一本。戸境の壁に木の手すり
        /// </summary>
        static void EstateInnerStair(EstateBanks b, int unit, Bank carpet)
        {
            var ox = FlatWest + FlatWide * unit;
            const float f3 = EstateTop;
            for (var i = 0; i < InnerRisers - 1; i++)
            {
                var d0 = InnerFoot + InnerGo * i;
                var y0 = f3 + InnerRise * i;
                carpet.FaceZ(FlatZ(d0), ox + TreadWest, ox + FlatInner, y0, y0 + InnerRise, 1);
                carpet.FaceY(y0 + InnerRise, ox + TreadWest, ox + FlatInner, FlatZ(d0 + InnerGo), FlatZ(d0), 1);
                b.Gear.FaceZ(FlatZ(d0) + 0.004f, ox + TreadWest, ox + FlatInner, y0 + InnerRise - 0.025f, y0 + InnerRise, 1);
            }
            carpet.FaceZ(FlatZ(InnerHead), ox + TreadWest, ox + FlatInner, f3 + InnerRise * (InnerRisers - 1), EstateUpper, 1);

            // 戸境の壁の手すり。段鼻の線から 0.9 m 上を通す
            var pitch = InnerRise / InnerGo;
            var a = new Vector3(ox + FlatInner - 0.06f, f3 + InnerRise + 0.9f, FlatZ(InnerFoot));
            var z = new Vector3(ox + FlatInner - 0.06f, f3 + InnerRise + 0.9f + (InnerHead - InnerFoot) * pitch, FlatZ(InnerHead));
            var dir = z - a;
            b.Set.Box((a + z) * 0.5f, new Vector3(0.05f, 0.05f, dir.magnitude), Quaternion.LookRotation(dir.normalized, Vector3.up));
        }

        /// <summary>住戸のローカルで箱一つ。仕切りや家具の塊に使う。y は絶対の高さ</summary>
        static void FlatPart(Bank b, int unit, float u0, float u1, float d0, float d1, float y0, float y1)
        {
            if (u1 - u0 < 0.001f || d1 - d0 < 0.001f || y1 - y0 < 0.001f) return;
            var ox = FlatWest + FlatWide * unit;
            b.Box(new Vector3(ox + (u0 + u1) * 0.5f, (y0 + y1) * 0.5f, FlatZ((d0 + d1) * 0.5f)),
                new Vector3(u1 - u0, y1 - y0, d1 - d0));
        }

        /// <summary>
        /// 住戸のローカルで物を一つ。(u, d) が物の真ん中、<paramref name="floor"/> が底の高さ。
        /// size は (u の幅, 高さ, d の奥行き)
        /// </summary>
        static void FlatThing(Bank b, int unit, float u, float d, float floor, Vector3 size)
        {
            var ox = FlatWest + FlatWide * unit;
            b.Box(new Vector3(ox + u, floor + size.y * 0.5f, FlatZ(d)), size);
        }

        /// <summary>向きを持つ物。<paramref name="yaw"/> は上から見て時計回り</summary>
        static void FlatThing(Bank b, int unit, float u, float d, float floor, Vector3 size, float yaw)
        {
            var ox = FlatWest + FlatWide * unit;
            b.Box(new Vector3(ox + u, floor + size.y * 0.5f, FlatZ(d)), size, Quaternion.Euler(0f, yaw, 0f));
        }

        /// <summary>
        /// 壁の穴の内側の四面。厚みのある壁は外と内に面を張るだけでは穴の縁が抜けて、
        /// 覗くと壁の中の虚空が見える。当たりも入れて、戸口を通るときに壁の中へ入らないようにする
        /// </summary>
        static void EstateReveal(Bank b, Vector4 hole, float zA, float zB, bool sill)
        {
            var z0 = Mathf.Min(zA, zB);
            var z1 = Mathf.Max(zA, zB);
            b.FaceX(hole.x, z0, z1, hole.z, hole.w, 1);
            b.FaceX(hole.y, z0, z1, hole.z, hole.w, -1);
            b.FaceY(hole.w, hole.x, hole.y, z0, z1, -1);
            if (sill) b.FaceY(hole.z, hole.x, hole.y, z0, z1, 1);
        }

        /// <summary>
        /// 窓の穴の中の白い枠。四辺と縦の桟、上に倒して開ける小窓の横の桟。
        /// 硝子は入れない。朝の光と外の景色をそのまま通す
        /// </summary>
        static void EstateSash(EstateBanks b, Vector4 hole, float z)
        {
            const float w = 0.06f;
            var x0 = hole.x;
            var x1 = hole.y;
            var y0 = hole.z;
            var y1 = hole.w;
            b.Frame.Box(new Vector3((x0 + x1) * 0.5f, y1 - w * 0.5f, z), new Vector3(x1 - x0, w, w));
            b.Frame.Box(new Vector3((x0 + x1) * 0.5f, y0 + w * 0.5f, z), new Vector3(x1 - x0, w, w));
            b.Frame.Box(new Vector3(x0 + w * 0.5f, (y0 + y1) * 0.5f, z), new Vector3(w, y1 - y0, w));
            b.Frame.Box(new Vector3(x1 - w * 0.5f, (y0 + y1) * 0.5f, z), new Vector3(w, y1 - y0, w));
            var count = x1 - x0 > 2.2f ? 2 : 1;
            for (var i = 1; i <= count; i++)
                b.Frame.Box(new Vector3(Mathf.Lerp(x0, x1, i / (float)(count + 1)), (y0 + y1) * 0.5f, z),
                    new Vector3(w * 0.8f, y1 - y0, w * 0.8f));
            b.Frame.Box(new Vector3((x0 + x1) * 0.5f, y1 - 0.34f, z), new Vector3(x1 - x0, w * 0.8f, w * 0.8f));
        }

        /// <summary>
        /// 窓の内側の白い窓台。<paramref name="inward"/> は部屋の側へ出る向き（z の符号）
        /// </summary>
        static void EstateBoardIn(EstateBanks b, Vector4 hole, float zWall, int inward)
        {
            b.Frame.Box(new Vector3((hole.x + hole.y) * 0.5f, hole.z - 0.015f, zWall + inward * 0.05f),
                new Vector3(hole.y - hole.x + 0.16f, 0.03f, 0.14f));
        }

        /// <summary>
        /// 幅木を一本。面を一枚だけ部屋の側へ向けて張る。
        /// <paramref name="alongZ"/> が真なら x が一定の壁（<paramref name="at"/> は x、from〜to は z）
        /// </summary>
        static void EstateSkirt(EstateBanks b, float at, float from, float to, float floor, bool alongZ, int sign)
        {
            var lo = Mathf.Min(from, to);
            var hi = Mathf.Max(from, to);
            if (alongZ) b.Frame.FaceX(at, lo, hi, floor, floor + 0.10f, sign);
            else b.Frame.FaceZ(at, lo, hi, floor, floor + 0.10f, sign);
        }
    }
}
