# kataaware（仮）

短時間で終わる一人称 3D Web ゲーム『HALF AWARE』。自作短編『かたあはれ』の着想を元にしたオリジナルシナリオ。

- 制作環境: Unity 6 LTS + URP + WebGL 書き出し。エディタの操作は MCP for Unity（CoplayDev/unity-mcp）経由で AI が行い、見た目の最終判断はオーナーがエディタと実機で行う
- リポジトリ構成: `unity/` が Unity プロジェクト、`docs/` が設計文書、`prototype-three/` は移行前の Three.js 試作（参照用。設計書のゲームデザインとシナリオは Unity 版に引き継ぐ）
- 路地裏の mesh は repo に置いていない（62 MB ある）。clone した直後や別端末では路地裏が空なので、`HalfAware/Build the alley` で一度組み直す。自室の小物の mesh は置いてある
- 素材の出典と許諾は `unity/Assets/Audio/LICENSES.md`、`unity/Assets/Fonts/LICENSES.md`、`unity/Assets/Models/LICENSES.md`
- 引き継ぎ: `HANDOFF.md` があればセッション開始時に読み、読了後に削除する
- 参考資料: `docs/reference/`
- 設計文書: `docs/superpowers/specs/`、実装計画: `docs/superpowers/plans/`
- 作業の分担: 設計・レビュー・統合は親セッションが担い、実装の細かいタスクは subagent に委ねる。subagent のモデルは呼び出し側で毎回 `opus` または `sonnet` を明示し、親からの継承に任せない

## 用語

カタカナで通じる技術用語を和語に訳さない。コメント・コミットメッセージ・UI 文言・会話のすべてに適用する。

ログ / ピン / マテリアル / テクスチャ / メッシュ / レンダラー / アセット / エディタ / ポリゴン / リポジトリ / ライセンス / パーティクル / アニメーション / コライダー / シーン / プレハブ

「記録」「控え」「鋲」「目印」「材質」「描画」「資産」「編集器」のような言い換えは使わない。
