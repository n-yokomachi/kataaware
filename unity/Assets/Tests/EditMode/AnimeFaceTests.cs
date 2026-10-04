using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace HalfAware.Tests
{
    /// <summary>
    /// 同じ顔の三人（主人公・片割れ・場面 6 の過去の主人公）の、リアル系のアニメ寄りの顔（Assets/Editor/Rocketbox/RocketboxAnimeFace.cs）。
    /// シェーダーが WebGL（GLES3）で通るか、三人のマテリアルが AnimeSkin と描いた絵を使うか、頭のメッシュに顔の比率（目を 1 割大きく）が焼けているか
    /// </summary>
    public class AnimeFaceTests
    {
        const string Shader = "HalfAware/AnimeSkin";
        const string Root = "Assets/Models/rocketbox/";
        static readonly string[] People = { "Face14_Hair14_BodySports02", "Face14_Hair14_MadeDress", "Face14_Hair14_GardenWear" };

        [Test]
        public void TheShaderCompilesForWebGL()
        {
            var shader = UnityEngine.Shader.Find(Shader);
            Assert.IsNotNull(shader, Shader);
            Assert.IsFalse(ShaderUtil.ShaderHasError(shader), Shader);
            var data = ShaderUtil.GetShaderData(shader).GetSubshader(0);
            // 色の pass は、主灯の影・追加の灯り・霧・α で抜くの組で。影と深さの pass も
            var sets = new[]
            {
                new string[0],
                new[] { "_MAIN_LIGHT_SHADOWS", "_ADDITIONAL_LIGHTS", "FOG_EXP2" },
                new[] { "_MAIN_LIGHT_SHADOWS_CASCADE", "_ADDITIONAL_LIGHTS", "_SHADOWS_SOFT", "_ALPHATEST_ON" },
            };
            for (var p = 0; p < data.PassCount; p++)
            {
                var pass = data.GetPass(p);
                foreach (var keys in sets)
                    foreach (var stage in new[] { UnityEditor.Rendering.ShaderType.Vertex, UnityEditor.Rendering.ShaderType.Fragment })
                    {
                        var info = pass.CompileVariant(stage, keys, UnityEditor.Rendering.ShaderCompilerPlatform.GLES3x, BuildTarget.WebGL);
                        Assert.IsTrue(info.Success, pass.Name + " " + stage + " [" + string.Join(",", keys) + "]: " + string.Join(" / ", info.Messages.Select(m => m.message)));
                    }
            }
        }

        [Test]
        public void TheThreePeopleUseTheAnimeSkin()
        {
            foreach (var person in People)
            {
                var dir = Root + person + "/Painted/";
                foreach (var part in new[] { "Head_self", "Hair", "Body", "Chest", "Legs" })
                {
                    var m = AssetDatabase.LoadAssetAtPath<Material>(dir + part + ".mat");
                    if (m == null && (part == "Chest" || part == "Legs")) continue;
                    Assert.IsNotNull(m, dir + part);
                    Assert.AreEqual(Shader, m.shader.name, dir + part);
                    var stem = part == "Head_self" ? "Head" : part;
                    Assert.AreSame(AssetDatabase.LoadAssetAtPath<Texture2D>(dir + stem + "_anime.png"), m.GetTexture("_BaseMap"), dir + part + " の色の絵");
                    Assert.AreSame(AssetDatabase.LoadAssetAtPath<Texture2D>(dir + stem + "_anime_mask.png"), m.GetTexture("_MaskMap"), dir + part + " の部位の絵");
                    Assert.AreEqual(part == "Hair", m.IsKeywordEnabled("_ALPHATEST_ON"), dir + part + " を α で抜くか");
                    // 名前は組み立てが頭の面を見分けるのに使う（PlaceProtagonist.IsHead）
                    Assert.AreEqual(part, m.name);
                }
                // 部位の絵は線形で読む
                var mask = (TextureImporter)AssetImporter.GetAtPath(dir + "Head_anime_mask.png");
                Assert.IsFalse(mask.sRGBTexture, person + " の部位の絵");
            }
        }

        [Test]
        public void TheHeadMeshHasTheEyesEnlarged()
        {
            foreach (var person in People)
            {
                var baked = AssetDatabase.LoadAssetAtPath<Mesh>(Root + person + "/" + person + "_nose_mesh.asset");
                var source = AssetDatabase.LoadAssetAtPath<Mesh>(Root + person + "/" + person + "_mesh.asset");
                Assert.IsNotNull(baked, person);
                Assert.IsNotNull(source, person);
                Assert.AreEqual(source.vertexCount, baked.vertexCount, person);
                // 骨ごとに、重み 1 で動く頂点（骨の位置から 2 cm 以内）の広がりを比べる。1.05 倍より広がった骨が、左右の目の二本だけで、1.10 倍ほど（表は 1.10・1.10・1.04）
                var grown = Grown(source.vertices, baked.vertices, source.boneWeights, source.bindposes);
                Assert.AreEqual(2, grown.Count, person + " の広がった骨の数");
                foreach (var r in grown) Assert.That(r, Is.InRange(1.05f, 1.12f), person + " の目の広がり " + r);
            }
        }

        /// <summary>重み 1 の頂点を持つ骨ごとの、骨の位置から 2 cm 以内の頂点の平均の離れの比（後 / 前）のうち、1.05 より大きい物</summary>
        static System.Collections.Generic.List<float> Grown(Vector3[] before, Vector3[] after, BoneWeight[] w, Matrix4x4[] bind)
        {
            var list = new System.Collections.Generic.List<float>();
            for (var bone = 0; bone < bind.Length; bone++)
            {
                var centre = bind[bone].inverse.MultiplyPoint3x4(Vector3.zero);
                float a = 0f, b = 0f;
                var n = 0;
                for (var i = 0; i < w.Length; i++)
                {
                    if (w[i].weight0 <= 0.999f || w[i].boneIndex0 != bone) continue;
                    var d = (before[i] - centre).magnitude;
                    if (d > 0.02f || d < 0.002f) continue;
                    a += d;
                    b += (after[i] - centre).magnitude;
                    n++;
                }
                if (n >= 20 && b / a > 1.05f) list.Add(b / a);
            }
            return list;
        }
    }
}
