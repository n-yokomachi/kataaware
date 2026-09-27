using UnityEngine;

namespace HalfAware
{
    /// <summary>
    /// 目を物へ向ける角度の決まり。調べた物や話す相手を画面の真ん中へ持ってくるのに使う（<see cref="PlayerController.Face"/>）。
    ///
    /// 角度は <see cref="PlayerController"/> の Yaw・Pitch と同じ取り方: x が左右（+z が 0 で東回りの度）、
    /// y が上下（度、下向きが正）。
    ///
    /// **目は体の前へ出ている**（<c>eyeLead</c>）。体ごと回ると目の置き場も体のまわりを回るので、
    /// 今の目の置き場から測った向きへ回すと、近い物ほど真ん中から外れる。左右は体の原点から測る（<see cref="Toward"/>）
    /// </summary>
    public static class Gaze
    {
        /// <summary>from から to を見る向き。重なっていれば 0</summary>
        public static Vector2 Angles(Vector3 from, Vector3 to)
        {
            var d = to - from;
            var flat = new Vector2(d.x, d.z).magnitude;
            if (flat < 1e-6f && Mathf.Abs(d.y) < 1e-6f) return Vector2.zero;
            return new Vector2(Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg, Mathf.Atan2(-d.y, flat) * Mathf.Rad2Deg);
        }

        /// <summary>
        /// 足元 foot（体の原点）に立ち、体の向き bodyYaw、目が体から見て eye（右・上・前、m）にあるとき、
        /// target を画面の真ん中に置く向き。
        /// turnsBody なら体ごと回るので、目の置き場も一緒に回る。首だけ振る（座っている）なら目の置き場は動かない
        /// </summary>
        public static Vector2 Toward(Vector3 foot, float bodyYaw, bool turnsBody, Vector3 eye, Vector3 target)
        {
            if (!turnsBody) return Angles(foot + Quaternion.Euler(0f, bodyYaw, 0f) * eye, target);
            // 体ごと回るなら、目は体の向きの線の上（横へ eye.x ずれた所）にある。
            // 体の原点から物への向きを、横のずれのぶんだけ戻せば、回ったあとの目から物がまっすぐ前に来る。
            // 目の置き場で測って回すのを繰り返すと、近い物ほど行き過ぎと戻りを繰り返して揃わない
            var flat = new Vector2(target.x - foot.x, target.z - foot.z);
            var far = flat.magnitude;
            var yaw = bodyYaw;
            if (far > 1e-4f)
            {
                yaw = Mathf.Atan2(flat.x, flat.y) * Mathf.Rad2Deg;
                if (Mathf.Abs(eye.x) > 1e-6f) yaw -= Mathf.Asin(Mathf.Clamp(eye.x / far, -1f, 1f)) * Mathf.Rad2Deg;
            }
            var a = Angles(foot + Quaternion.Euler(0f, yaw, 0f) * eye, target);
            // 物が目より体の近くにある（目の後ろ）ときは、測ると真後ろを向く。体の向きで答える
            a.x = yaw;
            return a;
        }

        /// <summary>
        /// 目を向ける動きの進み。k は経た秒を向ける秒で割った 0〜1。動き出しと止まりをなめらかにする（ease in と ease out）。
        ///
        /// **五次の曲線にする。** SmoothStep（三次）は速さこそ端で 0 になるが、加速は端でいきなり立ち上がって、
        /// いきなり止まる。五次（6k⁵ − 15k⁴ + 10k³）は加速も端で 0 なので、じわりと動き出し、じわりと止まる
        /// </summary>
        public static float Ease(float k)
        {
            k = Mathf.Clamp01(k);
            return k * k * k * (k * (k * 6f - 15f) + 10f);
        }

        /// <summary>
        /// from から to へ、k（0〜1）だけ寄せた向き。左右は近い回り方で回す。
        /// k は呼び手が時間から決める（端を滑らかにするなら <see cref="Ease"/> を掛けて渡す）
        /// </summary>
        public static Vector2 Blend(Vector2 from, Vector2 to, float k)
        {
            k = Mathf.Clamp01(k);
            return new Vector2(Mathf.LerpAngle(from.x, to.x, k), Mathf.Lerp(from.y, to.y, k));
        }

        /// <summary>
        /// 動いている相手を追うときの一フレーム分。lag 秒でおよそ 63% 詰める。
        /// 記憶の人はこまを落として段々に動く（<see cref="PersonMotion.Fps"/>）ので、そのまま貼り付けると目が跳ねる
        /// </summary>
        public static Vector2 Chase(Vector2 now, Vector2 want, float dt, float lag)
        {
            if (lag <= 0f) return want;
            return Blend(now, want, 1f - Mathf.Exp(-Mathf.Max(0f, dt) / lag));
        }
    }
}
