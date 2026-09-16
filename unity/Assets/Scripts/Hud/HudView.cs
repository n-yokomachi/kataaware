using TMPro;
using UnityEngine;

namespace HalfAware
{
    /// <summary>字幕（画面下の黒帯）、印（中央）、中央の文字。見せるだけで、何を出すかは SceneFlow が決める</summary>
    public sealed class HudView : MonoBehaviour
    {
        [SerializeField] GameObject subtitleBand;
        [SerializeField] TMP_Text subtitleText;
        [SerializeField] TMP_Text promptText;
        [SerializeField] TMP_Text centerText;

        void Awake()
        {
            SetSubtitle(null);
            SetPrompt(null);
            SetCenter(null);
        }

        /// <summary>null で黒帯ごと隠す</summary>
        public void SetSubtitle(string text)
        {
            subtitleBand.SetActive(text != null);
            subtitleText.text = text ?? string.Empty;
        }

        /// <summary>null で隠す</summary>
        public void SetPrompt(string text)
        {
            promptText.gameObject.SetActive(text != null);
            promptText.text = text ?? string.Empty;
        }

        /// <summary>null で隠す</summary>
        public void SetCenter(string text)
        {
            centerText.gameObject.SetActive(text != null);
            centerText.text = text ?? string.Empty;
        }
    }
}
