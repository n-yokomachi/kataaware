#!/usr/bin/env bash
# HALF AWARE 音素材の生成スクリプト（ffmpeg 8.0.1 のみで完結）
# 使い方: bash make-ambience.sh           （全部）
#         bash make-ambience.sh village   （5 節の村と麦畑の 3 つだけ）
#         bash make-ambience.sh gravel    （6 節の未舗装の路地の足音だけ）
#         bash make-ambience.sh grass     （7 節の芝の足音だけ）
#         bash make-ambience.sh garden    （8 節の場面 6 の庭の 4 つだけ）
# 出力先は OUT_DIR 直下。中間ファイルは OUT_DIR/tmp に置く。
set -euo pipefail

PART="${1:-all}"

# ---------------------------------------------------------------------------
# 入力パス（ここだけ書き換えれば別環境でも動く）
# ---------------------------------------------------------------------------
SRC_ROOMTONE="D:/Downloads/xomxomski-ambient-empty-room-noise-sound-effect-429845.mp3"
SRC_CROWD="D:/Downloads/freesound_community-crowd_talking-6762.mp3"
SRC_OST_DIR="D:/Downloads/HALF_AWARE_OST"

# 村と麦畑の素材は unity/RawAssets/audio/pixabay/ に Pixabay の元の名前のまま置く（git に入れない）
SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
SRC_PIXABAY_DIR="${SRC_PIXABAY_DIR:-$SCRIPT_DIR/../unity/RawAssets/audio/pixabay}"
SRC_WHICHFORD="$SRC_PIXABAY_DIR/freesound_community-030510whichford-18349.mp3"
SRC_WHEAT="$SRC_PIXABAY_DIR/freesound_community-wheat-in-the-wind-7159.mp3"
SRC_GATE="$SRC_PIXABAY_DIR/dobcommunications-creaky-wooden-gate-opens-170210.mp3"
SRC_GRAVEL="$SRC_PIXABAY_DIR/freesound_community-walking-on-a-road-with-gravel-01-30100.mp3"
SRC_GRASS="$SRC_PIXABAY_DIR/freesound_community-walking-through-grass-80308.mp3"
# 8 節（場面 6 の庭の記憶）の素材
SRC_HOSE="$SRC_PIXABAY_DIR/freesound_community-watering-62546.mp3"
SRC_HOSE_STOP="$SRC_PIXABAY_DIR/freesound_community-hose-sounds-24388.mp3"
SRC_GLITCH="$SRC_PIXABAY_DIR/freesound_community-computer-glitch-corrupted-file-96176.mp3"
SRC_CUT="$SRC_PIXABAY_DIR/freesound_community-glitch-49142.mp3"
# 6 節で大きさを揃える相手（村の芝と庭で鳴らしている柔らかい足音）
STEPS_DIR="${STEPS_DIR:-$SCRIPT_DIR/../unity/Assets/Audio}"

OUT_DIR="${OUT_DIR:-./out-ambience}"   # 出来た物を unity/Assets/Audio/ と unity/Assets/Audio/Music/ へ写す
TMP_DIR="$OUT_DIR/tmp"
ANALYSIS_DIR="$OUT_DIR/analysis"
mkdir -p "$TMP_DIR" "$ANALYSIS_DIR"

# 一歩あたりの大きさ。0.5 秒に伸ばして 10 回繰り返した物の integrated loudness（LUFS）。
# 6 節（砂利）と 7 節（芝）のどちらからも呼ぶので、PART の分岐の外に置く
step_loudness () {
  ffmpeg -hide_banner -nostats -i "$1" -af "aresample=44100,apad=whole_dur=0.5,aloop=loop=9:size=22050,ebur128" -f null - 2>&1 \
    | grep -E "^\s+I:" | tail -1 | grep -oE '[-0-9.]+' | head -1
}

# 1〜4 節は PART=all のときだけ（字下げはせず、4 節の末尾で閉じる）
if [ "$PART" = "all" ]; then

# ---------------------------------------------------------------------------
# 1. RoomTone.wav — 自室の空気の音（ループ）
#    元は 59.7秒。スペクトルと1秒/0.05秒窓のピーク値を調べた結果、目立つ「事件」
#    （足音・ドア等の広帯域ピーク）は無し。8kHz以上の帯域にごく微弱な
#    （ノイズフロアから10~15dBほどの)高域ティックが約18.6/28.0/33.75/53.5秒に
#    存在するが、いずれもブロードバンドのピークレベルには表れないレベル
#    （-54~-66dBFS、全体ノイズフロア-90dB付近からすると無視できる）。
#    3.0〜33.5秒（30.5秒）を採用: 冒頭2秒の録音開始アーティファクトと、
#    33.75秒・53.5秒のティック、末尾の編集点を避けている。
# ---------------------------------------------------------------------------
echo "=== RoomTone.wav ==="
ROOM_START=3.0
ROOM_LEN=30.5
ROOM_FADE=1.5
ROOM_BODYEND=$(awk "BEGIN{print $ROOM_LEN-$ROOM_FADE}")

ffmpeg -y -v error -i "$SRC_ROOMTONE" -ss $ROOM_START -t $ROOM_LEN -ac 1 -ar 22050 -c:a pcm_s16le "$TMP_DIR/room_seg.wav"

# 末尾1.5秒を頭に重ねる等パワー(qsin)クロスフェードで輪にする
ffmpeg -y -v error -i "$TMP_DIR/room_seg.wav" -filter_complex "
[0:a]asplit=2[a][b];
[a]atrim=start=0:end=${ROOM_BODYEND},asetpts=PTS-STARTPTS[body];
[b]atrim=start=${ROOM_BODYEND}:end=${ROOM_LEN},asetpts=PTS-STARTPTS[tail];
[tail][body]acrossfade=d=${ROOM_FADE}:curve1=qsin:curve2=qsin[out]
" -map "[out]" -c:a pcm_s16le "$TMP_DIR/room_loop.wav"

# 実効値(RMS)を-24dBFS前後に揃える
ROOM_RMS=$(ffmpeg -hide_banner -i "$TMP_DIR/room_loop.wav" -af "astats=metadata=0:reset=0" -f null - 2>&1 | grep "RMS level dB" | tail -1 | grep -oE '[-0-9.]+$')
ROOM_GAIN=$(awk "BEGIN{print -24 - ($ROOM_RMS)}")
echo "RoomTone: measured RMS=${ROOM_RMS}dB, applying gain=${ROOM_GAIN}dB"
ffmpeg -y -v error -i "$TMP_DIR/room_loop.wav" -af "volume=${ROOM_GAIN}dB" -c:a pcm_s16le "$OUT_DIR/RoomTone.wav"

