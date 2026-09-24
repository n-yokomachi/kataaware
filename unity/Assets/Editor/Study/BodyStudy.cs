using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace HalfAware.EditorTools.Study
{
    /// <summary>
    /// 一人称で下を向いたときに、自分の体がどう映るかを確かめる撮影台。
    ///
    /// 開いている場面の Player と Main Camera をそのまま使い、目の位置（Player から見た
    /// (0, 目の高さ, eyeLead)）に撮影用のカメラを置いて、下へ何度か向けて撮る。
    /// 撮影用のカメラは Main Camera の設定を写した物で、enabled = false・HideAndDontSave、撮り終えたら壊す。
    /// 場面には何も加えず、保存もしない（体の姿勢を撮るために骨を曲げたら、場面を開き直して捨てる）
    /// </summary>
    public static class BodyStudy
    {
        public const string OutDir = @"C:\Users\PC_User\AppData\Local\Temp\claude\D--work-kataaware\3eb6fc68-a1f9-4751-bb31-73277ee27f6d\scratchpad\body";

        /// <summary>ケーブルが体に入ったこまで、どこが入ったかも書き出すか</summary>
        public static bool Detail;

        /// <summary>下を向く角度。度。正が下向き（PlayerController.Pitch と同じ）</summary>
        public static readonly float[] Pitches = { 20f, 30f, 40f, 50f, 60f };

        /// <summary>
        /// 目の位置から下へ何度か向けて、ゲームの見え方（320×180）で撮る。
        /// 絵は OutDir へ {tag}_{角度}.png。撮った絵の名前を返す
        /// </summary>
        public static string Shoot(string tag, float eyeHeight, float eyeLead, float[] pitches)
        {
            var player = GameObject.Find("Player");
            var main = GameObject.Find("Player/Main Camera");
            if (player == null || main == null) return "Player か Main Camera が無い";
            var sb = new StringBuilder();
            var cam = MakeCamera(main.GetComponent<Camera>());
            try
            {
                foreach (var p in pitches)
                {
                    Aim(cam.transform, player.transform, eyeHeight, eyeLead, p);
                    var shot = FaceStudy.Grab(cam, FaceStudy.GameW, FaceStudy.GameH);
                    var name = string.Format("{0}_{1:00}.png", tag, p);
                    FaceStudy.Save(shot, Path.Combine(OutDir, name));
                    Object.DestroyImmediate(shot);
                    sb.Append(name).Append(' ');
                }
            }
            finally
            {
                Object.DestroyImmediate(cam.gameObject);
            }
            return sb.ToString();
        }

        /// <summary>撮影用のカメラ。Main Camera の設定と後処理の有無を写す</summary>
        public static Camera MakeCamera(Camera main)
        {
            var go = new GameObject("BodyStudyCamera");
            go.hideFlags = HideFlags.HideAndDontSave;
            var cam = go.AddComponent<Camera>();
            cam.CopyFrom(main);
            cam.enabled = false;
            cam.targetTexture = null;
            var from = main.GetUniversalAdditionalCameraData();
            var to = cam.GetUniversalAdditionalCameraData();
            to.renderPostProcessing = from.renderPostProcessing;
            to.antialiasing = from.antialiasing;
            to.renderShadows = from.renderShadows;
            to.volumeLayerMask = from.volumeLayerMask;
            to.volumeTrigger = main.transform;
            return cam;
        }

        /// <summary>PlayerController と同じ置き方。目は Player から見た (0, eyeHeight, eyeLead)、向きは下へ pitch 度</summary>
        public static void Aim(Transform cam, Transform player, float eyeHeight, float eyeLead, float pitch)
        {
            cam.position = player.TransformPoint(new Vector3(0f, eyeHeight, eyeLead));
            cam.rotation = player.rotation * Quaternion.Euler(pitch, 0f, 0f);
        }

        /// <summary>
        /// 胴（torso の骨に一番重く付いた頂点）が画面に入り始める角度を、0.5 度刻みで探す。
        /// 入り始めた頂点の、足元からの高さと体の前への出も返す（胸の頂点か腹かを見分けるため）。
        /// 80 度まで入らなければ NaN
        /// </summary>
        public static string ChestEntry(GameObject body, ICollection<Transform> torso, float eyeHeight, float eyeLead, out float entry)
        {
            entry = float.NaN;
            var player = GameObject.Find("Player");
            var main = GameObject.Find("Player/Main Camera");
            if (player == null || main == null) return "Player か Main Camera が無い";
            var points = TorsoPoints(body, torso);
            if (points.Count == 0) return "胴の頂点が無い";
            var cam = MakeCamera(main.GetComponent<Camera>());
            cam.aspect = FaceStudy.GameW / (float)FaceStudy.GameH;
            try
            {
                for (var p = 0f; p <= 80f; p += 0.5f)
                {
                    Aim(cam.transform, player.transform, eyeHeight, eyeLead, p);
                    foreach (var w in points)
                    {
                        var v = cam.WorldToViewportPoint(w);
                        if (v.z < cam.nearClipPlane || v.x < 0f || v.x > 1f || v.y < 0f || v.y > 1f) continue;
                        entry = p;
                        var local = player.transform.InverseTransformPoint(w);
                        return string.Format("{0:0.0} 度（入った頂点: 足元から {1:0.00} m、目より前へ {2:0.00} m）", p, local.y, local.z - eyeLead);
                    }
                }
                return "80 度まで入らない";
            }
            finally
            {
                Object.DestroyImmediate(cam.gameObject);
            }
        }

        /// <summary>体の面を今の姿勢で焼き、胴の骨に一番重く付いた頂点の世界の位置を集める</summary>
        public static List<Vector3> TorsoPoints(GameObject body, ICollection<Transform> torso)
        {
            var list = new List<Vector3>();
            foreach (var smr in body.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                if (!smr.enabled || !smr.gameObject.activeInHierarchy || smr.sharedMesh == null) continue;
                if (smr.shadowCastingMode == UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly) continue;
                var baked = new Mesh();
                smr.BakeMesh(baked, true);
                var weights = smr.sharedMesh.boneWeights;
                var bones = smr.bones;
                var verts = baked.vertices;
                var shown = Shown(smr);
                for (var i = 0; i < verts.Length && i < weights.Length; i++)
                {
                    if (!shown[i]) continue;
                    var w = weights[i];
                    var b = w.boneIndex0 < bones.Length ? bones[w.boneIndex0] : null;
                    if (b == null || !torso.Contains(b)) continue;
                    list.Add(smr.transform.TransformPoint(verts[i]));
                }
                Object.DestroyImmediate(baked);
            }
            return list;
        }

        /// <summary>頂点ごとに、映る面の組（隠す素材でない面の組）に入っているか</summary>
        static bool[] Shown(SkinnedMeshRenderer smr)
        {
            var mesh = smr.sharedMesh;
            var shown = new bool[mesh.vertexCount];
            var mats = smr.sharedMaterials;
            for (var s = 0; s < mesh.subMeshCount; s++)
            {
                var m = s < mats.Length ? mats[s] : null;
                if (m == null || m.shader == null || m.shader.name == "HalfAware/Hidden") continue;
                foreach (var i in mesh.GetTriangles(s)) shown[i] = true;
            }
            return shown;
        }

        /// <summary>Humanoid の体の胴の骨（背骨・胸・上の胸・首・左右の肩）</summary>
        public static HashSet<Transform> HumanTorso(Animator an)
        {
            var set = new HashSet<Transform>();
            foreach (var b in new[] { HumanBodyBones.Spine, HumanBodyBones.Chest, HumanBodyBones.UpperChest, HumanBodyBones.Neck,
                HumanBodyBones.LeftShoulder, HumanBodyBones.RightShoulder })
            {
                var t = an.GetBoneTransform(b);
                if (t != null) set.Add(t);
            }
            // 胸の骨（Rocketbox は背骨の子に左右の胸の骨を持つ）も胴に数える
            var chest = an.GetBoneTransform(HumanBodyBones.UpperChest) ?? an.GetBoneTransform(HumanBodyBones.Chest);
            if (chest != null)
                foreach (var t in chest.GetComponentsInChildren<Transform>(true))
                    if (t.name.Contains("Breast") || t.name.Contains("Pectoral")) set.Add(t);
            return set;
        }

        /// <summary>名前で探す胴の骨（Humanoid でない体）</summary>
        public static HashSet<Transform> NamedTorso(GameObject body, params string[] names)
        {
            var set = new HashSet<Transform>();
            var want = new HashSet<string>(names);
            foreach (var t in body.GetComponentsInChildren<Transform>(true))
                if (want.Contains(t.name)) set.Add(t);
            return set;
        }

        // ---- ジャックを抜く・挿す流れ ------------------------------------------------

        /// <summary>体の面（映る面の組だけ）を今の姿勢で焼いた頂点と法線（世界）</summary>
        public static void Surface(GameObject body, List<Vector3> points, List<Vector3> normals)
        {
            foreach (var smr in body.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                if (smr.shadowCastingMode == UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly) continue;
                var baked = new Mesh();
                smr.BakeMesh(baked, true);
                var v = baked.vertices;
                var n = baked.normals;
                var shown = Shown(smr);
                for (var i = 0; i < v.Length; i++)
                {
                    if (i < shown.Length && !shown[i]) continue;
                    points.Add(smr.transform.TransformPoint(v[i]));
                    normals.Add(smr.transform.TransformDirection(n[i]).normalized);
                }
                Object.DestroyImmediate(baked);
            }
        }

        /// <summary>
        /// 点の並びのうち、体の中に入っている数。いちばん近い体の頂点から見て、法線の裏（面の内側）にあり、
        /// その頂点から 4 cm 以内のものを中と数える。margin は面から外へ取るゆとり（ケーブルの太さなど）
        /// </summary>
        public static int Inside(IList<Vector3> probe, List<Vector3> points, List<Vector3> normals, float margin)
        {
            var count = 0;
            foreach (var p in probe)
            {
                var best = float.MaxValue;
                var at = -1;
                for (var i = 0; i < points.Count; i++)
                {
                    var d = (points[i] - p).sqrMagnitude;
                    if (d < best) { best = d; at = i; }
                }
                if (at < 0 || best > 0.04f * 0.04f) continue;
                if (Vector3.Dot(p - points[at], normals[at]) < margin) count++;
            }
            return count;
        }

        /// <summary>体に入った点の並びの、何番目の点がどの骨の頂点のそばで何 mm 入っているかを書き出す</summary>
        public static string InsideReport(IList<Vector3> probe, GameObject body, int perRing)
        {
            var sb = new StringBuilder();
            foreach (var smr in body.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                if (smr.shadowCastingMode == UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly) continue;
                var baked = new Mesh();
                smr.BakeMesh(baked, true);
                var v = baked.vertices;
                var n = baked.normals;
                var w = smr.sharedMesh.boneWeights;
                var shown = Shown(smr);
                for (var k = 0; k < probe.Count; k++)
                {
                    var best = float.MaxValue;
                    var at = -1;
                    for (var i = 0; i < v.Length; i++)
                    {
                        if (!shown[i]) continue;
                        var d = (smr.transform.TransformPoint(v[i]) - probe[k]).sqrMagnitude;
                        if (d < best) { best = d; at = i; }
                    }
                    if (at < 0 || best > 0.04f * 0.04f) continue;
                    var depth = Vector3.Dot(probe[k] - smr.transform.TransformPoint(v[at]), smr.transform.TransformDirection(n[at]).normalized);
                    if (depth >= 0f) continue;
                    sb.AppendFormat("  点 {0}（輪 {1}）{2} が {3} のそばで {4:0.0} mm 入る", k, k / Mathf.Max(1, perRing), probe[k].ToString("F3"),
                        smr.bones[w[at].boneIndex0].name, -depth * 1000f).AppendLine();
                }
                Object.DestroyImmediate(baked);
            }
            return sb.ToString();
        }

        /// <summary>ケーブルを今の端で張り直し、その頂点（世界）を返す。エディタでは Awake が走らないので、ここで呼ぶ</summary>
        public static List<Vector3> CablePoints(Cable cable)
        {
            var list = new List<Vector3>();
            if (cable == null) return list;
            var bf = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
            var mf = cable.GetComponent<MeshFilter>();
            if (mf.sharedMesh == null || mf.sharedMesh.name != "Cable") typeof(Cable).GetMethod("Awake", bf).Invoke(cable, null);
            typeof(Cable).GetMethod("LateUpdate", bf).Invoke(cable, null);
            foreach (var v in mf.sharedMesh.vertices) list.Add(cable.transform.TransformPoint(v));
            return list;
        }

        /// <summary>
        /// ジャック（円柱。座金の半径 11.8 mm、胴の半径 8.8 mm、長さ 30 mm）の中に入っている、左手の頂点の数。
        /// 手の頂点は、左手と左の指の骨に一番重く付いた頂点
        /// </summary>
        public static int FingersInJack(GameObject body, Transform jack, bool left)
        {
            var an = body.GetComponent<Animator>();
            var hand = new HashSet<Transform>();
            var first = left ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand;
            hand.Add(an.GetBoneTransform(first));
            for (var b = left ? HumanBodyBones.LeftThumbProximal : HumanBodyBones.RightThumbProximal;
                 b <= (left ? HumanBodyBones.LeftLittleDistal : HumanBodyBones.RightLittleDistal); b++)
            {
                var t = an.GetBoneTransform(b);
                if (t != null) hand.Add(t);
            }
            var count = 0;
            foreach (var p in TorsoPoints(body, hand))
            {
                var local = jack.InverseTransformPoint(p);
                var s = jack.lossyScale.x;
                local *= s;
                var r = new Vector2(local.x, local.y).magnitude;
                if (local.z < -0.002f || local.z > 0.030f) continue;
                var radius = local.z < 0.005f ? 0.0118f : local.z < 0.0185f ? 0.0088f : 0.005f;
                if (r < radius) count++;
            }
            return count;
        }

        /// <summary>
        /// 抜く流れ（JackPull）を t 秒ごとに止めて撮る。一人称（ゲームの見え方、視線はしぐさと同じくジャックを追う）と、
        /// 体と椅子だけを横から見た確かめの絵の二つ。こまごとに、ケーブルの頂点が体に入った数と、
        /// 左手の頂点がジャックに入った数を数える。
        /// **ジャックの親を付け替えるので、撮った後は場面を開き直して捨てること**
        /// </summary>
        public static string PullFrames(string tag, float[] times, float startPitch, float seatEye, float eyeLead, Vector3 sideAt, Vector3 sideLook, bool shoot = true)
        {
            var player = GameObject.Find("Player");
            var her = GameObject.Find("Player/Protagonist");
            var pose = her.GetComponent<SeatedPose>();
            var pull = her.GetComponent<JackPull>();
            var cable = Object.FindFirstObjectByType<Cable>();
            var chair = GameObject.Find("Room/Chair");
            var jack = (Transform)new SerializedObject(pull).FindProperty("jack").objectReferenceValue;
            pose.Bind();
            pull.Bind();
            // エディタでは一回の呼び出しの中で骨を何度動かしても、皮は最初に焼いた形のまま描かれる。撮るたびに焼き直させる
            foreach (var smr in her.GetComponentsInChildren<SkinnedMeshRenderer>()) smr.forceMatrixRecalculationPerRender = true;
            var cam = MakeCamera(GameObject.Find("Player/Main Camera").GetComponent<Camera>());
            var sb = new StringBuilder();
            var yaw0 = player.transform.eulerAngles.y;
            float aimYaw = yaw0, aimPitch = startPitch;
            try
            {
                foreach (var t in times)
                {
                    pose.Apply();
                    pull.StepForStudy(t);
                    pull.Apply(t);
                    var pts = CablePoints(cable);
                    var surf = new List<Vector3>();
                    var norms = new List<Vector3>();
                    Surface(her, surf, norms);
                    var inside = Inside(pts, surf, norms, 0f);
                    var fingers = FingersInJack(her, jack, true);
                    var eye = player.transform.TransformPoint(new Vector3(0f, seatEye, eyeLead));
                    if (PullTimeline.Follows(t))
                    {
                        var to = (jack.position - eye).normalized;
                        aimYaw = Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg;
                        aimPitch = -Mathf.Asin(Mathf.Clamp(to.y, -1f, 1f)) * Mathf.Rad2Deg;
                    }
                    var k = PullTimeline.Aim(t);
                    cam.fieldOfView = GameObject.Find("Player/Main Camera").GetComponent<Camera>().fieldOfView;
                    cam.cullingMask = -1;
                    cam.transform.position = eye;
                    cam.transform.rotation = Quaternion.Euler(Mathf.Lerp(startPitch, aimPitch, k), Mathf.LerpAngle(yaw0, aimYaw, k), 0f);
                    if (shoot)
                    {
                        var name = string.Format("{0}_{1:0.00}", tag, t);
                        var shot = FaceStudy.Grab(cam, FaceStudy.GameW, FaceStudy.GameH);
                        FaceStudy.Save(shot, Path.Combine(OutDir, name + ".png"));
                        Object.DestroyImmediate(shot);
                        Isolated(cam, new[] { her, chair }, sideAt, sideLook, 30f, Path.Combine(OutDir, name + "_side.png"));
                    }
                    var grip = (Transform)new SerializedObject(pull).FindProperty("grip").objectReferenceValue;
                    var parked = (Transform)new SerializedObject(pull).FindProperty("parked").objectReferenceValue;
                    var gap = grip != null ? Vector3.Distance(grip.position, jack.position) * 1000f : -1f;
                    var park = grip != null && parked != null ? Vector3.Distance(grip.position, parked.position) * 1000f : -1f;
                    // 手首に刺さっている間と、抜け始めて座金が皮膚から 6 mm 離れるまでは、座金（根元から 5 mm まで）が皮膚に埋まるのは数えない。
                    // 胴（根元から 6 mm より上）に皮膚が入っていれば数える
                    var pulled = new SerializedObject(pull).FindProperty("pullDistance").floatValue * PullTimeline.Lift(t);
                    var seated = !PullTimeline.Out(t) || (pulled <= 0.006f && PullTimeline.Show(t) <= 0f);
                    var touch = BodyInJack(her, jack, seated ? 0.006f : -0.002f);
                    var an = her.GetComponent<Animator>();
                    var hands = PartInPart(her, Hand(an, true, false), Hand(an, false, true));
                    sb.AppendFormat("{0:0.00} 秒: ケーブルが体に入った頂点 {1}（全 {2}）、左手がジャックに入った頂点 {3}、ジャックに入った体の頂点 {6}、左手が右の前腕と手に入った頂点 {7}、置き所とジャックの差 {4:0.0} mm、置き所と置き場の差 {5:0.0} mm",
                        t, inside, pts.Count, fingers, gap, park, touch, hands).AppendLine();
                    if (inside > 0 && Detail) sb.Append(InsideReport(pts, her, 6));
                }
            }
            finally
            {
                Object.DestroyImmediate(cam.gameObject);
            }
            return sb.ToString();
        }

        /// <summary>見せたい物だけを使っていない層へ移して撮り、層を戻す。背景は灰の地</summary>
        public static void Isolated(Camera cam, GameObject[] show, Vector3 at, Vector3 look, float fov, string path)
        {
            var layers = new Dictionary<GameObject, int>();
            var flags = cam.clearFlags;
            var bg = cam.backgroundColor;
            var mask = cam.cullingMask;
            var keepFov = cam.fieldOfView;
            try
            {
                foreach (var g in show)
                    if (g != null)
                        foreach (var t in g.GetComponentsInChildren<Transform>(true))
                        {
                            layers[t.gameObject] = t.gameObject.layer;
                            t.gameObject.layer = 31;
                        }
                cam.cullingMask = 1 << 31;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0.25f, 0.25f, 0.28f);
                cam.fieldOfView = fov;
                cam.transform.position = at;
                cam.transform.LookAt(look);
                var shot = FaceStudy.Grab(cam, FaceStudy.BigW, FaceStudy.BigH);
                FaceStudy.Save(shot, path);
                Object.DestroyImmediate(shot);
            }
            finally
            {
                foreach (var kv in layers) if (kv.Key != null) kv.Key.layer = kv.Value;
                cam.clearFlags = flags;
                cam.backgroundColor = bg;
                cam.cullingMask = mask;
                cam.fieldOfView = keepFov;
            }
        }

        /// <summary>
        /// 骨の組 from に付いた頂点のうち、骨の組 into に付いた面の内側に入っている数（左手が右の手首へ潜るのを数える）
        /// </summary>
        public static int PartInPart(GameObject body, ICollection<Transform> from, ICollection<Transform> into)
        {
            var probe = TorsoPoints(body, from);
            var pts = new List<Vector3>();
            var nrm = new List<Vector3>();
            foreach (var smr in body.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                if (smr.shadowCastingMode == UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly) continue;
                var baked = new Mesh();
                smr.BakeMesh(baked, true);
                var v = baked.vertices;
                var n = baked.normals;
                var w = smr.sharedMesh.boneWeights;
                var bones = smr.bones;
                var shown = Shown(smr);
                for (var i = 0; i < v.Length; i++)
                {
                    if (!shown[i] || !into.Contains(bones[w[i].boneIndex0])) continue;
                    pts.Add(smr.transform.TransformPoint(v[i]));
                    nrm.Add(smr.transform.TransformDirection(n[i]).normalized);
                }
                Object.DestroyImmediate(baked);
            }
            return Inside(probe, pts, nrm, -0.002f);
        }

        /// <summary>左右どちらかの手（手の骨と指の骨）</summary>
        public static HashSet<Transform> Hand(Animator an, bool left, bool withForearm)
        {
            var set = new HashSet<Transform> { an.GetBoneTransform(left ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand) };
            if (withForearm) set.Add(an.GetBoneTransform(left ? HumanBodyBones.LeftLowerArm : HumanBodyBones.RightLowerArm));
            for (var b = left ? HumanBodyBones.LeftThumbProximal : HumanBodyBones.RightThumbProximal;
                 b <= (left ? HumanBodyBones.LeftLittleDistal : HumanBodyBones.RightLittleDistal); b++)
            {
                var t = an.GetBoneTransform(b);
                if (t != null) set.Add(t);
            }
            return set;
        }

        /// <summary>ジャックの中に入っている体の頂点の数（左手と左の指の骨に付いた頂点は数えない。掴んでいる手は別に数える）</summary>
        public static int BodyInJack(GameObject body, Transform jack, float minZ = -0.002f)
        {
            var an = body.GetComponent<Animator>();
            var skip = new HashSet<Transform> { an.GetBoneTransform(HumanBodyBones.LeftHand) };
            for (var b = HumanBodyBones.LeftThumbProximal; b <= HumanBodyBones.LeftLittleDistal; b++)
            {
                var t = an.GetBoneTransform(b);
                if (t != null) skip.Add(t);
            }
            var all = new HashSet<Transform>();
            foreach (var t in body.GetComponentsInChildren<Transform>(true)) if (!skip.Contains(t)) all.Add(t);
            var count = 0;
            foreach (var p in TorsoPoints(body, all))
            {
                var local = jack.InverseTransformPoint(p) * jack.lossyScale.x;
                if (local.z < minZ || local.z > 0.030f) continue;
                var radius = local.z < 0.005f ? 0.0118f : local.z < 0.0185f ? 0.0088f : 0.005f;
                if (new Vector2(local.x, local.y).magnitude < radius) count++;
            }
            return count;
        }

        /// <summary>320×180 の絵を横に並べ、画素のまま scale 倍にした一枚を書く</summary>
        public static string Strip(string[] names, string outName, int scale)
        {
            var shots = new List<Texture2D>();
            try
            {
                foreach (var n in names)
                {
                    var p = Path.Combine(OutDir, n);
                    if (!File.Exists(p)) continue;
                    var t = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                    t.hideFlags = HideFlags.HideAndDontSave;
                    t.LoadImage(File.ReadAllBytes(p));
                    shots.Add(t);
                }
                if (shots.Count == 0) return "絵が無い";
                int w = FaceStudy.GameW * scale, h = FaceStudy.GameH * scale, gap = 4;
                var sheet = new Texture2D(shots.Count * (w + gap) - gap, h, TextureFormat.RGBA32, false);
                sheet.hideFlags = HideFlags.HideAndDontSave;
                var bg = new Color32[sheet.width * sheet.height];
                for (var i = 0; i < bg.Length; i++) bg[i] = new Color32(40, 40, 44, 255);
                sheet.SetPixels32(bg);
                for (var k = 0; k < shots.Count; k++)
                {
                    var src = shots[k].GetPixels32();
                    var dst = new Color32[w * h];
                    for (var y = 0; y < h; y++)
                        for (var x = 0; x < w; x++)
                            dst[y * w + x] = src[(y / scale) * shots[k].width + x / scale];
                    sheet.SetPixels32(k * (w + gap), 0, w, h, dst);
                }
                sheet.Apply();
                FaceStudy.Save(sheet, Path.Combine(OutDir, outName));
                Object.DestroyImmediate(sheet);
                return outName;
            }
            finally
            {
                foreach (var t in shots) Object.DestroyImmediate(t);
            }
        }
    }
}
