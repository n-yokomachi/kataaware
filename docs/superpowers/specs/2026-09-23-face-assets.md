# 主人公と片割れの顔と体に使える外部の素材

- 作成日: 2026-09-23
- 対象: 顔の検証（`2026-09-23-face-study.md`）の後のオーナーの言葉「どの段階も美しくは見えないな。別のアセットやテクスチャを検討して」
- 前提: シナリオ設計 1 節の顔の決まり（若い女性、黒い髪、両目が同じ琥珀色、ほくろは主人公が左目の下・片割れが右目の下）。顔が細かく見えるのは対面の 0.4 m だけで、そこで顔の幅は 57〜60 画素（顔の検証 4 節）。PS1 の頃の美しい顔は、少ないポリゴンの頭に写真か手描きのよく出来たテクスチャを貼って作られていた
- 調べ方: **何も落としていない・取り込んでいない・アカウントを作っていない。** 頁と見本の画像をブラウザで開いて見ただけ。ライセンスは素材の頁かライセンスの文面を開いて確かめ、読んだ所を添えた。読めなかった所は「未確認」と書いた。itch.io の一覧の頁と 3D.SK の頁ではボットの確認（Cloudflare）が出たので先へ進まず、個別の頁を別の手段で読むか、未確認とした
- ゲームの内側の Unity のエディタには触れていない

## 1. 勧め

1. **Microsoft Rocketbox の若い女性（`Female_Adult_05`。次に `03`・`08`）**
   - 顔は写真から作った頭だけの 2048 のテクスチャで、今回見た候補の中で一番整って見えた。少ないポリゴン（lowpoly 2,500 三角）に写真のテクスチャ、という PS1 の頃の美しい顔の作り方にそのまま落とせる
   - MIT ライセンス。ゲームに同梱でき、公開するリポジトリにも置ける。条件はライセンスの文を添えることだけ
   - 骨がある。Humanoid にする手順が公式のリポジトリにある。顔の骨もあり、瞬きなどを後で付けられる
   - 顔は実在の人の写真を部分ごとに混ぜて大きく変えた「実在しない人」と、公式の論文に書かれている。肖像の問題が小さい
2. **Vinrax の PSX Female Secretary Character**
   - PS1 そのものの作り（932 三角・256 のテクスチャ）で、手描きの顔が整っている。無料・126 kB・Mecanim 対応で、試すのが一番軽い
   - 「PS1 の手描きの顔」という方向がオーナーの目に合うかを、半日で確かめられる
   - 表示の義務がある。素材の再配布は書かれていない（未確認）ので、公開するリポジトリには置かない
3. **MakeHuman（Blender の MPFB2）**
   - 中核のアセットは CC0。顔の形・年齢・肌を自分で決められる唯一の候補で、書き出した物はリポジトリに置ける
   - 肌は写真から作った CC0 のテクスチャが揃っている
   - 手間は一番かかる（Blender での作業、ポリゴンを減らしてテクスチャを縮める）。1 と 2 の顔が気に入らなかったときの本命

次点は **FaceGen Modeller**（有料、Core $300）。写真 1 枚か無作為で頭とテクスチャを作る、PS1〜PS2 の頃の作り方そのものの道具。頭だけで体は別に要る。無料の体験版（顔にロゴが入る）で見た目を先に確かめられる。

AI で顔のテクスチャを作る道は、設計（片割れの顔はフリー素材ベース）から外れる（4 節）。採るなら、勧め 1 か 3 の頭に AI で描いた顔を貼る形になる。

## 2. 候補の表

