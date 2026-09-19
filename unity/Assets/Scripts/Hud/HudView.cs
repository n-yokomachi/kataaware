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
            Cover(fadeLayer);
            SetFade(0f);
            SetCurtain(false);
            SetLog(null);
        }

        /// <summary>
        /// 画面の実寸を拡大率で割って丸めるので、キャンバスは画面より 1 ピクセルほど
        /// 小さくなることがある。幕をぴったり張ると端に地が覗くので、少し外へはみ出させる
        /// </summary>
        const float Overscan = 96f;

        /// <summary>
        /// 幕を画面いっぱい、四方へ大きくはみ出させて張る。
        /// 暗転で端に地が覗くのは目に付くうえ、はみ出させて困ることは何も無いので、
        /// ぎりぎりを狙わずに十分な余りを取る
        /// </summary>
        static void Cover(Image layer)
        {
            if (layer == null) return;
            var rect = layer.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(-Overscan, -Overscan);
            rect.offsetMax = new Vector2(Overscan, Overscan);
        }

        /// <summary>
        /// null で黒帯ごと隠す。入っている行数に合わせて帯を伸ばし、
        /// 長いものは字を小さくして収める
        /// </summary>
        public void SetSubtitle(string text)
        {
            SetSubtitle(text, SubtitleKind.Line);
        }

        /// <summary>
        /// 二択は表に組まず、帯の真ん中へ寄せる。
        /// 「はい　いいえ」を左に寄せると、どちらを選んでいるかが目で追いにくい
        /// </summary>
        public void SetSubtitle(string text, SubtitleKind kind)
        {
            subtitleBand.SetActive(text != null);
            subtitleText.text = text ?? string.Empty;
            if (text == null) return;
            // 並びになっているものは表に組む。そうでない長い 1 行は割ってウインドウに収める
            var list = kind == SubtitleKind.Line && ListFormat.IsList(text);
            // 1 行に入る幅はウインドウの実寸から。全角 1 文字で半角 2 つぶん
            var fits = Mathf.Max(SubtitleBox.BaseRows * 2, Mathf.FloorToInt(RoomEm(1f) * 2f) - 1);
            var shown = list ? text : SubtitleBox.Wrap(text, fits);
            var rows = SubtitleBox.Rows(shown);
            var scale = SubtitleBox.FontScale(shown);
            // 列を揃えるため表は左寄せにして、表ごと帯の真ん中へ寄せる
            // ルビは折り返してから書式に直す。
            // 先に直すと、折り返しがタグを字数に数えてしまう
            subtitleText.text = Ruby.Expand(list ? ListFormat.Compose(text, RoomEm(scale)) : shown);
            // ルビのある文は行を少し開ける。
            // そのままだと下の行のルビが上の行の字にかぶる
            subtitleText.lineSpacing = shown.IndexOf(Ruby.Head) >= 0 ? Ruby.ExtraLineSpacing : 0f;
            subtitleText.alignment =
                kind == SubtitleKind.Choice ? TMPro.TextAlignmentOptions.Center :
                list ? TMPro.TextAlignmentOptions.Left : listlessAlignment;
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
            centerText.text = Ruby.Expand(text ?? string.Empty);
        }

        /// <summary>黒い層の濃さ。0 で透明、1 で真っ黒</summary>
        public void SetFade(float alpha)
        {
            Cover(fadeLayer);
            SetAlpha(fadeLayer, alpha);
        }

        /// <summary>seconds 秒かけて黒い層の濃さを変える</summary>
        public IEnumerator FadeTo(float alpha, float seconds)
        {
            Cover(fadeLayer);
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
            if (logText != null) logText.text = Ruby.Expand(text ?? string.Empty);
        }

        /// <summary>
        /// 黒い幕。true で画面を覆い、false で消す。動きは付けず、そのまま切り替える。
        /// 濃さもここで戻すので、CurtainTo で薄くした後に覆い直しても透けない
        /// </summary>
        public void SetCurtain(bool covered)
        {
            if (curtainLayer == null) return;
            Cover(curtainLayer);
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
