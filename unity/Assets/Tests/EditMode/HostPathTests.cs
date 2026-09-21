using NUnit.Framework;
using UnityEngine;

namespace HalfAware.Tests
{
    public sealed class HostPathTests
    {
        static HostKey[] TwoKeys()
        {
            return new[]
            {
                new HostKey { at = 0f, position = new Vector3(0f, 0f, 0f), yaw = 0f, pitch = 0f, eyeHeight = 1.5f },
                new HostKey { at = 10f, position = new Vector3(10f, 0f, 0f), yaw = 90f, pitch = 20f, eyeHeight = 1.7f },
            };
        }

        [Test]
        public void BeforeTheFirstKeyReturnsTheFirstKey()
        {
            var keys = TwoKeys();
            var at = HostPath.At(keys, -5f);
            Assert.AreEqual(keys[0].position, at.position);
            Assert.AreEqual(keys[0].yaw, at.yaw);
            Assert.AreEqual(keys[0].eyeHeight, at.eyeHeight);
        }

        [Test]
        public void AfterTheLastKeyReturnsTheLastKey()
        {
            var keys = TwoKeys();
            var at = HostPath.At(keys, 100f);
            Assert.AreEqual(keys[1].position, at.position);
            Assert.AreEqual(keys[1].yaw, at.yaw);
            Assert.AreEqual(keys[1].eyeHeight, at.eyeHeight);
        }

        [Test]
        public void TheExactMidpointMatchesLinearInterpolation()
        {
            // SmoothStep(0, 1, 0.5) はちょうど 0.5 になる（3x^2-2x^3 の対称性）ので、
            // 真ん中の一点だけは滑らかな補間と単純な線形補間が一致する
            var keys = TwoKeys();
            var at = HostPath.At(keys, 5f);
            Assert.AreEqual(new Vector3(5f, 0f, 0f), at.position);
            Assert.AreEqual(10f, at.pitch, 1e-4f);
            Assert.AreEqual(1.6f, at.eyeHeight, 1e-4f);
        }

        [Test]
        public void TurningGoesTheShortWayAround()
        {
            var keys = new[]
            {
                new HostKey { at = 0f, yaw = 350f },
                new HostKey { at = 10f, yaw = 10f },
            };
            var at = HostPath.At(keys, 5f);
            // 単純な線形補間なら 180 度（後ろ向き）に化ける。短い方（0 度付近）へ回ることを確かめる
            Assert.AreEqual(0f, Mathf.DeltaAngle(at.yaw, 0f), 1e-3f);
        }

        [Test]
        public void LengthIsTheLastKeysAt()
        {
            Assert.AreEqual(10f, HostPath.Length(TwoKeys()));
        }

        [Test]
        public void LengthOfAnEmptyArrayIsZero()
        {
            Assert.AreEqual(0f, HostPath.Length(new HostKey[0]));
        }
    }
}
