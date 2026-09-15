# kataaware（仮）

短時間で終わる Three.js 製の一人称 3D Web ゲーム。自作短編『かたあはれ』の着想を元にしたオリジナルシナリオ。

- 制作環境: Three.js + TypeScript + Vite
- 引き継ぎ: `HANDOFF.md` があればセッション開始時に読み、読了後に削除する
- 参考資料: `docs/reference/`
- 設計文書: `docs/superpowers/specs/`、実装計画: `docs/superpowers/plans/`
- 作業の分担: 設計・レビュー・統合は親セッションが担い、実装の細かいタスクは subagent に委ねる。subagent のモデルは呼び出し側で毎回 `opus` または `sonnet` を明示し、親からの継承に任せない
