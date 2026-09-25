using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using HalfAware.EditorTools.Rocketbox;

namespace HalfAware.EditorTools
{
    /// <summary>
    /// 記憶の中の人を測る。見た目ではなく数で確かめる（設計書 9.5 節）。
    ///
    /// どれもシーンを保存しない。記憶と場所を起こすのは <see cref="CheckDiveSky.Stage"/> で、
    /// 抜けるときに元の有効・無効と空へ戻す。測るために一時に置く物は HideAndDontSave にして、
    /// 使い終わったら捨てる
    /// </summary>
    public static class CheckDivePeople
    {
        /// <summary>模型の置き場から離れた、何も無い所。一時の物はここに並べる</summary>
        static readonly Vector3 Yard = new Vector3(-6400f, 0f, -6400f);

        // ---- 人の一覧 ------------------------------------------------------------------

        /// <summary>
        /// 記憶の人の id（<see cref="DiveCast"/> の id）。記憶に置いた人は一人ずつのプレハブ（<see cref="BuildDiveCast"/>）の写しなので、
        /// 元のプレハブの名前が id。プレハブと結ばれていなければ null
        /// </summary>
        public static string PersonId(Transform who)
        {
            if (who == null) return null;
            var src = UnityEditor.PrefabUtility.GetCorrespondingObjectFromSource(who.gameObject);
            return src != null ? src.name : null;
        }

        /// <summary>記憶ごとに、誰がどの名前で、どの模型・どの背で立っているか</summary>
        public static string Table()
        {
            var sb = new StringBuilder();
            sb.AppendLine("| 記憶 | 名前 | 人 | 年 | 模型 | 立ち方 | 背 m |");
            sb.AppendLine("|---|---|---|---|---|---|---|");
            foreach (var take in Takes())
            {
                using (new CheckDiveSky.Stage(Place(take), int.Parse(take.name)))
                {
                    foreach (var motion in take.GetComponentsInChildren<PersonMotion>(true))
                    {
                        var id = PersonId(motion.transform);
                        Person p;
                        DiveCast.TryById(id, out p);
                        var box = Extent(motion.transform);
                        sb.AppendLine(string.Format("| {0} | {1} | {2} | {3} | {4} | {5} | {6:F2} |",
                            take.name, motion.name, p.name, p.age, id != null ? RocketboxMemory.ById(id).Model.Label : "?",
                            motion.Seated ? "座る" : motion.GetComponent<Mover>() != null ? "歩く" : "立つ",
                            box.size.y));
                    }
                }
            }
            return sb.ToString();
        }

