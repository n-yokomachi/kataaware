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


def hblur(im, radius):
    """横だけ畳んでぼかす。縦へ繰り返さない絵（麦の札）に使う"""
    w, h = im.size
    big = Image.new(im.mode, (w * 3, h))
    for i in range(3):
        big.paste(im, (w * i, 0))
    return big.filter(ImageFilter.GaussianBlur(radius)).crop((w, 0, w * 2, h))


class Pen(object):
    """
    明暗と α へ同時に描く。横だけ畳むので、左右は繋がり上下は繋がらない。

    麦の札のように、下端が根で上端が穂先と決まっている絵に使う。
    上下へ畳むと、穂が絵の下から生えてくる
    """

    def __init__(self, col, mask):
        self.c = ImageDraw.Draw(col)
        self.a = ImageDraw.Draw(mask)
        self.w = col.size[0]

    @staticmethod
    def _tone(v):
        return max(0, min(255, int(v)))

    def line(self, xy, tone, width=1):
        v = self._tone(tone)
        for dx in (-self.w, 0, self.w):
            pts = [(x + dx, y) for (x, y) in xy]
            self.c.line(pts, fill=(v, v, v), width=width)
            self.a.line(pts, fill=255, width=width)

    def poly(self, xy, tone):
        v = self._tone(tone)
        for dx in (-self.w, 0, self.w):
            pts = [(x + dx, y) for (x, y) in xy]
            self.c.polygon(pts, fill=(v, v, v))
            self.a.polygon(pts, fill=255)


def level(im, mask, target):
    """
    α の残っているところだけを見て、その平均をこの明るさへ寄せる。

    aim と違って α のある絵に使う。透けたところまで平均に数えると、
    抜いたぶん暗い方へ引かれて、絵ぜんたいが持ち上がってしまう
    """
    px = im.load()
    mx = mask.load()
    w, h = im.size
    tot = 0.0
    n = 0
    for y in range(h):
        for x in range(w):
            if mx[x, y] > 0:
                tot += px[x, y][0]
                n += 1
    if n == 0:
        return im
    gain = target / max(1.0, tot / n)
    for y in range(h):
        for x in range(w):
            c = px[x, y]
            px[x, y] = tuple(min(255, int(c[k] * gain + 0.5)) for k in range(3))
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


# ---- 帯 4。畑と未舗装路 -------------------------------------------------

