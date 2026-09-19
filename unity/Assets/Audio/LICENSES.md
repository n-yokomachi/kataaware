# 音の出典

ほとんどが CC0 1.0（パブリックドメインの献呈）。息の 2 つだけ Pixabay Content License。
どちらも表示の義務は無いが、どこから来た物かを残しておく。

| ファイル | 出典 | 許諾 | 加工 |
|---|---|---|---|
| `Step1`〜`Step5.wav` | Kenney RPG Audio（https://kenney.nl/assets/rpg-audio）の `footstep00/02/04/06/08.ogg` | CC0 1.0 | モノラル 44.1kHz へ、末尾を落として頂点を −6dB に揃えた |
| `LighterClick.wav` | OpenGameArt「Zippo click sound」（https://opengameart.org/content/zippo-click-sound）作者 dawith | CC0 1.0 | 金属音の当たりだけを 0.060〜0.320 秒で切り出し、頂点 −3dB |
| `LighterFlame.wav` | OpenGameArt「Catching fire」（https://opengameart.org/content/catching-fire）作者 themightyglider | CC0 1.0 | 全体を使い、頭と尻に短い出入りを付けて頂点 −5dB |
| `Drag.wav` | Pixabay「Cigarette inhale」（https://pixabay.com/sound-effects/film-special-effects-cigarette-inhale-84768/）原作者 kczub（Freesound） | Pixabay Content License | 頭と尻を落として頂点 −8dB。実際に煙草を吸っている録音 |
| `Blow.wav` | Pixabay「Smoking a cigarette」（https://pixabay.com/sound-effects/people-smoking-a-cigarette-6335/）原作者 ammorts（Freesound） | Pixabay Content License | 69.67〜71.90 秒の一息を切り出して頂点 −8dB |

切り出しの手順は ffmpeg で、`docs/` ではなくここに残す。素材そのものは repo に置かず、加工後の物だけを置いている。

## 使うところ

- 足音は `Footsteps`。進んだ距離を積んで 0.88 メートルごとに 1 つ鳴らす。直前と同じ物は選ばず、音量と高さを少し振る
- ライターと息は `Cigarette`。`SmokeBeats` の時刻表に沿って、蓋 → 火 → 吸う → 吐く、の順に鳴らす
- 吐く息の頭で `SmokePuffs.Blow()` を呼び、煙をひと息ぶん足す。音と煙は同じ時刻表から出るのでずれない

## 息の選び分け

吸う息と吐く息は、こちらでは音を聴けない。帯域の偏りで見分けた。素材の「Cigarette inhale」は
2.5kHz 以上が 700Hz 以下より 12.2dB 強く、吸う息は高域寄りだと判る。長い素材から切り出す
吐く息は、逆に低域寄り（高域が 19.1dB 弱い）で頭が強いものを選んだ。
**最終的な採否はオーナーが聴いて決める。** 候補は 3 つ切り出してある。

はじめは呼吸の素材を引き伸ばして作ったが、元が息切れの録音だったため、
吸っているのではなく息が上がっているように聞こえた。実際に煙草を吸っている録音へ差し替えてある。

## 使わなかった物

`Sound Effects Pack` には `Technology/plugpull.wav`（差込を抜く音）もある。手首のジャックを抜く場面に合うが、今は入れていない。
