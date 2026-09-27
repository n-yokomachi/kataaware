namespace HalfAware
{
    /// <summary>
    /// 場面 7（自室・気づき）の調べる対象。
    /// シーンと文面のアセットと演出で同じ綴りを使うので、綴りはここ 1 箇所にまとめる。
    ///
    /// 調べられるのはこの四つだけ（シナリオ設計 11 節）。寄り道させない
    /// </summary>
    public static class NoticeIds
    {
        /// <summary>モニター。庭の記憶のアクセスログ（主の端末の場所と日時）が出る</summary>
        public const string Log = "log";
        /// <summary>右の手首のインプラントジャック。場面 1 と同じ抜き方（<see cref="JackPull"/> の既定の id と同じ綴り）。抜くと立てる</summary>
        public const string Jack = "jack";
        /// <summary>玄関先のコートハンガー。場面 3 で掛けたジャケットを取って着る</summary>
        public const string Coat = "coat";
        /// <summary>ドア。二択で確かめて、ガレージ（場面 8）へ</summary>
        public const string Door = "door";

        /// <summary>これを調べると立ち上がって歩けるようになる</summary>
        public const string StandAfter = Jack;

        /// <summary>調べる対象のすべて（調べる順）</summary>
        public static readonly string[] Order = { Log, Jack, Coat, Door };

        static readonly string[] None = new string[0];

        /// <summary>
        /// 対象を選べるようになる前に済ませておく対象（組み立てがシーンの after に張る）。
        /// 一つ前を済ませるまで次は選べない。文面は前提が未達のときの文（hint）を持たないので、
        /// 前提が済むまでは印も立たず、選べもしない。
        /// ログだけは after ではなく、気づく独白を出したところで <see cref="NoticeDirector"/> が開く
        /// </summary>
        public static string[] After(string id)
        {
            switch (id)
            {
                case Jack: return new[] { Log };
                case Coat: return new[] { Jack };
                case Door: return new[] { Coat };
                default: return None;
            }
        }
    }
}
