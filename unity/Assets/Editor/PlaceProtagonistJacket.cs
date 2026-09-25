using System.Text;
using UnityEditor;
using UnityEngine;
using HalfAware.EditorTools.Rocketbox;

namespace HalfAware.EditorTools
{
    public static partial class PlaceProtagonist
    {
        // ---- 場面 1 の、左の肘掛けに掛けたジャケット ------------------------------
        //
        // 場面 1 は着ていない形で始まり、煙草を吸い終えた後、座ったまま左の肘掛けのジャケットを調べて着る（RoomIntroDirector）。
        // 着ると立ち上がれる（SceneFlow の standAfter）。メモリハブ・端末・メモ・ドアは着た後
        //
        // 場面 3 と 5 は場面 1 から写して組むので、掛けたジャケットと体のジャケットもそのまま渡る（着る・脱ぐの頭の形は、それぞれの組み立てが決める）

        /// <summary>掛けたジャケットの置き場（椅子の子）の名前</summary>
        public const string DrapedName = "Jacket";
        /// <summary>ジャケットの調べる対象の名前（Interactables の子）</summary>
        public const string JacketItemName = "Interactable_" + RoomIds.Jacket;
        /// <summary>着る音</summary>
        public const string JacketOnPath = "Assets/Audio/JacketOn.wav";
        /// <summary>
        /// 調べる対象の半径。座った目から肘掛けまで 0.7 m ほど。
        /// 立ち上がる前にしか用が無いので、部屋の向こうから拾わせない
        /// </summary>
        const float JacketRadius = 1.2f;
        /// <summary>
        /// 掛けたジャケットの後ろの端（折り目）を置く z（椅子から見た位置）。幅は前へ 22 cm ほど並び、前寄りの袖の上に左の手が載る。
        /// 肘掛けの後ろ寄りは座った目から肩に隠れるので、手の下まで前へ出してある
        /// </summary>
        const float DrapedBack = 0.0f;

        /// <summary>椅子の左の肘掛けの形を、肘掛けと座面の見た目の大きさ（椅子から見た位置）から読む</summary>
        public static RocketboxJacketDrape.Armrest LeftArmrest(Transform chair)
        {
            var arm = new RocketboxJacketDrape.Armrest { inner = -0.248f, outer = -0.324f, top = 0.676f, seat = 0.549f, back = DrapedBack };
            var pad = chair.Find("ArmPadL");
            var seat = chair.Find("SeatPad");
            Bounds b;
            if (pad != null && LocalBounds(chair, pad, out b)) { arm.inner = b.max.x; arm.outer = b.min.x; arm.top = b.max.y; }
            if (seat != null && LocalBounds(chair, seat, out b)) arm.seat = b.max.y;
            return arm;
        }

        static bool LocalBounds(Transform frame, Transform t, out Bounds local)
        {
            local = new Bounds();
            var r = t.GetComponent<Renderer>();
            if (r == null) return false;
            var w = r.bounds;
            var first = true;
            for (var i = 0; i < 8; i++)
            {
                var c = new Vector3((i & 1) == 0 ? w.min.x : w.max.x, (i & 2) == 0 ? w.min.y : w.max.y, (i & 4) == 0 ? w.min.z : w.max.z);
                var p = frame.InverseTransformPoint(c);
                if (first) { local = new Bounds(p, Vector3.zero); first = false; }
                else local.Encapsulate(p);
            }
            return true;
        }

        /// <summary>
        /// 場面 1: 左の肘掛けに掛けたジャケット（見た目）と、それを調べる対象を置き、着る流れを繋ぐ。
        /// 体のジャケットは脱いだ形で始める。調べる順は ジャック → 煙草 → ジャケット → そのほか（灰皿と箱は煙草の後から、座ったまま調べられる）
        /// </summary>
        static bool Jacket(GameObject her, Transform chair, SceneFlow flow, StringBuilder note)
        {
            var who = BuildRocketboxProtagonist.Chosen;
            var garment = her.GetComponentInChildren<Garment>(true);
            if (garment == null) { note.AppendLine("体にジャケットが無い"); return false; }

            // 掛けたジャケット
            Vector3 pick;
            string drapeNote;
            var mesh = RocketboxJacketDrape.Make(who, LeftArmrest(chair), out pick, out drapeNote);
            note.AppendLine(drapeNote);
            var draped = chair.Find(DrapedName);
            if (draped == null)
            {
                draped = new GameObject(DrapedName).transform;
                draped.SetParent(chair, false);
            }
            draped.localPosition = Vector3.zero;
            draped.localRotation = Quaternion.identity;
            draped.localScale = Vector3.one;
            var mf = draped.GetComponent<MeshFilter>();
            if (mf == null) mf = draped.gameObject.AddComponent<MeshFilter>();
            mf.sharedMesh = mesh;
            var mr = draped.GetComponent<MeshRenderer>();
            if (mr == null) mr = draped.gameObject.AddComponent<MeshRenderer>();
            mr.sharedMaterials = new[]
            {
                AssetDatabase.LoadAssetAtPath<Material>(RocketboxJacket.LeatherPath(who)),
                AssetDatabase.LoadAssetAtPath<Material>(RocketboxJacket.MetalPath(who)),
                AssetDatabase.LoadAssetAtPath<Material>(RocketboxJacket.LiningPath(who)),
                AssetDatabase.LoadAssetAtPath<Material>(RocketboxJacket.TeethPath(who)),
            };
            draped.gameObject.SetActive(true);
            // 体のジャケットは脱いだ形で始める
            garment.Worn = false;
            EditorUtility.SetDirty(garment);

            // 調べる対象
            var script = AssetDatabase.LoadAssetAtPath<RoomScript>(RoomText.ScriptPath);
            var parent = GameObject.Find("Interactables");
            if (parent == null) { note.AppendLine("Interactables が無い"); return false; }
            var item = parent.transform.Find(JacketItemName);
            if (item == null)
            {
                item = new GameObject(JacketItemName).transform;
                item.SetParent(parent.transform, false);
            }
            item.position = chair.TransformPoint(pick);
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
            note.AppendFormat("ジャケットの調べる対象: 椅子から見て {0}", pick.ToString("F3")).AppendLine();

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
            iso.FindProperty("draped").objectReferenceValue = draped.gameObject;
            var voice = GameObject.Find("Player/Main Camera/Voice");
            iso.FindProperty("voice").objectReferenceValue = voice != null ? voice.GetComponent<AudioSource>() : null;
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(JacketOnPath);
            if (clip == null) note.AppendLine("着る音が無い: " + JacketOnPath);
            iso.FindProperty("jacketOn").objectReferenceValue = clip;
            iso.ApplyModifiedPropertiesWithoutUndo();
            if (voice == null) note.AppendLine("口元の音源（Player/Main Camera/Voice）が無い");
            return true;
        }

        static void After(SerializedObject so, params string[] ids)
        {
            var chain = so.FindProperty("after");
            chain.arraySize = ids.Length;
            for (var i = 0; i < ids.Length; i++) chain.GetArrayElementAtIndex(i).stringValue = ids[i];
        }
    }
}
