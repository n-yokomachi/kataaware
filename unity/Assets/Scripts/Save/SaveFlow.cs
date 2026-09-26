using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HalfAware
{
    /// <summary>
    /// 遊んでいる間のセーブの流れ（設計書 5 節）。場面の頭に着いたら自動に書き、
    /// 記憶する・思い出す・はじめる・目を閉じるで場面を読む。
    ///
    /// **シーンには何も置かない。** コンソール（<see cref="ImplantConsole"/>）と同じく、
    /// 再生の始まりに <c>RuntimeInitializeOnLoadMethod</c> で <c>sceneLoaded</c> を拾う。
    ///
    /// 場面の頭の状態（<see cref="Head"/>）は、着いた時に拾っておく。手動の記憶するは、押した時の途中の状態ではなく
    /// この頭を書く（思い出すと場面の頭から始まるので）
    /// </summary>
    public static class SaveFlow
    {
        /// <summary>タイトルの画面のシーンの名（<see cref="TitleScreen"/>）</summary>
        public const string TitleScene = "Title";

        static SaveData pending;
        static SaveData head;
        static int arrived = -1;

        /// <summary>いまの場面の頭。セーブしない所（タイトルの画面など）では null</summary>
        public static SaveData Head { get { return head; } }

        /// <summary>タイトルの画面にいるか。コンソールはここでは開かない</summary>
        public static bool OnTitle
        {
            get { return SceneManager.GetActiveScene().name == TitleScene; }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Forget()
        {
            SceneManager.sceneLoaded -= Arrived;
            pending = null;
            head = null;
            arrived = -1;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Hook()
        {
            SceneManager.sceneLoaded -= Arrived;
            SceneManager.sceneLoaded += Arrived;
        }

        /// <summary>最初のシーンで sceneLoaded が来なかった時の拾い直し</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void FirstScene()
        {
            var scene = SceneManager.GetActiveScene();
            if (scene.handle != arrived) Arrived(scene, LoadSceneMode.Single);
        }

        /// <summary>
        /// シーンに着いた。思い出した物なら村の時刻を戻す。場面の頭を拾って自動に書く。
        /// VillageHour の Awake の後、Start の前に来るので、村の環境音も戻した時刻から始まる
        /// </summary>
        static void Arrived(Scene scene, LoadSceneMode mode)
        {
            if (mode != LoadSceneMode.Single) return;
            arrived = scene.handle;
            var resumed = pending != null && pending.scene == scene.name ? pending : null;
            pending = null;
            if (resumed != null) SetHour(resumed.hour);
            var hour = CurrentHour();
            var stage = resumed != null ? resumed.stage : StageMap.StageOf(scene.name, hour);
            if (stage <= 0)
            {
                head = null;
                return;
            }
            head = SaveStore.Capture(stage, scene.name, hour);
            SaveStore.Write(SaveSlot.Auto, head);
        }

        /// <summary>
        /// 同じシーンの途中で場面が替わった（場面 10 のように）。そこを場面の頭にして自動に書く
        /// </summary>
        public static void EnterStage(int stage)
        {
            if (stage < StageMap.First || stage > StageMap.Last) return;
            var scene = SceneManager.GetActiveScene();
            head = SaveStore.Capture(stage, scene.name, CurrentHour());
            SaveStore.Write(SaveSlot.Auto, head);
        }

        /// <summary>はじめる。場面をまたぐ状態を空にして、場面 1 から</summary>
        public static void StartNew()
        {
            pending = null;
            DiveHandoff.Clear();
            SceneManager.LoadScene(StageMap.SceneOf(StageMap.First));
        }

        /// <summary>そのセーブを読んで始められるか。無い、壊れている、シーンが組み立ての一覧に無いなら false</summary>
        public static bool CanResume(SaveSlot slot)
        {
            var data = SaveStore.Read(slot);
            return data != null && CanLoad(data.scene);
        }

        /// <summary>タイトルの画面のシーンが組み立ての一覧にあるか</summary>
        public static bool CanReachTitle
        {
            get { return CanLoad(TitleScene); }
        }

        /// <summary>思い出す。そのセーブの場面の頭から始める。読めなければ false</summary>
        public static bool Resume(SaveSlot slot)
        {
            var data = SaveStore.Read(slot);
            if (data == null || !CanLoad(data.scene)) return false;
            SaveStore.Restore(data);
            pending = data;
            SceneManager.LoadScene(data.scene);
            return true;
        }

        /// <summary>記憶する。いまの場面の頭を手動の置き場へ書く。書けたら書いた物、書けなければ null</summary>
        public static SaveData Remember(SaveSlot slot)
        {
            if (slot == SaveSlot.Auto || head == null) return null;
            return SaveStore.Write(slot, head);
        }

        /// <summary>目を閉じる。タイトルの画面へ。組み立ての一覧に無ければ false</summary>
        public static bool ToTitle()
        {
            if (!CanLoad(TitleScene)) return false;
            pending = null;
            SceneManager.LoadScene(TitleScene);
            return true;
        }

        static bool CanLoad(string scene)
        {
            return !string.IsNullOrEmpty(scene) && Application.CanStreamedLevelBeLoaded(scene);
        }

        /// <summary>いまのシーンの村の時刻の名。村でなければ空</summary>
        static string CurrentHour()
        {
            var hour = UnityEngine.Object.FindFirstObjectByType<VillageHour>();
            return hour != null ? hour.Current.ToString() : string.Empty;
        }

        static void SetHour(string name)
        {
            if (string.IsNullOrEmpty(name)) return;
            VillageHour.Hour h;
            if (!Enum.TryParse(name, out h)) return;
            var hour = UnityEngine.Object.FindFirstObjectByType<VillageHour>();
            if (hour != null && hour.Current != h) hour.Set(h);
        }
    }
}
