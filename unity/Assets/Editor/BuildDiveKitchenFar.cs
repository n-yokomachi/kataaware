using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace HalfAware.EditorTools
{
    /// <summary>
    /// 場面 4 の台所の空・日・霞・環境光と、遠景の書き割り（設計書 9.1 節「台所の作り込み」の「時刻と光」「窓の外」の「撮って貼る」）。
    /// 仕組みは公営住宅・公園と同じで、空は <c>BuildDiveSky.cs</c>、書き割りは <c>BuildDiveFar.cs</c>、
    /// 撮る街並みの部品は <c>BuildDiveTown.cs</c> を使う。ここに持つのは台所の値だけ。
    ///
    /// **3 月の 06:55〜06:56、日の出の直後。** 日は窓の側（東南東）にあり、空は日の側の地平が暖かく、上へ行くほど青い。
    /// 日の当たらない所は青白い（設計書「色は青白い朝で、日の当たる所だけ暖かい」）。
    ///
    /// **窓から見える遠くは、裏庭の奥の塀の向こうの、隣の並びの裏側だけ。** 裏庭を挟んで背中合わせに建つ
    /// 次の通りのテラスハウスが、裏の壁と張り出し・窓・縦樋・煙突を見せる。朝日はその並びの向こうにあるので、
    /// 裏の壁は日陰で、いくつかの窓に朝の灯りが入っている。並びの屋根の上には、東北東の高層棟と南東の教会の尖塔だけが頭を出す。
    /// 窓の上端のせいで、台所の中からは並びの二階の窓までしか見えない。窓に寄って見上げると、煙突の列と朝の空が見える
    /// </summary>
    public static partial class BuildDive
    {
        // ---- 空と光 --------------------------------------------------------------

        const string KitchenSkyPath = DiveTextures + "KitchenSky.png";
        const string KitchenSkyMatPath = Materials + "KitchenSkybox.mat";

        /// <summary>
        /// 霞の濃さ（ExponentialSquared の density）。台所の中（5 m）ではほとんど掛からず、
        /// 隣の並び（30 m）で 3%、塔（110 m）で 31%。朝の街の薄い靄
        /// </summary>
        const float KitchenHazeDensity = 0.0055f;

        // 空の色。sRGB で書き、linear へ直して SkyPaint へ渡す。公営住宅の朝より地平を青白く、日の側だけ暖かく
        static readonly Color KitchenZenith = new Color(0.30f, 0.43f, 0.66f);
        static readonly Color KitchenMiddle = new Color(0.52f, 0.62f, 0.78f);
        /// <summary>地平の色。霞の色もこれにする</summary>
        static readonly Color KitchenHorizon = new Color(0.78f, 0.82f, 0.88f);
        static readonly Color KitchenWarm = new Color(1.00f, 0.80f, 0.62f);
        static readonly Color KitchenGlow = new Color(1.00f, 0.86f, 0.68f);
        static readonly Color KitchenDisc = new Color(1.00f, 0.97f, 0.88f);
        static readonly Color KitchenCloudLit = new Color(1.00f, 0.86f, 0.74f);
        static readonly Color KitchenCloudShade = new Color(0.62f, 0.66f, 0.76f);
        /// <summary>地面の照り返しの色。芝と敷石。sRGB</summary>
        static readonly Color KitchenBounce = new Color(0.44f, 0.45f, 0.38f);
        /// <summary>
        /// 環境光の倍率。台所の中はどこも同じ環境光になるので、公営住宅の部屋と同じ 1 に置く。
        /// 上げると日の当たらない床まで明るくなって、窓から落ちる四角が目立たなくなる
        /// </summary>
        const float KitchenAmbientGain = 1.0f;

        /// <summary>
        /// 台所の空の見え方。<paramref name="sun"/> は日へ向かう向き（影を落とす灯りの逆）。
        /// 日の円は公営住宅と同じく実際より大きく取る（半径 1.3 度）。日の出の直後なので、地平の暖かさを強く広く
        /// </summary>
        static SkyPaint.Look KitchenSkyLook(Vector3 sun)
        {
            return new SkyPaint.Look
            {
                zenith = KitchenZenith.linear,
                middle = KitchenMiddle.linear,
                horizon = KitchenHorizon.linear,
                whiteDepth = 0.09f,
                warm = KitchenWarm.linear,
                warmth = 0.90f,
                warmDepth = 0.14f,
                warmFocus = 3f,
                sun = sun.normalized,
                glow = KitchenGlow.linear,
                glowNear = 0.50f,
                glowNearWidth = 4f,
                glowWide = 0.24f,
                glowWideWidth = 28f,
                disc = KitchenDisc.linear,
                discRadius = 1.3f,
                discSoft = 0.5f,
                cloudLit = KitchenCloudLit.linear,
                cloudShade = KitchenCloudShade.linear,
                cloudCover = 0.55f,
                // 仰角 2 度から 16 度。並びの屋根の上に薄く流れる
                cloudLow = Mathf.Sin(2f * Mathf.Deg2Rad),
                cloudHigh = Mathf.Sin(16f * Mathf.Deg2Rad),
                cloudSeed = 13,
            };
        }

        /// <summary>日へ向かう向き。朝の日射し（Morning）の逆</summary>
        static Vector3 KitchenSunward(Transform place)
        {
            return Sunward(place, "Morning", KitchenMorningAim);
        }

        /// <summary>台所の空・霞・環境光・日を一揃いで。空の絵とマテリアルもここで焼き直す</summary>
        static PlaceSky KitchenPlaceSky(Transform place)
        {
            return PaintedSky(place, "Morning", KitchenSkyLook(KitchenSunward(place)), KitchenHorizon, KitchenHazeDensity,
                KitchenBounce, KitchenAmbientGain, KitchenSkyPath, KitchenSkyMatPath);
        }

        // ---- 書き割りの輪 ----------------------------------------------------------

        /// <summary>
        /// 書き割りの輪の中心。場所のローカル。台所の真ん中の、窓の正面の線の上
        /// </summary>
        public static readonly Vector3 KitchenFarCentre = new Vector3(0.6f, 0f, 1.3f);
        /// <summary>撮る目の高さ。記憶 5・6・12 の目（1.72・1.58・1.65）の真ん中あたり</summary>
        public const float KitchenFarEye = 1.64f;
        /// <summary>
        /// 本物の地面の遠い縁。中心から辺まで。裏庭の奥の塀（x 14.2）のすぐ外。
        /// 塀は目より高いので、塀の向こうの地面と書き割りの境目は、台所のどこからも見えない
        /// </summary>
        const float KitchenFarGround = 16f;
        /// <summary>
        /// 中心から板の辺まで。公営住宅と公園（200 m）より近くに置く。窓から見える遠くは 25〜40 m 先の隣の並びで、
        /// 板を遠くに置くほど、台所の中を歩いたときに並びが本物からずれて見える
        /// </summary>
        const float KitchenFarRadius = 45f;
        /// <summary>中心から見た仰角の上限。度。並びの煙突（18 度）と、その上に頭を出す塔（26 度）が収まる</summary>
        const float KitchenTownRise = 30f;
        /// <summary>板の上端。仰角の上限が板の上に落ちる高さ</summary>
        public const float KitchenFarTop = 28f;

        /// <summary>台所の輪</summary>
        static readonly FarRing KitchenRing = new FarRing
        {
            Centre = KitchenFarCentre,
            Eye = KitchenFarEye,
            Radius = KitchenFarRadius,
            Ground = KitchenFarGround,
            Top = KitchenFarTop,
            Picture = DiveTextures + "KitchenBackdrop.png",
            Material = Materials + "KitchenBackdrop.mat",
            Name = "KitchenBackdrop",
        };

        // ---- 撮る ----------------------------------------------------------------

        /// <summary>
        /// 遠景を撮る。撮るためだけの街並みを組み（<see cref="KitchenTown"/>）、台所の空・霞・日・環境光のまま 16 枚を撮り、
        /// 一枚の絵に並べて書き出す。組んだ物は全部捨ててから返る。輪に貼るのは `HalfAware/Build the dive`
        /// </summary>
        [MenuItem("HalfAware/Shoot the kitchen backdrop", false, 254)]
        public static void ShootKitchenBackdrop()
        {
            ShootBackdrop(DiveIds.Kitchen, KitchenRing, KitchenPlaceSky, KitchenTown);
        }

        /// <summary>
        /// 台所の書き割りに撮る街並み。
        /// <list type="table">
        /// <item><term>窓の正面（東）</term><description>裏庭の奥の塀の向こうで背中合わせに建つ、次の通りのテラスハウスの裏側。
        /// 裏の壁・張り出し・窓（いくつかに朝の灯り）・縦樋・煙突。裏庭の小屋の屋根と、葉の無い木</description></item>
        /// <item><term>その上</term><description>東北東の塔状の高層棟と、南東の教会の尖塔</description></item>
        /// <item><term>ぐるり</term><description>煙突の並ぶ長屋の屋根の海。升目は台所の並びと揃えて南北に。窓からは隣の並びに隠れて見えない</description></item>
        /// </list>
        /// 作った mesh とマテリアルは <paramref name="made"/> へ入れる。捨てるのは呼ぶ側
        /// </summary>
        static GameObject KitchenTown(Transform place, List<Object> made)
        {
            var root = new GameObject("KitchenTown");
            root.hideFlags = HideFlags.HideAndDontSave;
            // 場所の子にはしない。シーンの中身に触れずに済む
            root.transform.SetPositionAndRotation(place.position, place.rotation);
            var t = new Town(root.transform, made)
            {
                Centre = KitchenFarCentre,
                Eye = KitchenFarEye,
                Near = KitchenRing.Ground + 6f,
                Rise = KitchenTownRise,
                Roofs = 320f,
                Fine = 160f,
            };
            TownPaints(t);
            // 朝の灯りの入った窓。公営住宅の中の層の灯った窓と同じ光る色
            t.Use("lit", Glow("EstateMidLit", new Color(1f, 0.86f, 0.62f), 0.90f));
            // 地面。本物の遠い地面と同じマテリアルにする。縁の所で色が揃う
            t.Use("ground", EstateLandMat());
            t["ground"].Disc(new Vector3(KitchenFarCentre.x, -0.05f, KitchenFarCentre.z), 3000f, 64);

            var keep = new List<Vector3>();
            var rnd = new System.Random(53);
            KitchenTownBacks(t, keep, rnd);
            // 並びの屋根の上に頭を出す物
            var tower = t.At(110f, 62f);
            TownPoint(t, tower, 12f, 16f, 20, rnd);
            keep.Add(new Vector3(tower.x, 15f, tower.z));
            TownChurch(t, keep, 125f, 128f, 20f, 56f);
            TownRoofSea(t, keep, 29, 90f);
            t.Emit();
            return root;
        }

        /// <summary>
        /// 隣の並びの裏側。台所の並びと背中合わせに、裏の壁を西（台所の側）へ向けたテラスハウスを南北に二列。
        /// 形は長屋の部品（<see cref="TownTerrace"/>）で組み、裏から見える所だけ足す。
        /// 張り出しの端の窓と裏の壁の窓のいくつかに朝の灯り、戸境の縦樋、裏庭の小屋と葉の無い木
        /// </summary>
        static void KitchenTownBacks(Town t, List<Vector3> keep, System.Random rnd)
        {
            // 長屋の奥行きの真ん中。裏の壁は x 27.5、張り出しの端は x 23
            const float mx = 32f;
            const float deep = 9f;
            const float outDeep = 4.5f;
            var rows = new[] { new Vector2(-44f, 0f), new Vector2(0f, 44f) };
            for (var r = 0; r < rows.Length; r++)
            {
                var z0 = rows[r].x + 0.5f;
                var z1 = rows[r].y - 0.5f;
                var len = z1 - z0;
                var mid = new Vector3(mx, 0f, (z0 + z1) * 0.5f);
                const int storeys = 2;
                TownTerrace(t, mid, 270f, len, storeys, r == 0 ? "stock" : "stockDark", true, rnd);
                var rot = Quaternion.Euler(0f, 270f, 0f);
                System.Func<float, float, float, Vector3> p = (x, y, z) => mid + rot * new Vector3(x, y, z);
                var back = rot * Vector3.forward;
                var side = rot * Vector3.right;
                var houses = Mathf.Max(1, Mathf.RoundToInt(len / 5f));
                var step = len / houses;
                var eaves = storeys * 3f + 0.4f;
                for (var h = 0; h < houses; h++)
                {
                    var c = -len * 0.5f + (h + 0.5f) * step;
                    // 裏の壁の窓の灯り。部品の窓（c + step 0.2 の列）の手前に重ねる
                    for (var s = 0; s < storeys; s++)
                        if (rnd.NextDouble() < 0.28)
                            t["lit"].Face(p(c + step * 0.2f, s * 3f + 1.6f, deep * 0.5f + 0.04f), back, 0.9f, 1.4f);
                    // 張り出しの端の窓。一階と二階
                    for (var s = 0; s < storeys; s++)
                    {
                        var lit = rnd.NextDouble() < 0.22;
                        t[lit ? "lit" : "glass"].Face(p(c - step * 0.22f, s * 3f + 1.5f, deep * 0.5f + outDeep + 0.03f), back, 0.8f, 1.3f);
                        t["trim"].Face(p(c - step * 0.22f, s * 3f + 0.8f, deep * 0.5f + outDeep + 0.035f), back, 1.0f, 0.1f);
                    }
                    // 張り出しの脇の窓（北の面）
                    t["glass"].Face(p(c + step * 0.01f + 0.03f, 1.5f, deep * 0.5f + outDeep * 0.5f), side, 0.9f, 1.3f);
                    // 戸境の縦樋
                    var edge = -len * 0.5f + h * step;
                    t["iron"].Beam(p(edge + 0.1f, 0f, deep * 0.5f + 0.1f), p(edge + 0.1f, eaves, deep * 0.5f + 0.1f), 0.1f);
                    // 裏庭の小屋。三軒に一つ
                    if (h % 3 == 1)
                    {
                        var shed = p(c + step * 0.15f, 0f, deep * 0.5f + 10.5f);
                        t["sooty"].Box(shed + Vector3.up * 1.0f, new Vector3(2.0f, 2.0f, 1.8f), rot);
                        t["slate"].Gable(shed + Vector3.up * 2.0f, 2.2f, 2.0f, 0.4f, 270f);
                    }
                }
                // 裏庭の木。葉の無い細い枝
                for (var i = 0; i < 4; i++)
                {
                    var along = -len * 0.5f + (i + 0.3f + (float)rnd.NextDouble() * 0.4f) * len / 4f;
                    var foot = p(along, 0f, deep * 0.5f + 5.5f + (float)rnd.NextDouble() * 4f);
                    KitchenTownTree(t, foot, 7f + (float)rnd.NextDouble() * 3f, rnd);
                }
                for (var z = z0; z <= z1; z += 8f)
                    keep.Add(new Vector3(mx, 12f, z));
            }
        }

        /// <summary>
        /// 裏庭の葉の無い木を一本。幹から太い枝を五本、枝の先から細い枝を五本ずつ。
        ///
        /// 公園の遠くの木（<see cref="ParkTownTree"/>）は 130 m 先の靄に見せる太さで、20 m 先に置くと
        /// 黒い棒が放射状に広がった椰子の形になった。近い木は枝を細く分けて、先ほど撮った絵の一画素より細くする
        /// </summary>
        static void KitchenTownTree(Town t, Vector3 foot, float high, System.Random rnd)
        {
            var s = t["twig"];
            var crotch = foot + Vector3.up * (high * 0.42f);
            s.Beam(foot, crotch, 0.28f);
            for (var k = 0; k < 5; k++)
            {
                var az = (k + (float)rnd.NextDouble() * 0.6f) / 5f * Mathf.PI * 2f;
                var rise = Mathf.Lerp(45f, 70f, (float)rnd.NextDouble()) * Mathf.Deg2Rad;
                var dir = new Vector3(Mathf.Cos(az) * Mathf.Cos(rise), Mathf.Sin(rise), Mathf.Sin(az) * Mathf.Cos(rise));
                var from = crotch - Vector3.up * (high * 0.05f * (float)rnd.NextDouble());
                var limb = from + dir * (high * Mathf.Lerp(0.30f, 0.40f, (float)rnd.NextDouble()));
                s.Beam(from, limb, 0.13f);
                for (var j = 0; j < 5; j++)
                {
                    var a2 = az + ((float)rnd.NextDouble() - 0.5f) * 1.6f;
                    var r2 = Mathf.Lerp(30f, 80f, (float)rnd.NextDouble()) * Mathf.Deg2Rad;
                    var d2 = new Vector3(Mathf.Cos(a2) * Mathf.Cos(r2), Mathf.Sin(r2), Mathf.Sin(a2) * Mathf.Cos(r2));
                    var root = Vector3.Lerp(from, limb, Mathf.Lerp(0.4f, 1f, (float)rnd.NextDouble()));
                    s.Beam(root, root + d2 * (high * Mathf.Lerp(0.18f, 0.30f, (float)rnd.NextDouble())), 0.05f);
                }
            }
        }
    }
}
