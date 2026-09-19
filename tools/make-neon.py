# -*- coding: utf-8 -*-
"""通りのネオンサインのテクスチャを描く。

以前のものは淡い細い線が 1 本乗っているだけで、通りから見ると何の看板だか
分からなかった。ここでは店の看板として読めるように、

  - 文字を管に見立てた輪郭で描く（塗り潰しではなく、縁だけを残す）
  - 白く焼けた芯・色の乗った中間・広く滲む暈の 3 層を重ねる
  - 枠の管を回して、板としての形を出す

の 3 つをやる。マテリアルは加算合成（SrcAlpha, One）なので、
alpha が明るさ、RGB が色になる。暗い部分は書き込んでも消えるだけなので
背景は透明のままでよい。

書体はリポジトリに置いてある Noto Sans JP（SIL OFL）だけを使う。
OS の書体は再配布の扱いが面倒なので触らない。

    python tools/make-neon.py
"""

import os
from PIL import Image, ImageDraw, ImageFilter, ImageFont

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(HERE)
OUT = os.path.join(ROOT, 'unity', 'Assets', 'Textures')
FONT = os.path.join(ROOT, 'unity', 'Assets', 'Fonts', 'NotoSansJP-Regular.otf')

SS = 2          # 仕上げの何倍で描くか
TALL = (512, 1024)
WIDE = (1024, 512)


def font(px):
    return ImageFont.truetype(FONT, px)


def erode(mask, px):
    """px だけ細らせる。PIL の MinFilter は一度に 1 画素ずつしか削れない"""
    out = mask
    for _ in range(max(0, int(px))):
        out = out.filter(ImageFilter.MinFilter(3))
    return out


def tube(draw_fn, size, width):
    """
    draw_fn で塗り潰した形を描き、その縁だけを width の管として残す。
    塗り潰しから内側を削って引くと、輪郭に沿った一定幅の輪になる
    """
    solid = Image.new('L', size, 0)
    draw_fn(ImageDraw.Draw(solid))
    inner = erode(solid, width)
    ring = Image.new('L', size, 0)
    ring.paste(solid, (0, 0))
    return Image.composite(Image.new('L', size, 0), ring, inner.point(lambda v: 255 if v > 127 else 0))


def text_tube(size, lines, px, width, spacing, vertical, cx, cy):
    """
    文字を管で。lines は上から順。vertical なら 1 文字ずつ縦に積む。
    cx / cy は文字の塊を置く中心を寸法に対する割合で。飾りと喧嘩させないために要る
    """
    f = font(px)
    step = px * spacing
    items = [c for c in ''.join(lines)] if vertical else lines
    block = step * len(items)

    def paint(d):
        top = size[1] * cy - block * 0.5
        for i, item in enumerate(items):
            w = d.textlength(item, font=f)
            d.text((size[0] * cx - w * 0.5, top + step * i), item, font=f, fill=255)

    return tube(paint, size, width)


def frame(size, inset, width, radius):
    """枠の管。看板の輪郭をはっきりさせる"""
    def paint(d):
        d.rounded_rectangle([inset, inset, size[0] - inset, size[1] - inset],
                            radius=radius, fill=255)
    solid = Image.new('L', size, 0)
    paint(ImageDraw.Draw(solid))
    return Image.composite(Image.new('L', size, 0), solid,
                           erode(solid, width).point(lambda v: 255 if v > 127 else 0))


def glow(mask, colour, size):
    """芯・中間・暈の 3 層。alpha が明るさ、RGB は芯へ寄るほど白く焼ける"""
    core = mask.filter(ImageFilter.GaussianBlur(1.5 * SS))
    mid = mask.filter(ImageFilter.GaussianBlur(5.0 * SS))
    halo = mask.filter(ImageFilter.GaussianBlur(20.0 * SS))

    px_core, px_mid, px_halo = core.load(), mid.load(), halo.load()
    out = Image.new('RGBA', size, (0, 0, 0, 0))
    px = out.load()
    r, g, b = colour
    for y in range(size[1]):
        for x in range(size[0]):
            c = px_core[x, y] / 255.0
            m = px_mid[x, y] / 255.0
            h = px_halo[x, y] / 255.0
            a = c + m * 0.85 + h * 0.50
            if a <= 0.004:
                continue
            a = min(1.0, a)
            # 芯は白く、外へ行くほど色が濃くなる
            k = min(1.0, max(0.0, c - 0.30) * 1.45)
            px[x, y] = (
                int(min(255, r + (255 - r) * k)),
                int(min(255, g + (255 - g) * k)),
                int(min(255, b + (255 - b) * k)),
                int(a * 255),
            )
    return out


def add(base, layer):
    return Image.alpha_composite(base, layer)


def emblem_eye(d, size, px):
    """目。横長の看板の上に置く"""
    w, h = size
    cx, cy = w * 0.5, h * 0.27
    rx, ry = w * 0.15, h * 0.13
    d.polygon([(cx - rx, cy), (cx, cy - ry), (cx + rx, cy), (cx, cy + ry)], fill=255)
    # 瞳は穴として抜く。塗り潰すと輪郭を残す処理で消える
    r = min(rx, ry) * 0.50
    d.ellipse([cx - r, cy - r, cx + r, cy + r], fill=0)


def emblem_cross(d, size, px):
    """薬局の十字。縦長の看板の上に置く"""
    w, h = size
    cx, cy = w * 0.5, h * 0.21
    arm, thick = h * 0.098, h * 0.038
    d.rectangle([cx - thick, cy - arm, cx + thick, cy + arm], fill=255)
    d.rectangle([cx - arm, cy - thick, cx + arm, cy + thick], fill=255)


