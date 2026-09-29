# -*- coding: utf-8 -*-
"""自室の額に入れる絵を、元の画像から縮める。

元の画像は unity/RawAssets/art/ に置く（git に入らない。Assets の外）。ここで長い辺 128 画素に縮めた物だけを
unity/Assets/Textures/Furniture/Paintings/ に書き、HalfAware/Build the room furniture（BuildFurniture）が
家具のアトラスの右の半分の升へ写す。点で引く粗い画面なので、縮めるのは一度きり（面の平均）で、ぼかしも鋭くもしない。

  - MilletSpring        ミレー『春』（1868–73、オルセー美術館）。オーナーが渡した画像
  - TurnerTemeraire     ターナー『戦艦テメレール号』（1839、ナショナル・ギャラリー）。オーナーが渡した画像
  - GrisPlaceRavignan   グリス『開いた窓の前の静物（ラヴィニャン広場）』（1915、フィラデルフィア美術館）。Wikimedia Commons の幅 960 の縮小版
  - HammershoiDustMotes ハマスホイ『陽光の中で踊る塵』（1900、オードロップゴー美術館）。Wikimedia Commons の原寸

出典と許諾は unity/Assets/Models/LICENSES.md の「額の絵」。元の画像が無い絵は飛ばす（額は下塗りの布で組まれる）。

    python tools/make-paintings.py
"""

import os

from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(HERE)
RAW = os.path.join(ROOT, 'unity', 'RawAssets', 'art')
OUT = os.path.join(ROOT, 'unity', 'Assets', 'Textures', 'Furniture', 'Paintings')

LONG = 128

PAINTINGS = [
    ('MilletSpring', 'millet-spring.jpg'),
    ('TurnerTemeraire', 'turner-temeraire.jpg'),
    ('GrisPlaceRavignan', 'gris-place-ravignan.jpg'),
    ('HammershoiDustMotes', 'hammershoi-dust-motes.jpg'),
]


def shrink(src, dst):
    im = Image.open(src).convert('RGB')
    w, h = im.size
    scale = LONG / max(w, h)
    size = (max(1, round(w * scale)), max(1, round(h * scale)))
    small = im.resize(size, Image.BOX)
    small.save(dst, optimize=True)
    return im.size, size


def main():
    os.makedirs(OUT, exist_ok=True)
    for name, raw in PAINTINGS:
        src = os.path.join(RAW, raw)
        if not os.path.exists(src):
            print('無い（飛ばす）: ' + src)
            continue
        before, after = shrink(src, os.path.join(OUT, name + '.png'))
        print('%s: %dx%d -> %dx%d' % (name, before[0], before[1], after[0], after[1]))


if __name__ == '__main__':
    main()
