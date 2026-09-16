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

        /// <summary>煙のいちばん濃いときの不透明度。向こう側が透けて見える濃さに留める</summary>
        public const float SmokePeakAlpha = 0.45f;

        [SerializeField] GameObject subtitleBand;
        [SerializeField] TMP_Text subtitleText;
        [SerializeField] TMP_Text promptText;
        [SerializeField] TMP_Text centerText;
        [Tooltip("画面全体の黒い層。暗転に使う")]
        [SerializeField] Image fadeLayer;
        [Tooltip("画面下から立ち上る煙。見た目は段階 4 で作り込む")]
        [SerializeField] Image smokeLayer;
        [Tooltip("画面を上から下へ通り抜ける黒い幕。クレジットのカードを載せる")]
        [SerializeField] Image curtainLayer;

        Coroutine smoking;

        void Awake()
        {
            SetSubtitle(null);
            SetPrompt(null);
            SetCenter(null);
            SetFade(0f);
            SetSmoke(0f);
            SetCurtain(1f);
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
                SetSmoke(SmokePeakAlpha * t / SmokeFadeSeconds);
                yield return null;
            }
            SetSmoke(SmokePeakAlpha);
            yield return new WaitForSeconds(Mathf.Max(0f, seconds - SmokeFadeSeconds));
            for (var t = 0f; t < SmokeFadeSeconds; t += Time.deltaTime)
            {
                SetSmoke(SmokePeakAlpha * (1f - t / SmokeFadeSeconds));
                yield return null;
            }
            SetSmoke(0f);
            smoking = null;
        }

        /// <summary>
        /// 黒い幕の位置。画面の高さを 1 として、1 で上へ外れ、0 で画面を覆い、-1 で下へ外れる。
        /// 画面の大きさに依らないよう、位置は割合で持つ
        /// </summary>
        public void SetCurtain(float offset)
        {
            if (curtainLayer == null) return;
            var rect = curtainLayer.rectTransform;
            rect.anchorMin = new Vector2(0f, offset);
            rect.anchorMax = new Vector2(1f, 1f + offset);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            curtainLayer.gameObject.SetActive(offset > -1f && offset < 1f);
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
