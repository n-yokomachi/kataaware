# -*- coding: utf-8 -*-
"""場面 3 のモニターに流す帯のテクスチャを描く。

427×240 の出力ではモニターの面が横 100 px ほどにしかならないので、字を並べても
潰れて読めない。設計書 7.1 も「文字として読める必要はない」と断っている。
そこで字は描かず、行ごとに長さの違う明るい帯だけを置いて文字の行に見せる。

  - 背景は黒、帯は白。**どちらも不透明で置く。** 色はマテリアル側で付ける。
    透明を使うと、Unity の取り込みが「透け際の縁取りを防ぐ」ために
    透明な画素の rgb へ白を滲ませる。画面は不透明のマテリアルなので alpha は見られず、
    滲んだ白だけが残って、行の無いただの明るい板になる
  - 行の 3 割は空にする。全部埋めると段落ではなく砂に見える
  - 上下の端は行間の真ん中で切る。縦へずらして流すので、継ぎ目が出ると気づかれる
  - 種は固定。走らせ直すたびに絵が変わると、差分がノイズになる

    python tools/make-terminal.py
"""

import os
import random

from PIL import Image, ImageDraw

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(HERE)
OUT = os.path.join(ROOT, 'unity', 'Assets', 'Textures')

SIZE = 256
SEED = 30301

ROWS = 25               # 上から数えた行の数
ROW_HEIGHT = 6          # 1 行の高さ
EMPTY_SHARE = 0.30      # 空にする行の割合

BARS = (2, 3)           # 1 行に置く帯の数
BAR_LENGTH = (12, 90)   # 帯の長さ
BAR_GAP = (4, 10)       # 帯と帯のあいだ


def rows():
    """
    行の上端。行送りは SIZE / ROWS で割り切れないので、丸めずに送ってから丸める。
    はじめの行を行間の半分だけ下げておくと、上の端と下の端が合わせて行間 1 つになり、
    縦へ繋いだときに切れ目が出ない
    """
    pitch = float(SIZE) / ROWS
    half = (pitch - ROW_HEIGHT) * 0.5
    return [int(round(i * pitch + half)) for i in range(ROWS)]


def bars(rng):
    """1 行ぶんの帯を左端から並べる。右の端に届いたらそこで打ち切る"""
    made = []
    x = 0
    for i in range(rng.randint(*BARS)):
        if i > 0:
            x += rng.randint(*BAR_GAP)
        room = SIZE - x
        if room < BAR_LENGTH[0]:
            break
        length = min(rng.randint(*BAR_LENGTH), room)
        made.append((x, length))
        x += length
    return made


def build():
    rng = random.Random(SEED)
    im = Image.new('RGBA', (SIZE, SIZE), (0, 0, 0, 255))
    d = ImageDraw.Draw(im)

    # 空にする行は数を決めて選ぶ。行ごとに賽を振ると、種を変えないまま割合がぶれる
    blanks = set(rng.sample(range(ROWS), int(round(ROWS * EMPTY_SHARE))))

    drawn = []
    for i, top in enumerate(rows()):
        if i in blanks:
            drawn.append([])
            continue
        made = bars(rng)
        for x, length in made:
            d.rectangle([x, top, x + length - 1, top + ROW_HEIGHT - 1], fill=(255, 255, 255, 255))
        drawn.append(made)
    return im, blanks, drawn


def main():
    if not os.path.isdir(OUT):
        raise SystemExit('テクスチャの置き場が無い: ' + OUT)
    im, blanks, drawn = build()
    path = os.path.join(OUT, 'TerminalRows.png')
    im.save(path)

    lengths = [length for row in drawn for _, length in row]
    print('  TerminalRows.png  ' + str(im.size))
    print('  ' + str(ROWS) + ' 行、うち空が ' + str(len(blanks)) + ' 行（'
          + str(round(100.0 * len(blanks) / ROWS, 1)) + ' %）')
    print('  帯 ' + str(len(lengths)) + ' 本、長さ ' + str(min(lengths)) + '〜' + str(max(lengths)) + ' px')


if __name__ == '__main__':
    main()
