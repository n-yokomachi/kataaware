# Unity 移行 設計

- 作成日: 2026-09-16
- 対象: 制作環境を Three.js から Unity に移し、場面 1（自室）を Three.js 試作と同じ流れで Unity 上に作り直す
- 前提: `2026-09-15-game-design.md`（場面構成）、`2026-09-16-scenario-design.md`（場面 1 の本文と流れ）、`2026-09-16-assets-design.md`（見た目と素材の方針）はそのまま有効。`2026-09-15-foundation-design.md` は Three.js 版の記録として残し、本書がその役目を引き継ぐ
- 位置づけ: 実装時点の意思決定記録

## 1. 移行の理由と決定

- 残りの作業は配置・カメラ経路・照明・後処理の「見て置く・見て直す」が中心で、エディタで直接触れる利点が、Three.js で一人で実装と確認を通せる利点を上回った
- エディタの操作は MCP for Unity（CoplayDev/unity-mcp）経由で AI が行う。オーナーは実画面で見た目を判断し、必要ならエディタで直接動かす
- 版: Unity 6.3 LTS（6000.3.24f1）。描画は URP。書き出しは WebGL
- Three.js 試作は `prototype-three/` に保管し、設計と本文の参照元にする。コードは移植しない（言語も構造も変わるため）

## 2. プロジェクト構成

- Unity プロジェクトは `unity/`（同じリポジトリ）。Unity 用の `.gitignore`（`Library/`、`Temp/`、`Logs/`、`UserSettings/`、`Build/` など）を置く
- パッケージ: URP（テンプレート同梱）、Input System、Cinemachine 3、TextMeshPro（同梱）、MCP for Unity（`https://github.com/CoplayDev/unity-mcp.git?path=/MCPForUnity`）、glTFast（glTF の取り込み。Kenney や生成物が glTF のため）
- テスト: Unity Test Framework の EditMode テストで、描画に依存しない判定（進行、前提の判定、字幕の待ち行列）を確かめる
- スクリプトは `unity/Assets/Scripts/` に置き、責務ごとに分ける（下記 4 節）。シーンは `unity/Assets/Scenes/`、素材は `unity/Assets/Models/<系統名>/`、出典は `unity/Assets/Models/LICENSES.md`

## 3. 見た目の実現

- PS1 風: URP アセットの Render Scale を 1/3 前後にし、Upscaling Filter を Point にする。これで低解像度の描画と最近傍の拡大が設定だけで済む。減色とディザは Full Screen Pass Renderer Feature の自作シェーダで載せ、強さは Volume の数値で持つ。0 で素通し
- 眩暈: 同じ Full Screen Pass に、ぼかしと二重像を数値で持たせる。同じ絵を少しずらして 2 枚重ね、ずれの向きをゆっくり回す。ぼかしは 2 枚が溶け合わない程度に留める。漂いは後処理で絵をずらすのではなく、カメラそのものをゆっくり動かす。絵をずらしても画面が滑るだけだが、カメラを動かせば近くの物と遠くの物のずれ方が変わり、身体が揺れているように見えるため。強さは 2 つの数値（ぼかし、二重像）で持ち、時間で減衰する
- 試作（Three.js 版）は正弦波で UV を歪ませていたが、熱で空気が揺らぐように見えて酔いと結びつかなかったため、二重像に替えた。回転は入れない。ゲームデザイン設計書 8 節の酔い対策（頭の揺れ演出を入れない）に反し、遊ぶ側が実際に酔うため
- 色味: Volume の Color Adjustments（Color Filter）で場面ごとに寄せる
- 字幕、印、中央の文字、暗転、煙: uGUI + TextMeshPro の Canvas。黒帯・白文字、会話は話者と鉤括弧、独白はテキストのみ（シナリオ設計書 1 節）
- 主観の体: 首から下の主人公の体をプレイヤーに付け、カメラは目の位置に置く。頭は一人称のカメラに映さない。下を向けば、繋がった胴・腕・脚がそのまま見える（素材の設計書 5 節）。当初の「前腕とジャックをカメラの子にして、下を向いたときだけ表示する」作りはやめた

## 4. スクリプトの構成（Three.js 版からの対応）

| 役目 | Three.js 版 | Unity 版 |
|---|---|---|
| 歩く・見回す・座位と立位 | `walk.ts`, `input.ts` | `PlayerController`（CharacterController + Input System。`canMove`、目線の高さ、ポインタロック） |
| 調べる対象 | `interact.ts`, `types.ts` | `Interactable`（`id`、`required`、`after`、`hints`、`label`、`lines` を持つ MonoBehaviour） |
| 選択の規則 | `selectInteractable` | `InteractionPicker`（距離と視線の角度、前提の判定。純粋な C# クラスにして EditMode テスト） |
| 場面の進行 | `runtime.ts`, `scene-manager.ts` | `SceneFlow`（必須の完了で次へ、`freeze`、眩暈の保持と解放、立ち上がり）と `SceneDirector`（場面の切り替え、カットと暗転） |
| 字幕 | `subtitles.ts`, `overlay.ts` | `SubtitleQueue`（純粋な C#）と `HudView`（Canvas の表示） |
| 後処理 | `fx/*` | `DazeVolume`（数値の保持と減衰）と Full Screen Pass のシェーダ |
| 場面固有の演出 | `hooks/room-intro.ts` | `RoomIntroDirector`（クレジットとタイトル、煙草の自動演出。時刻はゲーム時間で判定） |
| 場面データ | `data/scenes/*.ts` | シーン内の Interactable と、文面は `ScriptableObject`（`RoomScript`）に持たせ、シナリオの文面をコードから分ける |

- 当たり判定は Unity の Collider に任せ、Three.js 版の外接箱の仕組みは持ち込まない
- 配置データ（`layout`）と `?layout` モードは、Unity のシーンとエディタがその役目を果たすため作らない

## 5. 進め方

1. 環境: Unity Hub と Unity 6.3 LTS（WebGL 付き）の導入、URP テンプレートでの `unity/` 作成、MCP for Unity の導入と接続確認、`.gitignore`
2. 骨組み: 空のシーンに床と壁の箱、`PlayerController`、`Interactable` と `InteractionPicker`、`SubtitleQueue` と `HudView`、`SceneFlow`。仮の箱の自室を歩いて調べて字幕を読める状態
3. 場面 1 の流れ: 座位で開始、ジャック、煙草と煙、立ち上がり、メモリハブ、端末、ドアの前提と文、暗転して「続く」。文面は `RoomScript` に入れる
4. 見た目: Render Scale と Point 拡大、減色とディザ、眩暈、色味。Kenney の物の取り込みと配置。固有物の生成は素材の設計書の段階 3 を引き継ぐ
5. WebGL 書き出しと itch.io での動作確認

段ごとに計画を書き、通しで遊べる状態を保ったまま進める。

## 6. 検証

- EditMode テスト: `InteractionPicker`、`SubtitleQueue`、`SceneFlow` の判定
- 動作: AI は MCP の `read_console` と `execute_code`（`ScreenCapture` でファイルに保存して読む）で確かめ、再生の操作は `manage_editor` で行う。見た目と操作感の最終判断はオーナーが行う
- 場面 1 の通し確認は各段の終わりに行う

## 7. 後続の段に委ねる事項

- 場面 2 以降（受け身の記憶シーンは Cinemachine の経路で作る）
- 音（英語の合成音声のサンプリング、効果音、BGM）
- 片割れの顔と端末の反射
- 頂点の揺れを入れるかどうか
