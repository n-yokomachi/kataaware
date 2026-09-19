using System.Collections.Generic;
using UnityEngine;

namespace HalfAware
{
    /// <summary>
    /// シーン上の調べる対象。位置はこの GameObject の位置。
    /// 印と文は RoomScript から id で引く。位置と規則はシーン、文面はアセットと分けてある
    /// </summary>
    public sealed class Interactable : MonoBehaviour, IInteractable
    {
        static readonly string[] NoLines = new string[0];

        [SerializeField] string id;
        [Tooltip("印と文を引く文面のアセット")]
        [SerializeField] RoomScript script;
        [SerializeField] float radius = InteractionPicker.DefaultRadius;
        [SerializeField] bool required;
        [SerializeField] bool once = true;
        [Tooltip("ここに挙げた id が済むまで選べない。文面に hint がある id については選べて、その文だけ出る")]
        [SerializeField] string[] after = new string[0];

        public string Id => id;
        public Vector3 Position => transform.position;
        /// <summary>切って隠している間は選ばせない。前腕のジャックがこれを使う</summary>
        public bool Active => isActiveAndEnabled;
        public float Radius => radius;
        public bool Required => required;
        public bool Once => once;
        public IReadOnlyList<string> After => after;
        /// <summary>文面が引けないときは id を出す。印が空欄になって気づけないのを避ける</summary>
        public string Label
        {
            get
            {
                if (script == null) return id;
                var entry = script.Find(id);
                return entry.id != null ? entry.Label : id;
            }
        }

        public IReadOnlyList<string> Lines => script != null ? script.Find(id).Lines : NoLines;

        public bool Asks => script != null && script.Find(id).Asks;

        public string Question => script != null ? (script.Find(id).choice.question ?? "") : "";

        public IReadOnlyList<string> AfterYes => script != null ? script.Find(id).choice.AfterYes : NoLines;

        public IReadOnlyList<string> HintFor(string afterId)
        {
            return script != null ? script.Find(id).HintFor(afterId) : null;
        }

        void OnDrawGizmos()
        {
            Gizmos.color = required ? new Color(1f, 0.6f, 0.2f) : new Color(0.6f, 0.8f, 1f);
            Gizmos.DrawWireSphere(transform.position, 0.1f);
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 1f, 1f, 0.35f);
            Gizmos.DrawWireSphere(transform.position, radius);
        }

#if UNITY_EDITOR
        void OnValidate()
        {
            // 組み立ての途中にも OnValidate は鳴る。
            // AddComponent した直後は id も文面もまだ入っていないので、
            // その場で見ると毎回無駄に警告が出る。一拍置いてから見る
            UnityEditor.EditorApplication.delayCall += Examine;
        }

        void Examine()
        {
            // 遅らせている間に消されていることがある
            if (this == null) return;
            if (string.IsNullOrEmpty(id)) Debug.LogWarning("Interactable に id がない: " + name, this);
            else if (script == null) Debug.LogWarning("Interactable に文面のアセットがない: " + name, this);
            else if (script.Find(id).id == null) Debug.LogWarning("文面に id が無い: " + id, this);
        }
#endif
    }
}
