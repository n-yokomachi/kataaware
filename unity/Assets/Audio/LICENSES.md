# 音の出典

出どころは 2 つ。足音とジッポとチップは CC0 1.0（パブリックドメインの献呈）、
煙草の 3 つ（吸う・吐く・火）と雨と扉は Pixabay。

## Pixabay Content License

https://pixabay.com/service/license-summary/

- **表記は要らない。** 出典を書く義務は無い（書けば喜ばれる、とある）
- 取り消されない・世界中で・期限なし・非独占・使用料なしで、落として・使って・
  複製して・作り変えてよい。商用でも非商用でもよい
- **単体で売ったり配ったりしてはいけない。** 音のファイルそのものを、
  素材として売る／配る／素材サイトへ出す、はできない
- ほかの素材や編集と組み合わせて「新しい作品」になっていれば単体扱いにならない。
  ゲームに組み込んで鳴らすのはこちらに当たる
- 商標・ロゴ・見分けのつく人物を含むものは、商用や誤解を招く使い方に制限がある

この repo に置いてあるのは**切り出して整えたあとの物**で、ゲームの一部として鳴らしている。
ただし **`Assets/Audio/` の中身を音素材として取り出して配るのは禁止**にあたる。

| ファイル | 出典 | 許諾 | 加工 |
|---|---|---|---|
| `Step1`〜`Step5.wav` | Kenney RPG Audio（https://kenney.nl/assets/rpg-audio）の `footstep00/02/04/06/08.ogg` | CC0 1.0 | モノラル 44.1kHz へ、末尾を落として頂点を −6dB に揃えた |
| `Concrete1`〜`Concrete4.wav` | 同上（`Step2` / `Step5` / `Step1` / `Step3` から作り直し） | CC0 1.0 | `tools/make-steps.py`。360Hz より下を落とし、1.2kHz より上を 2.6 倍に持ち上げ、時定数 48ms で尾を詰めて 0.165 秒へ切り、頂点 −6dB |
| `LighterClick.wav` | OpenGameArt「Zippo click sound」（https://opengameart.org/content/zippo-click-sound）作者 dawith | CC0 1.0 | 金属音の当たりだけを 0.060〜0.320 秒で切り出し、頂点 −3dB |
| `Drag.wav` | Pixabay の `crackle`。作品名と作者は未記入 | Pixabay Content License | 前後の無音を落として頂点 −8dB |
| `Blow.wav` | Pixabay の `blow`。作品名と作者は未記入 | Pixabay Content License | 同上 |
| `LighterFlame.wav` | Pixabay の `flint`。作品名と作者は未記入 | Pixabay Content License | 前後の無音を落として頂点 −5dB |
| `ChipPull.wav` | OpenGameArt「Sound Effects Pack」（https://opengameart.org/content/sound-effects-pack）作者 OwlishMedia の `Technology/plugpull.wav` | CC0 1.0 | 0.02〜0.40 秒、頂点 −6dB |
| `JackPull.wav` | 同上パックの `Technology/plugpull2.wav` | CC0 1.0 | 3.33 秒から 0.50 秒を切り出し、再生速度 0.85 倍で低く伸ばし、前後の無音を落として頂点 −6dB |
| `JackPlug.wav` | Pixabay `freesound_community-headphones-jack-plugged-in-97328` | Pixabay Content License | 0.58〜1.10 秒を切り出し、再生速度 0.85 倍で低く伸ばし（`JackPull` と同じ扱いにして、抜くのと挿すので音の質を揃えた）、前後の無音を落として頂点 −6dB。モノラル 44.1kHz、0.22 秒 |
| `RainLoop.wav` | Pixabay `dragon-studio-copyright-free-rain-sounds-331497` | Pixabay Content License | 60 秒地点から 24 秒を切り出し、モノラル 22.05kHz へ。末尾 1.5 秒を頭に重ねて輪にし、実効値を −22dBFS に揃えた |
| `DoorShut.wav` | Pixabay `dragon-studio-open-and-close-door-405453` | Pixabay Content License | モノラル 44.1kHz へ、頭から 2.15 秒を切り出して末尾 0.1 秒を落とし、頂点を −3dB に揃えた。開けると閉めるが 1 つに入っている（開ける 〜0.5 秒、間 〜1.0 秒、閉まる 1.45〜1.65 秒） |
| `CarDoorOpen.wav` | Pixabay `dragon-studio-open-car-door-372469` | Pixabay Content License | モノラル 44.1kHz へ、頭の無音を落として末尾 60ms を落とした。0.92 秒 |
| `CarDoorShut.wav` | Pixabay `freesound_community-car-door-close-6929` | Pixabay Content License | モノラル 44.1kHz へ、頭の無音を落とした。0.55 秒 |
| `Ignition.wav` | Pixabay `freesound_community-keys-in-the-ignition-101951` | Pixabay Content License | モノラル 44.1kHz へ、頭の無音を落として 6.72 秒。そこへ**末尾 0.55 秒から金属音を落としたもの**を 0.05 秒ずつ被せて 8 回繋ぎ、4.05 秒足して 10.52 秒。金属音を落とすのは 1.8kHz の二段の低域通過と、目立っていた 5.2k / 7.6k の山を Q 12 で −18dB。3kHz 以上の実効値が −54.1 → −64.2dB、エンジンの胴（800Hz 以下）は −26.19 → −26.21dB でほぼ無傷。本体から繰り返しへは 0.25 秒かけて渡すので、金属ありから無しへの移り目は段にならない |
| `DriveSealed.wav` | Pixabay `freesound_community-car-driving-interior-perspective-51388`（オーナーが 22.3〜286.7 秒で切り出したもの） | Pixabay Content License | 60 秒地点から 21 秒。**6900Hz の音を抜いてある**（周りより 18dB 突き出ていた。Q 28 で −30dB、Q 60 で −24dB の二段）。+7dB、ステレオ 44.1kHz へ。頭 1.0 秒を尻へ被せて輪にした。20.00 秒 |
| `DriveGravel.wav` | Pixabay `freesound_community-driving-a-truck-in-gravel-58271` | Pixabay Content License | **20 秒地点から最後まで**（97.27 秒）。閾値 −26dB / 比 2.5 / 持ち上げ 3dB で圧縮し、0.80 で頭打ち。ステレオ 44.1kHz へ。頭 1.0 秒を尻へ被せて輪にした。11.25 秒では「短すぎる」と差し戻されたので、素材にあるぶんを全部使っている |
| `RainWipers.wav` | Pixabay `tommylynn-interior-car-in-rain-with-wipers-369260` | Pixabay Content License | 6 秒地点から 13.879 秒、+12dB。ステレオ 44.1kHz へ。頭 0.6 秒を尻へ被せて輪にした。**長さはワイパーの周期で決めてある**（包絡線の自己相関で 0.9485 秒。その 14 倍の 13.279 秒）。秒数で切ると払う拍が輪の継ぎ目で飛ぶ |
| `Idle.wav` | Pixabay `freesound_community-car-keys-in-ignition-starting-stopping-engine-31102` | Pixabay Content License | 28 秒地点から 8.6 秒。モノラル 44.1kHz へ。頭 0.6 秒を尻へ被せて輪にした。8.00 秒。イグニッションのあと走り出すまでの間を埋める |
| `WindowDown.wav` | Pixabay `freesound_community-car-window-down-103833` | Pixabay Content License | 1.85 秒地点から 4.45 秒、+20dB。モノラル 44.1kHz へ。素材は 8.94 秒あるが、窓が動いているのは 2〜6 秒だけ。素のままだと実効 −41.9dB で、持ち上げた走行の輪（−19dB）に埋もれて聞こえなかった |
| `CarStopHandbrake.wav` | Pixabay `freesound_community-car-reverse-stop-handbrake-vw-scirocco-engine-continues-27879`（オーナーが 9.835〜21.240 秒で切り出したもの。元は 21.24 秒なので尻まで使っている） | Pixabay Content License | 元は 24kHz のステレオ mp3。左右を平均してモノラルに畳み（左右の差は和より 8dB 低く、畳んで痩せない）、44.1kHz へ。頭 20ms と尻 50ms をなだらかにし、−3.6dB で頂点を −6dB に揃えた。実効 −26.4dB、11.40 秒。中身は、0〜2.1 秒 エンジンの低い唸りだけ、2.15〜2.4 秒 150Hz より下が 12dB 持ち上がる（踏み込んで後ろへ動き出す）、3.3〜4.9 秒 1kHz より上が 7〜10dB 上がる擦れ（止めにかかる。3.30・3.50・3.65・3.95・4.30 秒に小さな当たり）、4.3〜5.7 秒 9.4kHz のブレーキの鳴き（頂は 4.85 秒）、4.9〜5.7 秒 擦れが引いて止まりきる、6.95〜7.10 秒 ハンドブレーキを引く音（1kHz より上が 17dB 跳ねる）、7.1 秒から尻まで エンジンが続く（9 秒あたりから録音の側でなだらかに下がり、尻で −40dB。10.10 秒に小さな当たり）。場面 8 の終わりに黒のあいだに鳴らす |
| `JacketOn.wav` | Pixabay `freesound_community-jacket-rustling-35100`（オーナーが 0.844〜5.596 秒で切り出したもの） | Pixabay Content License | モノラル 44.1kHz へ、頭 20ms と尻 50ms をなだらかにし、+11.8dB で頂点を −6dB に揃えた。4.78 秒。場面 1 で椅子の左の肘掛けのジャケットを着る音 |
| `RoomTone.wav` | Pixabay `xomxomski-ambient-empty-room-noise-sound-effect-429845` | Pixabay Content License | 3.0〜33.5 秒を切り出し（冒頭の録音の立ち上がりと、33.75 秒・53.5 秒の小さな高い音を避けた）、モノラル 22.05kHz へ。末尾 1.5 秒を頭に重ねて輪にし、実効値を −24dBFS に揃えた。29.0 秒。自室の場面の空気の音 |
| `CrowdLoop.wav` | Pixabay `freesound_community-crowd_talking-6762` | Pixabay Content License | 7.0〜57.0 秒の 50 秒を切り出し（6.6 秒のいちばん大きな声と、86 秒からのフェードアウトを避けた）、モノラル 22.05kHz へ。末尾 2 秒を頭に重ねて輪にし、実効値を −22dBFS に揃えた。48.0 秒。場面 2 の通りとヤードの雑踏 |
| `VillageMorning.wav` | Pixabay `freesound_community-030510whichford-18349`（「030510whichford」。作者 lunasound（Freesound）。イギリスの村の夜明けの鳥の声） | Pixabay Content License | 元は 309.2 秒。60.5〜122.5 秒の 62 秒を切り出し、モノラル 22.05kHz へ。末尾 2 秒を頭に重ねて輪にし、実効値を −24dBFS に揃えた。60.0 秒。避けたのは、録り始めの低い揺れ（0〜4 秒）、大きなしわがれた鳴き声（48.5〜53 秒・56〜60 秒）、150Hz より下の唸りが続く所（124〜234 秒。風か遠くの車か見分けられない。218 秒に低い衝撃音）、520〜560Hz の小さな音が 2.5 秒おきに続く所（186〜200 秒。遠くのカッコウか鳩の候補）、倍音のそろった鳴き声（256〜259 秒・302〜304 秒）、教会の鐘（268〜300 秒）。カッコウらしい「高→低」の二音の繰り返しは全体で見つからなかった。区間の中には 106.8〜109 秒に倍音のある鳴き声が 3〜4 回残る。村の朝 |
| `WheatWind.wav` | Pixabay `freesound_community-wheat-in-the-wind-7159`（「Wheat in the Wind」。作者 bdvictor（Freesound）） | Pixabay Content License | 元は 137.7 秒。37.8〜105.8 秒の 68 秒を切り出し（37.0 秒と 106.0 秒の乾いたクリックを避けた）、モノラル 22.05kHz へ。末尾 2 秒を頭に重ねて輪にし、実効値を −24dBFS に揃えた。66.0 秒。約 10 秒おきに入る 5.2kHz の虫の声は残してある（オーナーの了承済み）。村の朝と夕方、場面 8 の麦畑 |
| `GateCreak.wav` | Pixabay `dobcommunications-creaky-wooden-gate-opens-170210`（「Creaky Wooden Gate Opens」。作者 DOBCommunications） | Pixabay Content License | モノラル 44.1kHz へ、前後の −60dB 未満を落とし、頂点を −6dB に揃えた。2.19 秒。村の片割れの家の格子戸を開ける音 |

