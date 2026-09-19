# フォントの出典

| ファイル | 出典 | 許諾 |
|---|---|---|
| `ShipporiMincho-Regular.ttf` | しっぽり明朝（Google Fonts / ofl/shipporimincho、作者 Fontworks ほか） | SIL Open Font License 1.1 |
| `NotoSansJP-Regular.otf` | Noto Sans JP（notofonts/noto-cjk、Sans/SubsetOTF/JP） | SIL Open Font License 1.1 |

どちらも OFL なので、書き出したものに同梱して配れる。

## 使うところ

| 出るもの | 書体 |
|---|---|
| 暗転して真ん中に出すカード（制作／表題／日付と場所／続く） | しっぽり明朝 |
| 台詞・ログ・調べる印 | Noto Sans JP |

読ませる字はゴシックのほうが楽なので、明朝は暗転のカードだけに使う。
TextMeshPro の既定も Noto Sans JP。

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

TextMeshPro は .ttf から直に字を描かない。字の形を並べた**絵**を持っていて、
画面にはそこから切り出して貼る。だから使う字は先に絵へ入れておく必要がある。

`ShipporiMincho-Regular SDF.asset` は 1024×1024 を 2 枚、焼く大きさ 52・余白 5。
`HalfAware/Bake the font` で焼き直す。場面とコードから字を数え直し、
両方の場面へ当て直すところまでやる。

**暗転のカードの文を変えたら走らせること。** 絵に無い字は Noto へ落ちて、
明朝の中にゴシックが混じる。落ちれば見た目で分かる。

なお図を 2048 にしても入る字数は変わらない（Unity 側の都合）。1024 を複数枚で使う。

## 通りのネオンサインの絵

`Assets/Textures/Neon*.png` の字は Noto Sans JP の字形を描き写したもの。
`tools/make-neon.py` が字を管の輪郭に起こして、滲みを重ねて焼いている。

SIL Open Font License は描き出した絵の扱いを縛らないので、
この 12 枚はゲームの絵として自由に使える。OS 付属の書体は
再配布の扱いが面倒なので触っていない。

看板の文言を変えるときは `tools/make-neon.py` の `SIGNS` を直して走らせ直す。
