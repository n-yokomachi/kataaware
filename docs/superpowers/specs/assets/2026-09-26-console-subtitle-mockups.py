import base64, os
here = os.path.dirname(os.path.abspath(__file__))
root = os.path.dirname(here)

def b64(rel):
    return base64.b64encode(open(os.path.join(root, rel), 'rb').read()).decode()

BG_MENU = b64('alley/A_after_start_look_right.png')
BG_TALK = b64('talk2/walk/m0_talk0_Mother.png')
BG_MONO = b64('kitchen/place_window_floor_sun.png')

LOG = [
    ("examine", "端末", "世間ではホロコンソールが人気だが私はもっぱら物理モニターを使っている"),
    ("examine", "端末", "というのもほら、"),
    ("examine", "端末", "こうして反射で自分の顔が見られるからだ"),
    ("talk", "ハンナ", "メイ！　忘れもの！　上がっておいで！"),
    ("talk", "メイ", "えーなにー？"),
    ("talk", "ハンナ", "水筒！"),
    ("talk", "メイ", "投げてよー"),
    ("talk", "ハンナ", "投げません。いいから上がっておいで"),
]


def rows(fmt):
    last = len(LOG) - 1
    return "\n".join(fmt(i, k, w, t, i == last) for i, (k, w, t) in enumerate(LOG))


A_ROWS = rows(lambda i, k, w, t, last: (
    f'<div class="a-row{" a-last" if last else ""}"><span class="a-tag">{"調べる" if k == "examine" else "会話"}</span>'
    f'<span class="a-who">{w}</span><span>{t}</span></div>'))
B_ROWS = rows(lambda i, k, w, t, last: (
    f'<div class="b-row{" b-last" if last else ""}"><span class="b-who">{w}</span><span>{t}</span></div>'))
C_ROWS = rows(lambda i, k, w, t, last: (
    f'<div class="c-row{" c-last" if last else ""}"><span class="c-no">#{231 + i}</span>'
    f'<span class="c-who">{w}</span><span>{t}</span></div>'))

