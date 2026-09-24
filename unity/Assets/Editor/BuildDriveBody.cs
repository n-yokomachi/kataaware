using System.Text;
using UnityEditor;
using UnityEngine;

namespace HalfAware.EditorTools
{
    public static partial class BuildDrive
    {
        // ---- 主人公の体 ------------------------------------------------------
        //
        // **一つの繋がった体。** 場面 1・2 と同じ主人公（PlaceProtagonist.Body）を Player の下に置く。
        // ガレージでは立って歩き、運転席に着いたら座った形になる。頭は一人称のカメラに映さない。
        //
        // 座った形は二つ。帯 0〜1 は腕組み（原作「自動運転に任せて私は腕組みをしながら考える」）、
        // 最後の帯だけハンドルに手を乗せる。どちらも見た目だけで、操作には繋がらない。
        // 出し分けるのは DriveDirector

        /// <summary>
        /// 運転席の座面の上面。**助手席（1.05）より 0.265 低い。**
        ///
        /// 目（<see cref="SeatAt"/> の 1.55）は計器盤・天井・道の見え方がすべてこの高さから割り出して
        /// あるので動かせない。主人公の体をその目に合わせて座らせると、腰の骨が 0.87 まで下がり、
        /// 尻の下は 0.75 になる。助手席と同じ 1.05 の座面では腰が座面の中へ沈み、腿も膝もハンドルと
        /// 計器盤に刺さる。座面を床（<see cref="HoldFloorY"/>）に据えて、尻が 3 cm ほど沈む高さにした（場面 1 の椅子と同じくらい）
        /// </summary>
        public const float DriverSeatTop = 0.785f;

        /// <summary>背を前へ倒す角。度。腕を組んで考え込む背の丸まり。ハンドルの形も同じ背で解く（帯の切り替えで目が動かない）</summary>
        const float DriveLean = 20f;

        /// <summary>腕組みの手の、指の曲げの割合（<see cref="BodyPoser.Grip"/>）。力の抜けた手</summary>
        const float FoldCurl = 0.35f;

        // ハンドルに乗せた手の置き方。輪の三時と九時で、指の一節目を輪の上に載せ、
        // 指を関節の限りまで曲げて輪の内側へ回す。値は手の頂点が輪の中へ入らない所を探して決めた
        // （両手とも 0 個、輪の面との隙間 0.7 mm）

        /// <summary>手の骨（手首）を輪の芯から輪の面の向きへ上げる量</summary>
        const float GripAbove = 0.045f;
        /// <summary>手首を輪の芯から指の向きの逆へ下げる量。指の付け根の関節（手首から 9.4 cm）の少し先に輪が来る</summary>
        const float GripBack = 0.12f;
        /// <summary>指の向きを、輪に沿った向きから内側へ寄せる割合。大きいほど輪の中心を指す</summary>
        const float GripInward = 4f;
        /// <summary>握りの指の曲げの割合（<see cref="BodyPoser.Grip"/>）。四本の指は関節の限りまで曲がる</summary>
        const float WheelCurl = 2.6f;

