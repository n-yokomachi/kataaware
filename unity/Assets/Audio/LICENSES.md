# 音の出典

足音とジッポとチップは CC0 1.0（パブリックドメインの献呈）。
煙草の 3 つ（吸う・吐く・火）はオーナーが用意したもので、**出典と許諾は未記入**。
公開までに埋めること。

| ファイル | 出典 | 許諾 | 加工 |
|---|---|---|---|
| `Step1`〜`Step5.wav` | Kenney RPG Audio（https://kenney.nl/assets/rpg-audio）の `footstep00/02/04/06/08.ogg` | CC0 1.0 | モノラル 44.1kHz へ、末尾を落として頂点を −6dB に揃えた |
| `LighterClick.wav` | OpenGameArt「Zippo click sound」（https://opengameart.org/content/zippo-click-sound）作者 dawith | CC0 1.0 | 金属音の当たりだけを 0.060〜0.320 秒で切り出し、頂点 −3dB |
| `Drag.wav` | オーナーが用意した `crackle` | — | 前後の無音を落として頂点 −8dB |
| `Blow.wav` | オーナーが用意した `blow` | — | 同上 |
| `LighterFlame.wav` | オーナーが用意した `flint` | — | 前後の無音を落として頂点 −5dB |
| `ChipPull.wav` | OpenGameArt「Sound Effects Pack」（https://opengameart.org/content/sound-effects-pack）作者 OwlishMedia の `Technology/plugpull.wav` | CC0 1.0 | 0.02〜0.40 秒、頂点 −6dB |
| `JackPull.wav` | 同上パックの `Technology/plugpull2.wav` | CC0 1.0 | 3.33 秒から 0.50 秒を切り出し、再生速度 0.85 倍で低く伸ばし、前後の無音を落として頂点 −6dB |
| `RainLoop.wav` | 自作。白色雑音を 2 段の 1 極濾波で丸め、粒の跳ねを重ねた | 本作の一部 | 16 秒。頭と尻を 1.2 秒重ねて輪にしてある |

切り出しの手順は ffmpeg で、`docs/` ではなくここに残す。素材そのものは repo に置かず、加工後の物だけを置いている。

## 使うところ

- 足音は `Footsteps`。進んだ距離を積んで 0.88 メートルごとに 1 つ鳴らす。直前と同じ物は選ばず、音量と高さを少し振る
- 雨は `RainLoop`。プレイヤーの頭上で輪にして流し、`RainCover` が屋根の下で音量を 35% まで絞る
- インプラントジャックを抜く音は `JackPull`。`PullTimeline` の引き抜く段の頭で 1 度だけ鳴らす。チップを抜く音と同じパックから採ったが、低く伸ばして生々しさを足してある
- ライターと息は `Cigarette`。`SmokeBeats` の時刻表に沿って、蓋 → 火 → 吸う → 吐く、の順に鳴らす
- 吐く息の頭で `SmokePuffs.Blow()` を呼び、煙をひと息ぶん足す。音と煙は同じ時刻表から出るのでずれない

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
