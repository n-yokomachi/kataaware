using System;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace HalfAware.EditorTools
{
    /// <summary>
    /// 画面のフィルター（設定の「フィルター」、<see cref="ScreenFilter"/>）を場面ごとに撮り比べる。
    /// 自室・路地裏・村の朝・記憶の団地・車の中を、同じ目から「標準」と「減色＋ディザ」で一枚ずつ撮り、左右に並べた一枚も作る。
    ///
    /// 撮り方はタイトルの画面の背景（<see cref="TitleShots"/>）と同じ。場面の Player/Main Camera を写したカメラで 960×540 に撮るので、
    /// パイプラインの render scale 1/3 と Ps1 のパス、最近傍の引き伸ばしまでゲームと同じに通る。
    /// 型はシェーダーのグローバルの値で切り替え、撮り終えたら標準へ戻す（遊んでいない間のエディタは標準）。
    /// 設定の枠のフィルターの行（コンソールとタイトルの画面）も、標準と減色＋ディザの二通りで撮る（<see cref="Panels"/>）。
    /// 減色＋ディザの強さ（色の寄せ・点の濃さ）の段も、同じ目から撮り比べる（<see cref="Strength"/>。HalfAware/Shoot the filter strengths）。
    ///
    /// **場面は開くが保存しない。** 撮る前に開いていた場面へ、撮り終えたら開き直す（保存されていない場面だったなら、カメラと灯りだけの新しい場面を作る）。
    /// 開いている場面に未保存の変更があるときは撮らない
    /// </summary>
    public static class FilterShots
    {
        enum Way { Game, Dive, Drive }

        sealed class Spot
        {
            public string Name;
            public string Scene;
            public CheckVillage.View View;
            public Way Way;
        }

        /// <summary>撮る所。場面ごとに一つ</summary>
        static Spot[] Spots()
        {
            var lane = CheckVillage.Views()[2];
            return new[]
            {
                new Spot { Name = "room", Scene = "Room", View = TitleShots.ViewOf(TitleBackdrop.Room), Way = Way.Game },
                new Spot { Name = "alley", Scene = "Alley", View = TitleShots.ViewOf(TitleBackdrop.Alley), Way = Way.Game },
                // 路地の途中から東へ。電話ボックスと茅葺きと朝の空
                new Spot { Name = "village", Scene = "Village", View = lane, Way = Way.Game },
                new Spot { Name = "estate", Scene = "Dive", View = TitleShots.ViewOf(TitleBackdrop.Dive), Way = Way.Dive },
                // 運転席の目から前を。帯 0（倫敦の市街の夜、雨）を当てる
                new Spot { Name = "car", Scene = "Drive", View = new CheckVillage.View("car", BuildDrive.SeatAt + new Vector3(0f, 0f, BuildDrive.EyeLead), 0f, 4f), Way = Way.Drive },
            };
        }

        [MenuItem("HalfAware/Shoot the screen filters", false, 186)]
        public static void Menu()
        {
            var dir = Path.Combine(ConsoleShot.Scratch, "filter");
            Debug.Log(Shoot(dir, null) + "\n" + Panels(dir));
        }

        /// <summary>
        /// 全部の所を、標準と減色＋ディザ（既定の強さ）で撮って dir に置く（「名前_standard.png」「名前_dither.png」と、左右に並べた「名前_pair.png」）。
        /// only を渡すとその名前の所だけ
        /// </summary>
        public static string Shoot(string dir, string only)
        {
            return Run(dir, Only(only), false, s => ShootPair(s, dir));
        }

        /// <summary>Ps1 のパスを素通しにして撮る（減色の前の色）。中の 320×180 を「名前_raw.png」で置く。色の組を決めるとき使う</summary>
        public static string Raw(string dir, string only)
        {
            return Run(dir, Only(only), true, s => ShootRaw(s, dir));
        }

        /// <summary>減色＋ディザの強さの段。x が色の寄せ、y が点の濃さ。(1, 1) が強さを分ける前と同じ絵</summary>
        public static readonly Vector2[] StrengthSteps =
        {
            new Vector2(1f, 1f),
            new Vector2(0.6f, 0.6f),
            new Vector2(0.45f, 0.45f),
            new Vector2(0.3f, 0.3f),
            new Vector2(0.3f, 0.6f),
            new Vector2(0.6f, 0.3f),
        };

        /// <summary>強さを撮り比べる所。自室・路地裏・村の朝</summary>
        public static readonly string[] StrengthSpots = { "room", "alley", "village" };

        [MenuItem("HalfAware/Shoot the filter strengths", false, 187)]
        public static void StrengthMenu()
        {
            Debug.Log(Strength(Path.Combine(ConsoleShot.Scratch, "filter_strength"), StrengthSteps, StrengthSpots));
        }

        /// <summary>
        /// 減色＋ディザの強さを撮り比べる。names の所を、同じ目から標準と、steps の段ごとの減色＋ディザで撮って dir に置く
        /// （「名前_standard.png」「名前_t060_d030.png」。数は色の寄せと点の濃さの百分率）。
        /// 強さはグローバルの値を直に入れ替える（<see cref="ScreenFilter.Use(ScreenFilterKind, float, float)"/>）ので、段ごとにシェーダーを組み直さない
        /// </summary>
        public static string Strength(string dir, Vector2[] steps, params string[] names)
        {
            return Run(dir, names, false, s => ShootSteps(s, dir, steps));
        }

        /// <summary>段の絵の名。「名前_t060_d030」</summary>
        public static string StepName(string spot, Vector2 step)
        {
            return string.Format("{0}_t{1:000}_d{2:000}", spot, Mathf.RoundToInt(step.x * 100f), Mathf.RoundToInt(step.y * 100f));
        }

        static string[] Only(string only)
        {
            return string.IsNullOrEmpty(only) ? null : new[] { only };
        }

        /// <summary>names の所（null なら全部）を、場面を開いて shoot で撮る。撮り終えたら型を標準へ戻し、撮る前の場面へ戻す</summary>
        static string Run(string dir, string[] names, bool raw, Func<Spot, string> shoot)
        {
            if (EditorApplication.isPlaying) return "再生中は撮らない";
            var busy = Busy();
            if (busy != null) return busy;
            var setup = EditorSceneManager.GetSceneManagerSetup();
            var sb = new StringBuilder();
            var async = ShaderUtil.allowAsyncCompilation;
            var ps1 = AssetDatabase.LoadAssetAtPath<Material>(BuildScreenFilter.Ps1Path);
            var amount = ps1 != null ? ps1.GetFloat("_Amount") : 1f;
            Directory.CreateDirectory(dir);
            try
            {
                ShaderUtil.allowAsyncCompilation = false;
                if (raw && ps1 != null) ps1.SetFloat("_Amount", 0f);
                string open = null;
                foreach (var s in Spots())
                {
                    if (names != null && Array.IndexOf(names, s.Name) < 0) continue;
                    if (open != s.Scene)
                    {
                        var scene = EditorSceneManager.OpenScene("Assets/Scenes/" + s.Scene + ".unity", OpenSceneMode.Single);
                        if (!scene.IsValid()) { sb.AppendLine(s.Name + ": 開けない"); continue; }
                        open = s.Scene;
                        if (s.Scene == "Village") BuildVillage.SetHour(VillageHour.Hour.Morning);
                        if (s.Way == Way.Drive) Board();
                    }
                    sb.AppendLine(shoot(s));
                }
            }
            catch (Exception e)
            {
                sb.AppendLine("例外: " + e);
            }
            finally
            {
                if (ps1 != null && raw) ps1.SetFloat("_Amount", amount);
                // エディタでは標準に戻す（遊んでいない間は、設定の値に関わらず標準。ScreenFilter）
                ScreenFilter.Use(ScreenFilterKind.Standard);
                ShaderUtil.allowAsyncCompilation = async;
                Back(setup);
                EditorUtility.UnloadUnusedAssetsImmediate();
            }
            return sb.ToString().TrimEnd();
        }

        static string ShootRaw(Spot s, string dir)
        {
            var shot = Take(s);
            if (shot == null) return s.Name + ": カメラが無い";
            try
            {
                int loose;
                var small = TitleShots.Shrink(shot, out loose);
                try
                {
                    var path = Path.Combine(dir, s.Name + "_raw.png");
                    CheckDiveSky.Save(small, path);
                    return s.Name + " → " + path;
                }
                finally
                {
                    Object.DestroyImmediate(small);
                }
            }
            finally
            {
                Object.DestroyImmediate(shot);
            }
        }

        static string ShootPair(Spot s, string dir)
        {
            Texture2D a = null, b = null, pair = null;
            try
            {
                ScreenFilter.Use(ScreenFilterKind.Standard);
                a = Take(s);
                ScreenFilter.Use(ScreenFilterKind.Dither);
                b = Take(s);
                if (a == null || b == null) return s.Name + ": カメラが無い";
                CheckDiveSky.Save(a, Path.Combine(dir, s.Name + "_standard.png"));
                CheckDiveSky.Save(b, Path.Combine(dir, s.Name + "_dither.png"));
                pair = Side(a, b, 12);
                var path = Path.Combine(dir, s.Name + "_pair.png");
                CheckDiveSky.Save(pair, path);
                return s.Name + " → " + path;
            }
            finally
            {
                if (a != null) Object.DestroyImmediate(a);
                if (b != null) Object.DestroyImmediate(b);
                if (pair != null) Object.DestroyImmediate(pair);
            }
        }

        /// <summary>標準と、steps の段ごとの減色＋ディザで一枚ずつ撮る</summary>
        static string ShootSteps(Spot s, string dir, Vector2[] steps)
        {
            ScreenFilter.Use(ScreenFilterKind.Standard);
            var shot = Take(s);
            if (shot == null) return s.Name + ": カメラが無い";
            try
            {
                CheckDiveSky.Save(shot, Path.Combine(dir, s.Name + "_standard.png"));
            }
            finally
            {
                Object.DestroyImmediate(shot);
            }
            foreach (var step in steps)
            {
                ScreenFilter.Use(ScreenFilterKind.Dither, step.x, step.y);
                shot = Take(s);
                if (shot == null) return s.Name + ": カメラが無い";
                try
                {
                    CheckDiveSky.Save(shot, Path.Combine(dir, StepName(s.Name, step) + ".png"));
                }
                finally
                {
                    Object.DestroyImmediate(shot);
                }
            }
            return string.Format("{0} → {1}（標準と {2} 段）", s.Name, dir, steps.Length);
        }

        static Texture2D Take(Spot s)
        {
            switch (s.Way)
            {
                case Way.Dive: return TitleShots.Dive(s.View);
                default: return TitleShots.Game(s.View);
            }
        }

        /// <summary>
        /// 場面 8 を走り出した形にする（<see cref="DriveDirector"/> が乗り込んだ時にするのと同じ）。
        /// ガレージを伏せ、帯 0 の沿道・空の物・空と灯り・雨を出す。場面は保存しないので戻さない
        /// </summary>
        static void Board()
        {
            var director = Object.FindFirstObjectByType<DriveDirector>(FindObjectsInactive.Include);
            if (director == null) return;
            const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Instance;
            var t = typeof(DriveDirector);
            var garage = t.GetField("garage", flags).GetValue(director) as GameObject;
            var world = t.GetField("world", flags).GetValue(director) as DriveWorld;
            var skies = t.GetField("skies", flags).GetValue(director) as Transform[];
            var bands = t.GetField("bands", flags).GetValue(director) as DriveBand[];
            var sun = t.GetField("sun", flags).GetValue(director) as Light;
            var eye = t.GetField("eye", flags).GetValue(director) as Camera;
            var beams = t.GetField("beams", flags).GetValue(director) as Renderer;
            var rain = t.GetField("rainRig", flags).GetValue(director) as GameObject;
            if (garage != null) garage.SetActive(false);
            if (world != null) world.Dress(0);
            if (skies != null)
                for (var i = 0; i < skies.Length; i++)
                    if (skies[i] != null) skies[i].gameObject.SetActive(i == 0);
            if (bands != null && bands.Length > 0)
            {
                bands[0].sky.Apply(sun, eye, beams);
                if (rain != null) rain.SetActive(bands[0].rain);
            }
        }

        /// <summary>
        /// 設定の枠を、画面全体で撮る。コンソール（自室で開いた形）とタイトルの画面の二つを、
        /// フィルターの行を選んだ形で、標準（減色の強さ・ディザの強さの行が暗い）と減色＋ディザの二通り。
        /// 標準では、効いていないディザの強さの行を選んだ形も撮る（「_standard_dots」）。
        /// 設定の値は手元の辞書に差し替えて動かす（PlayerPrefs を汚さない）
        /// </summary>
        public static string Panels(string dir)
        {
            if (EditorApplication.isPlaying) return "再生中は撮らない";
            var busy = Busy();
            if (busy != null) return busy;
            var setup = EditorSceneManager.GetSceneManagerSetup();
            var sb = new StringBuilder();
            var async = ShaderUtil.allowAsyncCompilation;
            Directory.CreateDirectory(dir);
            try
            {
                ShaderUtil.allowAsyncCompilation = false;
                GameSettings.Box = new MemoryBox();
                var scene = EditorSceneManager.OpenScene("Assets/Scenes/Room.unity", OpenSceneMode.Single);
                if (!scene.IsValid()) return "Room を開けない";
                var paint = typeof(ImplantConsole).GetMethod("Paint", BindingFlags.NonPublic | BindingFlags.Instance);
                // 開いた時はカメラの速さ。一つ下がフィルター、三つ下がディザの強さ
                var shots = new[]
                {
                    new { Kind = ScreenFilterKind.Dither, Name = "dither", Down = 1 },
                    new { Kind = ScreenFilterKind.Standard, Name = "standard", Down = 1 },
                    new { Kind = ScreenFilterKind.Standard, Name = "standard_dots", Down = 3 },
                };
                foreach (var shot in shots)
                {
                    var down = shot.Down;
                    GameSettings.Filter.Value = (int)shot.Kind;
                    ScreenFilter.Apply();
                    sb.AppendLine(TitleShots.Console(Path.Combine(dir, "console_settings_" + shot.Name + ".png"), ConsolePanel.Settings, c =>
                    {
                        c.Menu.MoveRow(down);
                        if (paint != null) paint.Invoke(c, null);
                    }));
                    sb.AppendLine(TitleShots.Screen(Path.Combine(dir, "title_settings_" + shot.Name + ".png"), false, TitleShots.Sample(1), false,
                        stage: t =>
                        {
                            t.OpenSettings();
                            t.Settings.List.MoveRow(down);
                            t.Settings.Paint();
                        }));
                }
            }
            catch (Exception e)
            {
                sb.AppendLine("例外: " + e);
            }
            finally
            {
                GameSettings.Box = null;
                // エディタでは標準に戻す（遊んでいない間は、設定の値に関わらず標準。ScreenFilter）
                ScreenFilter.Use(ScreenFilterKind.Standard);
                ShaderUtil.allowAsyncCompilation = async;
                Back(setup);
                EditorUtility.UnloadUnusedAssetsImmediate();
            }
            return sb.ToString().TrimEnd();
        }

        /// <summary>
        /// 開いている場面に未保存の変更があれば、撮らない訳。無ければ null。
        /// 保存されていない場面がカメラと灯りだけ（<see cref="Back"/> が撮り終えた後に作る物）なら、変更ありでも撮る（撮り終えたら同じ物を作り直す）
        /// </summary>
        static string Busy()
        {
            for (var i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (scene.isDirty && !Blank(scene))
                    return "開いている場面に未保存の変更がある。保存するか捨ててからもう一度: " + scene.path;
            }
            return null;
        }

        /// <summary>保存されていない場面で、置いてあるのが新しい場面の既定の物（Main Camera と Directional Light、子無し）だけか</summary>
        static bool Blank(Scene scene)
        {
            if (!string.IsNullOrEmpty(scene.path)) return false;
            foreach (var go in scene.GetRootGameObjects())
                if ((go.name != "Main Camera" && go.name != "Directional Light") || go.transform.childCount > 0)
                    return false;
            return true;
        }

        /// <summary>二枚を左右に並べる。あいだは gap 画素の黒</summary>
        internal static Texture2D Side(Texture2D a, Texture2D b, int gap)
        {
            var w = a.width + gap + b.width;
            var h = Mathf.Max(a.height, b.height);
            var o = new Color32[w * h];
            var pa = a.GetPixels32();
            var pb = b.GetPixels32();
            for (var y = 0; y < a.height; y++)
                for (var x = 0; x < a.width; x++)
                    o[y * w + x] = pa[y * a.width + x];
            for (var y = 0; y < b.height; y++)
                for (var x = 0; x < b.width; x++)
                    o[y * w + a.width + gap + x] = pb[y * b.width + x];
            for (var y = 0; y < h; y++)
                for (var x = a.width; x < a.width + gap; x++)
                    o[y * w + x] = new Color32(0, 0, 0, 255);
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false, false);
            tex.SetPixels32(o);
            tex.Apply();
            return tex;
        }

        /// <summary>撮る前の場面へ戻す。保存されていない場面（道筋の無い物）だったなら、空の場面を作る</summary>
        static void Back(SceneSetup[] setup)
        {
            var usable = setup != null && setup.Length > 0;
            if (usable)
                foreach (var s in setup)
                    if (string.IsNullOrEmpty(s.path)) usable = false;
            if (usable) EditorSceneManager.RestoreSceneManagerSetup(setup);
            else EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
        }
    }
}