        /// <summary>
        /// 同じ人が同じ見た目か。人ごとに、マテリアルの並びと模型の縮尺と、立った背（座る人は除く）を比べる
        /// </summary>
        public static string SameLook()
        {
            var colours = new Dictionary<string, string>();
            var scales = new Dictionary<string, float>();
            var heights = new Dictionary<string, float>();
            var seen = new Dictionary<string, List<string>>();
            var bad = new List<string>();
            foreach (var take in Takes())
            {
                using (new CheckDiveSky.Stage(Place(take), int.Parse(take.name)))
                {
                    foreach (var motion in take.GetComponentsInChildren<PersonMotion>(true))
                    {
                        var id = PersonId(motion.transform) ?? motion.name;
                        var sb = new StringBuilder();
                        foreach (var smr in motion.Body.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                            foreach (var mat in smr.sharedMaterials) sb.Append(mat != null ? mat.name : "-").Append(',');
                        var scale = motion.Body.localScale.x;
                        var tall = motion.Seated ? -1f : Extent(motion.transform).size.y;
                        if (!seen.ContainsKey(id)) seen[id] = new List<string>();
                        seen[id].Add(take.name + "/" + motion.name);
                        string had;
                        if (colours.TryGetValue(id, out had))
                        {
                            if (had != sb.ToString()) bad.Add(id + " のマテリアルが " + take.name + "/" + motion.name + " で違う");
                            if (Mathf.Abs(scales[id] - scale) > 1e-4f) bad.Add(id + " の縮尺が違う");
                            if (tall > 0f && heights[id] > 0f && Mathf.Abs(heights[id] - tall) > 0.005f)
                                bad.Add(id + " の背が " + heights[id].ToString("F3") + " と " + tall.ToString("F3"));
                        }
                        else
                        {
                            colours[id] = sb.ToString();
                            scales[id] = scale;
                        }
                        if (tall > 0f && (!heights.ContainsKey(id) || heights[id] < 0f)) heights[id] = tall;
                        else if (!heights.ContainsKey(id)) heights[id] = tall;
                    }
                }
            }
            var out1 = new StringBuilder();
            foreach (var kv in seen)
            {
                if (kv.Value.Count < 2) continue;
                out1.AppendLine(kv.Key + " : " + string.Join(", ", kv.Value.ToArray()) + "  背 " +
                    (heights[kv.Key] > 0f ? heights[kv.Key].ToString("F3") : "（座る）") + "  マテリアル " +
                    (colours[kv.Key].Split(',').Length - 1).ToString());
            }
            out1.AppendLine(bad.Count == 0 ? "食い違い無し" : string.Join("\n", bad.ToArray()));
            return out1.ToString();
        }

        // ---- 体つき ---------------------------------------------------------------------

        /// <summary>
        /// 子ども・十代・大人・年寄りの代表で、背、頭の高さと背の比、脚の長さと背の比。
        /// 頭の高さは頭の骨（首の付け根の上）から頭のてっぺんまで、脚の長さは腿の付け根の高さ。
        /// 比べる「模型のまま」は Rocketbox の模型を骨の縮尺を掛けずに立たせた形
        /// </summary>
        public static string Proportions()
        {
            var sb = new StringBuilder();
            sb.AppendLine("| 人 | 区分 | 背 m | 頭/背 | 脚/背 | 模型のままの頭/背 | 模型のままの脚/背 |");
            sb.AppendLine("|---|---|---|---|---|---|---|");
            var ids = new[] { "Mei", "Sofia", "Lucas", "Daniel", "Aisha", "Priya", "Hanna", "Mark", "Albert", "Rosa" };
            foreach (var id in ids)
            {
                var copy = Copy(id);
                if (copy == null) { sb.AppendLine("| " + id + " | 見つからない |"); continue; }
                try
                {
                    Person p;
                    DiveCast.TryById(id, out p);
                    float head, leg, tall;
                    Ratios(copy, out tall, out head, out leg);
                    float head0, leg0, tall0;
                    RawRatios(RocketboxMemory.ById(id), out tall0, out head0, out leg0);
                    sb.AppendLine(string.Format("| {0} {1} | {2} | {3:F2} | {4:F3} | {5:F3} | {6:F3} | {7:F3} |",
                        p.name, p.age, p.Band, tall, head / tall, leg / tall, head0 / tall0, leg0 / tall0));
                }
                finally
                {
                    Object.DestroyImmediate(copy.gameObject);
                }
            }
            return sb.ToString();
        }

        static void Ratios(Transform who, out float tall, out float head, out float leg)
        {
            var box = Extent(who);
            tall = box.size.y;
            var h = Bone(who, HumanBodyBones.Head);
            head = box.max.y - who.InverseTransformPoint(h.position).y;
            var l = who.InverseTransformPoint(Bone(who, HumanBodyBones.LeftUpperLeg).position).y;
            var r = who.InverseTransformPoint(Bone(who, HumanBodyBones.RightUpperLeg).position).y;
            leg = (l + r) * 0.5f - box.min.y;
        }

        /// <summary>Rocketbox の模型をそのまま（骨の縮尺も背の丸みも掛けず）、記憶の人の立ちの動きの頭で置いた比</summary>
        static void RawRatios(RocketboxMemory m, out float tall, out float head, out float leg)
        {
            var src = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(m.Model.Model);
            var inst = Object.Instantiate(src);
            inst.hideFlags = HideFlags.HideAndDontSave;
            inst.transform.position = Yard + new Vector3(0f, 0f, -20f);
            try
            {
                BodyPoser.Stand(inst.GetComponent<Animator>(), BuildDiveCast.Clip(m.Cast.female, "Idle"));
                Ratios(inst.transform, out tall, out head, out leg);
            }
            finally
            {
                Object.DestroyImmediate(inst);
            }
        }

        // ---- 動き -------------------------------------------------------------------------

        /// <summary>
        /// 記憶一本の歩く人を、ゲームと同じ 60 フレーム毎秒で Mover と一緒に動かして測る。
        /// 合図（cue）を持つ人は記憶の頭で合図が来たことにする。
        /// - こま数: 骨を置き直した回数を秒で除く
        /// - 足の滑り: こまの頭ごとに、床に着いている足（低い方で、床から 3 cm 以内）が、
        ///   次のこまでも着いたままなら、そのあいだに床の上で動いた距離。滑らなければ 0。
        ///   比べに、同じこまのあいだに根が進んだ距離も出す（足を周期で運ばなければ、足はこれだけ滑る）
        /// - 一歩: 一周期で進む m の半分と、実際に一周期のあいだに進んだ距離の半分
        /// - 頭から: 伏せて起こし直した直後の骨の向きが、最初に起こした直後と同じか
        /// </summary>
        public static string Gait(int which)
        {
            var sb = new StringBuilder();
            var take = TakeAt(which);
            if (take == null) return "記憶 " + which + " が無い";
            using (new CheckDiveSky.Stage(Place(take), which))
            {
                Physics.SyncTransforms();
                foreach (var mover in take.GetComponentsInChildren<Mover>(true))
                {
                    var motion = mover.GetComponent<PersonMotion>();
                    if (motion == null) continue;
                    sb.AppendLine(Walk(take, mover, motion));
                }
            }
            return sb.ToString();
        }

        static string Walk(Transform take, Mover mover, PersonMotion motion)
        {
            const float dt = 1f / 60f;
            // 合図を持つ人は、記憶の頭で合図が来たことにする。DiveDirector はその時から数える。
            // 二本目の線（駆け出して戻ってくる人）があれば、戻り終えるまで
            var span = mover.Span + (mover.Returns ? mover.NextSpan : 0f);
            var end = mover.Until + 1f;
            mover.Play(0f);
            Physics.SyncTransforms();
            motion.Restart();
            motion.Late();
            var first = Snap(motion.Body);

            // 足首の骨（Rocketbox の足は足首の関節）。着いているかは、立った形の足首の高さから 3 cm 以内で見る
            var feet = new[] { Bone(motion.transform, HumanBodyBones.LeftFoot), Bone(motion.transform, HumanBodyBones.RightFoot) };
            var rest = float.MaxValue;
            for (var f = 0; f < 2; f++) if (feet[f] != null) rest = Mathf.Min(rest, feet[f].position.y - motion.transform.position.y);
            var lastTicks = motion.Ticks;
            var lastFeet = new Vector3[2];
            var lastPlanted = -1;
            var slips = new List<float>();
            var travels = new List<float>();
            var lastRoot = motion.transform.position;
            var lastTickRoot = lastRoot;
            var counts = new Dictionary<PersonMotion.Gait, int>();
            var cycles = 0f;
            var lastPhase = 0f;
            var walked = 0f;
            var walkTicks = 0;
            var strideSum = 0f;
            var ticksAtStart = motion.Ticks;
            var seconds = 0f;
            for (var t = 0f; t < end; t += dt)
            {
                mover.Play(t);
                Physics.SyncTransforms();
                motion.Step(dt);
                motion.Late();
                seconds += dt;
                var root = motion.transform.position;
                if (motion.Ticks == lastTicks) continue;
                lastTicks = motion.Ticks;
                PersonMotion.Gait g = motion.Current;
                counts[g] = (counts.ContainsKey(g) ? counts[g] : 0) + 1;
                var travel = Flat(root - lastTickRoot);
                lastTickRoot = root;
                if (g != PersonMotion.Gait.Stand)
                {
                    walked += travel;
                    walkTicks++;
                    strideSum += g == PersonMotion.Gait.Run ? motion.RunStride : PersonMotion.StrideAt(motion.Strides, motion.Reach);
                    var d = motion.Phase - lastPhase;
                    if (d < 0f) d += 1f;
                    cycles += d;
                }
                lastPhase = motion.Phase;

                // 床に着いている足
                var planted = -1;
                var low = float.MaxValue;
                for (var f = 0; f < 2; f++)
                {
                    if (feet[f] == null) continue;
                    var y = feet[f].position.y - motion.transform.position.y;
                    if (y < low) { low = y; planted = f; }
                }
                if (low > rest + 0.03f) planted = -1;
                if (planted >= 0 && planted == lastPlanted && g != PersonMotion.Gait.Stand)
                {
                    slips.Add(Flat(feet[planted].position - lastFeet[planted]));
                    travels.Add(travel);
                }
                for (var f = 0; f < 2; f++) if (feet[f] != null) lastFeet[f] = feet[f].position;
                lastPlanted = planted;
            }
            var fps = (motion.Ticks - ticksAtStart) / seconds;

            // 頭から
            motion.Close();
            mover.Play(0f);
            Physics.SyncTransforms();
            motion.Restart();
            motion.Late();
            var again = Snap(motion.Body);
            var worst = 0f;
            for (var i = 0; i < first.Length; i++) worst = Mathf.Max(worst, Quaternion.Angle(first[i], again[i]));
            motion.Close();
            mover.Play(0f);
            motion.Still();

            slips.Sort();
            travels.Sort();
            var sb = new StringBuilder();
            Person p;
            DiveCast.TryById(PersonId(motion.transform), out p);
            sb.Append(take.name + "/" + motion.name + "（" + p.name + "）");
            sb.Append(string.Format(" 線 {0:F2} m を {1:F1} 秒", Flat(mover.To - mover.From) + (mover.Returns ? Flat(mover.Next - mover.To) : 0f), span));
            sb.Append(string.Format("｜こま {0:F1}/秒", fps));
            sb.Append("｜立 " + Count(counts, PersonMotion.Gait.Stand) + " 歩 " + Count(counts, PersonMotion.Gait.Walk) + " 走 " + Count(counts, PersonMotion.Gait.Run));
            if (walkTicks > 0)
            {
                sb.Append(string.Format("｜一歩（一周期の半分） {0:F2} m", strideSum / walkTicks * 0.5f));
                sb.Append(string.Format("｜進んだ {0:F2} m ÷ 周期 {1:F2} 回 → 一周期 {2:F2} m", walked, cycles, cycles > 0f ? walked / cycles : 0f));
            }
            if (slips.Count > 0)
            {
                sb.Append(string.Format("｜着いた足の滑り 中央 {0:F1} cm 最大 {1:F1} cm（同じこまに根が進んだ 中央 {2:F1} cm 最大 {3:F1} cm、{4} こま）",
                    slips[slips.Count / 2] * 100f, slips[slips.Count - 1] * 100f,
                    travels[travels.Count / 2] * 100f, travels[travels.Count - 1] * 100f, slips.Count));
            }
            sb.Append(string.Format("｜入り直しの骨の向きの差 最大 {0:F3} 度", worst));
            return sb.ToString();
        }

        static int Count(Dictionary<PersonMotion.Gait, int> counts, PersonMotion.Gait g)
        {
            return counts.ContainsKey(g) ? counts[g] : 0;
        }

        static Quaternion[] Snap(Transform body)
        {
            var all = body.GetComponentsInChildren<Transform>(true);
            var r = new Quaternion[all.Length];
            for (var i = 0; i < all.Length; i++) r[i] = all[i].localRotation;
            return r;
        }

        static float Flat(Vector3 v)
        {
            v.y = 0f;
            return v.magnitude;
        }

        // ---- 会話をなぞる -------------------------------------------------------------------

        /// <summary>
        /// 記憶一本を、一行目から最後の板までなぞる。DiveDirector の中の口（Play と Step）を
        /// リフレクションで呼び、エディタのまま進める。
        ///
        /// 会話は並びの順に、相手の見える立ち位置（鍵打ちの点、相手のまわり 1.2・1.8・2.6 m）を探し、
        /// 目を留めて E で始め、E で送り切る。そのあと見える人の全員に目を留めて、板が出るかを見る。
        /// 見えるかは DiveDirector と同じ光線（目から頭と胸へ、行きと帰り）で測る。
        /// 人のコライダーは無いので、光線を遮るのは場所の壁と戸の板だけ。
        /// 相手を留めたところと板が出たところを <paramref name="shotDir"/> へ撮る。
        /// 抜けるときに全部元へ戻す
        /// </summary>
        public static string Walkthrough(int which, string shotDir)
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
            var d = UnityEngine.Object.FindFirstObjectByType<HalfAware.DiveDirector>();
            var T = typeof(HalfAware.DiveDirector);
            System.Func<string, System.Reflection.FieldInfo> F = n => T.GetField(n, flags);
            var player = (HalfAware.PlayerController)F("player").GetValue(d);
            var hud = (HalfAware.HudView)F("hud").GetValue(d);
            var panel = (HalfAware.HoloPanel)F("panel").GetValue(d);
            var caption = (TMPro.TMP_Text)F("caption").GetValue(d);
            var takes = (UnityEngine.Transform[])F("takes").GetValue(d);
            var places = (UnityEngine.Transform[])F("places").GetValue(d);
            var roster = (HalfAware.DiveRoster)F("roster").GetValue(d);
            var HT = typeof(HalfAware.HudView);
            var band = (UnityEngine.GameObject)HT.GetField("subtitleBand", flags).GetValue(hud);
            var subText = (TMPro.TMP_Text)HT.GetField("subtitleText", flags).GetValue(hud);
            var prompt = (TMPro.TMP_Text)HT.GetField("promptText", flags).GetValue(hud);
            var muted = new string[] { "body", "volume", "daze", "feet", "skies" };
            var saved = new System.Collections.Generic.Dictionary<string, object>();
            foreach (var n in muted) saved[n] = F(n).GetValue(d);
            var pT = player.transform; var pPos = pT.position; var pRot = pT.rotation;
            var hull = pT.GetComponent<UnityEngine.CharacterController>();
            var eye = player.Eye; var eLP = eye.localPosition; var eLR = eye.localRotation;
            var capText = caption.text;
            var panT = panel.transform; var panPos = panT.position; var panRot = panT.rotation; var panScale = panT.localScale; var panActive = panel.gameObject.activeSelf;
            var takeActive = new bool[takes.Length]; for (var i = 0; i < takes.Length; i++) takeActive[i] = takes[i].gameObject.activeSelf;
            var placeActive = new bool[places.Length]; for (var i = 0; i < places.Length; i++) placeActive[i] = places[i].gameObject.activeSelf;
            var moverPos = new System.Collections.Generic.Dictionary<UnityEngine.Transform, UnityEngine.Vector3>();
            foreach (var t in takes) foreach (var m in t.GetComponentsInChildren<HalfAware.Mover>(true)) moverPos[m.transform] = m.transform.localPosition;
            var promptActive = prompt.gameObject.activeSelf; var promptText0 = prompt.text;
            var bandActive = band.activeSelf; var subText0 = subText.text;
            var sky = HalfAware.EditorTools.CheckDiveSky.Sky.Read();
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("dirty before=" + scene.isDirty);
            var step = T.GetMethod("Step", flags);
            var blocking = T.GetMethod("Blocking", flags);
            System.Action<float, bool> S = (dt, press) => { step.Invoke(d, new object[] { dt, press }); UnityEngine.Physics.SyncTransforms(); };
            System.Func<UnityEngine.Transform, UnityEngine.Vector3> headOf = w => { var b = w.GetComponentInChildren<UnityEngine.Renderer>().bounds; return new UnityEngine.Vector3(b.center.x, b.max.y - b.size.y * 0.08f, b.center.z); };
            System.Func<UnityEngine.Transform, UnityEngine.Vector3> chestOf = w => { var b = w.GetComponentInChildren<UnityEngine.Renderer>().bounds; return new UnityEngine.Vector3(b.center.x, b.min.y + b.size.y * 0.70f, b.center.z); };
            // ゲームと同じく、体ごと相手へ向けてから目を向ける。目は体の前へ 0.22 m 出ているので、体を回さないと目の置き場が横へずれる
            System.Action<UnityEngine.Transform> aim = w => {
                var flat = (w.position + UnityEngine.Vector3.up * 1.2f) - eye.position; flat.y = 0f;
                if (flat.sqrMagnitude > 1e-6f) pT.rotation = UnityEngine.Quaternion.LookRotation(flat);
                UnityEngine.Physics.SyncTransforms();
                eye.rotation = UnityEngine.Quaternion.LookRotation((w.position + UnityEngine.Vector3.up * 1.2f) - eye.position);
                UnityEngine.Physics.SyncTransforms(); };
            System.Func<string> state = () => "t=" + d.Clock.ToString("F1")
                + " 帯=" + (band.activeSelf ? "「" + subText.text.Replace("\n", "/") + "」" : "なし")
                + " 案内=" + (prompt.gameObject.activeSelf ? "[" + prompt.text + "]" : "なし")
                + " 板=" + (panel.gameObject.activeSelf && panel.Showing ? "出" : "なし")
                + " 相手=" + (d.Shown != null ? d.Shown.name : "-") + " 話中=" + d.Talking + " 済=" + d.Done + " 出行=" + d.Spoken;
            System.Func<UnityEngine.Transform, UnityEngine.Collider> blockHead = w => (UnityEngine.Collider)blocking.Invoke(d, new object[] { eye.position, headOf(w), w });
            System.Func<UnityEngine.Transform, UnityEngine.Collider> blockChest = w => (UnityEngine.Collider)blocking.Invoke(d, new object[] { eye.position, chestOf(w), w });
            System.Func<UnityEngine.Transform, string> sight = w => {
                var c1 = blockHead(w); var c2 = blockChest(w);
                return w.name + " 頭" + headOf(w).y.ToString("F2") + ":" + (c1 == null ? "通る" : "遮り " + c1.name) + " 胸" + chestOf(w).y.ToString("F2") + ":" + (c2 == null ? "通る" : "遮り " + c2.name);
            };
            System.Func<UnityEngine.Transform, bool> seen = w => blockHead(w) == null || blockChest(w) == null;
            System.Func<float> pitchOf = () => { var x = eye.eulerAngles.x; if (x > 180f) x -= 360f; return -x; };
            try
            {
                foreach (var n in muted) F(n).SetValue(d, n == "skies" ? (object)new HalfAware.PlaceSky[0] : null);
                T.GetMethod("Awake", flags).Invoke(d, null);
                F("chain").SetValue(d, new HalfAware.DiveChain(roster.Count, HalfAware.DiveIds.Listed, 8, new System.Random(1)));
                T.GetMethod("Shut", flags).Invoke(d, null);
                T.GetMethod("Play", flags).Invoke(d, new object[] { which });
                var entry = roster[which];
                var psky = ((HalfAware.PlaceSky[])saved["skies"])[System.Array.IndexOf(HalfAware.DiveIds.Places, entry.place)];
                psky.Apply(null);
                var clear = psky.skybox != null ? UnityEngine.CameraClearFlags.Skybox : UnityEngine.CameraClearFlags.SolidColor;
                eye.localPosition = new UnityEngine.Vector3(0f, entry.eyeHeight, 0.22f);
                eye.localRotation = UnityEngine.Quaternion.identity;
                UnityEngine.Physics.SyncTransforms();
                var take = takes[which];
                var place = (UnityEngine.Transform)F("place").GetValue(d);
                var tk = take.GetComponent<HalfAware.Take>();
                System.Action<UnityEngine.Vector3> standWorld = at => { hull.enabled = false; pT.position = at + UnityEngine.Vector3.up * 0.06f; hull.enabled = true; UnityEngine.Physics.SyncTransforms(); };
                sb.AppendLine("記憶 " + which + " " + caption.text);
                S(0.1f, false); sb.AppendLine("[入った直後] " + state());
                System.Func<UnityEngine.Transform, System.Collections.Generic.List<UnityEngine.Vector3>> spots = w => {
                    var all = new System.Collections.Generic.List<UnityEngine.Vector3>();
                    foreach (var k in tk.Keys) all.Add(place.TransformPoint(k.position));
                    // 鍵打ちの点は、相手に近い順に当たる
                    all.Sort((a, b) => (a - w.position).sqrMagnitude.CompareTo((b - w.position).sqrMagnitude));
                    foreach (var r in new[]{1.2f, 1.8f, 2.6f})
                        for (var a = 0; a < 12; a++) {
                            var ang = a * 30f * UnityEngine.Mathf.Deg2Rad;
                            var c = w.position + new UnityEngine.Vector3(UnityEngine.Mathf.Sin(ang) * r, 0f, UnityEngine.Mathf.Cos(ang) * r);
                            UnityEngine.RaycastHit hit;
                            if (UnityEngine.Physics.Raycast(c + UnityEngine.Vector3.up * 1.0f, UnityEngine.Vector3.down, out hit, 1.6f) && hit.collider.GetComponent<UnityEngine.Renderer>() != null)
                                all.Add(hit.point);
                        }
                    return all;
                };
                System.Func<UnityEngine.Transform, bool> stand = w => {
                    foreach (var at in spots(w)) { standWorld(at); aim(w); if (seen(w)) { sb.AppendLine("   立ち位置 " + place.InverseTransformPoint(at).ToString("F2") + " から " + sight(w)); return true; } }
                    sb.AppendLine("   " + w.name + " の見える所が無い"); return false;
                };
                var talks = HalfAware.DiveEntry.Exchanges(entry.said);
                for (var k = 0; k < talks.Length; k++) {
                    var w = take.Find(talks[k].partner);
                    for (var i = 0; i < 70; i++) S(0.1f, false);
                    if (!stand(w)) { sb.AppendLine("会話 " + k + " の相手が選べない"); break; }
                    for (var i = 0; i < 7; i++) S(0.1f, false);
                    sb.AppendLine("[会話 " + k + " " + w.name + " を留める] " + state());
                    HalfAware.EditorTools.CheckDiveSky.Pair(eye.position, eye.eulerAngles.y, pitchOf(), clear, psky.flat, System.IO.Path.Combine(shotDir, "m" + which + "_talk" + k + "_" + w.name));
                    S(0.02f, true); sb.AppendLine("   E: " + state());
                    var guard = 0;
                    while (d.Talking && guard++ < 20) { S(0.02f, true); }
                    sb.AppendLine("   送り終え（" + guard + " 回）: " + state());
                }
                sb.AppendLine("会話 済 " + d.Done + "/" + talks.Length);
                foreach (var s in entry.seen) {
                    var w = take.Find(s.name);
                    // 歩く人は止まるまで待つ。記憶の時計で動く人の、いちばん遅い止まり時まで
                    var still = 0f;
                    foreach (var m in take.GetComponentsInChildren<HalfAware.Mover>(true))
                        if (m.Cue < 0) still = UnityEngine.Mathf.Max(still, m.At + m.Span);
                    for (var i = 0; i < 70 || d.Clock < still + 0.5f; i++) S(0.1f, false);
                    if (!stand(w)) continue;
                    for (var i = 0; i < 8; i++) S(0.1f, false);
                    sb.AppendLine("[板 " + s.name + "] " + state());
                    HalfAware.EditorTools.CheckDiveSky.Pair(eye.position, eye.eulerAngles.y, pitchOf(), clear, psky.flat, System.IO.Path.Combine(shotDir, "m" + which + "_panel_" + s.name));
                }
            }
            catch (System.Exception ex) { sb.AppendLine("例外: " + ex); }
            finally
            {
                foreach (var n in muted) F(n).SetValue(d, saved[n]);
                hud.SetSubtitle("x"); hud.SetSubtitle(null);
                hud.SetPrompt(promptActive ? promptText0 : null);
                panel.Hide();
                panel.gameObject.SetActive(panActive);
                panT.position = panPos; panT.rotation = panRot; panT.localScale = panScale;
                foreach (var kv in moverPos) kv.Key.localPosition = kv.Value;
                for (var i = 0; i < takes.Length; i++) takes[i].gameObject.SetActive(takeActive[i]);
                for (var i = 0; i < places.Length; i++) places[i].gameObject.SetActive(placeActive[i]);
                hull.enabled = false; pT.position = pPos; pT.rotation = pRot; hull.enabled = true;
                eye.localPosition = eLP; eye.localRotation = eLR;
                caption.text = capText;
                subText.text = subText0; band.SetActive(bandActive);
                player.CanMove = true; player.SpeedScale = 1f;
                F("chain").SetValue(d, null); F("take").SetValue(d, null); F("place").SetValue(d, null);
                sky.Write();
                UnityEngine.Physics.SyncTransforms();
            }
            sb.AppendLine("dirty after=" + scene.isDirty);
            return sb.ToString();
        }