def emblem_arrow(d, size, px):
    """下向きの矢。縦長の看板の上に置く"""
    w, h = size
    cx = w * 0.5
    top, bot = h * 0.09, h * 0.40
    shaft = w * 0.075
    d.rectangle([cx - shaft, top, cx + shaft, bot - h * 0.085], fill=255)
    d.polygon([(cx - w * 0.23, bot - h * 0.115), (cx + w * 0.23, bot - h * 0.115), (cx, bot)], fill=255)


def emblem_rings(d, size, px):
    """質屋の三つ玉を二つに詰めたもの。縦長の看板の上に置く"""
    w, h = size
    cy = h * 0.20
    r = h * 0.072
    for dx in (-r * 1.18, r * 1.18):
        d.ellipse([w * 0.5 + dx - r, cy - r, w * 0.5 + dx + r, cy + r], fill=255)


def emblem_bowl(d, size, px):
    """丼。横長の看板の左に置く"""
    w, h = size
    cx, cy = w * 0.155, h * 0.56
    rw, rh = w * 0.082, h * 0.19
    d.pieslice([cx - rw, cy - rh, cx + rw, cy + rh], 0, 180, fill=255)
    d.rectangle([cx - rw * 1.18, cy - rh * 0.14, cx + rw * 1.18, cy + rh * 0.04], fill=255)


def emblem_glass(d, size, px):
    """カクテルグラス。横長の看板の左に置く"""
    w, h = size
    cx, cy = w * 0.145, h * 0.38
    rw, rh = w * 0.072, h * 0.20
    d.polygon([(cx - rw, cy - rh), (cx + rw, cy - rh), (cx, cy + rh * 0.35)], fill=255)
    d.rectangle([cx - w * 0.010, cy + rh * 0.30, cx + w * 0.010, cy + rh * 1.05], fill=255)
    d.rectangle([cx - rw * 0.70, cy + rh * 1.00, cx + rw * 0.70, cy + rh * 1.18], fill=255)


# 名前 → (寸法, 文字, 文字の大きさ, 色, 縦書き, 飾り, 文字の中心 x, 文字の中心 y)
# 中心は寸法に対する割合。飾りを置いた側から文字を退かすのに使う
SIGNS = {
    'NeonHotel':  (TALL, ['HOTEL'],             124, (255, 46, 120), True,  None,         0.50, 0.50),
    'NeonBar':    (TALL, ['BAR'],               168, (60, 224, 255), True,  None,         0.50, 0.50),
    'NeonCross':  (TALL, ['CHEMIST'],            68, (90, 255, 150), True,  emblem_cross, 0.50, 0.68),
    'NeonArrow':  (TALL, ['ENTRY'],              78, (255, 196, 40), True,  emblem_arrow, 0.50, 0.72),
    'NeonRings':  (TALL, ['PAWN'],              104, (255, 150, 40), True,  emblem_rings, 0.50, 0.66),
    'NeonClub':   (WIDE, ['CLUB'],              196, (226, 90, 255), False, emblem_glass, 0.60, 0.50),
    'NeonEye':    (WIDE, ['OPTIC'],             142, (120, 190, 255), False, emblem_eye,  0.50, 0.68),
    'NeonNerve':  (WIDE, ['NERVE', 'TERMINAL'], 118, (90, 255, 210), False, None,         0.50, 0.50),
    'NeonNoodle': (WIDE, ['NOODLE'],            148, (255, 120, 60), False, emblem_bowl,  0.60, 0.50),
    'NeonOpen':   (WIDE, ['OPEN'],              210, (80, 255, 120), False, None,         0.50, 0.50),
    'NeonLive':   (WIDE, ['LIVE', 'HOUSE'],     132, (255, 70, 70), False, None,          0.50, 0.50),
    'NeonSleep':  (WIDE, ['SLEEP', 'POD'],      132, (170, 130, 255), False, None,        0.50, 0.50),
}


def build(name, spec):
    size, lines, px, colour, vertical, emb, cx, cy = spec
    big = (size[0] * SS, size[1] * SS)
    px_big = int(px * SS)
    tw = max(3, int(px * 0.135 * SS))       # 管の太さは字の大きさに比例させる

    layers = []

    # 枠。板としての輪郭
    layers.append((frame(big, int(14 * SS), max(3, int(6 * SS)), int(26 * SS)), colour))

    # 飾り
    if emb is not None:
        solid = Image.new('L', big, 0)
        emb(ImageDraw.Draw(solid), big, px_big)
        inner = erode(solid, tw)
        layers.append((Image.composite(Image.new('L', big, 0), solid,
                                       inner.point(lambda v: 255 if v > 127 else 0)), colour))

    layers.append((text_tube(big, lines, px_big, tw, 1.16, vertical, cx, cy), colour))

    out = Image.new('RGBA', big, (0, 0, 0, 0))
    for mask, col in layers:
        out = add(out, glow(mask, col, big))
    out = out.resize(size, Image.LANCZOS)
    path = os.path.join(OUT, name + '.png')
    out.save(path)
    return path


def main():
    for name in sorted(SIGNS):
        p = build(name, SIGNS[name])
        print('  ' + os.path.basename(p) + '  ' + str(Image.open(p).size))
    print(str(len(SIGNS)) + ' 枚')


if __name__ == '__main__':
    main()
