using System.Globalization;
using System.Text;
using UnityEditor;
using UnityEngine;
using HalfAware.EditorTools.Rocketbox;

namespace HalfAware.EditorTools
{
    public static partial class PlaceProtagonist
    {
        // ---- 場面 1 の、椅子の右の卓に置いたジャケット ------------------------------
        //
        // 場面 1 は着ていない形で始まり、煙草を吸い終えた後、座ったまま右の卓のジャケットを調べて着る（RoomIntroDirector）。
        // 着ると立ち上がれる（SceneFlow の standAfter）。メモリハブ・端末・メモ・ドアは着た後。
        // 卓の、メモリハブより手前（机の側）には明かり（Room/Lamp）が置いてあった。そこをジャケットの置き場にするので、明かりは切る
        // （Light を持たない置物なので、部屋の明るさは変わらない）。
        // ジャケットは前を上にして寝かせ、襟を卓の奥へ向けて身頃の上の半分を天板に載せ、下の半分と両の袖を天板の椅子の側の縁から垂らす（RocketboxJacketOff.MakeFolded）
        //
        // 場面 3 と 5 は場面 1 から写して組む。卓のジャケットは場面 3 の組み立てが消し、玄関先のコートハンガーに掛けたジャケットを置く

        /// <summary>卓に置いたジャケット（Room の子）の名前</summary>
        public const string FoldedName = "Jacket";
        /// <summary>卓の明かり（Room の子）。ジャケットに場所を譲って切る</summary>
        public const string LampName = "Lamp";
        /// <summary>卓（Room の子）</summary>
        const string TableName = "SideTable";
        /// <summary>卓のメモリハブ（Room の子）。ジャケットはこれに重ねない</summary>
        const string HubName = "MemoryHub";
        /// <summary>前の置き場（左の肘掛け）に掛けていたジャケット（椅子の子）の名前。残っていたら消す</summary>
        public const string OldDrapedName = "Jacket";
        /// <summary>ジャケットの調べる対象の名前（Interactables の子）</summary>
        public const string JacketItemName = "Interactable_" + RoomIds.Jacket;
        /// <summary>着る音</summary>
        public const string JacketOnPath = "Assets/Audio/JacketOn.wav";
        /// <summary>
        /// 調べる対象の半径。座った目から卓の椅子の側の縁まで 1.0 m ほど。
        /// 立ち上がる前にしか用が無いので、部屋の向こうから拾わせない
        /// </summary>
        const float JacketRadius = 1.5f;
        /// <summary>
        /// 置く向き（度、y まわり）。-90 で、垂らす側を椅子の側（-x）へ、襟を卓の奥（壁の側、+x）へ向ける。
        /// 座って右を見下ろすと、垂れた身頃の前が椅子の方を向き、天板の上の襟は奥に見える
        /// </summary>
        const float FoldedYaw = -90f;
        /// <summary>天板に載せる丈の上限（m）。襟から天板の椅子の側の縁まで。残り（裾と袖口の側）は縁から垂れる</summary>
        const float OnTopMax = 0.43f;
        /// <summary>卓の縁とメモリハブから空ける幅（m）</summary>
        const float Margin = 0.01f;

        static bool WorldBounds(Transform t, out Bounds b)
        {
            b = new Bounds();
            var first = true;
            foreach (var r in t.GetComponentsInChildren<Renderer>(true))
            {
                if (first) { b = r.bounds; first = false; }
                else b.Encapsulate(r.bounds);
            }
            return !first;
        }

