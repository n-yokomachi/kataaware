# -*- coding: utf-8 -*-
"""通りの、立ち止まって読む看板と、小路の口の案内の矢のテクスチャを描く。

ネオン（tools/make-neon.py）は店の灯りで、こちらは目の高さに掛かっている
塗りの板。調べる対象になるのはこの 5 枚と案内の矢なので、タヴァーンの看板と同じくらい
しっかり作る。

**板の文面は台詞の原稿（docs/scenario/02-alley.md の **看板**）の英語に揃える。**
調べると原稿の文面が画面の真ん中の枠に出るので、板に書いてある字と食い違わないように
（大文字にするのは板の書き方。語は原稿のまま）。原稿の看板を直したら、ここも直して描き直す。

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
ARROW = (512 * SS, 224 * SS)


def yard_arrow(size, point):
    """小路の口の案内の矢。紺の地に白い枠、矢と仕切りの線、ヤードの名。point が -1 なら矢は左、1 なら右。
    前の絵（2026-09-19、描いた道具は残っていなかった）の色と寸法を測って写し、原稿に無い二行目（BISTRO · TAVERN · MARKET）を外した"""
    w, h = size
    u = w / 512.0                                  # 512 × 224 の絵の 1 画素
    # 地は前の絵と同じ平らな紺に、暗い点を散らすだけ（光る板なので、塗りの板の粒子と雨垂れは乗せない）
    im = Image.new('RGB', size, (18, 24, 44))
    d = ImageDraw.Draw(im)
    rng = random.Random(61)
    for _ in range(90):
        px, py = rng.randrange(w), rng.randrange(h)
        r = rng.choice((1, 1, 2)) * u * 0.5
        d.rectangle([px, py, px + r, py + r], fill=rng.choice(((7, 13, 33), (11, 17, 37), (23, 29, 49))))
    d.rectangle([10 * u, 10 * u, w - 10 * u, h - 10 * u], outline=(224, 228, 236), width=int(5 * u))

    def x(v):                                      # 右を指す時は左右を返す
        return v * u if point < 0 else w - v * u

    head = [(x(34), 112 * u), (x(85), 67 * u), (x(85), 157 * u)]
    d.polygon(head, fill=(232, 235, 242))
    d.rectangle([min(x(84), x(119)), 96 * u, max(x(84), x(119)), 128 * u], fill=(232, 235, 242))
    d.rectangle([min(x(133), x(136)), 26 * u, max(x(133), x(136)), 197 * u], fill=(96, 106, 130))
    # 字は仕切りの線と枠の間の真ん中。一行なので縦も真ん中
    left, right = (136 * u, w - 16 * u) if point < 0 else (16 * u, w - 136 * u)
    f = font(int(31 * u))
    text = 'BLEEDING HEART YARD'
    tw = d.textlength(text, font=f)
    box = f.getbbox(text)
    d.text(((left + right) * 0.5 - tw * 0.5, h * 0.5 - (box[3] - box[1]) * 0.5 - box[1]), text, font=f, fill=(224, 228, 236))
    return im


SIGNS = {
    'SignStreetName': (SLIM, lambda s: street_name(s)),
    'SignChemist': (WIDE, lambda s: painted(s, 21, [
        ('HOLBORN CHEMIST', 92, 0.33),
        ('IMPLANT SUPPLIES & REPAIRS', 50, 0.56),
        ('OPEN ALL NIGHT', 44, 0.74),
    ], base=(20, 30, 26), trim=(96, 176, 124))),
    'SignPawn': (WIDE, lambda s: painted(s, 31, [
        ('H. GOODCHILD & SON', 80, 0.31),
        ('PAWNBROKERS', 58, 0.54),
        ('EST. 1871', 44, 0.74),
    ], base=(30, 22, 18), trim=(186, 140, 62))),
    # 原稿は二行。前は三行目に BY ORDER を書いていた（原稿に無いので外す）
    'SignNotice': (WIDE, lambda s: painted(s, 41, [
        ('NANOMACHINE ALERT', 86, 0.38),
        ('FIREWALL YOUR TERMINAL', 56, 0.64),
    ], base=(18, 22, 34), ink=(214, 222, 236), trim=(108, 132, 186))),
    'SignFitting': (WIDE, lambda s: painted(s, 51, [
        ('NERVE TERMINAL', 86, 0.30),
        ('FITTING & TUNING', 56, 0.53),
        ('WALK-INS WELCOME', 46, 0.73),
    ], base=(24, 20, 30), ink=(222, 210, 232), trim=(150, 118, 196))),
    # 小路の口の案内の矢。表（南から読む）は矢が左（西の小路）を、裏（北から読む）は右を指す。
    # 前は一枚の絵を裏で左右に返していたので、裏から見ると字が鏡に映したように反っていた
    'SignYardArrow': (ARROW, lambda s: yard_arrow(s, -1)),
    'SignYardArrowBack': (ARROW, lambda s: yard_arrow(s, 1)),
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
