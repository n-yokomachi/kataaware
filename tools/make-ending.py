# -*- coding: utf-8 -*-
"""エンディング（Ending.unity）の景色の帯のテクスチャを描く。

設計: docs/superpowers/specs/2026-09-16-scenario-design.md 13 節（13.3 の作った形）

描くのは次の三つ。

  - 野の花と木のアトラス（EndingWild.png）。村の庭のアトラス（make-garden.py）と同じ描き方で、
    8 月のイングランドの道端・林・海辺の崖の植物を描き足す。村の庭の升のうち、エンディングでも使う物
    （楢とイチイの樹冠・野バラ・スイカズラ・フランスギクに見立てた白いエキナセア・キャットミント・レディースマントル・
    ハーディー・ゼラニウム）は、同じ関数で同じ絵を描いて、この一枚にまとめる（描く回数を増やさない）
  - 林の光の筋（EndingShaft.png）。加算で重ねる、芯が明るく縁と両端が消える筋
  - 崖の上の石の塀（コーンウォールの石垣）の面は、色見本の石を並べて組むので絵は要らない

**描画解像度は 320×180 しか無い。** 花は大きめの塊で置き、同じ色の花を寄せて描く（村の庭と同じ）。

アトラスは 128 画素を一つの升にした 8×8 升（1024×1024）。**割り付けを変えたら BuildEndingLand.cs の Cells も直す。**

    python tools/make-ending.py
"""

import importlib.util
import math
import os
import random

from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(HERE)
OUT = os.path.join(ROOT, 'unity', 'Assets', 'Textures', 'Ending')


def _garden():
    spec = importlib.util.spec_from_file_location('make_garden', os.path.join(HERE, 'make-garden.py'))
    mod = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(mod)
    return mod


G = _garden()
UNIT = G.UNIT
Brush = G.Brush
jitter = G.jitter
mix = G.mix
foliage = G.foliage
LEAF_DARK, LEAF_MID, LEAF_LIGHT = G.LEAF_DARK, G.LEAF_MID, G.LEAF_LIGHT

ATLAS = 1024


# ---- 背の高い物（1×3 升、128×384） -------------------------------------------

def willowherb():
    """
    ヤナギラン（ローズベイ・ウィローハーブ）。林の縁と道端に群れて立つ、赤紫の花の穂。
    8 月は盛りの終わりで、穂の上の方が花、下の方は細く赤い実の莢が立ち始めている。
    細い柳のような葉を茎に沿って互い違いに
    """
    w, h = UNIT, UNIT * 3
    rng = random.Random(601)
    b = Brush(w, h)
    spikes = [(22, 40), (46, 12), (70, 30), (96, 18), (112, 56)]
    for x, top in spikes:
        b.line([(x, h - 4), (x + rng.uniform(-4, 4), top)], (104, 70, 60), 2)
    # 葉。茎の下の 2/3 に、斜め上へ細く
    for x, top in spikes:
        y = h - 12
        while y > top + (h - top) * 0.42:
            side = rng.choice((-1, 1))
            b.leaf(x, y, rng.uniform(22, 30), side * rng.uniform(35, 55), 3.2, jitter((58, 88, 44), rng, 10))
            y -= rng.uniform(9, 13)
    # 穂の下の方の、実の莢（細い赤い線）
    for x, top in spikes:
        y0 = top + (h - top) * 0.42
        y = y0
        while y > top + (h - top) * 0.28:
            side = rng.choice((-1, 1))
            b.line([(x, y), (x + side * rng.uniform(5, 9), y - rng.uniform(8, 12))], (150, 60, 80), 1)
            y -= 5
    # 花。四弁の赤紫。穂の先は蕾で濃く細く
    for x, top in spikes:
        y = top + (h - top) * 0.30
        while y > top:
            t = (y - top) / ((h - top) * 0.30)
            r = 3.5 + 3.5 * t
            for side in (-1, 1):
                cx = x + side * r * rng.uniform(0.5, 1.2)
                col = jitter(mix((150, 50, 110), (222, 92, 168), t), rng, 10)
                b.ell(cx, y, r, r * 0.8, col)
                b.ell(cx - 1, y - 1, r * 0.45, r * 0.35, mix(col, (255, 220, 240), 0.35))
            y -= r * 1.1
        for k in range(4):
            b.ell(x + rng.uniform(-2, 2), top - 2 + k * 4, 2.2, 3, (132, 44, 96))
    return b.done()


def hogweed():
    """
    ハナウド（ホグウィード）。道端の背の高いセリの仲間。太い茎の先に、白い平らな傘（複散形の花）。
    根元に大きな切れ込みの葉。夏の道端の白い点景
    """
    w, h = UNIT, UNIT * 3
    rng = random.Random(607)
    b = Brush(w, h)
    for _ in range(9):
        x = rng.uniform(14, 114)
        y = rng.uniform(h * 0.68, h - 8)
        for k in range(5):
            b.leaf(x, y, rng.uniform(20, 30), -70 + k * 35 + rng.uniform(-10, 10), 7, jitter(LEAF_MID, rng, 12))
    heads = [(40, 36, 30), (84, 16, 26), (104, 88, 20), (26, 120, 18)]
    for x, top, r in heads:
        b.line([(64 + (x - 64) * 0.3, h - 8), (x, top + 6)], (110, 124, 76), 3)
        # 小さな傘の骨
        for k in range(9):
            a = math.pi * (0.15 + 0.7 * k / 8.0)
            b.line([(x, top + 14), (x - math.cos(a) * r, top + 2 - math.sin(a) * 2)], (120, 136, 84), 1)
    for x, top, r in heads:
        for _ in range(int(r * 4)):
            dx = rng.uniform(-r, r)
            dy = rng.uniform(-3, 4) + abs(dx) / r * 3
            col = jitter(mix((238, 236, 222), (214, 214, 190), rng.random()), rng, 6)
            b.ell(x + dx, top + dy, rng.uniform(2.5, 4), rng.uniform(2, 3), col)
    return b.done()


# ---- 中くらいの物（1×2 升、128×256） -----------------------------------------

