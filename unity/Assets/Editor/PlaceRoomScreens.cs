using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace HalfAware.EditorTools
{
    /// <summary>
    /// 場面 1 の机のモニター 5 枚に、スリープを解除した時に点く画面（<see cref="TerminalScreen"/>）を組む
    /// （オーナー、2026-09-28「モニターのスリープを解除したときに、別の場面の時と同じようにコンソール的なものを表示して」）。
    ///
    /// 場面 3 の組み立て（<see cref="BuildConnect"/>）と同じ窓（<see cref="BuildConnect.Panes"/>）を、同じ割り付けで開ける。
    /// 窓のマテリアルも場面 3 のもの（Connect/TerminalPane.mat、帯を貼って光らせる）。画面の地は場面 1 のまま
    /// （Placeholder/Screen.mat。艶のある黒で、消えている間は部屋と顔を映す鏡）にし、光らせられるよう emission だけ入れる
    /// （光りの色は黒のまま。消えている間の見え方は変わらない）。TerminalScreen の消えている間の地は Screen.mat の色、光りは黒。
    ///
    /// 画面は Room/Monitors に付ける。場面 3 の組み立てはこれをそのまま使い回して窓を開け直し、地と色を場面 3 のものへ替えるので、
    /// 写した先で画面が二つにはならない。起動するのは端末の座る仕掛け（<see cref="TerminalSeat"/>）で、ここで繋ぐ。
    ///
    /// 保存の前に、場面の中の物の一覧（<see cref="PlaceProtagonist.Snapshot"/>）を置く前と比べ、
    /// Room/Monitors（画面を足した）とその下の窓のほかに差が無いことを確かめてから保存する
    /// </summary>
    public static class PlaceRoomScreens
    {
        public const string GroundPath = "Assets/Materials/Placeholder/Screen.mat";

        [MenuItem("HalfAware/Put the screens in the room", false, 208)]
        public static void Menu()
        {
            if (EditorApplication.isPlaying) { Debug.LogError("再生中は組み直さない。止めてからもう一度"); return; }
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.path != PlaceProtagonist.RoomPath)
            {
                Debug.LogError("画面を組むのは場面 1（Room.unity）だけ。場面 3・5・7 は組み直せば付いてくる。今開いているのは " + scene.path);
                return;
            }
            if (scene.isDirty) { Debug.LogError("開いているシーンに未保存の変更がある。保存するか捨ててからもう一度: " + scene.path); return; }
            var before = PlaceProtagonist.Snapshot();
            var note = new StringBuilder();
            var ok = Put(note);
            var after = PlaceProtagonist.Snapshot();
            var diff = Diff(before, after, out var unexpected);
            note.AppendLine("場面の中の物の差:").Append(diff);
            if (!ok || unexpected > 0)
            {
                Debug.LogError("画面を組んだが、ほかの物が " + unexpected + " 個変わった（または組めなかった）ので保存しない。開き直して捨てること\n" + note);
                return;
            }
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("画面を組んで保存した: " + scene.path + "\n" + note);
        }

        /// <summary>窓を開けて画面を付け、端末の座る仕掛けへ繋ぐ。前に組んだ物があれば使い回す（窓は開け直す）</summary>
        public static bool Put(StringBuilder note)
        {
            var monitors = GameObject.Find("Room/Monitors");
            if (monitors == null) { note.AppendLine("Room/Monitors が無い"); return false; }
            var seat = Object.FindFirstObjectByType<TerminalSeat>(FindObjectsInactive.Include);
            if (seat == null) { note.AppendLine("TerminalSeat が無い"); return false; }
            var ground = AssetDatabase.LoadAssetAtPath<Material>(GroundPath);
            if (ground == null) { note.AppendLine("画面の地のマテリアルが無い: " + GroundPath); return false; }
            // 窓は場面 3 と同じマテリアル。在ればそのまま使い、書き換えない（場面 3 の組み立てが持つ）
            var ink = AssetDatabase.LoadAssetAtPath<Material>(BuildConnect.PanePath);
            if (ink == null) ink = BuildConnect.PaneFace();
            if (ink == null) { note.AppendLine("窓のマテリアルが無い: " + BuildConnect.PanePath); return false; }

            // 地は艶のある黒のまま。MaterialPropertyBlock から光りを渡せるよう emission を入れる（色は黒で、消えている間の見え方は変わらない）
            if (!ground.IsKeywordEnabled("_EMISSION") || ground.GetColor("_EmissionColor") != Color.black)
            {
                ground.EnableKeyword("_EMISSION");
                ground.SetColor("_EmissionColor", Color.black);
                ground.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                EditorUtility.SetDirty(ground);
                note.AppendLine("Screen.mat に emission を入れた（色は黒）");
            }

            var backs = new List<Renderer>();
            var panes = new List<TerminalScreen.Pane>();
            var which = 0;
            // 場面 3 の組み立てと同じ順に数えて、同じ割り付けの窓を開ける
            foreach (Transform m in monitors.transform)
            {
                var face = m.Find("Face");
                var r = face != null ? face.GetComponent<Renderer>() : null;
                if (r == null) continue;
                if (r.sharedMaterial != ground) note.AppendLine("（見直し）" + m.name + " の地が Screen.mat ではない: " + (r.sharedMaterial != null ? r.sharedMaterial.name : "null"));
                backs.Add(r);
                panes.AddRange(BuildConnect.Panes(m, face, ink, which));
                which++;
            }
            if (backs.Count == 0) { note.AppendLine("モニターの面が見つからない"); return false; }

            var screen = monitors.GetComponent<TerminalScreen>();
            if (screen == null) screen = monitors.AddComponent<TerminalScreen>();
            var so = new SerializedObject(screen);
            so.FindProperty("off").colorValue = ground.GetColor("_BaseColor");
            so.FindProperty("offGlow").colorValue = Color.black;
            so.FindProperty("startLit").boolValue = false;
            var row = so.FindProperty("backs");
            row.arraySize = backs.Count;
            for (var i = 0; i < backs.Count; i++) row.GetArrayElementAtIndex(i).objectReferenceValue = backs[i];
            var win = so.FindProperty("panes");
            win.arraySize = panes.Count;
            for (var i = 0; i < panes.Count; i++)
            {
                var e = win.GetArrayElementAtIndex(i);
                e.FindPropertyRelative("face").objectReferenceValue = panes[i].face;
                e.FindPropertyRelative("tiling").vector2Value = panes[i].tiling;
                e.FindPropertyRelative("speed").floatValue = panes[i].speed;
                e.FindPropertyRelative("phase").floatValue = panes[i].phase;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(screen);

            var seatSo = new SerializedObject(seat);
            seatSo.FindProperty("screen").objectReferenceValue = screen;
            seatSo.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(seat);

            note.AppendFormat("画面: Room/Monitors に TerminalScreen、面 {0} 枚に窓 {1} 個（窓のマテリアル {2}）。スリープの解除で {3} が起動する",
                backs.Count, panes.Count, ink.name, seat.name).AppendLine();
            return true;
        }

        /// <summary>二つの一覧の差。Room/Monitors とその下の窓の差は書くが、思いがけない差には数えない</summary>
        static string Diff(Dictionary<string, string> a, Dictionary<string, string> b, out int unexpected)
        {
            unexpected = 0;
            var sb = new StringBuilder();
            var keys = new SortedSet<string>(a.Keys);
            keys.UnionWith(b.Keys);
            foreach (var k in keys)
            {
                a.TryGetValue(k, out var x);
                b.TryGetValue(k, out var y);
                if (x == y) continue;
                var allowed = k == "Room/Monitors" || (k.StartsWith("Room/Monitors/Monitor") && k.Contains("/Windows"));
                if (!allowed) unexpected++;
                sb.AppendFormat("  {0}{1}: {2} → {3}", allowed ? "" : "（思いがけない）", k, x ?? "無し", y ?? "無し").AppendLine();
            }
            if (sb.Length == 0) sb.AppendLine("  無し");
            return sb.ToString();
        }
    }
}
