# 音の出典

出どころは 2 つ。チップとジャックと、場面 2 の買い手のヒールの足音（Freesound）は CC0 1.0（パブリックドメインの献呈）、
足音と煙草の音（箱から一本取る・ジッポ・火が移る・吸う・吐く）と場面 1 の物音（椅子から立つ・紙をめくる）と雨と扉ほかは Pixabay。
前のライターの二つの音（`LighterClick.wav`・`LighterFlame.wav`）は、ジッポの一本（`Zippo.wav`）に置き替えて素材ごと外した（2026-09-28）。
村の既定の足音だった `Step1`〜`Step5`（Kenney RPG Audio、CC0 1.0）は、どこでも使わなくなったので素材ごと外した（2026-09-28）。

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
| `Concrete1`〜`Concrete4.wav` | Pixabay `freesound_community-concrete-footsteps-1-6265`（「concrete footsteps 1」。freesound_community。https://pixabay.com/sound-effects/film-special-effects-concrete-footsteps-1-6265/） | Pixabay Content License | 元は 7.58 秒、24kHz のステレオ mp3。硬い靴でコンクリートを歩いた録音から、一つの当たりで終わる一歩を四つ（0.287〜0.969 / 3.181〜3.669 / 4.127〜4.555 / 6.078〜6.420 秒。0.34〜0.68 秒）。14 歩のうちほかは、0.07〜0.16 秒後に二つ目の当たりがあるか、ほかより 12〜25dB 弱くて外した。左右を平均してモノラル 44.1kHz へ、50Hz より下を落とした。切り出しは、頭を一歩の立ち上がり（窓の実効値が頂点の 20dB 下を越える所）の 10ms ほど手前に、尻を次の一歩の立ち上がりの 15ms 手前（その前に擦りが来る物はその手前）に取り、余韻を残して尻 8ms だけなだらかに消した。大きさは一歩あたりの大きさ（0.5 秒おきに 10 回重ねて鳴らした物の integrated loudness。余韻が次の一歩に重なる分まで数える。0.5 秒に収まる音では前の測り方と同じ値）を −28.5 LUFS（前の村の既定の足音 `Step1`〜`Step5` の平均。いまは `tools/make-ambience.sh` の定数 `STEPS_TARGET_LUFS`）に、頂点 −1dB の天井を添えて揃えた。頂点 −1.8〜−4.9dB。前の `Concrete1`〜`4`（Kenney の `Step` から `tools/make-steps.py` で作り直した物）を置き替えた（`tools/make-ambience.sh steps`） |
| `HardFloor1`〜`HardFloor7.wav` | Pixabay `oxidvideos-footsteps-on-hard-floor-356919`（「Footsteps on hard floor」。作者 OxidVideos。https://pixabay.com/sound-effects/film-special-effects-footsteps-on-hard-floor-356919/） | Pixabay Content License | 元は 13.80 秒、48kHz のステレオ mp3。硬い床を歩いた録音から一歩を七つ（0.229 / 1.391 / 3.215 / 4.484 / 7.012 / 10.674 / 11.290 秒から、次の一歩の手前まで。0.53〜0.63 秒）。二つ目の当たり・擦り・尾の物音のある一歩は外した。左右を平均してモノラル 44.1kHz へ、50Hz より下を落とした。切り出しと大きさは `Concrete` と同じ。頂点 −1.2〜−3.5dB（`tools/make-ambience.sh steps`） |
| `Room1`〜`Room6.wav` | Pixabay `freesound_community-step_soundwav-14903`（「step_sound.wav」。freesound_community。https://pixabay.com/sound-effects/film-special-effects-step-soundwav-14903/） | Pixabay Content License | 元は 15.50 秒、24kHz のステレオ mp3（左右は同じ）。部屋の床を歩いた録音から一歩を六つ（1.656 / 4.117 / 8.111 / 9.245 / 11.001 / 11.524 秒から。0.52〜0.67 秒）。40Hz より下に −44dB の揺れがずっと乗っているので、100Hz より下を 24dB/oct で落とした。雑音の除去はしていないので、尾の床は頂点より 30〜40dB 下とほかの組より近い。切り出しと大きさは `Concrete` と同じ。頂点 −1.2〜−5.2dB（`tools/make-ambience.sh steps`） |
| `Rug1`〜`Rug6.wav` | Pixabay `freesound_community-footsteps_1-30138`（「footsteps_1」。freesound_community） | Pixabay Content License | 元は 13.87 秒、24kHz のステレオ mp3（左右は同じ）。頭の 3 秒は無音で、その後に柔らかい床を 0.6 秒ほどの歩調で 17 歩。一歩ごとに 40Hz より下の大きな揺れ（足が床に着いた時の揺れと思われる。中身の帯より 30dB 上）が乗るので、70Hz より下を 24dB/oct で落とした。一歩を六つ（3.316 / 3.877 / 4.519 / 5.108 / 9.474 / 10.733 秒から。0.56〜0.65 秒）。擦れが引いた 0.17〜0.25 秒後に二つ目の当たりがある物、ほかより弱い物、頂点の前に擦りの続く物、尾に次の一歩の擦りが来る物は外した。切り出しと大きさは `Concrete` と同じ。元が小さく（一歩の頂点 −44.6〜−50.5dB）、+31.8〜+36.8dB 持ち上げた。頂点 −7.7〜−10.7dB。自室のラグの上（オーナー、2026-09-29「自室のラグの上を歩く時の音を変更」。`tools/make-ambience.sh steps`） |
| `PackPull.wav` | Pixabay `freesound_community-cigarette-box-handling-shaking-dropping-59285`（「cigarette box handling shaking dropping」。freesound_community。https://pixabay.com/sound-effects/cigarette-box-handling-shaking-dropping-59285/ 。オーナーが 0〜2.434 秒で切り出したもの） | Pixabay Content License | 元は 133.51 秒、48kHz のモノラル mp3。0〜2.434 秒（頭はデジタルの無音、尻は次の物音の手前の静かな所）を 44.1kHz へ、尻 30ms をなだらかにした。低い揺れはほとんど無いので低域は落としていない。大きさは一番大きい 400ms の窓の大きさ（momentary の最大）を `Drag.wav` と同じ −30.6 LUFS に揃え（+6.2dB）、頂点 −7.8dB。場面 1 で煙草の箱から一本取る音（`tools/make-ambience.sh smoke`） |
| `Zippo.wav` | Pixabay `fronbondi_skegs-foley-zippo-cigarette-lighter-open-and-close-sound-effects-235249`（「FOLEY - Zippo Cigarette Lighter Open and Close Sound Effects」。作者 Fronbondi_Skegs。https://pixabay.com/sound-effects/foley-zippo-cigarette-lighter-open-and-close-sound-effects-235249/ 。オーナーが 1.630〜5.321 秒で切り出したもの） | Pixabay Content License | 元は 18.74 秒、48kHz のモノラル mp3。1.630〜5.321 秒の 3.691 秒に、蓋を開ける金属音（0.35 秒）・フリントを擦って火が点く音（0.954 秒から 50ms ほどの雑音の塊）・蓋を閉じる音（2.43 秒と 2.55 秒の二段）が入っている。地に 40〜160Hz の揺れ（静かな所で −53dB）が乗っているので、250Hz より下を 24dB/oct で落とした（静かな所が −63.5dB に下がり、三つの音は削れない）。44.1kHz へ、頭 5ms と尻 20ms をなだらかにした。**当たりの頭を 4 分の 1 に潰して胴を持ち上げ**（ffmpeg の acompressor。−30dB から上を比 4、アタック 0.3ms、リリース 60ms）、頂点が −1dB の天井に当たるまで上げた（momentary の最大 −17.1 LUFS。ほかの煙草の音の揃え先 `Drag.wav` の −30.6 LUFS より 13.5dB 上）。オーナー、2026-09-29「ライターの音を大きく」で一度 6dB 上げた（−24.6 LUFS）が、同日「ジッポの音自体まだ小さい」。中身が短い当たり三つと静かな間で、400ms の窓で揃えると当たりが薄まって数えられ、100ms の窓で比べると点火は箱の音より 7dB 小さかった。いまは 100ms の窓で、蓋を開ける音 −15.2・点火 −22.9・蓋を閉じる音 −20.3dB（箱の音の −27.9dB より 12.7・5.0・7.6dB 上）。**点火の時刻（0.954 秒）は `SmokeBeats.StrikeInZippo` が持つ。** 場面 1・5・8 のジッポ。前の `LighterClick.wav`（金属音だけ）と `LighterFlame.wav`（火の音だけ）をこの一本に置き替えた（`tools/make-ambience.sh smoke`） |
| `CigaretteLit.wav` | Pixabay `freesound_community-cigarette-suck-107102`（「Cigarette suck」。freesound_community。https://pixabay.com/sound-effects/cigarette-suck-107102/ 。オーナーが 0〜3.142 秒で切り出したもの） | Pixabay Content License | 元は 3.53 秒、44.1kHz のモノラル mp3。40Hz より下に大きな揺れ（吸う息がマイクに当たった物と思われる。全体の実効値の大半）が乗り、中身（0.72 秒の口元の当たり、0.88〜1.84 秒の葉が燃える小さなはぜ、その後の細い尾）は 5〜20kHz にある。250Hz より下を 24dB/oct で落とした（頭の静かな所が −68.6dB に下がり、はぜは 0.3dB しか削れない）。頭 5ms と尻 50ms をなだらかにし、momentary の最大を −30.6 LUFS に揃えようとして頂点 −3dB の天井で止めた（+15.8dB、−31.5 LUFS）。聞こえ始めは頭から 0.72 秒。場面 1・5・8 で、ジッポが点いた瞬間から鳴らす「煙草に火が移った音」（`tools/make-ambience.sh smoke`） |
| `ChairRise.wav` | Pixabay `freesound_community-couch-quick-rise-up-4_bip-35534`（「couch quick rise up 4_bip」。freesound_community。オーナーが 4.982〜7.730 秒で切り出したもの） | Pixabay Content License | 元は 13.70 秒、24kHz のステレオ mp3（左右は同じ）。ソファのクッションが沈みから戻って軋む音で、中身は 0.1〜0.9 秒の 80〜600Hz、その後は部屋の静かな所。低い揺れは無いので低域は落としていない。左右を平均してモノラル 44.1kHz へ、頭 5ms と尻 50ms をなだらかにし、momentary の最大を −26 LUFS に揃えた（+11.5dB）。頂点 −9.8dB。2.748 秒。自室の椅子から立ち上がる音（オーナー、2026-09-29「椅子から立ち上がる際の音を追加」。`tools/make-ambience.sh foley`） |
| `PaperTurn.wav` | Pixabay `freesound_community-paper-turn-40077`（「paper turn」。freesound_community。オーナーが 4.043〜6.474 秒で切り出したもの） | Pixabay Content License | 元は 10.75 秒、24kHz のステレオ mp3（左右は同じ）。紙をめくる擦れと返る音（0.4〜1.2 秒の 1.2〜5kHz）に、紙か手がマイクに当たった低い揺れ（40〜80Hz）が乗るので、150Hz より下を 24dB/oct で落とした。左右を平均してモノラル 44.1kHz へ、頭 5ms と尻 50ms をなだらかにし、momentary の最大を −26 LUFS に揃えようとして頂点 −3dB の天井で止めた（+3.0dB、−26.8 LUFS）。2.431 秒。場面 1 のクリップボードのメモと場面 3 の同じメモを調べた時の音（オーナー、2026-09-29「メモにインタラクトしたときの紙の音を追加」。`tools/make-ambience.sh foley`） |
| `Drag.wav` | Pixabay の `crackle`。作品名と作者は未記入 | Pixabay Content License | 前後の無音を落として頂点 −8dB |
| `Blow.wav` | Pixabay の `blow`。作品名と作者は未記入 | Pixabay Content License | 同上 |
| `ChipPull.wav` | OpenGameArt「Sound Effects Pack」（https://opengameart.org/content/sound-effects-pack）作者 OwlishMedia の `Technology/plugpull.wav` | CC0 1.0 | 0.02〜0.40 秒、頂点 −6dB |
| `JackPull.wav` | 同上パックの `Technology/plugpull2.wav` | CC0 1.0 | 3.33 秒から 0.50 秒を切り出し、再生速度 0.85 倍で低く伸ばし、前後の無音を落として頂点 −6dB |
| `Heels1`〜`Heels6.wav` | Freesound「Footsteps heels pavement denoised」（作者 YannSauvin。https://freesound.org/people/YannSauvin/sounds/778103/ 。落としたのは試聴用の mp3 の高い方で、元の wav は落とすのに会員の登録が要る） | CC0 1.0 | 元は 52.5 秒、48kHz のモノラル mp3（188kbps）。ヒールで舗道を 0.545 秒ほどの歩調で歩いた録音で、雑音は除いてある。踵の鋭い当たりに 72〜80ms 後の爪先の当たりが続く一歩を六つ（2.964〜3.505 / 3.510〜4.048 / 4.053〜4.593 / 4.598〜5.134 / 5.685〜6.227 / 6.232〜6.758 秒。0.53〜0.54 秒）。尾に物音が混じる歩と、7.85 秒から後（歩調が乱れる）は外した。44.1kHz へ、50Hz より下を落とし、切り出しと大きさは足音の組と同じ決まり（一歩あたり −28.5 LUFS、頂点 −1dB の天井。頂点 −1.1〜−3.2dB）。場面 2 の売り買いで、買い手 C（女）が卓の向こうへ歩いてくる足音（`tools/make-ambience.sh heels`）。男の買い手は `Concrete1`〜`4` |
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
| `Breathing.wav` | Pixabay `freesound_community-breathing-6811`（「Breathing」。作者 SofieHolmark（Freesound）。https://pixabay.com/sound-effects/breathing-6811/ 。タグは Mouth / Human / Breath / Breathing / Female、頁に AI generated の表示は無い。オーナーの許諾済み） | Pixabay Content License | 元は 20.74 秒、24kHz のステレオ mp3（左 −48.7dB・右 −49.1dB の実効値、頂点 −33dB と小さい）。**モノラルへは左右の平均で畳んだ。** 丸ごと平均すると左右より 5dB 小さくなるが、削れるのは 20Hz より下の左右で逆向きに揺れる低い揺れ（マイクの揺れと思われる）で、息の芯（500Hz〜8kHz）では平均と左右の差が −0.6〜−1.0dB しかない。200Hz より上の左右の相互相関はずれ 0 で 0.67 がいちばん高く、左右に時間のずれが無いので、平均しても櫛形の欠けは出ない。片側だけにしないのは、息ごとに左右の大きさが 3〜7dB 入れ替わる（オーナーの言う「左右に振れている」）ため。100Hz より下を 24dB/oct で落とした（息のあいだの静かな所にも残る部屋の揺れ）。高域寄りで短い吸う息と、低域寄りで長く細る吐く息が 3.1 秒ほどで一巡する。3.28〜18.66 秒を切り出し、尻 0.08 秒（吐き終わりの静かな所、20ms 窓で −77〜−80dB）を頭 0.08 秒（同じく静かな所）へ等パワーで重ねて輪にした（頭の一巡は吸う息が明るすぎて外した）。15.30 秒で吸って吐くのが 5 回。44.1kHz へ、integrated −24 LUFS（頂点 −5.3dB）に揃えた。場面 1 の冒頭の呼吸（`tools/make-ambience.sh breath`） |
| `CrowdLoop.wav` | Pixabay `freesound_community-crowd_talking-6762` | Pixabay Content License | 7.0〜57.0 秒の 50 秒を切り出し（6.6 秒のいちばん大きな声と、86 秒からのフェードアウトを避けた）、モノラル 22.05kHz へ。末尾 2 秒を頭に重ねて輪にし、実効値を −22dBFS に揃えた。48.0 秒。場面 2 の通りとヤードの雑踏 |
| `VillageMorning.wav` | Pixabay `freesound_community-030510whichford-18349`（「030510whichford」。作者 lunasound（Freesound）。イギリスの村の夜明けの鳥の声） | Pixabay Content License | 元は 309.2 秒。60.5〜122.5 秒の 62 秒を切り出し、モノラル 22.05kHz へ。末尾 2 秒を頭に重ねて輪にし、実効値を −24dBFS に揃えた。60.0 秒。避けたのは、録り始めの低い揺れ（0〜4 秒）、大きなしわがれた鳴き声（48.5〜53 秒・56〜60 秒）、150Hz より下の唸りが続く所（124〜234 秒。風か遠くの車か見分けられない。218 秒に低い衝撃音）、520〜560Hz の小さな音が 2.5 秒おきに続く所（186〜200 秒。遠くのカッコウか鳩の候補）、倍音のそろった鳴き声（256〜259 秒・302〜304 秒）、教会の鐘（268〜300 秒）。カッコウらしい「高→低」の二音の繰り返しは全体で見つからなかった。区間の中には 106.8〜109 秒に倍音のある鳴き声が 3〜4 回残る。村の朝 |
| `WheatWind.wav` | Pixabay `freesound_community-wheat-in-the-wind-7159`（「Wheat in the Wind」。作者 bdvictor（Freesound）） | Pixabay Content License | 元は 137.7 秒。37.8〜105.8 秒の 68 秒を切り出し（37.0 秒と 106.0 秒の乾いたクリックを避けた）、モノラル 22.05kHz へ。末尾 2 秒を頭に重ねて輪にし、実効値を −24dBFS に揃えた。66.0 秒。約 10 秒おきに入る 5.2kHz の虫の声は残してある（オーナーの了承済み）。村の朝と夕方、場面 8 の麦畑 |
| `GateCreak.wav` | Pixabay `dobcommunications-creaky-wooden-gate-opens-170210`（「Creaky Wooden Gate Opens」。作者 DOBCommunications） | Pixabay Content License | モノラル 44.1kHz へ、前後の −60dB 未満を落とし、頂点を −6dB に揃えた。2.19 秒。村の片割れの家の格子戸を開ける音 |
| `Gravel1`〜`Gravel6.wav` | Pixabay `freesound_community-going-on-a-forest-road-gravel-and-grass-6404`（「Going on a forest road gravel and grass」。freesound_community。https://pixabay.com/sound-effects/nature-going-on-a-forest-road-gravel-and-grass-6404/） | Pixabay Content License | 元は 31.32 秒、48kHz のモノラル mp3。林道の砂利と草の上を歩いた録音。**切り出す前に、全体に乗っている広い帯域の雑音を除いた**（ffmpeg の afftdn。足音の間の雑音だけの所 12 か所から型を取り、nf −42 / nr 24。型に使っていない別の 12 か所で測って、床が −45.6 → −64.9dB、足音の芯（塊の立ち上がりから 0.1 秒の 200Hz〜4kHz の実効値）の削れは 0.0〜0.9dB、頂点の削れは 0.2〜0.7dB）。44.1kHz へ、100Hz より下を落とし、一つの塊で終わる一歩を六つ（0.675 / 4.125 / 23.360 / 24.980 / 26.050 / 29.860 秒から。0.36〜0.53 秒）。二つ目の塊のある一歩、塊の前に長い擦りのある一歩、草に入って小さい 14〜22 秒は外した。切り出しと大きさは `Concrete` と同じ。頂点 −1.0〜−5.1dB。前の `Gravel1`〜`6`（「Walking on a road with gravel 01」から切った物）を置き替えた。村の未舗装の道の足音（`tools/make-ambience.sh gravel`） |
| `Grass1`〜`Grass6.wav` | Pixabay `freesound_community-walking-through-grass-80308`（「Walking through grass」。freesound_community、オーナーの許諾済み） | Pixabay Content License | 元は 12.38 秒、44.1kHz のモノラル mp3。一歩ずつ録った単発が並ぶ録音から、一つの塊で終わる一歩を六つ切り出した（1.790 / 2.490 / 5.110 / 5.770 / 8.910 / 10.010 秒から 0.34 秒ずつ。numpy が無いので ffmpeg で 16bit PCM に落とし、素の Python で 4ms 窓・2ms 送りの実効値を積んで塊を見た。次の塊まで 0.34 秒の余白が無い三つ（9.405 秒はほぼ天井の頂点で二歩の重なりの疑い、10.465 秒は塊の長さ自体が 0.40 秒で長すぎる、11.345 秒は残り一つで数が増えないので見送り）は避けた）。100Hz より下を落とし、頭 4ms をなだらかにして 0.20 秒から 0.14 秒かけて消した。大きさは `Gravel` と同じ揃え先（−28.5 LUFS）に、頂点 −3dB の天井を添えた。頂点は −3.0〜−10.5dB。村の芝の足音（`tools/make-ambience.sh grass`） |
| `GardenHose.wav` | Pixabay `freesound_community-watering-62546`（「Watering」。作者 elittle13（Freesound）。早朝に自分の庭のホースで植木に水をやった録音） | Pixabay Content License | 元は 25.22 秒、24kHz のステレオ mp3（左右はほぼ同じ）。1.15〜14.50 秒の 13.35 秒を切り出し（出し始めの 0.25〜1.1 秒、14.55 秒の何かに当たった低い音、24.5 秒からのフェードアウトを避けた）、左右を平均してモノラル 44.1kHz へ（いちばん強いのが 6〜11kHz の帯なので 22.05kHz にしない）。末尾 2 秒を頭に重ねて輪にし、実効値を −24dBFS に揃えた。11.35 秒、頂点 −5.3dB。場面 6 で女性がホースで花に水を撒く音（`tools/make-ambience.sh garden`） |
| `GardenHoseStop.wav` | Pixabay `freesound_community-hose-sounds-24388`（「Hose Sounds」。作者 JazzyBay（Freesound）。よく軋む栓を開け、吹きさらしの車庫でホースからコンクリートに水を撒いた録音） | Pixabay Content License | 元は 128.93 秒、48kHz のモノラル mp3。短く出しては止めた所のうち、ノズルを離して 0.2 秒で 18dB 落ちる 101.35 秒の止まりを使った。101.22〜102.32 秒の 1.10 秒（止まる前の噴きは 0.13 秒だけ。101.86 秒で噴き残りが止みきり、あとは滴り）。120Hz より下の車庫の唸りを落とし、頭 50ms をなだらかに入れ、0.80 秒から 0.30 秒かけて消した。モノラル 44.1kHz、頂点 −6dB、実効 −33.2dB。場面 6 で水を止める音 |
| `SignalGlitch.wav` | Pixabay `freesound_community-computer-glitch-corrupted-file-96176`（「Computer Glitch, Corrupted File」。作者 Diicorp95（Freesound。利用者の頁は消えていて、元の説明は読めない）） | Pixabay Content License | 元は 6.17 秒、24kHz のステレオ mp3（左右は同じ）。40ms ほどの雑音の粒がデジタルの無音を挟んで並ぶ、データが欠けて飛ぶような音。1.68〜4.62 秒の 2.94 秒を切り出し（頭の「ピッ」に近い音程のある一発と、4.72 秒からのまばらな所を避けた。切り口はどちらも無音の中）、モノラル 44.1kHz へ、頂点を −6dB に揃えた。実効 −23.1dB。場面 6 で名を呼ぶ口元が動く間に鳴らす |
| `SignalCut.wav` | Pixabay `freesound_community-glitch-49142`（「Glitch」。作者 smokenweewALT（Freesound。いまの名は smokevhstapes）。動画をデータベンディングして録り直した物） | Pixabay Content License | 元は 0.50 秒、44.1kHz のモノラル mp3。全帯域の雑音が立ち上がり、0.490 秒で断ち切られる。0.020〜0.490 秒の 0.47 秒を切り出し、頭 2ms と尻 3ms だけなだらかにして断ち切れの鋭さを残した。頂点を −6dB に揃えた（元は 0dB 近く）。実効 −18.3dB。場面 6 で記憶が途切れる瞬間の「ぶつっ」 |

