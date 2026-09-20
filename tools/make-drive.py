# -*- coding: utf-8 -*-
"""場面 8（車内）のテクスチャを描く。

原作の「ボロのオフロード車」で、手入れではなく修理だけで走らせてきた車。
新品の内装は一枚も無い。どの絵も、

  - 地の色は BuildDrive.Tone が持っていた色に寄せる（最後に平均を合わせる）。
    マテリアルは絵があれば _BaseColor を白にするので、明るさは絵の側が持つことになる
  - 擦れ・汚れ・傷を乗せる。手と足の触る所だけ地が出て光り、隅には汚れが溜まる
  - 汚れは輪郭を立てずにぼかしてから明暗として重ねる。縁の立った染みを置くと水玉になる
  - 継ぎ目が出ないよう、線も粒子も上下左右へ畳んで描く

メーターだけは Unlit で貼るので、夜に自分で光る絵として描く。

    python tools/make-drive.py
"""

import math
import os
import random

from PIL import Image, ImageDraw, ImageFilter, ImageFont

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(HERE)
OUT = os.path.join(ROOT, 'unity', 'Assets', 'Textures')
FONT = os.path.join(ROOT, 'unity', 'Assets', 'Fonts', 'NotoSansJP-Regular.otf')

SIZE = 256


# ---- 道具 ---------------------------------------------------------------

class Wrap(object):
    """上下左右へ畳んで描く。同じ形を 9 箇所へ置くので、端で切れた分が裏から出てくる"""

    def __init__(self, im):
        self.d = ImageDraw.Draw(im)
        self.w, self.h = im.size

    def _off(self):
        for dx in (-self.w, 0, self.w):
            for dy in (-self.h, 0, self.h):
                yield dx, dy

    def line(self, xy, **kw):
        for dx, dy in self._off():
            self.d.line([(x + dx, y + dy) for (x, y) in xy], **kw)

    def ellipse(self, box, **kw):
        x0, y0, x1, y1 = box
        for dx, dy in self._off():
            self.d.ellipse([x0 + dx, y0 + dy, x1 + dx, y1 + dy], **kw)

    def rect(self, box, **kw):
        x0, y0, x1, y1 = box
        for dx, dy in self._off():
            self.d.rectangle([x0 + dx, y0 + dy, x1 + dx, y1 + dy], **kw)


def tile_blur(im, radius):
    """継ぎ目の出ないぼかし。3x3 に並べてからぼかして真ん中を切り出す"""
    w, h = im.size
    big = Image.new(im.mode, (w * 3, h * 3))
    for i in range(3):
        for j in range(3):
            big.paste(im, (w * i, h * j))
    return big.filter(ImageFilter.GaussianBlur(radius)).crop((w, h, w * 2, h * 2))


def noise(size, seed, lo=0, hi=255, blur=0.0):
    """粒子。1 画素ずつ独立に引くので、そのままで継ぎ目が出ない"""
    rng = random.Random(seed)
    im = Image.new('L', size)
    im.putdata([rng.randint(lo, hi) for _ in range(size[0] * size[1])])
    return tile_blur(im, blur) if blur > 0 else im