def meadowsweet():
    """シモツケソウ（メドウスイート）。湿った道端の、赤みの茎の先の泡のようなクリーム色の花房。暗い羽状の葉"""
    w, h = UNIT, UNIT * 2
    rng = random.Random(611)
    b = Brush(w, h)
    heads = [(rng.uniform(16, 112), rng.uniform(16, 70)) for _ in range(7)]
    for x, y in heads:
        b.line([(64 + (x - 64) * 0.4, h - 6), (x, y + 8)], (120, 72, 64), 2)
    foliage(b, rng, 8, 120, h * 0.42, h - 3, 46, 15, (34, 56, 30), (70, 98, 48))
    for x, y in heads:
        for _ in range(22):
            dx, dy = rng.uniform(-14, 14), rng.uniform(-10, 8)
            b.ell(x + dx, y + dy, rng.uniform(3, 5), rng.uniform(2.5, 4), jitter((240, 232, 196), rng, 8))
        for _ in range(6):
            b.ell(x + rng.uniform(-10, 10), y + rng.uniform(-6, 6), 2, 2, (206, 196, 150))
    return b.done()


def knapweed():
    """ヤグルマギクの仲間（ノップウィード）。細い針金のような茎に、黒い総苞の上の赤紫の房。ざらついた細い葉"""
    w, h = UNIT, UNIT * 2
    rng = random.Random(613)
    b = Brush(w, h)
    heads = [(rng.uniform(14, 114), rng.uniform(18, 110)) for _ in range(13)]
    for x, y in heads:
        b.line([(64 + (x - 64) * 0.25, h - 4), (x + rng.uniform(-6, 6), (y + h) / 2.0), (x, y + 5)], (76, 92, 52), 2)
    foliage(b, rng, 14, 114, h * 0.58, h - 3, 26, 13, (44, 64, 36), (84, 104, 58))
    for x, y in heads:
        b.ell(x, y + 4, 5, 4.5, (58, 44, 34))
        for k in range(9):
            a = math.pi * (0.1 + 0.8 * k / 8.0)
            b.line([(x, y + 2), (x - math.cos(a) * 8, y - math.sin(a) * 7)], jitter((170, 70, 150), rng, 10), 3)
        b.ell(x, y - 1, 3, 2.5, (196, 104, 176))
    return b.done()


def scabious():
    """マツムシソウ（フィールド・スカビオス）。長い茎の先に、薄い藤色の平たい針山のような花"""
    w, h = UNIT, UNIT * 2
    rng = random.Random(617)
    b = Brush(w, h)
    heads = [(rng.uniform(14, 114), rng.uniform(14, 96)) for _ in range(11)]
    for x, y in heads:
        b.line([(64 + (x - 64) * 0.3, h - 4), (x + rng.uniform(-8, 8), (y + h) / 2.0), (x, y)], (86, 108, 60), 1)
    foliage(b, rng, 16, 112, h * 0.64, h - 3, 22, 13, (44, 66, 36), (86, 110, 60))
    for x, y in heads:
        r = rng.uniform(7, 9)
        base = jitter((168, 150, 214), rng, 10)
        b.ell(x, y, r, r * 0.62, mix(base, (90, 80, 130), 0.3))
        b.ell(x, y - 1, r * 0.82, r * 0.5, base)
        b.ell(x, y - 1.5, r * 0.4, r * 0.25, mix(base, (255, 255, 255), 0.35))
    return b.done()


def hemp_agrimony():
    """ヒヨドリバナの仲間（ヘンプ・アグリモニー）。湿った道端の、くすんだピンクのふわふわした平たい花房"""
    w, h = UNIT, UNIT * 2
    rng = random.Random(619)
    b = Brush(w, h)
    heads = [(rng.uniform(22, 106), rng.uniform(22, 70)) for _ in range(6)]
    for x, y in heads:
        b.line([(64 + (x - 64) * 0.4, h - 6), (x, y + 8)], (110, 74, 70), 3)
    foliage(b, rng, 8, 120, h * 0.38, h - 3, 50, 16, LEAF_DARK, LEAF_MID)
    for x, y in heads:
        for _ in range(26):
            dx, dy = rng.uniform(-16, 16), rng.uniform(-8, 8) + abs(rng.uniform(-16, 16)) * 0.15
            b.ell(x + dx, y + dy, rng.uniform(3, 5), rng.uniform(2.5, 3.5), jitter((214, 164, 184), rng, 10))
    return b.done()


def ragwort():
    """
    黄の平たい花房（ノボロギクの仲間のラグワートと、ムカシヨモギの仲間のフリーベン）。
    道端の黄の点景。小さな黄の花を平らな房に寄せる
    """
    w, h = UNIT, UNIT * 2
    rng = random.Random(623)
    b = Brush(w, h)
    heads = [(rng.uniform(20, 108), rng.uniform(22, 80)) for _ in range(7)]
    for x, y in heads:
        b.line([(64 + (x - 64) * 0.35, h - 6), (x, y + 6)], (80, 100, 52), 2)
    foliage(b, rng, 10, 118, h * 0.46, h - 3, 36, 14, (48, 70, 36), (92, 118, 58))
    for x, y in heads:
        for _ in range(9):
            G.daisy(b, rng, x + rng.uniform(-13, 13), y + rng.uniform(-6, 6), rng.uniform(4.5, 5.5), (238, 196, 40), (196, 130, 30))
    return b.done()


def tall_grass():
    """道端の背の高い草。8 月は穂が枯れ色に変わり始める。細く弧を描く葉と、藁色の穂"""
    w, h = UNIT, UNIT * 2
    rng = random.Random(629)
    b = Brush(w, h)
    for _ in range(46):
        x = rng.uniform(20, 108)
        top = rng.uniform(10, 120)
        bend = rng.uniform(-24, 24)
        col = jitter(mix((104, 128, 60), (190, 170, 110), rng.random() * 0.8), rng, 8)
        pts = [(64 + (x - 64) * 0.2, h - 2), (x + bend * 0.3, (h + top) / 2.0), (x + bend, top)]
        b.line(pts, col, 1)
        if rng.random() < 0.6:
            for k in range(5):
                b.ell(x + bend + rng.uniform(-3, 3), top + k * 4, 2.2, 3.2, jitter((196, 178, 120), rng, 10))
    for _ in range(36):
        x = rng.uniform(10, 118)
        b.leaf(64 + (x - 64) * 0.4, h - 2, rng.uniform(40, 80), (x - 64) * 0.8 + rng.uniform(-15, 15), 3, jitter((84, 112, 54), rng, 10))
    return b.done()


