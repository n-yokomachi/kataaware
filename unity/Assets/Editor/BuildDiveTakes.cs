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
    /// **顔は影で隠す。向きでは隠さない。** 買い手と同じ濃い色のマテリアルなので、
    /// どの人も暗がりでは影にしか見えない。逆光・帽子の影・伏せた角度でそこを確かめる。
    /// 背を向けて隠していた頃は、話しかけてくる相手まで後ろを向いていて、
    /// 誰が喋っているのか読めなかった。**相手をしている人はプレイヤーの方を向ける。**
    /// 目安は、その人の台詞が出る点への向きとの差が 90 度未満。
    /// 誰の相手もしていない人（記憶 8 の隣の母親は自分の娘を見ている）はこの限りではない。
    ///
    /// 秒・位置・向きはすべて仮置き。オーナーが実機で見て詰める
    /// </summary>
    public static partial class BuildDive
    {
        /// <summary>
        /// 組み終えた直後に立っている場所。記憶 0 の頭と同じ点。
        ///
        /// **階段の下の、上り口のすぐ手前。** 庭の真ん中（z 2.4）から始めていた頃は、
        /// 潜った直後に何も無い地面を 15.8 m 歩かされてから、ようやく一段目に着いた
        /// </summary>
        static readonly Vector3 FirstStand = new Vector3(StairEastMid, 0f, WalkFront - 0.45f);
        /// <summary>始まりの向き。東の一本は +z へ上がっていくので、0 度でそのまま階段が正面に来る</summary>
        const float FirstYaw = 0f;

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
        /// 戸口を一枚で塞ぐ。位置は <see cref="Cast"/> と同じく Take のローカル。
        ///
        /// **建物の面の穴は場所の側（<c>BuildDiveEstate</c>）が開けていて、記憶からは触れない。**
        /// 開いている戸は記憶ごとに違う（記憶 0 の朝はまだ老夫婦が出てきていない）ので、
        /// 閉めたい記憶だけ、開いた穴の手前へ戸の板を落として塞ぐ。
        /// 当たりも入れるので、覗けないし通り抜けられない。
        ///
        /// 高さは三和土の底（<see cref="EstateSunk"/> のぶん下がる）から戸の頭まで。
        /// 建物の面の穴は床から上だけだが、居間の側の穴は三和土の底から開いているので、
        /// 低い方へ合わせないと足元に隙間が残る
        /// </summary>
        /// <summary>
        /// 開いた戸。枠の外へ振り出して壁へ寄せる。
        ///
        /// **場所ではなく記憶が持つ。** 場所は四つの記憶で使い回すので、場所に置いた板は
        /// 記憶ごとに消せない。記憶 0 では隣の老夫婦はまだ一度も出てきていないのに
        /// その戸が開いた穴になっていた（オーナーの差し戻し）。開けるか閉めるかは
        /// 記憶ごとの話なので、<see cref="Shut"/> と対にして記憶の子として置く。
        ///
        /// **メーターの箱より手前へ置く。** 面に貼り付けると、板の中からメーターの角が生えてくる
        /// </summary>
        static Transform Ajar(Transform take, string name, float x)
        {
            const float half = 0.43f;
            var mesh = Shape("EstateAjar", 0.4f, b =>
            {
                b.Box(new Vector3(0f, DoorHigh * 0.5f, 0f), new Vector3(half * 2f, DoorHigh, 0.05f));
                // 面を囲う細い線。鉄扉の折り返しの縁。閉じた戸（Shut）と同じ組み合わせ
                b.Box(new Vector3(0f, 1.06f, 0.026f), new Vector3(half * 2f - 0.10f, 1.52f, 0.015f));
                // 換気口。細い羽根が三枚
                for (var i = 0; i < 3; i++)
                    b.Box(new Vector3(0f, 1.80f + i * 0.07f, 0.036f), new Vector3(half * 1.1f, 0.035f, 0.02f));
                // 新聞受けと覗き穴
                b.Box(new Vector3(0f, 0.34f, 0.036f), new Vector3(half * 0.86f, 0.05f, 0.02f));
                b.Box(new Vector3(0f, 1.52f, 0.036f), new Vector3(0.07f, 0.07f, 0.015f));
                // 把手。開いた戸なので、振り出した先の端に来る
                b.Box(new Vector3(half - 0.09f, 1.00f, 0.05f), new Vector3(0.05f, 0.05f, 0.12f));
            });
            var leaf = Piece(take, name, mesh, Mat("Door"));
            leaf.localPosition = new Vector3(x + 0.92f, EstateTop, EstateFace + 0.26f);
            return leaf;
        }

        static Transform Shut(Transform take, string name, float x)
        {
            const float high = DoorHigh + EstateSunk;
            var mesh = Shape("EstateShut", 0.4f, b =>
            {
                b.Box(new Vector3(0f, high * 0.5f, 0f), new Vector3(DoorHalf * 2f, high, 0.05f));
                // 面を囲う細い線。鉄扉の折り返しの縁。開いている戸（EstateLeaf）と同じ組み合わせ
                b.Box(new Vector3(0f, EstateSunk + 1.06f, 0.026f),
                    new Vector3(DoorHalf * 2f - 0.10f, 1.52f, 0.015f));
                // 換気口。細い羽根が三枚
                for (var i = 0; i < 3; i++)
                    b.Box(new Vector3(0f, EstateSunk + 1.80f + i * 0.07f, 0.036f),
                        new Vector3(DoorHalf * 1.1f, 0.035f, 0.02f));
                // 新聞受けと覗き穴。目の高さの黒い点ひとつで、そこが住戸の戸になる
                b.Box(new Vector3(0f, EstateSunk + 0.34f, 0.036f), new Vector3(DoorHalf * 0.86f, 0.05f, 0.02f));
                b.Box(new Vector3(0f, EstateSunk + 1.52f, 0.036f), new Vector3(0.07f, 0.07f, 0.015f));
                // 把手
                b.Box(new Vector3(DoorHalf - 0.13f, EstateSunk + 1.00f, 0.06f), new Vector3(0.05f, 0.05f, 0.12f));
            });
            var leaf = Piece(take, name, mesh, Mat("Door"));
            leaf.localPosition = new Vector3(x, EstateTop - EstateSunk, EstateFace + 0.05f);
            var box = leaf.gameObject.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, high * 0.5f, 0f);
            box.size = new Vector3(DoorHalf * 2f + 0.06f, high, 0.14f);
            return leaf;
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
            // **母は廊下の側（+z）を向く。** 150 度では戸口の奥を向いていて、
            // 廊下から上がってきたメイには背中しか見えなかった。
            // 顔が影になるのは向きではなく、戸口の奥の灯りを背にしているから
            Cast(take, "Mother", "W_Casual",
                new Vector3(DoorA, EstateTop - EstateSunk, EstateFace - 0.25f), 0f, 1, 1f);
            // 隣は老夫婦の家。この朝はまだ一度も出てきていないので、戸を閉めて穴を塞ぐ
            // メイの家は母が戸口に立っているので開いている。隣はまだ誰も出てきていない
            Ajar(take, "AjarA", DoorA);
            Shut(take, "ShutB", DoorB);
            return new[]
            {
                // 階段の下。上り口のすぐ手前で、始まりの立ち位置（FirstStand）と同じ点
                K(0f,   StairEastMid, 0f, WalkFront - 0.45f,   0f,  48f, 0.55f),  // しゃがんで靴紐。指と地面しか見えない
                K(3f,   StairEastMid, 0f, WalkFront - 0.35f,   0f, -32f, 1.00f),  // 立って階段の上を仰ぐ
                // 三階建てになって、上りは折り返しが二つ・階の高さの踊り場が一つになった
                K(10f,  0f,   Floor * 0.5f, EstateTurn,     180f, -12f, 1.00f),  // 一つ目の折り返し
                K(17f,  0f,   Floor,        EstateLanding1,   0f, -10f, 1.00f),  // 二階の廊下
                K(23f,  0.9f, EstateTop, EstateWalk + 0.20f, 149f,  22f, 1.00f), // 三階。母の脚
                K(25f,  1.25f, EstateTop, EstateWalk - 0.10f, 149f, -42f, 1.45f), // 抱き上げられる
                K(28f,  1.25f, EstateTop, EstateWalk - 0.10f, 236f, -34f, 1.50f), // 回る。空、団地の壁
                K(31f,  1.25f, EstateTop, EstateWalk - 0.10f, 160f,   6f, 1.00f), // 降ろされる
                K(35f,  1.25f, EstateTop, EstateWalk + 0.10f, 350f,   2f, 1.00f), // 背中を押されて向きが変わる
                K(40f,  0f,   EstateTop, EstateWalk + 0.40f,   0f,  12f, 1.00f),
                K(45f,  0f,   Floor * 1.5f, EstateTurn,     180f,   8f, 1.00f),  // 降りる途中の折り返し
                K(49f,  0f,   Floor,        EstateLanding2, 166f, -26f, 1.00f),  // 振り返る。戸口の母は逆光
                K(54f,  0f,   Floor * 0.5f, EstateTurn,     180f,   8f, 1.00f),
                K(60f,  StairEastMid, 0f, WalkFront - 0.45f,  96f,   0f, 1.00f), // 降り切って、庭の側へ向き直る
            };
        }

        // ---- 1. 女 34『ハンナ』 団地の廊下。30 秒 --------------------------------

        /// <summary>
        /// 鍵を掛けたところで隣の老人に呼ばれ、駆け上がってきた娘に体操着を渡す。
        /// 娘は背を向けて置く。設計書の「腕の中で顔は肩に埋まって見えない」に合わせたもの
        /// </summary>
        static HostKey[] Hanna(Transform take)
        {
            // **娘は廊下の壁の側へ寄せて止める。** 歩く線（EstateWalk）の上に立たせると、
            // 東の点からも西の点からも真横に来て、どちらを向いても片方に背を向けることになる
            var stood = new Vector3(StairEast - 0.45f, EstateTop, EstateWalk - 0.35f);
            var kid = Cast(take, "Daughter", "W_Casual", stood, 0f, 0, 0.6f);
            // 出てくるのは階段の口。折り返しになって、上がり切る一本が廊下の西へ寄ったので、
            // 元の x 0.2 は手すりの中になった
            Move(kid, new Vector3(StairWestMid, EstateTop, WalkFront - 0.10f), stood, 6f, 3.5f, true);
            // 老人も廊下の側（+z）を向く。130 度では自分の戸口の方を向いていて、
            // 廊下から寄っていくハンナには背中しか見えなかった
            Cast(take, "Neighbour", "M_Casual",
                new Vector3(DoorB - 0.20f, EstateTop, EstateFace + 0.25f), 0f, 3, 0.95f);
            // ハンナは出しなに鍵を掛けたところ。自分の戸は閉まっている
            Shut(take, "ShutA", DoorA);
            Ajar(take, "AjarB", DoorB);
            return new[]
            {
                K(0f,  1.9f,  EstateTop, EstateWalk,         178f,  20f, 1.55f),  // ドアに鍵を掛けている
                K(3f,  1.9f,  EstateTop, EstateWalk,         102f,   2f, 1.55f),  // 隣の戸口からの声
                K(6f,  1.85f, EstateTop, EstateWalk + 0.05f, 102f,   4f, 1.55f),  // 老人が会釈する
                K(10f, 1.8f,  EstateTop, EstateWalk + 0.05f, 275f,   8f, 1.55f),  // 娘が二段飛ばしで上がってくる
                K(14f, 1.75f, EstateTop, EstateWalk + 0.05f, 275f,  38f, 1.55f),  // 体操着の袋を渡す
                K(18f, 1.75f, EstateTop, EstateWalk + 0.05f, 275f,  24f, 1.55f),  // 抱き上げる
                K(22f, 1.75f, EstateTop, EstateWalk + 0.05f, 275f,  40f, 1.55f),  // 降ろす。駆け下りていく
                K(25f, 1.75f, EstateTop, EstateWalk + 0.05f, 108f,   4f, 1.55f),  // 老人はまだ新聞を広げている
                K(30f, 0.6f,  EstateTop, EstateWalk + 0.30f,   6f,  12f, 1.55f),
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
            // **妻は三和土ではなく居間に置く。** 三和土に立たせると、新聞を渡しに入ってくる
            // ジョルジョの通り道（会話の点）とそのまま重なって、体の中へ入り込む。
            // テレビの脇へ寄せて、抜けの側（-x）を向かせる
            Cast(take, "Wife", "W_Formal",
                new Vector3(EstateTv.x + 0.15f, EstateTop, EstateTv.z + 1.55f), 292f, 0, 0.95f);
            // 隣の母親は娘を抱き上げているところ。こちらではなく西の娘を見ているので、向きはそのまま
            Cast(take, "Mother", "W_Casual", new Vector3(1.9f, EstateTop, EstateWalk + 0.05f), 285f, 1, 1f);
            // 隣は鍵を掛けて出てきたところなので、戸は閉まっている
            Shut(take, "ShutA", DoorA);
            Ajar(take, "AjarB", DoorB);
            return new[]
            {
                K(0f,  4.3f,  EstateTop, EstateWalk + 0.05f, 270f,   6f, 1.65f),  // 隣を眺めている
                K(4f,  4.3f,  EstateTop, EstateWalk + 0.05f, 270f,  12f, 1.65f),  // 母親が娘を抱き上げる
                K(8f,  4.25f, EstateTop, EstateWalk - 0.10f, 183f,  10f, 1.65f),  // 戸の内側から妻の声
                K(12f, 4.2f,  EstateTop, EstateWalk - 0.25f, 183f,  22f, 1.65f),  // 新聞を渡す
                // 戸口から先は三和土で、床より EstateSunk のぶん下がる
                K(16f, 4.2f,  EstateTop - EstateSunk, EstateFace - 0.30f, 183f,   6f, 1.65f),  // 中へ入る
                K(20f, 4.3f,  EstateTop - EstateSunk, HallSill + 0.20f,   200f,  40f, 1.20f),  // 靴を脱ぐと視界が揺れる
                K(25f, 4.4f,  EstateTop, HallWall - 0.40f,   210f,  10f, 1.65f),  // テレビの音
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
            // 座る先は抜けを入ってすぐの西寄り。元の点は仕切りの壁へ 0.26 m めり込んでいた。
            // 向きも 200 度から直す。**居間の点はどれも夫の南東にあるので、そちらへ向ける**
            var sat = new Vector3(HallGap0 + 0.15f, EstateTop, HallWall - 0.50f);
            var man = Cast(take, "Husband", "M_Casual", sat, 120f, 0, 0.95f);
            Move(man, new Vector3(DoorB + 0.10f, EstateTop - EstateSunk, EstateFace - 0.15f), sat, 6f, 5f, true);
            // 隣は鍵を掛けて出ていったあと。戸は閉まっている
            Shut(take, "ShutA", DoorA);
            Ajar(take, "AjarB", DoorB);
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

        // ---- 会話の点 ----------------------------------------------------------
        //
        // 設計書 7 節。一行目は名前を呼ばれる声なので点を持たず、二行目からを
        // 歩く道筋の上、相手の近くを通るところに置く。点は順に armed になるので、
        // 先の点の中を通り抜けても順番は飛ばない。
        //
        // 座は場所のローカルで、主の足元で測る。高さも一緒に測るので、団地の
        // 三つの階が重なっていても、下の階の点が上の階で開くことはない。
        //
        // **いまは団地の四本（記憶 0・1・8・15）だけ。** 残りの十二本は点を持たず、
        // 一行目だけ出して黙る。オーナーが「まずはマンションのシーンを作りこんで」と
        // 決めたためで、公園・電車・台所・教室はその場所を詰めるときに一緒に置く。
        //
        // 階段の点は折り返しの踊り場（<see cref="EstateTurn"/>）に置いてある。半階ごとに
        // 高さが違うので、上りと下りで同じ z に置いても取り違えない。井戸の幅いっぱいを
        // 拾えるように、そこだけ半径を広く取ってある

        /// <summary>会話の点ひとつ。<see cref="Said.where"/> と <see cref="Said.radius"/> の元</summary>
        public struct TalkSpot
        {
            public Vector3 where;
            public float radius;
        }

        static TalkSpot S(float x, float y, float z, float radius)
        {
            return new TalkSpot { where = new Vector3(x, y, z), radius = radius };
        }

        /// <summary>
        /// 記憶 i の、二行目からの点。並びが会話の並びで、i 番の点が i+1 行目に付く。
        /// 点を置いていない記憶は空を返す
        /// </summary>
        public static TalkSpot[] Spots(int entry)
        {
            switch (entry)
            {
                case 0: return MeiSpots;
                case 1: return HannaSpots;
                case 8: return GiorgioSpots;
                case 15: return ElenaSpots;
            }
            return new TalkSpot[0];
        }

        /// <summary>
        /// 0. メイ。階段の下から三階の戸口まで駆け上がり、抱えられて降ろされ、また降りる。
        /// 母とのやりとりは戸口と玄関の三和土、見送りの二行は降りる途中の踊り場と階段の下
        /// </summary>
        static readonly TalkSpot[] MeiSpots =
        {
            S(0.00f, Floor * 0.5f, EstateTurn, 1.50f),                  // メイ「えー」　一つ目の折り返し
            S(0.20f, EstateTop, EstateWalk + 0.25f, 0.95f),             // 母「はい、水筒」　三階へ上がりきったところ
            // **戸口の手前。** 三和土の奥（PorchSill の先）に置いていた頃は、母を通り越して
            // 家の中に立つことになり、背中側から水筒を受け取っていた。灯りの逆光も裏返っていた
            S(DoorA, EstateTop, EstateFace + 0.50f, 0.85f),             // メイ「ありがとう」　戸口。母の正面
            S(DoorA + 1.15f, EstateTop, EstateWalk + 0.15f, 0.85f),     // 母「もう、毎日でしょ」　廊下へ出たところ
            // 手すりへ寄せすぎると体が通らない。廊下で立てるのは z EstateWalk + 0.40 まで
            S(DoorA - 0.95f, EstateTop, EstateWalk + 0.30f, 0.85f),     // メイ「わっ」　向きを変えられて階段の側へ
            S(0.00f, Floor * 1.5f, EstateTurn, 1.50f),                  // 母「気をつけてね」　降りる途中の折り返し
            S(StairEastMid, 0f, WalkFront - 0.35f, 1.40f),              // メイ「いってきます」　階段を降り切った地面
        };

        /// <summary>
        /// 1. ハンナ。自分の戸口から隣の老人の方へ寄り、駆け上がってきた娘のところへ戻って、
        /// 階段を降り始める。老人の三行は廊下の東、娘の三行は階段の頭の側
        /// </summary>
        static readonly TalkSpot[] HannaSpots =
        {
            S(3.00f, EstateTop, EstateWalk - 0.15f, 0.85f),             // ハンナ「おはようございます」　老人の方へ一歩
            // 老人の立つところ（EstateFace + 0.25）へ半歩ぶんしか離れていなかったので、廊下の側へ出す
            S(4.20f, EstateTop, EstateWalk + 0.25f, 0.85f),             // ジョルジョ「今日は遅いんだね」　老人の脇
            S(5.15f, EstateTop, EstateWalk + 0.20f, 0.85f),             // ハンナ「ええ、午後からで」　廊下の東
            // 手すりの内側（z EstateWalk + 0.50）は体が通らない。前の点との間も一歩ぶん空ける
            S(2.55f, EstateTop, EstateWalk + 0.25f, 0.85f),             // メイ「ママ！」　娘が上がってくる側へ戻る
            S(1.45f, EstateTop, EstateWalk + 0.20f, 0.80f),             // ハンナ「どうしたの」　娘の前
            S(-0.30f, EstateTop, EstateWalk + 0.30f, 0.80f),            // メイ「体操着、わすれた」　階段の頭
            // 三階建てになって折り返しは y 1.4 と 4.2 の二つだけ。
            // 最上階から降り始めた先は Floor * 1.5。Floor * 2.5（7.0）には、もう何も無い
            S(0.00f, Floor * 1.5f, EstateTurn, 1.40f),                  // ハンナ「……もう」　降り始めた先の折り返し
        };

        /// <summary>
        /// 8. ジョルジョ。廊下で隣を眺めてから自分の戸口へ戻り、新聞を渡して中へ入る。
        /// 妻の声は戸の内側から来るので、後の二行は三和土と居間に置く
        /// </summary>
        static readonly TalkSpot[] GiorgioSpots =
        {
            S(3.10f, EstateTop, EstateWalk + 0.35f, 0.85f),             // ジョルジョ「ああ」　隣を眺めたまま西へ一歩
            S(DoorB, EstateTop, EstateWalk + 0.30f, 0.80f),             // エレナ「新聞、来てる？」　自分の戸口の前
            S(DoorB - 0.05f, EstateTop - EstateSunk, HallSill + 0.35f, 0.70f), // ジョルジョ「来てるよ」　三和土
            S(3.90f, EstateTop, HallWall - 0.30f, 0.80f),               // ジョルジョ「隣、また忘れもの」　居間への抜け
            S(4.60f, EstateTop, -17.10f, 0.95f),                        // エレナ「あらあら」　居間
        };

        /// <summary>
        /// 15. エレナ。居間から出ない記憶なので、点も居間の中で回す。
        /// 戸口の側・夫の座るところ・卓・テレビの前・東の隅の順に一巡する
        /// </summary>
        static readonly TalkSpot[] ElenaSpots =
        {
            S(4.00f, EstateTop, -15.95f, 0.80f),                        // エレナ「なあに」　抜けの側
            S(3.35f, EstateTop, -17.05f, 0.80f),                        // ジョルジョ「新聞、あったよ」　夫の座るところ
            S(3.60f, EstateTop, -18.05f, 0.85f),                        // エレナ「そこ置いといて」　卓の奥
            S(5.30f, EstateTop, -17.95f, 0.90f),                        // ジョルジョ「隣の子、また走ってた」　テレビの前
            S(6.20f, EstateTop, -16.80f, 0.95f),                        // エレナ「元気ねえ」　居間の東
        };
    }
}
