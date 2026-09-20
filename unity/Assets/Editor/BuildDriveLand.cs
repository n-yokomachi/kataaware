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
    /// 場面 8 の道と沿道と空。タイルの環に乗る物はすべてここで作る。
    /// 寸法の定数と組み立ての入口は <see cref="BuildDrive"/> 本体にある
    /// </summary>
    public static partial class BuildDrive
    {
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

        // ---- 空に浮かべる雲 ---------------------------------------------------

        /// <summary>
        /// 帯ごとの空の物。今は雲だけ。
        ///
        /// 帯 0 は原作の「クラッカーたちが撒き散らすクラック用のナノマシンの黒雲」、
        /// 帯 4 は「倫敦のナノマシンの黒雲とは違い、真っ白な千切れ雲」。
        /// **この二つは対になっている。** 片方だけ外すと、原作が対比で書いている
        /// 倫敦と田舎町の差が、色の違いだけになる。
        ///
        /// 沿道の入れ物には入れられない。あの下には区切りしか置けず（<see cref="CheckDrive"/>）、
        /// 区切りに入れれば道と同じ速さで手前へ流れて、雲まで時速 40 km で走ることになる。
        /// 動きは <see cref="CloudDrift"/> が絵のほうをずらして出す。
        ///
        /// **板の広さには上限がある。** カメラの奥は 1000 m なので、四隅までの距離が
        /// そこを越えると空が切り落とされて直線が出る。半幅は 700 m までに留めること
        /// </summary>
        static void Clouds(Transform root)
        {
            var parent = Child(root, "Sky");
            Clear(parent);
            var nano = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Textures/CloudLayer.png");
            var torn = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Textures/CloudTorn.png");
            var high = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Textures/SkyHigh.png");
            if (nano == null || torn == null) Debug.LogWarning("雲の絵が無い");
            for (var b = 0; b < Bands; b++)
            {
                var band = Child(parent, "Band" + b);
                // ナノマシンの黒雲。低く垂れて、高架の上をそのまま塞ぐ
                if (b == 0 && nano != null)
                    Deck(band, "Nano", nano, 30f, 420f, 0.010f,
                        new Color(0.030f, 0.026f, 0.042f, 0.94f), 0.078f, 0.30f, new Vector2(0.0034f, 0.0012f));
                // 帯 3 の薄明。**暗い側を板にする。** 帯 4 の「高い空」（High）と同じ構えで、
                // 塗り潰しに地平の明るい色を置き、そこへ上ほど濃い暗い板を重ねる。
                // 逆を取ると、板の縁の外の仰角（26 / 680√2 ＝ 1.6 度）に地平の色が帯で残る。
                //
                // 絵は帯 4 と同じ SkyHigh を使い回す。要るのは一様な地と薄い斑だけで、
                // 色は板の側（_BaseColor）が持つ。夜明け前の天頂は青紫で、日の出の側から
                // 遠いほど暗い。ここは一枚の板なので向きは持てないが、
                // 傾き（地平が明るく天頂が暗い）が出れば薄明には見える
                else if (b == 3 && high != null)
                    Deck(band, "Deep", high, 26f, 680f, 0.0008f,
                        new Color(0.075f, 0.080f, 0.130f, 0.92f), 0.085f, 0.34f, Vector2.zero, 2940);
                else if (b == Bands - 1 && torn != null)
                {
                    // 高い空。**青のほうを板にする。**
                    //
                    // 空はカメラの塗り潰しの一色なので、そのままでは地平も天頂も同じ色になる。
                    // 原作の「まだ薄青い高い空」を出すには、上を深い青に、地平を朝靄の白にしたい。
                    // 長らく逆を取って、青い塗り潰しの上へ地平の側を白く抜く板を敷いていた。
                    //
                    // **それでは地平に生の青が残る。** 板は水平な有限の面なので、
                    // 届く仰角には必ず下限がある（26 / 680√2 ＝ 1.6 度）。その下は塗り潰しの
                    // 色が出るので、道の先のように稜線が視界を塞がない向きでは、
                    // 地平の上に 1.6 度ぶんの青い帯が硬い縁を引いて残った。
                    // 塗り潰しを靄の白にして青を板へ移せば、板の縁の外はそのまま靄になる。
                    // 薄れを 0 から始める仰角（0.085 ＝ 4.9 度）を板の縁より上に取ってあるので、
                    // 縁のところで色が動かない。空のどこにも境目が無くなる。
                    //
                    // 0.045（2.6 度）から 0.085 へ上げた。稜線は仰角 5.1 度に出るので、
                    // 2.6 度から青を混ぜ始めると稜線の真上にもう青が乗り、実際に測ると
                    // 稜線 (182,182,188) に対して空が (155,164,195) と暗くなって、
                    // 地平が空より明るいという逆さまの絵になっていた。靄がいちばん厚いのは
                    // 視線が水平に近いところで、そこは空も畑も同じ白へ寄るのが正しい。
                    //
                    // 濃さのままになるのは 0.26（15 度）。屋根に切られた窓は上が 21 度なので、
                    // 見上げた先には深い青が残り、稜線のあたりだけが靄で白く抜ける。
                    //
                    // **雲より先に塗る。** 並び順は距離で決まり、この板（26 m）は雲（88 / 155 m）
                    // より手前になるので、放っておくと青が雲の上に乗って千切れ雲が沈む
                    if (high != null)
                        Deck(band, "High", high, 26f, 680f, 0.0008f,
                            new Color(0.400f, 0.520f, 0.800f, 1f), 0.085f, 0.26f, Vector2.zero, 2940);
                    // 千切れ雲。二層に分けるのは、遠近だけでは高さが出ないため。
                    // 別々の速さで流れる二枚が重なって初めて「高い空」に見える。
                    // **絵の刻みは粗く取る。** 細かく繰り返すと、浅い角度で見たときに
                    // 雲ではなく空に散った点々になる。1 枚がおよそ 160 m になる刻みにしてある
                    Deck(band, "Torn0", torn, 88f, 680f, 0.0062f,
                        new Color(1f, 1f, 1f, 0.90f), 0.135f, 0.42f, new Vector2(0.0016f, 0.0006f));
                    Deck(band, "Torn1", torn, 155f, 680f, 0.0034f,
                        new Color(0.94f, 0.96f, 1f, 0.55f), 0.232f, 0.58f, new Vector2(0.0008f, 0.0003f));
                }
                // 出し分けるのは DriveDirector。組んだ直後は頭の帯だけ見せる
                band.gameObject.SetActive(b == 0);
            }
        }

        /// <summary>
        /// 雲の層 1 枚。下から見上げるので面は下へ向ける。
        ///
        /// fade は絵が消え切る仰角の sin、full は濃さのままになる仰角の sin。
        /// **fade は板の縁の仰角（high / 半幅）より上に取ること。**
        /// 下回ると、空を横切る板の縁がそのまま線になって出る。
        ///
        /// 上下を入れ替えて渡してもよい。下ほど濃い板は fade > full で渡す。
        /// シェーダーはどちらの向きも受ける。
        ///
        /// queue は透ける面どうしの並び。既定（2950）では距離で決まるので、
        /// 低く敷いた板ほど後から塗られる
        /// </summary>
        static void Deck(Transform parent, string name, Texture2D tex, float high, float half, float texel,
            Color tint, float fade, float full, Vector2 drift, int queue = 2950)
        {
            var bank = new Bank { Texel = texel };
            bank.FaceY(high, -half, half, -half, half, -1);
            var go = bank.Emit(parent, name, CloudMat(name, tex, tint, fade, full, queue), false, Generated);
            if (go == null) return;
            var drifter = go.AddComponent<CloudDrift>();
            var so = new SerializedObject(drifter);
            so.FindProperty("speed").vector2Value = drift;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>
        /// 雲のマテリアル。層ごとに 1 枚ずつ作る。CloudDrift が層ごとに別の絵をずらすので、
        /// 共有すると全部の層が一緒に動く
        /// </summary>
        static Material CloudMat(string name, Texture2D tex, Color tint, float fade, float full, int queue)
        {
            var path = Materials + "Cloud" + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(Shader.Find("HalfAware/SkyCloud"));
                m.name = "Cloud" + name;
                AssetDatabase.CreateAsset(m, path);
            }
            // 組み直すたびに結び直す。手で触った値は残らない
            m.shader = Shader.Find("HalfAware/SkyCloud");
            m.SetTexture("_BaseMap", tex);
            m.SetColor("_BaseColor", tint);
            m.SetFloat("_FadeAt", fade);
            m.SetFloat("_FullAt", full);
            // 透ける面どうしの並びは、同じ待ち行列なら距離で決まる。低く敷いた板ほど
            // 手前になるので、高い空の板は待ち行列のほうを繰り上げて雲の先に塗る
            m.renderQueue = queue;
            EditorUtility.SetDirty(m);
            return m;
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

            // 濡れた舗装に落ちるネオンの映り込み。**看板と同じ色を、同じ割り付けで引く。**
            // 板そのものは色を持たず、マテリアルが看板ごとに 1 枚ずつ要る
            var smear = Card("NeonSmear", SmearWide, SmearDeep);
            var smears = new Material[hues.Length];
            for (var h = 0; h < hues.Length; h++)
                smears[h] = GlowMat("NeonSmear" + h, "Smear", hues[h], 1.05f, SmearWide, SmearDeep);

            for (var i = 0; i < slices.Length; i++)
                Piece(slices[i], "Sheen", sheen, Mat("Sheen"));
            Along(slices, 45f, (slice, z, k) => Sides("Pier" + k, 6.5f, (at, side, name) =>
                Piece(slice, name, pier, Mat("Concrete")).localPosition = new Vector3(at, 0f, z)));
            Along(slices, 30f, (slice, z, k) => Sides("Neon" + k, 5.4f, (at, side, name) =>
                // 街灯の頭と同じ理由で 2.4 から落とす。ネオンは色が命で、
                // 白く飛んだ看板は蛍光灯にしか見えない
                Piece(slice, name, board, Glow(hues[(k + side) % hues.Length], 1.25f))
                    .localPosition = new Vector3(at, 3.2f, z)));
            // 映り込みは看板の足元から手前へ伸びる。看板は道の外（±5.4）に立っているので、
            // 舗装の縁（±3.5）へ寄せて落とす。艶（Sheen）の上に乗るのが正しい順で、
            // 板の高さ（GlowY 0.028）は白線より上に取ってある
            Along(slices, 30f, (slice, z, k) => Sides("Smear", 3.0f, (at, side, name) =>
                Piece(slice, name, smear, smears[(k + side) % hues.Length])
                    .localPosition = new Vector3(at, 0f, z)));
        }

        /// <summary>ネオンの映り込みの板の幅。濡れた舗装の映り込みは看板より広がる</summary>
        const float SmearWide = 3.2f;
        /// <summary>ネオンの映り込みの板の長さ。看板の間隔（30）より短くする</summary>
        const float SmearDeep = 20f;

        /// <summary>街灯の橙。頭も溜まりも同じ色から出す</summary>
        static readonly Color LampHue = new Color(1f, 0.72f, 0.36f);
        /// <summary>街灯の間隔。m。180 を割り切ること。28 m/s で 1.3 秒ごとに 1 本すれ違う</summary>
        const float LampStep = 36f;
        /// <summary>街灯の柱の、道の中心からの距離</summary>
        const float LampAt = 4.6f;
        /// <summary>柱から灯りの頭までの、道の上への差し出し</summary>
        const float LampArm = 1.18f;
        /// <summary>街灯が路面に落とす溜まりの幅</summary>
        const float PoolWide = 12f;
        /// <summary>街灯が路面に落とす溜まりの長さ。間隔（36）より短くする。溜まりが繋がると流れが消える</summary>
        const float PoolDeep = 22f;

        /// <summary>
        /// 帯 1。夜の高速。街灯と、街灯が路面に落とす橙の溜まり。
        /// 対向車は別の環に乗るので Traffic が持つ。
        ///
        /// 設計書の「街灯の橙が一定の間隔で流れる」を出しているのは溜まりの方で、
        /// 頭ではない。**頭だけ光らせていた頃は、遠くに橙の点が一つ見えるだけだった。**
        /// 頭は 0.62 × 0.12 m しかなく、36 m 先では低い描画解像度で 4 × 1 画素に届かない。
        /// そのうえ光る面は自分が光るだけで路面には何も落とさないので、
        /// 夜の高速で一番効くはずの「灯りの下を一定の間隔でくぐる」が丸ごと無かった
        /// </summary>
        static void Motorway(Transform[] slices)
        {
            var post = Shape("LampPost", 0.4f, b =>
            {
                b.Box(new Vector3(0f, 3.6f, 0f), new Vector3(0.18f, 7.2f, 0.18f));
                b.Box(new Vector3(0f, 0.28f, 0f), new Vector3(0.38f, 0.56f, 0.38f));
                // 道の上へ差し出す腕。真上にしか光らない街灯は道を照らさない
                b.Box(new Vector3(0.62f, 7.14f, 0f), new Vector3(1.30f, 0.12f, 0.12f));
            });
            // 灯りの頭。**0.62 × 0.12 × 0.26 から太らせた。** 遠くで画素に届かないと
            // 列にならず、点が一つ二つ瞬くだけになる。笠の下に伏せた面も足して、
            // 道側から見たときに橙の板として残るようにしてある
            var head = Shape("LampHead", 0.5f, b =>
            {
                b.Box(new Vector3(LampArm, 7.00f, 0f), new Vector3(0.78f, 0.20f, 0.42f));
                b.Box(new Vector3(LampArm, 6.86f, 0f), new Vector3(0.54f, 0.10f, 0.30f));
            });
            var pool = Card("LampPool", PoolWide, PoolDeep);
            var poolMat = GlowMat("LampPool", "Pool", LampHue, 0.34f, PoolWide, PoolDeep);

            Along(slices, LampStep, (slice, z, k) => Sides("Lamp" + k, LampAt, (at, side, name) =>
            {
                var lamp = Child(slice, name);
                lamp.localPosition = new Vector3(at, 0f, z);
                // 腕は mesh の +x へ伸ばしてあるので、右側は向きを返して道へ差し出す
                lamp.localRotation = Quaternion.Euler(0f, side == 0 ? 0f : 180f, 0f);
                Piece(lamp, "Post", post, Mat("Metal"));
                // **4.2 では白い点になる。** 加算ではなく自分の色で塗る面なので、
                // 利得を上げると赤も緑も青も 1 を越えて、橙が白へ抜ける。
                // 実際に測って (247,247,247) だった。明るさはここで頭打ちなので、
                // 遠くで見えるようにするのは大きさの方（頭を太らせてある）
                Piece(lamp, "Head", head, Glow(LampHue, 1.05f));
            }));
            // 溜まりは灯りの頭の真下。柱ではなく腕の先から下ろす。
            // 名前は帯を通して同じにする。CheckDrive の Paving（路面に貼る面）で引くため
            Along(slices, LampStep, (slice, z, k) => Sides("Pool", LampAt - LampArm, (at, side, name) =>
                Piece(slice, name, pool, poolMat).localPosition = new Vector3(at, 0f, z)));
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
                b.FaceY(PastureY, Lane(-46f), Lane(-(RoadHalf + Shoulder)), 0f, TileLength, 1);
                b.FaceY(PastureY, Lane(RoadHalf + Shoulder), Lane(46f), 0f, TileLength, 1);
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
                // 運転席が右なので、近いのは左の石垣。門もそちら側に置く。
                // 石垣と同軸（-5.2）に置くと笠石より上の 0.34 m しか出ず、
                // 門ではなく壁に付いた金具に見える。牧草地の側へ出して独りで立たせる。
                // 柱の足は牧草地の面まで下ろす。石垣に隠れていたときは気づかないが、
                // 独りで立つと 6 cm 浮いているのがそのまま見える
                Piece(slices[i], "Gate", gate, Mat("Metal"))
                    .localPosition = new Vector3(Lane(-6.0f), PastureY, 8.5f);
            }
        }

        /// <summary>
        /// 帯 4。朝靄の未舗装路。黄金色の小麦畑と土の轍。
        ///
        /// 原作の「朝靄の中で、緩やかな湾曲を描いて広がる小麦畑が黄金色に微風になびいている」。
        /// **畑は視界を埋めるところまで広げる。** 道の脇に麦を数本立てただけでは、
        /// 窓を開けて息を吸い込む場面にならない。株は 40 m まで、地の面は 150 m まで敷いて、
        /// その先は霧が畳む。なびかせるのは株のマテリアル（HalfAware/Wheat）で、
        /// 株ごとに Transform を持たせる手は取れない。区切り 1 つに 600 を越える札が
        /// 1 枚の mesh へ焼かれているため
        /// </summary>
        static void Furrows(Transform[] slices)
        {
            // 舗装のタイルはそのまま下に敷いてあるので、土の面で覆い隠す。
            // 帯 4 だけタイルを差し替える手は取らない。タイルは 1 種しか無い。
            // ±4.9 は路肩の段（±4.7）のすぐ外。ここより広げると土色が畑の下へ回り込み、
            // 黄金色が道の際で途切れる。実際 ±5.4 で敷いたときは、轍と麦のあいだに
            // 3 m 余りの裸地が延びて、農道ではなく採石場の取り付け道路に見えた
            var earth = Shape("Earth", 0.2f, b => b.FaceY(EarthY, Lane(-4.9f), Lane(4.9f), 0f, TileLength, 1));
            var ruts = Shape("Ruts", 0.3f, b =>
            {
                b.FaceY(RutY, Lane(-DirtHalf), Lane(DirtHalf), 0f, TileLength, 1);
                b.Box(new Vector3(Lane(-0.95f), RutY + 0.004f, TileLength * 0.5f), new Vector3(0.52f, 0.02f, TileLength));
                b.Box(new Vector3(Lane(0.95f), RutY + 0.004f, TileLength * 0.5f), new Vector3(0.52f, 0.02f, TileLength));
            });
            // 畑の地。株のあいだから覗く面で、丘の形を持っているのはこちら。
            //
            // **株だけでは畑にならない。** 遠くの株は霧に畳まれて消えるので、地の面が無いと
            // 畑の向こうに路肩の地面（Verge）の暗い色がそのまま出て、刈り取った跡に見える。
            // 起伏を追わせるため 1 枚の面では張れず、<see cref="FieldRows"/> の升目に割る。
            // uv は升の座標からじかに振る（Bank.Patch）。隅どうしの距離から振ると、
            // 斜面のぶんだけ伸びて隣の升と継ぎ目がずれる
            var field = Shape("Field", FieldTexel, b =>
            {
                // 株と同じシェーダーで塗るので、読まれる uv1 を空にしない。
                // 根の高さは使わないが（_Rooted 0）、無い頂点の流れを読ませない方が安全
                b.Rooted = true;
                b.RootY = 0f;
                b.RootHigh = 1f;
                var dz = TileLength / FieldSteps;
                // 轍のすぐ外まで畑の地を寄せる裾。
                //
                // **株の立つ足元が土だと、畑が土手に生えた雑草に見える。** 土の面（Earth）は
                // 下の舗装を覆い隠すために ±4.9 まで敷いてあり、その上に一番内側の株が
                // 立っていた。窓から見下ろすと、轍と麦のあいだに 1.5 m の裸地が延びて、
                // そこだけが畑のどこよりも暗い。実際に測ると土 (96,76,57) に対して
                // 株は (174,148,98) で、明るさが倍ほど違う。
                //
                // 裾は土の面より上に置く。下に潜らせると舗装が透ける。
                // 隔たりは路面に重ねる面の作法（CheckDrive.RoadGap）と同じ 8 mm。
                // 起伏は要らない。SwellFrom の内側なので Land はどこも 0 になる
                var hemY = EarthY + 0.008f;
                for (var s = 0; s < 2; s++)
                {
                    var side = s == 0 ? -1f : 1f;
                    var hA = side < 0f ? FieldRows[0] : HemFrom;
                    var hB = side < 0f ? HemFrom : FieldRows[0];
                    for (var k = 0; k < FieldSteps; k++)
                    {
                        var z0 = k * dz;
                        var z1 = z0 + dz;
                        var hx = Lane(hA * side);
                        var hy = Lane(hB * side);
                        b.Patch(
                            new Vector3(hx, hemY, z1), new Vector3(hy, hemY, z1),
                            new Vector3(hy, hemY, z0), new Vector3(hx, hemY, z0),
                            new Vector2(hx * FieldTexel, -z1 * FieldTexel),
                            new Vector2(hy * FieldTexel, -z1 * FieldTexel),
                            new Vector2(hy * FieldTexel, -z0 * FieldTexel),
                            new Vector2(hx * FieldTexel, -z0 * FieldTexel));
                    }
                    for (var c = 0; c + 1 < FieldRows.Length; c++)
                    {
                        // 面を上へ向けるには x の小さい側から回す。
                        // 左側は道から遠いほど x が小さくなるので、そこで入れ替わる
                        var dA = FieldRows[c];
                        var dB = FieldRows[c + 1];
                        if (side < 0f) { var swap = dA; dA = dB; dB = swap; }
                        var xA = Lane(dA * side);
                        var xB = Lane(dB * side);
                        for (var k = 0; k < FieldSteps; k++)
                        {
                            var z0 = k * dz;
                            var z1 = z0 + dz;
                            b.Patch(
                                new Vector3(xA, FieldY + Land(dA, z1), z1),
                                new Vector3(xB, FieldY + Land(dB, z1), z1),
                                new Vector3(xB, FieldY + Land(dB, z0), z0),
                                new Vector3(xA, FieldY + Land(dA, z0), z0),
                                new Vector2(xA * FieldTexel, -z1 * FieldTexel),
                                new Vector2(xB * FieldTexel, -z1 * FieldTexel),
                                new Vector2(xB * FieldTexel, -z0 * FieldTexel),
                                new Vector2(xA * FieldTexel, -z0 * FieldTexel));
                        }
                    }
                }
            });
            // 株。**箱ではなく、α で形を抜いた札を交差させて立てる。**
            //
            // 箱に麦の絵を貼っていたときは、絵をどれだけ描き込んでも輪郭が箱のままで、
            // 天面が平らに切れ、株のあいだが覗けなかった。麦が麦に見えるのは面の絵柄ではなく
            // 輪郭で、穂先の凸凹と株の隙間がそれを作る。形を絵の α に預けると、
            // 上端は勝手に毛羽立ち、向こうが透けて見える。三角も箱の 12 枚から 4 枚へ減る
            var wheat = Shape("Wheat", WheatTexel, b =>
            {
                var rnd = new System.Random(4021);
                // 根からの高さを uv1 に持たせる。丘の上の株は y が丸ごと持ち上がるので、
                // y を高さとして読むシェーダーは撓むかわりに横へ滑る
                b.Rooted = true;
                b.CardLift = WheatLift;
                // 列ごとに、道からの距離・札の幅・列の中の刻みを持つ。
                //
                // **札は透けるので、箱より数を要る。** 箱を一つ置けばそこは埋まったが、
                // 札は向こうが見える。近い列は幅を狭く刻みを細かく、遠い列は幅を広く粗くして、
                // 何枚も重なったところで面として繋がっていればよいことにする。
                // 刻みはどれも 20 を割り切る数にして、区切りの継ぎ目で列が詰まらないようにする。
                //
                // **遠い列の札を太らせても密度は変わらない。** 絵の横の繰り返しは実寸から
                // 引く（WheatTexel）ので、6 m の札には 6 m ぶんの株が並ぶ。箱のときに
                // 側面が横長の板になって丘が瓦葺きに見えた問題は、札には無い。
                //
                // 一番外の三列は丘の稜線のためにある。地の面だけで稜線を作ると、
                // 空との境が升目の縁そのままの折れ線になる。札を乗せて縁を毛羽立たせる。
                //
                // 背は実物どおり 0.72〜1.18。目線（1.55）より低くして見下ろせるようにする。
                // WheatWind.High はこの上端と揃えること。
                //
                // 札は y まわりに回しても、はみ出すのは幅の半分まで。箱（半幅の 1.414 倍）より
                // 狭いので、一番内側の列を 3.75 に置いてばらつきを外向きだけにすれば、
                // 轍（DirtHalf 2.3）までまだ 1 m 以上ある。
                // 風でさらに WheatWind.Reach だけ振れるぶんは見直しが見る
                var at = new[]
                {
                    3.30f, 3.95f, 4.75f, 5.70f, 7.8f, 9.4f, 11.4f, 14.0f, 17.5f, 21.5f,
                    26.0f, 31.5f, 38.0f, 46.0f, 56.0f, 68.0f, 83.0f, 101.0f, 122.0f,
                };
                var wide = new[]
                {
                    0.55f, 0.60f, 0.70f, 0.80f, 0.95f, 1.15f, 1.40f, 1.70f, 2.00f, 2.30f,
                    2.60f, 3.00f, 3.40f, 3.90f, 4.40f, 5.00f, 5.60f, 6.20f, 6.80f,
                };
                var step = new[]
                {
                    0.50f, 0.50f, 0.625f, 0.625f, 0.80f, 1.00f, 1.00f, 1.25f, 1.25f, 1.25f,
                    1.25f, 2.00f, 2.00f, 2.00f, 2.50f, 2.50f, 2.50f, 4.00f, 4.00f,
                };
                for (var c = 0; c < at.Length; c++)
                {
                    // 道からの距離のばらつき。
                    //
                    // 近い列は札の幅ぶんだけ。**外向きにしか振らない。** 内向きに振ると
                    // 一番内側の列が轍へ寄る。遠い列は次の列との間をほぼ埋めるまで散らす。
                    // 同じ距離に整列させると、丘の斜面に横縞の段が出て段々畑に見える
                    var spread = c < 9 || c + 1 >= at.Length
                        ? wide[c] * 0.9f
                        : (at[c + 1] - at[c]) * 0.85f;
                    for (var z = step[c] * 0.5f; z < TileLength; z += step[c])
                        for (var s = 0; s < 2; s++)
                        {
                            var side = s == 0 ? -1f : 1f;
                            var high = 0.72f + (float)rnd.NextDouble() * 0.46f;
                            // 札の幅も振る。列ごとに同じ幅で並べると、
                            // 手前の畑が畝ではなく畳の目に見える
                            var span = wide[c] * (0.75f + (float)rnd.NextDouble() * 0.5f);
                            var from = at[c] + (float)rnd.NextDouble() * spread;
                            var atZ = z + ((float)rnd.NextDouble() - 0.5f) * step[c] * 0.7f;
                            var ground = Land(from, atZ);
                            b.RootY = ground;
                            b.RootHigh = high;
                            // 向きは群ごとに振る。揃えると、脇を通り過ぎるときに
                            // 畑ぜんたいが一斉に薄くなって、立てた板だと分かる
                            var turn = (float)rnd.NextDouble() * Mathf.PI;
                            var half = span * 0.5f;
                            var uOff = (float)rnd.NextDouble();
                            for (var q = 0; q < WheatCross; q++)
                            {
                                var a = turn + q * Mathf.PI / WheatCross;
                                var across = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * half;
                                // 札は平らな一枚なので、斜面では端が地面から浮く。
                                // 両端の地面を引いて低い方まで下辺を沈める。
                                // 丘の斜面は 1 m につき 0.2 m 落ちるところがあり、
                                // 幅 6 m の札では 1 m 以上の差が付く。一律の沈めでは足りない
                                var nearEnd = Land(from - across.x * side, atZ - across.z);
                                var farEnd = Land(from + across.x * side, atZ + across.z);
                                var foot = Mathf.Min(ground, Mathf.Min(nearEnd, farEnd)) - 0.05f;
                                var up = new Vector3(0f, ground + high - foot, 0f);
                                b.Card(new Vector3(Lane(from * side), foot, atZ), across, up, uOff);
                            }
                        }
                }
            });
            var crop = WheatMat();
            var loam = FieldMat();
            for (var i = 0; i < slices.Length; i++)
            {
                Piece(slices[i], "Earth", earth, Mat("Dirt"));
                Piece(slices[i], "Ruts", ruts, Mat("Rut"));
                Piece(slices[i], "Field", field, loam);
                Piece(slices[i], "Wheat", wheat, crop);
            }
        }

        /// <summary>
        /// 畑の起伏。道から離れるほど持ち上がり、途中に二つの背を持つ。
        ///
        /// 原作の「緩やかな湾曲を描いて広がる小麦畑」。平らなままだと地平が定規を当てた
        /// ような直線になり、畑ではなく黄色い板が敷いてあるように見える。
        ///
        /// 形は三つの足し合わせ。全体を二乗で持ち上げる坂と、<see cref="CrestAt"/> の
        /// 主な背、<see cref="FoldAt"/> の手前のうねり。背を二つ重ねるのは、
        /// 一本調子の坂だと稜線が一本しか出ず、斜面が板に見えるため。
        ///
        /// z は区切りの中の座標。畝は区切りの長さ（20 m）でちょうどひと回りするので、
        /// 区切りの継ぎ目で地面が段にならない。**変えるなら 20 m を割り切る波長から選ぶこと。**
        ///
        /// 道際（<see cref="SwellFrom"/> の内側）は 0 のままにする。轍も土も平らな面なので、
        /// ここが動くと路面と畑の境に隙間が空く
        /// </summary>
        public static float Land(float from, float z)
        {
            var t = from - SwellFrom;
            if (t <= 0f) return 0f;
            var rise = SwellRate * t * t
                + CrestHigh * Bump((t - CrestAt) / CrestWide)
                + FoldHigh * Bump((t - FoldAt) / FoldWide);
            var high = Mathf.SmoothStep(0f, 1f, t / SwellEase) * rise;
            // 畝は丘の高さに掛ける。道際で丘が 0 なら畝も出ない
            return high * (1f + FurrowDeep *
                Mathf.Sin(z * (2f * Mathf.PI / TileLength) + from * FurrowSlant));
        }

        /// <summary>釣鐘。1 を頂点に、±1 で 1/e まで落ちる</summary>
        static float Bump(float u)
        {
            return Mathf.Exp(-u * u);
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
    }
}
