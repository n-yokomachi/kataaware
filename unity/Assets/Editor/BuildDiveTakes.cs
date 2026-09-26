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
    /// **顔は影で隠す。向きでは隠さない。** 人は Rocketbox の写真から作った顔を持つので、
    /// 逆光・帽子の影・伏せた角度で見せる（設計書 6 節）。そこを確かめる。
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
        /// **階段の塔の前の庭。** 塔の北東の角から 3 m ほど北で、三階の手すりから身を乗り出す母まで横に 7 m、
        /// 母の胸を見上げる角が 40° ほど。庭の真ん中（z 2.4）から始めていた頃は、
        /// 潜った直後に何も無い地面を 15.8 m 歩かされてから、ようやく一段目に着いた。
        /// 上り口のすぐ手前（デッキの下）から始めていた頃は、三階の手すりから呼ぶ母が
        /// デッキの床に遮られて見えず、「えーなにー？」と見上げて返す相手を選べなかった（設計書 7 節の 1）。
        /// 手すりの真下へ寄せると、見上げる角がきつくなって首が折れる
        /// </summary>
        static readonly Vector3 FirstStand = new Vector3(1.6f, 0f, WalkFront + 6.6f);
        /// <summary>始まりの向き。棟の方（南南東）を向く。見上げれば三階の手すりから母が身を乗り出している</summary>
        const float FirstYaw = 172f;

        static Transform Takes(Transform root, DiveRoster roster)
        {
            var parent = Child(root, "Takes");
            Clear(parent);
            ForgetFigures();

            for (var i = 0; i < roster.Count && i < Memories.Length; i++)
            {
                var go = new GameObject(i.ToString());
                go.transform.SetParent(parent, false);
                go.transform.localPosition = PlaceOrigin(roster[i].place);
                // 人は板の相手の名前から一覧の飛び先を引いて決まる（BuildDivePeople の Cast）
                casting = roster[i];
                stops.Clear();
                var keys = Memories[i](go.transform);

                var take = go.AddComponent<Take>();
                var so = new SerializedObject(take);
                so.FindProperty("entry").intValue = i;
                Notes(so.FindProperty("keys"), keys);
                Fill(so.FindProperty("people"), Lineup(go.transform, roster[i].seen));
                var stopRow = so.FindProperty("stops");
                stopRow.arraySize = stops.Count;
                for (var s = 0; s < stops.Count; s++) stopRow.GetArrayElementAtIndex(s).vector3Value = stops[s];
                so.ApplyModifiedPropertiesWithoutUndo();
                // 歩ける範囲の見えない囲い（BuildDivePens）。記憶の子なので、起こした記憶の分だけが効く
                Pen(go.transform, keys, i);

                // 潜るまでは伏せる。DiveDirector が一本だけ起こす
                go.SetActive(false);
                // 測りと持ち物のために置いた形を、模型の素へ戻してから保存する
                UnposeFigures(go.transform);
            }
            return parent;
        }

        /// <summary>
        /// 組んでいる記憶の〔区切り〕の行き先。区切りの並び。場所のローカル。
        /// 記憶ごとの関数が <see cref="Stop"/> で積み、<see cref="Takes"/> が Take へ書く
        /// </summary>
        static readonly List<Vector3> stops = new List<Vector3>();

        /// <summary>
        /// 〔区切り〕の後の会話を始める所を積む。区切りの並びに呼ぶ。
        /// 主の足元がここから <c>DiveDirector.reach</c> の内に入るまで、区切りの後の会話は始まらない。
        /// 囲い（BuildDivePens）もこの点を囲う
        /// </summary>
        static void Stop(float x, float y, float z)
        {
            stops.Add(new Vector3(x, y, z));
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
        //
        // 人を置く Cast と、見た目・骨・動きを組むところは BuildDivePeople.cs にある。
        // 誰かは一覧の Seen の名前と飛び先で決まるので、ここでは名前・立ち位置・向き・立ち方だけを渡す

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
        // **ロンドンの住戸の玄関の戸は内開き。** 丁番は戸口の内側の面の東の縁にあり、
        // 開いた戸はそこを軸に住戸の中へ開いて、玄関の東の壁（戸境の壁）へ寄せる

        /// <summary>戸の番地。建物の面の戸口と揃える（<c>EstateFront</c> の 12 + 戸口の番号 × 2）</summary>
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
        /// 番地は戸そのものに付ける（C と下の住戸の戸と揃える）。A と B の欄間には入れない。
        /// 欄間にも入れていた頃は、閉めた戸で番地が二つ並んだ
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
        /// 開いた戸。住戸の中へ開き、戸口の内側の面の東の縁の丁番を軸に、玄関の東の壁（戸境の壁）へ寄せる。
        ///
        /// **ロンドンの住戸の玄関の戸は内開き。** 前はデッキの側へ 180 度振り出して表の壁に寄せていたが、
        /// 開き方が逆だった。
        ///
        /// **板の先が戸境の壁に当たるまで開く。** 丁番から戸境の壁までは 0.25 m あり、
        /// 90 度では板が壁から浮く。先を壁へ当てると 101 度ほど開いて斜めに寄る。
        /// 板は戸口の幅の外（東の縁の線より東）に収めるので、戸口の 1.0 m を食わない。
        /// 板の先は階段の一段目のすぐ手前に来るので、仕切りの下の段を開けて
        /// （BuildDiveEstateFlat の <see cref="NewelSteps"/>）、板の先と仕切りの端のあいだを 1.0 m 以上取る。
        /// コライダーも入れる。板と戸境の壁のあいだの三角は、先が壁に付いているので体が入り込まない。
        ///
        /// **表は玄関の中の西を向く。** 番地・郵便受けの口・ノッカー・把手は表に、閉めた戸（<see cref="Shut"/>）と
        /// 同じ位置で付ける。デッキから覗くと、戸口の東の縁の奥に表が斜めに見える。
        /// 裏（戸境の壁の側）は郵便受けの内側の覆い・夜錠・把手。
        /// 裏も戸の色で塗る。白くすると、開いている戸だけ色が抜けて一戸ずつの塗り分けが読めない
        /// </summary>
        static Transform Ajar(Transform take, string name, float x)
        {
            const float thick = 0.05f;
            var wide = DoorHalf * 2f;
            // 板の先の裏（東の面）を戸境の壁の面から 5 mm 手前で止める開き。丁番から壁までは FlatInner - DoorAt
            var room = FlatInner - DoorAt - DoorHalf - thick - 0.004f;
            var lean = Mathf.Asin(room / wide);
            // 板のローカルは、丁番の縁が x = 0、戸先が x = -wide（閉めたときの西）、
            // 裏の面が z = 0、表の面が z = thick（閉めたときのデッキの側）
            var leaf = Piece(take, name, Shape("EstateAjar", 0.4f, b =>
            {
                b.Box(new Vector3(-DoorHalf, DoorHigh * 0.5f, thick * 0.5f), new Vector3(wide, DoorHigh, thick));
                // 鏡板。表と裏に二枚ずつ
                for (var i = 0; i < 2; i++)
                {
                    var px = -DoorHalf + (i == 0 ? -0.2f : 0.2f);
                    b.Box(new Vector3(px, 0.50f, thick + 0.0075f), new Vector3(0.30f, 0.62f, 0.015f));
                    b.Box(new Vector3(px, 0.50f, -0.0075f), new Vector3(0.30f, 0.62f, 0.015f));
                }
            }), EstateDoorPaint(x));
            // 丁番の芯。表の面の丁番の側の縁が、戸口の東の縁の線（x + DoorHalf）に来る
            leaf.localPosition = new Vector3(x + DoorHalf + thick * Mathf.Cos(lean), EstateTop, EstateFace - FaceSkin - 0.005f);
            leaf.localRotation = Quaternion.Euler(0f, -90f - lean * Mathf.Rad2Deg, 0f);

            Piece(leaf, "Glass", Shape("EstateAjarGlass", 0.5f, b =>
            {
                b.FaceZ(thick + 0.001f, -DoorHalf - 0.12f, -DoorHalf + 0.12f, 1.40f, 1.85f, 1);
                b.FaceZ(-0.001f, -DoorHalf - 0.12f, -DoorHalf + 0.12f, 1.40f, 1.85f, -1);
            }), EstateDoorMat("Glass"));
            Piece(leaf, "Brass", Shape("EstateAjarBrass", 0.5f, b =>
            {
                // 表。郵便受けの真鍮の板・ノッカー・把手・鍵。把手と鍵は戸先の側
                b.Box(new Vector3(-DoorHalf, 1.00f, thick + 0.006f), new Vector3(0.30f, 0.09f, 0.012f));
                b.Box(new Vector3(-DoorHalf, 1.42f, thick + 0.015f), new Vector3(0.10f, 0.03f, 0.03f));
                b.Box(new Vector3(-wide + 0.16f, 1.02f, thick + 0.04f), new Vector3(0.05f, 0.05f, 0.08f));
                b.Box(new Vector3(-wide + 0.16f, 1.20f, thick + 0.01f), new Vector3(0.05f, 0.07f, 0.02f));
                // 裏。郵便受けの内側の覆い・夜錠・把手
                b.Box(new Vector3(-DoorHalf, 1.00f, -0.025f), new Vector3(0.32f, 0.12f, 0.05f));
                b.Box(new Vector3(-wide + 0.16f, 1.22f, -0.02f), new Vector3(0.10f, 0.08f, 0.04f));
                b.Box(new Vector3(-wide + 0.14f, 1.02f, -0.035f), new Vector3(0.05f, 0.05f, 0.07f));
            }), EstateDoorMat("Brass"));
            // 郵便受けの横長の口
            Piece(leaf, "Slot", Shape("EstateAjarSlot", 0.5f, b =>
                b.FaceZ(thick + 0.013f, -DoorHalf - 0.12f, -DoorHalf + 0.12f, 0.985f, 1.015f, 1)), EstateDoorMat("Slot"));
            // 番地。閉めた戸と同じ高さ
            var number = EstateDoorNumber(x);
            Piece(leaf, "Number", Shape("EstateAjarNo" + number, 0.5f, b =>
                EstateDigits(b, -DoorHalf, 1.24f, thick + 0.004f, 1, number, 0.12f)), EstateDoorMat("Number"));

            var box = leaf.gameObject.AddComponent<BoxCollider>();
            box.center = new Vector3(-DoorHalf, DoorHigh * 0.5f, thick * 0.5f);
            box.size = new Vector3(wide, DoorHigh, thick);
            return leaf;
        }

        /// <summary>
        /// 人や鳩を一直線に動かす。位置は Take のローカル。
        /// 片脚に預ける人（pose 1）と座る人は脚の向きを据えるので、動かさない（<see cref="HeldBones"/>）。
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
        /// その場で振り向かせる。<paramref name="cue"/> 行が出てから <paramref name="at"/> 秒で、根の向きを <paramref name="yaw"/> へ替える。
        /// 振り向く前の向きは、置いたときの向き。
        /// 模型は PersonMotion がこまの頭ごとに根の向きへ寄せるので、半秒ほどで回り切る
        /// </summary>
        static void Turn(Transform who, float yaw, float at, int cue)
        {
            if (who == null) return;
            var from = who.localEulerAngles.y;
            Move(who, who.localPosition, who.localPosition, at, 0.1f, false, true, cue);
            var so = new SerializedObject(who.GetComponent<Mover>());
            so.FindProperty("turns").boolValue = true;
            so.FindProperty("yawFrom").floatValue = from;
            so.FindProperty("yawTo").floatValue = yaw;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>
        /// 歩く人の向きを据える。<paramref name="walk"/> は歩くあいだの向き、<paramref name="end"/> は歩き終えてからの向き。
        /// 歩き出す前は置いたときの向き。**Move の後に呼ぶ。**
        /// 根の向きを替えないまま歩かせると、階段の上から見下ろしていた母が、戸口まで横歩きで戻ってくる
        /// </summary>
        static void Face(Transform who, float walk, float end, float next = float.NaN)
        {
            var mover = who != null ? who.GetComponent<Mover>() : null;
            if (mover == null) return;
            var so = new SerializedObject(mover);
            so.FindProperty("turns").boolValue = true;
            so.FindProperty("yawFrom").floatValue = who.localEulerAngles.y;
            so.FindProperty("yawTo").floatValue = walk;
            so.FindProperty("settles").boolValue = true;
            so.FindProperty("yawEnd").floatValue = end;
            // 二本目を歩き出してからの向き。渡さなければ一本目を歩き終えた向きのまま
            so.FindProperty("yawNext").floatValue = float.IsNaN(next) ? end : next;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>
        /// 台所の夫（マーク）を、<paramref name="cue"/> 行が出たら玄関から出ていかせる（記憶 6・12）。
        /// 流しの前から台所の戸口（x ±0.6 の開口）の西寄りを抜け、廊下を玄関へ斜めに歩き、開いた戸の外の段で止まる。
        /// 一直線では台所の壁を抜けるので、戸口の外で一度折れる。
        /// 段の上で止めるのは、出ていったあとも開いた戸の向こうに背中が見えて、板を出せるようにするため
        /// </summary>
        static void Leave(Transform who, int cue)
        {
            var from = who.localPosition;
            var hall = new Vector3(-0.30f, 0f, -0.35f);
            var step = new Vector3(-3.40f, 0f, -1.45f);
            Move(who, from, hall, 0.3f, 2.6f, true, true, cue);
            Then(who, step, 2.9f, 3.4f);
            Face(who, Toward(from, hall), Toward(hall, step), Toward(hall, step));
        }

        /// <summary>据えた形（身を乗り出すなど）を、Mover で歩き出したら解く</summary>
        static void LetGo(Transform who)
        {
            var motion = who != null ? who.GetComponent<PersonMotion>() : null;
            if (motion == null) return;
            var so = new SerializedObject(motion);
            so.FindProperty("letsGo").boolValue = true;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>from から to を向く向き。+z を 0 とした度</summary>
        static float Toward(Vector3 from, Vector3 to)
        {
            var d = to - from;
            return Mathf.Repeat(Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg, 360f);
        }

        /// <summary>記憶 12 の先輩が、一行目が出てから振り向くまでの秒。背中を一度見せてから回る</summary>
        const float SeniorTurnsAfter = 0.8f;

        /// <summary>
        /// <see cref="Move"/> の線の終わりから、もう一本続けて動かす（駆け出して戻ってくる人）。
        /// 秒は一本目と同じ時計で数える。同じ人に Mover を二つ付けると後の方が先の方を上書きするので、一つにまとめる
        /// </summary>
        static void Then(Transform who, Vector3 to, float at, float span, int cue = -1)
        {
            var mover = who != null ? who.GetComponent<Mover>() : null;
            if (mover == null) return;
            var so = new SerializedObject(mover);
            so.FindProperty("next").vector3Value = to;
            so.FindProperty("nextAt").floatValue = at;
            so.FindProperty("nextSpan").floatValue = span;
            // 二本目を別の行で動かし出すなら、at はその合図から数える
            so.FindProperty("nextCue").intValue = cue;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>
        /// 鳩の群れ。足元から一斉に飛び立つので、端は滑らかに繋がない。
        /// 場所ではなく記憶の側に置くのは、飛び立つ秒が記憶ごとに違うため
        /// </summary>
        static Transform Doves(Transform take, Vector3 at, float when, int cue = -1)
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
                    when + i * 0.06f, 1.6f, false, false, cue);
            }
            return flock;
        }

        /// <summary>
        /// 飛び立った鳩を、撒いた餌へ寄せる。飛び立ちの線の先から、足元のまわりへ降りてくる。
        /// <paramref name="cue"/> 行が出てから数え始める（記憶 11 の「うわっ！　全部こっち来た」）。
        /// 撒いた主のすぐ足元には来ず、0.5〜0.9 m ほど離れた輪に散らばる
        /// </summary>
        static void Gather(Transform flock, Vector3 feet, int cue)
        {
            for (var i = 0; i < flock.childCount; i++)
            {
                var bird = flock.GetChild(i);
                var a = Mathf.Deg2Rad * (i * 45f + 10f);
                var ring = 0.5f + (i % 3) * 0.2f;
                Then(bird, feet + new Vector3(Mathf.Cos(a) * ring, 0f, Mathf.Sin(a) * ring), 0.1f + i * 0.08f, 1.3f, cue);
            }
        }

        /// <summary>
        /// 餌の紙袋。手の骨に付ける小さな紙袋で、口を折った茶色の箱。
        /// <paramref name="arrives"/> なら合図で現れ、切れば合図で消える（<see cref="Handed"/>）。
        /// 記憶 10・11 のローザの手から孫の手へ渡る
        /// </summary>
        static void FeedBag(Transform who, bool right, int cue, bool arrives)
        {
            var k = FigureSpan(who);
            var hand = right ? HumanBodyBones.RightHand : HumanBodyBones.LeftHand;
            var wrist = FigureAt(who, hand);
            var sign = wrist.x < 0f ? -1f : 1f;
            // 握った指の少し下に、袋の口を持つ形で提げる
            var at = new Vector3(wrist.x + sign * 0.02f * k, wrist.y - 0.14f * k, wrist.z + 0.02f * k);
            var mesh = Shape("FeedBag" + Stamp(at) + Mathf.RoundToInt(k * 100f), 0.5f, b =>
            {
                b.Box(at, new Vector3(0.09f, 0.16f, 0.06f) * k);
                // 折った口
                b.Box(at + new Vector3(0f, 0.09f * k, 0f), new Vector3(0.095f, 0.02f, 0.065f) * k);
            });
            var bag = FigureAttach(who, hand, "FeedBag", mesh, Mat("Paper"));
            if (bag == null) return;
            var handed = bag.gameObject.AddComponent<Handed>();
            var so = new SerializedObject(handed);
            so.FindProperty("cue").intValue = cue;
            so.FindProperty("arrives").boolValue = arrives;
            so.ApplyModifiedPropertiesWithoutUndo();
            handed.Show(0);
        }

        // ---- 0. 女 6『メイ』 団地の外階段。60 秒 ---------------------------------

        /// <summary>
        /// 靴紐を結んでいた階段の下から、三階のデッキを A の戸口まで駆けて抱き上げられ、
        /// 降ろされて降りる。母は戸口の奥の灯りを背にしているので、顔は影になる
        /// </summary>
        static HostKey[] Mei(Transform take)
        {
            // **母は初め三階のデッキの手すりから身を乗り出して、庭を覗き込んで呼ぶ**（pose 7）。
            // 階段の前の庭のメイが見上げて「えーなにー？」と返す相手なので（設計書 7 節の 1）、庭から誰か分からなければならない。
            // 手すりの内に立たせただけでは、庭から見上げても手すりの上に頭の天辺が点で出るだけだった（2026-09-26 の差し戻し）。
            // 胸から上を手すりの外へ出し、両手を手すりに掛ける。手すりの線（デッキの面から 0.075 m 外、z -12.875）から
            // RailAhead だけ内に立ち、庭の方（真北）を向いて身を乗り出す。メイの方へは首と頭を向ける（PersonMotion.Watch）。
            // x は階段の塔のすぐ東。庭のメイからほぼ正面に見上げる所。
            // **「いいから上がっておいで」が出たら戸口へ戻る。** 歩き出したら身を乗り出した形を解く（PersonMotion の letsGo）。
            // 三階の戸口で区切りの後の会話をする。戸口では敷居に立ち、デッキの西（階段の側）を向く。
            // メイは階段を上がり切るとデッキを東へ駆けてくるので、そちらへ顔を向けておく。
            // 顔が影になるのは向きではなく、背にした玄関の灯り（RoomAHall）と四階の張り出しのせい。
            // 穴の奥（面から内）へ入れると、デッキの西からは戸口の東の縁に隠れて選べない
            var rail = new Vector3(2.5f, EstateTop, -12.875f - RailAhead);
            var sill = new Vector3(DoorA, EstateTop, EstateFace + 0.05f);
            var mother = Cast(take, "Mother", rail, 0f, 7);
            LetGo(mother);
            // 五行目（ハンナ「投げません。いいから上がっておいで」）が出たら戸口へ戻る
            Move(mother, rail, sill, 0.6f, 3.2f, true, true, 5);
            Face(mother, Toward(rail, sill), 320f);
            // 区切りの行き先は、A の戸口の前のデッキ
            Stop(4.60f, EstateTop, EstateWalk);
            // メイの家は母が戸口に立っているので開いている。隣の老夫婦はまだ一度も出てきていない
            Ajar(take, "AjarA", DoorA);
            Shut(take, "ShutB", DoorB);
            return new[]
            {
                // 階段の前の庭。始まりの立ち位置（FirstStand）と同じ点。正面に階段の口と、三階の手すり
                K(0f,   FirstStand.x, 0f, FirstStand.z, FirstYaw,  48f, 0.55f),  // しゃがんで靴紐。指と地面しか見えない
                K(3f,   FirstStand.x, 0f, FirstStand.z, FirstYaw, -38f, 1.00f),  // 立って三階の手すりの母を仰ぐ
                K(5f,   StairEastMid, 0f, WalkFront - 0.35f,   0f, -12f, 1.00f),  // 階段の上り口
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
        /// A の戸に鍵を掛けたところで隣の老人に呼ばれ、階段から駆けてきた娘に体操着を渡す。
        /// 老人は B の戸口、娘は階段の口。ハンナは二人のあいだの自分の戸の前に立つ
        /// </summary>
        static HostKey[] Hanna(Transform take)
        {
            // **娘は階段の口からデッキを東へ駆けて、ハンナの西の脇で止まる。**
            // 上がり切る一本（西の一本）は南へ上がってデッキへ出るので、段の途中から
            // A の戸の前まで一直線に結ぶと、二本のあいだの中壁を抜ける。
            // 出てくるのは踊り場の上（階段の口）からにして、中壁の端から 0.4 m 南を通す。
            // 始まりの一枚にも娘が入る（下の鍵打ちの先頭の向き）
            var stood = new Vector3(4.30f, EstateTop, EstateWalk - 0.05f);
            var kid = Cast(take, "Daughter", stood, 80f, 0);
            // 娘が駆けてくるのは、老人とのやりとりの終わり際。
            // 四行目（ハンナ「今日は十時からなんです」）が出たら数え始める。6 m を 3.5 秒で。
            // 次の老人の「おや。誰か上がってくるぞ」で、階段の口から駆けてくる娘が見える
            Move(kid, new Vector3(StairWestMid, EstateTop, WalkFront - 0.35f), stood, 0f, 3.5f, true, true, 4);
            // 老人は B の戸口の前で、西のハンナの方を向く。新聞を取りに出たところ
            Cast(take, "Neighbour",
                new Vector3(DoorB - 0.25f, EstateTop, EstateFace + 0.45f), 272f, 3);
            // ハンナは出しなに鍵を掛けたところ。自分の戸は閉まっている。老人の戸は開いている
            Shut(take, "ShutA", DoorA);
            Ajar(take, "AjarB", DoorB);
            return new[]
            {
                // **始まりは娘の方を向く。** 鍵を掛けた戸を向いて始めていたが、
                // 目の前が自分の戸の板だけになって、誰の記憶に入ったのか読めなかった。
                // 階段の口に立つ娘へ向けておけば、最初の一枚で母娘だと分かる。
                // 老人の声は背の側（東）から来るので、探して振り向くだけの間を字幕の側で持たせている
                K(0f,  5.05f, EstateTop, EstateWalk - 0.40f, 280f,   4f, 1.55f),  // 階段の口の娘を見ている
                K(3f,  5.05f, EstateTop, EstateWalk - 0.40f,  93f,   2f, 1.55f),  // 隣の戸口からの声
                K(6f,  5.30f, EstateTop, EstateWalk - 0.35f,  92f,   4f, 1.55f),  // 老人が会釈する
                K(10f, 5.25f, EstateTop, EstateWalk - 0.30f, 275f,   6f, 1.55f),  // 娘がデッキを駆けてくる
                K(14f, 5.20f, EstateTop, EstateWalk - 0.25f, 272f,  38f, 1.55f),  // 体操着の袋を渡す
                K(18f, 5.20f, EstateTop, EstateWalk - 0.25f, 272f,  24f, 1.55f),  // 抱き上げる
                K(22f, 5.20f, EstateTop, EstateWalk - 0.25f, 275f,  40f, 1.55f),  // 降ろす。駆け戻っていく
                K(25f, 5.20f, EstateTop, EstateWalk - 0.25f,  92f,   4f, 1.55f),  // 老人はまだ新聞を広げている
                K(30f, 3.00f, EstateTop, EstateWalk + 0.30f, 280f,   8f, 1.55f),  // 階段の方へ歩き出す
            };
        }

        // ---- 2. 男 78『アルベルト』 公園のベンチ。60 秒 ---------------------------

        /// <summary>
        /// ベンチから門まで。手元がぼやける体なので、握らされた石は最後まで形にならない。
        /// 孫娘と妻は先に立って歩くので、どちらも背中しか見えない
        /// </summary>
        static HostKey[] Albert(Transform take)
        {
            // **人は台詞で動き出す**（設計書 7 節）。ベンチの前の会話を終えると（六行目、アルベルト「助かるよ」）、
            // 孫娘は池の縁へ、妻は門の前へ先に歩いていく。主は〔区切り〕で池の縁まで歩き、
            // 鳩の話と石の話を孫娘とし、門の前の妻と話す
            var girlFrom = new Vector3(-0.9f, 0f, 0.7f);
            var girlTo = new Vector3(-0.55f, 0f, 4.7f);
            var girl = Cast(take, "Granddaughter", girlFrom, 0f, 0);
            Move(girl, girlFrom, girlTo, 0.5f, 6f, true, true, 6);
            // 池の縁では、ベンチの方から歩いてくる祖父の方を向いて待つ
            Face(girl, Toward(girlFrom, girlTo), 230f);
            var wifeFrom = new Vector3(2.2f, 0f, 0.6f);
            var wifeTo = new Vector3(0.9f, 0f, 7.2f);
            var wife = Cast(take, "Wife", wifeFrom, 8f, 0);
            Move(wife, wifeFrom, wifeTo, 1.5f, 11f, true, true, 6);
            // 門の前では、池の方から来る夫の方を向く
            Face(wife, Toward(wifeFrom, wifeTo), 215f);
            // 鳩は区切りの後の最初の行（ソフィア「見て！　鳩がいっせいに飛んだ」）で飛び立つ
            Doves(take, new Vector3(-1.1f, 0.09f, 5.6f), 0.2f, 7);
            // 区切りの行き先は池の縁
            Stop(-1.1f, 0f, 4.3f);
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
            // **人は台詞で動き出す**（設計書 7 節）。ベンチの前の会話を終えると（五行目、アルベルト「助かるよ」）、
            // 祖父はゆっくり池の縁へ、祖母はその後ろを歩く。主は〔区切り〕で池の縁まで歩き、
            // 鳩が飛び立って、石を祖父の手に握らせる
            var oldFrom = new Vector3(0.95f, 0f, 0.35f);
            var oldTo = new Vector3(-0.5f, 0f, 4.9f);
            var old = Cast(take, "Grandfather", oldFrom, 20f, 3);
            Move(old, oldFrom, oldTo, 0.5f, 8f, true, true, 5);
            // 池の縁では、ベンチの方から来るソフィアの方を向く
            Face(old, Toward(oldFrom, oldTo), 200f);
            var granFrom = new Vector3(2.2f, 0f, 0.4f);
            var granTo = new Vector3(0.6f, 0f, 5.4f);
            var gran = Cast(take, "Grandmother", granFrom, 10f, 0);
            Move(gran, granFrom, granTo, 1.0f, 9f, true, true, 5);
            Face(gran, Toward(granFrom, granTo), 231f);
            // 鳩は区切りの後の最初の行（ソフィア「見て！　鳩がいっせいに飛んだ」）で飛び立つ
            Doves(take, new Vector3(-1.2f, 0.09f, 4.4f), 0.2f, 6);
            // 区切りの行き先は池の縁
            Stop(-0.9f, 0f, 4.2f);
            // 通りすがり。イヤホンをした少女（記憶 11 のプリヤ）が、小径の東の芝生の奥を北から南へ横切り、
            // 三脚目のベンチの北東で止まる。公園から電車へ出る口はここ一つ（設計書 6 節）。
            // ソフィアがしゃがんでいた所から東を向けば、植え込みの南の端より手前を通って見える。
            // 会話とは関わらないので記憶の時計で動く
            var stop = new Vector3(6.4f, 0f, 2.4f);
            var teen = Cast(take, "Passerby", stop, 188f, 0);
            Aside(teen);
            Earphones(teen);
            Move(teen, new Vector3(7.6f, 0f, 10.5f), stop, 3f, 9f, true);
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
            Cast(take, "Junior", new Vector3(0.15f, 0f, -0.9f), 315f, 0);
            // 向かいの男は、肘掛け（z 1.75）と仕切り板（z 3.55）のあいだの座面の真ん中に座る。
            // z 1.8 に置いていた頃は、肘掛けの上に跨って座っていた
            Aside(Cast(take, "Passenger", new Vector3(SeatX, 0f, 2.65f), 250f, 5));
            return new[]
            {
                K(0f,  0.2f,  0f, 0.6f,   70f,   6f, 1.58f),  // 窓の外を灯りが流れている
                K(4f,  0.2f,  0f, 0.6f,  182f,  16f, 1.58f),  // 後ろからの声に振り向く
                K(9f,  0.2f,  0f, 0.5f,  182f,  22f, 1.58f),  // 借りていた本を受け取る
                K(14f, 0.2f,  0f, 0.45f, 178f,  10f, 1.58f),
                K(17f, 0.32f, 0f, 0.35f, 205f,  18f, 1.52f),  // 電車が揺れて二人でよろける
                K(20f, 0.2f,  0f, 0.45f, 175f,  14f, 1.58f),  // 腕に掴まられる
                K(23f, 0.1f,  0f, 0.6f,   24f,   8f, 1.58f),  // 向かいの男が新聞を畳んで立つ
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
            // **妻は戸口から台所のマークの方を向く。** 昼食の袋を手渡す相手なので（設計書 6 節の 6）。
            // 175° では戸口で廊下の方を向き、マークに背を向けたまま袋を渡すことになっていた。
            // 流しの前（鍵打ちの頭）と袋を受け取る所（13 秒）のあいだへ向ける。廊下の灯りを背にするので顔は逆光のまま
            Cast(take, "Wife", new Vector3(0f, 0f, 0.35f), 335f, 1);
            // 息子は、妻が二階へ声を掛けて返事をしたら（十行目、ダニエル「分かってる」）階段を降りてくる
            var son = Cast(take, "Son", new Vector3(StairX, 0f, -0.7f), 190f, 0);
            Move(son, new Vector3(StairX, 2.4f, -4.1f), new Vector3(StairX, 0f, -0.7f), 0.5f, 5f, true, true, 10);
            // 〔区切り〕の行き先は玄関。靴を履き、戸を開けて振り返ると、台所の戸口に妻がいる
            Stop(-1.75f, 0f, -1.9f);
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
            // **夫は「行ってくる」で、息子より先に玄関から出ていく**（設計書 6 節の 7、記憶 6 で振り返ると戸口に妻と息子がいる）。
            // 十三行目（マーク「行ってくる」）が出たら、台所の戸口を抜けて玄関へ歩き、開いた戸の外の段で止まる
            var husband = Cast(take, "Husband", new Vector3(-1.05f, 0f, 2.2f), 350f, 0);
            Leave(husband, 13);
            // **息子は初め階段の途中にいる。** 「ダニエル、七時のバスでしょ」と見上げて声を掛ける相手なので。
            // 記憶 13 でダニエルが呼ばれる所（階段の途中、手すり）と同じ。二階の上り口（y 2.4）に置いていた頃は、
            // 廊下のどこから見上げても天井に隠れて選べなかった。
            // 返事（六行目、ダニエル「分かってる」）が出たら降りてくる。〔区切り〕の後の会話は降り切ってから。
            // 夫が「鞄に入ってる」と返したら（十五行目）玄関へ歩き、靴を履いて戸を開ける
            var sonFrom = new Vector3(StairX, 1.85f, -3.3f);
            var sonTo = new Vector3(1.75f, 0f, -1.9f);
            var sonOut = new Vector3(-1.6f, 0f, -1.75f);
            var son = Cast(take, "Son", sonFrom, 180f, 0);
            Move(son, sonFrom, sonTo, 0.5f, 5f, true, true, 6);
            Then(son, sonOut, 0.5f, 3.5f, 15);
            Face(son, Toward(sonFrom, sonTo), 321f, Toward(sonTo, sonOut));
            // 〔区切り〕の行き先は台所の戸口の前。息子が降りてくるのを待つ
            Stop(0.2f, 0f, -0.5f);
            return new[]
            {
                K(0f,   0f,    0f,  0.25f, 182f,   4f, 1.58f),  // 戸口。出かけるところだった
                K(3f,   0f,    0f,  0.3f,  331f,   6f, 1.58f),  // 台所から夫の声
                K(7f,  -0.2f,  0f,  0.8f,  335f,  10f, 1.58f),  // 皿を手にした夫が棚を顎で指す
                K(11f, -0.8f,  0f,  1.6f,  320f, -18f, 1.58f),  // 吊り棚を開けて場所を教える
                K(15f,  0.6f,  0f, -0.6f,  154f, -30f, 1.58f),  // 二階から足音。廊下へ出て、階段の途中の息子を見上げる
                K(19f,  0.2f,  0f, -0.5f,  150f,   2f, 1.58f),  // 息子が降りてくる
                K(22f, -0.1f,  0f, -0.5f,  160f,   8f, 1.58f),  // 昼食の袋を渡す
                K(26f, -0.4f,  0f, -1.0f,  235f,   6f, 1.58f),  // ドアが開いて白い光。閉まる
                K(30f, -0.5f,  0f,  0.9f,  350f,  20f, 1.58f),  // 台所へ戻って蛇口を締める
            };
        }

        // ---- 7. 男 41『リー』 教室。35 秒 -----------------------------------------

        /// <summary>
        /// ホワイトボードから机の列へ歩き、ノートを覗いて、隣の居眠りを叩いてホワイトボードへ戻る。
        /// 覗き込む角度が深いので、生徒はつむじしか見えない
        /// </summary>
        static HostKey[] Lee(Transform take)
        {
            Cast(take, "Pupil", new Vector3(DeskX[2], 0f, DeskZ[1] - 0.56f), 0f, 6);
            // 隣の生徒は机に伏せて眠っている
            Aside(Cast(take, "Sleeper", new Vector3(DeskX[3], 0f, DeskZ[1] - 0.56f), 0f, 6));
            // 〔区切り〕の行き先はアイシャの机の前。ホワイトボードの前から「どこだ。見せてみろ」と返し、机の列を歩いて近づく
            Stop(0.8f, 0f, 0.9f);
            return new[]
            {
                K(0f,  0f,    0f,    4.1f,    0f, -12f, 1.70f),  // ホワイトボードにマーカーで書いている
                K(4f,  0f,    0f,    4.1f,  169f,   8f, 1.70f),  // 後ろの席から呼ばれて振り向く
                K(10f, 0.3f,  0f,    2.6f,  172f,   8f, 1.70f),  // 前から机の列を歩く
                K(16f, 0.65f, 0f,    1.2f,  176f,  16f, 1.70f),
                K(21f, 0.8f,  0f,    0.75f, 180f,  42f, 1.70f),  // ノートを覗く。つむじしか見えない
                K(25f, 1.5f,  0f,    0.75f, 135f,  40f, 1.70f),  // 隣の机を指の背で二度叩く
                K(28f, 1.5f,  0f,    0.8f,  135f,  24f, 1.70f),  // 起きる
                K(31f, 1.2f,  0f,    1.4f,  250f,  -8f, 1.70f),  // 窓の外が白い
                K(35f, 0f,    0f,    4.05f,   4f,  -6f, 1.70f),  // ホワイトボードへ戻ってマーカーを取り直す
            };
        }

        // ---- 8. 男 66『ジョルジョ』 団地の廊下。25 秒 -----------------------------

        /// <summary>
        /// 新聞を取りに出て、隣の母親が娘を抱き上げるのを眺めていたところで、居間の妻に呼ばれる。
        /// 新聞を持って中へ入り、居間まで行って妻に隣の親子の話をする。
        /// 記憶 1 と同じ朝の同じデッキを、隣の B の戸口の前から見ている
        /// </summary>
        static HostKey[] Giorgio(Transform take)
        {
            // **妻の声は居間から。** 戸口の奥で新聞を受け取らせていた頃は、記憶 16 の
            // 「エレナは肘掛け椅子に座ったまま、夫が新聞を持って居間へ入ってくる」と食い違っていた（設計書 6 節の 9、2026-09-26）。
            // 記憶 16 で主（エレナ）が座る南の肘掛け椅子に、テレビの方を向いて座らせる。
            // デッキの戸口の前からは見えないので、会話は新聞を持って居間まで入ってから（設計書 7 節の 9）。
            // 椅子の真ん中（記憶 16 の主の立ち位置、x 9.62）に置くと、座面の前の縁に浅く掛けて背から離れるので、12 cm 背の側へ寄せる
            Cast(take, "Wife", new Vector3(9.50f, EstateTop, -22.10f), 100f, 5);
            // 隣の母親は A の戸の前で娘を抱き上げているところ。記憶 1 でハンナが立っていた所。
            // こちらではなく西の娘を見ている
            Aside(Cast(take, "Mother", new Vector3(5.05f, EstateTop, EstateWalk - 0.35f), 270f, 1));
            // 隣は鍵を掛けて出てきたところなので、戸は閉まっている。自分の戸は開けて出てきた
            Shut(take, "ShutA", DoorA);
            Ajar(take, "AjarB", DoorB);
            return new[]
            {
                K(0f,  11.10f, EstateTop, EstateWalk - 0.05f, 268f,   6f, 1.65f),  // 戸口の前で隣を眺めている
                K(4f,  11.10f, EstateTop, EstateWalk - 0.05f, 268f,  12f, 1.65f),  // 母親が娘を抱き上げる
                K(8f,  11.12f, EstateTop, EstateWalk - 0.15f, 183f,  10f, 1.65f),  // 居間から妻の声。開いた戸の奥
                K(12f, 11.10f, EstateTop, HallSill - 0.10f,   183f,  14f, 1.65f),  // 新聞を持って中へ入る。テレビの音
                K(16f, RoomWalk, EstateTop, HallSill - 0.90f, 183f,  34f, 1.40f),  // 玄関で靴を脱ぐと視界が揺れる
                K(20f, RoomWalk, EstateTop, RoomEnd - 0.40f,  180f,  10f, 1.65f),  // 廊下を居間へ
                K(25f, 10.30f, EstateTop, -20.55f,            204f,  16f, 1.65f),  // 居間で妻に隣の親子の話をする
            };
        }

        // ---- 9. 女 72『ローザ』 公園の門。30 秒 -----------------------------------

        /// <summary>
        /// 隣を歩いていた孫息子（11 のルーカス）が鳩を追って池のほうへ駆け出す。名前を呼ぶと戻ってきて、餌の袋を持たせる。門を出る。
        /// 夫は門の柱に手を置いて振り返っているが、午後の日を背にしているので影になる
        /// </summary>
        static HostKey[] Rosa(Transform take)
        {
            // **孫息子は一度駆け出して、呼ばれて戻ってくる。** 手を引いていた幼い孫（3 歳）の頃は、
            // 池のほうへ走ったきり戻ってこず、手を取り直す所で誰もいなかった。
            // 隣（ローザの左、門へ向かう向きの西）から池の手前の鳩の群れへ駆け、
            // 呼ばれて歩いて戻り、ローザの前の西寄りで止まる。止まったらローザの方を向く（袋を受け取る相手をしている）。
            // **駆け出す線と戻る線は、別の行で動き出す**（設計書 7 節の「人も台詞で動き出す」）。
            // 駆け出すのは夫とのやりとりの中（三行目、アルベルト「ベンチに座りすぎたんだ」）、
            // 戻るのは返事（六行目、ルーカス「今行くって。鳩がすごいんだよ」）のあと。
            // 〔区切り〕の後の会話は、戻り切ってから
            var home = new Vector3(0.15f, 0f, 6.15f);
            var pond = new Vector3(-1.5f, 0f, 4.75f);
            var back = new Vector3(0.0f, 0f, 6.2f);
            var boy = Cast(take, "Grandson", home, 73f, 0);
            Move(boy, home, pond, 0.3f, 1.4f, true, true, 3);
            Then(boy, back, 0.5f, 3.0f, 6);
            Face(boy, Toward(home, pond), Toward(home, pond), 65f);
            // **餌の袋はローザの手から孫の手へ。** 「餌の袋持ってて」（七行目）が出たら、孫の右手に紙袋が現れる。
            // 主の手は描かないので、渡すところは見えない
            FeedBag(boy, true, 7, true);
            // 〔区切り〕の行き先は、夫の方を向いて立っていた所。孫が戻ってくるのを待つ
            Stop(0.6f, 0f, 6.35f);
            Cast(take, "Husband", new Vector3(-0.9f, 0f, 8.7f), 340f, 3);
            var girl = Cast(take, "Granddaughter", new Vector3(0.15f, 0f, 8.05f), 350f, 0);
            Move(girl, new Vector3(-0.9f, 0f, 6.6f), new Vector3(0.15f, 0f, 8.05f), 20f, 7f, true);
            // 鳩は駆け込んでくる孫に追われて飛び立つ
            Doves(take, new Vector3(-1.4f, 0.09f, 4.4f), 1.2f, 3);
            return new[]
            {
                K(0f,  0.6f,  0f, 6.4f, 327f,   6f, 1.50f),  // 前から夫の声
                K(4f,  0.6f,  0f, 6.3f, 327f,  10f, 1.48f),  // 膝が痛くて視界が揺れる
                K(8f,  0.7f,  0f, 6.2f, 230f,  12f, 1.50f),  // 隣を歩いていた孫が鳩を追って駆け出す
                K(12f, 0.7f,  0f, 6.2f, 234f,   8f, 1.50f),  // 名前を呼ぶ
                K(17f, 0.7f,  0f, 6.3f, 240f,  10f, 1.50f),  // 戻ってくる
                K(21f, 0.65f, 0f, 6.4f, 255f,  30f, 1.50f),  // 餌の袋を持たせる
                K(25f, 0.6f,  0f, 7.0f, 322f,   6f, 1.50f),  // 孫娘が夫の腕を取り直す
                K(30f, 0.45f, 0f, 8.4f, 352f,   4f, 1.50f),  // 門を出る
            };
        }

        // ---- 10. 男 11『ルーカス』 同じ公園。25 秒 ---------------------------------

        /// <summary>
        /// 鳩を追いかけて池の縁まで来ていたところを祖母に呼ばれる。祖母は門のほうに立って、こちらを向いて呼んでいる。
        /// 戻って餌の袋を渡され、撒くと鳩がまた寄ってくる。目は 11 歳の高さ（1.32 m）で、祖母とはほぼ目が合う
        /// </summary>
        static HostKey[] Lucas(Transform take)
        {
            // 祖母は呼んでいる相手（池の縁のルーカスと、袋を渡しに戻ってくる所）の方を向く。
            // 餌の袋を右手に提げていて、「餌の袋持ってて」（三行目）が出たら手から消える（主の手へ渡った）
            var gran = Cast(take, "Grandmother", new Vector3(0.95f, 0f, 6.3f), 240f, 0);
            FeedBag(gran, true, 3, false);
            Cast(take, "Grandfather", new Vector3(-0.4f, 0f, 8.6f), 355f, 3);
            // **鳩は追いかけられて飛び立ち、門の前で撒くと足元へ寄ってくる**（設計書 6 節の 11）。
            // 寄ってくるのは区切りの後の最初の行（六行目、ルーカス「うわっ！　全部こっち来た」）が出てから
            var gate = new Vector3(0.1f, 0f, 8.1f);
            var flock = Doves(take, new Vector3(-1.5f, 0.09f, 4.5f), 2f);
            Gather(flock, gate + new Vector3(-0.2f, 0.09f, -0.1f), 6);
            // 〔区切り〕の行き先。一つ目は祖母のそば、二つ目は門の前
            Stop(0.3f, 0f, 5.95f);
            Stop(gate.x, gate.y, gate.z);
            return new[]
            {
                K(0f,  -1.6f, 0f, 4.7f, 250f,  34f, 1.32f),  // 鳩を追って池の縁まで来ていた
                K(3f,  -1.7f, 0f, 4.6f, 240f,  30f, 1.32f),
                K(6f,  -1.6f, 0f, 4.7f,  58f,  -3f, 1.32f),  // 振り向くと、祖母が門のほうで呼んでいる
                K(11f, -0.6f, 0f, 5.4f,  56f,   0f, 1.32f),  // 戻る
                K(15f,  0.3f, 0f, 5.95f, 62f,  30f, 1.32f),  // 餌の袋を渡される
                K(19f,  0.3f, 0f, 5.95f, 345f, -5f, 1.32f),  // 門の前に祖父と妹
                K(22f, gate.x, 0f, gate.z, 250f, 40f, 1.32f), // 門の前で袋から撒くと、鳩がまた足元に寄ってくる
                K(25f, gate.x, 0f, gate.z, 250f, 50f, 1.32f),
            };
        }

        // ---- 11. 女 18『プリヤ』 同じ電車。25 秒 -----------------------------------

        /// <summary>
        /// 先輩の背中に声を掛けて本を差し出したところ。振り向いた先輩は車内の灯りを背にする。
        ///
        /// **先輩は背中から始めて、名を返すところで振り向く。** 20° のまま据えていた頃は、
        /// 会話のあいだもずっとプリヤに背を向けていて、「エミリーが逆方向を向いている」と差し戻された。
        /// 一行目（「プリヤ？」）が出てから <see cref="SeniorTurnsAfter"/> 秒で、プリヤの立つ所（鍵打ちの頭）の方へ向き直る
        /// </summary>
        static HostKey[] Priya(Transform take)
        {
            var senior = Cast(take, "Senior", new Vector3(0.2f, 0f, 0.6f), 20f, 1);
            Turn(senior, Toward(senior.localPosition, new Vector3(0.15f, 0f, -0.9f)), SeniorTurnsAfter, 1);
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
            // **母は台所の戸口から、階段を降りてくるダニエルの方を向く。** 昼食の袋を渡す相手なので（設計書 6 節の 13）。
            // 300° では台所の奥を向き、ダニエルに背を向けていた。階段の下（8 秒）と袋を受け取る所（12 秒）のあいだへ向ける
            Cast(take, "Mother", new Vector3(0.1f, 0f, 0.2f), 150f, 1);
            // **父は「行ってくる」で、息子より先に玄関から出ていく**（設計書 6 節の 13）。
            // 九行目（マーク「行ってくる」）が出たら、台所の戸口を抜けて玄関へ歩き、開いた戸の外の段で止まる
            var father = Cast(take, "Father", new Vector3(-1.05f, 0f, 2.2f), 350f, 0);
            Leave(father, 9);
            // 〔区切り〕の行き先。一つ目は階段の下（階段を降りる）、二つ目は玄関（靴を履く）
            Stop(1.5f, 0f, -1.4f);
            Stop(-1.75f, 0f, -1.9f);
            // 通りすがり。玄関の外の踊り場で待っている同級生（記憶 13 のアイシャ）。
            // 台所から教室へ出る口はここ一つ（設計書 6 節）。
            // 設計書の「台所の窓の外」は、窓の外が朝の光を塗った板で、その先に何も無いので立たせられない。
            // 開いたままの玄関の戸の先で、白く飛ぶ外の光の前に立つ。
            // **外（通りの側）を向かせ、背中と鞄をダニエルに見せる。** 家の方を向かせると、
            // 戸口の脇の灯り（HallGlow、0.55 m 先）が正面から当たって顔の造りまで見えた。
            // 誰の相手もしていない人なので、向きで隠してよい（この関数群の頭の決まり）
            var mate = Cast(take, "Classmate", new Vector3(-3.15f, 0f, -1.95f), 270f, 0);
            Aside(mate);
            Satchel(mate);
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
            // **先生は机の間の通路を歩いてくる。** ホワイトボードの前から一直線に結んでいた頃は、アイシャの前の列の机と椅子を
            // 突き抜けて歩いていた。ホワイトボードの前から通路（x 0 の列の間）を下り、アイシャの机の向こう側へ横に入る。
            // 教壇は置かないので、歩き始めも床の高さ
            // 止まる所は机の奥の縁（z 0.62）から 20 cm 先。机に体が掛からない
            // **歩き出すのは名を呼んでから**（設計書 7 節の「人も台詞で動き出す」）。一行目（リー「アイシャ、どこだ。見せてみろ」）が
            // 出て 1 秒で歩き出す。会話は先生が歩き終えてから（DiveDirector は歩いている相手と話し始めない）
            var aisle = new Vector3(0.1f, 0f, 1.0f);
            var teacher = Cast(take, "Teacher", new Vector3(0.9f, 0f, 0.82f), 220f, 0);
            Move(teacher, new Vector3(0.45f, 0f, 3.9f), aisle, 1.0f, 6.5f, true, true, 1);
            Then(teacher, new Vector3(0.9f, 0f, 0.82f), 7.5f, 2.5f);
            Face(teacher, Toward(new Vector3(0.45f, 0f, 3.9f), aisle), 187f, 220f);
            // 隣（マテオ）は机に伏せて眠っている
            Aside(Cast(take, "Neighbour", new Vector3(DeskX[3], 0f, DeskZ[1] - 0.56f), 355f, 6));
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
        /// 机に伏せていて、目を開けると眩しい。遠くがぼやける体なのでホワイトボードの字は読めない。
        /// 目の高さが伏せているあいだだけ机の高さまで落ちる
        /// </summary>
        static HostKey[] Mateo(Transform take)
        {
            Cast(take, "Neighbour", new Vector3(DeskX[2], 0f, DeskZ[1] - 0.56f), 6f, 6);
            // 先生はマテオの机の向こう側に立つ。机の奥の縁から 33 cm、前の列の椅子から 31 cm。
            // z を 1.2 先に置いていた頃は、前の列の椅子の中に立っていた
            Cast(take, "Teacher", new Vector3(DeskX[3], 0f, DeskZ[1] + 0.55f), 215f, 2);
            var seat = new Vector3(DeskX[3], 0f, DeskZ[1] - 0.56f);
            return new[]
            {
                K(0f,  seat.x, 0f, seat.z,   0f,  62f, 0.82f),  // 机に伏せている
                K(3f,  seat.x, 0f, seat.z,   2f,  40f, 1.05f),  // 目を開けると眩しい
                K(7f,  seat.x, 0f, seat.z,   0f,   4f, 1.20f),  // 先生が机の前に立っている
                K(11f, seat.x, 0f, seat.z, 272f,  12f, 1.20f),  // 隣が肘でつつく
                K(15f, seat.x, 0f, seat.z,   6f, -12f, 1.20f),  // ホワイトボードの字が読めない
                K(19f, seat.x, 0f, seat.z, 268f, -14f, 1.20f),  // 窓の外が白い
                K(25f, seat.x, 0f, seat.z,   2f,  60f, 0.82f),  // また伏せる
            };
        }

        // ---- 15. 女 63『エレナ』 団地の部屋。25 秒 ---------------------------------

        /// <summary>
        /// 居間の肘掛け椅子に座ったまま。夫が新聞を持って玄関から廊下を歩いてきて、隣の椅子の前に立つ。
        /// 部屋で動くのはテレビの光と、入ってくる夫と、開いた玄関の外のデッキを通る老女だけ
        /// </summary>
        static HostKey[] Elena(Transform take)
        {
            // **夫は名を呼んだらすぐ、玄関から居間へ歩いてくる。** 玄関で止めておいて
            // 二行目（エレナ「なあに」）で歩き出す形にしていたが、居間の椅子からは
            // 玄関が台所との仕切りの端と階段の手すり壁の陰になり、座ったままでは夫を選べなかった。
            // 合図を一行目（ジョルジョ「エレナ」、記憶に入った瞬間に出る）に掛け、
            // 呼んで 1.5 秒で歩き出す。一行目が出ている 9 秒のうちに居間へ入ってくるので、
            // 声の方へ振り向いた先で選べる。
            // **歩く線は廊下の真ん中の一本。** Mover は始まりと終わりを一直線に結ぶだけなので、
            // 玄関の足拭きの西寄り（小卓の南）から、廊下の口を抜けて北の肘掛け椅子の前まで、
            // x を動かさずに結ぶ。台所との仕切りの端からも居間の戸の板からも 0.5 m 離れる
            var stood = new Vector3(10.30f, EstateTop, -20.55f);
            var man = Cast(take, "Husband", stood, 195f, 0);
            Move(man, new Vector3(RoomWalk, EstateTop, EstateFace - 0.70f), stood, 1.5f, 5.5f, true, true, 1);

            // **通りすがり。買い物袋を提げた老女（記憶 9 のローザ）が、開いた玄関の外のデッキを通る。**
            // 設計書の「居間の窓の下」は、居間の窓の下半分（床から 1.3 m まで）がレースで覆われていて、
            // 座った目（1.15 m）からは窓の下の地面が見えない。棟の南の地面も見えない仕切りの外になる。
            // そこで、居間の椅子から廊下を真っ直ぐ抜けて開いた玄関の先に見えるデッキへ置く。
            // 西（階段の側）から歩いてきて、B の戸口の前で止まり、袋を持ち直す。
            // **見通しは細い。** 南の椅子から見えるのは、廊下の口の角（x 9.8）と階段の手すり壁（x 10.8）の
            // あいだを抜けて玄関の穴に入る、デッキの上で x 10.75〜11.25 の帯だけ。戸口の真ん中（11.15）に
            // 立たせると体の東半分が手すり壁に隠れるので、体の芯が帯の真ん中寄りに来る x 10.95 で止める。
            // 会話とは関わらないので記憶の時計で動く
            var shopStop = new Vector3(DoorB - 0.20f, EstateTop, EstateWalk + 0.10f);
            var shopper = Cast(take, "Shopper", shopStop, 90f, 0);
            Aside(shopper);
            ShopBags(shopper);
            Move(shopper, new Vector3(6.00f, EstateTop, EstateWalk + 0.20f), shopStop, 8f, 7f, true);

            // 隣は鍵を掛けて出ていったあと。戸は閉まっている。夫は玄関を開けたまま入ってきた
            Shut(take, "ShutA", DoorA);
            Ajar(take, "AjarB", DoorB);
            return new[]
            {
                // 南の肘掛け椅子。暖炉とテレビを向いて並ぶ二つのうちの奥。北の椅子は夫が座る
                K(0f,  9.62f, EstateTop, -22.10f, 102f,   8f, 1.15f),  // テレビの前
                K(3f,  9.62f, EstateTop, -22.10f,  12f,   2f, 1.15f),  // 玄関から夫の声。廊下の先
                K(9f,  9.62f, EstateTop, -22.10f,  20f,   0f, 1.15f),  // 新聞を持って入ってくる
                K(14f, 9.62f, EstateTop, -22.10f,  30f,   8f, 1.15f),  // 隣の椅子の前に立つ
                K(18f, 9.62f, EstateTop, -22.10f,  25f,  16f, 1.15f),  // 新聞を広げる音
                K(25f, 9.62f, EstateTop, -22.10f, 102f,  26f, 1.15f),  // テレビの光が床に当たっている
            };
        }

        /// <summary>
        /// 買い物袋を二つ、両手に提げさせる。白いレジ袋。
        /// 手の骨（Humanoid の手。Rocketbox では手首の関節）に付けるので、立ちと歩きの動きで手が揺れると袋も付いてくる。
        ///
        /// **持ち手の上端を握った指の高さ（手首から 8 cm 下）に、袋の内側の面を腿の外側の面の 1 cm 外に置く。**
        /// 手首から決めた長さだけ下げていた頃は、持ち手が指先より 10 cm ほど下に離れ、袋がスカートに食い込んだ。
        /// 腿の外側の面は、袋の高さの皮のうち手より内の頂点から測る。
        /// 人の根の子として模型の後ろに置くので、光線の狙い（最初のレンダラーの広がり）には入らない
        /// </summary>
        static void ShopBags(Transform who)
        {
            var k = FigureSpan(who);
            var skin = FigureSkin(who);
            for (var i = 0; i < 2; i++)
            {
                var side = i == 0 ? "L" : "R";
                var hand = i == 0 ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand;
                var wrist = FigureAt(who, hand);
                var sign = wrist.x < 0f ? -1f : 1f;
                var grip = wrist.y - 0.08f * k;
                var top = grip - 0.08f * k;
                var bottom = top - 0.30f * k;
                var thigh = 0f;
                foreach (var p in skin)
                {
                    if (p.y < bottom || p.y > top || Mathf.Abs(p.z - wrist.z) > 0.16f * k) continue;
                    var out1 = p.x * sign;
                    if (out1 <= 0f || out1 > Mathf.Abs(wrist.x) - 0.03f) continue;
                    thigh = Mathf.Max(thigh, out1);
                }
                var x = sign * (thigh + 0.01f + 0.05f * k);
                var handle = (x + wrist.x) * 0.5f;
                var mesh = Shape("ShopBag" + side + Stamp(new Vector3(x, grip, wrist.z)) + Mathf.RoundToInt(k * 100f), 0.5f, b =>
                {
                    b.Box(new Vector3(x, (top + bottom) * 0.5f, wrist.z + 0.01f), new Vector3(0.10f, 0.30f, 0.28f) * k);
                    // 持ち手。袋の口から握った指まで
                    b.Box(new Vector3(handle, (grip + top) * 0.5f, wrist.z + 0.01f), new Vector3(0.02f, grip - top + 0.01f, 0.12f * k));
                });
                FigureAttach(who, hand, "Bag" + side, mesh,
                    AssetDatabase.LoadAssetAtPath<Material>(Materials + "EstateFrame.mat"));
            }
        }

        /// <summary>
        /// 白いイヤホンを両耳に、線を胸まで。頭の骨に付ける。
        /// 耳の置き場は両目の骨から決める（目の高さの 2 cm 下、9 cm 後ろ、真ん中から 7.5 cm 外）。
        /// 頭の皮の広がりの上端から測っていた頃は、プリヤの髪の上端から下げたので、耳より高い髪の中に浮いた。
        /// 遠目には点にしかならないが、近づいて見たときに「聞いている人」だと読める
        /// </summary>
        static void Earphones(Transform who)
        {
            var k = FigureSpan(who);
            var le = FigureBone(who, HumanBodyBones.LeftEye);
            var re = FigureBone(who, HumanBodyBones.RightEye);
            Vector3 eyes;
            if (le != null && re != null) eyes = (FigureAt(who, HumanBodyBones.LeftEye) + FigureAt(who, HumanBodyBones.RightEye)) * 0.5f;
            else eyes = FigureAt(who, HumanBodyBones.Head) + new Vector3(0f, 0.08f, 0.08f) * k;
            var ear = eyes.y - 0.02f * k;
            var mid = eyes.z - 0.09f * k;
            var cx = eyes.x;
            var half = 0.075f * k;
            var mesh = Shape("Earphones" + Stamp(new Vector3(cx, ear, mid)) + Mathf.RoundToInt(half * 1000f), 0.5f, b =>
            {
                for (var i = 0; i < 2; i++)
                {
                    var x = cx + (i == 0 ? -half : half);
                    b.Box(new Vector3(x, ear, mid), new Vector3(0.03f, 0.03f, 0.03f) * k);
                    b.Box(new Vector3(cx + (x - cx) * 0.6f, ear - 0.20f * k, mid + 0.05f * k), new Vector3(0.008f, 0.40f * k, 0.008f));
                }
            });
            FigureAttach(who, HumanBodyBones.Head, "Earphones", mesh,
                AssetDatabase.LoadAssetAtPath<Material>(Materials + "EstateFrame.mat"));
        }

        /// <summary>
        /// 学校の鞄。背中に一つ。肩紐は付けない。上の胸の骨（Rocketbox の Spine2。肩甲骨の高さ）に付けて、背中の皮から少し離す
        /// </summary>
        static void Satchel(Transform who)
        {
            var k = FigureSpan(who);
            var chest = FigureAt(who, HumanBodyBones.UpperChest);
            var back = FigureBack(who, chest.y);
            var at = new Vector3(chest.x, chest.y - 0.12f * k, back - 0.065f * k);
            var mesh = Shape("Satchel" + Stamp(at) + Mathf.RoundToInt(k * 100f), 0.5f, b =>
                b.Box(at, new Vector3(0.30f, 0.36f, 0.12f) * k));
            FigureAttach(who, HumanBodyBones.UpperChest, "Satchel", mesh, Mat("Cloth"));
        }

        /// <summary>模型ごとの縮尺。持ち物の大きさを背丈に合わせる。大人の模型の背（1.80 m）に対する比</summary>
        static float FigureSpan(Transform who)
        {
            var box = FigureExtent(who, who.Find("Figure") != null ? who.Find("Figure").gameObject : who.gameObject);
            return Mathf.Clamp(box.size.y / 1.80f, 0.4f, 1.2f);
        }

        /// <summary>高さ y あたりの背中の面。体の皮の、背骨の線の近く（横 0.12 m の内）でいちばん後ろの頂点</summary>
        static float FigureBack(Transform who, float y)
        {
            var back = 0f;
            var first = true;
            foreach (var p in FigureSkin(who))
            {
                if (Mathf.Abs(p.y - y) > 0.10f || Mathf.Abs(p.x) > 0.12f) continue;
                if (first || p.z < back) { back = p.z; first = false; }
            }
            return first ? -0.15f : back;
        }

        /// <summary>形の名前に位置を刻む。人ごとに寸法が違うので、同じ名前で別の形を引かないように</summary>
        static string Stamp(Vector3 at)
        {
            return "_" + Mathf.RoundToInt(at.x * 100f) + "_" + Mathf.RoundToInt(at.y * 100f) + "_" + Mathf.RoundToInt(at.z * 100f);
        }
    }
}
