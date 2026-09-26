using System;
using System.IO;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace HalfAware.EditorTools
{
    /// <summary>
    /// コンソールと字幕を、ゲームと同じ見え方で撮る（設計書 4 節）。再生せずに撮る。
    ///
    /// ゲームでは HUD とコンソールの Canvas を粗い画面（<see cref="UiLens"/>）へ描き、最近傍で引き伸ばして
    /// 3D の絵に重ねる。ここでも同じに、3D は場面のカメラで（1/3 の粗さと後処理ごと）、UI は UiLens の
    /// カメラで粗い RenderTexture へ描いて、乗算済みのアルファのままリニアで重ねる。
    ///
    /// 撮るあいだに HUD の中身や Canvas の向きを書き換えるので、**撮り終えたら場面を開き直して捨てる**。
    /// 未保存の変更がある場面では撮らない
    /// </summary>
    public static class ConsoleShot
    {
        /// <summary>撮る前に HUD とコンソールへ手を入れる。null なら何もしない</summary>
        public delegate void Stage(HudView hud, ImplantConsole console);

        static readonly MethodInfo Handle = typeof(CanvasScaler).GetMethod("Handle", BindingFlags.NonPublic | BindingFlags.Instance);

        /// <summary>
        /// 開いている場面を w×h で撮り、path へ PNG で書く。uiScale は UI の粗さ（1 でくっきり）。
        /// console が true ならコンソールを開いた形（back だけさかのぼる、listing でデバッグの一覧）で重ねる
        /// </summary>
        public static string Shoot(string path, int w, int h, float uiScale, bool console, float back, bool listing, Stage stage)
        {
            return Shoot(path, w, h, uiScale, console, back, listing ? ConsolePanel.Scenes : ConsolePanel.None, stage);
        }

        /// <summary>コンソールを開いて、ボタンの下の枠（記憶する・思い出す・デバッグ）を開いた形で撮る</summary>
        public static string Shoot(string path, int w, int h, float uiScale, bool console, float back, ConsolePanel open, Stage stage)
        {
            var cam = Eye();
            if (cam == null) return "カメラが無い";
            var hud = UnityEngine.Object.FindFirstObjectByType<HudView>(FindObjectsInactive.Include);
            if (hud == null) return "Hud が無い";
            Texture2D scene = null;
            Texture2D ui = null;
            Texture2D shot = null;
            UiLens lens = null;
            ImplantConsole panel = null;
            var hudCanvas = hud.GetComponent<Canvas>();
            var kept = Keep.Of(hudCanvas);
            try
            {
                scene = CheckDiveSky.Grab(cam, w, h);
                cam.ResetAspect();

                UiLens.Scale = uiScale;
                lens = UiLens.Make();
                lens.Fit(w, h);
                Lens(hudCanvas, lens.Eye, 0);
                if (console)
                {
                    panel = ImplantConsole.Make();
                    Lens(panel.GetComponent<Canvas>(), lens.Eye, ImplantConsole.SortingOrder);
                }
                // 見出し（冒頭のカード）は粗い画面の外に出すので、ここでは伏せる。
                // コンソールを開いている間は、ゲームでも字幕と印を伏せる（HudView.LateUpdate）
                hud.SetCenter(null);
                if (console)
                {
                    hud.SetSubtitle(null);
                    hud.SetPrompt(null);
                }
                if (stage != null) stage(hud, panel);
                if (panel != null) panel.Show(back, open);
                lens.Draw();
                ui = Read(lens.Target);
                shot = Blend(scene, ui, w, h);
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllBytes(path, shot.EncodeToPNG());
                return "撮った " + path + "（UI " + lens.Target.width + "x" + lens.Target.height + "）";
            }
            catch (Exception e)
            {
                return "例外: " + e;
            }
            finally
            {
                if (panel != null) { panel.Release(); UnityEngine.Object.DestroyImmediate(panel.gameObject); }
                if (lens != null) { lens.Release(); UnityEngine.Object.DestroyImmediate(lens.gameObject); }
                UiLens.ResetScale();
                kept.Restore();
                if (scene != null) UnityEngine.Object.DestroyImmediate(scene);
                if (ui != null) UnityEngine.Object.DestroyImmediate(ui);
                if (shot != null) UnityEngine.Object.DestroyImmediate(shot);
            }
        }

        /// <summary>場面の目。主人公の目のカメラ、無ければ Main Camera</summary>
        static Camera Eye()
        {
            var player = UnityEngine.Object.FindFirstObjectByType<PlayerController>(FindObjectsInactive.Include);
            if (player != null && player.Eye != null)
            {
                var c = player.Eye.GetComponentInChildren<Camera>(true);
                if (c != null) return c;
            }
            return Camera.main;
        }

        /// <summary>canvas を UiLens のカメラで描く向きにする。当たりの付け替えはしない（撮るだけなので）</summary>
        internal static void Lens(Canvas canvas, Camera eye, int order)
        {
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = eye;
            canvas.sortingOrder = order;
            canvas.planeDistance = Mathf.Max(1f, 10f - order * 0.01f);
            Layer(canvas.transform);
            var scaler = canvas.GetComponent<CanvasScaler>();
            if (scaler != null && Handle != null) Handle.Invoke(scaler, null);
            Canvas.ForceUpdateCanvases();
        }

        static void Layer(Transform t)
        {
            t.gameObject.layer = UiLens.UiLayer;
            for (var i = 0; i < t.childCount; i++) Layer(t.GetChild(i));
        }

        internal static Texture2D Read(RenderTexture rt)
        {
            var keep = RenderTexture.active;
            try
            {
                RenderTexture.active = rt;
                var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGBA32, false, false);
                tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
                tex.Apply();
                return tex;
            }
            finally
            {
                RenderTexture.active = keep;
            }
        }

        /// <summary>
        /// 粗い UI を最近傍で引き伸ばし、3D の絵に重ねる。どちらも sRGB で入っているので、
        /// リニアへ直して乗算済みのアルファで重ね、sRGB へ戻す（ゲームで GPU がしているのと同じ）
        /// </summary>
        internal static Texture2D Blend(Texture2D scene, Texture2D ui, int w, int h)
        {
            var lin = new float[256];
            for (var i = 0; i < 256; i++) lin[i] = Mathf.GammaToLinearSpace(i / 255f);
            var s = scene.GetPixels32();
            var u = ui.GetPixels32();
            var o = new Color32[w * h];
            var uw = ui.width;
            var uh = ui.height;
            for (var y = 0; y < h; y++)
            {
                var uy = Mathf.Min(uh - 1, y * uh / h);
                for (var x = 0; x < w; x++)
                {
                    var ux = Mathf.Min(uw - 1, x * uw / w);
                    var a = u[uy * uw + ux];
                    var b = s[y * w + x];
                    var k = 1f - a.a / 255f;
                    o[y * w + x] = new Color32(
                        Enc(lin[a.r] + lin[b.r] * k),
                        Enc(lin[a.g] + lin[b.g] * k),
                        Enc(lin[a.b] + lin[b.b] * k), 255);
                }
            }
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false, false);
            tex.SetPixels32(o);
            tex.Apply();
            return tex;
        }

        static byte Enc(float v)
        {
            return (byte)Mathf.Clamp(Mathf.RoundToInt(Mathf.LinearToGammaSpace(Mathf.Clamp01(v)) * 255f), 0, 255);
        }

        /// <summary>Canvas の向きとレイヤー。撮り終えたら戻す（場面は開き直して捨てるが、念のため）</summary>
        sealed class Keep
        {
            Canvas canvas;
            RenderMode mode;
            Camera eye;
            int order;
            float distance;
            Transform[] all;
            int[] layers;

            public static Keep Of(Canvas canvas)
            {
                var k = new Keep();
                k.canvas = canvas;
                k.mode = canvas.renderMode;
                k.eye = canvas.worldCamera;
                k.order = canvas.sortingOrder;
                k.distance = canvas.planeDistance;
                k.all = canvas.GetComponentsInChildren<Transform>(true);
                k.layers = new int[k.all.Length];
                for (var i = 0; i < k.all.Length; i++) k.layers[i] = k.all[i].gameObject.layer;
                return k;
            }

            public void Restore()
            {
                if (canvas == null) return;
                canvas.renderMode = mode;
                canvas.worldCamera = eye;
                canvas.sortingOrder = order;
                canvas.planeDistance = distance;
                for (var i = 0; i < all.Length; i++) if (all[i] != null) all[i].gameObject.layer = layers[i];
            }
        }

        // ---- 撮る物 ----------------------------------------------------------

        /// <summary>ログの見本。種類が一通り入るように</summary>
        public static void Sample()
        {
            var log = ConsoleLog.Here();
            log.Clear();
            log.AddLines(LogKind.Examine, "端末", new[]
            {
                "世間ではホロコンソールが人気だが私はもっぱら物理モニターを使っている",
                "というのもほら、",
                "こうして反射で自分の顔が見られるからだ",
            });
            log.AddChoice("チップを挿す", Choice.Yes);
            log.AddLines(LogKind.Monologue, "", new[]
            {
                "引き出しの奥に、使いかけのチップが三枚残っている",
                "どれも昨日の夜に抜いたもので、ラベルの字は私の癖のまま右へ傾いている",
            });
            log.AddLines(LogKind.Monologue, "", new[]
            {
                "ハンナ「メイ！　忘れもの！　上がっておいで！」",
                "メイ「えーなにー？」",
                "ハンナ「水筒！」",
                "メイ「投げてよー」",
                "ハンナ「投げません。いいから上がっておいで」",
            });
        }

        /// <summary>さかのぼりを見るための長いログ</summary>
        public static void LongSample()
        {
            Sample();
            var log = ConsoleLog.Here();
            var more = new[]
            {
                "ハンナ「水筒忘れるの毎日でしょ」",
                "メイ「きのうは忘れなかったもん」",
                "ハンナ「おとといは？」",
                "メイ「いってきまーす」",
                "ハンナ「今日は十時からなんです」",
                "メイ「ママ！」",
                "ハンナ「メイ？　学校は？」",
                "メイ「体操着わすれた」",
            };
            log.AddLines(LogKind.Monologue, "", more);
        }

        /// <summary>
        /// 開いている場面の物の一覧（道筋・有効か・コンポーネント）を name.txt に書く。
        /// 組み直す前と後で比べるのに使う。灯りの数と、URP の付属データが付いている数も返す
        /// </summary>
        public static string Dump(string name)
        {
            var scene = SceneManager.GetActiveScene();
            var sb = new System.Text.StringBuilder();
            foreach (var r in scene.GetRootGameObjects()) Walk(r.transform, "", sb);
            var lights = UnityEngine.Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            var extra = 0;
            foreach (var l in lights)
                if (l.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalLightData>() != null) extra++;
            Directory.CreateDirectory(Scratch);
            File.WriteAllText(Path.Combine(Scratch, name + ".txt"), sb.ToString());
            return scene.name + " 物 " + sb.ToString().Split('\n').Length + " 行、灯り " + lights.Length + "、URP の付属データ " + extra;
        }

        static void Walk(Transform t, string parent, System.Text.StringBuilder sb)
        {
            var path = parent + "/" + t.name;
            sb.Append(path).Append(t.gameObject.activeSelf ? " [on] " : " [off] ");
            foreach (var c in t.GetComponents<Component>()) sb.Append(c == null ? "MISSING" : c.GetType().Name).Append(',');
            sb.Append('\n');
            for (var i = 0; i < t.childCount; i++) Walk(t.GetChild(i), path, sb);
        }

        public static string Out(string name)
        {
            return Path.Combine(Scratch, name + ".png");
        }

        /// <summary>撮った絵の置き場。作業場の下</summary>
        public static string Scratch = Path.Combine(Path.GetTempPath(), "halfaware-shots");
    }
}