| # | 候補 | 種類 | ライセンス | ゲームに同梱 | 表示の義務 | 公開するリポジトリに置く | 骨 | 三角 / テクスチャ | PS1 との相性 | 見た目 |
|---|---|---|---|---|---|---|---|---|---|---|
| 1 | Microsoft Rocketbox `Female_Adult_*` | 体ごと | MIT | 可 | ライセンスの文を添える | 可 | 3ds Max の Biped（体 56・顔 28 本）。Humanoid にできる | 10k / 5k / 2.5k / 500 の 4 段。頭 2048 | ◎（落とせば） | 整っている。今回の中で一番 |
| 2 | Vinrax PSX Female Secretary | 体ごと | 作者の独自（"just credit me"） | 可 | 要 | 未確認（書かれていない）→ 置かない | 簡単な Humanoid。Mecanim 可 | 932 / 256 | ◎ | 手描きで整う。表情がきつい |
| 3 | McPato Agent PS1/PS2 Character | 体ごと | CC BY 4.0 | 可 | 要 | 可（出典を添える） | あり（アニメーション 1）。種類は未確認 | 12,650 面 / 6 枚（解像度は未確認） | ○（PS2 寄り） | 雰囲気はある。目元の化粧が強い |
| 4 | murilo.kleine PSX Low Poly Woman | 体ごと | CC BY 4.0 | 可 | 要 | 可（出典を添える） | あり（アニメーション 1）。種類は未確認 | 894 面 / 128 | ◎ | 写真風の顔。128 は 0.4 m でぼける |
| 5 | Quaternius Universal Base Characters | 体ごと | CC0（パックの頁）と QAL（サイトのライセンスの頁） | 可 | 不要 | 生の素材は置かない | Humanoid | 平均 13k / 未確認 | △ | 整うが今風のデフォルメ。無料の女性は筋肉質 |
| 6 | Mixamo の人物 | 体ごと | Adobe の条件 | 可 | 不要 | 不可（生のファイルを配れない） | 未確認 | 未確認 | 未確認 | 未確認（ログインしないと見られない） |
| 7 | Blender Studio Human Base Meshes | 頭（と体）の形だけ | CC0 | 可 | 不要 | 可 | 無し | 未確認 / テクスチャ無し | ○（テクスチャ次第） | 形は写実。顔の絵が無い |
| 8 | Sketchfab の写真スキャンの頭（例: 3130 "portrait of model 2.0"） | 頭と顔のテクスチャ | CC BY 4.0 | 可 | 要 | 可（出典を添える） | 無し | 約 100 万面 / 1 枚 | △（作り直しが要る） | 一番美しい（実写）。**実在の人** |
| 9 | MakeHuman（MPFB2） | 作る道具 | 中核のアセットは CC0 | 可 | 不要 | 可 | Game engine ほか | 基本の体は四角形 14,766 面。軽い proxy あり / 肌は未確認 | ○ | 写実。平均顔に寄りやすい |
| 10 | FaceGen Modeller | 作る道具（頭だけ） | 製品のライセンス契約（有料） | 可 | 書かれていない | 配布は許される。所有は Enterprise だけ | 無し（表情の morph 110 以上） | 未確認 | ◎ | 2000 年代の CG の顔。写真次第 |
| AI | 無料の頭 + AI で描いた顔のテクスチャ | 設計から外れる | 使う生成器による | — | — | — | 頭による | 頭による | 作り次第 | 作り次第 |

## 3. 候補ごとの詳しい所

### 3.1 Microsoft Rocketbox（体ごと）

- 頁: https://github.com/microsoft/Microsoft-Rocketbox
- 画像:
  - 見本の寄り: https://raw.githubusercontent.com/microsoft/Microsoft-Rocketbox/master/Docs/AvatarsSample.jpg
  - 人ごとの全身の見本: https://raw.githubusercontent.com/microsoft/Microsoft-Rocketbox/master/Assets/Avatars/Adults/Female_Adult_05/Female_Adult_05.png （`05` を `01`〜`17` に替えると女性の大人 17 人を見られる）
  - 顔の寄り（別のリポジトリ Headbox の説明の画像）: https://raw.githubusercontent.com/openVRlab/Headbox/main/Documentation/F96C911C-D2D8-49B1-BCB8-45C582CFD945.jpeg
- ライセンス
  - MIT（`LICENSE.md`、"Copyright (c) 2020 Microsoft"）。README に "November 2020 License Update: The library of avatars is now released under MIT License."
  - ゲームに同梱: 可。MIT は使用・改変・配布・販売を許す
  - 表示の義務: MIT の条件 "The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software." ゲームの配布物とリポジトリにライセンスの文を入れる。画面のクレジットは義務ではない
  - 再配布・リポジトリ: 可（ライセンスの文を添える）
  - 年齢・使い道の制限: 書かれていない。論文（Frontiers in Virtual Reality 2020、https://www.frontiersin.org/articles/10.3389/frvir.2020.561558/full ）には "free for research and academic use" とあるが、README の UPDATES によれば 2020-12 に MIT へ変わった。研究に使うなら論文を挙げるよう勧める、とだけ README にある
  - 顔の出どころ: 論文に、スタジオで撮った実在の人の写真を使い、部分を混ぜて大きく変えたので "generic humans that do not exist in reality" とある
- 手に入れ方: アカウントもアプリも要らない。GitHub から直に落とせる。リポジトリ全体は約 4.1 GB（GitHub の API の size）なので、使う人のフォルダだけを落とす。1 人分は FBX 2 つ（`Female_Adult_05.fbx` 0.6 MB、表情つきの `_facial.fbx` 2.2 MB）と TGA 8 枚（2048、1 枚 12〜16 MB、計 約 100 MB）。最後の更新は 2022-10
- 形式・数
  - FBX（3ds Max から書き出し）と TGA。テクスチャは体と頭で別（`f005_head_color` など色・法線・スペキュラ、髪などの `opacity`）。どれも 2048（ファイルの大きさから）
  - 一つの FBX に 4 段のメッシュ: hipoly 10,000、midpoly 5,000、lowpoly 2,500、ultralowpoly 500 三角（論文）
  - 骨: 3ds Max の Biped（`Bip01`）。体 56 本・顔 28 本（論文）。表情つきの FBX には口の形 15・FACS 48 などのブレンドシェイプ（README）
  - UV: あり（テクスチャで作られているので）
