using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace HalfAware.Tests
{
    /// <summary>
    /// 画面のフィルター（設計書 2.5 節・1 節の設定）。設定の値（既定・残して読み直す・知らない符丁・左右と回り・知らせ）、
    /// シェーダーへの渡し方（型と二つの強さのグローバルの値）、色の組・近い二色の表・点の模様とマテリアルの繋ぎ、シェーダーが WebGL（GLES3）で通るか
    /// </summary>
    public class ScreenFilterTests
    {
        MemoryBox box;

        [SetUp]
        public void Swap()
        {
            box = new MemoryBox();
            GameSettings.Box = box;
        }

        [TearDown]
        public void Restore()
        {
            GameSettings.Box = null;
            ScreenFilter.Use(ScreenFilterKind.Standard);
        }

        static SettingChoice Filter { get { return GameSettings.Filter; } }

        // ---- 設定の値 ----------------------------------------------------------

        [Test]
        public void TheFilterIsStandardOrDitherAndStartsStandard()
        {
            Assert.AreEqual("HalfAware.Settings.Filter", Filter.Key);
            CollectionAssert.AreEqual(new[] { "Standard", "Dither" }, Filter.Ids);
            CollectionAssert.AreEqual(new[] { "標準", "減色＋ディザ" }, Filter.Labels);
            Assert.AreEqual(2, Filter.Count);
            Assert.AreEqual((int)ScreenFilterKind.Standard, Filter.Default);
            Assert.AreEqual(0, Filter.Value, "何も書いていなければ標準");
            Assert.AreEqual(ScreenFilterKind.Standard, ScreenFilter.Current);
            Assert.AreEqual("標準", Filter.Text(0));
            Assert.AreEqual("減色＋ディザ", Filter.Text(1));
            // 符丁は型の名と揃える（並びを入れ替えても、残した選びが別の型に化けない）
            foreach (ScreenFilterKind k in Enum.GetValues(typeof(ScreenFilterKind)))
                Assert.AreEqual(k.ToString(), Filter.Ids[(int)k]);
            Assert.Contains(Filter, GameSettings.All, "読み直しと既定に戻すが回る");
        }

        [Test]
        public void TheChoiceIsWrittenAsItsIdAndReadBack()
        {
            Filter.Value = 1;
            Assert.AreEqual("Dither", box.Get(Filter.Key));
            Assert.AreEqual(0, box.Flushes, "書くたびには Flush しない");
            GameSettings.Commit();
            Assert.AreEqual(1, box.Flushes);
            GameSettings.Forget();
            Assert.AreEqual(1, Filter.Value, "起動し直しても残る");
            Assert.AreEqual(ScreenFilterKind.Dither, ScreenFilter.Current);
            box.Set(Filter.Key, "Sepia");
            GameSettings.Forget();
            Assert.AreEqual(0, Filter.Value, "知らない符丁は既定");
            box.Set(Filter.Key, "1");
            GameSettings.Forget();
            Assert.AreEqual(0, Filter.Value, "番号では読まない");
        }

        [Test]
        public void LeftAndRightStopAtTheEndsAndAPressGoesRound()
        {
            Filter.Nudge(-1);
            Assert.AreEqual(0, Filter.Value, "左の端で止まる");
            Filter.Nudge(1);
            Assert.AreEqual(1, Filter.Value);
            Filter.Nudge(5);
            Assert.AreEqual(1, Filter.Value, "右の端で止まる");
            Filter.Cycle();
            Assert.AreEqual(0, Filter.Value, "最後の次は最初へ");
            Filter.Cycle();
            Assert.AreEqual(1, Filter.Value);
            Filter.Value = 9;
            Assert.AreEqual(1, Filter.Value);
            Filter.Value = -3;
            Assert.AreEqual(0, Filter.Value);
        }

        [Test]
        public void ItTellsWhenItChangesAndResetBringsItBack()
        {
            var told = 0;
            Action count = () => told++;
            Filter.Changed += count;
            try
            {
                Filter.Value = 0;
                Assert.AreEqual(0, told, "同じ値では知らせない");
                Filter.Nudge(1);
                Assert.AreEqual(1, told);
                Filter.Nudge(1);
                Assert.AreEqual(1, told, "端で止まって変わらなければ知らせない");
                GameSettings.ResetAll();
                Assert.AreEqual(0, Filter.Value, "既定に戻すで標準へ");
                Assert.AreEqual("Standard", box.Get(Filter.Key));
                Assert.AreEqual(2, told);
                box.Set(Filter.Key, "Dither");
                GameSettings.Forget();
                Assert.AreEqual(1, Filter.Value);
                Assert.AreEqual(2, told, "鍵から読み直しただけでは知らせない");
            }
            finally
            {
                Filter.Changed -= count;
            }
        }

        // ---- シェーダーへの渡し方 ----------------------------------------------

        [Test]
        public void TheKindReachesTheShadersAsAGlobal()
        {
            ScreenFilter.Use(ScreenFilterKind.Dither);
            Assert.AreEqual(1f, Shader.GetGlobalFloat(ScreenFilter.GlobalName));
            Assert.AreEqual(ScreenFilterKind.Dither, ScreenFilter.InEffect);
            ScreenFilter.Use(ScreenFilterKind.Standard);
            Assert.AreEqual(0f, Shader.GetGlobalFloat(ScreenFilter.GlobalName));
            Assert.AreEqual(ScreenFilterKind.Standard, ScreenFilter.InEffect);
            // マテリアルに同じ名の値を持たせない。持たせるとグローバルの値よりマテリアルの値が勝つ
            foreach (var name in new[] { "HalfAware/Ps1", "HalfAware/FilteredPicture" })
            {
                var shader = Shader.Find(name);
                Assert.IsNotNull(shader, name);
                foreach (var global in new[] { ScreenFilter.GlobalName, ScreenFilter.TintName, ScreenFilter.DotsName })
                    Assert.AreEqual(-1, shader.FindPropertyIndex(global), name + ": " + global);
            }
        }

        [Test]
        public void TheStrengthsReachTheShadersWithTheKind()
        {
            Assert.AreEqual("_HaFilterTint", ScreenFilter.TintName);
            Assert.AreEqual("_HaFilterDots", ScreenFilter.DotsName);
            // 値を直に渡す形（撮り比べ）。0〜1 に収める
            ScreenFilter.Use(ScreenFilterKind.Dither, 0.3f, 0.7f);
            Assert.AreEqual(1f, Shader.GetGlobalFloat(ScreenFilter.GlobalName));
            Assert.AreEqual(0.3f, Shader.GetGlobalFloat(ScreenFilter.TintName), 1e-6f);
            Assert.AreEqual(0.7f, Shader.GetGlobalFloat(ScreenFilter.DotsName), 1e-6f);
            ScreenFilter.Use(ScreenFilterKind.Dither, -1f, 3f);
            Assert.AreEqual(0f, Shader.GetGlobalFloat(ScreenFilter.TintName));
            Assert.AreEqual(1f, Shader.GetGlobalFloat(ScreenFilter.DotsName));
            // 型だけ渡すと既定の強さ
            ScreenFilter.Use(ScreenFilterKind.Dither);
            Assert.AreEqual(ScreenFilter.DefaultTint, Shader.GetGlobalFloat(ScreenFilter.TintName), 1e-6f);
            Assert.AreEqual(ScreenFilter.DefaultDots, Shader.GetGlobalFloat(ScreenFilter.DotsName), 1e-6f);
            // 設定から効かせると、設定の強さ
            GameSettings.Filter.Value = (int)ScreenFilterKind.Dither;
            GameSettings.FilterTint.Value = 0.25f;
            GameSettings.FilterDots.Value = 0.8f;
            ScreenFilter.Apply();
            Assert.AreEqual(ScreenFilterKind.Dither, ScreenFilter.InEffect);
            Assert.AreEqual(0.25f, Shader.GetGlobalFloat(ScreenFilter.TintName), 1e-6f);
            Assert.AreEqual(0.8f, Shader.GetGlobalFloat(ScreenFilter.DotsName), 1e-6f);
        }

        [Test]
        public void InPlayTheSettingTakesEffectAtOnceAndLeavingGoesBackToStandard()
        {
            const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static;
            var boot = typeof(ScreenFilter).GetMethod("Boot", flags);
            var leave = typeof(ScreenFilter).GetMethod("Leave", flags);
            Assert.IsNotNull(boot);
            Assert.IsNotNull(leave);
            box.Set(Filter.Key, "Dither");
            GameSettings.Forget();
            try
            {
                // 遊び始めに残してあった型を入れる
                boot.Invoke(null, null);
                Assert.AreEqual(ScreenFilterKind.Dither, ScreenFilter.InEffect);
                // 二度呼ばれても（ドメインを読み直さない設定）繋ぎは一つ
                boot.Invoke(null, null);
                Filter.Nudge(-1);
                Assert.AreEqual(ScreenFilterKind.Standard, ScreenFilter.InEffect, "設定で動かすとその場で効く");
                Filter.Cycle();
                Assert.AreEqual(ScreenFilterKind.Dither, ScreenFilter.InEffect);
                GameSettings.ResetAll();
                Assert.AreEqual(ScreenFilterKind.Standard, ScreenFilter.InEffect, "既定に戻すでも効く");
                Filter.Value = 1;
                // 強さのつまみも、動かすとその場で効く
                GameSettings.FilterTint.Value = 0.3f;
                Assert.AreEqual(0.3f, Shader.GetGlobalFloat(ScreenFilter.TintName), 1e-6f);
                GameSettings.FilterDots.Value = 0.5f;
                GameSettings.FilterDots.Nudge(2);
                Assert.AreEqual(0.6f, Shader.GetGlobalFloat(ScreenFilter.DotsName), 1e-6f);
                Assert.AreEqual(ScreenFilterKind.Dither, ScreenFilter.InEffect);
            }
            finally
            {
                leave.Invoke(null, null);
            }
            Assert.AreEqual(ScreenFilterKind.Standard, ScreenFilter.InEffect, "遊び終えたらエディタは標準");
            Filter.Value = 0;
            Filter.Value = 1;
            Assert.AreEqual(ScreenFilterKind.Standard, ScreenFilter.InEffect, "遊び終えた後は設定を動かしても画面は変わらない");
            GameSettings.FilterTint.Value = 0.9f;
            Assert.AreEqual(ScreenFilter.DefaultTint, Shader.GetGlobalFloat(ScreenFilter.TintName), 1e-6f, "強さのつまみも繋がっていない");
        }

        // ---- 色の組・点の模様・マテリアル --------------------------------------

        static Type Builder
        {
            get
            {
                var t = AppDomain.CurrentDomain.GetAssemblies()
                    .Select(a => a.GetType("HalfAware.EditorTools.BuildScreenFilter"))
                    .FirstOrDefault(x => x != null);
                Assert.IsNotNull(t, "BuildScreenFilter が無い");
                return t;
            }
        }

        static string Const(string name)
        {
            return (string)Builder.GetField(name).GetRawConstantValue();
        }

        [Test]
        public void ThePaletteTextureIsWhatTheBuilderMakes()
        {
            var palette = (string[])Builder.GetField("Palette").GetValue(null);
            Assert.LessOrEqual(palette.Length, 64, "色の組のテクスチャの幅に収める（番号は表の 8 bit にも入る）");
            Assert.AreEqual(palette.Length, palette.Distinct().Count(), "同じ色を二度入れない");
            CollectionAssert.Contains(palette, "ffffff", "明るい所は白く飛ぶ");
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(Const("PalettePath"));
            Assert.IsNotNull(tex);
            Assert.AreEqual(TextureFormat.RGBAHalf, tex.format);
            Assert.AreEqual(FilterMode.Point, tex.filterMode);
            var want = (Color[])Builder.GetMethod("PalettePixels").Invoke(null, null);
            var got = tex.GetPixels();
            Assert.AreEqual(want.Length, got.Length);
            for (var i = 0; i < want.Length; i++)
                for (var k = 0; k < 4; k++)
                    Assert.AreEqual(want[i][k], got[i][k], 2e-3f,
                        "色の組を変えたら HalfAware/Build the screen filter で作り直す（" + i + "）");
        }

        [Test]
        public void TheNearPairTableIsWhatTheBuilderMakes()
        {
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);
            try
            {
                Assert.IsTrue(tex.LoadImage(File.ReadAllBytes(Const("LutPath"))));
                Assert.AreEqual(512, tex.width);
                Assert.AreEqual(512, tex.height);
                var want = (Color32[])Builder.GetMethod("LutPixels").Invoke(null, null);
                var got = tex.GetPixels32();
                Assert.AreEqual(want.Length, got.Length);
                var off = 0;
                for (var i = 0; i < want.Length; i++)
                    if (want[i].r != got[i].r || want[i].g != got[i].g) off++;
                Assert.AreEqual(0, off, "色の組を変えたら HalfAware/Build the screen filter で作り直す");
                // 二色はいつも別の色
                Assert.IsTrue(got.All(c => c.r != c.g));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(tex);
            }
            var imp = (TextureImporter)AssetImporter.GetAtPath(Const("LutPath"));
            Assert.IsFalse(imp.sRGBTexture, "番号はリニアのまま読む");
            Assert.IsFalse(imp.mipmapEnabled);
            Assert.AreEqual(FilterMode.Point, imp.filterMode);
            Assert.AreEqual(TextureImporterCompression.Uncompressed, imp.textureCompression);
        }

        [Test]
        public void TheNoiseIsAnEvenSpreadOfThresholds()
        {
            var path = Const("NoisePath");
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);
            try
            {
                Assert.IsTrue(tex.LoadImage(File.ReadAllBytes(path)));
                Assert.AreEqual(64, tex.width);
                Assert.AreEqual(64, tex.height);
                // 0〜255 のどの値も 16 画素ずつ。閾値が偏らない
                var counts = new int[256];
                foreach (var c in tex.GetPixels32()) counts[c.r]++;
                Assert.IsTrue(counts.All(n => n == 16));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(tex);
            }
            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            Assert.IsFalse(imp.sRGBTexture, "閾値はリニアのまま読む");
            Assert.IsFalse(imp.mipmapEnabled);
            Assert.AreEqual(FilterMode.Point, imp.filterMode);
            Assert.AreEqual(TextureWrapMode.Repeat, imp.wrapMode);
            Assert.AreEqual(TextureImporterCompression.Uncompressed, imp.textureCompression);
        }

        [Test]
        public void BothMaterialsCarryTheNoiseThePaletteAndTheTable()
        {
            var ps1 = AssetDatabase.LoadAssetAtPath<Material>(Const("Ps1Path"));
            var picture = Resources.Load<Material>(ScreenFilter.PictureMaterial);
            Assert.IsNotNull(picture, "タイトルの画面の背景のマテリアルが Resources に無い");
            Assert.AreEqual("HalfAware/FilteredPicture", picture.shader.name);
            foreach (var pair in new[] { new[] { "NoisePath", "NoiseProperty" }, new[] { "PalettePath", "PaletteProperty" }, new[] { "LutPath", "LutProperty" } })
            {
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(Const(pair[0]));
                Assert.IsNotNull(tex, pair[0]);
                Assert.AreSame(tex, ps1.GetTexture(Const(pair[1])), "Ps1: " + pair[1]);
                Assert.AreSame(tex, picture.GetTexture(Const(pair[1])), "背景: " + pair[1]);
            }
        }

        [Test]
        public void TheShadersCompileForWebGL()
        {
            foreach (var name in new[] { "HalfAware/Ps1", "HalfAware/FilteredPicture" })
            {
                var shader = Shader.Find(name);
                Assert.IsFalse(ShaderUtil.ShaderHasError(shader), name);
                var pass = ShaderUtil.GetShaderData(shader).GetSubshader(0).GetPass(0);
                var info = pass.CompileVariant(UnityEditor.Rendering.ShaderType.Vertex, new string[0],
                    UnityEditor.Rendering.ShaderCompilerPlatform.GLES3x, BuildTarget.WebGL);
                Assert.IsTrue(info.Success, name + ": " + string.Join(" / ", info.Messages.Select(m => m.message)));
            }
        }
    }
}
