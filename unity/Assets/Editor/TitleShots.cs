using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace HalfAware.EditorTools
{
    /// <summary>
    /// タイトルの画面の背景の絵を撮る（設計書 5 節の表の目と向き）と、タイトルの画面を Canvas ごと撮って確かめる。
    ///
    /// 背景は、ゲームと同じ見え方で撮る。場面の Player/Main Camera を写したカメラ（後処理を通す）で 960×540 に撮ると、
    /// パイプラインの render scale 1/3 で中は 320×180 になり、それを最近傍で 3 倍に引き伸ばした絵になる。
    /// 置くのは中の 320×180（3×3 の塊の真ん中の画素）。最近傍で画面に敷けば同じ絵になり、重さは 9 分の 1。
    /// 撮り方は <see cref="CheckVillage.Game"/> に倣う。村は <see cref="VillageHour"/> で朝と夕方の二枚。
    /// 潜るは記憶 0（メイ）の場所と記憶を起こし、記憶の色味（Color Filter）を一時の Volume で掛ける。
    ///
    /// **場面は開くが保存しない。** 撮る前に開いていた場面へ、撮り終えたら開き直す（撮る間に変えた時刻や起こした場所は捨てる）。
    /// 開いている場面に未保存の変更があるときは撮らない。
    /// カメラは <c>enabled = false</c>・<c>HideAndDontSave</c> で作り、撮り終えたら捨てる
    /// </summary>
    public static class TitleShots
    {
        /// <summary>潜るの背景に撮る記憶。記憶 0（メイ）</summary>
        public const int DiveTake = 0;

        /// <summary>撮る所（名前・目・向き（+z が 0 度で東回り）・俯き（下が正））</summary>
        public static CheckVillage.View ViewOf(TitleBackdrop b)
        {
            var name = TitleBackdrops.FileName(b);
            switch (b)
            {
                // 玄関の戸の前（南の壁際）から北を見る
                case TitleBackdrop.Room: return new CheckVillage.View(name, new Vector3(1.0f, 1.6f, -2.4f), 0f, 4f);
                // 場面 2 の始まりの立ち位置から北へ、通りの奥を見上げる
                case TitleBackdrop.Alley: return new CheckVillage.View(name, new Vector3(0f, 1.7f, -2.9f), 0f, -14f);
                // 記憶 0（メイ）。三階のデッキを西へ、A の戸口の脇から
                case TitleBackdrop.Dive: return new CheckVillage.View(name, new Vector3(6.8f, 6.6f, -13.5f), 240f, -2f);
                // 場面 8 の頭。ガレージの始まりの立ち位置から北北西
                case TitleBackdrop.Drive: return new CheckVillage.View(name, new Vector3(4.1f, 1.7f, -6.8f), 345f, 6f);
                // 東屋の側から、裏庭のアーチのトンネルを軸に沿って正面に（CheckVillage.GardenViews の g1_title と同じ目）
                default:
                    var title = CheckVillage.GardenViews()[0];
                    return new CheckVillage.View(name, title.Eye, title.Yaw, title.Pitch);
            }
        }

        [MenuItem("HalfAware/Shoot the title backgrounds", false, 184)]
        public static void ShootMenu()
        {
            Debug.Log(ShootAll());
        }

        /// <summary>全部撮る。同じ場面の物は一度開いて続けて撮る</summary>
        public static string ShootAll()
        {
            var sb = new StringBuilder();
            sb.AppendLine(Shoot(TitleBackdrop.Room));
            sb.AppendLine(Shoot(TitleBackdrop.Alley));
            sb.AppendLine(Shoot(TitleBackdrop.Dive));
            sb.AppendLine(Shoot(TitleBackdrop.Drive));
            sb.AppendLine(Shoot(TitleBackdrop.VillageMorning, TitleBackdrop.VillageEvening));
            return sb.ToString();
        }

        /// <summary>
        /// 背景を撮って <c>Assets/Textures/Title/</c> に置く。渡す物はみな同じ場面のもの。
        /// 撮る前に開いていた場面へ戻す
        /// </summary>
        public static string Shoot(params TitleBackdrop[] which)
        {
            if (EditorApplication.isPlaying) return "再生中は撮らない";
            if (which == null || which.Length == 0) return "撮る物が無い";
            for (var i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty)
                    return "開いている場面に未保存の変更がある。保存するか捨ててからもう一度: " + SceneManager.GetSceneAt(i).path;
            var setup = EditorSceneManager.GetSceneManagerSetup();
            var path = "Assets/Scenes/" + TitleBackdrops.SceneOf(which[0]) + ".unity";
            var sb = new StringBuilder();
            // シェーダーを後から組む設定のままだと、組み終わっていない物が写らない（開いたばかりの場面で黒く抜ける）
            var async = ShaderUtil.allowAsyncCompilation;
            try
            {
                ShaderUtil.allowAsyncCompilation = false;
                var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                if (!scene.IsValid()) return "開けない: " + path;
                foreach (var b in which)
                {
                    if (TitleBackdrops.SceneOf(b) != TitleBackdrops.SceneOf(which[0]))
                    {
                        sb.AppendLine(b + ": 別の場面の物は別に撮る");
                        continue;
                    }
                    sb.AppendLine(ShootOpen(b));
                }
            }
            catch (Exception e)
            {
                sb.AppendLine("例外: " + e);
            }
            finally
            {
                ShaderUtil.allowAsyncCompilation = async;
                Back(setup);
            }
            return sb.ToString().TrimEnd();
        }

        /// <summary>撮る前の場面へ戻す。撮る間に汚した場面は、開き直して捨てる</summary>
        static void Back(SceneSetup[] setup)
        {
            if (SceneManager.GetActiveScene().isDirty) EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var usable = setup != null && setup.Length > 0;
            if (usable)
                foreach (var s in setup)
                    if (string.IsNullOrEmpty(s.path)) usable = false;
            if (usable) EditorSceneManager.RestoreSceneManagerSetup(setup);
        }

        /// <summary>開いている場面で一枚撮って置く</summary>
        static string ShootOpen(TitleBackdrop b)
        {
            var view = ViewOf(b);
            Texture2D shot = null;
            try
            {
                if (b == TitleBackdrop.VillageMorning) BuildVillage.SetHour(VillageHour.Hour.Morning);
                if (b == TitleBackdrop.VillageEvening) BuildVillage.SetHour(VillageHour.Hour.Evening);
                shot = b == TitleBackdrop.Dive ? Dive(view) : Game(view);
                if (shot == null) return b + ": カメラが無い";
                var loose = 0;
                var small = Shrink(shot, out loose);
                try
                {
                    var path = BuildTitle.PicturePath(b);
                    CheckDiveSky.Save(small, path);
                    Import(path);
                    return string.Format("{0} → {1}（320×180。3×3 が揃っていない塊 {2}）", b, path, loose);
                }
                finally
                {
                    Object.DestroyImmediate(small);
                }
            }
            finally
            {
                if (shot != null) Object.DestroyImmediate(shot);
            }
        }

        /// <summary>ゲームの目（Player/Main Camera）を写したカメラで、960×540 に一枚</summary>
        static Texture2D Game(CheckVillage.View v)
        {
            var main = Main();
            if (main == null) return null;
            var go = new GameObject("TitleShotEye");
            go.hideFlags = HideFlags.HideAndDontSave;
            try
            {
                var cam = go.AddComponent<Camera>();
                cam.enabled = false;
                cam.CopyFrom(main);
                cam.GetUniversalAdditionalCameraData().renderPostProcessing = true;
                go.transform.position = v.Eye;
                go.transform.rotation = Quaternion.Euler(v.Pitch, v.Yaw, 0f);
                return Steady(cam);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        /// <summary>
        /// 絵が落ち着くまで撮り直す。開いたばかりの場面の一枚目は、壁や道が抜けることがある
        /// （エディタを組み直した後の最初の一度だけ起きる。二枚目からは揃う）。続けて撮った二枚が同じになったら採る
        /// </summary>
        static Texture2D Steady(Camera cam)
        {
            var shot = CheckDiveSky.Grab(cam, 960, 540);
            for (var i = 0; i < 4; i++)
            {
                var again = CheckDiveSky.Grab(cam, 960, 540);
                var same = CheckDiveSky.Diff(shot, again).y == 0f;
                Object.DestroyImmediate(shot);
                shot = again;
                if (same) break;
            }
            return shot;
        }

        /// <summary>記憶 0 の場所と記憶を起こし、その場所の空と記憶の色味で撮る。抜けるときに全部戻す</summary>
        static Texture2D Dive(CheckVillage.View v)
        {
            var roster = AssetDatabase.LoadAssetAtPath<DiveRoster>(BuildDive.RosterPath);
            if (roster == null) return null;
            var entry = roster[DiveTake];
            var main = Main();
            if (main == null) return null;
            GameObject eye = null, vol = null;
            VolumeProfile profile = null;
            using (var stage = new CheckDiveSky.Stage(entry.place, DiveTake))
            {
                try
                {
                    var sky = CheckDiveSky.SkyOf(entry.place);
                    sky.Apply(null);
                    eye = new GameObject("TitleShotEye");
                    eye.hideFlags = HideFlags.HideAndDontSave;
                    var cam = eye.AddComponent<Camera>();
                    cam.enabled = false;
                    cam.CopyFrom(main);
                    cam.clearFlags = sky.skybox != null ? CameraClearFlags.Skybox : CameraClearFlags.SolidColor;
                    cam.backgroundColor = sky.flat;
                    cam.GetUniversalAdditionalCameraData().renderPostProcessing = true;
                    var at = stage.Place != null ? stage.Place.TransformPoint(v.Eye) : v.Eye;
                    eye.transform.position = at;
                    eye.transform.rotation = Quaternion.Euler(v.Pitch, v.Yaw, 0f);

                    // 記憶の色味だけを掛ける。ぼやけは掛けない（背景は暗く沈めて字を重ねるので）
                    vol = new GameObject("TitleShotTint");
                    vol.hideFlags = HideFlags.HideAndDontSave;
                    profile = ScriptableObject.CreateInstance<VolumeProfile>();
                    profile.hideFlags = HideFlags.HideAndDontSave;
                    var tone = profile.Add<ColorAdjustments>(false);
                    tone.hideFlags = HideFlags.HideAndDontSave;
                    tone.colorFilter.Override(entry.tint);
                    var volume = vol.AddComponent<Volume>();
                    volume.isGlobal = true;
                    volume.priority = 100f;
                    volume.weight = 1f;
                    volume.sharedProfile = profile;
                    return Steady(cam);
                }
                finally
                {
                    if (vol != null) Object.DestroyImmediate(vol);
                    if (profile != null)
                    {
                        foreach (var c in profile.components) if (c != null) Object.DestroyImmediate(c);
                        Object.DestroyImmediate(profile);
                    }
                    if (eye != null) Object.DestroyImmediate(eye);
                }
            }
        }

        static Camera Main()
        {
            var go = GameObject.Find("Player/Main Camera");
            if (go != null && go.GetComponent<Camera>() != null) return go.GetComponent<Camera>();
            var player = Object.FindFirstObjectByType<PlayerController>(FindObjectsInactive.Include);
            if (player != null && player.Eye != null)
            {
                var c = player.Eye.GetComponentInChildren<Camera>(true);
                if (c != null) return c;
            }
            return Camera.main;
        }

        /// <summary>
        /// 960×540 を中の 320×180 へ。3×3 の塊の真ん中の画素を採る。
        /// loose は 3×3 が同じ色でない塊の数（後処理が画面の解像度で掛かるようになると増える。そのときは縮めずに置く）
        /// </summary>
        static Texture2D Shrink(Texture2D big, out int loose)
        {
            var w = big.width / 3;
            var h = big.height / 3;
            var src = big.GetPixels32();
            var dst = new Color32[w * h];
            loose = 0;
            for (var y = 0; y < h; y++)
                for (var x = 0; x < w; x++)
                {
                    var c = src[(y * 3 + 1) * big.width + x * 3 + 1];
                    dst[y * w + x] = c;
                    var same = true;
                    for (var j = 0; j < 3 && same; j++)
                        for (var i = 0; i < 3 && same; i++)
                        {
                            var o = src[(y * 3 + j) * big.width + x * 3 + i];
                            same = o.r == c.r && o.g == c.g && o.b == c.b;
                        }
                    if (!same) loose++;
                }
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false, false);
            tex.SetPixels32(dst);
            tex.Apply();
            return tex;
        }

        /// <summary>絵の取り込み。最近傍、ミップ無し、圧縮しない（粗い画素をそのまま出す）</summary>
        static void Import(string path)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var imp = AssetImporter.GetAtPath(path) as TextureImporter;
            if (imp == null) return;
            imp.textureType = TextureImporterType.Default;
            imp.sRGBTexture = true;
            imp.mipmapEnabled = false;
            imp.filterMode = FilterMode.Point;
            imp.wrapMode = TextureWrapMode.Clamp;
            imp.npotScale = TextureImporterNPOTScale.None;
            imp.alphaSource = TextureImporterAlphaSource.None;
            imp.textureCompression = TextureImporterCompression.Uncompressed;
            imp.maxTextureSize = 512;
            imp.SaveAndReimport();
        }

        // ---- タイトルの画面を Canvas ごと撮る -------------------------------------

        /// <summary>
        /// タイトルの画面を、ゲームと同じ見え方で撮る（960×540）。背景の Canvas は画面の解像度で、
        /// 枠と字の Canvas は粗い画面（UiLens）で描いて重ねる。セーブは手元の辞書（saves）に差し替えて撮る
        /// （PlayerPrefs を汚さない）。recall で思い出すの枠を開いた形
        /// </summary>
        public static string Screen(string path, bool cleared, SaveData[] saves, bool recall)
        {
            const int w = 960;
            const int h = 540;
            var box = new MemoryBox();
            if (saves != null)
                for (var i = 0; i < saves.Length && i < SaveStore.All.Length; i++)
                    if (saves[i] != null) box.Set(SaveStore.KeyOf(SaveStore.All[i]), JsonUtility.ToJson(saves[i]));
            if (cleared) box.Set(SaveStore.ClearedKey, "1");
            var wasDirty = SceneManager.GetActiveScene().isDirty;
            GameObject holder = null, backEye = null;
            TitleScreen screen = null;
            UiLens lens = null;
            RenderTexture rt = null;
            Texture2D back = null, ui = null, shot = null;
            var asset = UniversalRenderPipeline.asset;
            var keptScale = asset != null ? asset.renderScale : 1f;
            var async = ShaderUtil.allowAsyncCompilation;
            try
            {
                ShaderUtil.allowAsyncCompilation = false;
                SaveStore.Box = box;
                holder = new GameObject("TitleShot");
                holder.hideFlags = HideFlags.HideAndDontSave;
                screen = holder.AddComponent<TitleScreen>();
                BuildTitle.Wire(screen, null);
                screen.Compose(SaveStore.Cleared, SaveStore.Newest());
                foreach (var t in holder.GetComponentsInChildren<Transform>(true)) t.gameObject.hideFlags = HideFlags.HideAndDontSave;

                // 背景。画面の解像度で、UI のレンダラー（全画面の後処理なし）で描く
                backEye = new GameObject("TitleShotBackEye");
                backEye.hideFlags = HideFlags.HideAndDontSave;
                backEye.transform.position = new Vector3(0f, -6000f, 0f);
                var cam = backEye.AddComponent<Camera>();
                cam.enabled = false;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = Color.black;
                cam.cullingMask = 1 << UiLens.UiLayer;
                cam.orthographic = true;
                cam.nearClipPlane = 0.1f;
                cam.farClipPlane = 20f;
                var data = cam.GetUniversalAdditionalCameraData();
                data.renderPostProcessing = false;
                data.antialiasing = AntialiasingMode.None;
                var look = Resources.Load<UiLook>(UiLook.Path);
                if (asset != null && look != null)
                    for (var i = 0; i < asset.rendererDataList.Length; i++)
                        if (asset.rendererDataList[i] == look.renderer) data.SetRenderer(i);
                rt = new RenderTexture(w, h, 16, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                rt.hideFlags = HideFlags.HideAndDontSave;
                cam.targetTexture = rt;
                ConsoleShot.Lens(screen.BackdropCanvas, cam, 0);

                // 枠と字。ゲームと同じ粗い画面で
                lens = UiLens.Make();
                lens.Fit(w, h);
                ConsoleShot.Lens(screen.ScreenCanvas, lens.Eye, TitleScreen.SortingOrder);
                // 起動を出し切った形にしてから描く
                screen.Finish();
                if (recall) screen.OpenList();

                if (asset != null) asset.renderScale = 1f;
                Canvas.ForceUpdateCanvases();
                cam.Render();
                if (asset != null) asset.renderScale = keptScale;
                cam.targetTexture = null;
                back = ConsoleShot.Read(rt);
                lens.Draw();
                ui = ConsoleShot.Read(lens.Target);
                shot = ConsoleShot.Blend(back, ui, w, h);
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllBytes(path, shot.EncodeToPNG());
                return string.Format("撮った {0}（背景 {1}、UI {2}x{3}）", path, screen.Backdrop, lens.Target.width, lens.Target.height);
            }
            catch (Exception e)
            {
                return "例外: " + e;
            }
            finally
            {
                ShaderUtil.allowAsyncCompilation = async;
                if (asset != null) asset.renderScale = keptScale;
                SaveStore.Box = null;
                // 片づけは一つ転んでも残りを続ける
                try
                {
                    if (screen != null) screen.Release();
                }
                catch (Exception e)
                {
                    Debug.LogWarning("TitleShots: 絵とマテリアルを捨てられなかった: " + e.Message);
                }
                if (lens != null) { lens.Release(); Object.DestroyImmediate(lens.gameObject); }
                if (holder != null) Object.DestroyImmediate(holder);
                // TextMeshPro が図の枚ごとに作る写しのマテリアル（「TitleHeavy + …Atlas 1」）も捨てる
                foreach (var m in Resources.FindObjectsOfTypeAll<Material>())
                    if (m != null && string.IsNullOrEmpty(AssetDatabase.GetAssetPath(m))
                        && (m.name.StartsWith("TitleHeavy") || m.name.StartsWith("TitleGlow") || m.name.StartsWith("TitleShade")))
                        Object.DestroyImmediate(m);
                if (backEye != null) Object.DestroyImmediate(backEye);
                if (rt != null) { rt.Release(); Object.DestroyImmediate(rt); }
                if (back != null) Object.DestroyImmediate(back);
                if (ui != null) Object.DestroyImmediate(ui);
                if (shot != null) Object.DestroyImmediate(shot);
                var scene = SceneManager.GetActiveScene();
                if (!wasDirty && scene.isDirty) ClearDirty(scene);
            }
        }

        static void ClearDirty(Scene scene)
        {
            var m = typeof(EditorSceneManager).GetMethod("ClearSceneDirtiness",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            if (m != null) m.Invoke(null, new object[] { scene });
        }

        /// <summary>撮るときの見本のセーブ。自動・1・2・3 の並び（null は空き）</summary>
        public static SaveData[] Sample(int newestStage)
        {
            var now = new DateTime(2026, 9, 27, 5, 0, 0, DateTimeKind.Utc);
            var saves = new SaveData[SaveStore.All.Length];
            saves[0] = Made(newestStage, now.AddMinutes(40));
            saves[1] = Made(2, now);
            saves[3] = Made(5, now.AddMinutes(20));
            return saves;
        }

        static SaveData Made(int stage, DateTime utc)
        {
            var scene = StageMap.SceneOf(stage) ?? "Village";
            return new SaveData
            {
                stage = stage,
                scene = scene,
                hour = stage == 6 ? StageMap.Evening : StageMap.HourOf(stage),
                written = SaveStore.Stamp(LocalClock.ToLocal(utc)),
                writtenTicks = utc.Ticks,
            };
        }

        /// <summary>見本のセーブを手元の辞書に入れて、コンソールの記憶する・思い出すの枠を開いた形で撮る。開いている場面で撮る</summary>
        public static string Console(string path, ConsolePanel panel)
        {
            var box = new MemoryBox();
            var saves = Sample(3);
            for (var i = 0; i < saves.Length; i++)
                if (saves[i] != null) box.Set(SaveStore.KeyOf(SaveStore.All[i]), JsonUtility.ToJson(saves[i]));
            var async = ShaderUtil.allowAsyncCompilation;
            try
            {
                ShaderUtil.allowAsyncCompilation = false;
                SaveStore.Box = box;
                return ConsoleShot.Shoot(path, 960, 540, UiLens.Scale, true, 0f, panel, null);
            }
            finally
            {
                ShaderUtil.allowAsyncCompilation = async;
                SaveStore.Box = null;
            }
        }
    }
}