- **取り込みの注意**: 同梱の `Assets/Editor/FixRocketboxMaxImport.cs` はパスで絞らない AssetPostprocessor で、プロジェクトのすべての取り込みに掛かる。すべてのマテリアルの色を白にし、名前に "poly" を含む物を hipoly 以外は隠し、`Bip01` を持たない模型では `g.transform.Find("Bip01").Find(...)` が null で例外になる（Quaternius の人を取り込み直すたびに出る）。入れるなら Rocketbox のフォルダだけに掛かるよう書き換える。`animationType` は Generic に固定されていて、Humanoid が要るならそこを変えるとコメントにある。マテリアルの直しは Built-in の `_Mode` 向けで、URP には合わない
- 決まりへの合わせ方
  - 黒い髪: `03`・`05` は黒、`08` は黒に近い茶、`12` は暗い茶（見本の頭の寄りで見た）。そのまま使える
  - 琥珀の目: 虹彩が頭のテクスチャの中か別の目の玉かは未確認。どちらでも色相を回すだけで 30 分ほど
  - ほくろ: 頭のテクスチャに点を描く。数分
  - 二人: ほくろの位置だけ違う頭のテクスチャを 2 枚にすれば、同じメッシュで作れる。写真のテクスチャは左右が非対称で、UV も左右対称とは限らない（未確認）ので、テクスチャの左右反転では鏡像にならない
- PS1 との相性（見立て）: ◎。lowpoly か ultralowpoly のメッシュと、頭を 256 に縮めた色のテクスチャだけ（法線とスペキュラは使わない）にすれば、写真を貼った低いポリゴンの頭そのもの。2048 の頭のうち顔が 3 分の 1 ほどなら、256 に縮めても顔は 80 画素ほどあり、0.4 m の顔幅 60 画素を上回る（推定）。髪は α の板なので、切り抜き（α テスト）にして 320×180 でちらつかないかを見る
- 見た目（見立て）: 写真の肌で、20〜30 代の整った顔。2010 年前後のゲームの一般の人の顔で、華やかさはおとなしい。`05` が一番整って見えた。周りの Quaternius の人の中では二人だけ写実になり、浮く（顔の検証 9 節 3 の懸念）

### 3.2 Vinrax の PSX Female Secretary Character（体ごと）

- 頁: https://vinrax.itch.io/psx-secretary-character
- 画像: https://img.itch.zone/aW1hZ2UvMjQzMTMzNC8xNDM5NDc3Ny5wbmc=/original/xXI7AZ.png 、 https://img.itch.zone/aW1hZ2UvMjQzMTMzNC8xNDM5NDc3OC5wbmc=/original/4SvVrp.png
- ライセンス（頁の説明）
  - "feel free to use in your projects, just credit me"
  - ゲームに同梱: 可
  - 表示の義務: 要（クレジットに作者の名前）
  - 再配布・リポジトリ: 書かれていない。未確認。公開するリポジトリには置かない（今の Quaternius の生の FBX と同じ扱い）
  - 年齢・使い道の制限: 書かれていない。頁に "No generative AI was used" のタグ
- 手に入れ方: itch.io で無料。`female_secretary.zip` 126 kB。ログインの要否は未確認（この頁の落とす流れは開いていない）
- 形式・数: FBX。932 三角・476 頂点。テクスチャ 256。骨は頁に "Fully rigged with simple humanoid rig, ready to use inside unity with mecanim"。骨の数は未確認
- 決まりへの合わせ方: 髪は金髪で、テクスチャの髪の所を黒に塗る（30 分）。目は緑がかった茶に見えるので琥珀へ（数分）。ほくろを描く（数分）。口や鼻はテクスチャの中にあるので、顔の検証で困った「描いた口が模型の鼻に載る」問題は起きない。二人はテクスチャを 2 枚
- PS1 との相性（見立て）: ◎。PS1 の作りそのもの。256 の中で顔は 60〜70 画素ほど（見本の見た目からの推定）で、0.4 m の顔幅 60 画素とほぼ等倍
- 見た目（見立て）: 手描きで陰影まで描き込んだ整った顔。細い眉と締まった口で表情がきつく、顎がやや角ばる。PS1 の頃の実写寄りのホラーの女性の顔に近い。服は赤いシャツと黒いスカートの事務員。片割れの印象に合うかはオーナーの判断

### 3.3 McPato の Agent PS1/PS2 Character（体ごと）

