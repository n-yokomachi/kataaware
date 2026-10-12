using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HalfAware.EditorTools
{
    /// <summary>
    /// 自室（Room.unity）の小さな物を、マテリアルごとに一つの mesh へまとめて描く回数を減らす
    /// （<see cref="MeshMerge"/>。<c>docs/superpowers/plans/2026-10-12-mesh-weight-survey.md</c>）。
    ///
    /// 自室は、道具が名前で物を引いて形や広がりを読む（ジャケットを置く卓、メモを置くローテーブル、場面 3 で消すチップ、見比べの撮影など）。
    /// なので元の物は名前・当たり・mesh をそのまま残し、MeshRenderer を切るだけにする（hide）。
    ///
    /// まとめる物:
    /// - 家具（<see cref="BuildFurniture.MergeFurniture"/>。家具を置き直すたびにまとめ直す）
    /// - 紙ばさみ（Binder。<see cref="BuildProps.BuildBinder"/> が組むたびにまとめる）
    /// - メモリハブ（MemoryHub）の箱・面・桟・溝・通気口。チップと札（Chip・Label）は場面 3 の組み立て（<see cref="BuildConnect"/> の Sold）が
    ///   名前で消すので残す。持ち出しで消える物・灯りの替わる物（Taken が指す物）もまとめない
    /// - 窓の枠（窓ごと）。硝子は透けるのでまとめない
    ///
    /// まとめない物: 椅子（Room/Chair）・主人公・モニターと映り込み（Monitors。TerminalScreen が画面を指す）・煙草の箱と灰皿・
    /// カーテン（<see cref="BuildRoomView"/> の Drape が襞と棒を名前で動かす）・ドア（<see cref="BuildRoomShell"/> の Doors が裏の板を名前で動かす）。
    ///
    /// 何度呼んでも同じ形になる（切った物は次のまとめで数えないので、まとめた物が二重にならない）。
    /// 自室を変えたら、場面 3・5・7（Connect・Notice・Rest）をいつもの道具で組み直すと、まとめた形が写る
    /// </summary>
    public static class MergeRoom
    {
        /// <summary>まとめた mesh の置き場（家具の分は家具の置き場）</summary>
        public const string Generated = "Assets/Models/generated/roomprops/";

        const string HubName = "MemoryHub";

        [MenuItem("HalfAware/Merge the room boxes", false, 217)]
        public static void Menu()
        {
            Debug.Log(Run());
        }

        /// <summary>
        /// Room.unity を開いてまとめ、保存する。まとめた物が増える以外に場面の物の一覧が変わったら保存しない
        /// </summary>
        public static string Run()
        {
            if (EditorApplication.isPlaying) return "再生中はまとめない";
            for (var i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty)
                    return "開いている場面に未保存の変更がある。保存するか捨ててからもう一度: " + SceneManager.GetSceneAt(i).path;
            var scene = EditorSceneManager.OpenScene(BuildConnect.RoomPath, OpenSceneMode.Single);
            var room = BuildChair.Find(scene, "Room");
            if (room == null) return "Room が無い";
            var before = Paths(scene);
            var text = Apply(room);
            var after = Paths(scene);
            var bad = new List<string>();
            foreach (var p in before) if (!after.Contains(p)) bad.Add("無くなった " + p);
            foreach (var p in after)
            {
                if (before.Contains(p)) continue;
                var leaf = p.Substring(p.LastIndexOf('/') + 1);
                if (!leaf.StartsWith(MeshMerge.Prefix + ".")) bad.Add("増えた " + p);
            }
            if (bad.Count > 0) return text + "\n思いがけない差があるので保存しない:\n  " + string.Join("\n  ", bad.ToArray());
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) return text + "\nRoom.unity を保存できなかった";
            AssetDatabase.SaveAssets();
            return text + "\n物の一覧: " + before.Count + " → " + after.Count + "（増えたのはまとめた物だけ）";
        }

        /// <summary>開いている自室をまとめる。保存はしない</summary>
        public static string Apply(Transform room)
        {
            var log = new StringBuilder();
            var total = 0;
            log.AppendLine(BuildFurniture.MergeFurniture(room));
            var binder = room.Find("Binder");
            if (binder != null)
            {
                log.AppendLine("Binder");
                total += Binder(binder, log);
            }
            var hub = room.Find(HubName);
            if (hub != null)
            {
                log.AppendLine(HubName);
                total += MeshMerge.Merge(hub, Generated, HubName, r => r.name.StartsWith("Chip") || r.name.StartsWith("Label"), false, log, true);
            }
            foreach (var w in RoomPlan.Windows)
            {
                var t = room.Find(w.Name);
                if (t == null) continue;
                log.AppendLine(w.Name);
                total += MeshMerge.Merge(t, BuildRoomShell.MeshDir + "/", w.Name, null, false, log, true);
            }
            return "自室の小物をまとめた: " + total + " 個（家具は別）\n" + log.ToString().TrimEnd();
        }

        /// <summary>紙ばさみの部品をまとめる。組んだ直後に <see cref="BuildProps.BuildBinder"/> が呼ぶ</summary>
        public static int Binder(Transform binder, StringBuilder log)
        {
            return MeshMerge.Merge(binder, Generated, "Binder", null, false, log, true);
        }

        static HashSet<string> Paths(Scene scene)
        {
            var set = new HashSet<string>();
            foreach (var r in scene.GetRootGameObjects()) Walk(r.transform, r.name, set);
            return set;
        }

        static void Walk(Transform t, string path, HashSet<string> set)
        {
            set.Add(path);
            for (var i = 0; i < t.childCount; i++) Walk(t.GetChild(i), path + "/" + t.GetChild(i).name, set);
        }
    }
}