# ---- 低い物（2×1 升、256×128） -----------------------------------------------

def bramble():
    """
    キイチゴ（ブラックベリー）の茂み。弧を描く棘の茎、三つ葉から五つ葉の暗い葉、白から淡いピンクの五弁の花と、
    8 月の赤と黒の実。生け垣と林の縁を埋める
    """
    w, h = UNIT * 2, UNIT
    rng = random.Random(641)
    b = Brush(w, h)
    for _ in range(9):
        x0 = rng.uniform(20, 236)
        pts = [(x0, h - 2)]
        for k in range(1, 6):
            pts.append((x0 + (k * rng.uniform(8, 16)) * rng.choice((-1, 1)), h - 2 - k * rng.uniform(12, 20) + (k * k) * 1.5))
        b.line(pts, (96, 58, 60), 2)
    foliage(b, rng, 6, 250, h * 0.20, h - 2, 110, 16, (30, 50, 28), (74, 100, 50))
    for _ in range(26):
        x = rng.uniform(14, 242)
        y = rng.uniform(h * 0.22, h * 0.75)
        for k in range(5):
            a = 2 * math.pi * k / 5
            b.ell(x + math.cos(a) * 3.2, y + math.sin(a) * 3.0, 3.2, 2.8, jitter((244, 226, 230), rng, 8))
        b.ell(x, y, 1.6, 1.6, (220, 200, 120))
    for _ in range(22):
        x = rng.uniform(14, 242)
        y = rng.uniform(h * 0.3, h * 0.85)
        ripe = rng.random()
        col = (40, 20, 34) if ripe < 0.4 else (170, 40, 44) if ripe < 0.8 else (120, 140, 70)
        for k in range(4):
            b.ell(x + rng.uniform(-2.5, 2.5), y + rng.uniform(-2.5, 2.5), 2.4, 2.4, jitter(col, rng, 10))
    return b.done()


def bracken():
    """ワラビの仲間（ブラッケン）。林の地を覆う、三角の大きな羽の葉。8 月は明るい緑で、ところどころ黄ばむ"""
    w, h = UNIT * 2, UNIT
    rng = random.Random(643)
    b = Brush(w, h)
    for _ in range(14):
        x = rng.uniform(16, 240)
        top = rng.uniform(8, 40)
        lean = rng.uniform(-40, 40)
        col_stem = (120, 110, 70)
        pts = [(x, h - 2), (x + lean * 0.4, (h + top) / 2.0), (x + lean, top)]
        b.line(pts, col_stem, 2)
        age = rng.random()
        base = mix((86, 132, 50), (170, 150, 70), 0.25 * age if age < 0.8 else 0.7)
        for k in range(9):
            t = k / 8.0
            cx = x + lean * (0.35 + 0.65 * t)
            cy = (h + top) / 2.0 + (top - (h + top) / 2.0) * t
            span = 34 * (1 - t) + 6
            for sgn in (-1, 1):
                ex = cx + sgn * span
                ey = cy + span * 0.35
                b.line([(cx, cy), (ex, ey)], jitter(base, rng, 10), 3)
                for q in range(4):
                    s = (q + 1) / 5.0
                    b.leaf(cx + (ex - cx) * s, cy + (ey - cy) * s, span * 0.22, sgn * 150, 2.2, jitter(base, rng, 12))
    return b.done()


def gorse():
    """
    ハリエニシダ（ウエスタン・ゴース）。海辺の崖と石垣の上の、濃い緑の棘の丸い茂みを黄の花が覆う。
    8 月から 9 月、ヒースと絡んで崖の上を黄と紫に染める
    """
    w, h = UNIT * 2, UNIT
    rng = random.Random(647)
    b = Brush(w, h)
    for _ in range(900):
        x = rng.uniform(8, 248)
        t = (x - 128) / 120.0
        top = 14 + 70 * t * t
        y = rng.uniform(top, h - 2)
        col = jitter(mix((42, 62, 34), (78, 100, 52), rng.random()), rng, 8)
        a = rng.uniform(-60, 60)
        b.leaf(x, y, rng.uniform(6, 10), a, 1.4, col)
    for _ in range(360):
        x = rng.uniform(12, 244)
        t = (x - 128) / 118.0
        top = 12 + 70 * t * t
        y = rng.uniform(top, top + (h - top) * 0.75)
        b.ell(x, y, rng.uniform(2.8, 4.2), rng.uniform(2.4, 3.4), jitter((246, 196, 36), rng, 12))
    return b.done()


def heather():
    """
    ヒース（ベル・ヘザーとリング）。崖の上と荒野の、低い座布団のような株。細かい暗い葉に、
    赤紫の鐘形の花（ベル・ヘザー）と藤色の穂（リング）
    """
    w, h = UNIT * 2, UNIT
    rng = random.Random(653)
    b = Brush(w, h)
    for _ in range(700):
        x = rng.uniform(6, 250)
        t = (x - 128) / 122.0
        top = 34 + 50 * t * t
        y = rng.uniform(top, h - 2)
        b.leaf(x, y + 4, rng.uniform(5, 8), rng.uniform(-40, 40), 1.4, jitter(mix((50, 58, 40), (90, 90, 60), rng.random()), rng, 8))
    for _ in range(520):
        x = rng.uniform(8, 248)
        t = (x - 128) / 120.0
        top = 30 + 50 * t * t
        y = rng.uniform(top, top + (h - top) * 0.6)
        col = (178, 70, 146) if rng.random() < 0.55 else (190, 140, 186)
        b.ell(x, y, rng.uniform(1.8, 2.8), rng.uniform(2.2, 3.4), jitter(col, rng, 12))
    return b.done()