        // ---- 重さ -------------------------------------------------------------------------

        /// <summary>記憶ごとの人の数・骨つきのレンダラーの数・三角の数</summary>
        public static string Weight()
        {
            var sb = new StringBuilder();
            var most = 0;
            var mostTris = 0;
            foreach (var take in Takes())
            {
                var people = take.GetComponentsInChildren<PersonMotion>(true);
                var skinned = take.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                var tris = 0;
                foreach (var s in skinned) if (s.sharedMesh != null) tris += s.sharedMesh.triangles.Length / 3;
                most = Mathf.Max(most, people.Length);
                mostTris = Mathf.Max(mostTris, tris);
                sb.AppendLine(string.Format("記憶 {0}（{1}）: 人 {2}、骨つきのレンダラー {3}、三角 {4}",
                    take.name, Place(take), people.Length, skinned.Length, tris));
            }
            sb.AppendLine(string.Format("一度に起きる記憶は一本。骨つきの人は多くて {0} 人、三角は多くて {1}", most, mostTris));
            return sb.ToString();
        }

        // ---- 絵 ---------------------------------------------------------------------------

        /// <summary>
        /// 子ども・十代・大人・年寄りを並べた一枚。人は記憶から写して、何も無い所へ並べる。
        /// 区分のあいだは一人分あける。座ってしか出てこない人（マテオ）は並ばない
        /// </summary>
        public static string Lineup(string path)
        {
            var rows = new[]
            {
                new[] { "Mei", "Sofia", "Lucas" },
                new[] { "Daniel", "Aisha", "Priya", "Emily" },
                new[] { "Hanna", "Lee", "Linda", "Mark" },
                new[] { "Giorgio", "Elena", "Albert", "Rosa" },
            };
            var made = new List<GameObject>();
            try
            {
                var slots = new List<KeyValuePair<string, float>>();
                var x = 0f;
                foreach (var row in rows)
                {
                    foreach (var id in row) { slots.Add(new KeyValuePair<string, float>(id, x)); x += 0.55f; }
                    x += 0.45f;
                }
                var mid = (x - 1f) * 0.5f;
                foreach (var slot in slots)
                {
                    var copy = Copy(slot.Key);
                    if (copy == null) continue;
                    copy.position = Yard + new Vector3(slot.Value - mid, 0f, 0f);
                    copy.rotation = Quaternion.Euler(0f, 180f, 0f);
                    var motion = copy.GetComponent<PersonMotion>();
                    if (motion != null) motion.Still();
                    made.Add(copy.gameObject);
                }
                made.Add(Floor());
                made.Add(Lamp(Yard + new Vector3(-2f, 4f, -4f), 9f));
                made.Add(Lamp(Yard + new Vector3(3f, 3f, -3f), 5f));
                Pair(Yard + new Vector3(0f, 1.0f, -12.5f), 0f, -1.5f, 28f, path);
                return "並べた " + (made.Count - 3) + " 人 → " + path;
            }
            finally
            {
                foreach (var go in made) if (go != null) Object.DestroyImmediate(go);
            }
        }

