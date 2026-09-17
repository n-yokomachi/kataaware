# 音の出典

すべて CC0 1.0（パブリックドメインの献呈）。表示の義務は無いが、どこから来た物かを残すために記録する。

| ファイル | 出典 | 許諾 | 加工 |
|---|---|---|---|
| `Step1`〜`Step5.wav` | Kenney RPG Audio（https://kenney.nl/assets/rpg-audio）の `footstep00/02/04/06/08.ogg` | CC0 1.0 | モノラル 44.1kHz へ、末尾を落として頂点を −6dB に揃えた |
| `LighterClick.wav` | OpenGameArt「Zippo click sound」（https://opengameart.org/content/zippo-click-sound）作者 dawith | CC0 1.0 | 金属音の当たりだけを 0.060〜0.320 秒で切り出し、頂点 −3dB |
| `LighterFlame.wav` | OpenGameArt「Catching fire」（https://opengameart.org/content/catching-fire）作者 themightyglider | CC0 1.0 | 全体を使い、頭と尻に短い出入りを付けて頂点 −5dB |
| `Drag.wav` | OpenGameArt「Sound Effects Pack」（https://opengameart.org/content/sound-effects-pack）作者 OwlishMedia の `Human/breath-female2.wav` | CC0 1.0 | 0.980〜1.560 秒の一息を 1/0.62 倍に伸ばし、220Hz 以下を落として頂点 −8dB |
| `Blow.wav` | 同上 | CC0 1.0 | 1.803〜2.385 秒の一息を 2 倍に伸ばし、3.2kHz 以上を落として頂点 −8dB |

切り出しの手順は ffmpeg で、`docs/` ではなくここに残す。素材そのものは repo に置かず、加工後の物だけを置いている。

## 使うところ

- 足音は `Footsteps`。進んだ距離を積んで 0.88 メートルごとに 1 つ鳴らす。直前と同じ物は選ばず、音量と高さを少し振る
- ライターと息は `Cigarette`。`SmokeBeats` の時刻表に沿って、蓋 → 火 → 吸う → 吐く、の順に鳴らす
- 吐く息の頭で `SmokePuffs.Blow()` を呼び、煙をひと息ぶん足す。音と煙は同じ時刻表から出るのでずれない

## 使わなかった物

`Sound Effects Pack` には `Technology/plugpull.wav`（差込を抜く音）もある。手首のジャックを抜く場面に合うが、今は入れていない。