def clouds(size, seed, octaves=4, blur=1.0):
    """粗い斑。小さい粒子を引き伸ばして重ねる。汚れと褪せの下地"""
    acc = Image.new('L', size, 128)
    for o in range(octaves):
        step = 2 ** (o + 1)
        small = noise((max(2, size[0] // step), max(2, size[1] // step)), seed + o * 97)
        acc = Image.blend(acc, small.resize(size, Image.BICUBIC), 0.5 / (o + 1))
    return tile_blur(acc, blur)


def tint(size, grey, lo, hi):
    """明るさの地図を色に直す。lo が暗い側、hi が明るい側"""
    return Image.composite(Image.new('RGB', size, hi), Image.new('RGB', size, lo), grey)


def shade(im, mask, amount):
    """
    128 を中立とした地図で明暗を付ける。amount が最大の増減（1.0 で倍まで）。

    汚れも擦れもこれで重ねる。色を上から塗ると絵の具を置いたようになるが、
    もとの粒子を残したまま明暗だけ動かせば、下の素材が透けたまま汚れる
    """
    px = im.load()
    mx = mask.load()
    w, h = im.size
    for y in range(h):
        for x in range(w):
            k = 1.0 + (mx[x, y] - 128) / 128.0 * amount
            c = px[x, y]
            px[x, y] = (min(255, max(0, int(c[0] * k))),
                        min(255, max(0, int(c[1] * k))),
                        min(255, max(0, int(c[2] * k))))
    return im


def blot(size, seed, count, span, deep, soft, sign=-1):
    """
    染みの地図。sign が -1 なら暗く、+1 なら明るく。
    一度 L へ描いてからぼかすので、縁が立たない
    """
    rng = random.Random(seed)
    m = Image.new('L', size, 128)
    w = Wrap(m)
    for _ in range(count):
        x, y = rng.randrange(size[0]), rng.randrange(size[1])
        r = rng.uniform(span * 0.35, span)
        v = 128 + sign * rng.randint(int(deep * 0.35), deep)
        w.ellipse([x - r, y - r * rng.uniform(0.5, 1.0),
                   x + r, y + r * rng.uniform(0.5, 1.0)], fill=int(v))
    return tile_blur(m, soft)


def aim(im, target):
    """平均をこの色へ寄せる。マテリアルが持っていた明るさを絵へ移すため"""
    px = im.load()
    w, h = im.size
    n = float(w * h)
    tot = [0.0, 0.0, 0.0]
    for y in range(h):
        for x in range(w):
            c = px[x, y]
            for k in range(3):
                tot[k] += c[k]
    gain = [target[k] / max(1.0, tot[k] / n) for k in range(3)]
    for y in range(h):
        for x in range(w):
            c = px[x, y]
            px[x, y] = tuple(min(255, int(c[k] * gain[k] + 0.5)) for k in range(3))
    return im


def save(im, name):
    path = os.path.join(OUT, 'Drive' + name + '.png')
    im.save(path)
    print('%-24s %dx%d' % (os.path.basename(path), im.size[0], im.size[1]))


# ---- 内装。計器盤・内張り・天井・ハンドルの輪 ---------------------------

def trim():
    """
    艶の引けた黒い樹脂。型で押した梨地と、擦れて地の出た筋。

    **明るさの幅を広く取る。** 夜の帯では明暗しか手掛かりが無いので、
    一様な黒で塗ると計器盤が板一枚に潰れる
    """
    size = (SIZE, SIZE)
    base = Image.blend(noise(size, 5501, 100, 156, 0.7), clouds(size, 1207, 5, 2.0), 0.5)
    im = tint(size, base, (62, 60, 63), (122, 120, 125))
    w = Wrap(im)
    rng = random.Random(8821)
    # 型の梨地。粗い横の目を薄く入れると、押し出した板に見える
    for y in range(0, SIZE, 5):
        w.line([(0, y), (SIZE, y)], fill=(150, 148, 156), width=1)
        w.line([(0, y + 2), (SIZE, y + 2)], fill=(74, 72, 78), width=1)
    im = Image.blend(im, tile_blur(im, 1.4), 0.7)
    w = Wrap(im)
    # 擦り傷。手の当たる所だけ地が出る
    for _ in range(16):
        x, y = rng.randrange(SIZE), rng.randrange(SIZE)
        ln = rng.randint(10, 38)
        a = rng.uniform(-0.35, 0.35)
        w.line([(x, y), (x + ln * math.cos(a), y + ln * math.sin(a))],
               fill=(126, 124, 128), width=1)
    im = tile_blur(im, 0.45)
    # 汚れの溜まりと、擦れて明るくなった所
    im = shade(im, blot(size, 3301, 14, 30, 74, 9.0, -1), 0.55)
    im = shade(im, blot(size, 4402, 9, 26, 46, 11.0, +1), 0.40)
    return aim(im, (100, 99, 102))


def steel():
    """
    塗った鉄。ハンドルの輻・スイッチの座・取っ手。
    褪せた砂色で、角だけ塗りが剥げて下の錆が出ている
    """
    size = (SIZE, SIZE)
    base = Image.blend(noise(size, 3313, 112, 148, 0.8), clouds(size, 771, 5, 2.2), 0.55)
    im = tint(size, base, (104, 98, 84), (178, 170, 152))
    im = shade(im, blot(size, 6607, 12, 34, 60, 10.0, -1), 0.45)
    w = Wrap(im)
    rng = random.Random(5519)
    # 錆。塗りの薄い所から滲む。輪郭を立てずに小さく散らす
    rust = Image.new('L', size, 0)
    rw = Wrap(rust)
    for _ in range(7):
        x, y = rng.randrange(SIZE), rng.randrange(SIZE)
        r = rng.randint(7, 20)
        rw.ellipse([x - r, y - r * 0.45, x + r, y + r * 0.45], fill=rng.randint(80, 165))
    rust = tile_blur(rust, 6.0)
    im = Image.composite(Image.new('RGB', size, (112, 72, 46)), im, rust)
    # 引っ掻き傷
    for _ in range(18):
        x, y = rng.randrange(SIZE), rng.randrange(SIZE)
        ln = rng.randint(8, 26)
        a = rng.uniform(0, math.pi)
        w.line([(x, y), (x + ln * math.cos(a), y + ln * math.sin(a))],
               fill=(182, 176, 162), width=1)
    im = tile_blur(im, 0.6)
    return aim(im, (146, 140, 126))


# ---- 座席 ---------------------------------------------------------------

def seat():
    """
    擦り切れた布張り。縦の縫い目と、腰と腿の当たる所の照り。
    座面と背もたれで同じ絵を使うので、織りには向きを付けない
    """
    size = (SIZE, SIZE)
    base = Image.blend(noise(size, 4409, 104, 152, 0.5), clouds(size, 2903, 5, 2.4), 0.5)
    im = tint(size, base, (60, 52, 47), (140, 125, 114))
    w = Wrap(im)
    # 織り目。縦横の粗い格子。ぼかして糸の束に均す
    for x in range(0, SIZE, 4):
        w.line([(x, 0), (x, SIZE)], fill=(158, 142, 130), width=1)
    for y in range(0, SIZE, 6):
        w.line([(0, y), (SIZE, y)], fill=(70, 60, 54), width=1)
    im = Image.blend(im, tile_blur(im, 2.0), 0.82)
    w = Wrap(im)
    # 縫い目。窪んだ筋の上を糸が渡る
    for x in (42, 128, 214):
        w.line([(x, 0), (x, SIZE)], fill=(58, 49, 44), width=5)
        for y in range(0, SIZE, 11):
            w.line([(x - 4, y), (x + 4, y + 2)], fill=(146, 133, 119), width=2)
    im = tile_blur(im, 1.1)
    # 毛羽の落ちた照りと、染み
    im = shade(im, blot(size, 1733, 10, 40, 58, 13.0, +1), 0.42)
    im = shade(im, blot(size, 1907, 12, 22, 70, 8.0, -1), 0.50)
    return aim(im, (105, 98, 92))


# ---- ボンネット ---------------------------------------------------------

def body():
    """
    褪せて白茶けた塗り。**平均の明るさは動かさない。**

    この面だけは夜の場面で浮くほど明るく塗ってあり（BuildDrive.Tone の但し書き）、
    暗くすると前の縁が路面に溶けて車体が消える。絵で足すのは斑と傷だけ
    """
    size = (SIZE, SIZE)
    base = Image.blend(noise(size, 7717, 116, 150, 0.8), clouds(size, 4231, 5, 3.0), 0.66)
    im = tint(size, base, (158, 158, 150), (208, 210, 200))
    # 褪せ。日に灼けた斑を大きく取る
    im = shade(im, blot(size, 4801, 8, 56, 42, 18.0, +1), 0.30)
    im = shade(im, blot(size, 4903, 10, 44, 48, 16.0, -1), 0.34)
    # 錆の吹き。塗りの下から出るので、縁をぼかしたまま色を乗せる
    rust = Image.new('L', size, 0)
    rw = Wrap(rust)
    rng = random.Random(9091)
    for _ in range(6):
        x, y = rng.randrange(SIZE), rng.randrange(SIZE)
        r = rng.randint(8, 24)
        rw.ellipse([x - r, y - r * 0.4, x + r, y + r * 0.4], fill=rng.randint(70, 150))
    rust = tile_blur(rust, 7.0)
    im = Image.composite(Image.new('RGB', size, (128, 92, 62)), im, rust)
    w = Wrap(im)
    # 擦り傷。前後に走る向きで揃える
    for _ in range(18):
        x, y = rng.randrange(SIZE), rng.randrange(SIZE)
        ln = rng.randint(14, 44)
        a = math.pi * 0.5 + rng.uniform(-0.16, 0.16)
        w.line([(x, y), (x + ln * math.cos(a), y + ln * math.sin(a))],
               fill=(204, 206, 198), width=1)
    # 泥はね。未舗装路を走る車なので、細かい粒を散らす
    mud = blot(size, 6101, 18, 9, 48, 3.0, -1)
    im = shade(im, mud, 0.26)
    im = tile_blur(im, 0.55)
    return aim(im, (182, 184, 176))


# ---- 計器 ---------------------------------------------------------------

DIALS = (512, 128)


def dials():
    """
    メーターの面。Unlit で貼るので、夜はこれだけが自分で光る。

    丸い形も数字も全部ここに描く。低い解像度では、板を並べて丸を作るより
    絵に描いた方が読める。速度計・回転計と、小さい燃料計・水温計。

    **針は琥珀色にする。** 夜の車内で唯一の暖色になり、外の青白い光と分かれる
    """
    ss = 3
    size = (DIALS[0] * ss, DIALS[1] * ss)
    im = Image.new('RGB', size, (16, 16, 19))
    d = ImageDraw.Draw(im)
    # 地の板。上へ向かうほど暗くして、庇の影を焼き込む
    for y in range(size[1]):
        k = 0.55 + 0.45 * (y / float(size[1]))
        d.line([(0, y), (size[0], y)], fill=(int(34 * k), int(33 * k), int(38 * k)))

    def face(cx, cy, r, ticks, major, needle, label):
        cx, cy, r = cx * ss, cy * ss, r * ss
        # 枠の輪。塗った鉄
        d.ellipse([cx - r, cy - r, cx + r, cy + r], fill=(104, 99, 90))
        d.ellipse([cx - r + 3 * ss, cy - r + 3 * ss,
                   cx + r - 3 * ss, cy + r - 3 * ss], fill=(20, 19, 22))
        for i in range(ticks + 1):
            a = math.radians(135 + 270.0 * i / ticks)
            big = (i % major) == 0
            r0, r1 = r - (11 if big else 7) * ss, r - 5 * ss
            col = (238, 228, 198) if big else (148, 142, 124)
            d.line([cx + r0 * math.cos(a), cy + r0 * math.sin(a),
                    cx + r1 * math.cos(a), cy + r1 * math.sin(a)],
                   fill=col, width=(3 if big else 2) * ss)
        a = math.radians(135 + 270.0 * needle)
        d.line([cx - r * 0.18 * math.cos(a), cy - r * 0.18 * math.sin(a),
                cx + (r - 9 * ss) * math.cos(a), cy + (r - 9 * ss) * math.sin(a)],
               fill=(252, 160, 56), width=3 * ss)
        d.ellipse([cx - 5 * ss, cy - 5 * ss, cx + 5 * ss, cy + 5 * ss], fill=(126, 118, 104))
        if label:
            f = ImageFont.truetype(FONT, int(r * 0.32))
            box = d.textbbox((0, 0), label, font=f)
            d.text((cx - (box[2] - box[0]) * 0.5, cy + r * 0.36), label,
                   font=f, fill=(198, 188, 162))

    face(74, 64, 52, 12, 2, 0.46, 'MPH')
    face(190, 64, 52, 10, 2, 0.34, None)
    face(292, 64, 34, 4, 2, 0.62, None)
    face(366, 64, 34, 4, 2, 0.28, None)
    # 警告灯。消えている玉が二つと、点いている玉がひとつ
    for x, col in ((424, (54, 40, 34)), (452, (216, 76, 48)), (480, (44, 50, 42))):
        d.ellipse([(x - 8) * ss, 38 * ss, (x + 8) * ss, 54 * ss], fill=col)
    f = ImageFont.truetype(FONT, 14 * ss)
    d.text((424 * ss, 72 * ss), '08214', font=f, fill=(196, 188, 164))

    im = im.resize(DIALS, Image.LANCZOS)
    # 硝子の内側に溜まった埃。読めなくならない程度に
    im = shade(im, clouds(DIALS, 5171, 4, 1.4).point(lambda v: 128 + (v - 128) // 4), 0.30)
    return im


def main():
    if not os.path.isdir(OUT):
        raise SystemExit('テクスチャの置き場が無い: ' + OUT)
    save(trim(), 'CarTrim')
    save(steel(), 'CarSteel')
    save(seat(), 'CarSeat')
    save(body(), 'CarBody')
    save(dials(), 'CarDials')


if __name__ == '__main__':
    main()