def grass_tuft():
    """道の縁の草の株。低く、葉先を外へ倒す"""
    w, h = UNIT * 2, UNIT
    rng = random.Random(659)
    b = Brush(w, h)
    for _ in range(260):
        x = rng.uniform(10, 246)
        foot = 128 + (x - 128) * 0.6
        a = (x - 128) * 0.45 + rng.uniform(-20, 20)
        b.leaf(foot, h - 2, rng.uniform(40, 90), a, 2.4, jitter(mix((70, 100, 44), (130, 150, 70), rng.random()), rng, 8))
    for _ in range(30):
        x = rng.uniform(30, 226)
        y = rng.uniform(10, 60)
        for k in range(4):
            b.ell(x + rng.uniform(-2, 2), y + k * 4, 2, 3, jitter((176, 164, 112), rng, 10))
    return b.done()


def thrift():
    """
    ハマカンザシ（スリフト）。崖の上の草の座布団に、丸いピンクの花。8 月は盛りを過ぎて、茶の枯れた頭が混じる
    """
    w, h = UNIT * 2, UNIT
    rng = random.Random(661)
    b = Brush(w, h)
    for _ in range(300):
        x = rng.uniform(10, 246)
        t = (x - 128) / 118.0
        top = 70 + 40 * t * t
        y = rng.uniform(top, h - 2)
        b.leaf(x, y + 3, rng.uniform(8, 14), rng.uniform(-50, 50), 1.6, jitter((70, 100, 60), rng, 10))
    for _ in range(40):
        x = rng.uniform(20, 236)
        y = rng.uniform(24, 70)
        b.line([(x, y), (x + rng.uniform(-4, 4), y + 36)], (110, 120, 76), 1)
        col = (224, 140, 176) if rng.random() < 0.6 else (150, 110, 96)
        b.ell(x, y, 6, 5.5, jitter(col, rng, 10))
    return b.done()


# ---- 木（2×2 升、256×256） ----------------------------------------------------

def beech():
    """
    ブナの樹冠。林の天井の、明るい若緑の葉の房を重ねた広いドーム（村の庭の樹冠と同じ描き方）。
    房のあいだに隙間を空けて、空と日を透かす（木漏れ日が抜ける所。影も札の α で抜ける）。
    層を規則正しく積んだ形は、刈り込んだ庭木に見えた
    """
    b, rng = G.canopy(671, (150, 182, 82), (58, 92, 40), (9, 13))
    # 隙間は葉の形の小さな抜きを寄せて作る（丸い穴を開けると、穴の空いたチーズに見えた）
    for _ in range(34):
        x0 = rng.uniform(44, 212)
        y0 = rng.uniform(44, 186)
        for _k in range(rng.randint(4, 8)):
            x = x0 + rng.uniform(-9, 9)
            y = y0 + rng.uniform(-6, 6)
            a = math.radians(rng.uniform(0, 180))
            L = rng.uniform(5, 9)
            dx, dy = math.cos(a) * L, math.sin(a) * L
            px, py = -dy * 0.4, dx * 0.4
            b.a.polygon([(x - dx, y - dy), (x + px, y + py), (x + dx, y + dy), (x - px, y - py)], fill=0)
    return b.done()


def hawthorn():
    """
    風に刈られたサンザシ。海からの風で、樹冠が内陸の側（札の左）へ旗のように流れ、幹も同じ向きへ深く傾く。
    風上（右）の端は低く削がれ、上の面は風下へ向かって上がる（コーンウォールの崖の上の、くさび形の木）。
    葉は風に梳かれて横に寝る。暗い緑の詰まった葉に、色づき始めた実を少しだけ
    """
    w, h = UNIT * 2, UNIT * 2
    rng = random.Random(677)
    b = Brush(w, h)
    bark = (70, 58, 50)
    b.line([(198, 254), (186, 226), (160, 204), (118, 186), (78, 176)], bark, 7)
    b.line([(176, 216), (208, 206)], bark, 3)
    b.line([(150, 200), (126, 198), (104, 190)], bark, 3)

    def top(x):
        return 178 - (224 - x) * 0.42 + 5 * math.sin(x * 0.09) + 3 * math.sin(x * 0.23 + 1.0)

    def bottom(x):
        return 206 - (224 - x) * 0.16 + 4 * math.sin(x * 0.13 + 2.0)

    def inside(x, y):
        if x < 6 or x > 226:
            return -1
        # 風上の端は丸く削ぐ
        edge = min(x - 6, (226 - x) * 1.6)
        return min(y - top(x), bottom(x) - y, edge)

    for _ in range(3600):
        x = rng.uniform(4, 228)
        y = rng.uniform(80, 214)
        d = inside(x, y)
        if d < 1:
            continue
        # 上の面は日を受けて明るく、下は陰る
        t = min(1.0, max(0.0, (y - top(x)) / max(8.0, bottom(x) - top(x)) + rng.uniform(-0.12, 0.12)))
        col = jitter(mix((110, 132, 64), (30, 46, 28), t), rng, 7)
        b.leaf(x, y, rng.uniform(6, 9), 180 + rng.uniform(-22, 22), 2.6, col)
    for _ in range(16):
        x = rng.uniform(30, 200)
        y = rng.uniform(120, 200)
        if inside(x, y) < 5:
            continue
        b.ell(x, y, 2.0, 2.0, (142, 38, 34))
    return b.done()


def gull(flap):
    """カモメ。翼を広げて滑る、横から見た形。白い胴と灰の翼、黒い翼の先。flap は翼の上げ具合"""
    w, h = UNIT, UNIT
    rng = random.Random(683 + int(flap * 10))
    b = Brush(w, h)
    cx, cy = 64, 70
    lift = 26 * flap
    for sgn in (-1, 1):
        tip = (cx + sgn * 58, cy - lift - 6)
        elbow = (cx + sgn * 26, cy - lift * 0.6 - 10)
        b.poly([(cx + sgn * 4, cy - 4), elbow, tip, (tip[0] - sgn * 6, tip[1] + 6), (cx + sgn * 24, cy - lift * 0.4 - 2), (cx + sgn * 4, cy + 4)], (170, 176, 184))
        b.poly([tip, (tip[0] - sgn * 14, tip[1] + 2), (tip[0] - sgn * 10, tip[1] + 7), (tip[0] - sgn * 5, tip[1] + 6)], (30, 30, 34))
    b.ell(cx, cy, 16, 6, (240, 242, 244))
    b.ell(cx + 14, cy - 2, 6, 5, (244, 246, 248))
    b.poly([(cx + 19, cy - 2), (cx + 26, cy), (cx + 19, cy + 1)], (230, 190, 60))
    b.poly([(cx - 14, cy - 2), (cx - 24, cy + 2), (cx - 14, cy + 4)], (230, 232, 236))
    return b.done()