- 頁: https://sketchfab.com/3d-models/agent-ps1ps2-character-mcpato-a732cdebefcb4d4d8c771ca82f2a2d97 （作者の itch.io: https://mcpato.itch.io/ ）
- 画像: https://media.sketchfab.com/models/a732cdebefcb4d4d8c771ca82f2a2d97/thumbnails/2c1b19d464664a319ead20bd0c3d759c/f15904bdc77646e78862174abec92363.jpeg
- ライセンス（Sketchfab の頁のライセンスの欄。API の値: "Creative Commons Attribution"、"Author must be credited. Commercial use is allowed."、https://creativecommons.org/licenses/by/4.0/ ）
  - ゲームに同梱: 可
  - 表示の義務: 要
  - 再配布・リポジトリ: 可（CC BY 4.0 は表示を条件に再配布を許す）。出典とライセンスを添える
  - 年齢・使い道の制限: Sketchfab の年齢制限の印は無い（`isAgeRestricted: false`）。ただしタグに "NSFW" がある。理由は未確認
- 手に入れ方: Sketchfab のアカウントでのログインが要る（Sketchfab の Download API の説明 https://sketchfab.com/developers/download-api/downloading-models ）。落とせるのは自動で変換した glTF 系と、作者が上げた元の形式（何かは未確認）。**このプロジェクトでは骨入りの glTF が glTFast で失敗する**（`unity/Assets/Models/LICENSES.md`）ので、元の形式か、Blender で FBX にし直した物を使う
- 形式・数: 12,650 面・6,599 頂点・テクスチャ 6 枚（解像度は未確認）・アニメーション 1。骨の種類は未確認
- 決まりへの合わせ方: 髪は赤く、テクスチャで黒へ（30 分）。目は青緑で琥珀へ。ほくろを描く。強い目元の化粧を和らげるなら描き直しで数時間
- PS1 との相性（見立て）: ○。PS2 の頃の作り。三角は PS1 には多いが WebGL では問題にならない
- 見た目（見立て）: 手描きの顔で、濃いアイラインと細い眉。PS2 の頃のホラーの雰囲気がある。整っているが、若さと柔らかさは薄く、きつい印象

### 3.4 murilo.kleine の PSX GRAPHICS | Low Poly Woman（体ごと）

- 頁: https://sketchfab.com/3d-models/psx-graphics-low-poly-woman-free-download-08dea1dcd280451da4a8e621588409a3
- 画像: https://media.sketchfab.com/models/08dea1dcd280451da4a8e621588409a3/thumbnails/e50548df333a4a8b95d6597e3cd9a744/7418a256e9034230b37ff33e6c786250.jpeg
- ライセンス: CC BY 4.0（Sketchfab の頁のライセンスの欄）。ゲームに同梱は可、表示は要、再配布とリポジトリは出典を添えて可。年齢制限の印は無い。**顔のテクスチャの元の写真がどこから来たかは書かれていない（未確認）。** 実在の人の写真から作った物なら、その写真の権利と肖像の扱いが分からない
- 手に入れ方: Sketchfab のログインが要る。形式の注意は 3.3 と同じ
- 形式・数: 894 面・459 頂点・テクスチャ 2 枚。作者の説明で "128x128 texture maps with no texture filtering"。アニメーション 1（骨の種類は未確認）。2019-12 の作
- 決まりへの合わせ方: 髪は茶のお団子で黒へ、目は青で琥珀へ、ほくろを描く。どれも 30 分ほど。ただし 128 の中の顔は 30〜40 画素ほど（推定）で、ほくろは 1 テクセルに満たない大きさになりうる
- PS1 との相性（見立て）: ◎。作者自身が PS1 の再現として作った
- 見た目（見立て）: 写真を縮めたような顔で、PS1 の実写寄りの顔の趣は一番強い。細面で整う。0.4 m では 128 が 60 画素へ伸ばされ、ぼける

### 3.5 Quaternius の Universal Base Characters（体ごと）

- 頁: https://quaternius.com/packs/universalbasecharacters.html 、 https://quaternius.itch.io/universal-base-characters
- 画像: https://quaternius.com/assets/images/fullres/universalbasecharacters.jpg 、無料版の中身 https://quaternius.com/assets/images/fullres/universalbasecharacters/standard.jpg
- ライセンス
  - パックの頁と itch.io の頁: CC0（"Free to use in personal, educational and commercial projects"）
  - サイトのライセンスの頁（https://quaternius.com/license.html ）: Quaternius Asset License (QAL) v1.0、最終更新 2026-08-28。表示は不要、完成した作品への同梱は可。素材そのものを単体の素材として再配布することを禁じる
  - 今の Quaternius と同じく二つの表記があり、`unity/Assets/Models/LICENSES.md` の方針どおり、生の FBX は公開するリポジトリに置かない
  - 年齢・使い道の制限: 書かれていない
