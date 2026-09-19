using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace HalfAware.EditorTools
{
    /// <summary>
    /// 場面 2 の文面を書き出す。シナリオ 6.2〜6.4 の本文をここに持ち、
    /// `Assets/Data/AlleyScript.asset` へ流し込む。
    ///
    /// 文面をアセットへ置くのは、位置と規則（シーン）と文（アセット）を分けるため。
    /// ただし手で YAML を書くと日本語が読めなくなるので、元はこのファイルに置いて
    /// メニューから書き出す。**台詞を直すならここを直して走らせ直す。**
    /// </summary>
    public static class WriteAlleyScript
    {
        const string Path = "Assets/Data/AlleyScript.asset";

        /// <summary>通りの看板。どれから読んでもこの順に流れる</summary>
        public static readonly string[][] StreetPages =
        {
            new[]
            {
                "ホルボーンの大通りからグレビル・ストリートに入ったころから、この倫敦の霧がかった夜空からは雨が降り出した。グレビル・ストリートはこのあたりでも特にネオンサインが眩い",
            },
            new[]
            {
                "一世紀前まではモダニズムからポストモダニズムへの過渡期にあった建築物が大窓を並べ、落ち着いた都会の一角をなしていたというこの通りも、今ではそのほとんどに、瞼の裏に焼き付くような極彩色のネオンが熱帯植物のように絡みついている",
            },
            new[]
            {
                "倫敦を発端とした近年のインプラント革命は世界にとって再びの産業革命でもあった。埋め込み型のウェアラブルデバイスの発達と、神経接続の確立によってあらゆる情報が脳と外界との間でのやり取りが可能になった",
                "人間が本来具有する目や耳といった感覚器官や、一部の運動機能の生活上の必要性を大幅に下げたインプラント型神経端末、「ナーヴ・ターミナル」は｜最小の生活基盤《Minimum Infrastructure》とも呼ばれる普及率となった",
            },
            new[]
            {
                "こうした技術革命は一部の民衆に対して、数世紀前の小説や映画のジャンルであった『サイバーパンク』を想起させた",
                "かの舞台を彩る、日本の電脳都市｜千葉市《チバ・シティ》や宇宙船の浮かぶ｜羅府《ロサンゼルス》に憧れた人々は、かつて実在したこともない懐かしい街並みを取り戻そうとするかのように、通りを雑多に飾り立て、ネオン管を張り巡らせたのだ",
            },
            new[]
            {
                "その結果がこのけばけばしいストリートだ。大昔の産業革命が吐き出した霧の代わりに、今この都市を覆っているのは、クラッカーたちが撒き散らすナーヴ・クラック用のナノマシンの黒雲だ",
                "あれがナーヴ・ターミナルのジャックに入り込むと、ファイアウォールを立てていない人間は一発で個人情報も資産情報も抜き取られる",
            },
        };

        /// <summary>看板の数。BuildAlley が立てる数と揃える</summary>
        public static int Signs { get { return StreetPages.Length; } }

        [MenuItem("HalfAware/Write the alley script", false, 210)]
        public static void Write()
        {
            var asset = AssetDatabase.LoadAssetAtPath<RoomScript>(Path);
            var made = asset == null;
            if (made) asset = ScriptableObject.CreateInstance<RoomScript>();

            var entries = new List<ScriptEntry>();

            // 通りの看板。文は page 側に持ち、看板そのものは印だけ
            for (var i = 0; i < Signs; i++)
                entries.Add(new ScriptEntry
                {
                    id = AlleyIds.Sign(i),
                    label = "看板を読む",
                    lines = new string[0],
                    hints = new ScriptHint[0],
                });
            for (var i = 0; i < Signs; i++)
                entries.Add(new ScriptEntry
                {
                    id = AlleyIds.Page(i),
                    label = "",
                    lines = StreetPages[i],
                    hints = new ScriptHint[0],
                });

            // 小路の口の表示板
            entries.Add(new ScriptEntry
            {
                id = AlleyIds.Board,
                label = "表示板を読む",
                lines = new[]
                {
                    "｜ブリーディング・ハート・ヤード《流血する心臓の庭》",
                    "かつていくつかの小説にも登場したこの小さな中庭は、今や足の置き場もないほどに蚤の市の出店と、座り込んだ売り手、せわしなく歩き回る買い手が占めている",
                    "私の露店は一番奥だ",
                },
                hints = new ScriptHint[0],
            });

            // 自分の露店の看板
            entries.Add(new ScriptEntry
            {
                id = AlleyIds.StallSign,
                label = "自分の露店の看板を見る",
                lines = new[]
                {
                    "『記憶売ります』の看板。200年前のSF小説からとった宣伝文句だ。かつてはフィクションだったその言葉が、今や小汚い露店で現実になっている",
                },
                hints = new ScriptHint[0],
            });

            // テーブル。ここだけ必須。二択で「はい」を選ぶと売り買いが始まる
            entries.Add(new ScriptEntry
            {
                id = AlleyIds.Table,
                label = "チップを置く",
                lines = new[]
                {
                    "傷だらけのテーブル。ここにメモリーチップを並べておけば、あとは向こうから寄ってくる",
                },
                hints = new ScriptHint[0],
                choice = new ScriptChoice
                {
                    question = "メモリーチップを置く",
                    afterYes = new string[0],
                },
            });

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
            Debug.Log(string.Format("場面 2 の文面を書き出した。{0} 項目 → {1}", entries.Count, Path));
        }

        static void Fill(SerializedProperty list, string[] values)
        {
            var n = values == null ? 0 : values.Length;
            list.arraySize = n;
            for (var i = 0; i < n; i++) list.GetArrayElementAtIndex(i).stringValue = values[i];
        }
    }
}