        /// <summary>確かめ用のカメラで二枚撮る。960×540 とゲームと同じ 320×180。画角だけ変えられる</summary>
        static void Pair(Vector3 eye, float yaw, float pitch, float fov, string path)
        {
            var bg = new Color(0.30f, 0.31f, 0.34f);
            var big = CheckDiveSky.Shot(eye, yaw, pitch, 960, 540, CameraClearFlags.SolidColor, bg, ~0, fov);
            CheckDiveSky.Save(big, path + ".png");
            Object.DestroyImmediate(big);
            var small = CheckDiveSky.Shot(eye, yaw, pitch, 320, 180, CameraClearFlags.SolidColor, bg, ~0, fov);
            CheckDiveSky.Save(small, path + "_game.png");
            Object.DestroyImmediate(small);
        }

        /// <summary>
        /// 歩き（run なら走り）の数こまを横に並べた一枚。一人を写し、こまの頭ごとに歩きを置いて並べる。
        /// 周期は一周期を <paramref name="frames"/> こまで回す（こまの間隔は一周期 ÷ こま数）
        /// </summary>
        public static string WalkFrames(string id, int frames, string path, bool run = false)
        {
            var made = new List<GameObject>();
            try
            {
                for (var i = 0; i < frames; i++)
                {
                    var copy = Copy(id);
                    if (copy == null) return id + " が見つからない";
                    var motion = copy.GetComponent<PersonMotion>();
                    copy.position = Yard + new Vector3((i - (frames - 1) * 0.5f) * 0.7f, 0f, 0f);
                    copy.rotation = Quaternion.Euler(0f, 90f, 0f);
                    var clip = run ? motion.Run : motion.Walk;
                    motion.Sample(clip, clip.length * i / frames);
                    made.Add(copy.gameObject);
                }
                made.Add(Floor());
                made.Add(Lamp(Yard + new Vector3(-2f, 4f, -4f), 9f));
                made.Add(Lamp(Yard + new Vector3(3f, 3f, -3f), 5f));
                Pair(Yard + new Vector3(0f, 0.9f, -7f), 0f, -2f, 32f, path);
                return (run ? "走り" : "歩き") + "を " + frames + " こま → " + path;
            }
            finally
            {
                foreach (var go in made) if (go != null) Object.DestroyImmediate(go);
            }
        }

