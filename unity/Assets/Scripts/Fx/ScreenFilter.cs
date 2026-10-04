using UnityEngine;

namespace HalfAware
{
    /// <summary>画面のフィルターの型。設定の「フィルター」（<see cref="GameSettings.Filter"/>）の並び</summary>
    public enum ScreenFilterKind
    {
        /// <summary>標準。色ごとに 32 段へ刻み、4×4 の規則的な点で段のあいだを埋める</summary>
        Standard = 0,

        /// <summary>減色＋ディザ。色の組の中の近い二色を、画面に貼り付いた青色雑音の点で混ぜる</summary>
        Dither = 1,
    }

    /// <summary>
    /// 画面の加工（PC_Renderer の Ps1 のパス、設計書 2.5 節）の型を切り替える。
    /// 型はシェーダーのグローバルの値 <see cref="GlobalName"/>（0 が標準、1 が減色＋ディザ）で渡す。
    /// 減色＋ディザの二つの強さ（色の寄せ <see cref="TintName"/>・点の濃さ <see cref="DotsName"/>、どちらも 0〜1）も、型と一緒に毎回グローバルの値で渡す。
    /// Ps1.mat には書かない（アセットを書き換えると、エディタで遊んだ後に変更として残る）。
    ///
    /// 遊び始めに設定の値（型と、減色の強さ・ディザの強さ）を入れ、設定の枠で動かすとその場で入れ直す（<see cref="SettingValue.Changed"/>）。
    /// 遊び終えたら標準へ戻す。グローバルの値は再生を抜けても残るので、戻さないとエディタの絵が減色のままになる。
    /// タイトルの画面の背景（前もって撮った絵）も同じ値を読んで、同じ色の組で減色する（HalfAware/FilteredPicture）
    /// </summary>
    public static class ScreenFilter
    {
        /// <summary>鍵に書く符丁。<see cref="ScreenFilterKind"/> の並び</summary>
        public static readonly string[] Ids = { "Standard", "Dither" };

        /// <summary>設定の枠に出す名。<see cref="ScreenFilterKind"/> の並び</summary>
        public static readonly string[] Labels = { "標準", "減色＋ディザ" };

        /// <summary>型を渡すグローバルの値の名。Ps1 と FilteredPicture のシェーダーが読む（Properties には置かない。置くとマテリアルの値が勝つ）</summary>
        public const string GlobalName = "_HaFilter";

        /// <summary>
        /// 色の寄せを渡すグローバルの値の名（ScreenFilter.hlsl）。元の色を、点を打たずに色の組へ寄せた色へどこまで寄せるか。
        /// 0 で元の色のまま、1 で寄せきる。設定の「減色の強さ」（<see cref="GameSettings.FilterTint"/>）
        /// </summary>
        public const string TintName = "_HaFilterTint";

        /// <summary>
        /// 点の濃さを渡すグローバルの値の名（ScreenFilter.hlsl）。点の明暗差をどこまで残すか。
        /// 0 で点を打たない、1 で色の組の二色の明暗差のまま。設定の「ディザの強さ」（<see cref="GameSettings.FilterDots"/>）
        /// </summary>
        public const string DotsName = "_HaFilterDots";

        /// <summary>
        /// 色の寄せの既定。設定の「減色の強さ」の既定と、<see cref="Use(ScreenFilterKind)"/> が入れる値。
        /// 1（寄せきる）では強すぎた（オーナー、2026-10-05「減色やディザの度合いが強すぎる」）。自室・路地裏・村の朝を段ごとに撮り比べて 0.30 にした
        /// </summary>
        public const float DefaultTint = 0.3f;

        /// <summary>
        /// 点の濃さの既定。設定の「ディザの強さ」の既定と、<see cref="Use(ScreenFilterKind)"/> が入れる値。
        /// 色の寄せ以上にする。色の寄せの方が大きいと、点に紛れていた色の組の境が面の境として出る（村の朝の空、自室の天井）。
        /// 0.45 で手本の点の模様を残す
        /// </summary>
        public const float DefaultDots = 0.45f;

        /// <summary>タイトルの画面の背景の絵を敷くマテリアル（HalfAware/FilteredPicture）。Resources の中の名</summary>
        public const string PictureMaterial = "ScreenFilterPicture";

        static int id = -1;
        static int tintId = -1;
        static int dotsId = -1;

        /// <summary>設定のいまの型</summary>
        public static ScreenFilterKind Current { get { return (ScreenFilterKind)GameSettings.Filter.Value; } }

        /// <summary>設定のいまの型と、減色の強さ・ディザの強さを画面へ効かせる</summary>
        public static void Apply()
        {
            Use(Current, GameSettings.FilterTint.Value, GameSettings.FilterDots.Value);
        }

        /// <summary>kind の型を、既定の強さ（<see cref="DefaultTint"/>・<see cref="DefaultDots"/>）で画面へ効かせる。設定は読まず、書かない（エディタで撮るとき）</summary>
        public static void Use(ScreenFilterKind kind)
        {
            Use(kind, DefaultTint, DefaultDots);
        }

        /// <summary>
        /// kind の型を、色の寄せ tint・点の濃さ dots（0〜1）で画面へ効かせる。設定は読まず、書かない
        /// （エディタで強さを撮り比べるとき。グローバルの値を入れ替えるだけなので、段ごとにシェーダーを組み直さない）
        /// </summary>
        public static void Use(ScreenFilterKind kind, float tint, float dots)
        {
            if (id < 0)
            {
                id = Shader.PropertyToID(GlobalName);
                tintId = Shader.PropertyToID(TintName);
                dotsId = Shader.PropertyToID(DotsName);
            }
            Shader.SetGlobalFloat(id, kind == ScreenFilterKind.Dither ? 1f : 0f);
            Shader.SetGlobalFloat(tintId, Mathf.Clamp01(tint));
            Shader.SetGlobalFloat(dotsId, Mathf.Clamp01(dots));
        }

        /// <summary>シェーダーのグローバルの値として、いま入っている型</summary>
        public static ScreenFilterKind InEffect
        {
            get { return Shader.GetGlobalFloat(GlobalName) > 0.5f ? ScreenFilterKind.Dither : ScreenFilterKind.Standard; }
        }

        /// <summary>遊び始め。最初の場面を読む前に、残してあった型を入れる</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Boot()
        {
            // ドメインを読み直さない設定でも、前の再生の繋ぎを重ねない
            foreach (var v in Watched)
            {
                v.Changed -= Apply;
                v.Changed += Apply;
            }
            Application.quitting -= Leave;
            Application.quitting += Leave;
            Apply();
        }

        /// <summary>動かすとその場で画面へ効かせる設定の値。型と、減色の強さ・ディザの強さ</summary>
        static SettingValue[] Watched
        {
            get { return new SettingValue[] { GameSettings.Filter, GameSettings.FilterTint, GameSettings.FilterDots }; }
        }

        static void Leave()
        {
            foreach (var v in Watched) v.Changed -= Apply;
            Application.quitting -= Leave;
            Use(ScreenFilterKind.Standard);
        }
    }
}
