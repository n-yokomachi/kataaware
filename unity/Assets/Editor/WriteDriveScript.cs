using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace HalfAware.EditorTools
{
    /// <summary>
    /// 場面 8 の文面を書き出す。原作の車中の独白をここに持ち、
    /// `Assets/Data/DriveScript.asset` へ流し込む。
    ///
    /// 文面をアセットへ置くのは、位置と規則（シーン）と文（アセット）を分けるため。
    /// ただし手で YAML を書くと日本語が読めなくなるので、元はこのファイルに置いて
    /// メニューから書き出す。**台詞を直すならここを直して走らせ直す。**
    ///
    /// 独白の本文と行数はオーナーが決める。ここに入れてあるのは原作からの抜きで、
    /// 帯ごとの分け方も含めて差し替えてよい
    /// </summary>
    public static class WriteDriveScript
    {
        const string Path = "Assets/Data/DriveScript.asset";

        /// <summary>景色ごとの独白。DriveIds.Triggers と同じ並び</summary>
        public static readonly string[][] BandPages =
        {
            new[]
            {
                "自動運転に任せて私は腕組みをしながら考える",
                "思えばこの時のために、私は幾人もの思い出を盗み見てきた。盗み見る誰かの記憶の中に、私の姿があれば、きっとその人は私のことを知っている。思い出を持たない私の代わりに、私のことを記憶してくれている人がいる。そんな人を私はずっと探していた。私は私のことが知りたかった",
            },
            new[]
            {
                "この時が思わぬ形で訪れたことに、私はまだ動揺していた。記憶の中に自分の姿を見るのではなく、その記憶を持っている人間自身が私と全く同じ感覚を持っている、というのだから私は混乱していた",
                "もしかしたらただの勘違いかもしれない。偶然全く同じ背丈、全く同じ体重、全く同じ視力、全く同じ触覚、全く同じ・・・いやそんなことがあるのだろうか？",
                "考えても仕方がない",
                "今向かっている先にいる人が私のことを知っている確証はない。かと言ってこの巡りあわせを逃す手もない。ただ私と同じ感覚を持っているという点だけが、今は手掛かりなのだ",
            },
            new[]
            {
                "朝靄の中で、緩やかな湾曲を描いて広がる小麦畑が黄金色に微風になびいている",
                "この匂いを覚えている。あの記憶の中で嗅いだ匂いだ",
            },
        };

        /// <summary>きっかけの対象そのものの文。段の前に出る</summary>
        public static readonly string[] TriggerLines =
        {
            "助手席にはメモリーチップの束が転がっている",
            "煙草に火をつけ、窓を開ける",
            "窓を開けて、大きく息を吸い込んだ",
        };

        /// <summary>きっかけの対象の印に出す文</summary>
        public static readonly string[] TriggerLabels =
        {
            "チップの束を見る",
            "煙草を取る",
            "窓を開ける",
        };

        /// <summary>
        /// 書き出す項目をそのまま組んで返す。Write はこれをアセットへ流し込むだけにしてある。
        /// 分けてあるのは、見直し（CheckDrive）が「いま書き出すもの」と
        /// アセットの中身を突き合わせられるようにするため。
        /// ここの文を直したままアセットを書き出し忘れると、組み立てのたびに知らせが出る
        /// </summary>
        public static List<ScriptEntry> Entries()
        {
            var entries = new List<ScriptEntry>();

            // ガレージ。運転席のドアを調べると乗り込む。乗り込めばガレージには戻れないので、
            // 見た目や進行が変わる対象として二択で確かめる（場面 1 の扉・場面 2 のテーブルと同じ扱い）
            entries.Add(new ScriptEntry
            {
                id = DriveIds.Door,
                label = "車に乗り込む",
                lines = new[] { "ボロのオフロード車。ドアの立て付けは相変わらず悪い" },
                hints = new ScriptHint[0],
                choice = new ScriptChoice
                {
                    question = "車に乗り込む",
                    afterYes = new string[0],
                },
            });

            // シャッターの開閉ボタン。押しても何も起きない。
            // 二択は出さない（出すと SceneFlow.CloseChoice がプレイヤーを歩けるようにする）
            entries.Add(new ScriptEntry
            {
                id = DriveIds.Button,
                label = "シャッターのボタンを見る",
                lines = new[] { "シャッターのボタンだが、押さなくても車で近づけば勝手に開く" },
                hints = new ScriptHint[0],
            });

            // 帯ごとのきっかけ。対象の文のあとに、DriveDirector が段を積む
            for (var i = 0; i < DriveIds.Triggers.Count; i++)
            {
                entries.Add(new ScriptEntry
                {
                    id = DriveIds.Triggers[i],
                    label = TriggerLabels[i],
                    lines = new[] { TriggerLines[i] },
                    hints = new ScriptHint[0],
                });
                entries.Add(new ScriptEntry
                {
                    id = DriveIds.Page(i),
                    label = "",
                    lines = BandPages[i],
                    hints = new ScriptHint[0],
                });
            }

            // 帯を問わず置く、読んでも帯が進まない対象
            entries.Add(new ScriptEntry
            {
                id = DriveIds.Radio,
                label = "ラジオをつける",
                lines = new[] { "雑音しか拾わない。この辺りはもう中継塔の外だ" },
                hints = new ScriptHint[0],
            });
            entries.Add(new ScriptEntry
            {
                id = DriveIds.Fuel,
                label = "燃料計を見る",
                lines = new[] { "半分を切っている。町に着くまでは保つ" },
                hints = new ScriptHint[0],
            });

            return entries;
        }

        [MenuItem("HalfAware/Write the drive script", false, 220)]
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
                choice.FindPropertyRelative("question").stringValue = entries[i].choice.question ?? "";
                Fill(choice.FindPropertyRelative("afterYes"), entries[i].choice.afterYes);
            }
            so.ApplyModifiedPropertiesWithoutUndo();

            if (made)
            {
                if (!AssetDatabase.IsValidFolder("Assets/Data")) AssetDatabase.CreateFolder("Assets", "Data");
                AssetDatabase.CreateAsset(asset, Path);
            }
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
            Debug.Log(string.Format("場面 8 の文面を書き出した。{0} 項目 → {1}", entries.Count, Path));
        }

        static void Fill(SerializedProperty list, string[] values)
        {
            var n = values == null ? 0 : values.Length;
            list.arraySize = n;
            for (var i = 0; i < n; i++) list.GetArrayElementAtIndex(i).stringValue = values[i];
        }
    }
}
