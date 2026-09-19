using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HalfAware.EditorTools
{
    /// <summary>
    /// 場面 8 の車内と道を組む。車は原点に置いたままで、道の方を手前へ送るので、
    /// ここで作るのは「原点の周りに畳んだ世界」であって、何 km もの実寸の道ではない。
    ///
    /// 道はタイルを環にして流す（<see cref="RoadRing"/>／<see cref="DriveWorld"/>）。
    /// 沿道は帯ごとの入れ物に分け、その下をタイルと同じ枚数の区切りに割る。
    /// 区切りを道と同じ環に乗せるので、沿道と道の継ぎ目は勝手に揃う。
    /// 対向車だけは同じ形の入れ物を別に持ち、道より速い環に乗せる。
    ///
    /// 帯はここでは 0 から数える。設計書が「帯 1」と呼ぶものが 0 番にあたる
    /// </summary>
    public static class BuildDrive
    {
        public const string Materials = "Assets/Materials/Drive/";
        public const string Generated = "Assets/Models/generated/drive/";
        public const string ScenePath = "Assets/Scenes/Drive.unity";

        // ---- 道の寸法。メートル --------------------------------------------

        /// <summary>タイル 1 枚の長さ。2 の冪と相性の良い数にする。20 なら計算に丸めが入らない</summary>
        public const float TileLength = 20f;
        /// <summary>前方にここまで途切れず敷く</summary>
        public const float Ahead = 140f;
        /// <summary>車の後ろにここまで残す。負の値</summary>
        public const float Behind = -30f;

        /// <summary>舗装の半幅。道幅 7.0</summary>
        public const float RoadHalf = 3.5f;
        /// <summary>未舗装の轍の半幅。道幅 4.6。帯 4 だけ道がここまで細る</summary>
        public const float DirtHalf = 2.3f;
        /// <summary>路肩。片側</summary>
        public const float Shoulder = 1.2f;
        /// <summary>路肩の落ち込み。舗装と同じ高さだと縁が読めない</summary>
        public const float ShoulderDrop = 0.05f;

        /// <summary>帯の数</summary>
        public const int Bands = 5;

        /// <summary>タイルの枚数</summary>
        public static int TileCount { get { return RoadRing.Needed(Ahead, TileLength, Behind); } }

        /// <summary>環が一周する長さ。沿道の間隔はこれを割り切ること</summary>
        public static float Span { get { return TileCount * TileLength; } }

        /// <summary>同じ形を何十も並べるので、mesh は名前で引いて使い回す</summary>
        static readonly Dictionary<string, Mesh> shapes = new Dictionary<string, Mesh>();

        // ---- 組み立て ------------------------------------------------------

        [MenuItem("HalfAware/Build the drive", false, 230)]
        public static void BuildMenu()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogError("再生中は組み直さない。止めてからもう一度");
                return;
            }
            if (!Open()) return;
            if (!AssetDatabase.IsValidFolder("Assets/Models/generated/drive"))
                AssetDatabase.CreateFolder("Assets/Models/generated", "drive");
            if (!AssetDatabase.IsValidFolder("Assets/Materials/Drive"))
                AssetDatabase.CreateFolder("Assets/Materials", "Drive");
            shapes.Clear();

            var root = Root("Drive");
            Prune(root, new[] { "Car", "Road", "Roadsides", "Oncoming", "Garage", "Items" });
            Stage();
            Car(Child(root, "Car"));
            Road(Child(root, "Road"));
            Roadsides(Child(root, "Roadsides"));
            Traffic(Child(root, "Oncoming"));
            Garage(root);
            Items(root);
            Wire(root);
            // 見直し（CheckDrive.Run）は Task 11 で足す

            Selection.activeGameObject = root.gameObject;
            Mark(root.gameObject);
            AssetDatabase.SaveAssets();
            Debug.Log(string.Format("車と道を組んだ。タイル {0} 枚 × {1} m（前 {2} / 後ろ {3}）、沿道と対向車が {4} 帯 × {0} 区切りずつ",
                TileCount, TileLength, Ahead, Behind, Bands));
        }

        /// <summary>
        /// 場面 8 のシーンを開く。無ければ作る。
        /// 別のシーンに手を入れたまま呼ばれたら何もしない。ここで開き直すと黙って消える
        /// </summary>
        static bool Open()
        {
            var active = EditorSceneManager.GetActiveScene();
            if (active.path == ScenePath) return true;
            for (var i = 0; i < SceneManager.sceneCount; i++)
            {
                var other = SceneManager.GetSceneAt(i);
                if (!other.isDirty) continue;
                Debug.LogError("開いているシーンに未保存の変更がある。保存するか捨ててからもう一度: " + other.path);
                return false;
            }
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null)
            {
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                return true;
            }
            var made = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorSceneManager.SaveScene(made, ScenePath);
            return true;
        }

        // ---- シーンの地 ----------------------------------------------------

        /// <summary>
        /// カメラ・日射し・霧。組み直すたびに結び直すので、手で触った値は残らない。
        /// 道は z 160 で終わる。霧が無いと世界の端がそのまま見える
        /// </summary>
        static void Stage()
        {
            var cam = Loose("Main Camera");
            cam.tag = "MainCamera";
            cam.transform.position = new Vector3(0f, 1.18f, 0f);
            cam.transform.rotation = Quaternion.identity;
            var c = cam.GetComponent<Camera>();
            if (c == null) c = cam.AddComponent<Camera>();
            c.clearFlags = CameraClearFlags.SolidColor;
            c.backgroundColor = new Color(0.035f, 0.040f, 0.058f);
            c.nearClipPlane = 0.05f;
            c.farClipPlane = 1000f;
            c.fieldOfView = 70f;
            if (cam.GetComponent<AudioListener>() == null) cam.AddComponent<AudioListener>();
            EditorUtility.SetDirty(c);

            var sun = Loose("Directional Light");
            sun.transform.position = new Vector3(0f, 8f, 0f);
            sun.transform.rotation = Quaternion.Euler(24f, 152f, 0f);
            var l = sun.GetComponent<Light>();
            if (l == null) l = sun.AddComponent<Light>();
            l.type = LightType.Directional;
            l.color = new Color(0.60f, 0.68f, 0.92f);
            l.intensity = 0.55f;
            l.shadows = LightShadows.Soft;
            EditorUtility.SetDirty(l);

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = new Color(0.055f, 0.060f, 0.082f);
            RenderSettings.fogDensity = 0.014f;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.070f, 0.078f, 0.105f);
            RenderSettings.ambientEquatorColor = new Color(0.055f, 0.058f, 0.078f);
            RenderSettings.ambientGroundColor = new Color(0.035f, 0.036f, 0.045f);
            RenderSettings.skybox = null;
            RenderSettings.sun = l;
        }

        // ---- 車内 ----------------------------------------------------------

        /// <summary>
        /// 運転席から見える範囲だけ組む。車の外形は要らない。原点は車の中心で、
        /// 視点は seat の (0, 1.18, 0)。寸法はすべて仮置きで、オーナーが実画面を見てから詰める
        /// </summary>
        static void Car(Transform parent)
        {
            Clear(parent);
            var trim = new Bank { Texel = 1.2f };
            var seat = new Bank { Texel = 1.2f };
            var glass = new Bank { Texel = 0.8f };

            trim.Box(new Vector3(0f, 0.92f, 0.72f), new Vector3(1.72f, 0.26f, 0.42f));
            glass.Box(new Vector3(-0.38f, 1.02f, 0.60f), new Vector3(0.34f, 0.14f, 0.03f));
            // メーターより 0.05 手前へ引く。前後を揃えると輪の向こう端が計器の面と擦れる
            Wheel(trim, new Vector3(-0.38f, 1.02f, 0.39f), 0.36f, 0.035f, 68f);
            // 上を後ろへ倒す。屋根が前へ被さる向きにすると、外が見えなくなる。
            // 上の縁は天井の板の中へ差し込む。背を縮めずに下げると、下の縁が計器盤から離れて隙間が開く
            glass.Box(new Vector3(0f, 1.279f, 0.884f), new Vector3(1.66f, 0.49f, 0.02f), Quaternion.Euler(-22f, 0f, 0f));
            trim.Box(new Vector3(-0.86f, 0.86f, 0.10f), new Vector3(0.08f, 0.72f, 1.30f));
            trim.Box(new Vector3(0.86f, 0.86f, 0.10f), new Vector3(0.08f, 0.72f, 1.30f));
            seat.Box(new Vector3(0.42f, 0.62f, -0.06f), new Vector3(0.52f, 0.10f, 0.52f));
            seat.Box(new Vector3(0.42f, 0.94f, 0.22f), new Vector3(0.52f, 0.54f, 0.10f));
            trim.Box(new Vector3(0f, 1.52f, 0.10f), new Vector3(1.72f, 0.06f, 1.60f));
            glass.Box(new Vector3(0f, 1.44f, 0.74f), new Vector3(0.28f, 0.08f, 0.02f));

            trim.Emit(parent, "CarTrim", Mat("CarTrim"), false, Generated);
            seat.Emit(parent, "CarSeat", Mat("CarSeat"), false, Generated);
            glass.Emit(parent, "CarGlass", Mat("CarGlass"), false, Generated);

            // 視点の置き場。DriveDirector へ繋ぐのは Task 9
            var eye = Child(parent, "Seat");
            eye.localPosition = new Vector3(0f, 1.18f, 0f);
            eye.localRotation = Quaternion.identity;
        }

        /// <summary>
        /// ハンドルの輪。Bank に丸い形は無いので、短い箱を円周に並べて繋ぐ。
        /// lean は x 軸まわりに倒す角。オフロード車なので輪はバスに近いところまで寝ている。
        /// 立てるとメーターを真正面から塞ぐ
        /// </summary>
        static void Wheel(Bank bank, Vector3 centre, float outer, float thick, float lean)
        {
            const int seg = 20;
            var tilt = Quaternion.Euler(lean, 0f, 0f);
            var ring = outer * 0.5f - thick * 0.5f;
            // 継ぎ目を少し重ねる。ぴったりだと角と角のあいだに隙間が見える
            var chord = 2f * Mathf.PI * ring / seg * 1.08f;
            for (var i = 0; i < seg; i++)
            {
                var a = (i + 0.5f) / seg * Mathf.PI * 2f;
                var at = new Vector3(Mathf.Cos(a) * ring, Mathf.Sin(a) * ring, 0f);
                var rot = tilt * Quaternion.Euler(0f, 0f, a * Mathf.Rad2Deg + 90f);
                bank.Box(centre + tilt * at, new Vector3(chord, thick, thick), rot);
            }
            bank.Box(centre, new Vector3(0.11f, 0.11f, 0.045f), tilt);
            // 輪だけだと宙に浮いた環にしか見えない
            for (var i = 0; i < 3; i++)
            {
                var a = (90f + i * 120f) * Mathf.Deg2Rad;
                var at = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * ring * 0.5f;
                var rot = tilt * Quaternion.Euler(0f, 0f, a * Mathf.Rad2Deg);
                bank.Box(centre + tilt * at, new Vector3(ring, 0.022f, 0.018f), rot);
            }
        }

        // ---- 道 ------------------------------------------------------------

        /// <summary>
        /// タイルを環の枠の数だけ並べる。並べる z は DriveWorld が毎フレーム出すのと
        /// 同じ式から出す。組み立てと走り出しで並びが跳ばないように
        /// </summary>
        static void Road(Transform parent)
        {
            Clear(parent);
            var n = TileCount;
            var surface = TileMesh();
            var paint = PaintMesh();
            for (var i = 0; i < n; i++)
            {
                var tile = Piece(parent, "Tile" + i, surface, Mat("Asphalt"));
                tile.localPosition = new Vector3(0f, 0f, RoadRing.Slot(i, n, TileLength, 0f, Behind));
                Piece(tile, "Line", paint, Mat("RoadLine"));
            }
        }

        /// <summary>
        /// タイル 1 枚の形。原点は手前（-z）の端で、z 方向にちょうど TileLength だけ伸びる。
        ///
        /// RoadRing.Slot が返すのは mesh の原点の z なので、この置き方から外れると
        /// 環は綺麗に並んだまま地平に穴が空く。中央に置けば前の端が半枚ぶん手前へ寄り、
        /// 奥（+z）端に置けば丸ごと一枚ぶん足りない。絶対の z を頂点に焼くのも同じで、
        /// localPosition と二重にずれる。
        ///
        /// Texel × TileLength が整数でないと継ぎ目で絵柄が途切れる。0.5 × 20 = 10
        /// </summary>
        static Mesh TileMesh()
        {
            var bank = new Bank { Texel = 0.5f };
            bank.FaceY(0f, -RoadHalf, RoadHalf, 0f, TileLength, 1);
            for (var s = 0; s < 2; s++)
            {
                var side = s == 0 ? -1f : 1f;
                var inner = RoadHalf * side;
                var outer = (RoadHalf + Shoulder) * side;
                bank.FaceY(-ShoulderDrop, Mathf.Min(inner, outer), Mathf.Max(inner, outer), 0f, TileLength, 1);
                bank.FaceX(inner, 0f, TileLength, -ShoulderDrop, 0f, s == 0 ? -1 : 1);
            }
            return Bake(bank, "RoadTile");
        }

        /// <summary>
        /// 白線。一様な灰色の帯が流れても速さが読めないので、道が動いていることは
        /// これで見せる。破線の刻みは TileLength を割り切る数にする。
        /// 割り切らないと、タイルの継ぎ目のたびに破線が一箇所だけ詰まる
        /// </summary>
        static Mesh PaintMesh()
        {
            const float lift = 0.006f;
            const float dash = 2f;
            const float step = 5f;
            var bank = new Bank { Texel = 0.5f };
            for (var s = 0; s < 2; s++)
            {
                var x = (RoadHalf - 0.18f) * (s == 0 ? -1f : 1f);
                bank.FaceY(lift, x - 0.05f, x + 0.05f, 0f, TileLength, 1);
            }
            for (var z = 0f; z + dash <= TileLength + 0.001f; z += step)
                bank.FaceY(lift, -0.06f, 0.06f, z, z + dash, 1);
            return Bake(bank, "RoadLine");
        }

        // ---- 沿道 ----------------------------------------------------------

        /// <summary>
        /// 帯ごとの入れ物と、その下の区切り。
        ///
        /// 沿道の物をタイルの子にしてはいけない。子にすると Dress が空の入れ物を
        /// 切り替えるだけになり、5 帯ぶんが同時に出る。入れ物の直下へじかに並べると
        /// Dress は効くが、今度は沿道が一切動かない。
        ///
        /// Place は dressed.childCount を環の大きさに使うので、区切りの数はタイルの枚数と
        /// ぴったり同じにし、入れ物の直下には区切り以外を置かない。
        /// 帯ぜんたいを照らす灯りを 1 つ混ぜただけで、区切りが道の継ぎ目から全部ずれる
        /// </summary>
        static void Roadsides(Transform parent)
        {
            Clear(parent);
            for (var b = 0; b < Bands; b++)
            {
                var band = Child(parent, "Band" + b);
                var slices = Slices(band);
                if (b == 0) Outskirts(slices);
                else if (b == 1) Motorway(slices);
                else if (b == 2) Trunk(slices);
                else if (b == 3) Downs(slices);
                else Furrows(slices);
                // 帯を出し分けるのは DriveWorld.Dress。組んだ直後は頭の帯だけ見せる
                band.gameObject.SetActive(b == 0);
            }
        }

        /// <summary>
        /// 対向車。沿道と同じ帯と区切りの形に割るが、入れ物は別に持つ。
        ///
        /// すれ違う車は自分の速さと相手の速さの和で近づいてくる。沿道と同じ環に乗せると
        /// 道と同じ速さでしか流れず、隣を並んで走っているようにしか見えない。
        /// DriveWorld が oncomingRate を掛けた距離でこちらだけ別に流す。
        ///
        /// 中身があるのは帯 1 だけだが、入れ物と区切りは 5 帯ぶん揃えて作る。
        /// Place は childCount を環の大きさに使うので、数が揃っていないと繋ぎ替えで狂う
        /// </summary>
        static void Traffic(Transform parent)
        {
            Clear(parent);
            var dots = Shape("Oncoming", 0.5f, b =>
            {
                b.Box(new Vector3(-0.76f, 0.72f, 0f), new Vector3(0.26f, 0.16f, 0.10f));
                b.Box(new Vector3(0.76f, 0.72f, 0f), new Vector3(0.26f, 0.16f, 0.10f));
            });
            for (var b = 0; b < Bands; b++)
            {
                var band = Child(parent, "Band" + b);
                var slices = Slices(band);
                // 60 は 180 を割り切る。割り切らないと一周に一度だけ続けざまにすれ違う
                if (b == 1)
                    Along(slices, 60f, (slice, z, k) =>
                        Piece(slice, "Oncoming" + k, dots, Glow(new Color(0.92f, 0.94f, 1f), 3.4f))
                            .localPosition = new Vector3(-2.4f, 0f, z));
                band.gameObject.SetActive(b == 0);
            }
        }

        /// <summary>
        /// 帯の入れ物の下に、タイルと同じ枚数の区切りを割る。
        /// 入れ物の直下には区切り以外を置かない。1 つ混ざるだけで環の大きさが変わる
        /// </summary>
        static Transform[] Slices(Transform band)
        {
            var n = TileCount;
            var all = new Transform[n];
            for (var i = 0; i < n; i++)
            {
                var slice = Child(band, "Slice" + i);
                slice.localPosition = new Vector3(0f, 0f, RoadRing.Slot(i, n, TileLength, 0f, Behind));
                all[i] = slice;
            }
            return all;
        }

        /// <summary>帯 0。倫敦の外れ。高架の脚とネオン、濡れた路面</summary>
        static void Outskirts(Transform[] slices)
        {
            var pier = Shape("Pier", 0.45f, b => b.Box(new Vector3(0f, 3.0f, 0f), new Vector3(1.10f, 6.0f, 1.10f)));
            var board = Shape("NeonBoard", 0.5f, b => b.Box(Vector3.zero, new Vector3(0.12f, 1.10f, 2.60f)));
            var sheen = Shape("Sheen", 0.25f, b => b.FaceY(0.012f, -RoadHalf, RoadHalf, 0f, TileLength, 1));
            var hues = new[]
            {
                new Color(1f, 0.30f, 0.34f), new Color(0.36f, 0.72f, 1f), new Color(0.44f, 1f, 0.62f),
            };

            for (var i = 0; i < slices.Length; i++)
                Piece(slices[i], "Sheen", sheen, Mat("Sheen"));
            Along(slices, 45f, (slice, z, k) => Sides("Pier" + k, 6.5f, (at, side, name) =>
                Piece(slice, name, pier, Mat("Concrete")).localPosition = new Vector3(at, 0f, z)));
            Along(slices, 30f, (slice, z, k) => Sides("Neon" + k, 5.4f, (at, side, name) =>
                Piece(slice, name, board, Glow(hues[(k + side) % hues.Length], 2.4f))
                    .localPosition = new Vector3(at, 3.2f, z)));
        }

        /// <summary>帯 1。夜の高速。街灯だけ。対向車は別の環に乗るので Traffic が持つ</summary>
        static void Motorway(Transform[] slices)
        {
            var post = Shape("LampPost", 0.4f, b =>
            {
                b.Box(new Vector3(0f, 3.6f, 0f), new Vector3(0.18f, 7.2f, 0.18f));
                b.Box(new Vector3(0f, 0.28f, 0f), new Vector3(0.38f, 0.56f, 0.38f));
                // 道の上へ差し出す腕。真上にしか光らない街灯は道を照らさない
                b.Box(new Vector3(0.62f, 7.14f, 0f), new Vector3(1.30f, 0.12f, 0.12f));
            });
            var head = Shape("LampHead", 0.5f, b => b.Box(new Vector3(1.18f, 7.02f, 0f), new Vector3(0.62f, 0.12f, 0.26f)));

            Along(slices, 36f, (slice, z, k) => Sides("Lamp" + k, 4.6f, (at, side, name) =>
            {
                var lamp = Child(slice, name);
                lamp.localPosition = new Vector3(at, 0f, z);
                // 腕は mesh の +x へ伸ばしてあるので、右側は向きを返して道へ差し出す
                lamp.localRotation = Quaternion.Euler(0f, side == 0 ? 0f : 180f, 0f);
                Piece(lamp, "Post", post, Mat("Metal"));
                Piece(lamp, "Head", head, Glow(new Color(1f, 0.72f, 0.36f), 3.0f));
            }));
        }

        /// <summary>帯 2。深夜の幹線。木立だけ</summary>
        static void Trunk(Transform[] slices)
        {
            var trees = new Mesh[3];
            for (var v = 0; v < trees.Length; v++) trees[v] = TreeMesh(v);
            // 種を決め打ちにして、組み直しても同じ画になるようにする
            var rnd = new System.Random(20260920);
            for (var s = 0; s < 2; s++)
            {
                var side = s == 0 ? -1f : 1f;
                var tag = s == 0 ? "L" : "R";
                // 右は刻みを半分ずらす。左右を同じ位置に立てると、道を挟んで木が対で並んで見える。
                // ばらつきも左右で別に引くので、揺らぎまで揃うことはない
                Scatter(slices, 12f, 3.6f, s * 6f, rnd, (slice, z, k) =>
                {
                    var t = Piece(slice, "Tree" + tag + k, trees[(k + s) % trees.Length], Mat("Tree"));
                    t.localPosition = new Vector3((5.8f + (float)rnd.NextDouble() * 1.4f) * side, 0f, z);
                    t.localRotation = Quaternion.Euler(0f, (float)rnd.NextDouble() * 360f, 0f);
                    t.localScale = Vector3.one * (0.78f + (float)rnd.NextDouble() * 0.5f);
                });
            }
        }

        /// <summary>木 1 本。夜明け前の逆光では影にしかならないので、塊だけ作る</summary>
        static Mesh TreeMesh(int variant)
        {
            return Shape("Tree" + variant, 0.35f, b =>
            {
                var rnd = new System.Random(977 * variant + 31);
                b.Box(new Vector3(0f, 1.15f, 0f), new Vector3(0.34f, 2.30f, 0.34f));
                for (var i = 0; i < 6; i++)
                {
                    var y = 2.0f + i * 0.48f;
                    var wide = 2.5f - i * 0.30f;
                    var off = ((float)rnd.NextDouble() - 0.5f) * 0.7f;
                    b.Box(new Vector3(off, y, off * 0.6f), new Vector3(wide, 0.62f, wide),
                        Quaternion.Euler(0f, (float)rnd.NextDouble() * 90f, 0f));
                }
            });
        }

        /// <summary>帯 3。明け方の丘陵。石垣と牧草地</summary>
        static void Downs(Transform[] slices)
        {
            var wall = Shape("StoneWall", 0.8f, b =>
            {
                for (var s = 0; s < 2; s++)
                {
                    // 路肩は ±4.7 まで。石垣は道の縁より外に立つものなので、跨がせない
                    var x = s == 0 ? -5.2f : 5.2f;
                    b.Box(new Vector3(x, 0.40f, TileLength * 0.5f), new Vector3(0.46f, 0.80f, TileLength));
                    b.Box(new Vector3(x, 0.85f, TileLength * 0.5f), new Vector3(0.54f, 0.10f, TileLength));
                }
            });
            // 牧草地は路肩より下げる。同じ高さだと面が重なってちらつく
            var field = Shape("Pasture", 0.12f, b =>
            {
                b.FaceY(-0.06f, -46f, -4.6f, 0f, TileLength, 1);
                b.FaceY(-0.06f, 4.6f, 46f, 0f, TileLength, 1);
            });
            for (var i = 0; i < slices.Length; i++)
            {
                Piece(slices[i], "Pasture", field, Mat("Grass"));
                Piece(slices[i], "Wall", wall, Mat("Stone"));
            }
        }

        /// <summary>帯 4。朝靄の未舗装路。麦畑と土の轍</summary>
        static void Furrows(Transform[] slices)
        {
            // 舗装のタイルはそのまま下に敷いてあるので、土の面で覆い隠す。
            // 帯 4 だけタイルを差し替える手は取らない。タイルは 1 種しか無い
            var earth = Shape("Earth", 0.2f, b => b.FaceY(0.020f, -16f, 16f, 0f, TileLength, 1));
            var ruts = Shape("Ruts", 0.3f, b =>
            {
                b.FaceY(0.026f, -DirtHalf, DirtHalf, 0f, TileLength, 1);
                b.Box(new Vector3(-0.95f, 0.030f, TileLength * 0.5f), new Vector3(0.52f, 0.02f, TileLength));
                b.Box(new Vector3(0.95f, 0.030f, TileLength * 0.5f), new Vector3(0.52f, 0.02f, TileLength));
            });
            // 2.5 は 20 を割り切るので、区切り 1 つぶんの麦をそのまま全部の区切りで使い回せる
            var wheat = Shape("Wheat", 0.4f, b =>
            {
                var rnd = new System.Random(4021);
                for (var z = 1.25f; z < TileLength; z += 2.5f)
                    for (var s = 0; s < 2; s++)
                        foreach (var x in new[] { 3.6f, 5.0f, 7.2f })
                        {
                            var at = x * (s == 0 ? -1f : 1f);
                            for (var i = 0; i < 2; i++)
                            {
                                var high = 0.90f + (float)rnd.NextDouble() * 0.20f;
                                var off = ((float)rnd.NextDouble() - 0.5f) * 0.8f;
                                b.Box(new Vector3(at + off, high * 0.5f, z + off * 0.6f),
                                    new Vector3(0.70f, high, 0.70f),
                                    Quaternion.Euler(0f, (float)rnd.NextDouble() * 90f, 0f));
                            }
                        }
            });
            for (var i = 0; i < slices.Length; i++)
            {
                Piece(slices[i], "Earth", earth, Mat("Dirt"));
                Piece(slices[i], "Ruts", ruts, Mat("Rut"));
                Piece(slices[i], "Wheat", wheat, Mat("Wheat"));
            }
        }

        // ---- 沿道の並べ方 --------------------------------------------------

        /// <summary>
        /// 環ぜんたいを spacing ごとに刻んで並べる。
        ///
        /// 環はこの長さで一周し、同じ並びを繰り返す。spacing が環の長さを割り切らないと
        /// 一周に一箇所だけ間隔が詰まり、そこだけ物が倍の速さで飛んでくるように見える
        /// </summary>
        static void Along(Transform[] slices, float spacing, System.Action<Transform, float, int> put)
        {
            if (slices.Length == 0 || spacing <= 0f) return;
            var count = Mathf.RoundToInt(Span / spacing);
            if (count <= 0 || Mathf.Abs(count * spacing - Span) > 0.001f)
            {
                Debug.LogWarning(string.Format("沿道の間隔 {0} m が環の長さ {1} m を割り切らない。並べずに飛ばす", spacing, Span));
                return;
            }
            for (var k = 0; k < count; k++) Drop(slices, k * spacing, k, put);
        }

        /// <summary>
        /// Along と同じだが、刻みから wobble まで前後にばらけさせる。
        /// 木立のように並んで見えては困るものに使う。環の上で畳むので、
        /// 一周したところで並びは繋がったままになる。
        /// phase は並び全体をずらす量で、左右で対にならないようにするのに使う
        /// </summary>
        static void Scatter(Transform[] slices, float spacing, float wobble, float phase, System.Random rnd,
            System.Action<Transform, float, int> put)
        {
            if (slices.Length == 0 || spacing <= 0f) return;
            var count = Mathf.RoundToInt(Span / spacing);
            if (count <= 0 || Mathf.Abs(count * spacing - Span) > 0.001f)
            {
                Debug.LogWarning(string.Format("沿道の間隔 {0} m が環の長さ {1} m を割り切らない。並べずに飛ばす", spacing, Span));
                return;
            }
            for (var k = 0; k < count; k++)
                Drop(slices, k * spacing + phase + ((float)rnd.NextDouble() - 0.5f) * 2f * wobble, k, put);
        }

        /// <summary>環の上の位置を、区切りの番号と区切りの中の z に割る</summary>
        static void Drop(Transform[] slices, float at, int k, System.Action<Transform, float, int> put)
        {
            var p = at % Span;
            if (p < 0f) p += Span;
            var i = Mathf.Clamp((int)(p / TileLength), 0, slices.Length - 1);
            put(slices[i], p - i * TileLength, k);
        }

        /// <summary>道の左右へ 1 つずつ。side は 0 が左、1 が右</summary>
        static void Sides(string name, float x, System.Action<float, int, string> put)
        {
            put(-x, 0, name + "L");
            put(x, 1, name + "R");
        }

        // ---- Task 9 以降 ---------------------------------------------------

        /// <summary>ガレージ。Task 9 で組む</summary>
        static void Garage(Transform root) { }

        /// <summary>調べる対象。Task 9 で立てる</summary>
        static void Items(Transform root) { }

        /// <summary>
        /// DriveWorld にタイルと沿道と対向車を渡す。private な [SerializeField] なので
        /// SerializedObject 越しに書く。DriveDirector と調べる対象の繋ぎ込みは Task 9
        /// </summary>
        static void Wire(Transform root)
        {
            var world = root.GetComponent<DriveWorld>();
            if (world == null) world = root.gameObject.AddComponent<DriveWorld>();
            var so = new SerializedObject(world);
            Fill(so.FindProperty("tiles"), Kids(root.Find("Road")));
            Fill(so.FindProperty("roadsides"), Kids(root.Find("Roadsides")));
            Fill(so.FindProperty("oncoming"), Kids(root.Find("Oncoming")));
            // 寸法は組み立てとひとつの数から出す。Inspector で別々に持つと片方だけ直して隙間が開く
            so.FindProperty("tileLength").floatValue = TileLength;
            so.FindProperty("behind").floatValue = Behind;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static Transform[] Kids(Transform parent)
        {
            var all = new Transform[parent.childCount];
            for (var i = 0; i < all.Length; i++) all[i] = parent.GetChild(i);
            return all;
        }

        static void Fill(SerializedProperty row, Transform[] all)
        {
            row.arraySize = all.Length;
            for (var i = 0; i < all.Length; i++) row.GetArrayElementAtIndex(i).objectReferenceValue = all[i];
        }

        // ---- 道具 ----------------------------------------------------------

        /// <summary>
        /// 溜めた面を mesh のアセットにして返す。Bank.Emit は場面に物まで置くが、
        /// ここで要るのは形だけ。同じ形を何十も並べるので、mesh は 1 つを使い回す
        /// </summary>
        static Mesh Bake(Bank bank, string name)
        {
            var scratch = new GameObject("BakeScratch");
            var made = bank.Emit(scratch.transform, name, null, false, Generated);
            var mesh = made != null ? made.GetComponent<MeshFilter>().sharedMesh : null;
            Object.DestroyImmediate(scratch);
            return mesh;
        }

        /// <summary>名前で引ける形。同じ名前なら二度焼かない</summary>
        static Mesh Shape(string name, float texel, System.Action<Bank> draw)
        {
            Mesh mesh;
            if (shapes.TryGetValue(name, out mesh) && mesh != null) return mesh;
            var bank = new Bank { Texel = texel };
            draw(bank);
            mesh = Bake(bank, name);
            shapes[name] = mesh;
            return mesh;
        }

        /// <summary>形を 1 つ置く</summary>
        static Transform Piece(Transform parent, string name, Mesh mesh, Material mat)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            if (mesh != null)
            {
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                go.AddComponent<MeshRenderer>().sharedMaterial = mat;
            }
            return go.transform;
        }

        static Transform Root(string name)
        {
            var go = GameObject.Find(name);
            if (go == null) go = new GameObject(name);
            go.transform.position = Vector3.zero;
            go.transform.rotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;
            return go.transform;
        }

        /// <summary>シーンに直に置く物。カメラと日射しは Drive の下に入れない</summary>
        static GameObject Loose(string name)
        {
            var go = GameObject.Find(name);
            if (go == null) go = new GameObject(name);
            go.transform.SetParent(null, true);
            return go;
        }

        /// <summary>知らない子を落とす。組み方を変えたときに前の束が残らないように</summary>
        static void Prune(Transform parent, string[] keep)
        {
            for (var i = parent.childCount - 1; i >= 0; i--)
            {
                var c = parent.GetChild(i);
                if (System.Array.IndexOf(keep, c.name) >= 0) continue;
                Object.DestroyImmediate(c.gameObject);
            }
        }

        static Transform Child(Transform parent, string name)
        {
            var t = parent.Find(name);
            if (t != null) return t;
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.transform;
        }

        static void Clear(Transform parent)
        {
            for (var i = parent.childCount - 1; i >= 0; i--) Object.DestroyImmediate(parent.GetChild(i).gameObject);
        }

        /// <summary>
        /// 素材ごとのマテリアル。組み直すたびに色と艶を結び直すので、
        /// Inspector で触った値は残らない。詰めるならこの表を直す
        /// </summary>
        static Material Mat(string name)
        {
            var path = Materials + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                m.name = name;
                AssetDatabase.CreateAsset(m, path);
            }
            Color col;
            float smooth;
            Tone(name, out col, out smooth);
            m.SetColor("_BaseColor", col);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smooth);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", name == "Metal" ? 0.50f : 0f);
            if (col.a < 1f) SeeThrough(m);
            EditorUtility.SetDirty(m);
            return m;
        }

        /// <summary>透ける面にする。向こうが見えないと車内が箱にしか見えない</summary>
        static void SeeThrough(Material m)
        {
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 0f);
            m.SetFloat("_AlphaClip", 0f);
            m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 0f);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.DisableKeyword("_ALPHATEST_ON");
            m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        }

        /// <summary>
        /// 素材ごとの色と艶。夜と明け方しか無い場面なので、地の色はどれも暗い。
        /// 明るさは帯ごとに Volume（Color Adjustments）で寄せる前提で、ここでは触らない
        /// </summary>
        static void Tone(string name, out Color col, out float smooth)
        {
            switch (name)
            {
                case "CarTrim": col = new Color(0.085f, 0.082f, 0.090f); smooth = 0.18f; break;
                case "CarSeat": col = new Color(0.115f, 0.098f, 0.090f); smooth = 0.10f; break;
                case "CarGlass": col = new Color(0.55f, 0.60f, 0.66f, 0.12f); smooth = 0.85f; break;
                case "Asphalt": col = new Color(0.115f, 0.118f, 0.132f); smooth = 0.16f; break;
                // 塗り直されていない白線。真っ白だと夜の道で浮く
                case "RoadLine": col = new Color(0.520f, 0.510f, 0.470f); smooth = 0.10f; break;
                case "Concrete": col = new Color(0.150f, 0.150f, 0.155f); smooth = 0.10f; break;
                // 濡れた路面。艶だけの面は映る物が無いと穴に見えるので、地の明るさを持たせる
                case "Sheen": col = new Color(0.100f, 0.108f, 0.128f); smooth = 0.86f; break;
                case "Metal": col = new Color(0.085f, 0.088f, 0.095f); smooth = 0.26f; break;
                case "Tree": col = new Color(0.045f, 0.042f, 0.040f); smooth = 0.08f; break;
                case "Stone": col = new Color(0.165f, 0.162f, 0.150f); smooth = 0.08f; break;
                case "Grass": col = new Color(0.062f, 0.085f, 0.052f); smooth = 0.08f; break;
                case "Wheat": col = new Color(0.215f, 0.180f, 0.095f); smooth = 0.10f; break;
                case "Dirt": col = new Color(0.135f, 0.112f, 0.085f); smooth = 0.06f; break;
                // 踏み固められた轍。地の土より暗く湿っている
                case "Rut": col = new Color(0.098f, 0.082f, 0.066f); smooth = 0.14f; break;
                default: col = new Color(0.12f, 0.12f, 0.13f); smooth = 0.30f; break;
            }
        }

        /// <summary>
        /// 自分で光って見える面。灯りを何十も置くと WebGL では持たないので、
        /// ネオンも街灯の頭も明るい Unlit で済ませる
        /// </summary>
        static Material Glow(Color col, float gain)
        {
            var key = string.Format("Glow_{0:000}_{1:000}_{2:000}_{3:00}",
                (int)(col.r * 255), (int)(col.g * 255), (int)(col.b * 255), (int)(gain * 10));
            var path = Materials + key + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m != null) return m;
            m = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            m.name = key;
            m.SetColor("_BaseColor", new Color(col.r * gain, col.g * gain, col.b * gain, 1f));
            AssetDatabase.CreateAsset(m, path);
            return m;
        }

        static void Mark(GameObject go)
        {
            EditorUtility.SetDirty(go);
            EditorSceneManager.MarkSceneDirty(go.scene);
        }
    }
}
