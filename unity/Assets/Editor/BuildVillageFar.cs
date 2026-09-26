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
    /// 村の教会は書き割りに撮らず、路地の東の突き当たり、歩ける所を囲う見えない壁のすぐ外に組む（<see cref="ChurchNear"/>、設計書 7 節）。
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

            // 並木の楢と墓地のイチイの樹冠の札を溜める。焼くのは周りの庭の木と一緒に（BuildVillageYards の FloraLaneTrees）
            fieldCrowns = FloraBank();
            fieldCrowns.CardLift = 2.0f;

            var ground = new Bank { Texel = 0.12f, Rooted = true, RootY = 0f, RootHigh = 1f };
            foreach (var f in WheatFields) ground.FaceY(-0.03f, f.x, f.y, f.z, f.w, 1);
            NoShadow(Emit(parent, "VillageFieldGround", ground, crop, false));

            var pasture = new Bank { Texel = 0.2f };
            foreach (var f in Pastures) pasture.FaceY(-0.025f, f.x, f.y, f.z, f.w, 1);
            // 教会の墓地の上げた芝。塀の内に
            pasture.FaceY(ChurchAt.y - 0.01f, ChurchYardWest + 0.28f, ChurchYardEast - 0.28f, ChurchYardSouth + 0.28f, ChurchYardNorth - 0.28f, 1);
            NoShadow(Emit(parent, "VillagePasture", pasture, VergeMat(), false));

            Wheat(parent);
            ChurchNear(b);

            foreach (var h in Hedgerows)
                FieldHedge(b, new Vector3(h.x, 0f, h.y), new Vector3(h.z, 0f, h.w), 2.1f, 1.3f, (int)(h.x * 3f + h.y));
            // 並木の木。生け垣に沿って間を不揃いに。樹冠は花と葉のアトラスの札（FieldTree）
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
            // 焼くのは周りの庭の木と一緒に（BuildVillageYards の FloraLaneTrees）。一枚にまとめて描く回数を増やさない
        }

        /// <summary>並木の樹冠の札を溜める入れ物。Fields で溜め、周りの庭の木と一緒に焼いたら捨てる</summary>
        static Bank fieldCrowns;

        // ---- 村の教会（組む） --------------------------------------------------------

        /// <summary>
        /// 教会の墓地の西の塀。**歩ける所を囲う見えない壁（路地の東の端 <see cref="LaneEast"/>）のすぐ外**
        /// （設計書 7 節）。路地はそのまま墓地の屋根付きの門（lychgate）まで続き、門の奥に塔が立つ。
        /// 路地から近づけるのは見えない壁まで（門まで 10 m、塔まで 18 m）
        /// </summary>
        public const float ChurchYardWest = LaneEast + 10f;
        const float ChurchYardEast = 67f;
        const float ChurchYardSouth = -9f;
        const float ChurchYardNorth = 34f;
        /// <summary>
        /// 墓地の地面の高さ。路地より 1 m 上げる（イギリスの村の墓地によくある、塀で土を留めた高い墓地）。
        /// 0.6 m では、路地の目の高さから西の塀の向こうの墓石がまだ塀の笠石にかかって見えなかった
        /// </summary>
        const float YardRise = 1.0f;

        /// <summary>
        /// 塔の足元の芯。路地の芯より 24 m 北に寄せる。教会は東西に長く路地も東西に走るので、路地の真正面や
        /// 5・12・18 m 寄せた所では、路地の途中から見ると身廊が塔の真後ろに隠れた。24 m 寄せると、路地の途中からも
        /// 片割れの家の前からも、塔の右に身廊の南の壁と屋根と南のポーチが並び、手前（南）の芝に墓石が並ぶ。
        /// 墓地の東の塀は輪の地面の縁（x 69）の内に収める
        /// </summary>
        public static readonly Vector3 ChurchAt = new Vector3(ChurchYardWest + 9.5f, YardRise, 24f);

        /// <summary>
        /// 村の教会。コッツウォルズの羊毛の教会の、胸壁と四隅の尖りを持つ四角い塔を西（村の側、路地の突き当たり）に、
        /// その東に石版の屋根の身廊と、一段低い内陣。南にポーチ。
        ///
        /// **近くで見られる細かさにする。** 路地から 10〜30 m で見るので、尖頭アーチの窓（<see cref="Lancet"/>）、
        /// 身廊の控え壁、妻の笠石と十字、塔の時計と鐘楼の二連の窓と風見、墓地の塀の縦の笠石（路地の側の西の塀だけ）を置く。
        /// 重さを抑えるため、路地から見えない東と北の塀の頭は一本の笠石にし、窓の格子（トレーサリー）は作らない。
        /// 墓地には屋根付きの門、敷石の小路、墓石（立ち石・十字・台の墓）、イチイ
        /// </summary>
        static void ChurchNear(Banks b)
        {
            var t = ChurchAt;
            const float tw = 6.8f;
            const float th = 24f;
            var hw = tw * 0.5f;
            // 塔。胴と、隅の控え壁（二段）、途中の水切りの帯
            b.Stone.Box(t + Vector3.up * (th * 0.5f - 0.5f - t.y * 0.5f), new Vector3(tw, th + 1f + t.y, tw));
            foreach (var sx in new[] { -1f, 1f })
                foreach (var sz in new[] { -1f, 1f })
                {
                    b.Stone.Box(t + new Vector3(sx * (hw + 0.3f), 4.5f, sz * (hw - 0.6f)), new Vector3(0.6f, 9f, 1.0f));
                    b.Stone.Box(t + new Vector3(sx * (hw - 0.6f), 4.5f, sz * (hw + 0.3f)), new Vector3(1.0f, 9f, 0.6f));
                    b.Stone.Box(t + new Vector3(sx * (hw + 0.15f), 12.5f, sz * (hw - 0.6f)), new Vector3(0.3f, 7f, 0.8f));
                    b.Stone.Box(t + new Vector3(sx * (hw - 0.6f), 12.5f, sz * (hw + 0.15f)), new Vector3(0.8f, 7f, 0.3f));
                    // 控え壁の段の水切り
                    b.Dressed.Box(t + new Vector3(sx * (hw + 0.3f), 9.05f, sz * (hw - 0.6f)), new Vector3(0.66f, 0.12f, 1.06f));
                    b.Dressed.Box(t + new Vector3(sx * (hw - 0.6f), 9.05f, sz * (hw + 0.3f)), new Vector3(1.06f, 0.12f, 0.66f));
                }
            b.Dressed.Box(t + Vector3.up * 0.25f, new Vector3(tw + 0.3f, 0.5f, tw + 0.3f));
            foreach (var y in new[] { 9.2f, 16.6f })
                b.Dressed.Box(t + Vector3.up * y, new Vector3(tw + 0.24f, 0.22f, tw + 0.24f));
            // 西の戸口と、その上の大きな窓、時計。鐘楼は四方に二連の窓
            var west = new Vector3(-1f, 0f, 0f);
            Lancet(b, t + west * hw + Vector3.up * 0.05f, west, 1.5f, 2.9f, true);
            Lancet(b, t + west * hw + Vector3.up * 4.6f, west, 1.6f, 3.6f, false);
            var dial = t + west * (hw + 0.06f) + Vector3.up * 14.6f;
            var face = Quaternion.LookRotation(west, Vector3.up);
            Tint(b.Swatch, dial, new Vector3(1.5f, 1.5f, 0.06f), face, SwNavy);
            Tint(b.Swatch, dial, new Vector3(1.5f, 1.5f, 0.06f), face * Quaternion.Euler(0f, 0f, 45f), SwNavy);
            Tint(b.Swatch, dial + west * 0.04f + Vector3.up * 0.28f, new Vector3(0.08f, 0.56f, 0.03f), face, SwGold);
            Tint(b.Swatch, dial + west * 0.04f + new Vector3(0f, -0.08f, 0.2f), new Vector3(0.07f, 0.44f, 0.03f), face * Quaternion.Euler(0f, 0f, 60f), SwGold);
            foreach (var d in new[] { Vector3.forward, Vector3.back, Vector3.left, Vector3.right })
            {
                var side = Vector3.Cross(Vector3.up, d);
                foreach (var s in new[] { -0.55f, 0.55f })
                    Lancet(b, t + d * hw + side * s + Vector3.up * 19.2f, d, 0.8f, 2.8f, false);
            }
            // 胸壁。四辺に帯を回し、上に凸の石を並べる。四隅に尖り
            const float top = th;
            b.Dressed.Box(t + Vector3.up * (top + 0.1f), new Vector3(tw + 0.4f, 0.2f, tw + 0.4f));
            for (var k = 0; k < 4; k++)
            {
                var rot = Quaternion.Euler(0f, k * 90f, 0f);
                var n = rot * Vector3.forward;
                var r = rot * Vector3.right;
                b.Stone.Box(t + n * hw + Vector3.up * (top + 0.55f), new Vector3(tw + 0.4f, 0.7f, 0.4f), rot);
                for (var i = 0; i < 5; i++)
                {
                    var u = -hw + tw * (i + 0.5f) / 5f;
                    b.Stone.Box(t + n * hw + r * u + Vector3.up * (top + 1.12f), new Vector3(0.62f, 0.45f, 0.4f), rot);
                    b.Dressed.Box(t + n * hw + r * u + Vector3.up * (top + 1.38f), new Vector3(0.7f, 0.08f, 0.46f), rot);
                }
            }
            foreach (var sx in new[] { -1f, 1f })
                foreach (var sz in new[] { -1f, 1f })
                {
                    var c = t + new Vector3(sx * hw, 0f, sz * hw);
                    b.Stone.Box(c + Vector3.up * (top + 1.3f), new Vector3(0.6f, 2.6f, 0.6f));
                    b.Dressed.Box(c + Vector3.up * (top + 2.9f), new Vector3(0.36f, 0.8f, 0.36f), Quaternion.Euler(0f, 45f, 0f));
                    b.Dressed.Box(c + Vector3.up * (top + 3.5f), new Vector3(0.14f, 0.6f, 0.14f));
                }
            // 風見。北東の尖りの上に
            var vane = t + new Vector3(hw, top + 3.8f, hw);
            b.Iron.Box(vane + Vector3.up * 0.5f, new Vector3(0.04f, 1.0f, 0.04f));
            b.Iron.Box(vane + Vector3.up * 0.8f, new Vector3(0.7f, 0.18f, 0.03f), Quaternion.Euler(0f, 30f, 0f));

            // 身廊。塔の東に。控え壁を窓の間に
            const float nl = 16f;
            const float nw = 8.4f;
            const float ne = 8.6f;
            var nave = t + new Vector3(hw + nl * 0.5f, 0f, 0f);
            ChurchBody(b, nave, nl, nw, ne, 42f, false);
            for (var i = 0; i < 3; i++)
                foreach (var sz in new[] { -1f, 1f })
                {
                    var n = new Vector3(0f, 0f, sz);
                    var x = nave.x - nl * 0.5f + 3.2f + i * 4.8f;
                    if (sz < 0f && i == 0) continue;
                    Lancet(b, new Vector3(x, t.y + 2.4f, nave.z + sz * nw * 0.5f), n, 1.1f, 3.4f, false);
                }
            for (var i = 0; i < 4; i++)
                foreach (var sz in new[] { -1f, 1f })
                {
                    var x = nave.x - nl * 0.5f + 0.8f + i * 4.8f;
                    b.Stone.Box(new Vector3(x, t.y + 2.4f, nave.z + sz * (nw * 0.5f + 0.35f)), new Vector3(0.7f, 4.8f, 0.7f));
                    b.Dressed.Box(new Vector3(x, t.y + 4.85f, nave.z + sz * (nw * 0.5f + 0.25f)), new Vector3(0.72f, 0.14f, 0.5f), Quaternion.Euler(-sz * 30f, 0f, 0f));
                }
            // 内陣。身廊の東に一段低く。東の大きな窓
            const float cl = 7f;
            var chancel = nave + new Vector3(nl * 0.5f + cl * 0.5f, 0f, 0f);
            ChurchBody(b, chancel, cl, 6.4f, 7f, 42f, false);
            Lancet(b, chancel + new Vector3(cl * 0.5f, 2.2f, 0f), Vector3.right, 2.0f, 3.8f, false);
            foreach (var sz in new[] { -1f, 1f })
                Lancet(b, new Vector3(chancel.x, t.y + 2.2f, chancel.z + sz * 3.2f), new Vector3(0f, 0f, sz), 0.9f, 2.6f, false);
            // 南のポーチ。尖頭アーチの口
            var porch = nave + new Vector3(-nl * 0.5f + 3.6f, 0f, -nw * 0.5f - 1.7f);
            ChurchBody(b, porch, 3.4f, 3.2f, 3.2f, 45f, true);
            Lancet(b, porch + new Vector3(0f, 0f, -1.7f), Vector3.back, 1.5f, 2.5f, true);

            // 墓地の塀。西（路地の側）は路地から 1.25 m・中の芝から 0.25 m、ほかは路地の高さから 1.5 m。
            // 路地の側（西）は縦の笠石、ほかは一本の笠石。西の塀の真ん中（路地の芯）に屋根付きの門
            var x0 = ChurchYardWest;
            var x1 = ChurchYardEast;
            var z0 = ChurchYardSouth;
            var z1 = ChurchYardNorth;
            const float gate = 1.5f;
            foreach (var seg in new[] { new Vector2(z0, -gate), new Vector2(gate, z1) })
            {
                var a = new Vector3(x0, 0f, seg.x);
                var e = new Vector3(x0, 0f, seg.y);
                // 路地の側は低く（中の芝から 0.25 m）。高くすると、路地から墓石が塀に隠れる
                b.Stone.Box((a + e) * 0.5f + Vector3.up * 0.62f, new Vector3(0.6f, 1.24f, e.z - a.z));
                Coping(b, a + Vector3.up * 1.24f, e + Vector3.up * 1.24f, 0.5f, 0.36f);
            }
            foreach (var seg in new[] { new Vector4(x0, z0, x1, z0), new Vector4(x1, z0, x1, z1), new Vector4(x1, z1, x0, z1) })
            {
                var a = new Vector3(seg.x, 0f, seg.y);
                var e = new Vector3(seg.z, 0f, seg.w);
                var rot = Quaternion.LookRotation((e - a).normalized, Vector3.up);
                b.Stone.Box((a + e) * 0.5f + Vector3.up * 0.75f, new Vector3(0.6f, 1.5f, (e - a).magnitude), rot);
                b.Dressed.Box((a + e) * 0.5f + Vector3.up * 1.55f, new Vector3(0.7f, 0.12f, (e - a).magnitude + 0.1f), rot);
            }
            Lychgate(b, new Vector3(x0, 0f, 0f), gate);
            // 門の内の上り段と、敷石の小路。門から東へ入り、北へ折れて塔の戸口へ、南へ折れて南のポーチへ
            for (var k = 1; k <= 3; k++)
                b.Flag.Box(new Vector3(x0 + 0.1f + k * 0.55f, YardRise * k / 6f, 0f), new Vector3(0.55f, YardRise * k / 3f, 2.6f));
            var py = t.y + 0.02f;
            var ps = porch.z - 1.7f;
            b.Flag.FaceY(py, x0 + 1.8f, x0 + 4.2f, -0.65f, 0.65f, 1);
            b.Flag.FaceY(py, x0 + 2.8f, x0 + 4.2f, -0.65f, t.z + 0.7f, 1);
            b.Flag.FaceY(py, x0 + 4.2f, t.x - hw, t.z - 0.6f, t.z + 0.7f, 1);
            b.Flag.FaceY(py, x0 + 4.2f, porch.x + 0.7f, ps - 1.5f, ps - 0.2f, 1);
            b.Flag.FaceY(py, porch.x - 0.7f, porch.x + 0.7f, ps - 0.2f, ps + 0.1f, 1);

            // イチイの木。墓地の隅と、門の内の両脇
            var yews = new[]
            {
                new Vector3(x0 + 1.9f, t.y, -5.2f), new Vector3(x0 + 1.9f, t.y, 21.5f), new Vector3(x0 + 5.5f, t.y, z1 - 2.6f),
                new Vector3(x1 - 3.5f, t.y, z1 - 3.5f), new Vector3(x1 - 3f, t.y, z0 + 3f),
            };
            for (var i = 0; i < yews.Length; i++)
            {
                var y = yews[i];
                var s = 0.85f + Hash(951, i) * 0.4f;
                b.Bark.Box(y + Vector3.up * 1.0f, new Vector3(0.5f, 2.0f, 0.5f));
                // 樹冠は札（アトラスの Yew）を三枚交差させる。箱を重ねた玉では、裏庭から塀の向こうに暗い緑の箱が見えた
                Crown(Kind.Yew, y + Vector3.up * (0.9f * s), 5.6f * s, 4.6f * s, Hash(953, i) * 180f);
            }
            // 墓石。南（路地の側から見える）と北の芝に列をなして。立ち石を主に、十字と台の墓を混ぜる
            var n3 = 0;
            var g0 = t.y;
            foreach (var zRow in new[] { z0 + 2f, -3.8f, -0.6f, 2.6f, 5.8f, 9.0f, 12.2f, z1 - 2.2f })
                for (var i = 0; i < 5; i++)
                {
                    var gx = x0 + 5f + i * 5.6f + (Hash(961, n3) - 0.5f) * 1.4f;
                    var gz = zRow + (Hash(963, n3) - 0.5f) * 0.8f;
                    n3++;
                    if (gx > x1 - 2f || (Mathf.Abs(gz - t.z) < 5f && gx < chancel.x + 5f)) continue;
                    // 疎らにする。四つに一つは抜く
                    if (Hash(973, n3) < 0.25f) continue;
                    var tilt = Quaternion.Euler(0f, (Hash(967, n3) - 0.5f) * 10f, (Hash(965, n3) - 0.5f) * 8f);
                    var kind = Hash(969, n3);
                    if (kind < 0.12f)
                    {
                        b.Dressed.Box(new Vector3(gx, g0 + 0.4f, gz), new Vector3(1.9f, 0.8f, 0.9f));
                        b.Dressed.Box(new Vector3(gx, g0 + 0.84f, gz), new Vector3(2.0f, 0.08f, 1.0f));
                    }
                    else if (kind < 0.28f)
                    {
                        b.Dressed.Box(new Vector3(gx, g0 + 0.15f, gz), new Vector3(0.5f, 0.3f, 0.5f));
                        b.Dressed.Box(new Vector3(gx, g0 + 0.85f, gz), new Vector3(0.12f, 1.2f, 0.12f), tilt);
                        b.Dressed.Box(new Vector3(gx, g0 + 1.15f, gz), new Vector3(0.12f, 0.12f, 0.6f), tilt);
                    }
                    else
                    {
                        var high = 0.8f + Hash(971, n3) * 0.5f;
                        b.Dressed.Box(new Vector3(gx, g0 + high * 0.5f, gz), new Vector3(0.14f, high, 0.7f), tilt);
                        b.Dressed.Box(new Vector3(gx, g0 + high, gz), new Vector3(0.14f, 0.14f, 0.5f), tilt * Quaternion.Euler(45f, 0f, 0f));
                    }
                }
        }

        /// <summary>
        /// 尖頭アーチの窓（または戸口）。foot は窓の下の辺の芯（壁の外の面の上）、normal は壁の外の向き。
        /// 暗い奥の面と尖りの三角、石の枠（両脇・窓台・尖りの二本の斜めの石と要石）、幅のある窓は真ん中に桟。
        /// door なら窓台を置かず、足元から立てる
        /// </summary>
        static void Lancet(Banks b, Vector3 foot, Vector3 normal, float wide, float high, bool door)
        {
            var n = normal.normalized;
            var r = Vector3.Cross(Vector3.up, n).normalized;
            var rot = Quaternion.LookRotation(n, Vector3.up);
            var spring = high - wide * 0.55f;
            var inset = foot + n * 0.03f;
            // 奥の暗がり。四角と尖りの三角
            var c = inset + Vector3.up * (spring * 0.5f);
            b.Dark.Box(c, new Vector3(wide, spring, 0.03f), rot);
            var p0 = inset - r * (wide * 0.5f) + Vector3.up * spring;
            var p1 = inset + r * (wide * 0.5f) + Vector3.up * spring;
            var apex = inset + Vector3.up * high;
            Face(b.Dark, p0 + n * 0.015f, p1 + n * 0.015f, apex + n * 0.015f, apex + n * 0.015f, n);
            // 石の枠
            const float band = 0.16f;
            var o = n * 0.06f;
            foreach (var s in new[] { -1f, 1f })
                b.Dressed.Box(inset + o + r * (s * (wide * 0.5f + band * 0.5f)) + Vector3.up * (spring * 0.5f), new Vector3(band, spring, 0.14f), rot);
            var q0 = p0 - r * (band * 0.5f) + o;
            var q1 = p1 + r * (band * 0.5f) + o;
            var qa = apex + Vector3.up * (band * 0.7f) + o;
            Beam(b.Dressed, q0, qa, band, 0.14f);
            Beam(b.Dressed, q1, qa, band, 0.14f);
            if (!door)
                b.Dressed.Box(inset + o + Vector3.up * -0.06f, new Vector3(wide + band * 2f + 0.1f, 0.12f, 0.2f), rot);
            if (!door && wide > 1.0f)
                b.Dressed.Box(inset + o * 0.5f + Vector3.up * (spring * 0.5f + 0.2f), new Vector3(0.1f, spring + 0.4f, 0.1f), rot);
            if (door)
            {
                // 戸の板。暗がりの少し手前に、縦の板目の代わりに黒い鉄の帯を二本
                b.Boards.Box(inset + n * 0.01f + Vector3.up * (spring * 0.5f), new Vector3(wide - 0.08f, spring - 0.02f, 0.04f), rot);
                foreach (var y in new[] { 0.5f, spring - 0.4f })
                    b.Iron.Box(inset + n * 0.04f + Vector3.up * y, new Vector3(wide - 0.2f, 0.06f, 0.02f), rot);
            }
        }

        /// <summary>
        /// 墓地の屋根付きの門（lychgate）。路地の突き当たりの西の塀の口に、樫の柱四本と梁、石版の切妻の屋根。
        /// 屋根の棟は路地を横切る向き（z）で、斜面を路地へ向ける。門の戸は閉じてある（入れない）
        /// </summary>
        static void Lychgate(Banks b, Vector3 at, float half)
        {
            const float depth = 1.2f;
            const float post = 2.3f;
            var w = half + 0.35f;
            foreach (var sx in new[] { -1f, 1f })
                foreach (var sz in new[] { -1f, 1f })
                {
                    var p = at + new Vector3(sx * depth, 0f, sz * w);
                    b.Stone.Box(p + Vector3.up * 0.2f, new Vector3(0.4f, 0.4f, 0.4f));
                    b.Boards.Box(p + Vector3.up * (0.4f + post * 0.5f), new Vector3(0.2f, post, 0.2f));
                }
            foreach (var sx in new[] { -1f, 1f })
                b.Boards.Box(at + new Vector3(sx * depth, post + 0.45f, 0f), new Vector3(0.22f, 0.22f, w * 2f + 0.5f));
            foreach (var sz in new[] { -1f, 1f })
                b.Boards.Box(at + new Vector3(0f, post + 0.3f, sz * w), new Vector3(depth * 2f + 0.3f, 0.18f, 0.18f));
            // 屋根。棟は z に沿い、斜面は東西
            const float pitch = 48f;
            var run = depth + 0.5f;
            var rise = run * Mathf.Tan(pitch * Mathf.Deg2Rad);
            var slope = run / Mathf.Cos(pitch * Mathf.Deg2Rad) + 0.1f;
            foreach (var side in new[] { -1f, 1f })
            {
                var mid = at + new Vector3(side * run * 0.5f, post + 0.58f + rise * 0.5f, 0f);
                b.Slate.Box(mid, new Vector3(slope, 0.12f, w * 2f + 0.9f), Quaternion.Euler(0f, 0f, -side * pitch));
            }
            b.Dressed.Box(at + Vector3.up * (post + 0.62f + rise), new Vector3(0.24f, 0.14f, w * 2f + 0.95f));
            foreach (var sz in new[] { -1f, 1f })
            {
                var e = at + new Vector3(0f, post + 0.56f, sz * (w + 0.45f));
                Face(b.Boards, e + new Vector3(-run, 0f, 0f), e + new Vector3(run, 0f, 0f), e + Vector3.up * rise, e + Vector3.up * rise, new Vector3(0f, 0f, sz));
            }
            // 閉じた二枚の戸。横の桟と斜めの筋交い
            foreach (var sz in new[] { -1f, 1f })
            {
                var c = at + new Vector3(0f, 0.75f, sz * half * 0.5f);
                b.Boards.Box(c, new Vector3(0.06f, 1.3f, half - 0.04f));
                b.Boards.Box(c + new Vector3(-0.05f, 0f, 0f), new Vector3(0.04f, 0.1f, half - 0.04f), Quaternion.Euler(-sz * 50f, 0f, 0f));
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
            // 底は地面と生け垣の頭に隠れて見えないので張らない（三角を抑える）
            b.Hedge.BoxOpenBottom((from + to) * 0.5f + Vector3.up * (high * 0.5f), new Vector3(thick, high, len), rot);
            var n = Mathf.Max(1, Mathf.RoundToInt(len / 3.5f));
            for (var i = 0; i < n; i++)
            {
                var at = from + dir * (len * (i + 0.5f) / n);
                var bump = 0.15f + Hash(seed, i) * 0.45f;
                b.Hedge.BoxOpenBottom(at + Vector3.up * (high + bump * 0.5f - 0.05f), new Vector3(thick * 0.8f, bump, len / n * (0.55f + Hash(seed + 1, i) * 0.4f)), rot);
            }
        }

        /// <summary>
        /// 生け垣の並木の木（楢）。幹と、札を三枚交差させた樹冠。
        /// **樹冠を箱で組まない。** 葉の玉を箱三つで組んで四つ重ねていたら、庭の奥の生け垣の上に
        /// 緑の立方体が並んで見えた（2026-09-27、庭の仕上げ直し）。札にすると三角も一本 144 から 6 に減る。
        /// 上から見たとき用の寝かせた札は置かない。下から見上げると樹冠の中ほどに横一文字の線が出て、箱の底に見えた
        /// </summary>
        static void FieldTree(Banks b, Vector3 at, float scale, int seed)
        {
            Beam(b.Bark, at, at + Vector3.up * 3.6f * scale, 0.45f * scale, 0.42f * scale);
            var high = 6.2f * scale * (0.92f + Hash(seed * 7 + 5, 0) * 0.16f);
            var wide = 7.2f * scale * (0.9f + Hash(seed * 7 + 6, 0) * 0.2f);
            Crown(Kind.Oak, at + Vector3.up * (2.6f * scale), high, wide, Hash(seed * 7 + 7, 0) * 180f);
        }

        /// <summary>
        /// 樹冠の札を三枚、60 度ずつ回して交差させる。foot は札の下辺の中（樹冠の下の縁）。
        /// 根は地面（foot の下の樹冠の下の縁から、地面までは幹の分）に取り、揺れは背に応じて頭打ち（Foliage.shader）
        /// </summary>
        static void Crown(Kind kind, Vector3 foot, float high, float wide, float yaw)
        {
            Vector2 min, max;
            CellUv(kind, out min, out max);
            var f = fieldCrowns;
            f.RootY = foot.y - high * 0.4f;
            f.RootHigh = high * 1.4f;
            for (var c = 0; c < 3; c++)
            {
                var a = (yaw + 60f * c) * Mathf.Deg2Rad;
                var across = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * (wide * 0.5f);
                f.AtlasCard(foot, across, Vector3.up * high, min, max);
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
            // 25 m から先は、教会を近くに組んだ分の三角を浮かすため、もう一段粗くした
            return d < 25f ? 0.5f + 0.08f * d : 2.6f + 0.17f * (d - 25f);
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