        /// <summary>
        /// 主人公の体を Player の下に作る。組み上がった場面では立った形（ガレージに立っている）。
        /// 運転席の二つの形は、乗り込んだ後の置き方のまま解いて SeatedPose へ書く。
        ///
        /// **乗り込むと Player の根は目の位置へ上がる**（DriveDirector.Boarding が目の高さを 0 にする）。
        /// 体はその分だけ根を下げて地に残す。座った形は、Player を運転席の目（Car/Seat）に置き、
        /// 体の根を立った根から <see cref="PlayerController.StandingEyeHeight"/> 下げた所で解く
        /// </summary>
        static void Protagonist(Transform root)
        {
            var player = GameObject.Find("Player");
            var seat = Look(root, "Car/Seat");
            if (player == null || seat == null)
            {
                Debug.LogWarning("Player か Car/Seat が無いので主人公を置かない");
                return;
            }
            var note = new StringBuilder();
            var eyeLead = PlaceProtagonist.EyeLead(player.transform);
            var her = PlaceProtagonist.Body(player.transform, eyeLead, note);
            var an = her.GetComponent<Animator>();
            var stood = her.transform.localPosition;

            var pose = her.AddComponent<SeatedPose>();
            var pso = new SerializedObject(pose);
            pso.FindProperty("animator").objectReferenceValue = an;
            pso.ApplyModifiedPropertiesWithoutUndo();

            var keepAt = player.transform.position;
            var keepTurn = player.transform.rotation;
            try
            {
                player.transform.SetPositionAndRotation(seat.position, Quaternion.Euler(0f, seat.eulerAngles.y, 0f));
                her.transform.localPosition = stood + Vector3.down * PlayerController.StandingEyeHeight;
                var eye = player.transform.TransformPoint(new Vector3(0f, 0f, eyeLead));
                var car = seat.parent;

                var folded = Sit(car, false);
                folded.hips = PlaceProtagonist.SeatAtEye(an, folded, eye);
                BodyPoser.Grip(an, true, FoldCurl);
                BodyPoser.Grip(an, false, FoldCurl);
                BodyPoser.Write(pose, "seated", BodyPoser.Capture(an));
                var hips = car.InverseTransformPoint(folded.hips);
                note.AppendFormat("運転席: 腰の骨 ({0:0.000}, {1:0.000}, {2:0.000})、目のずれ {3:0.0} mm、尻の下 {4:0.000}（座面 {5:0.000}）",
                    hips.x, hips.y, hips.z, Vector3.Distance(BodyPoser.Eyes(an), eye) * 1000f, Lowest(her, car), DriverSeatTop).AppendLine();

                var wheel = Sit(car, true);
                wheel.hips = folded.hips;
                PlaceProtagonist.SeatAtEye(an, wheel, eye);
                BodyPoser.Grip(an, true, WheelCurl);
                BodyPoser.Grip(an, false, WheelCurl);
                BodyPoser.Write(pose, "alternate", BodyPoser.Capture(an));
                note.AppendFormat("ハンドルの形: 輪の中へ入った体の頂点 {0} 個", InRim(her, car)).AppendLine();
            }
            finally
            {
                player.transform.SetPositionAndRotation(keepAt, keepTurn);
                her.transform.localPosition = stood;
                BodyPoser.Stand(an);
            }
            Debug.Log("主人公を置いた。" + note.ToString().TrimEnd().Replace("\r", "").Replace("\n", " / "));
        }

