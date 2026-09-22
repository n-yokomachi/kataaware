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

        // **URP の Gaussian は遠い側しかぼかせない。** 焦点までの距離を持たないので、
        // 「手元がぼやけて遠くは見える」は作れない。老眼のつもりで 0.2〜1.2 m と置いていたのは
        // 「0.2 m から先が全部ぼける」で、板も部屋も丸ごと溶けていた。
        // 二つの体の差は、ぼける向きではなくぼけ始める距離で出す。
        //
        // どちらの始まりも、肩の脇に浮く板（相手まで 0.9〜3.2 m）より手前には置かない。
        // 板は主の端末が描くもので、借りた目の出来には従わない

        /// <summary>年寄りの目。腕を伸ばした先から先がぼやけ、4 m で溶ける</summary>
        public const float NearStart = 1.6f;
        public const float NearEnd = 4f;
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
        /// Volume の profile から override を取り出す。
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

            // 記憶の中でも歩くので、目の高さを毎フレーム書き直す者はもういない。
            // ここで入れた値がその記憶のあいだ残る
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