- 手に入れ方: 無料版（Standard）は quaternius.com か itch.io から直に。Source 版は itch.io で $19.99 以上（サイトの画像では $20）
- 形式・数: 無料版は FBX・OBJ・glTF。平均 13k 三角。Humanoid の骨（Universal Animation Library と共通）。無料版は「基本の体 2 つと髪 5 つ」で、見本の画像ではヒーロー体型の女性と男性。Regular と Teen の体、肌と目の色のシェーダーは Source 版。テクスチャの有無と解像度は未確認
- 決まりへの合わせ方: 髪は黒く塗れる。目の色は Source 版のシェーダーで変えられる（無料版は未確認）。ほくろは、顔にテクスチャが無ければ顔の検証の段 2 のように面を張るか、デカールで
- PS1 との相性（見立て）: △。13k 三角の滑らかに彫った顔で、今風のデフォルメ
- 見た目（見立て）: 顔は整っているが、太い眉とがっしりした顎。無料版の女性は筋肉質のヒーロー体型で、若い女性らしい体は Source 版。今の群衆（Ultimate Modular Women）と同じ作者なので絵柄の系統は揃う

### 3.6 Mixamo の人物（体ごと）

- 頁: https://www.mixamo.com/
- 画像: 未確認（ログインしないと人物の一覧が見られない）
- ライセンス
  - Adobe の公式 FAQ（https://helpx.adobe.com/creative-cloud/faq/mixamo-faq.html 、2021-09-14 更新）: "You can use both characters and animations royalty free for personal, commercial, and non-profit projects including: ... Create video games." Adobe ID があれば無料。中国の国コードのアカウントと企業の ID では使えない
  - 再配布: Adobe のコミュニティの FAQ の投稿（https://community.adobe.com/t5/mixamo-discussions/mixamo-faq-licensing-royalties-ownership-eula-and-tos/td-p/13234775 ）に、作れない物として "Any type of free distribution of character or animation raw files"、表示は不要、機械学習の学習には使えない、とある。これはコミュニティの投稿で、Adobe の利用条件の本文は未確認
  - ゲームに同梱: 可。表示: 不要。公開するリポジトリ: 生のファイルは置けない
- 手に入れ方: Adobe ID（無料）でログインし、ブラウザで選んで書き出す
- 形式・数・骨・見た目・相性: 未確認

### 3.7 Blender Studio の Human Base Meshes（頭の形だけ）

- 頁: https://www.blender.org/download/demo-files/ （Asset Bundles の Human Base Meshes）、説明 https://developer.blender.org/docs/features/asset_system/asset_bundles/human_base_meshes/
- 画像: https://www.cgchannel.com/wp-content/uploads/2023/06/230629_HumanMeshesBundle_Free3DCharacterMeshes.jpg
- ライセンス: blender.org の demo files の頁で CC0。ゲームに同梱・リポジトリは可、表示は不要、制限は無い
- 手に入れ方: 直に落とせる（`human-base-meshes-bundle-v1.4.1.zip`、49 MB、2025-01-20 更新）。開くのに Blender 4.2 LTS 以降
- 形式・数: `.blend`（Blender から FBX・OBJ へ書き出す）。写実の女性の体、頭、顎、目の玉など 17 のメッシュ（CG Channel の記事）。四角形で、UV あり（UDIM 対応）、多重解像度。骨もテクスチャも無い。ポリゴンの数は未確認
- 決まりへの合わせ方: 顔はすべてテクスチャで描く必要があり、それが作業の本体になる。今の Quaternius の体に頭だけ載せ替えるなら、首の継ぎ目と骨への重みの作業も要る
- PS1 との相性（見立て）: ○。形は写実で整うが、見た目はテクスチャ次第
- 見た目（見立て）: 形だけ（見本は UV の島ごとに色を付けた物）なので判断できない。顔の絵の当てが無いと進まないので、AI（4 節）か手描きとの組み合わせになる

### 3.8 Sketchfab の写真スキャンの頭（頭と顔のテクスチャ）

- 頁: https://sketchfab.com/3d-models/portrait-of-model-20-6832a653f9c647aba01f77b5d774e9ba （同じ作者に "portrait of model"、"HEAD 3 scan"、"Lmdclub Nastya" など）
- 画像: https://media.sketchfab.com/models/6832a653f9c647aba01f77b5d774e9ba/thumbnails/6c88dc39e3a246c2a9527068af1fdad3/15a7c34b625245f49606e55da3b7f125.jpeg
- ライセンス: CC BY 4.0（Sketchfab の頁のライセンスの欄）。ゲームに同梱は可、表示は要、再配布は可。年齢制限の印は無い。**写っているのは実在の人で、本人の同意（モデルリリース）について頁に何も書かれていない（説明は検索語の羅列）。未確認。** CC BY は撮った人の著作権の許しで、写っている人の肖像の扱いまでは含まない。ゲームの主人公の顔として配るのは危うい
- 手に入れ方: Sketchfab のログインが要る
- 形式・数: 999,853 面・500,558 頂点・テクスチャ 1 枚（解像度は未確認）。骨なし。写真測量の生のメッシュなので UV は細切れのはず（未確認）
- 決まりへの合わせ方: 低いポリゴンの頭を作り直してテクスチャを焼き直す作業が 1〜2 日。髪は黒に近い茶、目は青（見本）
- PS1 との相性（見立て）: △。そのままでは使えない
- 見た目（見立て）: 今回見た中で一番美しい（実写だから）

