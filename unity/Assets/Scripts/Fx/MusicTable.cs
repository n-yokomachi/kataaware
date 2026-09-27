using System;
using UnityEngine;

namespace HalfAware
{
    /// <summary>
    /// 場面ごとの BGM の呼び名。<see cref="MusicBed"/> にはこの名で頼む。どの曲を当てるかは <see cref="MusicTable"/> が持つ。
    /// **番号はアセットに書かれる。** 並べ替えず、足す時は新しい番号で足す
    /// </summary>
    public enum MusicCue
    {
        /// <summary>何も流さない</summary>
        None = 0,
        /// <summary>場面 4 の前半。潜って最初の記憶から、切断が押せるようになるまで</summary>
        Dive = 1,
        /// <summary>場面 4 の後半。切断が押せるようになってから、切断するまで</summary>
        DiveLate = 2,
        /// <summary>場面 6 の庭の記憶。記憶の頭から、途切れるまで</summary>
        Garden = 3,
        /// <summary>場面 7 の気づき。気づく独白から、ドアを出て暗転しきるまで（場面 8 へは持ち越さない）</summary>
        Notice = 4,
        /// <summary>場面 8。メモリーチップの独白から、村へ着く暗転のドアの音まで</summary>
        Drive = 5,
        /// <summary>場面 9。村のシーンに入ってから（場面 6 の夕方の庭では鳴らさない）</summary>
        Village = 6,
        /// <summary>場面 10 の対面。卓の前で場面 10 に替わった所から、最後の独白の後の暗転まで</summary>
        Reunion = 7,
        /// <summary>エンディング（クレジット）。まだ曲を当てていない</summary>
        Ending = 8,
    }

    /// <summary>
    /// BGM の割り当て（音楽の設計書 5 節）。**曲はこの一つのアセット（Resources/MusicTable）だけが持つ。**
    /// 場面の演出は <see cref="MusicCue"/> の名で <see cref="MusicBed"/> に頼むだけで、シーンに曲も部品も置かない。
    /// 曲を差し替える・大きさを耳で決める時はこのアセットをインスペクターで触る。再生中に触っても、次に鳴らした時から効く
    /// </summary>
    [CreateAssetMenu(menuName = "HalfAware/Music Table", fileName = "MusicTable")]
    public sealed class MusicTable : ScriptableObject
    {
        /// <summary>Resources の中の名</summary>
        public const string Path = "MusicTable";

        /// <summary>
        /// 曲ごとの大きさの既定。ファイルはどれも −22 LUFS に揃えてあり（<c>tools/make-ambience.sh</c> の 9 節）、
        /// 0.5（−6dB）で −28 LUFS ほど。村の朝・麦の風・雑踏の輪（−22 LUFS の素材を 0.4〜0.6 で鳴らしている）と同じ所で、
        /// 車の走行の輪（−18〜−23 LUFS をほぼ 1 で鳴らしている）よりは下。台詞と環境音の下に敷く
        /// </summary>
        public const float DefaultVolume = 0.5f;
        /// <summary>何も鳴っていない所から入る秒の既定</summary>
        public const float DefaultFadeIn = 3f;
        /// <summary>ほかの曲から渡る秒の既定</summary>
        public const float DefaultCrossFade = 6f;

        /// <summary>曲一つ</summary>
        [Serializable]
        public sealed class Track
        {
            public MusicCue cue;
            [Tooltip("流す曲。空なら何も鳴らさない")]
            public AudioClip clip;
            [Tooltip("大きさ。ファイルはどれも −22 LUFS に揃えてある")]
            [Range(0f, 1f)] public float volume = DefaultVolume;
            [Tooltip("何も鳴っていない所から入る秒")]
            public float fadeIn = DefaultFadeIn;
            [Tooltip("ほかの曲から渡る秒。前の曲はこの秒で消え、この曲はこの秒で上がる（電力を保つ形）")]
            public float crossFade = DefaultCrossFade;
            [Tooltip("尻から頭へ戻って流し続けるか。曲のファイルは継ぎ目を作ってある")]
            public bool loop = true;
        }

        [Tooltip("曲の割り当て。同じ呼び名が二つあれば上の方を使う")]
        public Track[] tracks = new Track[0];

        [Tooltip("入った時に Village の曲をフェードインで流すシーンの名。場面 6（夕方の庭の記憶）として開いた時は流さない")]
        public string villageScene = "Village";

        /// <summary>呼び名の曲。当てていなければ null（曲が空の行も null）</summary>
        public Track Find(MusicCue cue)
        {
            if (cue == MusicCue.None || tracks == null) return null;
            foreach (var t in tracks)
                if (t != null && t.cue == cue) return t.clip != null ? t : null;
            return null;
        }

        /// <summary>
        /// シーンに入った時に流す曲。村（villageScene）に入って、場面 6（夕方の庭の記憶）として開いていなければ <see cref="MusicCue.Village"/>。
        /// ほかは何も流さない（場面ごとの曲は、その場面の演出が決めた所で頼む）
        /// </summary>
        public static MusicCue Arrival(string scene, bool garden, string villageScene)
        {
            if (garden || string.IsNullOrEmpty(scene) || string.IsNullOrEmpty(villageScene)) return MusicCue.None;
            return string.Equals(scene, villageScene, StringComparison.Ordinal) ? MusicCue.Village : MusicCue.None;
        }
    }
}
