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
        /// <summary>ログに一度に出す行数。画面に収まる分だけ</summary>
        public const int LogLines = 18;

        [SerializeField] GameObject subtitleBand;
        [SerializeField] TMP_Text subtitleText;
        [Tooltip("字幕 1 行ぶんの高さ。ウインドウはこの倍数で伸びる")]
        [SerializeField] float subtitleRowHeight = 44f;
        [Tooltip("帯の上下の余白をあわせた高さ")]
        [SerializeField] float subtitlePadding = 34f;
        [SerializeField] TMP_Text promptText;
        [SerializeField] TMP_Text centerText;
        [Tooltip("画面全体の黒い層。暗転に使う")]
        [SerializeField] Image fadeLayer;
        [Tooltip("画面全体を覆う黒い幕。クレジットのカードを載せる")]
        [SerializeField] Image curtainLayer;
        [Tooltip("Tab で出す、これまでの文のログ")]
        [SerializeField] GameObject logPanel;
        [SerializeField] TMP_Text logText;

        float baseFontSize;
        TMPro.TextAlignmentOptions listlessAlignment = TMPro.TextAlignmentOptions.Center;

        void Awake()
        {
            if (subtitleText != null)
            {
                baseFontSize = subtitleText.fontSize;
                listlessAlignment = subtitleText.alignment;
            }
            SetSubtitle(null);
            SetPrompt(null);
            SetCenter(null);
            SetFade(0f);
            SetCurtain(false);
            SetLog(null);
        }

        /// <summary>
        /// null で黒帯ごと隠す。入っている行数に合わせて帯を伸ばし、
        /// 長いものは字を小さくして収める
        /// </summary>
        public void SetSubtitle(string text)
        {
            SetSubtitle(text, true);
        }

        /// <summary>
        /// asTable が false なら表に組まない。二択のように、
        /// 空白で分かれていても列にしたくないものに使う
        /// </summary>
        public void SetSubtitle(string text, bool asTable)
        {
            subtitleBand.SetActive(text != null);
            subtitleText.text = text ?? string.Empty;
            if (text == null) return;
            // 並びになっているものは表に組む。そうでない長い 1 行は 2 行に割って、
            // ウインドウの 2 行を埋める
            var list = asTable && ListFormat.IsList(text);
            // 1 行に入る幅はウインドウの実寸から。全角 1 文字で半角 2 つぶん
            var fits = Mathf.Max(SubtitleBox.BaseRows * 2, Mathf.FloorToInt(RoomEm(1f) * 2f) - 1);
            var shown = list ? text : SubtitleBox.Wrap(text, fits);
            var rows = SubtitleBox.Rows(shown);
            var scale = SubtitleBox.FontScale(shown);
            // 列を揃えるため表は左寄せにして、表ごと帯の真ん中へ寄せる
            subtitleText.text = list ? ListFormat.Compose(text, RoomEm(scale)) : shown;
            subtitleText.alignment = list ? TMPro.TextAlignmentOptions.Left : listlessAlignment;
            var band = subtitleBand.GetComponent<RectTransform>();
            if (band != null)
            {
                // 字を小さくしたぶん 1 行も低くなる。帯の高さも同じだけ詰める
                var size = band.sizeDelta;
                size.y = subtitlePadding + subtitleRowHeight * rows * scale;
                band.sizeDelta = size;
            }
            if (baseFontSize <= 0f) baseFontSize = subtitleText.fontSize;
            subtitleText.fontSize = baseFontSize * scale;
        }

        /// <summary>帯に入る横幅を em で。表の列数と寄せ方をこれで決める</summary>
        float RoomEm(float scale)
        {
            var size = baseFontSize * scale;
            if (size <= 0f) return 0f;
            return subtitleText.rectTransform.rect.width / size;
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
            return Ramp(fadeLayer, alpha, seconds);
        }

        /// <summary>seconds 秒かけて黒い幕の濃さを変える。切り替えずに明けたいときに使う</summary>
        public IEnumerator CurtainTo(float alpha, float seconds)
        {
            return Ramp(curtainLayer, alpha, seconds);
        }

        static IEnumerator Ramp(Image layer, float alpha, float seconds)
        {
            if (layer == null || seconds <= 0f)
            {
                SetAlpha(layer, alpha);
                yield break;
            }
            var from = layer.color.a;
            for (var t = 0f; t < seconds; t += Time.deltaTime)
            {
                SetAlpha(layer, Mathf.Lerp(from, alpha, t / seconds));
                yield return null;
            }
            SetAlpha(layer, alpha);
        }

        /// <summary>これまでの文のログ。null で閉じる</summary>
        public void SetLog(string text)
        {
            if (logPanel != null) logPanel.SetActive(text != null);
            if (logText != null) logText.text = text ?? string.Empty;
        }

        /// <summary>
        /// 黒い幕。true で画面を覆い、false で消す。動きは付けず、そのまま切り替える。
        /// 濃さもここで戻すので、CurtainTo で薄くした後に覆い直しても透けない
        /// </summary>
        public void SetCurtain(bool covered)
        {
            if (curtainLayer == null) return;
            var rect = curtainLayer.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            SetAlpha(curtainLayer, covered ? 1f : 0f);
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