# ---------------------------------------------------------------------------
# 2. CrowdLoop.wav — 群衆のざわめき（ループ）
#    元は89.9秒。ピーク値の解析で目立つ出来事（笑い声・掛け声等と思われる
#    ブロードバンドの単発ピーク）を4箇所検出: 約6.6~6.7秒(-18.1dBFS、最大)、
#    24.3秒(-21.5dBFS)、49.8秒(-19.3dBFS)、62.2秒(-20.7dBFS)。いずれも
#    0.1~0.2秒ほどの短い単発ピークで、周辺の-25~-28dBFSに対し3~9dBほど
#    突出している。45~60秒の切り出しでは全てを避けることは不可能（最大の
#    間隔でも25秒しかない）ため、最大のピーク(6.6~6.7秒)と末尾の
#    フェードアウト(86秒以降)を避け、7.0〜57.0秒(50秒)を採用。
#    この区間には24.3秒・49.8秒の2箇所の中程度のピークが含まれる
#    （群衆の中の短い笑い声・掛け声程度で、ループ音として不自然ではない
#    範囲と判断）。スペクトルは低域に密集した連続的な帯域で、個々の単語が
#    識別できる構造は見られず、典型的な"クラウドウォラ"（不明瞭な群衆の声）
#    の特徴。再生確認はできないため言語の聞き取りはできなかったが、
#    素材名(crowd_talking, Pixabay)と収録意図から英語の群衆と考えて矛盾はない。
# ---------------------------------------------------------------------------
echo "=== CrowdLoop.wav ==="
CROWD_START=7.0
CROWD_LEN=50.0
CROWD_FADE=2.0
CROWD_BODYEND=$(awk "BEGIN{print $CROWD_LEN-$CROWD_FADE}")

ffmpeg -y -v error -i "$SRC_CROWD" -ss $CROWD_START -t $CROWD_LEN -ac 1 -ar 22050 -c:a pcm_s16le "$TMP_DIR/crowd_seg.wav"

ffmpeg -y -v error -i "$TMP_DIR/crowd_seg.wav" -filter_complex "
[0:a]asplit=2[a][b];
[a]atrim=start=0:end=${CROWD_BODYEND},asetpts=PTS-STARTPTS[body];
[b]atrim=start=${CROWD_BODYEND}:end=${CROWD_LEN},asetpts=PTS-STARTPTS[tail];
[tail][body]acrossfade=d=${CROWD_FADE}:curve1=qsin:curve2=qsin[out]
" -map "[out]" -c:a pcm_s16le "$TMP_DIR/crowd_loop.wav"

# 実効値(RMS)を-22dBFS前後に揃える
CROWD_RMS=$(ffmpeg -hide_banner -i "$TMP_DIR/crowd_loop.wav" -af "astats=metadata=0:reset=0" -f null - 2>&1 | grep "RMS level dB" | tail -1 | grep -oE '[-0-9.]+$')
CROWD_GAIN=$(awk "BEGIN{print -22 - ($CROWD_RMS)}")
echo "CrowdLoop: measured RMS=${CROWD_RMS}dB, applying gain=${CROWD_GAIN}dB"
ffmpeg -y -v error -i "$TMP_DIR/crowd_loop.wav" -af "volume=${CROWD_GAIN}dB" -c:a pcm_s16le "$OUT_DIR/CrowdLoop.wav"

# ---------------------------------------------------------------------------
# 3. 蚤の市スピーカー用インパルス応答(IR)を合成
#    レンガ壁の広場を想定: 初期反射5本(3/9/17/28/42ms) + 拡散する残響尾部
#    (ピンクノイズ, 指数減衰 RT60 ~0.7秒)。すべて ffmpeg のフィルタのみで生成。
# ---------------------------------------------------------------------------
echo "=== ir.wav（畳み込み用インパルス応答）==="
ffmpeg -y -v error -filter_complex "
anoisesrc=d=1.0:c=pink:r=44100:a=1,volume=eval=frame:volume='pow(10,-3.0*t)',adelay=8|8:all=1,volume=0.5[tail];
anoisesrc=d=0.003:c=white:r=44100:a=1[t1s];
[t1s]afade=t=out:st=0:d=0.003:curve=exp,adelay=3|3:all=1,volume=0.9[t1];
anoisesrc=d=0.003:c=white:r=44100:a=1[t2s];
[t2s]afade=t=out:st=0:d=0.003:curve=exp,adelay=9|9:all=1,volume=0.6[t2];
anoisesrc=d=0.003:c=white:r=44100:a=1[t3s];
[t3s]afade=t=out:st=0:d=0.003:curve=exp,adelay=17|17:all=1,volume=0.42[t3];
anoisesrc=d=0.003:c=white:r=44100:a=1[t4s];
[t4s]afade=t=out:st=0:d=0.003:curve=exp,adelay=28|28:all=1,volume=0.28[t4];
anoisesrc=d=0.003:c=white:r=44100:a=1[t5s];
[t5s]afade=t=out:st=0:d=0.003:curve=exp,adelay=42|42:all=1,volume=0.18[t5];
[tail][t1][t2][t3][t4][t5]amix=inputs=6:normalize=0[mixed];
[mixed]atrim=0:1.0,asetpts=PTS-STARTPTS,alimiter=limit=0.95[ir]
" -map "[ir]" -c:a pcm_f32le "$TMP_DIR/ir.wav"

