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

        // ハンドルを握る手の置き方。輪の 9 時と 3 時を、ふつうの車と同じく横から握る。
        // 輪は手のひらを斜めに渡り（人差し指の付け根から小指の側の手首へ）、四本の指はそろえて輪の外周から計器盤の側へ回り込み、
        // 指先は輪の向こうに隠れる。親指は輪の運転席の側の面に沿う。指は輪に触れるまで曲げ（BodyPoser.Wrap）、関節の限りまでは曲げない。
        // 値は、手と腕の頂点がハンドルへ入らず、四本の指先が計器盤の側へ回って目から隠れ、
        // 手首の曲げが小さい所を探して決めた（Study/GripStudy）

        /// <summary>握る所の、輪の 12 時からの角。度。右手は 3 時の側、左手は 9 時の側へ</summary>
        const float GripClock = 90f;
        /// <summary>手の向き（手首から中指の付け根へ）と、輪に沿って 12 時へ向かう向きのなす角。度。90 で輪に直に交わる</summary>
        const float GripCross = 60f;
        /// <summary>手のひらが輪の断面のどこに当たるか。度。輪の面から運転席の側を 0、輪の外周の側を 90 として、外周から少し計器盤の側</summary>
        const float GripAround = 105f;
        /// <summary>輪の芯を、手の骨（手首）から手の向きへ離す量。指の付け根（手首から 8.7 cm）の手前、手のひらの指寄りに輪が来る</summary>
        const float GripReach = 0.07f;
        /// <summary>輪の芯を、手の骨から手のひらの側へ離す量。手の骨から手のひらの面まで（2.5 cm）と輪の太さの半分</summary>
        const float GripDepth = 0.042f;
        /// <summary>肘を寄せる所。運転席の真ん中から横へ、と高さ。肘は軽く曲げて体の横へ下ろす</summary>
        const float GripElbow = 0.35f;
        const float GripElbowY = 0.95f;
        /// <summary>握る前に指をそろえる割合（<see cref="BodyPoser.Close"/>）。立ちの形の指は開いている</summary>
        const float GripClose = 0.35f;
        /// <summary>
        /// 握りの指の曲げの限り（付け根・中・先、まっすぐな指からの度）。握る前に指をまっすぐへ戻し、輪に触れるまで曲げる。
        /// 触れないまま曲がる関節もここで止め、関節の限り（85・100・70）まで曲げた鉤爪にしない
        /// </summary>
        static readonly Vector3 GripFingerMost = new Vector3(70f, 85f, 40f);
        /// <summary>握りの親指の、関節ごとの曲げの限り。度</summary>
        const float GripThumbMost = 30f;

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
                note.AppendFormat("腕組みの形: ハンドルの中へ入った体の頂点 {0} 個", InWheel(her, car)).AppendLine();
                var hips = car.InverseTransformPoint(folded.hips);
                note.AppendFormat("運転席: 腰の骨 ({0:0.000}, {1:0.000}, {2:0.000})、目のずれ {3:0.0} mm、尻の下 {4:0.000}（座面 {5:0.000}）",
                    hips.x, hips.y, hips.z, Vector3.Distance(BodyPoser.Eyes(an), eye) * 1000f, Lowest(her, car), DriverSeatTop).AppendLine();

                var wheel = Sit(car, true);
                wheel.hips = folded.hips;
                PlaceProtagonist.SeatAtEye(an, wheel, eye);
                var skin = Skin(her);
                System.Func<Vector3, float> gap = w => WheelGap(car.InverseTransformPoint(w));
                for (var k = 0; k < 2; k++)
                {
                    BodyPoser.Close(an, k == 0, GripClose);
                    BodyPoser.Wrap(an, skin, k == 0, gap, GripFingerMost, GripThumbMost, null, true);
                }
                BodyPoser.Write(pose, "alternate", BodyPoser.Capture(an));
                note.AppendFormat("ハンドルの形: ハンドルの中へ入った体の頂点 {0} 個", InWheel(her, car)).AppendLine();
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
        /// - 腕組み: 右の前腕を上、左を下に重ね、手は反対の肘の手前。組んだ腕は輪の下をくぐる（ハンドルまで 5 mm）。
        ///   左右の腕が互いに潜らず、前腕と手が胴へ潜らない所を探して決めた
        /// - ハンドル: 輪の 9 時と 3 時を横から握る（置き方は GripClock ほか）
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
            // 輪の面から運転席の側へ向く向きと、12 時の向き
            var face = tilt * Vector3.back;
            var twelve = tilt * Vector3.up;
            var clock = GripClock * Mathf.Deg2Rad;
            var cross = GripCross * Mathf.Deg2Rad;
            var around = GripAround * Mathf.Deg2Rad;
            for (var k = 0; k < 2; k++)
            {
                var side = k == 0 ? 1f : -1f;
                // 輪の芯の上の握る所と、そこから外周への向き・12 時へ輪に沿う向き
                var rim = WheelAt + WheelRing * (side * Mathf.Sin(clock) * Vector3.right + Mathf.Cos(clock) * twelve);
                var outward = (rim - WheelAt).normalized;
                var along = (-side * Mathf.Cos(clock) * Vector3.right + Mathf.Sin(clock) * twelve).normalized;
                // 手のひらが当たる向き（輪の芯から見て）と、そこから指が輪に巻き付いていく向き
                var contact = Mathf.Cos(around) * face + Mathf.Sin(around) * outward;
                var wrap = -Mathf.Sin(around) * face + Mathf.Cos(around) * outward;
                var fingers = (Mathf.Cos(cross) * along + Mathf.Sin(cross) * wrap).normalized;
                var palm = Vector3.ProjectOnPlane(-contact, fingers).normalized;
                var wrist = car.TransformPoint(rim - fingers * GripReach - palm * GripDepth);
                var pole = P(x0 + side * GripElbow, GripElbowY, 0.10f);
                if (side > 0f) { s.wristR = wrist; s.elbowPoleR = pole; s.fingersR = D(fingers); s.palmR = D(palm); }
                else { s.wristL = wrist; s.elbowPoleL = pole; s.fingersL = D(fingers); s.palmL = D(palm); }
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

        /// <summary>体の頂点で、ハンドル（輪・芯・警笛の押し・輻）の中に入っているものの数</summary>
        static int InWheel(GameObject her, Transform car)
        {
            var count = 0;
            foreach (var p in Baked(her))
                if (WheelGap(car.InverseTransformPoint(p)) < 0f) count++;
            return count;
        }

        /// <summary>体の肌（頭の影でない方）</summary>
        static SkinnedMeshRenderer Skin(GameObject her)
        {
            foreach (var smr in her.GetComponentsInChildren<SkinnedMeshRenderer>())
                if (smr.shadowCastingMode != UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly) return smr;
            return null;
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
