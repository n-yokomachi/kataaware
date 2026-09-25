using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace HalfAware.EditorTools
{
    /// <summary>
    /// 公営住宅の遠景を撮るためだけに組む、ロンドンの街並み。`HalfAware/Shoot the estate backdrop` の中で組み、
    /// 16 枚を撮り終えたらその場で捨てる。シーンにもアセットにも残らない。
    ///
    /// 部品（長屋・煙突・高層棟・教会・入れ物）は公園と分け合うので <c>BuildDiveTown.cs</c> にある。
    /// ここに持つのは団地の撮る目と、方角ごとの置き方だけ。
    ///
    /// **置くのは本物の地面の縁（<see cref="EstateFarGround"/>）より外だけ。** 内側に置くと、
    /// 板に貼ったときに足元が本物の地面に埋まる。高さは中心から仰角 12.5 度まで
    /// （<see cref="Town.Cap"/>）。それより高いと板の上端で切れる。
    ///
    /// **日本の町に固有の物は置かない**（設計書 9.1 節）。電柱と電線、戸建ての瓦屋根、神社の森、
    /// 給水塔、鉄塔と送電線、団地の棟の列、高架の道路の遮音壁、川の堤防、山の稜線は外した。
    ///
    /// 方角ごとの置き方。廊下（三階）の目の高さからは、中の層（隣の棟・長屋・高層棟）が地平から
    /// 5〜10 度までを塞ぐので、その上へ頭を出す高い物を、中の層の低い所の向こうへ置いた。
    /// <list type="table">
    /// <item><term>ぐるり</term><description>煙突の並ぶ煉瓦の長屋の屋根の海。升目の通りに沿って二〜三階の長屋の列、
    /// 列の前に通り、裏に細長い庭。ところどころに葉の無い街路樹</description></item>
    /// <item><term>北北東（日の方角）の奥</term><description>シティの高いビルとクレーン。霞にほとんど溶けて、輪郭だけが残る</description></item>
    /// <item><term>北東</term><description>教会の尖塔</description></item>
    /// <item><term>東</term><description>煉瓦のアーチが続く鉄道の高架を南北に。東の少し南に電車が一編成。
    /// 高架の向こうにガスタンクの骨組みが二つ</description></item>
    /// <item><term>西・北西・南</term><description>高層棟（塔状の棟と、板状の棟）。団地がいくつも固まる</description></item>
    /// </list>
    /// </summary>
    public static partial class BuildDive
    {
        // 鉄道の高架。長屋が避けるので、置く前から決めておく。東を南北に通す
        static readonly Vector3 RailFrom = EstateFarCentre + new Vector3(150f, 0f, 430f);
        static readonly Vector3 RailTo = EstateFarCentre + new Vector3(178f, 0f, -430f);

        /// <summary>
        /// 団地の街並みの撮る目と置いてよい所。目は輪の中心（<see cref="EstateFarCentre"/>）の二階の廊下の高さ。
        /// 仰角は 12.5 度まで。長屋の屋根の海は、地平まで屋根が切れ目なく続くよう霞が 9 割に届く 480 m まで。
        /// 320 m より遠い長屋は窓と裏の張り出しを省く
        /// </summary>
        static Town EstateFrame(Transform root, List<Object> made)
        {
            return new Town(root, made)
            {
                Centre = EstateFarCentre,
                Eye = EstateFarEye,
                Near = EstateFarGround + 6f,
                Rise = 12.5f,
                Roofs = 480f,
                Fine = 320f,
                Rail = true,
                RailFrom = RailFrom,
                RailTo = RailTo,
            };
        }

        // ---- 組む ------------------------------------------------------------------

        /// <summary>
        /// 街並みを組む。作った mesh とマテリアルは <paramref name="made"/> へ入れる。捨てるのは呼ぶ側
        /// </summary>
        static GameObject EstateTown(Transform place, List<Object> made)
        {
            var root = new GameObject("EstateTown");
            root.hideFlags = HideFlags.HideAndDontSave;
            // 場所の子にはしない。シーンの中身に触れずに済む
            root.transform.SetPositionAndRotation(place.position, place.rotation);
            var t = EstateFrame(root.transform, made);
            TownPaints(t);

            // 地面。本物の遠い地面と同じマテリアルにする。縁の所で色が揃う
            t.Use("ground", EstateLandMat());
            t["ground"].Disc(new Vector3(EstateFarCentre.x, -0.05f, EstateFarCentre.z), 3000f, 64);

            // 長屋を置かない円。x, z, 半径
            var keep = new List<Vector3>();
            TownViaduct(t);
            TownGasholders(t, keep);
            TownChurch(t, keep, 205f, 38f, 20f, float.MaxValue);
            TownCity(t, 31, 8f, 34f, 14f, float.MaxValue);
            TownTowers(t, keep);
            TownRoofSea(t, keep, 23, 14f);
            t.Emit();
            return root;
        }

        // ---- 鉄道の高架 --------------------------------------------------------------

        /// <summary>
        /// 煉瓦のアーチが続く鉄道の高架。9 m ごとに橋脚を立て、あいだに半円のアーチを一つずつ。
        /// アーチの輪は短い箱を半円に並べ、その上の壁は輪の上から床まで細い柱を並べて埋める。
        /// 床の上に低い胸壁と、架線の無い線路。東の少し南に電車が一編成
        /// </summary>
        static void TownViaduct(Town t)
        {
            const float deck = 9f;
            const float bay = 9f;
            const float pier = 1.6f;
            const float wide = 9.5f;
            const float spring = 4.4f;
            var along = RailTo - RailFrom;
            var length = new Vector2(along.x, along.z).magnitude;
            var dir = along / length;
            var rot = Quaternion.LookRotation(dir, Vector3.up);
            var side = Vector3.Cross(Vector3.up, dir);
            var radius = (bay - pier) * 0.5f;
            const int steps = 8;
            for (var s = 0f; s < length; s += bay)
            {
                var at = RailFrom + dir * s;
                if (t.Reach(at) < t.Near + 10f) continue;
                // 橋脚
                t["sooty"].Box(at + Vector3.up * (deck * 0.5f), new Vector3(wide, deck, pier), rot);
                // アーチの輪と、その上の壁
                var mid = at + dir * (bay * 0.5f);
                for (var k = 0; k < steps; k++)
                {
                    var a0 = Mathf.PI * k / steps;
                    var a1 = Mathf.PI * (k + 1) / steps;
                    var p0 = mid - dir * (Mathf.Cos(a0) * radius) + Vector3.up * (spring + Mathf.Sin(a0) * radius);
                    var p1 = mid - dir * (Mathf.Cos(a1) * radius) + Vector3.up * (spring + Mathf.Sin(a1) * radius);
                    // 輪の一片。高架の幅いっぱいの箱を、弧に沿って傾ける
                    var f = p1 - p0;
                    t["brick"].Box((p0 + p1) * 0.5f, new Vector3(wide, 0.7f, f.magnitude + 0.1f), Quaternion.LookRotation(f, Vector3.Cross(f, side)));
                    var top = Mathf.Max(p0.y, p1.y);
                    var c = (p0 + p1) * 0.5f;
                    var w = Vector3.Distance(new Vector3(p0.x, 0f, p0.z), new Vector3(p1.x, 0f, p1.z)) + 0.05f;
                    t["brick"].Box(new Vector3(c.x, (top + deck) * 0.5f, c.z), new Vector3(wide, deck - top, Mathf.Max(w, 0.2f)), rot);
                }
                // 床と胸壁
                t["sooty"].Box(mid + Vector3.up * (deck + 0.3f), new Vector3(wide + 0.4f, 0.6f, bay), rot);
                t["brick"].Box(mid + Vector3.up * (deck + 1.1f) + side * (wide * 0.5f), new Vector3(0.4f, 1.0f, bay), rot);
                t["brick"].Box(mid + Vector3.up * (deck + 1.1f) - side * (wide * 0.5f), new Vector3(0.4f, 1.0f, bay), rot);
            }

            // 電車。高架が長屋の屋根の上に見える所（中心から見て東の少し南）に一編成、五両
            var best = 0f;
            var bestGap = float.MaxValue;
            for (var s = 0f; s < length; s += 5f)
            {
                var gap = Mathf.Abs(Mathf.DeltaAngle(t.Az(RailFrom + dir * s), 96f));
                if (gap < bestGap) { bestGap = gap; best = s; }
            }
            for (var c = 0; c < 5; c++)
            {
                var at = RailFrom + dir * (best + (c - 2f) * 20.4f) + side * 1.9f + Vector3.up * (deck + 0.6f);
                t["train"].Box(at + Vector3.up * 1.9f, new Vector3(2.8f, 3.4f, 20f), rot);
                t["glass"].Box(at + Vector3.up * 2.35f, new Vector3(2.84f, 0.9f, 18.6f), rot);
                t["band"].Box(at + Vector3.up * 1.35f, new Vector3(2.86f, 0.35f, 20f), rot);
                // 戸。片側に三つずつ
                for (var d = 0; d < 3; d++)
                    t["band"].Box(at + Vector3.up * 1.95f + dir * (-6f + d * 6f), new Vector3(2.88f, 2.0f, 1.3f), rot);
            }
        }

        // ---- ガスタンクの骨組み ----------------------------------------------------------

        /// <summary>
        /// ガスタンクの骨組み（もう使われていない、円い鉄の枠だけのもの）を二つ。
        /// 円に並べた柱と、三段の輪の梁と、柱のあいだの斜めの筋交い
        /// </summary>
        static void TownGasholders(Town t, List<Vector3> keep)
        {
            TownGasholder(t, t.At(232f, 58f), 30f, 16, keep);
            TownGasholder(t, t.At(268f, 70f), 22f, 12, keep);
        }

        // ---- 高層棟 --------------------------------------------------------------------

        /// <summary>
        /// 高層棟。西から北西と、南に、団地をいくつか固める。塔状の棟（正方形の平面に二十階前後）と、
        /// 板状の棟（長い面にデッキの帯が横に通る）
        /// </summary>
        static void TownTowers(Town t, List<Vector3> keep)
        {
            var spots = new[]
            {
                // 距離、角度、階、塔か板か
                new Vector4(150f, 262f, 18f, 0f), new Vector4(185f, 248f, 21f, 0f), new Vector4(215f, 275f, 22f, 0f),
                new Vector4(175f, 300f, 9f, 1f), new Vector4(250f, 312f, 24f, 0f), new Vector4(230f, 330f, 20f, 0f),
                new Vector4(170f, 205f, 8f, 1f), new Vector4(210f, 190f, 19f, 0f), new Vector4(260f, 222f, 23f, 0f),
                new Vector4(190f, 138f, 16f, 0f), new Vector4(290f, 160f, 22f, 0f), new Vector4(120f, 235f, 7f, 1f),
            };
            var rnd = new System.Random(53);
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
                    // 長手を中心から見て横に向け、デッキの面を中心の側へ
                    TownDeckBlock(t, at, s.y + yaw, len, floors);
                    keep.Add(new Vector3(at.x, len * 0.5f + 4f, at.z));
                }
            }
        }
    }
}
