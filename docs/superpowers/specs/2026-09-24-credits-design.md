# 終わりのクレジット 設計メモ

- 作成日: 2026-09-24
- 位置づけ: 載せる物と、載せなければならない表示の整理。文面と見せ方はオーナーが決める
- 前提: ゲーム設計 7 節（終わりのクレジットの置き場は書き出しの段までに決める）、シナリオ設計 9 節（素材の出典と許諾の置き場が要る）、BGM のメモ（`2026-09-24-music-design.md`。エンディングは「HALF AWARE」）
- 出典の元の一覧: `unity/Assets/Models/LICENSES.md`、`unity/Assets/Fonts/LICENSES.md`、`unity/Assets/Audio/LICENSES.md`

## 1. 流れ

- 場面 10 の最後の独白 → 暗転 → タイトル → **終わりのクレジット**。エンディングの「HALF AWARE」（3:35）に乗せて流す
- 冒頭の 3 枚のカード（煙草の間の「制作 : yoko」「HALF AWARE／かたあはれ」「2166 年 8 月 15 日 18 時 35 分 倫敦」）と書体を揃える。見出しはしっぽり明朝、名前と許諾は Noto Sans JP
- 流し方（巻物のように流すか、カードを切り替えるか）と速さは、曲の長さに合わせて実画面で決める

## 2. 載せる物（案）

| 区分 | 載せる物 | 許諾 | 表示の義務 |
|---|---|---|---|
| 題 | HALF AWARE／かたあはれ | | |
| 制作 | yoko | | |
| 原作 | 『かたあはれ』（yoko） | | 冒頭では出さない（シナリオ設計 4.1）。終わりで出すかはオーナーが決める |
| 音楽 | 「HALF AWARE」ほか（Suno の Pro プランで生成） | Pro プランの商用利用の権利 | 無し |
| 3D モデル | Microsoft Rocketbox Avatar Library（© 2020 Microsoft） | MIT | **有り**（著作権の表示と許諾の文を添える） |
| 3D モデル | Quaternius Ultimate Modular Women / Men | CC0（同梱の License.txt） | 無し |
| 3D モデル | Kenney Furniture Kit | CC0 | 無し |
| 書体 | しっぽり明朝（Fontworks ほか） | SIL Open Font License 1.1 | **有り**（書体を同梱して配るので、著作権の表示と許諾の文を添える） |
| 書体 | Noto Sans JP（Google） | SIL Open Font License 1.1 | **有り**（同上） |
| 効果音 | Kenney RPG Audio | CC0 | 無し |
| 効果音 | OpenGameArt「Zippo click sound」（dawith） | CC0 | 無し |
| 効果音 | OpenGameArt「Sound Effects Pack」（OwlishMedia） | CC0 | 無し |
| 効果音 | Pixabay の音（雨・扉・車・ジャックを挿す音・煙草の 3 つ） | Pixabay Content License | 無し（書けば喜ばれる） |
| 道具 | Unity | | 無し（Unity 6 は起動時のロゴも出さなくてよい） |

- 表示の義務が無い物も、どこから来たかを示すために載せるのを基本にする（CC0 の素材の作者への礼）
- 実際に書き出しに入っている物だけを載せる。Rocketbox は、候補として入れた人（女大 03・02・11・10 など）を含めてライブラリ一つとして載せればよい

## 3. 許諾の文の置き場

- **MIT（Rocketbox）と OFL（二つの書体）は、許諾の文そのものを配る物に添える義務がある。** 流れるクレジットに全文を出すのは長すぎるので、次の二つに置く
  - 書き出した zip に `THIRD_PARTY_NOTICES.txt` を同梱する（MIT の全文、OFL の全文と各書体の著作権の表示）
  - itch.io のページの説明にも同じ内容か、その在りかを書く
- 流れるクレジットには短い表記（「Microsoft Rocketbox Avatar Library — MIT License」など）だけを出し、最後に「許諾の全文は THIRD_PARTY_NOTICES.txt に」と一行添える

## 4. 決めること・やり残し

- 載せる名前の表記（「yoko」でよいか、原作の表記を入れるか）
- 音楽の表記（「Music: yoko」「Music generated with Suno」など、どう書くか）
- 開発に使った道具（AI の道具を含む）を載せるか
- 煙草の 3 つの音（`Drag`・`Blow`・`LighterFlame`）は、Pixabay のどの作品か辿れていない。表示の義務は無いが、載せるなら作品名と作者を探す（`Audio/LICENSES.md` の注記）
- `Models/LICENSES.md` の Rocketbox の節は、主人公と片割れの今の組み合わせ（主人公は女大 14 の頭とスポーツ 02 の体、片割れは作ったワンピース）に合わせて書き直す
- クリアモード（BGM のメモ 3 節）にも、クレジットを読み返せる物を置くか
