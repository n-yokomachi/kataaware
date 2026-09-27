using NUnit.Framework;
using UnityEngine;

namespace HalfAware.Tests
{
    /// <summary>
    /// 人の脇の板は、遠い人でも見かけの大きさを保つ（HoloPanel.Beside）。
    /// 3.2 m より遠い人の脇にそのまま置くと距離なりに縮み、7 m 先の公園の少女では粗い画面で読めなかった（2026-09-27）
    /// </summary>
    public class HoloPanelSizeTests
    {
        /// <summary>場面の物から離れた、何も無い所</summary>
        static readonly Vector3 Yard = new Vector3(5200f, 0f, 5200f);

        static float Apparent(float far, out float angle)
        {
            var made = new System.Collections.Generic.List<GameObject>();
            try
            {
                var eye = new GameObject("HoloPanelSizeTests.Eye");
                made.Add(eye);
                // 目は相手の肩（背 1.6 m の 0.82）と同じ高さに置き、左右の角だけを比べる
                eye.transform.SetPositionAndRotation(Yard + Vector3.up * 1.31f, Quaternion.identity);
                var host = new GameObject("HoloPanelSizeTests.Host");
                made.Add(host);
                host.transform.position = Yard + new Vector3(0f, 0f, far);
                var shape = GameObject.CreatePrimitive(PrimitiveType.Cube);
                made.Add(shape);
                Object.DestroyImmediate(shape.GetComponent<Collider>());
                shape.transform.SetParent(host.transform, false);
                shape.transform.localScale = new Vector3(0.45f, 1.6f, 0.3f);
                shape.transform.localPosition = new Vector3(0f, 0.8f, 0f);
                var go = new GameObject("HoloPanelSizeTests.Panel");
                made.Add(go);
                var panel = go.AddComponent<HoloPanel>();
                typeof(HoloPanel).GetField("eye", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                    .SetValue(panel, eye.transform);
                Physics.SyncTransforms();
                panel.Show(host.transform, "女　18　『プリヤ』", "");
                var at = go.transform.position - eye.transform.position;
                var flatAt = new Vector3(at.x, 0f, at.z);
                var toHost = host.transform.position - eye.transform.position;
                angle = Vector3.Angle(flatAt, new Vector3(toHost.x, 0f, toHost.z));
                return go.transform.localScale.x / at.magnitude;
            }
            finally
            {
                foreach (var m in made) if (m != null) Object.DestroyImmediate(m);
            }
        }

        [Test]
        public void AFarBoardIsAsBigToTheEyeAsANearOne()
        {
            float nearAngle, farAngle, fartherAngle;
            var near = Apparent(2.5f, out nearAngle);
            var far = Apparent(7f, out farAngle);
            var farther = Apparent(12f, out fartherAngle);
            Assert.That(far, Is.EqualTo(near).Within(near * 0.12f), "7 m 先の人の板も、2.5 m の人の板と同じ見かけの大きさ");
            Assert.That(farther, Is.EqualTo(near).Within(near * 0.12f), "12 m 先でも縮まない");
            // 画面の上では、相手の脇に出る（相手から離れて宙に浮かない）
            Assert.That(farAngle, Is.EqualTo(nearAngle).Within(6f), "遠くても相手の脇に出る");
        }

        // ---- 画面の真ん中の下の案内を避ける --------------------------------------------

        [Test]
        public void ABoardOverThePromptIsLiftedAboveIt()
        {
            var band = HoloPanel.PromptBand;
            // 二行目が案内の帯に掛かる板
            var board = Rect.MinMaxRect(0.55f, band.yMin + 0.01f, 0.75f, band.yMax + 0.05f);
            var lift = HoloPanel.Lift(board, band, HoloPanel.PromptMargin);
            Assert.That(lift, Is.GreaterThan(0f));
            Assert.That(board.yMin + lift, Is.EqualTo(band.yMax + HoloPanel.PromptMargin).Within(1e-5f), "持ち上げた板の下の縁は、帯の上の縁と余白の上");
        }

        [Test]
        public void ABoardClearOfThePromptStays()
        {
            var band = HoloPanel.PromptBand;
            // 帯より上
            Assert.AreEqual(0f, HoloPanel.Lift(Rect.MinMaxRect(0.55f, band.yMax + 0.05f, 0.75f, band.yMax + 0.15f), band, HoloPanel.PromptMargin));
            // 帯より下
            Assert.AreEqual(0f, HoloPanel.Lift(Rect.MinMaxRect(0.55f, 0.1f, 0.75f, band.yMin - 0.05f), band, HoloPanel.PromptMargin));
            // 帯の横（画面の端）
            Assert.AreEqual(0f, HoloPanel.Lift(Rect.MinMaxRect(band.xMax + 0.01f, band.yMin, 0.99f, band.yMax), band, HoloPanel.PromptMargin));
        }

        [Test]
        public void ThePromptBandSitsJustBelowTheMiddle()
        {
            var band = HoloPanel.PromptBand;
            Assert.That(band.center.x, Is.EqualTo(0.5f).Within(1e-5f));
            Assert.That(band.yMax, Is.LessThan(0.5f));
            Assert.That(band.yMin, Is.GreaterThan(0.35f));
        }
    }
}
