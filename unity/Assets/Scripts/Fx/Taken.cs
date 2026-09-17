using UnityEngine;

namespace HalfAware
{
    /// <summary>
    /// 持っていった物を場から消す。メモリラックから抜いたメモリのように、
    /// 調べた結果が部屋の見た目に残るところに使う。
    /// 消す物と、代わりに現れる物と、色を替える材質を挙げておく
    /// </summary>
    public sealed class Taken : MonoBehaviour
    {
        [Tooltip("調べ終わりを受け取る")]
        [SerializeField] SceneFlow flow;
        [Tooltip("この id を調べたら持っていったことにする")]
        [SerializeField] string id = "chips";
        [Tooltip("持っていったら消える物")]
        [SerializeField] GameObject[] removed = new GameObject[0];
        [Tooltip("持っていったら現れる物")]
        [SerializeField] GameObject[] revealed = new GameObject[0];
        [Tooltip("持っていったら材質が替わる描画。lamp と対にする")]
        [SerializeField] Renderer[] relit = new Renderer[0];
        [Tooltip("替えた後の材質")]
        [SerializeField] Material lamp;

        bool taken;

        /// <summary>もう持っていった後か。動作確認から読む</summary>
        public bool IsTaken { get { return taken; } }

        /// <summary>この知らせで持ち出しが起きるか。済んだ後や別の対象では起きない</summary>
        public static bool Triggers(string examinedId, string wantedId, bool already)
        {
            if (already) return false;
            if (string.IsNullOrEmpty(wantedId)) return false;
            return examinedId == wantedId;
        }

        void OnEnable()
        {
            if (flow != null) flow.Examined += OnExamined;
            Apply();
        }

        void OnDisable()
        {
            if (flow != null) flow.Examined -= OnExamined;
        }

        void OnExamined(IInteractable item)
        {
            if (item == null) return;
            if (!Triggers(item.Id, id, taken)) return;
            taken = true;
            Apply();
        }

        void Apply()
        {
            foreach (var g in removed) if (g != null) g.SetActive(!taken);
            foreach (var g in revealed) if (g != null) g.SetActive(taken);
            if (!taken || lamp == null) return;
            foreach (var r in relit) if (r != null) r.sharedMaterial = lamp;
        }
    }
}
