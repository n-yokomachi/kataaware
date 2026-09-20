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


# ---- 帯 4。畑と未舗装路 -------------------------------------------------

def wheat():
    """
    麦の株の面。下端が根、上端が穂先。横へだけ繰り返す。

    シェーダーは縦を「根からの高さの割合」で引くので、株の背に関わらず
    絵の下端が根、上端が穂先に来る。天面は割合 1 のところを引くので、
    上から見たときは穂先の行だけが並ぶ。

    **色はここが持たない。** 黄金色は WheatMat の根元色と穂先色が持っていて、
    この絵は明暗だけを預かる。平均を中庸（128）に寄せてあり、シェーダーが
    2 倍して albedo に掛ける。色まで焼き込むと、色を詰めるつまみが二箇所に割れる
    """
    size = (SIZE, SIZE)
    rng = random.Random(3307)
    # 地。根元ほど暗い。株の隙間には日が届かないので、下へ行くほど落とす
    im = Image.new('RGB', size)
    px = im.load()
    base = clouds(size, 4411, 4, 1.2).load()
    for y in range(SIZE):
        v = 1.0 - y / float(SIZE - 1)          # 1 が上（穂先）
        deep = 0.30 + 0.46 * v * v
        for x in range(SIZE):
            k = deep * (0.82 + base[x, y] / 255.0 * 0.36)
            c = int(min(255, max(0, k * 255)))
            px[x, y] = (c, c, c)
    w = Wrap(im)
    # 茎。少しずつ傾けて、縦に真っ直ぐ並ばないようにする
    stalks = 34
    ear = int(SIZE * 0.36)                     # ここから上が穂
    for i in range(stalks):
        x0 = (i + rng.uniform(-0.35, 0.35)) * SIZE / float(stalks)
        lean = rng.uniform(-6.0, 6.0)
        tone = rng.uniform(0.55, 1.0)
        top = ear - rng.randint(0, int(SIZE * 0.10))
        pts = []
        for k in range(9):
            t = k / 8.0
            pts.append((x0 + lean * t * t, SIZE - (SIZE - top) * t))
        col = int(120 + 110 * tone)
        w.line(pts, fill=(col, col, col), width=2 if tone > 0.8 else 1)
        # 穂。茎の先に少し太い塊を置き、その上に芒を数本立てる
        tx, ty = pts[-1]
        eh = rng.randint(int(SIZE * 0.16), int(SIZE * 0.30))
        bright = int(150 + 105 * rng.uniform(0.6, 1.0))
        for k in range(7):
            t = k / 6.0
            yy = ty - eh * t
            half = (1.6 + 2.6 * math.sin(math.pi * (0.15 + 0.85 * t))) * (1.0 - 0.25 * t)
            tail = int(bright * (0.72 + 0.28 * t))
            w.line([(tx - half, yy), (tx + half, yy)], fill=(tail, tail, tail), width=2)
        for k in range(3):
            aw = rng.uniform(-5.0, 5.0)
            w.line([(tx, ty - eh), (tx + aw, ty - eh - rng.randint(8, 20))],
                   fill=(bright, bright, bright), width=1)
    # **一番上の数行に横のむらを入れる。** 箱の天面はここ一行だけを引くので、
    # 一様だと蓋が平らな板に見える。穂を上から覗いた粗さをここへ焼いておく
    w = Wrap(im)
    for _ in range(260):
        x = rng.randrange(SIZE)
        y = rng.randint(0, int(SIZE * 0.09))
        v = rng.randint(96, 255)
        w.line([(x, y), (x + rng.uniform(-1.5, 1.5), y + rng.randint(3, 10))],
               fill=(v, v, v), width=rng.randint(1, 3))
    # 低い解像度で潰れないよう、ぼかしは軽く
    im = Image.blend(im, tile_blur(im, 0.8), 0.45)
    return aim(im, (128, 128, 128))


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
    # 穂の粒。筋だけだと縞の板になるので、細かい点を撒いて面を荒らす
    for _ in range(1100):
        x, y = rng.randrange(SIZE), rng.randrange(SIZE)
        v = rng.randint(150, 235)
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


def skyhaze():
    """
    地平に敷く朝靄の板。

    帯 4 の空はカメラの塗り潰し（一色）なので、そのままでは天頂も地平も同じ青になる。
    原作の「まだ薄青い高い空」を出すには上を深く、地平を白く抜きたい。
    天頂を沈める板は置けない（板は水平なので、低い仰角には届かない）ので、
    逆に地平の側を明るい靄で塗る。空の色そのものを深い青へ落としておけば、
    見上げるほど青が深くなる。

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
    save_plain(skyhaze(), 'SkyHaze')


if __name__ == '__main__':
    main()