# ---------------------------------------------------------------------------
# 4. 蚤の市 BGM 4曲
#    モノラル化 → 帯域を絞る(200Hz以下/24dB oct, 5000Hz以上/24dB oct)
#    → 1.8kHzを+3dB → 軽い圧縮(acompressor) → 軽いサチュレーション(asoftclip)
#    → afir で ir.wav を畳み込んだものを+28dBして原音に足す(dry+wet手動ミックス。
#      afir の dry/wet 引数は ffmpeg 8.0.1 のこのビルドでは正規化まわりの
#      挙動が不安定で単純な線形ミックスにならなかったため、afir はデフォルト
#      設定のまま畳み込みだけに使い、ウェット量は外側の amix で作る)
#    → loudnorm 2passで integrated -18 LUFS / true peak -1.5dBTP に揃える
#    → 22.05kHz mono の Ogg Vorbis (-q:a 5) で書き出す
#    頭と尻は一切トリムしない(元のフェードのまま、曲の全長を使う)。
# ---------------------------------------------------------------------------
process_bgm () {
  local name="$1"       # 出力ファイル名 (拡張子なし)
  local srcfile="$2"    # 元mp3のパス

  echo "=== ${name}.ogg ==="
  local fx="$TMP_DIR/${name}_fx.wav"
  local norm="$TMP_DIR/${name}_norm.wav"

  ffmpeg -y -v error -i "$srcfile" -i "$TMP_DIR/ir.wav" -filter_complex "
[0:a]pan=mono|c0=0.5*c0+0.5*c1[dry0];
[dry0]highpass=f=200:poles=2,highpass=f=200:poles=2,lowpass=f=5000:poles=2,lowpass=f=5000:poles=2,equalizer=f=1800:t=q:w=1.4:g=3[eq];
[eq]acompressor=threshold=0.1:ratio=3:attack=5:release=80:makeup=1.2[comp];
[comp]asoftclip=type=tanh:threshold=0.9,asplit=2[satA][satB];
[satB][1:a]afir[Rraw];
[Rraw]volume=28dB[Rboost];
[satA][Rboost]amix=inputs=2:duration=first:normalize=0[fxout]
" -map "[fxout]" -c:a pcm_f32le "$fx"

  # loudnorm 1st pass: 測定
  local json
  json=$(ffmpeg -hide_banner -i "$fx" -af "loudnorm=I=-18:TP=-1.5:LRA=11:print_format=json" -f null - 2>&1 | tail -20)
  local mI mTP mLRA mThresh mOffset
  mI=$(echo "$json" | grep -oE '"input_i"\s*:\s*"[-0-9.]+"' | grep -oE '[-0-9.]+')
  mTP=$(echo "$json" | grep -oE '"input_tp"\s*:\s*"[-0-9.]+"' | grep -oE '[-0-9.]+')
  mLRA=$(echo "$json" | grep -oE '"input_lra"\s*:\s*"[-0-9.]+"' | grep -oE '[-0-9.]+')
  mThresh=$(echo "$json" | grep -oE '"input_thresh"\s*:\s*"[-0-9.]+"' | grep -oE '[-0-9.]+')
  mOffset=$(echo "$json" | grep -oE '"target_offset"\s*:\s*"[-0-9.]+"' | grep -oE '[-0-9.]+')
  echo "  measured: I=${mI} TP=${mTP} LRA=${mLRA} thresh=${mThresh} offset=${mOffset}"

  # loudnorm 2nd pass: 適用（線形ゲインで自然に、22.05kHz/monoへ）
  ffmpeg -y -v error -i "$fx" -af "loudnorm=I=-18:TP=-1.5:LRA=11:measured_I=${mI}:measured_TP=${mTP}:measured_LRA=${mLRA}:measured_thresh=${mThresh}:offset=${mOffset}:linear=true:print_format=summary" -ar 22050 -ac 1 -c:a pcm_s16le "$norm"

  # Ogg Vorbis で書き出し
  ffmpeg -y -v error -i "$norm" -c:a libvorbis -q:a 5 -ar 22050 -ac 1 "$OUT_DIR/${name}.ogg"
}

process_bgm "SlowCountry"     "$SRC_OST_DIR/Slow Country.mp3"
process_bgm "PeachLight"      "$SRC_OST_DIR/Peach Light.mp3"
process_bgm "TheOnesWhoStayed" "$SRC_OST_DIR/The Ones Who Stayed.mp3"
process_bgm "CopperHeart"     "$SRC_OST_DIR/Copper Heart.mp3"

fi   # PART=all

# 5 節は PART=all か village のとき（字下げはせず、5 節の末尾で閉じる）
if [ "$PART" = "all" ] || [ "$PART" = "village" ]; then

# ---------------------------------------------------------------------------
# 5. 村と麦畑の 3 つ（VillageMorning.wav / WheatWind.wav / GateCreak.wav）
#    輪の 2 つはモノラル 22.05kHz / 16bit。末尾 2 秒を頭に重ねる等パワー(qsin)
#    クロスフェードで輪にし、実効値(RMS)を -24dBFS 前後に揃える。
#    継ぎ目の確かめ用に、末尾 2 秒 + 頭 2 秒をつないだ 4 秒の波形とスペクトルを
#    ANALYSIS_DIR に書き出す。
# ---------------------------------------------------------------------------
make_ambient_loop () {
  local name="$1" src="$2" start="$3" len="$4" fade="$5" target="$6"
  local bodyend
  bodyend=$(awk "BEGIN{print $len-$fade}")
  echo "=== ${name}.wav ==="

  ffmpeg -y -v error -i "$src" -ss "$start" -t "$len" -ac 1 -ar 22050 -c:a pcm_s16le "$TMP_DIR/${name}_seg.wav"

  ffmpeg -y -v error -i "$TMP_DIR/${name}_seg.wav" -filter_complex "
[0:a]asplit=2[a][b];
[a]atrim=start=0:end=${bodyend},asetpts=PTS-STARTPTS[body];
[b]atrim=start=${bodyend}:end=${len},asetpts=PTS-STARTPTS[tail];
[tail][body]acrossfade=d=${fade}:curve1=qsin:curve2=qsin[out]
" -map "[out]" -c:a pcm_s16le "$TMP_DIR/${name}_loop.wav"

  local rms gain
  rms=$(ffmpeg -hide_banner -i "$TMP_DIR/${name}_loop.wav" -af "astats=metadata=0:reset=0" -f null - 2>&1 | grep "RMS level dB" | tail -1 | grep -oE '[-0-9.]+$')
  gain=$(awk "BEGIN{print $target - ($rms)}")
  echo "${name}: measured RMS=${rms}dB, applying gain=${gain}dB"
  ffmpeg -y -v error -i "$TMP_DIR/${name}_loop.wav" -af "volume=${gain}dB" -c:a pcm_s16le "$OUT_DIR/${name}.wav"

  # 継ぎ目: 輪の末尾 2 秒のあとに頭 2 秒をつなぐ（loop で鳴らしたときと同じ並び）
  local dur tailst
  dur=$(ffprobe -v error -show_entries format=duration -of csv=p=0 "$OUT_DIR/${name}.wav")
  tailst=$(awk "BEGIN{print $dur-2}")
  ffmpeg -y -v error -i "$OUT_DIR/${name}.wav" -filter_complex "
[0:a]asplit=2[a][b];
[a]atrim=start=${tailst},asetpts=PTS-STARTPTS[t];
[b]atrim=end=2,asetpts=PTS-STARTPTS[h];
[t][h]concat=n=2:v=0:a=1[s]
" -map "[s]" -c:a pcm_s16le "$ANALYSIS_DIR/${name}_seam.wav"
  ffmpeg -y -v error -i "$ANALYSIS_DIR/${name}_seam.wav" -lavfi "showwavespic=s=1600x300:colors=0x3070c0" -frames:v 1 "$ANALYSIS_DIR/${name}_seam_wave.png"
  ffmpeg -y -v error -i "$ANALYSIS_DIR/${name}_seam.wav" -lavfi "showspectrumpic=s=1600x420:fscale=lin:legend=1:color=intensity:gain=2:start=0:stop=11025:win_func=hann" "$ANALYSIS_DIR/${name}_seam_spec.png"
}

