using System;
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
    /// 机のモニターに映る、煙草を吸っている主人公（<see cref="TerminalReflection"/>・<see cref="MirrorSmoking"/>）の撮り比べ。再生せずに撮る。
    ///
    /// 自室（Room.unity）を開き、端末を調べた時の形（座った正面へ向き直した所。座った形のまま、ジャケットはまだ着ていない）にして、
    /// 写す範囲（鼻から胸の上・髪から胸の上）ごとに、映り込みが出てから <see cref="Times"/> 秒の四つの時刻で、
    /// 座ってモニターを見る一人称の目からゲームの解像度で撮る（960×540。中は 320×180、標準のフィルター）。
    /// 映り込みのカメラが撮る絵そのもの（板へ重ねる前）、腕が一人称の視界に入っていないことを見る広い一枚
    /// （モニターの全体と机の手前）、手と煙草の寄り（形を読むための、撮る間だけの灯りを足した絵）も撮る。
    ///
    /// **場面は開くが保存しない。** build なら、保存してある一式の代わりに映り込みの一式を組み直してから撮る（組み直しの検証）。
    /// 撮り終えたら前に開いていた場面へ戻す。開いている場面に未保存の変更があるときは撮らない。
    /// 撮るカメラと灯りと RenderTexture は同じ呼び出しの中で捨てる
    /// </summary>
    public static class MirrorSmokeShots
    {
        /// <summary>映り込みが出てからの秒。煙草を唇へ運ぶ所・吸っている所・吐いている所・吐いた煙が流れている所</summary>
        public static readonly float[] Times = { 0.5f, 2.5f, 4.5f, 6.5f };

        /// <summary>広い一枚の、座った正面からの見下ろしの足し（度）。モニターの下の縁と机の手前の縁が入る</summary>
        public const float WideDown = 14f;

        [MenuItem("HalfAware/Shoot the terminal reflection smoking", false, 207)]
        public static void ShootMenu()
        {
            var dir = Path.Combine(Path.GetTempPath(), "HalfAware", "reflection_smoke");
            Debug.Log(Shoot(dir, false));
        }

        /// <summary>dir へ撮る。build なら映り込みの一式を組み直してから。quick なら寄りと、範囲ごとに映り込みのカメラの絵を一枚だけ（形を詰める間）</summary>
        public static string Shoot(string dir, bool build, bool quick = false)
        {
            if (EditorApplication.isPlaying) return "再生中は撮らない";
            for (var i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty)
                    return "開いている場面に未保存の変更がある。保存するか捨ててからもう一度: " + SceneManager.GetSceneAt(i).path;
            var filter = Shader.GetGlobalFloat("_HaFilter");
            if (filter != 0f) return "画面のフィルターが標準でない（_HaFilter = " + filter + "）。標準に戻してから撮る";
            Directory.CreateDirectory(dir);
            var setup = EditorSceneManager.GetSceneManagerSetup();
            var sb = new StringBuilder();
            var async = ShaderUtil.allowAsyncCompilation;
            TerminalReflection reflection = null;
            try
            {
                ShaderUtil.allowAsyncCompilation = false;
                EditorSceneManager.OpenScene(PlaceProtagonist.RoomPath, OpenSceneMode.Single);
                if (build && !PlaceProtagonist.Reflection(sb)) return sb.Append("組み直せなかった").ToString();
                reflection = Object.FindFirstObjectByType<TerminalReflection>(FindObjectsInactive.Include);
                var seat = Object.FindFirstObjectByType<TerminalSeat>(FindObjectsInactive.Include);
                var player = Object.FindFirstObjectByType<PlayerController>(FindObjectsInactive.Include);
                if (reflection == null || seat == null || player == null) return "映り込み・端末の座る所・Player のどれかが無い";
                var smoking = reflection.Smoking;
                if (smoking == null) return "映り込みに煙草が無い（組み直していない）";
                Seat(seat, player);
                var room = RoomWisp(sb);
                var eye = player.Eye.position;
                var saved = reflection.Range;
                // 組み直したばかりの写し（mesh を書き換えた直後）は、初めの二こまほどは描かれない。三こま空撮りしておく
                smoking.Rehearse(MirrorSmoking.StartAt);
                reflection.Aim(eye, 1f);
                for (var i = 0; i < 3; i++) reflection.RenderNow(true);
                var main = TitleShots.Main();
                var look = main.transform.rotation;
                foreach (TerminalReflection.Extent extent in Enum.GetValues(typeof(TerminalReflection.Extent)))
                {
                    reflection.Range = extent;
                    var tag = extent.ToString().ToLowerInvariant();
                    // 部屋の一筋も同じ秒だけ流す（範囲ごとに、流れている形から始め直す）
                    var roomAt = 0f;
                    if (room != null) room.Simulate(room.main.startLifetime.constantMax, true, true);
                    foreach (var s in Times)
                    {
                        if (quick && s != Times[1]) continue;
                        if (room != null)
                        {
                            room.Simulate(s - roomAt, true, false);
                            roomAt = s;
                        }
                        smoking.Rehearse(MirrorSmoking.StartAt + s);
                        reflection.Aim(eye, 1f);
                        reflection.RenderNow(true);
                        var name = string.Format("{0}_{1:0.0}s", tag, s).Replace('.', '_');
                        if (!quick) sb.AppendLine(Eye(main, eye, look, Path.Combine(dir, name + "_eye.png")));
                        sb.AppendLine(Target(reflection, Path.Combine(dir, name + "_mirror.png")));
                    }
                }
                // 広い一枚（腕と煙が一人称の視界に入っていないこと）。映り込みは出ている所（場面に保存してある写す範囲で）
                reflection.Range = saved;
                smoking.Rehearse(MirrorSmoking.StartAt + Times[1]);
                reflection.Aim(eye, 1f);
                reflection.RenderNow(true);
                if (!quick) sb.AppendLine(Eye(main, eye, Quaternion.AngleAxis(WideDown, main.transform.right) * look, Path.Combine(dir, "wide_eye.png")));
                // 手と煙草の寄り（形を読むための絵）
                sb.AppendLine(Close(reflection, Path.Combine(dir, "close_front.png"), 0f, false));
                sb.AppendLine(Close(reflection, Path.Combine(dir, "close_quarter.png"), 40f, false));
                sb.AppendLine(Close(reflection, Path.Combine(dir, "close_side.png"), 80f, false));
                smoking.Rehearse(MirrorSmoking.StartAt + Times[3]);
                sb.AppendLine(Close(reflection, Path.Combine(dir, "close_front_aside.png"), 0f, false));
                sb.AppendLine(Close(reflection, Path.Combine(dir, "close_quarter_aside.png"), 40f, false));
            }
            catch (Exception e)
            {
                sb.AppendLine("例外: " + e);
            }
            finally
            {
                if (reflection != null)
                {
                    if (reflection.Smoking != null) reflection.Smoking.Clear();
                    foreach (var p in reflection.Panes)
                        if (p != null && p.target != null)
                        {
                            if (p.camera != null) p.camera.targetTexture = null;
                            p.target.Release();
                            Object.DestroyImmediate(p.target);
                            p.target = null;
                        }
                }
                ShaderUtil.allowAsyncCompilation = async;
                // 撮る間に書き換えた自室は捨てる。前に開いていたのが名前の無い場面なら、空の場面にしておく
                var hadRoom = false;
                foreach (var s in setup) if (s.path == PlaceProtagonist.RoomPath) hadRoom = true;
                if (SceneManager.GetActiveScene().path == PlaceProtagonist.RoomPath)
                    EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                TitleShots.Back(setup);
                if (!hadRoom && SceneManager.GetActiveScene().path == PlaceProtagonist.RoomPath)
                    EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                EditorUtility.UnloadUnusedAssetsImmediate();
            }
            File.WriteAllText(Path.Combine(dir, "log.txt"), sb.ToString());
            return sb.ToString().TrimEnd();
        }

        /// <summary>
        /// 部屋の煙（吸い終えた後、肘掛けに置いた右手の煙草から立ち続ける一筋。SmokePuffs）を、もう流れている形で立てる。
        /// モニターを読む間は、この一筋がモニターの前を横切る（映り込みの煙と並べて見るため）。返すのは一筋
        /// </summary>
        static ParticleSystem RoomWisp(StringBuilder log)
        {
            var puffs = Object.FindFirstObjectByType<SmokePuffs>(FindObjectsInactive.Include);
            var pose = Object.FindFirstObjectByType<SeatedPose>(FindObjectsInactive.Include);
            var an = pose != null ? pose.Animator : null;
            var finger = an != null ? an.GetBoneTransform(HumanBodyBones.RightIndexIntermediate) : null;
            if (puffs == null || finger == null) { log.AppendLine("部屋の煙が無い（映り込みの煙だけで撮る）"); return null; }
            puffs.Smolder(finger);
            return puffs.MakeWisp(finger);
        }

        /// <summary>端末を調べた時の形。座った正面へ向き直し、座った形（右手は肘掛け）のまま</summary>
        static void Seat(TerminalSeat seat, PlayerController player)
        {
            var ss = new SerializedObject(seat);
            player.PlaceAt(ss.FindProperty("seatSpot").vector3Value, ss.FindProperty("seatYaw").floatValue, 0f, 0f,
                ss.FindProperty("seatPitch").floatValue, ss.FindProperty("seatEyeHeight").floatValue);
            var chair = (Transform)ss.FindProperty("chair").objectReferenceValue;
            if (chair != null) chair.position = ss.FindProperty("chairSeated").vector3Value;
            var pose = (SeatedPose)ss.FindProperty("pose").objectReferenceValue;
            if (pose == null) return;
            pose.Seated = true;
            pose.UseAlternate = false;
            pose.Bind();
            pose.Apply();
        }

        /// <summary>一人称の目（目のカメラを写したもの）から、ゲームの解像度で撮る</summary>
        static string Eye(Camera main, Vector3 at, Quaternion turn, string path)
        {
            var go = new GameObject("MirrorSmokeEye") { hideFlags = HideFlags.HideAndDontSave };
            Texture2D shot = null;
            try
            {
                var cam = go.AddComponent<Camera>();
                cam.enabled = false;
                cam.CopyFrom(main);
                cam.enabled = false;
                cam.GetUniversalAdditionalCameraData().renderPostProcessing = true;
                go.transform.SetPositionAndRotation(at, turn);
                shot = TitleShots.Steady(cam);
                CheckDiveSky.Save(shot, path);
                return "撮った " + path;
            }
            finally
            {
                if (shot != null) Object.DestroyImmediate(shot);
                Object.DestroyImmediate(go);
            }
        }

        /// <summary>映り込みのカメラの絵そのもの（左右を返す前、板へ重ねる前）</summary>
        static string Target(TerminalReflection reflection, string path)
        {
            var rt = reflection.Panes[0].target;
            if (rt == null) return "映り込みのカメラの絵が無い";
            var keep = RenderTexture.active;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGBA32, false, false);
            try
            {
                RenderTexture.active = rt;
                tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
                tex.Apply();
                CheckDiveSky.Save(tex, path);
                return "撮った " + path;
            }
            finally
            {
                RenderTexture.active = keep;
                Object.DestroyImmediate(tex);
            }
        }

        /// <summary>
        /// 写しの手と煙草の寄り。唇から 0.35 m、体の正面から体の右へ yaw 度回った所から、撮る間だけの灯りを足して撮る（800×800）。
        /// 映り込みのカメラが撮る間と同じに、写しと煙草（smoke なら煙も）を点け、場面の主人公の体を伏せる
        /// </summary>
        static string Close(TerminalReflection reflection, string path, float yaw, bool smoke)
        {
            var so = new SerializedObject(reflection);
            var mirror = (Renderer)so.FindProperty("mirror").objectReferenceValue;
            var original = (Renderer)so.FindProperty("original").objectReferenceValue;
            var body = (Transform)so.FindProperty("body").objectReferenceValue;
            var smoking = reflection.Smoking;
            var go = new GameObject("MirrorSmokeClose") { hideFlags = HideFlags.HideAndDontSave };
            var lamp = new GameObject("MirrorSmokeCloseFill") { hideFlags = HideFlags.HideAndDontSave };
            var rt = RenderTexture.GetTemporary(800, 800, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            var keep = RenderTexture.active;
            var wasOriginal = original != null && original.enabled;
            Texture2D tex = null;
            try
            {
                var facing = Quaternion.Euler(0f, body.eulerAngles.y, 0f);
                var at = smoking.Mouth + Vector3.down * 0.02f;
                var from = at + facing * (Quaternion.Euler(0f, yaw, 0f) * new Vector3(0f, 0.04f, 0.35f));
                var cam = go.AddComponent<Camera>();
                cam.enabled = false;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0.12f, 0.12f, 0.13f);
                cam.fieldOfView = 34f;
                cam.nearClipPlane = 0.02f;
                cam.farClipPlane = 3f;
                cam.GetUniversalAdditionalCameraData().renderPostProcessing = false;
                go.transform.SetPositionAndRotation(from, Quaternion.LookRotation(at - from, Vector3.up));
                var l = lamp.AddComponent<Light>();
                l.type = LightType.Point;
                l.range = 2f;
                l.intensity = 0.35f;
                l.color = new Color(0.95f, 0.93f, 1f);
                l.shadows = LightShadows.None;
                lamp.transform.position = from + Vector3.up * 0.25f + go.transform.right * 0.2f;
                if (mirror != null) mirror.enabled = true;
                if (original != null) original.enabled = false;
                smoking.ShowCigarette(true);
                if (smoke) smoking.Shoot(true);
                cam.targetTexture = rt;
                cam.Render();
                cam.targetTexture = null;
                RenderTexture.active = rt;
                tex = new Texture2D(800, 800, TextureFormat.RGBA32, false, false);
                tex.ReadPixels(new Rect(0, 0, 800, 800), 0, 0);
                tex.Apply();
                CheckDiveSky.Save(tex, path);
                return "撮った " + path;
            }
            finally
            {
                RenderTexture.active = keep;
                RenderTexture.ReleaseTemporary(rt);
                if (tex != null) Object.DestroyImmediate(tex);
                if (mirror != null) mirror.enabled = false;
                if (original != null) original.enabled = wasOriginal;
                smoking.Shoot(false);
                Object.DestroyImmediate(go);
                Object.DestroyImmediate(lamp);
            }
        }
    }
}