## 曲

オーナーが Suno の Pro プランで生成した曲（`docs/superpowers/specs/2026-09-24-music-design.md` の 4 節）。
許諾は Pro プランの商用利用の権利で、帰属の表示は求められていない。元の mp3 は repo に置かない。

| ファイル | 元の曲 | 許諾 | 加工 |
|---|---|---|---|
| `Music/SlowCountry.ogg` | 「Slow Country」 | Suno の Pro プランで生成 | ヤードの出店の古いスピーカーから鳴る音にした。モノラルに畳み、200Hz より下と 5kHz より上を 24dB/oct で落とし、1.8kHz を +3dB。軽く圧縮して軽く歪ませ、ffmpeg で作ったインパルス応答（煉瓦の壁の初期反射 5 本と、0.7 秒ほどの残響）の響きを 15〜20% 足した。integrated −18 LUFS・true peak −1.5dBTP 以下に揃え、22.05kHz の Ogg Vorbis（q5）へ。長さは元のまま |
| `Music/PeachLight.ogg` | 「Peach Light」 | 同上 | 同上 |
| `Music/TheOnesWhoStayed.ogg` | 「The Ones Who Stayed」 | 同上 | 同上 |
| `Music/CopperHeart.ogg` | 「Copper Heart」 | 同上 | 同上 |
| `Music/HalfAware.ogg` | 「HALF AWARE」（エンディングの曲。元は 215.43 秒、44.1kHz のステレオ mp3、integrated −10.2 LUFS） | 同上 | エンディングの素の版。元の 0〜213.0 秒（尾の後の無音を落とし、尻 0.5 秒をなだらかに。頭は切らず、秒は元のまま）。ステレオ 44.1kHz のまま、線形の増減（−7.8dB）だけで integrated −18 LUFS（true peak −7dBTP ほど）、Ogg Vorbis（q6）。ドロップ（36.012 秒の頭の打ち）から後はこちらだけが鳴る |
| `Music/HalfAwareCar.ogg` | 同上 | 同上 | エンディングの車の版。車の古いスピーカーから鳴って、車内で聞いている音にした。元の 0〜40.0 秒（ドロップの 4 秒後まで。後は鳴らさないので切る）。モノラルに畳み、160Hz より下と 5.5kHz より上を 24dB/oct で落とし、1.5kHz を +4dB、350Hz を −2dB。軽く圧縮して軽く歪ませ、ffmpeg で作ったインパルス応答（車内の初期反射 5 本 0.9〜5.6ms と、0.12 秒で消える尾）の響きを原音より 9dB ほど下で足した。0〜36 秒の大きさを素の版の同じ所（−21.3 LUFS）に揃え、頂点を −1.5dB で止め、22.05kHz の Ogg Vorbis（q5）へ。素の版と同じ頭から切り出してあり、同じ時刻に鳴らし始めてドロップの手前で入れ替える |

