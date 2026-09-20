using System;
using System.Collections.Generic;
using UnityEngine;

namespace HalfAware
{
    /// <summary>
    /// 帯 1 つぶんの空と灯り。
    ///
    /// RenderSettings はシーンにひとつしか無いので、組み立てのときに一度置くと
    /// 全部の帯が同じ時間帯になる。夜の値で置けば、朝の小麦畑まで真っ暗になる。
    /// 帯ごとに値を持って、暗転の裏で差し替えるためにここがある。
    ///
    /// Color と Vector3 を使うので UnityEngine には依るが、MonoBehaviour でも
    /// ScriptableObject でもない。DriveBand の中に素の値として並ぶだけ
    /// </summary>
    [Serializable]
    public struct DriveSky
    {
        /// <summary>背景の色。カメラの塗り潰しに入る</summary>
        public Color sky;
        /// <summary>
        /// 霧の色。**ふつうは sky と同じ色にする。** 違えると、地面が霧に溶け切った
        /// ところに横一線の継ぎ目が出る。狙って地平を描くときだけ離す
        /// </summary>
        public Color haze;
        /// <summary>霧の濃さ。0.02 の ExponentialSquared でおよそ 100 m 先が溶ける</summary>
        public float density;
        /// <summary>
        /// 靄として掛けるか。true なら Exponential、false なら ExponentialSquared。
        ///
        /// 二乗は近くをほとんど素通しにして、ある距離から一気に溶かす。夜の帯のように
        /// 「向こうが見えない」だけが要るときはこれでよい。朝靄は違って、近くの株にも
        /// 薄く白が掛かり、遠いものほど濃くなる。一乗はその掛かり方をする。
        /// 既定（false）は二乗。帯 4 だけがこれを立てている
        /// </summary>
        public bool mist;
        /// <summary>日射しの色</summary>
        public Color sun;
        /// <summary>日射しの強さ</summary>
        public float power;
        /// <summary>日射しの向き。度。Light の回転そのもの。x を小さくするほど低い位置から薙ぐ</summary>
        public Vector3 aim;
        /// <summary>環境光の上（空側）</summary>
        public Color lift;
        /// <summary>環境光の下（地面側）</summary>
        public Color ground;

        /// <summary>
        /// 環境光の水平。上と下の中ほどを取る。
        /// 三色めを別に持たせないのは、実画面で決めるつまみが増えるだけで、
        /// 上と下を決めた後に中だけ動かしたくなったことが無いため
        /// </summary>
        public Color Equator { get { return Color.Lerp(lift, ground, 0.5f); } }

        /// <summary>
        /// この帯の空と灯りに差し替える。暗転の裏で呼ぶ。
        /// 明るいところで呼ぶと、時間帯が切り替わるのがそのまま見える
        /// </summary>
        public void Apply(Light sunLight, Camera eye)
        {
            if (eye != null)
            {
                eye.clearFlags = CameraClearFlags.SolidColor;
                eye.backgroundColor = sky;
            }
            if (sunLight != null)
            {
                sunLight.color = sun;
                sunLight.intensity = power;
                sunLight.transform.rotation = Quaternion.Euler(aim);
                RenderSettings.sun = sunLight;
            }
            RenderSettings.fog = true;
            RenderSettings.fogMode = mist ? FogMode.Exponential : FogMode.ExponentialSquared;
            RenderSettings.fogColor = haze;
            RenderSettings.fogDensity = density;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = lift;
            RenderSettings.ambientEquatorColor = Equator;
            RenderSettings.ambientGroundColor = ground;
            // 空の球は使わない。濡れた面の映り込みが要る路地裏と違い、
            // ここは平らな色で塗る。階調の少ない絵に合う
            RenderSettings.skybox = null;
            // 環境光の球面調和はここで焼き直す。**書き換えただけでは次のフレームまで効かない。**
            // 帯を跨ぐのは黒のあいだなので 1 フレームの遅れは見えないが、
            // エディタで絵を撮ると 1 枚前の帯の灯りで撮れてしまい、
            // 実際そうと気づくまでに帯 4 が夜の明るさで撮れていた
            DynamicGI.UpdateEnvironment();
        }
    }

    /// <summary>
    /// 景色の帯 1 つ分。尺に関わる値はここにしか無い。
    /// 秒数はオーナーが実画面を見てから決めるので、組み立て側には仮置きしか入れない
    /// </summary>
    [Serializable]
    public struct DriveBand
    {
        /// <summary>帯の名前。ログと見直しで使う</summary>
        public string name;
        /// <summary>この帯の独白を始める対象の id。調べるまで帯は終わらない</summary>
        public string trigger;
        /// <summary>走る速さ。m/s。タイルの送りと揺れがこの一つから出る</summary>
        public float speed;
        /// <summary>路面の粗さ。1 が舗装、未舗装はもっと大きい。車体の揺れ幅に掛かる</summary>
        public float rough;
        /// <summary>独白を送り切ってから黒へ切り替わるまでの秒数。黙って走る</summary>
        public float afterglow;
        /// <summary>黒のまま置く秒数。仮眠にあたる切れ目だけ長く取る</summary>
        public float black;
        /// <summary>黒から次の帯へ浮かび上がる秒数</summary>
        public float fadeIn;
        /// <summary>
        /// この帯の空と灯り。暗転の裏で差し替わる。
        /// 秒数と同じく、ここへ置くのはオーナーが実画面を見て決めるため
        /// </summary>
        public DriveSky sky;
    }

    /// <summary>
    /// 帯の並び。順送りと、きっかけの id からの引き当てだけを持つ。
    /// 範囲の外を渡されても落ちない。組み立ての途中で帯が空のことがある
    /// </summary>
    public sealed class DriveRoute
    {
        readonly DriveBand[] bands;

        public DriveRoute(IReadOnlyList<DriveBand> from)
        {
            if (from == null) { bands = new DriveBand[0]; return; }
            bands = new DriveBand[from.Count];
            for (var i = 0; i < from.Count; i++) bands[i] = from[i];
        }

        public int Count { get { return bands.Length; } }

        /// <summary>i 番目の帯。範囲の外なら空の帯</summary>
        public DriveBand At(int i)
        {
            return i >= 0 && i < bands.Length ? bands[i] : new DriveBand();
        }

        /// <summary>
        /// i が最後の帯か。帯がひとつも無いうちは最後にしない。
        /// 組み立て途中の場面が、入った瞬間に閉じてしまうのを防ぐ（SceneProgress.IsComplete と同じ構え）
        /// </summary>
        public bool IsLast(int i)
        {
            return bands.Length > 0 && i >= bands.Length - 1;
        }

        /// <summary>そのきっかけの id を持つ帯。どの帯のものでもなければ -1</summary>
        public int BandOf(string trigger)
        {
            if (string.IsNullOrEmpty(trigger)) return -1;
            for (var i = 0; i < bands.Length; i++)
                if (bands[i].trigger == trigger) return i;
            return -1;
        }
    }
}