        /// <summary>
        /// 記憶の一本を起こして撮る。eye は場所のローカル、向きは度。
        /// その場所の空を当て、終わったら戻す
        /// </summary>
        public static string Shoot(int which, Vector3 eye, float yaw, float pitch, string path)
        {
            var take = TakeAt(which);
            if (take == null) return "記憶 " + which + " が無い";
            var placeId = Place(take);
            using (var stage = new CheckDiveSky.Stage(placeId, which))
            {
                CheckDiveSky.SkyOf(placeId).Apply(null);
                var world = stage.Place != null ? stage.Place.TransformPoint(eye) : eye;
                CheckDiveSky.PairIn(placeId, world, yaw, pitch, path);
            }
            return "撮った → " + path;
        }

        /// <summary>
        /// 記憶の一本を、ゲームと同じ見え方で一枚撮る（960×540。パイプラインの render scale 1/3 で中は 320×180）。
        /// 主の足元 <paramref name="foot"/>（場所のローカル）に立ち、一覧の目の高さから <paramref name="target"/> の人（板の相手の名前）を見る。
        ///
        /// 見え方はゲームのカメラ（Player/Main Camera）の写しで、後処理も通す。記憶の体の差は一時の Volume で掛ける:
        /// 色味（Color Filter）と、ぼやけ（<see cref="HostBody"/> と同じ式。疲れ <paramref name="strain"/>、1 でその体のぼやけが出きる）。
        /// 動く人は、記憶の時計で動く人も合図で動く人も <paramref name="at"/> 秒の所（負なら動き終えた所）に置く。
        /// 相手をしている人は、首と頭をこのカメラ（主の目）へ向け切った形で撮る（<see cref="PersonMotion.Watch"/>。watch を切れば向けない）。
        /// 画面の角の白い膜と字幕は HUD の Canvas なので写らない。
        /// 抜けるときに、動く人の置き場・一時の Volume とカメラ・空を全部戻す
        /// </summary>
        public static string Game(int which, Vector3 foot, string target, string path, float strain = 1f, float at = -1f, float lift = 0f, bool watch = true)
        {
            var take = TakeAt(which);
            if (take == null) return "記憶 " + which + " が無い";
            var roster = UnityEditor.AssetDatabase.LoadAssetAtPath<DiveRoster>(BuildDive.RosterPath);
            var entry = roster[which];
            var placeId = entry.place;
            var main = GameObject.Find("Player/Main Camera");
            if (main == null) return "Player/Main Camera が無い";
            var kept = new Dictionary<Transform, Vector3>();
            foreach (var m in take.GetComponentsInChildren<Mover>(true)) kept[m.transform] = m.transform.localPosition;
            GameObject eyeGo = null, volGo = null;
            VolumeProfileHolder hold = null;
            try
            {
                using (var stage = new CheckDiveSky.Stage(placeId, which))
                {
                    var sky = CheckDiveSky.SkyOf(placeId);
                    sky.Apply(null);
                    foreach (var m in take.GetComponentsInChildren<Mover>(true))
                    {
                        m.Play(at < 0f ? m.Until + 1f : at);
                        var motion = m.GetComponent<PersonMotion>();
                        if (motion != null) motion.Still();
                    }
                    Physics.SyncTransforms();

                    var who = take.Find(target);
                    if (who == null) return "記憶 " + which + " に " + target + " がいない";
                    var place = stage.Place;
                    var footWorld = place != null ? place.TransformPoint(foot) : foot;
                    // 目は体の前へ出ている（BuildDive.EyeLead）。体ごと相手へ向けてから目を置く
                    var box = BodyBox(who);
                    var aim = new Vector3(box.center.x, box.min.y + box.size.y * (0.78f + lift), box.center.z);
                    var flat = aim - footWorld; flat.y = 0f;
                    var yaw = Mathf.Atan2(flat.x, flat.z) * Mathf.Rad2Deg;
                    var eye = footWorld + Quaternion.Euler(0f, yaw, 0f) * new Vector3(0f, entry.eyeHeight, 0.22f);
                    var look = aim - eye;
                    var pitch = Mathf.Atan2(-look.y, new Vector2(look.x, look.z).magnitude) * Mathf.Rad2Deg;

                    eyeGo = new GameObject("CheckDivePeopleEye");
                    eyeGo.hideFlags = HideFlags.HideAndDontSave;
                    var cam = eyeGo.AddComponent<Camera>();
                    cam.enabled = false;
                    cam.CopyFrom(main.GetComponent<Camera>());
                    cam.clearFlags = sky.skybox != null ? CameraClearFlags.Skybox : CameraClearFlags.SolidColor;
                    cam.backgroundColor = sky.flat;
                    var data = cam.GetUniversalAdditionalCameraData();
                    data.renderPostProcessing = true;
                    eyeGo.transform.position = eye;
                    eyeGo.transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
                    if (watch)
                        foreach (var motion in take.GetComponentsInChildren<PersonMotion>(true))
                        {
                            motion.Watch(eyeGo.transform);
                            motion.Still();
                        }

                    volGo = new GameObject("CheckDivePeopleVolume");
                    volGo.hideFlags = HideFlags.HideAndDontSave;
                    hold = new VolumeProfileHolder(volGo, entry, strain);

                    var shot = CheckDiveSky.Grab(cam, 960, 540);
                    CheckDiveSky.Save(shot, path);
                    Object.DestroyImmediate(shot);
                    return string.Format("記憶 {0}: {1} を {2:F2} m 先に、目 {3:F2} m・向き {4:F0}°・俯き {5:F0}°、色味 {6}、ぼやけ {7} {8:F1}（疲れ {9:F1}） → {10}",
                        which, target, Vector3.Distance(eye, aim), entry.eyeHeight, yaw, pitch,
                        ColorUtility.ToHtmlStringRGB(entry.tint), entry.blur, entry.blurAmount, strain, path);
                }
            }
            finally
            {
                if (hold != null) hold.Dispose();
                if (volGo != null) Object.DestroyImmediate(volGo);
                if (eyeGo != null) Object.DestroyImmediate(eyeGo);
                foreach (var kv in kept) if (kv.Key != null) kv.Key.localPosition = kv.Value;
                foreach (var motion in take.GetComponentsInChildren<PersonMotion>(true)) motion.Watch(null);
            }
        }

