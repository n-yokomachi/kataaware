using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace HalfAware.EditorTools
{
    /// <summary>
    /// 場面 4 の公営住宅の景色（敷地・隣の棟・遠景の書き割り）を、絵を撮って測る道具。
    ///
    /// <see cref="CheckDiveSky"/> と同じく、測り終えたら場所・記憶・空を必ず元へ戻す
    /// （<see cref="CheckDiveSky.Stage"/> を using で囲む）。一つの呼び出しには一つの測りだけを持たせ、
    /// 重い処理を抱え込まない。撮るカメラは <see cref="CheckDiveSky.Shot"/> の確認用のカメラ
    /// （fov 70・far 260・後処理なし・HideAndDontSave で、使い終わったら DestroyImmediate）
    /// </summary>
    public static class CheckDiveEstateView
    {
        /// <summary>撮る構図。名前、立ち位置（場所のローカル、目の高さ込み）、yaw（+z が 0 で東回り）、pitch（上が正）</summary>
        public struct View
        {
            public string name;
            public Vector3 at;
            public float yaw;
            public float pitch;

            public View(string name, Vector3 at, float yaw, float pitch)
            {
                this.name = name;
                this.at = at;
                this.yaw = yaw;
                this.pitch = pitch;
            }
        }

        /// <summary>階段の下。三階へ上がり始める点</summary>
        public static readonly Vector3 StairFoot = new Vector3(0.65f, 1.62f, -13.85f);

        /// <summary>見比べる構図。廊下から北・北東・北西、庭から東と西、階段の下から三方</summary>
        public static View[] Views()
        {
            var c = CheckDiveSky.Corridor;
            var y = CheckDiveSky.Yard;
            return new[]
            {
                new View("corridor_n", c, 0f, -8f),
                new View("corridor_ne", c, 45f, -8f),
                new View("corridor_nw", c, 315f, -8f),
                new View("yard_e", y, 90f, 4f),
                new View("yard_w", y, 270f, 4f),
                new View("stair_n", StairFoot, 0f, 2f),
                new View("stair_ne", StairFoot, 60f, 2f),
                new View("stair_nw", StairFoot, 300f, 2f),
            };
        }

        /// <summary>
        /// 構図を全部撮って <paramref name="dir"/> へ書き出す。名前の頭に <paramref name="tag"/>。
        /// 960×540 と、ゲームと同じ 320×180 の二枚ずつ。書き割りの絵もそのまま写しておく
        /// </summary>
        public static string Shoot(string dir, string tag)
        {
            Directory.CreateDirectory(dir);
            var done = new List<string>();
            using (var stage = new CheckDiveSky.Stage(DiveIds.Estate))
            {
                if (stage.Place == null) return "場所が無い";
                foreach (var v in Views())
                {
                    var eye = stage.Place.TransformPoint(v.at);
                    var yaw = stage.Place.eulerAngles.y + v.yaw;
                    CheckDiveSky.PairIn(DiveIds.Estate, eye, yaw, v.pitch, Path.Combine(dir, tag + "_" + v.name));
                    done.Add(v.name);
                }
            }
            var backdrop = "Assets/Textures/Dive/EstateBackdrop.png";
            if (File.Exists(backdrop)) File.Copy(backdrop, Path.Combine(dir, tag + "_backdrop.png"), true);
            return "撮った: " + string.Join(", ", done.ToArray());
        }

        /// <summary>
        /// 空の色そのままの画素の割合。廊下の真ん中から水平に北を見て、同じ構図を空だけで撮った絵と
        /// 画素ごとに比べ、三色の差がどれも 2 以下の画素を数える（960×540）
        /// </summary>
        public static string SkyShare()
        {
            using (var stage = new CheckDiveSky.Stage(DiveIds.Estate))
            {
                var sky = CheckDiveSky.SkyOf(DiveIds.Estate);
                sky.Apply(null);
                var eye = stage.Place.TransformPoint(CheckDiveSky.Corridor);
                var yaw = stage.Place.eulerAngles.y;
                var full = CheckDiveSky.Shot(eye, yaw, 0f, 960, 540, CameraClearFlags.Skybox, Color.black);
                var bare = CheckDiveSky.Shot(eye, yaw, 0f, 960, 540, CameraClearFlags.Skybox, Color.black, 0);
                var a = full.GetPixels32();
                var b = bare.GetPixels32();
                Object.DestroyImmediate(full);
                Object.DestroyImmediate(bare);
                var same = 0;
                for (var i = 0; i < a.Length; i++)
                    if (Mathf.Abs(a[i].r - b[i].r) <= 2 && Mathf.Abs(a[i].g - b[i].g) <= 2 && Mathf.Abs(a[i].b - b[i].b) <= 2)
                        same++;
                return string.Format("空の色そのままの画素: {0:0.0} %（廊下の真ん中から水平に北、960×540）", 100f * same / a.Length);
            }
        }

        /// <summary>
        /// 視差。廊下の目の高さで東（x+3）から西（x-3）へ 6 m 歩いたとき、北を向いたままの画面で
        /// 点が横に何画素動くか（960×540）。点は場所のローカル。書き割りの点は輪の板の上に取る
        /// </summary>
        public static string Parallax(string label, Vector3 point)
        {
            var dive = GameObject.Find("Dive");
            var place = dive != null ? dive.transform.Find("Places/" + DiveIds.Estate) : null;
            if (place == null) return "場所が無い";
            var go = new GameObject("CheckDiveEstateEye");
            go.hideFlags = HideFlags.HideAndDontSave;
            var cam = go.AddComponent<Camera>();
            cam.enabled = false;
            try
            {
                cam.fieldOfView = CheckDiveSky.Fov;
                cam.nearClipPlane = CheckDiveSky.Near;
                cam.farClipPlane = CheckDiveSky.Far;
                cam.aspect = 960f / 540f;
                var c = CheckDiveSky.Corridor;
                var world = place.TransformPoint(point);
                go.transform.rotation = place.rotation;
                go.transform.position = place.TransformPoint(new Vector3(c.x + 3f, c.y, c.z));
                var east = cam.WorldToViewportPoint(world);
                go.transform.position = place.TransformPoint(new Vector3(c.x - 3f, c.y, c.z));
                var west = cam.WorldToViewportPoint(world);
                var dist = new Vector2(point.x - c.x, point.z - c.z).magnitude;
                return string.Format("{0}: {1:0.0} 画素（中心からの水平の距離 {2:0.0} m、画面の x {3:0}→{4:0}）",
                    label, (west.x - east.x) * 960f, dist, east.x * 960f, west.x * 960f);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        /// <summary>
        /// 書き割りの輪の板の上の点。中心から見て方角 <paramref name="az"/> 度、高さ <paramref name="y"/>
        /// </summary>
        public static Vector3 OnRing(float az, float y)
        {
            var c = BuildDive.EstateFarCentre;
            var dir = BackdropRing.Heading(az);
            var d = BackdropRing.Along(c, dir, c, BuildDive.EstateFarRadius, BuildDive.EstateFarPanels);
            var p = c + dir * d;
            return new Vector3(p.x, y, p.z);
        }

        /// <summary>場所の中の mesh の頂点を、名前ごとに並べる</summary>
        public static string Vertices()
        {
            var dive = GameObject.Find("Dive");
            var place = dive != null ? dive.transform.Find("Places/" + DiveIds.Estate) : null;
            if (place == null) return "場所が無い";
            long all = 0;
            var row = new System.Text.StringBuilder();
            foreach (var mf in place.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.sharedMesh == null) continue;
                all += mf.sharedMesh.vertexCount;
                row.AppendFormat("{0} {1} / ", mf.name, mf.sharedMesh.vertexCount);
            }
            return "頂点 " + all + " … " + row;
        }

        /// <summary>
        /// 他の四つの場所を撮る。場所の地の真ん中、目の高さ 1.6 m から四方を水平に、320×180 で
        /// </summary>
        public static string ShootOthers(string dir, string tag)
        {
            Directory.CreateDirectory(dir);
            var done = new List<string>();
            foreach (var id in DiveIds.Places)
            {
                if (id == DiveIds.Estate) continue;
                using (var stage = new CheckDiveSky.Stage(id))
                {
                    if (stage.Place == null) continue;
                    var sky = CheckDiveSky.SkyOf(id);
                    sky.Apply(null);
                    var bounds = new Bounds(stage.Place.position, Vector3.zero);
                    foreach (var r in stage.Place.GetComponentsInChildren<Renderer>())
                        bounds.Encapsulate(r.bounds);
                    var eye = new Vector3(bounds.center.x, stage.Place.position.y + 1.6f, bounds.center.z);
                    for (var k = 0; k < 4; k++)
                    {
                        var shot = CheckDiveSky.Shot(eye, stage.Place.eulerAngles.y + k * 90f, 0f, 320, 180,
                            sky.skybox != null ? CameraClearFlags.Skybox : CameraClearFlags.SolidColor, sky.flat);
                        CheckDiveSky.Save(shot, Path.Combine(dir, tag + "_" + id + "_" + k + ".png"));
                        Object.DestroyImmediate(shot);
                    }
                    done.Add(id);
                }
            }
            return "撮った: " + string.Join(", ", done.ToArray());
        }

        /// <summary>二つの印で撮った他の場所の絵の、画素の差（0〜255 の平均と最大）</summary>
        public static string DiffOthers(string dir, string before, string after)
        {
            var row = new System.Text.StringBuilder();
            foreach (var id in DiveIds.Places)
            {
                if (id == DiveIds.Estate) continue;
                var mean = 0f;
                var max = 0f;
                for (var k = 0; k < 4; k++)
                {
                    var pa = Path.Combine(dir, before + "_" + id + "_" + k + ".png");
                    var pb = Path.Combine(dir, after + "_" + id + "_" + k + ".png");
                    if (!File.Exists(pa) || !File.Exists(pb)) { mean = -1f; break; }
                    var a = new Texture2D(2, 2);
                    var b = new Texture2D(2, 2);
                    a.LoadImage(File.ReadAllBytes(pa));
                    b.LoadImage(File.ReadAllBytes(pb));
                    var d = CheckDiveSky.Diff(a, b);
                    Object.DestroyImmediate(a);
                    Object.DestroyImmediate(b);
                    mean = Mathf.Max(mean, d.x);
                    max = Mathf.Max(max, d.y);
                }
                row.AppendFormat("{0} 平均 {1:0.00} 最大 {2:0} / ", id, mean, max);
            }
            return row.ToString();
        }
    }
}
