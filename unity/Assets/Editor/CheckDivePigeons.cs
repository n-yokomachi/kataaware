using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace HalfAware.EditorTools
{
    /// <summary>
    /// 公園の鳩（<see cref="Pigeon"/>）を、再生せずにエディタのまま動かして撮り、公園の見る所の重さを測る。
    ///
    /// **記憶の時計を自分で回す。** 鳩と人の Mover へ、DiveDirector と同じ形で秒を渡し（合図を持つ者は、その行が出た秒から数える）、
    /// 鳩の <see cref="Pigeon.Step"/> を 1/60 秒ずつ進める。主の目は一時の物（<see cref="Pigeon.Watcher"/>）で、
    /// 渡した道筋の上を歩かせる。寄られた鳩は、ゲームの中と同じく歩いて退くか短く飛ぶ。
    ///
    /// **抜けるときに全部戻す。** 鳩と人の根の置き場・向き・形（mesh）・レンダラーの有効、一時のカメラ・Volume・主の目、
    /// RenderTexture と Texture2D は同じ呼び出しの中で捨てる。記憶と場所の有効・無効と空は <see cref="CheckDiveSky.Stage"/> が戻す
    /// </summary>
    public static class CheckDivePigeons
    {
        /// <summary>主の道筋の一点。記憶の頭からの秒と、足元（場所のローカル）</summary>
        public struct Foot
        {
            public float at;
            public Vector3 foot;
            public Foot(float at, float x, float z) { this.at = at; foot = new Vector3(x, 0f, z); }
        }

        /// <summary>
        /// 記憶 <paramref name="which"/> を <paramref name="until"/> 秒まで回し、<paramref name="frames"/> の秒ごとに一枚撮る。
        /// 目は道筋のその秒の所、向きは <paramref name="yaw"/>・<paramref name="pitch"/>（上が正）。
        /// <paramref name="lines"/> と <paramref name="lineAt"/> は、何行目がいつ出たか（合図を持つ Mover がそこから数える）。
        /// 撮るのはゲームと同じ 960×540（中は 320×180）で、記憶の色味を掛ける
        /// </summary>
        public static string Run(int which, Foot[] path, float yaw, float pitch, int[] lines, float[] lineAt,
            float[] frames, string shotPrefix)
        {
            var sb = new StringBuilder();
            var take = TakeAt(which);
            if (take == null) return "記憶 " + which + " が無い";
            var roster = AssetDatabase.LoadAssetAtPath<DiveRoster>(BuildDive.RosterPath);
            var entry = roster[which];
            var main = GameObject.Find("Player/Main Camera");
            if (main == null) return "Player/Main Camera が無い";

            var movers = take.GetComponentsInChildren<Mover>(true);
            var birds = take.GetComponentsInChildren<Pigeon>(true);
            var keptAt = new Dictionary<Transform, Vector3>();
            var keptTurn = new Dictionary<Transform, Quaternion>();
            var keptMesh = new Dictionary<MeshFilter, Mesh>();
            var keptShow = new Dictionary<Renderer, bool>();
            foreach (var m in movers) { keptAt[m.transform] = m.transform.localPosition; keptTurn[m.transform] = m.transform.localRotation; }
            foreach (var b in birds)
            {
                keptAt[b.transform] = b.transform.localPosition;
                keptTurn[b.transform] = b.transform.localRotation;
                var f = b.GetComponent<MeshFilter>();
                if (f != null) keptMesh[f] = f.sharedMesh;
                var r = b.GetComponent<Renderer>();
                if (r != null) keptShow[r] = r.enabled;
            }
            GameObject eyeGo = null, volGo = null;
            UnityEngine.Rendering.VolumeProfile profile = null;
            try
            {
                using (var stage = new CheckDiveSky.Stage(entry.place, which))
                {
                    var sky = CheckDiveSky.SkyOf(entry.place);
                    sky.Apply(null);
                    var place = stage.Place;
                    eyeGo = new GameObject("CheckDivePigeonsEye");
                    eyeGo.hideFlags = HideFlags.HideAndDontSave;
                    var cam = eyeGo.AddComponent<Camera>();
                    cam.enabled = false;
                    cam.CopyFrom(main.GetComponent<Camera>());
                    cam.clearFlags = sky.skybox != null ? CameraClearFlags.Skybox : CameraClearFlags.SolidColor;
                    cam.backgroundColor = sky.flat;
                    cam.GetUniversalAdditionalCameraData().renderPostProcessing = true;
                    Pigeon.Watcher = eyeGo.transform;

                    volGo = new GameObject("CheckDivePigeonsVolume");
                    volGo.hideFlags = HideFlags.HideAndDontSave;
                    profile = ScriptableObject.CreateInstance<UnityEngine.Rendering.VolumeProfile>();
                    profile.hideFlags = HideFlags.HideAndDontSave;
                    var tone = profile.Add<ColorAdjustments>(false);
                    tone.hideFlags = HideFlags.HideAndDontSave;
                    tone.colorFilter.Override(entry.tint);
                    var volume = volGo.AddComponent<UnityEngine.Rendering.Volume>();
                    volume.isGlobal = true;
                    volume.priority = 100f;
                    volume.sharedProfile = profile;

                    // 頭から。Mover は有効になった瞬間の形へ戻っているので、鳩も頭から数え直させる
                    foreach (var b in birds) b.Cue(0f, -1f);
                    const float dt = 1f / 60f;
                    var clock = 0f;
                    var next = 0;
                    var end = frames.Length > 0 ? frames[frames.Length - 1] : 0f;
                    Place(eyeGo.transform, place, path, 0f, entry.eyeHeight, yaw, pitch);
                    while (next < frames.Length)
                    {
                        Place(eyeGo.transform, place, path, clock, entry.eyeHeight, yaw, pitch);
                        foreach (var m in movers)
                        {
                            float t, after;
                            Times(m, clock, lines, lineAt, out t, out after);
                            m.Play(t, after);
                            var motion = m.GetComponent<PersonMotion>();
                            if (motion != null) motion.Still();
                        }
                        foreach (var b in birds) b.Step(dt);
                        if (clock + 1e-4f >= frames[next])
                        {
                            Physics.SyncTransforms();
                            var file = string.Format("{0}_{1:00.00}.png", shotPrefix, frames[next]);
                            var shot = Grab(cam, 960, 540);
                            CheckDiveSky.Save(shot, file);
                            Object.DestroyImmediate(shot);
                            sb.AppendLine(string.Format("{0:F2} 秒 → {1}", frames[next], file));
                            sb.AppendLine("  " + Doing(birds, eyeGo.transform));
                            next++;
                        }
                        clock += dt;
                        if (clock > end + 1f) break;
                    }
                }
            }
            finally
            {
                Pigeon.Watcher = null;
                if (profile != null)
                {
                    foreach (var c in profile.components) if (c != null) Object.DestroyImmediate(c);
                    Object.DestroyImmediate(profile);
                }
                if (volGo != null) Object.DestroyImmediate(volGo);
                if (eyeGo != null) Object.DestroyImmediate(eyeGo);
                foreach (var kv in keptAt) if (kv.Key != null) kv.Key.localPosition = kv.Value;
                foreach (var kv in keptTurn) if (kv.Key != null) kv.Key.localRotation = kv.Value;
                foreach (var kv in keptMesh) if (kv.Key != null) kv.Key.sharedMesh = kv.Value;
                foreach (var kv in keptShow) if (kv.Key != null) kv.Key.enabled = kv.Value;
                foreach (var m in movers) { var motion = m != null ? m.GetComponent<PersonMotion>() : null; if (motion != null) motion.Still(); }
            }
            return sb.ToString();
        }

        /// <summary>DiveDirector.Drift と同じ形で、Mover へ渡す二つの秒</summary>
        static void Times(Mover m, float clock, int[] lines, float[] lineAt, out float t, out float after)
        {
            var c2 = m.NextCue < 0 ? -1f : Said(m.NextCue, lines, lineAt);
            after = c2 < 0f || clock < c2 ? -1f : clock - c2;
            if (m.Cue < 0) { t = clock; return; }
            var c = Said(m.Cue, lines, lineAt);
            t = c < 0f || clock < c ? 0f : clock - c;
        }

        /// <summary>その行数が出た秒。出ないなら負</summary>
        static float Said(int cue, int[] lines, float[] lineAt)
        {
            var best = -1f;
            for (var i = 0; i < lines.Length; i++)
                if (lines[i] >= cue && (best < 0f || lineAt[i] < best)) best = lineAt[i];
            return best;
        }

        /// <summary>主の目を道筋のその秒の所へ。道筋の点のあいだは直線で繋ぐ</summary>
        static void Place(Transform eye, Transform place, Foot[] path, float clock, float height, float yaw, float pitch)
        {
            var foot = path[0].foot;
            for (var i = 0; i + 1 < path.Length; i++)
            {
                if (clock < path[i].at) break;
                var k = Mathf.Clamp01((clock - path[i].at) / Mathf.Max(1e-3f, path[i + 1].at - path[i].at));
                foot = Vector3.Lerp(path[i].foot, path[i + 1].foot, k);
            }
            if (path.Length > 0 && clock >= path[path.Length - 1].at) foot = path[path.Length - 1].foot;
            var world = place != null ? place.TransformPoint(foot) : foot;
            var turn = place != null ? place.eulerAngles.y : 0f;
            eye.position = world + Vector3.up * height;
            eye.rotation = Quaternion.Euler(-pitch, turn + yaw, 0f);
        }

        /// <summary>鳩ごとの今。主からの隔たりと、していること</summary>
        static string Doing(Pigeon[] birds, Transform eye)
        {
            var sb = new StringBuilder();
            foreach (var b in birds)
            {
                var d = b.transform.position - eye.position;
                d.y = 0f;
                sb.AppendFormat("{0}/{1}:{2}{3}({4:F1}m) ", b.transform.parent.name, b.name, b.Doing, b.Gone ? "*" : "", d.magnitude);
            }
            return sb.ToString();
        }

        /// <summary>
        /// カメラの絵を読み出す。RenderTexture は一時の物を借りずに作って、同じ呼び出しの中で捨てる
        /// </summary>
        static Texture2D Grab(Camera cam, int w, int h)
        {
            var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            rt.hideFlags = HideFlags.HideAndDontSave;
            var keep = RenderTexture.active;
            try
            {
                cam.targetTexture = rt;
                cam.aspect = w / (float)h;
                cam.Render();
                RenderTexture.active = rt;
                var shot = new Texture2D(w, h, TextureFormat.RGBA32, false, false);
                shot.hideFlags = HideFlags.HideAndDontSave;
                shot.ReadPixels(new Rect(0, 0, w, h), 0, 0);
                shot.Apply();
                return shot;
            }
            finally
            {
                cam.targetTexture = null;
                RenderTexture.active = keep;
                rt.Release();
                Object.DestroyImmediate(rt);
            }
        }

        // ---- 重さ -------------------------------------------------------------------------

        /// <summary>
        /// 公園の一か所から見た重さ。視錐台に掛かるレンダラーの数（描く回数の目安。一つの mesh の面の組ごとに一回）・
        /// そのうち影を落とす物・三角形。鳩の分も別に数える。鳩は始まりの形（立った形）で数える
        /// </summary>
        public static string Weight(int which, Vector3 foot, float yaw, float pitch, string label)
        {
            var take = TakeAt(which);
            if (take == null) return "記憶 " + which + " が無い";
            var roster = AssetDatabase.LoadAssetAtPath<DiveRoster>(BuildDive.RosterPath);
            var entry = roster[which];
            var main = GameObject.Find("Player/Main Camera");
            GameObject eyeGo = null;
            try
            {
                using (var stage = new CheckDiveSky.Stage(entry.place, which))
                {
                    eyeGo = new GameObject("CheckDivePigeonsWeigh");
                    eyeGo.hideFlags = HideFlags.HideAndDontSave;
                    var cam = eyeGo.AddComponent<Camera>();
                    cam.enabled = false;
                    if (main != null) cam.CopyFrom(main.GetComponent<Camera>());
                    cam.aspect = 16f / 9f;
                    Place(eyeGo.transform, stage.Place, new[] { new Foot(0f, foot.x, foot.z) }, 0f, entry.eyeHeight, yaw, pitch);
                    var planes = GeometryUtility.CalculateFrustumPlanes(cam);
                    int draws = 0, casters = 0, tris = 0, birdDraws = 0, birdTris = 0;
                    foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
                    {
                        if (!r.enabled || !r.gameObject.activeInHierarchy) continue;
                        if (!GeometryUtility.TestPlanesAABB(planes, r.bounds)) continue;
                        var mf = r.GetComponent<MeshFilter>();
                        var smr = r as SkinnedMeshRenderer;
                        var mesh = smr != null ? smr.sharedMesh : mf != null ? mf.sharedMesh : null;
                        if (mesh == null) continue;
                        var n = mesh.subMeshCount;
                        var t = 0;
                        for (var s = 0; s < n; s++) t += (int)mesh.GetIndexCount(s) / 3;
                        draws += n;
                        tris += t;
                        if (r.shadowCastingMode != UnityEngine.Rendering.ShadowCastingMode.Off) casters += n;
                        if (r.GetComponent<Pigeon>() != null || r.transform.parent != null && r.transform.parent.name.EndsWith("Doves")) { birdDraws += n; birdTris += t; }
                    }
                    return string.Format("{0}: 描く {1}（影を落とす {2}）、三角 {3}。うち鳩 描く {4}・三角 {5}",
                        label, draws, casters, tris, birdDraws, birdTris);
                }
            }
            finally
            {
                if (eyeGo != null) Object.DestroyImmediate(eyeGo);
            }
        }

        /// <summary>どの資産にも属さない RenderTexture の数。撮る前と後で比べる</summary>
        public static int LooseTextures()
        {
            var n = 0;
            foreach (var rt in Resources.FindObjectsOfTypeAll<RenderTexture>()) if (!EditorUtility.IsPersistent(rt)) n++;
            return n;
        }

        static Transform TakeAt(int which)
        {
            var dive = GameObject.Find("Dive");
            var takes = dive != null ? dive.transform.Find("Takes") : null;
            return takes != null ? takes.Find(which.ToString()) : null;
        }
    }
}
