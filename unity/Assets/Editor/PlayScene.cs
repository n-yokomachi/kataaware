using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace HalfAware.EditorTools
{
    /// <summary>
    /// 場面を開いて再生する近道。どの場面を開いていても、
    /// メニューひとつで自室から、あるいは路地裏から始められる
    /// </summary>
    public static class PlayScene
    {
        const string Room = "Assets/Scenes/Room.unity";
        const string Alley = "Assets/Scenes/Alley.unity";

        [MenuItem("HalfAware/Play the room _F5", false, 100)]
        public static void PlayRoom()
        {
            Open(Room, true);
        }

        [MenuItem("HalfAware/Play the alley _F6", false, 101)]
        public static void PlayAlley()
        {
            Open(Alley, true);
        }

        [MenuItem("HalfAware/Open the room", false, 120)]
        public static void OpenRoom()
        {
            Open(Room, false);
        }

        [MenuItem("HalfAware/Open the alley", false, 121)]
        public static void OpenAlley()
        {
            Open(Alley, false);
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