## 場面ごとの BGM

オーナーが Pixabay の Music から選んだ、歌の無い曲（`docs/superpowers/specs/2026-09-24-music-design.md` の 5 節）。場の外から鳴る曲で、`MusicBed` が 2D で流す。
7 曲とも Pixabay Content License（上の節）。取り込む前に頁で、AI generated の表示もタグも無いこと、タグとジャンルに歌が無いことを確かめた（耳では確かめていない）。
どれもステレオ 44.1kHz のまま、輪にしたクリップ一つ分の積分ラウドネスを −22 LUFS に揃え（true peak −1.5dBTP まで）、Ogg Vorbis（q6）で書き出した。
切り出しと輪の作りは `tools/make-ambience.sh` の 9 節（`bash make-ambience.sh bgm`）。元の mp3 は `unity/RawAssets/audio/pixabay/` に Pixabay の元の名前で置く（git には入れない）。

**Content ID に登録のある曲がある**（表の「Content ID」の列）。許諾の上では使ってよいが、遊んでいる所を撮った YouTube の動画に自動の申し立てが付くことがある。
申し立ては Pixabay の許諾の頁を添えて異議を出せる。登録のある曲は、Pixabay の頁（要ログイン）から許諾の証書を落とせる。

| ファイル | 出典 | Content ID | 加工 |
|---|---|---|---|
| `Music/MelancholicAmbient.ogg` | Pixabay `universfield-melancholic-ambient-background-351787`（「Melancholic Ambient Background」。作者 Universfield。https://pixabay.com/music/ambient-melancholic-ambient-background-351787/） | 無し | 元は 132.07 秒、48kHz。頭 12 秒ほどのフェードインと 124.5 秒からのフェードアウトを避け、23.70 秒から 95.94245 秒を輪にした（同じ素材が 95.94 秒おきに繰り返す。波形の相関 0.92）。尻の 4 秒を一周前の同じ所（19.70〜23.70 秒）へ hsin で渡す。−4.7dB。95.94 秒。場面 4 の前半 |
| `Music/Parkside.ogg` | Pixabay `blairellair-parkside-114970`（「Parkside」。作者 blairellair。https://pixabay.com/music/ambient-parkside-114970/） | 無し | 元は 238.03 秒。頭 3.96 秒の無音と、228 秒過ぎに録音の側で断たれる所を避けて 3.80〜228.00 秒。尻の持続音 3 秒を頭の立ち上がりへ qsin で重ねて輪にした。+4.7dB（true peak −2.1dBTP）。221.2 秒。場面 4 の後半（`切断` が押せるようになってから） |
| `Music/MellowAmbient.ogg` | Pixabay `sharvarion-mellow-ambient-piano-pad-guitar-strings-138801`（「Mellow Ambient (Piano Pad, Guitar, Strings)」。作者 sharvarion。https://pixabay.com/music/ambient-mellow-ambient-piano-pad-guitar-strings-138801/） | 無し | 元は 270.89 秒。場面 6 は 1 分ほどで途切れるので、頭の 0.80〜90.00 秒だけ（90.0 秒は次の区切り）。尻を 6 秒で消し、頭から弾き直す輪。+8.8dB（頭の 90 秒は −30.8 LUFS と静か）。89.2 秒。場面 6 の庭の記憶 |
| `Music/Remembrance.ogg` | Pixabay `joelfazhari-remembrance-dreamy-emotional-and-melancholic-music-loopable-13943`（「Remembrance - Dreamy Emotional and Melancholic Music (Loopable)」。作者 JoelFazhari。https://pixabay.com/music/ambient-remembrance-dreamy-emotional-and-melancholic-music-loopable-13943/） | **有り** | 元は 65.49 秒。前半と後半が同じ物（32.72728 秒ずれた波形の相関 0.99）なので、後半の頭 32.748096 秒から 32.72728 秒を輪にし、尻の 2 秒を前半の尻へ hsin で渡した（そのままだと頭の 21ms の無音と、残響の無い頭 1 秒で継ぎ目に段が付く）。−6.4dB。32.73 秒。場面 8（チップの独白から村へ着くまで） |
| `Music/Ambient580528.ogg` | Pixabay `leberch-ambient-580528`（「Ambient」。作者 leberch。https://pixabay.com/music/ambient-ambient-580528/） | **有り** | 元は 140.04 秒。0.40〜138.80 秒（頭の無音と尻の −60dB より下を落とす）、尻 0.30 秒を消して頭から弾き直す輪（尻の減衰から頭の句へ戻るのは、曲の中の息継ぎと同じ形）。−2.2dB。138.4 秒。場面 9 の村に近づく所 |
| `Music/Ambient578724.ogg` | Pixabay `leberch-ambient-578724`（「Ambient」。作者 leberch。https://pixabay.com/music/ambient-ambient-578724/） | **有り** | 元は 152.03 秒、142 秒からフェードアウト。8.889 秒おき（108 BPM の 4 小節）の句の打ち 12 回ぶん、13.75 秒から 106.7542 秒を輪にし、尻の 4 秒を一周前（9.75〜13.75 秒）へ qsin で渡した（色の相似 0.88、波形の相関 0.50）。−1.5dB。106.75 秒。場面 10 の対面 |
| `Music/Cinematic586317.ogg` | Pixabay `leberch-cinematic-586317`（「Cinematic」。作者 leberch。https://pixabay.com/music/suspense-cinematic-586317/） | **有り** | 元は 146.05 秒。0.55〜145.80 秒（頭の無音と尻の −70dB より下を落とす）、尻 0.30 秒を消して頭から弾き直す輪（曲の終わりの減衰と始まりのまばらな入りがそのままつながる）。−3.7dB。145.25 秒。場面 7 の気づき |