# 5-1. VillageMorning.wav — 朝の村（whichford。イギリスの村の夜明けの録音。名前の 030510 は 5 月の日付と読める）
#    元は 309.2 秒。0〜3kHz の帯のスペクトルと、0.5 秒窓の帯域別の実効値
#    （25〜150 / 150〜450 / 450〜1100 / 1100〜3000Hz）で調べた。避けた物:
#    - 0〜4 秒: 録り始めの低い揺れ（150Hz 以下が中央値より +11dB）
#    - 48.5〜53 秒・56〜60 秒: 大きな鳴き声（450〜1100Hz が +14〜17dB。しわがれた
#      鳥の声と思われる）
#    - 124〜234 秒: 150Hz 以下の唸りが +6〜11dB で続く区間（風か遠くの車か
#      見分けられない）。218 秒に +21dB の低い衝撃音
#    - 186〜200 秒: 520〜560Hz のかすかな音が約 2.5 秒おきに続く（遠くのカッコウか
#      鳩の候補。はっきりした「高→低」の二音は見分けられなかったが、念のため避けた）
#    - 256〜259 秒・302〜304 秒: 倍音の山がそろった声（ミヤマガラスか羊の声と思われる）
#    - 268〜300 秒: 380〜1300Hz に長く伸びる音程が重なる（教会の鐘）
#    カッコウらしい「高→低」の二音の繰り返しは全体で見つからなかった。
#    60.5〜122.5 秒（62 秒）を採用。区間内に残る目立つ物は 106.8〜109 秒の倍音の
#    ある鳴き声 3〜4 回（全帯域では +2.5dB ほど）と、112.5〜114 秒の弱い音程のある声。
#    輪は 60.0 秒。
make_ambient_loop "VillageMorning" "$SRC_WHICHFORD" 60.5 62.0 2.0 -24

# 5-2. WheatWind.wav — 麦の風（Wheat in the Wind。虫の声入り、オーナーの了承済み）
#    元は 137.7 秒。頭 0.6 秒のフェードインと 134 秒からのフェードアウトがある。
#    全体に 5.2kHz の短い鳴き声が約 10 秒おきに入る（虫。残す）。
#    37.0 秒と 106.0 秒に 1〜3.5kHz の乾いたクリック（450〜3000Hz が +5〜7dB）があるので、
#    その間の 37.8〜105.8 秒（68 秒）を採用。輪は 66.0 秒。
make_ambient_loop "WheatWind" "$SRC_WHEAT" 37.8 68.0 2.0 -24

# 5-3. GateCreak.wav — 格子戸を開ける軋み（Creaky Wooden Gate Opens、2.35 秒）
#    前後の無音（-60dB 未満）を落とし、頂点を -6dB に揃える。モノラル 44.1kHz。
echo "=== GateCreak.wav ==="
ffmpeg -y -v error -i "$SRC_GATE" -af "pan=mono|c0=0.5*c0+0.5*c1,aresample=44100,silenceremove=start_periods=1:start_threshold=-60dB:start_silence=0.005,areverse,silenceremove=start_periods=1:start_threshold=-60dB:start_silence=0.02,areverse" -c:a pcm_s16le "$TMP_DIR/gate_trim.wav"
GATE_PEAK=$(ffmpeg -hide_banner -i "$TMP_DIR/gate_trim.wav" -af "astats=metadata=0" -f null - 2>&1 | grep "Peak level dB" | tail -1 | grep -oE '[-0-9.]+$')
GATE_GAIN=$(awk "BEGIN{print -6 - ($GATE_PEAK)}")
echo "GateCreak: measured peak=${GATE_PEAK}dB, applying gain=${GATE_GAIN}dB"
ffmpeg -y -v error -i "$TMP_DIR/gate_trim.wav" -af "volume=${GATE_GAIN}dB" -ar 44100 -ac 1 -c:a pcm_s16le "$OUT_DIR/GateCreak.wav"

fi   # PART=all か village

# ---------------------------------------------------------------------------
# 6. Gravel1〜6.wav — 未舗装の路地の足音（Walking on a road with gravel 01）
#    元は 61.99 秒、24kHz のステレオ mp3。砂利道を一定の歩調（0.53 秒ほどの間）で
#    歩いた録音で、一歩は 500Hz〜4.5kHz の砂利の擦れの塊が 0.15〜0.2 秒続き、
#    床の雑音（-55dB 前後）まで落ちてから次の一歩が来る。
#    10ms と 5ms の窓の実効値で一歩ずつの立ち上がりを拾い、次の物を避けた:
#    - 次の一歩が 0.35 秒より早く来る所（足の重なり。2.35〜3.0 秒・13.1 秒・24.3 秒・
#      31.9〜32.2 秒・47.2〜47.8 秒・48.5〜49.2 秒ほか）
#    - 立ち上がりの前に弱い擦りが 0.1〜0.2 秒続く所（踵を引きずった足。10.1 秒・
#      20.8 秒・21.3 秒・29.5 秒・46.75 秒）と、一歩の塊が 0.3 秒を超えて平たく続く所（54.7 秒）
#    - 一歩の後ろに二つ目の当たりが続く所（1.3 秒・12.3 秒）
#    - 周りより 10dB 以上突き出た単発の当たり（2.97 秒・13.19 秒・27.99 秒・37.75 秒。小石を蹴った音か）
#    残った中から、一つの塊で終わる六つを採用。切り出しは立ち上がりの 15ms 前から 0.34 秒:
#      5.540 / 6.055 / 7.650 / 16.650 / 17.295 / 20.245 秒
#    左右を平均してモノラルに畳み、44.1kHz へ。100Hz より下の唸りを落とし、頭 4ms を
#    なだらかにして、0.20 秒から 0.14 秒かけて消す（qsin）。
#    大きさは村の柔らかい足音（Step1〜5）と一歩あたりの大きさで揃える。どちらも 0.5 秒に
#    伸ばして 10 回繰り返した物の integrated loudness（ebur128）を測り、Step1〜5 の
#    平均（電力の平均）に合わせる。頂点が -3dB を超えるなら、そこで止める。
#    輪にはしない。一歩ずつの単発で、Footsteps が一歩ごとに一つ選んで鳴らす。
# ---------------------------------------------------------------------------
if [ "$PART" = "all" ] || [ "$PART" = "gravel" ]; then