## 曲

オーナーが Suno の Pro プランで生成した曲（`docs/superpowers/specs/2026-09-24-music-design.md` の 4 節）。
許諾は Pro プランの商用利用の権利で、帰属の表示は求められていない。元の mp3 は repo に置かない。

| ファイル | 元の曲 | 許諾 | 加工 |
|---|---|---|---|
| `Music/SlowCountry.ogg` | 「Slow Country」 | Suno の Pro プランで生成 | ヤードの出店の古いスピーカーから鳴る音にした。モノラルに畳み、200Hz より下と 5kHz より上を 24dB/oct で落とし、1.8kHz を +3dB。軽く圧縮して軽く歪ませ、ffmpeg で作ったインパルス応答（煉瓦の壁の初期反射 5 本と、0.7 秒ほどの残響）の響きを 15〜20% 足した。integrated −18 LUFS・true peak −1.5dBTP 以下に揃え、22.05kHz の Ogg Vorbis（q5）へ。長さは元のまま |
| `Music/PeachLight.ogg` | 「Peach Light」 | 同上 | 同上 |
| `Music/TheOnesWhoStayed.ogg` | 「The Ones Who Stayed」 | 同上 | 同上 |
| `Music/CopperHeart.ogg` | 「Copper Heart」 | 同上 | 同上 |

