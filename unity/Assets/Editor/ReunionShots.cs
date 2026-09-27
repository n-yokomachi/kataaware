using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace HalfAware.EditorTools
{
    /// <summary>
    /// 場面 10 のモンタージュの (a)・(b) の絵を、ゲームの見え方で前もって撮る（タイトルの背景の撮り方 <see cref="TitleShots"/> に倣う）。
    ///
    /// - (a) 場面 1 で端末の黒い画面に映った自分（口元・顎・首）。自室（<c>Room.unity</c>）を開き、端末の前に座らせ、
    ///   映り込み（<see cref="TerminalReflection"/>）を浮かべて、正面のモニターを近くから撮る
    /// - (b) 場面 6 の続き。振り返った女性（過去の主人公。黒い髪、黒子は口元の左）の顔が初めて見える。村を夕方の庭にして、
    ///   主の座った目から、寄ってきた女性の顔を撮る。逆光の影（場面 6 の演出が色に掛ける暗さ）は掛けない
    ///
    /// 場面の Player/Main Camera を写したカメラで 960×540 に撮り（render scale 1/3 で中は 320×180）、中の 320×180 を
    /// <c>Assets/Textures/Reunion/</c> に置く（最近傍で画面に敷けば同じ絵）。
    ///
    /// **場面は開くが保存しない。** 撮り終えたら前に開いていた場面へ戻す。開いている場面に未保存の変更があるときは撮らない。
    /// カメラと灯りは <c>HideAndDontSave</c> で作り、撮り終えたら捨てる
    /// </summary>
    public static class ReunionShots
    {
        public const string RoomPath = "Assets/Scenes/Room.unity";

        /// <summary>(a) の目。正面のモニターの表の芯から、手前へ・上へ（m）</summary>
        static readonly Vector3 MirrorFrom = new Vector3(0f, 0.06f, -0.60f);

        /// <summary>
        /// (b) の女性の顔と、主の目の隔たり（上から見て）。m。途切れる所（1.6 m）より近く、歩いてくる線の終わり（主の椅子の前）の近く。
        /// 座った主が、前に立った女性の顔を見上げる。0.95 m では顔が画面の中で小さく、(a)・(c) の顔の大きさと揃わなかった
        /// </summary>
        public const float GardenApart = 0.6f;

        /// <summary>
        /// (b) を撮る目と女性の目の隔たり。m。主の目から女性の顔への線の上で、顔へ寄せる（場面 6 の頭で聞き返すように身を乗り出した形の続き）。
        /// 座った目のままでは、顔は画面の高さの 2 割ほどで、(c) の目の前の顔と釣り合わなかった
        /// </summary>
        public const float GardenLean = 0.42f;

        /// <summary>(b) で女性の顔を起こす灯り（撮る間だけ）。主の目の側、顔の前から。強さ。0 なら足さない</summary>
        public const float GardenFill = 1.4f;

        [MenuItem("HalfAware/Shoot the reunion montage", false, 185)]
        public static void ShootMenu()
        {
            Debug.Log(ShootAll());
        }

        public static string ShootAll()
        {
            if (EditorApplication.isPlaying) return "再生中は撮らない";
            for (var i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty)
                    return "開いている場面に未保存の変更がある。保存するか捨ててからもう一度: " + SceneManager.GetSceneAt(i).path;
            var setup = EditorSceneManager.GetSceneManagerSetup();
            var sb = new StringBuilder();
            sb.AppendLine("RenderTexture（どのアセットにも属さない）: 始め " + CheckVillage.LooseRenderTextures());
            var async = ShaderUtil.allowAsyncCompilation;
            try
            {
                ShaderUtil.allowAsyncCompilation = false;
                Directory.CreateDirectory(Path.GetDirectoryName(BuildVillage.MirrorShotPath));
                sb.AppendLine(Mirror());
                sb.AppendLine(Garden());
            }
            catch (Exception e)
            {
                sb.AppendLine("例外: " + e);
            }
            finally
            {
                ShaderUtil.allowAsyncCompilation = async;
                TitleShots.Back(setup);
                GardenHandoff.Clear();
                sb.AppendLine("RenderTexture（どのアセットにも属さない）: 終わり " + CheckVillage.LooseRenderTextures());
            }
            return sb.ToString().TrimEnd();
        }

        /// <summary>(a) 端末の黒い画面の映り込み</summary>
        static string Mirror()
        {
            EditorSceneManager.OpenScene(RoomPath, OpenSceneMode.Single);
            var reflection = Object.FindFirstObjectByType<TerminalReflection>(FindObjectsInactive.Include);
            var seat = Object.FindFirstObjectByType<TerminalSeat>(FindObjectsInactive.Include);
            var player = Object.FindFirstObjectByType<PlayerController>(FindObjectsInactive.Include);
            if (reflection == null || seat == null || player == null) return "(a) 自室に映り込み・端末の座る所・Player のどれかが無い";
            var ss = new SerializedObject(seat);
            // 端末を調べた時と同じ形。端末の前に座り、両手を腿に置いた形（二つ目の座った形）、ジャケットは着ている
            player.PlaceAt(ss.FindProperty("seatSpot").vector3Value, ss.FindProperty("seatYaw").floatValue, 0f, 0f,
                ss.FindProperty("seatPitch").floatValue, ss.FindProperty("seatEyeHeight").floatValue);
            var chair = (Transform)ss.FindProperty("chair").objectReferenceValue;
            if (chair != null) chair.position = ss.FindProperty("chairSeated").vector3Value;
            var pose = (SeatedPose)ss.FindProperty("pose").objectReferenceValue;
            if (pose != null)
            {
                pose.Seated = true;
                pose.UseAlternate = true;
                pose.Bind();
                pose.Apply();
            }
            foreach (var g in Object.FindObjectsByType<Garment>(FindObjectsInactive.Include, FindObjectsSortMode.None)) g.Worn = true;
            reflection.Aim(player.Eye.position, 1f);
            reflection.RenderNow(true);
            var screen = reflection.Panes[0].screen;
            var from = screen.position + screen.rotation * MirrorFrom;
            var look = screen.position - from;
            var shot = Shoot(from, Quaternion.LookRotation(look, Vector3.up), BuildVillage.MirrorShotPath);
            // 映り込みのカメラの絵を捨てる（再生中は TerminalReflection が OnDestroy で捨てる物）
            foreach (var p in reflection.Panes)
                if (p != null && p.target != null)
                {
                    if (p.camera != null) p.camera.targetTexture = null;
                    p.target.Release();
                    Object.DestroyImmediate(p.target);
                    p.target = null;
                }
            return "(a) " + shot;
        }

        /// <summary>(b) 場面 6 の続き。振り返った女性の顔</summary>
        static string Garden()
        {
            EditorSceneManager.OpenScene(BuildVillage.ScenePath, OpenSceneMode.Single);
            var d = Object.FindFirstObjectByType<GardenMemoryDirector>(FindObjectsInactive.Include);
            if (d == null) return "(b) 村に場面 6 の進行が無い";
            var so = new SerializedObject(d);
            var motion = (PersonMotion)so.FindProperty("womanMotion").objectReferenceValue;
            var mover = (Mover)so.FindProperty("woman").objectReferenceValue;
            var player = (PlayerController)so.FindProperty("player").objectReferenceValue;
            GardenHandoff.Pending = true;
            GardenHandoff.Take();
            d.Begin();
            d.Open();
            var an = motion.GetComponentInChildren<Animator>();
            var eye = player.Eye.position;
            // 歩いてくる線の上で、顔と目の隔たりが GardenApart になる所
            var t = mover.At;
            for (; t < mover.Until; t += 0.02f)
            {
                mover.Play(t);
                var face = BodyPoser.Eyes(an);
                if (new Vector2(face.x - eye.x, face.z - eye.z).magnitude <= GardenApart) break;
            }
            mover.Play(t);
            var toward = eye - mover.transform.position;
            mover.transform.rotation = Quaternion.Euler(0f, Mathf.Atan2(toward.x, toward.z) * Mathf.Rad2Deg, 0f);
            motion.Watch(player.Eye);
            motion.Sample(motion.Idle, 0.3f);
            // 逆光の影は掛けない（顔が初めて見える一枚）
            foreach (var r in motion.GetComponentsInChildren<Renderer>(true)) r.SetPropertyBlock(null);
            var at = BodyPoser.Eyes(an);
            var look = at + Vector3.down * 0.03f - eye;
            GameObject lamp = null;
            try
            {
                if (GardenFill > 0f)
                {
                    // 夕日は女性の背の側にあり、顔は影になる。撮る間だけ、主の目の側から顔の近くを弱く照らす
                    lamp = new GameObject("ReunionShotFill") { hideFlags = HideFlags.HideAndDontSave };
                    var l = lamp.AddComponent<Light>();
                    l.type = LightType.Point;
                    l.range = 1.4f;
                    l.intensity = GardenFill;
                    l.color = new Color(1f, 0.9f, 0.78f);
                    l.shadows = LightShadows.None;
                    lamp.transform.position = at - look.normalized * 0.55f + Vector3.up * 0.05f;
                }
                var from = at - look.normalized * GardenLean;
                return string.Format("(b) 主の目から女性の目まで {0:0.00} m（上から見て {1:0.00} m）・撮る目は顔から {2:0.00} m・灯り {3} → {4}", look.magnitude,
                    new Vector2(look.x, look.z).magnitude, GardenLean, GardenFill, Shoot(from, Quaternion.LookRotation(look, Vector3.up), BuildVillage.GardenShotPath));
            }
            finally
            {
                if (lamp != null) Object.DestroyImmediate(lamp);
            }
        }

        /// <summary>場面の目を写したカメラで撮り、中の 320×180 を path に置く</summary>
        static string Shoot(Vector3 at, Quaternion turn, string path)
        {
            var main = TitleShots.Main();
            if (main == null) return "カメラが無い";
            GameObject go = null;
            Texture2D shot = null, small = null;
            try
            {
                go = new GameObject("ReunionShotEye") { hideFlags = HideFlags.HideAndDontSave };
                var cam = go.AddComponent<Camera>();
                cam.enabled = false;
                cam.CopyFrom(main);
                UnityEngine.Rendering.Universal.CameraExtensions.GetUniversalAdditionalCameraData(cam).renderPostProcessing = true;
                go.transform.SetPositionAndRotation(at, turn);
                shot = TitleShots.Steady(cam);
                int loose;
                small = TitleShots.Shrink(shot, out loose);
                CheckDiveSky.Save(small, path);
                TitleShots.Import(path);
                return string.Format("{0}（320×180。3×3 が揃っていない塊 {1}）", path, loose);
            }
            finally
            {
                if (small != null) Object.DestroyImmediate(small);
                if (shot != null) Object.DestroyImmediate(shot);
                if (go != null) Object.DestroyImmediate(go);
            }
        }
    }
}
