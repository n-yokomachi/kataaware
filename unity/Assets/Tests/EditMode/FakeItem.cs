using System.Collections.Generic;
using UnityEngine;

namespace HalfAware.Tests
{
    /// <summary>テスト用の調べる対象。必要な項目だけ設定する</summary>
    public sealed class FakeItem : IInteractable
    {
        public string Id { get; set; }
        public Vector3 Position { get; set; }
        public bool Active { get; set; } = true;
        public float Radius { get; set; } = InteractionPicker.DefaultRadius;
        public bool Required { get; set; }
        public bool Once { get; set; } = true;
        public IReadOnlyList<string> After { get; set; } = new string[0];
        public string Label { get; set; } = "調べる";
        public IReadOnlyList<string> Lines { get; set; } = new string[0];
        public Dictionary<string, string[]> Hints { get; } = new Dictionary<string, string[]>();

        public FakeItem(string id, Vector3 position)
        {
            Id = id;
            Position = position;
        }

        public IReadOnlyList<string> HintFor(string afterId)
        {
            string[] lines;
            return Hints.TryGetValue(afterId, out lines) ? lines : null;
        }
    }
}
