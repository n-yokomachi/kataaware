using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace HalfAware.EditorTools.Study
{
    /// <summary>
    /// 手の形を数で確かめる台。
    ///
    /// - 肌: 手と指の三角ごとに、束ねた姿勢（mesh の元の形）と比べた辺の伸び・縮みと、面の裏返りを数える
    /// - 骨: 指の関節ごとに、立った形からの曲げを、Humanoid の筋肉が曲げる軸（屈曲の軸）まわりの角と、
    ///   それ以外の回し（ひねり・横倒れ）の角に分ける。屈曲の軸は筋肉の値を少し動かして骨がどう回るかから読む
    /// </summary>
    public static class HandStudy
    {
        /// <summary>伸び・縮みと数える辺の比</summary>
        public const float Stretch = 1.4f;
        public const float Squash = 0.6f;

        public struct Skin
        {
            public int triangles, stretched, squashed, flipped;
            public float longest, shortest;
            /// <summary>伸び・縮み・裏返りの三角が付いている骨（いちばん重みの大きい骨）ごとの数</summary>
            public Dictionary<string, int> byBone;
            /// <summary>裏返った三角が付いている骨ごとの数</summary>
            public Dictionary<string, int> flippedBy;

            public string Flips()
            {
                if (flippedBy == null) return "";
                var sb = new StringBuilder();
                foreach (var kv in flippedBy) sb.Append(kv.Key.Replace("Bip01 ", "")).Append(':').Append(kv.Value).Append(' ');
                return sb.ToString();
            }

            public string Bones()
            {
                if (byBone == null) return "";
                var sb = new StringBuilder();
                foreach (var kv in byBone) sb.Append(kv.Key.Replace("Bip01 ", "")).Append(':').Append(kv.Value).Append(' ');
                return sb.ToString();
            }

            public override string ToString()
            {
                return string.Format("三角 {0}: 伸び {1}（最大 {2:0.00}）縮み {3}（最小 {4:0.00}）裏返り {5}", triangles, stretched, longest, squashed, shortest, flipped);
            }
        }

        /// <summary>手（left）の、手の骨から先に付いた三角の伸び・縮み・裏返りを数える。fingersOnly なら指の骨に付いた三角だけ（手のひらと親指の付け根の水かきは除く）</summary>
        public static Skin Measure(SkinnedMeshRenderer skin, Animator an, bool left, bool fingersOnly = false)
        {
            var res = new Skin { longest = 0f, shortest = 9f, byBone = new Dictionary<string, int>(), flippedBy = new Dictionary<string, int>() };
            var hand = an.GetBoneTransform(left ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand);
            var mesh = skin.sharedMesh;
            var bones = skin.bones;
            var bw = mesh.boneWeights;
            var bind = mesh.bindposes;
            var rest = mesh.vertices;
            var restN = mesh.normals;
            var baked = new Mesh();
            skin.BakeMesh(baked, true);
            var now = baked.vertices;
            Object.DestroyImmediate(baked);
            var inHand = new bool[rest.Length];
            for (var i = 0; i < rest.Length; i++)
                inHand[i] = bones[bw[i].boneIndex0] != null && bones[bw[i].boneIndex0].IsChildOf(hand) && (!fingersOnly || bones[bw[i].boneIndex0] != hand);
            var world = skin.transform.localToWorldMatrix;
            for (var sub = 0; sub < mesh.subMeshCount; sub++)
            {
                var tris = mesh.GetTriangles(sub);
                for (var t = 0; t < tris.Length; t += 3)
                {
                    int a = tris[t], b = tris[t + 1], c = tris[t + 2];
                    if (!inHand[a] || !inHand[b] || !inHand[c]) continue;
                    res.triangles++;
                    var wa = world.MultiplyPoint3x4(now[a]);
                    var wb = world.MultiplyPoint3x4(now[b]);
                    var wc = world.MultiplyPoint3x4(now[c]);
                    var worst = 1f;
                    var least = 1f;
                    foreach (var e in new[] { new[] { a, b }, new[] { b, c }, new[] { c, a } })
                    {
                        var r0 = Vector3.Distance(rest[e[0]], rest[e[1]]);
                        if (r0 < 1e-6f) continue;
                        var w0 = world.MultiplyPoint3x4(now[e[0]]);
                        var w1 = world.MultiplyPoint3x4(now[e[1]]);
                        var k = Vector3.Distance(w0, w1) / r0;
                        worst = Mathf.Max(worst, k);
                        least = Mathf.Min(least, k);
                    }
                    if (worst > Stretch) res.stretched++;
                    if (least < Squash) res.squashed++;
                    res.longest = Mathf.Max(res.longest, worst);
                    res.shortest = Mathf.Min(res.shortest, least);
                    // 裏返り: いちばん重みの大きい骨の回しで、元の面の向きを今へ運んだ向きと、今の面の向きが逆
                    var bi = bw[a].boneIndex0;
                    var m = bones[bi].localToWorldMatrix * bind[bi];
                    var n0 = Vector3.Cross(rest[b] - rest[a], rest[c] - rest[a]);
                    var expect = m.MultiplyVector(n0);
                    var n1 = Vector3.Cross(wb - wa, wc - wa);
                    var flip = Vector3.Dot(expect, n1) < 0f;
                    if (flip) res.flipped++;
                    if (flip || worst > Stretch || least < Squash)
                    {
                        // 三つの頂点のうち、いちばん指先に近い骨で数える
                        var name = bones[bw[a].boneIndex0].name;
                        foreach (var v in new[] { b, c })
                        {
                            var other = bones[bw[v].boneIndex0].name;
                            if (string.CompareOrdinal(other, name) > 0) name = other;
                        }
                        res.byBone.TryGetValue(name, out var n);
                        res.byBone[name] = n + 1;
                        if (flip)
                        {
                            res.flippedBy.TryGetValue(name, out var nf);
                            res.flippedBy[name] = nf + 1;
                        }
                    }
                }
            }
            return res;
        }

        /// <summary>
        /// 手（left）を、別の場面（プレビューの場面）で四つの向き（手の甲・手のひら・親指の側・小指の側）から撮り、横に並べた一枚にして path に書く。
        /// 体は組み立てた体をそのまま使う（呼ぶ側で姿勢を付けておく）。体はプレビューの場面へ移るので、撮った後は使えない
        /// </summary>
        public static void ShootHand(GameObject holder, Animator an, bool left, string path)
        {
            var hand = an.GetBoneTransform(left ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand);
            var centre = hand.position + BodyPoser.FingerDir(an, left) * 0.07f;
            var palm = BodyPoser.PalmDir(an, left);
            var along = BodyPoser.FingerDir(an, left);
            var across = Vector3.Cross(palm, along).normalized;
            var preview = new UnityEditor.PreviewRenderUtility();
            const int w = 320, h = 320;
            var sheet = new Texture2D(w * 4, h, TextureFormat.RGBA32, false);
            try
            {
                preview.AddSingleGO(holder);
                var dirs = new[] { -palm, palm, across, -across };
                for (var i = 0; i < dirs.Length; i++)
                {
                    preview.BeginStaticPreview(new Rect(0, 0, w, h));
                    var cam = preview.camera;
                    cam.orthographic = false;
                    cam.fieldOfView = 30f;
                    cam.nearClipPlane = 0.01f;
                    cam.farClipPlane = 3f;
                    cam.clearFlags = CameraClearFlags.SolidColor;
                    cam.backgroundColor = new Color(0.16f, 0.17f, 0.2f);
                    cam.transform.position = centre + dirs[i] * 0.42f;
                    cam.transform.LookAt(centre, along);
                    preview.lights[0].intensity = 1.1f;
                    preview.lights[0].transform.rotation = Quaternion.LookRotation(-dirs[i] + Vector3.down * 0.4f);
                    preview.lights[1].intensity = 0.5f;
                    preview.ambientColor = new Color(0.3f, 0.3f, 0.3f);
                    preview.Render(true);
                    var tex = (Texture2D)preview.EndStaticPreview();
                    sheet.SetPixels(i * w, 0, w, h, tex.GetPixels());
                    Object.DestroyImmediate(tex);
                }
                sheet.Apply();
                System.IO.File.WriteAllBytes(path, sheet.EncodeToPNG());
            }
            finally
            {
                Object.DestroyImmediate(sheet);
                preview.Cleanup();
            }
        }

        /// <summary>
        /// 手がつまんだ物（jack）を、物の枠の六つの向き（物の前・後ろ・上・下・右・左）から寄って撮り、横に並べた一枚にして path に書く。
        /// 物の芯から depth（m）より向こうは写さない。体はプレビューの場面へ移るので、撮った後は使えない
        /// </summary>
        public static void ShootPinch(GameObject holder, Transform jack, string path, float distance = 0.2f, float depth = 0.08f)
        {
            var centre = jack.position + jack.forward * BodyPoser.GripAlong;
            var preview = new UnityEditor.PreviewRenderUtility();
            const int w = 320, h = 320;
            var dirs = new[] { jack.forward, -jack.forward, jack.up, -jack.up, jack.right, -jack.right };
            var ups = new[] { jack.up, jack.up, -jack.forward, jack.forward, jack.up, jack.up };
            var sheet = new Texture2D(w * dirs.Length, h, TextureFormat.RGBA32, false);
            try
            {
                preview.AddSingleGO(holder);
                for (var i = 0; i < dirs.Length; i++)
                {
                    preview.BeginStaticPreview(new Rect(0, 0, w, h));
                    var cam = preview.camera;
                    cam.orthographic = false;
                    cam.fieldOfView = 30f;
                    cam.nearClipPlane = 0.01f;
                    // 物の向こうの体（顔や胸）は写さない
                    cam.farClipPlane = distance + depth;
                    cam.clearFlags = CameraClearFlags.SolidColor;
                    cam.backgroundColor = new Color(0.16f, 0.17f, 0.2f);
                    cam.transform.position = centre + dirs[i] * distance;
                    cam.transform.LookAt(centre, ups[i]);
                    preview.lights[0].intensity = 1.1f;
                    preview.lights[0].transform.rotation = Quaternion.LookRotation(-dirs[i] + Vector3.down * 0.4f);
                    preview.lights[1].intensity = 0.5f;
                    preview.ambientColor = new Color(0.3f, 0.3f, 0.3f);
                    preview.Render(true);
                    var tex = (Texture2D)preview.EndStaticPreview();
                    sheet.SetPixels(i * w, 0, w, h, tex.GetPixels());
                    Object.DestroyImmediate(tex);
                }
                sheet.Apply();
                System.IO.File.WriteAllBytes(path, sheet.EncodeToPNG());
            }
            finally
            {
                Object.DestroyImmediate(sheet);
                preview.Cleanup();
            }
        }

        /// <summary>
        /// 場面の物 source を複製して、別の場面（プレビューの場面）で centre を向く views の向き（centre から見たカメラの向き）から撮り、
        /// 横に並べた一枚にして path に書く。複製は撮った後に捨てるので、場面の物はそのまま残る。
        /// 骨の姿勢は複製に写るので、呼ぶ側で姿勢を付けてから呼ぶ
        /// </summary>
        public static void ShootAround(GameObject source, Vector3 centre, Vector3[] views, Vector3 up, string path, float distance = 0.35f, float fov = 30f)
        {
            var copy = Object.Instantiate(source, source.transform.position, source.transform.rotation);
            foreach (var c in copy.GetComponentsInChildren<Camera>(true)) c.enabled = false;
            var preview = new UnityEditor.PreviewRenderUtility();
            const int w = 480, h = 360;
            var sheet = new Texture2D(w * views.Length, h, TextureFormat.RGBA32, false);
            try
            {
                preview.AddSingleGO(copy);
                for (var i = 0; i < views.Length; i++)
                {
                    preview.BeginStaticPreview(new Rect(0, 0, w, h));
                    var cam = preview.camera;
                    cam.orthographic = false;
                    cam.fieldOfView = fov;
                    cam.nearClipPlane = 0.01f;
                    cam.farClipPlane = 5f;
                    cam.clearFlags = CameraClearFlags.SolidColor;
                    cam.backgroundColor = new Color(0.16f, 0.17f, 0.2f);
                    var dir = views[i].normalized;
                    cam.transform.position = centre + dir * distance;
                    cam.transform.LookAt(centre, Mathf.Abs(Vector3.Dot(dir, up)) > 0.95f ? Vector3.Cross(dir, Vector3.right) : up);
                    preview.lights[0].intensity = 1.1f;
                    preview.lights[0].transform.rotation = Quaternion.LookRotation(-dir + Vector3.down * 0.4f);
                    preview.lights[1].intensity = 0.5f;
                    preview.ambientColor = new Color(0.3f, 0.3f, 0.3f);
                    preview.Render(true);
                    var tex = (Texture2D)preview.EndStaticPreview();
                    sheet.SetPixels(i * w, 0, w, h, tex.GetPixels());
                    Object.DestroyImmediate(tex);
                }
                sheet.Apply();
                System.IO.File.WriteAllBytes(path, sheet.EncodeToPNG());
            }
            finally
            {
                Object.DestroyImmediate(sheet);
                preview.Cleanup();
                if (copy != null) Object.DestroyImmediate(copy);
            }
        }

        /// <summary>指の骨の組（付け根・中・先）。親指が最後</summary>
        public static HumanBodyBones[][] Fingers(bool left)
        {
            HumanBodyBones F(HumanBodyBones l, HumanBodyBones r) { return left ? l : r; }
            return new[]
            {
                new[] { F(HumanBodyBones.LeftIndexProximal, HumanBodyBones.RightIndexProximal), F(HumanBodyBones.LeftIndexIntermediate, HumanBodyBones.RightIndexIntermediate), F(HumanBodyBones.LeftIndexDistal, HumanBodyBones.RightIndexDistal) },
                new[] { F(HumanBodyBones.LeftMiddleProximal, HumanBodyBones.RightMiddleProximal), F(HumanBodyBones.LeftMiddleIntermediate, HumanBodyBones.RightMiddleIntermediate), F(HumanBodyBones.LeftMiddleDistal, HumanBodyBones.RightMiddleDistal) },
                new[] { F(HumanBodyBones.LeftRingProximal, HumanBodyBones.RightRingProximal), F(HumanBodyBones.LeftRingIntermediate, HumanBodyBones.RightRingIntermediate), F(HumanBodyBones.LeftRingDistal, HumanBodyBones.RightRingDistal) },
                new[] { F(HumanBodyBones.LeftLittleProximal, HumanBodyBones.RightLittleProximal), F(HumanBodyBones.LeftLittleIntermediate, HumanBodyBones.RightLittleIntermediate), F(HumanBodyBones.LeftLittleDistal, HumanBodyBones.RightLittleDistal) },
                new[] { F(HumanBodyBones.LeftThumbProximal, HumanBodyBones.RightThumbProximal), F(HumanBodyBones.LeftThumbIntermediate, HumanBodyBones.RightThumbIntermediate), F(HumanBodyBones.LeftThumbDistal, HumanBodyBones.RightThumbDistal) },
            };
        }

        /// <summary>
        /// 指の骨ごとの屈曲の軸（骨の枠で見た向き）。Humanoid の「曲げ」の筋肉を 0 の前後で動かし、骨の回しの軸を読む。
        /// 軸の向きは、筋肉の値を下げる（指を握る）向きに回すと正になるように揃える
        /// </summary>
        public static Dictionary<HumanBodyBones, Vector3> FlexAxes(Animator an)
        {
            var axes = new Dictionary<HumanBodyBones, Vector3>();
            var handler = new HumanPoseHandler(an.avatar, an.transform);
            var keep = Keep(an);
            try
            {
                var pose = new HumanPose();
                handler.GetHumanPose(ref pose);
                foreach (var left in new[] { true, false })
                    foreach (var f in Fingers(left))
                        foreach (var hb in f)
                        {
                            var muscle = HumanTrait.MuscleFromBone((int)hb, 2);
                            if (muscle < 0) continue;
                            var bone = an.GetBoneTransform(hb);
                            var baseValue = pose.muscles[muscle];
                            pose.muscles[muscle] = 0.1f;
                            handler.SetHumanPose(ref pose);
                            var q0 = bone.localRotation;
                            pose.muscles[muscle] = -0.1f;
                            handler.SetHumanPose(ref pose);
                            var q1 = bone.localRotation;
                            pose.muscles[muscle] = baseValue;
                            handler.SetHumanPose(ref pose);
                            // 骨の枠で見た回し（q1 = q0 * d）
                            var d = Quaternion.Inverse(q0) * q1;
                            d.ToAngleAxis(out var angle, out var axis);
                            if (angle > 180f) { angle = 360f - angle; axis = -axis; }
                            axes[hb] = axis.normalized;
                        }
            }
            finally
            {
                Restore(keep);
                handler.Dispose();
            }
            return axes;
        }

        /// <summary>
        /// 指の関節ごとに、基の形 baseRot（骨の親から見た向き）からの回しを、屈曲の軸まわりの角（曲げ、度、握る向きが正）と、
        /// 骨の長さの軸まわりのひねり（度）に分けて「曲げ/ひねり」で書く。worstOff はひねりのいちばん大きい値
        /// </summary>
        public static string Joints(Animator an, bool left, Dictionary<HumanBodyBones, Vector3> axes, Dictionary<Transform, Quaternion> baseRot, out float worstOff)
        {
            worstOff = 0f;
            var sb = new StringBuilder();
            foreach (var f in Fingers(left))
            {
                foreach (var hb in f)
                {
                    var bone = an.GetBoneTransform(hb);
                    if (bone == null || !axes.ContainsKey(hb) || !baseRot.ContainsKey(bone)) continue;
                    var d = Quaternion.Inverse(baseRot[bone]) * bone.localRotation;
                    Split(d, axes[hb], out var bend, out var off);
                    // 曲げ以外の回しのうち、骨の長さの軸まわりのひねり（開く・閉じるの横倒れは数えない）
                    var along = bone.childCount > 0 ? bone.GetChild(0).localPosition.normalized : bone.localPosition.normalized;
                    Split(d, along, out var twist, out _);
                    twist = Mathf.Abs(twist);
                    worstOff = Mathf.Max(worstOff, twist);
                    sb.AppendFormat("{0}:{1:0}/{2:0} ", hb.ToString().Replace("Left", "").Replace("Right", ""), bend, twist);
                }
                sb.Append("| ");
            }
            return sb.ToString();
        }

        /// <summary>回し d を、軸 axis まわりの回し（度、符号つき）と、残りの回しの角（度）に分ける（振りとひねりの分け方）</summary>
        public static void Split(Quaternion d, Vector3 axis, out float bend, out float off)
        {
            var v = new Vector3(d.x, d.y, d.z);
            var p = Vector3.Project(v, axis);
            var twist = new Quaternion(p.x, p.y, p.z, d.w);
            if (twist.x * twist.x + twist.y * twist.y + twist.z * twist.z + twist.w * twist.w < 1e-12f) twist = Quaternion.identity;
            else twist = Normalize(twist);
            twist.ToAngleAxis(out var angle, out var ax);
            if (angle > 180f) { angle -= 360f; }
            bend = Vector3.Dot(ax, axis) >= 0f ? angle : -angle;
            var rest = d * Quaternion.Inverse(twist);
            rest.ToAngleAxis(out var restAngle, out _);
            off = restAngle > 180f ? 360f - restAngle : restAngle;
        }

        static Quaternion Normalize(Quaternion q)
        {
            var m = Mathf.Sqrt(q.x * q.x + q.y * q.y + q.z * q.z + q.w * q.w);
            return new Quaternion(q.x / m, q.y / m, q.z / m, q.w / m);
        }

        /// <summary>
        /// ジャックと手（left）の肌の間を、指の骨ごとに測って書く。骨ごとに、いちばん近い頂点のジャックの面からの距離（mm、中なら負）と、中に入った頂点の数。
        /// 頂点はいちばん重みの大きい骨で分ける。ジャックの形は <see cref="BodyPoser.JackSurface"/>
        /// </summary>
        public static string JackContact(SkinnedMeshRenderer skin, Animator an, Transform jack, bool left)
        {
            var bones = new Dictionary<Transform, string>();
            bones[an.GetBoneTransform(left ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand)] = "手";
            var names = new[] { "人", "中", "薬", "小", "親" };
            var fingers = Fingers(left);
            for (var f = 0; f < fingers.Length; f++)
                for (var j = 0; j < 3; j++)
                {
                    var t = an.GetBoneTransform(fingers[f][j]);
                    if (t != null) bones[t] = names[f] + (j + 1);
                }
            var baked = new Mesh();
            skin.BakeMesh(baked, true);
            var v = baked.vertices;
            var bw = skin.sharedMesh.boneWeights;
            var sb = skin.bones;
            var near = new Dictionary<string, float>();
            var inside = new Dictionary<string, int>();
            for (var i = 0; i < v.Length; i++)
            {
                var b = sb[bw[i].boneIndex0];
                string name;
                if (b == null || !bones.TryGetValue(b, out name)) continue;
                var d = BodyPoser.JackSurface(jack, skin.transform.TransformPoint(v[i]));
                float was;
                if (!near.TryGetValue(name, out was) || d < was) near[name] = d;
                if (d < 0f) { int n; inside.TryGetValue(name, out n); inside[name] = n + 1; }
            }
            Object.DestroyImmediate(baked);
            var res = new StringBuilder();
            foreach (var kv in near)
            {
                if (kv.Value > 0.015f) continue;
                int n;
                inside.TryGetValue(kv.Key, out n);
                res.AppendFormat("{0} {1:0.0}", kv.Key, kv.Value * 1000f);
                if (n > 0) res.AppendFormat("（中 {0}）", n);
                res.Append(' ');
            }
            return res.ToString();
        }

        /// <summary>骨の向きと位置の写し。試した後に書き戻す</summary>
        public static Dictionary<Transform, (Vector3, Quaternion)> Keep(Animator an)
        {
            var keep = new Dictionary<Transform, (Vector3, Quaternion)>();
            foreach (var t in an.GetComponentsInChildren<Transform>(true)) keep[t] = (t.localPosition, t.localRotation);
            return keep;
        }

        public static void Restore(Dictionary<Transform, (Vector3, Quaternion)> keep)
        {
            foreach (var kv in keep) { kv.Key.localPosition = kv.Value.Item1; kv.Key.localRotation = kv.Value.Item2; }
        }

        /// <summary>指の骨の、親から見た向き（基の形として使う）</summary>
        public static Dictionary<Transform, Quaternion> FingerRotations(Animator an)
        {
            var map = new Dictionary<Transform, Quaternion>();
            foreach (var left in new[] { true, false })
                foreach (var f in Fingers(left))
                    foreach (var hb in f)
                    {
                        var t = an.GetBoneTransform(hb);
                        if (t != null) map[t] = t.localRotation;
                    }
            return map;
        }
    }
}
