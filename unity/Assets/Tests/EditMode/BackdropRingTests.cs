using NUnit.Framework;
using UnityEngine;

namespace HalfAware.Tests
{
    public sealed class BackdropRingTests
    {
        const int Count = 16;
        const float Apothem = 200f;
        const float Bottom = -16.6f;
        const float Top = 56f;
        const float Eye = 4.42f;

        [Test]
        public void TheFrustumSeesExactlyThePanelCorners()
        {
            var proj = BackdropRing.Frustum(Apothem, Count, Bottom, Top, Eye, 5f, 3000f);
            var centre = new Vector3(3.4f, 0f, -14.1f);
            for (var i = 0; i < Count; i++)
            {
                var corners = BackdropRing.Corners(centre, Apothem, Count, i, Bottom, Top);
                var view = Quaternion.Inverse(BackdropRing.Facing(i, Count));
                var eye = new Vector3(centre.x, Eye, centre.z);
                var expect = new[] { new Vector2(-1f, -1f), new Vector2(1f, -1f), new Vector2(1f, 1f), new Vector2(-1f, 1f) };
                for (var k = 0; k < 4; k++)
                {
                    // Unity のカメラは -z を向く右手系の視点空間で射影するので、z を裏返して渡す
                    var local = view * (corners[k] - eye);
                    var clip = proj * new Vector4(local.x, local.y, -local.z, 1f);
                    Assert.AreEqual(expect[k].x, clip.x / clip.w, 1e-4f, "板 " + i + " の隅 " + k + " の x");
                    Assert.AreEqual(expect[k].y, clip.y / clip.w, 1e-4f, "板 " + i + " の隅 " + k + " の y");
                }
            }
        }

        [Test]
        public void NeighbouringPanelsShareTheirEdges()
        {
            var centre = Vector3.zero;
            for (var i = 0; i < Count; i++)
            {
                var a = BackdropRing.Corners(centre, Apothem, Count, i, Bottom, Top);
                var b = BackdropRing.Corners(centre, Apothem, Count, (i + 1) % Count, Bottom, Top);
                Assert.Less(Vector3.Distance(a[1], b[0]), 1e-3f, "板 " + i + " の右下と次の左下");
                Assert.Less(Vector3.Distance(a[2], b[3]), 1e-3f, "板 " + i + " の右上と次の左上");
            }
        }

        [Test]
        public void CornersSitOnTheCircumscribedCircle()
        {
            var c = BackdropRing.Corners(Vector3.zero, Apothem, Count, 3, Bottom, Top);
            var reach = new Vector2(c[0].x, c[0].z).magnitude;
            Assert.AreEqual(Apothem / Mathf.Cos(Mathf.PI / Count), reach, 1e-3f);
            Assert.AreEqual(BackdropRing.Width(Apothem, Count), Vector3.Distance(c[0], c[1]), 1e-3f);
        }

        [Test]
        public void TheFirstPanelFacesNorthAndTheRightEdgeIsEastward()
        {
            var c = BackdropRing.Corners(Vector3.zero, Apothem, Count, 0, Bottom, Top);
            Assert.Greater(c[0].z, 0f);
            Assert.Less(c[0].x, 0f, "左の縁は西");
            Assert.Greater(c[1].x, 0f, "右の縁は東");
        }

        [Test]
        public void AlongReachesTheApothemStraightOut()
        {
            var d = BackdropRing.Along(Vector3.zero, BackdropRing.Heading(BackdropRing.Azimuth(5, Count)), Vector3.zero, Apothem, Count);
            Assert.AreEqual(Apothem, d, 1e-3f);
            var corner = BackdropRing.Along(Vector3.zero, BackdropRing.Heading(180f / Count), Vector3.zero, Apothem, Count);
            Assert.AreEqual(BackdropRing.Corner(Apothem, Count), corner, 1e-2f);
        }

        [Test]
        public void TheSeamIsFlushWhereItWasShot()
        {
            var centre = new Vector3(3.4f, 0f, -14.1f);
            var at = new Vector3(centre.x, Eye, centre.z);
            for (var az = 0f; az < 360f; az += 45f)
                Assert.AreEqual(0f, BackdropRing.Seam(at, az, centre, Eye, 80f, Apothem, Count, 385.6f), 1e-3f, "方角 " + az);
        }

        [Test]
        public void AHigherEyeSeesTheDrawnGroundAboveTheRealEdge()
        {
            var centre = Vector3.zero;
            var high = BackdropRing.Seam(new Vector3(0f, 7.22f, 0f), 0f, centre, Eye, 80f, Apothem, Count, 385.6f);
            var low = BackdropRing.Seam(new Vector3(0f, 1.62f, 0f), 0f, centre, Eye, 80f, Apothem, Count, 385.6f);
            Assert.Greater(high, 0f, "高い目からは、書き割りの境目が本物の縁より上に来る（隙間）");
            Assert.Less(low, 0f, "低い目からは、本物の地面が書き割りに被る");
        }

        [Test]
        public void GroundThatReachesThePanelMeetsItsFoot()
        {
            Assert.AreEqual(0f, BackdropRing.GroundLine(Eye, Apothem, Apothem), 1e-5f);
            Assert.Less(BackdropRing.GroundLine(Eye, 80f, Apothem), 0f);
        }
    }
}
