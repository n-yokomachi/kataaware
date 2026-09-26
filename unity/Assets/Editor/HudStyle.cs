using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace HalfAware.EditorTools
{
    /// <summary>
    /// 場面の Hud の字幕を、ノベルの枠（設計書 2 節、案 2）に組む。どの場面の Hud も同じ形にする。
    ///
    /// - 字幕の地: 黒い帯をやめ、画面の下から上へ薄れる黒のグラデーション（SubtitleShade.png）
    /// - 左に寄せて、名前の行（暖かい薄茶）と台詞の行（白）を分ける
    /// - 送れる時だけ右下に「E　送る ▼」
    /// - Tab の字のログ（LogPanel）は外す。ログは TAB のコンソール（<see cref="ImplantConsole"/>）が持つ
    ///
    /// **寸法は粗い画面（<see cref="UiLens"/>、既定で画面の 1/2）の 1 画素 = Dot で決める。**
    /// 字はいちばん小さいものでも 11 Dot（粗い画面の中で縦 10 画素ほど）。印（E　調べる）と
    /// 場面 4 の右上の行も同じ粗い画面で描くので、同じ下限まで上げる。
    /// 中央の文字（冒頭のカード・「続く」）は粗くしないので触らない。
    ///
    /// 場面の組み立て（<see cref="BuildDrive.Screen"/>・BuildDive の Screen）が Hud を作った後にこれを通す。
    /// 組み立てを通さずに Hud だけ組み直すときは、メニューから開いている場面へ当てて保存する
    /// </summary>
    public static class HudStyle
    {
        public const string ShadePath = "Assets/Textures/Hud/SubtitleShade.png";

        /// <summary>粗い画面の 1 画素。1280×720 のキャンバスで、既定の粗さ（1/2）のとき</summary>
        const float Dot = 1280f / 480f;

        const float TextFont = 12f * Dot;
        const float NameFont = 11f * Dot;
        const float HintFont = 11f * Dot;
        const float PromptFont = 11f * Dot;
        const float CaptionFont = 11f * Dot;

        /// <summary>地の上の縁から名前の行まで</summary>
        const float Top = 8f * Dot;
        const float NameHeight = 14f * Dot;
        const float NameGap = 1f * Dot;
        /// <summary>台詞 1 行の高さ。12 Dot の字の素の行送り（1.45 em）</summary>
        const float Row = 17.5f * Dot;
        const float Bottom = 6f * Dot;
        /// <summary>左右の余白。画面の幅に対する割合</summary>
        const float Side = 0.12f;
        const float HintRight = 0.11f;
        const float HintBottom = 4f * Dot;

        /// <summary>E で送る字幕の地より薄い、流れる行の地の濃さ</summary>
        const float PassingAlpha = 0.45f;

        static readonly Color NameColor = new Color(0xe6 / 255f, 0xc7 / 255f, 0xa0 / 255f, 1f);
        public const string Hint = "E　送る ▼";

        [MenuItem("HalfAware/Restyle the HUD in the open scene", false, 280)]
        public static void RestyleMenu()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogError("再生中は組み直さない。止めてからもう一度");
                return;
            }
            var scene = SceneManager.GetActiveScene();
            var hud = Object.FindFirstObjectByType<HudView>(FindObjectsInactive.Include);
            if (hud == null)
            {
                Debug.LogError("開いている場面に Hud が無い: " + scene.path);
                return;
            }
            Apply(hud);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("字幕をノベルの枠に組み直した: " + scene.path);
        }

        /// <summary>hud の字幕・印・右上の行を組み直す。何度通しても同じ形になる</summary>
        public static void Apply(HudView hud)
        {
            var root = hud.transform;
            var so = new SerializedObject(hud);
            var band = (GameObject)so.FindProperty("subtitleBand").objectReferenceValue;
            var text = (TMP_Text)so.FindProperty("subtitleText").objectReferenceValue;
            var prompt = (TMP_Text)so.FindProperty("promptText").objectReferenceValue;
            if (band == null || text == null)
            {
                Debug.LogError("HudStyle: 字幕の地か字が繋がっていない", hud);
                return;
            }
            var font = text.font;

            // Tab の字のログはコンソールへ移った
            var log = root.Find("LogPanel");
            if (log != null) Object.DestroyImmediate(log.gameObject);

            var rect = band.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(0f, Top + NameHeight + NameGap + Row * SubtitleBox.BaseRows + Bottom);
            var shade = band.GetComponent<Image>();
            shade.sprite = Shade();
            shade.type = Image.Type.Simple;
            shade.color = Color.black;
            shade.raycastTarget = false;

            var line = text.rectTransform;
            line.anchorMin = new Vector2(Side, 1f);
            line.anchorMax = new Vector2(1f - Side, 1f);
            line.pivot = new Vector2(0.5f, 1f);
            line.anchoredPosition = new Vector2(0f, -(Top + NameHeight + NameGap));
            line.sizeDelta = new Vector2(0f, Row * SubtitleBox.BaseRows);
            text.fontSize = TextFont;
            text.color = Color.white;
            text.alignment = TextAlignmentOptions.TopLeft;
            text.textWrappingMode = TextWrappingModes.Normal;

            var name = Child(band.transform, "Name", font);
            var nr = name.rectTransform;
            nr.anchorMin = new Vector2(Side, 1f);
            nr.anchorMax = new Vector2(1f - Side, 1f);
            nr.pivot = new Vector2(0.5f, 1f);
            nr.anchoredPosition = new Vector2(0f, -Top);
            nr.sizeDelta = new Vector2(0f, NameHeight);
            name.fontSize = NameFont;
            name.color = NameColor;
            name.alignment = TextAlignmentOptions.TopLeft;

            var hint = Child(band.transform, "Hint", font);
            var hr = hint.rectTransform;
            hr.anchorMin = new Vector2(1f - HintRight, 0f);
            hr.anchorMax = new Vector2(1f - HintRight, 0f);
            hr.pivot = new Vector2(1f, 0f);
            hr.anchoredPosition = new Vector2(0f, HintBottom);
            hr.sizeDelta = new Vector2(120f * Dot, NameHeight);
            hint.fontSize = HintFont;
            hint.color = ImplantConsole.Tint(new Color(1f, 1f, 1f, 0.6f));
            hint.alignment = TextAlignmentOptions.BottomRight;
            hint.text = Hint;
            hint.gameObject.SetActive(false);

            if (prompt != null)
            {
                prompt.fontSize = PromptFont;
                var pr = prompt.rectTransform;
                pr.sizeDelta = new Vector2(pr.sizeDelta.x, Mathf.Max(pr.sizeDelta.y, 16f * Dot));
            }
            // 場面 4 の右上の行（いま潜っている人）
            var caption = root.Find("Caption");
            if (caption != null)
            {
                var ct = caption.GetComponent<TMP_Text>();
                if (ct != null)
                {
                    ct.fontSize = CaptionFont;
                    var cr = ct.rectTransform;
                    cr.sizeDelta = new Vector2(Mathf.Max(cr.sizeDelta.x, 300f * Dot), Mathf.Max(cr.sizeDelta.y, 16f * Dot));
                }
            }

            so.FindProperty("subtitleName").objectReferenceValue = name;
            so.FindProperty("subtitleHint").objectReferenceValue = hint;
            so.FindProperty("subtitleRowHeight").floatValue = Row;
            so.FindProperty("subtitlePadding").floatValue = Top + NameHeight + NameGap + Bottom;
            so.FindProperty("subtitleHead").floatValue = Top + NameHeight + NameGap;
            so.FindProperty("passingAlpha").floatValue = PassingAlpha;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(hud);
        }

        static TMP_Text Child(Transform parent, string name, TMP_FontAsset font)
        {
            var found = parent.Find(name);
            GameObject go;
            if (found != null) go = found.gameObject;
            else
            {
                go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
                go.layer = parent.gameObject.layer;
                go.transform.SetParent(parent, false);
            }
            var t = go.GetComponent<TMP_Text>();
            if (font != null) t.font = font;
            t.raycastTarget = false;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.text = string.Empty;
            return t;
        }

        /// <summary>
        /// 字幕の地の絵。下から 45% までは濃さ 0.82 の黒、そこから上の縁へ向けて 0 まで薄れる（案 2 の CSS）。
        /// 濃さはリニアで重ねたときに CSS と同じ暗さになるよう直して焼く（<see cref="ImplantConsole.Veil"/>）。
        /// 無ければ作る
        /// </summary>
        public static Sprite Shade()
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(ShadePath);
            if (sprite != null) return sprite;
            const int H = 64;
            var tex = new Texture2D(4, H, TextureFormat.RGBA32, false);
            try
            {
                for (var y = 0; y < H; y++)
                {
                    var p = (y + 0.5f) / H;
                    var a = p <= 0.45f ? 0.82f : 0.82f * (1f - (p - 0.45f) / 0.55f);
                    var c = ImplantConsole.Veil(new Color(1f, 1f, 1f, a));
                    for (var x = 0; x < 4; x++) tex.SetPixel(x, y, c);
                }
                tex.Apply();
                Directory.CreateDirectory(Path.GetDirectoryName(ShadePath));
                File.WriteAllBytes(ShadePath, tex.EncodeToPNG());
            }
            finally
            {
                Object.DestroyImmediate(tex);
            }
            AssetDatabase.ImportAsset(ShadePath);
            var importer = (TextureImporter)AssetImporter.GetAtPath(ShadePath);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.alphaIsTransparency = true;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(ShadePath);
        }
    }
}
