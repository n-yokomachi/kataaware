using UnityEngine;

namespace HalfAware
{
    /// <summary>
    /// 自室の窓の外の景色の寸法と時刻（シナリオ設計 5 節「窓の外」）。組むのは <c>HalfAware/Build the room view</c>（BuildRoomView）。
    ///
    /// 部屋は日本の数え方で 5 階。床（y 0）から地面まで 12 m 下がる。窓は東の壁（x 3）と北の壁（z 3）に一つずつ。
    /// 角度は +z（北）を 0 として東（+x）回りの度（<see cref="BackdropRing"/> と同じ決まり）。
    ///
    /// **見えない側は作らない。** 窓から見える向きは、窓の抜けの幅と壁の厚みで決まる（<see cref="Reach"/>）。
    /// 二つの窓を合わせた向きの幅（<see cref="ArcFrom"/>〜<see cref="ArcTo"/>）だけに空と街並みを張る。
    ///
    /// 実行時には呼ばない。純粋な計算なので、ここに置いて試験から見る
    /// </summary>
    public static class RoomView
    {
        /// <summary>時刻。場面 1 は夕暮れ（19 時台）、場面 3・5・7 は夜（22 時台）</summary>
        public enum Hour { Dusk, Night }

        /// <summary>地面の高さ。床から 12 m 下（5 階の床）</summary>
        public const float Ground = -12f;

        /// <summary>空の球の半径。目のカメラの far（1000 m）の内に収める</summary>
        public const float SkyRadius = 450f;
        /// <summary>空の絵の下の縁の仰角。遠い街の足元まで塗る</summary>
        public const float SkyBottom = -14f;

        /// <summary>街並みを組む遠さ（原点から）。この先は空の絵の遠い屋根と街の灯り</summary>
        public const float TownReach = 230f;

        /// <summary>空と街並みを張る向きの幅。窓から見える向き（<see cref="Reach"/>。北の窓 ±77.5 度、東の窓 12.5〜167.5 度）に余裕を足した</summary>
        public const float ArcFrom = -95f;
        public const float ArcTo = 185f;

        // ---- 部屋の場所と名所 ------------------------------------------------------

        /// <summary>部屋の場所。テムズ川の南岸のバーモンジー（タワーブリッジの南南東 900 m ほど）</summary>
        public const double Latitude = 51.4985;
        public const double Longitude = -0.0700;

        /// <summary>
        /// 部屋の向き。部屋の北の窓（+z）は、本物では北北西（北から西へ 30 度）を向いている。
        /// 本物の向きに <see cref="Turn"/> を足すと、景色の中の向きになる（名所の群れが北の窓の正面に来る）
        /// </summary>
        public const float Turn = 30f;

        /// <summary>
        /// テムズ川。本物のように東西に流れる川を、部屋の向きに合わせて <see cref="Turn"/> だけ回した「川の座標」で決める
        /// （<see cref="RiverLocal"/>。z が川を渡る向き）。南岸と北岸の護岸の縁。作り話の倫敦なので、本物（北へ 600 m ほど）より近い
        /// </summary>
        public const float RiverSouth = 96f;
        public const float RiverNorth = 197f;
        /// <summary>
        /// タワーブリッジ（川の座標）。軸の x と、川の中の二本の塔の z。窓から南の塔が左 11.5 度・145 m、北の塔が右 2.5 度・208 m。
        /// 塔の間（歩道橋の渡る所）を北の窓の真ん中の桟より左に空け、窓際の正面から歩道橋が桟に隠れきらない
        /// </summary>
        public const float BridgeX = -96.1f;
        public const float BridgeSouthTower = 108.6f;
        public const float BridgeNorthTower = 184.6f;

        /// <summary>景色の中の位置を川の座標へ（x が川に沿う向き、z が川を渡る向き）</summary>
        public static Vector2 RiverLocal(Vector3 world)
        {
            var a = Turn * Mathf.Deg2Rad;
            return new Vector2(world.x * Mathf.Cos(a) - world.z * Mathf.Sin(a), world.x * Mathf.Sin(a) + world.z * Mathf.Cos(a));
        }

        /// <summary>川の座標の点を景色の中へ。y はそのまま</summary>
        public static Vector3 RiverWorld(Vector3 local)
        {
            var a = Turn * Mathf.Deg2Rad;
            return new Vector3(local.x * Mathf.Cos(a) + local.z * Mathf.Sin(a), local.y, -local.x * Mathf.Sin(a) + local.z * Mathf.Cos(a));
        }

