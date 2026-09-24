using System.Collections.Generic;
using System.Text;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace HalfAware.EditorTools.Study
{
    /// <summary>
    /// 場面 8 のハンドルを握る手の置き方を試す台。Drive.unity を開き、Player を運転席の目へ、体をハンドルの形にして、
    /// 片手ずつ置き方の組をすべて試す。場面は保存しない（終わったら開き直して捨てる）。
    ///
    /// 置き方: 輪の上の角 alpha（12 時から 3 時の側へ。9 時の側は左右を返す）、指の向き（輪に沿った前向きから輪の外へ beta 度）、
    /// 手のひらの向き（輪の面に向かって下から、輪の内側へ gamma 度）、輪の芯を手首から指の向きへ sf・手のひらの側へ dp、指の曲げ curl、肘を寄せる所の横の開き px
    /// </summary>
    public static class GripStudy
    {
        public struct Grip
        {
            public float alpha, beta, gamma, sf, dp, curl, px;
        }

        public struct Result
        {
            public Grip grip;
            public int inside, wrapped, body, tips;
            public float gap, bend, depression, radius;
            public bool thumbInside;

            public override string ToString()
            {
                return string.Format("a{0} b{1} g{2} sf{3} dp{4} curl{5} px{6}: 手が入った頂点 {7} 腕 {14} 隙間 {8:0.0}mm 回り込んだ指 {9} 輪に乗った指先 {15} 親指が内側 {10} 手首の曲げ {11:0} 下へ {12:0} 度 断面 {13:0.00}",
                    grip.alpha, grip.beta, grip.gamma, grip.sf, grip.dp, grip.curl, grip.px, inside, gap * 1000f, wrapped, thumbInside, bend, depression, radius, body, tips);
            }
        }

        static Transform car, player, her;
        static Animator an;
        static SeatedPose pose;
        static SkinnedMeshRenderer skin;
        static Dictionary<Transform, Quaternion> rest;
        static Mesh baked;

        // ハンドルの寸法は組むときの値をそのまま使う
        public const float Lean = BuildDrive.WheelLean;
        public static readonly Vector3 Center = BuildDrive.WheelAt;
        public static readonly float Ring = BuildDrive.WheelRing;
        public const float Tube = BuildDrive.WheelThick * 0.5f;
        /// <summary>curl が 0 のときは指を輪に沿わせて曲げる（BodyPoser.Wrap）。触れない指はこの角で止める（BuildDrive の GripFingerMost と同じ）</summary>
        public static Vector3 Relaxed = new Vector3(70f, 75f, 40f);
        public static float ThumbRelaxed = 30f;
        /// <summary>null でなければ、指の関節ごとの曲げ（度、* は物に触れて止まった）を書く</summary>
        public static StringBuilder Log;

        /// <summary>場面を開いてハンドルの形にする。これを呼んでから Try を呼ぶ</summary>
        public static void Open()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/Drive.unity");
            car = GameObject.Find("Car").transform;
            var seat = car.Find("Seat");
            player = GameObject.Find("Player").transform;
            her = player.Find("Protagonist");
            pose = her.GetComponent<SeatedPose>();
            an = her.GetComponent<Animator>();
            foreach (var s in her.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                s.forceMatrixRecalculationPerRender = true;
                if (s.shadowCastingMode != UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly) skin = s;
            }
            player.SetPositionAndRotation(seat.position, Quaternion.Euler(0f, seat.eulerAngles.y, 0f));
            her.localPosition += Vector3.down * PlayerController.StandingEyeHeight;
            pose.Bind();
            BodyPoser.Stand(an);
            rest = new Dictionary<Transform, Quaternion>();
            foreach (var hb in new[] { HumanBodyBones.RightHand, HumanBodyBones.LeftHand })
            {
                var h = an.GetBoneTransform(hb);
                foreach (var t in h.GetComponentsInChildren<Transform>()) if (t != h) rest[t] = t.localRotation;
            }
            pose.UseAlternate = true;
            pose.Apply();
            if (baked == null) baked = new Mesh { hideFlags = HideFlags.HideAndDontSave };
        }

        public static void Close()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/Drive.unity");
        }

        /// <summary>片手を置き方 g で置いて測る</summary>
        public static Result Try(bool left, Grip g, BodyStudy.ArmGauge gauge)
        {
            var side = left ? -1f : 1f;
            var tilt = car.rotation * Quaternion.Euler(Lean, 0f, 0f);
            var n = tilt * Vector3.back;
            var up = tilt * Vector3.up;
            var x = car.right;
            var c = car.TransformPoint(Center);
            var a = g.alpha * Mathf.Deg2Rad;
            var p = c + Ring * (side * Mathf.Sin(a) * x + Mathf.Cos(a) * up);
            var r = (p - c).normalized;
            var along = left ? (Mathf.Cos(a) * x + Mathf.Sin(a) * up).normalized : (-Mathf.Cos(a) * x + Mathf.Sin(a) * up).normalized;
            var b = g.beta * Mathf.Deg2Rad;
            var gg = g.gamma * Mathf.Deg2Rad;
            var f = (Mathf.Cos(b) * along + Mathf.Sin(b) * r).normalized;
            var palm = Vector3.ProjectOnPlane(-(Mathf.Cos(gg) * n + Mathf.Sin(gg) * r), f).normalized;
            var wrist = p - f * g.sf - palm * g.dp;
            var pole = car.TransformPoint(new Vector3(0.38f + side * g.px, 0.95f, 0.10f));

            var hand = an.GetBoneTransform(left ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand);
            var lower = an.GetBoneTransform(left ? HumanBodyBones.LeftLowerArm : HumanBodyBones.RightLowerArm);
            foreach (var kv in rest)
                if (kv.Key.IsChildOf(hand)) kv.Key.localRotation = kv.Value;
            BodyPoser.Arm(an, left, wrist, pole, f, palm);
            if (g.curl > 0f) BodyPoser.Grip(an, left, g.curl);
            else
            {
                System.Func<Vector3, float> clear = Wheel;
                BodyPoser.Wrap(an, skin, left, clear, Relaxed, ThumbRelaxed, Log);
            }

            var res = new Result { grip = g };
            var bones = skin.bones;
            var bw = skin.sharedMesh.boneWeights;
            skin.BakeMesh(baked, true);
            var v = baked.vertices;
            var minD = 9f;
            var upperArm = an.GetBoneTransform(left ? HumanBodyBones.LeftUpperArm : HumanBodyBones.RightUpperArm);
            for (var i = 0; i < v.Length; i++)
            {
                if (!bones[bw[i].boneIndex0].IsChildOf(upperArm)) continue;
                var w = skin.transform.TransformPoint(v[i]);
                var dist = Wheel(w);
                if (dist < 0f) res.body++;
                if (!bones[bw[i].boneIndex0].IsChildOf(hand)) continue;
                if (dist < 0f) res.inside++;
                minD = Mathf.Min(minD, dist);
            }
            res.gap = minD;
            // 回り込んだ指: 先の節の肌が、輪の芯より手のひらの向こう側で、輪の面から 5 mm 以内にある
            var distals = left
                ? new[] { HumanBodyBones.LeftIndexDistal, HumanBodyBones.LeftMiddleDistal, HumanBodyBones.LeftRingDistal, HumanBodyBones.LeftLittleDistal }
                : new[] { HumanBodyBones.RightIndexDistal, HumanBodyBones.RightMiddleDistal, HumanBodyBones.RightRingDistal, HumanBodyBones.RightLittleDistal };
            var palmNow = BodyPoser.PalmDir(an, left);
            foreach (var db in distals)
            {
                var dt = an.GetBoneTransform(db);
                var near = 9f;
                var round = false;
                for (var i = 0; i < v.Length; i++)
                {
                    if (bones[bw[i].boneIndex0] != dt) continue;
                    var w = skin.transform.TransformPoint(v[i]);
                    var d = w - c;
                    var q = c + (d - n * Vector3.Dot(d, n)).normalized * Ring;
                    var dist = Wheel(w);
                    near = Mathf.Min(near, dist);
                    if (Vector3.Dot(w - q, palmNow) > 0f && dist < 0.005f) round = true;
                }
                if (round) res.wrapped++;
                // 指先が輪に乗っている（宙に浮いていない）
                if (near < 0.003f) res.tips++;
            }
            var thumb = an.GetBoneTransform(left ? HumanBodyBones.LeftThumbDistal : HumanBodyBones.RightThumbDistal).position;
            res.thumbInside = Vector3.Dot(thumb - p, -r) > 0f;
            var armRest = ArmReach.RestOf(skin, an.GetBoneTransform(left ? HumanBodyBones.LeftUpperArm : HumanBodyBones.RightUpperArm), lower, hand);
            ArmReach.Untwist(lower, hand, armRest, ArmReach.TwistShare);
            res.bend = ArmReach.WristBend(lower, hand, armRest);
            var eye = player.TransformPoint(new Vector3(0f, 0f, 0.22f));
            var hc = hand.position + f * 0.06f - eye;
            res.depression = Mathf.Atan2(-hc.y, new Vector2(hc.x, hc.z).magnitude) * Mathf.Rad2Deg;
            if (gauge != null)
            {
                float rr, ww;
                gauge.Thinnest(out rr, out ww);
                res.radius = rr;
            }
            return res;
        }

        /// <summary>点からハンドルの面までの距離（中なら負）。BuildDrive.WheelGap（組むのと同じ箱）で測る</summary>
        public static float Wheel(Vector3 world)
        {
            return BuildDrive.WheelGap(car.InverseTransformPoint(world));
        }

        /// <summary>組をすべて試し、手と腕がハンドルに入らず、輪に乗った指先と回り込んだ指の多い順・手首の曲げの小さい順に top 個を返す</summary>
        public static string Search(bool left, float[] alphas, float[] betas, float[] gammas, float[] sfs, float[] dps, float[] curls, float[] pxs, int top)
        {
            var list = new List<Result>();
            foreach (var a in alphas)
            foreach (var b in betas)
            foreach (var g in gammas)
            foreach (var sf in sfs)
            foreach (var dp in dps)
            foreach (var cu in curls)
            foreach (var px in pxs)
                list.Add(Try(left, new Grip { alpha = a, beta = b, gamma = g, sf = sf, dp = dp, curl = cu, px = px }, null));
            list.Sort((x, y) =>
            {
                if (x.inside + x.body != y.inside + y.body) return (x.inside + x.body).CompareTo(y.inside + y.body);
                if (x.tips + x.wrapped != y.tips + y.wrapped) return (y.tips + y.wrapped).CompareTo(x.tips + x.wrapped);
                return x.bend.CompareTo(y.bend);
            });
            var sb = new StringBuilder();
            for (var i = 0; i < list.Count && i < top; i++) sb.AppendLine(list[i].ToString());
            return sb.ToString();
        }

        /// <summary>
        /// 両手を置いて、ゲームの見え方で下へ pitches 度と、手の寄り（確認用）を撮る。絵の名は {tag}_{角度}.png、
        /// {tag}_hand.png（上から両手）、{tag}_handR.png（右の外から）、{tag}_close.png（右手の寄り）、{tag}_side.png（右手を外の横から）
        /// </summary>
        public static string Shoot(string tag, Grip right, Grip leftGrip, float[] pitches)
        {
            BodyPoser.Stand(an);
            var gauge = new BodyStudy.ArmGauge(her.gameObject);
            try
            {
                pose.Apply();
                var r = Try(false, right, gauge);
                var l = Try(true, leftGrip, gauge);
                var sb = new StringBuilder();
                sb.AppendLine("右 " + r).AppendLine("左 " + l);
                sb.AppendLine(Cameras(tag, pitches));
                return sb.ToString();
            }
            finally
            {
                gauge.Dispose();
            }
        }

        /// <summary>
        /// 場面に書いてあるハンドルの形（SeatedPose の alternate）のまま撮る。Open の後に呼ぶ。
        /// ハンドルへ入った体の頂点の数と、腕のいちばん細い所（立った形に対する割合）も返す
        /// </summary>
        public static string ShootAsBuilt(string tag, float[] pitches)
        {
            BodyPoser.Stand(an);
            var gauge = new BodyStudy.ArmGauge(her.gameObject);
            try
            {
                pose.Apply();
                float rr, ww;
                var thin = gauge.Thinnest(out rr, out ww);
                skin.BakeMesh(baked, true);
                var inside = 0;
                foreach (var v in baked.vertices)
                    if (Wheel(skin.transform.TransformPoint(v)) < 0f) inside++;
                var sb = new StringBuilder();
                sb.AppendFormat("場面のハンドルの形: ハンドルへ入った体の頂点 {0}、腕のいちばん細い断面（立った形に対して）半径 {1:0.00} 幅 {2:0.00}: {3}", inside, rr, ww, thin).AppendLine();
                sb.AppendLine(Cameras(tag, pitches));
                return sb.ToString();
            }
            finally
            {
                gauge.Dispose();
            }
        }

        static string Cameras(string tag, float[] pitches)
        {
            var result = BodyStudy.Shoot(tag, 0f, 0.22f, pitches);
            var cam = BodyStudy.MakeCamera(GameObject.Find("Player/Main Camera").GetComponent<Camera>());
            try
            {
                cam.nearClipPlane = 0.01f;
                cam.fieldOfView = 50f;
                cam.transform.position = car.TransformPoint(new Vector3(0.38f, 1.62f, 0.14f));
                cam.transform.LookAt(car.TransformPoint(new Vector3(0.38f, 1.24f, 0.42f)));
                Grab(cam, tag + "_hand.png", 640, 360);
                cam.fieldOfView = 35f;
                cam.transform.position = car.TransformPoint(new Vector3(0.86f, 1.40f, 0.60f));
                cam.transform.LookAt(car.TransformPoint(new Vector3(0.55f, 1.26f, 0.42f)));
                Grab(cam, tag + "_handR.png", 640, 360);
                // 右手の寄り: 目の側の上から、握った所を大きく
                var grip = an.GetBoneTransform(HumanBodyBones.RightHand).position;
                var toward = an.GetBoneTransform(HumanBodyBones.RightMiddleProximal).position;
                cam.fieldOfView = 22f;
                cam.transform.position = car.TransformPoint(new Vector3(0.40f, 1.60f, 0.20f));
                cam.transform.LookAt(Vector3.Lerp(grip, toward, 0.8f));
                Grab(cam, tag + "_close.png", 640, 480);
                // 右手を外の横から
                cam.fieldOfView = 16f;
                cam.transform.position = car.TransformPoint(new Vector3(0.95f, 1.40f, 0.66f));
                cam.transform.LookAt(Vector3.Lerp(grip, toward, 0.8f));
                Grab(cam, tag + "_side.png", 640, 480);
            }
            finally
            {
                Object.DestroyImmediate(cam.gameObject);
            }
            return result;
        }

        static void Grab(Camera cam, string name, int w, int h)
        {
            var shot = FaceStudy.Grab(cam, w, h);
            FaceStudy.Save(shot, System.IO.Path.Combine(BodyStudy.OutDir, name));
            Object.DestroyImmediate(shot);
        }
    }
}