def wheat():
    """
    麦の札の絵。**α が形そのもの。**

    箱に麦の絵を貼っても、輪郭は箱のままになる。天面が平らに切れて、株のあいだが
    覗けない。麦が麦に見えるのは面の絵柄ではなく輪郭で、穂先の凸凹と株の隙間が
    それを作っている。そこで形を α に持たせ、交差させた札（BuildDrive.Furrows）へ
    貼って、シェーダーが閾値で抜く。三角も箱の 12 枚から 2 枚へ減る。

    横は繰り返す。札ごとに u をずらすので、同じ並びが隣へ続かない。
    縦は繰り返さない。下端が根、上端が穂の芒で、上へ行くほど疎になる。
    この疎になり方がそのまま畑の稜線の毛羽立ちになる。

    **色はここが持たない。** 黄金色は WheatMat の根元色と穂先色が持っていて、
    この絵は明暗と α だけを預かる。明暗の平均は α の残っているところで中庸（128）に
    寄せてあり、シェーダーが 2 倍して albedo に掛ける。透けたところの rgb も
    同じ 128 で埋める。黒のまま残すと、mip で縁に暗い輪が出る
    """
    size = (SIZE, SIZE)
    rng = random.Random(3307)
    col = Image.new('RGB', size, (128, 128, 128))
    mask = Image.new('L', size, 0)
    pen = Pen(col, mask)

    def py(u):
        """下端を 0、上端を 1 とした高さを画素の y に直す"""
        return (SIZE - 1) * (1.0 - u)

    # 根元の下草。株の付け根を塞ぐ。ここが抜けていると、
    # 近くの札の下から畑の地がそのまま覗いて、麦が宙に浮いて見える。
    #
    # **本数と丈を増やした。** 130 本・丈 0.30 では、札を二枚交差させて重ねても
    # 光の 4 割が素通りして、畑の地が株のあいだからそのまま見えていた。
    # 麦畑を横から見たとき、地面が見えるのは足元の一列だけで、その先は
    # 茎の重なりで詰まっている。密にするのはこの帯で、穂の側ではない
    for _ in range(190):
        x = rng.uniform(0, SIZE)
        tone = rng.randint(48, 104)
        pen.line([(x, py(-0.02)), (x + rng.uniform(-5.5, 5.5), py(rng.uniform(0.10, 0.42)))],
                 tone, width=rng.randint(1, 3))

    # 奥の株。細く暗い茎だけを疎らに立てて、手前の株のあいだを埋める。
    # **手前の株を太らせて埋めてはいけない。** 太らせると穂の輪郭が鈍って、
    # 低い描画解像度では麦ではなく穂綿の塊になる。奥へもう一列置けば、
    # 覆いは増えるのに輪郭は変わらず、そのうえ札 1 枚の中に前後が出る
    for i in range(9):
        x0 = (i + 0.5 + rng.uniform(-0.45, 0.45)) * SIZE / 9.0
        lean = rng.uniform(-11.0, 11.0)
        top = rng.uniform(0.34, 0.62)
        tone = rng.randint(40, 72)
        pts = [(x0 + lean * (k / 9.0) ** 2, py(top * k / 9.0)) for k in range(10)]
        pen.line(pts, tone, width=2)
        for _ in range(rng.randint(1, 2)):
            lu = rng.uniform(0.12, top * 0.8)
            lx = x0 + lean * (lu / max(top, 1e-3)) ** 2
            out = (1 if rng.random() < 0.5 else -1) * rng.uniform(6.0, 14.0)
            tip = lu + rng.uniform(0.08, 0.18)
            pen.poly([(lx - 1.0, py(lu)),
                      (lx + out * 0.70, py(lu + (tip - lu) * 0.50)),
                      (lx + out, py(tip)),
                      (lx + out * 0.42, py(lu + (tip - lu) * 0.44)),
                      (lx + 1.5, py(lu))], tone)

    # 株。奥から手前へ描く。手前ほど明るく太くして、1 枚の札の中にも前後を出す
    stalks = 13
    order = list(range(stalks))
    rng.shuffle(order)
    for rank, i in enumerate(order):
        deep = rank / float(stalks - 1)
        x0 = (i + 0.5 + rng.uniform(-0.38, 0.38)) * SIZE / float(stalks)
        lean = rng.uniform(-13.0, 13.0)
        base = rng.uniform(0.40, 0.60)          # 穂の付け根の高さ
        ear = rng.uniform(0.16, 0.26)           # 穂の丈
        stem = int(56 + 64 * deep + rng.uniform(-10, 14))
        thick = 3 if deep > 0.55 else 2
        pts = []
        for k in range(10):
            t = k / 9.0
            pts.append((x0 + lean * t * t, py(base * t)))
        pen.line(pts, stem, width=thick)
        # 葉。茎だけだと針金を並べたように見える。**札の中ほどの嵩はこれが持つ。**
        # 0〜2 枚では茎と茎のあいだが空いたままで、そこが畑の透け方をそのまま決めていた
        for _ in range(rng.randint(2, 4)):
            lu = rng.uniform(0.08, 0.44)
            lx = x0 + lean * (lu / base) ** 2
            out = (1 if rng.random() < 0.5 else -1) * rng.uniform(8.0, 20.0)
            tip = lu + rng.uniform(0.12, 0.26)
            pen.poly([(lx - 1.0, py(lu)),
                      (lx + out * 0.70, py(lu + (tip - lu) * 0.50)),
                      (lx + out, py(tip)),
                      (lx + out * 0.42, py(lu + (tip - lu) * 0.44)),
                      (lx + 1.5, py(lu))], int(stem * 1.12))
        # 穂。**ここが麦と草を分ける。** 穂軸に沿って小穂を左右へ振り分け、
        # それぞれを斜め上へ向けると、麦の穂の矢筈が出る
        bx = x0 + lean
        tipx = bx + lean * rng.uniform(0.25, 0.70)
        wide = rng.uniform(4.4, 7.0)
        grain = int(148 + 78 * deep + rng.uniform(-16, 22))
        pen.line([(bx, py(base)), (tipx, py(base + ear))], int(grain * 0.66), width=2)
        rows = rng.randint(7, 10)
        for k in range(rows):
            t = k / float(rows - 1)
            cx = bx + (tipx - bx) * t
            cu = base + ear * t
            half = wide * (0.55 + 0.45 * math.sin(math.pi * min(1.0, 0.22 + 0.78 * t))) * (1.0 - 0.45 * t)
            for s in (-1, 1):
                pen.line([(cx + s * half * 0.18, py(cu)),
                          (cx + s * half, py(cu + ear * 0.15))],
                         grain + rng.randint(-14, 14) + int(20 * t),
                         width=3 if deep > 0.45 else 2)
        # 芒。穂の先から跳ねる細い毛。**上端をぎざぎざにしているのはこれ。**
        # 1 画素の線なので遠くでは消えるが、消えたぶんは mip の α が拾う
        for _ in range(rng.randint(3, 6)):
            pen.line([(tipx + rng.uniform(-2.0, 2.0), py(base + ear * rng.uniform(0.55, 1.0))),
                      (tipx + rng.uniform(-6.5, 6.5), py(base + ear + rng.uniform(0.02, 0.10)))],
                     int(grain * 0.92), width=1)

    # ぼかしは明暗だけ。α をぼかすと、閾値で抜いた縁が距離で太ったり痩せたりする
    col = Image.blend(col, hblur(col, 0.7), 0.35)
    col = level(col, mask, 128)
    # **明暗の幅を狭める。** 描いたままだと穂が 255 近くまで行き、シェーダーが 2 倍して
    # 穂先の色（0.935）に掛けた先が振り切れて白く飛ぶ。描画解像度が 1/3 なので、
    # 飛んだ画素は隣と混ざらずそのまま残り、畑が金銀の砂を撒いたようにちらつく。
    # 株を株として見せているのは明暗ではなく α の抜けなので、幅は詰めてよい
    cp = col.load()
    for y in range(SIZE):
        for x in range(SIZE):
            v = int(128 + (cp[x, y][0] - 128) * 0.65)
            cp[x, y] = (v, v, v)
    # 透けたところは平均色で塗り直す。mip が縁の外の色を混ぜても明るさが動かない
    cp = col.load()
    mp = mask.load()
    for y in range(SIZE):
        for x in range(SIZE):
            if mp[x, y] == 0:
                cp[x, y] = (128, 128, 128)
    im = col.convert('RGBA')
    im.putalpha(mask)
    return im


