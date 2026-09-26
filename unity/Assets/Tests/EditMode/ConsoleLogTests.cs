using NUnit.Framework;

namespace HalfAware.Tests
{
    public class ConsoleLogTests
    {
        [Test]
        public void ItKeepsTheOrderTheyCameIn()
        {
            var log = new ConsoleLog();
            log.Add(LogKind.Examine, "端末", "一");
            log.Add(LogKind.Monologue, "", "二");
            Assert.AreEqual(2, log.Count);
            Assert.AreEqual("一", log.Entries[0].text);
            Assert.AreEqual("二", log.Entries[1].text);
        }

        [Test]
        public void EmptyTextIsNotKept()
        {
            var log = new ConsoleLog();
            log.Add(LogKind.Talk, "ハンナ", "");
            log.Add(LogKind.Talk, "ハンナ", null);
            Assert.AreEqual(0, log.Count);
        }

        [Test]
        public void ANamedLineBecomesTalkWithoutTheBrackets()
        {
            var log = new ConsoleLog();
            log.AddLine(LogKind.Monologue, "", "ハンナ「水筒！」");
            Assert.AreEqual(LogKind.Talk, log.Entries[0].kind);
            Assert.AreEqual("ハンナ", log.Entries[0].who);
            Assert.AreEqual("水筒！", log.Entries[0].text);
        }

        [Test]
        public void AnExaminedLineKeepsTheLabel()
        {
            var log = new ConsoleLog();
            log.AddLines(LogKind.Examine, "端末", new[] { "世間ではホロコンソールが人気だが", "私「どうも」" });
            Assert.AreEqual(LogKind.Examine, log.Entries[0].kind);
            Assert.AreEqual("端末", log.Entries[0].who);
            // 調べた先で交わした台詞は会話に分ける
            Assert.AreEqual(LogKind.Talk, log.Entries[1].kind);
            Assert.AreEqual("私", log.Entries[1].who);
        }

        [Test]
        public void AMonologueHasNoName()
        {
            var log = new ConsoleLog();
            log.AddLine(LogKind.Monologue, "誰か", "というのもほら、");
            Assert.AreEqual(LogKind.Monologue, log.Entries[0].kind);
            Assert.AreEqual("", log.Entries[0].who);
        }

        [Test]
        public void AChoiceKeepsTheQuestionAndTheAnswer()
        {
            var log = new ConsoleLog();
            log.AddChoice("チップを抜く", Choice.Yes);
            Assert.AreEqual(LogKind.Choice, log.Entries[0].kind);
            StringAssert.Contains("チップを抜く", log.Entries[0].text);
            StringAssert.EndsWith(Choice.Yes, log.Entries[0].text);
        }

        [Test]
        public void TheOldestFallOffPastTheLimit()
        {
            var log = new ConsoleLog();
            for (var i = 0; i < ConsoleLog.Keep + 5; i++) log.Add(LogKind.Monologue, "", "行" + i);
            Assert.AreEqual(ConsoleLog.Keep, log.Count);
            Assert.AreEqual("行5", log.Entries[0].text);
        }

        [Test]
        public void MovingToAnotherSceneStartsAnEmptyLog()
        {
            var log = new ConsoleLog();
            Assert.IsTrue(log.Follow(10));
            log.Add(LogKind.Monologue, "", "自室の行");
            // 同じ場面のうちは消えない
            Assert.IsFalse(log.Follow(10));
            Assert.AreEqual(1, log.Count);
            // 場面を移ると消える
            Assert.IsTrue(log.Follow(11));
            Assert.AreEqual(0, log.Count);
            log.Add(LogKind.Monologue, "", "路地裏の行");
            Assert.AreEqual("路地裏の行", log.Entries[0].text);
        }

        [Test]
        public void TheTagsReadAsTheDesignSays()
        {
            Assert.AreEqual("調べる", LogEntry.Tag(LogKind.Examine));
            Assert.AreEqual("会話", LogEntry.Tag(LogKind.Talk));
            Assert.AreEqual("独白", LogEntry.Tag(LogKind.Monologue));
        }

        // ---- さかのぼり --------------------------------------------------------

        [Test]
        public void ItOpensOnTheNewestAndStopsAtTheOldest()
        {
            var s = new LogScroll();
            s.Fit(1000f, 400f);
            Assert.IsTrue(s.AtNewest);
            s.By(250f);
            Assert.AreEqual(250f, s.Back, 1e-4f);
            s.By(10000f);
            Assert.AreEqual(600f, s.Back, 1e-4f);
            s.By(-10000f);
            Assert.AreEqual(0f, s.Back, 1e-4f);
            s.By(300f);
            s.Newest();
            Assert.IsTrue(s.AtNewest);
        }

        [Test]
        public void ShortLogsDoNotScroll()
        {
            var s = new LogScroll();
            s.Fit(120f, 400f);
            s.By(50f);
            Assert.AreEqual(0f, s.Back);
            Assert.AreEqual(1f, s.Thumb);
        }

        [Test]
        public void TheThumbFollowsTheScroll()
        {
            var s = new LogScroll();
            s.Fit(1000f, 250f);
            Assert.AreEqual(0.25f, s.Thumb, 1e-4f);
            Assert.AreEqual(0f, s.ThumbBottom, 1e-4f);
            s.By(750f);
            Assert.AreEqual(0.75f, s.ThumbBottom, 1e-4f);
            // つまみを溝の真ん中まで下ろすと、半分だけさかのぼった所
            s.DragTo(0.375f);
            Assert.AreEqual(375f, s.Back, 1e-3f);
        }

        [Test]
        public void GrowingContentKeepsTheScrollInRange()
        {
            var s = new LogScroll();
            s.Fit(1000f, 400f);
            s.By(600f);
            s.Fit(500f, 400f);
            Assert.AreEqual(100f, s.Back, 1e-4f);
        }
    }
}
