# 終わりのクレジットの短い表記（案）

- 流れるクレジットに出す、素材の短い表記。1 行が 1 枚のカードか 1 行ぶん
- ライセンスの全文と著作権の表示は `THIRD_PARTY_NOTICES.txt`（同じフォルダ。書き出しの段で zip へ写す）
- 題・制作・原作・音楽の書き方はオーナーが決める（`docs/superpowers/specs/2026-09-24-credits-design.md` の 4 節）。ここには素材の行だけを並べる

| 区分 | 日本語 | English |
|---|---|---|
| 3D モデル | Microsoft Rocketbox Avatar Library（© 2020 Microsoft）— MIT ライセンス | Microsoft Rocketbox Avatar Library (© 2020 Microsoft) — MIT License |
| 3D モデル | Quaternius「Ultimate Modular Women / Men」— CC0 | Quaternius "Ultimate Modular Women / Men" — CC0 |
| 3D モデル | Kenney「Furniture Kit」— CC0 | Kenney "Furniture Kit" — CC0 |
| 書体 | しっぽり明朝（© 2021 The Shippori Mincho Project Authors）— SIL Open Font License 1.1 | Shippori Mincho (© 2021 The Shippori Mincho Project Authors) — SIL Open Font License 1.1 |
| 書体 | Noto Sans JP（© 2014-2021 Adobe）— SIL Open Font License 1.1 | Noto Sans JP (© 2014-2021 Adobe) — SIL Open Font License 1.1 |
| 効果音 | Kenney「RPG Audio」— CC0 | Kenney "RPG Audio" — CC0 |
| 効果音 | dawith「Zippo click sound」（OpenGameArt）— CC0 | dawith "Zippo click sound" (OpenGameArt) — CC0 |
| 効果音 | OwlishMedia「Sound Effects Pack」（OpenGameArt）— CC0 | OwlishMedia "Sound Effects Pack" (OpenGameArt) — CC0 |
| 効果音 | Pixabay（dragon-studio、tommylynn、freesound_community ほか）— Pixabay Content License | Pixabay (dragon-studio, tommylynn, freesound_community and others) — Pixabay Content License |
| 音楽 | 音楽は Suno で生成（Pro プラン） | Music generated with Suno (Pro plan) |
| 道具（任意） | Unity で制作 | Made with Unity |
| 結び | ライセンスの全文は同梱の THIRD_PARTY_NOTICES.txt に | Full license texts are in THIRD_PARTY_NOTICES.txt |

## 注意

- 素材の行は Noto Sans JP で出す（設計メモ 1 節）。Noto の SDF は字を足しながら使う設定なので、`©` や `—` もそのまま出る。しっぽり明朝の SDF は焼いた字だけなので、見出しに記号を使うなら `HalfAware/Bake the font` で焼き直す
- Noto Sans JP の著作権の表示は、notofonts/noto-cjk の `Sans/LICENSE` には書かれていない（ライセンスの本文だけ）。`THIRD_PARTY_NOTICES.txt` には、google/fonts の `ofl/notosansjp/OFL.txt` の行（`Copyright 2014-2021 Adobe (http://www.adobe.com/), with Reserved Font Name 'Source'`）を写した。同梱の `NotoSansJP-Regular.otf` の中の表示は `© 2014-2021 Adobe (http://www.adobe.com/).`

## 確かめること

- `unity/Assets/TextMesh Pro/Resources/` にある物は、場面が使っていなくても書き出しに入る。`LiberationSans SDF.asset`（Liberation Sans、SIL Open Font License 1.1）と、TMP の既定のスプライトアセット `EmojiOne.asset`（EmojiOne の絵。同梱の `EmojiOne Attribution.txt` は EmojiOne のサイトのライセンスを見るように、とだけ書いてある）。書き出しから外すか、`THIRD_PARTY_NOTICES.txt` に足すかを決める