        /// <summary>
        /// 運転席の座った形の狙い。値は車から見た位置。
        /// - 腰: 目が運転席の目（SeatAt + EyeLead）に来る所を解く（<see cref="PlaceProtagonist.SeatAtEye"/>）。ここの値は探し始め
        /// - 脚: 座面が低いので足を前へ投げ出し、ほとんど伸ばして踵を床に着ける。
        ///   膝を立てると、脛の上が計器盤の下の面（0.98）に 3 cm 刺さる。伸ばすと脚の上は 0.92 に収まる
        /// - 腕組み: 右の前腕を上、左を下に重ね、手は反対の肘の手前。組んだ腕は輪の下（輪の面まで 7 mm）をくぐる。
        ///   左右の腕が互いに潜らず、前腕と手が胴へ潜らない所を探して決めた
        /// - ハンドル: 輪の三時と九時を握る
        /// </summary>
        static BodyPoser.Sit Sit(Transform car, bool wheel)
        {
            System.Func<float, float, float, Vector3> P = (x, y, z) => car.TransformPoint(new Vector3(x, y, z));
            System.Func<Vector3, Vector3> D = v => car.TransformDirection(v);
            var x0 = SeatAt.x;
            var s = new BodyPoser.Sit
            {
                hips = P(x0, 0.87f, 0.06f),
                pelvis = 0f,
                lean = DriveLean,
                headKeep = 1f,
                ankleL = P(x0 - 0.10f, HoldFloorY + 0.115f, 0.87f),
                ankleR = P(x0 + 0.10f, HoldFloorY + 0.115f, 0.87f),
                kneePoleL = P(x0 - 0.14f, 1.4f, 1.2f),
                kneePoleR = P(x0 + 0.14f, 1.4f, 1.2f),
                footPoint = -10f,
            };
            if (!wheel)
            {
                // 右の手は左の肘の手前へ。肘の上から被せると指が左の腕へ潜る
                s.wristR = P(x0 - 0.06f, 1.145f, 0.31f);
                s.elbowPoleR = P(x0 + 0.37f, 0.95f, 0.35f);
                s.fingersR = D(new Vector3(-1f, -0.5f, 0.1f));
                s.palmR = D(new Vector3(0f, -0.5f, -1f));
                s.wristL = P(x0 + 0.10f, 1.05f, 0.215f);
                s.elbowPoleL = P(x0 - 0.38f, 0.95f, 0.35f);
                s.fingersL = D(new Vector3(1f, 0.2f, -0.3f));
                s.palmL = D(new Vector3(0f, 0.3f, -1f));
                return s;
            }
            var tilt = Quaternion.Euler(WheelLean, 0f, 0f);
            var up = tilt * Vector3.back;
            var along = tilt * Vector3.up;
            for (var k = 0; k < 2; k++)
            {
                var side = k == 0 ? 1f : -1f;
                var rim = WheelAt + Vector3.right * side * WheelRing;
                var fingers = (along - Vector3.right * side * GripInward).normalized;
                var wrist = car.TransformPoint(rim + up * GripAbove - fingers * GripBack);
                var pole = P(x0 + side * 0.67f, 0.95f, 0.10f);
                if (side > 0f) { s.wristR = wrist; s.elbowPoleR = pole; s.fingersR = D(fingers); s.palmR = D(-up); }
                else { s.wristL = wrist; s.elbowPoleL = pole; s.fingersL = D(fingers); s.palmL = D(-up); }
            }
            return s;
        }

        /// <summary>体の頂点で、運転席の座面の上（腰の下）にあるものの一番低い高さ。車から見た値</summary>
        static float Lowest(GameObject her, Transform car)
        {
            var low = float.MaxValue;
            foreach (var p in Baked(her))
            {
                var c = car.InverseTransformPoint(p);
                if (Mathf.Abs(c.x - SeatAt.x) > 0.27f || c.z < -0.19f || c.z > 0.31f) continue;
                if (c.y < low) low = c.y;
            }
            return low;
        }

        /// <summary>体の頂点で、ハンドルの輪（芯から太さの半分より内）に入っているものの数</summary>
        static int InRim(GameObject her, Transform car)
        {
            var tilt = Quaternion.Euler(WheelLean, 0f, 0f);
            var up = tilt * Vector3.back;
            var count = 0;
            foreach (var p in Baked(her))
            {
                var d = car.InverseTransformPoint(p) - WheelAt;
                var flat = d - up * Vector3.Dot(d, up);
                if (flat.sqrMagnitude < 1e-8f) continue;
                if (Vector3.Distance(d, flat.normalized * WheelRing) < WheelThick * 0.5f) count++;
            }
            return count;
        }

        /// <summary>体（頭の影を除く）の今の姿勢の頂点を、世界の位置で</summary>
        static Vector3[] Baked(GameObject her)
        {
            foreach (var smr in her.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                if (smr.shadowCastingMode == UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly) continue;
                var mesh = new Mesh();
                try
                {
                    smr.BakeMesh(mesh, true);
                    var v = mesh.vertices;
                    for (var i = 0; i < v.Length; i++) v[i] = smr.transform.TransformPoint(v[i]);
                    return v;
                }
                finally
                {
                    Object.DestroyImmediate(mesh);
                }
            }
            return new Vector3[0];
        }
    }
}