        static Vector3 BridgeCentre()
        {
            return RiverWorld(new Vector3(BridgeX, 0f, (BridgeSouthTower + BridgeNorthTower) * 0.5f));
        }

        public const string Shard = "Shard";
        public const string StPauls = "StPauls";
        public const string WalkieTalkie = "WalkieTalkie";
        public const string TowerBridge = "TowerBridge";
        public const string Gherkin = "Gherkin";
        public const string TowerOfLondon = "TowerOfLondon";
        public const string CanaryWharf = "CanaryWharf";
        public const string O2 = "O2";

        /// <summary>
        /// 窓から見える名所。Azimuth と Distance は景色に置く向きと遠さ（遠い物はここから縮めて置く）、
        /// Lat・Lon は本物の場所。向きは本物の並びのまま、重なりを避けて少しずつずらし、遠さは形が読める所まで詰めてある
        /// </summary>
        public struct Landmark
        {
            public string Name;
            public float Azimuth;
            public float Distance;
            public double Lat;
            public double Lon;

            public Landmark(string name, float azimuth, float distance, double lat, double lon)
            {
                Name = name;
                Azimuth = azimuth;
                Distance = distance;
                Lat = lat;
                Lon = lon;
            }
        }

        /// <summary>
        /// 北の窓の名所は左（西）から順に、東の窓の名所はその後に。本物の向き + <see cref="Turn"/> の並びのまま、重ならないよう少しずつずらす（15 度まで。一番ずらしたのはタワーブリッジの南の塔と重なるウォーキートーキー）。
        /// タワーブリッジは川の座標の塔から出す。ロンドン塔はガーキンの右、窓から B の通りの抜けに見える向き
        /// </summary>
        public static readonly Landmark[] Landmarks =
        {
            new Landmark(Shard, -27f, 1000f, 51.5045, -0.0865),
            new Landmark(StPauls, -21.5f, 800f, 51.5138, -0.0984),
            new Landmark(WalkieTalkie, -17.5f, 800f, 51.5113, -0.0836),
            new Landmark(TowerBridge, Mathf.Atan2(BridgeCentre().x, BridgeCentre().z) * Mathf.Rad2Deg,
                new Vector2(BridgeCentre().x, BridgeCentre().z).magnitude, 51.5055, -0.0754),
            new Landmark(Gherkin, 9f, 900f, 51.5145, -0.0803),
            new Landmark(TowerOfLondon, 18.5f, 260f, 51.5081, -0.0760),
            new Landmark(CanaryWharf, 109f, 1400f, 51.5049, -0.0195),
            new Landmark(O2, 115.5f, 1700f, 51.5030, 0.0030),
        };

        /// <summary>部屋から本物の場所への向き（度。北が 0、東が正）。大圏の出だしの向き</summary>
        public static float Bearing(double lat, double lon)
        {
            var p1 = Latitude * Mathf.Deg2Rad;
            var p2 = lat * Mathf.Deg2Rad;
            var dl = (lon - Longitude) * Mathf.Deg2Rad;
            var x = System.Math.Sin(dl) * System.Math.Cos(p2);
            var y = System.Math.Cos(p1) * System.Math.Sin(p2) - System.Math.Sin(p1) * System.Math.Cos(p2) * System.Math.Cos(dl);
            return (float)(System.Math.Atan2(x, y) * Mathf.Rad2Deg);
        }

        /// <summary>窓のある壁の内側と外側の面（東の壁は x、北の壁は z）。部屋の壁は 0.2 m 厚</summary>
        public const float WallIn = 2.9f;
        public const float WallOut = 3.1f;

        /// <summary>
        /// 窓の抜け（東の窓は z の幅、北の窓は x の幅）。壁の内側の面では窓枠（部屋の Window・WindowFront の Frame*）が、
        /// 外側の面では壁の抜けが縁になる
        /// </summary>
        public static readonly Vector2 EastInner = new Vector2(-0.92f, -0.08f);
        public static readonly Vector2 EastOuter = new Vector2(-0.98f, -0.02f);
        public static readonly Vector2 NorthInner = new Vector2(-1.72f, -0.88f);
        public static readonly Vector2 NorthOuter = new Vector2(-1.78f, -0.82f);