GRAVEL_STARTS="5.540 6.055 7.650 16.650 17.295 20.245"
GRAVEL_LEN=0.34
GRAVEL_FADE_AT=0.20
GRAVEL_FADE=0.14
GRAVEL_CEIL=-3

STEP_SUM=0
for s in 1 2 3 4 5; do
  l=$(step_loudness "$STEPS_DIR/Step$s.wav")
  echo "Step$s: ${l} LUFS"
  STEP_SUM=$(awk "BEGIN{print $STEP_SUM + 10^($l/10)}")
done
GRAVEL_TARGET=$(awk "BEGIN{printf \"%.2f\", 10*log($STEP_SUM/5)/log(10)}")
echo "Gravel: target ${GRAVEL_TARGET} LUFS（Step1〜5 の電力の平均）"

n=0
for st in $GRAVEL_STARTS; do
  n=$((n+1))
  name="Gravel$n"
  echo "=== ${name}.wav（${st} 秒から）==="
  ffmpeg -y -v error -ss "$st" -t 0.40 -i "$SRC_GRAVEL" \
    -af "pan=mono|c0=0.5*c0+0.5*c1,aresample=44100,highpass=f=100:poles=2,atrim=0:${GRAVEL_LEN},afade=t=in:st=0:d=0.004,afade=t=out:st=${GRAVEL_FADE_AT}:d=${GRAVEL_FADE}:curve=qsin" \
    -c:a pcm_s16le "$TMP_DIR/${name}_cut.wav"
  l=$(step_loudness "$TMP_DIR/${name}_cut.wav")
  pk=$(ffmpeg -hide_banner -i "$TMP_DIR/${name}_cut.wav" -af "astats=metadata=0" -f null - 2>&1 | grep "Peak level dB" | tail -1 | grep -oE '[-0-9.]+$')
  gain=$(awk "BEGIN{g=$GRAVEL_TARGET - ($l); c=$GRAVEL_CEIL - ($pk); printf \"%.2f\", (g < c ? g : c)}")
  echo "${name}: measured ${l} LUFS, peak ${pk}dB, applying gain=${gain}dB"
  ffmpeg -y -v error -i "$TMP_DIR/${name}_cut.wav" -af "volume=${gain}dB" -ar 44100 -ac 1 -c:a pcm_s16le "$OUT_DIR/${name}.wav"
done

# 確かめ用に、六つを 0.45 秒ずつ並べた波形とスペクトルを書き出す
ffmpeg -y -v error -i "$OUT_DIR/Gravel1.wav" -i "$OUT_DIR/Gravel2.wav" -i "$OUT_DIR/Gravel3.wav" \
  -i "$OUT_DIR/Gravel4.wav" -i "$OUT_DIR/Gravel5.wav" -i "$OUT_DIR/Gravel6.wav" -filter_complex "
[0:a]apad=whole_dur=0.45[a0];[1:a]apad=whole_dur=0.45[a1];[2:a]apad=whole_dur=0.45[a2];
[3:a]apad=whole_dur=0.45[a3];[4:a]apad=whole_dur=0.45[a4];[5:a]apad=whole_dur=0.45[a5];
[a0][a1][a2][a3][a4][a5]concat=n=6:v=0:a=1,asplit=2[w][s];
[w]showwavespic=s=1600x300:colors=0x3070c0[wv];
[s]showspectrumpic=s=1600x420:fscale=lin:legend=0:color=intensity:gain=2:stop=12000[sp];
[wv][sp]vstack=inputs=2[out]
" -map "[out]" -frames:v 1 "$ANALYSIS_DIR/Gravel_sheet.png"

fi   # PART=all か gravel

# ---------------------------------------------------------------------------
# 7. Grass1〜6.wav — 芝と草むらの足音（Walking through grass、freesound_community、Pixabay Content License）
#    元は 12.38 秒、44.1kHz のモノラル mp3。歩調は一定でなく、一歩ごとに録った単発が並ぶ
#    （足の重なりや長い擦りが無い代わりに、一歩の中でも草を踏む音の強さがまちまち）。
#    numpy が無いので、ffmpeg で 16bit の生 PCM に落としてから素の Python（wave の実効値を
#    4ms 窓・2ms 送りで自前に積む）で包絡線を追った。実効値が -28dBFS を上回ってから
#    16ms 途切れず下回るまでを一つの塊とし、次の九つを見つけた（秒、塊の長さ、頂点）:
#      1.805(0.03s,-6.1dB) 2.505(0.06s,-7.4dB) 5.125(0.19s,-4.9dB) 5.785(0.06s,-10.8dB)
#      8.925(0.05s,-8.9dB) 9.405(0.18s,-1.1dB) 10.025(0.19s,-8.5dB) 10.465(0.40s,-6.3dB) 11.345(0.09s,-14.3dB)
#    このうち、次の塊まで 0.34 秒の切り出しが届かない三つは避けた:
#    - 9.405（頂点が -1.1dB とほぼ天井で、直前の 8.925 と直後の 10.025 のどちらとも間が狭く、
#      二歩分が重なった塊の疑い）
#    - 10.465（塊の長さ自体が 0.40 秒あり、一歩には長すぎる。二歩の重なりの疑い）
#    - 11.345（残り一つだけ採っても数が増えないので、前の六つで足りるとして見送った）
#    残った六つを採用。切り出しは塊の頭の 15ms 前から 0.34 秒（砂利と同じ切り出しの型）。
#    左右は最初からモノラルなので畳まず、100Hz より下の唸りを落とし、頭 4ms をなだらかにして、
#    0.20 秒から 0.14 秒かけて消す（qsin）。大きさは砂利と同じ揃え方（Step1〜5 の一歩あたりの
#    大きさの電力平均に、頂点 -3dB の天井を添えて）。輪にはしない単発
# ---------------------------------------------------------------------------
if [ "$PART" = "all" ] || [ "$PART" = "grass" ]; then

