using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace HalfAware.EditorTools
{
    /// <summary>
    /// 場面 4 の教室の空・日・霞・環境光と、遠景の書き割り（設計書 9.1 節「教室の作り込み」の「時刻と光」「窓の外」）。
    /// 仕組みは公営住宅・公園・台所と同じで、空は <c>BuildDiveSky.cs</c>、書き割りは <c>BuildDiveFar.cs</c>、
    /// 撮る街並みの部品は <c>BuildDiveTown.cs</c> を使う。ここに持つのは教室の値だけ。
    ///
    /// **3 月の 11:38〜11:39、昼前。** 日は窓の側（南のやや東）の仰角 31 度ほど。空は上へ行くほど淡い青、地平は白い。
    ///
    /// **窓の外は白く飛ばす。ただし真っ白の板にはしない。** 窓は日の方を向いているので、外は逆光の明るさになる。
    /// 霞を濃く（<see cref="ClassroomHazeDensity"/>）して、校庭の向こうの柵と木（40 m）は半分、
    /// 向かいの校舎（55 m）は三分の二ほど白に沈め、輪郭だけがうっすら見える露出を上げた絵にする。
    /// その先の街並みは霞に消えるので組まない
    /// </summary>
    public static partial class BuildDive
    {
        // ---- 空と光 --------------------------------------------------------------

        const string ClassroomSkyPath = DiveTextures + "ClassroomSky.png";
        const string ClassroomSkyMatPath = Materials + "ClassroomSkybox.mat";

        /// <summary>
        /// 霞の濃さ（ExponentialSquared の density）。教室の中（9 m）では 3%、校庭の奥の木（28 m）で 27%、
        /// 柵と木（40 m）で 47%、向かいの校舎（55 m）で 70%。晴れた昼の、日の側の白い靄
        /// </summary>
        const float ClassroomHazeDensity = 0.020f;

        // 空の色。sRGB で書き、linear へ直して SkyPaint へ渡す。台所の朝より明るく、地平はほとんど白
        static readonly Color ClassroomZenith = new Color(0.40f, 0.55f, 0.78f);
        static readonly Color ClassroomMiddle = new Color(0.62f, 0.73f, 0.87f);
        /// <summary>地平の色。霞の色もこれにする</summary>
        static readonly Color ClassroomHorizon = new Color(0.88f, 0.90f, 0.92f);
        static readonly Color ClassroomWarm = new Color(1.00f, 0.96f, 0.88f);
        static readonly Color ClassroomGlow = new Color(1.00f, 0.97f, 0.90f);
        static readonly Color ClassroomDisc = new Color(1.00f, 1.00f, 0.96f);
        static readonly Color ClassroomCloudLit = new Color(1.00f, 1.00f, 0.98f);
        static readonly Color ClassroomCloudShade = new Color(0.76f, 0.79f, 0.85f);
        /// <summary>地面の照り返しの色。校庭のアスファルト。sRGB</summary>
        static readonly Color ClassroomBounce = new Color(0.46f, 0.46f, 0.44f);
        /// <summary>
        /// 環境光の倍率。台所（1）より上げる。教室は台所の三倍の広さがあり、窓から遠い廊下側と天井が沈む。
        /// 上げすぎると日の当たらない床まで明るくなって、窓の四角が目立たなくなる
        /// </summary>
        const float ClassroomAmbientGain = 1.5f;

        /// <summary>
        /// 教室の空の見え方。<paramref name="sun"/> は日へ向かう向き（影を落とす灯りの逆）。
        /// 昼なので地平の暖かさは弱く、日の周りの白い輝きを広く取る
        /// </summary>
        static SkyPaint.Look ClassroomSkyLook(Vector3 sun)
        {
            return new SkyPaint.Look
            {
                zenith = ClassroomZenith.linear,
                middle = ClassroomMiddle.linear,
                horizon = ClassroomHorizon.linear,
                whiteDepth = 0.14f,
                warm = ClassroomWarm.linear,
                warmth = 0.35f,
                warmDepth = 0.20f,
                warmFocus = 2f,
                sun = sun.normalized,
                glow = ClassroomGlow.linear,
                glowNear = 0.60f,
                glowNearWidth = 5f,
                glowWide = 0.32f,
                glowWideWidth = 36f,
                disc = ClassroomDisc.linear,
                discRadius = 1.3f,
                discSoft = 0.5f,
                cloudLit = ClassroomCloudLit.linear,
                cloudShade = ClassroomCloudShade.linear,
                cloudCover = 0.45f,
                // 仰角 3 度から 22 度。昼の薄い積雲
                cloudLow = Mathf.Sin(3f * Mathf.Deg2Rad),
                cloudHigh = Mathf.Sin(22f * Mathf.Deg2Rad),
                cloudSeed = 17,
            };
        }

        /// <summary>日へ向かう向き。昼の日射し（Noon）の逆</summary>
        static Vector3 ClassroomSunward(Transform place)
        {
            return Sunward(place, "Noon", ClassNoonAim);
        }

        /// <summary>教室の空・霞・環境光・日を一揃いで。空の絵とマテリアルもここで焼き直す</summary>
        static PlaceSky ClassroomPlaceSky(Transform place)
        {
            return PaintedSky(place, "Noon", ClassroomSkyLook(ClassroomSunward(place)), ClassroomHorizon, ClassroomHazeDensity,
                ClassroomBounce, ClassroomAmbientGain, ClassroomSkyPath, ClassroomSkyMatPath);
        }

        // ---- 書き割りの輪 ----------------------------------------------------------

        /// <summary>書き割りの輪の中心。場所のローカル。教室の真ん中</summary>
        public static readonly Vector3 ClassroomFarCentre = new Vector3(0.4f, 0f, 0.5f);
        /// <summary>撮る目の高さ。記憶 8・14・15 の目（1.70・1.20・1.20）の間</summary>
        public const float ClassroomFarEye = 1.4f;
        /// <summary>本物の地面の遠い縁。中心から辺まで。校庭のコートの奥の角（29 m）のすぐ外</summary>
        const float ClassroomFarGround = 30f;
        /// <summary>
        /// 中心から板の辺まで。台所と同じく近くに置く。窓から見える遠くは 40〜60 m 先の柵と向かいの校舎で、
        /// 板を遠くに置くほど、教室の中を歩いたときに校舎が本物からずれて見える
        /// </summary>
        const float ClassroomFarRadius = 45f;
        /// <summary>中心から見た仰角の上限。度。向かいの校舎（11 度）と木（15 度）が収まる</summary>
        const float ClassroomTownRise = 22f;
        /// <summary>板の上端。仰角の上限が板の上に落ちる高さ</summary>
        public const float ClassroomFarTop = 20f;

        /// <summary>教室の輪</summary>
        static readonly FarRing ClassroomRing = new FarRing
        {
            Centre = ClassroomFarCentre,
            Eye = ClassroomFarEye,
            Radius = ClassroomFarRadius,
            Ground = ClassroomFarGround,
            Top = ClassroomFarTop,
            Picture = DiveTextures + "ClassroomBackdrop.png",
            Material = Materials + "ClassroomBackdrop.mat",
            Name = "ClassroomBackdrop",
        };

        // ---- 撮る ----------------------------------------------------------------

        /// <summary>
        /// 遠景を撮る。撮るためだけの校舎と木を組み（<see cref="ClassroomTown"/>）、教室の空・霞・日・環境光のまま 16 枚を撮り、
        /// 一枚の絵に並べて書き出す。組んだ物は全部捨ててから返る。輪に貼るのは `HalfAware/Build the dive`
        /// </summary>
        [MenuItem("HalfAware/Shoot the classroom backdrop", false, 255)]
        public static void ShootClassroomBackdrop()
        {
            ShootBackdrop(DiveIds.Classroom, ClassroomRing, ClassroomPlaceSky, ClassroomTown);
        }

        /// <summary>
        /// 教室の書き割りに撮る物。窓は -x（南）を向く。
        /// <list type="table">
        /// <item><term>窓の正面</term><description>校庭の奥の縁の、黒い鉄の柵と葉の無い木の列。その向こうに向かいの校舎
        /// （1960〜70 年代の三階建て。コンクリートの骨組みと帯の窓、端に煉瓦の階段室）</description></item>
        /// <item><term>両脇</term><description>体育館の低い棟と、二階建ての煉瓦の棟</description></item>
        /// </list>
        /// その先は霞に消えるので組まない。
        /// 作った mesh とマテリアルは <paramref name="made"/> へ入れる。捨てるのは呼ぶ側
        /// </summary>
        static GameObject ClassroomTown(Transform place, List<Object> made)
        {
            var root = new GameObject("ClassroomTown");
            root.hideFlags = HideFlags.HideAndDontSave;
            // 場所の子にはしない。シーンの中身に触れずに済む
            root.transform.SetPositionAndRotation(place.position, place.rotation);
            var t = new Town(root.transform, made)
            {
                Centre = ClassroomFarCentre,
                Eye = ClassroomFarEye,
                Near = ClassroomRing.Ground + 6f,
                Rise = ClassroomTownRise,
                Roofs = 0f,
                Fine = 120f,
            };
            TownPaints(t);
            // 地面。本物の遠い地面（校庭）と同じマテリアルにする。縁の所で色が揃う
            t.Use("ground", ClassYardMat());
            t["ground"].Disc(new Vector3(ClassroomFarCentre.x, -0.05f, ClassroomFarCentre.z), 3000f, 64);
            var rnd = new System.Random(1139);

            // 校庭の奥の縁の柵。窓の正面を南北に通し、両脇で校舎の方へ折り返す
            const float fence = -37f;
            ClassroomRailing(t, new Vector3(fence, 0f, -36f), new Vector3(fence, 0f, 38f));
            ClassroomRailing(t, new Vector3(fence, 0f, -36f), new Vector3(-8f, 0f, -36f));
            ClassroomRailing(t, new Vector3(fence, 0f, 38f), new Vector3(-8f, 0f, 38f));
            // 柵の内側の木の列。柵と同じ線に並べ、窓の正面は間を空ける
            var trees = new[] { -30f, -21f, -9f, 13f, 24f, 33f };
            foreach (var z in trees)
                KitchenTownTree(t, new Vector3(fence + 2.5f + (float)rnd.NextDouble() * 2f, 0f, z), 9.5f + (float)rnd.NextDouble() * 3f, rnd);
            KitchenTownTree(t, new Vector3(-30f, 0f, -40f), 11f, rnd);
            KitchenTownTree(t, new Vector3(-29f, 0f, 42f), 12f, rnd);

            // 向かいの校舎
            ClassroomBlock(t, new Vector3(-58f, 0f, 2f), 70f, 3);
            // 両脇の低い棟
            ClassroomHall(t, new Vector3(-50f, 0f, -44f));
            t["stockDark"].Box(new Vector3(-52f, 3.3f, 52f), new Vector3(14f, 6.6f, 24f), Quaternion.identity);
            t["slate"].Box(new Vector3(-52f, 6.75f, 52f), new Vector3(14.4f, 0.3f, 24.4f), Quaternion.identity);
            for (var s = 0; s < 2; s++)
                t["glass"].Face(new Vector3(-44.98f, s * 3.2f + 1.7f, 52f), Vector3.right, 20f, 1.3f);
            t.Emit();
            return root;
        }

        /// <summary>黒い鉄の柵を a から b まで。高さ 2 m、支柱 2.5 m ごと、上下の横桟と細い縦の棒</summary>
        static void ClassroomRailing(Town t, Vector3 a, Vector3 b)
        {
            var len = (b - a).magnitude;
            var dir = (b - a) / len;
            t["iron"].Beam(a + Vector3.up * 1.9f, b + Vector3.up * 1.9f, 0.05f);
            t["iron"].Beam(a + Vector3.up * 0.25f, b + Vector3.up * 0.25f, 0.05f);
            for (var d = 0f; d <= len; d += 2.5f)
                t["iron"].Beam(a + dir * d, a + dir * d + Vector3.up * 2.05f, 0.09f);
            for (var d = 0.25f; d < len; d += 0.25f)
                t["iron"].Beam(a + dir * d + Vector3.up * 0.25f, a + dir * d + Vector3.up * 2.0f, 0.025f);
        }

        /// <summary>
        /// 1960〜70 年代の校舎を一棟。長手を南北に通し、教室の側（+x）へ表を向ける。
        /// コンクリートの床の帯と、その間の帯の窓、等間隔の縦の骨。平らな屋根に笠木。北の端に煉瓦の階段室
        /// </summary>
        static void ClassroomBlock(Town t, Vector3 mid, float len, int storeys)
        {
            const float deep = 12f;
            const float storey = 3.5f;
            var high = storeys * storey + 0.6f;
            var face = mid.x + deep * 0.5f;
            t["concreteDark"].Box(new Vector3(mid.x, high * 0.5f, mid.z), new Vector3(deep, high, len), Quaternion.identity);
            for (var s = 0; s < storeys; s++)
            {
                var y0 = s * storey;
                // 床の帯（明るいコンクリート）と、窓の帯
                t["concrete"].Box(new Vector3(face + 0.05f, y0 + 0.45f, mid.z), new Vector3(0.2f, 0.9f, len), Quaternion.identity);
                t["glass"].Face(new Vector3(face + 0.02f, y0 + 2.1f, mid.z), Vector3.right, len - 1f, 2.2f);
            }
            t["trim"].Box(new Vector3(face + 0.1f, high - 0.2f, mid.z), new Vector3(0.3f, 0.4f, len + 0.3f), Quaternion.identity);
            for (var z = mid.z - len * 0.5f + 1.5f; z < mid.z + len * 0.5f; z += 3.0f)
                t["concrete"].Box(new Vector3(face + 0.15f, high * 0.5f, z), new Vector3(0.3f, high, 0.3f), Quaternion.identity);
            // 階段室
            t["brick"].Box(new Vector3(face - 2f, (high + 2.5f) * 0.5f, mid.z + len * 0.5f - 4f), new Vector3(5f, high + 2.5f, 6f), Quaternion.identity);
            t["glass"].Face(new Vector3(face + 0.52f, high * 0.5f + 1f, mid.z + len * 0.5f - 4f), Vector3.right, 1.2f, high - 2f);
        }

        /// <summary>体育館。高い一層の煉瓦の箱に、上の方だけの横長の窓と、浅い屋根</summary>
        static void ClassroomHall(Town t, Vector3 mid)
        {
            const float wide = 20f;
            const float deep = 26f;
            const float high = 7.5f;
            t["brick"].Box(new Vector3(mid.x, high * 0.5f, mid.z), new Vector3(wide, high, deep), Quaternion.identity);
            t["lead"].Box(new Vector3(mid.x, high + 0.2f, mid.z), new Vector3(wide + 0.4f, 0.4f, deep + 0.4f), Quaternion.identity);
            t["glass"].Face(new Vector3(mid.x + wide * 0.5f + 0.02f, high - 1.4f, mid.z), Vector3.right, deep - 3f, 1.2f);
        }
    }
}
