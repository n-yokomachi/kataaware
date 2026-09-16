using System;
using System.Collections.Generic;
using UnityEngine;

namespace HalfAware
{
    /// <summary>シーン上の調べる対象。位置はこの GameObject の位置。文面はこの段では仮の文を直接持ち、段階 3 で RoomScript に移す</summary>
    public sealed class Interactable : MonoBehaviour, IInteractable
    {
        [Serializable]
        public struct Hint
        {
            /// <summary>after に挙げた id</summary>
            public string after;
            /// <summary>その id が未達のときに出す文。出しても済んだことにはならない</summary>
            [TextArea] public string[] lines;
        }

        [SerializeField] string id;
        [SerializeField] float radius = InteractionPicker.DefaultRadius;
        [SerializeField] bool required;
        [SerializeField] bool once = true;
        [Tooltip("ここに挙げた id が済むまで選べない。hints に文がある id については選べて、その文だけ出る")]
        [SerializeField] string[] after = new string[0];
        [SerializeField] Hint[] hints = new Hint[0];
        [SerializeField] string label = "調べる";
        [SerializeField, TextArea] string[] lines = new string[0];

        public string Id => id;
        public Vector3 Position => transform.position;
        public float Radius => radius;
        public bool Required => required;
        public bool Once => once;
        public IReadOnlyList<string> After => after;
        public string Label => label;
        public IReadOnlyList<string> Lines => lines;

        public IReadOnlyList<string> HintFor(string afterId)
        {
            foreach (var hint in hints)
            {
                if (hint.after == afterId) return hint.lines;
            }
            return null;
        }

        void OnDrawGizmos()
        {
            Gizmos.color = required ? new Color(1f, 0.6f, 0.2f) : new Color(0.6f, 0.8f, 1f);
            Gizmos.DrawWireSphere(transform.position, 0.1f);
        }
    }
}