        /// <summary>
        /// 撮るあいだだけ置く Volume。記憶の色味とぼやけを、場面の Volume より高い優先度で掛ける。
        /// profile も override も一時の物で、Dispose で捨てる（場面の DiveVolume.asset には触らない）
        /// </summary>
        sealed class VolumeProfileHolder : System.IDisposable
        {
            readonly UnityEngine.Rendering.VolumeProfile profile;

            public VolumeProfileHolder(GameObject go, DiveEntry entry, float strain)
            {
                profile = ScriptableObject.CreateInstance<UnityEngine.Rendering.VolumeProfile>();
                profile.hideFlags = HideFlags.HideAndDontSave;
                var tone = profile.Add<UnityEngine.Rendering.Universal.ColorAdjustments>(false);
                tone.hideFlags = HideFlags.HideAndDontSave;
                tone.colorFilter.Override(entry.tint);
                var blur = profile.Add<UnityEngine.Rendering.Universal.DepthOfField>(false);
                blur.hideFlags = HideFlags.HideAndDontSave;
                // HostBody.Blurred と同じ式
                var force = Mathf.Clamp01(entry.blurAmount) * strain;
                if (entry.blur == Blur.Sharp || force <= 1e-3f) blur.active = false;
                else
                {
                    var start = entry.blur == Blur.Near ? HostBody.NearStart : HostBody.FarStart;
                    var end = entry.blur == Blur.Near ? HostBody.NearEnd : HostBody.FarEnd;
                    blur.mode.Override(UnityEngine.Rendering.Universal.DepthOfFieldMode.Gaussian);
                    blur.gaussianStart.Override(Mathf.Lerp(24f, start, strain));
                    blur.gaussianEnd.Override(Mathf.Lerp(24f + (end - start), end, strain));
                    blur.gaussianMaxRadius.Override(force * 1.5f);
                }
                var volume = go.AddComponent<UnityEngine.Rendering.Volume>();
                volume.isGlobal = true;
                volume.priority = 100f;
                volume.weight = 1f;
                volume.sharedProfile = profile;
            }