雑踏の輪と曲の加工は `tools/make-ambience.sh` で作り直せる（村の朝・麦の風・格子戸は同じ中の 5 節。`bash make-ambience.sh village` でそれだけ作り直す。
村の未舗装の道の足音は 6 節で、`bash make-ambience.sh gravel`。芝の足音は 7 節で、`bash make-ambience.sh grass`。
足音の三つの組（硬い床・コンクリート・自室）は 11 節で、`bash make-ambience.sh steps`。
場面 6 の庭の 4 つ（ホースの輪・水を止める音・途切れ・断ち切れ）は 8 節で、`bash make-ambience.sh garden`。
エンディングの曲の二つの版（素の版と車の版。ドロップの時刻の調べ方もここ）は 10 節で、`bash make-ambience.sh ending`（元の mp3 は `SRC_SONG`）。
素材の mp3 は `unity/RawAssets/audio/pixabay/` に Pixabay の元の名前で置く。git には入れない）。WebGL では Unity のオーディオのフィルターが効かないので、
スピーカーらしさと響きはファイルに焼き込んである。

切り出しの手順は ffmpeg で、`docs/` ではなくここに残す。素材そのものは repo に置かず、加工後の物だけを置いている。

煙草の 3 つは Pixabay のどの作品かまで辿れていない。**作品名と作者を控えておくこと。**
表記の義務は無いので公開の妨げにはならないが、差し替えや問い合わせのときに要る。

