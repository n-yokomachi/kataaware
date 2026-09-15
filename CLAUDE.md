# kataaware（仮）

短時間で終わる Three.js 製の一人称 3D Web ゲーム。自作短編『かたあはれ』の着想を元にしたオリジナルシナリオ。

- 制作環境: Three.js + TypeScript + Vite
- 引き継ぎ: `HANDOFF.md` があればセッション開始時に読み、読了後に削除する
- 参考資料: `docs/reference/`
- 設計文書: `docs/superpowers/specs/`、実装計画: `docs/superpowers/plans/`
- 作業の分担: 設計・レビュー・統合は親セッションが担い、実装の細かいタスクは subagent に委ねる。subagent のモデルは呼び出し側で毎回 `opus` または `sonnet` を明示し、親からの継承に任せない
- 動作確認: `.claude/launch.json` の `dev` で起動し `http://localhost:5173/?nolock&debug` を開く。in-app ブラウザはペインが非表示だと描画ループもポインタロックも止まるため、`window.__step(dt, n)` でフレームを進め、`KeyboardEvent` / `MouseEvent` を `window` に送って操作し、`#subtitle` `#prompt` `#center` `#fade` の DOM で状態を読む。スクリーンショットは左上 4 分の 1 しか写らないので、3D は `#view` を小さな canvas に描いて `toDataURL` で取り出す。非表示中は CSS の transition も進まないので、不透明度は `style.opacity` で判断する。`?debug` では `window.__camera` でカメラ位置も読める