            public void Dispose()
            {
                foreach (var c in profile.components) if (c != null) Object.DestroyImmediate(c);
                Object.DestroyImmediate(profile);
            }
        }

        /// <summary>人の体の皮の広がり（世界）。持ち物は除く</summary>
        static Bounds BodyBox(Transform who)
        {
            var box = new Bounds();
            var first = true;
            var tmp = new Mesh();
            try
            {
                foreach (var smr in who.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    if (smr.sharedMesh == null || !smr.gameObject.activeInHierarchy) continue;
                    smr.BakeMesh(tmp, true);
                    var at = smr.transform.localToWorldMatrix;
                    foreach (var v in tmp.vertices)
                    {
                        var p = at.MultiplyPoint3x4(v);
                        if (first) { box = new Bounds(p, Vector3.zero); first = false; }
                        else box.Encapsulate(p);
                    }
                }
            }
            finally
            {
                Object.DestroyImmediate(tmp);
            }
            return box;
        }

        // ---- 道具 ---------------------------------------------------------------------------

        static IEnumerable<Transform> Takes()
        {
            var dive = GameObject.Find("Dive");
            if (dive == null) yield break;
            var takes = dive.transform.Find("Takes");
            if (takes == null) yield break;
            for (var i = 0; i < takes.childCount; i++) yield return takes.GetChild(i);
        }

