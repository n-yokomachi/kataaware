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

        /// <summary>いま着ている体のぼやけ方と強さ。<see cref="Strain"/> が変わったら掛け直す</summary>
        Blur kind = Blur.Sharp;
        float amount;
        float strain;

        /// <summary>
        /// 目の疲れ。0 で素のまま、1 でその体のぼやけが出きる。
        ///
        /// **一人目からぼやけていては、渡り歩いた結果に見えない。** 借りた目の出来を
        /// そのまま出すと、潜った瞬間に世界が溶けて、何を見ればよいのか分からないと
        /// 差し戻された。<see cref="DiveChain.CutSize"/> と同じ歩みで 0 から 1 へ上げ、
        /// `切断` が押せるようになったところで出きるようにする
        /// </summary>
        public float Strain
        {
            get { return strain; }
            set
            {
                var next = Mathf.Clamp01(value);
                if (Mathf.Approximately(next, strain)) return;
                strain = next;
                Blurred(kind, amount);
            }
        }

        /// <summary>
        /// <see cref="DiveChain.CutSize"/> を 0〜1 の疲れに読み替える。
        ///
        /// **前半は素のまま。** `切断` が育ち始めた時点で疲れも上がる作りにしていたが、
        /// それでもまだ早いと差し戻された。半分まで育つあいだは 0 で置き、
        /// そこから `潜る` と同じ大きさになるまでで 0 から 1 へ上げる。
        /// threshold が 8 人なら、四人目までは素のまま、五人目から曇り始める
        /// </summary>
        public static float StrainOf(float cutSize)
        {
            return Mathf.Clamp01((cutSize - Quiet) / (1f - Quiet));
        }

        /// <summary>疲れが出始める `切断` の大きさ。<see cref="DiveChain.CutStart"/> と 1 の中ほど</summary>
        public const float Quiet = (DiveChain.CutStart + 1f) * 0.5f;   // 0.7

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
            kind = Blur.Sharp;
            amount = 0f;
            if (player != null) player.EyeHeight = PlayerController.StandingEyeHeight;
            if (blur != null) blur.active = false;
            if (tone != null) tone.colorFilter.Override(Color.white);
            if (ear != null) ear.cutoffFrequency = Open;
            Heart(false);
        }

        /// <summary>
        /// ぼやけを掛け直す。**疲れが 0 のあいだは掛けない。**
        ///
        /// ぼけ始める距離も疲れで動かす。強さだけを上げると、遠くが一様に濁るだけで
        /// 「目が利かなくなってきた」に読めない。疲れていないうちは遠くの遠くから、
        /// 疲れるほど手前から溶け始める
        /// </summary>
        void Blurred(Blur kind, float amount)
        {
            this.kind = kind;
            this.amount = amount;
            if (blur == null) return;
            var force = Mathf.Clamp01(amount) * strain;
            if (kind == Blur.Sharp || force <= 1e-3f)
            {
                blur.active = false;
                return;
            }
            var start = kind == Blur.Near ? NearStart : FarStart;
            var end = kind == Blur.Near ? NearEnd : FarEnd;
            blur.active = true;
            blur.mode.Override(DepthOfFieldMode.Gaussian);
            blur.gaussianStart.Override(Mathf.Lerp(Clearest, start, strain));
            blur.gaussianEnd.Override(Mathf.Lerp(Clearest + (end - start), end, strain));
            blur.gaussianMaxRadius.Override(force * 1.5f);
        }

        /// <summary>疲れが 0 に近いときのぼけ始め。ここまで遠ければ画の中にはまず入らない</summary>
        const float Clearest = 24f;

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
