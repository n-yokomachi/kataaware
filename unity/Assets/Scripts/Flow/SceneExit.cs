namespace HalfAware
{
    /// <summary>
    /// 場面を終えたときの行き先。次のシーンの名前を持っていればそこへ切り替え、
    /// 空なら「続く」で止める。読み込みそのものは呼び手が行う
    /// </summary>
    public static class SceneExit
    {
        /// <summary>次のシーンへ進むか。名前が空白だけのときも進まない</summary>
        public static bool Continues(string nextScene)
        {
            return !string.IsNullOrEmpty(nextScene) && nextScene.Trim().Length > 0;
        }

        /// <summary>
        /// 切り替えの前に暗転するか。ゲームデザイン 8 節のとおり、
        /// カットは暗転を挟まない。止まる（次が無い）ときだけ暗転する
        /// </summary>
        public static bool FadesOut(string nextScene)
        {
            return !Continues(nextScene);
        }

        /// <summary>読み込むシーンの名前。前後の空白は落とす</summary>
        public static string Target(string nextScene)
        {
            return Continues(nextScene) ? nextScene.Trim() : null;
        }
    }
}