雑踏の輪と曲の加工は `tools/make-ambience.sh` で作り直せる（村の朝・麦の風・格子戸は同じ中の 5 節。`bash make-ambience.sh village` でそれだけ作り直す。
素材の mp3 は `unity/RawAssets/audio/pixabay/` に Pixabay の元の名前で置く。git には入れない）。WebGL では Unity のオーディオのフィルターが効かないので、
スピーカーらしさと響きはファイルに焼き込んである。

切り出しの手順は ffmpeg で、`docs/` ではなくここに残す。素材そのものは repo に置かず、加工後の物だけを置いている。

煙草の 3 つは Pixabay のどの作品かまで辿れていない。**作品名と作者を控えておくこと。**
表記の義務は無いので公開の妨げにはならないが、差し替えや問い合わせのときに要る。

## 使うところ

- 足音は `Footsteps`。進んだ距離を積んで 0.88 メートルごとに 1 つ鳴らす。直前と同じ物は選ばず、音量と高さを少し振る
- 場面 2（小道）と場面 1（自室）は `Step1`〜`Step5`、場面 8（共用ガレージ）は `Concrete1`〜`Concrete4`。
  同じ足音を裸のコンクリートの上で鳴らすと床が土に聞こえるので、場面 8 だけ作り直してある。
  ガレージの反響は素材に焼かず、足元の `AudioReverbFilter` に持たせる
