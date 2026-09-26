using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore;

namespace HalfAware.EditorTools
{
    /// <summary>
    /// 案内と送りの印に出す左クリックのアイコン（<see cref="HudView.Prompt"/>・<see cref="HudView.Advance"/>）を作る。
    /// 左のボタンだけ塗ったマウスの画素絵を PNG に描き、TMP のスプライトアセット（Resources/Sprite Assets/MouseLeft）にする。
    ///
    /// **粗い画面（<see cref="UiLens"/>、0.75）の中で、字の高さに一画素ずつ揃えて描く。** 字幕の寸法を決めている
    /// 960×540（粗い画面の中は 720×405）で、案内の字は 16.5 画素、送りの印の字は 11 画素。
    /// 同じ絵を拡げ縮めすると画素の太さが揃わないので、二つの大きさを描き分ける:
    /// <c>large</c>（11×15 画素、案内）と <c>small</c>（7×10 画素、送りの印）。どちらも字の 0.91 em の高さで、
    /// 下の端を漢字の下の端（ベースラインの 0.08 em 下）に揃える。
    /// 絵は一画素を 4×4 に point のまま拡げて焼き、最近傍で引く（ぼかさない）。
    /// 色は白で、字の色で塗らせる（<c>tint=1</c>）
    /// </summary>
    public static class ClickIcon
    {
        public const string TexturePath = "Assets/Textures/Hud/MouseLeft.png";
        public const string AssetPath = "Assets/Resources/Sprite Assets/" + HudView.ClickAsset + ".asset";

        /// <summary>一画素を何テクセルに拡げて焼くか</summary>
        const int Zoom = 4;
        /// <summary>絵の高さを字の何 em にするか</summary>
        const float Tall = 0.91f;
        /// <summary>絵の下の端。ベースラインから下へ何 em か（Noto の漢字の下の端に揃える）</summary>
        const float Sink = 0.08f;

        /// <summary>送りの印の字（11 画素）に揃えた絵。上の行から。# が塗る所</summary>
        static readonly string[] Small =
        {
            ".#####.",
            "####..#",
            "####..#",
            "####..#",
            "#######",
            "#.....#",
            "#.....#",
            "#.....#",
            "#.....#",
            ".#####.",
        };

        /// <summary>案内の字（16.5 画素）に揃えた絵</summary>
        static readonly string[] Large =
        {
            "..#######..",
            ".#####...#.",
            "######....#",
            "######....#",
            "######....#",
            "######....#",
            "###########",
            "#.........#",
            "#.........#",
            "#.........#",
            "#.........#",
            "#.........#",
            "#.........#",
            ".#.......#.",
            "..#######..",
        };

