using NUnit.Framework;

namespace HalfAware.Tests
{
    public class ScriptEntryTests
    {
        static ScriptEntry[] Entries()
        {
            var jack = new ScriptEntry
            {
                id = "jack",
                label = "インプラントジャックを抜く",
                lines = new[] { "大小の差こそあれ、他人の記憶を観た後はいつもこうだ" },
                hints = new ScriptHint[0],
            };
            var door = new ScriptEntry
            {
                id = "door",
                label = "ドア",
                lines = new[] { "タバコを買いに行くついでに、今日のチップを売ってしまおう" },
                hints = new[]
                {
                    new ScriptHint { after = "chips", lines = new[] { "テーブルからチップを取ってこよう" } },
                },
            };
            return new[] { jack, door };
        }

        [Test]
        public void FindsAnEntryById()
        {
            Assert.That(ScriptEntry.Find(Entries(), "door").label, Is.EqualTo("ドア"));
        }

        [Test]
        public void ReturnsAnEmptyEntryForAnUnknownId()
        {
            var found = ScriptEntry.Find(Entries(), "nothing");
            Assert.That(found.id, Is.Null);
            Assert.That(found.Label, Is.EqualTo(""));
            Assert.That(found.Lines, Is.Empty);
        }

        [Test]
        public void LabelFallsBackToTheIdWhenItIsBlank()
        {
            var entry = new ScriptEntry { id = "ashtray", label = "", lines = null, hints = null };
            Assert.That(entry.Label, Is.EqualTo("ashtray"));
        }

        [Test]
        public void LinesAreNeverNull()
        {
            var entry = new ScriptEntry { id = "a", label = "a", lines = null, hints = null };
            Assert.That(entry.Lines, Is.Not.Null);
            Assert.That(entry.Lines, Is.Empty);
        }

        [Test]
        public void FindsTheHintOfAPrerequisite()
        {
            var door = ScriptEntry.Find(Entries(), "door");
            Assert.That(door.HintFor("chips"), Is.EqualTo(new[] { "テーブルからチップを取ってこよう" }));
        }

        [Test]
        public void ReturnsNullForAPrerequisiteWithoutAHint()
        {
            var door = ScriptEntry.Find(Entries(), "door");
            Assert.That(door.HintFor("terminal"), Is.Null);
            Assert.That(ScriptEntry.Find(Entries(), "jack").HintFor("x"), Is.Null);
        }

        [Test]
        public void TellsAnEntryWithNoLinesApartFromAMissingOne()
        {
            // 煙草は文を持たず、吸い終わりの独白を演出の側が言う。「在るが空」を「無い」と取り違えない
            var found = ScriptEntry.Find(new[] { new ScriptEntry { id = "cigarette" } }, "cigarette");
            Assert.That(found.id, Is.Not.Null);
            Assert.That(found.Lines, Is.Empty);
            Assert.That(ScriptEntry.Find(new[] { new ScriptEntry { id = "cigarette" } }, "other").id, Is.Null);
        }

        [Test]
        public void TakesTheFirstEntryWhenAnIdIsRepeated()
        {
            var first = new ScriptEntry { id = "a", label = "先", lines = null, hints = null };
            var second = new ScriptEntry { id = "a", label = "後", lines = null, hints = null };
            Assert.That(ScriptEntry.Find(new[] { first, second }, "a").label, Is.EqualTo("先"));
        }
    }
}
