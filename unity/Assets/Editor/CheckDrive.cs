using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace HalfAware.EditorTools
{
    /// <summary>
    /// 組み立てた場面 8 を機械で見直す。`HalfAware/Build the drive` の最後に必ず走る。
    ///
    /// ここに並べた見直しは、どれもこの場面を組む途中で実際に出た不具合から来ている。
    /// 目で気づくまで放っておくと、地平の穴も沿道のずれも「そういうものだ」と見過ごす。
    /// 減らすときは、その不具合がもう起きない理由の方を先に書くこと。
    ///
    /// 帯はここでも 0 から数える。ただし警告に出す名前は <see cref="DriveBand.name"/> を使う。
    /// オーナーは 1 始まりの設計書を横に置いて読むので、番号では突き合わせられない
    /// </summary>
    public static class CheckDrive
    {
        /// <summary>
        /// 道の上を見る高さ。車の背（<see cref="BuildDrive.BlockTop"/> 1.94）に 0.16 の余裕を足したところ。
        ///
        /// ここより上を見ないのは、帯 1 の街灯の腕が道の上へわざと差し出してあり（7.1 m）、
        /// 帯 2 の枝も道に被るため。捕まえたいのは車が通り抜ける高さに立っている物であって、
        /// 頭上を跨いでいる物ではない。
        ///
        /// **上と下から挟まれている。** 下は車の背で、オフロード車にしたときに 1.55 から 1.94 へ迫った。
        /// 上は帯 2 の一番低い枝の 2.155 m で、そこまで上げると木の側で知らせが出る。
        /// 残りは上へ 0.055、下へ 0.16 しか無い。車をこれ以上高くするなら、
        /// 先に木を持ち上げるか細らせること。乗用車だった頃の 2.0 では、車の背まで 0.06 しか空かない
        /// </summary>
        const float Clearance = 2.10f;

        /// <summary>
        /// 路面にじかに貼る面。道に乗っているのが正しいので、道へのはみ出しは見ない。
        /// 帯 0 の濡れた照り返しと、帯 4 の土と轍がこれにあたる
        /// </summary>
        static readonly string[] Paving = { "Sheen", "Earth", "Ruts" };

        /// <summary>
        /// 路面に重ねる面の隔たり。m。BuildDrive の「路面に重ねる面の高さ」の但し書きから取った。
        /// 手前 0.1 / 奥 1000 の深度では 100 m あたり 6 mm ほどが限界なので、8 mm 取ってある
        /// </summary>
        const float RoadGap = 0.008f;

        /// <summary>
        /// ガレージの床に重ねる面の隔たり。m。こちらも BuildDrive の但し書きから。
        /// 道の面ほど遠くを見ないので 4 mm で足りる
        /// </summary>
        const float BayGap = 0.004f;

        /// <summary>
        /// 路面に重なる面。下から上へ。BuildDrive の高さの表と同じ並び。
        /// タイルと区切りは 0 番だけ見る。同じ mesh を全部で使い回しているので、
        /// 1 枚見れば残りも同じ高さに乗っている
        /// </summary>
        static readonly string[] RoadStack =
        {
            "Road/Tile0",                        // 舗装。0.000
            "Roadsides/Band0/Slice0/Sheen",      // 帯 0 の濡れた照り返し
            "Road/Tile0/Line",                   // 白線。照り返しより上でないと帯 0 で消える
            "Roadsides/Band4/Slice0/Earth",      // 帯 4 の土。白線を覆い隠す
            "Roadsides/Band4/Slice0/Ruts",       // 帯 4 の轍
        };

        /// <summary>ガレージの床に重なる面。下から上へ。塗りの上に油、その上に排水口</summary>
        static readonly string[] BayStack =
        {
            "Garage/Floor",
            "Garage/BayPaint",
            "Garage/OilStains",
            "Garage/DrainPan",
            "Garage/DrainGrate",
        };

        /// <summary>
        /// 歩く線から排水口までの許す隔たり。m。
        ///
        /// プレイヤーの当たりの半径が 0.3 なので、線から 0.4 の内側にあれば
        /// 歩いている体の真下か、すぐ脇を過ぎる。それより離れたものは
        /// 「歩く線の上」ではなく、ただ横に置いてある飾りになる
        /// </summary>
        const float OnWalk = 0.40f;

        [MenuItem("HalfAware/Check the drive", false, 235)]
        public static void Menu()
        {
            var root = GameObject.Find("Drive");
            if (root == null) { Debug.LogWarning("Drive が無い。先に組み立てる"); return; }
            Run(root.transform);
        }

        public static void Run(Transform root)
        {
            // 組み立て直後は当たりの位置がまだ揃っていないことがある。
            // CheckCapsule を呼ぶ前に一度だけ合わせる
            Physics.SyncTransforms();

            var bad = 0;
            bad += Tiles(root);
            bad += Rings(root);
            bad += OnRoad(root);
            bad += Pins(root);
            bad += Ids(root);
            bad += Bands(root);
            bad += Targets(root);
            bad += Counts();
            bad += Reach();
            bad += Fonts();
            bad += Drift();
            bad += Walk(root);
            bad += Ladder(root);
            if (bad == 0) Debug.Log("見直し: 気になるところは無し");
            else Debug.LogWarning("見直し: 気になるところ " + bad + " 件。上を参照");
        }

        // ---- 1. タイルの環 ----------------------------------------------------

        /// <summary>
        /// シーンに立っているタイルの実物を見る。環の計算そのものは RoadRingTests が
        /// 確かめているので、ここで数え直しても落ちない。見るのは置き方の方。
        ///
        /// 空きがあれば 20 m の穴が環に乗って回り、16 m/s なら 11 秒ごとに正面へ飛んでくる。
        /// z が式とずれれば継ぎ目が割れる。そして mesh の原点が手前（-z）端から外れていると、
        /// 環は綺麗に並んだままで地平にだけ穴が空く。中央に置けば半枚ぶん、
        /// 奥端に置けば丸ごと一枚ぶん足りない
        /// </summary>
        static int Tiles(Transform root)
        {
            var road = root.Find("Road");
            if (road == null) { Debug.LogWarning("見直し: Road が無い"); return 1; }

            var bad = 0;
            var n = road.childCount;
            if (n != BuildDrive.TileCount)
            {
                Debug.LogWarning(string.Format("見直し: タイルが {0} 枚。環に要るのは {1} 枚",
                    n, BuildDrive.TileCount), road.gameObject);
                bad++;
            }
            for (var i = 0; i < n; i++)
            {
                var tile = road.GetChild(i);
                var mf = tile.GetComponent<MeshFilter>();
                if (mf == null || mf.sharedMesh == null)
                {
                    Debug.LogWarning("見直し: タイル " + tile.name + " に mesh が無い。環に穴が空く", tile.gameObject);
                    bad++;
                    continue;
                }
                // 組み立ても DriveWorld も同じ式で置く。ここがずれていると走り出しで並びが跳ぶ
                var want = RoadRing.Slot(i, n, BuildDrive.TileLength, 0f, BuildDrive.Behind);
                if (Mathf.Abs(tile.localPosition.z - want) > 0.001f)
                {
                    Debug.LogWarning(string.Format("見直し: タイル {0} の z が {1:F3}。環の枠は {2:F3}",
                        tile.name, tile.localPosition.z, want), tile.gameObject);
                    bad++;
                }
                var box = mf.sharedMesh.bounds;
                if (Mathf.Abs(box.size.z - BuildDrive.TileLength) > 0.001f)
                {
                    Debug.LogWarning(string.Format("見直し: タイル {0} の mesh が z 方向に {1:F3} m。タイルの長さは {2} m",
                        tile.name, box.size.z, BuildDrive.TileLength), tile.gameObject);
                    bad++;
                }
                else if (Mathf.Abs(box.min.z) > 0.001f)
                {
                    Debug.LogWarning(string.Format("見直し: タイル {0} の mesh の原点が手前端から {1:F3} m ずれている。地平に穴が空く",
                        tile.name, -box.min.z), tile.gameObject);
                    bad++;
                }
            }
            return bad;
        }

        // ---- 2. 沿道と対向車の環 ----------------------------------------------

        /// <summary>
        /// 帯の入れ物の下が、タイルと同じ枚数の区切りだけで出来ているか。
        ///
        /// DriveWorld.Place は入れ物の childCount をそのまま環の大きさに使う。
        /// 帯ぜんたいを照らす灯りを 1 つ混ぜただけで環が 10 になり、
        /// 沿道が道の継ぎ目から全部ずれる。対向車の入れ物も同じ作りなので一緒に見る
        /// </summary>
        static int Rings(Transform root)
        {
            return Ring(root.Find("Roadsides"), "沿道") + Ring(root.Find("Oncoming"), "対向車");
        }

        static int Ring(Transform parent, string what)
        {
            if (parent == null) { Debug.LogWarning("見直し: " + what + " の入れ物が無い"); return 1; }
            var bad = 0;
            if (parent.childCount != BuildDrive.Bands)
            {
                Debug.LogWarning(string.Format("見直し: {0} の帯が {1} 個。帯は {2} 本",
                    what, parent.childCount, BuildDrive.Bands), parent.gameObject);
                bad++;
            }
            for (var b = 0; b < parent.childCount; b++)
            {
                var band = parent.GetChild(b);
                if (band.childCount != BuildDrive.TileCount)
                {
                    Debug.LogWarning(string.Format("見直し: {0}・帯「{1}」の区切りが {2} 個。タイルは {3} 枚",
                        what, BandName(b), band.childCount, BuildDrive.TileCount), band.gameObject);
                    bad++;
                }
                for (var i = 0; i < band.childCount; i++)
                {
                    var slice = band.GetChild(i);
                    if (slice.name == "Slice" + i) continue;
                    Debug.LogWarning(string.Format("見直し: {0}・帯「{1}」の {2} 番目が区切りではない: {3}",
                        what, BandName(b), i, slice.name), slice.gameObject);
                    bad++;
                }
            }
            return bad;
        }

        // ---- 3. 沿道が道に出ていないか ----------------------------------------

        /// <summary>
        /// 沿道の物が道の上へ出ていないか。
        ///
        /// 沿道の座標は道の中心からの距離で書いてあるのに、mesh の大きさと回しと
        /// 拡げ方は別に決まるので、手で置いた数字だけでは道に掛かるかどうか分からない。
        /// 車は原点で +z を向いたまま動かないので、世界の x がそのまま車から見た左右になる。
        /// あとは道の中心（LaneOffset）を引くだけでよい。
        ///
        /// 帯 4 だけは道が轍の幅まで細る（DirtHalf）。麦はその外に立てる約束なので、
        /// 舗装の幅で見ると必ず引っかかる。
        ///
        /// 麦はさらに風でなびく。**頂点の位置だけでは足りない。** 止まっているときは
        /// 道の外にいても、なびいた瞬間だけ轍へ倒れ込むことがあり、それは絵を撮っても写らない。
        /// 穂先が横へ振れる幅（WheatWind.Reach）を足して見る
        /// </summary>
        static int OnRoad(Transform root)
        {
            var parent = root.Find("Roadsides");
            if (parent == null) return 0;
            var bad = 0;
            for (var b = 0; b < parent.childCount; b++)
            {
                var band = parent.GetChild(b);
                // 未舗装は最後の帯。BuildDrive.Roadsides も同じ数え方で Furrows を呼ぶ
                var half = b == BuildDrive.Bands - 1 ? BuildDrive.DirtHalf : BuildDrive.RoadHalf;
                foreach (var mf in band.GetComponentsInChildren<MeshFilter>(true))
                {
                    if (mf.sharedMesh == null) continue;
                    if (System.Array.IndexOf(Paving, mf.name) >= 0) continue;
                    // なびく物は振れ幅のぶんだけ道へ寄せて見る
                    var reach = mf.name == "Wheat" ? WheatWind.Reach : 0f;
                    var into = 0f;
                    var low = 0f;
                    var m = mf.transform.localToWorldMatrix;
                    var verts = mf.sharedMesh.vertices;
                    for (var i = 0; i < verts.Length; i++)
                    {
                        var p = m.MultiplyPoint3x4(verts[i]);
                        if (p.y > Clearance) continue;
                        var deep = half + reach - Mathf.Abs(p.x - BuildDrive.LaneOffset);
                        if (deep <= into) continue;
                        into = deep;
                        low = p.y;
                    }
                    if (into <= 0f) continue;
                    Debug.LogWarning(string.Format(
                        "見直し: 沿道の物が道に出ている: 帯「{0}」の {1}。{2:F3} m 食い込んでいる（高さ {3:F2}、風の振れ {4:F3} を含む）",
                        BandName(b), mf.name, into, low, reach), mf.gameObject);
                    bad++;
                }
            }
            return bad;
        }

        // ---- 4. ピンが埋まっていないか ----------------------------------------

        /// <summary>印の出る高さ。<see cref="PinMarkers"/> の lift と同じ</summary>
        const float Lift = 0.17f;

        /// <summary>
        /// 調べる対象の印（ピン）が出る高さが、絵のある形の中に入っていないか。
        /// 埋まると印が見えない。CheckAlley.Pins と同じ狙い。
        ///
        /// **ただし当たりでは見られない。** 車内の形は当たりをそもそも持たず、
        /// ガレージの当たりは全部が非凸の MeshCollider で、Physics.OverlapSphere はそれを拾わない。
        /// 車体を塞ぐ箱だけが BoxCollider だが、そちらは絵を持たないので隠しもしない。
        /// CheckAlley と同じ書き方をここへ写すと、何を埋めても黙って通る見直しになる。
        /// なので mesh の三角形をじかに数える。点から軸の向きへ線を引いて、
        /// 出入りの差が三方向とも 0 でなければ形の中に居る。
        /// 三方向を見るのは、床に貼った一枚きりの面のような閉じていない形を中と数えないため。
        ///
        /// 場面 8 には今 PinMarkers が置かれておらず、印そのものはまだ出ない。
        /// それでも見るのは、判定点が形の中に据わっていること自体が置き間違いで、
        /// 印を出した途端に見えなくなるため
        /// </summary>
        static int Pins(Transform root)
        {
            var items = root.Find("Items");
            if (items == null) return 0;
            var bad = 0;
            // 道と沿道と対向車は流れて通り過ぎる。組んだ時点のひとところで見ても、
            // 一周のうち一区切りのあいだ重なっていたというだけで意味が無い。
            // ピンを隠しうるのは据わっている車内とガレージの形だけ
            var flows = new[] { root.Find("Road"), root.Find("Roadsides"), root.Find("Oncoming") };
            var solids = new List<MeshFilter>();
            foreach (var mf in Object.FindObjectsByType<MeshFilter>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (mf.sharedMesh == null) continue;
                // 絵を持たない形は隠さない
                if (mf.GetComponent<MeshRenderer>() == null) continue;
                var flowing = false;
                for (var i = 0; i < flows.Length; i++)
                    if (flows[i] != null && mf.transform.IsChildOf(flows[i])) { flowing = true; break; }
                if (flowing) continue;
                solids.Add(mf);
            }
            var shapes = new Dictionary<Mesh, Vector3[]>();
            var faces = new Dictionary<Mesh, int[]>();
            foreach (var it in items.GetComponentsInChildren<Interactable>(true))
            {
                var at = it.Position + Vector3.up * Lift;
                var inside = "";
                foreach (var mf in solids)
                {
                    var mesh = mf.sharedMesh;
                    var local = mf.transform.InverseTransformPoint(at);
                    // 外枠の外なら中には入れない。ここで大半が落ちる
                    if (!mesh.bounds.Contains(local)) continue;
                    if (!shapes.ContainsKey(mesh))
                    {
                        shapes[mesh] = mesh.vertices;
                        faces[mesh] = mesh.triangles;
                    }
                    if (Wound(shapes[mesh], faces[mesh], local, Vector3.right) == 0) continue;
                    if (Wound(shapes[mesh], faces[mesh], local, Vector3.up) == 0) continue;
                    if (Wound(shapes[mesh], faces[mesh], local, Vector3.forward) == 0) continue;
                    inside += (inside == "" ? "" : ", ") + mf.name;
                }
                if (inside == "") continue;
                Debug.LogWarning(string.Format("見直し: {0} のピンが {1} に埋まっている", it.Id, inside), it.gameObject);
                bad++;
            }
            return bad;
        }

        /// <summary>
        /// at から dir へ引いた線が、形をいくつ抜け出るか。出るのを +1、入るのを -1 と数える。
        /// 0 なら形の外、0 でなければ中。
        ///
        /// 跨いだ回数の偶数奇数では数えられない。この場面の形は Bank が箱を重ねて作るので、
        /// 重なったところでは面が二枚続き、中に居ても偶数回になる。
        /// 覆いを掛けた車が実際にそれで、8 回跨いでいながら中に居た。
        /// 出入りの向きまで数えれば、重なっている数がそのまま出る
        /// </summary>
        static int Wound(Vector3[] verts, int[] tris, Vector3 at, Vector3 dir)
        {
            var sum = 0;
            for (var i = 0; i + 2 < tris.Length; i += 3)
                sum += Crosses(at, dir, verts[tris[i]], verts[tris[i + 1]], verts[tris[i + 2]]);
            return sum;
        }

        /// <summary>
        /// 線が三角形を前向きに跨ぐか（Moller-Trumbore）。跨がなければ 0、
        /// 跨ぐなら面の向きで +1 か -1 を返す。どちらが「出る」側かは形の巻き方で決まるが、
        /// 中に居るかどうかは和が 0 かどうかで見るので、向きの取り決めは要らない
        /// </summary>
        static int Crosses(Vector3 from, Vector3 dir, Vector3 a, Vector3 b, Vector3 c)
        {
            var e1 = b - a;
            var e2 = c - a;
            var p = Vector3.Cross(dir, e2);
            var det = Vector3.Dot(e1, p);
            if (Mathf.Abs(det) < 1e-9f) return 0;
            var inv = 1f / det;
            var s = from - a;
            var u = Vector3.Dot(s, p) * inv;
            if (u < 0f || u > 1f) return 0;
            var q = Vector3.Cross(s, e1);
            var w = Vector3.Dot(dir, q) * inv;
            if (w < 0f || u + w > 1f) return 0;
            if (Vector3.Dot(e2, q) * inv <= 0f) return 0;             // 後ろは数えない
            return det > 0f ? 1 : -1;
        }

        // ---- 5. id の食い違い --------------------------------------------------

        /// <summary>
        /// 文面とシーンの id が揃っているか。片方だけあると黙って何も出ない。
        /// CheckAlley.Ids と同じ。独白の段は看板ではなく帯から引くので、対象は無くてよい
        /// </summary>
        static int Ids(Transform root)
        {
            var script = AssetDatabase.LoadAssetAtPath<RoomScript>(BuildDrive.ScriptPath);
            if (script == null) { Debug.LogWarning("見直し: 場面 8 の文面が無い"); return 1; }
            var inScene = new HashSet<string>();
            var items = root.Find("Items");
            var bad = 0;
            if (items != null)
                foreach (var it in items.GetComponentsInChildren<Interactable>(true))
                {
                    if (string.IsNullOrEmpty(it.Id))
                    {
                        Debug.LogWarning("見直し: " + it.name + " に id が無い", it.gameObject);
                        bad++;
                        continue;
                    }
                    inScene.Add(it.Id);
                }
            foreach (var id in inScene)
                if (script.Find(id).id == null)
                {
                    Debug.LogWarning("見直し: シーンの " + id + " が文面に無い");
                    bad++;
                }
            foreach (var id in script.Ids())
            {
                if (DriveIds.IsPage(id)) continue;                    // 段は帯から引くので、対象は無くてよい
                if (inScene.Contains(id)) continue;
                Debug.LogWarning("見直し: 文面の " + id + " を出す対象がシーンに無い");
                bad++;
            }
            return bad;
        }

        // ---- 6. 帯ときっかけの対応 --------------------------------------------

        /// <summary>
        /// DriveDirector の帯に、DriveIds.Triggers がひとつずつ順に割り当たっているか。
        ///
        /// 帯を跨ぐ条件はきっかけを調べることだけなので、きっかけが空の帯に入ると
        /// そこから先へ二度と進めない。並びが入れ替わっていると、調べても
        /// DriveRoute.BandOf が別の帯を返して独白が出ない
        /// </summary>
        static int Bands(Transform root)
        {
            var row = BandRow();
            if (row == null) { Debug.LogWarning("見直し: DriveDirector が無い。帯を見られない"); return 1; }

            var bad = 0;
            if (row.arraySize != DriveIds.Triggers.Count)
            {
                Debug.LogWarning(string.Format("見直し: 帯が {0} 本。きっかけは {1} 個",
                    row.arraySize, DriveIds.Triggers.Count));
                bad++;
            }
            var seen = new Dictionary<string, int>();
            for (var i = 0; i < row.arraySize; i++)
            {
                var e = row.GetArrayElementAtIndex(i);
                var name = e.FindPropertyRelative("name").stringValue;
                if (string.IsNullOrEmpty(name)) name = "Band" + i;
                var trigger = e.FindPropertyRelative("trigger").stringValue;
                if (string.IsNullOrEmpty(trigger))
                {
                    Debug.LogWarning("見直し: 「" + name + "」にきっかけが無い");
                    bad++;
                    continue;
                }
                if (seen.ContainsKey(trigger))
                {
                    Debug.LogWarning("見直し: きっかけが二つの帯で使われている: " + trigger);
                    bad++;
                }
                else seen[trigger] = i;
                if (i >= DriveIds.Triggers.Count || trigger == DriveIds.Triggers[i]) continue;
                Debug.LogWarning(string.Format("見直し: 「{0}」のきっかけが {1}。DriveIds.Triggers の {2} 番目は {3}",
                    name, trigger, i, DriveIds.Triggers[i]));
                bad++;
            }
            for (var i = 0; i < DriveIds.Triggers.Count; i++)
            {
                if (seen.ContainsKey(DriveIds.Triggers[i])) continue;
                Debug.LogWarning("見直し: どの帯にも割り当たっていないきっかけ: " + DriveIds.Triggers[i]);
                bad++;
            }
            return bad;
        }

        // ---- 7. きっかけの対象がシーンにあるか --------------------------------

        /// <summary>
        /// 帯のきっかけの id を持つ Interactable が、DriveDirector.triggers の同じ番号に
        /// 繋がっているか。DriveDirector.ShowTrigger は番号でしか出し分けないので、
        /// 繋ぎ先が違う対象でも黙って出る。出た対象の id が帯と合わなければ、
        /// 調べても Examined が弾いて帯が終わらない
        /// </summary>
        static int Targets(Transform root)
        {
            var director = Object.FindFirstObjectByType<DriveDirector>(FindObjectsInactive.Include);
            if (director == null) return 0;                           // Bands が既に知らせている
            var so = new SerializedObject(director);
            var row = so.FindProperty("bands");
            var picked = so.FindProperty("triggers");
            var bad = 0;
            if (picked.arraySize != row.arraySize)
            {
                Debug.LogWarning(string.Format("見直し: 帯 {0} 本に対してきっかけの対象が {1} 個",
                    row.arraySize, picked.arraySize), director);
                bad++;
            }
            for (var i = 0; i < row.arraySize; i++)
            {
                var name = row.GetArrayElementAtIndex(i).FindPropertyRelative("name").stringValue;
                if (string.IsNullOrEmpty(name)) name = "Band" + i;
                var id = row.GetArrayElementAtIndex(i).FindPropertyRelative("trigger").stringValue;
                var go = i < picked.arraySize
                    ? picked.GetArrayElementAtIndex(i).objectReferenceValue as GameObject
                    : null;
                if (go == null)
                {
                    Debug.LogWarning("見直し: 「" + name + "」のきっかけの対象が繋がっていない", director);
                    bad++;
                    continue;
                }
                var found = false;
                foreach (var it in go.GetComponentsInChildren<Interactable>(true))
                    if (it.Id == id) { found = true; break; }
                if (found) continue;
                Debug.LogWarning(string.Format("見直し: 「{0}」のきっかけ {1} を持つ対象が {2} の下に無い",
                    name, id, go.name), go);
                bad++;
            }
            return bad;
        }

        // ---- 8. 文面の数が揃っているか ----------------------------------------

        /// <summary>
        /// WriteDriveScript の表の長さが、きっかけの数と揃っているか。
        ///
        /// Triggers を減らしたときに、余った独白が黙って書き出されなくなるのを捕まえる。
        /// EditMode テストからは Assembly-CSharp-Editor の中が見えないので、
        /// この突き合わせはここでしか出来ない
        /// </summary>
        static int Counts()
        {
            var n = DriveIds.Triggers.Count;
            var bad = 0;
            bad += Length("BandPages", WriteDriveScript.BandPages.Length, n);
            bad += Length("TriggerLines", WriteDriveScript.TriggerLines.Length, n);
            bad += Length("TriggerLabels", WriteDriveScript.TriggerLabels.Length, n);
            return bad;
        }

        static int Length(string what, int got, int want)
        {
            if (got == want) return 0;
            Debug.LogWarning(string.Format("見直し: WriteDriveScript.{0} が {1} 行。きっかけは {2} 個",
                what, got, want));
            return 1;
        }

        // ---- 9. 乗る前に車内の物へ届かないか ----------------------------------

        /// <summary>
        /// ガレージで立てるところから InteractionPicker.Select を実際に呼び、
        /// `drive.` で始まる id が返らないことを見る。
        ///
        /// 車内の対象は once: true なので、乗り込む前にガレージから読まれると走行中に
        /// 二度と出ない。塞ぐ箱があっても立ち位置から 1.1〜1.3 m しか離れず、
        /// 拾える距離 1.4 の内側に入る。今これが通るのは箱のおかげではなく、
        /// 乗り込むまで対象を伏せてあるからで、**出しっぱなしにすれば必ずここで落ちる。**
        ///
        /// 立てるところは当たりで決める。当たりの筒（半径 0.3・高さ 1.7）が
        /// 壁にも柱にも車体の箱にも入らない床の上を、20 cm 刻みで拾う。
        /// 拾う範囲を車内の対象の周りに絞るのは、Select が拾える距離の外からは
        /// 何をどう向いても null しか返さないため
        /// </summary>
        static int Reach()
        {
            var all = new List<IInteractable>(
                Object.FindObjectsByType<Interactable>(FindObjectsInactive.Include, FindObjectsSortMode.InstanceID));
            var done = new HashSet<string>();

            // 車内の対象が届きうる範囲。拾える距離に、目が体より前に出るぶんを足す
            var lo = new Vector3(float.MaxValue, 0f, float.MaxValue);
            var hi = new Vector3(float.MinValue, 0f, float.MinValue);
            var span = 0f;
            foreach (var it in all)
            {
                if (it.Id == null || !it.Id.StartsWith("drive.")) continue;
                lo = Vector3.Min(lo, it.Position);
                hi = Vector3.Max(hi, it.Position);
                span = Mathf.Max(span, it.Radius);
            }
            if (span <= 0f) { Debug.LogWarning("見直し: 車内の対象が 1 つも無い"); return 1; }
            var edge = span + BuildDrive.EyeLead;

            var foot = BuildDrive.GarageFloorY + 0.06f;
            var spots = new List<Vector3>();
            // 立ち位置そのもの。ここから歩き始める
            spots.Add(new Vector3(BuildDrive.StandAt.x, foot, BuildDrive.StandAt.z));
            for (var x = lo.x - edge; x <= hi.x + edge; x += 0.2f)
                for (var z = lo.z - edge; z <= hi.z + edge; z += 0.2f)
                {
                    var at = new Vector3(x, foot, z);
                    // 当たりの筒が何かに入っていれば、そこには立てない。
                    // 半径をわずかに縮めるのは、床に触れているだけで塞がれたことにしないため
                    if (Physics.CheckCapsule(at + Vector3.up * 0.30f, at + Vector3.up * 1.40f, 0.29f)) continue;
                    spots.Add(at);
                }

            var bad = 0;
            var told = new HashSet<string>();
            foreach (var at in spots)
            {
                for (var turn = 0; turn < 24; turn++)
                {
                    var yaw = turn * 15f;
                    var body = Quaternion.Euler(0f, yaw, 0f);
                    // 目は顔にあるので体より前に出る。PlayerController が毎フレーム置き直す位置
                    var eye = at + body * new Vector3(0f, PlayerController.StandingEyeHeight, BuildDrive.EyeLead);
                    for (var tip = -PlayerController.PitchLimit; tip <= PlayerController.PitchLimit; tip += 20f)
                    {
                        var look = Quaternion.Euler(tip, yaw, 0f) * Vector3.forward;
                        var hit = InteractionPicker.Select(eye, look, all, done);
                        if (hit == null || hit.Id == null || !hit.Id.StartsWith("drive.")) continue;
                        if (!told.Add(hit.Id)) continue;
                        Debug.LogWarning(string.Format(
                            "見直し: 乗る前に車内の {0} へ届く。立ち位置 {1} から {2:F0} 度を向いたとき。once: true なので走行中は二度と出ない",
                            hit.Id, at.ToString("F2"), yaw));
                        bad++;
                    }
                }
            }
            return bad;
        }

        // ---- 10. HUD のフォント ------------------------------------------------

        /// <summary>
        /// 暗転中に出る字だけ明朝、台詞はゴシック。Alley.unity も Room.unity も同じ取り決めで、
        /// Center 層だけ明朝を使っている。
        ///
        /// 場面 8 の HUD は手で置いたものではなく BuildDrive が毎回組み直すので、
        /// 写し間違えると「続く」だけが黙ってゴシックで出る。
        /// 暗い画面に一行だけ出る字なので、並べて見比べないと気づけない
        /// </summary>
        static int Fonts()
        {
            var hud = Object.FindFirstObjectByType<HudView>(FindObjectsInactive.Include);
            if (hud == null) { Debug.LogWarning("見直し: Hud が無い"); return 1; }
            var gothic = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(BuildDrive.FontPath);
            var mincho = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(BuildDrive.MinchoPath);
            if (gothic == null || mincho == null) { Debug.LogWarning("見直し: 字の形が読めない"); return 1; }
            var bad = 0;
            foreach (var text in hud.GetComponentsInChildren<TMP_Text>(true))
            {
                var want = text.name == "Center" ? mincho : gothic;
                if (text.font == want) continue;
                Debug.LogWarning(string.Format("見直し: HUD の {0} の字が {1}。{2} のはず",
                    text.name, text.font == null ? "未設定" : text.font.name, want.name), text.gameObject);
                bad++;
            }
            return bad;
        }

        // ---- 11. アセットが今の文面と合っているか ------------------------------

        /// <summary>
        /// DriveScript.asset の中身が、いま WriteDriveScript が書き出すものと一致するか。
        ///
        /// アセットは Inspector で直に触れてしまうので、書き出しを走らせ忘れたまま
        /// commit されたのを捕まえる。台詞を直したのに書き出していない、という向きも同じく捕まる。
        /// WriteDriveScript は同じアセンブリなので、書き出す中身をそのまま作らせて突き合わせられる。
        /// EditMode テストからは見えないので、この突き合わせもここでしか出来ない
        /// </summary>
        static int Drift()
        {
            var asset = AssetDatabase.LoadAssetAtPath<RoomScript>(BuildDrive.ScriptPath);
            if (asset == null) { Debug.LogWarning("見直し: 場面 8 の文面が無い"); return 1; }
            // 表がきっかけより短いと Entries が範囲の外を引いて落ちる。
            // それは Counts が既に知らせているので、ここは黙って飛ばす
            var n = DriveIds.Triggers.Count;
            if (WriteDriveScript.BandPages.Length < n
                || WriteDriveScript.TriggerLines.Length < n
                || WriteDriveScript.TriggerLabels.Length < n) return 0;
            var want = WriteDriveScript.Entries();
            var got = asset.Ids();
            var bad = 0;
            if (got.Length != want.Count)
            {
                Debug.LogWarning(string.Format("見直し: 文面のアセットが {0} 項目。書き出すのは {1} 項目。Write the drive script を走らせ直す",
                    got.Length, want.Count), asset);
                bad++;
            }
            for (var i = 0; i < want.Count; i++)
            {
                if (i < got.Length && got[i] != want[i].id)
                {
                    Debug.LogWarning(string.Format("見直し: 文面のアセットの {0} 番目が {1}。書き出すのは {2}",
                        i, got[i], want[i].id), asset);
                    bad++;
                }
                var here = asset.Find(want[i].id);
                if (here.id == null)
                {
                    Debug.LogWarning("見直し: 文面のアセットに " + want[i].id + " が無い。Write the drive script を走らせ直す", asset);
                    bad++;
                    continue;
                }
                var how = Differs(here, want[i]);
                if (how == null) continue;
                Debug.LogWarning(string.Format("見直し: 文面のアセットの {0} が書き出しと違う（{1}）。Write the drive script を走らせ直す",
                    want[i].id, how), asset);
                bad++;
            }
            foreach (var id in got)
            {
                var extra = true;
                for (var i = 0; i < want.Count; i++) if (want[i].id == id) { extra = false; break; }
                if (!extra) continue;
                Debug.LogWarning("見直し: 文面のアセットに書き出しの無い項目がある: " + id, asset);
                bad++;
            }
            return bad;
        }

        /// <summary>二つの項目の違い。同じなら null</summary>
        static string Differs(ScriptEntry got, ScriptEntry want)
        {
            if (got.label != want.label) return "印が「" + got.label + "」/「" + want.label + "」";
            var lines = Rows(got.lines, want.lines);
            if (lines != null) return "文の " + lines;
            if (Rows(got.hints == null ? 0 : got.hints.Length, want.hints == null ? 0 : want.hints.Length) != null)
                return "前提の文の数が違う";
            if ((got.choice.question ?? "") != (want.choice.question ?? "")) return "二択の問いが違う";
            var yes = Rows(got.choice.afterYes, want.choice.afterYes);
            if (yes != null) return "二択の後の文の " + yes;
            return null;
        }

        static string Rows(string[] got, string[] want)
        {
            var a = got == null ? 0 : got.Length;
            var b = want == null ? 0 : want.Length;
            var many = Rows(a, b);
            if (many != null) return many;
            for (var i = 0; i < a; i++)
                if (got[i] != want[i]) return (i + 1) + " 行目が違う";
            return null;
        }

        static string Rows(int got, int want)
        {
            return got == want ? null : "数が " + got + " / " + want;
        }

        // ---- 12. 歩く線の上の飾り ---------------------------------------------

        /// <summary>
        /// 排水口が、立ち位置から運転席のドアまでの歩く線の上に乗っているか。
        ///
        /// 「歩く線がちょうど踏む場所に置く」と書いてあるのに、座標は手で出した数字で、
        /// StandAt からもドアからも引いていない。どちらかを動かせば黙って線から外れ、
        /// 8 m 歩くあいだ床に見るものが無くなる。
        ///
        /// 油染みも同じく歩く線の上に落としてあるが、三つの染みが一つの mesh に
        /// まとまっているので、どれが線の上のものか形からは分けられない。
        /// 排水口は同じ線から手で出した数字なので、線が動けばこちらで気づける
        /// </summary>
        static int Walk(Transform root)
        {
            var items = root.Find("Items");
            Interactable door = null;
            if (items != null)
                foreach (var it in items.GetComponentsInChildren<Interactable>(true))
                    if (it.Id == DriveIds.Door) { door = it; break; }
            if (door == null) { Debug.LogWarning("見直し: ガレージのドアの対象が無い。歩く線が引けない"); return 1; }

            var pan = root.Find("Garage/DrainPan");
            var mf = pan == null ? null : pan.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) { Debug.LogWarning("見直し: 排水口が無い"); return 1; }
            var at = pan.TransformPoint(mf.sharedMesh.bounds.center);

            var from = new Vector2(BuildDrive.StandAt.x, BuildDrive.StandAt.z);
            var to = new Vector2(door.Position.x, door.Position.z);
            var gap = Away(new Vector2(at.x, at.z), from, to);
            if (gap <= OnWalk) return 0;
            Debug.LogWarning(string.Format("見直し: 排水口が歩く線から {0:F2} m 離れている（許すのは {1:F2} m）。立ち位置 {2} からドア {3} まで",
                gap, OnWalk, from.ToString("F2"), to.ToString("F2")), pan.gameObject);
            return 1;
        }

        /// <summary>点から線分までの隔たり</summary>
        static float Away(Vector2 at, Vector2 from, Vector2 to)
        {
            var span = to - from;
            var len = span.sqrMagnitude;
            if (len < 1e-6f) return Vector2.Distance(at, from);
            var t = Mathf.Clamp01(Vector2.Dot(at - from, span) / len);
            return Vector2.Distance(at, from + span * t);
        }

        // ---- 13. 重ねた面の梯子 -------------------------------------------------

        /// <summary>
        /// 同じ平面に重ねた面が、決めた順に、決めた隔たりだけ離れているか。
        ///
        /// 高さは BuildDrive の定数と但し書きだけで決まっていて、これまで誰も数えていなかった。
        /// 一つ動かすと遠くでちらつくか、下の絵が消える。帯 0 の白線が照り返しに呑まれたのは
        /// 実際に起きたことで、しかも遠くでしか出ないので目では必ず遅れて気づく。
        ///
        /// 定数どうしを比べても意味が無い。mesh が定数の言うところに乗っていなければ
        /// 同じことなので、組み上がった mesh の頂点の高さから測る。
        ///
        /// 路肩（Verge）と牧草地（Pasture）はこの梯子に入れていない。路肩の mesh は
        /// 地面・段・段の立ち上がりの三つの高さを一つに抱えていて、頂点からはどれがどれか
        /// 分けられない。どちらも車道の外にあり、面として重なるのではなく縁で接している
        /// </summary>
        static int Ladder(Transform root)
        {
            return Rungs(root, RoadStack, RoadGap, "路面") + Rungs(root, BayStack, BayGap, "ガレージの床");
        }

        static int Rungs(Transform root, string[] stack, float gap, string what)
        {
            var bad = 0;
            var lo = new float[stack.Length];
            var hi = new float[stack.Length];
            for (var i = 0; i < stack.Length; i++)
            {
                lo[i] = float.NaN;
                var t = root.Find(stack[i]);
                var mf = t == null ? null : t.GetComponent<MeshFilter>();
                if (mf == null || mf.sharedMesh == null)
                {
                    Debug.LogWarning("見直し: " + what + "に重なる面が見つからない: " + stack[i]);
                    bad++;
                    continue;
                }
                var m = mf.transform.localToWorldMatrix;
                var verts = mf.sharedMesh.vertices;
                var low = float.PositiveInfinity;
                var high = float.NegativeInfinity;
                for (var v = 0; v < verts.Length; v++)
                {
                    var y = m.MultiplyPoint3x4(verts[v]).y;
                    if (y < low) low = y;
                    if (y > high) high = y;
                }
                lo[i] = low;
                hi[i] = high;
            }
            for (var i = 0; i + 1 < stack.Length; i++)
            {
                if (float.IsNaN(lo[i]) || float.IsNaN(lo[i + 1])) continue;
                // 下の面の一番上と、上の面の一番下を比べる。轍のように厚みのある面は、
                // 板そのものの高さではなく下に張り出した縁の方が下の面と取り合う
                var apart = lo[i + 1] - hi[i];
                // 4 mm ちょうどの組（排水口の受けと格子）が小数の丸めで落ちないよう、
                // 0.01 mm だけ見逃す
                if (apart >= gap - 1e-5f) continue;
                if (apart < 0f)
                    Debug.LogWarning(string.Format("見直し: {0}に重なる面の順が入れ替わっている。{1} の上端 {2:F4} が {3} の下端 {4:F4} より上",
                        what, stack[i], hi[i], stack[i + 1], lo[i + 1]));
                else
                    Debug.LogWarning(string.Format("見直し: {0}の {1} と {2} が {3:F4} m しか離れていない（{4:F3} m 要る）。遠くでちらつく",
                        what, stack[i], stack[i + 1], apart, gap));
                bad++;
            }
            return bad;
        }

        // ---- 道具 --------------------------------------------------------------

        /// <summary>DriveDirector の帯の表。無ければ null</summary>
        static SerializedProperty BandRow()
        {
            var director = Object.FindFirstObjectByType<DriveDirector>(FindObjectsInactive.Include);
            if (director == null) return null;
            return new SerializedObject(director).FindProperty("bands");
        }

        /// <summary>
        /// 帯の名前。警告に番号でなく名前を出すのは、オーナーが 1 始まりの設計書を
        /// 横に置いて読むため。DriveBand.name の説明にも「ログと見直しで使う」と書いてある
        /// </summary>
        static string BandName(int i)
        {
            var row = BandRow();
            if (row == null || i < 0 || i >= row.arraySize) return "Band" + i;
            var name = row.GetArrayElementAtIndex(i).FindPropertyRelative("name").stringValue;
            return string.IsNullOrEmpty(name) ? "Band" + i : name;
        }
    }
}