def field():
    """
    畑の地。株のあいだから覗く面と、株の届かない遠くの丘を覆う絵。

    真上からではなく浅い角度で見るので、条播きの筋を道と同じ向きに通す。
    uv は道を横切る向きが u、道に沿う向きが v（BuildDrive.FieldTexel）なので、
    筋は縦線として描く。上下左右へ繰り返す。

    **株の絵と同じく、色はここが持たない。** 地も株と同じシェーダーで塗るので、
    黄金色は FieldMat の色が持つ。平均は中庸（128）
    """
    size = (SIZE, SIZE)
    rng = random.Random(9151)
    # 熟れ具合のむら。これが遠くの丘に斑を作って、一枚の板に見せない
    patch = clouds(size, 2237, 5, 2.4)
    im = tint(size, patch, (84, 84, 84), (176, 176, 176))
    w = Wrap(im)
    # 条播きの筋。0.6 m 間隔にあたる 23 画素ごと。
    # **薄く通す。** 濃く引くと、浅い角度で見たときに畑ではなく板張りに見える
    for i in range(11):
        x = i * SIZE / 11.0 + rng.uniform(-2.0, 2.0)
        w.line([(x, -4), (x + rng.uniform(-6, 6), SIZE + 4)], fill=(186, 186, 186), width=1)
        w.line([(x + 9, -4), (x + 9 + rng.uniform(-6, 6), SIZE + 4)], fill=(96, 96, 96), width=1)
    im = Image.blend(im, tile_blur(im, 2.0), 0.75)
    w = Wrap(im)
    # 穂の粒。筋だけだと縞の板になるので、細かい点を撒いて面を荒らす。
    # **明るさの幅は詰める。** 地は株の陰として暗く塗る（BuildDrive.FieldMat）ので、
    # 235 の粒はシェーダーが二倍した先で 1.84 倍になり、暗い地の上に
    # 白い粒が散って見える。畑の地ではなく砂利を撒いた面に見えた
    for _ in range(1100):
        x, y = rng.randrange(SIZE), rng.randrange(SIZE)
        v = rng.randint(142, 190)
        w.line([(x, y), (x + rng.uniform(-1, 1), y - rng.randint(2, 5))], fill=(v, v, v), width=1)
    im = shade(im, blot(size, 7717, 14, 46, 46, 9.0), 0.34)
    return aim(im, (128, 128, 128))


