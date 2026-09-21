using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace HalfAware
{
    /// <summary>
    /// 場面 4 の段の進行。記憶を一本ずつ流し、目を留めた人の脇に板を出し、
    /// その板から次の人へ渡し、`切断` で自室へ返す。
    ///
    /// 渡り歩きの決まりは <see cref="DiveChain"/> が、体の道筋は <see cref="HostPath"/> が
    /// 持っている。ここはそれを場所・記憶・板・眩暈・色味へ繋ぐだけにしてある。
    ///
    /// **<see cref="PlayerController"/> より先に動かす。** カメラは体の子として
    /// 毎フレーム <c>(0, EyeHeight, lead)</c> へ置き直される。位置・向き・目の高さの三つを
    /// 揃えてから構えさせないと、あるフレームの高さで別のフレームの位置に目が乗り、
    /// 頭の中で絵が僅かに滑る。
    ///
    /// **`E 次へ` は無い。** 板が出ていないときの E は何もしない。
    /// 記憶は尽きるまで流れ、尽きたら端末が次を選ぶ
    /// </summary>
    [DefaultExecutionOrder(-15)]
    public sealed class DiveDirector : MonoBehaviour
    {
        [SerializeField] PlayerController player;
        [SerializeField] HudView hud;
        [Tooltip("右上に出す、いま潜っている人の行")]
        [SerializeField] TMP_Text caption;
        [SerializeField] DazeVolume daze;
        [Tooltip("記憶ごとのぼやけと色味を持つ Volume。中身を書き換えるのは HostBody")]
        [SerializeField] Volume volume;
        [SerializeField] HostBody body;
        [SerializeField] HoloPanel panel;
        [SerializeField] DiveRoster roster;
        [Tooltip("五つの場所。DiveIds.Places の並び")]
        [SerializeField] Transform[] places = new Transform[0];
        [Tooltip("十六の記憶。一覧の番号の並び")]
        [SerializeField] Transform[] takes = new Transform[0];

        [Header("渡り歩き")]
        [Tooltip("何人渡れば `切断` が `潜る` と同じ大きさになるか")]
        [SerializeField] int cutAfter = 8;

        [Header("板")]
        [Tooltip("目の中央からこの角度の内側にいる人だけ拾う。度")]
        [SerializeField] float watchAngle = 12f;
        [Tooltip("目を留めてから板が出るまで、外してから消えるまで。秒")]
        [SerializeField] float watchSeconds = 0.5f;

        [Header("眩暈")]
        [Tooltip("cutAfter 人まで渡ったときの眩暈の濃さ")]
        [SerializeField] float dazeMax = 0.8f;

        [Header("切り替え")]
        [Tooltip("場面の頭で黒から明ける秒数")]
        [SerializeField] float openSeconds = 0.6f;
        [Tooltip("記憶の頭から、名前を呼ぶ声が鳴るまでの秒数")]
        [SerializeField] float callAfter = 0.3f;
        [Tooltip("切断してから自室が出るまで。秒。裂ける見え方はまだ無いので暗転で代えている")]
        [SerializeField] float cutSeconds = 0.4f;
        [Tooltip("切断で読むシーン。空なら読まずに黒いまま止まる")]
        [SerializeField] string nextScene = "Rest";
        [Tooltip("潜っているあいだ左右に振れる首の角度。度。片側の値")]
        [SerializeField] float headYawLimit = HeadTurn.DefaultLimit;

        DiveChain chain;
        DiveEntry entry;
        Take take;
        Transform place;
        Mover[] movers = new Mover[0];
        /// <summary>目からカメラまでの前へのずれ。体を傾けたぶんを打ち消すのに要る</summary>
        float lead;
        /// <summary>記憶の頭からの秒。速さを掛けた後の、鍵打ちの時計の上での位置</summary>
        float clock;
        bool called;
        /// <summary>いま目を留めている相手。外していれば null</summary>
        Transform aimed;
        /// <summary>板がいま誰の脇に出ているか</summary>
        Transform shown;
        /// <summary>同じ相手を留めている（あるいは外している）秒</summary>
        float dwell;
        int lastStep;
        bool cutting;

        /// <summary>いま潜っている人。一覧での番号。動作確認から読む</summary>
        public int Current { get { return chain != null ? chain.Current : -1; } }

        /// <summary>これまでに渡った人数。動作確認から読む</summary>
        public int Hops { get { return chain != null ? chain.Hops : 0; } }

        /// <summary>`切断` が押せるか。動作確認から読む</summary>
        public bool CanCut { get { return chain != null && chain.CanCut; } }

        /// <summary>いまの記憶の再生位置。秒。動作確認から読む</summary>
        public float Clock { get { return clock; } }

        void Awake()
        {
            if (player == null || roster == null || roster.Count == 0)
            {
                Debug.LogError("DiveDirector: player か記憶の一覧が未接続", this);
                enabled = false;
            }
        }

        IEnumerator Start()
        {
            // EyeOffset がまだ 0 のこのときにしか、素のずれは読めない。
            // 眩暈の漂いが入ると目の置き場そのものが毎フレーム動く
            lead = player.Eye != null ? player.Eye.localPosition.z : 0f;
            // どちらも直列化されないので、組み立てではなくここで掛ける。
            // 体は鍵打ちが運ぶので歩かせない。首を制限しておけば、
            // PlayerController はマウスの向きを首と Pitch に入れ、体の向きには触らない
            player.CanMove = false;
            player.CanLook = true;
            player.HeadYawLimit = headYawLimit;
            // 見るのは sharedProfile の方。profile は読んだだけで空の写しが出来てしまうので、
            // 繋ぎ忘れていても null にならず、ぼやけも色味も掛からないまま素通りする
            if (volume == null || volume.sharedProfile == null)
                Debug.LogWarning("DiveDirector: Volume に profile が無い。記憶ごとのぼやけと色味が掛からない", this);
            else volume.weight = 1f;

            chain = new DiveChain(roster.Count, DiveIds.Listed, cutAfter, new System.Random());
            Shut();
            Play(chain.Current);

            // 場面 3 からは暗転して来る。明けるのはこちらの仕事
            if (hud == null) yield break;
            hud.SetFade(1f);
            yield return hud.FadeTo(0f, openSeconds);
        }

        void OnDisable()
        {
            if (body != null) body.Clear();
            if (player != null) player.CanLook = true;
        }

        void Update()
        {
            if (cutting || chain == null || take == null) return;
            clock += Time.deltaTime * Mathf.Max(0.05f, entry.speed);
            Carry();
            Drift();
            Voice();
            Watch();
            Choose();
            if (cutting || clock < entry.length) return;
            // 尽きたら端末が次を選ぶ。終わりの合図は入れず、そのまま次の記憶へ切り替える
            chain.Next();
            Play(chain.Current);
        }

        // ---- 記憶の切り替え --------------------------------------------------

        /// <summary>五つの場所と十六の記憶をすべて伏せる。開いたときの状態に頼らない</summary>
        void Shut()
        {
            for (var i = 0; i < places.Length; i++)
                if (places[i] != null) places[i].gameObject.SetActive(false);
            for (var i = 0; i < takes.Length; i++)
                if (takes[i] != null) takes[i].gameObject.SetActive(false);
        }

        /// <summary>i 番の記憶を頭から流す。前の記憶と場所は伏せる</summary>
        void Play(int i)
        {
            if (roster == null || i < 0 || i >= roster.Count) return;
            if (take != null) take.gameObject.SetActive(false);
            if (place != null) place.gameObject.SetActive(false);

            entry = roster[i];
            place = Where(entry.place);
            take = i < takes.Length && takes[i] != null ? takes[i].GetComponent<Take>() : null;
            if (take == null) { Debug.LogWarning("DiveDirector: 記憶 " + i + " が無い", this); return; }

            if (place != null) place.gameObject.SetActive(true);
            else Debug.LogWarning("DiveDirector: 場所が無い " + entry.place, this);
            take.gameObject.SetActive(true);
            // 同じ人へ戻れば頭から流し直す。Mover は有効になった瞬間に開始位置へ戻る
            movers = take.GetComponentsInChildren<Mover>(true);

            if (body != null) body.Apply(entry);
            if (caption != null) caption.text = entry.row ?? "";
            Deepen();

            clock = 0f;
            called = false;
            aimed = null;
            shown = null;
            dwell = 0f;
            lastStep = 0;
            if (panel != null) panel.Hide();
            // 首は記憶ごとに正面へ戻す。前の記憶で振り向いたままだと、
            // 次の記憶が始まった瞬間に壁を見ていることになる。
            // ReleaseHead は溜めた向きを体へ渡すが、体の向きはこの直後の Carry が置き直す
            player.ReleaseHead();
            player.HeadYawLimit = headYawLimit;
            player.Pitch = 0f;
            Carry();
        }

        /// <summary>id の場所。一覧の並びで探す</summary>
        Transform Where(string id)
        {
            var which = System.Array.IndexOf(DiveIds.Places, id);
            return which >= 0 && which < places.Length ? places[which] : null;
        }

        // ---- 主の体 ----------------------------------------------------------

        /// <summary>
        /// 鍵打ちの上へ体を運ぶ。
        ///
        /// **傾けたぶんを打ち消して置く。** 目はカメラとして体の子の
        /// <c>(0, EyeHeight, lead)</c> にあるので、体を x 回りに傾けると目もその弧を動く。
        /// 傾き 40 度・目の高さ 1.6・lead 0.22 では目が前へ 0.98 m、下へ 0.52 m ずれる。
        /// 鍵打ちは足元の位置として書かれていて、目はその真上にある前提なので、
        /// そのままでは壁を抜け、脇に立っている相手を通り越す
        /// </summary>
        void Carry()
        {
            if (take == null || place == null) return;
            var key = take.At(clock);
            var turn = place.eulerAngles.y + key.yaw;
            var foot = place.TransformPoint(key.position);
            var spin = Quaternion.Euler(key.pitch, turn, 0f);
            var flat = Quaternion.Euler(0f, turn, 0f);
            var offset = new Vector3(0f, key.eyeHeight, lead);
            // 傾けない体での目の座を守り、そこへ傾けた体を合わせる
            player.transform.position = foot + flat * offset - spin * offset;
            player.transform.rotation = spin;
            player.EyeHeight = key.eyeHeight;
        }

        /// <summary>
        /// 人と鳩を進める。<see cref="Mover"/> は自分の時計を持たないので、
        /// ここが渡さないと始まりの位置に止まったままになる
        /// </summary>
        void Drift()
        {
            for (var i = 0; i < movers.Length; i++)
                if (movers[i] != null) movers[i].Play(clock);
        }

        /// <summary>
        /// 名前を呼ぶ声。音源はまだ無いので、たいていは何も鳴らずに過ぎる。
        /// 声は借りた体の内側で聞こえるものなので、耳の位置から鳴らす
        /// </summary>
        void Voice()
        {
            if (called || take.Call == null || clock < callAfter) return;
            called = true;
            AudioSource.PlayClipAtPoint(take.Call, player.Eye != null ? player.Eye.position : transform.position);
        }

        /// <summary>渡るたびに眩暈を一段濃くする。cutAfter 人で最大に達し、以後は最大のまま</summary>
        void Deepen()
        {
            if (daze == null) return;
            var step = Mathf.Clamp01((float)chain.Hops / Mathf.Max(1, cutAfter)) * dazeMax;
            daze.Hold(step, step);
        }

        // ---- 板 --------------------------------------------------------------

        /// <summary>
        /// 目を留めた相手の脇に板を出す。一つの記憶に人が何人いても、
        /// 出るのは目を留めている一人だけ
        /// </summary>
        void Watch()
        {
            if (panel == null) return;
            var who = Nearest();
            if (who != aimed) { aimed = who; dwell = 0f; }
            else dwell += Time.deltaTime;
            if (dwell < watchSeconds) return;
            if (aimed != null)
            {
                if (shown != aimed)
                {
                    shown = aimed;
                    lastStep = 0;
                    panel.Show(aimed, entry.row, Row(Target(aimed)));
                }
                panel.Grow(chain.CutSize);
            }
            else if (shown != null)
            {
                shown = null;
                panel.Hide();
            }
        }

        /// <summary>目の中央にいちばん近い人。誰も角の内側にいなければ null</summary>
        Transform Nearest()
        {
            var eye = player.Eye;
            if (eye == null || take == null) return null;
            Transform best = null;
            var closest = watchAngle;
            var people = take.People;
            for (var i = 0; i < people.Length; i++)
            {
                var who = people[i];
                if (who == null || !who.gameObject.activeInHierarchy) continue;
                var toward = who.position + Vector3.up * 1.2f - eye.position;
                if (toward.sqrMagnitude < 1e-4f) continue;
                var apart = Vector3.Angle(eye.forward, toward);
                if (apart > closest) continue;
                closest = apart;
                best = who;
            }
            return best;
        }

        /// <summary>その人の飛び先。一覧に無ければ -1</summary>
        int Target(Transform who)
        {
            if (who == null) return -1;
            var seen = entry.seen;
            if (seen == null) return -1;
            for (var i = 0; i < seen.Length; i++)
                if (seen[i].name == who.name) return seen[i].target;
            return -1;
        }

        /// <summary>一覧の i 番の行。範囲の外なら空</summary>
        string Row(int i)
        {
            return roster != null && i >= 0 && i < roster.Count ? roster[i].row : "";
        }

        // ---- 選ぶ ------------------------------------------------------------

        /// <summary>
        /// 板が出ているあいだの入力。左右で `潜る` と `切断` を選び、E で決める。
        /// 板が出ていなければ E は何もしない
        /// </summary>
        void Choose()
        {
            if (panel == null || shown == null) return;
            var step = player.ChoiceStep;
            if (step != 0 && step != lastStep) panel.Select(step > 0 ? 1 : 0);
            lastStep = step;
            if (!player.InteractPressed) return;
            if (panel.Index == 1 && chain.CanCut) { Cut(); return; }
            var target = Target(shown);
            if (target < 0) return;
            chain.Hop(target);
            Play(chain.Current);
        }

        // ---- 切断 ------------------------------------------------------------

        /// <summary>ケーブルを抜く。渡った人数だけを場面 5 へ持ち越す</summary>
        void Cut()
        {
            if (cutting) return;
            cutting = true;
            DiveHandoff.Hops = chain.Hops;
            DiveHandoff.FromDive = true;
            StartCoroutine(Cutting());
        }

        IEnumerator Cutting()
        {
            if (panel != null) panel.Hide();
            // 抜けるあいだは見回しも受け付けない。視線はもう主のものではない
            player.CanLook = false;
            if (hud != null)
            {
                hud.SetPrompt(null);
                // 設計書の「画面が裂ける」はまだ作っていない。いまは暗転で代える
                yield return hud.FadeTo(1f, cutSeconds);
            }
            // 借りた体の色味とぼやけは、自室が出る前に落とす
            if (body != null) body.Clear();
            if (volume != null) volume.weight = 0f;
            if (!SceneExit.Continues(nextScene))
            {
                Debug.LogWarning("DiveDirector: 切断の行き先が空。黒いまま止まる", this);
                yield break;
            }
            SceneManager.LoadScene(SceneExit.Target(nextScene));
        }
    }
}
