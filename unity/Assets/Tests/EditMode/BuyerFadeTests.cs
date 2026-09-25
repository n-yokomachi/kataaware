using NUnit.Framework;
using UnityEngine;

namespace HalfAware.Tests
{
    public class BuyerFadeTests
    {
        GameObject go;
        Material solid, fade;

        [SetUp]
        public void Make()
        {
            go = new GameObject("buyer");
            go.AddComponent<MeshRenderer>();
            solid = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            fade = new Material(solid);
            fade.SetColor("_BaseColor", new Color(0.35f, 0.35f, 0.35f, 1f));
            go.GetComponent<MeshRenderer>().sharedMaterials = new[] { solid };
            go.AddComponent<BuyerFade>().Bind(new[] { solid }, new[] { fade });
        }

        [TearDown]
        public void Clear()
        {
            Object.DestroyImmediate(go);
            Object.DestroyImmediate(solid);
            Object.DestroyImmediate(fade);
        }

        [Test]
        public void WhileAppearingItIsSeeThrough()
        {
            var r = go.GetComponent<MeshRenderer>();
            go.GetComponent<BuyerFade>().Set(0.4f);
            Assert.That(r.sharedMaterials[0], Is.SameAs(fade), "浮かび上がるあいだは透かせるマテリアル");
            var block = new MaterialPropertyBlock();
            r.GetPropertyBlock(block);
            var c = block.GetColor("_BaseColor");
            Assert.That(c.a, Is.EqualTo(0.4f).Within(1e-4f), "濃さは α で持つ");
            Assert.That(c.r, Is.EqualTo(0.35f).Within(1e-4f), "色は透かせるマテリアルの色のまま");
        }

        [Test]
        public void OnceFullyShownItIsSolidAgain()
        {
            var r = go.GetComponent<MeshRenderer>();
            var f = go.GetComponent<BuyerFade>();
            f.Set(0f);
            f.Set(1f);
            Assert.That(r.sharedMaterials[0], Is.SameAs(solid), "濃さを上げきったら透かさないマテリアルへ戻す（深さを書くため）");
            Assert.That(r.HasPropertyBlock(), Is.False, "上書きも外す");
        }
    }
}
