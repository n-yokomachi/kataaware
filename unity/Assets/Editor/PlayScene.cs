using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace HalfAware.EditorTools
{
    /// <summary>
    /// 場面の一覧。タブとして開いておけば、そこから直に開いて再生できる。
    /// どの場面を開いていても、押した場面から始められる
    /// </summary>
    public sealed class SceneWindow : EditorWindow
    {
        struct Entry
        {
            public string title;
            public string path;
            public string note;
        }

        static readonly Entry[] Scenes =
        {
            new Entry { title = "自室", path = "Assets/Scenes/Room.unity", note = "場面 1。ロンドンの安宿。煙草と記憶の抜き取り" },
            new Entry { title = "路地裏", path = "Assets/Scenes/Alley.unity", note = "場面 2。グレビル・ストリートとブリーディング・ハート・ヤード" },
            new Entry { title = "自室・接続", path = "Assets/Scenes/Connect.unity", note = "場面 3。売上を書き足し、ジャックを繋いで潜る" },
            new Entry { title = "潜る", path = "Assets/Scenes/Dive.unity", note = "場面 4。他人の記憶を人から人へ渡り歩き、切断で戻る" },
            new Entry { title = "小休止", path = "Assets/Scenes/Rest.unity", note = "場面 5。座ったまま眩暈が薄れ、一本吸って、もう一度潜る" },
            new Entry { title = "車内", path = "Assets/Scenes/Drive.unity", note = "場面 8。ガレージから乗り込み、3 つの景色を抜ける" },
        };

        [MenuItem("HalfAware/Scenes", false, 0)]
        public static void Open()
        {
            var w = GetWindow<SceneWindow>("Scenes");
            w.minSize = new Vector2(240f, 140f);
            w.Focus();
        }

        void OnGUI()
        {
            var open = EditorSceneManager.GetActiveScene().path;
            EditorGUILayout.Space(6f);
            for (var i = 0; i < Scenes.Length; i++)
            {
                var e = Scenes[i];
                var here = e.path == open;
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField(here ? e.title + "（開いている）" : e.title, EditorStyles.boldLabel);
                EditorGUILayout.LabelField(e.note, EditorStyles.wordWrappedMiniLabel);
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("開く")) Open(e.path, false);
                if (GUILayout.Button("開いて再生")) Open(e.path, true);
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
                EditorGUILayout.Space(4f);
            }

            EditorGUILayout.Space(4f);
            using (new EditorGUI.DisabledScope(!EditorApplication.isPlaying))
                if (GUILayout.Button("再生を止める")) EditorApplication.isPlaying = false;
        }

        /// <summary>場面を開く。再生中なら一度止めてから。保存は本人に訊く</summary>
        static void Open(string path, bool play)
        {
            if (EditorApplication.isPlaying) EditorApplication.isPlaying = false;
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) == null)
            {
                Debug.LogError("場面が無い: " + path);
                return;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            if (play) EditorApplication.isPlaying = true;
        }
    }
}
