using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HalfAware
{
    /// <summary>
    /// 字幕（画面下の黒帯）、印（中央）、中央の文字、暗転、幕。
    /// 見せるだけで、何をいつ出すかは SceneFlow と場面固有の演出が決める。
    /// 煙は画面を覆う層ではなく世界の粒で描くので、ここには無い（SmokePuffs）。
    /// 暗転と幕の層は、繋がっていなければ何もしない
    /// </summary>
    public sealed class HudView : MonoBehaviour
    {
        [SerializeField] GameObject subtitleBand;
        [SerializeField] TMP_Text subtitleText;
        [SerializeField] TMP_Text promptText;
        [SerializeField] TMP_Text centerText;
        [Tooltip("画面全体の黒い層。暗転に使う")]
        [SerializeField] Image fadeLayer;
        [Tooltip("画面全体を覆う黒い幕。クレジットのカードを載せる")]
        [SerializeField] Image curtainLayer;

        void Awake()
        {
            SetSubtitle(null);
            SetPrompt(null);
            SetCenter(null);
            SetFade(0f);
            SetCurtain(false);
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

        /// <summary>黒い層の濃さ。0 で透明、1 で真っ黒</summary>
        public void SetFade(float alpha)
        {
            SetAlpha(fadeLayer, alpha);
        }

        /// <summary>seconds 秒かけて黒い層の濃さを変える</summary>
        public IEnumerator FadeTo(float alpha, float seconds)
        {
            if (fadeLayer == null || seconds <= 0f)
            {
                SetFade(alpha);
                yield break;
            }
            var from = fadeLayer.color.a;
            for (var t = 0f; t < seconds; t += Time.deltaTime)
            {
                SetFade(Mathf.Lerp(from, alpha, t / seconds));
                yield return null;
            }
            SetFade(alpha);
        }

        /// <summary>黒い幕。true で画面を覆い、false で消す。動きは付けず、そのまま切り替える</summary>
        public void SetCurtain(bool covered)
        {
            if (curtainLayer == null) return;
            var rect = curtainLayer.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            curtainLayer.gameObject.SetActive(covered);
        }

        static void SetAlpha(Image layer, float alpha)
        {
            if (layer == null) return;
            var color = layer.color;
            color.a = alpha;
            layer.color = color;
            layer.gameObject.SetActive(alpha > 0f);
        }
    }
}
