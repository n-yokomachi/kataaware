# -*- coding: utf-8 -*-
"""場面 4 の電車の窓へ貼る、外を流れる灯りの帯を描く。

窓は 1.5 m 四方に満たず、夕方の車内から見える外は輪郭を失った灯りの塊でしかない。
建物を建てるのではなく、灯りの並びだけを横一列に描いて、これを横へ送る。

  - **横に繋がる**。送るのは横なので、右の端と左の端が合っていないと継ぎ目が毎周来る。
    端をまたぐ形は反対側にも描いて閉じる
  - 背景は黒、灯りは明るい色。**どちらも不透明で置く。** 透明を使うと、Unity の取り込みが
    透け際の縁取りを防ぐために透明な画素の rgb へ白を滲ませる（`make-terminal.py` と同じ）
  - 上は空、真ん中は建物の窓、下は線路際の灯り。三段にしておくと、
    どの高さを切り取っても外の景色に見える
  - 横へ僅かに流す。送っている最中でも一枚ごとの絵は止まっているので、
    ここで流しておかないと速さが伝わらない
  - 種は固定。走らせ直すたびに絵が変わると、差分がノイズになる

    python tools/make-window.py
"""

import os
import random

from PIL import Image, ImageDraw, ImageFilter

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(HERE)
OUT = os.path.join(ROOT, 'unity', 'Assets', 'Textures')

WIDE = 512
TALL = 128
SEED = 41807

SKY = (12, 14, 26)               # 夕方の暮れ残り。真っ黒にすると窓が抜けて見える
BLOCK = (17, 18, 26)             # 建物の影
HORIZON = (48, 40, 44)           # 空と建物の境。うっすら明るい

SILL = 86                        # 建物の足元。ここから下は線路際
VERGE = 104                      # 線路際の灯りが並ぶ高さ

WARM = [(255, 214, 138), (255, 236, 190), (238, 176, 96), (255, 250, 232)]
COOL = [(168, 206, 255), (206, 228, 255)]

BUILD_WIDE = (22, 74)            # 建物一つの幅
BUILD_GAP = (10, 46)             # 建物と建物のあいだ
BUILD_TALL = (24, 74)            # 建物の背。SILL からの高さ

CELL = 7                         # 建物の窓の間隔
LIT_SHARE = 0.50                 # そのうち灯っている割合

LAMP_GAP = (38, 62)              # 線路際の灯りの間隔
BLUR = 1.1                       # 送っている速さを絵にも残す


def wrapped(draw, box, fill):
    """端をまたぐ形は反対側にも描く。横へ繋いだときに切れ目を出さないため"""
    x0, y0, x1, y1 = box
    draw.rectangle([x0, y0, x1, y1], fill=fill)
    if x1 >= WIDE:
        draw.rectangle([x0 - WIDE, y0, x1 - WIDE, y1], fill=fill)
    if x0 < 0:
        draw.rectangle([x0 + WIDE, y0, x1 + WIDE, y1], fill=fill)


def sky(im):
    """空から線路際へ向けて僅かに明るくする。一色だと板に見える"""
    d = ImageDraw.Draw(im)
    for y in range(TALL):
        t = min(1.0, max(0.0, (y - 10) / float(SILL - 10)))
        col = tuple(int(SKY[i] + (HORIZON[i] - SKY[i]) * t * t) for i in range(3))
        d.line([(0, y), (WIDE, y)], fill=col)


def blocks(im, rng):
    """建物の影と、そこに開いた窓。灯りは塊で置く。一つずつ散らすと星に見える"""
    d = ImageDraw.Draw(im)
    made = 0
    lit = 0
    x = rng.randint(0, BUILD_GAP[1])
    while x < WIDE + BUILD_WIDE[1]:
        w = rng.randint(*BUILD_WIDE)
        h = rng.randint(*BUILD_TALL)
        top = SILL - h
        wrapped(d, (x, top, x + w - 1, SILL), BLOCK)
        # 屋上の縁を一段明るく。建物の切り口が空に出る
        wrapped(d, (x, top, x + w - 1, top + 1), (30, 28, 34))
        for cy in range(top + 4, SILL - 3, CELL):
            for cx in range(x + 3, x + w - 4, CELL):
                if rng.random() > LIT_SHARE:
                    continue
                col = rng.choice(WARM if rng.random() < 0.78 else COOL)
                wrapped(d, (cx, cy, cx + 3, cy + 3), col)
                lit += 1
        made += 1
        x += w + rng.randint(*BUILD_GAP)
    return made, lit


def verge(im, rng):
    """線路際。等間隔に並ぶ灯りと、足元を走る一本の帯"""
    d = ImageDraw.Draw(im)
    wrapped(d, (0, VERGE + 9, WIDE, TALL), (10, 10, 14))
    wrapped(d, (0, VERGE + 7, WIDE, VERGE + 8), (34, 32, 30))
    lamps = 0
    x = rng.randint(0, LAMP_GAP[0])
    while x < WIDE:
        wrapped(d, (x, VERGE - 12, x + 1, VERGE + 7), (24, 24, 28))     # 柱
        wrapped(d, (x - 3, VERGE - 15, x + 4, VERGE - 12), (255, 226, 168))
        wrapped(d, (x - 6, VERGE - 13, x + 7, VERGE - 9), (118, 96, 60))  # 灯りの下の滲み
        lamps += 1
        x += rng.randint(*LAMP_GAP)
    return lamps


def build():
    rng = random.Random(SEED)
    im = Image.new('RGB', (WIDE, TALL), SKY)
    sky(im)
    made, lit = blocks(im, rng)
    lamps = verge(im, rng)
    # 横だけに滲ませたいので、横へ繋げた三枚から真ん中を切り出す。
    # そのまま掛けると端が内側の色だけで平均され、繋ぎ目に縦の筋が出る
    wide = Image.new('RGB', (WIDE * 3, TALL))
    for i in range(3):
        wide.paste(im, (WIDE * i, 0))
    wide = wide.filter(ImageFilter.GaussianBlur(BLUR))
    return wide.crop((WIDE, 0, WIDE * 2, TALL)), made, lit, lamps


def main():
    if not os.path.isdir(OUT):
        raise SystemExit('テクスチャの置き場が無い: ' + OUT)
    im, made, lit, lamps = build()
    path = os.path.join(OUT, 'WindowLights.png')
    im.save(path)
    print('  WindowLights.png  ' + str(im.size))
    print('  建物 ' + str(made) + ' 棟、灯った窓 ' + str(lit) + ' 個、線路際の灯り ' + str(lamps) + ' 本')
    # 継ぎ目は、隣り合う列どうしの差として測る。建物の切れ目がたまたま端に来ると
    # 最大値は跳ねるので、中ほどの列の差と並べて見ないと繋がっているか分からない
    gaps = sorted(column(im, x, x + 1) for x in range(WIDE - 1))
    print('  端どうしの差 %.1f／内側の列の差は中央 %.1f・上位一割 %.1f'
          % (column(im, WIDE - 1, 0), gaps[len(gaps) // 2], gaps[int(len(gaps) * 0.9)]))


def column(im, a, b):
    """列 a と列 b の、画素あたりの色の差"""
    total = 0
    for y in range(TALL):
        p = im.getpixel((a, y))
        q = im.getpixel((b, y))
        total += max(abs(p[i] - q[i]) for i in range(3))
    return total / float(TALL)


if __name__ == '__main__':
    main()
