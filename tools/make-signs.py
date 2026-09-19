# -*- coding: utf-8 -*-
"""通りの、立ち止まって読む看板のテクスチャを描く。

ネオン（tools/make-neon.py）は店の灯りで、こちらは目の高さに掛かっている
塗りの板。調べる対象になるのはこの 5 枚なので、タヴァーンの看板と同じくらい
しっかり作る。

  - 地は暗い塗り板。粒子を散らして刷った紙の風合いを出す
  - 枠を二重に回し、隅に留めの鋲を打つ
  - 字は生成りの色。書体はリポジトリの Noto Sans JP（SIL OFL）だけを使う
  - 雨垂れと汚れを少し乗せる。新品の板は 22 世紀のロンドンには無い

    python tools/make-signs.py
"""

import os
import random

from PIL import Image, ImageDraw, ImageFilter, ImageFont

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(HERE)
OUT = os.path.join(ROOT, 'unity', 'Assets', 'Textures')
FONT = os.path.join(ROOT, 'unity', 'Assets', 'Fonts', 'NotoSansJP-Regular.otf')

SS = 2


def font(px):
    return ImageFont.truetype(FONT, px)


def grain(size, seed, strength):
    """刷った紙の粒子。一様な塗りは板に見えない"""
    rng = random.Random(seed)
    small = Image.new('L', (size[0] // 3, size[1] // 3))
    small.putdata([rng.randint(128 - strength, 128 + strength) for _ in range(small.width * small.height)])
    return small.resize(size, Image.BILINEAR).filter(ImageFilter.GaussianBlur(0.6 * SS))


def plate(size, base, seed):
    """地の板。粒子と、上から下への僅かな暗がりを乗せる"""
    im = Image.new('RGB', size, base)
    g = grain(size, seed, 16)
    px, gx = im.load(), g.load()
    for y in range(size[1]):
        shade = 1.0 - 0.18 * (y / float(size[1]))
        for x in range(size[0]):
            k = (gx[x, y] / 128.0) * shade
            r, gg, b = px[x, y]
            px[x, y] = (min(255, int(r * k)), min(255, int(gg * k)), min(255, int(b * k)))
    return im


def border(d, size, colour, inset, width, gap=None):
    """枠。gap を渡すと二重に回す"""
    w, h = size
    d.rectangle([inset, inset, w - inset, h - inset], outline=colour, width=width)
    if gap:
        i = inset + gap
        d.rectangle([i, i, w - i, h - i], outline=colour, width=max(1, width // 2))


def studs(d, size, colour, inset, r):
    """隅の留めの鋲"""
    w, h = size
    for x, y in ((inset, inset), (w - inset, inset), (inset, h - inset), (w - inset, h - inset)):
        d.ellipse([x - r, y - r, x + r, y + r], fill=colour)


def centred(d, size, lines, colour):
    """(文字列, 大きさ, y の割合) の並びを、それぞれ横中央に置く"""
    for text, px, cy in lines:
        f = font(int(px * SS))
        w = d.textlength(text, font=f)
        box = f.getbbox(text)
        d.text((size[0] * 0.5 - w * 0.5, size[1] * cy - (box[3] - box[1]) * 0.5 - box[1]),
               text, font=f, fill=colour)


def weather(im, seed):
    """雨垂れと汚れ。縦に流れる暗がりを薄く重ねる"""
    rng = random.Random(seed)
    w, h = im.size
    veil = Image.new('L', im.size, 0)
    d = ImageDraw.Draw(veil)
    for _ in range(26):
        x = rng.randrange(w)
        top = rng.randrange(0, int(h * 0.5))
        wide = rng.randint(2 * SS, 9 * SS)
        d.rectangle([x, top, x + wide, h], fill=rng.randint(18, 54))
    for _ in range(14):
        x, y = rng.randrange(w), rng.randrange(h)
        r = rng.randint(6 * SS, 26 * SS)
        d.ellipse([x - r, y - r, x + r, y + r], fill=rng.randint(10, 30))
    veil = veil.filter(ImageFilter.GaussianBlur(4.0 * SS))
    return Image.composite(Image.new('RGB', im.size, (0, 0, 0)), im, veil)


def street_name(size):
    """ロンドンの街路名の札。白地に黒、枠は赤"""
    im = plate(size, (232, 228, 218), 11)
    d = ImageDraw.Draw(im)
    border(d, size, (150, 36, 32), int(16 * SS), max(2, int(7 * SS)))
    centred(d, size, [('GREVILLE STREET', 96, 0.5)], (28, 26, 26))
    return weather(im, 12)


def painted(size, seed, lines, base=(26, 24, 26), ink=(226, 214, 186), trim=(178, 146, 72)):
    """塗りの板。枠を二重に回し、隅に鋲を打つ"""
    im = plate(size, base, seed)
    d = ImageDraw.Draw(im)
    border(d, size, trim, int(16 * SS), max(2, int(5 * SS)), int(12 * SS))
    studs(d, size, trim, int(30 * SS), int(5 * SS))
    centred(d, size, lines, ink)
    return weather(im, seed + 1)


WIDE = (1024 * SS, 512 * SS)
SLIM = (1024 * SS, 320 * SS)

SIGNS = {
    'SignStreetName': (SLIM, lambda s: street_name(s)),
    'SignChemist': (WIDE, lambda s: painted(s, 21, [
        ('HOLBORN CHEMIST', 92, 0.33),
        ('IMPLANT SUPPLIES · REPAIRS', 50, 0.56),
        ('OPEN ALL NIGHT', 44, 0.74),
    ], base=(20, 30, 26), trim=(96, 176, 124))),
    'SignPawn': (WIDE, lambda s: painted(s, 31, [
        ('H. GOODCHILD & SON', 80, 0.31),
        ('PAWNBROKER', 58, 0.54),
        ('EST. 1871', 44, 0.74),
    ], base=(30, 22, 18), trim=(186, 140, 62))),
    'SignNotice': (WIDE, lambda s: painted(s, 41, [
        ('NANOMACHINE ADVISORY', 80, 0.32),
        ('FIREWALL YOUR TERMINAL', 54, 0.56),
        ('BY ORDER', 42, 0.76),
    ], base=(18, 22, 34), ink=(214, 222, 236), trim=(108, 132, 186))),
    'SignFitting': (WIDE, lambda s: painted(s, 51, [
        ('NERVE TERMINAL', 86, 0.30),
        ('FITTING & CALIBRATION', 52, 0.53),
        ('WALK-IN · NO APPOINTMENT', 44, 0.73),
    ], base=(24, 20, 30), ink=(222, 210, 232), trim=(150, 118, 196))),
}


def main():
    for name in sorted(SIGNS):
        size, make = SIGNS[name]
        im = make(size).resize((size[0] // SS, size[1] // SS), Image.LANCZOS)
        path = os.path.join(OUT, name + '.png')
        im.save(path)
        print('  ' + name + '.png  ' + str(im.size))
    print(str(len(SIGNS)) + ' 枚')


if __name__ == '__main__':
    main()
