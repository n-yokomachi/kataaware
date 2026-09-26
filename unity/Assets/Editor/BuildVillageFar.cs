using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace HalfAware.EditorTools
{
    /// <summary>
    /// 村の中と遠く（設計書 2 節の 7）。
    ///
    /// **中（組む）:** 村の縁の生け垣で区切られた畑。麦畑は場面 8 の最後の帯と同じ作りで、
    /// 絵の α で形を抜いた札を交差させて立て（<see cref="Bank.Card"/>）、HalfAware/Wheat でなびかせる。
    /// マテリアルは場面 8 の麦と畑の地（<c>Assets/Materials/Drive/Wheat.mat</c>・<c>FieldCrop.mat</c>）を写して、
    /// 朝靄の溜まりだけ切る（村は時刻を切り替えるので、場面 8 の朝の靄の色を焼き込まない）。
    /// 札は路地から離れるほど幅を広く、間を粗くする。家の裏の畑は塀と生け垣の上に穂先が覗くだけなので粗くてよい。
    ///
    /// **遠く（撮って貼る）:** 麦畑と牧草地の継ぎはぎの丘、生け垣と雑木林、遠くの農家。
    /// 村の教会は書き割りに撮らず、村の東の外れに組む（<see cref="ChurchNear"/>、設計書 7 節）。
    /// 場面 4 と同じ書き割りの道具（<see cref="FarBackdrop"/>）で、路地の真ん中を中心にした輪に貼る。
    ///
    /// **書き割りは時刻ごとに二枚撮る。** 朝は東北東の低い日に靄、夕方は西北西の地平の日に暖かい霞で、
    /// 丘の明るい面と影の向きが逆になる。一枚を両方に使うと、夕方に朝の影の丘が立つ。
    /// 輪は時刻の灯りの子（Hours/Morning・Hours/Evening）に置くので、<see cref="VillageHour"/> が灯りと一緒に切り替える。
    /// 描く回数はどちらの時刻も一回のまま
    /// </summary>
    public static partial class BuildVillage
    {
        // ---- 書き割りの輪 ----------------------------------------------------------

        /// <summary>輪の中心。路地の真ん中あたり（車の着く所と片割れの家の中ほど）</summary>
        static readonly Vector3 FarCentre = new Vector3(-31f, 0f, 2f);

        /// <summary>
        /// 輪。歩ける所は中心から 49 m まで（車の着く所と片割れの裏庭の奥）。
        /// 板の一番遠い角（193.7 m）まで、歩ける所のどこからでも 243 m で、カメラの far（260 m）に収まる
        /// </summary>
        static FarRing VillageRing(bool morning)
        {
            var tag = morning ? "Morning" : "Evening";
            return new FarRing
            {
                Centre = FarCentre,
                Eye = 1.6f,
                Radius = 190f,
                Ground = 100f,
                Panels = 16,
                Top = 46f,
                Picture = Textures + "VillageBackdrop" + tag + ".png",
                Material = Materials + "VillageBackdrop" + tag + ".mat",
                Name = "VillageBackdrop" + tag,
            };
        }

        const string ShootMenu = "HalfAware/Shoot the village backdrop";

        /// <summary>書き割りの輪を時刻の灯りの子に置く。絵が無ければ一行だけ知らせる</summary>
        static void Backdrops(Transform hours)
        {
            FarBackdrop.Place(hours.Find("Morning"), VillageRing(true), ShootMenu, Generated);
            FarBackdrop.Place(hours.Find("Evening"), VillageRing(false), ShootMenu, Generated);
        }

        /// <summary>
        /// 遠景を撮る。朝と夕方に切り替えてそれぞれの空・霞・日のまま撮り、二枚の絵に書き出す。
        /// 組んだ物は全部捨て、時刻を元に戻してから返る。輪に貼るのは `HalfAware/Build the village`
        /// </summary>
        [MenuItem(ShootMenu, false, 273)]
        public static void ShootMenuItem()
        {
            if (EditorApplication.isPlaying || EditorApplication.isCompiling)
            {
                Debug.LogError("再生中とコンパイル中は撮らない");
                return;
            }
            var root = GameObject.Find("Village");
            var hour = Object.FindFirstObjectByType<VillageHour>(FindObjectsInactive.Include);
            if (root == null || hour == null)
            {
                Debug.LogError("村がまだ組まれていない。先に HalfAware/Build the village");
                return;
            }
            var kept = hour.Current;
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            var wasDirty = scene.isDirty;
            try
            {
                foreach (var h in new[] { VillageHour.Hour.Morning, VillageHour.Hour.Evening })
                {
                    SetHour(h);
                    var ring = VillageRing(h == VillageHour.Hour.Morning);
                    var picture = FarBackdrop.Take(root.transform, hour.SkyOf(h), ring, VillageTown);
                    if (picture != null) FarBackdrop.Write(ring, picture);
                }
            }
            finally
            {
                SetHour(kept);
                if (!wasDirty && scene.isDirty) ClearDirty(scene);
            }
            Debug.Log("村の遠景を撮った。HalfAware/Build the village で輪に貼る");
        }

        static void ClearDirty(UnityEngine.SceneManagement.Scene scene)
        {
            var m = typeof(UnityEditor.SceneManagement.EditorSceneManager).GetMethod("ClearSceneDirtiness",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            if (m != null) m.Invoke(null, new object[] { scene });
        }

        // ---- 畑のマテリアル --------------------------------------------------------

        /// <summary>
        /// 場面 8 の麦（株か畑の地）のマテリアルを村の置き場へ写す。朝靄の溜まりは切る
        /// </summary>
        static Material CropMat(string name, string drive)
        {
            var src = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Drive/" + drive + ".mat");
            var path = Materials + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (src == null)
            {
                Debug.LogWarning("場面 8 の麦のマテリアルが無い: " + drive + "。HalfAware/Build the drive で一度組む");
                return m != null ? m : Paint(name, new Color(0.62f, 0.50f, 0.24f), 0.04f);
            }
            if (m == null)
            {
                m = new Material(src) { name = name };
                AssetDatabase.CreateAsset(m, path);
            }
            m.shader = src.shader;
            m.CopyPropertiesFromMaterial(src);
            if (m.HasProperty("_MistDeep")) m.SetFloat("_MistDeep", 0f);
            EditorUtility.SetDirty(m);
            return m;
        }

        static Material WheatCrop() { return CropMat("VillageWheat", "Wheat"); }
        static Material FieldCrop() { return CropMat("VillageFieldCrop", "FieldCrop"); }

        // ---- 中（組む） ------------------------------------------------------------

        /// <summary>麦畑。(西, 東, 南, 北)</summary>
        static readonly Vector4[] WheatFields =
        {
            new Vector4(-131f, LaneWest - 0.5f, RoadHalf + 1.0f, 100f),
            new Vector4(LaneWest, PlotAWest - 0.6f, NorthEdge + 0.9f, 100f),
            new Vector4(PlotAWest + 0.6f, 20f, BackHedge + 0.8f, 100f),
            new Vector4(-131f, LaneWest - 0.5f, -98f, -RoadHalf - 1.0f),
            new Vector4(LaneWest, PlotCWest - 0.6f, -98f, -NorthEdge - 0.9f),
            new Vector4(PlotCWest, 40f, -98f, SouthHedge - 0.8f),
            new Vector4(PlotCEast + 0.6f, PlotDWest - 0.6f, SouthHedge - 0.4f, -NorthEdge - 0.9f),
        };

        /// <summary>牧草地。片割れの東と、家 D の東</summary>
        static readonly Vector4[] Pastures =
        {
            new Vector4(PlotEast + 0.6f, 69f, NorthEdge + 0.9f, BackHedge + 0.5f),
            new Vector4(20f, 69f, BackHedge + 0.5f, 100f),
            new Vector4(PlotDEast + 0.5f, 69f, SouthHedge, -NorthEdge - 0.9f),
            new Vector4(40f, 69f, -98f, SouthHedge),
        };

        /// <summary>畑を区切る生け垣の並木。(x0, z0, x1, z1)</summary>
        static readonly Vector4[] Hedgerows =
        {
            new Vector4(-131f, 72f, 69f, 72f), new Vector4(-131f, -62f, 69f, -62f),
            new Vector4(-100f, RoadHalf + 0.8f, -100f, 100f), new Vector4(-100f, -RoadHalf - 0.8f, -100f, -98f),
            new Vector4(PlotAWest, BackHedge, PlotAWest, 100f), new Vector4(20f, BackHedge, 20f, 100f),
            new Vector4(PlotDWest, SouthHedge, PlotDWest, -98f), new Vector4(40f, SouthHedge, 40f, -98f),
            new Vector4(PlotEast + 0.4f, BackHedge, 20f, BackHedge), new Vector4(PlotDEast, SouthHedge, 40f, SouthHedge),
        };

        /// <summary>
        /// 中の畑。麦畑の地と札、牧草地、生け垣の並木と木。遠い地面（輪の多角形）もここで敷く
        /// </summary>
        static void Fields(Transform parent, Banks b)
        {
            // 遠い地面。輪と同じ多角形で切った一枚の麦畑の地。組んだ畑はこの上に重ねる
            var crop = FieldCrop();
            FarBackdrop.Land(parent, VillageRing(true), "VillageFarLand", crop, Generated, true);

            var ground = new Bank { Texel = 0.12f, Rooted = true, RootY = 0f, RootHigh = 1f };
            foreach (var f in WheatFields) ground.FaceY(-0.03f, f.x, f.y, f.z, f.w, 1);
            NoShadow(Emit(parent, "VillageFieldGround", ground, crop, false));

            var pasture = new Bank { Texel = 0.2f };
            foreach (var f in Pastures) pasture.FaceY(-0.025f, f.x, f.y, f.z, f.w, 1);
            NoShadow(Emit(parent, "VillagePasture", pasture, VergeMat(), false));

            Wheat(parent);
            ChurchNear(b);

            foreach (var h in Hedgerows)
                FieldHedge(b, new Vector3(h.x, 0f, h.y), new Vector3(h.z, 0f, h.w), 2.1f, 1.3f, (int)(h.x * 3f + h.y));
            // 並木の木。生け垣に沿って間を不揃いに
            var n = 0;
            foreach (var h in Hedgerows)
            {
                var a = new Vector3(h.x, 0f, h.y);
                var c = new Vector3(h.z, 0f, h.w);
                var len = Vector3.Distance(a, c);
                for (var d = 12f + Hash(601, n) * 20f; d < len - 5f; d += 26f + Hash(603, n) * 30f)
                {
                    var at = Vector3.Lerp(a, c, d / len);
                    // 路地から見える近い所には立てない（家並みの見通しを塞がない）
                    if (Mathf.Abs(at.z) < 30f && at.x > -60f && at.x < 20f) { n++; continue; }
                    FieldTree(b, at, 0.8f + Hash(605, n) * 0.5f, n);
                    n++;
                }
            }
        }

        // ---- 村の教会（組む） --------------------------------------------------------

        /// <summary>
        /// 村の教会の塔の足元の芯。**村の東の外れの畑越し**（設計書 7 節）。路地の真ん中から 157 m、
        /// 片割れの家の前から 130 m、車の着く所から 199 m。書き割りの輪（半径 190 m）の内に置くので、板より手前に描かれる。
        /// 路地を東へ歩くと、片割れの家の屋根の左（北東）に塔と身廊の屋根が出る
        /// </summary>
        public static readonly Vector3 ChurchAt = new Vector3(110f, 0f, 70f);

        /// <summary>
        /// 村の教会。コッツウォルズの羊毛の教会の、胸壁と四隅の尖りを持つ四角い塔を西（村の側）に、
        /// その東に石版の屋根の身廊と、一段低い内陣。南に小さなポーチ。
        /// まわりに野石の墓地の塀と、濃いイチイの木と、墓石を少し。
        /// 足元は書き割りの地面の上に立つので、墓地の塀とイチイで根元を隠す
        /// </summary>
        static void ChurchNear(Banks b)
        {
            var t = ChurchAt;
            // 塔は 24 m（羊毛の教会の塔の高さ）。19 m では路地から生け垣の上に頭が少し出るだけだった
            const float tw = 6.8f;
            const float th = 24f;
            // 塔。胴と、隅の控え壁（二段）、途中の水切りの帯
            b.Stone.Box(t + Vector3.up * (th * 0.5f - 0.5f), new Vector3(tw, th + 1f, tw));
            foreach (var sx in new[] { -1f, 1f })
                foreach (var sz in new[] { -1f, 1f })
                {
                    b.Stone.Box(t + new Vector3(sx * (tw * 0.5f + 0.25f), 4f, sz * (tw * 0.5f - 0.6f)), new Vector3(0.5f, 8f, 1.0f));
                    b.Stone.Box(t + new Vector3(sx * (tw * 0.5f - 0.6f), 4f, sz * (tw * 0.5f + 0.25f)), new Vector3(1.0f, 8f, 0.5f));
                    b.Stone.Box(t + new Vector3(sx * (tw * 0.5f + 0.15f), 11f, sz * (tw * 0.5f - 0.6f)), new Vector3(0.3f, 6f, 0.8f));
                    b.Stone.Box(t + new Vector3(sx * (tw * 0.5f - 0.6f), 11f, sz * (tw * 0.5f + 0.15f)), new Vector3(0.8f, 6f, 0.3f));
                }
            foreach (var y in new[] { 8.2f, 16.2f })
                b.Dressed.Box(t + Vector3.up * y, new Vector3(tw + 0.24f, 0.22f, tw + 0.24f));
            // 鐘楼の窓（四方）と、西の戸口と窓
            foreach (var d in new[] { Vector3.forward, Vector3.back, Vector3.left, Vector3.right })
            {
                var at = t + d * (tw * 0.5f + 0.02f) + Vector3.up * 20.6f;
                b.Dark.Box(at, new Vector3(d.x != 0f ? 0.06f : 1.3f, 2.6f, d.z != 0f ? 0.06f : 1.3f));
                b.Dressed.Box(at + Vector3.up * 1.4f, new Vector3(d.x != 0f ? 0.12f : 1.6f, 0.2f, d.z != 0f ? 0.12f : 1.6f));
            }
            b.Dark.Box(t + new Vector3(-tw * 0.5f - 0.02f, 1.3f, 0f), new Vector3(0.06f, 2.6f, 1.5f));
            b.Dark.Box(t + new Vector3(-tw * 0.5f - 0.02f, 5.4f, 0f), new Vector3(0.06f, 2.8f, 1.1f));
            // 胸壁。四辺に帯を回し、上に凸の石を並べる。四隅に尖り
            const float top = th;
            b.Dressed.Box(t + Vector3.up * (top + 0.1f), new Vector3(tw + 0.4f, 0.2f, tw + 0.4f));
            for (var k = 0; k < 4; k++)
            {
                var rot = Quaternion.Euler(0f, k * 90f, 0f);
                var n = rot * Vector3.forward;
                var r = rot * Vector3.right;
                b.Stone.Box(t + n * (tw * 0.5f) + Vector3.up * (top + 0.55f), new Vector3(tw + 0.4f, 0.7f, 0.4f), rot);
                for (var i = 0; i < 4; i++)
                {
                    var u = -tw * 0.5f + tw * (i + 0.5f) / 4f;
                    b.Stone.Box(t + n * (tw * 0.5f) + r * u + Vector3.up * (top + 1.15f), new Vector3(0.7f, 0.5f, 0.4f), rot);
                }
            }
            foreach (var sx in new[] { -1f, 1f })
                foreach (var sz in new[] { -1f, 1f })
                {
                    var c = t + new Vector3(sx * tw * 0.5f, 0f, sz * tw * 0.5f);
                    b.Stone.Box(c + Vector3.up * (top + 1.3f), new Vector3(0.6f, 2.6f, 0.6f));
                    b.Dressed.Box(c + Vector3.up * (top + 2.9f), new Vector3(0.36f, 0.8f, 0.36f), Quaternion.Euler(0f, 45f, 0f));
                    b.Dressed.Box(c + Vector3.up * (top + 3.5f), new Vector3(0.14f, 0.6f, 0.14f));
                }
            // 身廊。塔の東に
            var nave = t + new Vector3(tw * 0.5f + 10f, 0f, 0f);
            const float nl = 20f;
            const float nw = 8.4f;
            ChurchBody(b, nave, nl, nw, 9f, 42f, false);
            for (var i = 0; i < 4; i++)
                foreach (var sz in new[] { -1f, 1f })
                    b.Dark.Box(nave + new Vector3(-nl * 0.5f + 3f + i * 4.6f, 4.2f, sz * (nw * 0.5f + 0.02f)), new Vector3(1.3f, 4.2f, 0.06f));
            // 内陣。身廊の東に一段低く
            var chancel = nave + new Vector3(nl * 0.5f + 4.5f, 0f, 0f);
            ChurchBody(b, chancel, 9f, 6.4f, 7f, 42f, false);
            b.Dark.Box(chancel + new Vector3(4.52f, 3.4f, 0f), new Vector3(0.06f, 3.6f, 2.2f));
            // 南のポーチ
            ChurchBody(b, nave + new Vector3(-nl * 0.5f + 5f, 0f, -nw * 0.5f - 1.6f), 3.2f, 3.2f, 3.2f, 45f, true);
            // 墓地の塀。身廊を囲む四角に、西（村の側）に口
            var x0 = t.x - tw * 0.5f - 9f;
            var x1 = chancel.x + 12f;
            var z0 = t.z - 16f;
            var z1 = t.z + 16f;
            var segs = new[]
            {
                new Vector4(x0, z0, x1, z0), new Vector4(x1, z0, x1, z1), new Vector4(x1, z1, x0, z1),
                new Vector4(x0, z1, x0, t.z + 2f), new Vector4(x0, t.z - 2f, x0, z0),
            };
            foreach (var sg in segs)
            {
                var a = new Vector3(sg.x, 0f, sg.y);
                var e = new Vector3(sg.z, 0f, sg.w);
                b.Stone.Box((a + e) * 0.5f + Vector3.up * 0.7f, new Vector3(0.6f, 1.4f, (e - a).magnitude), Quaternion.LookRotation((e - a).normalized, Vector3.up));
            }
            // イチイの木。墓地の隅と口の脇
            var yews = new[]
            {
                new Vector3(x0 + 3f, 0f, z0 + 3f), new Vector3(x0 + 3f, 0f, z1 - 3f), new Vector3(x0 + 2.5f, 0f, t.z + 4f),
                new Vector3(x1 - 4f, 0f, z1 - 4f), new Vector3(nave.x + 2f, 0f, z1 - 4f), new Vector3(x1 - 3f, 0f, z0 + 4f),
            };
            for (var i = 0; i < yews.Length; i++)
            {
                var y = yews[i];
                var s = 1.0f + Hash(951, i) * 0.5f;
                b.Bark.Box(y + Vector3.up * 1.2f, new Vector3(0.6f, 2.4f, 0.6f));
                Ball(b.Yew, y + Vector3.up * (3.6f * s), 2.4f * s);
                Ball(b.Yew, y + Vector3.up * (5.8f * s) + new Vector3(0.4f, 0f, -0.3f), 1.7f * s);
                Ball(b.Yew, y + Vector3.up * (2.4f * s) + new Vector3(-0.8f, 0f, 0.6f), 1.8f * s);
            }
            // 墓石。身廊の南と北に疎らに
            for (var i = 0; i < 14; i++)
            {
                var gx = nave.x - 8f + Hash(961, i) * 20f;
                var gz = (i % 2 == 0 ? -1f : 1f) * (nw * 0.5f + 3f + Hash(963, i) * 6f) + t.z;
                b.Dressed.Box(new Vector3(gx, 0.45f, gz), new Vector3(0.12f, 0.9f, 0.6f), Quaternion.Euler(0f, 0f, (Hash(965, i) - 0.5f) * 8f));
            }
        }

        /// <summary>
        /// 教会の棟一つ。芯・長さ（x）・幅（z）・軒の高さ・屋根の勾配。石の四方の壁と、石版の切妻の屋根と、妻の三角と十字。
        /// alongZ なら棟を z に沿わせる（南のポーチ）
        /// </summary>
        static void ChurchBody(Banks b, Vector3 c, float len, float wide, float eaves, float pitch, bool alongZ)
        {
            var lx = alongZ ? wide : len;
            var lz = alongZ ? len : wide;
            b.Stone.Box(c + Vector3.up * (eaves * 0.5f), new Vector3(lx, eaves, lz));
            var half = (alongZ ? lx : lz) * 0.5f;
            var rise = half * Mathf.Tan(pitch * Mathf.Deg2Rad);
            var slope = Mathf.Sqrt(half * half + rise * rise) + 0.3f;
            var run = alongZ ? lz : lx;
            foreach (var side in new[] { -1f, 1f })
            {
                // 斜面の中ほどを芯に、勾配だけ傾けた薄い板
                var q = alongZ ? Quaternion.Euler(0f, 0f, side * pitch) : Quaternion.Euler(side * pitch, 0f, 0f);
                var mid = alongZ
                    ? c + new Vector3(-side * half * 0.5f, eaves + rise * 0.5f + 0.1f, 0f)
                    : c + new Vector3(0f, eaves + rise * 0.5f + 0.1f, side * half * 0.5f);
                var size = alongZ ? new Vector3(slope, 0.16f, run + 0.3f) : new Vector3(run + 0.3f, 0.16f, slope);
                b.Slate.Box(mid, size, q);
            }
            foreach (var e in new[] { -1f, 1f })
            {
                var ec = alongZ ? c + new Vector3(0f, 0f, e * lz * 0.5f) : c + new Vector3(e * lx * 0.5f, 0f, 0f);
                var outward = alongZ ? new Vector3(0f, 0f, e) : new Vector3(e, 0f, 0f);
                var across = alongZ ? Vector3.right : Vector3.forward;
                Face(b.Stone, ec + Vector3.up * eaves - across * half, ec + Vector3.up * eaves + across * half,
                    ec + Vector3.up * (eaves + rise + 0.3f), ec + Vector3.up * (eaves + rise + 0.3f), outward);
                b.Dressed.Box(ec + Vector3.up * (eaves + rise + 0.8f), new Vector3(0.14f, 0.9f, 0.14f));
                b.Dressed.Box(ec + Vector3.up * (eaves + rise + 0.95f), alongZ ? new Vector3(0.6f, 0.14f, 0.14f) : new Vector3(0.14f, 0.14f, 0.6f));
            }
        }

        /// <summary>
        /// 畑の境の生け垣。<see cref="Hedge"/> と同じ作りで、頭の凸凹を 3.5 m ごとに粗くする（長いので三角を抑える）
        /// </summary>
        static void FieldHedge(Banks b, Vector3 from, Vector3 to, float high, float thick, int seed)
        {
            var run = to - from;
            var len = run.magnitude;
            if (len < 0.05f) return;
            var dir = run / len;
            var rot = Quaternion.LookRotation(dir, Vector3.up);
            b.Hedge.Box((from + to) * 0.5f + Vector3.up * (high * 0.5f), new Vector3(thick, high, len), rot);
            var n = Mathf.Max(1, Mathf.RoundToInt(len / 3.5f));
            for (var i = 0; i < n; i++)
            {
                var at = from + dir * (len * (i + 0.5f) / n);
                var bump = 0.15f + Hash(seed, i) * 0.45f;
                b.Hedge.Box(at + Vector3.up * (high + bump * 0.5f - 0.05f), new Vector3(thick * 0.8f, bump, len / n * (0.55f + Hash(seed + 1, i) * 0.4f)), rot);
            }
        }

        /// <summary>生け垣の並木の木（楢）。幹と、葉の玉を幾つか重ねた樹冠</summary>
        static void FieldTree(Banks b, Vector3 at, float scale, int seed)
        {
            Beam(b.Bark, at, at + Vector3.up * 3.2f * scale, 0.45f * scale, 0.42f * scale);
            for (var i = 0; i < 4; i++)
            {
                var o = new Vector3(Hash(seed * 7 + 1, i) - 0.5f, Hash(seed * 7 + 2, i) * 0.6f, Hash(seed * 7 + 3, i) - 0.5f) * 3.6f * scale;
                Ball(b.Hedge, at + Vector3.up * 5.4f * scale + o, (1.8f + Hash(seed * 7 + 4, i) * 0.9f) * scale);
            }
        }

        /// <summary>
        /// 麦の札。路地の線（x は車の着く所から東の端、z 0）からの隔たりで札の幅と間を変える。
        /// 近くは細かく（車の着く所の未舗装路の脇）、遠くは粗く。札は交差させて二枚一組、向きは群ごとに振る
        /// </summary>
        static void Wheat(Transform parent)
        {
            var wheat = new Bank { Texel = BuildDrive.WheatTexel, Rooted = true, CardLift = BuildDrive.WheatLift };
            var rnd = new System.Random(20260926);
            foreach (var f in WheatFields)
            {
                var z = f.z;
                while (z < f.w)
                {
                    var near = Mathf.Min(Mathf.Abs(z), 200f);
                    var step = Spacing(near);
                    var x = f.x + (float)rnd.NextDouble() * step;
                    while (x < f.y)
                    {
                        var along = x < LaneWest ? LaneWest - x : x > LaneEast ? x - LaneEast : 0f;
                        var d = Mathf.Sqrt(along * along + z * z);
                        var s = Spacing(d);
                        var wide = d < 25f ? 0.6f + 0.07f * d : 2.35f + 0.1f * (d - 25f);
                        var high = 0.72f + (float)rnd.NextDouble() * 0.46f;
                        var atZ = Mathf.Clamp(z + ((float)rnd.NextDouble() - 0.5f) * s * 0.7f, f.z + 0.2f, f.w - 0.2f);
                        var span = wide * (0.75f + (float)rnd.NextDouble() * 0.5f);
                        wheat.RootY = 0f;
                        wheat.RootHigh = high;
                        var turn = (float)rnd.NextDouble() * Mathf.PI;
                        var uOff = (float)rnd.NextDouble();
                        for (var q = 0; q < BuildDrive.WheatCross; q++)
                        {
                            var a = turn + q * Mathf.PI / BuildDrive.WheatCross;
                            var across = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * (span * 0.5f);
                            // 畑の縁（塀と生け垣）を越えて路地の側へはみ出さないよう、縁の近くは畑の向きに寄せる
                            var root = new Vector3(Mathf.Clamp(x, f.x + span * 0.5f, f.y - span * 0.5f), -0.05f, atZ);
                            wheat.Card(root, across, new Vector3(0f, high + 0.05f, 0f), uOff);
                        }
                        x += s;
                    }
                    z += step;
                }
            }
            var made = Emit(parent, "VillageWheat", wheat, WheatCrop(), false);
            NoShadow(made);
        }

        /// <summary>
        /// 麦の札の間。路地から 25 m までは細かく、その先は急に粗くする。
        /// 遠い畑は家と塀と生け垣の上に穂先の帯が覗くだけなので、粗くても面として繋がる
        /// </summary>
        static float Spacing(float d)
        {
            return d < 25f ? 0.5f + 0.08f * d : 2.5f + 0.14f * (d - 25f);
        }

        // ---- 遠く（撮って貼る） ------------------------------------------------------

        /// <summary>丘の高さ。中心からの隔たりが地面の縁（100 m）を越えてから持ち上がる</summary>
        static float HillY(float x, float z)
        {
            var dx = x - FarCentre.x;
            var dz = z - FarCentre.z;
            var r = Mathf.Sqrt(dx * dx + dz * dz);
            var theta = Mathf.Atan2(dz, dx);
            // 靄に沈まない近さ（400 m まで）で持ち上げる。遠くほど高くするとどれも靄の色一色になる
            var rise = SkyPaint.Smooth(104f, 420f, r);
            var swell = SkyPaint.Noise(new Vector3(Mathf.Cos(theta) * 2.2f, 0.4f, Mathf.Sin(theta) * 2.2f), 7);
            var roll = SkyPaint.Noise(new Vector3(x * 0.008f, 1.3f, z * 0.008f), 5);
            return rise * (6f + swell * 22f + roll * 8f) - 0.05f;
        }

        /// <summary>
        /// 書き割りに撮る物。輪の地面の縁より外にだけ置く。
        /// <list type="table">
        /// <item><term>ぐるり</term><description>麦畑と牧草地の継ぎはぎの丘。升を畑に見立て、升の境に生け垣</description></item>
        /// <item><term>尾根</term><description>雑木林の帯と、点々と木立</description></item>
        /// <item><term>西・南・北</term><description>遠くの農家と納屋</description></item>
        /// </list>
        /// 作った mesh とマテリアルは <paramref name="made"/> へ入れる。捨てるのは呼ぶ側
        /// </summary>
        static GameObject VillageTown(Transform place, List<Object> made)
        {
            var root = new GameObject("VillageTown");
            root.hideFlags = HideFlags.HideAndDontSave;
            root.transform.SetPositionAndRotation(place.position, place.rotation);
            var field = new Bank { Texel = 0.08f, Rooted = true, RootY = 0f, RootHigh = 1f };
            var grass = new Bank { Texel = 0.1f };
            var hedge = new Bank { Texel = 0.3f };
            var wood = new Bank { Texel = 0.3f };
            var stone = new Bank { Texel = 0.5f };
            var slate = new Bank { Texel = 0.5f };
            var dark = new Bank { Texel = 0.5f };

            // 丘の升。極座標で角を 96、隔たりを 30 段（外ほど粗く）に割る
            const int arcs = 96;
            var rings = new List<float>();
            for (var r = 99f; r < 1500f; r *= 1.11f) rings.Add(r);
            rings[0] = 99f;
            for (var k = 0; k + 1 < rings.Count; k++)
                for (var i = 0; i < arcs; i++)
                {
                    var a0 = Mathf.PI * 2f * i / arcs;
                    var a1 = Mathf.PI * 2f * (i + 1) / arcs;
                    System.Func<float, float, Vector3> at = (a, r) =>
                    {
                        var x = FarCentre.x + Mathf.Cos(a) * r;
                        var z = FarCentre.z + Mathf.Sin(a) * r;
                        return new Vector3(x, HillY(x, z), z);
                    };
                    var p00 = at(a0, rings[k]);
                    var p01 = at(a1, rings[k]);
                    var p11 = at(a1, rings[k + 1]);
                    var p10 = at(a0, rings[k + 1]);
                    // 升を畑に見立てる。角 4 升・隔たり 3 段ごとの区画で、麦か牧草を決める
                    var plotHash = Hash(701 + k / 3, i / 4);
                    var bank = plotHash < 0.62f ? field : grass;
                    // 上から見て右回りが表（FaceY と同じ）。角の増える向きは左回りなので、内→角の先→外→角の元と渡す
                    bank.Patch(p00, p01, p11, p10,
                        new Vector2(p00.x, p00.z) * bank.Texel, new Vector2(p01.x, p01.z) * bank.Texel,
                        new Vector2(p11.x, p11.z) * bank.Texel, new Vector2(p10.x, p10.z) * bank.Texel);
                    // 区画の境に生け垣。輪の地面の縁のすぐ外には立てない（地面の縁の線の上に黒い帯と縦の棒が並んだ）
                    if (k % 3 == 0 && rings[k] > 165f && rings[k] < 700f)
                        HedgeLine(hedge, p00, p01, 1.8f + rings[k] * 0.003f);
                    if (i % 4 == 0 && rings[k] > 165f && rings[k] < 700f)
                        HedgeLine(hedge, p00, p10, 1.8f + rings[k] * 0.003f);
                    // 木立と雑木林。尾根の高い所に寄せ、靄に沈む遠くには置かない
                    var h = Hash(709 + k, i);
                    if (rings[k] > 125f && rings[k] < 520f && h < 0.05f + (p00.y > 16f ? 0.22f : 0f))
                        Copse(wood, Vector3.Lerp(p00, p11, 0.5f), 3.2f + rings[k] * 0.006f, 3 + (int)(h * 40f) % 4, k * 131 + i);
                }

            // 村の教会は書き割りに撮らない。村の東の外れに組んである（ChurchNear）
            // 遠くの農家と納屋
            Farm(stone, slate, dark, wood, new Vector3(-236f, 0f, 70f), 20f);
            Farm(stone, slate, dark, wood, new Vector3(-150f, 0f, -190f), 70f);
            Farm(stone, slate, dark, wood, new Vector3(30f, 0f, 230f), 5f);
            Farm(stone, slate, dark, wood, new Vector3(110f, 0f, -190f), 110f);
            Farm(stone, slate, dark, wood, new Vector3(-90f, 0f, 250f), 80f);

            var mats = new List<KeyValuePair<Bank, Material>>
            {
                new KeyValuePair<Bank, Material>(field, FieldCrop()),
                new KeyValuePair<Bank, Material>(grass, VergeMat()),
                new KeyValuePair<Bank, Material>(hedge, Pictured("VillageHedge", "VillageHedge.png", Color.white, 0.04f)),
                new KeyValuePair<Bank, Material>(wood, TownMat(made, new Color(0.20f, 0.27f, 0.13f))),
                new KeyValuePair<Bank, Material>(stone, StoneMat()),
                new KeyValuePair<Bank, Material>(slate, Pictured("VillageSlate", "VillageSlate.png", Color.white, 0.10f)),
                new KeyValuePair<Bank, Material>(dark, DarkMat()),
            };
            var n = 0;
            foreach (var pair in mats)
            {
                var mesh = TownMesh(pair.Key, "VillageTown" + n++);
                if (mesh == null) continue;
                made.Add(mesh);
                var go = new GameObject(mesh.name);
                go.hideFlags = HideFlags.HideAndDontSave;
                go.transform.SetParent(root.transform, false);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                go.AddComponent<MeshRenderer>().sharedMaterial = pair.Value;
            }
            return root;
        }

        /// <summary>撮るためだけの mesh。アセットにはしない（捨てるのは呼ぶ側）</summary>
        static Mesh TownMesh(Bank bank, string name)
        {
            if (bank.Count == 0) return null;
            // Bank は焼くときにアセットへ書くので、書かずに中身だけを取り出す手として一時の置き場へ吐いてから読む
            var tmp = new GameObject("tmp");
            tmp.hideFlags = HideFlags.HideAndDontSave;
            try
            {
                var go = bank.Emit(tmp.transform, name, null, false, "Assets/Models/generated/village/_town_");
                var src = go.GetComponent<MeshFilter>().sharedMesh;
                var copy = Object.Instantiate(src);
                copy.name = name;
                copy.hideFlags = HideFlags.HideAndDontSave;
                AssetDatabase.DeleteAsset(AssetDatabase.GetAssetPath(src));
                return copy;
            }
            finally
            {
                Object.DestroyImmediate(tmp);
            }
        }

        static Material TownMat(List<Object> made, Color c)
        {
            var m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            m.hideFlags = HideFlags.HideAndDontSave;
            m.SetColor("_BaseColor", c);
            m.SetFloat("_Smoothness", 0.02f);
            made.Add(m);
            return m;
        }

        /// <summary>丘の上の生け垣の線。a から b へ、地面に沿って低い箱を並べる</summary>
        static void HedgeLine(Bank b, Vector3 a, Vector3 c, float high)
        {
            var len = Vector3.Distance(a, c);
            if (len < 0.5f) return;
            var mid = (a + c) * 0.5f;
            b.Box(mid + Vector3.up * (high * 0.5f - 0.3f), new Vector3(high * 0.8f, high, len), Quaternion.LookRotation(c - a, Vector3.up));
        }

        /// <summary>
        /// 木立。丸い樹冠を幾つか寄せる。樹冠は向きを変えた箱を三つ重ねて角を落とす（<see cref="Ball"/> と同じ作り）。
        /// 大きな箱を積んだだけでは、丘の上に建物の並ぶ街の空の線に見えた
        /// </summary>
        static void Copse(Bank b, Vector3 at, float size, int count, int seed)
        {
            for (var i = 0; i < count; i++)
            {
                var o = new Vector3(Hash(seed, i * 3) - 0.5f, 0f, Hash(seed, i * 3 + 1) - 0.5f) * size * 2.6f;
                var r = size * (0.6f + Hash(seed, i * 3 + 2) * 0.5f);
                var p = at + o;
                p.y = HillY(p.x, p.z) + r * 1.1f;
                Ball(b, p, r);
                Ball(b, p + new Vector3(r * 0.5f, -r * 0.35f, r * 0.3f), r * 0.75f);
                b.Box(new Vector3(p.x, p.y - r * 0.9f, p.z), new Vector3(r * 0.25f, r * 0.9f, r * 0.25f));
            }
        }

        /// <summary>遠くの農家。二階建ての石の母屋と、長い納屋。yaw で向きを振る</summary>
        static void Farm(Bank stone, Bank slate, Bank dark, Bank wood, Vector3 at, float yaw)
        {
            at.y = HillY(at.x, at.z);
            var rot = Quaternion.Euler(0f, yaw, 0f);
            stone.Box(at + rot * new Vector3(0f, 3.2f, 0f), new Vector3(11f, 6.4f, 6f), rot);
            foreach (var side in new[] { -1f, 1f })
                slate.Box(at + rot * new Vector3(0f, 8.2f, side * 1.6f), new Vector3(11.6f, 0.3f, 4.4f), rot * Quaternion.Euler(side * -45f, 0f, 0f));
            stone.Box(at + rot * new Vector3(-4.8f, 10f, 0f), new Vector3(0.9f, 2.6f, 0.8f), rot);
            var barn = at + rot * new Vector3(9f, 0f, 8f);
            stone.Box(barn + Vector3.up * 3f, new Vector3(16f, 6f, 7f), rot);
            foreach (var side in new[] { -1f, 1f })
                slate.Box(barn + rot * new Vector3(0f, 7.3f, side * 1.9f), new Vector3(16.6f, 0.3f, 4.8f), rot * Quaternion.Euler(side * -40f, 0f, 0f));
            dark.Box(barn + rot * new Vector3(0f, 2.2f, 3.52f), new Vector3(4f, 4.4f, 0.05f), rot);
            Copse(wood, at + rot * new Vector3(-8f, 0f, -6f), 4f, 3, (int)(at.x * 7f));
        }
    }
}