## 使うところ

- 足音は `Footsteps`。進んだ距離を積んで 0.88 メートルごとに 1 つ鳴らす。直前と同じ物は選ばず、音量と高さを少し振る。
  一歩ずつの単発は余韻を次の一歩の手前まで残してあり（0.34〜0.68 秒）、歩き（1.4 m/s で 0.63 秒おき）でも次の一歩が前の余韻に重なることがある。
  `PlayOneShot` は鳴っている音を切らずに別の声で重ねるので、余韻は途切れない（高さの振れは足元の AudioSource に一つなので、
  次の一歩を鳴らすと前の余韻の高さも替わる。そのときの余韻は床の空気だけで、頂点（5ms 窓の実効値）より 28〜50dB 下。自室の組が 28〜38dB でいちばん近い）
- 自室（場面 1・3・5・7）は `Room1`〜`Room6`、場面 2（通り・小道・ヤード）は `Concrete1`〜`Concrete4`、場面 8（共用ガレージ）は `HardFloor1`〜`HardFloor7`
  （オーナーの指定、2026-09-28。組の表は `StepSets`、差し替えは `HalfAware/Put the footsteps in the scene`）。
  自室のラグの上は `Rug1`〜`Rug6`（2026-09-29）。ラグ（Room/Rug）自身に当たりと `StepGround` を付けてあり、ラグを動かせば一緒に動く（`HalfAware/Put the foley in the room`）
  ガレージと小道の反響は素材に焼かず、足元の `AudioReverbFilter` に持たせる
