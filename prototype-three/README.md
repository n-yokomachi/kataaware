# Three.js による試作（保管）

Unity への移行前に作った試作。場面 1（自室）が仮の箱と PS1 風の描画で最初から最後まで遊べる。設計書は `../docs/` にあり、ゲームデザインとシナリオはそのまま Unity 版に引き継ぐ。

## 動かし方

```bash
cd prototype-three
npm install
npm run dev
```

`http://localhost:5173/?nolock&debug` を開く。`?layout` で配置モード。テストは `npm test`。

## 確認の要領（in-app ブラウザ向け）

ペインが非表示だと描画ループとポインタロックが止まるため、`window.__step(dt, n)` でフレームを進め、`KeyboardEvent` / `MouseEvent` を `window` に送って操作し、`#subtitle` `#prompt` `#center` `#fade` の DOM で状態を読む。3D は `#view` を小さな canvas に描いて `toDataURL` で取り出す。`?debug` では `window.__camera` でカメラ位置も読める。
