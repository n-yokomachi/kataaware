using System;
using System.Collections.Generic;
using UnityEngine;

namespace HalfAware
{
    /// <summary>
    /// 記憶する（手動のセーブ）が残す、場面の中の状態（設計書 5 節「セーブの形」の手動）。
    ///
    /// - 立ち位置・向き・目の高さ（座っているか立っているか）
    /// - 調べ済みの物（二択で「はい」を選んだ物もここに入る。「いいえ」は何も残らないので、入れる物が無い）
    /// - 場面の演出が進めた物の状態（<see cref="parts"/>。演出ごとに <see cref="ISceneMemory"/> が書く）
    ///
    /// 場面 4 は <see cref="SceneFlow"/> を持たないので、<see cref="done"/> は空で、記憶の道筋は <see cref="parts"/> に入る
    /// </summary>
    [Serializable]
    public sealed class SceneMemo
    {
        /// <summary>足元の位置</summary>
        public Vector3 at;
        /// <summary>体の向き。度</summary>
        public float turn;
        /// <summary>首の振れる限り。度。0 以下なら体ごと回る（立っている）</summary>
        public float headLimit;
        /// <summary>体から見た首の向き。度。座っている間だけ意味がある</summary>
        public float head;
        /// <summary>上下の向き。度。正が下向き</summary>
        public float pitch;
        /// <summary>足元から目までの高さ。座っているか立っているかはここに出る</summary>
        public float eye = PlayerController.StandingEyeHeight;
        /// <summary>歩けるか</summary>
        public bool moves = true;
        /// <summary>調べ済みの物の id（<see cref="SceneProgress.Done"/>）。並びは綴りの順</summary>
        public string[] done = new string[0];
        /// <summary>演出ごとの状態。鍵は <see cref="ISceneMemory.MemoryKey"/></summary>
        public MemoPart[] parts = new MemoPart[0];

        /// <summary>鍵の物の中身。無ければ null</summary>
        public string Part(string key)
        {
            if (parts == null) return null;
            for (var i = 0; i < parts.Length; i++)
                if (parts[i].key == key) return parts[i].data;
            return null;
        }

        /// <summary>その id を調べ済みか</summary>
        public bool Did(string id)
        {
            return done != null && Array.IndexOf(done, id) >= 0;
        }
    }

    /// <summary>演出一つ分の状態。中身は演出が自分の形で JSON にした物</summary>
    [Serializable]
    public struct MemoPart
    {
        public string key;
        public string data;

        public MemoPart(string key, string data)
        {
            this.key = key;
            this.data = data;
        }
    }

    /// <summary>
    /// 場面の演出が、進めた物の状態を取り出す・当てる口（設計書 5 節）。
    ///
    /// **当てる時は、音・字幕・動き・待ちを出さない。** 終わった形へ一度に置く（戸は開き切り、ジャックは置き場に、上着は着た形に）。
    /// 当てるのは、シーンを読んだ直後（Awake・OnEnable の後、Start の前、最初のフレームを出す前）。
    /// 思い出した時は、中身が無くても（null でも）必ず呼ばれるので、Start の場面の頭の演出（目覚め、見出し、入った時の眩暈、
    /// 名を呼ぶ声など）を出さない印にも使う
    /// </summary>
    public interface ISceneMemory
    {
        /// <summary>場面の中で重ならない鍵</summary>
        string MemoryKey { get; }

        /// <summary>
        /// いまの状態を残してよいか。演出の途中（動いている、暗転している、売り買いの途中など）なら false。
        /// どれか一つでも false なら、その場面は自由に動ける所ではない
        /// </summary>
        bool Settled { get; }

        /// <summary>いまの状態。残す物が無ければ null（調べ済みの物から決まる物は、残さずそちらから戻してよい）</summary>
        string Capture();

        /// <summary>
        /// 残した状態を当てる。data は <see cref="Capture"/> が返した物か null。
        /// <see cref="SceneFlow"/> のある場面では、調べ済みの物（<see cref="SceneFlow.Progress"/>）を先に戻してから呼ぶ
        /// </summary>
        void Restore(string data);
    }