- 場面 4（潜る）は場所ごとに既定の組を替え（`DiveDirector` の placeSteps。`BuildDive.PlaceStepPaths`）、一つの場所で床の違う所は床の当たりの `StepGround` で分ける。
  公営住宅は `Concrete`（外階段・踊り場・デッキ・地面の小径）で、住戸の絨毯は `Room`、台所と浴室の陶板は `HardFloor`。公園は `Grass`（芝）で、小径と門の外の歩道は `Concrete`。
  電車と教室は `HardFloor`。台所の家は `Room`（居間の板と階段）で、台所のリノリウムと廊下・玄関先の陶板は `HardFloor`
- 村（`Village.unity`）の足音は地面で替える。`Footsteps` が一歩ごとに足元の当たりを見て、`StepGround` が付いていればその音で鳴らす。
  未舗装の路地（車の着く所の未舗装路と門の前の砂利の溜まりを含む）は `Gravel1`〜`Gravel6`、庭の煉瓦の小路（格子戸からの小路とトンネルを抜ける小路）・東屋の前の踊り場・玄関の小路・テラスは `Concrete1`〜`Concrete4`、
  芝の路肩と庭の芝は `Grass1`〜`Grass6`（`BuildVillageSound.StepGrounds`）。村で `StepGround` の無い所も `Grass1`〜`Grass6`（村の既定。2026-09-28 に `Step1`〜`Step5` から替えた）
