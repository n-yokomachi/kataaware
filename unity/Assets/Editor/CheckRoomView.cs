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
    /// 窓の外の景色（<see cref="BuildRoomView"/>）の確かめの撮影。再生せずに、場面を開いてプローブのカメラで撮る。
    ///
    /// 目は主人公の目のカメラと同じ組み方で出す（体の位置 + 体の向き × (0, 目の高さ, 目の前へのずれ)、向きは体の向き × (上下, 首)）。
    /// 場面の物には触らない（主人公も動かさない）ので、場面は汚れない。プローブのカメラは主人公の目のカメラを写した
    /// HideAndDontSave・enabled=false のカメラで、後処理を通して 960×540 に撮る（パイプラインの粗さで中は 320×180）。
    /// 撮り終えたら撮る前の場面へ戻す
    /// </summary>
    public static class CheckRoomView
    {
        /// <summary>撮る所。foot は体の位置、body は体の向き、head は首（座っているときだけ）、pitch は下が正、fov は 0 なら目のカメラのまま</summary>
        struct View
        {
            public string Name;
            public Vector3 Foot;
            public float Body;
            public float Head;
            public float Pitch;
            public float Eye;
            public float Fov;
            /// <summary>目の置き場をそのまま渡す（窓のガラスの面から遠くを拡大するときだけ）</summary>
            public bool Raw;

            public View(string name, Vector3 foot, float body, float head, float pitch, float eye, float fov = 0f, bool raw = false)
            {
                Name = name;
                Foot = foot;
                Body = body;
                Head = head;
                Pitch = pitch;
                Eye = eye;
                Fov = fov;
                Raw = raw;
            }
        }

        const float Lead = 0.22f;
        const float Stand = PlayerController.StandingEyeHeight;

        [MenuItem("HalfAware/Shoot the room view", false, 213)]
        public static void Menu()
        {
            Debug.Log(ShootAll(Path.Combine(Path.GetTempPath(), "HalfAwareRoomView")));
        }

        /// <summary>場面 1（夕暮れ）と場面 3（夜）の窓を撮って dir に置く</summary>
        public static string ShootAll(string dir)
        {
            if (EditorApplication.isPlaying) return "再生中は撮らない";
            for (var i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty)
                    return "開いている場面に未保存の変更がある: " + SceneManager.GetSceneAt(i).path;
            Directory.CreateDirectory(dir);
            var setup = EditorSceneManager.GetSceneManagerSetup();
            var sb = new StringBuilder();
            var async = ShaderUtil.allowAsyncCompilation;
            try
            {
                ShaderUtil.allowAsyncCompilation = false;
                var room = EditorSceneManager.OpenScene(BuildRoomView.RoomPath, OpenSceneMode.Single);
                sb.AppendLine("場面 1 の時刻: " + BuildRoomView.HourIn(room));
                var seat = SeatEye();
                var views = new[]
                {
                    new View("1_seat_left", new Vector3(1.5f, 0.05f, 1.2f), 0f, -60f, 0f, seat),
                    new View("2a_east_window", new Vector3(2.0f, 0.05f, -0.5f), 90f, 0f, -2f, Stand),
                    new View("2b_east_window_ne", new Vector3(2.45f, 0.05f, -0.85f), 50f, 0f, 0f, Stand),
                    new View("2c_north_window", new Vector3(-1.3f, 0.05f, 2.0f), 0f, 0f, -2f, Stand),
                    new View("2d_north_window_nw", new Vector3(-1.05f, 0.05f, 2.45f), -40f, 0f, 0f, Stand),
                    new View("3a_down_east", new Vector3(2.58f, 0.05f, -0.5f), 90f, 0f, 40f, Stand),
                    new View("3b_down_north", new Vector3(-1.3f, 0.05f, 2.58f), 0f, 0f, 40f, Stand),
                    new View("4a_neon_zoom", new Vector3(2.82f, 1.65f, -0.8f), 45f, 0f, -3f, 0f, 20f, true),
                    new View("4b_neon_zoom_east", new Vector3(2.82f, 1.65f, -0.72f), 100f, 0f, -3f, 0f, 20f, true),
                };
                foreach (var v in views) sb.AppendLine(Shoot(v, dir));

                var connect = EditorSceneManager.OpenScene(BuildConnect.ScenePath, OpenSceneMode.Single);
                sb.AppendLine("場面 3 の時刻: " + BuildRoomView.HourIn(connect));
                var night = new[]
                {
                    new View("5a_connect_east_window", new Vector3(2.0f, 0.05f, -0.5f), 90f, 0f, -2f, Stand),
                    new View("5b_connect_east_window_ne", new Vector3(2.45f, 0.05f, -0.85f), 50f, 0f, 0f, Stand),
                    new View("5c_connect_north_window", new Vector3(-1.3f, 0.05f, 2.0f), 0f, 0f, -2f, Stand),
                    new View("5d_connect_down_east", new Vector3(2.58f, 0.05f, -0.5f), 90f, 0f, 40f, Stand),
                };
                foreach (var v in night) sb.AppendLine(Shoot(v, dir));
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

        /// <summary>場面 1 の座った目の高さ（SceneFlow の seatEyeHeight）</summary>
        static float SeatEye()
        {
            var flow = Object.FindFirstObjectByType<SceneFlow>(FindObjectsInactive.Include);
            return flow != null ? new SerializedObject(flow).FindProperty("seatEyeHeight").floatValue : 1.26f;
        }

        static string Shoot(View v, string dir)
        {
            var main = TitleShots.Main();
            if (main == null) return v.Name + ": カメラが無い";
            var go = new GameObject("RoomViewProbe");
            go.hideFlags = HideFlags.HideAndDontSave;
            Texture2D shot = null;
            try
            {
                var cam = go.AddComponent<Camera>();
                cam.enabled = false;
                cam.CopyFrom(main);
                cam.enabled = false;
                cam.GetUniversalAdditionalCameraData().renderPostProcessing = true;
                if (v.Fov > 0f) cam.fieldOfView = v.Fov;
                var body = Quaternion.Euler(0f, v.Body, 0f);
                var eye = v.Raw ? v.Foot : v.Foot + body * new Vector3(0f, v.Eye, Lead);
                go.transform.SetPositionAndRotation(eye, v.Raw ? Quaternion.Euler(v.Pitch, v.Body, 0f) : body * Quaternion.Euler(v.Pitch, v.Head, 0f));
                shot = TitleShots.Steady(cam);
                var path = Path.Combine(dir, v.Name + ".png");
                CheckDiveSky.Save(shot, path);
                return string.Format("{0}: 目 {1}、向き {2:0}（首 {3:0}）、下へ {4:0} → {5}", v.Name, eye.ToString("F2"), v.Body + v.Head, v.Head, v.Pitch, path);
            }
            finally
            {
                if (shot != null) Object.DestroyImmediate(shot);
                Object.DestroyImmediate(go);
            }
        }

        /// <summary>
        /// タイトルの画面の部屋の背景を撮り直し（Assets/Textures/Title/room_1.png）、題と枠を重ねた画面を dir に撮る
        /// </summary>
        public static string ShootTitle(string dir)
        {
            var sb = new StringBuilder();
            sb.AppendLine(TitleShots.Shoot(TitleBackdrop.Room));
            var picture = BuildTitle.PicturePath(TitleBackdrop.Room);
            Directory.CreateDirectory(dir);
            File.Copy(picture, Path.Combine(dir, "6a_title_backdrop_320.png"), true);
            sb.AppendLine(TitleShots.Screen(Path.Combine(dir, "6b_title_screen.png"), false, TitleShots.Sample(1), false));
            return sb.ToString().TrimEnd();
        }
    }
}