- 自室の空気の音は `RoomTone`。自室が舞台の場面 1・3・5 で、プレイヤーの頭上で 2D の輪にして小さく流す（`RoomTone`。大きさはインスペクターで変える）。場面を終えて暗転するときは、その暗転に合わせて絞る
- 村（`Village.unity`）の環境音は `VillageAmbience`（Player/Ambience）。プレイヤーの頭上で 2D の輪にして流し、時刻（`VillageHour`）で鳴らす物を替える。
  朝は `VillageMorning`（0.6）と `WheatWind`（0.5）を重ね、夕方は `WheatWind`（0.6）だけ。時刻が替わると 2 秒で入れ替える。
  大きさはインスペクターで変える。組み直しても前の値を引き継ぐ
- 格子戸は `GateCreak`。`SwingGate` が開けるときに頭から終わりまで 1 度鳴らす（以前は自室の扉の `DoorShut` の頭を借りていた）
- 場面 8 の最後の景色（朝靄の未舗装路と小麦畑）で窓を調べて開けると、`WindowDown` と同時に `WheatWind` を走行音に重ね始める（`DriveSound.Field`、Motor/Field）。
  0 から 3 秒で 0.80 まで上げる。夜の高速の煙草でも窓は下りるが、そちらでは鳴らさない。
  未舗装の輪は 500Hz より下に寄っていて上の帯が −41〜−51dB しかなく、麦の風は上の帯が −29〜−31dB あるので、0.80 でも埋もれない

## 車の音

`DriveSound` が持つ。乗り込みの単発（ドアを開ける・閉める・イグニッション・動き出し）と、
走行音の輪（舗装・未舗装）。**足音とは別の入れ物に置いてある。** 足音の入れ物には
`AudioReverbFilter` が付いていて、同じ入れ物の AudioSource は全部そこを通る。
走行音まで通すと、車の中にいるあいだずっとコンクリートの車庫の響きが乗る。

鳴る順は、ドアを開ける → 目が運転席へ滑る → ドアを閉める → イグニッション →
黒へ切り替えて走行音の輪 → フェードイン。**動き出しの一発（PullAway）は消した。**
舗装の走行音と同じ録音から切ったもので、オーナーの指示で外している。
黒のあいだは走行音の輪そのものを鳴らすので、明けたときには同じ音が続いている。秒数はどれも `DriveDirector` の
値で、オーナーが実画面を見てから決める。

**走行の輪だけは大きさを揃えてある。** 三度「小さい」と差し戻され、`DriveSound` の音量が
1.00 の上限に張り付いて余地が無くなったため、素材の側を持ち上げた。実効値で舗装 −19.0dB、
未舗装 −18.4dB、雨 −20.5dB、頂点はどれも −1.5dB 前後。ここだけ「素材の頂点は動かさない」の
例外にあたる。ほかの単発は素のまま置いてある。

