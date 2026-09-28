using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace HalfAware.EditorTools
{
    /// <summary>
    /// エンディングを、再生せずにゲームと同じ見え方で撮る（960×540）。3D は場面の目のカメラで（1/3 の粗さと後処理ごと）、
    /// クレジットの見出しと名前の行と黒は粗い画面（UiLens）で、題と読みは画面の解像度で描いて重ねる。
    ///
    /// 曲の秒を渡すと、段取り（<see cref="EndingDirector.Apply"/>）と道の流れ（走った距離）をその秒に合わせてから撮る。
    /// 撮る間に場面へ手を入れるので、撮り終えたら開き直して捨てる
    /// </summary>
    public static class CheckEnding
    {
        /// <summary>撮る所。名と曲の秒</summary>
        public struct Take
        {
            public string name;
            public float song;
            public Take(string name, float song) { this.name = name; this.song = song; }
        }

        /// <summary>確かめに要る所。明けた所・片割れへ向いた所・クレジットの頭と途中と終わり</summary>
        public static Take[] Takes(EndingBeats b)
        {
            return new[]
            {
                new Take("open", b.openFade + 0.5f),
                new Take("glance", b.glanceAt + b.glanceTurn + b.glanceHold * 0.5f),
                new Take("credits_head", b.creditsFrom + 22f),
                new Take("credits_mid", (b.creditsFrom + b.creditsTo) * 0.5f),
                new Take("credits_end", b.creditsTo + 3f),
            };
        }

        /// <summary>帯ごとに一枚。帯に入って 12 秒（短い帯は半ば）。クレジットも重なった、ゲームと同じ見え方</summary>
        public static Take[] BandTakes(EndingBand[] bands, EndingBeats b)
        {
            var takes = new Take[bands.Length];
            for (var i = 0; i < bands.Length; i++)
            {
                var end = i + 1 < bands.Length ? bands[i + 1].from : b.fadeOutFrom;
                var t = bands[i].from + Mathf.Min(12f, (end - bands[i].from) * 0.5f);
                if (i == 0) t = 20f;
                takes[i] = new Take("band" + (i + 1), t);
            }
            return takes;
        }

        public static string Shoot(string dir)
        {
            return Shoot(dir, null);
        }

        /// <summary>帯ごとに一枚ずつ撮る（<see cref="BandTakes"/>）</summary>
        public static string ShootBands(string dir)
        {
            return Shoot(dir, null, true);
        }

        /// <summary>dir へ撮る。takes が null なら <see cref="Takes"/> を全部</summary>
        public static string Shoot(string dir, Take[] takes)
        {
            return Shoot(dir, takes, false);
        }

        static string Shoot(string dir, Take[] takes, bool bands)
        {
            if (EditorApplication.isPlaying) return "再生中は撮らない";
            for (var i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty)
                    return "開いている場面に未保存の変更がある: " + SceneManager.GetSceneAt(i).path;
            var setup = EditorSceneManager.GetSceneManagerSetup();
            var sb = new StringBuilder();
            sb.AppendLine("RenderTexture（どのアセットにも属さない）: 始め " + CheckVillage.LooseRenderTextures());
            var async = ShaderUtil.allowAsyncCompilation;
            try
            {
                ShaderUtil.allowAsyncCompilation = false;
                EditorSceneManager.OpenScene(BuildEnding.ScenePath, OpenSceneMode.Single);
                var d = UnityEngine.Object.FindFirstObjectByType<EndingDirector>(FindObjectsInactive.Include);
                if (d == null) return "EndingDirector が無い";
                if (takes == null) takes = bands ? BandTakes(d.Bands, d.Beats) : Takes(d.Beats);
                Directory.CreateDirectory(dir);
                foreach (var t in takes)
                    sb.AppendLine(t.name + ": " + One(d, t, Path.Combine(dir, "ending_" + t.name + ".png")));
            }
            catch (Exception e)
            {
                sb.AppendLine("例外: " + e);
            }
            finally
            {
                ShaderUtil.allowAsyncCompilation = async;
                TitleShots.Back(setup);
                sb.AppendLine("RenderTexture（どのアセットにも属さない）: 終わり " + CheckVillage.LooseRenderTextures());
            }
            return sb.ToString().TrimEnd();
        }

        /// <summary>曲の秒 song の形にして撮る</summary>
        static string One(EndingDirector d, Take t, string path)
        {
            var so = new SerializedObject(d);
            var world = (DriveWorld)so.FindProperty("world").objectReferenceValue;
            Roll(world, Travelled(d, t.song));
            d.Apply(t.song);
            // クレジットの並びは列の高さから決まる。撮る Canvas に替えてから（1280×720 になってから）当て直す
            return Grab(path, () => d.Apply(t.song));
        }

        /// <summary>曲の秒 song までに走った距離。黒のあいだから走っていて、帯ごとに速さが替わる</summary>
        public static float Travelled(EndingDirector d, float song)
        {
            var bands = d.Bands;
            var far = 0f;
            var from = -d.Beats.OpenAt;
            for (var i = 0; i < bands.Length; i++)
            {
                var start = i == 0 ? from : bands[i].from;
                var end = i + 1 < bands.Length ? bands[i + 1].from : float.MaxValue;
                if (song <= start) break;
                far += bands[i].speed * (Mathf.Min(song, end) - start);
            }
            return far;
        }

        /// <summary>道と沿道を、走った距離 travelled の並びへ置く（DriveWorld.Place と同じ計算）。どの帯を出すかは EndingDirector.Apply が決める</summary>
        public static void Roll(DriveWorld world, float travelled)
        {
            if (world == null) return;
            var so = new SerializedObject(world);
            var length = so.FindProperty("tileLength").floatValue;
            var behind = so.FindProperty("behind").floatValue;
            var tiles = so.FindProperty("tiles");
            for (var i = 0; i < tiles.arraySize; i++) Slide((Transform)tiles.GetArrayElementAtIndex(i).objectReferenceValue, i, tiles.arraySize, length, travelled, behind);
            var sides = so.FindProperty("roadsides");
            for (var b = 0; b < sides.arraySize; b++)
            {
                var band = (Transform)sides.GetArrayElementAtIndex(b).objectReferenceValue;
                if (band == null) continue;
                for (var i = 0; i < band.childCount; i++) Slide(band.GetChild(i), i, band.childCount, length, travelled, behind);
            }
        }

        static void Slide(Transform what, int i, int n, float length, float travelled, float behind)
        {
            if (what == null) return;
            var at = what.localPosition;
            what.localPosition = new Vector3(at.x, at.y, RoadRing.Slot(i, n, length, travelled, behind));
        }

        /// <summary>
        /// いまの形を 960×540 で撮って path へ書く。refresh は UI の Canvas を撮る向きに替えた後に呼ぶ
        /// （エディタの Game の画面の大きさのままでは、クレジットの列の高さが違い、並びがずれる）
        /// </summary>
        public static string Grab(string path, System.Action refresh = null)
        {
            const int w = 960, h = 540;
            var main = TitleShots.Main();
            if (main == null) return "カメラが無い";
            var hud = GameObject.Find("Hud");
            var card = GameObject.Find("HudCard");
            GameObject eye = null, crispEye = null;
            UiLens lens = null;
            RenderTexture crispRt = null;
            Texture2D scene = null, ui = null, mixed = null, crisp = null, final = null;
            var asset = UniversalRenderPipeline.asset;
            var keptScale = asset != null ? asset.renderScale : 1f;
            try
            {
                eye = new GameObject("EndingShotEye") { hideFlags = HideFlags.HideAndDontSave };
                var cam = eye.AddComponent<Camera>();
                cam.enabled = false;
                cam.CopyFrom(main);
                cam.GetUniversalAdditionalCameraData().renderPostProcessing = true;
                eye.transform.SetPositionAndRotation(main.transform.position, main.transform.rotation);
                scene = TitleShots.Steady(cam);

                lens = UiLens.Make();
                lens.Fit(w, h);
                if (hud != null) ConsoleShot.Lens(hud.GetComponent<Canvas>(), lens.Eye, 0, w, h);
                if (card != null) ConsoleShot.Lens(card.GetComponent<Canvas>(), lens.Eye, -1, w, h);
                if (refresh != null)
                {
                    refresh();
                    Canvas.ForceUpdateCanvases();
                }
                // くっきり描く列は粗い画面では描かない（下で画面の解像度で描く）
                if (card != null) card.GetComponent<Canvas>().enabled = false;
                lens.Draw();
                if (card != null) card.GetComponent<Canvas>().enabled = true;
                ui = ConsoleShot.Read(lens.Target);
                mixed = ConsoleShot.Blend(scene, ui, w, h);

                if (card != null)
                {
                    // 題と読み。画面の解像度で、透明な地に描いてから重ねる（TitleShots.Screen と同じ）
                    crispEye = new GameObject("EndingShotCrisp") { hideFlags = HideFlags.HideAndDontSave };
                    crispEye.transform.position = new Vector3(0f, -7000f, 0f);
                    var cc = crispEye.AddComponent<Camera>();
                    cc.enabled = false;
                    cc.clearFlags = CameraClearFlags.SolidColor;
                    cc.backgroundColor = new Color(0f, 0f, 0f, 0f);
                    cc.cullingMask = 1 << UiLens.UiLayer;
                    cc.orthographic = true;
                    cc.nearClipPlane = 0.1f;
                    cc.farClipPlane = 20f;
                    cc.allowHDR = false;
                    cc.allowMSAA = false;
                    var data = cc.GetUniversalAdditionalCameraData();
                    data.renderPostProcessing = false;
                    data.antialiasing = AntialiasingMode.None;
                    data.requiresColorOption = CameraOverrideOption.Off;
                    data.requiresDepthOption = CameraOverrideOption.Off;
                    var look = Resources.Load<UiLook>(UiLook.Path);
                    if (asset != null && look != null)
                        for (var i = 0; i < asset.rendererDataList.Length; i++)
                            if (asset.rendererDataList[i] == look.renderer) data.SetRenderer(i);
                    crispRt = new RenderTexture(w, h, 16, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { hideFlags = HideFlags.HideAndDontSave };
                    cc.targetTexture = crispRt;
                    ConsoleShot.Lens(card.GetComponent<Canvas>(), cc, UiLens.ShowOrder + 1, w, h);
                    if (asset != null) asset.renderScale = 1f;
                    Canvas.ForceUpdateCanvases();
                    cc.Render();
                    if (asset != null) asset.renderScale = keptScale;
                    cc.targetTexture = null;
                    crisp = ConsoleShot.Read(crispRt);
                    final = ConsoleShot.Blend(mixed, crisp, w, h);
                }
                var png = (final != null ? final : mixed).EncodeToPNG();
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllBytes(path, png);
                return path;
            }
            finally
            {
                if (asset != null) asset.renderScale = keptScale;
                if (lens != null) { lens.Release(); UnityEngine.Object.DestroyImmediate(lens.gameObject); }
                if (eye != null) UnityEngine.Object.DestroyImmediate(eye);
                if (crispEye != null) UnityEngine.Object.DestroyImmediate(crispEye);
                if (crispRt != null) { crispRt.Release(); UnityEngine.Object.DestroyImmediate(crispRt); }
                foreach (var tex in new[] { scene, ui, mixed, crisp, final })
                    if (tex != null) UnityEngine.Object.DestroyImmediate(tex);
            }
        }

        /// <summary>
        /// 片割れの座った体を、目の外から見る（形の確かめ用。ゲームの見え方ではない）。from と at は車の座標
        /// </summary>
        public static string Look(string path, Vector3 from, Vector3 at, float fov = 50f)
        {
            var main = TitleShots.Main();
            if (main == null) return "カメラが無い";
            GameObject eye = null;
            Texture2D shot = null;
            try
            {
                eye = new GameObject("EndingLookEye") { hideFlags = HideFlags.HideAndDontSave };
                var cam = eye.AddComponent<Camera>();
                cam.enabled = false;
                cam.CopyFrom(main);
                cam.fieldOfView = fov;
                cam.nearClipPlane = 0.02f;
                cam.GetUniversalAdditionalCameraData().renderPostProcessing = true;
                var car = GameObject.Find("Ending/Car");
                var t = car != null ? car.transform : null;
                var p = t != null ? t.TransformPoint(from) : from;
                var q = t != null ? t.TransformPoint(at) : at;
                eye.transform.SetPositionAndRotation(p, Quaternion.LookRotation(q - p, Vector3.up));
                shot = TitleShots.Steady(cam);
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllBytes(path, shot.EncodeToPNG());
                return path;
            }
            finally
            {
                if (eye != null) UnityEngine.Object.DestroyImmediate(eye);
                if (shot != null) UnityEngine.Object.DestroyImmediate(shot);
            }
        }
    }
}
