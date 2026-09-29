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
    /// 部屋の形（<see cref="BuildRoomShell"/>）の確かめの撮影。再生せずに、場面を開いてプローブのカメラで撮る。
    ///
    /// 撮るのは、上から見下ろした間取り（天井と天井の明かりを伏せる）、玄関から廊下の奥、廊下の出口から LDK、居間の窓から外、
    /// 廊下の脇のドア、場面 3 の始まり（Connect、夜）。体は伏せる（座った形で保存してあり、立った目の絵に座った体が写る）。
    /// プローブのカメラは主人公の目のカメラを写した HideAndDontSave・enabled=false のカメラで、同じ呼び出しの中で捨てる。
    /// 場面の物（天井・体）に手を入れるので、**撮り終えたら撮る前の場面へ戻し、手を入れた場面は捨てる**
    /// </summary>
    public static class CheckRoomShell
    {
        /// <summary>撮る所。eye は目の位置、yaw は向き（北が 0、東回り）、pitch は俯き（下が正）。size が正なら真上からの正射影で、その半分の高さ</summary>
        struct View
        {
            public string Name;
            public Vector3 Eye;
            public float Yaw;
            public float Pitch;
            public float Size;

            public View(string name, Vector3 eye, float yaw, float pitch, float size = 0f)
            {
                Name = name;
                Eye = eye;
                Yaw = yaw;
                Pitch = pitch;
                Size = size;
            }
        }

        const float Lead = 0.22f;

        static float Stand { get { return PlayerController.StandingEyeHeight; } }

        /// <summary>立った目。体の位置（x, z）と向きから、目の前へのずれを足す</summary>
        static Vector3 EyeAt(Vector2 foot, float yaw)
        {
            return new Vector3(foot.x, 0.05f + Stand, foot.y) + Quaternion.Euler(0f, yaw, 0f) * new Vector3(0f, 0f, Lead);
        }

        static View[] RoomViews()
        {
            var ldk = RoomPlan.Ldk;
            var hall = RoomPlan.Hall;
            var plan = Rect.MinMaxRect(ldk.xMin, hall.yMin, ldk.xMax, ldk.yMax);
            return new[]
            {
                new View("1_plan", new Vector3(plan.center.x, 20f, plan.center.y), 0f, 90f, plan.height * 0.5f + 0.4f),
                new View("2a_entrance_to_hall", EyeAt(RoomPlan.EntranceStand, 0f), 0f, 4f),
                new View("2b_hall_doors_west", EyeAt(new Vector2(0.15f, -1.9f), 200f), 200f, 6f),
                new View("2c_hall_doors_east", EyeAt(new Vector2(-0.15f, -4.4f), 25f), 25f, 6f),
                new View("2d_hall_back_to_entrance", EyeAt(new Vector2(0f, -2.2f), 180f), 180f, 6f),
                new View("3a_mouth_to_ldk", EyeAt(new Vector2(0f, -1.8f), 0f), 0f, 4f),
                new View("3b_mouth_to_living", EyeAt(new Vector2(0f, -1.3f), -55f), -55f, 6f),
                new View("3c_ldk_to_mouth", EyeAt(new Vector2(-1.2f, 1.6f), 160f), 160f, 8f),
                new View("4a_living_window", EyeAt(new Vector2(RoomPlan.WestWindow.Centre, 1.9f), 0f), 0f, -4f),
                new View("4b_living_from_desk_side", EyeAt(new Vector2(0.2f, 0.2f), -80f), -80f, 6f),
            };
        }

        [MenuItem("HalfAware/Shoot the room shell", false, 206)]
        public static void Menu()
        {
            Debug.Log(ShootAll(Path.Combine(Path.GetTempPath(), "HalfAwareRoomShell")));
        }

        /// <summary>場面 1 の部屋と、場面 3 の始まりを撮って dir に置く。撮る前の場面へ戻す</summary>
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
                EditorSceneManager.OpenScene(BuildRoomShell.RoomPath, OpenSceneMode.Single);
                ShootRoom(dir, sb);
                // 場面 3 の始まり。組み立てが置いた立ち位置と向きのまま、立った目で
                EditorSceneManager.OpenScene(BuildConnect.ScenePath, OpenSceneMode.Single);
                var player = Object.FindFirstObjectByType<PlayerController>(FindObjectsInactive.Include);
                HideBody();
                if (player != null)
                {
                    var foot = player.transform.position;
                    var yaw = player.transform.eulerAngles.y;
                    var eye = foot + Quaternion.Euler(0f, yaw, 0f) * new Vector3(0f, Stand, Lead);
                    sb.AppendLine(Shoot(new View("5a_connect_start", eye, yaw, 0f), dir));
                    sb.AppendLine(Shoot(new View("5b_connect_start_left", eye, yaw - 60f, 10f), dir));
                    sb.AppendLine(string.Format("場面 3 の始まり: 足元 {0}、向き {1:0}", foot.ToString("F2"), yaw));
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

        /// <summary>
        /// 場面を保存せずに確かめる。Room を開いて部屋の形と窓の外を組み（<see cref="BuildRoomShell.Assemble"/>・<see cref="BuildRoomView.Assemble"/>）、
        /// 部屋の所を撮って、**組んだ場面は捨てる**（撮る前の場面へ戻す）。mesh とマテリアルのアセットは焼き直されたまま残る
        /// </summary>
        public static string Preview(string dir)
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
                var room = EditorSceneManager.OpenScene(BuildRoomShell.RoomPath, OpenSceneMode.Single);
                sb.AppendLine(BuildRoomShell.Assemble(room));
                sb.AppendLine(BuildRoomView.Assemble(room));
                ShootRoom(dir, sb);
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

        /// <summary>開いている Room で部屋の所を撮る</summary>
        static void ShootRoom(string dir, StringBuilder sb)
        {
            HideBody();
            foreach (var v in RoomViews())
            {
                var roof = v.Size > 0f;
                Roof(!roof);
                sb.AppendLine(Shoot(v, dir));
                Roof(true);
            }
        }

        /// <summary>主人公の体を伏せる（撮った後は場面ごと捨てる）</summary>
        static void HideBody()
        {
            var player = Object.FindFirstObjectByType<PlayerController>(FindObjectsInactive.Include);
            if (player == null) return;
            foreach (var r in player.GetComponentsInChildren<Renderer>(true)) r.enabled = false;
        }

        /// <summary>天井と天井の明かりの器を出す・伏せる（見下ろした間取りを撮る間だけ伏せる）</summary>
        static void Roof(bool on)
        {
            foreach (var path in new[] { "Room/Ceiling", "Room/CeilingLamp", "Room/HallLamp" })
            {
                var go = GameObject.Find(path);
                var r = go != null ? go.GetComponent<Renderer>() : null;
                if (r != null) r.enabled = on;
            }
        }

        static string Shoot(View v, string dir)
        {
            var main = TitleShots.Main();
            if (main == null) return v.Name + ": カメラが無い";
            var go = new GameObject("RoomShellProbe");
            go.hideFlags = HideFlags.HideAndDontSave;
            Texture2D shot = null;
            try
            {
                var cam = go.AddComponent<Camera>();
                cam.enabled = false;
                cam.CopyFrom(main);
                cam.enabled = false;
                cam.GetUniversalAdditionalCameraData().renderPostProcessing = true;
                if (v.Size > 0f)
                {
                    cam.orthographic = true;
                    cam.orthographicSize = v.Size;
                    cam.farClipPlane = 60f;
                }
                go.transform.SetPositionAndRotation(v.Eye, Quaternion.Euler(v.Pitch, v.Yaw, 0f));
                shot = TitleShots.Steady(cam);
                var path = Path.Combine(dir, v.Name + ".png");
                CheckDiveSky.Save(shot, path);
                return string.Format("{0}: 目 {1}、向き {2:0}、下へ {3:0} → {4}", v.Name, v.Eye.ToString("F2"), v.Yaw, v.Pitch, path);
            }
            finally
            {
                if (shot != null) Object.DestroyImmediate(shot);
                Object.DestroyImmediate(go);
            }
        }
    }
}
