using NUnit.Framework;
using UnityEngine;

namespace HalfAware.Tests
{
    public sealed class SkyPaintTests
    {
        static SkyPaint.Look Morning()
        {
            return new SkyPaint.Look
            {
                zenith = new Color(0.27f, 0.40f, 0.63f).linear,
                middle = new Color(0.47f, 0.59f, 0.76f).linear,
                horizon = new Color(0.76f, 0.80f, 0.85f).linear,
                whiteDepth = 0.09f,
                warm = new Color(0.97f, 0.85f, 0.70f).linear,
                warmth = 0.75f,
                warmDepth = 0.10f,
                warmFocus = 4f,
                sun = -(Quaternion.Euler(30f, -148f, 0f) * Vector3.forward),
                glow = new Color(1f, 0.88f, 0.70f).linear,
                glowNear = 0.45f,
                glowNearWidth = 4f,
                glowWide = 0.2f,
                glowWideWidth = 26f,
                disc = new Color(1f, 0.97f, 0.9f).linear,
                discRadius = 1.3f,
                discSoft = 0.5f,
                cloudLit = new Color(0.96f, 0.88f, 0.80f).linear,
                cloudShade = new Color(0.66f, 0.68f, 0.75f).linear,
                cloudCover = 0.55f,
                cloudLow = Mathf.Sin(1.5f * Mathf.Deg2Rad),
                cloudHigh = Mathf.Sin(15f * Mathf.Deg2Rad),
                cloudSeed = 7,
            };
        }

        static float Luma(Color c)
        {
            return 0.2126f * c.r + 0.7152f * c.g + 0.0722f * c.b;
        }

        [Test]
        public void TheUvRoundTripsThroughTheDirection()
        {
            for (var u = 0.03f; u < 1f; u += 0.11f)
                for (var v = 0.05f; v < 1f; v += 0.13f)
                {
                    var back = SkyPaint.Uv(SkyPaint.Direction(u, v));
                    Assert.AreEqual(u, back.x, 1e-4f);
                    Assert.AreEqual(v, back.y, 1e-4f);
                }
        }

        [Test]
        public void TheMappingMatchesThePanoramicSkybox()
        {
            // Skybox/Panoramic: u = 0.5 は +x、u = 0.25 は +z、v = 1 は真上
            Assert.Less(Vector3.Distance(Vector3.right, SkyPaint.Direction(0.5f, 0.5f)), 1e-4f);
            Assert.Less(Vector3.Distance(Vector3.forward, SkyPaint.Direction(0.25f, 0.5f)), 1e-4f);
            Assert.Greater(SkyPaint.Direction(0.3f, 0.999f).y, 0.99f);
        }

        [Test]
        public void TheSkyDeepensUpwardAwayFromTheSun()
        {
            var look = Morning();
            var away = -new Vector3(look.sun.x, 0f, look.sun.z).normalized;
            var last = float.MaxValue;
            for (var el = 0f; el <= 90f; el += 10f)
            {
                var d = Quaternion.AngleAxis(-el, Vector3.Cross(Vector3.up, away)) * away;
                var c = SkyPaint.At(d, look);
                var l = Luma(c);
                Assert.LessOrEqual(l, last + 0.02f, "仰角 " + el + " で明るくなった");
                last = l;
                Assert.Greater(c.b, c.r, "日の反対側は青い");
            }
        }

        [Test]
        public void TheBrightestPlaceIsTheSun()
        {
            var look = Morning();
            var sunLuma = Luma(SkyPaint.At(look.sun, look));
            var best = 0f;
            var bestDir = Vector3.zero;
            for (var u = 0.0025f; u < 1f; u += 0.005f)
                for (var v = 0.5f; v < 1f; v += 0.005f)
                {
                    var d = SkyPaint.Direction(u, v);
                    var l = Luma(SkyPaint.At(d, look));
                    if (l > best) { best = l; bestDir = d; }
                }
            Assert.LessOrEqual(best, sunLuma + 1e-4f);
            Assert.Less(Vector3.Angle(bestDir, look.sun), 1.5f);
        }

        [Test]
        public void TheHorizonTowardTheSunIsWarmer()
        {
            var look = Morning();
            var toward = new Vector3(look.sun.x, 0.01f, look.sun.z).normalized;
            var away = new Vector3(-look.sun.x, 0.01f, -look.sun.z).normalized;
            var w = SkyPaint.At(toward, look);
            var c = SkyPaint.At(away, look);
            Assert.Greater(w.r - w.b, c.r - c.b);
        }

        [Test]
        public void CloudsStayInTheirBand()
        {
            var look = Morning();
            for (var az = 0f; az < 360f; az += 7f)
            {
                var flat = new Vector3(Mathf.Sin(az * Mathf.Deg2Rad), 0f, Mathf.Cos(az * Mathf.Deg2Rad));
                Assert.AreEqual(0f, SkyPaint.Cloud((flat + Vector3.up * 0.8f).normalized, look), "天頂に雲");
                Assert.AreEqual(0f, SkyPaint.Cloud((flat - Vector3.up * 0.1f).normalized, look), "地平の下に雲");
            }
            var any = 0f;
            for (var az = 0f; az < 360f; az += 2f)
            {
                var d = Quaternion.Euler(-6f, az, 0f) * Vector3.forward;
                any = Mathf.Max(any, SkyPaint.Cloud(d, look));
            }
            Assert.Greater(any, 0.1f, "帯の中に雲が一つも無い");
        }

        [Test]
        public void CloudsDoNotBreakAtTheSeamOfThePicture()
        {
            var look = Morning();
            // u = 0 と u = 1 は同じ方角（-x）。左右の端で雲の濃さが揃う
            for (var v = 0.51f; v < 0.6f; v += 0.01f)
                Assert.AreEqual(SkyPaint.Cloud(SkyPaint.Direction(0f, v), look),
                    SkyPaint.Cloud(SkyPaint.Direction(1f, v), look), 1e-4f);
        }

        [Test]
        public void HazeFollowsTheExponentialSquaredCurve()
        {
            Assert.AreEqual(0f, PlaceSky.Haze(0.003f, 0f), 1e-6f);
            Assert.AreEqual(1f - Mathf.Exp(-0.36f), PlaceSky.Haze(0.003f, 200f), 1e-5f);
            Assert.Less(PlaceSky.Haze(0.003f, 25f), 0.01f, "隣の棟まで霞んでいる");
        }
    }
}
