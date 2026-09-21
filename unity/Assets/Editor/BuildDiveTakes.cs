using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace HalfAware.EditorTools
{
    /// <summary>
    /// 場面 4 の十六の記憶。設計書 6 節の出来事を秒に割ったもの。
    ///
    /// 記憶ごとに一つの関数を持つ。関数は人を置いて、主の体の鍵打ちを返す。
    /// 位置はどれも場所のローカルで、<c>yaw</c> は +z を 0 とした度、<c>pitch</c> は下が正。
    /// 鍵打ちの最後の <c>at</c> は一覧の <c>length</c> と揃える。揃っていなければ見直しが言う。
    ///
    /// **顔は見せない。** 背を向ける・帽子の影に入る・逆光になる向きで置く。
    /// 買い手と同じ濃い色のマテリアルなので、どの人も暗がりでは影にしか見えない。
    /// それでもこちらを正面から向いている人は作らないようにしてある。
    ///
    /// 秒・位置・向きはすべて仮置き。オーナーが実機で見て詰める
    /// </summary>
    public static partial class BuildDive
    {
        /// <summary>組み終えた直後に立っている場所。記憶 0 の頭と同じ点</summary>
        static readonly Vector3 FirstStand = new Vector3(0f, 0f, 2.4f);
        const float FirstYaw = 176f;

        /// <summary>同じ体つきは一つの mesh を使い回す。人ごとに焼くと repo が 30 MB 増える</summary>
        static readonly Dictionary<string, Mesh> figures = new Dictionary<string, Mesh>();

        static Transform Takes(Transform root, DiveRoster roster)
        {
            var parent = Child(root, "Takes");
            Clear(parent);
            figures.Clear();

            for (var i = 0; i < roster.Count && i < Memories.Length; i++)
            {
                var go = new GameObject(i.ToString());
                go.transform.SetParent(parent, false);
                go.transform.localPosition = PlaceOrigin(roster[i].place);
                var keys = Memories[i](go.transform);

                var take = go.AddComponent<Take>();
                var so = new SerializedObject(take);
                so.FindProperty("entry").intValue = i;
                Notes(so.FindProperty("keys"), keys);
                Fill(so.FindProperty("people"), Lineup(go.transform, roster[i].seen));
                so.ApplyModifiedPropertiesWithoutUndo();

                // 潜るまでは伏せる。DiveDirector が一本だけ起こす
                go.SetActive(false);
            }
            return parent;
        }

        /// <summary>記憶ごとの受け持ち。並びが一覧の番号になる</summary>
        static readonly System.Func<Transform, HostKey[]>[] Memories =
        {
            Mei, Hanna, Albert, Sofia, Emily, Mark, Linda, Lee,
            Giorgio, Rosa, Lucas, Priya, Daniel, Aisha, Mateo, Elena,
        };

        /// <summary>鍵打ちを並べる。private な [SerializeField] なので SerializedObject 越しに書く</summary>
        static void Notes(SerializedProperty row, HostKey[] keys)
        {
            row.arraySize = keys.Length;
            for (var i = 0; i < keys.Length; i++)
            {
                var e = row.GetArrayElementAtIndex(i);
                e.FindPropertyRelative("at").floatValue = keys[i].at;
                e.FindPropertyRelative("position").vector3Value = keys[i].position;
                e.FindPropertyRelative("yaw").floatValue = keys[i].yaw;
                e.FindPropertyRelative("pitch").floatValue = keys[i].pitch;
                e.FindPropertyRelative("eyeHeight").floatValue = keys[i].eyeHeight;
            }
        }

        /// <summary>
        /// 板の出る相手を一覧の並びで拾う。名前が一字でも違えば見つからず、見直しがそこで言う
        /// </summary>
        static Transform[] Lineup(Transform take, Seen[] seen)
        {
            var all = new List<Transform>();
            if (seen == null) return all.ToArray();
            for (var i = 0; i < seen.Length; i++)
            {
                var who = take.Find(seen[i].name);
                if (who == null) { Debug.LogWarning("板の相手がいない: " + take.name + "/" + seen[i].name); continue; }
                all.Add(who);
            }
            return all.ToArray();
        }

        // ---- 人 --------------------------------------------------------------

        /// <summary>
        /// 人をひとり置く。名前は一覧の <see cref="Seen.name"/> と揃える。
        /// build は体つきで、子どもを 0.6、老人を 0.95 にしてある
        /// </summary>
        static Transform Cast(Transform take, string name, string model, Vector3 at, float yaw, int pose, float build)
        {
            var who = Piece(take, name, Figure(model, pose, build), BuildAlley.BuyerMat());
            who.localPosition = at;
            who.localRotation = Quaternion.Euler(0f, yaw, 0f);
            return who;
        }

        /// <summary>
        /// 体つきごとに焼いた形。模型・姿勢・体つきが同じなら一つを使い回す。
        ///
        /// <see cref="BuildAlley.BakeOne"/> は路地裏の焼き置き場へ書くが、あちらは 62 MB あるので
        /// repo に入れない決まりになっている。場面 4 のぶんはこちらの置き場へ移して、
        /// 別の端末でも組み直さずに開けるようにする
        /// </summary>
        static Mesh Figure(string model, int pose, float build)
        {
            var key = "Body_" + model + "_p" + pose + "_b" + Mathf.RoundToInt(build * 100f);
            Mesh had;
            if (figures.TryGetValue(key, out had) && had != null) return had;

            var path = Generated + key + ".asset";
            var made = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (made == null)
            {
                var scratch = new GameObject("__figure");
                var one = BuildAlley.BakeOne(scratch.transform, key, model, Vector3.zero, 0f,
                    pose, Vector3.one * build, BuildAlley.BuyerMat());
                if (one != null)
                {
                    var moved = AssetDatabase.MoveAsset(BuildAlley.Generated + key + ".asset", path);
                    if (!string.IsNullOrEmpty(moved)) Debug.LogWarning("焼いた形を移せなかった: " + moved);
                    made = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                }
                Object.DestroyImmediate(scratch);
            }
            figures[key] = made;
            return made;
        }

        /// <summary>
        /// 人や鳩を一直線に動かす。位置は Take のローカル。
        ///
        /// <paramref name="ground"/> を立てると足元が真下の床へ下りる。
        /// 階段を降りる人も教壇から降りる人も、線の上下だけでは段を追えない
        /// </summary>
        static void Move(Transform who, Vector3 from, Vector3 to, float at, float span,
            bool ease, bool ground = true)
        {
            if (who == null) return;
            var mover = who.gameObject.AddComponent<Mover>();
            var so = new SerializedObject(mover);
            so.FindProperty("from").vector3Value = from;
            so.FindProperty("to").vector3Value = to;
            so.FindProperty("at").floatValue = at;
            so.FindProperty("span").floatValue = span;
            so.FindProperty("ease").boolValue = ease;
            so.FindProperty("ground").boolValue = ground;
            so.ApplyModifiedPropertiesWithoutUndo();
            who.localPosition = from;
        }

        /// <summary>
        /// 鳩の群れ。足元から一斉に飛び立つので、端は滑らかに繋がない。
        /// 場所ではなく記憶の側に置くのは、飛び立つ秒が記憶ごとに違うため
        /// </summary>
        static void Doves(Transform take, Vector3 at, float when)
        {
            var flock = Child(take, "Doves");
            var mesh = Shape("Pigeon", 1f, b =>
            {
                b.Box(new Vector3(0f, 0.09f, 0f), new Vector3(0.13f, 0.11f, 0.24f));
                b.Box(new Vector3(0f, 0.17f, 0.11f), new Vector3(0.08f, 0.08f, 0.09f));
            });
            for (var i = 0; i < 8; i++)
            {
                var a = Mathf.Deg2Rad * (i * 47f);
                var spread = new Vector3(Mathf.Cos(a) * (0.5f + i * 0.13f), 0f, Mathf.Sin(a) * (0.5f + i * 0.11f));
                var bird = Piece(flock, "Dove" + i, mesh, Mat("Bird"));
                bird.localRotation = Quaternion.Euler(0f, i * 47f + 20f, 0f);
                // 飛ぶので床へは下ろさない
                Move(bird, at + spread, at + spread * 2.4f + new Vector3(0f, 3.4f + i * 0.2f, 0.6f),
                    when + i * 0.06f, 1.6f, false, false);
            }
        }

        // ---- 0. 女 6『メイ』 団地の外階段。60 秒 ---------------------------------

        /// <summary>
        /// 靴紐を結んでいた階段の下から、三階の戸口まで駆け上がって抱き上げられ、
        /// 降ろされて降りる。母は戸口の奥の灯りを背にしているので、顔は影になる
        /// </summary>
        static HostKey[] Mei(Transform take)
        {
            Cast(take, "Mother", "W_Casual", new Vector3(DoorA, EstateTop, EstateFace - 0.25f), 150f, 1, 1f);
            return new[]
            {
                K(0f,   0f,   0f,       2.4f,   176f,  48f, 0.55f),  // しゃがんで靴紐。指と地面しか見えない
                K(3f,   0f,   0f,       2.3f,   180f, -32f, 1.00f),  // 立って階段の上を仰ぐ
                K(10f,  0f,   Floor,    EstateLanding1, 180f, -12f, 1.00f),
                K(17f,  0f,   Floor*2f, EstateLanding2, 180f, -10f, 1.00f),
                K(23f,  0.9f, EstateTop, -13.9f, 149f,  22f, 1.00f), // 三階。母の脚
                K(25f,  1.25f, EstateTop, -14.2f, 149f, -42f, 1.45f), // 抱き上げられる
                K(28f,  1.25f, EstateTop, -14.2f, 236f, -34f, 1.50f), // 回る。空、団地の壁
                K(31f,  1.25f, EstateTop, -14.2f, 160f,   6f, 1.00f), // 降ろされる
                K(35f,  1.25f, EstateTop, -14.0f, 350f,   2f, 1.00f), // 背中を押されて向きが変わる
                K(40f,  0f,   EstateTop, -13.6f,   0f,  12f, 1.00f),
                K(47f,  0f,   Floor*2f, EstateLanding2,   0f,   8f, 1.00f),
                K(49f,  0f,   Floor*2f, EstateLanding2, 166f, -26f, 1.00f), // 振り返る。戸口の母は逆光
                K(54f,  0f,   Floor,    EstateLanding1,   0f,   8f, 1.00f),
                K(60f,  0f,   0f,       2.4f,     0f,   0f, 1.00f),
            };
        }

        // ---- 1. 女 34『ハンナ』 団地の廊下。30 秒 --------------------------------

        /// <summary>
        /// 鍵を掛けたところで隣の老人に呼ばれ、駆け上がってきた娘に体操着を渡す。
        /// 娘は背を向けて置く。設計書の「腕の中で顔は肩に埋まって見えない」に合わせたもの
        /// </summary>
        static HostKey[] Hanna(Transform take)
        {
            var kid = Cast(take, "Daughter", "W_Casual", new Vector3(0.75f, EstateTop, -13.95f), 280f, 0, 0.6f);
            Move(kid, new Vector3(0.2f, EstateTop, -13.4f), new Vector3(0.75f, EstateTop, -13.95f), 6f, 3.5f, true);
            Cast(take, "Neighbour", "M_Casual", new Vector3(4.0f, EstateTop, -14.55f), 130f, 3, 0.95f);
            return new[]
            {
                K(0f,  1.9f,  EstateTop, -14.1f, 178f,  20f, 1.55f),  // ドアに鍵を掛けている
                K(3f,  1.9f,  EstateTop, -14.1f, 102f,   2f, 1.55f),  // 隣の戸口からの声
                K(6f,  1.85f, EstateTop, -14.05f, 102f,   4f, 1.55f), // 老人が会釈する
                K(10f, 1.8f,  EstateTop, -14.05f, 275f,   8f, 1.55f), // 娘が二段飛ばしで上がってくる
                K(14f, 1.75f, EstateTop, -14.05f, 275f,  38f, 1.55f), // 体操着の袋を渡す
                K(18f, 1.75f, EstateTop, -14.05f, 275f,  24f, 1.55f), // 抱き上げる
                K(22f, 1.75f, EstateTop, -14.05f, 275f,  40f, 1.55f), // 降ろす。駆け下りていく
                K(25f, 1.75f, EstateTop, -14.05f, 108f,   4f, 1.55f), // 老人はまだ新聞を広げている
                K(30f, 0.6f,  EstateTop, -13.7f,    6f,  12f, 1.55f),
            };
        }

        // ---- 2. 男 78『アルベルト』 公園のベンチ。60 秒 ---------------------------

        /// <summary>
        /// ベンチから門まで。手元がぼやける体なので、握らされた石は最後まで形にならない。
        /// 孫娘と妻は先に立って歩くので、どちらも背中しか見えない
        /// </summary>
        static HostKey[] Albert(Transform take)
        {
            var girl = Cast(take, "Granddaughter", "W_Casual", new Vector3(-0.9f, 0f, 0.7f), 0f, 0, 0.6f);
            Move(girl, new Vector3(-0.9f, 0f, 0.7f), new Vector3(-0.9f, 0f, 7.6f), 12f, 34f, true);
            var wife = Cast(take, "Wife", "W_Formal", new Vector3(2.2f, 0f, 0.6f), 8f, 0, 0.95f);
            Move(wife, new Vector3(2.2f, 0f, 0.6f), new Vector3(0.9f, 0f, 7.2f), 16f, 38f, true);
            Doves(take, new Vector3(-1.1f, 0.09f, 5.6f), 39f);
            return new[]
            {
                K(0f,  0f,    0f, -0.1f,   0f,  55f, 1.20f),  // 掛けたまま。膝の上の手がぼやけている
                K(4f,  0f,    0f, -0.1f,  88f,  12f, 1.22f),  // 隣のベンチから妻の声
                K(8f,  0f,    0f, -0.1f, 312f,  28f, 1.22f),  // 孫娘の手が腕を取る
                K(14f, 0f,    0f,  0.35f, 330f,   8f, 1.75f), // 時間をかけて立つ。視界が上がる
                K(20f, -0.5f, 0f,  1.6f,  345f,  10f, 1.74f),
                K(28f, -1.2f, 0f,  3.0f,  355f,   6f, 1.76f), // 膝が揺れる
                K(34f, -1.3f, 0f,  4.2f,  300f,   4f, 1.75f), // 池の縁
                K(40f, -1.1f, 0f,  5.2f,   12f,  22f, 1.74f), // 鳩が足元から飛び立つ
                K(43f, -1.0f, 0f,  5.4f,    8f, -30f, 1.75f),
                K(48f, -0.7f, 0f,  6.2f,  348f,  34f, 1.74f), // 石を握らされる
                K(53f, -0.35f, 0f, 7.4f,    6f,   6f, 1.75f),
                K(60f, -0.3f, 0f,  8.5f,  318f,  26f, 1.75f), // 門の柱に手を置く
            };
        }

        // ---- 3. 女 7『ソフィア』 同じ公園。30 秒 ---------------------------------

        /// <summary>
        /// 石を拾っていたところから祖父を引き起こし、門まで歩く。
        /// 祖父は帽子の影と逆光に入る向きで、顔は最後まで見えない
        /// </summary>
        static HostKey[] Sofia(Transform take)
        {
            // 祖父はベンチの +x 側の端に立たせる。正面から向き合わせると、
            // 逆光にも帽子の影にも入らないまま、顔だけが画面いっぱいに来る
            var old = Cast(take, "Grandfather", "M_Casual", new Vector3(0.95f, 0f, 0.35f), 20f, 3, 0.95f);
            Move(old, new Vector3(0.95f, 0f, 0.35f), new Vector3(-0.5f, 0f, 4.9f), 11f, 17f, true);
            var gran = Cast(take, "Grandmother", "W_Formal", new Vector3(2.2f, 0f, 0.4f), 10f, 0, 0.95f);
            Move(gran, new Vector3(2.2f, 0f, 0.4f), new Vector3(0.6f, 0f, 5.4f), 14f, 16f, true);
            Doves(take, new Vector3(-1.2f, 0.09f, 4.4f), 21f);
            return new[]
            {
                K(0f,  -0.9f, 0f, 0.9f, 200f,  55f, 0.55f),  // しゃがんで白い石を拾っている
                K(3f,  -0.9f, 0f, 0.9f, 107f,  -5f, 1.05f),  // 祖父の低い声
                K(7f,  -0.6f, 0f, 0.6f,  99f,  12f, 1.05f),  // 手を取る
                K(11f, -0.7f, 0f, 0.8f, 105f,   6f, 1.05f),  // 引っ張る。なかなか立てない
                K(16f, -0.9f, 0f, 2.2f,   0f,   4f, 1.05f),
                K(19f, -0.9f, 0f, 2.6f, 170f,  -4f, 1.05f),  // 振り返って待つ
                K(22f, -0.9f, 0f, 3.6f,  10f, -38f, 1.05f),  // 鳩を目で追って空を見上げる
                K(26f, -0.8f, 0f, 4.6f, 150f,  28f, 1.05f),  // 石を祖父の手に握らせる
                K(30f, -0.7f, 0f, 5.6f,   5f,   0f, 1.05f),
            };
        }

        // ---- 4. 女 19『エミリー』 電車。30 秒 -------------------------------------

        /// <summary>
        /// 吊り革に掴まって窓の外を見ていたところから、後輩に本を返される。
        /// 後輩は背が低いので見下ろす角度になり、そのぶん顔が入らない
        /// </summary>
        static HostKey[] Emily(Transform take)
        {
            Cast(take, "Junior", "W_Casual", new Vector3(0.15f, 0f, -0.9f), 315f, 0, 0.92f);
            Cast(take, "Passenger", "M_Suit", new Vector3(SeatX, 0f, 1.8f), 250f, 5, 1f);
            return new[]
            {
                K(0f,  0.2f,  0f, 0.6f,   70f,   6f, 1.58f),  // 窓の外を灯りが流れている
                K(4f,  0.2f,  0f, 0.6f,  182f,  16f, 1.58f),  // 後ろからの声に振り向く
                K(9f,  0.2f,  0f, 0.5f,  182f,  22f, 1.58f),  // 借りていた本を受け取る
                K(14f, 0.2f,  0f, 0.45f, 178f,  10f, 1.58f),
                K(17f, 0.32f, 0f, 0.35f, 205f,  18f, 1.52f),  // 電車が揺れて二人でよろける
                K(20f, 0.2f,  0f, 0.45f, 175f,  14f, 1.58f),  // 腕に掴まられる
                K(23f, 0.1f,  0f, 0.6f,   37f,   8f, 1.58f),  // 向かいの男が新聞を畳んで立つ
                K(27f, 0.1f,  0f, 0.8f,  300f,   4f, 1.58f),  // 駅。ドアが開く
                K(30f, 0.0f,  0f, 1.2f,  330f,   2f, 1.58f),
            };
        }

        // ---- 5. 男 52『マーク』 台所。35 秒 ---------------------------------------

        /// <summary>
        /// 皿を洗う手から、玄関の白い光まで。遠くがぼやける体なので、
        /// 最後に振り返った戸口の二人はぼやけたまま終わる
        /// </summary>
        static HostKey[] Mark(Transform take)
        {
            Cast(take, "Wife", "W_Casual", new Vector3(0f, 0f, 0.35f), 175f, 1, 1f);
            var son = Cast(take, "Son", "M_Casual", new Vector3(StairX, 0f, -0.7f), 190f, 0, 1f);
            Move(son, new Vector3(StairX, 2.4f, -4.1f), new Vector3(StairX, 0f, -0.7f), 16f, 6f, true);
            return new[]
            {
                K(0f,  -1.05f, 0f,  2.3f,    0f,  44f, 1.72f),  // 皿を洗っている手
                K(4f,  -1.05f, 0f,  2.3f,  152f,   4f, 1.72f),  // 戸口から妻の声。逆光
                K(9f,  -0.7f,  0f,  1.9f,  160f,   8f, 1.72f),
                K(13f, -0.35f, 0f,  1.3f,  168f,  18f, 1.72f),  // 昼食の袋を手渡される
                K(16f, -0.3f,  0f,  1.2f,  150f, -30f, 1.72f),  // 二階から足音
                K(20f, -0.15f, 0f,  0.35f, 176f,  10f, 1.72f),
                K(23f, -0.1f,  0f, -0.3f,  138f,  10f, 1.72f),  // 息子が寝癖のまま降りてくる
                K(27f, -1.4f,  0f, -1.6f,  250f,  20f, 1.60f),  // 玄関
                K(29f, -1.6f,  0f, -1.8f,  260f,  40f, 1.15f),  // 靴を履くと視界が下がる
                K(32f, -1.9f,  0f, -1.95f, 268f,   4f, 1.72f),  // ドアを開けると外の光で白く飛ぶ
                K(35f, -1.85f, 0f, -1.9f,   39f,   6f, 1.72f),  // 振り返ると戸口に妻と息子
            };
        }

        // ---- 6. 女 49『リンダ』 同じ台所の戸口。30 秒 -----------------------------

        /// <summary>
        /// 戸口に立っていたところから、夫に棚の場所を教え、息子を送り出して台所へ戻る。
        /// 記憶 5 と同じ朝の、一分あとの同じ家
        /// </summary>
        static HostKey[] Linda(Transform take)
        {
            Cast(take, "Husband", "M_Casual", new Vector3(-1.05f, 0f, 2.2f), 350f, 0, 1f);
            var son = Cast(take, "Son", "M_Casual", new Vector3(1.75f, 0f, -1.9f), 215f, 0, 1f);
            Move(son, new Vector3(StairX, 2.4f, -4.1f), new Vector3(1.75f, 0f, -1.9f), 15f, 8f, true);
            return new[]
            {
                K(0f,   0f,    0f,  0.25f, 182f,   4f, 1.58f),  // 戸口。出かけるところだった
                K(3f,   0f,    0f,  0.3f,  331f,   6f, 1.58f),  // 台所から夫の声
                K(7f,  -0.2f,  0f,  0.8f,  335f,  10f, 1.58f),  // 皿を手にした夫が棚を顎で指す
                K(11f, -0.8f,  0f,  1.6f,  320f, -18f, 1.58f),  // 吊り棚を開けて場所を教える
                K(15f, -0.7f,  0f,  1.4f,  190f, -32f, 1.58f),  // 二階から足音
                K(19f, -0.2f,  0f,  0.5f,  150f,   2f, 1.58f),  // 階段を見上げると息子が降りてくる
                K(22f, -0.1f,  0f, -0.5f,  160f,   8f, 1.58f),  // 昼食の袋を渡す
                K(26f, -0.4f,  0f, -1.0f,  235f,   6f, 1.58f),  // ドアが開いて白い光。閉まる
                K(30f, -0.5f,  0f,  0.9f,  350f,  20f, 1.58f),  // 台所へ戻って蛇口を締める
            };
        }

        // ---- 7. 男 41『リー』 教室。35 秒 -----------------------------------------

        /// <summary>
        /// 黒板から机の列へ歩き、ノートを覗いて、隣の居眠りを叩いて黒板へ戻る。
        /// 覗き込む角度が深いので、生徒はつむじしか見えない
        /// </summary>
        static HostKey[] Lee(Transform take)
        {
            Cast(take, "Pupil", "W_Casual", new Vector3(DeskX[2], 0f, DeskZ[1] - 0.56f), 0f, 6, 1f);
            Cast(take, "Sleeper", "M_Casual", new Vector3(DeskX[3], 0f, DeskZ[1] - 0.56f), 0f, 6, 1f);
            return new[]
            {
                K(0f,  0f,    0.15f, 4.1f,    0f, -12f, 1.70f),  // 黒板にチョークで書いている
                K(4f,  0f,    0.15f, 4.1f,  169f,   8f, 1.70f),  // 後ろの席から呼ばれて振り向く
                K(10f, 0.3f,  0f,    2.6f,  172f,   8f, 1.70f),  // 教壇を降りて机の列を歩く
                K(16f, 0.65f, 0f,    1.2f,  176f,  16f, 1.70f),
                K(21f, 0.8f,  0f,    0.75f, 180f,  42f, 1.70f),  // ノートを覗く。つむじしか見えない
                K(25f, 1.5f,  0f,    0.75f, 135f,  40f, 1.70f),  // 隣の机を指の背で二度叩く
                K(28f, 1.5f,  0f,    0.8f,  135f,  24f, 1.70f),  // 起きる
                K(31f, 1.2f,  0f,    1.4f,  250f,  -8f, 1.70f),  // 窓の外が白い
                K(35f, 0f,    0.15f, 4.05f,   4f,  -6f, 1.70f),  // 黒板へ戻ってチョークを取り直す
            };
        }

        // ---- 8. 男 66『ジョルジョ』 団地の廊下。25 秒 -----------------------------

        /// <summary>
        /// 新聞を取りに出て、隣の母親が娘を抱き上げるのを眺めていたところ。
        /// 記憶 1 と同じ朝の同じ廊下を、二軒隣の目で見ている
        /// </summary>
        static HostKey[] Giorgio(Transform take)
        {
            Cast(take, "Wife", "W_Formal", new Vector3(DoorB, EstateTop, -15.25f), 340f, 0, 0.95f);
            Cast(take, "Mother", "W_Casual", new Vector3(1.9f, EstateTop, -14.05f), 285f, 1, 1f);
            return new[]
            {
                K(0f,  4.3f, EstateTop, -14.05f, 270f,   6f, 1.65f),  // 隣を眺めている
                K(4f,  4.3f, EstateTop, -14.05f, 270f,  12f, 1.65f),  // 母親が娘を抱き上げる
                K(8f,  4.25f, EstateTop, -14.2f, 183f,  10f, 1.65f),  // 戸の内側から妻の声
                K(12f, 4.2f, EstateTop, -14.35f, 183f,  22f, 1.65f),  // 新聞を渡す
                K(16f, 4.2f, EstateTop, -15.1f,  183f,   6f, 1.65f),  // 中へ入る
                K(20f, 4.4f, EstateTop, -16.2f,  200f,  10f, 1.65f),  // テレビの音
                K(25f, 4.4f, EstateTop, -16.3f,  210f,  40f, 1.20f),  // 靴を脱ぐと視界が揺れる
            };
        }

        // ---- 9. 女 72『ローザ』 公園の門。30 秒 -----------------------------------

        /// <summary>
        /// 手を引いていた幼い孫が鳩を追って駆け出す。呼び戻して手を取り直し、門を出る。
        /// 夫は門の柱に手を置いて振り返っているが、午後の日を背にしているので影になる
        /// </summary>
        static HostKey[] Rosa(Transform take)
        {
            var tot = Cast(take, "Toddler", "M_Casual", new Vector3(0.55f, 0f, 6.0f), 215f, 0, 0.55f);
            Move(tot, new Vector3(0.55f, 0f, 6.0f), new Vector3(-1.5f, 0f, 4.6f), 6f, 6f, true);
            Cast(take, "Husband", "M_Casual", new Vector3(-0.9f, 0f, 8.7f), 340f, 3, 0.95f);
            var girl = Cast(take, "Granddaughter", "W_Casual", new Vector3(0.15f, 0f, 8.05f), 350f, 0, 0.6f);
            Move(girl, new Vector3(-0.9f, 0f, 6.6f), new Vector3(0.15f, 0f, 8.05f), 20f, 7f, true);
            Doves(take, new Vector3(-1.4f, 0.09f, 4.4f), 8f);
            return new[]
            {
                K(0f,  0.6f,  0f, 6.4f, 327f,   6f, 1.50f),  // 前から夫の声
                K(4f,  0.6f,  0f, 6.3f, 327f,  10f, 1.48f),  // 膝が痛くて視界が揺れる
                K(8f,  0.7f,  0f, 6.2f, 230f,  26f, 1.50f),  // 孫が鳩を追って駆け出す
                K(12f, 0.7f,  0f, 6.2f, 234f,  22f, 1.50f),  // 名前を呼ぶ
                K(17f, 0.7f,  0f, 6.3f, 240f,  26f, 1.50f),  // 戻ってくる
                K(21f, 0.65f, 0f, 6.4f, 270f,  34f, 1.50f),  // 手を取り直す
                K(25f, 0.6f,  0f, 7.0f, 322f,   6f, 1.50f),  // 孫娘が夫の腕を取り直す
                K(30f, 0.45f, 0f, 8.4f, 352f,   4f, 1.50f),  // 門を出る
            };
        }

        // ---- 10. 男 3『ルーカス』 同じ公園。25 秒 ---------------------------------

        /// <summary>
        /// 鳩を追いかけていたところを祖母に呼ばれる。目が低いので地面が近く、鳩が大きい
        /// </summary>
        static HostKey[] Lucas(Transform take)
        {
            Cast(take, "Grandmother", "W_Formal", new Vector3(0.95f, 0f, 6.3f), 100f, 0, 0.95f);
            Cast(take, "Grandfather", "M_Casual", new Vector3(-0.4f, 0f, 8.6f), 355f, 3, 0.95f);
            Doves(take, new Vector3(-1.5f, 0.09f, 4.5f), 2f);
            return new[]
            {
                K(0f,  -1.2f, 0f, 4.8f, 250f,  38f, 0.90f),  // 鳩を追いかけている。地面が近い
                K(3f,  -1.4f, 0f, 4.6f, 240f,  34f, 0.90f),
                K(6f,  -1.3f, 0f, 4.7f,  55f, -16f, 0.90f),  // 振り向くと祖母は大きい
                K(11f, -0.4f, 0f, 5.4f,  52f, -18f, 0.90f),  // 戻る
                K(15f,  0.55f, 0f, 6.1f,  40f, -34f, 0.90f), // 手を取られる
                K(19f,  0.5f, 0f, 6.3f, 350f, -12f, 0.90f),  // 門の前に祖父と姉
                K(22f,  0.5f, 0f, 6.3f, 330f,  26f, 0.90f),  // 鳩がまた足元に寄ってくる
                K(25f,  0.5f, 0f, 6.2f, 300f,  40f, 0.90f),
            };
        }

        // ---- 11. 女 18『プリヤ』 同じ電車。25 秒 -----------------------------------

        /// <summary>
        /// 先輩の背中に声を掛けて本を差し出したところ。振り向いた先輩は車内の灯りを背にする
        /// </summary>
        static HostKey[] Priya(Transform take)
        {
            Cast(take, "Senior", "W_Casual", new Vector3(0.2f, 0f, 0.6f), 20f, 1, 1f);
            return new[]
            {
                K(0f,  0.15f, 0f, -0.9f,   2f,   8f, 1.55f),  // 先輩の背中に本を差し出したところ
                K(4f,  0.15f, 0f, -0.85f,  2f,   2f, 1.55f),  // 振り向く。逆光で髪しか見えない
                K(9f,  0.15f, 0f, -0.8f,   2f,   6f, 1.55f),  // 受け取ってもらう
                K(13f, 0.2f,  0f, -0.7f,  20f,  10f, 1.50f),  // 電車が揺れる
                K(16f, 0.2f,  0f, -0.65f,  6f,  14f, 1.55f),  // 腕に掴まる
                K(20f, 0.12f, 0f, -0.5f, 300f,   4f, 1.55f),  // 駅。ドアが開く
                K(25f, 0.05f, 0f, -0.2f, 285f,   2f, 1.55f),
            };
        }

        // ---- 12. 男 15『ダニエル』 同じ家の階段。25 秒 -----------------------------

        /// <summary>
        /// 階段の途中から玄関まで。振り返らないので、最後まで家の中は視界に入らない
        /// </summary>
        static HostKey[] Daniel(Transform take)
        {
            Cast(take, "Mother", "W_Casual", new Vector3(0.1f, 0f, 0.2f), 300f, 1, 1f);
            Cast(take, "Father", "M_Casual", new Vector3(-1.05f, 0f, 2.2f), 350f, 0, 1f);
            return new[]
            {
                K(0f,  StairX, 1.85f, -3.3f,   0f,  30f, 1.65f),  // 階段の途中。手すり
                K(5f,  StairX, 0.68f, -1.6f, 340f,  22f, 1.65f),
                K(8f,  1.5f,   0f,    -1.4f,  319f,  12f, 1.65f), // 台所の戸口に母
                K(12f, 0.55f,  0f,    -0.9f,  338f,  14f, 1.65f), // 昼食の袋を受け取る
                K(15f, 0.35f,  0f,     0.5f,  320f,   6f, 1.65f), // 台所で父が皿を拭いている
                K(18f, -0.8f,  0f,    -1.1f,  250f,  34f, 1.30f), // 玄関で靴を履く
                K(21f, -1.7f,  0f,    -1.9f,  265f,  40f, 1.20f),
                K(25f, -2.15f, 0f,    -1.95f, 272f,   0f, 1.65f), // ドアを開ける。白い朝
            };
        }

        // ---- 13. 女 16『アイシャ』 同じ教室。25 秒 ---------------------------------

        /// <summary>
        /// 手を挙げていたところへ先生が近づいてくる。座ったままなので体はほとんど動かず、
        /// 動くのは首と、ノートを覗かれるあいだの伏せた目だけ
        /// </summary>
        static HostKey[] Aisha(Transform take)
        {
            var teacher = Cast(take, "Teacher", "M_Suit", new Vector3(0.9f, 0f, 0.75f), 220f, 0, 1f);
            Move(teacher, new Vector3(0.45f, 0.15f, 3.9f), new Vector3(0.9f, 0f, 0.75f), 6f, 9f, true);
            Cast(take, "Neighbour", "M_Casual", new Vector3(DeskX[3], 0f, DeskZ[1] - 0.56f), 355f, 6, 1f);
            var seat = new Vector3(DeskX[2], 0f, DeskZ[1] - 0.56f);
            return new[]
            {
                K(0f,  seat.x, 0f, seat.z,   4f,  -6f, 1.20f),  // 手を挙げている
                K(4f,  seat.x, 0f, seat.z,   2f,  -4f, 1.20f),  // 前から先生の声
                K(10f, seat.x, 0f, seat.z,   6f,   6f, 1.20f),  // 机の列を歩いて近づいてくる
                K(15f, seat.x, 0f, seat.z,  10f,  34f, 1.20f),  // ノートを覗かれる
                K(19f, seat.x, 0f, seat.z,  90f,  14f, 1.20f),  // 隣の居眠りを叩く
                K(22f, seat.x, 0f, seat.z,  88f,  10f, 1.20f),  // 隣が起きてこちらを見る
                K(25f, seat.x, 0f, seat.z, 276f, -10f, 1.20f),  // 窓の外が白い
            };
        }

        // ---- 14. 男 16『マテオ』 同じ教室。25 秒 -----------------------------------

        /// <summary>
        /// 机に伏せていて、目を開けると眩しい。遠くがぼやける体なので黒板の字は読めない。
        /// 目の高さが伏せているあいだだけ机の高さまで落ちる
        /// </summary>
        static HostKey[] Mateo(Transform take)
        {
            Cast(take, "Neighbour", "W_Casual", new Vector3(DeskX[2], 0f, DeskZ[1] - 0.56f), 6f, 6, 1f);
            Cast(take, "Teacher", "M_Suit", new Vector3(DeskX[3], 0f, DeskZ[1] + 1.2f), 215f, 2, 1f);
            var seat = new Vector3(DeskX[3], 0f, DeskZ[1] - 0.56f);
            return new[]
            {
                K(0f,  seat.x, 0f, seat.z,   0f,  62f, 0.82f),  // 机に伏せている
                K(3f,  seat.x, 0f, seat.z,   2f,  40f, 1.05f),  // 目を開けると眩しい
                K(7f,  seat.x, 0f, seat.z,   0f,   4f, 1.20f),  // 先生が机の前に立っている
                K(11f, seat.x, 0f, seat.z, 272f,  12f, 1.20f),  // 隣が肘でつつく
                K(15f, seat.x, 0f, seat.z,   6f, -12f, 1.20f),  // 黒板の字が読めない
                K(19f, seat.x, 0f, seat.z, 268f, -14f, 1.20f),  // 窓の外が白い
                K(25f, seat.x, 0f, seat.z,   2f,  60f, 0.82f),  // また伏せる
            };
        }

        // ---- 15. 女 63『エレナ』 団地の部屋。25 秒 ---------------------------------

        /// <summary>
        /// テレビの前に座ったまま。夫が新聞を持って入ってきて隣に座る。
        /// 部屋で動くのはテレビの光と、入ってくる夫だけ
        /// </summary>
        static HostKey[] Elena(Transform take)
        {
            var man = Cast(take, "Husband", "M_Casual", new Vector3(3.5f, EstateTop, -16.1f), 200f, 0, 0.95f);
            Move(man, new Vector3(4.3f, EstateTop, -14.95f), new Vector3(3.5f, EstateTop, -16.1f), 6f, 5f, true);
            return new[]
            {
                K(0f,  4.95f, EstateTop, -16.62f, 168f,   8f, 1.15f),  // テレビの前
                K(3f,  4.95f, EstateTop, -16.62f, 339f,   2f, 1.15f),  // 戸口から夫の声
                K(9f,  4.95f, EstateTop, -16.62f, 308f,   0f, 1.15f),  // 新聞を持って入ってくる
                K(14f, 4.95f, EstateTop, -16.62f, 290f,   8f, 1.15f),  // 隣に座る
                K(18f, 4.95f, EstateTop, -16.62f, 288f,  16f, 1.15f),  // 新聞を広げる音
                K(25f, 4.95f, EstateTop, -16.62f, 168f,  26f, 1.15f),  // テレビの光が床に当たっている
            };
        }
    }
}
