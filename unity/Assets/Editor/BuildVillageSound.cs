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
        static readonly string[] AmbienceKnobs = { "morningVillage", "morningWheat", "eveningWheat", "follow", "wheatFadeFrom", "wheatFadeTo" };

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

        // ---- 足音の地面 -------------------------------------------------------------------

        /// <summary>
        /// 未舗装の路地の足音。砂利道を歩いた録音から一歩ずつ切り出した単発
        /// （`tools/make-ambience.sh` の 6 節。出どころは Assets/Audio/LICENSES.md）
        /// </summary>
        static readonly string[] GravelSteps =
        {
            "Assets/Audio/Gravel1.wav", "Assets/Audio/Gravel2.wav", "Assets/Audio/Gravel3.wav",
            "Assets/Audio/Gravel4.wav", "Assets/Audio/Gravel5.wav", "Assets/Audio/Gravel6.wav",
        };

        /// <summary>
        /// 庭の煉瓦の小路とテラスの敷石。硬い靴でコンクリートを歩いた録音から一歩ずつ切り出した単発（場面 2 の通りと同じ組。<see cref="StepSets.Concrete"/>）。
        /// `tools/make-ambience.sh` の 11 節。出どころは Assets/Audio/LICENSES.md
        /// </summary>
        static readonly string[] HardSteps =
        {
            "Assets/Audio/Concrete1.wav", "Assets/Audio/Concrete2.wav",
            "Assets/Audio/Concrete3.wav", "Assets/Audio/Concrete4.wav",
        };

        /// <summary>
        /// 芝と草むらの足音。草を踏んだ録音から一歩ずつ切り出した単発（オーナー、2026-09-27）。
        /// 村の足元の既定（床の当たりに <see cref="StepGround"/> の無い所）もこの組（2026-09-28。前は Kenney の柔らかい足音 Step1〜5）。
        /// `tools/make-ambience.sh` の 7 節。出どころは Assets/Audio/LICENSES.md
        /// </summary>
        static readonly string[] GrassSteps =
        {
            "Assets/Audio/Grass1.wav", "Assets/Audio/Grass2.wav", "Assets/Audio/Grass3.wav",
            "Assets/Audio/Grass4.wav", "Assets/Audio/Grass5.wav", "Assets/Audio/Grass6.wav",
        };

        /// <summary>
        /// 床の当たりに足音の地面（<see cref="StepGround"/>）を付ける。
        /// 路地（未舗装路と門の前の砂利の溜まりを含む一枚）は砂利。芝の路肩は草の足音。
        /// 片割れの敷地（芝も小路もテラスも一枚の当たり）は既定を草の足音にし、煉瓦の小路の二本・東屋の前の踊り場・玄関の小路・
        /// テラスだけを硬い音の区画で上書きする。形は見た目の小路と同じ線（<see cref="Samples"/>）から取る。
        /// 草の素材が無ければ <see cref="StepClips"/> が空を返し、その地面は Footsteps の既定の音に落ちる（偽の音は鳴らさない）
        /// </summary>
        static void StepGrounds(Transform road, Transform plot, Transform verge)
        {
            var hard = StepClips(HardSteps);
            var grass = StepClips(GrassSteps);
            if (road != null)
            {
                var gravel = StepClips(GravelSteps);
                var so = new SerializedObject(road.gameObject.AddComponent<StepGround>());
                WriteClips(so.FindProperty("clips"), gravel);
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            if (verge != null)
            {
                var so = new SerializedObject(verge.gameObject.AddComponent<StepGround>());
                WriteClips(so.FindProperty("clips"), grass);
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            if (plot != null)
            {
                var parts = new[]
                {
                    // 煉瓦の小路の二本（格子戸からの小路と、トンネルを抜ける小路）と東屋の前の踊り場。縁取りの煉瓦の分だけ半幅を広げる
                    new StepGround.Patch { name = "WestWalk", line = Samples(WestWalk).ToArray(), half = PathWide * 0.5f + 0.06f, clips = hard },
                    new StepGround.Patch { name = "TunnelWalk", line = Samples(TunnelWalk).ToArray(), half = PathWide * 0.5f + 0.06f, clips = hard },
                    new StepGround.Patch { name = "Landing", box = Landing, clips = hard },
                    new StepGround.Patch { name = "FrontPath", box = Rect.MinMaxRect(FrontDoorX - PathWide * 0.5f, NorthEdge, FrontDoorX + PathWide * 0.5f, HouseFront), clips = hard },
                    // テラス（芝へ下りる段は無くした）
                    new StepGround.Patch { name = "Terrace", box = Rect.MinMaxRect(TerraceWest, HouseRear, HouseEast, TerraceNorth), clips = hard },
                };
                var so = new SerializedObject(plot.gameObject.AddComponent<StepGround>());
                // 区画のどれにも入らない所（芝と花の縁）は、既定（top の clips）の草の足音へ落ちる
                WriteClips(so.FindProperty("clips"), grass);
                var list = so.FindProperty("patches");
                list.arraySize = parts.Length;
                for (var i = 0; i < parts.Length; i++)
                {
                    var p = list.GetArrayElementAtIndex(i);
                    p.FindPropertyRelative("name").stringValue = parts[i].name;
                    var line = p.FindPropertyRelative("line");
                    var pts = parts[i].line ?? new Vector2[0];
                    line.arraySize = pts.Length;
                    for (var k = 0; k < pts.Length; k++) line.GetArrayElementAtIndex(k).vector2Value = pts[k];
                    p.FindPropertyRelative("half").floatValue = parts[i].half;
                    p.FindPropertyRelative("box").rectValue = parts[i].box;
                    WriteClips(p.FindPropertyRelative("clips"), parts[i].clips);
                }
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        /// <summary>足音の素材を読む。無い物は飛ばして知らせる</summary>
        static AudioClip[] StepClips(string[] paths)
        {
            var all = new System.Collections.Generic.List<AudioClip>();
            foreach (var path in paths)
            {
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                if (clip == null) { Debug.LogWarning("足音の素材が無い: " + path); continue; }
                all.Add(clip);
            }
            return all.ToArray();
        }

        static void WriteClips(SerializedProperty at, AudioClip[] clips)
        {
            at.arraySize = clips.Length;
            for (var i = 0; i < clips.Length; i++) at.GetArrayElementAtIndex(i).objectReferenceValue = clips[i];
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
