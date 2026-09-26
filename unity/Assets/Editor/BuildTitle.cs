using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace HalfAware.EditorTools
{
    /// <summary>
    /// タイトルの画面の <c>Title.unity</c> を組む（設計書 5 節）。組み立ての一覧の先頭に入れる。
    ///
    /// 置くのはカメラ（黒く塗るだけ）と <see cref="TitleScreen"/>（背景の絵・環境音・明朝・潜るの日時と場所の参照）だけ。
    /// 見た目は <see cref="TitleScreen"/> が遊び始めに組む。**シーンは手で直さず、ここで組み直す。**
    ///
    /// 開いている場面には触らない。Title.unity を横に開いて組み、保存して閉じる（同じエディタで別の担当が場面を開いていても崩さない）。
    /// 環境音の大きさはオーナーが耳で決めるので、組み直しても前の値を引き継ぐ
    /// </summary>
    public static class BuildTitle
    {
        public const string ScenePath = "Assets/Scenes/Title.unity";
        public const string PictureDir = "Assets/Textures/Title";

        [MenuItem("HalfAware/Build the title", false, 180)]
        public static void BuildMenu()
        {
            Debug.Log(Build());
        }

        public static string Build()
        {
            if (EditorApplication.isPlaying) return "再生中は組まない。止めてからもう一度";
            var keep = SceneManager.GetActiveScene();
            var loaded = SceneManager.GetSceneByPath(ScenePath);
            if (loaded.IsValid() && loaded.isLoaded && loaded.isDirty)
                return "Title.unity に未保存の変更がある。保存するか捨ててからもう一度";
            Scene scene;
            var opened = false;
            if (loaded.IsValid() && loaded.isLoaded) scene = loaded;
            else if (File.Exists(ScenePath))
            {
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
                opened = true;
            }
            else
            {
                scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
                opened = true;
            }
            float volume = TitleScreen.DefaultVolume;
            try
            {
                foreach (var r in scene.GetRootGameObjects())
                {
                    var old = r.GetComponentInChildren<TitleScreen>(true);
                    if (old != null) volume = new SerializedObject(old).FindProperty("volume").floatValue;
                    Object.DestroyImmediate(r);
                }
                SceneManager.SetActiveScene(scene);
                Eye();
                var title = new GameObject("Title");
                var sound = title.AddComponent<AudioSource>();
                sound.playOnAwake = false;
                sound.loop = true;
                sound.spatialBlend = 0f;
                var screen = title.AddComponent<TitleScreen>();
                var note = Wire(screen, sound);
                var so = new SerializedObject(screen);
                so.FindProperty("volume").floatValue = volume;
                so.ApplyModifiedPropertiesWithoutUndo();
                // 3D の物は無いので、空も環境光も使わない
                RenderSettings.skybox = null;
                RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
                RenderSettings.ambientLight = Color.black;
                RenderSettings.fog = false;
                if (keep.IsValid() && keep != scene) SceneManager.SetActiveScene(keep);
                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene, ScenePath)) return "Title.unity を保存できなかった";
                Register();
                // 組み立ての一覧（ProjectSettings/EditorBuildSettings.asset）を書き出す
                AssetDatabase.SaveAssets();
                return "タイトルの画面を組んだ → " + ScenePath + "（" + note + "）";
            }
            finally
            {
                if (keep.IsValid() && keep != scene && keep.isLoaded) SceneManager.SetActiveScene(keep);
                if (opened && scene.IsValid() && SceneManager.sceneCount > 1) EditorSceneManager.CloseScene(scene, true);
            }
        }

        /// <summary>黒く塗るだけのカメラ。3D は写さない。耳もここに置く</summary>
        static void Eye()
        {
            var go = new GameObject("Main Camera");
            go.tag = "MainCamera";
            var cam = go.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            cam.cullingMask = 0;
            cam.orthographic = true;
            cam.allowHDR = false;
            cam.allowMSAA = false;
            var data = cam.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = false;
            data.renderShadows = false;
            go.AddComponent<AudioListener>();
        }

        /// <summary>
        /// 背景の絵・環境音・明朝・潜るの日時と場所を繋ぐ。撮るとき（<see cref="TitleShots"/>）にも使う。
        /// 何が欠けているかを返す
        /// </summary>
        public static string Wire(TitleScreen screen, AudioSource sound)
        {
            var missing = new List<string>();
            var so = new SerializedObject(screen);
            var pics = so.FindProperty("backdrops");
            var clips = so.FindProperty("sounds");
            pics.arraySize = TitleBackdrops.Count;
            clips.arraySize = TitleBackdrops.Count;
            for (var i = 0; i < TitleBackdrops.Count; i++)
            {
                var b = (TitleBackdrop)i;
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(PicturePath(b));
                if (tex == null) missing.Add(TitleBackdrops.FileName(b));
                pics.GetArrayElementAtIndex(i).objectReferenceValue = tex;
                var clipPath = SoundPath(b);
                clips.GetArrayElementAtIndex(i).objectReferenceValue =
                    clipPath != null ? AssetDatabase.LoadAssetAtPath<AudioClip>(clipPath) : null;
            }
            var mincho = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(BuildDrive.MinchoPath);
            if (mincho == null) missing.Add("明朝");
            so.FindProperty("mincho").objectReferenceValue = mincho;
            so.FindProperty("divingLine").stringValue = DivingLine();
            if (sound != null) so.FindProperty("sound").objectReferenceValue = sound;
            so.ApplyModifiedPropertiesWithoutUndo();
            return missing.Count == 0 ? "絵と音と書体は揃っている" : "無い物: " + string.Join("・", missing) + "（HalfAware/Shoot the title backgrounds で撮る）";
        }

        public static string PicturePath(TitleBackdrop b)
        {
            return PictureDir + "/" + TitleBackdrops.FileName(b) + ".png";
        }

        /// <summary>背景の場所の環境音。場面で流している輪を小さく流す。無い所（潜る・ガレージ）は無音</summary>
        static string SoundPath(TitleBackdrop b)
        {
            switch (b)
            {
                case TitleBackdrop.Room: return "Assets/Audio/RoomTone.wav";
                case TitleBackdrop.Alley: return "Assets/Audio/CrowdLoop.wav";
                case TitleBackdrop.VillageMorning: return "Assets/Audio/VillageMorning.wav";
                // 夕方の村は麦の風だけ（VillageAmbience と同じ）
                case TitleBackdrop.VillageEvening: return "Assets/Audio/WheatWind.wav";
                default: return null;
            }
        }

        /// <summary>潜るの背景に撮った記憶（記憶 0、メイ）の日時と場所。コンソールの頭の行と同じ書き方</summary>
        public static string DivingLine()
        {
            var roster = AssetDatabase.LoadAssetAtPath<DiveRoster>(BuildDive.RosterPath);
            if (roster == null || roster.Count == 0) return ConsolePlace.Diving;
            var entry = roster[TitleShots.DiveTake];
            return ConsolePlace.Dive(entry.row, entry.place);
        }

        /// <summary>組み立ての一覧の先頭へ入れる。書き出した物はタイトルの画面から始まる</summary>
        static void Register()
        {
            var all = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            var at = all.FindIndex(s => s.path == ScenePath);
            if (at == 0 && all[0].enabled) return;
            if (at >= 0) all.RemoveAt(at);
            all.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = all.ToArray();
            Debug.Log("組み立ての一覧の先頭へ Title.unity を入れた");
        }

        // ---- 確かめ ------------------------------------------------------------

        [MenuItem("HalfAware/Title: mark cleared", false, 181)]
        public static void MarkCleared()
        {
            SaveStore.MarkCleared();
            Debug.Log("クリアの印を付けた。タイトルの背景は朝の村になる");
        }

        [MenuItem("HalfAware/Title: unmark cleared", false, 182)]
        public static void UnmarkCleared()
        {
            SaveStore.ForgetCleared();
            Debug.Log("クリアの印を外した（確かめ用）");
        }

        [MenuItem("HalfAware/Title: mark cleared", true)]
        static bool CanMark()
        {
            return !SaveStore.Cleared;
        }

        [MenuItem("HalfAware/Title: unmark cleared", true)]
        static bool CanUnmark()
        {
            return SaveStore.Cleared;
        }

        [MenuItem("HalfAware/Title: forget the saves", false, 183)]
        public static void ForgetSaves()
        {
            foreach (var slot in SaveStore.All) SaveStore.Forget(slot);
            Debug.Log("セーブを全部消した（確かめ用）。クリアの印は残す");
        }
    }
}