# ---- アトラス -----------------------------------------------------------------

# (升の x, 升の y, 幅の升, 高さの升, 描く関数)。BuildEndingLand.cs の Cells と同じ並び
CELLS = [
    (0, 0, 1, 3, willowherb),                                                # 0 ヤナギラン
    (1, 0, 1, 3, hogweed),                                                   # 1 ハナウド
    (2, 0, 1, 2, meadowsweet),                                               # 2 シモツケソウ
    (3, 0, 1, 2, knapweed),                                                  # 3 ノップウィード
    (4, 0, 1, 2, scabious),                                                  # 4 マツムシソウ
    (5, 0, 1, 2, hemp_agrimony),                                             # 5 ヘンプ・アグリモニー
    (6, 0, 1, 2, ragwort),                                                   # 6 黄の平たい花房
    (7, 0, 1, 2, tall_grass),                                                # 7 背の高い草
    (2, 2, 2, 1, bramble),                                                   # 8 キイチゴ
    (4, 2, 2, 1, bracken),                                                   # 9 ワラビの仲間
    (6, 2, 2, 1, gorse),                                                     # 10 ハリエニシダ
    (0, 3, 2, 1, heather),                                                   # 11 ヒース
    (2, 3, 2, 1, grass_tuft),                                                # 12 草の株
    (4, 3, 2, 1, thrift),                                                    # 13 ハマカンザシ
    (6, 3, 2, 1, G.geranium),                                                # 14 ハーディー・ゼラニウム（村の庭と同じ）
    (0, 4, 2, 2, beech),                                                     # 15 ブナの樹冠
    (2, 4, 2, 2, hawthorn),                                                  # 16 風に曲げられたサンザシ
    (4, 4, 2, 2, G.oak),                                                     # 17 楢の樹冠（村の庭と同じ）
    (6, 4, 2, 2, G.yew),                                                     # 18 イチイの樹冠（村の庭と同じ）
    (0, 6, 2, 2, G.roses),                                                   # 19 野バラ（村の庭のつるバラ）
    (2, 6, 2, 2, G.honeysuckle),                                             # 20 スイカズラ（村の庭と同じ）
    (4, 6, 1, 1, lambda: gull(0.2)),                                         # 21 カモメ（翼を広げて滑る）
    (4, 7, 1, 1, lambda: gull(0.8)),                                         # 22 カモメ（翼を上げる）
    (5, 6, 1, 2, lambda: G.echinacea((240, 238, 226), (196, 146, 52), 239)),  # 23 フランスギクに見立てた白いエキナセア（村の庭と同じ）
    (6, 6, 2, 1, G.catmint),                                                 # 24 キャットミント（村の庭と同じ）
    (6, 7, 2, 1, G.ladysmantle),                                             # 25 レディースマントル（村の庭と同じ）
]


def atlas():
    im = Image.new('RGBA', (ATLAS, ATLAS), (60, 84, 40, 0))
    for (ux, uy, uw, uh, draw) in CELLS:
        cell = draw()
        assert cell.size == (uw * UNIT, uh * UNIT), (ux, uy, cell.size)
        im.paste(cell, (ux * UNIT, uy * UNIT))
    return im


# ---- 畑と荒野と石垣の丘のアトラス（EndingField.png、帯 3〜5） ----------------------------------------

def poppy():
    """
    ヒナゲシ（コモン・ポピー）。畑の縁の、細い毛の生えた茎の先の緋色の四弁の杯。黒い芯。
    6〜8 月に咲き、8 月は花と、壺の形の青灰の実が混じる
    """
    w, h = UNIT, UNIT * 2
    rng = random.Random(701)
    b = Brush(w, h)
    heads = [(rng.uniform(16, 112), rng.uniform(18, 150)) for _ in range(9)]
    for x, y in heads:
        b.line([(64 + (x - 64) * 0.3, h - 4), (x + rng.uniform(-10, 10), (y + h) / 2.0), (x, y + 6)], (104, 128, 74), 1)
    foliage(b, rng, 16, 112, h * 0.70, h - 3, 20, 12, (56, 80, 46), (104, 128, 76))
    for i, (x, y) in enumerate(heads):
        if i % 3 == 2:
            # 実。青灰の壺に平たい蓋
            b.ell(x, y + 2, 4, 5.5, (116, 138, 110))
            b.ell(x, y - 3, 4.5, 1.6, (92, 104, 90))
            continue
        r = rng.uniform(9, 12)
        col = jitter((214, 38, 26), rng, 10)
        b.ell(x - r * 0.35, y, r * 0.8, r * 0.72, mix(col, (150, 18, 16), 0.25))
        b.ell(x + r * 0.35, y, r * 0.8, r * 0.72, col)
        b.ell(x, y - r * 0.25, r * 0.7, r * 0.55, mix(col, (255, 90, 60), 0.2))
        b.ell(x, y + 1, r * 0.22, r * 0.2, (34, 26, 32))
    return b.done()


def cornflower():
    """ヤグルマギク（コーンフラワー）。畑の縁の、針金のような灰緑の茎の先の、縁の裂けた鮮やかな青の花"""
    w, h = UNIT, UNIT * 2
    rng = random.Random(703)
    b = Brush(w, h)
    heads = [(rng.uniform(16, 112), rng.uniform(20, 130)) for _ in range(9)]
    for x, y in heads:
        b.line([(64 + (x - 64) * 0.25, h - 4), (x + rng.uniform(-8, 8), (y + h) / 2.0), (x, y + 4)], (110, 130, 104), 1)
    for _ in range(18):
        x = rng.uniform(30, 98)
        y = rng.uniform(h * 0.45, h - 8)
        b.leaf(x, y, rng.uniform(14, 22), rng.uniform(-50, 50), 1.6, jitter((96, 118, 90), rng, 8))
    for x, y in heads:
        for k in range(8):
            a = 2 * math.pi * k / 8
            b.ell(x + math.cos(a) * 4.5, y + math.sin(a) * 3.4, 3.2, 2.6, jitter((62, 96, 208), rng, 12))
        b.ell(x, y, 2.6, 2.2, (44, 50, 120))
    return b.done()


