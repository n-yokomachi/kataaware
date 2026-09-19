# フォントの出典

| ファイル | 出典 | 許諾 |
|---|---|---|
| `ShipporiMincho-Regular.ttf` | しっぽり明朝（Google Fonts / ofl/shipporimincho、作者 Fontworks ほか） | SIL Open Font License 1.1 |
| `NotoSansJP-Regular.otf` | Noto Sans JP（notofonts/noto-cjk、Sans/SubsetOTF/JP） | SIL Open Font License 1.1 |

どちらも OFL なので、書き出したものに同梱して配れる。

## 使うところ

画面の文字はすべて `ShipporiMincho-Regular SDF`。見出し（暗転のカード・場面の見出し）、
字幕、ログ、調べる印まで通して同じ書体で組む。Noto Sans JP はもう場面からは参照していない。

角ゴシックから明朝へ替えたのは、夜と雨の話に字面を寄せるため。
`倫敦` `かたあはれ` のような字が締まる。
上端・行送りを点の大きさで割った比はどちらの書体も 1.160 / 1.448 で同じなので、
字幕の帯の高さや行送りの設定はそのままでよい。

## 絞り込み

元の `ShipporiMincho.ttf` は 15,363 字・8.28 MB ある。書き出しに載せるには重いので、
使う字だけに絞ってある。**台詞を足したら絞り直すこと。**

絞る字は 3 つを合わせたもの。

1. 仮名・ASCII・約物はまるごと（U+0020–007E, U+3000–30FF, U+FF00–FF65 ほか）
2. 学年か JLPT の指定がある漢字 2,998 字
3. 場面・コード・原作・仕様書に出てくる字

`fonttools` で絞る。

```
pyftsubset ShipporiMincho.ttf --text-file=keep.txt \
  --output-file=ShipporiMincho-Regular.ttf \
  --layout-features=kern,liga,palt,vert,vrt2 \
  --drop-tables+=DSIG --no-hinting --desubroutinize
```

結果は 3,612 字・1.62 MB（元の 20%）。
両方の場面に出る 116 字はすべて入っていることを確かめてある。

## 図（アトラス）の作り

TextMeshPro は .ttf から直に字を描かない。字の形を並べた**絵**を 1 枚持っていて、
画面にはそこから切り出して貼る。だから使う字は先に絵へ入れておく必要がある。
これを「焼く」と呼んでいる。

`ShipporiMincho-Regular SDF.asset` は 1024×1024 を 2 枚、焼く大きさ 52・余白 5。
実行中には足さない（Static）作りにしてある。

**台詞を足したら `HalfAware/Bake the font` を走らせること。**
焼いていない字は予備の Noto Sans JP へ落ちて、明朝の中にゴシックが混じる。

図を 2048 にすると焼けない（Unity 側の都合）。1024 を複数枚で使う。

## 予備の書体

`NotoSansJP-Regular SDF` を予備に置いてある。しっぽり明朝が持っていない字
（罫線 U+2500 など）や、焼き忘れた字はここへ落ちる。落ちれば見た目で分かるので、
気づいたら焼き直す。
