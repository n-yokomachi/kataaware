using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HalfAware
{
    /// <summary>
    /// 字幕（画面下の黒帯）、印（中央）、中央の文字、暗転、煙。
    /// 見せるだけで、何をいつ出すかは SceneFlow と場面固有の演出が決める。
    /// 暗転と煙の層は、繋がっていなければ何もしない（段階を追って足すため）
    /// </summary>
    public sealed class HudView : MonoBehaviour
    {
        /// <summary>煙が消えるのにかける秒数</summary>
        public const float SmokeFadeSeconds = 1f;

        [SerializeField] GameObject subtitleBand;
        [SerializeField] TMP_Text subtitleText;
        [SerializeField] TMP_Text promptText;
        [SerializeField] TMP_Text centerText;
        [Tooltip("画面全体の黒い層。暗転に使う")]
        [SerializeField] Image fadeLayer;
        [Tooltip("画面下から立ち上る煙。見た目は段階 4 で作り込む")]
        [SerializeField] Image smokeLayer;

        Coroutine smoking;

        void Awake()
        {
            SetSubtitle(null);
            SetPrompt(null);
            SetCenter(null);
            SetFade(0f);
            SetSmoke(0f);
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

        /// <summary>煙を seconds 秒立ち上らせ、その後 SmokeFadeSeconds 秒かけて消す</summary>
        public void ShowSmoke(float seconds)
        {
            CancelSmoke();
            if (smokeLayer == null) return;
            smoking = StartCoroutine(Smoke(seconds));
        }

        /// <summary>進行中の煙を止めて消す。場面が終わるときに呼ぶ</summary>
        public void CancelSmoke()
        {
            if (smoking != null)
            {
                StopCoroutine(smoking);
                smoking = null;
            }
            SetSmoke(0f);
        }

        IEnumerator Smoke(float seconds)
        {
            for (var t = 0f; t < SmokeFadeSeconds; t += Time.deltaTime)
            {
                SetSmoke(t / SmokeFadeSeconds);
                yield return null;
            }
            SetSmoke(1f);
            yield return new WaitForSeconds(Mathf.Max(0f, seconds - SmokeFadeSeconds));
            for (var t = 0f; t < SmokeFadeSeconds; t += Time.deltaTime)
            {
                SetSmoke(1f - t / SmokeFadeSeconds);
                yield return null;
            }
            SetSmoke(0f);
            smoking = null;
        }

        void SetSmoke(float alpha)
        {
            SetAlpha(smokeLayer, alpha);
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
