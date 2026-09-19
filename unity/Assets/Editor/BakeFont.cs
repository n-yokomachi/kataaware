using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace HalfAware.EditorTools
{
    /// <summary>
    /// 暗転のカードに使う明朝を焼き直す。
    ///
    /// 台詞・ログ・調べる印は Noto Sans JP のまま。読ませる字はゴシックのほうが楽で、
    /// 明朝は暗転して見せるカードだけに使う。
    ///
    /// TextMeshPro は .ttf から直に字を描かず、字の形を並べた絵から切り出して貼る。
    /// だから使う字は先に絵へ入れておく（焼いておく）必要がある。
    /// **カードの文を変えたらこれを走らせること。**
    /// </summary>
    public static class BakeFont
    {
        const string Face = "Assets/Fonts/ShipporiMincho-Regular SDF.asset";
        const string Source = "Assets/Fonts/ShipporiMincho-Regular.ttf";
        const string Spare = "Assets/Fonts/NotoSansJP-Regular SDF.asset";

        /// <summary>明朝にする文字の名。暗転して真ん中に出すカード</summary>
        const string Card = "Center";

        /// <summary>
        /// 焼く大きさ。大きいほど綺麗だが、図に入る字数が減る。
        /// 図は 1024 を複数枚。2048 にすると焼けないので広げない
        /// </summary>
        const int PointSize = 52;
        const int Padding = 5;
        const int AtlasSide = 1024;

        static readonly string[] Scenes =
        {
            "Assets/Scenes/Room.unity",
            "Assets/Scenes/Alley.unity",
        };

        /// <summary>
        /// コードが組み立てて画面に出す言葉。場面には入っていないのでここに並べる。
        /// 足したらここも足すこと
        /// </summary>
        const string FromCode =
            "――  場面  ――　← いま　Tab 閉じる　自室　路地裏　（仮）続く　E　" +
            "倫敦　ホルボーン　グレビル・ストリート";

        [MenuItem("HalfAware/Bake the font", false, 200)]
        public static void Bake()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogError("再生中は焼かない。止めてからもう一度");
                return;
            }
            var font = AssetDatabase.LoadAssetAtPath<Font>(Source);
            if (font == null) { Debug.LogError("元のフォントが無い: " + Source); return; }

            var want = Collect();
            var chars = Sorted(want);

            // 図を作ってから字を足す。順を違えると足せない
            if (AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(Face) != null) AssetDatabase.DeleteAsset(Face);
            var asset = TMP_FontAsset.CreateFontAsset(font, PointSize, Padding, GlyphRenderMode.SDFAA,
                AtlasSide, AtlasSide, AtlasPopulationMode.Dynamic, true);
            asset.name = "ShipporiMincho-Regular SDF";
            asset.isMultiAtlasTexturesEnabled = true;
            asset.TryAddCharacters(chars, true);

            // 焼き終えたら、実行中には足さない作りへ移す
            asset.atlasPopulationMode = AtlasPopulationMode.Static;
            var spare = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(Spare);
            if (spare != null) asset.fallbackFontAssetTable = new List<TMP_FontAsset> { spare };

            AssetDatabase.CreateAsset(asset, Face);
            for (var i = 0; i < asset.atlasTextures.Length; i++)
            {
                if (asset.atlasTextures[i] == null) continue;
                asset.atlasTextures[i].name = "Atlas " + i;
                AssetDatabase.AddObjectToAsset(asset.atlasTextures[i], asset);
            }
            if (asset.material != null)
            {
                asset.material.name = "ShipporiMincho-Regular SDF Material";
                AssetDatabase.AddObjectToAsset(asset.material, asset);
            }
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(Face);

            var missing = new StringBuilder();
            var lost = 0;
            for (var i = 0; i < chars.Length; i++)
            {
                if (asset.HasCharacter(chars[i])) continue;
                lost++;
                if (lost <= 24) missing.AppendFormat("U+{0:X4}({1}) ", (int)chars[i], chars[i]);
            }
            Debug.Log(string.Format("書体を焼いた。{0} 字のうち {1} 字、図 {2} 枚 {3}x{3}。焼けなかった {4} 字 {5}",
                chars.Length, asset.characterTable.Count, asset.atlasTextures.Length, AtlasSide, lost, missing));

            Apply(asset);
        }

        /// <summary>
        /// 書体を当てる。画面の真ん中に出すカードだけ明朝、
        /// 台詞・ログ・調べる印はゴシックのまま
        /// </summary>
        static void Apply(TMP_FontAsset mincho)
        {
            var gothic = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(Spare);
            var settings = TMP_Settings.instance;
            if (settings != null && gothic != null)
            {
                var so = new SerializedObject(settings);
                var p = so.FindProperty("m_defaultFontAsset");
                if (p != null) p.objectReferenceValue = gothic;
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(settings);
            }
            var open = EditorSceneManager.GetActiveScene().path;
            var sb = new StringBuilder();
            for (var i = 0; i < Scenes.Length; i++)
            {
                EditorSceneManager.OpenScene(Scenes[i], OpenSceneMode.Single);
                var texts = Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.InstanceID);
                for (var k = 0; k < texts.Length; k++)
                {
                    var card = texts[k].gameObject.name == Card;
                    texts[k].font = card ? mincho : gothic;
                    EditorUtility.SetDirty(texts[k]);
                    sb.AppendFormat("  {0} / {1} → {2}\n", Scenes[i].Substring(14), texts[k].gameObject.name,
                        card ? "明朝" : "ゴシック");
                }
                var scene = EditorSceneManager.GetActiveScene();
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
            if (!string.IsNullOrEmpty(open)) EditorSceneManager.OpenScene(open, OpenSceneMode.Single);
            AssetDatabase.SaveAssets();
            Debug.Log("書体を当てた\n" + sb);
        }

        /// <summary>焼く字を集める</summary>
        static HashSet<char> Collect()
        {
            var want = new HashSet<char>();
            Range(want, 0x0020, 0x007E);   // ASCII
            Range(want, 0x3000, 0x303F);   // 約物
            Range(want, 0x3041, 0x309F);   // ひらがな
            Range(want, 0x30A0, 0x30FF);   // カタカナ
            Range(want, 0xFF01, 0xFF65);   // 全角
            Add(want, "―—…‥•·′″‰†‡←→↑↓●○◆■□▲△※〒℃±×÷≦≧∞√");
            Add(want, FromCode);

            var open = EditorSceneManager.GetActiveScene().path;
            for (var i = 0; i < Scenes.Length; i++)
            {
                EditorSceneManager.OpenScene(Scenes[i], OpenSceneMode.Single);
                var all = Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.InstanceID);
                for (var k = 0; k < all.Length; k++)
                {
                    var so = new SerializedObject(all[k]);
                    var p = so.GetIterator();
                    while (p.Next(true))
                    {
                        if (p.propertyType != SerializedPropertyType.String) continue;
                        if (string.IsNullOrEmpty(p.stringValue)) continue;
                        // 飾りの札は字ではない
                        Add(want, Regex.Replace(p.stringValue, "<[^>]+>", ""));
                    }
                }
            }
            if (!string.IsNullOrEmpty(open)) EditorSceneManager.OpenScene(open, OpenSceneMode.Single);
            return want;
        }

        static void Range(HashSet<char> set, int from, int to)
        {
            for (var c = from; c <= to; c++) set.Add((char)c);
        }

        static void Add(HashSet<char> set, string text)
        {
            for (var i = 0; i < text.Length; i++) if (text[i] >= ' ') set.Add(text[i]);
        }

        static string Sorted(HashSet<char> set)
        {
            var list = new char[set.Count];
            set.CopyTo(list);
            System.Array.Sort(list);
            return new string(list);
        }
    }
}