GRASS_STARTS="1.790 2.490 5.110 5.770 8.910 10.010"
GRASS_LEN=0.34
GRASS_FADE_AT=0.20
GRASS_FADE=0.14
GRASS_CEIL=-3

GRASS_SUM=0
for s in 1 2 3 4 5; do
  l=$(step_loudness "$STEPS_DIR/Step$s.wav")
  GRASS_SUM=$(awk "BEGIN{print $GRASS_SUM + 10^($l/10)}")
done
GRASS_TARGET=$(awk "BEGIN{printf \"%.2f\", 10*log($GRASS_SUM/5)/log(10)}")
echo "Grass: target ${GRASS_TARGET} LUFS（Step1〜5 の電力の平均）"

n=0
for st in $GRASS_STARTS; do
  n=$((n+1))
  name="Grass$n"
  echo "=== ${name}.wav（${st} 秒から）==="
  ffmpeg -y -v error -ss "$st" -t 0.40 -i "$SRC_GRASS" \
    -af "aresample=44100,highpass=f=100:poles=2,atrim=0:${GRASS_LEN},afade=t=in:st=0:d=0.004,afade=t=out:st=${GRASS_FADE_AT}:d=${GRASS_FADE}:curve=qsin" \
    -c:a pcm_s16le "$TMP_DIR/${name}_cut.wav"
  l=$(step_loudness "$TMP_DIR/${name}_cut.wav")
  pk=$(ffmpeg -hide_banner -i "$TMP_DIR/${name}_cut.wav" -af "astats=metadata=0" -f null - 2>&1 | grep "Peak level dB" | tail -1 | grep -oE '[-0-9.]+$')
  gain=$(awk "BEGIN{g=$GRASS_TARGET - ($l); c=$GRASS_CEIL - ($pk); printf \"%.2f\", (g < c ? g : c)}")
  echo "${name}: measured ${l} LUFS, peak ${pk}dB, applying gain=${gain}dB"
  ffmpeg -y -v error -i "$TMP_DIR/${name}_cut.wav" -af "volume=${gain}dB" -ar 44100 -ac 1 -c:a pcm_s16le "$OUT_DIR/${name}.wav"
done

# 確かめ用に、六つを 0.45 秒ずつ並べた波形とスペクトルを書き出す
ffmpeg -y -v error -i "$OUT_DIR/Grass1.wav" -i "$OUT_DIR/Grass2.wav" -i "$OUT_DIR/Grass3.wav" \
  -i "$OUT_DIR/Grass4.wav" -i "$OUT_DIR/Grass5.wav" -i "$OUT_DIR/Grass6.wav" -filter_complex "
[0:a]apad=whole_dur=0.45[a0];[1:a]apad=whole_dur=0.45[a1];[2:a]apad=whole_dur=0.45[a2];
[3:a]apad=whole_dur=0.45[a3];[4:a]apad=whole_dur=0.45[a4];[5:a]apad=whole_dur=0.45[a5];
[a0][a1][a2][a3][a4][a5]concat=n=6:v=0:a=1,asplit=2[w][s];
[w]showwavespic=s=1600x300:colors=0x3070c0[wv];
[s]showspectrumpic=s=1600x420:fscale=lin:legend=0:color=intensity:gain=2:stop=12000[sp];
[wv][sp]vstack=inputs=2[out]
" -map "[out]" -frames:v 1 "$ANALYSIS_DIR/Grass_sheet.png"

fi   # PART=all か grass

# ---------------------------------------------------------------------------
# 8. 場面 6（庭の記憶）の 4 つ（GardenHose.wav / GardenHoseStop.wav / SignalGlitch.wav / SignalCut.wav）
#    夕方の英国の田舎の庭で、女性がホースで花に水を撒き、振り返って名を呼ぶ。その声は途切れと雑音に
#    かき消され、記憶がぶつっと途切れる。
#    4 つとも Pixabay の freesound_community（Freesound から移された物）。作品の頁に AI generated の
#    表示もタグも無く、説明にも AI の言葉は無い。ゲームの音を抜き出した物でもない（8-3 の注を参照）。
#    どれもモノラル 44.1kHz / 16bit。中間ファイルは頭打ちを避けて 32bit の浮動小数で持つ。
#    単発は -ss を -i の前に置く（afade の時刻を切り口から数えるため。後ろに置くと元の頭から数えて全部消える）。
#    確かめ用の絵（波形とスペクトル）を ANALYSIS_DIR に書き出す。
# ---------------------------------------------------------------------------
if [ "$PART" = "all" ] || [ "$PART" = "garden" ]; then

# 頂点を target（dB）に揃えて OUT_DIR へ書き出す（8-2〜8-4 の単発。どれも -6dB）
garden_peak_to () {
  local name="$1" in="$2" target="$3"
  local pk gain
  pk=$(ffmpeg -hide_banner -i "$in" -af "astats=metadata=0" -f null - 2>&1 | grep "Peak level dB" | tail -1 | grep -oE '[-0-9.]+$')
  gain=$(awk "BEGIN{printf \"%.2f\", $target - ($pk)}")
  echo "${name}: measured peak=${pk}dB, applying gain=${gain}dB"
  ffmpeg -y -v error -i "$in" -af "volume=${gain}dB" -ar 44100 -ac 1 -c:a pcm_s16le "$OUT_DIR/${name}.wav"
}

# 出来た物の長さ・実効値・頂点を表示し、波形とスペクトルの絵を書き出す
garden_report () {
  local name="$1" dur rms pk
  dur=$(ffprobe -v error -show_entries format=duration -of csv=p=0 "$OUT_DIR/${name}.wav")
  rms=$(ffmpeg -hide_banner -i "$OUT_DIR/${name}.wav" -af "astats=metadata=0" -f null - 2>&1 | grep "RMS level dB" | tail -1 | grep -oE '[-0-9.]+$')
  pk=$(ffmpeg -hide_banner -i "$OUT_DIR/${name}.wav" -af "astats=metadata=0" -f null - 2>&1 | grep "Peak level dB" | tail -1 | grep -oE '[-0-9.]+$')
  echo "${name}: ${dur}s, RMS ${rms}dB, peak ${pk}dB"
  ffmpeg -y -v error -i "$OUT_DIR/${name}.wav" -filter_complex "
[0:a]asplit=2[w][s];
[w]showwavespic=s=1600x300:colors=0x3070c0[wv];
[s]showspectrumpic=s=1600x420:fscale=lin:legend=0:color=intensity:gain=2:stop=16000[sp];
[wv][sp]vstack=inputs=2[out]
" -map "[out]" -frames:v 1 "$ANALYSIS_DIR/${name}_sheet.png"
}