CSS = """
:root{--bg:#101114;--fg:#e8e6e1;--muted:#9a978f}
body{margin:0;background:var(--bg);color:var(--fg);font-family:"Noto Sans JP",sans-serif;padding:24px 16px 60px}
h1{font-family:"Shippori Mincho",serif;font-weight:400;font-size:24px;margin:0 0 6px}
h2{font-family:"Shippori Mincho",serif;font-weight:400;font-size:19px;margin:36px 0 6px}
h3{font-family:"Shippori Mincho",serif;font-weight:400;font-size:17px;margin:28px 0 6px;color:#d8d4cb}
p,li{font-size:14px;line-height:1.7;color:#cfccc4;max-width:960px}
.stage{position:relative;width:100%;max-width:960px;aspect-ratio:16/9;background-size:cover;background-position:center;image-rendering:pixelated;overflow:hidden;border:1px solid #333}
.stage *{box-sizing:border-box}
.menu-bg{background-image:url(data:image/png;base64,BG_MENU)}
.talk-bg{background-image:url(data:image/png;base64,BG_TALK)}
.mono-bg{background-image:url(data:image/png;base64,BG_MONO)}
/* A 端末 */
.a-dim{position:absolute;inset:0;background:rgba(3,10,14,.74)}
.a-scan{position:absolute;inset:0;background:repeating-linear-gradient(0deg,rgba(120,230,240,.035) 0 1px,transparent 1px 3px)}
.a-panel{position:absolute;inset:6% 7%;border:1px solid rgba(120,220,230,.45);font-family:"Share Tech Mono","Noto Sans JP",monospace}
.a-panel:before,.a-panel:after{content:"";position:absolute;width:14px;height:14px;border:2px solid #7fe3ec}
.a-panel:before{top:-2px;left:-2px;border-right:0;border-bottom:0}
.a-panel:after{bottom:-2px;right:-2px;border-left:0;border-top:0}
.a-head{display:flex;justify-content:space-between;padding:10px 14px 0;font-size:12px;color:#7fe3ec;letter-spacing:.08em}
.a-btns{display:flex;gap:10px;padding:12px 14px}
.a-btn{flex:1;border:1px solid rgba(127,227,236,.55);color:#cff7fa;text-align:center;padding:8px 0;font-size:14px;font-family:"Noto Sans JP",sans-serif;letter-spacing:.1em}
.a-btn.sel{background:#7fe3ec;color:#061114}
.a-log{position:absolute;left:14px;right:14px;top:92px;bottom:14px;border:1px solid rgba(127,227,236,.3);padding:10px 22px 10px 12px;display:flex;flex-direction:column;justify-content:flex-end;gap:5px;font-family:"Noto Sans JP",sans-serif;-webkit-mask:linear-gradient(to bottom,transparent 0,#000 22%);mask:linear-gradient(to bottom,transparent 0,#000 22%)}
.a-row{display:flex;gap:10px;font-size:13px;color:#b9d9dc;line-height:1.5}
.a-last{color:#fff}
.a-tag{font-family:"Share Tech Mono",monospace;font-size:11px;color:#061114;background:rgba(127,227,236,.7);padding:0 5px;height:17px;line-height:17px;flex:none}
.a-who{color:#7fe3ec;flex:none;min-width:3.5em}
.a-bar{position:absolute;right:20px;top:100px;bottom:22px;width:3px;background:rgba(127,227,236,.15)}
.a-bar i{position:absolute;left:0;right:0;bottom:0;height:22%;background:#7fe3ec}
/* B 字幕の帯 */
.b-dim{position:absolute;inset:0;background:rgba(0,0,0,.84)}
.b-title{position:absolute;top:6%;width:100%;text-align:center;font-family:"Shippori Mincho",serif;font-size:13px;letter-spacing:.5em;color:#8d8a84}
.b-menu{position:absolute;top:13%;width:100%;display:flex;justify-content:center;gap:44px;font-family:"Shippori Mincho",serif;font-size:21px}
.b-menu span{color:rgba(255,255,255,.42);padding-bottom:6px}
.b-menu span.sel{color:#fff;border-bottom:1px solid #fff}
.b-rule{position:absolute;top:27%;left:18%;right:18%;border-top:1px solid rgba(255,255,255,.18)}
.b-log{position:absolute;top:30%;bottom:7%;left:18%;right:18%;display:flex;flex-direction:column;justify-content:flex-end;gap:9px;-webkit-mask:linear-gradient(to bottom,transparent 0,#000 30%);mask:linear-gradient(to bottom,transparent 0,#000 30%)}
.b-row{font-size:15px;line-height:1.6;color:rgba(255,255,255,.62)}
.b-last{color:#fff}
.b-who{color:rgba(255,255,255,.4);margin-right:1em;font-size:13px}
/* C 記憶チップ */
.c-dim{position:absolute;inset:0;background:rgba(10,9,8,.7)}
.c-panel{position:absolute;inset:7% 9%;background:linear-gradient(180deg,#2a2b2e,#1d1e21);border:1px solid #45464a;box-shadow:inset 0 0 0 3px #17181a,0 8px 30px rgba(0,0,0,.6)}
.c-screw{position:absolute;width:7px;height:7px;border-radius:50%;background:#55565a}
.c-head{padding:12px 18px 0;display:flex;justify-content:space-between;align-items:center;font-family:"Share Tech Mono",monospace;font-size:12px;color:#d9a441;letter-spacing:.1em}
.c-led{display:inline-block;width:7px;height:7px;border-radius:50%;background:#ffb347;box-shadow:0 0 6px #ffb347;margin-right:7px}
.c-btns{display:flex;gap:12px;padding:12px 18px}
.c-btn{flex:1;height:48px;background:#3a3b3f;border:1px solid #58595e;clip-path:polygon(0 0,88% 0,100% 30%,100% 100%,0 100%);padding:6px 10px}
.c-btn b{display:block;font-family:"Share Tech Mono",monospace;font-size:10px;color:#9d9a93;font-weight:400;letter-spacing:.15em}
.c-btn span{font-size:14px;color:#ece8df}
.c-btn.sel{background:#d9a441}
.c-btn.sel b{color:#3a2a0a}
.c-btn.sel span{color:#1a1206}
.c-log{position:absolute;left:18px;right:18px;top:112px;bottom:16px;background:#141517;border:1px solid #3a3b3f;padding:8px 20px 8px 8px;display:flex;flex-direction:column;justify-content:flex-end;-webkit-mask:linear-gradient(to bottom,transparent 0,#000 20%);mask:linear-gradient(to bottom,transparent 0,#000 20%)}
.c-row{display:flex;gap:10px;font-size:13px;line-height:1.5;padding:3px 6px;color:#cfcac0}
.c-row:nth-child(odd){background:rgba(255,255,255,.025)}
.c-last{color:#fff;background:rgba(217,164,65,.14)!important}
.c-no{font-family:"Share Tech Mono",monospace;color:#d9a441;font-size:12px;flex:none;width:3.4em}
.c-who{color:#a8a399;flex:none;min-width:3.5em}
.c-bar{position:absolute;right:24px;top:120px;bottom:24px;width:4px;background:#2c2d30}
.c-bar i{position:absolute;left:0;right:0;bottom:0;height:24%;background:#d9a441}
/* 字幕の案 */
.now{position:absolute;left:0;right:0;bottom:0;height:22%;background:#000;display:flex;align-items:center;justify-content:center;font-size:19px;color:#fff}
.s1{position:absolute;left:0;right:0;bottom:8%;text-align:center}
.s1 .who{display:block;font-size:12px;color:rgba(255,255,255,.72);letter-spacing:.2em;margin-bottom:4px;text-shadow:0 0 4px #000}
.s1 .txt{font-size:20px;color:#fff;text-shadow:0 0 2px #000,0 0 6px #000,1px 1px 0 #000,-1px -1px 0 #000,1px -1px 0 #000,-1px 1px 0 #000}
.s1 .next{display:inline-block;margin-left:10px;font-size:11px;color:rgba(255,255,255,.7);vertical-align:middle}
.s2{position:absolute;left:0;right:0;bottom:0;height:24%;background:linear-gradient(to top,rgba(0,0,0,.82) 45%,rgba(0,0,0,0));padding:28px 12% 0}
.s2 .who{font-size:13px;color:#e6c7a0;margin-bottom:4px}
.s2 .txt{font-size:19px;color:#fff;line-height:1.6}
.s2 .next{position:absolute;right:11%;bottom:14px;font-size:12px;color:rgba(255,255,255,.6)}
.s3shade{position:absolute;right:0;top:0;bottom:0;width:34%;background:linear-gradient(to left,rgba(0,0,0,.62),rgba(0,0,0,0))}
.s3{position:absolute;right:7%;top:9%;height:84%;writing-mode:vertical-rl;font-family:"Shippori Mincho",serif;font-size:19px;line-height:2.1;color:#fff;text-shadow:0 0 3px #000,0 0 10px rgba(0,0,0,.9);letter-spacing:.12em}
.s3 .dim{color:rgba(255,255,255,.45)}
"""