### 3.9 MakeHuman（Blender の MPFB2。作る道具）

- 頁: https://static.makehumancommunity.org/mpfb.html 、 https://extensions.blender.org/add-ons/mpfb/
- 画像: https://extensions.blender.org/media/thumbnails/b9/b9b78555c93993fdbaf7929440b3d4007dd13160937ed657986b2f78c818a6d7_1920x1080.webp （MPFB の頁の見本。写真の肌の顔の寄り）
- ライセンス
  - https://static.makehumancommunity.org/about/license.html : "All core assets are shared under Creative Commons, CC0." 道具そのものは MakeHuman が AGPL、MPFB が GPL
  - FAQ（https://static.makehumancommunity.org/makehuman/faq/are_makehuman_files_free.html ）: "Use a character model in a closed source game without need for attribution"。第三者のアセットはそれぞれのライセンスに従う
  - アセットパック: system assets（267 MB）は頁の表で全部 CC0。skins01（99 MB）も 23 種すべて CC0（https://static.makehumancommunity.org/assets/assetpacks/skins01.html ）。他のパック（目のマテリアルの追加など）は未確認で、前回の検証では CC-BY の物が混ざると書いた
  - ゲームに同梱・リポジトリ: 可。表示: 不要。制限: 書かれていない
- 手に入れ方: 無料、アカウント不要。Blender 4.2 LTS 以降に、Blender の拡張から MPFB 2.0.17（2026-07-22）を入れる。アセットパックは別に落とす。作るのは Blender の中でつまみを動かす作業
- 形式・数: Blender から FBX。骨は複数から選べる（Game engine の骨が Mecanim と合うと公式にあることは前回の検証で確かめた）。基本の体は四角形 14,766 面で、軽い proxy がある（前回の検証）。肌は写真から作ったテクスチャ（skins01 に "onlytheghosts_young_eurasian_female"、"darthfurby_caucasian_female" など）。肌の解像度は未確認。目は別のメッシュで、色は blue・brown・brownlight・green など（system assets の頁）。髪は long01・bob01・bob02・ponytail01 など 10 種
- 決まりへの合わせ方: 髪は黒のマテリアルに。琥珀の目は brown か brownlight のテクスチャの色相を回す（30 分）。ほくろは肌のテクスチャの顔の所に描く。二人は肌のテクスチャを 2 枚
- PS1 との相性（見立て）: ○。そのままだと細かすぎるので、proxy で減らし、テクスチャを 256〜512 に縮める作業が要る
- 見た目（見立て）: 見本の顔は写真の肌で写実。ただし既定の顔は平均顔で個性が薄く、目の作りが人形めく。美しさは作り手のつまみの操作に掛かる

### 3.10 FaceGen Modeller（作る道具・有料）

- 頁: https://facegen.com/modeller.htm 、ライセンス https://facegen.com/modeller_legal.htm と https://facegen.com/fm_license.htm
- 画像: https://facegen.com/images/modeller_instant_3.jpg 、 https://facegen.com/images/modeller_photofit_front.jpg
- ライセンス
  - ライセンス契約 4.8 条（有料のライセンス）: "CUSTOMER may license, sell or distribute 3D meshes and related color maps derived from Models."
  - License & Legal の頁: "Exported 3D models may be distributed as you wish unless they retain color details (Modify → Texture) from a photo for which you do not have such rights. These models are not owned by you, but you own any modifications you make to them after exporting."
  - 版の比較の表: "Ownership of exported models for distribution in a product" は Enterprise（$2,900）だけ。配ることは Core でも許され、所有は Enterprise だけ、と読める
  - 体験版は評価だけ（3.1 条）で、作った物をゲームには使えない
  - ゲームに同梱: 可（有料の版）。表示の義務: 書かれていない。公開するリポジトリ: 4.8 条は配布を許すので置けると読めるが、権利は Singular Inversions に残る（5.1 条）
  - 制限: 写真から作るなら、その写真の権利が要る。輸出管理の条項（10 条）
- 手に入れ方: Windows。体験版は無料（顔にロゴ）。Core $300 でロゴが消え、追加の髪と Model Set が付く。Pro $900、Enterprise $2,900。支払いは FastSpring
- 形式・数: DAE・FBX・3DS・OBJ ほか。テクスチャは PNG・JPEG・TGA・BMP。頭だけ（体は無い）。表情の morph 110 以上（FACS を含む）。ポリゴンの数とテクスチャの解像度は未確認
- 決まりへの合わせ方: 無作為に作った顔か、オーナーが権利を持つ写真から作る。目の色とほくろはテクスチャで。体は Rocketbox か Quaternius に繋ぐ（首の継ぎ目の作業が要る）
- PS1 との相性（見立て）: ◎。写真から頭とテクスチャを起こすのは、PS1〜PS2 の頃の実写寄りの顔の作り方そのもの
- 見た目（見立て）: 公式の見本は 2000 年代の CG の顔で、髪はヘルメットのような形。写真の選び方次第で美しくも不気味にもなる

