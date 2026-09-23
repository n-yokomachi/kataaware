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
    /// 目安は、話しかけに寄っていく主の立つ所への向きとの差が 90 度未満。
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

        // ---- 戸の板 ------------------------------------------------------------
        //
        // **場所ではなく記憶が持つ。** 場所は四つの記憶で使い回すので、場所に置いた板は
        // 記憶ごとに消せない。記憶 0 では隣の老夫婦はまだ一度も出てきていないのに
        // その戸が開いた穴になっていた（オーナーの差し戻し）。開けるか閉めるかは
        // 記憶ごとの話なので、Shut と Ajar を対にして記憶の子として置く。
        //
        // **形と色は建物の閉めた戸（<c>EstateDoorway</c>）を写す。** ロンドンの玄関の戸で、
        // 下に鏡板が二枚、上に細い硝子、腰の高さに真鍮の郵便受けの口、その上に番地とノッカー。
        // 色は一戸ずつ違い、<see cref="EstateDoorPaint"/> が返す（A は青、B は赤）。
        // 日本の鉄扉（換気口・新聞受け・覗き穴）だった頃の板は外した。
        //
        // 建物の面の穴・枠・欄間・丁番は場所の側（<c>BuildDiveEstate</c>）が持ち、記憶からは触れない。
        // 丁番は東の枠にあり、開いた戸はそこを軸に外（デッキの側）へ振り出して壁へ寄せる

        /// <summary>戸の番地。建物の面の欄間と揃える（<c>EstateFront</c> の 12 + 戸口の番号 × 2）</summary>
        static int EstateDoorNumber(float x)
        {
            if (Mathf.Abs(x - DoorA) < 0.5f) return 12;
            if (Mathf.Abs(x - DoorB) < 0.5f) return 14;
            return 16;
        }

        /// <summary>真鍮・硝子・郵便受けの口の暗がり・番地のマテリアル。建物の戸口と同じもの</summary>
        static Material EstateDoorMat(string which)
        {
            switch (which)
            {
                // 建物の真鍮（EstateGear）は Rail の金物、口の暗がり（EstateShade）は Ceiling
                case "Brass": return Mat("Rail");
                case "Slot": return Mat("Ceiling");
                case "Glass": return AssetDatabase.LoadAssetAtPath<Material>(Materials + "EstateGlaze.mat");
                default: return AssetDatabase.LoadAssetAtPath<Material>(Materials + "EstatePale.mat");
            }
        }

        /// <summary>
        /// 閉めた戸。戸口の穴の手前へ据えて塞ぐ。位置は <see cref="Cast"/> と同じく Take のローカル。
        /// 当たりも入れるので、覗けないし通り抜けられない。
        ///
        /// 表（デッキの側）の形は <c>EstateDoorway</c> の閉めた戸と同じ寸法で組む。
        /// 番地は欄間にも入っているが、戸そのものにも付ける（C と下の住戸の戸と揃える）
        /// </summary>
        static Transform Shut(Transform take, string name, float x)
        {
            var leaf = Piece(take, name, Shape("EstateShut", 0.4f, b =>
            {
                b.Box(new Vector3(0f, DoorHigh * 0.5f, 0.045f), new Vector3(DoorHalf * 2f, DoorHigh, 0.05f));
                // 鏡板。下に二枚
                for (var i = 0; i < 2; i++)
                    b.Box(new Vector3(i == 0 ? -0.2f : 0.2f, 0.50f, 0.075f), new Vector3(0.30f, 0.62f, 0.015f));
            }), EstateDoorPaint(x));
            leaf.localPosition = new Vector3(x, EstateTop, EstateFace);

            // 上の細い硝子
            Piece(leaf, "Glass", Shape("EstateShutGlass", 0.5f, b =>
                b.FaceZ(0.072f, -0.12f, 0.12f, 1.40f, 1.85f, 1)), EstateDoorMat("Glass"));
            // 郵便受けの真鍮の板・ノッカー・把手・鍵
            Piece(leaf, "Brass", Shape("EstateShutBrass", 0.5f, b =>
            {
                b.Box(new Vector3(0f, 1.00f, 0.078f), new Vector3(0.30f, 0.09f, 0.012f));
                b.Box(new Vector3(0f, 1.42f, 0.085f), new Vector3(0.10f, 0.03f, 0.03f));
                b.Box(new Vector3(-0.34f, 1.02f, 0.10f), new Vector3(0.05f, 0.05f, 0.08f));
                b.Box(new Vector3(-0.34f, 1.20f, 0.075f), new Vector3(0.05f, 0.07f, 0.02f));
            }), EstateDoorMat("Brass"));
            // 郵便受けの横長の口
            Piece(leaf, "Slot", Shape("EstateShutSlot", 0.5f, b =>
                b.FaceZ(0.085f, -0.12f, 0.12f, 0.985f, 1.015f, 1)), EstateDoorMat("Slot"));
            // 番地
            var number = EstateDoorNumber(x);
            Piece(leaf, "Number", Shape("EstateShutNo" + number, 0.5f, b =>
                EstateDigits(b, 0f, 1.24f, 0.074f, 1, number, 0.12f)), EstateDoorMat("Number"));

            var box = leaf.gameObject.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, DoorHigh * 0.5f, 0.045f);
            box.size = new Vector3(DoorHalf * 2f + 0.06f, DoorHigh, 0.14f);
            return leaf;
        }

        /// <summary>
        /// 開いた戸。東の枠の丁番を軸に、外へ 180 度振り出して壁へ寄せる。
        ///
        /// **見えているのは戸の裏（住戸の側）。** 振り出すと表は壁を向くので、
        /// デッキから見えるのは裏の鏡板と、郵便受けの内側の覆い、夜錠、把手。
        /// 振り出すと左右が入れ替わるので、表では西の端にあった把手と錠は東の端に来る。
        /// 裏も戸の色で塗る。白くすると、開いている戸だけ色が抜けて一戸ずつの塗り分けが読めない。
        ///
        /// **メーターの物入れは戸の西にある。** 東へ寄せる板とは重ならない
        /// </summary>
        static Transform Ajar(Transform take, string name, float x)
        {
            var leaf = Piece(take, name, Shape("EstateAjar", 0.4f, b =>
            {
                b.Box(new Vector3(0f, DoorHigh * 0.5f, 0f), new Vector3(DoorHalf * 2f, DoorHigh, 0.05f));
                for (var i = 0; i < 2; i++)
                    b.Box(new Vector3(i == 0 ? -0.2f : 0.2f, 0.50f, 0.032f), new Vector3(0.30f, 0.62f, 0.015f));
            }), EstateDoorPaint(x));
            // 丁番の芯は枠の外（x + 0.51）。板の厚みの真ん中を面から 0.25 の所に置き、丁番の出に掛ける
            leaf.localPosition = new Vector3(x + 0.51f + DoorHalf, EstateTop, EstateFace + 0.25f);

            Piece(leaf, "Glass", Shape("EstateAjarGlass", 0.5f, b =>
                b.FaceZ(0.026f, -0.12f, 0.12f, 1.40f, 1.85f, 1)), EstateDoorMat("Glass"));
            // 郵便受けの内側の覆い・夜錠・把手
            Piece(leaf, "Brass", Shape("EstateAjarBrass", 0.5f, b =>
            {
                b.Box(new Vector3(0f, 1.00f, 0.05f), new Vector3(0.32f, 0.12f, 0.05f));
                b.Box(new Vector3(0.34f, 1.22f, 0.045f), new Vector3(0.10f, 0.08f, 0.04f));
                b.Box(new Vector3(0.36f, 1.02f, 0.06f), new Vector3(0.05f, 0.05f, 0.07f));
            }), EstateDoorMat("Brass"));
            return leaf;
        }

        /// <summary>
        /// 人や鳩を一直線に動かす。位置は Take のローカル。
        ///
        /// <paramref name="ground"/> を立てると足元が真下の床へ下りる。
        /// 階段を降りる人も教壇から降りる人も、線の上下だけでは段を追えない
        /// </summary>
        static void Move(Transform who, Vector3 from, Vector3 to, float at, float span,
            bool ease, bool ground = true, int cue = -1)
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
            so.FindProperty("cue").intValue = cue;
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
        /// 靴紐を結んでいた階段の下から、三階のデッキを A の戸口まで駆けて抱き上げられ、
        /// 降ろされて降りる。母は戸口の奥の灯りを背にしているので、顔は影になる
        /// </summary>
        static HostKey[] Mei(Transform take)
        {
            // **母は戸口の敷居に立ち、デッキの西（階段の側）を向く。** メイは階段を上がり切ると
            // デッキを東へ駆けてくるので、そちらへ顔を向けておく。デッキの幅の真ん中から
            // 見上げる所（下の鍵打ちの 23 秒）への向きは 310 度で、差は 10 度。
            // 顔が影になるのは向きではなく、背にした玄関の灯り（RoomAHall）と四階の張り出しのせい。
            // 穴の奥（面から内）へ入れると、デッキの西からは戸口の東の縁に隠れて選べない
            Cast(take, "Mother", "W_Casual",
                new Vector3(DoorA, EstateTop, EstateFace + 0.05f), 320f, 1, 1f);
            // メイの家は母が戸口に立っているので開いている。隣の老夫婦はまだ一度も出てきていない
            Ajar(take, "AjarA", DoorA);
            Shut(take, "ShutB", DoorB);
            return new[]
            {
                // 階段の下。上り口のすぐ手前で、始まりの立ち位置（FirstStand）と同じ点。正面に東の一本の段
                K(0f,   StairEastMid, 0f, WalkFront - 0.45f,   0f,  48f, 0.55f),  // しゃがんで靴紐。指と地面しか見えない
                K(3f,   StairEastMid, 0f, WalkFront - 0.35f,   0f, -32f, 1.00f),  // 立って階段の上を仰ぐ
                // 東の一本を北へ上がり、折り返して西の一本を南へ。半階ごとに向きが入れ替わる
                K(8f,   0f,   Floor * 0.5f, EstateTurn,     180f, -12f, 1.00f),  // 一つ目の折り返し
                K(12f,  0f,   Floor,        EstateLanding1,   0f, -10f, 1.00f),  // 二階の踊り場
                K(16f,  0f,   Floor * 1.5f, EstateTurn,     180f, -12f, 1.00f),  // 二つ目の折り返し
                K(20f,  StairWestMid, EstateTop, WalkFront - 0.30f, 100f, -4f, 1.00f), // 三階のデッキ。東に戸が並ぶ
                K(23f,  4.60f, EstateTop, EstateWalk,       124f,  22f, 1.00f),  // A の戸口の前。母の脚
                K(25f,  5.00f, EstateTop, EstateWalk - 0.40f, 124f, -42f, 1.45f), // 抱き上げられる
                K(28f,  5.00f, EstateTop, EstateWalk - 0.40f, 215f, -34f, 1.50f), // 回る。空、四階の張り出し、隣の戸口
                K(31f,  5.00f, EstateTop, EstateWalk - 0.40f, 140f,   6f, 1.00f), // 降ろされる
                K(35f,  4.70f, EstateTop, EstateWalk - 0.10f, 280f,   2f, 1.00f), // 背中を押されて向きが変わる
                K(40f,  StairWestMid, EstateTop, WalkFront - 0.30f, 0f, 12f, 1.00f), // 西の一本を北へ降り始める
                K(45f,  0f,   Floor * 1.5f, EstateTurn,     180f,   8f, 1.00f),  // 降りる途中の折り返し
                K(49f,  0f,   Floor,        EstateLanding2,  60f, -26f, 1.00f),  // 二階の踊り場で振り返る
                K(54f,  0f,   Floor * 0.5f, EstateTurn,     180f,   8f, 1.00f),
                K(60f,  1.80f, 0f,          WalkFront - 0.45f, 20f,  0f, 1.00f), // 降り切って、庭への小道へ
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
            // 娘が駆け上がってくるのは、老人とのやりとりが終わってから。
            // 四行目（ハンナ「ええ、午後からで」）が出たら数え始める
            Move(kid, new Vector3(StairWestMid, EstateTop, WalkFront - 0.10f), stood, 0f, 3.5f, true, true, 4);
            // 老人も廊下の側（+z）を向く。130 度では自分の戸口の方を向いていて、
            // 廊下から寄っていくハンナには背中しか見えなかった
            Cast(take, "Neighbour", "M_Casual",
                new Vector3(DoorB - 0.20f, EstateTop, EstateFace + 0.25f), 0f, 3, 0.95f);
            // ハンナは出しなに鍵を掛けたところ。自分の戸は閉まっている
            Shut(take, "ShutA", DoorA);
            Ajar(take, "AjarB", DoorB);
            return new[]
            {
                // **始まりは娘の方を向く。** 鍵を掛けた戸（178 度）を向いて始めていたが、
                // 目の前が自分の戸の板だけになって、誰の記憶に入ったのか読めなかった。
                // 階段の口に立つ娘へ向けておけば、最初の一枚で母娘だと分かる。
                // 老人の声は東から来るので、探して振り向くだけの間を字幕の側で持たせている
                K(0f,  1.9f,  EstateTop, EstateWalk,         283f,  13f, 1.55f),  // 階段の口の娘を見ている
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
            // **妻は廊下の突き当たりの硝子戸の奥に立たせる。** 戸口の前から廊下を真っ直ぐ
            // 覗いた線の先で、引き込み残った硝子戸の一枚越しに胸から上が見える。
            // 「新聞、来てる？」は戸口の前で聞くので、そこから姿が見えないと誰の声か分からない。
            // 通り道（廊下の真ん中）から西へ外し、戸口の方を向かせる
            Cast(take, "Wife", "W_Formal", new Vector3(RoomHall0 + 0.12f, EstateTop, RoomEnd - 0.54f), 9f, 0, 0.95f);
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
            // 座る先は廊下の硝子戸を抜けてすぐの座布団。炬燵の北の辺に、妻と並んで座る。
            // **歩く線は廊下の真ん中の一本（RoomWalk）。** Mover は始まりと終わりを
            // 一直線に結ぶだけなので、三和土の始まりと座る所を同じ x に揃えて、
            // 廊下の壁も硝子戸も手すりも跨がないようにする。向きは炬燵とテレビの方
            var sat = new Vector3(RoomWalk, EstateTop, RoomEnd - 0.34f);
            var man = Cast(take, "Husband", "M_Casual", sat, 180f, 0, 0.95f);
            // 夫が三和土から座るところまで歩くのは、名を呼ばれて返事をしてから。
            // 二行目（エレナ「なあに」）が出たら数え始める
            Move(man, new Vector3(RoomWalk, EstateTop - EstateSunk, EstateFace - 0.15f), sat, 0f, 5f, true, true, 2);
            // 隣は鍵を掛けて出ていったあと。戸は閉まっている
            Shut(take, "ShutA", DoorA);
            Ajar(take, "AjarB", DoorB);
            return new[]
            {
                // 座っているのは炬燵の北の辺の東、座椅子の上。夫の座る所の 0.74 m 東。
                // 戸口は廊下の東の壁の陰で見えないので、声の方へは廊下の突き当たりの抜けを向く
                K(0f,  5.10f, EstateTop, -17.02f, 171f,   8f, 1.15f),  // テレビの前
                K(3f,  5.10f, EstateTop, -17.02f, 292f,   2f, 1.15f),  // 戸口から夫の声
                K(9f,  5.10f, EstateTop, -17.02f, 296f,   0f, 1.15f),  // 新聞を持って入ってくる
                K(14f, 5.10f, EstateTop, -17.02f, 272f,   8f, 1.15f),  // 隣に座る
                K(18f, 5.10f, EstateTop, -17.02f, 270f,  16f, 1.15f),  // 新聞を広げる音
                K(25f, 5.10f, EstateTop, -17.02f, 171f,  26f, 1.15f),  // テレビの光が床に当たっている
            };
        }
    }
}
