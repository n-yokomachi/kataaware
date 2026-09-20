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
| `RainLoop.wav` | Pixabay `dragon-studio-copyright-free-rain-sounds-331497` | Pixabay Content License | 60 秒地点から 24 秒を切り出し、モノラル 22.05kHz へ。末尾 1.5 秒を頭に重ねて輪にし、実効値を −22dBFS に揃えた |
| `DoorShut.wav` | Pixabay `dragon-studio-open-and-close-door-405453` | Pixabay Content License | モノラル 44.1kHz へ、頭から 2.15 秒を切り出して末尾 0.1 秒を落とし、頂点を −3dB に揃えた。開けると閉めるが 1 つに入っている（開ける 〜0.5 秒、間 〜1.0 秒、閉まる 1.45〜1.65 秒） |
| `CarDoorOpen.wav` | Pixabay `dragon-studio-open-car-door-372469` | Pixabay Content License | モノラル 44.1kHz へ、頭の無音を落として末尾 60ms を落とした。0.92 秒 |
| `CarDoorShut.wav` | Pixabay `freesound_community-car-door-close-6929` | Pixabay Content License | モノラル 44.1kHz へ、頭の無音を落とした。0.55 秒 |
| `Ignition.wav` | Pixabay `freesound_community-keys-in-the-ignition-101951` | Pixabay Content License | モノラル 44.1kHz へ、頭の無音を落として 6.72 秒。そこへ**同じ録音の定常部から始まりの違う 6 つの切れ端**（6.00 / 4.62 / 5.48 / 6.24 / 4.94 / 5.74 秒から 0.78 秒ずつ）を 0.08 秒被せながら繋いで 3.83 秒足し、10.55 秒。**同じ切れ端を繰り返さない。** 末尾 0.5 秒を 8 回並べたときは金具の音がそのまま毎回鳴り、包絡線の自己相関が 0.5 秒と 1.0 秒で 1.00（完全な繰り返し）になって輪だと分かってしまった。いまは最大 0.37（0.26 秒＝エンジン自身の回転の刻み）で継ぎ目の周期は無い |
| `DriveSealed.wav` | Pixabay `freesound_community-car-driving-interior-perspective-51388`（オーナーが 22.3〜286.7 秒で切り出したもの） | Pixabay Content License | 60 秒地点から 21 秒。**6900Hz の音を抜いてある**（周りより 18dB 突き出ていた。Q 28 で −30dB、Q 60 で −24dB の二段）。+7dB、ステレオ 44.1kHz へ。頭 1.0 秒を尻へ被せて輪にした。20.00 秒 |
| `DriveGravel.wav` | Pixabay `freesound_community-driving-a-truck-in-gravel-58271` | Pixabay Content License | 20 秒地点から 12 秒。閾値 −26dB / 比 2.5 / 持ち上げ 3dB で圧縮し、0.80 で頭打ち。ステレオ 44.1kHz へ。頭 0.75 秒を尻へ被せて輪にした。11.25 秒 |
| `RainWipers.wav` | Pixabay `tommylynn-interior-car-in-rain-with-wipers-369260` | Pixabay Content License | 6 秒地点から 13.879 秒、+12dB。ステレオ 44.1kHz へ。頭 0.6 秒を尻へ被せて輪にした。**長さはワイパーの周期で決めてある**（包絡線の自己相関で 0.9485 秒。その 14 倍の 13.279 秒）。秒数で切ると払う拍が輪の継ぎ目で飛ぶ |
| `Idle.wav` | Pixabay `freesound_community-car-keys-in-ignition-starting-stopping-engine-31102` | Pixabay Content License | 28 秒地点から 8.6 秒。モノラル 44.1kHz へ。頭 0.6 秒を尻へ被せて輪にした。8.00 秒。イグニッションのあと走り出すまでの間を埋める |
| `WindowDown.wav` | Pixabay `freesound_community-car-window-down-103833` | Pixabay Content License | 1.85 秒地点から 4.45 秒、+20dB。モノラル 44.1kHz へ。素材は 8.94 秒あるが、窓が動いているのは 2〜6 秒だけ。素のままだと実効 −41.9dB で、持ち上げた走行の輪（−19dB）に埋もれて聞こえなかった |

切り出しの手順は ffmpeg で、`docs/` ではなくここに残す。素材そのものは repo に置かず、加工後の物だけを置いている。

煙草の 3 つは Pixabay のどの作品かまで辿れていない。**作品名と作者を控えておくこと。**
表記の義務は無いので公開の妨げにはならないが、差し替えや問い合わせのときに要る。

## 使うところ

- 足音は `Footsteps`。進んだ距離を積んで 0.88 メートルごとに 1 つ鳴らす。直前と同じ物は選ばず、音量と高さを少し振る
- 場面 2（小道）と場面 1（自室）は `Step1`〜`Step5`、場面 8（共用ガレージ）は `Concrete1`〜`Concrete4`。
  同じ足音を裸のコンクリートの上で鳴らすと床が土に聞こえるので、場面 8 だけ作り直してある。
  ガレージの反響は素材に焼かず、足元の `AudioReverbFilter` に持たせる

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
