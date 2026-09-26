using UnityEditor;
using UnityEngine;

namespace HalfAware.EditorTools
{
    public static partial class BuildVillage
    {
        // ---- 環境音 ---------------------------------------------------------------------

        /// <summary>音の入れ物の名前。Player の子に置く</summary>
        public const string AmbienceName = "Ambience";

        /// <summary>インスペクターで決める値の名。組み直しても前の値を引き継ぐ</summary>
        static readonly string[] AmbienceKnobs = { "morningVillage", "morningWheat", "eveningWheat", "follow" };

        /// <summary>
        /// 前に置いた環境音の大きさを控える。**Player を落とす前に呼ぶ。**
        /// Rig は Player ごと作り直すので、控えないとインスペクターで決めた大きさが既定へ戻る。
        /// 前の物が無ければ null
        /// </summary>
        static float[] HeardAmbience()
        {
            var had = Object.FindFirstObjectByType<VillageAmbience>(FindObjectsInactive.Include);
            if (had == null) return null;
            var so = new SerializedObject(had);
            var values = new float[AmbienceKnobs.Length];
            for (var i = 0; i < AmbienceKnobs.Length; i++) values[i] = so.FindProperty(AmbienceKnobs[i]).floatValue;
            return values;
        }

        /// <summary>
        /// 朝の村と麦の風の輪を、Player の頭上で 2D にして流す（<see cref="VillageAmbience"/>）。
        /// 時刻（<see cref="VillageHour"/>）が替わると鳴らす物を入れ替える。Stage の後に呼ぶ（時刻の物を繋ぐため）
        /// </summary>
        static void Ambience(Transform hours, float[] heard)
        {
            var player = GameObject.Find("Player");
            if (player == null) { Debug.LogWarning("Player が無い。村の環境音を置けない"); return; }
            var go = new GameObject(AmbienceName);
            go.transform.SetParent(player.transform, false);
            go.transform.localPosition = new Vector3(0f, 2.2f, 0f);

            var village = Loop(go.transform, "Village", VillageAudioImport.MorningPath);
            var wheat = Loop(go.transform, "Wheat", VillageAudioImport.WheatPath);

            var amb = go.AddComponent<VillageAmbience>();
            var so = new SerializedObject(amb);
            so.FindProperty("hour").objectReferenceValue = hours.GetComponent<VillageHour>();
            so.FindProperty("village").objectReferenceValue = village;
            so.FindProperty("wheat").objectReferenceValue = wheat;
            if (heard != null)
                for (var i = 0; i < AmbienceKnobs.Length; i++) so.FindProperty(AmbienceKnobs[i]).floatValue = heard[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>輪で流す 2D の音を一つ。鳴らし始めと大きさは VillageAmbience が決める</summary>
        static AudioSource Loop(Transform parent, string name, string path)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var a = go.AddComponent<AudioSource>();
            a.clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            if (a.clip == null) Debug.LogWarning("村の環境音の素材が無い: " + path);
            a.loop = true;
            a.playOnAwake = false;
            a.spatialBlend = 0f;
            a.priority = 200;
            a.volume = 0f;
            return a;
        }
    }
}
