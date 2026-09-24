using NUnit.Framework;
using UnityEngine;

namespace HalfAware.Tests
{
    public class SkinPointTests
    {
        GameObject root;

        [TearDown]
        public void TearDown()
        {
            if (root != null) Object.DestroyImmediate(root);
        }

        /// <summary>骨を二本と、肌の一点を作る。点は二本の骨の真ん中の上</summary>
        SkinPoint Make(out Transform a, out Transform b, float weightA)
        {
            root = new GameObject("root");
            a = new GameObject("a").transform;
            a.SetParent(root.transform, false);
            b = new GameObject("b").transform;
            b.SetParent(root.transform, false);
            b.localPosition = new Vector3(0.2f, 0f, 0f);
            var point = new GameObject("point").transform;
            point.SetParent(a, false);
            point.position = new Vector3(0.1f, 0.03f, 0f);
            point.rotation = Quaternion.LookRotation(Vector3.up, Vector3.right);
            var frame = point.localToWorldMatrix;
            var skin = point.gameObject.AddComponent<SkinPoint>();
            skin.Set(new[] { a, b }, new[] { weightA, 1f - weightA },
                new[] { a.worldToLocalMatrix * frame, b.worldToLocalMatrix * frame });
            return skin;
        }

        [Test]
        public void ItStaysPutWhileTheBonesDoNotMove()
        {
            Transform a, b;
            var skin = Make(out a, out b, 0.8f);
            var before = skin.transform.position;
            skin.Follow();
            Assert.Less(Vector3.Distance(before, skin.transform.position), 1e-5f, "組み立てた姿勢では動かない");
        }

        [Test]
        public void OneBoneCarriesItRigidly()
        {
            Transform a, b;
            var skin = Make(out a, out b, 1f);
            var local = a.InverseTransformPoint(skin.transform.position);
            a.rotation = Quaternion.AngleAxis(40f, Vector3.forward);
            a.position = new Vector3(0f, 0.1f, 0f);
            skin.Follow();
            Assert.Less(Vector3.Distance(a.TransformPoint(local), skin.transform.position), 1e-5f, "重みが一本だけなら、その骨と一緒に動く");
        }

        [Test]
        public void TwoBonesMixByTheirWeights()
        {
            Transform a, b;
            var skin = Make(out a, out b, 0.8f);
            var before = skin.transform.position;
            b.position += new Vector3(0f, 0.05f, 0f);
            skin.Follow();
            Assert.AreEqual(before.y + 0.05f * 0.2f, skin.transform.position.y, 1e-5f, "動いた骨の重みの分だけ動く");
            Assert.AreEqual(before.x, skin.transform.position.x, 1e-5f);
        }

        /// <summary>骨の下の差込口の輪（SkinPoint の下）と頭の影は、体の肌として拾わない。骨組みが体のメッシュより先に並んでいても</summary>
        [Test]
        public void BodyOfSkipsWhatRidesOnTheSkinAndTheHeadShadow()
        {
            root = new GameObject("root");
            var bone = new GameObject("bone").transform;
            bone.SetParent(root.transform, false);
            var port = new GameObject("port");
            port.transform.SetParent(bone, false);
            port.AddComponent<SkinPoint>();
            var ring = new GameObject("ring");
            ring.transform.SetParent(port.transform, false);
            var ringSkin = ring.AddComponent<SkinnedMeshRenderer>();
            var shadow = new GameObject("shadow");
            shadow.transform.SetParent(root.transform, false);
            shadow.AddComponent<SkinnedMeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly;
            var body = new GameObject("body");
            body.transform.SetParent(root.transform, false);
            var bodySkin = body.AddComponent<SkinnedMeshRenderer>();

            Assert.AreSame(bodySkin, SkinPoint.BodyOf(root.transform));
            Assert.IsTrue(SkinPoint.Rides(ringSkin));
            Assert.IsFalse(SkinPoint.Rides(bodySkin));
        }
    }
}
