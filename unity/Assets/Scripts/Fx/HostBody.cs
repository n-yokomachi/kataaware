using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace HalfAware
{
    /// <summary>
    /// 借りた体の差を目線・ぼやけ・色味・音に当てる。
    ///
    /// **繋がれていないものは黙って飛ばす。** Volume も心音のクリップも Task 8 の
    /// 組み立てが置くので、揃う前に走らせても止まらないようにしてある。
    /// 音源が無いまま止まると、場所の組み立てを見ることすらできなくなる。
    ///
    /// ぼやけは Volume の Depth of Field（Gaussian）で作る。老眼は手元が、
    /// 近視は遠くがぼやけるので、掛ける帯を前後に振り分けている。
    /// 値はオーナーが決めるので仮置き
    /// </summary>
    public sealed class HostBody : MonoBehaviour
    {
        /// <summary>素の耳。Hz</summary>
        public const float Open = 22000f;
        /// <summary>詰まった耳。Hz</summary>
        public const float Shut = 900f;

        /// <summary>老眼。手元から 1.2 m までがぼやける</summary>
        public const float NearStart = 0.2f;
        public const float NearEnd = 1.2f;
        /// <summary>近視。3 m から先がぼやけ、8 m で溶ける</summary>
        public const float FarStart = 3f;
        public const float FarEnd = 8f;

        [Tooltip("主。目の高さを入れる")]
        [SerializeField] PlayerController player;
        [Tooltip("記憶ごとのぼやけと色味を持つ Volume")]
        [SerializeField] Volume volume;
        [Tooltip("AudioListener に付けた低域通過。耳の詰まり")]
        [SerializeField] AudioLowPassFilter ear;
        [Tooltip("心音の音源。ループで鳴らす")]
        [SerializeField] AudioSource heart;
        [Tooltip("心音。無ければ黙って鳴らさない")]
        [SerializeField] AudioClip heartbeat;

        DepthOfField blur;
        ColorAdjustments tone;

        void Awake()
        {
            Borrow();
        }

        /// <summary>
        /// Volume の profile から override を引く。
        /// profile を持たない Volume を渡されても落ちないようにしてある
        /// </summary>
        void Borrow()
        {
            blur = null;
            tone = null;
            if (volume == null) return;
            var profile = volume.profile;
            if (profile == null) return;
            profile.TryGet(out blur);
            profile.TryGet(out tone);
        }

        /// <summary>記憶一つ分の体を着る</summary>
        public void Apply(DiveEntry entry)
        {
            if (blur == null && tone == null) Borrow();

            // 鍵打ちの eyeHeight が毎フレーム上書きするので、ここは呼ばれた直後の高さだけを決める
            if (player != null) player.EyeHeight = entry.eyeHeight;

            Blurred(entry.blur, entry.blurAmount);

            if (tone != null)
            {
                tone.active = true;
                tone.colorFilter.Override(entry.tint);
            }

            if (ear != null) ear.cutoffFrequency = Mathf.Lerp(Open, Shut, Mathf.Clamp01(entry.muffle));

            Heart(entry.heartbeat);
        }

        /// <summary>素の体へ戻す。場面を抜けるときに呼ぶ</summary>
        public void Clear()
        {
            if (player != null) player.EyeHeight = PlayerController.StandingEyeHeight;
            if (blur != null) blur.active = false;
            if (tone != null) tone.colorFilter.Override(Color.white);
            if (ear != null) ear.cutoffFrequency = Open;
            Heart(false);
        }

        void Blurred(Blur kind, float amount)
        {
            if (blur == null) return;
            if (kind == Blur.Sharp)
            {
                blur.active = false;
                return;
            }
            blur.active = true;
            blur.mode.Override(DepthOfFieldMode.Gaussian);
            blur.gaussianStart.Override(kind == Blur.Near ? NearStart : FarStart);
            blur.gaussianEnd.Override(kind == Blur.Near ? NearEnd : FarEnd);
            blur.gaussianMaxRadius.Override(Mathf.Clamp01(amount) * 1.5f);
        }

        void Heart(bool beating)
        {
            if (heart == null) return;
            var clip = heartbeat != null ? heartbeat : heart.clip;
            if (!beating || clip == null)
            {
                if (heart.isPlaying) heart.Stop();
                return;
            }
            heart.clip = clip;
            heart.loop = true;
            if (!heart.isPlaying) heart.Play();
        }
    }
}
