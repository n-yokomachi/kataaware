using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

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
        /// <summary>
        /// 路肩の落ち込み。舗装と地続きに見せないための段。
        /// 座ったままの目線では舗装の面に隠れて段の立ち上がりそのものは見えないので、
        /// 縁を読ませているのは高さではなく色の違い（Verge）の方
        /// </summary>
        public const float ShoulderDrop = 0.05f;

        /// <summary>
        /// 道の中心の x。車は原点から動かせないので、走る車線の真ん中が原点に来るよう
        /// 道の方をずらす。英国なので左側通行。車線 1 本ぶん右へ寄せると、
        /// 中心線も対向車線も車の右側に来る。
        /// 右ハンドルの国を左ハンドルに変えるなら、この符号と車内の左右を返すだけで済む
        /// </summary>
        public const float LaneOffset = RoadHalf * 0.5f;

        /// <summary>対向車線の真ん中。世界の x</summary>
        public const float OncomingX = LaneOffset + RoadHalf * 0.5f;

        /// <summary>対向車が流れる速さ。道の何倍か。DriveWorld へそのまま渡す</summary>
        public const float OncomingRate = 2.2f;

        // ---- 路面に重ねる面の高さ ------------------------------------------
        //
        // 同じ平面に何枚も重ねるので、上下の順と間隔をここで一箇所に決める。
        // 近すぎると遠くで深度が潰れてちらつき、順を違えると下の絵が消える。
        // 手前 0.1 / 奥 1000 の深度では 100 m あたりで 6 mm ほどが限界なので、
        // 隣り合う面はどれも 8 mm 以上あける

        /// <summary>沿道より下に敷く地面。道だけが明るい帯に見えないように</summary>
        public const float VergeY = -0.12f;
        /// <summary>帯 0 の濡れた照り返し。白線の下に敷く</summary>
        public const float SheenY = 0.008f;
        /// <summary>白線。照り返しより上でないと、帯 0 だけ線が消える</summary>
        public const float PaintY = 0.020f;
        /// <summary>帯 4 の土。白線を覆い隠す高さが要る</summary>
        public const float EarthY = 0.036f;
        /// <summary>帯 4 の轍。土の上</summary>
        public const float RutY = 0.050f;

        /// <summary>空と霧の色。カメラの背景と揃える</summary>
        public static readonly Color Sky = new Color(0.055f, 0.060f, 0.082f);

        /// <summary>帯の数</summary>
        public const int Bands = 5;

        // ---- ガレージ。メートル ---------------------------------------------

        /// <summary>床の中心。車は原点にいるので、後ろへ寄せて歩く間を取る</summary>
        public static readonly Vector3 GarageAt = new Vector3(0f, 0f, -2f);
        /// <summary>床の広さ。x 方向</summary>
        public const float GarageWide = 14f;
        /// <summary>床の広さ。z 方向。柱の割り付けでいう長辺はこちら</summary>
        public const float GarageDeep = 16f;
        /// <summary>天井の高さ</summary>
        public const float GarageHigh = 2.9f;
        /// <summary>壁・床・天井の厚み</summary>
        public const float GarageThick = 0.25f;
        /// <summary>
        /// 床の面の高さ。
        ///
        /// 道のタイルは y 0 のまま z -30 から先へ敷いてあり、<see cref="PaintY"/> の
        /// 白線が 0.020 まで上がる。DriveWorld には道を伏せる手立てが無いので、
        /// ガレージの中にも舗装がそのまま重なっている。床をちょうど 0 に置くと
        /// 同じ高さの面が二枚重なってちらつくので、白線より上へ逃がす。
        /// 2 cm の段は歩いていても分からないし、壁の外からは見えない
        /// </summary>
        public const float GarageFloorY = 0.040f;
        /// <summary>柱の一辺</summary>
        public const float PillarSide = 0.35f;
        /// <summary>シャッターの幅</summary>
        public const float ShutterWide = 4.2f;
        /// <summary>シャッターの高さ</summary>
        public const float ShutterHigh = 2.6f;
        /// <summary>天井の灯りの色</summary>
        public static readonly Color GarageLamp = new Color(0.62f, 0.66f, 0.72f);

        /// <summary>立ち位置。運転席のドア側から近づく。ここから車まで約 8 m</summary>
        public static readonly Vector3 StandAt = new Vector3(4.2f, 0f, -7f);
        /// <summary>立ち位置の向き。度</summary>
        public const float StandYaw = -28f;
        /// <summary>
        /// 運転席。ハンドルの真後ろ。x 0 に座るとハンドルが横に 44 度ずれて見える。
        /// 右ハンドルなので正の側で、<see cref="LaneOffset"/> と対になっている
        /// </summary>
        public static readonly Vector3 SeatAt = new Vector3(0.38f, 1.18f, 0f);

        // ---- 調べる対象と繋ぎ先 ----------------------------------------------

        public const string ScriptPath = "Assets/Data/DriveScript.asset";
        const string ActionsPath = "Assets/InputSystem_Actions.inputactions";
        const string FontPath = "Assets/Fonts/NotoSansJP-Regular SDF.asset";

        /// <summary>車内の対象を拾える距離。座ったまま手の届く範囲</summary>
        const float ItemRadius = 1.4f;
        /// <summary>ガレージのドア。歩いて近づくので、車内の対象より少し遠くから拾える</summary>
        const float DoorRadius = 1.6f;
        /// <summary>目は顔にあるので体の前へ出す。PlayerController の eyeLead と同じ値</summary>
        const float EyeLead = 0.22f;

        /// <summary>
        /// 帯の値。<see cref="DriveIds.Triggers"/> と同じ並び。
        /// **秒数も速さもすべて仮置きで、オーナーが実画面を見てから決める。**
        /// 帯 2 の黒と明けだけ長いのが仮眠にあたる
        /// </summary>
        static readonly DriveBand[] Route =
        {
            new DriveBand { name = "倫敦の外れ", trigger = DriveIds.Chips, speed = 16f, rough = 1.0f, afterglow = 5f, black = 0.8f, fadeIn = 1.4f },
            new DriveBand { name = "夜の高速", trigger = DriveIds.Log, speed = 28f, rough = 1.0f, afterglow = 5f, black = 0.8f, fadeIn = 1.4f },
            new DriveBand { name = "深夜の幹線", trigger = DriveIds.Mirror, speed = 24f, rough = 1.0f, afterglow = 5f, black = 3.5f, fadeIn = 2.6f },
            new DriveBand { name = "明け方の丘陵", trigger = DriveIds.Photo, speed = 20f, rough = 1.6f, afterglow = 5f, black = 0.8f, fadeIn = 1.6f },
            new DriveBand { name = "朝靄の未舗装路", trigger = DriveIds.Window, speed = 11f, rough = 4.5f, afterglow = 5f, black = 0.8f, fadeIn = 1.4f },
        };

        /// <summary>帯ごとのきっかけの対象。Items が立てて Wire が DriveDirector へ渡す</summary>
        static GameObject[] triggerItems = new GameObject[0];

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
            // Stage より先に呼ぶ。Stage は Player/Main Camera があればそちらへ譲るので、
            // 後から rig を作ると札と AudioListener が二つずつになる
            Rig();
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
            Debug.Log(string.Format("ガレージ {0} × {1} × 高さ {2}。立ち位置 {3} から運転席 {4} まで約 {5:F1} m、調べる対象 {6} 個",
                GarageWide, GarageDeep, GarageHigh, StandAt.ToString("F1"), SeatAt.ToString("F2"),
                Vector3.Distance(new Vector3(StandAt.x, 0f, StandAt.z), new Vector3(SeatAt.x, 0f, SeatAt.z)),
                Object.FindObjectsByType<Interactable>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length));
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
            // カメラは本来 Player の持ち物（BuildAlley.Place と同じ構え）。場面 8 の rig は Task 9 で入る。
            // それまでの間に合わせとしてシーンに直に 1 つ置き、rig が来たらそちらへ譲る。
            // 両方に札と AudioListener を持たせると、耳が二つになって警告が出続ける
            var rig = GameObject.Find("Player/Main Camera");
            var cam = rig != null ? rig : Loose("Main Camera");
            if (rig == null)
            {
                cam.tag = "MainCamera";
                cam.transform.position = new Vector3(0f, 1.18f, 0f);
                cam.transform.rotation = Quaternion.identity;
                if (cam.GetComponent<AudioListener>() == null) cam.AddComponent<AudioListener>();
            }
            var c = cam.GetComponent<Camera>();
            if (c == null) c = cam.AddComponent<Camera>();
            c.clearFlags = CameraClearFlags.SolidColor;
            c.backgroundColor = Sky;
            // 車内で一番近いのは天井の板の 0.31 m。手前を 0.1 まで引くと、
            // 路面に重ねた面の深度の余裕がそのぶん増える
            c.nearClipPlane = 0.1f;
            c.farClipPlane = 1000f;
            c.fieldOfView = 70f;
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
            // 背景と同じ色。違えると、地面が霧に溶け切ったところに横一線の継ぎ目が出る
            RenderSettings.fogColor = Sky;
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
            // 英国なので右ハンドル。運転席が道の中心線側に来る（LaneOffset と対）
            glass.Box(new Vector3(0.38f, 1.02f, 0.60f), new Vector3(0.34f, 0.14f, 0.03f));
            // メーターより 0.05 手前へ引く。前後を揃えると輪の向こう端が計器の面と擦れる
            Wheel(trim, new Vector3(0.38f, 1.02f, 0.39f), 0.36f, 0.035f, 68f);
            // 上を後ろへ倒す。屋根が前へ被さる向きにすると、外が見えなくなる。
            // 上の縁は天井の板の中へ差し込む。背を縮めずに下げると、下の縁が計器盤から離れて隙間が開く
            glass.Box(new Vector3(0f, 1.279f, 0.884f), new Vector3(1.66f, 0.49f, 0.02f), Quaternion.Euler(-22f, 0f, 0f));
            trim.Box(new Vector3(-0.86f, 0.86f, 0.10f), new Vector3(0.08f, 0.72f, 1.30f));
            trim.Box(new Vector3(0.86f, 0.86f, 0.10f), new Vector3(0.08f, 0.72f, 1.30f));
            // 助手席は運転席の反対、道の外側
            seat.Box(new Vector3(-0.42f, 0.62f, -0.06f), new Vector3(0.52f, 0.10f, 0.52f));
            seat.Box(new Vector3(-0.42f, 0.94f, 0.22f), new Vector3(0.52f, 0.54f, 0.10f));
            trim.Box(new Vector3(0f, 1.52f, 0.10f), new Vector3(1.72f, 0.06f, 1.60f));
            glass.Box(new Vector3(0f, 1.44f, 0.74f), new Vector3(0.28f, 0.08f, 0.02f));

            trim.Emit(parent, "CarTrim", Mat("CarTrim"), false, Generated);
            seat.Emit(parent, "CarSeat", Mat("CarSeat"), false, Generated);
            glass.Emit(parent, "CarGlass", Mat("CarGlass"), false, Generated);

            // 視点の置き場。DriveDirector.seat へ繋ぐ。
            // ハンドルの真後ろに寄せてあるので、座ると輪が正面に来る
            var eye = Child(parent, "Seat");
            eye.localPosition = SeatAt;
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
            var ground = GroundMesh();
            var paint = PaintMesh();
            for (var i = 0; i < n; i++)
            {
                var tile = Piece(parent, "Tile" + i, surface, Mat("Asphalt"));
                tile.localPosition = new Vector3(0f, 0f, RoadRing.Slot(i, n, TileLength, 0f, Behind));
                Piece(tile, "Ground", ground, Mat("Verge"));
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
            bank.FaceY(0f, Lane(-RoadHalf), Lane(RoadHalf), 0f, TileLength, 1);
            return Bake(bank, "RoadTile");
        }

        /// <summary>
        /// 舗装の外の地面と路肩。タイルと同じ形なので同じ環に乗る。
        ///
        /// 舗装しか敷かないと、その外は霧の色のまま抜ける。霧は路面より明るい色なので、
        /// 道だけが地平まで明るい帯として浮き、高架の脚も木立も何も無い所に立って見える。
        /// ここを暗く塞ぐのが目的で、帯 3 の牧草地（-0.06）と帯 4 の土の下に来る高さに置く。
        ///
        /// 舗装と別の mesh にしてあるのは、路肩と地面に舗装と違う色を持たせるため。
        /// 段の立ち上がりは外向きのままにする。運転席からは舗装の面に隠れて見えないが、
        /// 内向きに返すと道の塊が裏返り、ガレージから歩いて近づいたときに中身が見える
        /// </summary>
        static Mesh GroundMesh()
        {
            var bank = new Bank { Texel = 0.25f };
            bank.FaceY(VergeY, Lane(-24f), Lane(24f), 0f, TileLength, 1);
            for (var s = 0; s < 2; s++)
            {
                var side = s == 0 ? -1f : 1f;
                var inner = Lane(RoadHalf * side);
                var outer = Lane((RoadHalf + Shoulder) * side);
                bank.FaceY(-ShoulderDrop, Mathf.Min(inner, outer), Mathf.Max(inner, outer), 0f, TileLength, 1);
                bank.FaceX(inner, 0f, TileLength, -ShoulderDrop, 0f, s == 0 ? -1 : 1);
            }
            return Bake(bank, "RoadGround");
        }

        /// <summary>
        /// 白線。一様な灰色の帯が流れても速さが読めないので、道が動いていることは
        /// これで見せる。破線の刻みは TileLength を割り切る数にする。
        /// 割り切らないと、タイルの継ぎ目のたびに破線が一箇所だけ詰まる
        /// </summary>
        static Mesh PaintMesh()
        {
            const float dash = 2f;
            const float step = 5f;
            var bank = new Bank { Texel = 0.5f };
            for (var s = 0; s < 2; s++)
            {
                var x = Lane((RoadHalf - 0.18f) * (s == 0 ? -1f : 1f));
                bank.FaceY(PaintY, x - 0.05f, x + 0.05f, 0f, TileLength, 1);
            }
            for (var z = 0f; z + dash <= TileLength + 0.001f; z += step)
                bank.FaceY(PaintY, Lane(-0.06f), Lane(0.06f), z, z + dash, 1);
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
                            .localPosition = new Vector3(OncomingX, 0f, z));
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
            var sheen = Shape("Sheen", 0.25f, b => b.FaceY(SheenY, Lane(-RoadHalf), Lane(RoadHalf), 0f, TileLength, 1));
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
                    t.localPosition = new Vector3(Lane((5.8f + (float)rnd.NextDouble() * 1.4f) * side), 0f, z);
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
                    // 路肩は道の中心から ±4.7 まで。石垣は道の縁より外に立つものなので、跨がせない
                    var x = Lane(s == 0 ? -5.2f : 5.2f);
                    b.Box(new Vector3(x, 0.40f, TileLength * 0.5f), new Vector3(0.46f, 0.80f, TileLength));
                    b.Box(new Vector3(x, 0.85f, TileLength * 0.5f), new Vector3(0.54f, 0.10f, TileLength));
                }
            });
            // 牧草地は路肩より下げる。同じ高さだと面が重なってちらつく
            var field = Shape("Pasture", 0.12f, b =>
            {
                b.FaceY(-0.06f, Lane(-46f), Lane(-(RoadHalf + Shoulder)), 0f, TileLength, 1);
                b.FaceY(-0.06f, Lane(RoadHalf + Shoulder), Lane(46f), 0f, TileLength, 1);
            });
            // 石垣は 20 m ずつの一様な押し出しなので、それだけでは何も流れて見えない。
            // 区切りに 1 つ門を入れると、区切りの長さがそのまま間隔になって必ず 180 を割り切る
            var gate = Shape("FieldGate", 0.5f, b =>
            {
                for (var i = 0; i < 2; i++)
                    b.Box(new Vector3(0f, 0.62f, i * 3.0f), new Vector3(0.16f, 1.24f, 0.16f));
                for (var i = 0; i < 3; i++)
                    b.Box(new Vector3(0f, 0.34f + i * 0.34f, 1.5f), new Vector3(0.07f, 0.07f, 3.0f));
            });
            for (var i = 0; i < slices.Length; i++)
            {
                Piece(slices[i], "Pasture", field, Mat("Grass"));
                Piece(slices[i], "Wall", wall, Mat("Stone"));
                // 運転席が右なので、近いのは左の石垣。門もそちら側に置く
                Piece(slices[i], "Gate", gate, Mat("Metal"))
                    .localPosition = new Vector3(Lane(-5.2f), 0f, 8.5f);
            }
        }

        /// <summary>帯 4。朝靄の未舗装路。麦畑と土の轍</summary>
        static void Furrows(Transform[] slices)
        {
            // 舗装のタイルはそのまま下に敷いてあるので、土の面で覆い隠す。
            // 帯 4 だけタイルを差し替える手は取らない。タイルは 1 種しか無い
            var earth = Shape("Earth", 0.2f, b => b.FaceY(EarthY, Lane(-16f), Lane(16f), 0f, TileLength, 1));
            var ruts = Shape("Ruts", 0.3f, b =>
            {
                b.FaceY(RutY, Lane(-DirtHalf), Lane(DirtHalf), 0f, TileLength, 1);
                b.Box(new Vector3(Lane(-0.95f), RutY + 0.004f, TileLength * 0.5f), new Vector3(0.52f, 0.02f, TileLength));
                b.Box(new Vector3(Lane(0.95f), RutY + 0.004f, TileLength * 0.5f), new Vector3(0.52f, 0.02f, TileLength));
            });
            // 2.5 は 20 を割り切るので、区切り 1 つぶんの麦をそのまま全部の区切りで使い回せる
            var wheat = Shape("Wheat", 0.4f, b =>
            {
                var rnd = new System.Random(4021);
                // 株は 0.70 角の箱で、y まわりに回すと角が最大 0.495 はみ出す。
                // 一番内側の列を 3.9 に置き、ばらつきも外向きだけにして、轍の側へ倒れ込ませない
                for (var z = 1.25f; z < TileLength; z += 2.5f)
                    for (var s = 0; s < 2; s++)
                        foreach (var x in new[] { 3.9f, 5.2f, 7.3f })
                        {
                            var side = s == 0 ? -1f : 1f;
                            for (var i = 0; i < 2; i++)
                            {
                                var high = 0.90f + (float)rnd.NextDouble() * 0.20f;
                                var away = (float)rnd.NextDouble() * 0.8f;
                                b.Box(new Vector3(Lane((x + away) * side), high * 0.5f,
                                        z + ((float)rnd.NextDouble() - 0.5f) * 0.5f),
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

        /// <summary>道の中心から測った x を、世界の x へ直す</summary>
        static float Lane(float x)
        {
            return LaneOffset + x;
        }

        /// <summary>道の中心から左右へ 1 つずつ。x は道の中心からの距離。side は 0 が左、1 が右</summary>
        static void Sides(string name, float x, System.Action<float, int, string> put)
        {
            put(Lane(-x), 0, name + "L");
            put(Lane(x), 1, name + "R");
        }

        // ---- ガレージ --------------------------------------------------------

        /// <summary>
        /// 車を入れてある共用のガレージ。プレイヤーは隅に立ち、歩いて運転席のドアまで来る。
        /// 乗り込んだ時点で DriveDirector が丸ごと伏せるので、入れ物はひとつにまとめる。
        ///
        /// **壁は省けない。** 道のタイルは y 0 のまま z -30 から 150 まで敷いてあり、
        /// DriveWorld には道を伏せる手立てが無い（Dress(-1) が消すのは沿道だけ）。
        /// 壁が無いと、ガレージに立っている間ずっと道が左右へ突き抜けて見える。
        ///
        /// 面はどれも Box で作る。板 1 枚で立てると内側から見た面が裏になり、
        /// 絵が出ないうえに MeshCollider の光線も素通りする
        /// </summary>
        static void Garage(Transform root)
        {
            var parent = Child(root, "Garage");
            Clear(parent);
            parent.gameObject.SetActive(true);
            parent.localPosition = Vector3.zero;

            var halfX = GarageWide * 0.5f;
            var halfZ = GarageDeep * 0.5f;
            var skin = GarageThick * 0.5f;
            // 壁は床の面から天井の面まで。中心と高さをここで一度だけ出す
            var midY = (GarageFloorY + GarageHigh) * 0.5f;
            var tall = GarageHigh - GarageFloorY;
            var front = GarageAt.z + halfZ + skin;

            var floor = new Bank { Texel = 0.5f };
            floor.Box(new Vector3(GarageAt.x, GarageFloorY - skin, GarageAt.z),
                new Vector3(GarageWide, GarageThick, GarageDeep));

            var walls = new Bank { Texel = 0.5f };
            // 長辺の壁。四隅を覆うので、z 方向は厚みのぶん伸ばす
            for (var s = 0; s < 2; s++)
            {
                var side = s == 0 ? -1f : 1f;
                walls.Box(new Vector3(GarageAt.x + side * (halfX + skin), midY, GarageAt.z),
                    new Vector3(GarageThick, tall, GarageDeep + GarageThick * 2f));
            }
            walls.Box(new Vector3(GarageAt.x, midY, GarageAt.z - halfZ - skin),
                new Vector3(GarageWide, tall, GarageThick));
            // 前の壁はシャッターの口を空けて、左右とまぐさだけ立てる
            var jamb = halfX - ShutterWide * 0.5f;
            for (var s = 0; s < 2; s++)
            {
                var side = s == 0 ? -1f : 1f;
                walls.Box(new Vector3(GarageAt.x + side * (ShutterWide * 0.5f + jamb * 0.5f), midY, front),
                    new Vector3(jamb, tall, GarageThick));
            }
            walls.Box(new Vector3(GarageAt.x, (ShutterHigh + GarageHigh) * 0.5f, front),
                new Vector3(ShutterWide, GarageHigh - ShutterHigh, GarageThick));

            var roof = new Bank { Texel = 0.5f };
            roof.Box(new Vector3(GarageAt.x, GarageHigh + skin, GarageAt.z),
                new Vector3(GarageWide, GarageThick, GarageDeep));

            // 柱は四隅と長辺の中ほど。壁から部屋の側へ出して、面の中に埋もれないようにする
            var posts = new Bank { Texel = 0.5f };
            var inset = halfX - PillarSide * 0.5f;
            var rows = new[] { GarageAt.z - halfZ + PillarSide * 0.5f, GarageAt.z, GarageAt.z + halfZ - PillarSide * 0.5f };
            for (var s = 0; s < 2; s++)
            {
                var side = s == 0 ? -1f : 1f;
                for (var i = 0; i < rows.Length; i++)
                    posts.Box(new Vector3(GarageAt.x + side * inset, midY, rows[i]),
                        new Vector3(PillarSide, tall, PillarSide));
            }

            // シャッターは壁の厚みの中へ収める。壁と同じ面に置くと、縁でちらつく
            var door = new Bank { Texel = 0.5f };
            door.Box(new Vector3(GarageAt.x, (GarageFloorY + ShutterHigh) * 0.5f, front),
                new Vector3(ShutterWide, ShutterHigh - GarageFloorY, GarageThick * 0.5f));

            // 当たりは壁と床に要る。歩いて出られては困るし、床が無いと落ちる
            floor.Emit(parent, "Floor", Mat("GarageFloor"), true, Generated);
            walls.Emit(parent, "Walls", Mat("GarageWall"), true, Generated);
            roof.Emit(parent, "Ceiling", Mat("Concrete"), true, Generated);
            posts.Emit(parent, "Pillars", Mat("Concrete"), true, Generated);
            door.Emit(parent, "Shutter", Mat("Shutter"), true, Generated);

            Lamps(parent);
        }

        /// <summary>
        /// 天井の灯り。2 本。強さと届く範囲は仮置きで、オーナーが実画面を見てから決める。
        /// 影を落とさせないのは、WebGL で影を持つ灯りを増やすと重くなるため
        /// </summary>
        static void Lamps(Transform parent)
        {
            var tube = Shape("GarageTube", 0.5f, b => b.Box(Vector3.zero, new Vector3(0.18f, 0.08f, 2.40f)));
            var lit = Glow(GarageLamp, 1.6f);
            for (var s = 0; s < 2; s++)
            {
                var lamp = Child(parent, "Lamp" + s);
                lamp.localPosition = new Vector3(s == 0 ? -3.0f : 3.0f, 2.8f, -2.0f);
                Piece(lamp, "Tube", tube, lit);
                var bulb = new GameObject("Light");
                bulb.transform.SetParent(lamp, false);
                var l = bulb.AddComponent<Light>();
                l.type = LightType.Point;
                l.color = GarageLamp;
                l.intensity = 2.6f;
                l.range = 16f;
                l.shadows = LightShadows.None;
            }
        }

        // ---- 調べる対象 ------------------------------------------------------

        /// <summary>
        /// 調べる対象。判定点は物の少し手前に置く。位置は右ハンドルの車のもので、
        /// 運転席のドアと窓は x の正の側、助手席と上着のポケットは負の側にある。
        /// <see cref="LaneOffset"/> を裏返して左ハンドルにするなら、ここの x もまとめて裏返す。
        ///
        /// **必須にするのは garage.door と drive.window の 2 つだけ。** 最後の帯で窓の独白を
        /// 送り切ったところで SceneFlow が場面を閉じる前提になっている。ほかに必須を足すと、
        /// 閉じられないまま BandClock が一巡して戻り、古い入れ替えの知らせで
        /// 範囲の外を並べにいって走りが止まる。
        ///
        /// **二択を出すのも garage.door だけ。** SceneFlow.CloseChoice は最後に
        /// player.CanMove = standUp == null || standUp.Standing と書く。standAfter が空の
        /// この場面では standUp が null なので、どの二択を閉じてもプレイヤーが歩けるようになる。
        /// ドアは直後に Board が false へ戻すので構わないが、ほかの対象に二択を足すと
        /// 走っている車から歩いて降りられる。DriveDirector は Board のあと二度と CanMove を書かない
        /// </summary>
        static void Items(Transform root)
        {
            var parent = Child(root, "Items");
            Clear(parent);
            var script = AssetDatabase.LoadAssetAtPath<RoomScript>(ScriptPath);
            if (script == null)
                Debug.LogWarning("場面 8 の文面が無い。先に HalfAware/Write the drive script を走らせる: " + ScriptPath);

            // ガレージのドアだけは歩いて近づくので、ほかより遠くから拾える
            Put(parent, "Door", new Vector3(0.92f, 1.05f, 0.10f), script, DriveIds.Door, DoorRadius, true);

            triggerItems = new GameObject[Bands];
            triggerItems[0] = Put(parent, "Chips", new Vector3(-0.42f, 0.78f, -0.02f), script, DriveIds.Chips, ItemRadius, false);
            triggerItems[1] = Put(parent, "Log", new Vector3(-0.42f, 0.80f, 0.24f), script, DriveIds.Log, ItemRadius, false);
            triggerItems[2] = Put(parent, "Mirror", new Vector3(0f, 1.42f, 0.72f), script, DriveIds.Mirror, ItemRadius, false);
            triggerItems[3] = Put(parent, "Photo", new Vector3(0.16f, 1.00f, 0.62f), script, DriveIds.Photo, ItemRadius, false);
            // 窓だけ必須。帯 4 に入るまで伏せてあるので、それまで場面は閉じない
            triggerItems[4] = Put(parent, "Window", new Vector3(0.84f, 1.00f, 0.10f), script, DriveIds.Window, ItemRadius, true);
            // きっかけはその帯に入るまで出さない。出し分けるのは DriveDirector.ShowTrigger
            for (var i = 0; i < triggerItems.Length; i++) triggerItems[i].SetActive(false);

            // 帯を問わず置く、読んでも帯が進まない対象
            Put(parent, "Radio", new Vector3(-0.02f, 0.96f, 0.70f), script, DriveIds.Radio, ItemRadius, false);
            Put(parent, "Pocket", new Vector3(-0.10f, 0.70f, -0.30f), script, DriveIds.Pocket, ItemRadius, false);
            Put(parent, "Fuel", new Vector3(0.46f, 1.02f, 0.58f), script, DriveIds.Fuel, ItemRadius, false);
        }

        /// <summary>
        /// 調べる対象をひとつ立てる。どれも一度調べたら終わりで、前提は持たない。
        /// 二択は文面の側（DriveScript）が持つので、ここでは何も決めない
        /// </summary>
        static GameObject Put(Transform parent, string name, Vector3 at, RoomScript script,
            string id, float radius, bool required)
        {
            var go = new GameObject("Interactable_" + name);
            go.transform.SetParent(parent, false);
            go.transform.position = at;
            // Interactable の OnValidate は AddComponent の中で走るので、
            // ここで「id がない」と一度警告が出る。id はこの直後に入れている
            var item = go.AddComponent<Interactable>();
            var so = new SerializedObject(item);
            so.FindProperty("id").stringValue = id;
            so.FindProperty("script").objectReferenceValue = script;
            so.FindProperty("radius").floatValue = radius;
            so.FindProperty("required").boolValue = required;
            so.FindProperty("once").boolValue = true;
            so.FindProperty("after").arraySize = 0;
            so.ApplyModifiedPropertiesWithoutUndo();
            return go;
        }

        // ---- プレイヤーと画面と進行 -------------------------------------------

        /// <summary>
        /// プレイヤーの rig・画面・進行の入れ物。ほかの場面では手で置いてあるものを、
        /// 場面 8 は空のシーンから組み上げるのでここで作る。
        ///
        /// 組み直すたびに作り直す。手で触った値は残らないが、そのぶん
        /// どの端末で組んでも同じものが立つ。Stage より先に呼ぶこと
        /// </summary>
        static void Rig()
        {
            // Task 8 が間に合わせに置いた根のカメラを落とす。
            // 残したまま rig を足すと、MainCamera の札と AudioListener が二つずつになる
            Drop("Main Camera");
            Drop("Player");
            Drop("Hud");
            Drop("SceneFlow");

            var player = new GameObject("Player");
            var body = player.AddComponent<CharacterController>();
            body.height = 1.7f;
            body.radius = 0.3f;
            body.center = new Vector3(0f, 0.85f, 0f);
            body.slopeLimit = 45f;
            body.stepOffset = 0.3f;
            body.skinWidth = 0.08f;
            body.minMoveDistance = 0.001f;

            var eye = new GameObject("Main Camera");
            eye.transform.SetParent(player.transform, false);
            eye.transform.localPosition = new Vector3(0f, PlayerController.StandingEyeHeight, EyeLead);
            eye.transform.localRotation = Quaternion.identity;
            eye.tag = "MainCamera";
            eye.AddComponent<Camera>();
            // 耳は場面にひとつだけ。カメラと同じ所へ付ける
            eye.AddComponent<AudioListener>();

            var walker = player.AddComponent<PlayerController>();
            var pso = new SerializedObject(walker);
            var actions = AssetDatabase.LoadAssetAtPath<UnityEngine.InputSystem.InputActionAsset>(ActionsPath);
            if (actions == null) Debug.LogWarning("入力の割り当てが無い: " + ActionsPath);
            pso.FindProperty("actions").objectReferenceValue = actions;
            pso.FindProperty("eye").objectReferenceValue = eye.transform;
            pso.FindProperty("eyeLead").floatValue = EyeLead;
            pso.ApplyModifiedPropertiesWithoutUndo();

            // 当たりを入れたまま動かすと床や壁に押し出されて狙った場所に立たない（BuildAlley.Place と同じ）
            body.enabled = false;
            player.transform.position = new Vector3(StandAt.x, GarageFloorY + 0.06f, StandAt.z);
            player.transform.rotation = Quaternion.Euler(0f, StandYaw, 0f);
            body.enabled = true;

            Flow(walker, Screen());
        }

        /// <summary>同じ名前の根を落とす</summary>
        static void Drop(string name)
        {
            foreach (var go in SceneManager.GetActiveScene().GetRootGameObjects())
                if (go.name == name) Object.DestroyImmediate(go);
        }

        /// <summary>
        /// 字幕・印・暗転・幕・ログ。作りも値もほかの場面の Hud に揃えてある。
        /// 並び順がそのまま重なりの順になるので、暗転より後に中央の文字を置く
        /// </summary>
        static HudView Screen()
        {
            var go = new GameObject("Hud", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(HudView));
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            if (font == null) Debug.LogWarning("字の形が無い: " + FontPath);

            var band = Layer(go.transform, "SubtitleBand", new Color(0f, 0f, 0f, 0.75f), true);
            Frame(band, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 40f), new Vector2(0f, 120f));
            var subtitle = Line(band, "Subtitle", font, 28f, Color.white, TextAlignmentOptions.Left);
            Frame(subtitle.rectTransform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(-160f, -24f));

            var prompt = Line(go.transform, "Prompt", font, 22f, Color.white, TextAlignmentOptions.Center);
            Frame(prompt.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, -40f), new Vector2(800f, 40f));

            var log = Layer(go.transform, "LogPanel", new Color(0.02f, 0.02f, 0.025f, 0.88f), true);
            Frame(log, new Vector2(0.07f, 0.07f), new Vector2(0.93f, 0.93f), new Vector2(0.5f, 0.5f),
                Vector2.zero, Vector2.zero);
            var title = Line(log, "Title", font, 22.4f, new Color(0.62f, 0.64f, 0.68f), TextAlignmentOptions.TopLeft);
            title.text = "ログ　　Tab で閉じる";
            Frame(title.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -22f), new Vector2(-76f, 48f));
            var logText = Line(log, "Text", font, 24.08f, new Color(0.86f, 0.87f, 0.89f), TextAlignmentOptions.TopLeft);
            Frame(logText.rectTransform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
                new Vector2(0f, -28f), new Vector2(-80f, -116f));
            log.gameObject.SetActive(false);

            var fade = Layer(go.transform, "Fade", new Color(0f, 0f, 0f, 0f), false);
            Frame(fade, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            var curtain = Layer(go.transform, "Curtain", new Color(0f, 0f, 0f, 1f), false);
            Frame(curtain, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            curtain.gameObject.SetActive(false);

            var centre = Line(go.transform, "Center", font, 40f, Color.white, TextAlignmentOptions.Center);
            Frame(centre.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(1000f, 80f));

            var hud = go.GetComponent<HudView>();
            var so = new SerializedObject(hud);
            so.FindProperty("subtitleBand").objectReferenceValue = band.gameObject;
            so.FindProperty("subtitleText").objectReferenceValue = subtitle;
            so.FindProperty("subtitleRowHeight").floatValue = 44f;
            so.FindProperty("subtitlePadding").floatValue = 34f;
            so.FindProperty("promptText").objectReferenceValue = prompt;
            so.FindProperty("centerText").objectReferenceValue = centre;
            so.FindProperty("fadeLayer").objectReferenceValue = fade.GetComponent<Image>();
            so.FindProperty("curtainLayer").objectReferenceValue = curtain.GetComponent<Image>();
            so.FindProperty("logPanel").objectReferenceValue = log.gameObject;
            so.FindProperty("logText").objectReferenceValue = logText;
            so.ApplyModifiedPropertiesWithoutUndo();
            return hud;
        }

        /// <summary>塗り潰しの層</summary>
        static RectTransform Layer(Transform parent, string name, Color col, bool blocks)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.color = col;
            img.raycastTarget = blocks;
            return go.GetComponent<RectTransform>();
        }

        /// <summary>文字の層</summary>
        static TMP_Text Line(Transform parent, string name, TMP_FontAsset font, float size, Color col, TextAlignmentOptions align)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var text = go.GetComponent<TextMeshProUGUI>();
            if (font != null) text.font = font;
            text.fontSize = size;
            text.color = col;
            text.alignment = align;
            text.text = "";
            return text;
        }

        /// <summary>層の位置と大きさ。アンカーとピボットをまとめて決める</summary>
        static void Frame(RectTransform rect, Vector2 min, Vector2 max, Vector2 pivot, Vector2 at, Vector2 size)
        {
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.pivot = pivot;
            rect.anchoredPosition = at;
            rect.sizeDelta = size;
        }

        /// <summary>
        /// 場面の進行。SceneFlow と DriveDirector を同じ入れ物に置く（AlleyDirector と同じ構え）。
        ///
        /// **standAfter は空のままにする。** 何か入れると Stand が毎フレーム player.CanMove を
        /// 書くので、Board が false にしたものを歩ける側へ戻してしまう。
        /// 車内は座ったままなので、立ち上がりの段取りはそもそも要らない
        /// </summary>
        static SceneFlow Flow(PlayerController player, HudView hud)
        {
            var go = new GameObject("SceneFlow");
            var flow = go.AddComponent<SceneFlow>();
            go.AddComponent<DriveDirector>();
            var so = new SerializedObject(flow);
            so.FindProperty("player").objectReferenceValue = player;
            so.FindProperty("hud").objectReferenceValue = hud;
            // 眩暈は場面 1 のもの。車内では使わない
            so.FindProperty("daze").objectReferenceValue = null;
            so.FindProperty("maxAngle").floatValue = InteractionPicker.MaxAngle;
            // 次の場面（場面 9）はまだ無い。必須を済ませたら「続く」で止まる
            so.FindProperty("nextScene").stringValue = "";
            so.FindProperty("openingCard").stringValue = "";
            so.FindProperty("standAfter").stringValue = "";
            so.FindProperty("standSpot").objectReferenceValue = null;
            so.FindProperty("chairBlocker").objectReferenceValue = null;
            so.FindProperty("chair").objectReferenceValue = null;
            so.FindProperty("body").objectReferenceValue = null;
            so.FindProperty("exitSound").objectReferenceValue = null;
            so.FindProperty("cutToBlack").boolValue = false;
            so.FindProperty("dazeUntil").stringValue = "";
            so.ApplyModifiedPropertiesWithoutUndo();
            return flow;
        }

        // ---- 繋ぎ込み --------------------------------------------------------

        /// <summary>
        /// DriveWorld にタイルと沿道と対向車を、DriveDirector にプレイヤー・画面・文面・
        /// ガレージ・運転席・帯を渡す。private な [SerializeField] なので
        /// SerializedObject 越しに書く
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
            so.FindProperty("oncomingRate").floatValue = OncomingRate;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(world);

            var flow = Object.FindFirstObjectByType<SceneFlow>();
            if (flow == null) { Debug.LogWarning("SceneFlow が無い。DriveDirector を繋げない"); return; }
            var director = flow.GetComponent<DriveDirector>();
            if (director == null) director = flow.gameObject.AddComponent<DriveDirector>();
            var dso = new SerializedObject(director);
            dso.FindProperty("flow").objectReferenceValue = flow;
            dso.FindProperty("hud").objectReferenceValue = Object.FindFirstObjectByType<HudView>();
            dso.FindProperty("player").objectReferenceValue = Object.FindFirstObjectByType<PlayerController>();
            dso.FindProperty("script").objectReferenceValue = AssetDatabase.LoadAssetAtPath<RoomScript>(ScriptPath);
            dso.FindProperty("world").objectReferenceValue = world;
            // 乗り込んだら丸ごと伏せる。ガレージの中身はこの入れ物ひとつに収めてある
            var garage = Look(root, "Garage");
            dso.FindProperty("garage").objectReferenceValue = garage != null ? garage.gameObject : null;
            dso.FindProperty("seat").objectReferenceValue = Look(root, "Car/Seat");
            FillBands(dso.FindProperty("bands"));
            var picked = dso.FindProperty("triggers");
            picked.arraySize = triggerItems.Length;
            for (var i = 0; i < triggerItems.Length; i++)
                picked.GetArrayElementAtIndex(i).objectReferenceValue = triggerItems[i];
            dso.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(director);
            EditorUtility.SetDirty(flow);
        }

        /// <summary>
        /// 帯の値を書き込む。DriveBand は struct なので、配列の要素ごとに中身を並べ直す。
        /// 秒数はすべて仮置きで、オーナーが再生しながら Inspector で決める
        /// </summary>
        static void FillBands(SerializedProperty row)
        {
            row.arraySize = Route.Length;
            for (var i = 0; i < Route.Length; i++)
            {
                var e = row.GetArrayElementAtIndex(i);
                e.FindPropertyRelative("name").stringValue = Route[i].name;
                e.FindPropertyRelative("trigger").stringValue = Route[i].trigger;
                e.FindPropertyRelative("speed").floatValue = Route[i].speed;
                e.FindPropertyRelative("rough").floatValue = Route[i].rough;
                e.FindPropertyRelative("afterglow").floatValue = Route[i].afterglow;
                e.FindPropertyRelative("black").floatValue = Route[i].black;
                e.FindPropertyRelative("fadeIn").floatValue = Route[i].fadeIn;
            }
        }

        /// <summary>繋ぎ先を引く。黙って null を渡すと、再生して初めて気づくことになる</summary>
        static Transform Look(Transform root, string path)
        {
            var t = root.Find(path);
            if (t == null) Debug.LogWarning("繋ぎ先が見つからない: " + path);
            return t;
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

        /// <summary>
        /// シーンに直に置く物。カメラと日射しは Drive の下に入れない。
        /// 根だけを見るのは、GameObject.Find が Player の下の同じ名前を掴んでしまうため。
        /// 掴んだうえで根へ引き出すと、rig からカメラを抜き取ることになる
        /// </summary>
        static GameObject Loose(string name)
        {
            foreach (var go in SceneManager.GetActiveScene().GetRootGameObjects())
                if (go.name == name) return go;
            return new GameObject(name);
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
            // 絵があれば貼る。無ければ色だけ。組み直すたびに結び直すので、
            // Assets/Textures へ置くだけで差し替わる（BuildAlley と同じ構え）
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Textures/Drive" + name + ".png");
            if (tex != null)
            {
                m.SetTexture("_BaseMap", tex);
                m.SetColor("_BaseColor", new Color(1f, 1f, 1f, col.a));
            }
            else
            {
                m.SetTexture("_BaseMap", null);
                m.SetColor("_BaseColor", col);
            }
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
                // 舗装の外の地面と路肩。舗装より暗く、少し土を帯びた色にして道の縁を読ませる
                case "Verge": col = new Color(0.078f, 0.074f, 0.066f); smooth = 0.06f; break;
                case "Concrete": col = new Color(0.150f, 0.150f, 0.155f); smooth = 0.10f; break;
                // ガレージ。共用の車庫なので、床は油が染みて壁の塗りも褪せている
                case "GarageFloor": col = new Color(0.098f, 0.096f, 0.094f); smooth = 0.14f; break;
                case "GarageWall": col = new Color(0.128f, 0.128f, 0.133f); smooth = 0.07f; break;
                case "Shutter": col = new Color(0.155f, 0.150f, 0.140f); smooth = 0.24f; break;
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
