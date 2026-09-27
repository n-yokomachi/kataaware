using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HalfAware.EditorTools
{
    /// <summary>
    /// 場面 6（庭の記憶）を、再生せずに時計を進めて撮る。場面 4 の <c>CheckDivePeople.Walkthrough</c> に倣う。
    ///
    /// 村を開き、<see cref="GardenMemoryDirector"/> を場面 6 として起こし（<see cref="GardenMemoryDirector.Begin"/>・<see cref="GardenMemoryDirector.Open"/>）、
    /// 演出の一歩（<see cref="GardenMemoryDirector.Step"/>）と人の動き（<see cref="PersonMotion.Step"/>・<see cref="PersonMotion.Late"/>）と筒（<see cref="GardenHose.Refresh"/>）を
    /// 再生中と同じ順に回す。目を人へ向ける動き（<see cref="PlayerController.Follow"/>）は再生中にしか回らないので、撮る所では目を直に向ける。
    /// 撮るのは HUD を重ねたゲームの見え方（<see cref="ConsoleShot.Shoot(string,int,int,float,bool,float,bool,ConsoleShot.Stage)"/>、960×540、中 320×180）。
    ///
    /// **場面を書き換えるので、撮り終えたら村を開き直して捨てる。** 開いているシーンに未保存の変更があれば撮らない
    /// </summary>
    public static class CheckGarden
    {
        public const string ScenePath = "Assets/Scenes/Village.unity";

        /// <summary>一歩の秒。再生中の 60 こまに近く</summary>
        const float Dt = 1f / 30f;

        /// <summary>流れを頭から回して、決めた所で撮る。dir に PNG を書く。結果の文を返す</summary>
        public static string Shoot(string dir)
        {
            for (var i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) return "未保存の変更がある: " + SceneManager.GetSceneAt(i).path;
            var sb = new StringBuilder();
            var loose = CheckVillage.LooseRenderTextures();
            sb.AppendLine("RenderTexture（どのアセットにも属さない）: 始め " + loose);
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            // エディタは shader の型をまだ作っていない間、仮の水色で描く。撮る絵に水色の人が出るので、撮る間はその場で作らせる
            var async = ShaderUtil.allowAsyncCompilation;
            ShaderUtil.allowAsyncCompilation = false;
            try
            {
                var run = new Run();
                if (!run.Ready(sb)) return sb.ToString();
                // 1. 頭。名を呼ばれ、目が女性の顔へ向いた所（明けて 1 秒回した後、口が動いている）
                run.Until(() => run.D.Clock >= 1.75f, false);
                run.Look(run.Face());
                sb.AppendLine(run.Note("1 頭") + " → " + run.Shot(dir + "/g6_1_call.png"));
                // 2. 送って、乗り出して戻り、女性が向き直って水を撒いている所で目を留めると板が出る
                run.Step(true);
                run.Until(() => run.D.Current == GardenMemoryDirector.Beat.Garden && run.D.Garden >= 7f, false);
                run.Until(() => run.D.Showing || run.D.Garden >= 9f, true);
                sb.AppendLine(run.Note("2 板") + " → " + run.Shot(dir + "/g6_2_board.png"));
                // 3. 下を向く（首は体の向きのまま、下は向けられる限り）。膝の先に裾から出た足首とサンダル
                run.Until(() => run.D.Garden >= 12f, false);
                run.Hold(0f, PlayerController.PitchDownLimit, 0.3f);
                sb.AppendLine(run.Note("3 下") + " → " + run.Shot(dir + "/g6_3_down.png"));
                // 3b. 庭を見回す（女性と花の縁とトンネルの口）
                run.Hold(-10f, 2f, 0.3f);
                sb.AppendLine(run.Note("3b 庭") + " → " + run.Shot(dir + "/g6_3b_garden.png"));
                // 4. 近づいて、途切れる直前
                // 途切れる隔たり（演出の cutDistance）の少し手前
                var cutAt = new SerializedObject(run.D).FindProperty("cutDistance").floatValue;
                run.Until(() => run.D.Garden >= BuildVillage.WalkAt && run.D.Apart <= cutAt + 0.12f || run.D.Current == GardenMemoryDirector.Beat.Cut, true);
                sb.AppendLine(run.Note("4 途切れる直前") + " → " + run.Shot(dir + "/g6_4_near.png"));
                run.Until(() => run.D.Current == GardenMemoryDirector.Beat.Cut || run.D.Garden >= 75f, true);
                sb.AppendLine(run.Note("5 途切れ"));
                sb.AppendLine(run.Timeline());
            }
            catch (System.Exception e)
            {
                sb.AppendLine("例外: " + e);
            }
            finally
            {
                ShaderUtil.allowAsyncCompilation = async;
                GardenHandoff.Clear();
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                sb.AppendLine("RenderTexture（どのアセットにも属さない）: 終わり " + CheckVillage.LooseRenderTextures());
            }
            return sb.ToString();
        }

        /// <summary>
        /// 女性の服を、夕方の庭で正面・横・後ろ・斜め後ろから撮る（ゲームと同じ粗さ）。水を撒く所に立たせ、ホースを持った形のまま。
        /// 逆光の影（演出が色に掛ける暗さ）は掛けない。far は目からの隔たり（m）
        /// </summary>
        public static string Woman(string dir, float far)
        {
            for (var i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) return "未保存の変更がある: " + SceneManager.GetSceneAt(i).path;
            var sb = new StringBuilder();
            sb.AppendLine("RenderTexture（どのアセットにも属さない）: 始め " + CheckVillage.LooseRenderTextures());
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var async = ShaderUtil.allowAsyncCompilation;
            ShaderUtil.allowAsyncCompilation = false;
            try
            {
                var d = Object.FindFirstObjectByType<GardenMemoryDirector>(FindObjectsInactive.Include);
                var so = new SerializedObject(d);
                var memory = (GameObject)so.FindProperty("memory").objectReferenceValue;
                var motion = (PersonMotion)so.FindProperty("womanMotion").objectReferenceValue;
                var hose = (GardenHose)so.FindProperty("hose").objectReferenceValue;
                BuildVillage.SetHour(VillageHour.Hour.Evening);
                memory.SetActive(true);
                motion.Still();
                if (hose != null) hose.Refresh();
                // 水を撒いている形で撮る（粒はエディタでは流れないので、流れた後の形にする）
                var spray = (ParticleSystem)so.FindProperty("spray").objectReferenceValue;
                if (spray != null) spray.Simulate(0.9f, true, true);
                var root = motion.transform;
                var an = motion.GetComponentInChildren<Animator>();
                var chest = an.GetBoneTransform(HumanBodyBones.UpperChest) != null ? an.GetBoneTransform(HumanBodyBones.UpperChest).position : root.position + Vector3.up * 1.2f;
                var aim = new Vector3(root.position.x, chest.y - 0.25f, root.position.z);
                foreach (var v in new[] { new { n = "front", a = 0f }, new { n = "side", a = 90f }, new { n = "back", a = 180f }, new { n = "back_side", a = 225f } })
                {
                    var dirv = Quaternion.Euler(0f, root.eulerAngles.y + v.a, 0f) * Vector3.forward;
                    var eye = aim + dirv * far + Vector3.up * 0.25f;
                    var yaw = Mathf.Atan2(-dirv.x, -dirv.z) * Mathf.Rad2Deg;
                    var look = aim - eye;
                    var pitch = -Mathf.Atan2(look.y, new Vector2(look.x, look.z).magnitude) * Mathf.Rad2Deg;
                    sb.AppendLine(CheckVillage.Game(new CheckVillage.View("woman_" + v.n, eye, yaw, pitch), dir + "/woman_" + v.n + ".png"));
                }
            }
            catch (System.Exception e)
            {
                sb.AppendLine("例外: " + e);
            }
            finally
            {
                ShaderUtil.allowAsyncCompilation = async;
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                sb.AppendLine("RenderTexture（どのアセットにも属さない）: 終わり " + CheckVillage.LooseRenderTextures());
            }
            return sb.ToString();
        }

        /// <summary>
        /// 服の突き抜けを見る。服の面の組を、スカート（UV の下半分）を赤、シャツの胴と裾を緑、袖を青の一色で塗り分け、
        /// 粗くしない（render scale 1）で近くから撮る。シャツの緑の中に赤が出たら、スカートが裾から突き抜けている。
        /// 塗り替えは撮る間だけで、場面は開き直して捨てる
        /// </summary>
        public static string Seams(string dir, bool twoSided = false)
        {
            for (var i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) return "未保存の変更がある: " + SceneManager.GetSceneAt(i).path;
            var sb = new StringBuilder();
            var urp = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline as UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset;
            var scale = urp != null ? urp.renderScale : 1f;
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var async = ShaderUtil.allowAsyncCompilation;
            ShaderUtil.allowAsyncCompilation = false;
            Texture2D tex = null;
            Material mat = null;
            try
            {
                if (urp != null) urp.renderScale = 1f;
                var d = Object.FindFirstObjectByType<GardenMemoryDirector>(FindObjectsInactive.Include);
                var so = new SerializedObject(d);
                var memory = (GameObject)so.FindProperty("memory").objectReferenceValue;
                var motion = (PersonMotion)so.FindProperty("womanMotion").objectReferenceValue;
                BuildVillage.SetHour(VillageHour.Hour.Evening);
                memory.SetActive(true);
                motion.Still();
                SkinnedMeshRenderer smr = null;
                foreach (var s in motion.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                    if (s.sharedMesh != null && s.sharedMesh.subMeshCount > 3) smr = s;
                tex = new Texture2D(64, 64, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, hideFlags = HideFlags.HideAndDontSave };
                var px = new Color[64 * 64];
                for (var y = 0; y < 64; y++)
                    for (var x = 0; x < 64; x++) px[y * 64 + x] = y < 32 ? Color.red : x < 32 ? Color.green : Color.blue;
                tex.SetPixels(px);
                tex.Apply();
                mat = new Material(Shader.Find("Universal Render Pipeline/Lit")) { hideFlags = HideFlags.HideAndDontSave };
                mat.SetTexture("_BaseMap", tex);
                if (twoSided) mat.SetFloat("_Cull", 0f);
                var mats = smr.sharedMaterials;
                mats[mats.Length - 1] = mat;
                smr.sharedMaterials = mats;
                var root = motion.transform;
                var aim = root.position + Vector3.up * 0.98f;
                foreach (var a in new[] { 150f, 180f, 210f, 240f, 90f, 0f })
                {
                    var dirv = Quaternion.Euler(0f, root.eulerAngles.y + a, 0f) * Vector3.forward;
                    var eye = aim + dirv * 0.8f;
                    var yaw = Mathf.Atan2(-dirv.x, -dirv.z) * Mathf.Rad2Deg;
                    sb.AppendLine(CheckVillage.Game(new CheckVillage.View("seam_" + a, eye, yaw, 0f), dir + "/seam_" + a + ".png"));
                }
            }
            catch (System.Exception e)
            {
                sb.AppendLine("例外: " + e);
            }
            finally
            {
                if (urp != null) urp.renderScale = scale;
                ShaderUtil.allowAsyncCompilation = async;
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                if (tex != null) Object.DestroyImmediate(tex);
                if (mat != null) Object.DestroyImmediate(mat);
            }
            return sb.ToString();
        }

        /// <summary>
        /// スカートがシャツの裾から突き抜けていないかを数で見る。女性を立ちの動きの頭の一こまに置いて皮を焼き、
        /// スカートの三角の重心と辺の中点ごとに、腰の縦の軸から外へ向けた線でシャツ（胴と裾）の面までの半径を測り、それより外にある点を数える
        /// </summary>
        public static string Pokes()
        {
            for (var i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) return "未保存の変更がある: " + SceneManager.GetSceneAt(i).path;
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Mesh baked = null, shell = null;
            GameObject probe = null;
            try
            {
                var d = Object.FindFirstObjectByType<GardenMemoryDirector>(FindObjectsInactive.Include);
                var so = new SerializedObject(d);
                var memory = (GameObject)so.FindProperty("memory").objectReferenceValue;
                var motion = (PersonMotion)so.FindProperty("womanMotion").objectReferenceValue;
                memory.SetActive(true);
                motion.Still();
                SkinnedMeshRenderer smr = null;
                foreach (var s in motion.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                    if (s.sharedMesh != null && s.sharedMesh.subMeshCount > 3) smr = s;
                baked = new Mesh();
                smr.BakeMesh(baked, true);
                var m = smr.transform.localToWorldMatrix;
                var v = baked.vertices;
                var uv = smr.sharedMesh.uv;
                var tris = smr.sharedMesh.GetTriangles(smr.sharedMesh.subMeshCount - 1);
                var front = tris.Length / 2;
                var w = new Vector3[v.Length];
                for (var i = 0; i < v.Length; i++) w[i] = m.MultiplyPoint3x4(v[i]);
                var sv = new List<Vector3>();
                var st = new List<int>();
                var skirt = new List<int>();
                for (var t = 0; t < front; t += 3)
                {
                    bool shirt = true, sk = true;
                    for (var e = 0; e < 3; e++)
                    {
                        var u = uv[tris[t + e]];
                        if (!(u.y >= 0.5f && u.x < 0.46f)) shirt = false;
                        if (!(u.y < 0.5f)) sk = false;
                    }
                    if (shirt)
                    {
                        for (var e = 0; e < 3; e++) sv.Add(w[tris[t + e]]);
                        var n = sv.Count;
                        st.AddRange(new[] { n - 3, n - 2, n - 1, n - 3, n - 1, n - 2 });
                    }
                    if (sk) skirt.Add(t);
                }
                shell = new Mesh { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
                shell.SetVertices(sv);
                shell.SetTriangles(st, 0);
                probe = new GameObject("CheckGardenShirt") { hideFlags = HideFlags.HideAndDontSave };
                var col = probe.AddComponent<MeshCollider>();
                col.sharedMesh = shell;
                Physics.SyncTransforms();
                var pel = motion.GetComponentInChildren<Animator>().GetBoneTransform(HumanBodyBones.Hips).position;
                float lo = float.MaxValue, hi = float.MinValue;
                foreach (var p in sv) { lo = Mathf.Min(lo, p.y); hi = Mathf.Max(hi, p.y); }
                int tested = 0, pokes = 0;
                var worst = 0f;
                foreach (var t in skirt)
                {
                    Vector3 a = w[tris[t]], b = w[tris[t + 1]], c = w[tris[t + 2]];
                    foreach (var p in new[] { (a + b + c) / 3f, (a + b) * 0.5f, (b + c) * 0.5f, (a + c) * 0.5f })
                    {
                        if (p.y < lo + 0.003f || p.y > pel.y + 0.12f) continue;
                        var axis = new Vector3(pel.x, p.y, pel.z);
                        var dir = p - axis;
                        var dist = dir.magnitude;
                        if (dist < 0.02f) continue;
                        dir /= dist;
                        tested++;
                        RaycastHit hit;
                        var r = col.Raycast(new Ray(axis + dir * 0.6f, -dir), out hit, 0.6f) ? 0.6f - hit.distance : -1f;
                        if (r >= dist) continue;
                        pokes++;
                        worst = Mathf.Max(worst, dist - r);
                    }
                }
                return string.Format("スカートの点 {0}（シャツの裾の高さ {1:0.000}〜）のうち、シャツの面より外 {2}、いちばん外 {3:0.0} mm", tested, lo, pokes, worst * 1000f);
            }
            finally
            {
                if (probe != null) Object.DestroyImmediate(probe);
                if (shell != null) Object.DestroyImmediate(shell);
                if (baked != null) Object.DestroyImmediate(baked);
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            }
        }

        /// <summary>
        /// 主（片割れ）の座った体を撮る。主の目から下を向いた所（首は正面と左へ 25 度、下は向けられる限り）と、脚を左の横（卓の側）から見た所
        /// </summary>
        public static string Host(string dir)
        {
            for (var i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) return "未保存の変更がある: " + SceneManager.GetSceneAt(i).path;
            var sb = new StringBuilder();
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var async = ShaderUtil.allowAsyncCompilation;
            ShaderUtil.allowAsyncCompilation = false;
            try
            {
                var d = Object.FindFirstObjectByType<GardenMemoryDirector>(FindObjectsInactive.Include);
                var so = new SerializedObject(d);
                var memory = (GameObject)so.FindProperty("memory").objectReferenceValue;
                BuildVillage.SetHour(VillageHour.Hour.Evening);
                memory.SetActive(true);
                var seat = (Transform)so.FindProperty("seat").objectReferenceValue;
                var eye = seat.position + Vector3.up * so.FindProperty("seatEyeHeight").floatValue + seat.forward * 0.22f;
                foreach (var head in new[] { 0f, -25f })
                    sb.AppendLine(CheckVillage.Game(new CheckVillage.View("host_down_" + head, eye, seat.eulerAngles.y + head, PlayerController.PitchDownLimit), dir + "/host_down_" + head + ".png"));
                var an = memory.transform.Find("Host").GetComponent<Animator>();
                var knee = an.GetBoneTransform(HumanBodyBones.LeftLowerLeg).position;
                var foot = an.GetBoneTransform(HumanBodyBones.LeftFoot).position;
                var mid = (knee + foot) * 0.5f;
                var side = Quaternion.Euler(0f, seat.eulerAngles.y - 90f, 0f) * Vector3.forward;
                var from = mid + side * 1.2f + Vector3.up * 0.15f;
                var look = mid - from;
                sb.AppendLine(CheckVillage.Game(new CheckVillage.View("host_legs", from, Mathf.Atan2(look.x, look.z) * Mathf.Rad2Deg, 6f), dir + "/host_legs.png"));
                var other = an.GetBoneTransform(HumanBodyBones.RightFoot).position;
                sb.AppendLine("足: 左 " + foot.ToString("F3") + "、目から前へ " + Vector3.Dot(foot - eye, seat.forward).ToString("F2") + " m・下へ " + (eye.y - foot.y).ToString("F2") + " m。右 " + other.ToString("F3") + "（足首の骨の高さはテラスから左 " + (foot.y - seat.position.y).ToString("F3") + "・右 " + (other.y - seat.position.y).ToString("F3") + " m）");
            }
            catch (System.Exception e)
            {
                sb.AppendLine("例外: " + e);
            }
            finally
            {
                ShaderUtil.allowAsyncCompilation = async;
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            }
            return sb.ToString();
        }

        /// <summary>場面 6 を回す一式</summary>
        sealed class Run
        {
            public GardenMemoryDirector D;
            PlayerController player;
            PersonMotion motion;
            GardenHose hose;
            ParticleSystem spray;
            HoloPanel panel;
            Transform face;
            float played;
            readonly List<string> marks = new List<string>();
            GardenMemoryDirector.Beat last;
            bool dropped;

            public bool Ready(StringBuilder sb)
            {
                D = Object.FindFirstObjectByType<GardenMemoryDirector>(FindObjectsInactive.Include);
                if (D == null) { sb.AppendLine("GardenMemoryDirector が無い"); return false; }
                var so = new SerializedObject(D);
                player = (PlayerController)so.FindProperty("player").objectReferenceValue;
                motion = (PersonMotion)so.FindProperty("womanMotion").objectReferenceValue;
                hose = (GardenHose)so.FindProperty("hose").objectReferenceValue;
                spray = (ParticleSystem)so.FindProperty("spray").objectReferenceValue;
                panel = (HoloPanel)so.FindProperty("panel").objectReferenceValue;
                GardenHandoff.Pending = true;
                if (!GardenHandoff.Take()) return false;
                D.Begin();
                D.Open();
                var an = motion != null ? motion.GetComponentInChildren<Animator>() : null;
                face = an != null ? an.GetBoneTransform(HumanBodyBones.Head) : null;
                if (motion != null) { motion.Restart(); motion.Late(); }
                if (hose != null) hose.Refresh();
                last = D.Current;
                return true;
            }

            /// <summary>一歩。再生中の順（演出 → 人の動き → 筒 → 口）</summary>
            public void Step(bool press)
            {
                D.Step(Dt, press);
                played += Dt;
                if (motion != null) { motion.Step(Dt); motion.Late(); }
                if (hose != null) hose.Refresh();
                D.Mouth();
                Physics.SyncTransforms();
                if (D.Current != last) { marks.Add(string.Format("{0:0.00} 秒（庭 {1:0.00}）: {2}", D.Clock, D.Garden, D.Current)); last = D.Current; }
                if (hose != null && hose.Dropped && !dropped) { dropped = true; marks.Add(string.Format("{0:0.00} 秒（庭 {1:0.00}）: ホースを置いて歩き出す", D.Clock, D.Garden)); }
            }

            /// <summary>条件が立つまで回す。watch なら女性の顔へ目を向けたまま（板が出るように）</summary>
            public void Until(System.Func<bool> done, bool watch)
            {
                for (var n = 0; n < 3000 && !done(); n++)
                {
                    if (watch) Look(Face());
                    Step(false);
                }
            }

            public Vector3 Face()
            {
                return face != null ? face.position + Vector3.up * 0.07f : D.transform.position;
            }

            /// <summary>目を点へ向ける。首は体の向きからの差、上下は向けられる範囲に収める</summary>
            public void Look(Vector3 at)
            {
                var eye = player.Eye.position;
                var d = at - eye;
                var yaw = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
                var pitch = -Mathf.Atan2(d.y, new Vector2(d.x, d.z).magnitude) * Mathf.Rad2Deg;
                LookAt(Mathf.DeltaAngle(player.transform.eulerAngles.y, yaw), pitch);
            }

            /// <summary>その向きのまま seconds 秒回す（板と案内をその向きの形にする）</summary>
            public void Hold(float head, float pitch, float seconds)
            {
                for (var t = 0f; t < seconds; t += Dt)
                {
                    LookAt(head, pitch);
                    Step(false);
                }
                LookAt(head, pitch);
            }

            /// <summary>首の向き（体から、度）と上下（下が正、度）</summary>
            public void LookAt(float head, float pitch)
            {
                var offset = player.EyeOffset;
                player.PlaceAt(player.transform.position, player.transform.eulerAngles.y, player.HeadYawLimit, head, pitch, player.EyeHeight);
                player.EyeOffset = offset;
                var eye = player.Eye;
                var lead = new SerializedObject(player).FindProperty("eyeLead").floatValue;
                eye.localPosition = new Vector3(0f, player.EyeHeight, lead) + offset;
                eye.localRotation = Quaternion.Euler(PlayerController.ClampPitch(pitch), player.HeadYaw, 0f);
                Physics.SyncTransforms();
            }

            public string Shot(string path)
            {
                // 水の粒は再生中にしか流れないので、撮る前に流れた形にしておく
                if (spray != null)
                {
                    var on = D.Spraying;
                    spray.Simulate(0.9f, true, true);
                    if (!on) spray.Clear(true);
                }
                // 板は LateUpdate で置き直すので、撮る前に今の目の所へ置く（撮る時と同じ 16:9 で測る）
                if (panel != null && panel.Showing)
                {
                    var cam = player.Eye.GetComponent<Camera>();
                    cam.aspect = 960f / 540f;
                    panel.PlaceNow();
                    cam.ResetAspect();
                }
                return ConsoleShot.Shoot(path, 960, 540, UiLens.Scale, false, 0f, false, null);
            }

            public string Note(string what)
            {
                return string.Format("{0}: 記憶 {1:0.00} 秒・庭 {2:0.00} 秒・段 {3}・女性の顔まで {4:0.00} m・影 {5:0.00}・板 {6}",
                    what, D.Clock, D.Garden, D.Current, D.Apart, D.Shade, D.Showing ? "出" : "無し");
            }

            public string Timeline()
            {
                return "段の移り:\n  " + string.Join("\n  ", marks.ToArray());
            }
        }
    }
}
