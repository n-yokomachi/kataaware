using System.Collections.Generic;
using System.Text;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace HalfAware.EditorTools.Study
{
    /// <summary>
    /// 場面 8 のハンドルを握る手の置き方を試す台。Drive.unity を開き、Player を運転席の目へ、体をハンドルの形にして、
    /// 片手ずつ置き方の組をすべて試す。場面は保存しない（終わったら開き直して捨てる）。
    /// ハンドルの傾きと芯（<see cref="Lean"/>・<see cref="Center"/>）は組むときの値から始まり、書き換えて <see cref="Reset"/> を呼べば
    /// 場面を組み直さずに試せる（体の形だけを測る。場面の中の輪の絵は組み直すまで元のまま）。
    ///
    /// 置き方: 輪の上の角 alpha（12 時から 3 時の側へ。9 時の側は左右を返す）、手のひらが輪の断面のどこに当たるか phi
    /// （輪の面から運転席の側を 0、輪の外周の側を 90 度）、手の向き（手首から中指の付け根へ）と輪に沿って 12 時へ向かう向きの角 beta、
    /// 輪の芯を手首から手の向きへ sf・手のひらの側へ dp、肘を寄せる所（運転席の真ん中から横へ px・高さ py）、指をそろえる割合 close
    /// </summary>
    public static class GripStudy
    {
        public struct Grip
        {
            public float alpha, beta, phi, sf, dp, px, py, close;
        }

        public struct Result
        {
            public Grip grip;
            /// <summary>ハンドルに入った手の頂点・腕（上腕から先）の頂点</summary>
            public int inside, body;
            /// <summary>四本の指先のうち、輪の断面の中心から見て計器盤の側へ回ったもの・目から輪に隠れたもの</summary>
            public int far, hidden;
            /// <summary>四本の指先の、輪の断面の中での角（phi と同じ測り方、度）</summary>
            public float[] tipAngle;
            public float gap, bend, flex, deviate, depression, radius, spread, thumbAngle;

            public override string ToString()
            {
                var tips = tipAngle == null ? "" : string.Join("/", System.Array.ConvertAll(tipAngle, t => t.ToString("0")));
                return string.Format("a{0} b{1} phi{2} sf{3} dp{4} px{5} py{6} close{7}: 手が入った頂点 {8} 腕 {9} 隙間 {10:0.0}mm 指先の角 {11}（計器盤の側 {12}・目から隠れた {13}）親指の角 {14:0} 指の開き {15:0} 手首の曲げ {16:0}（掌屈 {17:0} 橈尺 {18:0}）下へ {19:0} 度 断面 {20:0.00}",
                    grip.alpha, grip.beta, grip.phi, grip.sf, grip.dp, grip.px, grip.py, grip.close, inside, body, gap * 1000f, tips, far, hidden, thumbAngle, spread, bend, flex, deviate, depression, radius);
            }
        }

        static Transform car, player, her;
        static Animator an;
        static SeatedPose pose;
        static SkinnedMeshRenderer skin;
        static Dictionary<Transform, Quaternion> rest;
        static Mesh baked;

        /// <summary>試すハンドルの傾き（度）と芯。既定は組むときの値</summary>
        public static float Lean = BuildDrive.WheelLean;
        public static Vector3 Center = BuildDrive.WheelAt;
        public static readonly float Ring = BuildDrive.WheelRing;
        public const float Tube = BuildDrive.WheelThick * 0.5f;
        static List<BuildDrive.WheelPart> parts;
        /// <summary>指を輪に沿わせて曲げる（BodyPoser.Wrap）。触れない指はこの角で止める</summary>
        public static Vector3 Relaxed = new Vector3(70f, 75f, 40f);
        public static float ThumbRelaxed = 30f;
        /// <summary>null でなければ、指の関節ごとの曲げ（度、* は物に触れて止まった）を書く</summary>
        public static StringBuilder Log;

        /// <summary>試すハンドルを置き直す（Lean・Center を書き換えた後に呼ぶ）</summary>
        public static void Reset()
        {
            parts = BuildDrive.WheelParts(Center, BuildDrive.WheelOuter, BuildDrive.WheelThick, Lean);
        }

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
            Reset();
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
            // 輪の面から運転席の側へ向く向きと、12 時の向き
            var n = tilt * Vector3.back;
            var up = tilt * Vector3.up;
            var x = car.right;
            var c = car.TransformPoint(Center);
            var a = g.alpha * Mathf.Deg2Rad;
            var p = c + Ring * (side * Mathf.Sin(a) * x + Mathf.Cos(a) * up);
            var r = (p - c).normalized;
            var along = (-side * Mathf.Cos(a) * x + Mathf.Sin(a) * up).normalized;
            var phi = g.phi * Mathf.Deg2Rad;
            var contact = Mathf.Cos(phi) * n + Mathf.Sin(phi) * r;
            var wrap = -Mathf.Sin(phi) * n + Mathf.Cos(phi) * r;
            var b = g.beta * Mathf.Deg2Rad;
            var f = (Mathf.Cos(b) * along + Mathf.Sin(b) * wrap).normalized;
            var palm = Vector3.ProjectOnPlane(-contact, f).normalized;
            var wrist = p - f * g.sf - palm * g.dp;
            var pole = car.TransformPoint(new Vector3(Center.x + side * g.px, g.py, 0.10f));

            var hand = an.GetBoneTransform(left ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand);
            var lower = an.GetBoneTransform(left ? HumanBodyBones.LeftLowerArm : HumanBodyBones.RightLowerArm);
            foreach (var kv in rest)
                if (kv.Key.IsChildOf(hand)) kv.Key.localRotation = kv.Value;
            BodyPoser.Arm(an, left, wrist, pole, f, palm);
            if (g.close > 0f) BodyPoser.Close(an, left, g.close);
            BodyPoser.Wrap(an, skin, left, Wheel, Relaxed, ThumbRelaxed, Log);

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

            // 指先: 末節の肌の頂点のうち、手首からいちばん遠いもの
            var eye = player.TransformPoint(new Vector3(0f, 0f, BuildDrive.EyeLead));
            var distals = left
                ? new[] { HumanBodyBones.LeftIndexDistal, HumanBodyBones.LeftMiddleDistal, HumanBodyBones.LeftRingDistal, HumanBodyBones.LeftLittleDistal }
                : new[] { HumanBodyBones.RightIndexDistal, HumanBodyBones.RightMiddleDistal, HumanBodyBones.RightRingDistal, HumanBodyBones.RightLittleDistal };
            res.tipAngle = new float[4];
            for (var k = 0; k < 4; k++)
            {
                var tip = Tip(an.GetBoneTransform(distals[k]), bones, bw, v, hand.position);
                res.tipAngle[k] = AroundTube(tip, c, n);
                // 断面の中心から見て計器盤の側（輪の面より前）
                var q = Nearest(tip, c, n);
                if (Vector3.Dot(tip - q, n) < 0f) res.far++;
                if (Blocked(eye, tip)) res.hidden++;
            }
            var thumb = Tip(an.GetBoneTransform(left ? HumanBodyBones.LeftThumbDistal : HumanBodyBones.RightThumbDistal), bones, bw, v, hand.position);
            res.thumbAngle = AroundTube(thumb, c, n);
            res.spread = Spread(left);

            var armRest = ArmReach.RestOf(skin, upperArm, lower, hand);
            ArmReach.Untwist(lower, hand, armRest, ArmReach.TwistShare);
            res.bend = ArmReach.WristBend(lower, hand, armRest);
            WristParts(left, hand, armRest, out res.flex, out res.deviate);
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

        /// <summary>点に近い輪の芯の上の点</summary>
        static Vector3 Nearest(Vector3 w, Vector3 c, Vector3 n)
        {
            var d = w - c;
            return c + (d - n * Vector3.Dot(d, n)).normalized * Ring;
        }

        /// <summary>
        /// 点が輪の断面の中のどの角にあるか。度。輪の面から運転席の側を 0、その点の近くの輪の外周の側を 90、計器盤の側を 180、
        /// 輪の内側（中心の側）を 270
        /// </summary>
        static float AroundTube(Vector3 w, Vector3 c, Vector3 n)
        {
            var q = Nearest(w, c, n);
            var outward = (q - c).normalized;
            var d = w - q;
            var ang = Mathf.Atan2(Vector3.Dot(d, outward), Vector3.Dot(d, n)) * Mathf.Rad2Deg;
            return ang < 0f ? ang + 360f : ang;
        }

        /// <summary>骨 bone にいちばん重みの大きい肌の頂点のうち、from からいちばん遠いもの</summary>
        static Vector3 Tip(Transform bone, Transform[] bones, BoneWeight[] bw, Vector3[] v, Vector3 from)
        {
            var best = bone.position;
            var far = -1f;
            for (var i = 0; i < v.Length; i++)
            {
                if (bones[bw[i].boneIndex0] != bone) continue;
                var w = skin.transform.TransformPoint(v[i]);
                var d = (w - from).sqrMagnitude;
                if (d > far) { far = d; best = w; }
            }
            return best;
        }

        /// <summary>目から点までの間に、ハンドルがあるか（点の手前 5 mm まで）</summary>
        static bool Blocked(Vector3 eye, Vector3 to)
        {
            var span = Vector3.Distance(eye, to) - 0.005f;
            var dir = (to - eye).normalized;
            for (var t = 0f; t < span;)
            {
                var d = Wheel(eye + dir * t);
                if (d < 0.001f) return true;
                t += Mathf.Max(d, 0.002f);
            }
            return false;
        }

        /// <summary>人差し指と小指の付け根の節の向きの開き。手のひらの面に落として測る。度</summary>
        static float Spread(bool left)
        {
            var palm = BodyPoser.PalmDir(an, left);
            System.Func<HumanBodyBones, HumanBodyBones, Vector3> dir = (from, to) =>
                Vector3.ProjectOnPlane(an.GetBoneTransform(to).position - an.GetBoneTransform(from).position, palm).normalized;
            var index = dir(left ? HumanBodyBones.LeftIndexProximal : HumanBodyBones.RightIndexProximal, left ? HumanBodyBones.LeftIndexIntermediate : HumanBodyBones.RightIndexIntermediate);
            var little = dir(left ? HumanBodyBones.LeftLittleProximal : HumanBodyBones.RightLittleProximal, left ? HumanBodyBones.LeftLittleIntermediate : HumanBodyBones.RightLittleIntermediate);
            return Vector3.Angle(index, little);
        }

        /// <summary>
        /// 手首の曲げを、掌屈・背屈（正で手のひらの側へ）と、橈屈・尺屈（正で親指の側へ）に分ける。度。束ねた姿勢を 0 とする
        /// </summary>
        static void WristParts(bool left, Transform hand, ArmReach.Rest armRest, out float flex, out float deviate)
        {
            flex = deviate = 0f;
            if (!armRest.valid) return;
            // 手の骨の枠で見た、手の向きと手のひらの向き（指を曲げても変わらない）
            var fingers = Quaternion.Inverse(hand.rotation) * BodyPoser.FingerDir(an, left);
            var palm = Quaternion.Inverse(hand.rotation) * BodyPoser.PalmDir(an, left);
            // 前腕の枠で見た、今と束ねた姿勢
            var now = hand.localRotation * fingers;
            var f0 = armRest.hand * fingers;
            var p0 = armRest.hand * palm;
            var thumb0 = (left ? Vector3.Cross(p0, f0) : Vector3.Cross(f0, p0)).normalized;
            flex = Mathf.Atan2(Vector3.Dot(now, p0), Vector3.Dot(now, f0)) * Mathf.Rad2Deg;
            deviate = Mathf.Atan2(Vector3.Dot(now, thumb0), Vector3.Dot(now, f0)) * Mathf.Rad2Deg;
        }

        /// <summary>点からハンドルの面までの距離（中なら負）。BuildDrive.WheelGap（組むのと同じ箱）で、試している置き方の輪を測る</summary>
        public static float Wheel(Vector3 world)
        {
            if (parts == null) Reset();
            return BuildDrive.WheelGap(car.InverseTransformPoint(world), parts);
        }

        /// <summary>
        /// 組をすべて試し、手と腕がハンドルに入らず、計器盤の側へ回って目から隠れた指先の多い順、
        /// 手首の曲げの小さい順に top 個を返す
        /// </summary>
        public static string Search(bool left, float[] alphas, float[] betas, float[] phis, float[] sfs, float[] dps, float[] pxs, float[] pys, float close, int top)
        {
            var list = new List<Result>();
            foreach (var a in alphas)
            foreach (var b in betas)
            foreach (var ph in phis)
            foreach (var sf in sfs)
            foreach (var dp in dps)
            foreach (var px in pxs)
            foreach (var py in pys)
                list.Add(Try(left, new Grip { alpha = a, beta = b, phi = ph, sf = sf, dp = dp, px = px, py = py, close = close }, null));
            list.Sort((u, w) =>
            {
                if (u.inside + u.body != w.inside + w.body) return (u.inside + u.body).CompareTo(w.inside + w.body);
                if (u.far + u.hidden != w.far + w.hidden) return (w.far + w.hidden).CompareTo(u.far + u.hidden);
                return Mathf.Max(Mathf.Abs(u.flex), Mathf.Abs(u.deviate)).CompareTo(Mathf.Max(Mathf.Abs(w.flex), Mathf.Abs(w.deviate)));
            });
            var sb = new StringBuilder();
            for (var i = 0; i < list.Count && i < top; i++) sb.AppendLine(list[i].ToString());
            return sb.ToString();
        }

        /// <summary>
        /// 両手を置いて、ゲームの見え方で下へ pitches 度と、手の寄り（確認用）を撮る。絵の名は {tag}_{角度}.png、
        /// {tag}_hand.png（上から両手）と、右手の寄り {tag}_eye.png（目から）・_top.png（上から）・_side.png（外の横から）・_front.png（計器盤の側から）
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
            var result = BodyStudy.Shoot(tag, 0f, BuildDrive.EyeLead, pitches);
            var cam = BodyStudy.MakeCamera(GameObject.Find("Player/Main Camera").GetComponent<Camera>());
            try
            {
                cam.nearClipPlane = 0.01f;
                // 運転席の上から両手
                cam.fieldOfView = 50f;
                cam.transform.position = car.TransformPoint(new Vector3(0.38f, 1.62f, 0.14f));
                cam.transform.LookAt(car.TransformPoint(Center));
                Grab(cam, tag + "_hand.png", 640, 360);
                // 右手の握った所（手の骨から指の付け根の少し先）を、目から・上から・横から・計器盤の側から
                var hand = an.GetBoneTransform(HumanBodyBones.RightHand);
                var grip = hand.position + BodyPoser.FingerDir(an, false) * 0.08f;
                var views = new[]
                {
                    new { name = "_eye", at = player.TransformPoint(new Vector3(0f, 0f, BuildDrive.EyeLead)), fov = 20f },
                    new { name = "_top", at = grip + car.TransformDirection(new Vector3(0.02f, 0.40f, -0.12f)), fov = 24f },
                    new { name = "_side", at = grip + car.TransformDirection(new Vector3(0.20f, 0.12f, -0.12f)), fov = 40f },
                    new { name = "_front", at = grip + car.TransformDirection(new Vector3(0.02f, 0.21f, 0.11f)), fov = 44f },
                };
                foreach (var v in views)
                {
                    cam.fieldOfView = v.fov;
                    cam.transform.position = v.at;
                    cam.transform.LookAt(grip);
                    Grab(cam, tag + v.name + ".png", 480, 360);
                }
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
