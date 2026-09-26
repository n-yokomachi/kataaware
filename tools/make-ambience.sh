#!/usr/bin/env bash
# HALF AWARE 音素材の生成スクリプト（ffmpeg 8.0.1 のみで完結）
# 使い方: bash make-ambience.sh           （全部）
#         bash make-ambience.sh village   （5 節の村と麦畑の 3 つだけ）
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

OUT_DIR="${OUT_DIR:-./out-ambience}"   # 出来た物を unity/Assets/Audio/ と unity/Assets/Audio/Music/ へ写す
TMP_DIR="$OUT_DIR/tmp"
ANALYSIS_DIR="$OUT_DIR/analysis"
mkdir -p "$TMP_DIR" "$ANALYSIS_DIR"

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

echo "=== done ==="
