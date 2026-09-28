using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HalfAware.EditorTools
{
    /// <summary>
    /// 場面 9 の終わりと場面 10（対面）を、再生せずに時計を進めて撮る。場面 6 の <see cref="CheckGarden"/> に倣う。
    ///
    /// 村を開き、卓の区画の外に立たせてから、プレイヤーの足を毎秒 1.4 m で運び（歩きの速さ）、演出の一歩（<see cref="ReunionDirector.Step"/>）と
    /// 片割れの動き（<see cref="PersonMotion.Step"/>・<see cref="PersonMotion.Late"/>）を再生中と同じ順に回す。
    /// 目を片割れへ向ける動き（<see cref="PlayerController.Follow"/>）は再生中にしか回らないので、撮る所では目を直に向ける。
    /// 撮るのは HUD を重ねたゲームの見え方（<see cref="ConsoleShot.Shoot(string,int,int,float,bool,float,bool,ConsoleShot.Stage)"/>、960×540、中 320×180）。
    ///
    /// **場面を書き換えるので、撮り終えたら村を開き直して捨てる。** 開いているシーンに未保存の変更があれば撮らない。
    /// rebuild なら、撮る前に場面 10 の物だけを組み直す（保存しない。形や値を詰める間に使う）
    /// </summary>
    public static class CheckReunion
    {
        public const string ScenePath = "Assets/Scenes/Village.unity";

        /// <summary>一歩の秒。再生中の 60 こまに近く</summary>
        const float Dt = 1f / 30f;

        /// <summary>流れを回して、決めた所で撮る。dir に PNG を書く。結果の文を返す</summary>
        public static string Shoot(string dir, bool rebuild)
        {
            for (var i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) return "未保存の変更がある: " + SceneManager.GetSceneAt(i).path;
            var sb = new StringBuilder();
            sb.AppendLine("RenderTexture（どのアセットにも属さない）: 始め " + CheckVillage.LooseRenderTextures());
            var setup = EditorSceneManager.GetSceneManagerSetup();
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var async = ShaderUtil.allowAsyncCompilation;
            ShaderUtil.allowAsyncCompilation = false;
            var cleared = SaveStore.Cleared;
            var box = new MemoryBox();
            Run run = null;
            try
            {
                // クリアの印とセーブは手元の辞書に書かせる（PlayerPrefs を汚さない）
                SaveStore.Box = box;
                if (rebuild) sb.AppendLine(BuildVillage.RebuildReunion(false));
                run = new Run();
                if (!run.Ready(sb)) return sb.ToString();

                // 場面 9 の終わり。トンネルの南の口から下りる小路の上（区画の外）から、テラスの北西の角へ歩いて入る
                // （2026-09-28。格子戸から卓までは必ずトンネルを通るので、卓の区画に入るのはこの小路からだけ）
                run.Place(new Vector3(-3.45f, 0.06f, 20.2f), 170f);
                run.WalkTo(new Vector3(-3.1f, 0.06f, 18.3f), () => run.D.Current != ReunionDirector.Beat.Waiting);
                // 区画に入ると、演出が卓の前まで歩かせる（卓の北を回る）
                var from = run.Where();
                run.Until(() => run.D.Current == ReunionDirector.Beat.Musing, false);
                sb.AppendLine("区画に入った所 " + from + " から卓の前 " + run.Where() + " へ（秒は段の移りの Arriving から Musing）");
                run.Hold(0.3f);
                sb.AppendLine(run.Note("1 卓の前") + " → " + run.Shot(dir + "/r10_1_table.png"));
                // 独白を送ると場面 10。戸が開いて片割れが出てきて立ち止まった所
                run.Step(true);
                run.Until(() => run.D.Clock >= run.D.StopAt + 0.1f, true);
                sb.AppendLine(run.Note("2 出てきた片割れ") + " → " + run.Shot(dir + "/r10_2_door.png"));
                // 口元を覆った所
                run.Until(() => run.D.Clock >= run.D.GaspAt + 0.8f, true);
                sb.AppendLine(run.Note("2a 口元を覆う") + " → " + run.Shot(dir + "/r10_2a_gasp.png"));
                // 封じが解けたら、片割れの方へ歩く。半ばで撮る
                run.Until(() => run.D.Current == ReunionDirector.Beat.Approach, true);
                var start = run.D.Apart;
                run.WalkToward(() => run.D.Apart <= (start + 1f) * 0.5f || run.D.Current != ReunionDirector.Beat.Approach);
                sb.AppendLine(run.Note("3 歩み寄る途中") + " → " + run.Shot(dir + "/r10_3_approach.png"));
                sb.AppendLine("3b 歩み寄る途中（横から） → " + run.Side(dir + "/r10_3b_approach_side.png"));
                run.WalkToward(() => run.D.Current != ReunionDirector.Beat.Approach);
                // 頬に触れる所（手を伸ばし切った所）
                run.Until(() => run.D.Current == ReunionDirector.Beat.Montage, true);
                sb.AppendLine(run.Note("4 頬に触れる") + " → " + run.Shot(dir + "/r10_4_touch.png"));
                sb.AppendLine("4b 頬に触れる（横から） → " + run.Side(dir + "/r10_4b_touch_side.png"));
                // モンタージュ (c) はその場の絵
                run.Until(() => run.D.MontageFrame == 2, true);
                sb.AppendLine(run.Note("5c モンタージュ (c)") + " → " + run.Shot(dir + "/r10_5c_twin.png"));
                // 聞き取れなかった言葉
                run.Until(() => run.D.Current == ReunionDirector.Beat.Voice, true);
                run.Hold(0.2f);
                sb.AppendLine(run.Note("6 聞こえた言葉") + " → " + run.Shot(dir + "/r10_6_voice.png"));
                run.Step(true);
                run.Hold(0.2f);
                sb.AppendLine(run.Note("7 片割れの言葉") + " → " + run.Shot(dir + "/r10_7_words.png"));
                run.Step(true);
                run.Hold(0.2f);
                sb.AppendLine(run.Note("8 最後の独白") + " → " + run.Shot(dir + "/r10_8_last.png"));
                run.Step(true);
                // 暗転の半ば
                run.Until(() => run.D.BeatClock >= 1.1f, false);
                sb.AppendLine(run.Note("9 暗転の半ば") + " → " + run.Shot(dir + "/r10_9_black.png"));
                run.Until(() => run.D.Current == ReunionDirector.Beat.Done, false);
                // 読めるかはエディタで遊んでいない間は分からない（Application.CanStreamedLevelBeLoaded がいつも false）。組み立ての一覧を見る
                var ending = false;
                foreach (var e in EditorBuildSettings.scenes)
                    if (e.enabled && System.IO.Path.GetFileNameWithoutExtension(e.path) == ReunionDirector.EndingScene) ending = true;
                sb.AppendLine(run.Note("10 終わり") + "・クリアの印 " + SaveStore.Cleared + "・次に読むシーン " + (ending ? ReunionDirector.EndingScene + "（組み立ての一覧にある）" : "（エンディングが組み立ての一覧に無い。タイトルの画面へ戻る）"));
                sb.AppendLine(run.Timeline());
            }
            catch (System.Exception e)
            {
                sb.AppendLine("例外: " + e);
            }
            finally
            {
                if (run != null) run.Close();
                SaveStore.Box = null;
                ShaderUtil.allowAsyncCompilation = async;
                ReunionHandoff.Clear();
                // 撮る前に開いていた場面へ戻す（書き換えた村は捨てる）。同じエディタでほかの場面を扱っている者の邪魔をしない
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                TitleShots.Back(setup);
                sb.AppendLine("クリアの印（PlayerPrefs）: 撮る前 " + cleared + "・後 " + SaveStore.Cleared);
                sb.AppendLine("RenderTexture（どのアセットにも属さない）: 終わり " + CheckVillage.LooseRenderTextures());
            }
            return sb.ToString();
        }

        /// <summary>
        /// 片割れの手の形を撮る。テラスの上の卓の前に片割れを立たせ、形 pose（-1 で立ったまま）を掛けて、
        /// 頬に触れる時のプレイヤーの目（片割れの前 <see cref="BuildVillage.TouchApart"/>）から、顔を見て一枚、横から一枚、斜め前から一枚。
        /// 灯り（顔を起こす灯り）は light のとき点ける
        /// </summary>
        public static string Pose(string dir, bool rebuild, int pose, bool light)
        {
            for (var i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) return "未保存の変更がある: " + SceneManager.GetSceneAt(i).path;
            var sb = new StringBuilder();
            sb.AppendLine("RenderTexture（どのアセットにも属さない）: 始め " + CheckVillage.LooseRenderTextures());
            var setup = EditorSceneManager.GetSceneManagerSetup();
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var async = ShaderUtil.allowAsyncCompilation;
            ShaderUtil.allowAsyncCompilation = false;
            PersonMotion twin = null;
            try
            {
                if (rebuild) sb.AppendLine(BuildVillage.RebuildReunion(false));
                var d = Object.FindFirstObjectByType<ReunionDirector>(FindObjectsInactive.Include);
                var so = new SerializedObject(d);
                twin = (PersonMotion)so.FindProperty("twin").objectReferenceValue;
                var reunion = (GameObject)so.FindProperty("reunion").objectReferenceValue;
                var lamp = (Light)so.FindProperty("faceLight").objectReferenceValue;
                var player = (PlayerController)so.FindProperty("player").objectReferenceValue;
                reunion.SetActive(true);
                if (lamp != null) lamp.enabled = light;
                // 卓の東、テラスの上。片割れは西（卓とプレイヤーの方）を向く
                var at = new Vector3(0.9f, 0.1f, 16.4f);
                twin.transform.SetPositionAndRotation(at, Quaternion.Euler(0f, 270f, 0f));
                var eyeFoot = at + twin.transform.forward * BuildVillage.TouchApart;
                // Player もテラスの敷石の上に立つ。組み立ての置き場と同じく 0.06 m 浮かせる
                player.PlaceAt(new Vector3(eyeFoot.x, 0.16f, eyeFoot.z) - Quaternion.Euler(0f, 90f, 0f) * Vector3.forward * 0.22f, 90f, 0f, 0f, 0f, PlayerController.StandingEyeHeight);
                twin.Watch(player.Eye);
                twin.Pose(pose, 1f);
                twin.Sample(twin.Idle, 0.3f);
                var face = d.Face();
                var eye = player.Eye.position;
                sb.AppendLine("目 " + eye.ToString("F3") + "・顔 " + face.ToString("F3") + "・隔たり " + Vector3.Distance(eye, face).ToString("F2"));
                var look = face - eye;
                sb.AppendLine(CheckVillage.Game(new CheckVillage.View("pose_eye", eye, Mathf.Atan2(look.x, look.z) * Mathf.Rad2Deg,
                    -Mathf.Atan2(look.y, new Vector2(look.x, look.z).magnitude) * Mathf.Rad2Deg), dir + "/pose" + pose + "_eye.png"));
                // プレイヤーの左の肩越し（プレイヤーは東を向き、左は北）。手が頬のあった所に来ているか
                var shoulder = eye + new Vector3(-0.45f, 0.08f, 0.32f);
                var sl = face + Vector3.down * 0.1f - shoulder;
                sb.AppendLine(CheckVillage.Game(new CheckVillage.View("pose_side", shoulder, Mathf.Atan2(sl.x, sl.z) * Mathf.Rad2Deg,
                    -Mathf.Atan2(sl.y, new Vector2(sl.x, sl.z).magnitude) * Mathf.Rad2Deg), dir + "/pose" + pose + "_side.png"));
                var front = face + new Vector3(-1.1f, -0.1f, 0.6f);
                var fl = face + Vector3.down * 0.15f - front;
                sb.AppendLine(CheckVillage.Game(new CheckVillage.View("pose_front", front, Mathf.Atan2(fl.x, fl.z) * Mathf.Rad2Deg,
                    -Mathf.Atan2(fl.y, new Vector2(fl.x, fl.z).magnitude) * Mathf.Rad2Deg), dir + "/pose" + pose + "_front.png"));
            }
            catch (System.Exception e)
            {
                sb.AppendLine("例外: " + e);
            }
            finally
            {
                if (twin != null) twin.Close();
                ShaderUtil.allowAsyncCompilation = async;
                // 撮る前に開いていた場面へ戻す（書き換えた村は捨てる）。同じエディタでほかの場面を扱っている者の邪魔をしない
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                TitleShots.Back(setup);
                sb.AppendLine("RenderTexture（どのアセットにも属さない）: 終わり " + CheckVillage.LooseRenderTextures());
            }
            return sb.ToString();
        }

        /// <summary>場面 10 を回す一式</summary>
        sealed class Run
        {
            public ReunionDirector D;
            PlayerController player;
            PersonMotion twin;
            HudView hud;
            float played;
            readonly List<string> marks = new List<string>();
            ReunionDirector.Beat last;

            public bool Ready(StringBuilder sb)
            {
                D = Object.FindFirstObjectByType<ReunionDirector>(FindObjectsInactive.Include);
                if (D == null) { sb.AppendLine("ReunionDirector が無い"); return false; }
                var so = new SerializedObject(D);
                player = (PlayerController)so.FindProperty("player").objectReferenceValue;
                twin = (PersonMotion)so.FindProperty("twin").objectReferenceValue;
                hud = (HudView)so.FindProperty("hud").objectReferenceValue;
                // エディタでは、一度描いた後に骨を動かしても皮が描き直されず、前に撮った時の形（首を振った形・腕を下ろした形）のまま写った。
                // 描くたびに骨から組み直させる
                if (twin != null)
                    foreach (var smr in twin.GetComponentsInChildren<SkinnedMeshRenderer>(true)) smr.forceMatrixRecalculationPerRender = true;
                // 場面 9 として起こす（Awake は鳴らないので、段を待つ所へ置く）
                var beat = typeof(ReunionDirector).GetField("beat", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                beat.SetValue(D, ReunionDirector.Beat.Waiting);
                last = D.Current;
                return true;
            }

            /// <summary>Player の足元</summary>
            public string Where() { return player.transform.position.ToString("F2"); }

            /// <summary>片割れの動きの図を捨てる（エディタで Restart すると作られ、伏せても残る）</summary>
            public void Close()
            {
                if (twin != null) twin.Close();
            }

            public void Place(Vector3 at, float yaw)
            {
                player.PlaceAt(Ground(at), yaw, 0f, 0f, 0f, PlayerController.StandingEyeHeight);
                player.CanMove = true;
                player.CanLook = true;
                Physics.SyncTransforms();
            }

            /// <summary>一歩。再生中の順（演出 → 人の動き）</summary>
            public void Step(bool press)
            {
                D.Step(Dt, press);
                played += Dt;
                if (twin != null && twin.isActiveAndEnabled)
                {
                    if (twin.Ticks == 0 && !fresh) { twin.Restart(); twin.Late(); fresh = true; }
                    twin.Step(Dt);
                    twin.Late();
                }
                Physics.SyncTransforms();
                if (D.Current != last)
                {
                    marks.Add(string.Format("{0:0.00} 秒（場面 10 の頭から {1:0.00}）: {2}", played, D.Clock, D.Current));
                    last = D.Current;
                }
            }
            bool fresh;

            /// <summary>条件が立つまで回す。watch なら片割れの顔へ目を向けたまま</summary>
            public void Until(System.Func<bool> done, bool watch)
            {
                for (var n = 0; n < 9000 && !done(); n++)
                {
                    if (watch && D.Running && twin != null && twin.isActiveAndEnabled) Look(D.Face());
                    Step(false);
                }
            }

            public void Hold(float seconds)
            {
                for (var t = 0f; t < seconds; t += Dt) Step(false);
            }

            /// <summary>歩く速さで to へ運ぶ。stop が立ったら止める</summary>
            public void WalkTo(Vector3 to, System.Func<bool> stop)
            {
                for (var n = 0; n < 3000 && !stop(); n++)
                {
                    var at = player.transform.position;
                    var d = new Vector3(to.x - at.x, 0f, to.z - at.z);
                    if (d.magnitude < 0.02f) break;
                    var go = Vector3.ClampMagnitude(d, PlayerController.WalkSpeed * Dt);
                    player.transform.position = Ground(at + go);
                    Step(false);
                }
            }

            /// <summary>足元を真下の床へ下ろす（当たりの肌の厚みのぶん浮かせる。組み立ての置き場と同じ 0.06 m）。Player の当たりは拾わない</summary>
            Vector3 Ground(Vector3 at)
            {
                var hits = Physics.RaycastAll(at + Vector3.up * 1.0f, Vector3.down, 3f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
                var best = float.NegativeInfinity;
                foreach (var h in hits)
                {
                    if (h.collider.transform.IsChildOf(player.transform)) continue;
                    if (h.point.y > best) best = h.point.y;
                }
                if (float.IsNegativeInfinity(best)) return at;
                return new Vector3(at.x, best + 0.06f, at.z);
            }

            /// <summary>片割れの方へ歩く（目も向けたまま）。stop が立ったら止める</summary>
            public void WalkToward(System.Func<bool> stop)
            {
                for (var n = 0; n < 3000 && !stop(); n++)
                {
                    var at = player.transform.position;
                    var f = D.Face();
                    var d = new Vector3(f.x - at.x, 0f, f.z - at.z);
                    var go = Vector3.ClampMagnitude(d, PlayerController.WalkSpeed * Dt);
                    player.transform.position = Ground(at + go);
                    player.transform.rotation = Quaternion.Euler(0f, Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg, 0f);
                    Look(f);
                    Step(false);
                }
            }

            /// <summary>目を点へ向ける。立っているので体ごと回す</summary>
            public void Look(Vector3 at)
            {
                var eye = player.Eye.position;
                var d = at - eye;
                var yaw = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
                var pitch = -Mathf.Atan2(d.y, new Vector2(d.x, d.z).magnitude) * Mathf.Rad2Deg;
                player.PlaceAt(player.transform.position, yaw, 0f, 0f, pitch, player.EyeHeight);
                var lead = new SerializedObject(player).FindProperty("eyeLead").floatValue;
                player.Eye.localPosition = new Vector3(0f, player.EyeHeight, lead);
                player.Eye.localRotation = Quaternion.Euler(player.Pitch, 0f, 0f);
                Physics.SyncTransforms();
            }

            /// <summary>プレイヤーの目と片割れの顔の真ん中を、横（プレイヤーの左）から 1.4 m 離れて撮る。HUD は重ねない</summary>
            public string Side(string path)
            {
                var eye = player.Eye.position;
                var face = D.Face();
                var mid = (eye + face) * 0.5f + Vector3.down * 0.2f;
                var across = Vector3.Cross(Vector3.up, new Vector3(face.x - eye.x, 0f, face.z - eye.z).normalized);
                var from = mid - across * 1.4f + Vector3.up * 0.1f;
                var look = mid - from;
                return CheckVillage.Game(new CheckVillage.View("side", from, Mathf.Atan2(look.x, look.z) * Mathf.Rad2Deg,
                    -Mathf.Atan2(look.y, new Vector2(look.x, look.z).magnitude) * Mathf.Rad2Deg), path);
            }

            public string Shot(string path)
            {
                return ConsoleShot.Shoot(path, 960, 540, UiLens.Scale, false, 0f, false, null);
            }

            public string Note(string what)
            {
                var line = string.Format("{0}: {1:0.00} 秒・場面 10 の頭から {2:0.00} 秒・段 {3}・顔まで {4:0.00} m・形 {5}/{6:0.00}・モンタージュ {7}",
                    what, played, D.Clock, D.Current, D.Apart, twin != null ? twin.PoseIndex : -1, twin != null ? twin.PoseWeight : 0f, D.MontageFrame);
                if (twin != null && twin.isActiveAndEnabled)
                {
                    var an = twin.GetComponentInChildren<Animator>();
                    var aim = new SerializedObject(twin).FindProperty("headAim").vector3Value;
                    var faceTo = an.GetBoneTransform(HumanBodyBones.Head).rotation * aim;
                    line += string.Format("（片割れの根 {0} 向き {1:0}・体 {2:0}・首 {3}・顔の向き {6:0}／目 {4} 向き {5:0}）", twin.transform.position.ToString("F2"),
                        twin.transform.eulerAngles.y, twin.Body.eulerAngles.y, twin.Look, player.Eye.position.ToString("F2"), player.transform.eulerAngles.y,
                        Mathf.Atan2(faceTo.x, faceTo.z) * Mathf.Rad2Deg);
                }
                return line;
            }

            public string Timeline()
            {
                return "段の移り:\n  " + string.Join("\n  ", marks.ToArray());
            }
        }
    }
}