def thistle():
    """
    アザミ（スピア・シスル）。牧草地と道端の、棘の縁の灰緑の葉と、棘の球の上の赤紫の刷毛。
    8 月は白い綿毛の頭が混じる
    """
    w, h = UNIT, UNIT * 2
    rng = random.Random(707)
    b = Brush(w, h)
    b.line([(64, h - 2), (62, 120), (60, 40)], (96, 116, 70), 4)
    for bx, by in ((60, 60), (60, 90), (62, 120)):
        for sgn in (-1, 1):
            b.line([(bx, by), (bx + sgn * 26, by - 30)], (96, 116, 70), 2)
    for k in range(9):
        y = h - 10 - k * 20
        for sgn in (-1, 1):
            length = 46 - k * 4
            x = 62 + sgn * 2
            b.leaf(x, y, length, sgn * rng.uniform(55, 75), 7, jitter((82, 108, 72), rng, 8))
            for q in range(5):
                t = (q + 1) / 6.0
                a = math.radians(sgn * 65)
                px, py = x + math.sin(a) * length * t, y - math.cos(a) * length * t
                b.line([(px, py), (px + rng.uniform(-3, 3), py - 5)], (200, 206, 170), 1)
    heads = [(60, 40), (86, 30), (34, 30), (88, 62), (32, 58)]
    for i, (x, y) in enumerate(heads):
        if i == 3:
            b.ell(x, y, 9, 8, (238, 234, 222))
            b.ell(x - 2, y - 2, 6, 5, (252, 250, 244))
            continue
        b.ell(x, y + 5, 6, 6.5, (86, 104, 60))
        for k in range(10):
            b.line([(x + rng.uniform(-5, 5), y + 1), (x + rng.uniform(-7, 7), y - 7)], jitter((178, 60, 146), rng, 12), 2)
    return b.done()


def mayweed():
    """シカギク（スセントレス・メイウィード）。畑の縁の、糸のように細かい葉の上の白い花びらと黄の芯"""
    w, h = UNIT, UNIT * 2
    rng = random.Random(709)
    b = Brush(w, h)
    heads = [(rng.uniform(14, 114), rng.uniform(h * 0.42, h * 0.72)) for _ in range(12)]
    for x, y in heads:
        b.line([(64 + (x - 64) * 0.4, h - 3), (x, y + 4)], (84, 116, 60), 1)
    for _ in range(90):
        x = rng.uniform(16, 112)
        y = rng.uniform(h * 0.66, h - 4)
        b.line([(x, y), (x + rng.uniform(-6, 6), y - rng.uniform(6, 12))], jitter((70, 104, 52), rng, 10), 1)
    for x, y in heads:
        G.daisy(b, rng, x, y, rng.uniform(7, 8.5), (246, 246, 238), (232, 196, 48))
    return b.done()


def _hedge(seed, top, bumps, extras):
    """生け垣（2×1 升）。上の輪郭 top(x) から下の縁まで葉を詰め、上ほど日を受けて明るく。extras は実や花を足す"""
    w, h = UNIT * 2, UNIT
    rng = random.Random(seed)
    b = Brush(w, h)
    for _ in range(3000):
        x = rng.uniform(2, 254)
        t0 = top(x) + bumps(x, rng)
        y = rng.uniform(t0, h - 1)
        k = min(1.0, max(0.0, (y - t0) / 70.0 + rng.uniform(-0.12, 0.12)))
        col = jitter(mix((104, 132, 60), (30, 48, 26), k), rng, 7)
        b.leaf(x, y + 3, rng.uniform(5, 8), rng.uniform(0, 360), 2.6, col)
    extras(b, rng, top)
    return b.done()


def hedge_a():
    """刈り込んだサンザシの生け垣。上は平らに刈られ、縁は少し波打つ。8 月の終わりに色づき始めた赤い実を少し"""
    def top(x):
        return 22 + 3 * math.sin(x * 0.06) + 2 * math.sin(x * 0.19 + 1.0)

    def bumps(x, rng):
        return rng.uniform(-2, 3)

    def extras(b, rng, top):
        for _ in range(16):
            x = rng.uniform(10, 246)
            y = rng.uniform(top(x) + 10, 100)
            b.ell(x, y, 1.8, 1.8, (148, 30, 30))
    return _hedge(711, top, bumps, extras)


def hedge_b():
    """
    伸びた生け垣。サンザシにキイチゴと野バラが絡み、明るい緑のカエデが頭を出す。
    上の輪郭は不揃いで、若い枝が突き出る。野バラの橙の実とキイチゴの黒い実
    """
    def top(x):
        return 16 + 10 * math.sin(x * 0.035 + 0.5) + 6 * math.sin(x * 0.11)

    def bumps(x, rng):
        return rng.uniform(-4, 6)

    def extras(b, rng, top):
        # カエデの明るい房
        for cx in (60, 190):
            for _ in range(160):
                a = rng.uniform(0, 2 * math.pi)
                d = 22 * math.sqrt(rng.random())
                x, y = cx + math.cos(a) * d, top(cx) + 8 + math.sin(a) * d * 0.7
                b.leaf(x, y, rng.uniform(5, 7), rng.uniform(0, 360), 3, jitter(mix((150, 170, 80), (96, 124, 60), rng.random()), rng, 6))
        for _ in range(10):
            x = rng.uniform(10, 246)
            y0 = top(x)
            b.line([(x, y0 + 6), (x + rng.uniform(-8, 8), y0 - rng.uniform(8, 16))], (92, 110, 60), 1)
        for _ in range(12):
            x = rng.uniform(10, 246)
            b.ell(x, rng.uniform(top(x) + 8, 90), 2.2, 2.6, (222, 76, 36))
        for _ in range(10):
            x = rng.uniform(10, 246)
            b.ell(x, rng.uniform(top(x) + 14, 110), 2, 2, (36, 22, 34))
    return _hedge(713, top, bumps, extras)


