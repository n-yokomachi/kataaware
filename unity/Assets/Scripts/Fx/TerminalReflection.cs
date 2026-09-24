using System.Collections.Generic;
using UnityEngine;

namespace HalfAware
{
    /// <summary>
    /// 場面 1 の端末の黒い画面に映る、主人公の口元。
    ///
    /// 端末を調べた独白の 3 行目（「こうして反射で自分の顔が見られるからだ」）から、独白を読み終えるまでだけ、
    /// ゆっくり浮かべて、読み終えたら消す。映すのは鼻の下から顎まで（口元のほくろを含む）だけで、
    /// 目も胸元も映さない（主人公の性別は対面まで見せない）。絵は組み立てで焼いた、暗く色を抜いた口元（左右を返してある）。
    ///
    /// 置き場は鏡の決まりに合わせる。目から画面へ下ろした点に目が映り、口元はそこから下へ映る。
    /// 大きさと下がりは、本物の映り込み（口元の半分の大きさ）を <see cref="size"/>・<see cref="drop"/> で拡げた値。
    /// 席から画面まで 1.15 m あり、本物の大きさでは画面の上で 5 px ほどにしかならない。
    /// 映る所が画面の外に出るときは、画面の縁の内へ寄せる
    /// </summary>
    [DefaultExecutionOrder(20)]
    public sealed class TerminalReflection : MonoBehaviour
    {
        [SerializeField] SceneFlow flow;
        [Tooltip("独白を持つ調べる対象（端末）")]
        [SerializeField] Interactable source;
        [Tooltip("独白の何行目から映すか。0 から数える")]
        [SerializeField] int fromLine = 2;
        [Tooltip("画面の板。前（+z）が画面の奥、上（+y）が画面の上")]
        [SerializeField] Transform screen;
        [Tooltip("画面の板の大きさ（m）")]
        [SerializeField] Vector2 screenSize = new Vector2(0.81f, 0.48f);
        [Tooltip("画面の板の厚み（m）。映り込みの板は表の面のすぐ手前に置く")]
        [SerializeField] float screenThick = 0.012f;
        [Tooltip("映り込みの板の大きさ（m）")]
        [SerializeField] Vector2 size = new Vector2(0.15f, 0.09f);
        [Tooltip("目が映る所から口元の真ん中までの下がり（m）")]
        [SerializeField] float drop = 0.11f;
        [Tooltip("浮かべるのと消すのにかける秒数")]
        [SerializeField] float fadeSeconds = 0.8f;
        [Tooltip("いちばん濃いときの濃さ")]
        [Range(0f, 1f)]
        [SerializeField] float strength = 1f;
        [SerializeField] Renderer face;

        static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        MaterialPropertyBlock block;
        float level;

        /// <summary>今の濃さ（0〜1）。動作確認から読む</summary>
        public float Level => level;

        void Awake()
        {
            block = new MaterialPropertyBlock();
            if (face != null) face.enabled = false;
        }

        void LateUpdate()
        {
            if (flow == null || source == null || face == null || screen == null) return;
            var want = Showing(source.Lines, flow.CurrentLine, fromLine) ? 1f : 0f;
            level = Mathf.MoveTowards(level, want, fadeSeconds > 0f ? Time.deltaTime / fadeSeconds : 1f);
            face.enabled = level > 0f;
            if (!face.enabled) return;

            var eye = flow.Player != null && flow.Player.Eye != null ? flow.Player.Eye.position : Camera.main.transform.position;
            var d = eye - screen.position;
            var at = Spot(new Vector2(Vector3.Dot(d, screen.right), Vector3.Dot(d, screen.up)), drop, screenSize, size);
            // 画面の表は板の手前（-z）の面。その 2 mm 手前に浮かべる
            transform.SetPositionAndRotation(
                screen.position + screen.right * at.x + screen.up * at.y - screen.forward * (screenThick * 0.5f + 0.002f),
                screen.rotation);
            var parent = transform.parent != null ? transform.parent.lossyScale : Vector3.one;
            transform.localScale = new Vector3(size.x / parent.x, size.y / parent.y, 1f);

            face.GetPropertyBlock(block);
            block.SetColor(BaseColor, new Color(1f, 1f, 1f, Mathf.SmoothStep(0f, 1f, level) * strength));
            face.SetPropertyBlock(block);
        }

        /// <summary>今出している行 current が、独白 lines の from 行目から後ろにあるか</summary>
        public static bool Showing(IReadOnlyList<string> lines, string current, int from)
        {
            if (lines == null || current == null) return false;
            for (var i = Mathf.Max(0, from); i < lines.Count; i++)
                if (lines[i] == current) return true;
            return false;
        }

        /// <summary>
        /// 映り込みの板の真ん中を、画面の真ん中から見た位置（右・上、m）で返す。
        /// eye は目を画面の面へ下ろした点。口元はそこから drop 下へ映る。板が画面の縁からはみ出さないよう寄せる
        /// </summary>
        public static Vector2 Spot(Vector2 eye, float drop, Vector2 screen, Vector2 size)
        {
            var hx = Mathf.Max(0f, (screen.x - size.x) * 0.5f);
            var hy = Mathf.Max(0f, (screen.y - size.y) * 0.5f);
            return new Vector2(Mathf.Clamp(eye.x, -hx, hx), Mathf.Clamp(eye.y - drop, -hy, hy));
        }
    }
}