        /// <summary>抜けの上下（床から）。内側は窓枠、外側は壁</summary>
        public static readonly Vector2 InnerRise = new Vector2(0.74f, 2.16f);
        public static readonly Vector2 OuterRise = new Vector2(0.68f, 2.22f);

        /// <summary>
        /// 窓一つから見える向きの幅（度）。目は内側の面の枠の抜けと外側の面の壁の抜けの両方を通して見るので、
        /// 法線から振れる角は、枠の一方の縁と壁の向こうの縁を結ぶ向きまでになる
        /// </summary>
        public static Vector2 Reach(bool east)
        {
            var inner = east ? EastInner : NorthInner;
            var outer = east ? EastOuter : NorthOuter;
            var swing = Mathf.Atan2(Mathf.Max(outer.y - inner.x, inner.y - outer.x), WallOut - WallIn) * Mathf.Rad2Deg;
            var normal = east ? 90f : 0f;
            return new Vector2(normal - swing, normal + swing);
        }

        /// <summary>
        /// 目 eye から向き dir へ、窓の抜けを通って外が見えるか。壁の内と外の面で抜けの中を通るかで見る。
        /// azimuth に向き（度）、elevation に仰角（度）を入れる
        /// </summary>
        public static bool Through(bool east, Vector3 eye, Vector3 dir, out float azimuth, out float elevation)
        {
            azimuth = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
            elevation = Mathf.Atan2(dir.y, new Vector2(dir.x, dir.z).magnitude) * Mathf.Rad2Deg;
            var along = east ? dir.x : dir.z;
            if (along <= 1e-5f) return false;
            return Pass(east, eye, dir, along, WallIn, east ? EastInner : NorthInner, InnerRise)
                && Pass(east, eye, dir, along, WallOut, east ? EastOuter : NorthOuter, OuterRise);
        }

        static bool Pass(bool east, Vector3 eye, Vector3 dir, float along, float plane, Vector2 open, Vector2 rise)
        {
            var from = east ? eye.x : eye.z;
            var t = (plane - from) / along;
            if (t < 0f) return false;
            var at = eye + dir * t;
            var side = east ? at.z : at.x;
            return side >= open.x && side <= open.y && at.y >= rise.x && at.y <= rise.y;
        }

        /// <summary>空の球の上の点。azimuth は向き（度）、elevation は仰角（度）</summary>
        public static Vector3 OnSky(float azimuth, float elevation)
        {
            var a = azimuth * Mathf.Deg2Rad;
            var e = elevation * Mathf.Deg2Rad;
            return new Vector3(Mathf.Sin(a) * Mathf.Cos(e), Mathf.Sin(e), Mathf.Cos(a) * Mathf.Cos(e)) * SkyRadius;
        }

        /// <summary>
        /// 空の絵の縦の位置（0〜1）。地平の近くに絵の画素を寄せる。仰角 20 度までに縦の 7 割を充て（1 度が 10 画素ほど）、
        /// そこから天頂までの残りを 3 割で塗る（色の移りしか無い）
        /// </summary>
        public static float SkyV(float elevation)
        {
            const float knee = 20f;
            const float share = 0.7f;
            if (elevation <= knee) return Mathf.Clamp01((elevation - SkyBottom) / (knee - SkyBottom) * share);
            return Mathf.Clamp01(share + (elevation - knee) / (90f - knee) * (1f - share));
        }

        /// <summary><see cref="SkyV"/> の逆。空の絵を塗るときに、画素の行から仰角を出す</summary>
        public static float SkyElevation(float v)
        {
            const float knee = 20f;
            const float share = 0.7f;
            if (v <= share) return SkyBottom + v / share * (knee - SkyBottom);
            return knee + (v - share) / (1f - share) * (90f - knee);
        }

        /// <summary>空の絵の横の位置（0〜1）。<see cref="ArcFrom"/> が 0、<see cref="ArcTo"/> が 1</summary>
        public static float SkyU(float azimuth)
        {
            return (azimuth - ArcFrom) / (ArcTo - ArcFrom);
        }

        /// <summary>
        /// その場面の時刻。場面 1（Room）は夕暮れ、同じ部屋を写して組む場面 3・5・7（Connect・Rest・Notice）は夜
        /// </summary>
        public static Hour HourOf(string scene)
        {
            switch (scene)
            {
                case "Connect":
                case "Rest":
                case "Notice":
                    return Hour.Night;
                default:
                    return Hour.Dusk;
            }
        }
    }
}
