using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace HalfAware.EditorTools
{
    /// <summary>
    /// 場面 1 の詰め（2026-09-28）の確かめの撮影。再生せずに、エディタで場面を置き直して撮る。
    ///
    /// 3D はプローブのカメラ（主人公の目のカメラを写した、HideAndDontSave・enabled=false のカメラ）で撮り、
    /// UI は粗い画面（<see cref="UiLens"/>）で撮って、<see cref="ConsoleShot"/> と同じく重ねる。プローブのカメラと RenderTexture は、
    /// 撮った同じ呼び出しの中で捨てる。場面の中の物（主人公の向き・HUD の中身・画面の灯り）を書き換えるので、
    /// **撮り終えたら場面を開き直して捨てる。** 未保存の変更がある場面では撮らない
    /// </summary>
    public static class CheckRoom
    {
        public const int Width = 960;
        public const int Height = 540;

        /// <summary>撮る前に HUD へ手を入れる。null なら何もしない</summary>
        public delegate void Stage(HudView hud);

        [MenuItem("HalfAware/Shoot the room checks", false, 212)]
        public static void Menu()
        {
            var dir = Path.Combine(Path.GetTempPath(), "HalfAwareRoomChecks");
            Debug.Log(ShootAll(dir));
        }

        /// <summary>場面 1 の冒頭・一服・首の限り・画面・リストの枠・ドアと、場面 3 の候補の表を dir へ撮る。終えたら場面 1 を開き直す</summary>
        public static string ShootAll(string dir)
        {
            if (EditorApplication.isPlaying) return "再生中は撮らない";
            var active = EditorSceneManager.GetActiveScene();
            if (active.isDirty) return "開いているシーンに未保存の変更がある: " + active.path;
            Directory.CreateDirectory(dir);
            var log = new System.Text.StringBuilder();
            try
            {
                Room(dir, log);
                Connect(dir, log);
            }
            catch (Exception e)
            {
                log.AppendLine("例外: " + e);
            }
            finally
            {
                Daze(0f, 0f);
                EditorSceneManager.OpenScene(PlaceProtagonist.RoomPath, OpenSceneMode.Single);
            }
            return log.ToString();
        }

        // ---- 場面 1 ------------------------------------------------------------

        static void Room(string dir, System.Text.StringBuilder log)
        {
            EditorSceneManager.OpenScene(PlaceProtagonist.RoomPath, OpenSceneMode.Single);
            var flow = UnityEngine.Object.FindFirstObjectByType<SceneFlow>(FindObjectsInactive.Include);
            var player = flow.Player;
            var intro = UnityEngine.Object.FindFirstObjectByType<RoomIntroDirector>(FindObjectsInactive.Include);
            var fso = new SerializedObject(flow);
            var seatEye = fso.FindProperty("seatEyeHeight").floatValue;
            var drop = fso.FindProperty("wakeDrop").floatValue;
            var startPitch = fso.FindProperty("wakeStartPitch").floatValue;
            var limit = fso.FindProperty("seatedHeadLimit").floatValue;
            var dazeBlur = fso.FindProperty("dazeBlur").floatValue;
            var dazeWobble = fso.FindProperty("dazeWobble").floatValue;
            var iso = new SerializedObject(intro);
            var first = iso.FindProperty("firstLines").GetArrayElementAtIndex(0).stringValue;
            var card = iso.FindProperty("card").stringValue;
            var foot = player.transform.position;
            var body = player.transform.eulerAngles.y;
            var lead = new SerializedObject(player).FindProperty("eyeLead").floatValue;
            var items = Items();
            log.AppendFormat("場面 1: 座った足元 {0}、体の向き {1:0}、目 {2:0.000}、首の限り {3}", foot.ToString("F2"), body, seatEye, limit).AppendLine();

            // 1. 冒頭。瞬きの途中（開きかけて止まった所）と、起き上がって最初の独白が出た所。眩暈は保ったまま
            var blink = intro.Blink;
            Daze(dazeBlur, dazeWobble);
            player.PlaceAt(foot, body, limit, 0f, startPitch, seatEye - drop);
            log.AppendLine(Shoot(Path.Combine(dir, "1a_blink_peek.png"), h =>
            {
                h.SetFade(blink.Level(blink.PeekAt + blink.Peek + blink.Hold * 0.5f));
            }));
            player.PlaceAt(foot, body, limit, 0f, 0f, seatEye);
            log.AppendLine(Shoot(Path.Combine(dir, "1b_after_rising.png"), h =>
            {
                h.SetFade(0f);
                h.SetSubtitle(first, SubtitleKind.Line, true);
            }));
            Daze(0f, 0f);

            // 2. 一服。煙草へ目を向けたまま火を点けている所と、二服目の後の場所と時刻のカード
            var cig = Find(items, RoomIds.Cigarette);
            var aim = Aim(foot, body, seatEye, lead, cig.Position, limit);
            player.PlaceAt(foot, body, limit, aim.x, aim.y, seatEye);
            log.AppendFormat("煙草へ向けた首 {0:0.0} 度・下へ {1:0.0} 度", aim.x, aim.y).AppendLine();
            log.AppendLine(Shoot(Path.Combine(dir, "2a_smoke_lighting.png"), h => h.SetFade(0f)));
            player.PlaceAt(foot, body, limit, 0f, 0f, seatEye);
            // カードはゲームでも粗くしない（HudView.Unblur）。くっきり撮る
            log.AppendLine(Shoot(Path.Combine(dir, "2b_smoke_card.png"), h =>
            {
                h.SetCurtain(true);
                h.SetCenter(card);
            }, 1f));

            // 3. 座位の見回しの左右の限りと、ジャケットを狙えている所（印が出る）
            player.PlaceAt(foot, body, limit, -limit, 0f, seatEye);
            log.AppendLine(Shoot(Path.Combine(dir, "3a_head_left_limit.png"), null));
            player.PlaceAt(foot, body, limit, limit, 0f, seatEye);
            log.AppendLine(Shoot(Path.Combine(dir, "3b_head_right_limit.png"), null));
            var jacket = Find(items, RoomIds.Jacket);
            var toJacket = Aim(foot, body, seatEye, lead, jacket.Position, limit);
            player.PlaceAt(foot, body, limit, toJacket.x, toJacket.y, seatEye);
            var done = new HashSet<string> { RoomIds.Jack, RoomIds.Cigarette };
            var picked = Pick(player, items, done);
            log.AppendFormat("ジャケットへ: 首 {0:0.0} 度（限り {1}）・下へ {2:0.0} 度。選ばれた物: {3}", toJacket.x, limit, toJacket.y, picked != null ? picked.Id : "無し").AppendLine();
            log.AppendLine(Shoot(Path.Combine(dir, "3c_jacket_in_reach.png"), h => h.SetPrompt(picked != null ? HudView.Prompt(picked.Label) : null)));

            // 4. スリープを解除した後のモニター。端末の前に座った正面で、画面を灯す
            var seat = UnityEngine.Object.FindFirstObjectByType<TerminalSeat>(FindObjectsInactive.Include);
            var sso = new SerializedObject(seat);
            var screen = (TerminalScreen)sso.FindProperty("screen").objectReferenceValue;
            var chair = (Transform)sso.FindProperty("chair").objectReferenceValue;
            if (chair != null) chair.position = sso.FindProperty("chairSeated").vector3Value;
            Wear();
            player.PlaceAt(sso.FindProperty("seatSpot").vector3Value, sso.FindProperty("seatYaw").floatValue, 0f, 0f,
                sso.FindProperty("seatPitch").floatValue, sso.FindProperty("seatEyeHeight").floatValue);
            if (screen != null) screen.LightNow();
            var terminal = Find(items, RoomIds.Terminal);
            log.AppendLine(Shoot(Path.Combine(dir, "4a_monitor_lit.png"), h => h.SetSubtitle(terminal.AfterYes[0], SubtitleKind.Line, true)));
            log.AppendLine(Shoot(Path.Combine(dir, "4b_monitor_conditions.png"), h => h.SetSubtitle(terminal.AfterYes[1], SubtitleKind.Line, true)));
            log.AppendLine(Frame("走査条件"));

            // 5. リストの枠。チップのリスト（抜き差し台の前）と、売り上げのメモ（ソファの前）
            var chips = Find(items, RoomIds.Chips);
            Stand(player, new Vector3(1.85f, 0.05f, 0.75f), chips.Position);
            log.AppendLine(Shoot(Path.Combine(dir, "5a_list_chips.png"), h => h.SetSubtitle(chips.Lines[1], SubtitleKind.Line, true)));
            log.AppendLine(Frame("チップ"));
            var memo = Find(items, RoomIds.Clipboard);
            Stand(player, new Vector3(-1.35f, 0.05f, 0.35f), memo.Position);
            log.AppendLine(Shoot(Path.Combine(dir, "5b_list_memo.png"), h => h.SetSubtitle(memo.Lines[1], SubtitleKind.Line, true)));
            log.AppendLine(Frame("売り上げのメモ"));

            // 6. ドア。必須が残っている間（チップとモニターがまだ）と、済んだ後
            var door = Find(items, RoomIds.Door);
            Stand(player, new Vector3(0.8f, 0.05f, -1.35f), door.Position);
            var early = new HashSet<string> { RoomIds.Jack, RoomIds.Cigarette, RoomIds.Jacket };
            var before = Pick(player, items, early);
            log.AppendFormat("ドア（ジャケットだけ済み）: 選ばれた物 {0}", before != null ? before.Id : "無し").AppendLine();
            log.AppendLine(Shoot(Path.Combine(dir, "6a_door_blocked.png"), h => h.SetPrompt(before != null ? HudView.Prompt(before.Label) : null)));
            var all = new HashSet<string> { RoomIds.Jack, RoomIds.Cigarette, RoomIds.Jacket, RoomIds.Chips, RoomIds.Terminal };
            var after = Pick(player, items, all);
            log.AppendFormat("ドア（必須が済んだ後）: 選ばれた物 {0}", after != null ? after.Id : "無し").AppendLine();
            log.AppendLine(Shoot(Path.Combine(dir, "6b_door_open.png"), h => h.SetPrompt(after != null ? HudView.Prompt(after.Label) : null)));
        }

        // ---- 場面 1 の台詞の原稿（2026-09-28） ------------------------------------------

        [MenuItem("HalfAware/Shoot the room text checks", false, 213)]
        public static void TextMenu()
        {
            var dir = Path.Combine(Path.GetTempPath(), "HalfAwareRoomText");
            Debug.Log(ShootText(dir));
        }

        /// <summary>
        /// 原稿から写した文面の組み方を dir へ撮る。ルビと改行（ジャックの 1 ページ目）、傍点（3 ページ目）、長いページの窓の伸び方（モニターの 4 ページ目）、
        /// リストの枠（走査条件・チップ・売り上げのメモ）、カード、煙草の箱、煙草を取った時の 1 ページ（煙草へ向いた絵）、コンソールのログ。
        /// 終えたら場面 1 を開き直す（場面は保存しない）
        /// </summary>
        public static string ShootText(string dir)
        {
            if (EditorApplication.isPlaying) return "再生中は撮らない";
            var active = EditorSceneManager.GetActiveScene();
            if (active.isDirty) return "開いているシーンに未保存の変更がある: " + active.path;
            Directory.CreateDirectory(dir);
            var log = new System.Text.StringBuilder();
            try
            {
                EditorSceneManager.OpenScene(PlaceProtagonist.RoomPath, OpenSceneMode.Single);
                var flow = UnityEngine.Object.FindFirstObjectByType<SceneFlow>(FindObjectsInactive.Include);
                var player = flow.Player;
                var fso = new SerializedObject(flow);
                var seatEye = fso.FindProperty("seatEyeHeight").floatValue;
                var limit = fso.FindProperty("seatedHeadLimit").floatValue;
                var intro = UnityEngine.Object.FindFirstObjectByType<RoomIntroDirector>(FindObjectsInactive.Include);
                var card = new SerializedObject(intro).FindProperty("card").stringValue;
                var foot = player.transform.position;
                var body = player.transform.eulerAngles.y;
                var lead = new SerializedObject(player).FindProperty("eyeLead").floatValue;
                var items = Items();
                var jack = Find(items, RoomIds.Jack);
                var cig = Find(items, RoomIds.Cigarette);
                var box = Find(items, RoomIds.CigaretteBox);

                // ジャックの 1 ページ目（身体のルビと改行）と 3 ページ目（傍点）。ジャックへ目を向けた所
                var toJack = Aim(foot, body, seatEye, lead, jack.Position, limit);
                player.PlaceAt(foot, body, limit, toJack.x, toJack.y, seatEye);
                log.AppendLine(Shoot(Path.Combine(dir, "t1_jack_page1.png"), h => h.SetSubtitle(jack.Lines[0], SubtitleKind.Line, true)));
                log.AppendLine(Shoot(Path.Combine(dir, "t2_jack_page3_dots.png"), h => h.SetSubtitle(jack.Lines[2], SubtitleKind.Line, true)));

                // 煙草を取った時の 1 ページ。煙草へ目を向けた所（火を点けるのもこの向き）
                var toCig = Aim(foot, body, seatEye, lead, cig.Position, limit);
                player.PlaceAt(foot, body, limit, toCig.x, toCig.y, seatEye);
                log.AppendFormat("煙草へ向けた首 {0:0.0} 度・下へ {1:0.0} 度", toCig.x, toCig.y).AppendLine();
                log.AppendLine(Shoot(Path.Combine(dir, "t3_cigarette_page.png"), h => h.SetSubtitle(cig.Lines[0], SubtitleKind.Line, true)));
                // 暗転のカード（ゲームでも粗くしない）
                player.PlaceAt(foot, body, limit, 0f, 0f, seatEye);
                log.AppendLine(Shoot(Path.Combine(dir, "t4_card.png"), h =>
                {
                    h.SetCurtain(true);
                    h.SetCenter(card);
                }, 1f));
                // 煙草の箱（双鶴のルビ）
                var toBox = Aim(foot, body, seatEye, lead, box.Position, limit);
                player.PlaceAt(foot, body, limit, toBox.x, toBox.y, seatEye);
                Wear();
                log.AppendLine(Shoot(Path.Combine(dir, "t5_box_page.png"), h => h.SetSubtitle(box.Lines[0], SubtitleKind.Line, true)));

                // モニターの 4 ページ目（長いページ）と、スリープを解除した後の走査条件
                var seat = UnityEngine.Object.FindFirstObjectByType<TerminalSeat>(FindObjectsInactive.Include);
                var sso = new SerializedObject(seat);
                var screen = (TerminalScreen)sso.FindProperty("screen").objectReferenceValue;
                var chair = (Transform)sso.FindProperty("chair").objectReferenceValue;
                if (chair != null) chair.position = sso.FindProperty("chairSeated").vector3Value;
                player.PlaceAt(sso.FindProperty("seatSpot").vector3Value, sso.FindProperty("seatYaw").floatValue, 0f, 0f,
                    sso.FindProperty("seatPitch").floatValue, sso.FindProperty("seatEyeHeight").floatValue);
                var terminal = Find(items, RoomIds.Terminal);
                log.AppendLine(Shoot(Path.Combine(dir, "t6_monitor_page4.png"), h => h.SetSubtitle(terminal.Lines[3], SubtitleKind.Line, true)));
                if (screen != null) screen.LightNow();
                log.AppendLine(Shoot(Path.Combine(dir, "t7_list_conditions.png"), h => h.SetSubtitle(terminal.AfterYes[1], SubtitleKind.Line, true)));
                log.AppendLine(Frame("走査条件"));

                // チップのリストと売り上げのメモ
                var chips = Find(items, RoomIds.Chips);
                Stand(player, new Vector3(1.85f, 0.05f, 0.75f), chips.Position);
                log.AppendLine(Shoot(Path.Combine(dir, "t8_list_chips.png"), h => h.SetSubtitle(chips.Lines[1], SubtitleKind.Line, true)));
                log.AppendLine(Frame("チップ"));
                var memo = Find(items, RoomIds.Clipboard);
                Stand(player, new Vector3(-1.35f, 0.05f, 0.35f), memo.Position);
                log.AppendLine(Shoot(Path.Combine(dir, "t9_list_memo.png"), h => h.SetSubtitle(memo.Lines[1], SubtitleKind.Line, true)));
                log.AppendLine(Frame("売り上げのメモ"));

                // コンソールのログ。ルビ・傍点・改行の付いた行
                log.AppendLine(ConsoleShot.Shoot(Path.Combine(dir, "t10_console_log.png"), Width, Height, UiLens.Scale, true, 0f, false, (h, c) =>
                {
                    var l = ConsoleLog.Here();
                    l.Clear();
                    // 新しい行ほど下。見える所にルビ・傍点・改行の行が来るよう、リストを先に積む
                    ConsoleLog.Examined(chips.Label, chips.Lines);
                    ConsoleLog.Picked(chips.Question, Choice.Yes);
                    ConsoleLog.Said(new[] { "『今回のは酔いが酷いな…』" });
                    ConsoleLog.Examined(jack.Label, jack.Lines);
                    ConsoleLog.Examined(box.Label, box.Lines);
                }));
            }
            catch (Exception e)
            {
                log.AppendLine("例外: " + e);
            }
            finally
            {
                EditorSceneManager.OpenScene(PlaceProtagonist.RoomPath, OpenSceneMode.Single);
            }
            return log.ToString();
        }

        // ---- 字幕の窓の余白（2026-09-29） ------------------------------------------

        /// <summary>
        /// 字幕の窓を、1 行・2 行・3 行（ルビ有りと無し）のページと会話のページで撮り、二択と印の出ている所も撮る。
        /// 名は tag_…png。場面 1 の座った目の正面で撮る。終えたら撮る前に開いていたシーンを開き直す（場面は保存しない）
        /// </summary>
        public static string ShootSubtitles(string dir, string tag)
        {
            if (EditorApplication.isPlaying) return "再生中は撮らない";
            var active = EditorSceneManager.GetActiveScene();
            if (active.isDirty) return "開いているシーンに未保存の変更がある: " + active.path;
            var open = active.path;
            Directory.CreateDirectory(dir);
            var log = new System.Text.StringBuilder();
            try
            {
                EditorSceneManager.OpenScene(PlaceProtagonist.RoomPath, OpenSceneMode.Single);
                var flow = UnityEngine.Object.FindFirstObjectByType<SceneFlow>(FindObjectsInactive.Include);
                var player = flow.Player;
                var fso = new SerializedObject(flow);
                var seatEye = fso.FindProperty("seatEyeHeight").floatValue;
                var limit = fso.FindProperty("seatedHeadLimit").floatValue;
                player.PlaceAt(player.transform.position, player.transform.eulerAngles.y, limit, 0f, 0f, seatEye);
                var items = Items();
                var cig = Find(items, RoomIds.Cigarette);
                var jack = Find(items, RoomIds.Jack);
                var monitor = Find(items, RoomIds.Terminal);
                string P(string name) { return Path.Combine(dir, tag + "_" + name + ".png"); }
                log.AppendLine(Shoot(P("1line"), h => h.SetSubtitle(cig.Lines[0], SubtitleKind.Line, true)));
                log.AppendLine(Shoot(P("2line"), h => h.SetSubtitle(monitor.Lines[0], SubtitleKind.Line, true)));
                log.AppendLine(Shoot(P("3line"), h => h.SetSubtitle(monitor.Lines[3], SubtitleKind.Line, true)));
                log.AppendLine(Shoot(P("3line_ruby"), h => h.SetSubtitle(jack.Lines[0], SubtitleKind.Line, true)));
                log.AppendLine(Shoot(P("talk"), h => h.SetSubtitle("ハンナ「メイ！　忘れもの！　上がっておいで！」", SubtitleKind.Line, true)));
                log.AppendLine(Shoot(P("talk2"), h => h.SetSubtitle("ハンナ「水筒忘れるの毎日でしょ。<br/>投げません。いいから上がっておいで」", SubtitleKind.Line, true)));
                log.AppendLine(Shoot(P("choice"), h => h.SetChoice(new Choice(monitor.Question))));
                log.AppendLine(Shoot(P("prompt"), h => h.SetPrompt(HudView.Prompt(monitor.Label))));
            }
            catch (Exception e)
            {
                log.AppendLine("例外: " + e);
            }
            finally
            {
                if (!string.IsNullOrEmpty(open)) EditorSceneManager.OpenScene(open, OpenSceneMode.Single);
            }
            return log.ToString();
        }

        // ---- 場面 3 ------------------------------------------------------------

        static void Connect(string dir, System.Text.StringBuilder log)
        {
            EditorSceneManager.OpenScene(BuildConnect.ScenePath, OpenSceneMode.Single);
            var player = UnityEngine.Object.FindFirstObjectByType<PlayerController>(FindObjectsInactive.Include);
            var screen = UnityEngine.Object.FindFirstObjectByType<TerminalScreen>(FindObjectsInactive.Include);
            var script = AssetDatabase.LoadAssetAtPath<RoomScript>(BuildConnect.ScriptPath);
            var monitor = script.Find(ConnectIds.Monitor);
            string table = null;
            foreach (var line in monitor.Lines) if (ListFormat.IsList(line) && SubtitleBox.LineCount(line) >= 6) table = line;
            player.PlaceAt(BuildConnect.SeatAt, 0f, HeadTurn.DefaultLimit, 0f, 0f, BuildConnect.SeatEyeHeight());
            if (screen != null) screen.LightNow();
            log.AppendLine(Shoot(Path.Combine(dir, "5c_list_connect_candidates.png"), h => h.SetSubtitle(table, SubtitleKind.Line, true)));
            log.AppendLine(Frame("場面 3 の候補の表"));
        }

        // ---- 置き方 ------------------------------------------------------------

        static List<IInteractable> Items()
        {
            return new List<IInteractable>(UnityEngine.Object.FindObjectsByType<Interactable>(FindObjectsInactive.Include, FindObjectsSortMode.InstanceID));
        }

        static IInteractable Find(List<IInteractable> items, string id)
        {
            foreach (var it in items) if (it.Id == id) return it;
            throw new Exception("対象が無い: " + id);
        }

        /// <summary>座ったまま target を見る首の向き（体から見た左右）と上下。首の限りと上下の範囲に収める</summary>
        static Vector2 Aim(Vector3 foot, float body, float eye, float lead, Vector3 target, float limit)
        {
            var want = Gaze.Toward(foot, body, false, new Vector3(0f, eye, lead), target);
            var head = Mathf.Clamp(Mathf.DeltaAngle(body, want.x), -limit, limit);
            return new Vector2(head, PlayerController.ClampPitch(want.y));
        }

        /// <summary>
        /// 立って at に立ち、target を見る。**体は写さない。** 場面 1 は座った形で保存してあり、エディタでは立った形
        /// （遊んでいる間に SeatedPose が形を解いて Animator が動かす形）を作れない。そのままだと立った目の高さから座った体の膝や手が写る。
        /// 立って 40 度（下を向ける限り）まで見下ろしても、遊んでいる間は立った体は画面に入らない（シナリオ設計 1 節）
        /// </summary>
        static void Stand(PlayerController player, Vector3 at, Vector3 target)
        {
            var eye = PlayerController.StandingEyeHeight;
            var lead = new SerializedObject(player).FindProperty("eyeLead").floatValue;
            var want = Gaze.Toward(at, 0f, true, new Vector3(0f, eye, lead), target);
            player.PlaceAt(at, want.x, 0f, 0f, PlayerController.ClampPitch(want.y), eye);
            var pose = player.GetComponentInChildren<SeatedPose>(true);
            if (pose == null) return;
            foreach (var r in pose.GetComponentsInChildren<Renderer>(true)) r.enabled = false;
        }

        /// <summary>今の目の向きで、調べる操作が拾う物（SceneFlow と同じ選び方）</summary>
        static IInteractable Pick(PlayerController player, List<IInteractable> items, ICollection<string> done)
        {
            return InteractionPicker.Select(player.Eye.position, player.Eye.forward, items, done, InteractionPicker.MaxAngle);
        }

        /// <summary>ジャケットを着て、卓のジャケットを消す（着た後に撮る所）</summary>
        static void Wear()
        {
            var intro = UnityEngine.Object.FindFirstObjectByType<RoomIntroDirector>(FindObjectsInactive.Include);
            if (intro == null) return;
            var so = new SerializedObject(intro);
            var garment = so.FindProperty("garment").objectReferenceValue as Garment;
            var folded = so.FindProperty("folded").objectReferenceValue as GameObject;
            if (garment != null) garment.Worn = true;
            if (folded != null) folded.SetActive(false);
        }

        /// <summary>眩暈の強さ。遊んでいる間は DazeVolume が毎こま入れるシェーダのグローバル変数</summary>
        static void Daze(float blur, float wobble)
        {
            Shader.SetGlobalFloat("_DazeBlur", blur);
            Shader.SetGlobalFloat("_DazeWobble", wobble);
        }

        /// <summary>いま出しているリストの枠の大きさ（Dot）と、表の行と列の数</summary>
        static string Frame(string what)
        {
            var hud = UnityEngine.Object.FindFirstObjectByType<HudView>(FindObjectsInactive.Include);
            var list = hud != null ? hud.List : null;
            if (list == null) return what + ": リストの枠が無い";
            var f = list.Frame;
            return string.Format("{0}: 枠 {1:0}×{2:0} Dot、表 {3:0}×{4:0} Dot、送りの印 {5}",
                what, f.size.x / ListLayout.Dot, f.size.y / ListLayout.Dot, f.table.width / ListLayout.Dot, f.table.height / ListLayout.Dot, list.Advancing ? "有り" : "無し");
        }

        // ---- 撮る ------------------------------------------------------------

        public static string Shoot(string path, Stage stage)
        {
            return Shoot(path, stage, UiLens.Scale);
        }

        /// <summary>
        /// 開いている場面を撮る。3D は主人公の目のカメラを写したプローブのカメラで、UI は粗さ uiScale（1 でくっきり）の粗い画面で撮って重ねる。
        /// stage は HUD へ手を入れる（撮ったあと HUD の中身は戻さない。場面は開き直して捨てる）
        /// </summary>
        public static string Shoot(string path, Stage stage, float uiScale)
        {
            var player = UnityEngine.Object.FindFirstObjectByType<PlayerController>(FindObjectsInactive.Include);
            var eye = player != null && player.Eye != null ? player.Eye.GetComponentInChildren<Camera>(true) : Camera.main;
            if (eye == null) return "カメラが無い";
            var hud = UnityEngine.Object.FindFirstObjectByType<HudView>(FindObjectsInactive.Include);
            if (hud == null) return "Hud が無い";
            Camera probe = null;
            UiLens lens = null;
            Texture2D scene = null, ui = null, shot = null;
            try
            {
                probe = Probe(eye);
                scene = CheckDiveSky.Grab(probe, Width, Height);

                UiLens.Scale = uiScale;
                lens = UiLens.Make();
                lens.gameObject.hideFlags = HideFlags.HideAndDontSave;
                lens.Fit(Width, Height);
                ConsoleShot.Lens(hud.GetComponent<Canvas>(), lens.Eye, 0, Width, Height);
                hud.SetPrompt(null);
                hud.SetSubtitle(null);
                hud.SetChoice(null);
                hud.SetCenter(null);
                hud.SetCurtain(false);
                if (stage != null) stage(hud);
                lens.Draw();
                ui = ConsoleShot.Read(lens.Target);
                shot = ConsoleShot.Blend(scene, ui, Width, Height);
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllBytes(path, shot.EncodeToPNG());
                return "撮った " + path;
            }
            catch (Exception e)
            {
                return "例外: " + e;
            }
            finally
            {
                if (lens != null) { lens.Release(); UnityEngine.Object.DestroyImmediate(lens.gameObject); }
                UiLens.ResetScale();
                if (probe != null) UnityEngine.Object.DestroyImmediate(probe.gameObject);
                if (scene != null) UnityEngine.Object.DestroyImmediate(scene);
                if (ui != null) UnityEngine.Object.DestroyImmediate(ui);
                if (shot != null) UnityEngine.Object.DestroyImmediate(shot);
            }
        }

        /// <summary>src を写したプローブのカメラ。場面に残らず（HideAndDontSave）、自分では描かない（enabled=false）。捨てるのは呼ぶ側</summary>
        static Camera Probe(Camera src)
        {
            var go = new GameObject("CheckRoomProbe");
            go.hideFlags = HideFlags.HideAndDontSave;
            go.transform.SetPositionAndRotation(src.transform.position, src.transform.rotation);
            var cam = go.AddComponent<Camera>();
            cam.enabled = false;
            cam.CopyFrom(src);
            cam.enabled = false;
            var from = src.GetComponent<UniversalAdditionalCameraData>();
            var to = cam.GetUniversalAdditionalCameraData();
            if (from != null && to != null)
            {
                to.renderPostProcessing = from.renderPostProcessing;
                to.antialiasing = from.antialiasing;
                to.volumeLayerMask = from.volumeLayerMask;
                to.volumeTrigger = from.volumeTrigger;
                to.stopNaN = from.stopNaN;
                to.dithering = from.dithering;
            }
            return cam;
        }
    }
}
