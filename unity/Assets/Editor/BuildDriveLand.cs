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
        /// 切り替えるだけになり、帯ぜんたいが同時に出る。入れ物の直下へじかに並べると
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
                // **未舗装路は必ず最後の帯。添字を決め打ちにしない。**
                // 景色の数はシナリオの改訂で動く。番号で書くと、帯を減らしたときに
                // 末尾が途中の帯の中身になり、朝の畑がどこにも出なくなる。
                // CheckDrive.OnRoad も同じ数え方（Bands - 1）で轍の幅に切り替えている
                if (b == Bands - 1) Furrows(slices);
                else if (b == 0) Outskirts(slices);
                else if (b == 1) Motorway(slices);
                else if (b == 2) Trunk(slices);
                else Downs(slices);
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
        /// 中身があるのは帯 1 だけだが、入れ物と区切りは帯の数だけ揃えて作る。
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

        /// <summary>
        /// 帯 0。倫敦の市街。道の両側にビルが並ぶ夜の街と、濡れた路面のネオン。
        ///
        /// **ここはまだ都市部。** 以前は高架の脚とネオンの板が疎らに並ぶだけで、
        /// 市街ではなく郊外の空き地に見えていた。道の両側をビルで塞いで初めて、
        /// 倫敦を出て行くところになる。ビルの中身は <see cref="City"/>
        /// </summary>
        static void Outskirts(Transform[] slices)
        {
            var pier = Shape("Pier", 0.45f, b => b.Box(new Vector3(0f, 3.0f, 0f), new Vector3(1.10f, 6.0f, 1.10f)));
            var board = Shape("NeonBoard", 0.5f, b => b.Box(Vector3.zero, new Vector3(0.12f, 1.10f, 2.60f)));
            // 袖看板。壁から道へ突き出すので、板の面は近づいてくる側を向く。
            // 正面を向いた板（NeonBoard）は真横を通り過ぎる瞬間しか面にならないので、
            // 道の先に続いて見えるネオンはこちらが出す。
            // 突き出しは 0.8 m。高架の脚の外の面（±7.05）の手前で止める
            var blade = Shape("NeonBlade", 0.5f, b => b.Box(Vector3.zero, new Vector3(0.80f, 0.90f, 0.10f)));
            var sheen = Shape("Sheen", 0.25f, b => b.FaceY(SheenY, Lane(-RoadHalf), Lane(RoadHalf), 0f, TileLength, 1));
            var walk = WalkMesh();
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
            {
                Piece(slices[i], "Sheen", sheen, Mat("Sheen"));
                Piece(slices[i], "Walk", walk, Mat("Concrete"));
            }
            City(slices);
            Along(slices, 45f, (slice, z, k) => Sides("Pier" + k, 6.5f, (at, side, name) =>
                Piece(slice, name, pier, Mat("Concrete")).localPosition = new Vector3(at, 0f, z)));
            // 看板は建物の壁へ移した。±5.4 に立っていた頃は歩道の真ん中に板が浮いており、
            // 背後に何も無いので看板ではなく道標に見えていた。壁の面（CityFace）から
            // 0.12 だけ手前へ出すと、壁に付いた看板になる。
            // 一軒目の間口は CityLead で必ず看板を跨ぐように割り付けてある
            Along(slices, 30f, (slice, z, k) => Sides("Neon" + k, CityFace - 0.12f, (at, side, name) =>
                // 街灯の頭と同じ理由で 2.4 から落とす。ネオンは色が命で、
                // 白く飛んだ看板は蛍光灯にしか見えない
                Piece(slice, name, board, Glow(hues[(k + side) % hues.Length], 1.25f))
                    .localPosition = new Vector3(at, 3.2f, z)));
            // 袖看板は正面の板の中ほどへ差し込む。刻みは同じ 30 m なので環を割り切るままで、
            // 15 m ごとに何かしらの灯りが流れる。色も正面の板と一つずらす
            Along(slices, 30f, (slice, z, k) => Sides("Blade" + k, CityFace - 0.40f, (at, side, name) =>
                Piece(slice, name, blade, Glow(hues[(k + side + 1) % hues.Length], 1.25f))
                    .localPosition = new Vector3(at, 4.4f, z + 15f)));
            // 映り込みは看板の足元から手前へ伸びる。看板は道の外（±7.9）に立っているので、
            // 舗装の縁（±3.5）へ寄せて落とす。艶（Sheen）の上に乗るのが正しい順で、
            // 板の高さ（GlowY 0.028）は白線より上に取ってある。
            // 袖看板の下にも同じ色で落とす。灯りが 15 m ごとに来るなら、照り返しも 15 m ごとに来る
            Along(slices, 30f, (slice, z, k) => Sides("Smear", 3.0f, (at, side, name) =>
                Piece(slice, name, smear, smears[(k + side) % hues.Length])
                    .localPosition = new Vector3(at, 0f, z)));
            Along(slices, 30f, (slice, z, k) => Sides("Smear", 3.0f, (at, side, name) =>
                Piece(slice, name, smear, smears[(k + side + 1) % hues.Length])
                    .localPosition = new Vector3(at, 0f, z + 15f)));
        }

        /// <summary>ネオンの映り込みの板の幅。濡れた舗装の映り込みは看板より広がる</summary>
        const float SmearWide = 3.2f;
        /// <summary>ネオンの映り込みの板の長さ。看板の間隔（15）より短くする。繋がると流れが消える</summary>
        const float SmearDeep = 13f;

        // ---- 帯 0 の市街 --------------------------------------------------

        /// <summary>
        /// 手前の列の壁の面。道の中心から測る。
        ///
        /// 路肩の段（RoadHalf + Shoulder ＝ 4.7）と高架の脚（±6.5、半幅 0.55 なので
        /// 外の面が 7.05）のどちらより外に取る。ここを詰めると脚が壁にめり込む。
        ///
        /// **手前の列だけは道からの距離を振らない。** 一軒ごとに前後へ振ると、道沿いの線が
        /// 消えて、建物が各々に立っている空き地に見える。市街の道は壁の線が揃っていること
        /// そのものが絵になるので、ばらつきは背・間口・奥行き・路地の方で出す
        /// </summary>
        const float CityFace = 8.0f;

        /// <summary>歩道の内側の縁。路肩の段のすぐ外</summary>
        const float WalkFrom = RoadHalf + Shoulder;

        /// <summary>歩道の高さ。縁石のぶんだけ舗装より上げる</summary>
        const float WalkY = 0.13f;

        /// <summary>
        /// 手前の街区ひとつの長さ。m。**180 を割り切ること。**
        /// 割り切らないと、環が一周したところで街区が一つだけ詰まる
        /// </summary>
        const float CityBlock = 30f;

        /// <summary>二列目の街区の長さ。m。180 を割り切ること</summary>
        const float CitySecond = 20f;

        /// <summary>三列目の街区の長さ。m。180 を割り切ること</summary>
        const float CityThird = 30f;

        /// <summary>
        /// 街区の頭を看板より手前へ出す量。m。
        ///
        /// 看板は 30 m 刻みで並び、手前の街区も同じ 30 m 刻みで始まる。街区の頭だけ
        /// これだけ手前へずらすと、看板は必ず一軒目の壁の上に来る。
        /// 0 にすると看板がちょうど建物の継ぎ目に跨がり、路地に当たれば宙に浮く
        /// </summary>
        const float CityLead = 4f;

        /// <summary>一軒の最小の間口。m。看板（長さ 2.6）が乗り切る幅を下回らせない</summary>
        const float CityNarrow = 7f;

        /// <summary>窓と店先を壁から浮かせる量。m。壁と同じ面に置くと深度で削り合う</summary>
        const float CityProud = 0.05f;

        /// <summary>軒（パラペット）の厚み。m。壁よりせり出させて、棟と棟の境に線を引かせる</summary>
        const float CityCap = 0.55f;

        /// <summary>点いた窓のうち寒色（蛍光灯）の割合。残りは暖色の白熱灯</summary>
        const float CityCool = 0.30f;

        /// <summary>建物の足元を地面より下へ下ろす量。m</summary>
        const float CitySink = -0.6f;

        /// <summary>市街の地の面の外縁。m。路肩の地面（±24）の外をこれで塞ぐ</summary>
        const float CityGround = 95f;

        /// <summary>市街の地の面の高さ。路肩の地面（VergeY）の下へ潜らせて面を重ねない</summary>
        const float CityGroundY = VergeY - 0.02f;

        /// <summary>
        /// 市街のビルを焼く先。素材ごとに 1 つ持ち、区切り 1 つぶんをまとめて 1 枚の mesh にする
        /// </summary>
        sealed class CityBanks
        {
            /// <summary>
            /// 手前の列の壁。**二色あるのは隣り合う建物を見分けるため。**
            ///
            /// この帯には影が出ず、道を向いた面も手前（-z）の面も環境光しか受けない。
            /// 一色で塗ると隣り合う壁が一分の違いも無く揃い、軒を連ねた一列が
            /// 一枚の塀に見える。境は色で引くほかない
            /// </summary>
            public readonly Bank WallA = new Bank { Texel = 0.3f };
            public readonly Bank WallB = new Bank { Texel = 0.3f };
            /// <summary>奥の列の壁と地の面。手前より暗く冷たい色にして、空気の厚みを出す</summary>
            public readonly Bank Deep = new Bank { Texel = 0.3f };
            /// <summary>消えている窓</summary>
            public readonly Bank Dark = new Bank { Texel = 0.5f };
            /// <summary>点いている窓。暖色（白熱灯）と寒色（蛍光灯）</summary>
            public readonly Bank Warm = new Bank { Texel = 0.5f };
            public readonly Bank Cool = new Bank { Texel = 0.5f };
        }

        /// <summary>
        /// 市街のビル。三列に分けて並べ、区切りごとに 1 枚の mesh へ焼く。
        ///
        /// **一軒ずつ物を置いてはいけない。** 三列で 70 軒を越えるので、素材ごとに分けて
        /// 置けばレンダラーが四百になる。区切り 1 つに素材 6 枚（壁二色・消えた窓・
        /// 点いた窓二色）へ焼けば、帯ぜんたいで 54 枚に収まる。
        ///
        /// 並べるのは環の上の位置で、<see cref="Along"/> は使わない。軒の間口は一軒ごとに
        /// 違うので、一定の刻みでは並べられない。代わりに街区（CityBlock / CitySecond /
        /// CityThird）の長さだけが 180 を割り切ればよい。街区の中では幅を伸ばし縮めして
        /// ちょうど埋めるので、環が一周しても継ぎ目が飛ばない
        /// </summary>
        static void City(Transform[] slices)
        {
            var banks = new CityBanks[slices.Length];
            for (var i = 0; i < banks.Length; i++) banks[i] = new CityBanks();
            // 種を決め打ちにして、組み直しても同じ街並みになるようにする
            var rnd = new System.Random(20260921);
            for (var s = 0; s < 2; s++)
            {
                var side = s == 0 ? -1f : 1f;
                // 手前の列。街区の頭を看板より CityLead だけ手前へ出す
                for (var k = 0; k < Mathf.RoundToInt(Span / CityBlock); k++)
                    Terrace(banks, rnd, side, k * CityBlock - CityLead, CityBlock, 0);
                // 二列目と三列目。**左右で位相を半刻みずらす。**
                // 揃えると道を挟んで建物が対で並び、正面に遠近法の消点だけが残る
                for (var k = 0; k < Mathf.RoundToInt(Span / CitySecond); k++)
                    Terrace(banks, rnd, side, k * CitySecond + (s == 0 ? 0f : CitySecond * 0.5f), CitySecond, 1);
                for (var k = 0; k < Mathf.RoundToInt(Span / CityThird); k++)
                    Terrace(banks, rnd, side, k * CityThird + (s == 0 ? CityThird * 0.5f : 0f), CityThird, 2);
            }

            // 壁の色。**この帯のほかの素材（舗装 0.115 / 路肩 0.078）と揃えてはいけない。**
            //
            // それらは上を向いた面で、環境光の空側（lift 0.250）を丸ごと受ける。
            // 壁は立っているので水平の環境光しか来ず、日射しは右手の壁の道向きの面に
            // しか当たらない。実測したところ、色 0.112 で塗った壁は画面で luma 3〜12 しか
            // 無く、空（24）より暗い真っ黒な影絵になっていた。窓だけが宙に浮いて、
            // ビルが並んでいるという絵にならない。
            //
            // 上げても夜が壊れないのは、壁の受ける光が舗装の 1/5 しか無いからで、
            // 色を 0.5 まで上げても陰の側の面は luma 14 にしかならない。
            // 倫敦の建物はポートランド石と塗った塑塢で、実物もこの辺りに明るい。
            // 手前を温かく明るく、奥を冷たく暗くして、色の側からも遠近を読ませる
            var brick = CityMat("CityBrick", new Color(0.560f, 0.492f, 0.424f), 0.10f);
            var stucco = CityMat("CityStucco", new Color(0.390f, 0.396f, 0.424f), 0.08f);
            var deep = CityMat("CityDeep", new Color(0.250f, 0.262f, 0.310f), 0.06f);
            // 消えている窓。壁より暗く、艶だけ高くする。映り込みは切ってあるので
            // 何も映らないが、暗い面が格子に並ぶだけで壁の絵柄になる
            var glass = CityMat("CityPane", new Color(0.055f, 0.060f, 0.082f), 0.70f);
            // 点いている窓。灯りを何百も置けないので、ネオンや街灯の頭と同じく
            // 明るい Unlit で済ませる。利得を上げ過ぎると白へ抜けて窓が穴になる
            var warm = Glow(new Color(1f, 0.80f, 0.55f), 0.42f);
            var cool = Glow(new Color(0.78f, 0.86f, 1f), 0.34f);

            for (var i = 0; i < slices.Length; i++)
            {
                // 路肩の地面（GroundMesh の ±24）の外を塞ぐ。二列目から先はその外に建つので、
                // 塞がないと建物の足元に空の色が抜けて、地平の手前に暗い帯が一本入る
                for (var s = 0; s < 2; s++)
                {
                    var side = s == 0 ? -1f : 1f;
                    var a = Lane(WalkFrom * side);
                    var b = Lane(CityGround * side);
                    banks[i].Deep.FaceY(CityGroundY, Mathf.Min(a, b), Mathf.Max(a, b), 0f, TileLength, 1);
                }
                banks[i].WallA.Emit(slices[i], "CityBrick" + i, brick, false, Generated);
                banks[i].WallB.Emit(slices[i], "CityStucco" + i, stucco, false, Generated);
                banks[i].Deep.Emit(slices[i], "CityDeep" + i, deep, false, Generated);
                banks[i].Dark.Emit(slices[i], "CityPane" + i, glass, false, Generated);
                banks[i].Warm.Emit(slices[i], "CityWarm" + i, warm, false, Generated);
                banks[i].Cool.Emit(slices[i], "CityCool" + i, cool, false, Generated);
            }
        }

        /// <summary>
        /// 街区ひとつ。room メートルを軒と路地でちょうど埋める。
        ///
        /// 幅を先に重みで引いてから room へ伸ばし縮めするのは、余りを最後の一軒に
        /// 押し付けると街区の尻だけが必ず同じ間口になるため。一周すれば 6 回来る
        /// </summary>
        static void Terrace(CityBanks[] banks, System.Random rnd, float side, float at, float room, int row)
        {
            // 一区画に何軒建てるか。手前ほど小割りにする
            var n = row == 0 ? 2 + rnd.Next(2) : 1 + rnd.Next(2);
            // 建物のあいだの隔たり。**ほとんどは 0（軒を連ねる）。**
            // たまに路地を空けると、そこから奥の列が覗いて街に厚みが出る。
            // さらにたまに横町ほどの幅を空けるのは、三列目まで見通せる穴を作るため
            var gaps = new float[n];
            var spare = 0f;
            for (var i = 0; i < n; i++)
            {
                var roll = rnd.NextDouble();
                gaps[i] = roll < 0.55 ? 0f
                    : roll < 0.88 ? 2.0f + (float)rnd.NextDouble() * 2.2f
                    : 5.5f + (float)rnd.NextDouble() * 3.5f;
                spare += gaps[i];
            }
            var built = room - spare;
            // 隔たりを取りすぎて軒が瘦せたら、隔たりの方を捨てる
            if (built < n * CityNarrow)
            {
                built = room;
                for (var i = 0; i < n; i++) gaps[i] = 0f;
            }
            var wide = new float[n];
            var sum = 0f;
            for (var i = 0; i < n; i++) { wide[i] = 0.7f + (float)rnd.NextDouble(); sum += wide[i]; }
            for (var i = 0; i < n; i++) wide[i] = wide[i] / sum * built;
            // 一軒目は看板を乗せるので、一番広い間口をそこへ持ってくる
            for (var i = 1; i < n; i++)
                if (wide[i] > wide[0]) { var swap = wide[0]; wide[0] = wide[i]; wide[i] = swap; }
            var z = at;
            for (var i = 0; i < n; i++)
            {
                Shell(banks, rnd, side, z, wide[i], row);
                z += wide[i] + gaps[i];
            }
        }

        /// <summary>
        /// 建物 1 棟の寸法を引いて、環の上の位置から区切りへ振る。
        ///
        /// 描く中身には別の <see cref="System.Random"/> を渡す。区切りを索く順番に
        /// 描くことになるので、一つの乱数を使い回すと並べ方を一つ直しただけで
        /// 街並みが丸ごと変わる
        /// </summary>
        static void Shell(CityBanks[] banks, System.Random rnd, float side, float ring, float wide, int row)
        {
            float from, deep, high;
            if (row == 0)
            {
                from = CityFace;
                deep = 6f + (float)rnd.NextDouble() * 5f;
                // 背は二乗寄りで引く。一様に引くと軒の線が真ん中に集まって、
                // 屋上の線が波打っているだけの一枚の帯に見える
                high = 10f + Mathf.Pow((float)rnd.NextDouble(), 1.4f) * 19f;
            }
            else if (row == 1)
            {
                from = 19f + (float)rnd.NextDouble() * 8f;
                deep = 8f + (float)rnd.NextDouble() * 5f;
                high = 16f + Mathf.Pow((float)rnd.NextDouble(), 1.2f) * 22f;
            }
            else
            {
                from = 40f + (float)rnd.NextDouble() * 16f;
                deep = 12f + (float)rnd.NextDouble() * 9f;
                high = 24f + Mathf.Pow((float)rnd.NextDouble(), 1.1f) * 24f;
            }
            var pick = rnd.Next(2);
            // 点いている窓の割合。**棟ごとに振る。** 全部を同じ割合で散らすと、
            // どの建物も同じ密度の点描になって、建物の境が窓の側から読めなくなる
            var lit = 0.26f + (float)rnd.NextDouble() * 0.34f;
            var shop = row == 0 && rnd.NextDouble() < 0.55;
            var seed = rnd.Next();
            Put(banks, ring, (c, z) =>
                Raise(c, new System.Random(seed), side, z, wide, from, deep, high, row, pick, lit, shop));
        }

        /// <summary>
        /// ビル 1 棟。壁と軒を積み、道を向いた面と手前（-z）の面に窓を並べる。
        ///
        /// **面は 6 つとも張るが、窓は 2 面にしか並べない。** 車は原点で +z を向いたまま
        /// 動かないので、建物の向こう側（+z）の面と道と反対を向いた面は一度も画面に入らない。
        /// 描画解像度 427 × 240 では窓一つが数画素しか無いので、見えない面にまで並べる
        /// 余裕は無い。壁の方を削らないのは、路地の口から裏が覗くため
        /// </summary>
        static void Raise(CityBanks c, System.Random rnd, float side, float z0, float wide,
            float from, float deep, float high, int row, int pick, float lit, bool shop)
        {
            var z1 = z0 + wide;
            var xIn = Lane(from * side);
            var xOut = Lane((from + deep) * side);
            var x0 = Mathf.Min(xIn, xOut);
            var x1 = Mathf.Max(xIn, xOut);
            // 奥の列は一色で塗る。遠いほど霧に喰われるので、色を割っても見分けが付かない
            var wall = row > 0 ? c.Deep : pick == 0 ? c.WallA : c.WallB;
            // 軒は壁と違う色で塗る。同じ色だと、せり出しただけの軒はこの帯では一切読めない
            var cap = row > 0 ? c.Deep : pick == 0 ? c.WallB : c.WallA;
            var cx = (x0 + x1) * 0.5f;
            var cz = (z0 + z1) * 0.5f;
            wall.Box(new Vector3(cx, (high + CitySink) * 0.5f, cz), new Vector3(x1 - x0, high - CitySink, wide));
            cap.Box(new Vector3(cx, high + CityCap * 0.5f, cz),
                new Vector3(x1 - x0 + 0.44f, CityCap, wide + 0.44f));

            // 窓の格子。奥の列ほど粗く取る。実寸で同じ刻みにすると、
            // 60 m 先では窓一つが 3 画素を割ってちらつくだけになる
            var pitch = row == 0 ? 2.70f : row == 1 ? 3.00f : 4.20f;
            var floor = row == 0 ? 3.30f : row == 1 ? 3.60f : 4.40f;
            // 一階ぶんを空ける。手前の列はそこが店先になる
            var foot = row == 0 ? 3.45f : 5.0f;
            var face = xIn - side * CityProud;
            var look = side < 0f ? 1 : -1;
            Panes(c, rnd, true, face, look, z0, z1, foot, high, pitch, floor, lit);
            Panes(c, rnd, false, z0 - CityProud, -1, x0, x1, foot, high, pitch, floor, lit);

            if (row == 0)
            {
                // 一階の店先。**ここが点くと建物の足元が読める。**
                // 上の窓と同じ大きさで刻むと店に見えないので、間口いっぱいの硝子を
                // 方立てで三枚に割る。消えている店は同じ形の暗い面になる
                var sill = WalkY + 0.85f;
                var head = 2.75f;
                var run = (wide - 1.6f - 0.8f) / 3f;
                var pane = shop ? (rnd.NextDouble() < CityCool ? c.Cool : c.Warm) : c.Dark;
                for (var p = 0; p < 3 && run > 0.4f; p++)
                {
                    var b0 = z0 + 0.8f + p * (run + 0.40f);
                    pane.FaceX(face, b0, b0 + run, sill, head, look);
                }
                // 看板の地。**点いた硝子の帯をここで切る。**
                // 店先を一階ぶんそのまま立ち上げると、隣り合う二軒が点いただけで
                // 幅 20 m の光る板になり、店ではなく行燈に見える。
                // 横に暗い線が一本入るだけで、上の窓と下の店が別の階に読める
                c.Dark.FaceX(face, z0 + 0.35f, z1 - 0.35f, head + 0.10f, head + 0.68f, look);
            }

            // 屋上。**ここを平らなままにすると、街が箱を並べたように見える。**
            // 背の高い棟は上を一段引き、低い棟には塔屋と煙突を乗せる
            var top = high + CityCap;
            if (high > 17f && rnd.NextDouble() < 0.55)
            {
                var lift = 4f + (float)rnd.NextDouble() * 8f;
                var inset = 1.3f + (float)rnd.NextDouble() * 2.2f;
                var sz0 = z0 + inset;
                var sz1 = z1 - inset;
                // 引くのは道の側。背の面はそのまま残す
                var sIn = xIn + side * inset;
                var sx0 = Mathf.Min(sIn, xOut);
                var sx1 = Mathf.Max(sIn, xOut);
                if (sx1 - sx0 > 3f && sz1 - sz0 > 3f)
                {
                    wall.Box(new Vector3((sx0 + sx1) * 0.5f, top + lift * 0.5f, (sz0 + sz1) * 0.5f),
                        new Vector3(sx1 - sx0, lift, sz1 - sz0));
                    cap.Box(new Vector3((sx0 + sx1) * 0.5f, top + lift + CityCap * 0.4f, (sz0 + sz1) * 0.5f),
                        new Vector3(sx1 - sx0 + 0.36f, CityCap * 0.8f, sz1 - sz0 + 0.36f));
                    Panes(c, rnd, true, sIn - side * CityProud, look, sz0, sz1, top + 0.9f, top + lift, pitch, floor, lit);
                    Panes(c, rnd, false, sz0 - CityProud, -1, sx0, sx1, top + 0.9f, top + lift, pitch, floor, lit);
                    top += lift + CityCap * 0.8f;
                }
            }
            if (rnd.NextDouble() < 0.5)
            {
                // 塔屋。階段室と水槽で、屋上には必ず何かしら立っている。
                // 空との境を毛羽立たせるのが狙いなので、棟の真ん中ではなく寄せて置く
                var hw = 0.9f + (float)rnd.NextDouble() * 1.6f;
                var hd = 0.9f + (float)rnd.NextDouble() * 1.4f;
                var hh = 1.6f + (float)rnd.NextDouble() * 2.2f;
                if (x1 - x0 > hw * 2.6f && wide > hd * 2.6f)
                    wall.Box(new Vector3(
                            Mathf.Lerp(x0 + hw, x1 - hw, (float)rnd.NextDouble()),
                            top + hh * 0.5f,
                            Mathf.Lerp(z0 + hd, z1 - hd, (float)rnd.NextDouble())),
                        new Vector3(hw * 2f, hh, hd * 2f));
            }
            if (row == 0 && high < 17f && wide > 5f)
                // 煙突。倫敦の低い棟割長屋の屋根には必ず立っている。
                // 道から引いたところへ置く。軒先に立てると看板の支柱に見える
                wall.Box(new Vector3(Mathf.Lerp(x0, x1, side < 0f ? 0.34f : 0.66f), top + 0.85f, cz),
                    new Vector3(0.9f, 1.7f, 1.5f));
        }

        /// <summary>
        /// 壁ひと面ぶんの窓。点いている窓と消えている窓を混ぜて格子に並べる。
        ///
        /// **夜の街が街に見えるのはここだけ。** 窓の無い箱をいくら並べても倉庫にしかならず、
        /// 逆に一様に点けると点描になる。**階ごと丸ごと暗い階を混ぜる。**
        /// 縦にも横にも塊ができて初めて、人の入っている建物に見える。
        ///
        /// wallX が true なら x が一定の面（a0〜a1 は z の範囲）、
        /// false なら z が一定の面（a0〜a1 は x の範囲）
        /// </summary>
        static void Panes(CityBanks c, System.Random rnd, bool wallX, float plane, int sign,
            float a0, float a1, float low, float high, float pitch, float floor, float lit)
        {
            var cols = Mathf.FloorToInt((a1 - a0) / pitch);
            var rows = Mathf.FloorToInt((high - low - CityCap) / floor);
            if (cols < 1 || rows < 1) return;
            var wide = pitch * 0.52f;
            var tall = floor * 0.52f;
            // 余りは両端へ振り分けて、格子を面の真ん中へ寄せる
            var pad = ((a1 - a0) - cols * pitch) * 0.5f;
            for (var r = 0; r < rows; r++)
            {
                var y0 = low + r * floor + (floor - tall) * 0.5f;
                var dark = rnd.NextDouble() < 0.22;
                for (var k = 0; k < cols; k++)
                {
                    var b0 = a0 + pad + k * pitch + (pitch - wide) * 0.5f;
                    var bank = c.Dark;
                    if (!dark && rnd.NextDouble() < lit)
                        bank = rnd.NextDouble() < CityCool ? c.Cool : c.Warm;
                    if (wallX) bank.FaceX(plane, b0, b0 + wide, y0, y0 + tall, sign);
                    else bank.FaceZ(plane, b0, b0 + wide, y0, y0 + tall, sign);
                }
            }
        }

        /// <summary>
        /// 環の上の位置を、区切りの番号と区切りの中の z に割る。<see cref="Drop"/> の mesh 版。
        /// 物を置かずに素材へ焼く沿道（市街のビルと畑の農家）はどれもここを通る
        /// </summary>
        static void Put<T>(T[] banks, float ring, System.Action<T, float> draw)
        {
            var p = ring % Span;
            if (p < 0f) p += Span;
            var i = Mathf.Clamp((int)(p / TileLength), 0, banks.Length - 1);
            draw(banks[i], p - i * TileLength);
        }

        /// <summary>
        /// 歩道。区切り 1 つぶんで、左右に 1 本ずつ。
        ///
        /// ビルが路肩からじかに生えていると、建物が道に置いてあるように見える。
        /// 縁石で一段上げた歩道を挟むと、道と建物のあいだに街の床が入る。
        /// 縁石の立ち上がりは道の側だけ張ればよい。外は壁が塞ぐ
        /// </summary>
        static Mesh WalkMesh()
        {
            return Shape("CityWalk", 0.3f, b =>
            {
                for (var s = 0; s < 2; s++)
                {
                    var side = s == 0 ? -1f : 1f;
                    var inner = Lane(WalkFrom * side);
                    var outer = Lane(CityFace * side);
                    b.FaceY(WalkY, Mathf.Min(inner, outer), Mathf.Max(inner, outer), 0f, TileLength, 1);
                    b.FaceX(inner, 0f, TileLength, VergeY, WalkY, s == 0 ? 1 : -1);
                }
            });
        }

        /// <summary>
        /// 色だけの Lit のマテリアル。市街のビルと畑の農家が使う。<see cref="Mat"/> を通さない。
        /// あちらは名前から色を引くので、色を足すには BuildDrive の表を触ることになる。
        ///
        /// **絵は貼らない。** 描画解像度 427 × 240 では、窓の格子より細かい絵柄は
        /// 画素に届かず、繰り返しの継ぎ目だけが残る。壁の絵柄は窓が持つ
        /// </summary>
        static Material CityMat(string name, Color col, float smooth)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            var path = Materials + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(shader);
                m.name = name;
                AssetDatabase.CreateAsset(m, path);
            }
            // 組み直すたびに結び直す。手で触った値は残らない
            m.shader = shader;
            m.SetTexture("_BaseMap", null);
            m.SetColor("_BaseColor", col);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smooth);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0f);
            EditorUtility.SetDirty(m);
            return m;
        }

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

            // 濡れた舗装。**この帯だけ雨が降っている**（Route の rain）。
            // 市街（Outskirts）と同じ艶の面を敷く。艶は 0.86 あるので、素の舗装（0.16）では
            // 出なかった照り返しが乗り、街灯の溜まりの外の路面も濡れて見える。
            // 名前は市街と同じにする。CheckDrive の Paving で引くため
            var sheen = Shape("Sheen", 0.25f,
                b => b.FaceY(SheenY, Lane(-RoadHalf), Lane(RoadHalf), 0f, TileLength, 1));
            for (var i = 0; i < slices.Length; i++) Piece(slices[i], "Sheen", sheen, Mat("Sheen"));

            Wayside(slices);
        }


        // ---- 帯 1 の沿道 ----------------------------------------------------

        /// <summary>
        /// 路肩の縁石の内側の面。路肩の段（<see cref="RoadHalf"/> ＋ <see cref="Shoulder"/>）に
        /// そのまま立てる。ここを詰めると縁石が道へ出て、CheckDrive.OnRoad が知らせる
        /// </summary>
        const float KerbFrom = RoadHalf + Shoulder;

        /// <summary>縁石の外側の面</summary>
        const float KerbTo = KerbFrom + 0.28f;

        /// <summary>縁石の天端。路肩の段（-ShoulderDrop）から立ち上がる</summary>
        const float KerbY = 0.12f;

        /// <summary>
        /// ガードレールの、道の中心からの距離。
        /// **街灯の柱（<see cref="LampAt"/> 4.6、半幅 0.09）より外に取る。**
        /// 内へ入れると、柱がレールを突き抜けて生えることになる
        /// </summary>
        const float RailAt = 5.30f;

        /// <summary>
        /// ガードレールの帯の中心の高さ。**目線（1.55）よりずっと低く抑える。**
        /// 近いので、ここを上げると土手も遠景もまとめてこれ一本で塞がる
        /// </summary>
        const float RailY = 0.64f;

        /// <summary>ガードレールの支柱の間隔。m。区切り（20）と環一周（180）のどちらも割り切る</summary>
        const float RailStep = 5f;

        /// <summary>
        /// 反射板の間隔。m。区切りと環一周のどちらも割り切る。
        /// 28 m/s なら 1 秒に 2.8 枚。街灯（36 m ＝ 0.78 本／秒）より細かい刻みにしてあり、
        /// 走っている速さを読ませているのはこちらの方
        /// </summary>
        const float StudStep = 10f;

        /// <summary>境の柵の、道の中心からの距離</summary>
        const float FenceAt = 7.0f;

        /// <summary>柵の胴の高さ</summary>
        const float FenceY = 1.02f;

        /// <summary>植え込みの内側と外側の縁</summary>
        const float HedgeFrom = 7.35f;
        const float HedgeTo = 9.35f;

        /// <summary>法面の裾。切り通しの壁はここから立ち上がる。植え込み（HedgeTo）の外に取る</summary>
        const float BundToe = 9.6f;

        /// <summary>法面の中ほど。裾と天のあいだに 1 本入れて、斜面を S 字に丸める</summary>
        const float BundMid = 13.5f;

        /// <summary>法面の天。ここまでで壁を登り切る</summary>
        const float BundTop = 17f;

        /// <summary>土手の天端の中ほど。天と肩のあいだに 1 本入れて、稜線の折れを丸める</summary>
        const float BundCrown = 24f;

        /// <summary>土手の肩。ここから先は野へ均していく</summary>
        const float BundBrow = 31f;

        /// <summary>野の始まり。m。路肩の地面（±24）より外なので、ここから下は負に落としてよい</summary>
        const float MoorFrom = 70f;

        /// <summary>
        /// 地の面の外縁。m。
        ///
        /// **ここを閉じないと地平が抜ける。** 路肩の地面（<see cref="GroundMesh"/>）は
        /// ±24 までしか敷いておらず、その外は霧の色がそのまま出る。帯 1 の霧は
        /// density 0.0085 の二乗掛かりで、24 m ではまだ 96% 抜けるので、
        /// 道の左右に空の色の帯が地平まで届いて残った。市街が ±95（<see cref="CityGround"/>）で
        /// 塞いでいるのと同じことを、こちらは霧の薄いぶん遠くまで敷いてやる。
        /// 150 m なら 8 割が畳まれて空へ溶ける
        /// </summary>
        const float MoorEdge = 150f;

        /// <summary>
        /// 地の面を路肩の地面（<see cref="VergeY"/>）の上へ浮かせる量。m。
        /// 路面に重ねる面の作法（CheckDrive.RoadGap）と同じ
        /// </summary>
        const float TurfLift = 0.010f;

        /// <summary>
        /// 法面を z 方向に刻む数。区切り 1 つぶん。地形の一番短い波は 60 m なので、
        /// 4 m 刻みなら 1 波に 15 点が乗る。4 では稜線に折れが見えた
        /// </summary>
        const int BundSteps = 5;

        /// <summary>植え込みを z 方向に刻む数。区切り 1 つぶん</summary>
        const int HedgeSteps = 5;

        /// <summary>
        /// 地の面を横に割る境。道の中心からの距離。
        ///
        /// **裾と天のあいだ、天と肩のあいだにも 1 本ずつ入れてある。** 4 本で割ったときは
        /// 切り通しの稜線が折れ線になって、土手ではなく折った紙に見えた。
        /// 6 本なら区切り 1 つあたり 120 枚で、帯ぜんたいでも千枚ほどに収まる
        /// </summary>
        static readonly float[] TurfRows =
        {
            BundToe, BundMid, BundTop, BundCrown, BundBrow, MoorFrom, MoorEdge,
        };

        /// <summary>門型の間隔。m。180 を割り切る。28 m/s なら 3.2 秒に 1 基くぐる</summary>
        const float GantryStep = 90f;

        /// <summary>門型の脚の、道の中心からの距離。ガードレール（<see cref="RailAt"/>）の外</summary>
        const float GantryAt = 6.4f;

        /// <summary>
        /// 門型の桁の下端。m。
        /// **CheckDrive の Clearance（2.10）より高く取ること。** 見直しは高さ 2.10 より
        /// 上の頂点を見ないので、そこを越えていれば道を跨いでも咎められない。
        /// 逆に下回ると、道の上に物が出ていると知らせが出る
        /// </summary>
        const float GantryClear = 5.4f;

        /// <summary>行き先の看板の間隔。m。180 を割り切る。門型と半刻みずらすので 45 m ごとに何かが来る</summary>
        const float BoardStep = 90f;

        /// <summary>行き先の看板の内側の縁の、道の中心からの距離</summary>
        const float BoardAt = 6.2f;

        /// <summary>小さな標識の間隔。m。180 を割り切る</summary>
        const float PlateStep = 45f;

        /// <summary>送電塔の間隔。m。180 を割り切る</summary>
        const float PylonStep = 60f;

        /// <summary>工場と倉庫の間隔。m。180 を割り切る</summary>
        const float WorksStep = 45f;

        /// <summary>
        /// 路肩の灌木の茂みの間隔。m。180 を割り切る。
        /// 45 では環に 4 群しか無く、土手の稜線の大半が裸のままだった
        /// </summary>
        const float CopseStep = 30f;

        /// <summary>雨に滲む灯りの札の大きさ。m</summary>
        const float HaloWide = 5.4f;
        const float HaloHigh = 4.4f;

        /// <summary>
        /// 滲みの強さ。加算で重ねるので、上げ過ぎると灯りの形が消えて白い塊になる。
        /// 溜まり（0.34）より少し上げてあるのは、空気の中に立てる札で、
        /// 路面に寝かせた板より正面から見る面積が小さいため
        /// </summary>
        const float HaloGain = 0.40f;

        /// <summary>路肩の水たまりの真ん中。道の中心からの距離。舗装の縁（RoadHalf 3.5）に掛けない</summary>
        const float PuddleAt = 4.7f;
        const float PuddleWide = 2.0f;
        const float PuddleDeep = 7.0f;

        /// <summary>
        /// 帯 1 の沿道を焼く先。素材ごとに 1 つ持ち、区切り 1 つぶんをまとめて 1 枚の mesh にする
        /// </summary>
        sealed class MotorBanks
        {
            /// <summary>切り通しの壁と土手と、その先の野。地平を塞いでいるのはこれ</summary>
            public readonly Bank Turf = new Bank { Texel = 0.12f };
            /// <summary>縁石。夜の道で唯一、道の縁をまっすぐ引くもの</summary>
            public readonly Bank Kerb = new Bank { Texel = 0.5f };
            /// <summary>ガードレール・柵・門型・標識の柱。濡れた鉄は前照灯を返すので、この帯で一番明るい素材</summary>
            public readonly Bank Steel = new Bank { Texel = 0.5f };
            /// <summary>植え込みと灌木。地の面より暗くして、土手の前に黒い縁を引かせる</summary>
            public readonly Bank Scrub = new Bank { Texel = 0.3f };
            /// <summary>遠景。送電塔と工場と倉庫。空より暗い影絵にする</summary>
            public readonly Bank Far = new Bank { Texel = 0.2f };
            /// <summary>標識の地</summary>
            public readonly Bank Face = new Bank { Texel = 0.5f };
            /// <summary>標識の白い縁と文字、工場の点いた窓</summary>
            public readonly Bank Mark = new Bank { Texel = 0.5f };
            /// <summary>反射板と、遠くの街の灯り</summary>
            public readonly Bank Spark = new Bank { Texel = 0.5f };
            /// <summary>雨の夜の滲み。街灯の暈と路肩の水たまり。uv は 0〜1 を直に振る</summary>
            public readonly Bank Haze = new Bank { Texel = 1f };
        }

        /// <summary>
        /// 帯 1 の沿道ぜんたい。
        ///
        /// **一つずつ物を置いてはいけない。** 区切り 1 つに 20 m ぶんの縁石と支柱と柵と
        /// 植え込みが乗り、環ぜんたいには門型も標識も送電塔も工場も並ぶ。物で置けば
        /// レンダラーが三百を越える。市街（<see cref="City"/>）と同じ構えで、
        /// 区切りごとに素材へ焼く。焼く先は <see cref="MotorBanks"/> の 9 枚で、
        /// 中身の無い素材は <c>Bank.Emit</c> が何も置かないので、
        /// 門型の乗らない区切りにはレンダラーが増えない。
        ///
        /// **刻みはどれも環一周（<see cref="Span"/> 180 m）を割り切る。**
        /// 区切りに続けて張るもの（地の面・縁石・レール・柵・植え込み）は区切りの長さ（20）が
        /// そのまま刻みになるので必ず割り切る。環の上へ置くもの（門型 90・行き先の看板 90・
        /// 小さな標識 45・送電塔 60・工場 45・灌木 45・街灯の暈 36）は <see cref="Sow"/> が
        /// 割り切るかどうかを見て、割り切らない刻みは並べずに飛ばす。
        ///
        /// 地形の波長も同じ理由で 180 / 90 / 60 / 45 / 20 しか使わない。
        /// 割り切らない波長を混ぜると、環が一周したところで土手が段になる
        /// </summary>
        static void Wayside(Transform[] slices)
        {
            var banks = new MotorBanks[slices.Length];
            for (var i = 0; i < banks.Length; i++) banks[i] = new MotorBanks();

            // 区切りに続けて張るもの。環の上の位置は区切りの番号から出す
            for (var i = 0; i < banks.Length; i++)
            {
                var ring = i * TileLength;
                Slopes(banks[i], ring);
                Railing(banks[i]);
                Hedgerow(banks[i], ring);
                Farside(banks[i], i, ring);
            }

            // 種を決め打ちにして、組み直しても同じ沿道になるようにする
            var rnd = new System.Random(20260923);
            // 門型。道をまたぐ枠に看板を吊る。夜はここが一番よく光る
            Sow(GantryStep, 0f, 0f, rnd, (ring, k) => Put(banks, ring, (c, z) => Gantry(c, z, k)));
            // 行き先の看板。門型と半刻みずらして、45 m ごとにどちらかが来るようにする
            Sow(BoardStep, 0f, BoardStep * 0.5f, rnd, (ring, k) => Put(banks, ring, (c, z) => Signpost(c, z, k)));
            // 小さな標識。左右を振り分ける
            Sow(PlateStep, 0f, PlateStep * 0.5f, rnd, (ring, k) => Put(banks, ring, (c, z) => Plate(c, z, k)));
            // 送電塔。倫敦を出たばかりの郊外なので、野を跨いで送電線が続いている
            Sow(PylonStep, 9f, 0f, rnd, (ring, k) =>
            {
                var seed = rnd.Next();
                Put(banks, ring, (c, z) => Pylon(c, new System.Random(seed), ring, z, k));
            });
            // 工場と倉庫
            Sow(WorksStep, 11f, WorksStep * 0.5f, rnd, (ring, k) =>
            {
                var seed = rnd.Next();
                Put(banks, ring, (c, z) => Works(c, new System.Random(seed), ring, z, k));
            });
            // 路肩の灌木の茂み。土手の縁を毛羽立たせる
            Sow(CopseStep, 10f, CopseStep * 0.25f, rnd, (ring, k) =>
            {
                var seed = rnd.Next();
                Put(banks, ring, (c, z) => Copse(c, new System.Random(seed), ring, z, k));
            });
            // 雨の夜の滲み。街灯と同じ刻みに乗せる
            Sow(LampStep, 0f, 0f, rnd, (ring, k) => Put(banks, ring, (c, z) => Halos(c, z)));

            // 切り通しと土手の草。**路肩の地面（Verge 0.078）より明るくする。**
            //
            // 路肩と同じ色で塗ったときは、左右の土手が形の読めない一枚の黒い塊になり、
            // その手前に立つ植え込みとも遠景の工場とも見分けが付かなかった。
            // 立った斜面は水平の環境光しか受けないので、0.078 では画面で 20 に届かない。
            // 0.150 まで上げても、空（塗り潰しがそのまま出るので画面 94）との差は
            // 十分に残って影絵のままになる
            var turf = CityMat("MotorTurf", new Color(0.150f, 0.156f, 0.130f), 0.05f);
            // 縁石。**舗装（0.115）より明るくする。** 同じ明るさで塗ると、
            // 夜の道で道幅を読ませるものが白線だけになる
            var kerb = CityMat("MotorKerb", new Color(0.196f, 0.192f, 0.180f), 0.14f);
            // 亜鉛引きの鉄。**この帯で一番明るい素材。**
            //
            // 街灯の柱（Metal 0.085）と同じ色で塗っていては、ガードレールは夜に消える。
            // レールは道と平行に延々と続く唯一の物で、道の縁と走っている速さを
            // 同時に見せられるのはこれしかない。
            //
            // **艶は上げない。** 濡れた鉄のつもりで 0.55 まで上げたときは、
            // 脇を過ぎる支柱が日射しの反射で真っ白に飛び、レールの列に白い札が
            // 一枚だけ混ざったように見えた。夜の帯には点の灯りが無く、艶を上げても
            // 返るものが日射し（power 0.42）しか無い。濡れて見せるのは
            // 路面の艶（Sheen）と灯りの暈（<see cref="HaloMat"/>）の側に任せる
            var steel = CityMat("MotorSteel", new Color(0.250f, 0.258f, 0.268f), 0.24f);
            // 植え込みと灌木。地の面より暗くして、土手の前に黒い縁を引かせる
            var scrub = CityMat("MotorScrub", new Color(0.062f, 0.074f, 0.054f), 0.06f);
            // 遠景。空（0.115）より暗く冷たくして影絵にする。**真っ黒にはしない。**
            // 霧が 4 割から 6 割を畳むので、素の色が黒だと畳まれた先が霧の色そのものになり、
            // 工場の塊が空に溶けて輪郭だけの穴になる
            var far = CityMat("MotorFar", new Color(0.068f, 0.072f, 0.086f), 0.05f);
            // 標識の地。英国の高速の案内は青地に白。前照灯を返す面なので、
            // 灯りを置かずに明るい Unlit で済ませる。上げ過ぎると青が白へ抜ける
            var face = Glow(new Color(0.20f, 0.38f, 0.66f), 0.58f);
            // 白い縁と文字。工場の点いた窓もこれで済ませる
            var mark = Glow(new Color(0.90f, 0.93f, 1f), 0.62f);
            // 反射板と遠くの街の灯り。街灯（LampHue）と同じ橙から出す。
            // 街灯の頭（1.05）より落とす。同じ明るさにすると、脇を過ぎる反射板が
            // 灯りそのものに見えて、街灯の列の刻みが読めなくなる
            var spark = Glow(LampHue, 0.78f);
            var haze = HaloMat();

            for (var i = 0; i < slices.Length; i++)
            {
                banks[i].Turf.Emit(slices[i], "MotorTurf" + i, turf, false, Generated);
                banks[i].Kerb.Emit(slices[i], "MotorKerb" + i, kerb, false, Generated);
                banks[i].Steel.Emit(slices[i], "MotorSteel" + i, steel, false, Generated);
                banks[i].Scrub.Emit(slices[i], "MotorScrub" + i, scrub, false, Generated);
                banks[i].Far.Emit(slices[i], "MotorFar" + i, far, false, Generated);
                banks[i].Face.Emit(slices[i], "MotorSign" + i, face, false, Generated);
                banks[i].Mark.Emit(slices[i], "MotorMark" + i, mark, false, Generated);
                banks[i].Spark.Emit(slices[i], "MotorSpark" + i, spark, false, Generated);
                banks[i].Haze.Emit(slices[i], "MotorHaze" + i, haze, false, Generated);
            }
        }

        // ---- 地形 ----------------------------------------------------------

        /// <summary>
        /// 切り通しの壁の高さ。環の上の位置から引く。
        ///
        /// **波長は 180 / 90 / 60 しか使わない。** どれも環一周を割り切るので、
        /// 一周したところで地形が段にならない。0 で切ってあるので、
        /// 壁の立たない素通しの区間もできる。高速は切り通しと素通しを繰り返して走る
        /// </summary>
        static float Cutting(float side, float ring)
        {
            var p = ring * (2f * Mathf.PI / Span);
            var o = side < 0f ? 0f : 1.9f;
            return Mathf.Max(0f, 2.2f
                + 2.1f * Mathf.Sin(p + o)
                + 1.3f * Mathf.Sin(2f * p + o * 1.7f)
                + 0.7f * Mathf.Sin(3f * p + o * 0.6f));
        }

        /// <summary>
        /// 土手の外の野の高さ。**こちらは負にも落ちる**（道が盛り土の上を走っているところ）。
        /// 負へ落としてよいのは路肩の地面（±24）より外だけなので、
        /// <see cref="Rise"/> は <see cref="BundBrow"/> より内側でこれを使わない
        /// </summary>
        static float Moor(float side, float ring)
        {
            var p = ring * (2f * Mathf.PI / Span);
            var o = side < 0f ? 0.8f : 2.6f;
            return -1.0f + 1.9f * Mathf.Sin(p + o) + 0.9f * Mathf.Sin(2f * p + o * 1.4f);
        }

        /// <summary>
        /// 沿道の地の高さ。道の中心からの距離と環の上の位置で引く。
        ///
        /// 裾（<see cref="BundToe"/>）では必ず路肩の地面と同じ高さになる。ここを動かすと、
        /// 敷いてある路肩の地面との境に段が出る
        /// </summary>
        static float Rise(float side, float ring, float from)
        {
            var wall = Cutting(side, ring);
            float up;
            if (from <= BundToe) up = 0f;
            else if (from <= BundTop)
                // 裾と天で傾きが 0 になる S 字。直線の斜面は、稜線の折れがそのまま影の縁になる
                up = wall * Mathf.SmoothStep(0f, 1f, (from - BundToe) / (BundTop - BundToe));
            else if (from <= BundBrow)
                up = Mathf.Lerp(wall, wall * 0.55f,
                    Mathf.SmoothStep(0f, 1f, (from - BundTop) / (BundBrow - BundTop)));
            else if (from <= MoorFrom)
                up = Mathf.Lerp(wall * 0.55f, Moor(side, ring), (from - BundBrow) / (MoorFrom - BundBrow));
            else up = Moor(side, ring);
            return VergeY + TurfLift + up;
        }

        /// <summary>
        /// 切り通しと土手と野。区切り 1 つぶんを升目に割って張る。
        ///
        /// 升の高さは環の上の位置から引くので、隣の区切りと継ぎ目で必ず揃う。
        /// uv は升の座標からじかに振る（<see cref="Bank.Patch"/>）。
        /// 隅どうしの距離から振ると、斜面のぶんだけ伸びて隣の升とずれる
        /// </summary>
        static void Slopes(MotorBanks c, float ring0)
        {
            var dz = TileLength / BundSteps;
            for (var s = 0; s < 2; s++)
            {
                var side = s == 0 ? -1f : 1f;
                for (var k = 0; k < BundSteps; k++)
                {
                    var z0 = k * dz;
                    var z1 = z0 + dz;
                    var r0 = ring0 + z0;
                    var r1 = ring0 + z1;
                    for (var n = 0; n + 1 < TurfRows.Length; n++)
                    {
                        // 面を上へ向けるには x の小さい側から回す。
                        // 左側は道から遠いほど x が小さくなるので、そこで入れ替わる
                        var dA = TurfRows[n];
                        var dB = TurfRows[n + 1];
                        if (side < 0f) { var swap = dA; dA = dB; dB = swap; }
                        var xA = Lane(dA * side);
                        var xB = Lane(dB * side);
                        c.Turf.Patch(
                            new Vector3(xA, Rise(side, r1, dA), z1),
                            new Vector3(xB, Rise(side, r1, dB), z1),
                            new Vector3(xB, Rise(side, r0, dB), z0),
                            new Vector3(xA, Rise(side, r0, dA), z0),
                            new Vector2(xA * 0.12f, -z1 * 0.12f),
                            new Vector2(xB * 0.12f, -z1 * 0.12f),
                            new Vector2(xB * 0.12f, -z0 * 0.12f),
                            new Vector2(xA * 0.12f, -z0 * 0.12f));
                    }
                }
            }
        }

        // ---- 道の構え ------------------------------------------------------

        /// <summary>
        /// 縁石・ガードレール・反射板・境の柵。区切り 1 つぶんを左右に張る。
        ///
        /// **支柱と反射板は札 1 枚で足りる。** 車は原点で +z を向いたまま動かないので、
        /// 沿道の物の裏側は一度も画面に入らない。描画解像度 427 × 240 では支柱 1 本が
        /// 数画素しか無く、箱にして 12 枚使う謂れが無い。
        /// レールと柵の胴だけは環の継ぎ目で断面が覗くので箱にしてある
        /// </summary>
        static void Railing(MotorBanks c)
        {
            for (var s = 0; s < 2; s++)
            {
                var side = s == 0 ? -1f : 1f;
                // 道を向く向き。左側の物は +x、右側の物は -x に法線を向ける
                var look = side < 0f ? 1 : -1;
                var inner = Lane(KerbFrom * side);
                var outer = Lane(KerbTo * side);
                // 縁石。天端と、道を向いた立ち上がり
                c.Kerb.FaceY(KerbY, Mathf.Min(inner, outer), Mathf.Max(inner, outer), 0f, TileLength, 1);
                c.Kerb.FaceX(inner, 0f, TileLength, -ShoulderDrop, KerbY, look);
                // ガードレールの帯
                c.Steel.Box(new Vector3(Lane(RailAt * side), RailY, TileLength * 0.5f),
                    new Vector3(0.09f, 0.34f, TileLength));
                // 境の柵の胴
                c.Steel.Box(new Vector3(Lane(FenceAt * side), FenceY, TileLength * 0.5f),
                    new Vector3(0.05f, 0.05f, TileLength));
                // 支柱
                for (var z = RailStep * 0.5f; z < TileLength - 0.001f; z += RailStep)
                {
                    c.Steel.FaceX(Lane((RailAt - 0.05f) * side), z - 0.09f, z + 0.09f, VergeY, RailY + 0.10f, look);
                    c.Steel.FaceX(Lane((FenceAt - 0.03f) * side), z - 0.055f, z + 0.055f, VergeY, FenceY + 0.04f, look);
                }
                // 反射板。**道ではなく手前（-z）を向ける。**
                // 道を向けた札は真横を過ぎる瞬間しか面にならず、遠くで列にならない。
                // レールの内側の面のすぐ手前へ浮かせて、レールの箱に埋めない
                var a = Lane((RailAt - 0.05f) * side);
                var b = Lane((RailAt - 0.23f) * side);
                for (var z = StudStep * 0.5f; z < TileLength - 0.001f; z += StudStep)
                    c.Spark.FaceZ(z, Mathf.Min(a, b), Mathf.Max(a, b), RailY - 0.055f, RailY + 0.055f, -1);
            }
        }

        /// <summary>
        /// 道に沿って続く植え込み。区切り 1 つぶんを z に刻んで、道を向いた面と天を張る。
        ///
        /// **背は目線（1.55）より低く抑える。** 越えると、土手も遠景も送電塔も
        /// これ一枚で塞がれて、沿道が黒い塀の続く道になる。
        /// 外を向いた面は張らない。道の側からしか見ないので、要るのは内と天だけ
        /// </summary>
        static void Hedgerow(MotorBanks c, float ring0)
        {
            var dz = TileLength / HedgeSteps;
            for (var s = 0; s < 2; s++)
            {
                var side = s == 0 ? -1f : 1f;
                var xi = Lane(HedgeFrom * side);
                var xo = Lane(HedgeTo * side);
                var x0 = Mathf.Min(xi, xo);
                var x1 = Mathf.Max(xi, xo);
                var low = VergeY;
                for (var k = 0; k < HedgeSteps; k++)
                {
                    var z0 = k * dz;
                    var z1 = z0 + dz;
                    var h0 = Bush(side, ring0 + z0);
                    var h1 = Bush(side, ring0 + z1);
                    // 道を向いた面。FaceX と同じ回し方で、高さだけ z ごとに振る
                    if (side < 0f)
                        c.Scrub.Patch(
                            new Vector3(xi, low, z1), new Vector3(xi, low, z0),
                            new Vector3(xi, h0, z0), new Vector3(xi, h1, z1),
                            new Vector2(z1 * 0.3f, low * 0.3f), new Vector2(z0 * 0.3f, low * 0.3f),
                            new Vector2(z0 * 0.3f, h0 * 0.3f), new Vector2(z1 * 0.3f, h1 * 0.3f));
                    else
                        c.Scrub.Patch(
                            new Vector3(xi, low, z0), new Vector3(xi, low, z1),
                            new Vector3(xi, h1, z1), new Vector3(xi, h0, z0),
                            new Vector2(z0 * 0.3f, low * 0.3f), new Vector2(z1 * 0.3f, low * 0.3f),
                            new Vector2(z1 * 0.3f, h1 * 0.3f), new Vector2(z0 * 0.3f, h0 * 0.3f));
                    // 天
                    c.Scrub.Patch(
                        new Vector3(x0, h1, z1), new Vector3(x1, h1, z1),
                        new Vector3(x1, h0, z0), new Vector3(x0, h0, z0),
                        new Vector2(x0 * 0.3f, -z1 * 0.3f), new Vector2(x1 * 0.3f, -z1 * 0.3f),
                        new Vector2(x1 * 0.3f, -z0 * 0.3f), new Vector2(x0 * 0.3f, -z0 * 0.3f));
                }
            }
        }

        /// <summary>
        /// 植え込みの背。波長は 45 と 20 で、どちらも環一周を割り切る。
        /// 天端は 0.20〜1.28 m（路肩の地面から）で、目線には届かない
        /// </summary>
        static float Bush(float side, float ring)
        {
            var p = ring * (2f * Mathf.PI / Span);
            var o = side < 0f ? 0.4f : 2.2f;
            return VergeY + 0.80f + 0.28f * Mathf.Sin(4f * p + o) + 0.20f * Mathf.Sin(9f * p + o * 2f);
        }

        // ---- 標識と門型 ----------------------------------------------------

        /// <summary>
        /// 道をまたぐ門型。脚を左右に立て、桁を渡し、車線ごとに看板を吊る。
        ///
        /// **桁と看板は必ず 2.10 m より上に置く。** CheckDrive.OnRoad は高さ 2.10 を
        /// 越える頂点を見ないので、そこより上なら道を跨いでよい。
        /// 脚の方は下まで通っているので、道の中心から 3.5 m より外に立てる
        /// </summary>
        static void Gantry(MotorBanks c, float z, int k)
        {
            var xL = Lane(-GantryAt);
            var xR = Lane(GantryAt);
            var top = GantryClear + 1.6f;
            var mid = (xL + xR) * 0.5f;
            var span = xR - xL + 0.34f;
            // 脚
            c.Steel.Box(new Vector3(xL, (top + VergeY) * 0.5f, z), new Vector3(0.34f, top - VergeY, 0.34f));
            c.Steel.Box(new Vector3(xR, (top + VergeY) * 0.5f, z), new Vector3(0.34f, top - VergeY, 0.34f));
            // 桁。上弦と下弦の二本に割ると、一本の梁より桁組みに見える
            c.Steel.Box(new Vector3(mid, top - 0.24f, z), new Vector3(span, 0.48f, 0.42f));
            c.Steel.Box(new Vector3(mid, GantryClear - 0.13f, z), new Vector3(span, 0.26f, 0.32f));
            // 看板。車線ごとに 1 枚ずつ吊る
            for (var p = 0; p < 2; p++)
            {
                var at = Lane(p == 0 ? -RoadHalf * 0.5f : RoadHalf * 0.5f);
                Board(c, at, z - 0.24f, 3.1f, GantryClear - 0.32f, 1.72f, k * 2 + p);
            }
        }

        /// <summary>
        /// 路肩に立つ行き先の看板。左右を一つおきに振り分ける。
        /// 揃えると、環を一周するあいだ片側にだけ看板が並ぶ
        /// </summary>
        static void Signpost(MotorBanks c, float z, int k)
        {
            var side = k % 2 == 0 ? -1f : 1f;
            const float wide = 4.8f;
            const float tall = 2.7f;
            const float top = 4.7f;
            var inner = Lane(BoardAt * side);
            var outer = Lane((BoardAt + wide) * side);
            // 柱。板の左右から 1 本ずつ下ろす
            for (var p = 0; p < 2; p++)
            {
                var x = Lane((BoardAt + 0.9f + p * (wide - 1.8f)) * side);
                c.Steel.Box(new Vector3(x, (top - tall + VergeY) * 0.5f, z + 0.14f),
                    new Vector3(0.22f, top - tall - VergeY, 0.22f));
            }
            Board(c, (inner + outer) * 0.5f, z, wide, top, tall, k + 31);
        }

        /// <summary>小さな標識。速さの札や注意の札にあたる</summary>
        static void Plate(MotorBanks c, float z, int k)
        {
            var side = k % 2 == 0 ? 1f : -1f;
            var x = Lane(6.2f * side);
            c.Steel.Box(new Vector3(x, (2.95f + VergeY) * 0.5f, z + 0.11f),
                new Vector3(0.14f, 2.95f - VergeY, 0.14f));
            c.Face.FaceZ(z, x - 0.55f, x + 0.55f, 1.85f, 2.95f, -1);
            c.Mark.FaceZ(z - 0.012f, x - 0.40f, x + 0.40f, 2.24f, 2.50f, -1);
        }

        /// <summary>
        /// 標識の板 1 枚。青い地を 1 枚張って、白い帯を何本か重ねて文字に見せる。
        ///
        /// **絵は貼らない。** 描画解像度 427 × 240 では文字が画素に届かず、
        /// 読めない絵柄の繰り返しだけが残る。夜の高速で効いているのは、
        /// 前照灯を受けて浮かぶ青い面と、そこに並ぶ白い帯の並びの方。
        /// 裏は張らない。くぐったあとを振り返ることはない
        /// </summary>
        static void Board(MotorBanks c, float at, float z, float wide, float top, float tall, int seed)
        {
            var x0 = at - wide * 0.5f;
            var x1 = at + wide * 0.5f;
            var y0 = top - tall;
            c.Face.FaceZ(z, x0, x1, y0, top, -1);
            var rnd = new System.Random(seed * 7919 + 13);
            // 白い縁。板の上端に一本通す
            c.Mark.FaceZ(z - 0.012f, x0 + 0.10f, x1 - 0.10f, top - 0.17f, top - 0.09f, -1);
            // 文字の帯。長さを振ると行に見える
            var rows = 2 + rnd.Next(2);
            var step = (tall - 0.48f) / rows;
            for (var r = 0; r < rows; r++)
            {
                var y = y0 + 0.16f + r * step;
                var run = (wide - 0.6f) * (0.34f + (float)rnd.NextDouble() * 0.52f);
                c.Mark.FaceZ(z - 0.012f, x0 + 0.28f, x0 + 0.28f + run, y, y + step * 0.44f, -1);
            }
        }

        // ---- 遠景 ----------------------------------------------------------

        /// <summary>
        /// 送電塔。倫敦を出たばかりの郊外なので、野を跨いで送電線が続いている。
        ///
        /// **腕は z の向きへ伸ばす。** 道の脇に立つものを道から見るので、
        /// x へ伸ばした腕は奥行きの向きに潰れて、ただの柱にしか見えない。
        /// 送電線そのものは張らない。4 cm の線は 60 m 先で画素に届かない
        /// </summary>
        static void Pylon(MotorBanks c, System.Random rnd, float ring, float z, int k)
        {
            var side = k % 2 == 0 ? -1f : 1f;
            var from = 46f + (float)rnd.NextDouble() * 30f;
            var foot = Rise(side, ring, from);
            var high = 23f + (float)rnd.NextDouble() * 8f;
            var x = Lane(from * side);
            var waist = high * 0.52f;
            var spread = high * 0.19f;
            // 脚。四隅から腰へすぼめる
            for (var q = 0; q < 4; q++)
            {
                var sx = (q & 1) == 0 ? -1f : 1f;
                var sz = (q & 2) == 0 ? -1f : 1f;
                Strut(c.Far,
                    new Vector3(x + sx * spread, foot, z + sz * spread),
                    new Vector3(x + sx * 0.85f, foot + waist, z + sz * 0.85f), 0.38f);
            }
            // 胴
            c.Far.Box(new Vector3(x, foot + (waist + high) * 0.5f, z), new Vector3(1.7f, high - waist, 1.7f));
            // 腕。上へ行くほど短く
            for (var r = 0; r < 3; r++)
            {
                var y = foot + waist + (high - waist) * (0.20f + r * 0.32f);
                c.Far.Box(new Vector3(x, y, z), new Vector3(0.9f, 0.5f, 11f - r * 2.6f));
            }
            // 頂
            c.Far.Box(new Vector3(x, foot + high + 0.9f, z), new Vector3(0.55f, 1.8f, 0.55f));
        }

        /// <summary>
        /// 工場と倉庫。夜勤の窓が点いていて、煙突の天辺に赤い灯りが灯る。
        /// 道からは影絵にしか見えないので、形は長く低い塊と煙突だけでよい
        /// </summary>
        static void Works(MotorBanks c, System.Random rnd, float ring, float z, int k)
        {
            var side = k % 2 == 0 ? 1f : -1f;
            var from = 44f + (float)rnd.NextDouble() * 32f;
            var deep = 14f + (float)rnd.NextDouble() * 16f;
            var run = 22f + (float)rnd.NextDouble() * 26f;
            var high = 7f + (float)rnd.NextDouble() * 6f;
            var foot = Rise(side, ring, from);
            var xi = Lane(from * side);
            var xo = Lane((from + deep) * side);
            var x0 = Mathf.Min(xi, xo);
            var x1 = Mathf.Max(xi, xo);
            c.Far.Box(new Vector3((x0 + x1) * 0.5f, foot + high * 0.5f, z + run * 0.5f),
                new Vector3(x1 - x0, high, run));
            // 屋上の小屋。空との境を毛羽立たせる
            c.Far.Box(new Vector3((x0 + x1) * 0.5f, foot + high + 1.1f, z + run * 0.3f),
                new Vector3((x1 - x0) * 0.45f, 2.2f, run * 0.22f));
            if (rnd.NextDouble() < 0.6)
            {
                // 煙突。天辺の赤い灯りは航空障害灯
                var tall = high + 12f + (float)rnd.NextDouble() * 15f;
                var cx = Lane((from + deep * 0.7f) * side);
                var cz = z + run * 0.24f;
                c.Far.Box(new Vector3(cx, foot + tall * 0.5f, cz), new Vector3(2.2f, tall, 2.2f));
                c.Spark.FaceZ(cz - 1.2f, cx - 0.75f, cx + 0.75f, foot + tall - 1.4f, foot + tall - 0.5f, -1);
            }
            // 点いた窓。手前（-z）の面だけに並べる。向こう側は一度も画面に入らない
            var rows = 1 + rnd.Next(2);
            for (var r = 0; r < rows; r++)
            {
                var y = foot + high * (0.34f + r * 0.30f);
                for (var p = 0; p < 4; p++)
                {
                    var b0 = x0 + 1.2f + p * (x1 - x0 - 2.4f) / 4f;
                    if (rnd.NextDouble() < 0.35) continue;
                    c.Mark.FaceZ(z - 0.06f, b0, b0 + (x1 - x0 - 2.4f) / 6f, y, y + high * 0.13f, -1);
                }
            }
        }

        /// <summary>
        /// 遠くの街の灯り。野の上に橙の点が帯になって散る。
        ///
        /// **地平を灯りだけで見せる。** 92〜144 m の野は霧が 6 割から 8 割を畳むので、
        /// 建物を置いても形にならない。灯りは加算ではなく明るい面なので、
        /// 霧に混ぜられてなお空より明るく残る
        /// </summary>
        static void Farside(MotorBanks c, int slice, float ring0)
        {
            var rnd = new System.Random(slice * 131 + 17);
            for (var s = 0; s < 2; s++)
            {
                var side = s == 0 ? -1f : 1f;
                for (var n = 0; n < 6; n++)
                {
                    var from = 92f + (float)rnd.NextDouble() * 52f;
                    var z = (float)rnd.NextDouble() * TileLength;
                    var x = Lane(from * side);
                    var y = Rise(side, ring0 + z, from) + 0.5f + (float)rnd.NextDouble() * 3.6f;
                    var wide = 0.9f + (float)rnd.NextDouble() * 1.9f;
                    c.Spark.FaceZ(z, x - wide * 0.5f, x + wide * 0.5f, y, y + 0.55f, -1);
                }
            }
        }

        /// <summary>路肩の灌木の茂み。土手の縁を毛羽立たせて、法面が板に見えないようにする</summary>
        static void Copse(MotorBanks c, System.Random rnd, float ring, float z, int k)
        {
            var side = k % 2 == 0 ? -1f : 1f;
            var n = 2 + rnd.Next(3);
            for (var i = 0; i < n; i++)
            {
                var from = 12f + (float)rnd.NextDouble() * 9f;
                var at = z + i * 3.4f;
                var foot = Rise(side, ring + i * 3.4f, from);
                var high = 2.2f + (float)rnd.NextDouble() * 2.6f;
                var wide = 1.8f + (float)rnd.NextDouble() * 2.2f;
                c.Scrub.Box(new Vector3(Lane(from * side), foot + high * 0.5f, at),
                    new Vector3(wide, high, wide * 1.2f));
            }
        }

        // ---- 雨の夜の滲み --------------------------------------------------

        /// <summary>
        /// 街灯の暈と、路肩の水たまり。**雨の夜はどの灯りにも暈が付く。**
        ///
        /// 灯りの頭のところへ加算の札を十字に立てる。1 枚だと真横を過ぎる瞬間に
        /// 面が消えて、暈が瞬いたように見える。
        /// 札は高さ 4.85〜9.05 m にあるので、道の上へ広がっても
        /// CheckDrive.OnRoad は見ない（2.10 より上の頂点は見ないため）。
        ///
        /// 水たまりは路肩の上。舗装の縁（RoadHalf 3.5）に掛からないところへ置く
        /// </summary>
        static void Halos(MotorBanks c, float z)
        {
            for (var s = 0; s < 2; s++)
            {
                var side = s == 0 ? -1f : 1f;
                var at = new Vector3(Lane((LampAt - LampArm) * side), 6.95f, z);
                Blot(c.Haze, at, new Vector3(HaloWide * 0.5f, 0f, 0f), new Vector3(0f, HaloHigh * 0.5f, 0f));
                Blot(c.Haze, at, new Vector3(0f, 0f, HaloWide * 0.5f), new Vector3(0f, HaloHigh * 0.5f, 0f));
                Blot(c.Haze,
                    new Vector3(Lane(PuddleAt * side), -ShoulderDrop + 0.010f, z + 2.6f),
                    new Vector3(PuddleWide * 0.5f, 0f, 0f), new Vector3(0f, 0f, PuddleDeep * 0.5f));
            }
        }

        /// <summary>
        /// 滲みの札 1 枚。uv を 0〜1 で直に振るので、絵は札ごとにちょうど 1 枚ぶん出る。
        /// <see cref="Card"/> と <see cref="GlowMat"/> の組は寝かせた板しか作れず、
        /// 空気の中に立てる札はこちらで作る。
        /// 表裏は見ない。HalfAware/RoadGlow は Cull Off で両面とも塗る
        /// </summary>
        static void Blot(Bank b, Vector3 at, Vector3 across, Vector3 up)
        {
            b.Patch(at - across - up, at + across - up, at + across + up, at - across + up,
                new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f));
        }

        /// <summary>
        /// 雨に滲む灯りのマテリアル。加算なので、暗いところには何も足さない。
        /// <see cref="GlowMat"/> と違って絵を 0〜1 へ畳み直さないのは、
        /// 札の側が uv をすでに 0〜1 で持っているため
        /// </summary>
        static Material HaloMat()
        {
            var shader = Shader.Find("HalfAware/RoadGlow");
            if (shader == null) Debug.LogWarning("HalfAware/RoadGlow が見つからない。灯りの暈が出ない");
            var path = Materials + "MotorHalo.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(shader);
                m.name = "MotorHalo";
                AssetDatabase.CreateAsset(m, path);
            }
            // 組み直すたびに結び直す。手で触った値は残らない
            m.shader = shader;
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Textures/DrivePool.png");
            if (tex == null) Debug.LogWarning("灯りの絵が無い: Assets/Textures/DrivePool.png");
            m.SetTexture("_BaseMap", tex);
            m.SetColor("_BaseColor",
                new Color(LampHue.r * HaloGain, LampHue.g * HaloGain, LampHue.b * HaloGain, 1f));
            m.SetTextureScale("_BaseMap", Vector2.one);
            m.SetTextureOffset("_BaseMap", Vector2.zero);
            EditorUtility.SetDirty(m);
            return m;
        }

        /// <summary>2 点を結ぶ細い柱。送電塔の脚のように傾いだものを置く</summary>
        static void Strut(Bank b, Vector3 from, Vector3 to, float thick)
        {
            var dir = to - from;
            var len = dir.magnitude;
            if (len < 0.01f) return;
            b.Box((from + to) * 0.5f, new Vector3(thick, len, thick),
                Quaternion.FromToRotation(Vector3.up, dir / len));
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
        /// 最後の帯。朝靄の未舗装路。黄金色の小麦畑と土の轍、畑に建つ農家。
        ///
        /// 原作の「朝靄の中で、緩やかな湾曲を描いて広がる小麦畑が黄金色に微風になびいている」。
        /// **畑は視界を埋めるところまで広げる。** 道の脇に麦を数本立てただけでは、
        /// 窓を開けて息を吸い込む場面にならない。株は 40 m まで、地の面は 150 m まで敷いて、
        /// その先は霧が畳む。なびかせるのは株のマテリアル（HalfAware/Wheat）で、
        /// 株ごとに Transform を持たせる手は取れない。区切り 1 つに 600 を越える札が
        /// 1 枚の mesh へ焼かれているため。
        ///
        /// 農家は <see cref="Crofts"/>。独白を送り切ってから暗転までの余韻に
        /// 通り過ぎるのはこれで、麦だけでは「町に着いた」ところにならない
        /// </summary>
        static void Furrows(Transform[] slices)
        {
            // 舗装のタイルはそのまま下に敷いてあるので、土の面で覆い隠す。
            // この帯だけタイルを差し替える手は取らない。タイルは 1 種しか無い。
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
            Crofts(slices);
        }

        // ---- 畑に建つ農家 --------------------------------------------------

        /// <summary>
        /// 農家ひと構えの間隔。m。片側ぶんの刻みで、左右は半刻みずらす。
        ///
        /// **環一周（<see cref="Span"/> 180 m）を割り切ること。** 割り切らないと、
        /// 環が一周したところで一構えぶん間隔が飛ぶ
        /// （<see cref="Sow"/> が知らせて並べずに飛ばす）。
        /// 60 より大きくて 180 を割り切るのは 90 と 180 だけ。
        ///
        /// 90 なら片側 2 構え、左右あわせて 45 m にひと構え。走る速さは 11 m/s で、
        /// 独白を送り切ってからの余韻が 10 秒＝110 m なので、そのあいだに二つか三つが
        /// 通り過ぎる。**60 では多すぎた。** 環に 6 構えが並び、110 m に三つ四つが
        /// 続けて来て、畑に散った農家ではなく街道沿いの集落に見えた。
        /// 180 まで広げると環に 2 構えしか無く、余韻に一つか二つしか来ない
        /// </summary>
        const float CroftStep = 90f;

        /// <summary>
        /// 刻みから前後へばらけさせる幅。m。原作は「好き勝手な間隔で並んでいて」。
        /// 刻みどおりに並べると、畑に建つ農家ではなく街道沿いの宿場に見える。
        /// 環の上で畳むので、一周しても並びは繋がったまま
        /// </summary>
        const float CroftWobble = 14f;

        /// <summary>
        /// 母屋の道からの距離。m。
        ///
        /// **奥は稜線より内側に留める。** 主な背は道から 78 m のところに出るので
        /// （<see cref="CrestAt"/> ＋ <see cref="SwellFrom"/>）、そこを越えて置くと
        /// 丘の裏に落ちて屋根の先だけが覗く。
        /// 手前は、構えの境（母屋の 10 m ほど手前）が道際の裸地まで下りてこない距離まで
        /// </summary>
        const float CroftNear = 22f;
        const float CroftFar = 48f;

        /// <summary>軒の出。m。白い屋根の下に鼻隠しの暗い線を一本入れて、屋根の縁を読ませる</summary>
        const float CroftEaves = 0.30f;

        /// <summary>窓と戸口を壁から浮かせる量。m。壁と同じ面に置くと深度で削り合う</summary>
        const float CroftProud = 0.04f;

        /// <summary>門の口の広さ。m。<see cref="Downs"/> の門と同じ寸法にしてある</summary>
        const float CroftGate = 3.0f;

        /// <summary>
        /// 農家をまとめて伏せる入れ物の名前。**区切り 1 つに 1 つ置く。**
        ///
        /// 小麦畑に入った直後は麦だけを見せ、独白を送り切ってから家を出す。
        /// 出し分けるのは <c>DriveDirector</c> で、こちらは伏せた入れ物を用意するだけ。
        ///
        /// **帯の直下には置けない。** DriveWorld.Place は帯の childCount を環の枠の数に
        /// 使うので、区切り以外を 1 つ混ぜるだけで環が 10 枠になり、道と沿道の継ぎ目が
        /// 丸ごとずれる（<see cref="Slices"/> の説明と同じ理由）。区切りの下へ入れれば
        /// 環の枠は 9 のままで、伏せても出しても並びは動かない。
        /// 区切りは Place が走った距離で毎フレーム置き直すので、
        /// 伏せているあいだも家は区切りに連れて正しい位置へ運ばれている。
        ///
        /// 農家の乗らない区切りにも空の入れ物を作る。出し分ける側が
        /// 「どの区切りにも同じ名前の子が 1 つある」とだけ知っていればよくするため
        /// </summary>
        public const string CroftHolder = "Crofts";

        /// <summary>農家ひと構えの割り付け。環の上の位置、道からの距離、左右、描き分けの種</summary>
        public struct CroftPlot
        {
            public float ring;
            public float from;
            public float side;
            public int seed;
        }

        /// <summary>
        /// 農家の割り付け。**組み立てとは別に呼べる形にしてある。**
        ///
        /// 一構えずつ素材へ焼くので、組み上がった mesh からは一軒ごとの位置が読めない。
        /// 環が一周したところで並びが飛んでいないかを数で検める術がここしか無く、
        /// 組み立てのときに控えを静的な入れ物へ残す手は取れない。
        /// Editor の静的な値はスクリプトを組み直すたびに消えるので、
        /// 組み直したあとで検めようとすると空になっている。
        /// 種を決め打ちにして、いつ呼んでも同じ並びを引き直す
        /// </summary>
        public static List<CroftPlot> CroftPlan()
        {
            var all = new List<CroftPlot>();
            var rnd = new System.Random(20260922);
            for (var s = 0; s < 2; s++)
            {
                var side = s == 0 ? -1f : 1f;
                // 左右で半刻みずらす。揃えると道を挟んで家が対で並び、
                // 畑に散った農家ではなく門前町の並びになる
                Sow(CroftStep, CroftWobble, s == 0 ? 0f : CroftStep * 0.5f, rnd, (ring, k) =>
                    all.Add(new CroftPlot
                    {
                        ring = ring,
                        // 道からの距離は一つおきに手前と奥を引く。**一様に引いてはいけない。**
                        // 実際、一様に引いたときは片側の三軒が三軒とも 36 m より先に固まり、
                        // 近くを通り過ぎる家が一軒も無かった。遠い家は畑の向こうの点景で、
                        // 「家々が通り過ぎる」という絵を作るのは近い家の方
                        from = Mathf.Lerp(CroftNear, CroftFar, (k % 2 + (float)rnd.NextDouble()) * 0.5f),
                        side = side,
                        // 中身には別の乱数を渡す。区切りを索く順に描くことになるので、
                        // 一つの乱数を使い回すと並べ方を一つ直しただけで集落が丸ごと変わる
                        seed = rnd.Next(),
                    }));
            }
            return all;
        }

        /// <summary>
        /// 農家を焼く先。素材ごとに 1 つ持ち、区切り 1 つぶんをまとめて 1 枚の mesh にする
        /// </summary>
        sealed class CroftBanks
        {
            /// <summary>漆喰を掛けた石の壁と煙突、石垣の胴</summary>
            public readonly Bank Wall = new Bank { Texel = 0.3f };
            /// <summary>白い屋根。母屋も納屋も同じ白で塗る</summary>
            public readonly Bank Roof = new Bank { Texel = 0.3f };
            /// <summary>暗いもの。窓・戸口・鼻隠し・笠石・門・生け垣・防風の木立</summary>
            public readonly Bank Dark = new Bank { Texel = 0.4f };
        }

        /// <summary>
        /// 畑に建つ農家。母屋と納屋、構えの境（石垣か生け垣）、門、防風の木立。
        ///
        /// 原作の「麦畑を突っ切った先にはいくつかの家々が好き勝手な間隔で並んでいて、
        /// どれも清潔感のある白い屋根をしている」。**白い屋根は原作が名指しで挙げている
        /// ただ一つの特徴なので、母屋も納屋も同じ白で塗る。** 黄金色の畑に白が点々と
        /// 続くことそのものが、この場面の終いの絵になる。
        ///
        /// **一軒ずつ物を置いてはいけない。** 区切りごとに素材 3 枚（壁・屋根・暗いもの）へ
        /// 焼く。市街のビル（<see cref="City"/>）と同じ構えで、置き方も <see cref="Put"/> を
        /// 通す。物で置けばひと構えに十を越えるレンダラーが要り、この帯は麦だけでもう
        /// 36 枚ある。焼けば、農家が乗った区切りだけが素材 3 枚ぶん増える。
        ///
        /// **麦は避けてくれない。** 株は区切り 1 枚の mesh へ先に焼いてあり、どの区切りでも
        /// 同じ 1 枚を使い回すので、農家のところだけ株を抜くことはできない。
        /// 足元が麦に埋まるのはそれで正しい――畑は母屋の壁際まで来ている。
        ///
        /// **家は伏せて置く。** 畑に入った直後は麦だけで、独白を送り切ってから家が現れる。
        /// 焼いた 3 枚は区切りごとの入れ物（<see cref="CroftHolder"/>）へ入れて
        /// 伏せておき、出すのは <c>DriveDirector</c> に任せる
        /// </summary>
        static void Crofts(Transform[] slices)
        {
            var banks = new CroftBanks[slices.Length];
            for (var i = 0; i < banks.Length; i++) banks[i] = new CroftBanks();
            foreach (var plot in CroftPlan())
            {
                var p = plot;
                Put(banks, p.ring, (c, z) => Stead(c, new System.Random(p.seed), p.side, p.from, z));
            }

            // 漆喰を掛けた石の壁。**石そのものの色（<see cref="Mat"/> の Stone、0.165）では暗すぎる。**
            // あちらは薄明の帯のために置いた値で、この帯は日射しが 1.46 倍で入る。
            // 黄金色の畑（画面で 177,145,75）の中に 0.165 の壁を置くと、屋根の下が
            // 影絵になって、白い屋根だけが畑に浮いて見える
            var harl = CityMat("CroftHarl", new Color(0.300f, 0.286f, 0.258f), 0.06f);
            // 白い屋根。原作の「清潔感のある白い屋根」。**壁より確かに明るくする。**
            //
            // 0.556 で置いたときは、日を受ける流れが霧を通したところで画面 213、
            // 壁が 184 にしかならず、屋根と壁が同じ一枚の灰色に見えた。
            // 霧（20 m で 15%、40 m で 27%）がどちらも靄の白へ寄せるので、
            // 素の色の差はそのぶん詰まる。詰まったあとで差が残る値にしてある。
            // 棟を境に片流れずつ明暗が割れるので、屋根そのものが日の向きを教える
            var lime = CityMat("CroftRoof", new Color(0.630f, 0.626f, 0.600f), 0.10f);
            // 暗いもの。窓の硝子も生け垣も木立も、この距離では暗い塊としてしか読めない。
            // 一枚にまとめれば素材が増えない。わずかに緑へ寄せてあるのは、
            // 木立と生け垣の方が量として多いため。
            //
            // **0.062 では穴になる。** 右手の構えは道を向いた面に日が回らないので
            // （日は後ろ寄りの右から差す）、家も垣も木立もまとめて日陰に入る。
            // そこへ真っ黒に近い素材を置くと、脇を過ぎるあいだ右の畑に黒い帯が乗る。
            // 0.085 まで上げても、硝子は日の当たる壁（画面 184）に対して 49〜95 に留まり、
            // 窓が窓として読める暗さは変わらない
            var shade = CityMat("CroftShade", new Color(0.085f, 0.095f, 0.072f), 0.08f);

            for (var i = 0; i < slices.Length; i++)
            {
                // 家は区切りの下の入れ物へ入れて、伏せたまま置く（<see cref="CroftHolder"/>）。
                // 麦・土・轍は入れ物の外なので、伏せても畑はそのまま出たまま
                var holder = Child(slices[i], CroftHolder);
                // Emit は面が 1 枚も無ければ何も置かない。農家の乗らない区切りには
                // レンダラーが増えない。入れ物だけは空でも作る
                banks[i].Wall.Emit(holder, "CroftHarl" + i, harl, false, Generated);
                banks[i].Roof.Emit(holder, "CroftRoof" + i, lime, false, Generated);
                banks[i].Dark.Emit(holder, "CroftShade" + i, shade, false, Generated);
                holder.gameObject.SetActive(false);
            }
        }

        /// <summary>
        /// ひと構え。母屋を据えて、納屋と境と門と木立をその周りへ置く。
        ///
        /// 向きは左右で変える。**日射しの向きが決まっているので、揃えると片側が丸損になる。**
        ///
        /// 日は後ろ寄りの右（方位 298 度）から差すので、明るいのは +x を向いた面
        /// （日射しの 86%）と -z を向いた面（46%）だけ。
        /// 左手の家は棟を道と平行にすれば、道を向いた屋根の流れが +x を向いて明るい。
        /// 右手の家は同じことをすると道を向いた流れが -x になり、白い屋根がどこにも
        /// 出ない。実際に組んで見たところ、右手の家は屋根も壁も日陰の一色の塊だった。
        /// 棟を道と直交させれば、近づいてくるあいだ見えている流れが -z を向いて明るく、
        /// 戸口の並ぶ面も同じ -z を向く。
        ///
        /// それでも四軒に一軒ほどは逆を取る。全部を同じ向きに揃えると、
        /// 畑に並んだ家がどれも同じ角度の同じ絵になる
        /// </summary>
        static void Stead(CroftBanks c, System.Random rnd, float side, float from, float at)
        {
            var cross = rnd.NextDouble() < (side < 0f ? 0.28 : 0.78);
            var turn = (cross ? 90f : side < 0f ? 0f : 180f) + ((float)rnd.NextDouble() - 0.5f) * 44f;
            var rot = Quaternion.Euler(0f, turn, 0f);

            // 母屋。軒は低く、棟は高く取る。イギリスの田舎家は一階半で、
            // 二階の窓は妻の面に出る
            var wide = 7.0f + (float)rnd.NextDouble() * 3.2f;
            var deep = 5.6f + (float)rnd.NextDouble() * 2.0f;
            Roost(c, rnd, new Vector3(Lane(from * side), 0f, at), rot,
                wide, deep, 2.95f + (float)rnd.NextDouble() * 0.55f, 2.0f + (float)rnd.NextDouble() * 0.9f, true);

            // 納屋。**母屋と平行にしない。** 揃えると二棟が一つの長い建屋に見えて、
            // 農家ではなく倉庫になる。道から見て母屋の向こうへ置くので、
            // 手前の構えが納屋に塞がれることもない
            if (rnd.NextDouble() < 0.80)
            {
                var back = from + 3.2f + (float)rnd.NextDouble() * 7.5f;
                var along = at + (rnd.Next(2) == 0 ? -1f : 1f) * (7f + (float)rnd.NextDouble() * 6f);
                Roost(c, rnd, new Vector3(Lane(back * side), 0f, along),
                    Quaternion.Euler(0f, turn + 90f + ((float)rnd.NextDouble() - 0.5f) * 40f, 0f),
                    5.0f + (float)rnd.NextDouble() * 2.0f, 8.0f + (float)rnd.NextDouble() * 4.0f,
                    2.5f + (float)rnd.NextDouble() * 0.5f, 1.4f + (float)rnd.NextDouble() * 0.6f, false);
            }

            // 構えの境と門。道の側へ一筋通す。**門の口は必ず空ける。**
            // 一本の垣で塞ぐと、農家ではなく畑の囲いになって、そこに人が住んでいる
            // ことが読めない。道から農道が入る口があって初めて構えになる
            Bound(c, rnd, side, Mathf.Max(10f, from - wide * 0.5f - 3.5f - (float)rnd.NextDouble() * 3f), at,
                12f + (float)rnd.NextDouble() * 6f, rnd.NextDouble() < 0.58);

            // 防風の木立。**麦の丈（1 m 前後）を確かに越える背があるのはこれと建屋だけ。**
            // 石垣は畑に沈んでほとんど見えないので、構えの輪郭を上で作るのは木立の役。
            // 北の風を切るので、農家の一方の側へ寄せて並べる
            var grove = 1 + rnd.Next(3);
            var groveAt = from + 2f + (float)rnd.NextDouble() * 9f;
            var groveZ = at + (rnd.Next(2) == 0 ? -1f : 1f) * (5f + (float)rnd.NextDouble() * 5f);
            for (var t = 0; t < grove; t++)
                Shelter(c, rnd, groveAt + ((float)rnd.NextDouble() - 0.5f) * 5f,
                    groveZ + (t - (grove - 1) * 0.5f) * (3.4f + (float)rnd.NextDouble() * 1.6f), side);
        }

        /// <summary>
        /// 建屋 1 棟。漆喰の壁に切妻の白い屋根。
        ///
        /// **窓は道を向いた面と妻の両面に並べる。** 車は原点で +z を向いたまま動かないので、
        /// 近づいてくるあいだ画面に入るのは妻の面、脇を過ぎるときは道を向いた面になる。
        /// 棟を横にした家では役割が入れ替わるだけで、どちらも要る。
        ///
        /// home が false なら納屋。窓は持たず、道を向いた面に大きな戸口がひとつ
        /// </summary>
        static void Roost(CroftBanks c, System.Random rnd, Vector3 at, Quaternion rot,
            float wide, float deep, float eave, float rise, bool home)
        {
            var w = wide * 0.5f;
            var d = deep * 0.5f;
            // **斜面に建つので、一点だけ測って据えてはいけない。** 畑は 1 m につき
            // 0.2 m 落ちるところがあり、間口 10 m の建屋では隅どうしで 1 m 以上違う。
            // 足元は一番低い隅の下まで下ろし、軒と窓は一番高い隅から測る。
            // 均すと、上り側で壁が畑に埋まるか、下り側で床が畑から浮く
            float low, high;
            Footing(at, rot, w, d, out low, out high);
            var foot = low - 0.7f;
            var top = high + eave;
            var peak = top + rise;
            System.Func<float, float, float, Vector3> P = (lx, ly, lz) => at + rot * new Vector3(lx, ly, lz);

            // 壁。天と底は張らない。天は屋根が覆い、底は畑の下にある
            c.Wall.Quad(P(w, foot, d), P(w, foot, -d), P(w, top, -d), P(w, top, d));
            c.Wall.Quad(P(-w, foot, -d), P(-w, foot, d), P(-w, top, d), P(-w, top, -d));
            c.Wall.Quad(P(-w, foot, d), P(w, foot, d), P(w, top, d), P(-w, top, d));
            c.Wall.Quad(P(w, foot, -d), P(-w, foot, -d), P(-w, top, -d), P(w, top, -d));
            // 妻。三角形なので、四隅のうち二つを棟の一点に重ねる
            c.Wall.Quad(P(-w, top, d), P(w, top, d), P(0f, peak, d), P(0f, peak, d));
            c.Wall.Quad(P(w, top, -d), P(-w, top, -d), P(0f, peak, -d), P(0f, peak, -d));

            // 屋根。軒の出のぶんだけ流れを伸ばすので、軒先は壁の天より下へ垂れる
            var ex = w + CroftEaves;
            var ey = peak - rise / w * ex;
            var ez = d + CroftEaves;
            c.Roof.Quad(P(0f, peak, ez), P(ex, ey, ez), P(ex, ey, -ez), P(0f, peak, -ez));
            c.Roof.Quad(P(-ex, ey, ez), P(0f, peak, ez), P(0f, peak, -ez), P(-ex, ey, -ez));
            // 鼻隠し。**白い屋根と白い空のあいだに線が要る。** 朝靄で地平が白く飛ぶので、
            // 縁の無い白い屋根は靄に溶けて、屋根の形そのものが読めなくなる
            c.Dark.Quad(P(ex, ey - 0.16f, ez), P(ex, ey - 0.16f, -ez), P(ex, ey, -ez), P(ex, ey, ez));
            c.Dark.Quad(P(-ex, ey - 0.16f, -ez), P(-ex, ey - 0.16f, ez), P(-ex, ey, ez), P(-ex, ey, -ez));

            if (home)
            {
                // 煙突。妻の上へ立てる。片方だけの家も混ぜる
                for (var s = 0; s < 2; s++)
                {
                    if (s == 1 && rnd.NextDouble() < 0.45) continue;
                    var stack = 1.0f + (float)rnd.NextDouble() * 0.8f;
                    var cz = (s == 0 ? 1f : -1f) * (d - 0.40f);
                    c.Wall.Box(P(0f, peak - 0.4f + stack * 0.5f, cz),
                        new Vector3(0.80f, stack + 0.8f, 0.74f), rot);
                }
                // 道を向いた面の窓と戸口。一階半なので窓は一段しか入らない
                var cols = wide > 8.4f ? 3 : 2;
                for (var k = 0; k < cols; k++)
                    Sash(c.Dark, P, w + CroftProud,
                        Mathf.Lerp(-d + 1.15f, d - 1.15f, k / (float)(cols - 1)), 0.84f, high + 0.95f, 1.10f);
                var dk = rnd.Next(cols - 1);
                Sash(c.Dark, P, w + CroftProud,
                    Mathf.Lerp(-d + 1.15f, d - 1.15f, (dk + 0.5f) / (cols - 1)), 0.92f, high, 1.98f);
                // 妻の面。下は一階、上は屋根裏の窓
                for (var s = 0; s < 2; s++)
                {
                    var gz = (s == 0 ? 1f : -1f) * (d + CroftProud);
                    Gable(c.Dark, P, gz, s == 0, 0.86f, high + 0.95f, 1.10f);
                    Gable(c.Dark, P, gz, s == 0, 0.80f, top + 0.30f, 0.86f);
                }
            }
            else
            {
                // 納屋の戸口。大きく開けて暗く落とす。壁の面に一つ暗い口があるだけで、
                // 窓の無い箱が納屋に見える
                Sash(c.Dark, P, w + CroftProud, 0f, Mathf.Min(3.2f, wide * 0.46f),
                    high, Mathf.Min(eave - 0.35f, 2.6f));
            }
        }

        /// <summary>
        /// 建屋の足元。footprint の四隅と中を測って、畑の面の一番低いところと
        /// 一番高いところを返す。<see cref="Land"/> は道からの距離と区切りの中の z で引く
        /// </summary>
        static void Footing(Vector3 at, Quaternion rot, float w, float d, out float low, out float high)
        {
            low = float.MaxValue;
            high = float.MinValue;
            for (var i = -1; i <= 1; i++)
                for (var k = -1; k <= 1; k++)
                {
                    var p = at + rot * new Vector3(w * i, 0f, d * k);
                    var y = Turf(p.x, p.z);
                    low = Mathf.Min(low, y);
                    high = Mathf.Max(high, y);
                }
        }

        /// <summary>畑の面の高さ。x は世界の x、z は区切りの中の z</summary>
        static float Turf(float x, float z)
        {
            return FieldY + Land(Mathf.Abs(x - LaneOffset), z);
        }

        /// <summary>局所の +x を向いた面に貼る一枚。窓も戸口も納屋の口もこれで置く</summary>
        static void Sash(Bank bank, System.Func<float, float, float, Vector3> P,
            float x, float z, float wide, float y0, float high)
        {
            var h = wide * 0.5f;
            bank.Quad(P(x, y0, z + h), P(x, y0, z - h), P(x, y0 + high, z - h), P(x, y0 + high, z + h));
        }

        /// <summary>妻の面に貼る一枚。plus が true なら +z の側</summary>
        static void Gable(Bank bank, System.Func<float, float, float, Vector3> P,
            float z, bool plus, float wide, float y0, float high)
        {
            var h = wide * 0.5f;
            if (plus) bank.Quad(P(-h, y0, z), P(h, y0, z), P(h, y0 + high, z), P(-h, y0 + high, z));
            else bank.Quad(P(h, y0, z), P(-h, y0, z), P(-h, y0 + high, z), P(h, y0 + high, z));
        }

        /// <summary>
        /// 構えの境。石垣か生け垣を道と平行に一筋、真ん中を門のぶんだけ空けて。
        ///
        /// **石垣は <see cref="Downs"/> の石垣と同じ組み方にする。** 厚み 0.46 の胴に
        /// 0.54 の笠石。あちらは牧草地の境でこちらは畑の境だが、同じ地方の同じ石を
        /// 積んだものなので、寸法を違えると別の土地の垣に見える。
        /// 背だけは 0.80 から 1.25 へ上げた。麦の丈が 1 m 前後あるので、
        /// あの背では畑に丸ごと沈んで一本の線も出ない。
        ///
        /// 笠石を暗い方の素材で焼くのは、市街の軒（<see cref="Raise"/>）と同じ理由。
        /// 胴と同じ色では、麦の穂先から出ている僅か 20 cm が壁として読めない。
        ///
        /// 生け垣を混ぜるのは背のため。1.7 m あれば麦を確かに越える
        /// </summary>
        static void Bound(CroftBanks c, System.Random rnd, float side, float from, float at, float run, bool hedge)
        {
            var x = Lane(from * side);
            var thick = hedge ? 1.05f : 0.46f;
            var stand = hedge ? 1.62f + (float)rnd.NextDouble() * 0.34f : 1.25f;
            for (var s = 0; s < 2; s++)
            {
                var a0 = s == 0 ? at - run * 0.5f : at + CroftGate * 0.5f;
                var a1 = s == 0 ? at - CroftGate * 0.5f : at + run * 0.5f;
                if (a1 - a0 < 0.6f) continue;
                // **一続きの箱で置かない。** 麦の穂先から出ているのは上の 20〜60 cm だけなので、
                // 一本の水平な線として出る。そこが一分の狂いも無く真っ直ぐだと、
                // 生け垣でも石垣でもなく畑に置いた板に見える。
                // 3 つに割って背をわずかに振ると、天端が凸凹になる
                var lumps = Mathf.Max(1, Mathf.RoundToInt((a1 - a0) / 4.5f));
                for (var q = 0; q < lumps; q++)
                {
                    var z0 = Mathf.Lerp(a0, a1, q / (float)lumps);
                    var z1 = Mathf.Lerp(a0, a1, (q + 1) / (float)lumps);
                    // 斜面に乗るので、胴は一番低いところまで下ろし、天は一番高いところから測る
                    var low = Mathf.Min(Turf(x, z0), Mathf.Min(Turf(x, (z0 + z1) * 0.5f), Turf(x, z1)));
                    var top = Mathf.Max(Turf(x, z0), Mathf.Max(Turf(x, (z0 + z1) * 0.5f), Turf(x, z1)))
                        + stand * (0.88f + (float)rnd.NextDouble() * 0.24f);
                    var mid = (z0 + z1) * 0.5f;
                    var bank = hedge ? c.Dark : c.Wall;
                    bank.Box(new Vector3(x, (low - 0.4f + top) * 0.5f, mid),
                        new Vector3(thick, top - low + 0.4f, z1 - z0 + 0.06f));
                    if (!hedge)
                        c.Dark.Box(new Vector3(x, top + 0.05f, mid),
                            new Vector3(thick + 0.08f, 0.10f, z1 - z0 + 0.06f));
                }
            }
            // 門。**<see cref="Downs"/> の門と同じ寸法。** 二本の柱に三本の横木
            var foot = Turf(x, at) - 0.10f;
            for (var i = 0; i < 2; i++)
                c.Dark.Box(new Vector3(x, foot + 0.62f, at + (i == 0 ? -1f : 1f) * CroftGate * 0.5f),
                    new Vector3(0.16f, 1.24f, 0.16f));
            for (var i = 0; i < 3; i++)
                c.Dark.Box(new Vector3(x, foot + 0.34f + i * 0.34f, at),
                    new Vector3(0.07f, 0.07f, CroftGate));
        }

        /// <summary>
        /// 防風の木立 1 本。<see cref="TreeMesh"/> と同じ考えで、塊だけを積む。
        /// 逆光でも順光でも、この距離では枝葉は一つの暗い塊にしかならない
        /// </summary>
        static void Shelter(CroftBanks c, System.Random rnd, float from, float at, float side)
        {
            var x = Lane(from * side);
            var foot = Turf(x, at) - 0.2f;
            var high = 3.4f + (float)rnd.NextDouble() * 2.2f;
            var turn = Quaternion.Euler(0f, (float)rnd.NextDouble() * 90f, 0f);
            c.Dark.Box(new Vector3(x, foot + high * 0.30f, at), new Vector3(0.36f, high * 0.60f, 0.36f));
            for (var i = 0; i < 3; i++)
            {
                var wide = (2.3f - i * 0.55f) * (0.8f + (float)rnd.NextDouble() * 0.4f);
                c.Dark.Box(new Vector3(x + ((float)rnd.NextDouble() - 0.5f) * 0.6f,
                        foot + high * 0.52f + i * high * 0.17f,
                        at + ((float)rnd.NextDouble() - 0.5f) * 0.6f),
                    new Vector3(wide, high * 0.22f, wide), turn);
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

        /// <summary>
        /// <see cref="Scatter"/> の mesh 版。物を置かずに、環の上の位置だけを返す。
        ///
        /// 素材へ焼く沿道は区切りの Transform を先に決められないので、位置を受け取った側が
        /// <see cref="Put"/> で区切りへ割る。割り切るかどうかの見方は Scatter と同じで、
        /// 環の長さを割り切らない刻みは並べずに飛ばす
        /// </summary>
        static void Sow(float spacing, float wobble, float phase, System.Random rnd,
            System.Action<float, int> put)
        {
            var count = Mathf.RoundToInt(Span / spacing);
            if (spacing <= 0f || count <= 0 || Mathf.Abs(count * spacing - Span) > 0.001f)
            {
                Debug.LogWarning(string.Format("沿道の間隔 {0} m が環の長さ {1} m を割り切らない。並べずに飛ばす", spacing, Span));
                return;
            }
            for (var k = 0; k < count; k++)
                put(k * spacing + phase + ((float)rnd.NextDouble() - 0.5f) * 2f * wobble, k);
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