## 4. AI で作る道（設計から外れる）

**これは設計からの外れ。** ゲームデザイン設計 1 節は「素材は、舞台と小物は AI 生成を許容、片割れの顔はフリー素材ベースに特徴づけ」と決めている。頭の形がフリー素材でも、顔の見た目（誰に見えるか）を AI が決めるので、設計の外れになる。採るなら、オーナーがこの行を書き換える判断をする。

- 形: 勧め 1（Rocketbox）か 3（MakeHuman）、あるいは 3.7 の Blender の頭に、AI で描いた顔のテクスチャを貼る。手順の例は、正面の顔の絵を生成し、Blender の投影ペイントか FaceGen の写真合わせで頭の UV へ写し、256〜512 に縮める
- 良い点
  - 決まり（若い女性・黒い髪・琥珀の目・ほくろ）を最初から絵に入れられる。国籍を決めない曖昧な顔にもできる
  - 一枚の絵を二人で使い、ほくろの位置だけを替えれば、同じ顔を二人に保つのは簡単（二枚目を生成し直さない）
  - 実在の人の写真を使わないので、3.8 のような肖像の問題は小さい
  - PS1 の頃の「写真を貼った顔」の質感を狙って作れる
- 危うい点
  - 絵柄の揃い: AI の顔は肌が滑らかすぎて左右が揃いすぎ、「整いすぎた」顔になりやすい。PS1 の写真のテクスチャの粒や平たい光と合わせるには、縮めて減色してから手で直す作業が要る。周りの Quaternius の人とも浮く。小物は AI で作っているので、舞台との揃いは悪くない
  - UV に合った絵は直接は作りにくい。正面の絵を頭に写すと、耳・横顔・生え際に継ぎ目と伸びが出て、30 度の向きの寄りで粗が見える
  - ライセンス: 生成器ごとに利用規約が違う。Hunyuan3D で欧州連合・英国・韓国が外れていたように（アセット設計 4 節）、地域の制限を先に読む。生成物の著作権は国によって認められないことがある（米国著作権局の立場。今回は文面を開いていないので未確認）。その場合、他人が写しても止められない。学習元に似た実在の人の顔が出る危うさもゼロではない
  - 素材と混ぜるとき: Dysfunctional_Dev の素材（AI の生成の道具に使うことを禁じる）、BitSoft の素材（AI の学習を禁じる）、Mixamo（機械学習の学習を禁じる、コミュニティの FAQ）は AI への入力にしない。Rocketbox（MIT）・MakeHuman（CC0）・Blender の頭（CC0）なら入力にしてよい
  - itch.io の開示: 生成 AI の開示はアセットには必須、ゲームには任意（itch.io の告知 2024-11-20、https://itch.io/t/4309690/generative-ai-disclosure-tagging ）。小物で AI を使っているので、Graphics の開示をするなら顔が AI でも中身は変わらない
  - 同じ顔を二人に保つこと: 一枚で済ませれば問題ない。目を閉じた顔など表情違いを生成すると、顔が別人に変わりやすい

## 5. 外した物

| 物 | 外した理由 |
|---|---|
| MetaHuman | 2025-06 から Unity など他のエンジンでも使えるようになった（CG Channel の記事 https://www.cgchannel.com/2025/06/you-can-now-sell-metahumans-or-use-them-in-unity-or-godot/ 。Unreal Engine の EULA の下、年の売り上げ $1M 未満は無料、AI の学習には使えない）。ただし作るのは Unreal Engine 5.6 の中で、Epic のアカウントが要る。公式のライセンスの頁（https://www.metahuman.com/license ）はログインの後ろで未確認。模型とテクスチャが一番重く、PS1 へ落とす作業が一番大きい |
| Reallusion Character Creator | 有料で、素材をリポジトリに置けない（前回の検証 7 節） |
| 3D.SK の無料の頭のテクスチャ（Female 0007） | 写真から作った展開済みの頭のテクスチャ（3072×2048、22 歳の女性）で、作りとしては理想に近い。ただし ArtStation の頁（https://artist_reference_3dsk.artstation.com/store/1Vylb/free-female-high-res-head-texture-0007 ）のライセンスは "Hobby License $0 / License: Standard License" で、"Hobby" の中身は ArtStation の Marketplace の契約の本文に見当たらない。3D.SK の hobby の条件の頁（https://www.3d.sk/static/hobby-terms ）はボットの確認が出て読めなかった（未確認）。検索結果の抜粋では非商用で、無料の配布も禁じる向き。商用のライセンスは有料（値段は未確認）。ArtStation のログインも要る |
| Kenney の人の模型 | Animated Characters は丸い頭のデフォルメ、Blocky と Mini は箱型で、顔を見せる役に合わない（Animated Characters Protagonists の見本の画像で見た） |
| Dysfunctional_Dev の PSX Detective Pack | $7.50（セール、通常 $15）、2,329 三角、512 のテクスチャ。見本の画像では顔が小さく、顔の出来を判断できなかった。ライセンスは AI の生成の道具への使用を禁じる |
| BitSoft の Rigged Low-Poly Female NPCs | $4。頭のテクスチャが 64×64 で 0.4 m では粗い。変えた物も含め再配布を禁じる |
| ManNeko の PSOne Style – Female Character 01 | $2。頁の説明では 90 年代の玩具とアニメが下敷きの金髪の人物で、写実の顔ではない（画像は見ていない） |
| Humans of the World（Sketchfab の PS2 風の北欧の男女） | 説明が人種の型を分けるサイトに拠っていて、題材として避けた |
| VRoid Studio・Ready Player Me・MB-Lab | 前回の検証 7 節のとおり（アニメ調、サービス終了、作った模型に AGPL3） |
| Unity Asset Store の無料の物 | 写実の若い女性で目立つ物を、検索の範囲では見つけられなかった |