# 8-1. GardenHose.wav — ホースで植え込みに水を撒く音（輪。女性の手元から 3D で鳴らす）
#    「Watering」（elittle13。Freesound の説明は、早朝に自分の庭のホースで植木に水をやった音、
#    Tascam DR-05X で録音）。元は 25.22 秒、24kHz のステレオ mp3。左右の差は和より 36dB 低く、
#    実質モノラルなので左右を平均して畳む。近くで録っていて、噴く「しゃー」と葉に当たる粒が主。
#    0.25 秒窓の実効値と、250Hz より下の帯の実効値で調べた。避けた物:
#    - 0.25〜1.1 秒: 出し始め。周りより 5〜7dB 大きい
#    - 14.55〜15.1 秒: 何かに当たった低い音（250Hz より下が周りより 17dB 跳ねる）
#    - 24.5 秒から: 録音の側のフェードアウト
#    1.15〜14.50 秒（13.35 秒）を採用。5.25 秒と 11.75 秒に 250Hz より下が +7dB の小さな揺れが残るが、
#    その帯は全体より 20dB 低く、全帯域の実効値には表れない。人の声・車・犬・鳥のさえずりらしい
#    音程の筋はスペクトルに見当たらない。頭 1.5 秒と尻 1.5 秒では 3kHz より上が 3dB ほど違う
#    （ホースの向きが動いたぶん）が、2 秒の等パワーのクロスフェードの中でなだらかに移る。
#    見送った候補: Watering the garden（ほかの人が撒くのを離れて録った物で、実効値が -37dB。-24dBFS まで
#    13dB 上げると周りの音ごと持ち上がる。3D の音源として鳴らすなら近くで録った方がよい）、
#    Garden Hose Spraying（ノズルの鳴りの筋が 1〜3.6kHz に並び、桶に溜める音に寄る。途中に短い当たりがいくつかある）、
#    07-1 Watering, hose（まばらで、低い揺れが 2 回ある）、Watering Plants（13 秒で終わり、あとは無音）。
#    いちばん強いのが 6〜11kHz の帯なので、22.05kHz にせず 44.1kHz にする（22.05kHz では
#    リサンプルの低域通過が 10.7kHz あたりから削る）。
#    末尾 2 秒を頭に重ねる等パワー(qsin)クロスフェードで輪にし、実効値を -24dBFS 前後に揃える。輪は 11.35 秒。
echo "=== GardenHose.wav ==="
HOSE_START=1.15
HOSE_LEN=13.35
HOSE_FADE=2.0
HOSE_BODYEND=$(awk "BEGIN{print $HOSE_LEN-$HOSE_FADE}")

ffmpeg -y -v error -i "$SRC_HOSE" -ss $HOSE_START -t $HOSE_LEN -af "pan=mono|c0=0.5*c0+0.5*c1,aresample=44100" -c:a pcm_f32le "$TMP_DIR/hose_seg.wav"

ffmpeg -y -v error -i "$TMP_DIR/hose_seg.wav" -filter_complex "
[0:a]asplit=2[a][b];
[a]atrim=start=0:end=${HOSE_BODYEND},asetpts=PTS-STARTPTS[body];
[b]atrim=start=${HOSE_BODYEND}:end=${HOSE_LEN},asetpts=PTS-STARTPTS[tail];
[tail][body]acrossfade=d=${HOSE_FADE}:curve1=qsin:curve2=qsin[out]
" -map "[out]" -c:a pcm_f32le "$TMP_DIR/hose_loop.wav"

HOSE_RMS=$(ffmpeg -hide_banner -i "$TMP_DIR/hose_loop.wav" -af "astats=metadata=0:reset=0" -f null - 2>&1 | grep "RMS level dB" | tail -1 | grep -oE '[-0-9.]+$')
HOSE_GAIN=$(awk "BEGIN{printf \"%.2f\", -24 - ($HOSE_RMS)}")
echo "GardenHose: measured RMS=${HOSE_RMS}dB, applying gain=${HOSE_GAIN}dB"
ffmpeg -y -v error -i "$TMP_DIR/hose_loop.wav" -af "volume=${HOSE_GAIN}dB" -c:a pcm_s16le "$OUT_DIR/GardenHose.wav"
garden_report GardenHose

# 継ぎ目: 輪の末尾 2 秒のあとに頭 2 秒をつなぐ（loop で鳴らしたときと同じ並び）
HOSE_DUR=$(ffprobe -v error -show_entries format=duration -of csv=p=0 "$OUT_DIR/GardenHose.wav")
HOSE_TAILST=$(awk "BEGIN{print $HOSE_DUR-2}")
ffmpeg -y -v error -i "$OUT_DIR/GardenHose.wav" -filter_complex "
[0:a]asplit=2[a][b];
[a]atrim=start=${HOSE_TAILST},asetpts=PTS-STARTPTS[t];
[b]atrim=end=2,asetpts=PTS-STARTPTS[h];
[t][h]concat=n=2:v=0:a=1[s]
" -map "[s]" -c:a pcm_s16le "$ANALYSIS_DIR/GardenHose_seam.wav"
ffmpeg -y -v error -i "$ANALYSIS_DIR/GardenHose_seam.wav" -lavfi "showwavespic=s=1600x300:colors=0x3070c0" -frames:v 1 "$ANALYSIS_DIR/GardenHose_seam_wave.png"
ffmpeg -y -v error -i "$ANALYSIS_DIR/GardenHose_seam.wav" -lavfi "showspectrumpic=s=1600x420:fscale=lin:legend=1:color=intensity:gain=2:start=0:stop=12000:win_func=hann" "$ANALYSIS_DIR/GardenHose_seam_spec.png"

