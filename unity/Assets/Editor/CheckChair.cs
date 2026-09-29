using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace HalfAware.EditorTools
{
    /// <summary>
    /// 自室の椅子（<see cref="BuildChair"/>）の確かめ。再生せずに、場面を開いてプローブのカメラで撮り、座った体が椅子に食い込んでいないかを測る。
    ///
    /// 撮る所: 立って斜め前と斜め後ろ・横・真後ろ（体は写さない）、座ったまま下（正面・右・左へ首を振って）、場面 1 の頭の天井を仰いだ形と起き上がった正面、
    /// 首を右へ振りきってジャケットを狙った所（印の文字も重ねる）、モニターの映り込み、ジャックの置き場の寄り、立ち上がった後の椅子と机のあいだ。
    /// プローブのカメラと RenderTexture は撮った同じ呼び出しの中で捨てる。場面の物（主人公の向き・椅子の位置・体の見え方）を書き換えるので、
    /// **撮り終えたら撮る前の場面へ戻し、書き換えた場面は捨てる。** 未保存の変更がある場面では撮らない
    /// </summary>
    public static class CheckChair
    {
        /// <summary>前の椅子（保存してある Room）を撮る</summary>
        public static string Before(string dir)
        {
            return Run(dir, "before", false);
        }

        /// <summary>Room を開いて椅子を組み（保存しない）、撮って測ってから捨てる</summary>
        public static string Preview(string dir)
        {
            return Run(dir, "after", true);
        }

        /// <summary>保存した後の Room・Connect・Rest・Notice を撮る。Connect は座ってジャックを挿す所も</summary>
        public static string Final(string dir)
        {
            var sb = new StringBuilder(Run(dir, "final", false));
            sb.AppendLine();
            sb.Append(Scenes(dir));
            return sb.ToString();
        }

        static string Guard()
        {
            if (EditorApplication.isPlaying) return "再生中は撮らない";
            for (var i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty)
                    return "開いている場面に未保存の変更がある: " + SceneManager.GetSceneAt(i).path;
            return null;
        }

        static string Run(string dir, string tag, bool build)
        {
            var stop = Guard();
            if (stop != null) return stop;
            Directory.CreateDirectory(dir);
            var setup = EditorSceneManager.GetSceneManagerSetup();
            var sb = new StringBuilder();
            var async = ShaderUtil.allowAsyncCompilation;
            try
            {
                ShaderUtil.allowAsyncCompilation = false;
                var room = EditorSceneManager.OpenScene(BuildChair.RoomPath, OpenSceneMode.Single);
                if (build) sb.AppendLine(BuildChair.Assemble(room));
                sb.AppendLine(Clash(room));
                sb.AppendLine("端末の前の形（両手を腿に）の " + Clash(room, true));
                RoomShots(room, dir, tag, sb);
            }
            catch (Exception e)
            {
                sb.AppendLine("例外: " + e);
            }
            finally
            {
                ShaderUtil.allowAsyncCompilation = async;
                TitleShots.Back(setup);
            }
            File.WriteAllText(Path.Combine(dir, tag + "_log.txt"), sb.ToString());
            return sb.ToString().TrimEnd();
        }

        // ---- 場面 1 --------------------------------------------------------------------

        static void RoomShots(Scene room, string dir, string tag, StringBuilder log)
        {
            var flow = Object.FindFirstObjectByType<SceneFlow>(FindObjectsInactive.Include);
            var player = flow.Player;
            var fso = new SerializedObject(flow);
            var seatEye = fso.FindProperty("seatEyeHeight").floatValue;
            var drop = fso.FindProperty("wakeDrop").floatValue;
            var startPitch = fso.FindProperty("wakeStartPitch").floatValue;
            var limit = fso.FindProperty("seatedHeadLimit").floatValue;
            var push = fso.FindProperty("chairPushBack").floatValue;
            var spot = (Transform)fso.FindProperty("standSpot").objectReferenceValue;
            var foot = player.transform.position;
            var body = player.transform.eulerAngles.y;
            var lead = new SerializedObject(player).FindProperty("eyeLead").floatValue;
            var chair = BuildChair.Find(room, BuildChair.ChairPath);
            var home = chair.position;
            var pose = Object.FindFirstObjectByType<SeatedPose>(FindObjectsInactive.Include);
            Seat(pose, false);
            log.AppendFormat("座った足元 {0}、目 {1:0.000}（背を預けた始まりは {2:0.000}、上へ {3:0} 度）、首の限り {4}、椅子の押し下げ {5:0.00}",
                foot.ToString("F2"), seatEye, seatEye - drop, -startPitch, limit, push).AppendLine();
            string P(string name) { return Path.Combine(dir, tag + "_" + name + ".png"); }

            // 1. 立って部屋から。体は写さない（椅子は保存した所）
            Body(false);
            log.AppendLine(Free(P("1a_front_quarter"), W(chair, 0.85f, 1.50f, 0.78f), W(chair, 0f, 0.72f, -0.05f), 0f));
            log.AppendLine(Free(P("1b_back_quarter"), W(chair, -0.95f, 1.55f, -1.05f), W(chair, 0f, 0.85f, -0.1f), 0f));
            log.AppendLine(Free(P("1c_side_left"), W(chair, -1.25f, 1.05f, 0.05f), W(chair, 0f, 0.72f, -0.05f), 0f));
            log.AppendLine(Free(P("1d_back"), W(chair, 0.12f, 1.45f, -1.35f), W(chair, 0f, 0.95f, -0.3f), 0f));
            log.AppendLine(Free(P("1e_crown_close"), W(chair, 0.35f, 1.62f, -0.95f), W(chair, 0f, 1.12f, -0.45f), 42f));
            // 形を読むための、撮る間だけの灯りを足した絵（ゲームの明るさではない）
            log.AppendLine(Free(P("1f_front_fill"), W(chair, 0.95f, 1.35f, 1.0f), W(chair, 0f, 0.72f, -0.05f), 50f, 1.2f));
            log.AppendLine(Free(P("1g_side_fill"), W(chair, -1.35f, 1.0f, 0.0f), W(chair, 0f, 0.75f, -0.08f), 50f, 1.2f));
            log.AppendLine(Free(P("1h_back_fill"), W(chair, -0.75f, 1.45f, -1.25f), W(chair, 0f, 0.85f, -0.2f), 50f, 1.2f));
            Body(true);

            // 2. 座ったまま下を見る。正面・右・左
            player.PlaceAt(foot, body, limit, 0f, PlayerController.PitchDownLimit, seatEye);
            log.AppendLine(CheckRoom.Shoot(P("2a_seated_down"), null));
            player.PlaceAt(foot, body, limit, 50f, PlayerController.PitchDownLimit, seatEye);
            log.AppendLine(CheckRoom.Shoot(P("2b_seated_down_right"), null));
            player.PlaceAt(foot, body, limit, -50f, PlayerController.PitchDownLimit, seatEye);
            log.AppendLine(CheckRoom.Shoot(P("2c_seated_down_left"), null));

            // 3. 場面 1 の頭。背を預けて天井を仰いだ始まりと、起き上がった正面
            player.PlaceAt(foot, body, limit, 0f, startPitch, seatEye - drop);
            log.AppendLine(CheckRoom.Shoot(P("3a_wake_start"), null));
            player.PlaceAt(foot, body, limit, 0f, 0f, seatEye);
            log.AppendLine(CheckRoom.Shoot(P("3b_after_rising"), null));

            // 4. 首を右へ振りきってジャケットを狙う（印の文字も）
            var items = Items();
            var jacket = Find(items, RoomIds.Jacket);
            if (jacket != null)
            {
                var aim = Aim(foot, body, seatEye, lead, jacket.Position, limit);
                player.PlaceAt(foot, body, limit, aim.x, aim.y, seatEye);
                var picked = InteractionPicker.Select(player.Eye.position, player.Eye.forward, items, new HashSet<string> { RoomIds.Jack, RoomIds.Cigarette }, InteractionPicker.MaxAngle);
                log.AppendFormat("ジャケットへ: 首 {0:0.0} 度（限り {1}）・下へ {2:0.0} 度。選ばれた物: {3}", aim.x, limit, aim.y, picked != null ? picked.Id : "無し").AppendLine();
                log.AppendLine(CheckRoom.Shoot(P("4_jacket_aim"), h => h.SetPrompt(picked != null ? HudView.Prompt(picked.Label) : null)));
                // 左の煙草も狙えるか
                var cig = Find(items, RoomIds.Cigarette);
                if (cig != null)
                {
                    var toCig = Aim(foot, body, seatEye, lead, cig.Position, limit);
                    player.PlaceAt(foot, body, limit, toCig.x, toCig.y, seatEye);
                    var c = InteractionPicker.Select(player.Eye.position, player.Eye.forward, items, new HashSet<string> { RoomIds.Jack }, InteractionPicker.MaxAngle);
                    log.AppendFormat("煙草へ: 首 {0:0.0} 度・下へ {1:0.0} 度。選ばれた物: {2}", toCig.x, toCig.y, c != null ? c.Id : "無し").AppendLine();
                    log.AppendLine(CheckRoom.Shoot(P("4b_cigarette_aim"), h => h.SetPrompt(c != null ? HudView.Prompt(c.Label) : null)));
                }
                var jack = Find(items, RoomIds.Jack);
                if (jack != null)
                {
                    var toJack = Aim(foot, body, seatEye, lead, jack.Position, limit);
                    player.PlaceAt(foot, body, limit, toJack.x, toJack.y, seatEye);
                    var c = InteractionPicker.Select(player.Eye.position, player.Eye.forward, items, new HashSet<string>(), InteractionPicker.MaxAngle);
                    log.AppendFormat("手首のジャックへ: 首 {0:0.0} 度・下へ {1:0.0} 度。選ばれた物: {2}", toJack.x, toJack.y, c != null ? c.Id : "無し").AppendLine();
                }
            }

            // 5. モニターの映り込み（端末の前に座り、両手を腿に置いた形）
            log.AppendLine(Mirror(P("5_reflection"), player, chair));
            chair.position = home;
            Seat(pose, false);

            // 6. ジャックの置き場の寄り。場面 3 と同じく、手首のジャックを肘掛けの置き場へ移して撮る
            Park(chair);
            log.AppendLine(Free(P("6_jack_rest_close"), W(chair, 0.62f, 1.02f, 0.32f), W(chair, 0.30f, 0.66f, 0.0f), 45f));

            // 7. 立ち上がった後。椅子を押し下げ、椅子と机のあいだに立って椅子を見下ろす。横から椅子と机のあいだの幅も
            chair.position = home - chair.forward * push;
            Body(false);
            if (spot != null)
            {
                var eye = spot.position + Vector3.up * PlayerController.StandingEyeHeight + Quaternion.Euler(0f, 180f, 0f) * Vector3.forward * lead;
                log.AppendLine(Free(P("7a_stand_between"), eye, eye + Quaternion.Euler(35f, 180f, 0f) * Vector3.forward, 0f));
                log.AppendLine(Free(P("7b_stand_gap_side"), new Vector3(0.25f, 1.75f, 1.55f), new Vector3(1.5f, 0.55f, 1.45f), 0f));
                log.AppendLine(Gap(chair, spot));
            }
            chair.position = home;
        }

        // ---- 場面 3・5・7 -----------------------------------------------------------------

        static string Scenes(string dir)
        {
            var stop = Guard();
            if (stop != null) return stop;
            var setup = EditorSceneManager.GetSceneManagerSetup();
            var sb = new StringBuilder();
            var async = ShaderUtil.allowAsyncCompilation;
            try
            {
                ShaderUtil.allowAsyncCompilation = false;
                // 場面 3。座って、置き場のジャックを見下ろし、手首の差込口を狙う
                var connect = EditorSceneManager.OpenScene(BuildConnect.ScenePath, OpenSceneMode.Single);
                var chair = BuildChair.Find(connect, BuildChair.ChairPath);
                var player = Object.FindFirstObjectByType<PlayerController>(FindObjectsInactive.Include);
                var pose = Object.FindFirstObjectByType<SeatedPose>(FindObjectsInactive.Include);
                Seat(pose, false);
                var eye = BuildConnect.SeatEyeHeight();
                var lead = new SerializedObject(player).FindProperty("eyeLead").floatValue;
                var rest = chair.Find("JackRest");
                sb.AppendLine("場面 3: 置き場の子 " + (rest != null && rest.childCount > 0 ? rest.GetChild(0).name : "無し"));
                sb.AppendLine(Free(Path.Combine(dir, "final_6a_connect_jack_rest"), W(chair, 0.62f, 1.02f, 0.32f), W(chair, 0.30f, 0.66f, 0.0f), 45f));
                Interactable jack = null;
                foreach (var it in Object.FindObjectsByType<Interactable>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                    if (it.Id == ConnectIds.Jack) jack = it;
                if (jack != null)
                {
                    // 判定点は手首に付いて回るので、先に座らせてから狙う
                    player.PlaceAt(BuildConnect.SeatAt, 0f, HeadTurn.DefaultLimit, 0f, 0f, eye);
                    var aim = Aim(BuildConnect.SeatAt, 0f, eye, lead, jack.Position, HeadTurn.DefaultLimit);
                    player.PlaceAt(BuildConnect.SeatAt, 0f, HeadTurn.DefaultLimit, aim.x, aim.y, eye);
                    var restAt = rest != null ? rest.position : Vector3.zero;
                    sb.AppendFormat("ジャックを挿す対象（手首）へ: 首 {0:0.0} 度・下へ {1:0.0} 度。対象から置き場まで {2:0.000} m、座った目から置き場まで {3:0.000} m",
                        aim.x, aim.y, Vector3.Distance(jack.transform.position, restAt), Vector3.Distance(player.Eye.position, restAt)).AppendLine();
                    sb.AppendLine(CheckRoom.Shoot(Path.Combine(dir, "final_6b_connect_plug_aim.png"), h => h.SetPrompt(HudView.Prompt(jack.Label))));
                    player.PlaceAt(BuildConnect.SeatAt, 0f, HeadTurn.DefaultLimit, 55f, PlayerController.PitchDownLimit, eye);
                    sb.AppendLine(CheckRoom.Shoot(Path.Combine(dir, "final_6c_connect_down_right.png"), null));
                }
                // 場面 5・7。座ったまま下を見る
                foreach (var path in new[] { BuildRest.ScenePath, BuildNotice.ScenePath })
                {
                    var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                    var p = Object.FindFirstObjectByType<PlayerController>(FindObjectsInactive.Include);
                    Seat(Object.FindFirstObjectByType<SeatedPose>(FindObjectsInactive.Include), false);
                    p.PlaceAt(BuildConnect.SeatAt, 0f, HeadTurn.DefaultLimit, -45f, PlayerController.PitchDownLimit, BuildConnect.SeatEyeHeight());
                    var n = Path.GetFileNameWithoutExtension(path).ToLowerInvariant();
                    sb.AppendLine(n + ": 椅子の子 " + BuildChair.ChairChildren(scene));
                    sb.AppendLine(CheckRoom.Shoot(Path.Combine(dir, "final_" + n + "_seated_down_left.png"), null));
                }
            }
            catch (Exception e)
            {
                sb.AppendLine("例外: " + e);
            }
            finally
            {
                ShaderUtil.allowAsyncCompilation = async;
                TitleShots.Back(setup);
            }
            return sb.ToString().TrimEnd();
        }

        // ---- 置き方 ------------------------------------------------------------------

        /// <summary>椅子から見た位置を場面の位置へ</summary>
        static Vector3 W(Transform chair, float x, float y, float z)
        {
            return chair.TransformPoint(new Vector3(x, y, z));
        }

        /// <summary>座った形を当てる。alternate なら端末の前の形（両手を腿に）</summary>
        static void Seat(SeatedPose pose, bool alternate)
        {
            if (pose == null) return;
            pose.Seated = true;
            pose.UseAlternate = alternate;
            pose.Bind();
            pose.Apply();
        }

        /// <summary>主人公の体とジャックのケーブルを写すか。立って外から撮る時は写さない（座った形のまま置いてあるので）</summary>
        static void Body(bool on)
        {
            var pro = GameObject.Find("Player/Protagonist");
            if (pro != null) foreach (var r in pro.GetComponentsInChildren<Renderer>(true)) r.enabled = on;
            var cable = GameObject.Find("Room/Chair/Cable");
            if (cable != null) foreach (var r in cable.GetComponentsInChildren<Renderer>(true)) r.enabled = on;
        }

        /// <summary>手首のジャックを肘掛けの置き場へ移す（場面 3 の組み立て BuildConnect.Park と同じ置き方）</summary>
        static void Park(Transform chair)
        {
            var rest = chair.Find("JackRest");
            var pro = GameObject.Find("Player/Protagonist");
            var an = pro != null ? pro.GetComponent<Animator>() : null;
            var arm = an != null ? an.GetBoneTransform(HumanBodyBones.RightLowerArm) : null;
            if (rest == null || arm == null) return;
            var port = arm.Find(BuildProps.PortName);
            var holder = port != null ? port : arm;
            var jack = holder.Find("Jack");
            if (jack == null) return;
            jack.SetParent(rest, false);
            jack.localPosition = Vector3.zero;
            jack.localRotation = Quaternion.identity;
            jack.localScale = Vector3.one;
        }

        static List<IInteractable> Items()
        {
            return new List<IInteractable>(Object.FindObjectsByType<Interactable>(FindObjectsInactive.Include, FindObjectsSortMode.InstanceID));
        }

        static IInteractable Find(List<IInteractable> items, string id)
        {
            foreach (var it in items) if (it.Id == id) return it;
            return null;
        }

        /// <summary>座ったまま target を見る首の向き（体から見た左右）と上下。首の限りと上下の範囲に収める</summary>
        static Vector2 Aim(Vector3 foot, float body, float eye, float lead, Vector3 target, float limit)
        {
            var want = Gaze.Toward(foot, body, false, new Vector3(0f, eye, lead), target);
            var head = Mathf.Clamp(Mathf.DeltaAngle(body, want.x), -limit, limit);
            return new Vector2(head, PlayerController.ClampPitch(want.y));
        }

        /// <summary>立ち上がった後の、椅子の当たりと立ち位置の当たりの隙間（前後）と、椅子の見た目の前の端から立ち位置まで</summary>
        static string Gap(Transform chair, Transform spot)
        {
            var player = Object.FindFirstObjectByType<PlayerController>(FindObjectsInactive.Include);
            var cc = player != null ? player.GetComponent<CharacterController>() : null;
            var radius = cc != null ? cc.radius : 0.3f;
            var front = float.MinValue;
            var nearest = float.MaxValue;
            var near = "";
            var blocker = chair.Find("Blocker");
            var centre = new Vector2(spot.position.x, spot.position.z);
            if (blocker != null)
                foreach (var box in blocker.GetComponentsInChildren<BoxCollider>(true))
                {
                    var c = box.transform.TransformPoint(box.center);
                    var h = box.size * 0.5f;
                    front = Mathf.Max(front, c.z + h.z);
                    // 上から見た、立ち位置の芯から箱までの距離
                    var dx = Mathf.Max(0f, Mathf.Abs(centre.x - c.x) - h.x);
                    var dz = Mathf.Max(0f, Mathf.Abs(centre.y - c.z) - h.z);
                    var d = Mathf.Sqrt(dx * dx + dz * dz);
                    if (d < nearest) { nearest = d; near = box.name; }
                }
            var mesh = chair.Find(BuildChair.MeshName);
            var seen = mesh != null ? mesh.GetComponent<Renderer>().bounds.max.z : float.NaN;
            return string.Format("立ち上がった後: 椅子の当たりの前の端 z {0:0.000}。立ち位置 {1} の芯から最も近い当たり（{2}）まで上から見て {3:0.000} m（体の当たりの半径 {4:0.00}、ゆとり {5:0.000} m）。見た目の前の端 z {6:0.000}（足置きの先）",
                front, spot.position.ToString("F2"), near, nearest, radius, nearest - radius, seen);
        }

        // ---- 撮る ------------------------------------------------------------------

        /// <summary>
        /// 主人公の目のカメラを写したプローブで、eye から look へ向けて撮る。fov が 0 なら目のカメラのまま。
        /// fill が 0 より大きければ、撮る間だけ目の脇に灯りを置く（形を読むための絵。ゲームの明るさではない）
        /// </summary>
        static string Free(string path, Vector3 eye, Vector3 look, float fov, float fill = 0f)
        {
            if (!path.EndsWith(".png")) path += ".png";
            var main = TitleShots.Main();
            if (main == null) return "カメラが無い";
            var go = new GameObject("ChairProbe") { hideFlags = HideFlags.HideAndDontSave };
            GameObject lamp = null;
            Texture2D shot = null;
            try
            {
                if (fill > 0f)
                {
                    lamp = new GameObject("ChairProbeFill") { hideFlags = HideFlags.HideAndDontSave };
                    var l = lamp.AddComponent<Light>();
                    l.type = LightType.Point;
                    l.range = 4f;
                    l.intensity = fill;
                    l.color = new Color(0.95f, 0.93f, 1f);
                    l.shadows = LightShadows.None;
                    lamp.transform.position = eye + Vector3.up * 0.4f + Vector3.Cross(Vector3.up, look - eye).normalized * 0.3f;
                }
                var cam = go.AddComponent<Camera>();
                cam.enabled = false;
                cam.CopyFrom(main);
                cam.enabled = false;
                cam.nearClipPlane = 0.02f;
                cam.GetUniversalAdditionalCameraData().renderPostProcessing = true;
                if (fov > 0f) cam.fieldOfView = fov;
                go.transform.SetPositionAndRotation(eye, Quaternion.LookRotation(look - eye, Vector3.up));
                shot = TitleShots.Steady(cam);
                CheckDiveSky.Save(shot, path);
                return "撮った " + path;
            }
            finally
            {
                if (shot != null) Object.DestroyImmediate(shot);
                if (lamp != null) Object.DestroyImmediate(lamp);
                Object.DestroyImmediate(go);
            }
        }

        /// <summary>モニターの映り込み（ReunionShots の (a) と同じ置き方）。映り込みのカメラの絵は撮った後に捨てる</summary>
        static string Mirror(string path, PlayerController player, Transform chair)
        {
            var reflection = Object.FindFirstObjectByType<TerminalReflection>(FindObjectsInactive.Include);
            var seat = Object.FindFirstObjectByType<TerminalSeat>(FindObjectsInactive.Include);
            if (reflection == null || seat == null) return "映り込みか端末の座る所が無い";
            var ss = new SerializedObject(seat);
            player.PlaceAt(ss.FindProperty("seatSpot").vector3Value, ss.FindProperty("seatYaw").floatValue, 0f, 0f,
                ss.FindProperty("seatPitch").floatValue, ss.FindProperty("seatEyeHeight").floatValue);
            chair.position = ss.FindProperty("chairSeated").vector3Value;
            Seat((SeatedPose)ss.FindProperty("pose").objectReferenceValue, true);
            foreach (var g in Object.FindObjectsByType<Garment>(FindObjectsInactive.Include, FindObjectsSortMode.None)) g.Worn = true;
            try
            {
                reflection.Aim(player.Eye.position, 1f);
                reflection.RenderNow(true);
                var screen = reflection.Panes[0].screen;
                var from = screen.position + screen.rotation * new Vector3(0f, 0.06f, -0.60f);
                return Free(path, from, screen.position, 0f);
            }
            finally
            {
                foreach (var p in reflection.Panes)
                    if (p != null && p.target != null)
                    {
                        if (p.camera != null) p.camera.targetTexture = null;
                        p.target.Release();
                        Object.DestroyImmediate(p.target);
                        p.target = null;
                    }
                foreach (var g in Object.FindObjectsByType<Garment>(FindObjectsInactive.Include, FindObjectsSortMode.None)) g.Worn = false;
            }
        }

        // ---- 食い込み ------------------------------------------------------------------

        /// <summary>
        /// 座った体（保存してある座った形。alternate なら端末の前の形）の頂点が、椅子の中身の詰まった部品の面より 5 mm 以上内にあるかを部品ごとに数える。
        /// 部品の面は BuildChair.Shape で組み直して測る（保存した mesh と同じ形）。前の椅子（箱を並べた物）は箱の中で測る
        /// </summary>
        public static string Clash(Scene room, bool alternate = false)
        {
            var chair = BuildChair.Find(room, BuildChair.ChairPath);
            var pro = BuildChair.Find(room, "Player/Protagonist");
            if (chair == null || pro == null) return "椅子か主人公が無い";
            Seat(pro.GetComponent<SeatedPose>(), alternate);
            var skin = SkinPoint.BodyOf(pro);
            var baked = new Mesh();
            skin.BakeMesh(baked, true);
            var local = baked.vertices;
            Object.DestroyImmediate(baked);
            for (var i = 0; i < local.Length; i++) local[i] = chair.InverseTransformPoint(skin.transform.TransformPoint(local[i]));
            var weights = skin.sharedMesh.boneWeights;
            var bones = skin.bones;
            var sb = new StringBuilder();

            if (chair.Find(BuildChair.MeshName) == null)
            {
                // 前の椅子。見た目の箱の中で測る
                sb.Append("食い込み（前の椅子の箱）: ");
                foreach (Transform part in chair)
                {
                    var mf = part.GetComponent<MeshFilter>();
                    if (mf == null || mf.sharedMesh == null || mf.sharedMesh.name != "Cube") continue;
                    var n = 0;
                    var deepest = 0f;
                    var who = "";
                    for (var i = 0; i < local.Length; i++)
                    {
                        var p = part.InverseTransformPoint(chair.TransformPoint(local[i]));
                        if (Mathf.Abs(p.x) >= 0.5f || Mathf.Abs(p.y) >= 0.5f || Mathf.Abs(p.z) >= 0.5f) continue;
                        var s = part.localScale;
                        var d = Mathf.Min((0.5f - Mathf.Abs(p.x)) * s.x, Mathf.Min((0.5f - Mathf.Abs(p.y)) * s.y, (0.5f - Mathf.Abs(p.z)) * s.z));
                        if (d < 0.005f) continue;
                        n++;
                        if (d > deepest) { deepest = d; who = bones[weights[i].boneIndex0].name; }
                    }
                    if (n > 0) sb.AppendFormat("{0} {1} 点（深さ {2:0.000}、{3}）。", part.name, n, deepest, who);
                }
                return sb.ToString();
            }

            var made = BuildChair.Shape();
            var verts = made.Mesh.vertices;
            Object.DestroyImmediate(made.Mesh);
            sb.Append("食い込み（5 mm より深い点。中身の詰まった部品ごと）: ");
            var any = false;
            foreach (var piece in made.Pieces)
            {
                if (!piece.Solid) continue;
                var box = piece.Bounds;
                box.Expand(0.02f);
                var n = 0;
                var deepest = 0f;
                var who = "";
                var where = Vector3.zero;
                for (var i = 0; i < local.Length; i++)
                {
                    var p = local[i];
                    if (!box.Contains(p)) continue;
                    var best = float.MaxValue;
                    var sign = 1f;
                    for (var t = 0; t < piece.Triangles.Length; t += 3)
                    {
                        var a = verts[piece.Triangles[t]];
                        var b = verts[piece.Triangles[t + 1]];
                        var c = verts[piece.Triangles[t + 2]];
                        var q = Closest(p, a, b, c);
                        var dist = (p - q).sqrMagnitude;
                        if (dist >= best) continue;
                        var nrm = Vector3.Cross(b - a, c - a);
                        if (nrm.sqrMagnitude < 1e-14f) continue;
                        best = dist;
                        sign = Vector3.Dot(p - q, nrm) < 0f ? -1f : 1f;
                    }
                    if (sign > 0f) continue;
                    var depth = Mathf.Sqrt(best);
                    if (depth < 0.005f) continue;
                    n++;
                    if (depth > deepest) { deepest = depth; who = bones[weights[i].boneIndex0].name; where = p; }
                }
                if (n == 0) continue;
                any = true;
                sb.AppendFormat("{0} {1} 点（最も深い {2:0.000} m、{3} {4}）。", piece.Name, n, deepest, who, where.ToString("F3"));
            }
            if (!any) sb.Append("無し");
            return sb.ToString();
        }

        /// <summary>三角 abc の上で p にいちばん近い点</summary>
        static Vector3 Closest(Vector3 p, Vector3 a, Vector3 b, Vector3 c)
        {
            var ab = b - a;
            var ac = c - a;
            var ap = p - a;
            var d1 = Vector3.Dot(ab, ap);
            var d2 = Vector3.Dot(ac, ap);
            if (d1 <= 0f && d2 <= 0f) return a;
            var bp = p - b;
            var d3 = Vector3.Dot(ab, bp);
            var d4 = Vector3.Dot(ac, bp);
            if (d3 >= 0f && d4 <= d3) return b;
            var vc = d1 * d4 - d3 * d2;
            if (vc <= 0f && d1 >= 0f && d3 <= 0f) return a + ab * (d1 / (d1 - d3));
            var cp = p - c;
            var d5 = Vector3.Dot(ab, cp);
            var d6 = Vector3.Dot(ac, cp);
            if (d6 >= 0f && d5 <= d6) return c;
            var vb = d5 * d2 - d1 * d6;
            if (vb <= 0f && d2 >= 0f && d6 <= 0f) return a + ac * (d2 / (d2 - d6));
            var va = d3 * d6 - d5 * d4;
            if (va <= 0f && (d4 - d3) >= 0f && (d5 - d6) >= 0f) return b + (c - b) * ((d4 - d3) / ((d4 - d3) + (d5 - d6)));
            var denom = 1f / (va + vb + vc);
            return a + ab * (vb * denom) + ac * (vc * denom);
        }
    }
}