def _heath(seed, leaf_dark, leaf_light, flowers, flower_share, top_lo=34, leggy=False):
    """ヒースの株（2×1 升）。低い座布団の輪郭に細かい葉を詰め、上の方に花の穂を散らす"""
    w, h = UNIT * 2, UNIT
    rng = random.Random(seed)
    b = Brush(w, h)
    if leggy:
        for _ in range(26):
            x = rng.uniform(20, 236)
            b.line([(x, h - 2), (x + rng.uniform(-10, 10), rng.uniform(40, 70))], (92, 76, 66), 1)
    for _ in range(760):
        x = rng.uniform(6, 250)
        t = (x - 128) / 122.0
        top = top_lo + 50 * t * t
        y = rng.uniform(top, h - 2)
        b.leaf(x, y + 4, rng.uniform(5, 8), rng.uniform(-40, 40), 1.4, jitter(mix(leaf_dark, leaf_light, rng.random()), rng, 8))
    for _ in range(int(560 * flower_share)):
        x = rng.uniform(8, 248)
        t = (x - 128) / 120.0
        top = top_lo - 4 + 50 * t * t
        y = rng.uniform(top, top + (h - top) * 0.55)
        col = flowers[rng.randrange(len(flowers))]
        b.ell(x, y, rng.uniform(1.8, 2.8), rng.uniform(2.2, 3.4), jitter(col, rng, 12))
    return b.done()


def ling():
    """
    リング（カルーナ）。荒野を一面に覆う主のヒース。8 月の盛りの、藤色がかった紫の細かい花の穂。
    花を詰めすぎると、昼の日を受けて綿菓子のような桃色の塊に見えたので、暗い葉を覗かせる
    """
    return _heath(717, (50, 56, 40), (86, 88, 58), [(150, 92, 150), (132, 80, 136), (168, 114, 166)], 0.62)


def bell_heather():
    """ベル・ヘザー。乾いた斜面の、濃い赤紫の鐘形の花。リングより色が深い"""
    return _heath(719, (44, 52, 38), (80, 84, 56), [(140, 40, 104), (120, 30, 90), (160, 60, 124)], 0.75, top_lo=40)


def old_heather():
    """年を経て伸びたヒース。茶色い木の茎が目立ち、花は少なくくすむ（焼いて若返らせる前の株）"""
    return _heath(723, (60, 54, 44), (98, 88, 70), [(150, 110, 130), (130, 100, 110)], 0.35, top_lo=26, leggy=True)


def bilberry():
    """ビルベリー。ヒースのあいだの、明るい緑の小さな葉の低い茂みと、青黒い実"""
    w, h = UNIT * 2, UNIT
    rng = random.Random(727)
    b = Brush(w, h)
    for _ in range(900):
        x = rng.uniform(8, 248)
        t = (x - 128) / 120.0
        top = 50 + 44 * t * t
        y = rng.uniform(top, h - 2)
        b.leaf(x, y + 3, rng.uniform(5, 7), rng.uniform(0, 360), 2.4, jitter(mix((70, 110, 40), (132, 160, 64), rng.random()), rng, 8))
    for _ in range(20):
        x = rng.uniform(20, 236)
        b.ell(x, rng.uniform(64, 110), 1.8, 1.8, (44, 44, 76))
    return b.done()


def moor_grass():
    """ムラサキ・ムーアグラス。荒野の湿った所の株立ちの草。藁色がかった緑の葉と、紫を帯びた穂"""
    w, h = UNIT * 2, UNIT
    rng = random.Random(729)
    b = Brush(w, h)
    for _ in range(240):
        x = rng.uniform(10, 246)
        foot = 128 + (x - 128) * 0.5
        a = (x - 128) * 0.5 + rng.uniform(-18, 18)
        b.leaf(foot, h - 2, rng.uniform(40, 84), a, 2.2, jitter(mix((112, 124, 70), (170, 160, 104), rng.random()), rng, 8))
    for _ in range(26):
        x = rng.uniform(40, 216)
        y = rng.uniform(8, 50)
        b.line([(x, y), (x + rng.uniform(-3, 3), y + 30)], (140, 130, 96), 1)
        for k in range(5):
            b.ell(x + rng.uniform(-1.5, 1.5), y + k * 3.5, 1.5, 2.6, jitter((124, 92, 116), rng, 10))
    return b.done()


def ash():
    """トネリコ（アッシュ）の樹冠。生け垣の木と畑の一本木。明るい緑の、羽の葉の房の透けた軽い樹冠"""
    b, rng = G.canopy(733, (138, 160, 84), (58, 88, 44), (7, 10))
    for _ in range(44):
        x0 = rng.uniform(40, 216)
        y0 = rng.uniform(40, 190)
        for _k in range(rng.randint(3, 7)):
            x = x0 + rng.uniform(-8, 8)
            y = y0 + rng.uniform(-6, 6)
            a = math.radians(rng.uniform(0, 180))
            L = rng.uniform(4, 8)
            dx, dy = math.cos(a) * L, math.sin(a) * L
            px, py = -dy * 0.4, dx * 0.4
            b.a.polygon([(x - dx, y - dy), (x + px, y + py), (x + dx, y + dy), (x - px, y - py)], fill=0)
    return b.done()