# 8-2. GardenHoseStop.wav — ノズルを離して水が止まる音（単発）
#    「Hose Sounds」（JazzyBay。Freesound の説明は、よく軋む水の栓を開け、広い吹きさらしの車庫に
#    置いてあるホースでコンクリートに水を撒いた、短く何度か出し、閉めたあとは滴るに任せた、というもの）。
#    元は 128.93 秒、48kHz のモノラル mp3。短く出しては止めた所のうち、101.35 秒の止まり方がいちばんはっきりしている:
#    - 101.20〜101.40 秒で 18dB 落ちる（ノズルを離した所）
#    - 101.40〜101.84 秒は弱い噴き残りが続き、101.86 秒で小さく「しゅっ」と鳴って止みきる
#    - そのあとは 102.28 / 102.60 / 102.72 / 102.84 秒に滴り
#    101.22〜102.32 秒（1.10 秒）を採用。止まる前の噴きは 0.13 秒だけ入れる（コンクリートに当たる音を
#    長く残さないため）。100.9〜101.2 秒は頂点が 0dB を超える（元で頭打ちしていると思われる）ので、その後ろから切る。
#    ほかの止め所は、62.5 秒（栓の「キュッ」という軋みが 8kHz 前後の筋で 8 回ほど続き、長い）、
#    76.9 秒と 119.5 秒（0.5〜2 秒かけてなだらかに細り、止まった感じが薄い）で見送った。
#    ほかの素材では「Garden Hose Spraying」の末尾（18.2 秒で止まって滴りが続く。庭で録った物）が次点。
#    ただし 0.4 秒かけて 12dB しか落ちず、止まり方がこちらほどはっきりしない。
#    蛇口の音は、「Squeak Rusty Metal Water Tap」は軋みだけで水の音が無く、室内の流しの物は流しに溜まる音が
#    入るので見送った。
#    120Hz より下の車庫の唸りを落とし、頭 50ms をなだらかに入れて、0.80 秒から 0.30 秒かけて消す（qsin）。
#    44.1kHz モノラルへ、頂点を -6dB に揃える。
echo "=== GardenHoseStop.wav ==="
ffmpeg -y -v error -ss 101.22 -t 1.10 -i "$SRC_HOSE_STOP" \
  -af "aresample=44100,highpass=f=120:poles=2,afade=t=in:st=0:d=0.05:curve=qsin,afade=t=out:st=0.80:d=0.30:curve=qsin" \
  -c:a pcm_f32le "$TMP_DIR/hose_stop_cut.wav"
garden_peak_to GardenHoseStop "$TMP_DIR/hose_stop_cut.wav" -6
garden_report GardenHoseStop

# 8-3. SignalGlitch.wav — 途切れと雑音（名を呼ぶ口元が動く 2〜3 秒の単発）
#    「Computer Glitch, Corrupted File」（Diicorp95。Freesound の側は利用者ごと消えていて説明は読めない。
#    Pixabay のタグは Noise / Corrupted / Glitch / Computer / Digital / Weird / Electronic）。
#    元は 6.17 秒、24kHz のステレオ mp3（左右は同じ）。40ms 前後の雑音の粒が並び、粒と粒の間は
#    デジタルの無音（-100dB 未満）へ落ちる。データが欠けて飛ぶ感じ。粒の芯は 1.6kHz より下にあり、
#    1.9kHz おきに細い谷（櫛の目）が入る。音程の並び（旋律）や一定の拍は無い。20ms 窓の実効値で粒と無音を追った。避けた物:
#    - 0.08〜0.14 秒: 頭の一発。400Hz / 1.3kHz に音程の筋があり、周りより 8〜10dB 大きい（電子音の「ピッ」に近い）
#    - 4.72 秒から: 粒がまばらになり、ぽつぽつ鳴るだけになる
#    1.68〜4.62 秒（2.94 秒）を採用。まばらな所（1.7〜2.1 秒）から入り、2.1〜4.46 秒で粒が詰まり、4.52 秒の粒で終わる。
#    切り口はどちらもデジタルの無音の中なので継ぎ目の音は出ない（念のため頭と尻を 3ms ずつなだらかにする）。
#    見送った候補: 「TV glitch」（頂点が 0dB を超えて割れている。タグに FNAF があり、ゲームの音の疑いもある）、
#    「No Signal」と Alex_Jauk の「Static Noise」（途切れの無い一様な砂嵐）、「Speaker Phone Call Interference Sound」
#    （携帯の電波の「ジジジ」で、拍がそろっていて律動に聞こえる）、「DR2 Digital Noise」（うねる雑音で途切れが無い）、
#    「Sound interference」（頂点が 0dB を超えて割れている。砂嵐が段々に強まるだけで途切れが無い）、
#    「glitch_databending_04」（実効値 -3dB の、ほぼ全部が頭打ちの雑音）。
#    「Digital Glitch」（PWLPL）は頁に AI generated の表示とタグがあるので外した。
#    44.1kHz モノラルへ、頂点を -6dB に揃える。
echo "=== SignalGlitch.wav ==="
ffmpeg -y -v error -ss 1.68 -t 2.94 -i "$SRC_GLITCH" \
  -af "pan=mono|c0=0.5*c0+0.5*c1,aresample=44100,afade=t=in:st=0:d=0.003,afade=t=out:st=2.937:d=0.003" \
  -c:a pcm_f32le "$TMP_DIR/signal_glitch_cut.wav"
garden_peak_to SignalGlitch "$TMP_DIR/signal_glitch_cut.wav" -6
garden_report SignalGlitch

# 8-4. SignalCut.wav — 記憶が途切れる瞬間の「ぶつっ」（単発）
#    「Glitch」（smokenweewALT、いまの名は smokevhstapes。Freesound の説明は、動画を Audacity で
#    データベンディングし、それを Audacity で録り直した、というもの。2016 年）。
#    元は 0.50 秒、44.1kHz のモノラル mp3。全帯域の砂嵐のような雑音が 0.024 秒に立ち上がり、
#    0.23 秒に 20ms ほど細り、0.490 秒でそのまま断ち切られる。この断ち切れを「ぶつっ」として使う。
#    0.020〜0.490 秒（0.47 秒）を採用。頭 2ms と尻 3ms だけなだらかにして、断ち切れの鋭さは残す。
#    8-3 と同じ素材から切ると、粒の間の無音と区別がつかず「切れた」感じが出ないので、別の素材にした。
#    同じ作者の「Glitch」（82312）は、低い「ぼん」という減衰音が真ん中にあって断ち切れが目立たないので見送った。
#    44.1kHz モノラルへ、頂点を -6dB に揃える（元の頂点は 0dB 近く）。
echo "=== SignalCut.wav ==="
ffmpeg -y -v error -ss 0.020 -t 0.470 -i "$SRC_CUT" \
  -af "aresample=44100,afade=t=in:st=0:d=0.002,afade=t=out:st=0.467:d=0.003" \
  -c:a pcm_f32le "$TMP_DIR/signal_cut_cut.wav"
garden_peak_to SignalCut "$TMP_DIR/signal_cut_cut.wav" -6
garden_report SignalCut

fi   # PART=all か garden

echo "=== done ==="