HTML = f"""<!doctype html><html lang="ja"><head><meta charset="utf-8"><title>メニューと字幕の案</title>
<meta name="viewport" content="width=device-width,initial-scale=1">
<link href="https://fonts.googleapis.com/css2?family=Noto+Sans+JP:wght@400;700&family=Shippori+Mincho&family=Share+Tech+Mono&display=swap" rel="stylesheet">
<style>{CSS.replace('BG_MENU', BG_MENU).replace('BG_TALK', BG_TALK).replace('BG_MONO', BG_MONO)}</style></head><body>
<h1>TAB のメニューと字幕の案</h1>
<p>背景はどれも実際のゲームの画面（320×180 を拡大）。メニューは三つ、字幕は三つ。メニューと字幕は同じ系統で揃えると、画面の作りに一貫性が出る（例: A の端末のメニューには 2 の字幕、B の帯のメニューには 1 か 3 の字幕）。</p>

<h2>メニュー</h2>
<p>中身はどれも同じ。上にセーブ・ロード・タイトルへ戻る・デバッグの四つのボタン、下に大きなログの枠。ログは最新の行が下にあり、上へスクロールするとさかのぼれる（枠の上のほうは古い行が薄れて消える）。</p>

<h3>A　端末</h3>
<p>主人公が記憶を売り買いする端末と、場面 4 の板に揃えた、薄い青緑の線の画面。左上にいまの時刻と場所。ログの行に「調べる」「会話」の札を付ける。</p>
<div class="stage menu-bg"><div class="a-dim"></div><div class="a-scan"></div>
<div class="a-panel"><div class="a-head"><span>2166/08/15 18:42　倫敦・自室</span><span>TAB　閉じる</span></div>
<div class="a-btns"><div class="a-btn sel">セーブ</div><div class="a-btn">ロード</div><div class="a-btn">タイトルへ戻る</div><div class="a-btn">デバッグ</div></div>
<div class="a-log">{A_ROWS}</div><div class="a-bar"><i></i></div></div></div>

<h3>B　字幕の帯</h3>
<p>冒頭のカード（制作・題・日時）と同じ、黒と白としっぽり明朝だけの静かな画面。ボタンは字だけで、選んでいる物に下線。ログは本編の字幕と同じ字で、話した人の名前を薄く添える。作品の空気をいちばん壊さない。</p>
<div class="stage menu-bg"><div class="b-dim"></div><div class="b-title">HALF AWARE</div>
<div class="b-menu"><span class="sel">セーブ</span><span>ロード</span><span>タイトルへ戻る</span><span>デバッグ</span></div>
<div class="b-rule"></div><div class="b-log">{B_ROWS}</div></div>

<h3>C　記憶チップ</h3>
<p>場面 1・2 の記憶チップと、それを挿すハブの金属の筐体に揃えた、手触りのある画面。ボタンはチップの形、ログはチップのラベルのように番号付きの行。橙の灯りで、端末の青緑とは違う温度にする。</p>
<div class="stage menu-bg"><div class="c-dim"></div>
<div class="c-panel"><i class="c-screw" style="left:8px;top:8px"></i><i class="c-screw" style="right:8px;top:8px"></i><i class="c-screw" style="left:8px;bottom:8px"></i><i class="c-screw" style="right:8px;bottom:8px"></i>
<div class="c-head"><span><span class="c-led"></span>MEMORY HUB　LOG</span><span>2166/08/15 18:42</span></div>
<div class="c-btns"><div class="c-btn sel"><b>SAVE</b><span>セーブ</span></div><div class="c-btn"><b>LOAD</b><span>ロード</span></div><div class="c-btn"><b>TITLE</b><span>タイトルへ戻る</span></div><div class="c-btn"><b>DEBUG</b><span>デバッグ</span></div></div>
<div class="c-log">{C_ROWS}</div><div class="c-bar"><i></i></div></div></div>

<h2>字幕</h2>
<p>いまの字幕は、画面の下を黒い帯で塗って白い字を真ん中に出す形（下の「いま」）。帯が画面の四分の一ほどを隠し、話した人の名前も字の中の「ハンナ「……」」の形で出ている。</p>
<h3>いま</h3>
<div class="stage talk-bg"><div class="now">ハンナ「メイ！　忘れもの！　上がっておいで！」</div></div>

<h3>1　映画の字幕</h3>
<p>帯をやめて、字に細い縁取りと影を付け、画面の下に直に置く。話した人の名前は字の上に小さく。送れる時だけ小さな ▼ を出す。絵を隠す面積がいちばん小さい。</p>
<div class="stage talk-bg"><div class="s1"><span class="who">ハンナ</span><span class="txt">メイ！　忘れもの！　上がっておいで！</span><span class="next">▼</span></div></div>

<h3>2　ノベルの枠</h3>
<p>下から上へ薄れる黒のグラデーションの上に、左寄せで名前と台詞。名前は台詞と色を変える。行の長い独白や、場面 4 のように会話が続く所で読みやすい。端末のメニュー（A）と合う。</p>
<div class="stage talk-bg"><div class="s2"><div class="who">ハンナ</div><div class="txt">メイ！　忘れもの！　上がっておいで！</div><div class="next">E　送る ▼</div></div></div>

<h3>3　独白だけ縦書き</h3>
<p>会話は 1 か 2 の横書きのまま、主人公の独白だけを、画面の右に縦書きのしっぽり明朝で出す。原作が短編小説なので、独白が本の頁のように読める。独白と会話が一目で分かれる。前の行は薄く残して、二、三行で消す。</p>
<div class="stage menu-bg"><div class="s3shade"></div><div class="s3"><span class="dim">世間ではホロコンソールが人気だが<br>私はもっぱら物理モニターを使っている<br></span>というのもほら、</div></div>

<p style="margin-top:32px">どの案も、組み合わせを変えられる（例: B の静けさに A の札だけ入れる、1 の字幕に 3 の縦書きの独白を合わせる、など）。</p>
</body></html>"""

open(os.path.join(here, 'menu_designs.html'), 'w', encoding='utf-8').write(HTML)
print(len(HTML))