        static Transform TakeAt(int which)
        {
            foreach (var t in Takes()) if (t.name == which.ToString()) return t;
            return null;
        }

        static string Place(Transform take)
        {
            var roster = UnityEditor.AssetDatabase.LoadAssetAtPath<DiveRoster>(BuildDive.RosterPath);
            var i = int.Parse(take.name);
            return roster != null && i < roster.Count ? roster[i].place : DiveIds.Estate;
        }

        /// <summary>
        /// その人を記憶から一人写して、何も無い所へ置く。写しはプレハブと結ばれない一時の物。
        /// 座っている人は写さない（立った形で並べたいので、同じ人の立っている記憶を探す）。
        /// 据えた姿勢（腕組み・手を後ろ・片脚に預ける）も写る
        /// </summary>
        static Transform Copy(string id)
        {
            foreach (var take in Takes())
            {
                foreach (var motion in take.GetComponentsInChildren<PersonMotion>(true))
                {
                    if (PersonId(motion.transform) != id) continue;
                    if (motion.Seated) continue;
                    var copy = Object.Instantiate(motion.gameObject);
                    copy.hideFlags = HideFlags.HideAndDontSave;
                    copy.SetActive(true);
                    foreach (var m in copy.GetComponents<Mover>()) Object.DestroyImmediate(m);
                    copy.transform.position = Yard;
                    copy.transform.rotation = Quaternion.identity;
                    var cm = copy.GetComponent<PersonMotion>();
                    if (cm != null) cm.Still();
                    return copy.transform;
                }
            }
            return null;
        }

        static GameObject Floor()
        {
            var floor = GameObject.CreatePrimitive(PrimitiveType.Quad);
            floor.hideFlags = HideFlags.HideAndDontSave;
            floor.transform.position = Yard;
            floor.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            floor.transform.localScale = Vector3.one * 30f;
            return floor;
        }

        static GameObject Lamp(Vector3 at, float power)
        {
            var go = new GameObject("CheckDivePeopleLamp");
            go.hideFlags = HideFlags.HideAndDontSave;
            go.transform.position = at;
            var l = go.AddComponent<Light>();
            l.type = LightType.Point;
            l.range = 20f;
            l.intensity = power;
            l.shadows = LightShadows.None;
            return go;
        }

        /// <summary>人の Humanoid の骨</summary>
        static Transform Bone(Transform root, HumanBodyBones bone)
        {
            var an = root.GetComponentInChildren<Animator>(true);
            return an != null ? an.GetBoneTransform(bone) : null;
        }

        /// <summary>皮を置いた形の広がり。人の根のローカル</summary>
        static Bounds Extent(Transform who)
        {
            var box = new Bounds();
            var first = true;
            var tmp = new Mesh();
            foreach (var smr in who.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                smr.BakeMesh(tmp, true);
                var at = who.worldToLocalMatrix * smr.transform.localToWorldMatrix;
                foreach (var v in tmp.vertices)
                {
                    var p = at.MultiplyPoint3x4(v);
                    if (first) { box = new Bounds(p, Vector3.zero); first = false; }
                    else box.Encapsulate(p);
                }
            }
            Object.DestroyImmediate(tmp);
            return box;
        }
    }
}
