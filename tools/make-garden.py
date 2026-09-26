# -*- coding: utf-8 -*-
"""村と庭の場所（Village.unity）のテクスチャを描く。

設計: docs/superpowers/specs/2026-09-26-village-garden-design.md
下調べ: docs/reference/english-cottage-garden.md

描くのは次の二つの組。

  - 花と葉のアトラス（VillageFlora.png）。花の縁・つる・木の札に貼る絵を一枚にまとめる。
    **α が形そのもの**（麦の札と同じ考え。make-drive.py の wheat）。札は交差させて株にし、
    シェーダー（HalfAware/Foliage）が閾値で抜く
  - 面の繰り返しの絵。石灰岩の壁、石版の屋根、煉瓦の小路、敷石、生け垣の葉、木の板

**描画解像度は 320×180 しか無い。** 花の縁を 5 m 先から見ると、1 m の株が 26 画素ほどになる。
小さな花を細かく散らすと色の点の雑音にしか見えないので、花は大きめの塊で置き、
同じ色の花を寄せて描く。葉は花より暗く沈め、花の色が葉の上に浮くようにする。

アトラスの割り付けは 128 画素を一つの升にした 8×16 升（1024×2048）。札の縦横の比に合わせて、
升を 1×3（背の高い物）、1×2（中くらい）、2×1（低い塊）、2×2（つると木）、4×1（アーチの帯）で使う。
**割り付けを変えたら BuildVillagePlants.cs の Cells も直す。**

    python tools/make-garden.py          # 全部
    python tools/make-garden.py flora    # 花と葉のアトラスだけ
"""

import importlib.util
import math
import os
import random

from PIL import Image, ImageDraw, ImageFilter

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(HERE)
OUT = os.path.join(ROOT, 'unity', 'Assets', 'Textures', 'Village')


def _drive():
    """場面 8 の描き方の道具（継ぎ目の出ないぼかし・粒子・斑）を借りる"""
    spec = importlib.util.spec_from_file_location('make_drive', os.path.join(HERE, 'make-drive.py'))
    mod = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(mod)
    return mod


D = _drive()

UNIT = 128
ATLAS = 1024
# アトラスの縦。上の 1024 が初めの 8×8 升、下の 1024 に 2026-09-27 からの升を足していく
ATLAS_H = 2048


# ---- 札を描く筆 --------------------------------------------------------------

