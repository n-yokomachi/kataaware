using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace HalfAware.EditorTools
{
    /// <summary>
    /// 足音の組（<see cref="StepSets"/>）を、開いているシーンの <see cref="Footsteps"/> に差し替えて保存する。
    /// 足音の音だけを替える道具で、ほかの物には触らない。
    /// <list type="table">
    /// <item><term>Room / Connect / Rest / Notice（場面 1・3・5・7）</term><description><see cref="StepSets.Room"/></description></item>
    /// <item><term>Alley（場面 2）</term><description><see cref="StepSets.Concrete"/></description></item>
    /// <item><term>Drive（場面 8）</term><description><see cref="StepSets.HardFloor"/></description></item>
    /// </list>
    /// 場面 4（Dive）は <see cref="BuildDive"/> が、村は BuildVillageSound が組み立てのときに入れる。
    ///
    /// 場面 3・5・7 は場面 1 を写して組む（<see cref="BuildConnect"/> → <see cref="BuildRest"/>・<see cref="BuildNotice"/>）ので、
    /// 組み直せば場面 1 の音が付いてくる。組み直さずに音だけ替えるときにここを通す。
    /// 場面 2 の足元（Feet）は Alley.unity に手で置いた物で、<c>HalfAware/Build the alley</c> は触らない。場面 8 は
    /// <see cref="BuildDrive"/> が組み直すたびに同じ組を入れる。
    ///
    /// 保存の前に、場面の中の物の一覧（<see cref="PlaceProtagonist.Snapshot"/>）を差し替える前と比べ、
    /// 差が一つでもあれば保存しない（Room.unity を保存すると場面 1 が丸ごと書き換わるので、ここは固く見る）
    /// </summary>
    public static class PlaceFootsteps
    {
        [MenuItem("HalfAware/Put the footsteps in the scene", false, 208)]
        public static void Menu()
        {
            if (EditorApplication.isPlaying) { Debug.LogError("再生中は組み直さない。止めてからもう一度"); return; }
            var scene = EditorSceneManager.GetActiveScene();
            var note = new StringBuilder();
            if (Put(scene.path, note)) Debug.Log("足音を差し替えて保存した: " + scene.path + "\n" + note);
            else Debug.LogError("足音を差し替えなかった: " + scene.path + "\n" + note);
        }

        /// <summary>シーンの道のりから、鳴らす組を決める。知らないシーンは null</summary>
        public static string[] SetFor(string scenePath)
        {
            switch (scenePath)
            {
                case PlaceProtagonist.RoomPath:
                case BuildConnect.ScenePath:
                case BuildRest.ScenePath:
                case BuildNotice.ScenePath:
                    return StepSets.Room;
                case PlaceProtagonist.AlleyPath:
                    return StepSets.Concrete;
                case BuildDrive.ScenePath:
                    return StepSets.HardFloor;
                default:
                    return null;
            }
        }

        /// <summary>
        /// 開いているシーン（scenePath であること）の足音を差し替えて保存する。
        /// 未保存の変更があるシーン、足音が一つでないシーン、物の一覧に差が出たシーンは保存しない
        /// </summary>
        public static bool Put(string scenePath, StringBuilder note)
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.path != scenePath) { note.AppendLine("開いているのは " + scene.path + "。先に " + scenePath + " を開く"); return false; }
            if (scene.isDirty) { note.AppendLine("未保存の変更がある。保存するか捨ててからもう一度"); return false; }
            var paths = SetFor(scenePath);
            if (paths == null) { note.AppendLine("このシーンの足音はここでは替えない（場面 4 は Build the dive、村は Build the village）"); return false; }
            var clips = StepSets.Clips(paths);
            if (clips.Length != paths.Length) { note.AppendLine("足音の素材が欠けている"); return false; }

            var all = Object.FindObjectsByType<Footsteps>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (all.Length != 1) { note.AppendLine("足音（Footsteps）が " + all.Length + " 個ある。一つのはず"); return false; }

            var before = PlaceProtagonist.Snapshot();
            var so = new SerializedObject(all[0]);
            var row = so.FindProperty("clips");
            var was = new StringBuilder();
            for (var i = 0; i < row.arraySize; i++)
            {
                var c = row.GetArrayElementAtIndex(i).objectReferenceValue;
                was.Append(i == 0 ? "" : " ").Append(c != null ? c.name : "null");
            }
            row.arraySize = clips.Length;
            for (var i = 0; i < clips.Length; i++) row.GetArrayElementAtIndex(i).objectReferenceValue = clips[i];
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(all[0]);
            var after = PlaceProtagonist.Snapshot();

            var unexpected = Diff(before, after, note);
            var now = new StringBuilder();
            foreach (var c in clips) now.Append(now.Length == 0 ? "" : " ").Append(c.name);
            note.AppendLine(Path(all[0].transform) + " の足音: " + was + " → " + now);
            if (unexpected > 0)
            {
                note.AppendLine("物の一覧に " + unexpected + " 個の差が出たので保存しない。開き直して捨てること");
                return false;
            }
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) { note.AppendLine("保存に失敗した"); return false; }
            return true;
        }

        /// <summary>二つの一覧の差を書き、差の数を返す。足音の差し替えでは一つも出ないはず</summary>
        static int Diff(Dictionary<string, string> a, Dictionary<string, string> b, StringBuilder note)
        {
            var n = 0;
            var keys = new SortedSet<string>(a.Keys);
            keys.UnionWith(b.Keys);
            foreach (var k in keys)
            {
                a.TryGetValue(k, out var x);
                b.TryGetValue(k, out var y);
                if (x == y) continue;
                n++;
                note.AppendFormat("  （思いがけない）{0}: {1} → {2}", k, x ?? "無し", y ?? "無し").AppendLine();
            }
            note.AppendLine("物の一覧の差: " + n + " 個（" + a.Count + " 個を比べた）");
            return n;
        }

        static string Path(Transform t)
        {
            var s = t.name;
            for (var p = t.parent; p != null; p = p.parent) s = p.name + "/" + s;
            return s;
        }
    }
}