def dirt():
    """
    未舗装路の土。乾いて白茶けた地面に、小石と車の擦った跡。

    **灰にしない。** 朝日は 11 度から薙ぐので路面に直に当たる光は弱く、
    青い空の環境光が勝つ。絵の側を土の色へ振り切っておかないと、
    黄金色の畑のあいだを灰色の帯が抜けていくことになる
    """
    size = (SIZE, SIZE)
    rng = random.Random(6619)
    grain = Image.blend(noise(size, 1811, 96, 190, 0.6), clouds(size, 5507, 5, 1.8), 0.55)
    im = tint(size, grain, (76, 56, 34), (188, 154, 108))
    w = Wrap(im)
    # 小石。粒を置いて、上側だけ明るくする
    for _ in range(140):
        x, y = rng.randrange(SIZE), rng.randrange(SIZE)
        r = rng.uniform(1.2, 3.4)
        v = rng.randint(150, 210)
        w.ellipse([x - r, y - r * 0.8, x + r, y + r * 0.8], fill=(v, v - 14, v - 34))
        w.ellipse([x - r, y - r * 0.9, x + r * 0.5, y - r * 0.1],
                  fill=(min(255, v + 34), v + 16, v - 12))
    # 引きずった跡。道は z 方向に流れるので筋も縦へ向くが、
    # 端まで通すと板目になる。短く切って途中で絶えさせる
    for _ in range(22):
        x = rng.randrange(SIZE)
        y = rng.randrange(SIZE)
        ln = rng.randint(24, 80)
        v = rng.randint(74, 112)
        w.line([(x, y), (x + rng.uniform(-6, 6), y + ln)],
               fill=(v, v - 8, v - 18), width=rng.randint(1, 2))
    im = Image.blend(im, tile_blur(im, 1.4), 0.55)
    im = shade(im, blot(size, 4231, 16, 40, 40, 8.0), 0.26)
    return aim(im, (124, 94, 58))


def rut():
    """
    車輪の通る筋。**土より明るい。**

    夏の朝の乾いた農道で、轍は埃が磨かれて白茶ける。湿った暗い轍として描くと、
    黄金色の畑のあいだを暗い溝が抜けることになり、道が水路に見える。
    踏み固められているので小石は路肩へ弾かれて残らない
    """
    size = (SIZE, SIZE)
    rng = random.Random(2903)
    grain = Image.blend(noise(size, 3313, 108, 176, 1.1), clouds(size, 7727, 5, 2.6), 0.6)
    im = tint(size, grain, (96, 72, 46), (206, 174, 128))
    w = Wrap(im)
    # 轍の溝。縦に通るが、板目に見えないよう本数を抑えて薄く引く
    for _ in range(14):
        x = rng.randrange(SIZE)
        v = rng.randint(58, 92)
        w.line([(x, -4), (x + rng.uniform(-9, 9), SIZE + 4)], fill=(v, v - 6, v - 14), width=1)
    for _ in range(9):
        x = rng.randrange(SIZE)
        v = rng.randint(138, 176)
        w.line([(x, -4), (x + rng.uniform(-9, 9), SIZE + 4)], fill=(v, v - 12, v - 30), width=1)
    # 泥のはねと乾いた斑。踏み固めた面なので粒立ちは残さない
    for _ in range(70):
        x, y = rng.randrange(SIZE), rng.randrange(SIZE)
        r = rng.uniform(1.5, 5.0)
        v = rng.randint(96, 150)
        w.ellipse([x - r, y - r * 0.6, x + r, y + r * 0.6], fill=(v, v - 10, v - 24))
    im = Image.blend(im, tile_blur(im, 2.2), 0.7)
    im = shade(im, blot(size, 8837, 12, 52, 38, 10.0), 0.24)
    return aim(im, (148, 116, 74))