- 自室の空気の音は `RoomTone`。自室が舞台の場面 1・3・5 で、プレイヤーの頭上で 2D の輪にして小さく流す（`RoomTone`。大きさはインスペクターで変える）。場面を終えて暗転するときは、その暗転に合わせて絞る
- 場面 1 の冒頭の呼吸は `Breathing`。場面の頭から黒のまま Player/Breath の 2D の音源で輪にして鳴らし、ジャックが手首から抜けたところから 1.2 秒で薄れて止む（`RoomIntroDirector`。大きさは breathVolume、既定 0.5。`RoomTone`（0.5）と並べてインスペクターで詰める）。コンソールを開いている間も止めない
- 村（`Village.unity`）の環境音は `VillageAmbience`（Player/Ambience）。プレイヤーの頭上で 2D の輪にして流し、時刻（`VillageHour`）で鳴らす物を替える。
  朝は `VillageMorning`（0.4）と `WheatWind`（0.5）を重ね、夕方は `WheatWind`（0.6）だけ。時刻が替わると 2 秒で入れ替える。
  朝の `WheatWind` は立ち位置で薄くする。車を降りて路地を歩き出してすぐ（x −70）から薄れ始め、いちばん手前の家（家 C の西の端、x −52.6）にたどり着く頃には消えきる。戻ればまた聞こえる。
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

**場面 8 の終わり**（最後の景色の余韻が明けたところ）は、黒へ切り替えて `CarStopHandbrake` を鳴らし（`DriveSound.Park`）、
走行の輪と麦の風をそこから 5.7 秒かけて両端を緩めて 0 まで下げる（`DriveSound.Settle`。止まる音の中で車が止まりきるのが 5.7 秒あたり）。
止まる音が鳴り終わって 0.6 秒おいて `CarDoorShut`、その 2 秒後に村へ切り替える。暗転は挟まない。
秒数はどれも `DriveDirector` の「村へ」の値で、仮置き。取り込みはイグニッションと同じく単発の既定（Decompress On Load、Vorbis 品質 100%）のまま。