        [MenuItem("HalfAware/Make the click icon", false, 281)]
        public static void Make()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogError("再生中は作らない。止めてからもう一度");
                return;
            }
            var texture = Paint();
            var asset = Wrap(texture);
            Debug.Log("左クリックのアイコンを作った → " + TexturePath + "・" + AssetPath + "（" + asset.spriteCharacterTable.Count + " 枚）");
        }

        /// <summary>二つの絵を一枚の PNG に焼いて読み込む。左が small、右が large。どちらも下の端に揃える</summary>
        static Texture2D Paint()
        {
            var w = Small[0].Length * Zoom + Zoom + Large[0].Length * Zoom;
            var h = Mathf.Max(Small.Length, Large.Length) * Zoom;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            try
            {
                var clear = new Color32(255, 255, 255, 0);
                var fill = new Color32[w * h];
                for (var i = 0; i < fill.Length; i++) fill[i] = clear;
                tex.SetPixels32(fill);
                Draw(tex, Small, 0);
                Draw(tex, Large, Small[0].Length * Zoom + Zoom);
                tex.Apply();
                Directory.CreateDirectory(Path.GetDirectoryName(TexturePath));
                File.WriteAllBytes(TexturePath, tex.EncodeToPNG());
            }
            finally
            {
                Object.DestroyImmediate(tex);
            }
            AssetDatabase.ImportAsset(TexturePath);
            var importer = (TextureImporter)AssetImporter.GetAtPath(TexturePath);
            importer.textureType = TextureImporterType.Default;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Point;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.alphaIsTransparency = true;
            importer.sRGBTexture = true;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath);
        }

        /// <summary>絵を x の所へ、下の端を揃えて描く</summary>
        static void Draw(Texture2D tex, string[] rows, int x)
        {
            var white = new Color32(255, 255, 255, 255);
            for (var r = 0; r < rows.Length; r++)
            {
                // 上の行から並べてあるので、テクスチャの下（y = 0）から数え直す
                var y = (rows.Length - 1 - r) * Zoom;
                for (var c = 0; c < rows[r].Length; c++)
                {
                    if (rows[r][c] != '#') continue;
                    for (var dy = 0; dy < Zoom; dy++)
                        for (var dx = 0; dx < Zoom; dx++)
                            tex.SetPixel(x + c * Zoom + dx, y + dy, white);
                }
            }
        }

        /// <summary>
        /// テクスチャをスプライトアセットに包む。あれば中身だけ入れ替える（GUID を変えない）。
        ///
        /// 大きさは字の大きさに比例させる: 字の大きさ ÷ pointSize × glyph.scale がテクセル 1 つの大きさ。
        /// pointSize は small の高さ（10 画素 × 4）が字の <see cref="Tall"/> em になるように決め、
        /// large は glyph.scale で同じ em の高さに縮める。ascentLine と descentLine は絵の上下の端にしておき、
        /// 行の高さは字の方が決めるようにする
        /// </summary>
        static TMP_SpriteAsset Wrap(Texture2D texture)
        {
            var asset = AssetDatabase.LoadAssetAtPath<TMP_SpriteAsset>(AssetPath);
            var made = asset == null;
            if (made)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(AssetPath));
                asset = ScriptableObject.CreateInstance<TMP_SpriteAsset>();
                AssetDatabase.CreateAsset(asset, AssetPath);
            }
            // 版を空のままにすると、表を引いた時に古い形からの作り直しが走って、足した表が消える
            typeof(TMP_Asset).GetProperty("version").GetSetMethod(true).Invoke(asset, new object[] { "1.1.0" });
            asset.hashCode = TMP_TextUtilities.GetSimpleHashCode(asset.name);
            asset.spriteSheet = texture;

            var point = Small.Length * Zoom / Tall;
            var face = new FaceInfo();
            face.familyName = HudView.ClickAsset;
            face.styleName = "Regular";
            face.pointSize = Mathf.RoundToInt(point);
            face.scale = 1f;
            face.ascentLine = (Tall - Sink) * face.pointSize;
            face.descentLine = -Sink * face.pointSize;
            face.lineHeight = face.ascentLine - face.descentLine;
            face.baseline = 0f;
            asset.faceInfo = face;

            var glyphs = asset.spriteGlyphTable;
            var chars = asset.spriteCharacterTable;
            glyphs.Clear();
            chars.Clear();
            Add(asset, glyphs, chars, "small", Small, 0, 1f);
            // large は small と同じ em の高さへ縮める
            Add(asset, glyphs, chars, "large", Large, Small[0].Length * Zoom + Zoom, (float)Small.Length / Large.Length);

            var material = asset.material;
            if (material == null)
            {
                material = new Material(Shader.Find("TextMeshPro/Sprite"));
                material.name = asset.name + " Material";
                AssetDatabase.AddObjectToAsset(material, asset);
                asset.material = material;
            }
            material.SetTexture(ShaderUtilities.ID_MainTex, texture);
            asset.UpdateLookupTables();
            EditorUtility.SetDirty(material);
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(AssetPath);
            return asset;
        }

        static void Add(TMP_SpriteAsset asset, List<TMP_SpriteGlyph> glyphs, List<TMP_SpriteCharacter> chars,
            string name, string[] rows, int x, float scale)
        {
            var w = rows[0].Length * Zoom;
            var h = rows.Length * Zoom;
            var point = asset.faceInfo.pointSize;
            // 絵の上の端をベースラインの上 (Tall - Sink) em に。glyph.scale で縮むぶんを割り戻す
            var top = (Tall - Sink) * point / scale;
            // 左右に一画素ずつ空ける
            var metrics = new GlyphMetrics(w, h, Zoom, top, w + Zoom * 2);
            var glyph = new TMP_SpriteGlyph((uint)glyphs.Count, metrics, new GlyphRect(x, 0, w, h), scale, 0);
            glyphs.Add(glyph);
            var character = new TMP_SpriteCharacter(0xFFFE, asset, glyph);
            character.name = name;
            character.scale = 1f;
            chars.Add(character);
        }
    }
}
