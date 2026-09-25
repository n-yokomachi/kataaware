using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace HalfAware.EditorTools
{
    /// <summary>
    /// 場面 4 の公園の空・日・霞・環境光と、遠景の書き割り（設計書 9.1 節「公園の作り込み」の「時刻と光」「遠く」）。
    /// 仕組みは公営住宅と同じで、空は <c>BuildDiveSky.cs</c>、書き割りは <c>BuildDiveFar.cs</c>、
    /// 撮る街並みの部品は <c>BuildDiveTown.cs</c> を使う。ここに持つのは公園の値だけ。
    ///
    /// **3 月の 15 時 47 分〜50 分。** 日は門の側（南西）の仰角 20 度にあり、影が長い。
    /// 空は日の側の地平が暖かく明るく、上へ行くほど青い。色は午後の黄ばみ。
    ///
    /// **遠くは家並みの屋根の上に頭を出す物で見せる。** 公園は二〜三階の家並みに四方を囲まれていて、
    /// 公園の中の目の高さからは、家並みが地平から 17〜28 度までを塞ぐ。屋根の海は門の正面の横丁の奥と、
    /// 四隅の通りの口からしか見えない。家並みの上には高層棟・教会の尖塔・遠くの公園の大きな木々・シティの塔が出る。
    /// そのため仰角の上限を公営住宅（12.5 度）より高い 22 度に取り、板も高くしてある
    /// </summary>
    public static partial class BuildDive
    {
        // ---- 空と光 --------------------------------------------------------------

        const string ParkSkyPath = DiveTextures + "ParkSky.png";
        const string ParkSkyMatPath = Materials + "ParkSkybox.mat";

        /// <summary>
        /// 霞の濃さ（ExponentialSquared の density）。向かいの家並み（25 m）で 0.6%、横丁の突き当たり（60 m）で 3.6%、
        /// 書き割りの始まり（80 m）で 6%、板（200 m）で 34%。3 月の午後のロンドンの薄い霞
        /// </summary>
        const float ParkHazeDensity = 0.0032f;

        // 空の色。sRGB で書き、linear へ直して SkyPaint へ渡す。公営住宅の朝より地平を黄ばませる
        static readonly Color ParkZenith = new Color(0.29f, 0.42f, 0.64f);
        static readonly Color ParkMiddle = new Color(0.50f, 0.60f, 0.75f);
        /// <summary>地平の色。霞の色もこれにする</summary>
        static readonly Color ParkHorizon = new Color(0.80f, 0.79f, 0.74f);
        static readonly Color ParkWarm = new Color(1.00f, 0.83f, 0.60f);
        static readonly Color ParkGlow = new Color(1.00f, 0.85f, 0.62f);
        static readonly Color ParkDisc = new Color(1.00f, 0.95f, 0.82f);
        static readonly Color ParkCloudLit = new Color(0.98f, 0.87f, 0.72f);
        static readonly Color ParkCloudShade = new Color(0.64f, 0.65f, 0.70f);
        /// <summary>地面の照り返しの色。芝と砂利。sRGB</summary>
        static readonly Color ParkBounce = new Color(0.42f, 0.42f, 0.28f);
        /// <summary>
        /// 環境光の倍率。公営住宅は部屋の中まで同じ環境光になるので空から取った値を控えめに寄せてあるが、
        /// 公園は屋根の無い所だけなので上げる。上げないと、日の当たらない東西の面（植え込み・門柱・家並みの妻）が
        /// 黒い塊に沈む
        /// </summary>
        const float ParkAmbientGain = 1.6f;

        /// <summary>
        /// 公園の空の見え方。<paramref name="sun"/> は日へ向かう向き（影を落とす灯りの逆）。
        /// 日の円は公営住宅と同じく実際より大きく取る（半径 1.3 度）。日が低いぶん、地平の暖かさを強く広く
        /// </summary>
        static SkyPaint.Look ParkSkyLook(Vector3 sun)
        {
            return new SkyPaint.Look
            {
                zenith = ParkZenith.linear,
                middle = ParkMiddle.linear,
                horizon = ParkHorizon.linear,
                whiteDepth = 0.10f,
                warm = ParkWarm.linear,
                warmth = 0.80f,
                warmDepth = 0.16f,
                warmFocus = 3f,
                sun = sun.normalized,
                glow = ParkGlow.linear,
                glowNear = 0.50f,
                glowNearWidth = 4f,
                glowWide = 0.26f,
                glowWideWidth = 30f,
                disc = ParkDisc.linear,
                discRadius = 1.3f,
                discSoft = 0.5f,
                cloudLit = ParkCloudLit.linear,
                cloudShade = ParkCloudShade.linear,
                cloudCover = 0.50f,
                // 仰角 2 度から 16 度。家並みの屋根の上に薄く流れる
                cloudLow = Mathf.Sin(2f * Mathf.Deg2Rad),
                cloudHigh = Mathf.Sin(16f * Mathf.Deg2Rad),
                cloudSeed = 11,
            };
        }

        /// <summary>日へ向かう向き。午後の日射し（Afternoon）の逆</summary>
        static Vector3 ParkSunward(Transform place)
        {
            return Sunward(place, "Afternoon", ParkAfternoonAim);
        }

        /// <summary>公園の空・霞・環境光・日を一揃いで。空の絵とマテリアルもここで焼き直す</summary>
        static PlaceSky ParkPlaceSky(Transform place)
        {
            return PaintedSky(place, "Afternoon", ParkSkyLook(ParkSunward(place)), ParkHorizon, ParkHazeDensity,
                ParkBounce, ParkAmbientGain, ParkSkyPath, ParkSkyMatPath);
        }

        // ---- 書き割りの輪 ----------------------------------------------------------

        /// <summary>
        /// 書き割りの輪の中心。場所のローカル。柵の内側の真ん中で、門の小径の線の上
        /// </summary>
        public static readonly Vector3 ParkFarCentre = new Vector3(0.25f, 0f, -1.4f);
        /// <summary>
        /// 撮る目の高さ。記憶 2・3・9・10 の目（1.75・1.05・1.50・1.32）の真ん中あたり。
        /// 目の高さの差が本物の地面の縁と書き割りの地面の境目のずれになるが、公園の中からは
        /// 境目の手前を家並みが塞ぐので、見えるのは横丁と四隅の通りの口の奥だけ
        /// </summary>
        public const float ParkFarEye = 1.45f;
        /// <summary>
        /// 板の上端。絵の中の一番高い物（中心から仰角 22 度まで）が収まる高さ。
        /// 板の一番遠い角まで、歩ける所のどこからでも 250 m を超えない（カメラの far は 260 m）
        /// </summary>
        public const float ParkFarTop = 84f;
        /// <summary>中心から見た仰角の上限。度</summary>
        const float ParkTownRise = 22f;

        /// <summary>公園の輪。半径・地面の縁・枚数は公営住宅と同じ</summary>
        static readonly FarRing ParkRing = new FarRing
        {
            Centre = ParkFarCentre,
            Eye = ParkFarEye,
            Top = ParkFarTop,
            Picture = DiveTextures + "ParkBackdrop.png",
            Material = Materials + "ParkBackdrop.mat",
            Name = "ParkBackdrop",
        };

        // ---- 撮る ----------------------------------------------------------------

        /// <summary>
        /// 遠景を撮る。撮るためだけの街並みを組み（<see cref="ParkTown"/>）、公園の空・霞・日・環境光のまま 16 枚を撮り、
        /// 一枚の絵に並べて書き出す。組んだ物は全部捨ててから返る。輪に貼るのは `HalfAware/Build the dive`
        /// </summary>
        [MenuItem("HalfAware/Shoot the park backdrop", false, 253)]
        public static void ShootParkBackdrop()
        {
            ShootBackdrop(DiveIds.Park, ParkRing, ParkPlaceSky, ParkTown);
        }

        /// <summary>
        /// 公園の書き割りに撮る街並み。
        /// <list type="table">
        /// <item><term>ぐるり</term><description>煙突の並ぶ長屋の屋根の海。升目は公園の四辺と揃える</description></item>
        /// <item><term>門の正面（+z）の奥</term><description>横丁の突き当たりの家並みの上に、教会の尖塔。その奥にシティの塔とクレーン</description></item>
        /// <item><term>東から南東</term><description>遠くの公園の大きな木々の塊。葉の無いプラタナスの梢が屋根の上に出る</description></item>
        /// <item><term>北東・西・南西・南</term><description>塔状の高層棟</description></item>
        /// </list>
        /// 作った mesh とマテリアルは <paramref name="made"/> へ入れる。捨てるのは呼ぶ側
        /// </summary>
        static GameObject ParkTown(Transform place, List<Object> made)
        {
            var root = new GameObject("ParkTown");
            root.hideFlags = HideFlags.HideAndDontSave;
            // 場所の子にはしない。シーンの中身に触れずに済む
            root.transform.SetPositionAndRotation(place.position, place.rotation);
            var t = new Town(root.transform, made)
            {
                Centre = ParkFarCentre,
                Eye = ParkFarEye,
                Near = ParkRing.Ground + 6f,
                Rise = ParkTownRise,
                Roofs = 420f,
                Fine = 300f,
            };
            TownPaints(t);
            // 地面。本物の遠い地面と同じマテリアルにする。縁の所で色が揃う
            t.Use("ground", EstateLandMat());
            t["ground"].Disc(new Vector3(ParkFarCentre.x, -0.05f, ParkFarCentre.z), 3000f, 64);

            var keep = new List<Vector3>();
            // 横丁の突き当たりの奥の教会。尖塔の先は 62 m まで
            TownChurch(t, keep, 150f, 3f, 90f, 62f);
            TownCity(t, 57, -24f, 44f, -14f, 95f);
            ParkTownTowers(t, keep);
            ParkTownTrees(t, keep);
            TownRoofSea(t, keep, 41, 0f);
            t.Emit();
            return root;
        }

        /// <summary>
        /// 高層棟。北東・西・南西・南に散らす。家並みの屋根（仰角 17〜21 度）の上へ頭を出す高さ。
        ///
        /// **板状の棟は置かない。** 公園からは四隅の通りの口越しに下の方だけが覗き、窓の帯の読めない
        /// 灰色の壁が一枚立っているように見えた。塔状の棟なら、屋根の上に頭を出した形で高層棟だと読める
        /// </summary>
        static void ParkTownTowers(Town t, List<Vector3> keep)
        {
            var spots = new[]
            {
                // 距離、角度、階、塔か板か
                new Vector4(140f, 52f, 22f, 0f), new Vector4(175f, 66f, 19f, 0f),
                new Vector4(165f, 262f, 21f, 0f), new Vector4(200f, 248f, 24f, 0f), new Vector4(215f, 296f, 17f, 0f),
                new Vector4(185f, 215f, 20f, 0f), new Vector4(230f, 200f, 23f, 0f),
                new Vector4(205f, 166f, 16f, 0f), new Vector4(215f, 140f, 18f, 0f),
            };
            var rnd = new System.Random(61);
            foreach (var s in spots)
            {
                var at = t.At(s.x, s.y);
                var yaw = (float)rnd.NextDouble() * 30f - 15f;
                if (s.w < 0.5f)
                {
                    const float wide = 18f;
                    var floors = (int)s.z;
                    while (floors > 6 && 1.2f + floors * 2.7f + 3f > t.Cap(s.x - wide * 0.7f)) floors--;
                    TownPoint(t, at, yaw, wide, floors, rnd);
                    keep.Add(new Vector3(at.x, 15f, at.z));
                }
                else
                {
                    var len = 60f + rnd.Next(0, 3) * 12f;
                    var floors = (int)s.z;
                    while (floors > 4 && floors * 2.8f + 1f > t.Cap(s.x - len * 0.5f)) floors--;
                    TownDeckBlock(t, at, s.y + yaw, len, floors);
                    keep.Add(new Vector3(at.x, len * 0.5f + 4f, at.z));
                }
            }
        }

        /// <summary>
        /// 遠くの公園の木々。東から南東の 110〜150 m に、葉の無い大きなプラタナスを固めて植える。
        /// 梢が家並みの屋根の上に、茶色がかった灰色の靄になって出る
        /// </summary>
        static void ParkTownTrees(Town t, List<Vector3> keep)
        {
            var rnd = new System.Random(71);
            var centre = t.At(128f, 118f);
            keep.Add(new Vector3(centre.x, 34f, centre.z));
            t["grass"].Disc(centre + Vector3.up * 0.02f, 32f, 24);
            for (var i = 0; i < 34; i++)
            {
                var a = (float)rnd.NextDouble() * Mathf.PI * 2f;
                var r = Mathf.Sqrt((float)rnd.NextDouble()) * 30f;
                var foot = centre + new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
                var high = Mathf.Min(24f + (float)rnd.NextDouble() * 8f, t.Cap(t.Reach(foot)) - 2f);
                ParkTownTree(t, foot, high, rnd);
            }
        }

        /// <summary>
        /// 遠くの大きな葉の無い木を一本。幹から細い枝を放射状に三十本ほど伸ばす。
        ///
        /// **樹冠を箱の塊で作らない。** 街路樹（<see cref="TownTree"/>）の三段の箱は、130 m 先の大きな木の塊にすると
        /// 茶色い積み木に見えた。細い枝を重ねると、撮った絵の一画素（0.4 m ほど）より細い線が混ざって、
        /// 梢の透けた靄になる
        /// </summary>
        static void ParkTownTree(Town t, Vector3 foot, float high, System.Random rnd)
        {
            var s = t["twig"];
            s.Box(foot + Vector3.up * (high * 0.25f), new Vector3(0.9f, high * 0.5f, 0.9f), Quaternion.identity);
            for (var k = 0; k < 30; k++)
            {
                var az = (float)rnd.NextDouble() * Mathf.PI * 2f;
                var rise = Mathf.Lerp(25f, 75f, (float)rnd.NextDouble()) * Mathf.Deg2Rad;
                var dir = new Vector3(Mathf.Cos(az) * Mathf.Cos(rise), Mathf.Sin(rise), Mathf.Sin(az) * Mathf.Cos(rise));
                var from = foot + Vector3.up * (high * Mathf.Lerp(0.32f, 0.6f, (float)rnd.NextDouble()));
                var len = high * Mathf.Lerp(0.28f, 0.48f, (float)rnd.NextDouble());
                s.Beam(from, from + dir * len, Mathf.Lerp(0.25f, 0.5f, (float)rnd.NextDouble()));
            }
        }
    }
}