**長い輪は Vorbis で持つ。** 未舗装の輪は 97 秒あり、生のままだと 17MB。
WebGL 書き出しなので、走行の輪と雨とエンジンは取り込みを CompressedInMemory / Vorbis（品質 0.55）にしてある。

輪の継ぎ目は頭を尻へ被せて消してある。頭と尻 0.25 秒の実効値の差は舗装で 1.4dB、
未舗装で 2.4dB なので、そのまま `loop` に掛けて段は出ない。

雨とワイパーは走行音に重ねる。別の AudioSource を持っていて、降っている景色でだけ鳴らす。

**どの景色で未舗装を鳴らすかは `DriveBand.gravel`、雨は `DriveBand.rain` が決める。** `rough`（揺れ幅）から
割り出さない。明け方の丘陵は所々荒れていて `rough` が 1.6 あるが、道そのものは舗装されている。
- 雨は `RainLoop`。プレイヤーの頭上で輪にして流し、`RainCover` が屋根の下で音量を 35% まで絞る
- インプラントジャックを抜く音は `JackPull`。`PullTimeline` の引き抜く段の頭で 1 度だけ鳴らす。チップを抜く音と同じパックから採ったが、低く伸ばして生々しさを足してある
- 扉は `DoorShut`。自室を出るときに 1 度だけ鳴らし、1.75 秒おいてから暗転へ移る
- ライターと息は `Cigarette`。`SmokeBeats` の時刻表に沿って、蓋 → 火 → 吸う → 吐く、の順に鳴らす
- 吐く息の頭で `SmokePuffs.Blow()` を呼び、煙をひと息ぶん足す。音と煙は同じ時刻表から出るのでずれない

## コンクリートの足音の作り直し

Kenney の足音はどれも柔らかい地面のもので、2.5kHz 以上が 700Hz 以下より
10.9〜15.5dB 弱い。裸のコンクリートは逆に高い打ちつけが勝つ。
`tools/make-steps.py` で唸りを抜いて打音を持ち上げ、偏りを +2.5 / −0.3 / −3.0 / −7.7dB へ寄せた。

**重さを足そうとして二度失敗し、素に戻してある。** 「軽い」と言われて落とす端を下げ、
さらに 92Hz の低い打ちを合成して重ねたが、三度目は「足音そのものが良く聞こえない」
「反響音が先になっている」「ぶつ切り」と差し戻された。軽く聞こえていた本当の原因は
素材ではなく反響の掛け方で、直の音を −140 まで削ったうえに大きく遅らせた尾を
重ねていたこと。素材は素のまま（360Hz / 2.6 倍 / 0.165 秒）へ戻し、
反響は場面 2 の小道と同じ値に揃えてある。

**5 つ全部は使っていない。** `Step4` は元から高域がいちばん少なく、持ち上げても
素材に無いものは出てこないので落とした。4 つ目（`Step3` 由来）だけ鈍いのはわざとで、
毎歩きれいに踏まない一歩が混じっていた方が繰り返しに聞こえにくい。

**採否はオーナーが聴いて決める。** こちらでは音を聴けないので、判断できるのは帯域の偏りだけ。

## 息の選び分け

吸う息と吐く息は、こちらでは音を聴けない。帯域の偏りで見分けた。素材の「Cigarette inhale」は
2.5kHz 以上が 700Hz 以下より 12.2dB 強く、吸う息は高域寄りだと判る。長い素材から切り出す
吐く息は、逆に低域寄り（高域が 19.1dB 弱い）で頭が強いものを選んだ。
**最終的な採否はオーナーが聴いて決める。** 候補は 3 つ切り出してある。

はじめは呼吸の素材を引き伸ばして作ったが、元が息切れの録音だったため、
吸っているのではなく息が上がっているように聞こえた。実際に煙草を吸っている録音へ差し替えてある。

## 音量

効果音は AudioSource 側で絞ってある。口元 0.40、足元 0.28。素材そのものは頂点を揃えたまま置く。

## 差し替えた経緯

はじめは呼吸の素材を引き伸ばして合成したが、元が息切れの録音で吸っているように聞こえなかった。
次に「Cigarette inhale」、次に火の粉が鳴る録音を試したが、いずれもこちらでは音を聴けないため決められず、
最終的にオーナーが `flint` `crackle` `blow` を用意した。いまはそれを使っている。
