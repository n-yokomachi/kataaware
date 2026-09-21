using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace HalfAware.EditorTools
{
    /// <summary>
    /// 場面 5 の文面を書き出す。<c>Assets/Data/RestScript.asset</c> へ流し込む。
    ///
    /// **項目はモニターひとつだけ。** 場面 5 で調べられるのはそれしかない。
    /// 文も二択も付けない。調べた瞬間に必須が済んで場面が閉じる。
    /// 吸い終わりの独白は対象に紐づかないので、ここではなく
    /// <see cref="RestDirector"/> が持っている。
    ///
    /// id は場面 3 と同じ <see cref="ConnectIds.Dive"/> を使う。同じ部屋の同じモニターを、
    /// 同じ「潜る」で調べるので、綴りを分ける理由がない
    /// </summary>
    public static class WriteRestScript
    {
        const string Path = "Assets/Data/RestScript.asset";

        /// <summary>書き出す項目。見直しがアセットと突き合わせられるように分けてある</summary>
        public static List<ScriptEntry> Entries()
        {
            return new List<ScriptEntry>
            {
                new ScriptEntry
                {
                    id = ConnectIds.Dive,
                    label = "潜る",
                    lines = new string[0],
                    hints = new ScriptHint[0],
                },
            };
        }

        [MenuItem("HalfAware/Write the rest script", false, 252)]
        public static void Write()
        {
            var asset = AssetDatabase.LoadAssetAtPath<RoomScript>(Path);
            var made = asset == null;
            if (made) asset = ScriptableObject.CreateInstance<RoomScript>();

            var entries = Entries();
            var so = new SerializedObject(asset);
            var list = so.FindProperty("entries");
            list.arraySize = entries.Count;
            for (var i = 0; i < entries.Count; i++)
            {
                var e = list.GetArrayElementAtIndex(i);
                e.FindPropertyRelative("id").stringValue = entries[i].id;
                e.FindPropertyRelative("label").stringValue = entries[i].label;
                Fill(e.FindPropertyRelative("lines"), entries[i].lines);
                e.FindPropertyRelative("hints").arraySize = 0;
                var choice = e.FindPropertyRelative("choice");
                choice.FindPropertyRelative("question").stringValue = "";
                choice.FindPropertyRelative("afterYes").arraySize = 0;
            }
            so.ApplyModifiedPropertiesWithoutUndo();

            if (made)
            {
                if (!AssetDatabase.IsValidFolder("Assets/Data")) AssetDatabase.CreateFolder("Assets", "Data");
                AssetDatabase.CreateAsset(asset, Path);
            }
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
            Debug.Log(string.Format("場面 5 の文面を書き出した。{0} 項目 → {1}", entries.Count, Path));
        }

        static void Fill(SerializedProperty list, string[] values)
        {
            var n = values == null ? 0 : values.Length;
            list.arraySize = n;
            for (var i = 0; i < n; i++) list.GetArrayElementAtIndex(i).stringValue = values[i];
        }
    }
}