    /// <summary>
    /// 記憶する・思い出すの、場面の中の状態の取り出しと当て（設計書 5 節）。
    ///
    /// **自由に動ける所を覚えておく。** 字幕・二択・止まり・演出の途中で記憶した時は、その直前の自由に動ける所を残す。
    /// 覚えるのは場面を回している者（<see cref="SceneFlow"/>、場面 4 は <see cref="DiveDirector"/>）で、
    /// 自由に動けるフレームごとに写しを持つ。記憶するは、いま自由に動けるならその場で、そうでなければその写しを書く。
    /// どちらも持たない場面（村）は、押した時の状態をそのまま書く
    /// </summary>
    public static class SceneMemory
    {
        /// <summary>読み込んでいる場面の <see cref="ISceneMemory"/>。鍵の順</summary>
        public static List<ISceneMemory> Find()
        {
            var found = new List<ISceneMemory>();
            var all = UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (var i = 0; i < all.Length; i++)
            {
                var m = all[i] as ISceneMemory;
                if (m != null) found.Add(m);
            }
            found.Sort((a, b) => string.CompareOrdinal(a.MemoryKey, b.MemoryKey));
            return found;
        }

        /// <summary>どの演出も途中でないか</summary>
        public static bool Settled(List<ISceneMemory> parts)
        {
            if (parts == null) return true;
            for (var i = 0; i < parts.Count; i++)
                if (parts[i] != null && !parts[i].Settled) return false;
            return true;
        }

        /// <summary>演出ごとの状態を取り出す。null を返した物は入れない</summary>
        public static MemoPart[] Capture(List<ISceneMemory> parts)
        {
            var kept = new List<MemoPart>();
            if (parts != null)
            {
                for (var i = 0; i < parts.Count; i++)
                {
                    if (parts[i] == null) continue;
                    var data = parts[i].Capture();
                    if (data != null) kept.Add(new MemoPart(parts[i].MemoryKey, data));
                }
            }
            return kept.ToArray();
        }

        /// <summary>演出ごとの状態を当てる。中身が無い物にも null を渡して呼ぶ</summary>
        public static void Restore(List<ISceneMemory> parts, SceneMemo memo)
        {
            if (parts == null) return;
            for (var i = 0; i < parts.Count; i++)
                if (parts[i] != null) parts[i].Restore(memo != null ? memo.Part(parts[i].MemoryKey) : null);
        }

        /// <summary>立ち位置・向き・目の高さを memo へ写す</summary>
        public static void Hold(PlayerController player, SceneMemo memo)
        {
            if (player == null || memo == null) return;
            memo.at = player.transform.position;
            memo.turn = player.transform.eulerAngles.y;
            memo.headLimit = player.HeadYawLimit;
            memo.head = player.HeadYaw;
            memo.pitch = player.Pitch;
            memo.eye = player.EyeHeight;
            memo.moves = player.CanMove;
        }

        /// <summary>立ち位置・向き・目の高さを memo の形に置く。見回しは返す</summary>
        public static void Place(PlayerController player, SceneMemo memo)
        {
            if (player == null || memo == null) return;
            player.PlaceAt(memo.at, memo.turn, memo.headLimit, memo.head, memo.pitch, memo.eye);
            player.CanMove = memo.moves;
            player.CanLook = true;
        }

        /// <summary>
        /// 記憶する。いまの場面の、残してよい状態。自由に動ける所へまだ一度も来ていなければ null（場面の頭を書く）
        /// </summary>
        public static SceneMemo Take()
        {
            var flow = UnityEngine.Object.FindFirstObjectByType<SceneFlow>();
            if (flow != null) return flow.Kept();
            var dive = UnityEngine.Object.FindFirstObjectByType<DiveDirector>();
            if (dive != null) return dive.Kept();
            var player = UnityEngine.Object.FindFirstObjectByType<PlayerController>();
            if (player == null) return null;
            var parts = Find();
            if (!Settled(parts)) return null;
            var memo = new SceneMemo();
            Hold(player, memo);
            memo.parts = Capture(parts);
            return memo;
        }

        /// <summary>
        /// 思い出す。シーンを読んだ直後（最初のフレームを出す前）に、memo を場面へ当てる。
        /// 黒のまま当て、明けるのは場面を回している者（SceneFlow・DiveDirector）の Start。どちらも無い場面（村）は、ここで明けを始める
        /// </summary>
        public static void Resume(SceneMemo memo)
        {
            if (memo == null) return;
            var flow = UnityEngine.Object.FindFirstObjectByType<SceneFlow>();
            if (flow != null) { flow.Restore(memo); return; }
            var dive = UnityEngine.Object.FindFirstObjectByType<DiveDirector>();
            if (dive != null) { dive.Restore(memo); return; }
            Restore(Find(), memo);
            Place(UnityEngine.Object.FindFirstObjectByType<PlayerController>(), memo);
            var hud = UnityEngine.Object.FindFirstObjectByType<HudView>();
            if (hud == null || !Application.isPlaying) return;
            hud.SetFade(1f);
            hud.StartCoroutine(hud.FadeTo(0f, SceneFlow.FadeInSeconds));
        }
    }
}