def torn():
    """
    千切れ雲。原作の「真っ白な千切れ雲と、まだ薄青い高い空」。

    層雲の一枚板ではなく、切れ切れの積雲にする。芯を白く抜いて縁を毛羽立たせ、
    小さな千切れも散らす。空の板は下から見上げるので、透けるところは
    まるごと抜く（α 0）。上下左右へ繰り返す
    """
    size = (512, 512)
    rng = random.Random(5477)
    a = Image.new('L', size, 0)
    w = Wrap(a)

    def puff(cx, cy, span, tall, seed):
        """積雲ひとつ。丸を重ねて、上を盛り上げ下を平らにする"""
        r2 = random.Random(seed)
        lumps = int(span / 7) + 5
        for _ in range(lumps):
            t = r2.uniform(-1.0, 1.0)
            x = cx + t * span
            lift = (1.0 - t * t) * tall
            r = r2.uniform(span * 0.14, span * 0.34) * (0.5 + 0.7 * (1.0 - abs(t)))
            y = cy - r2.uniform(0.0, lift)
            w.ellipse([x - r, y - r * 0.82, x + r, y + r * 0.82], fill=r2.randint(228, 255))
        # 底。平らに切り揃える
        for _ in range(lumps // 2):
            t = r2.uniform(-0.9, 0.9)
            x = cx + t * span
            r = r2.uniform(span * 0.10, span * 0.22)
            w.ellipse([x - r, cy - r * 0.34, x + r, cy + r * 0.30], fill=r2.randint(196, 236))

    for i in range(5):
        puff(rng.randrange(512), rng.randrange(512), rng.uniform(46, 96), rng.uniform(30, 64), 700 + i)
    for i in range(9):
        puff(rng.randrange(512), rng.randrange(512), rng.uniform(16, 34), rng.uniform(9, 20), 800 + i)
    # 千切れ。小さな欠片をばら撒く
    for _ in range(40):
        x, y = rng.randrange(512), rng.randrange(512)
        r = rng.uniform(2.5, 9.0)
        w.ellipse([x - r, y - r * rng.uniform(0.28, 0.5), x + r, y + r * rng.uniform(0.28, 0.5)],
                  fill=rng.randint(120, 210))
    # 縁を毛羽立たせる。斑を掛けてから縁だけ削る
    a = tile_blur(a, 2.2)
    ragged = clouds(size, 6121, 5, 1.6)
    ap = a.load()
    rp = ragged.load()
    for y in range(512):
        for x in range(512):
            v = ap[x, y]
            if v == 0:
                continue
            k = 0.45 + rp[x, y] / 255.0 * 1.15
            ap[x, y] = min(255, max(0, int(v * k)))
    a = tile_blur(a, 1.1)
    # 芯は白く抜く。α が 200 止まりだと、真っ白な雲にならない
    a = a.point(lambda v: min(255, int(v * 1.45)))
    # 底はわずかに影る。真っ白のままだと切り絵に見える
    sh = clouds(size, 9311, 4, 2.2).point(lambda v: 210 + v // 6)
    body = Image.composite(Image.new('RGB', size, (255, 255, 255)),
                           Image.new('RGB', size, (222, 230, 244)), sh)
    im = body.convert('RGBA')
    im.putalpha(a)
    return im


def skyhigh():
    """
    高い空を青く沈める板。

    帯 4 の空はカメラの塗り潰し（一色）で、そこには朝靄の白を置いてある。
    地平まで塗り潰しが届くので、畑が靄に溶け切ったところに継ぎ目が出ない。
    そのうえへこの板を敷き、仰角が上がるほど濃く青を乗せて、原作の
    「まだ薄青い高い空」を出す。**靄の側ではなく青の側を板にするのが肝。**
    板には必ず縁があり、縁の外の仰角は塗り潰しの色がそのまま出る。
    青を塗り潰しに置くと、その縁の外に生の青が帯になって残る。

    絵は一様でよいが、まったくの平面だと帯が出るので薄い斑を入れる。
    α は抜かない。濃さは板の色（_BaseColor）と仰角の薄れが決める
    """
    size = (64, 64)
    body = tint(size, clouds(size, 3733, 3, 1.6), (236, 236, 240), (255, 255, 255))
    im = body.convert('RGBA')
    im.putalpha(Image.new('L', size, 255))
    return im


def save_plain(im, name):
    """Drive を冠さない名前で保存する。雲の絵は帯をまたいで名前が決まっている"""
    path = os.path.join(OUT, name + '.png')
    im.save(path)
    print('%-24s %dx%d' % (os.path.basename(path), im.size[0], im.size[1]))


def main():
    if not os.path.isdir(OUT):
        raise SystemExit('テクスチャの置き場が無い: ' + OUT)
    save(trim(), 'CarTrim')
    save(steel(), 'CarSteel')
    save(seat(), 'CarSeat')
    save(body(), 'CarBody')
    save(dials(), 'CarDials')
    save(wheat(), 'Wheat')
    save(field(), 'Field')
    save(dirt(), 'Dirt')
    save(rut(), 'Rut')
    save_plain(torn(), 'CloudTorn')
    save_plain(skyhigh(), 'SkyHigh')


if __name__ == '__main__':
    main()