**どの景色で未舗装を鳴らすかは `DriveBand.gravel`、雨は `DriveBand.rain` が決める。** `rough`（揺れ幅）から
割り出さない。明け方の丘陵は所々荒れていて `rough` が 1.6 あるが、道そのものは舗装されている。
- 雨は `RainLoop`。プレイヤーの頭上で輪にして流し、`RainCover` が屋根の下で音量を 35% まで絞る
- インプラントジャックを抜く音は `JackPull`。`PullTimeline` の引き抜く段の頭で 1 度だけ鳴らす。チップを抜く音と同じパックから採ったが、低く伸ばして生々しさを足してある
- 扉は `DoorShut`。自室を出るときに 1 度だけ鳴らし、1.75 秒おいてから暗転へ移る
- ライターと息は `Cigarette`。`SmokeBeats` の時刻表に沿って、蓋 → 火 → 吸う → 吐く、の順に鳴らす。
  場面 1 は、箱から一本取る音（`PackPull`）が鳴り終わった所でジッポを鳴らす（煙草を取った 1 ページを読んでいる途中でも待たない）
- 椅子から立ち上がる音は `ChairRise`。`SceneFlow` の物音の音源（口元の Voice）で、自室の椅子から立ち上がり始めた時に鳴らす。
  場面 1 はジャケットを着た後の独白を読み終えて立つ時と、端末の席から戻り始めた時（`TerminalSeat`）、場面 7 はジャックの後に立つ時
- 紙をめくる音は `PaperTurn`。場面 1 のクリップボードのメモと場面 3 の同じメモを調べた時に、同じ音源で鳴らす（`Interactable` の sound）
- 吐く息の頭で `SmokePuffs.Blow()` を呼び、煙をひと息ぶん足す。音と煙は同じ時刻表から出るのでずれない

## 足音の差し替え（2026-09-28）

オーナーの指示で、自室・場面 2・場面 8・村の未舗装の道の足音を Pixabay の録音から切り出した物に替え、
場面 4 も場所ごとにそこから選ぶようにした。どれも録音の中で何歩も鳴っている物を一歩ずつに切り分け、
`Footsteps` が組の中からランダムに選ぶ。余韻は次の一歩が鳴る手前まで残してある。

前の `Concrete1`〜`4` は Kenney の柔らかい足音から唸りを抜いて打音を持ち上げた物（`tools/make-steps.py`）で、
使う所が無くなったので中身を新しいコンクリートの録音に置き替え、`make-steps.py` は消した（走らせると新しい `Concrete` を上書きするため）。
そのとき分かった**反響の掛け方**はそのまま生きている: 軽く聞こえた本当の原因は素材ではなく、直の音を −140 まで削ったうえに
大きく遅らせた尾を重ねていたことだった。場面 2 と 8 の足元の `AudioReverbFilter` は、直の音を削らず遅れも触らない値にしてある。

**採否はオーナーが聴いて決める。** こちらでは音を聴けないので、判断できるのは帯域の偏りと包絡だけ。

## 息の選び分け

吸う息と吐く息は、こちらでは音を聴けない。帯域の偏りで見分けた。素材の「Cigarette inhale」は
2.5kHz 以上が 700Hz 以下より 12.2dB 強く、吸う息は高域寄りだと判る。長い素材から切り出す
吐く息は、逆に低域寄り（高域が 19.1dB 弱い）で頭が強いものを選んだ。
**最終的な採否はオーナーが聴いて決める。** 候補は 3 つ切り出してある。

はじめは呼吸の素材を引き伸ばして作ったが、元が息切れの録音だったため、
吸っているのではなく息が上がっているように聞こえた。実際に煙草を吸っている録音へ差し替えてある。

## 音量

効果音は AudioSource 側で絞ってある。口元 0.40、足元 0.28。素材そのものは頂点を揃えたまま置く。
煙草の三つ（`PackPull`・`Zippo`・`CigaretteLit`）は、頂点ではなく一番大きい 400ms の窓の大きさ（momentary の最大）を `Drag.wav`（−30.6 LUFS。前の `LighterFlame.wav` も −30.3 で並んでいた）に揃えてある。ジッポだけは当たりを潰してから頂点 −1dB まで上げた（−17.1 LUFS。2026-09-29。6dB 上げただけではまだ小さかった）。
場面 1 の物音の二つ（`ChairRise`・`PaperTurn`）は、同じ測り方で −26 LUFS（箱の音より 4.6dB 上。紙は頂点 −3dB の天井で止まって −26.8）。
ジッポは鋭い金属音で頂点が高く出るので、頂点で揃えると一段小さく聞こえる。`Blow.wav`（吐く息）は −20.1 LUFS で一段大きい。
足音だけは頂点ではなく一歩あたりの大きさ（−28.5 LUFS）で揃えてある（組と組を替えても同じ大きさに聞こえるように）。

## 差し替えた経緯

はじめは呼吸の素材を引き伸ばして合成したが、元が息切れの録音で吸っているように聞こえなかった。
次に「Cigarette inhale」、次に火の粉が鳴る録音を試したが、いずれもこちらでは音を聴けないため決められず、
最終的にオーナーが `flint` `crackle` `blow` を用意した。いまは `crackle`（`Drag.wav`）と `blow`（`Blow.wav`）を使っている。
`flint`（`LighterFlame.wav`）と OpenGameArt の金属音（`LighterClick.wav`）は、2026-09-28 にオーナーが用意したジッポの開閉の音（`Zippo.wav`）と火が移る音（`CigaretteLit.wav`）に置き替えて外した。