def coping():
    """
    石垣の頭の、縦に立てて詰めた笠石（コック・アンド・ヘン）。2×1 升の下の 40 画素に、2 m ぶん（128 画素で 1 m）。
    3〜6 cm の薄い石を隙間なく詰め、高いのと低いのを混ぜ、少しずつ傾ける。上は透かす。
    頭に明るい線を引いて濃い目地で区切ったら、木の杭の柵に見えた（村の庭の石垣でも、間を空けると白い杭の柵に見えた）。
    目地は石の色を少し沈めるだけにし、頭の輪郭の凸凹で石と読ませる
    """
    w, h = UNIT * 2, UNIT
    rng = random.Random(737)
    b = Brush(w, h)
    x = 0.0
    i = 0
    while x < w:
        wide = rng.uniform(4, 7)
        tall = rng.uniform(19, 22) if i % 3 == 0 else rng.uniform(15, 18)
        lean = rng.uniform(-2.5, 2.5)
        base = jitter(mix((160, 146, 112), (132, 122, 98), rng.random()), rng, 7)
        x1 = min(float(w), x + wide)
        top_l = h - tall + rng.uniform(-1.5, 1.5)
        top_r = h - tall + rng.uniform(-1.5, 1.5)
        b.poly([(x, h), (x1, h), (x1 + lean, top_r), (x + lean, top_l)], base)
        b.line([(x1 - 0.5, h), (x1 - 0.5 + lean, top_r + 3)], mix(base, (90, 78, 60), 0.35), 1)
        if rng.random() < 0.3:
            b.ell(x + wide * 0.5, h - rng.uniform(6, 16), 1.4, 1.2, (196, 192, 120))
        x = x1
        i += 1
    return b.done()


# (升の x, 升の y, 幅の升, 高さの升, 描く関数)。BuildEndingFarm.cs の FieldCells と同じ並び。
# 下の 7・8 行目は空けておく（帯 6・7 の作り込みで足す）
FIELD_CELLS = [
    (0, 0, 1, 2, poppy),          # 0 ヒナゲシ
    (1, 0, 1, 2, cornflower),     # 1 ヤグルマギク
    (2, 0, 1, 2, thistle),        # 2 アザミ
    (3, 0, 1, 2, mayweed),        # 3 シカギク
    (4, 0, 1, 2, knapweed),       # 4 ノップウィード（EndingWild と同じ）
    (5, 0, 1, 2, scabious),       # 5 マツムシソウ（同）
    (6, 0, 1, 2, ragwort),        # 6 黄の平たい花房（同）
    (7, 0, 1, 2, tall_grass),     # 7 背の高い草（同）
    (0, 2, 2, 1, hedge_a),        # 8 刈り込んだ生け垣
    (2, 2, 2, 1, hedge_b),        # 9 伸びた生け垣
    (4, 2, 2, 1, bracken),        # 10 ワラビ（同）
    (6, 2, 2, 1, gorse),          # 11 ハリエニシダ（同）
    (0, 3, 2, 1, ling),           # 12 リング
    (2, 3, 2, 1, bell_heather),   # 13 ベル・ヘザー
    (4, 3, 2, 1, old_heather),    # 14 伸びたヒース
    (6, 3, 2, 1, bilberry),       # 15 ビルベリー
    (0, 4, 2, 2, G.oak),          # 16 楢の樹冠（村の庭と同じ）
    (2, 4, 2, 2, beech),          # 17 ブナの樹冠（EndingWild と同じ）
    (4, 4, 2, 2, ash),            # 18 トネリコの樹冠
    (6, 4, 2, 1, moor_grass),     # 19 ムーアグラス
    (6, 5, 2, 1, grass_tuft),     # 20 草の株（EndingWild と同じ）
    (0, 6, 2, 1, coping),         # 21 石垣の笠石
]


def field_atlas():
    im = Image.new('RGBA', (ATLAS, ATLAS), (70, 90, 50, 0))
    for (ux, uy, uw, uh, draw) in FIELD_CELLS:
        cell = draw()
        assert cell.size == (uw * UNIT, uh * UNIT), (ux, uy, cell.size)
        im.paste(cell, (ux * UNIT, uy * UNIT))
    return im


def heath_ground():
    """
    荒野の道の近くの地面（256 画素で 4 m 四方、繰り返す）。ヒースの細かい葉の暗い地に、盛りの紫の花の粒、伸びた株の茶、
    焼いた後の若い緑を、大きな斑で寄せて散らす。色見本の平らな色では、株のあいだの地面が芝生に見えた
    """
    size = (256, 256)
    rng = random.Random(751)
    im = Image.new('RGB', size, (58, 50, 46))
    w = G.D.Wrap(im)
    patch = G.D.spread(G.D.clouds(size, 753, 4, 1.0), 3.0).load()
    for _ in range(9000):
        x, y = rng.uniform(0, 256), rng.uniform(0, 256)
        k = patch[int(x) % 256, int(y) % 256] / 255.0
        roll = rng.random()
        if roll < 0.18 + 0.5 * k:
            col = mix((150, 92, 146), (116, 70, 112), rng.random())
        elif roll < 0.62 + 0.2 * k:
            col = mix((64, 72, 46), (92, 96, 60), rng.random())
        else:
            col = mix((88, 72, 62), (112, 94, 78), rng.random())
        r = rng.uniform(1.2, 2.6)
        w.ellipse([x - r, y - r * 0.8, x + r, y + r * 0.8], fill=jitter(col, rng, 8))
    G.D.shade(im, G.D.spread(G.D.clouds(size, 757, 3, 1.0), 2.0), 0.18)
    return im


def shaft():
    """
    林の光の筋（32×128）。加算で重ねる。横は芯が明るく縁で消える。縦は上（樹冠の隙間）で細く始まり、
    下ほど厚い霞を抜けるぶん少し明るく、地面の手前で消える。色は白（光の色はマテリアルの色が持つ）
    """
    w, h = 32, 128
    im = Image.new('RGBA', (w, h), (0, 0, 0, 255))
    px = im.load()
    for y in range(h):
        v = y / float(h - 1)
        along = min(1.0, v / 0.12) * min(1.0, (1.0 - v) / 0.25) * (0.55 + 0.45 * v)
        for x in range(w):
            u = (x + 0.5) / w
            across = max(0.0, 1.0 - abs(u - 0.5) / 0.5)
            k = across * across * (3 - 2 * across) * along
            c = int(255 * k)
            px[x, y] = (c, c, c, 255)
    return im


def save(im, name):
    path = os.path.join(OUT, name + '.png')
    im.save(path)
    print('%-24s %dx%d' % (os.path.basename(path), im.size[0], im.size[1]))


def main():
    if not os.path.isdir(OUT):
        os.makedirs(OUT)
    save(atlas(), 'EndingWild')
    save(field_atlas(), 'EndingField')
    save(heath_ground(), 'EndingHeathGround')
    save(shaft(), 'EndingShaft')


if __name__ == '__main__':
    main()