class Brush(object):
    """色と α へ同時に描く。札は繰り返さないので畳まない"""

    def __init__(self, w, h):
        self.col = Image.new('RGB', (w, h), (0, 0, 0))
        self.mask = Image.new('L', (w, h), 0)
        self.c = ImageDraw.Draw(self.col)
        self.a = ImageDraw.Draw(self.mask)
        self.w = w
        self.h = h

    def ell(self, cx, cy, rx, ry, col):
        box = [cx - rx, cy - ry, cx + rx, cy + ry]
        self.c.ellipse(box, fill=col)
        self.a.ellipse(box, fill=255)

    def poly(self, pts, col):
        self.c.polygon(pts, fill=col)
        self.a.polygon(pts, fill=255)

    def line(self, pts, col, width=1):
        self.c.line(pts, fill=col, width=width)
        self.a.line(pts, fill=255, width=width)

    def leaf(self, x, y, length, angle, width, col):
        """先の尖った葉を一枚。根が (x, y)、angle は上向きを 0 とした度"""
        a = math.radians(angle)
        dx, dy = math.sin(a), -math.cos(a)
        px, py = -dy, dx
        tip = (x + dx * length, y + dy * length)
        mid = (x + dx * length * 0.45, y + dy * length * 0.45)
        self.poly([(x, y),
                   (mid[0] + px * width, mid[1] + py * width),
                   tip,
                   (mid[0] - px * width, mid[1] - py * width)], col)

    def done(self):
        """透けたところの色を、見えているところの平均で埋める。黒のままだと mip で縁に暗い輪が出る"""
        px = self.col.load()
        mx = self.mask.load()
        tot = [0, 0, 0]
        n = 0
        for y in range(self.h):
            for x in range(self.w):
                if mx[x, y] > 127:
                    c = px[x, y]
                    tot[0] += c[0]; tot[1] += c[1]; tot[2] += c[2]
                    n += 1
        n = max(1, n)
        bg = Image.new('RGB', (self.w, self.h), (tot[0] // n, tot[1] // n, tot[2] // n))
        rgb = Image.composite(self.col, bg, self.mask)
        rgb.putalpha(self.mask)
        return rgb


def jitter(col, rng, amount=14):
    return tuple(max(0, min(255, int(c + rng.uniform(-amount, amount)))) for c in col)


def mix(a, b, t):
    return tuple(int(a[k] + (b[k] - a[k]) * t) for k in range(3))


# 葉の色。花より暗く沈める
LEAF_DARK = (40, 60, 30)
LEAF_MID = (64, 90, 42)
LEAF_LIGHT = (96, 124, 56)
GREY_DARK = (86, 98, 84)
GREY_LIGHT = (132, 144, 120)


def foliage(b, rng, x0, x1, y0, y1, count, size, dark=LEAF_DARK, light=LEAF_LIGHT, dome=True):
    """
    葉の塊。下ほど暗く、上ほど明るい。dome なら上の輪郭を丸く、x の端ほど低くする。
    葉は奥（暗い）から手前（明るい）へ描いて、塊に前後を出す
    """
    pts = []
    # 320×180 では疎らな葉は点の雑音になる。塊として読めるだけ詰める
    for _ in range(int(count * 2.6)):
        x = rng.uniform(x0, x1)
        t = (x - x0) / max(1.0, x1 - x0)
        top = y0 + (y1 - y0) * (0.55 * (2 * t - 1) ** 2 if dome else 0.0)
        y = rng.uniform(top, y1)
        pts.append((x, y))
    pts.sort(key=lambda p: p[1] + rng.uniform(-20, 20), reverse=False)
    for i, (x, y) in enumerate(pts):
        h = (y - y0) / max(1.0, y1 - y0)
        depth = i / float(max(1, len(pts) - 1))
        col = jitter(mix(light, dark, h * 0.7 + (1 - depth) * 0.3), rng, 10)
        b.leaf(x, y + size * 0.4, size * rng.uniform(0.8, 1.3), rng.uniform(-70, 70), size * 0.38, col)


def stems(b, rng, xs, y_top, y_bot, col, width=2, lean=6):
    for x, top in xs:
        mid = x + rng.uniform(-lean, lean)
        b.line([(x, y_bot), (mid, (y_bot + top) / 2.0), (mid + rng.uniform(-lean, lean) * 0.5, top)], col, width)


def blossom(b, rng, cx, cy, r, petal, centre, rim=None):
    """丸い花を一つ。外に明るい縁、真ん中に暗い芯"""
    b.ell(cx, cy, r, r * 0.9, jitter(petal, rng, 8))
    if rim is not None:
        b.ell(cx - r * 0.2, cy - r * 0.25, r * 0.55, r * 0.45, rim)
    b.ell(cx, cy, r * 0.32, r * 0.3, centre)


def daisy(b, rng, cx, cy, r, petal, centre, droop=0.0):
    """花びらの円盤と真ん中の球。droop で花びらを下へ反らす（エキナセア）"""
    n = 12
    for i in range(n):
        a = 2 * math.pi * i / n
        px = cx + math.cos(a) * r * 0.62
        py = cy + math.sin(a) * r * 0.45 + droop * r * 0.35 * (1 + math.sin(a))
        b.ell(px, py, r * 0.42, r * 0.26, jitter(petal, rng, 10))
    b.ell(cx, cy - droop * r * 0.2, r * 0.36, r * 0.36, centre)


# ---- 背の高い物（1×3 升、128×384） -------------------------------------------

def hollyhock(petal, centre, seed):
    """タチアオイ。太い茎に円盤の花を下から上へ並べる。根元に丸い大きな葉"""
    w, h = UNIT, UNIT * 3
    rng = random.Random(seed)
    b = Brush(w, h)
    foliage(b, rng, 10, 118, h * 0.66, h - 4, 46, 17, LEAF_DARK, LEAF_MID)
    spikes = [(34, 30), (64, 8), (94, 40)]
    for x, top in spikes:
        b.line([(x, h - 10), (x + rng.uniform(-5, 5), top)], (60, 84, 40), 3)
    for x, top in spikes:
        y = h * 0.62
        while y > top + 14:
            t = (y - top) / (h * 0.62 - top)
            r = 7.5 + 3.5 * t
            side = rng.choice((-1, 1))
            b.leaf(x, y + 8, 12, side * 60, 4, LEAF_MID)
            blossom(b, rng, x + side * rng.uniform(1, 5), y, r, petal, centre, rim=mix(petal, (255, 255, 255), 0.35))
            y -= r * 1.35
        for k in range(4):
            b.ell(x + rng.uniform(-3, 3), top + k * 5, 3, 3, (84, 110, 52))
    return b.done()


def delphinium():
    """デルフィニウム。花の終わった穂。緑と枯れ色の莢に、二番花の青を少しだけ"""
    w, h = UNIT, UNIT * 3
    rng = random.Random(211)
    b = Brush(w, h)
    foliage(b, rng, 8, 120, h * 0.70, h - 4, 50, 14, LEAF_DARK, LEAF_MID)
    for i, x in enumerate((24, 50, 78, 104)):
        top = rng.uniform(40, 110)
        b.line([(x, h - 20), (x + rng.uniform(-4, 4), top)], (78, 96, 50), 2)
        y = h * 0.62
        while y > top:
            col = (120, 118, 70) if rng.random() < 0.6 else (96, 110, 60)
            b.ell(x + rng.uniform(-4, 4), y, 3.2, 4.5, col)
            y -= 7
        if i in (1, 2):
            # 二番花。穂の上の方にだけ青い花を少し
            for k in range(6):
                b.ell(x + rng.uniform(-6, 6), top + 10 + k * 7, 5, 4.5, jitter((84, 104, 196), rng, 10))
    return b.done()


def foxglove():
    """ジギタリスとルピナスの葉の株。大きな葉の株に、花の終わった穂を一二本"""
    w, h = UNIT, UNIT * 3
    rng = random.Random(223)
    b = Brush(w, h)
    for x, top in ((46, 90), (86, 140)):
        b.line([(x, h - 40), (x + rng.uniform(-4, 4), top)], (104, 100, 62), 3)
        y = h * 0.66
        while y > top:
            b.ell(x + rng.uniform(-5, 5), y, 3.5, 5, (126, 112, 70) if rng.random() < 0.7 else (150, 110, 150))
            y -= 8
    # 葉の株。ルピナスの掌のような葉とジギタリスの広い葉を低く
    for _ in range(26):
        x = rng.uniform(14, 114)
        y = rng.uniform(h * 0.74, h - 6)
        for k in range(5):
            b.leaf(x, y, rng.uniform(18, 28), -60 + k * 30 + rng.uniform(-8, 8), 5, jitter(LEAF_MID, rng, 12))
    foliage(b, rng, 8, 120, h * 0.80, h - 2, 30, 20, LEAF_DARK, LEAF_MID)
    return b.done()


# ---- 中くらいの物（1×2 升、128×256） -----------------------------------------

def bush_with(flower, seed, leaf_top=0.45, count=9, lo=0.08, hi=0.55, stem=(58, 82, 38)):
    """茂みの上に花を並べる。flower(b, rng, x, y) が花を一つ描く"""
    w, h = UNIT, UNIT * 2
    rng = random.Random(seed)
    b = Brush(w, h)
    xs = []
    for _ in range(count):
        x = rng.uniform(16, 112)
        y = rng.uniform(h * lo, h * hi)
        xs.append((x, y))
    for x, y in xs:
        b.line([(64 + (x - 64) * 0.3, h - 6), (x, y)], stem, 2)
    foliage(b, rng, 6, 122, h * leaf_top, h - 3, 44, 15)
    for x, y in sorted(xs, key=lambda p: -p[1]):
        flower(b, rng, x, y)
    return b.done()


def dahlia(petal, seed):
    """
    ダリア。茎の先の大きな花。一枚の円で描くと茸の傘に見えたので、
    外の花びらの輪と内の輪を重ね、芯へ向かって暗くする。
    **花を大きくしすぎない。** 半径 10 画素の花を 14 輪にしたら、近くで淡いピンクの皿が並んで見えた。
    一回り小さな花を多めに付け、株として読ませる
    """
    def one(b, rng, x, y):
        r = rng.uniform(7, 9)
        dark = mix(petal, (0, 0, 0), 0.35)
        b.ell(x, y, r, r * 0.92, dark)
        for k in range(10):
            a = 2 * math.pi * k / 10 + rng.uniform(-0.1, 0.1)
            b.ell(x + math.cos(a) * r * 0.62, y + math.sin(a) * r * 0.58, r * 0.36, r * 0.30, jitter(petal, rng, 10))
        for k in range(6):
            a = 2 * math.pi * k / 6 + 0.4
            b.ell(x + math.cos(a) * r * 0.3, y + math.sin(a) * r * 0.28, r * 0.26, r * 0.22,
                  mix(jitter(petal, rng, 8), (255, 255, 255), 0.18))
        b.ell(x, y, r * 0.16, r * 0.15, mix(petal, (60, 40, 20), 0.5))
    return bush_with(one, seed, 0.40, 19, 0.08, 0.50)


def rudbeckia():
    def one(b, rng, x, y):
        daisy(b, rng, x, y, rng.uniform(9, 11), (232, 178, 34), (64, 36, 18))
    return bush_with(one, 241, 0.46, 22, 0.10, 0.52)


def echinacea(petal, cone, seed):
    def one(b, rng, x, y):
        daisy(b, rng, x, y, rng.uniform(9, 11), petal, cone, droop=1.0)
    return bush_with(one, seed, 0.50, 16, 0.08, 0.46)


def aster():
    def one(b, rng, x, y):
        for _ in range(5):
            daisy(b, rng, x + rng.uniform(-8, 8), y + rng.uniform(-6, 6), rng.uniform(5, 6.5), (168, 156, 222), (220, 190, 70))
    return bush_with(one, 251, 0.40, 16, 0.12, 0.50)


def allium():
    """アリウム。8 月は花が終わって、細い茎の先に枯れた球が残る"""
    w, h = UNIT, UNIT * 2
    rng = random.Random(257)
    b = Brush(w, h)
    heads = [(rng.uniform(20, 108), rng.uniform(20, 90)) for _ in range(5)]
    for x, y in heads:
        b.line([(x + rng.uniform(-6, 6), h - 4), (x, y)], (150, 138, 92), 2)
    for _ in range(10):
        b.leaf(rng.uniform(30, 98), h - 4, rng.uniform(26, 44), rng.uniform(-60, 60), 3, (132, 124, 80))
    for x, y in heads:
        r = rng.uniform(10, 13)
        b.ell(x, y, r, r, (112, 92, 62))
        for _ in range(26):
            a = rng.uniform(0, 2 * math.pi)
            d = rng.uniform(0, r)
            b.ell(x + math.cos(a) * d, y + math.sin(a) * d, 2.2, 2.2, jitter((178, 152, 104), rng, 16))
    return b.done()


def rosemary():
    """ローズマリー。直立する針葉の株"""
    w, h = UNIT, UNIT * 2
    rng = random.Random(263)
    b = Brush(w, h)
    for _ in range(38):
        x = rng.uniform(18, 110)
        top = h * rng.uniform(0.18, 0.5) + abs(x - 64) * 0.9
        col = jitter((70, 88, 70), rng, 10)
        b.line([(64 + (x - 64) * 0.5, h - 3), (x, top)], (80, 70, 50), 2)
        y = h - 12
        while y > top:
            b.leaf(x, y, 9, rng.choice((-35, 35)), 2, jitter(col, rng, 12))
            y -= 5
    return b.done()


def sweetpea():
    """スイートピー。細い支柱の網を這い上がる葉と、小さな花の房"""
    w, h = UNIT, UNIT * 2
    rng = random.Random(269)
    b = Brush(w, h)
    for x in (14, 64, 114):
        b.line([(x, h - 2), (x, 2)], (120, 100, 70), 2)
    for y in range(12, h, 22):
        b.line([(8, y), (120, y)], (150, 150, 140), 1)
    for _ in range(220):
        x = rng.uniform(6, 122)
        y = rng.uniform(16, h - 4)
        b.leaf(x, y, rng.uniform(8, 12), rng.uniform(-80, 80), 3.5, jitter(LEAF_MID, rng, 14))
    cols = [(236, 150, 186), (170, 60, 130), (246, 240, 240), (150, 120, 210), (224, 110, 130)]
    for _ in range(46):
        x = rng.uniform(10, 118)
        y = rng.uniform(14, h * 0.8)
        col = rng.choice(cols)
        for k in range(3):
            b.ell(x + rng.uniform(-4, 4), y + k * 5, 5.5, 4.5, jitter(col, rng, 8))
    return b.done()


def pelargonium():
    """ペラルゴニウム。丸い葉の塊の上に、赤い花の房（札は鉢の上に立てる）"""
    w, h = UNIT, UNIT * 2
    rng = random.Random(271)
    b = Brush(w, h)
    for _ in range(90):
        x = rng.uniform(10, 118)
        y = rng.uniform(h * 0.45, h - 4)
        b.ell(x, y, rng.uniform(8, 12), rng.uniform(7, 10), jitter((56, 88, 40), rng, 12))
    for _ in range(12):
        x = rng.uniform(20, 108)
        y = rng.uniform(h * 0.25, h * 0.55)
        b.line([(x, y), (x + rng.uniform(-4, 4), h * 0.6)], (70, 96, 44), 2)
        base = rng.choice([(196, 34, 38), (214, 46, 52), (226, 90, 120)])
        for _k in range(9):
            b.ell(x + rng.uniform(-8, 8), y + rng.uniform(-7, 6), 4.5, 4.5, jitter(base, rng, 12))
    return b.done()


# ---- 低い塊（2×1 升、256×128） ------------------------------------------------

def mound(seed, dark, light, count=90, size=12):
    w, h = UNIT * 2, UNIT
    rng = random.Random(seed)
    b = Brush(w, h)
    foliage(b, rng, 6, 250, h * 0.18, h - 2, count, size, dark, light)
    return b, rng


def catmint():
    """
    キャットミント。灰緑の株が外へ倒れ、薄紫の穂が霞のように乗る。
    **穂で株を塗りつぶさない。** 太い濃い紫の穂を詰めたら、芝の縁に沿って濃い青紫の帯が一本通り、
    日陰では紺の塊に沈んだ。穂は細く疎らに、色は灰みの淡い青紫にして、下の灰緑を透かす
    """
    b, rng = mound(281, GREY_DARK, GREY_LIGHT, 120, 11)
    for _ in range(175):
        x = rng.uniform(14, 242)
        t = (x - 128) / 128.0
        y = rng.uniform(h_top(t, 16), 92)
        lean = (x - 128) * 0.05
        col = jitter(mix((138, 138, 210), (166, 168, 214), rng.random()), rng, 10)
        b.line([(x - lean, y + 16), (x + rng.uniform(-2, 2), y)], col, 4)
    return b.done()


def h_top(t, lo):
    return lo + 60 * t * t


def geranium():
    """ハーディー・ゼラニウム（'Rozanne'）。緑の塊に、真ん中の白い青紫の花"""
    b, rng = mound(283, LEAF_DARK, LEAF_LIGHT, 110, 12)
    for _ in range(80):
        x = rng.uniform(12, 244)
        t = (x - 128) / 128.0
        y = rng.uniform(h_top(t, 18), 100)
        b.ell(x, y, 6.5, 6, jitter((112, 100, 204), rng, 10))
        b.ell(x, y, 2, 2, (226, 222, 236))
    return b.done()


def lavender():
    """ラベンダー。灰緑の低い株から、細い棒の紫の穂がたくさん立つ"""
    w, h = UNIT * 2, UNIT
    rng = random.Random(293)
    b = Brush(w, h)
    # **穂は細く、間を空ける。** 太い穂を 200 本詰めたら、テラスの縁の手前で紫の塗り壁になり、
    # 札の四角が透けて見えた。灰緑の株を半分の高さまで盛り、その上に細い穂を立てて、穂の間から株を見せる
    foliage(b, rng, 12, 244, h * 0.36, h - 2, 130, 10, GREY_DARK, GREY_LIGHT)
    for _ in range(190):
        x = rng.uniform(12, 244)
        t = (x - 128) / 128.0
        top = 6 + 36 * t * t + rng.uniform(0, 14)
        foot = 128 + (x - 128) * 0.8
        b.line([(foot, h * 0.6), (x, top + 18)], (116, 130, 100), 1)
        col = jitter(mix((98, 76, 164), (128, 104, 188), rng.random()), rng, 10)
        b.line([(x, top + 20), (x, top)], col, 3)
    return b.done()


def ladysmantle():
    """レディースマントル。丸い葉の塊を、黄緑の泡のような小花が覆う"""
    w, h = UNIT * 2, UNIT
    rng = random.Random(307)
    b = Brush(w, h)
    for _ in range(110):
        x = rng.uniform(14, 242)
        t = (x - 128) / 128.0
        y = rng.uniform(h_top(t, 40), h - 6)
        b.ell(x, y, rng.uniform(12, 16), rng.uniform(8, 11), jitter((96, 124, 58), rng, 10))
    for _ in range(170):
        x = rng.uniform(12, 244)
        t = (x - 128) / 128.0
        y = rng.uniform(h_top(t, 16), 80)
        b.ell(x, y, rng.uniform(5, 8), rng.uniform(4, 6), jitter((196, 204, 92), rng, 12))
    return b.done()


def sage():
    """セージ。灰緑でビロードの楕円の葉の、こんもりした株"""
    w, h = UNIT * 2, UNIT
    rng = random.Random(311)
    b = Brush(w, h)
    for _ in range(360):
        x = rng.uniform(10, 246)
        t = (x - 128) / 128.0
        y = rng.uniform(h_top(t, 14), h - 4)
        col = jitter(mix((104, 116, 96), (150, 160, 136), 1 - (y / h)), rng, 8)
        b.ell(x, y, rng.uniform(6, 9), rng.uniform(4, 6), col)
    return b.done()


def hydrangea():
    """アジサイ。暗い葉の茂みに、大きな球の花房"""
    w, h = UNIT * 2, UNIT
    rng = random.Random(313)
    b = Brush(w, h)
    foliage(b, rng, 8, 248, h * 0.25, h - 2, 120, 14, LEAF_DARK, LEAF_MID)
    for _ in range(17):
        x = rng.uniform(26, 230)
        t = (x - 128) / 128.0
        y = rng.uniform(h_top(t, 22), 70)
        base = rng.choice([(176, 192, 226), (160, 178, 222), (232, 234, 228), (214, 200, 226)])
        r = rng.uniform(15, 19)
        b.ell(x, y, r, r * 0.85, mix(base, (60, 70, 90), 0.25))
        for _k in range(14):
            a = rng.uniform(0, 2 * math.pi)
            d = rng.uniform(0, r * 0.75)
            b.ell(x + math.cos(a) * d - 2, y + math.sin(a) * d * 0.8 - 2, 4.5, 4, jitter(base, rng, 10))
    return b.done()


def filler():
    """地を埋める葉の株。花の縁の株のあいだと根元に置く"""
    b, rng = mound(317, LEAF_DARK, LEAF_LIGHT, 150, 14)
    return b.done()


def ivy():
    """
    アイビー。石垣や壁に貼る、密な葉の塊。
    **形を四角にしない。** 札いっぱいに葉を敷いたら、塀に黒い箱を貼ったように見えた。
    根元から上へ広がる不揃いな塊（円をいくつか重ねた形）に葉を詰め、縁から蔓を垂らす
    """
    w, h = UNIT * 2, UNIT
    rng = random.Random(331)
    b = Brush(w, h)
    blobs = [(128 + rng.uniform(-30, 30), 96 + rng.uniform(-10, 10), rng.uniform(40, 56)) for _ in range(3)]
    blobs += [(rng.uniform(50, 206), rng.uniform(34, 80), rng.uniform(22, 36)) for _ in range(4)]

    def inside(x, y):
        return any(math.hypot(x - bx, (y - by) * 1.3) < br for bx, by, br in blobs)

    for _ in range(900):
        x = rng.uniform(4, 252)
        y = rng.uniform(4, 124)
        if not inside(x, y):
            continue
        col = jitter(mix((40, 66, 36), (96, 128, 66), rng.random()), rng, 6)
        r = rng.uniform(5, 8)
        b.poly([(x, y - r), (x + r, y), (x + r * 0.4, y + r * 0.6), (x - r * 0.4, y + r * 0.6), (x - r, y)], col)
    for _ in range(9):
        x, y = rng.uniform(40, 216), rng.uniform(30, 90)
        for _k in range(8):
            x += rng.uniform(-9, 9)
            y += rng.uniform(-10, 4)
            col = jitter((70, 100, 54), rng, 10)
            b.poly([(x, y - 5), (x + 5, y), (x, y + 4), (x - 5, y)], col)
    return b.done()


# ---- つると木（2×2 升、256×256） ---------------------------------------------

def tangle(seed, count=160, size=14, dark=LEAF_DARK, light=LEAF_LIGHT, spread=None):
    """
    つるの葉の塊（2×2 升）。
    **枝も葉も塊の外へ散らさない。** 輪郭の外に葉を疎らに撒き、枝を塊の外まで引いたら、
    アーチの頭や柱の肩から細い枝が突き出して見えた（オーナーの指摘、2026-09-27）。
    輪郭は円を幾つか重ねた丸い凸凹にし、枝は輪郭より内にだけ引く。
    spread は輪郭の円の中心の散らばり（画素）
    """
    w, h = UNIT * 2, UNIT * 2
    rng = random.Random(seed)
    b = Brush(w, h)
    spread = 40 if spread is None else spread
    blobs = [(128, 128, 98)]
    for _ in range(6):
        blobs.append((128 + rng.uniform(-spread, spread), 128 + rng.uniform(-spread, spread), rng.uniform(46, 62)))

    def room(x, y):
        """輪郭の内への深さ（画素）。負なら外"""
        return max(br - math.hypot(x - bx, y - by) for bx, by, br in blobs)

    for _ in range(12):
        x0, y0 = rng.uniform(70, 186), rng.uniform(70, 186)
        pts = [(x0, y0)]
        for _k in range(5):
            nx, ny = x0 + rng.uniform(-22, 22), y0 + rng.uniform(-22, 22)
            if room(nx, ny) < size * 1.4:
                break
            x0, y0 = nx, ny
            pts.append((x0, y0))
        if len(pts) > 1:
            b.line(pts, (72, 60, 42), 2)
    for _ in range(int(count * 3.4)):
        x = rng.uniform(8, 248)
        y = rng.uniform(8, 248)
        r = room(x, y)
        if r < size * 0.9:
            continue
        d = math.hypot(x - 128, y - 128) / 128.0
        col = jitter(mix(light, dark, min(1.0, d * 0.6 + rng.uniform(0, 0.5))), rng, 10)
        b.leaf(x, y, size * rng.uniform(0.7, 1.0), rng.uniform(0, 360), size * 0.4, col)
    b.room = room
    return b, rng


def roses():
    """つるバラ。淡いピンクと白の、丸く花びらの詰まった花の房"""
    b, rng = tangle(337)
    for _ in range(44):
        x = rng.uniform(24, 232)
        y = rng.uniform(24, 232)
        if b.room(x, y) < 12:
            continue
        base = rng.choice([(238, 186, 196), (244, 214, 214), (246, 240, 232), (238, 186, 196), (214, 120, 150)])
        for _k in range(rng.randint(2, 4)):
            cx, cy = x + rng.uniform(-10, 10), y + rng.uniform(-8, 8)
            r = rng.uniform(7, 9)
            b.ell(cx, cy, r, r, mix(base, (80, 40, 50), 0.22))
            b.ell(cx - 1, cy - 1, r * 0.8, r * 0.78, base)
            b.ell(cx - 2, cy - 2, r * 0.35, r * 0.35, mix(base, (255, 255, 255), 0.4))
    return b.done()


def clematis():
    """クレマチス。平たい星形の紫の花を散らす"""
    b, rng = tangle(347)
    for _ in range(48):
        x = rng.uniform(20, 236)
        y = rng.uniform(20, 236)
        if b.room(x, y) < 12:
            continue
        base = rng.choice([(104, 58, 150), (126, 76, 176), (88, 44, 126)])
        r = rng.uniform(9, 11)
        spin = rng.uniform(0, 1)
        for k in range(6):
            a = 2 * math.pi * (k + spin) / 6
            b.leaf(x, y, r, math.degrees(a), r * 0.36, jitter(base, rng, 10))
        b.ell(x, y, 2.5, 2.5, (230, 220, 170))
    return b.done()


def honeysuckle():
    """ハニーサックル。筒状の花の房。クリームと黄に、外側の淡い紅"""
    b, rng = tangle(353)
    for _ in range(36):
        x = rng.uniform(24, 232)
        y = rng.uniform(24, 232)
        if b.room(x, y) < 13:
            continue
        for k in range(7):
            a = 2 * math.pi * k / 7 + rng.uniform(-0.2, 0.2)
            col = (242, 222, 150) if k % 2 == 0 else (230, 176, 150)
            b.line([(x, y), (x + math.cos(a) * 11, y + math.sin(a) * 11)], jitter(col, rng, 10), 4)
        b.ell(x, y, 4, 4, (236, 206, 120))
    return b.done()


def apple():
    """リンゴの樹冠。密な葉の塊に、赤い実を点々と。札を交差させて丸い樹冠にする"""
    # 暗い緑では、日陰の樹冠が黒い塊に沈んだ。葉は明るめの緑に置く
    b, rng = tangle(359, count=420, size=13, dark=(58, 88, 40), light=(118, 152, 66))
    for _ in range(34):
        x = rng.uniform(26, 230)
        y = rng.uniform(40, 236)
        if b.room(x, y) < 10:
            continue
        r = rng.uniform(5, 6.5)
        b.ell(x, y, r, r, (150, 30, 24))
        b.ell(x - 1.5, y - 1.5, r * 0.65, r * 0.6, (200, 54, 38))
        b.ell(x - 2, y - 2.5, 1.4, 1.2, (240, 170, 120))
    return b.done()


def rose_head(b, rng, cx, cy, base, r):
    """花びらの詰まったバラの花を一つ。影の輪、花、明るい芯"""
    b.ell(cx, cy, r, r, mix(base, (80, 40, 50), 0.22))
    b.ell(cx - 1, cy - 1, r * 0.8, r * 0.78, base)
    b.ell(cx - 2, cy - 2, r * 0.35, r * 0.35, mix(base, (255, 255, 255), 0.4))


def star(b, rng, x, y, r, base):
    """クレマチスの平たい星形の花"""
    spin = rng.uniform(0, 1)
    for k in range(6):
        a = 2 * math.pi * (k + spin) / 6
        b.leaf(x, y, r, math.degrees(a), r * 0.36, jitter(base, rng, 10))
    b.ell(x, y, 2.5, 2.5, (230, 220, 170))


def garland():
    """
    アーチの弧に沿わせるバラとクレマチスの帯（4×1 升、512×128）。
    札の下の辺を弧の内の縁、上の辺を外の縁に当てて、弧に沿って並べる（BuildVillagePlants.ArchPlants）。
    **上の縁は丸い凸凹で止め、枝も葉も外へ突き出さない。** 弧の形が読めるように、帯の厚みを揃える。
    下の縁からは短い房を垂らす（アーチの内へ垂れる）。横に繋げて使うので、左右の端まで葉で埋める。
    誘引した枝は弧に沿って横に這わせる（RHS の誘引の手引き: 枝を支柱に巻き、なるべく横に寝かせる）
    """
    w, h = UNIT * 4, UNIT
    rng = random.Random(367)
    b = Brush(w, h)

    def top(x):
        return 22 + 7 * math.sin(x / 21.0 + 0.7) + 5 * math.sin(x / 8.5 + 2.1)

    def bottom(x):
        return 100 + 4 * math.sin(x / 17.0 + 1.3)

    # 横に這う枝。帯の真ん中あたりを弧に沿って
    for k in range(4):
        y0 = rng.uniform(50, 80)
        pts = []
        for x in range(-8, w + 9, 16):
            pts.append((x, y0 + 8 * math.sin(x / 40.0 + k * 1.7)))
        b.line(pts, (78, 64, 44), 2)
    # 葉。縁の内に収まる所にだけ
    for _ in range(1500):
        x = rng.uniform(-6, w + 6)
        size = rng.uniform(9, 12)
        y = rng.uniform(top(x) + size * 0.9, bottom(x))
        d = abs(y - 62) / 44.0
        col = jitter(mix(LEAF_LIGHT, LEAF_DARK, min(1.0, d * 0.5 + rng.uniform(0, 0.5))), rng, 10)
        b.leaf(x, y, size, rng.uniform(0, 360), size * 0.4, col)
    # 下の縁から垂れる房
    for _ in range(16):
        x0 = rng.uniform(14, w - 14)
        length = rng.uniform(10, 22)
        for k in range(5):
            y = bottom(x0) + length * k / 4.0
            b.leaf(x0 + rng.uniform(-4, 4), y - 6, 9, 180 + rng.uniform(-40, 40), 3.6, jitter(LEAF_MID, rng, 10))
        if rng.random() < 0.6:
            rose_head(b, rng, x0 + rng.uniform(-3, 3), bottom(x0) + length * 0.7, (244, 214, 214), 6)
    # 花。バラ（淡いピンクと白、少し濃いピンク）を房に、クレマチス（紫）を散らす
    # 花は房に寄せ、房のあいだに葉を見せる。敷き詰めると、花の輪の飾りに見えた
    for _ in range(44):
        x = rng.uniform(8, w - 8)
        y = rng.uniform(top(x) + 12, bottom(x) - 6)
        base = rng.choice([(238, 186, 196), (244, 214, 214), (246, 240, 232), (238, 186, 196), (214, 120, 150)])
        for _k in range(rng.randint(2, 3)):
            rose_head(b, rng, x + rng.uniform(-8, 8), y + rng.uniform(-6, 6), base, rng.uniform(6.5, 8.5))
    for _ in range(18):
        x = rng.uniform(8, w - 8)
        y = rng.uniform(top(x) + 12, bottom(x) - 4)
        star(b, rng, x, y, rng.uniform(8, 10), rng.choice([(104, 58, 150), (126, 76, 176), (88, 44, 126)]))
    return b.done()


def potmix():
    """
    鉢の寄せ植え（1×1 升）。丸い葉の塊にペラルゴニウムの房、縁から白いバコパと青いロベリアを垂らす。
    札の下の 3 割が鉢の縁から外へ垂れる分（BuildVillagePlants.Pots が鉢の縁より下に根を置く）
    """
    w, h = UNIT, UNIT
    rng = random.Random(373)
    b = Brush(w, h)
    rim = h * 0.66
    # 垂れる茎
    for _ in range(16):
        x0 = rng.uniform(14, 114)
        side = -1 if x0 < 64 else 1
        pts = [(x0, rim - 4)]
        for k in range(4):
            pts.append((pts[-1][0] + side * rng.uniform(1, 4), pts[-1][1] + rng.uniform(6, 10)))
        b.line(pts, (70, 96, 50), 2)
        for (px, py) in pts[1:]:
            b.ell(px, py, 3, 2.6, jitter((80, 112, 56), rng, 10))
            col = (246, 244, 236) if rng.random() < 0.55 else (74, 88, 196)
            b.ell(px + rng.uniform(-3, 3), py + rng.uniform(-2, 3), 2.4, 2.4, jitter(col, rng, 8))
    # 葉の塊
    for _ in range(70):
        x = rng.uniform(16, 112)
        t = (x - 64) / 64.0
        y = rng.uniform(26 + 18 * t * t, rim + 2)
        b.ell(x, y, rng.uniform(7, 10), rng.uniform(6, 8), jitter((58, 90, 42), rng, 12))
    # 花の房
    for _ in range(9):
        x = rng.uniform(24, 104)
        t = (x - 64) / 64.0
        y = rng.uniform(22 + 16 * t * t, rim - 12)
        base = rng.choice([(222, 60, 86), (236, 120, 150), (246, 236, 236), (196, 40, 52)])
        for _k in range(8):
            b.ell(x + rng.uniform(-6, 6), y + rng.uniform(-5, 4), 3.6, 3.6, jitter(base, rng, 10))
    return b.done()


def obelisk_vine():
    """
    オベリスクに這わせたスイートピー（1×2 升）。先の細い葉の柱に、桃・紫・白の小さな花の房。
    支柱は形（BuildVillageGarden.Obelisk）が持つので、絵には描かない
    """
    w, h = UNIT, UNIT * 2
    rng = random.Random(379)
    b = Brush(w, h)

    def half(y):
        """y での柱の半幅。上ほど細く"""
        return 12 + 44 * (y / float(h)) ** 0.9

    for _ in range(420):
        y = rng.uniform(10, h - 4)
        x = 64 + rng.uniform(-1, 1) * half(y)
        b.leaf(x, y, rng.uniform(8, 11), rng.uniform(-80, 80), 3.5, jitter(mix(LEAF_MID, LEAF_DARK, rng.random() * 0.6), rng, 12))
    cols = [(236, 150, 186), (170, 60, 130), (246, 240, 240), (150, 120, 210), (224, 110, 130)]
    for _ in range(38):
        y = rng.uniform(14, h * 0.86)
        x = 64 + rng.uniform(-0.8, 0.8) * half(y)
        col = rng.choice(cols)
        for k in range(3):
            b.ell(x + rng.uniform(-3, 3), y + k * 4.5, 4.6, 3.8, jitter(col, rng, 8))
    return b.done()


def oak():
    """
    畑の生け垣の並木の楢の樹冠（2×2 升）。丸い塊を幾つか重ねた輪郭に、下ほど暗く上ほど明るい葉を詰め、
    ところどころ空を透かす。**箱を重ねた樹冠をやめる。** 葉の玉を箱三つで組んだら、庭の奥の生け垣の上に
    緑の立方体が並んで見えた（2026-09-27）
    """
    w, h = UNIT * 2, UNIT * 2
    rng = random.Random(389)
    b = Brush(w, h)
    blobs = [(128, 142, 92)]
    for _ in range(8):
        blobs.append((128 + rng.uniform(-70, 70), 120 + rng.uniform(-62, 38), rng.uniform(40, 54)))

    def room(x, y):
        return max(br - math.hypot(x - bx, (y - by) * 1.08) for bx, by, br in blobs)

    # 空の透ける穴
    holes = [(rng.uniform(50, 206), rng.uniform(50, 200), rng.uniform(6, 11)) for _ in range(7)]
    for _ in range(3400):
        x = rng.uniform(4, 252)
        y = rng.uniform(4, 252)
        if room(x, y) < 8:
            continue
        if any(math.hypot(x - hx, y - hy) < hr for hx, hy, hr in holes):
            continue
        t = y / 256.0
        col = jitter(mix((104, 132, 62), (38, 58, 30), min(1.0, t * 0.9 + rng.uniform(0, 0.35))), rng, 8)
        b.leaf(x, y, rng.uniform(9, 13), rng.uniform(0, 360), 4.6, col)
    return b.done()


# ---- アトラス -----------------------------------------------------------------

# (升の x, 升の y, 幅の升, 高さの升, 描く関数)。BuildVillagePlants.cs の Cells と同じ並び
CELLS = [
    (0, 0, 1, 3, lambda: hollyhock((224, 140, 170), (150, 50, 88), 201)),   # 0 タチアオイ（ピンク）
    (1, 0, 1, 3, lambda: hollyhock((242, 234, 228), (214, 160, 176), 203)),  # 1 タチアオイ（白）
    (2, 0, 1, 3, delphinium),                                              # 2 デルフィニウム（花の終わった穂）
    (3, 0, 1, 3, foxglove),                                                # 3 ジギタリスとルピナスの葉の株
    (4, 0, 1, 2, lambda: dahlia((150, 18, 40), 227)),                      # 4 ダリア（濃い赤）
    (5, 0, 1, 2, lambda: dahlia((234, 182, 192), 229)),                    # 5 ダリア（淡いピンク）
    (6, 0, 1, 2, rudbeckia),                                               # 6 ルドベキア
    (7, 0, 1, 2, lambda: echinacea((220, 118, 168), (170, 90, 40), 233)),  # 7 エキナセア（ピンク）
    (0, 3, 1, 2, lambda: echinacea((240, 238, 226), (196, 146, 52), 239)),  # 8 エキナセア（白）
    (1, 3, 1, 2, aster),                                                   # 9 アスター
    (2, 3, 1, 2, allium),                                                  # 10 アリウム（枯れた球）
    (3, 3, 1, 2, rosemary),                                                # 11 ローズマリー
    (4, 3, 1, 2, sweetpea),                                                # 12 スイートピー
    (5, 3, 1, 2, pelargonium),                                             # 13 ペラルゴニウム
    (4, 2, 2, 1, catmint),                                                 # 14 キャットミント
    (6, 2, 2, 1, geranium),                                                # 15 ハーディー・ゼラニウム
    (6, 3, 2, 1, lavender),                                                # 16 ラベンダー
    (6, 4, 2, 1, ladysmantle),                                             # 17 レディースマントル
    (0, 5, 2, 1, sage),                                                    # 18 セージ
    (2, 5, 2, 1, hydrangea),                                               # 19 アジサイ
    (4, 5, 2, 1, filler),                                                  # 20 地を埋める葉
    (6, 5, 2, 1, ivy),                                                     # 21 アイビー
    (0, 6, 2, 2, roses),                                                   # 22 つるバラ
    (2, 6, 2, 2, clematis),                                                # 23 クレマチス
    (4, 6, 2, 2, honeysuckle),                                             # 24 ハニーサックル
    (6, 6, 2, 2, apple),                                                   # 25 リンゴ
    # 下の半分（2026-09-27 に縦を 2048 へ広げた）
    (0, 8, 4, 1, garland),                                                 # 26 アーチの弧のバラとクレマチスの帯
    (4, 8, 1, 1, potmix),                                                  # 27 鉢の寄せ植え
    (5, 8, 1, 2, obelisk_vine),                                            # 28 オベリスクのスイートピー
    (6, 8, 2, 2, oak),                                                     # 29 畑の並木の楢の樹冠
]


def atlas():
    im = Image.new('RGBA', (ATLAS, ATLAS_H), (60, 84, 40, 0))
    for (ux, uy, uw, uh, draw) in CELLS:
        cell = draw()
        assert cell.size == (uw * UNIT, uh * UNIT), (ux, uy, cell.size)
        im.paste(cell, (ux * UNIT, uy * UNIT))
    return im


# ---- 面の繰り返しの絵 ---------------------------------------------------------

def stone():
    """
    コッツウォルズの石灰岩の壁。256 画素で 2 m 四方（Texel 0.5）。
    高さ 10〜20 cm の段に、長さの揃わない石を積む。石ごとに色を揺らし、目地は細く暗く
    """
    size = (256, 256)
    rng = random.Random(401)
    im = Image.new('RGB', size, (150, 124, 84))
    w = D.Wrap(im)
    y = 0
    while y < 256:
        hh = rng.choice((13, 16, 19, 22, 26))
        if y + hh > 256:
            hh = 256 - y
        x = rng.uniform(0, 40)
        while x < 256 + 40:
            ww = rng.uniform(26, 66)
            base = jitter((196, 160, 100), rng, 20)
            base = mix(base, (170, 150, 120), rng.random() * 0.35)
            w.rect([x + 1, y + 1, x + ww - 2, y + hh - 2], fill=base)
            w.rect([x + 1, y + hh - 4, x + ww - 2, y + hh - 2], fill=mix(base, (70, 55, 35), 0.35))
            w.rect([x + 2, y + 1, x + ww - 3, y + 3], fill=mix(base, (255, 240, 210), 0.20))
            x += ww
        y += hh
    grain = D.spread(D.clouds(size, 403, 4, 1.0), 2.4)
    D.shade(im, grain, 0.16)
    D.shade(im, D.noise(size, 405, 100, 156), 0.10)
    return im


def slate():
    """
    コッツウォルズの石版の屋根。256 画素で 2 m 四方。段ごとに下の縁へ暗い影、
    石版ごとに灰茶を揺らし、黄色い地衣を点々と
    """
    size = (256, 256)
    rng = random.Random(411)
    im = Image.new('RGB', size, (70, 64, 58))
    w = D.Wrap(im)
    course = 21
    for row in range(0, 256 // course + 1):
        y = row * course
        x = rng.uniform(0, 30)
        while x < 256 + 30:
            ww = rng.uniform(22, 44)
            base = jitter((122, 112, 98), rng, 14)
            w.rect([x + 1, y, x + ww - 1, y + course - 1], fill=base)
            w.rect([x + 1, y + course - 5, x + ww - 1, y + course - 1], fill=mix(base, (30, 26, 22), 0.55))
            x += ww
    for _ in range(90):
        x, y = rng.uniform(0, 256), rng.uniform(0, 256)
        r = rng.uniform(1.5, 4)
        w.ellipse([x - r, y - r * 0.7, x + r, y + r * 0.7], fill=jitter((168, 156, 96), rng, 14))
    D.shade(im, D.spread(D.clouds(size, 413, 4, 1.2), 2.0), 0.18)
    return im


def brick():
    """煉瓦の小路。128 画素で 1 m 四方（Texel 1）。小路を横切る長手積みで、目地に苔"""
    size = (128, 128)
    rng = random.Random(421)
    im = Image.new('RGB', size, (96, 96, 70))
    w = D.Wrap(im)
    bh = 128 / 9.0
    for row in range(9):
        y = row * bh
        off = (row % 2) * 14.0
        x = -off
        while x < 128:
            base = jitter((150, 74, 52), rng, 16)
            base = mix(base, (120, 96, 80), rng.random() * 0.3)
            w.rect([x + 1, y + 1, x + 27, y + bh - 1.5], fill=base)
            x += 28.5
    D.shade(im, D.spread(D.clouds(size, 423, 3, 1.0), 2.0), 0.2)
    return im


def flag():
    """敷石（ヨークストーン）。256 画素で 2 m 四方。大小の四角を目地で区切る"""
    size = (256, 256)
    rng = random.Random(431)
    im = Image.new('RGB', size, (74, 70, 60))
    w = D.Wrap(im)
    rows = [0, 70, 118, 190, 256]
    for r in range(len(rows) - 1):
        y0, y1 = rows[r], rows[r + 1]
        x = rng.uniform(0, 50)
        while x < 256 + 50:
            ww = rng.uniform(50, 104)
            base = jitter((170, 160, 136), rng, 14)
            w.rect([x + 2, y0 + 2, x + ww - 2, y1 - 2], fill=base)
            x += ww
    D.shade(im, D.spread(D.clouds(size, 433, 4, 1.2), 2.2), 0.16)
    D.shade(im, D.noise(size, 435, 100, 156), 0.08)
    return im


def hedge():
    """刈り込んだ生け垣とツゲの葉。128 画素で 1 m 四方。繰り返しても継ぎ目が出ないよう畳んで描く"""
    size = (128, 128)
    rng = random.Random(441)
    im = Image.new('RGB', size, (34, 52, 28))
    w = D.Wrap(im)
    # 暗い緑では、日陰の生け垣が東屋の後ろで黒い壁に見えた。明るみを一段上げる
    for _ in range(900):
        x, y = rng.uniform(0, 128), rng.uniform(0, 128)
        r = rng.uniform(2.5, 4.5)
        col = jitter(mix((46, 70, 36), (104, 134, 64), rng.random() ** 1.2), rng, 6)
        w.ellipse([x - r, y - r * 0.7, x + r, y + r * 0.7], fill=col)
    D.shade(im, D.spread(D.clouds(size, 443, 3, 1.0), 2.0), 0.22)
    return im


def boards():
    """木の塀と物置の板。128 画素で 1 m 四方。縦の板を 15 cm ごとに、隙間を暗く"""
    size = (128, 128)
    rng = random.Random(451)
    im = Image.new('RGB', size, (40, 32, 24))
    w = D.Wrap(im)
    x = 0.0
    while x < 128:
        ww = 19.2
        base = jitter((112, 92, 68), rng, 10)
        w.rect([x + 1, 0, x + ww - 1.5, 127], fill=base)
        x += ww
    grain = D.spread(D.clouds((128, 128), 453, 3, 0.6), 2.0)
    D.shade(im, grain, 0.14)
    return im


def thatch():
    """
    家 B の茅葺きの屋根。256 画素で 2 m 四方（Texel 0.5）。縦（絵の上下）が屋根の流れ。
    麦藁の束の端を段に葺いた面で、束ごとに色を揺らし、段の下の縁へ暗い影。
    何年か経った茅なので、金色ではなく灰色がかった麦藁色に沈め、ところどころ苔で緑に
    """
    size = (256, 256)
    rng = random.Random(461)
    im = Image.new('RGB', size, (104, 88, 60))
    w = D.Wrap(im)
    course = 32
    for row in range(0, 256 // course + 1):
        y = row * course
        x = rng.uniform(0, 8)
        while x < 256 + 8:
            ww = rng.uniform(5, 11)
            base = jitter((146, 124, 88), rng, 7)
            base = mix(base, (118, 112, 98), rng.random() * 0.4)
            w.rect([x, y, x + ww - 1, y + course - 1], fill=base)
            # 藁の筋。上から下へ細く
            for _ in range(3):
                sx = x + rng.uniform(0, ww)
                w.line([(sx, y + 2), (sx + rng.uniform(-1.5, 1.5), y + course - 3)],
                       fill=mix(base, (190, 170, 120), 0.35), width=1)
            w.rect([x, y + course - 4, x + ww - 1, y + course - 1], fill=mix(base, (40, 32, 22), 0.28))
            x += ww
    for _ in range(14):
        x, y = rng.uniform(0, 256), rng.uniform(0, 256)
        r = rng.uniform(3, 7)
        w.ellipse([x - r, y - r * 0.5, x + r, y + r * 0.5], fill=jitter((92, 98, 60), rng, 10))
    D.shade(im, D.spread(D.clouds(size, 463, 4, 1.4), 2.0), 0.20)
    return im


def redbrick():
    """
    家 C の赤煉瓦の壁。128 画素で 1 m 四方（Texel 1）。段は 75 mm で 13 段と少し、
    フランドル積み（一段に長手と小口を交互に）。目地は明るい灰。焼きの揺らぎで煉瓦ごとに色を振り、
    ところどころ焦げた暗い小口を混ぜる
    """
    size = (128, 128)
    rng = random.Random(471)
    im = Image.new('RGB', size, (168, 160, 146))
    w = D.Wrap(im)
    rows = 13
    bh = 128.0 / rows
    long_, head = 23.5, 11.0
    for row in range(rows):
        y = row * bh
        x = -(row % 2) * (long_ + head) * 0.5
        k = 0
        while x < 128 + 40:
            ww = long_ if k % 2 == 0 else head
            base = jitter((150, 66, 44), rng, 18)
            if k % 2 == 1 and rng.random() < 0.35:
                base = mix(base, (70, 38, 34), 0.5)
            base = mix(base, (176, 110, 80), rng.random() * 0.25)
            w.rect([x + 1, y + 1, x + ww - 1, y + bh - 1.2], fill=base)
            x += ww + 1.0
            k += 1
    D.shade(im, D.spread(D.clouds(size, 473, 3, 1.0), 2.0), 0.14)
    return im


ROAD_W = 256
ROAD_H = 512
# 路地の幅（m）と、絵の縦の長さ（m）。BuildVillage.RoadWide・RoadRepeat と揃える
ROAD_M = 4.6
ROAD_LEN = 9.2


def road():
    """
    村の路地。舗装しない（オーナー、2026-09-26）。横（絵の左右）が路地の幅 4.6 m、縦が路地に沿った 9.2 m で、
    縦にだけ繰り返す。場面 8 の最後の帯の土（make-drive.py の dirt と rut）と同じ系統の色で、
    踏み固めた土に砂利を混ぜ、二本の轍と真ん中の草の筋を絵の中に描き込む。
    轍と草の筋は縦の繰り返しに合わせてゆるく揺らす（揺れの周期を絵の縦の長さで割り切る）ので、継ぎ目が出ない。
    縁は砂利が溜まり、端は路肩の芝へ草の房でほどける
    """
    w, h = ROAD_W, ROAD_H
    size = (w, h)
    rng = random.Random(481)
    px_per_m = w / ROAD_M

    def x_of(m):
        return w * 0.5 + m * px_per_m

    grain = Image.blend(D.noise(size, 4811, 100, 186, 0.7), D.clouds(size, 4813, 5, 1.8), 0.5)
    im = D.tint(size, grain, (92, 72, 48), (186, 158, 118))
    wr = D.Wrap(im)

    def wobble(y, amp, k, phase):
        return amp * math.sin(2 * math.pi * (y / float(h) * k + phase))

    # 轍。土より少し明るく、踏み固めて滑らか。**段ごとに色を振らない。** 横の細い帯を色を変えて重ねたら、
    # 轍が横縞の板張りに見えた。一色の帯を置き、粒は後の斑でまとめて付ける
    for side in (-1, 1):
        pts_l = []
        pts_r = []
        for y in range(-4, h + 5, 4):
            cx = x_of(side * 0.95) + wobble(y, 3.0, 2, 0.1 * side) + wobble(y, 1.2, 5, 0.3)
            half = 0.27 * px_per_m + wobble(y, 1.5, 3, 0.2 * side)
            pts_l.append((cx - half, y))
            pts_r.append((cx + half, y))
        wr.d.polygon(pts_l + pts_r[::-1], fill=(162, 138, 100))
    # 真ん中の草の筋。細く疎らに。緑は路肩の芝より褪せた色
    for _ in range(700):
        y = rng.uniform(0, h)
        cx = x_of(0.0) + wobble(y, 3.5, 2, 0.6) + rng.gauss(0, 0.09 * px_per_m)
        r = rng.uniform(1.0, 2.4)
        col = mix((74, 88, 46), (132, 122, 76), rng.random() * 0.7)
        wr.ellipse([cx - r, y - r * 1.5, cx + r, y + r * 1.5], fill=jitter(col, rng, 6))
    # 縁の砂利の溜まりと、路肩の芝へほどける房
    for side in (-1, 1):
        for _ in range(900):
            y = rng.uniform(0, h)
            d = abs(rng.gauss(0, 0.28)) * px_per_m
            cx = x_of(side * 2.3) - side * d
            r = rng.uniform(0.8, 2.0)
            v = rng.randint(128, 196)
            wr.ellipse([cx - r, y - r * 0.8, cx + r, y + r * 0.8], fill=(v, v - 8, v - 26))
        for _ in range(420):
            y = rng.uniform(0, h)
            cx = x_of(side * 2.3) - side * abs(rng.gauss(0, 0.12)) * px_per_m
            r = rng.uniform(1.2, 2.6)
            wr.ellipse([cx - r, y - r * 1.5, cx + r, y + r * 1.5], fill=jitter((66, 88, 42), rng, 10))
    # 土の小石
    for _ in range(420):
        x, y = rng.uniform(0, w), rng.uniform(0, h)
        r = rng.uniform(0.8, 2.2)
        v = rng.randint(140, 206)
        wr.ellipse([x - r, y - r * 0.8, x + r, y + r * 0.8], fill=(v, v - 12, v - 32))
    im = Image.blend(im, D.tile_blur(im, 1.0), 0.45)
    im = D.shade(im, D.noise(size, 4819, 100, 156, 0.5), 0.14)
    im = D.shade(im, D.blot(size, 4817, 18, 46, 36, 9.0), 0.20)
    return im


def save(im, name):
    path = os.path.join(OUT, name + '.png')
    im.save(path)
    print('%-24s %dx%d' % (os.path.basename(path), im.size[0], im.size[1]))


def main():
    import sys
    if not os.path.isdir(OUT):
        os.makedirs(OUT)
    save(atlas(), 'VillageFlora')
    if 'flora' in sys.argv[1:]:
        return
    save(stone(), 'VillageStone')
    save(slate(), 'VillageSlate')
    save(brick(), 'VillageBrick')
    save(flag(), 'VillageFlag')
    save(hedge(), 'VillageHedge')
    save(boards(), 'VillageBoards')
    save(thatch(), 'VillageThatch')
    save(redbrick(), 'VillageRedBrick')
    save(road(), 'VillageRoad')


if __name__ == '__main__':
    main()