## 6. どの候補でも要ること

- 今の主人公の動き（`Idle.anim`・`Walk.anim`）は Quaternius の Generic の骨向けで、別の骨の模型へはそのまま載らない。Humanoid で取り直すか、動きを用意し直す（顔の検証 7 節）
- 撮り比べは顔の検証 7 節の仕組み（`FaceHeadSpec`・`FaceStudy.ShootSet`）で、段 2 と同じ表と絵を出せる。美しく見えるかの判断はオーナーがエディタと実機で行う
- 鏡像の二人は、ほくろの位置だけが違うテクスチャを 2 枚にするのが一番安全。メッシュごと x を −1 倍して鏡像にする手もあり、そうすれば顔の非対称まで鏡像になるが、骨入りのメッシュで裏表や影が崩れないかは Unity 6 で確かめていない（未確認）
- 出典は `unity/Assets/Models/LICENSES.md` に書き加える。再配布が禁じられているか書かれていない物（Vinrax・Mixamo・Quaternius の生の FBX）は、リポジトリを公開する前に外す
- テクスチャは大きいまま入れない。Rocketbox の TGA は 1 人分で約 100 MB あるので、取り込む前にリポジトリの外で縮めて PNG にする
- Sketchfab から落とす物は、骨入りの glTF が glTFast で失敗する（`LICENSES.md`）ので FBX で入れる

## 7. 試すのにオーナーがすること

### 勧め 1: Rocketbox

1. 見本の画像（3.1 の URL の番号を `01`〜`17` に替える）で人を選ぶ。黒い髪は `03`・`05`、黒に近いのは `08`・`12`
2. 親が GitHub から選んだ人のフォルダ（FBX 2 つと TGA 8 枚、約 100 MB）と `FixRocketboxMaxImport.cs`・`LICENSE.md` を落とすことを許す。アカウントもアプリも要らない
3. 親がすること: TGA を縮めて PNG にし、取り込みの仕組みを Rocketbox のフォルダだけに掛かるよう直してから取り込む。lowpoly のメッシュを使い、琥珀の目とほくろを描き、主人公と片割れの 2 枚を作って撮り比べる。目安は 1〜2 日（動きの載せ替えは別に 1 日前後）
4. オーナーは撮り比べの絵（0.4 m と 1 m）を見て決める

### 勧め 2: Vinrax の PSX Female Secretary

1. 親が itch.io の頁から `female_secretary.zip`（無料、126 kB）を落とすことを許す
2. 使うなら、クレジットに作者の名前（Vinrax）を出すことを決める
3. 親がすること: FBX を取り込み、テクスチャの髪を黒、目を琥珀にし、ほくろを描いて撮り比べる。目安は半日
4. 素材は公開するリポジトリに置かない前提で扱う

### 勧め 3: MakeHuman（MPFB2）

1. Blender 4.2 LTS 以降を入れる（入っていなければ）。Blender の拡張の画面から MPFB を入れる
2. アセットパックの makehuman_system_assets（267 MB）と skins01（99 MB）を落として MPFB に読ませる（親が落とすならその許しを出す）
3. MPFB で若い女性を作る。年齢と顔の形のつまみ、肌は skins01 の若い女性、髪は long01 か bob01、目は brown。骨は Game engine を選び、FBX で書き出す。導入と作りで 2〜3 時間
4. 親がすること: 取り込み、proxy で減らし、テクスチャを縮めて琥珀とほくろを描き、撮り比べる。目安は 1〜2 日

### 次点: FaceGen（買う前に）

1. Windows に体験版（無料）を入れ、写真を使わずに無作為の若い女性の顔を作って見る。体験版で作った物は評価にしか使えないので、ゲームには入れない
2. 気に入れば Core（$300）を買い、改めて作って書き出す。体は Rocketbox か Quaternius と繋ぐ
