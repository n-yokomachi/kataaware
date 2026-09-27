using UnityEditor;
using UnityEngine;

namespace HalfAware.EditorTools
{
    /// <summary>
    /// 足音の組。どれも一つの録音から一歩ずつ切り出した単発で、余韻は次の一歩の手前まで残してある
    /// （切り出しと大きさは <c>tools/make-ambience.sh</c> の 6 節と 11 節、出典は <c>Assets/Audio/LICENSES.md</c>）。
    /// <see cref="Footsteps"/> が組の中から直前と違う物を選び、音量と高さを少し振って鳴らす。
    ///
    /// どこでどの組を鳴らすか（2026-09-28。自室・場面 2・場面 8・村はオーナーの指定、場面 4 は床の作りから選んだ）:
    /// <list type="table">
    /// <item><term><see cref="HardFloor"/></term><description>場面 8 の共用ガレージ。場面 4 の電車・教室、台所の家の台所と廊下と玄関、公営住宅の台所と浴室の床</description></item>
    /// <item><term><see cref="Concrete"/></term><description>場面 2 の通り・小路・ヤード。場面 4 の公営住宅の外階段とデッキ、公園の小径と門の外の歩道。村の庭の煉瓦の小路とテラス（村は BuildVillageSound が持つ）</description></item>
    /// <item><term><see cref="Room"/></term><description>自室（場面 1・3・5・7）。場面 4 の公営住宅の部屋の中、台所の家の居間と階段</description></item>
    /// <item><term><see cref="Grass"/></term><description>場面 4 の公園の芝（村の芝と同じ組）</description></item>
    /// </list>
    /// 村の未舗装の道の Gravel1〜6 は村（BuildVillageSound）だけが使う。
    /// 村の地面は BuildVillageSound が自分の表で持っていて、ここは見ない（場面 10 の組み立てと切り離すため）
    /// </summary>
    public static class StepSets
    {
        /// <summary>硬い床（Pixabay「Footsteps on hard floor」、OxidVideos）</summary>
        public static readonly string[] HardFloor = Paths("HardFloor", 7);
        /// <summary>コンクリート（Pixabay「concrete footsteps 1」、freesound_community）</summary>
        public static readonly string[] Concrete = Paths("Concrete", 4);
        /// <summary>自室の床（Pixabay「step_sound.wav」、freesound_community）</summary>
        public static readonly string[] Room = Paths("Room", 6);
        /// <summary>芝（Pixabay「Walking through grass」、freesound_community）</summary>
        public static readonly string[] Grass = Paths("Grass", 6);

        static string[] Paths(string name, int count)
        {
            var all = new string[count];
            for (var i = 0; i < count; i++) all[i] = "Assets/Audio/" + name + (i + 1) + ".wav";
            return all;
        }

        /// <summary>組の音を読む。無い物は警告して抜く（偽の音は鳴らさない）</summary>
        public static AudioClip[] Clips(string[] paths)
        {
            var found = new System.Collections.Generic.List<AudioClip>(paths.Length);
            foreach (var p in paths)
            {
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(p);
                if (clip == null) { Debug.LogWarning("足音の素材が無い: " + p); continue; }
                found.Add(clip);
            }
            return found.ToArray();
        }
    }
}