        /// <summary>
        /// 場面 1: 椅子の右の卓に置いたジャケット（見た目）と、それを調べる対象を置き、着る流れを繋ぐ。
        /// 体のジャケットは脱いだ形で始める。調べる順は ジャック → 煙草 → ジャケット → そのほか（灰皿と箱は煙草の後から、座ったまま調べられる）
        /// </summary>
        static bool Jacket(GameObject her, Transform chair, SceneFlow flow, StringBuilder note)
        {
            var who = BuildRocketboxProtagonist.Chosen;
            var garment = her.GetComponentInChildren<Garment>(true);
            if (garment == null) { note.AppendLine("体にジャケットが無い"); return false; }
            var room = GameObject.Find("Room");
            if (room == null) { note.AppendLine("Room が無い"); return false; }
            var table = room.transform.Find(TableName);
            var hub = room.transform.Find(HubName);
            Bounds tb, hb;
            if (table == null || !WorldBounds(table, out tb)) { note.AppendLine("卓（Room/" + TableName + "）が無い"); return false; }
            if (hub == null || !WorldBounds(hub, out hb)) { note.AppendLine("メモリハブ（Room/" + HubName + "）が無い"); return false; }

            // 前の置き場（左の肘掛け）のジャケットは消す
            var old = chair.Find(OldDrapedName);
            if (old != null) Object.DestroyImmediate(old.gameObject);
            // 明かりは切る
            var lamp = room.transform.Find(LampName);
            if (lamp != null) lamp.gameObject.SetActive(false);
            else note.AppendLine("明かり（Room/" + LampName + "）が無い");

            // 卓のジャケット。天板の椅子の側の縁（卓のいちばん手前。天板が脚と引き出しより張り出している）の、メモリハブと卓の机の側の縁の間の真ん中から垂らす
            var rot = Quaternion.Euler(0f, FoldedYaw, 0f);
            float z0 = hb.max.z + Margin, z1 = tb.max.z - Margin;
            var onTop = Mathf.Min(OnTopMax, tb.max.x - Margin - tb.min.x);
            Vector3 size;
            string foldNote;
            var mesh = RocketboxJacketOff.MakeFolded(who, onTop, out size, out foldNote);
            note.AppendLine(foldNote);
            var at = new Vector3(tb.min.x, tb.max.y, (z0 + z1) * 0.5f);
            if (size.x > z1 - z0)
                note.AppendFormat(CultureInfo.InvariantCulture, "（思いがけない）卓のジャケットの幅 {0:0.000} m が、メモリハブと卓の縁の間 {1:0.000} m に収まらない", size.x, z1 - z0).AppendLine();
            var folded = room.transform.Find(FoldedName);
            if (folded == null)
            {
                folded = new GameObject(FoldedName).transform;
                folded.SetParent(room.transform, false);
            }
            folded.position = at;
            folded.rotation = rot;
            folded.localScale = Vector3.one;
            var mf = folded.GetComponent<MeshFilter>();
            if (mf == null) mf = folded.gameObject.AddComponent<MeshFilter>();
            mf.sharedMesh = mesh;
            var mr = folded.GetComponent<MeshRenderer>();
            if (mr == null) mr = folded.gameObject.AddComponent<MeshRenderer>();
            mr.sharedMaterials = JacketMaterials(who);
            folded.gameObject.SetActive(true);
            note.AppendFormat(CultureInfo.InvariantCulture, "卓のジャケット: 縁 {0}、z {1:0.000}〜{2:0.000}（メモリハブの縁 z {3:0.000}、卓の机の側の縁 z {4:0.000}）、天板の上 x {5:0.000}〜{6:0.000}、垂れた裾の下の縁 y {7:0.000}",
                at.ToString("F3"), at.z - size.x * 0.5f, at.z + size.x * 0.5f, hb.max.z, tb.max.z, at.x, at.x + onTop, at.y - size.y).AppendLine();
            // 体のジャケットは脱いだ形で始める
            garment.Worn = false;
            EditorUtility.SetDirty(garment);

            // 調べる対象。天板の縁の角の上。メモリハブのチップより目に近くしておく
            var script = AssetDatabase.LoadAssetAtPath<RoomScript>(RoomText.ScriptPath);
            var parent = GameObject.Find("Interactables");
            if (parent == null) { note.AppendLine("Interactables が無い"); return false; }
            var item = parent.transform.Find(JacketItemName);
            if (item == null)
            {
                item = new GameObject(JacketItemName).transform;
                item.SetParent(parent.transform, false);
            }
            item.position = at + Vector3.up * 0.03f;
            var it = item.GetComponent<Interactable>();
            if (it == null) it = item.gameObject.AddComponent<Interactable>();
            var so = new SerializedObject(it);
            so.FindProperty("id").stringValue = RoomIds.Jacket;
            so.FindProperty("script").objectReferenceValue = script;
            so.FindProperty("radius").floatValue = JacketRadius;
            so.FindProperty("required").boolValue = true;
            so.FindProperty("once").boolValue = true;
            After(so, RoomIds.Cigarette);
            so.ApplyModifiedPropertiesWithoutUndo();
            note.AppendFormat("ジャケットの調べる対象: {0}", item.position.ToString("F3")).AppendLine();

            // 調べる順: 歩いて調べる物は着た後。ドアは前提の先頭に文を持たない id（ジャケット）を置いて、着るまで弾く
            foreach (var other in Object.FindObjectsByType<Interactable>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                string[] chain = null;
                switch (other.Id)
                {
                    case RoomIds.Chips:
                    case RoomIds.Terminal:
                    case RoomIds.Clipboard:
                        chain = new[] { RoomIds.Jacket };
                        break;
                    case RoomIds.Door:
                        chain = new[] { RoomIds.Jacket, RoomIds.Chips, RoomIds.Terminal };
                        break;
                }
                if (chain == null) continue;
                var oso = new SerializedObject(other);
                After(oso, chain);
                oso.ApplyModifiedPropertiesWithoutUndo();
            }

            // 立ち上がるのは着た後
            if (flow != null)
            {
                var fso = new SerializedObject(flow);
                fso.FindProperty("standAfter").stringValue = RoomIds.StandAfter;
                fso.ApplyModifiedPropertiesWithoutUndo();
            }

            // 着る流れ。音は煙草の息と同じ口元の音源で鳴らす
            var intro = Object.FindFirstObjectByType<RoomIntroDirector>(FindObjectsInactive.Include);
            if (intro == null) { note.AppendLine("RoomIntroDirector が無い"); return false; }
            var iso = new SerializedObject(intro);
            iso.FindProperty("jacketId").stringValue = RoomIds.Jacket;
            iso.FindProperty("garment").objectReferenceValue = garment;
            iso.FindProperty("folded").objectReferenceValue = folded.gameObject;
            var voice = GameObject.Find("Player/Main Camera/Voice");
            iso.FindProperty("voice").objectReferenceValue = voice != null ? voice.GetComponent<AudioSource>() : null;
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(JacketOnPath);
            if (clip == null) note.AppendLine("着る音が無い: " + JacketOnPath);
            iso.FindProperty("jacketOn").objectReferenceValue = clip;
            iso.ApplyModifiedPropertiesWithoutUndo();
            if (voice == null) note.AppendLine("口元の音源（Player/Main Camera/Voice）が無い");
            return true;
        }

        /// <summary>脱いだジャケットのマテリアル（着ているジャケットと同じ、革・金具・裏地・ジッパーの歯）</summary>
        public static Material[] JacketMaterials(RocketboxPerson who)
        {
            return new[]
            {
                AssetDatabase.LoadAssetAtPath<Material>(RocketboxJacket.LeatherPath(who)),
                AssetDatabase.LoadAssetAtPath<Material>(RocketboxJacket.MetalPath(who)),
                AssetDatabase.LoadAssetAtPath<Material>(RocketboxJacket.LiningPath(who)),
                AssetDatabase.LoadAssetAtPath<Material>(RocketboxJacket.TeethPath(who)),
            };
        }

        static void After(SerializedObject so, params string[] ids)
        {
            var chain = so.FindProperty("after");
            chain.arraySize = ids.Length;
            for (var i = 0; i < ids.Length; i++) chain.GetArrayElementAtIndex(i).stringValue = ids[i];
        }
    }
}
