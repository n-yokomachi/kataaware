# -*- coding: utf-8 -*-
"""
しっぽり明朝を、この作品で使う字だけに絞る。

元のフォントは 15,363 字・8.28 MB あって、書き出しに載せるには重い。
仮名と約物はまるごと、漢字は「学年か JLPT の指定があるもの」と
「場面・コード・原作・仕様書に出てくるもの」だけ残す。

  python tools/subset-font.py <元の ShipporiMincho.ttf>

出来たものは unity/Assets/Fonts/ShipporiMincho-Regular.ttf へ置く。
**台詞を足したら絞り直すこと。** 絞ったフォントに無い字は画面に出せない。
fonttools が要る（pip install fonttools）。
"""
import io
import os
import re
import glob
import subprocess
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
TOOLS = os.path.join(ROOT, "tools")
OUT = os.path.join(ROOT, "unity", "Assets", "Fonts", "ShipporiMincho-Regular.ttf")

# 仮名・ASCII・約物はまるごと。台詞はこれから増えるので惜しまない
RANGES = [
    (0x0020, 0x007E), (0x00A0, 0x00FF),
    (0x3000, 0x303F), (0x3040, 0x309F), (0x30A0, 0x30FF),
    (0x31F0, 0x31FF), (0xFF00, 0xFF65),
    (0x2010, 0x2027), (0x2030, 0x205E),
    (0x2190, 0x21FF), (0x2460, 0x24FF), (0x25A0, 0x25FF), (0x2600, 0x26FF),
]

ESCAPED = re.compile(r'\\u([0-9A-Fa-f]{4})')


def read(path):
    return io.open(path, encoding="utf-8", errors="replace").read()


def collect():
    """作品で使う字を集める"""
    chars = set()
    for lo, hi in RANGES:
        for c in range(lo, hi + 1):
            chars.add(chr(c))

    # 学年か JLPT の指定がある漢字
    chars.update(read(os.path.join(TOOLS, "joyo-kanji.txt")))

    # 場面の中の文字列。\uXXXX で入っているので戻す
    for p in glob.glob(os.path.join(ROOT, "unity", "Assets", "Scenes", "*.unity")):
        chars.update(ESCAPED.sub(lambda m: chr(int(m.group(1), 16)), read(p)))

    # コードの中の文字列
    for p in glob.glob(os.path.join(ROOT, "unity", "Assets", "**", "*.cs"), recursive=True):
        chars.update(read(p))

    # 原作と仕様書。これから書く台詞の語彙も拾えるように
    for pat in [os.path.join(ROOT, "docs", "reference", "*.md"),
                os.path.join(ROOT, "docs", "superpowers", "specs", "*.md"),
                os.path.join(ROOT, "docs", "superpowers", "plans", "*.md")]:
        for p in glob.glob(pat):
            chars.update(read(p))

    return set(c for c in chars if 0x20 <= ord(c) <= 0xFFFF)


def main():
    if len(sys.argv) < 2:
        print(__doc__)
        return 1
    src = sys.argv[1]
    if not os.path.exists(src):
        print("元のフォントが無い: %s" % src)
        return 1

    keep = collect()
    kanji = sum(1 for c in keep if 0x4E00 <= ord(c) <= 0x9FFF)
    print("残す字 %d（うち漢字 %d）" % (len(keep), kanji))

    text = os.path.join(TOOLS, "keep.txt")
    io.open(text, "w", encoding="utf-8").write("".join(sorted(keep)))

    cmd = [
        "pyftsubset", src,
        "--text-file=" + text,
        "--output-file=" + OUT,
        "--layout-features=kern,liga,palt,vert,vrt2",
        "--drop-tables+=DSIG",
        "--no-hinting",
        "--desubroutinize",
    ]
    r = subprocess.run(cmd, capture_output=True, text=True)
    os.remove(text)
    if r.returncode != 0:
        print(r.stdout)
        print(r.stderr)
        return r.returncode

    before = os.path.getsize(src)
    after = os.path.getsize(OUT)
    print("%.2f MB → %.2f MB（%.0f%%）" % (before / 1048576.0, after / 1048576.0, 100.0 * after / before))

    # 画面に出る字がすべて入ったか確かめる
    from fontTools.ttLib import TTFont
    cmap = TTFont(OUT).getBestCmap()
    shown = set()
    for p in glob.glob(os.path.join(ROOT, "unity", "Assets", "Scenes", "*.unity")):
        shown.update(ESCAPED.sub(lambda m: chr(int(m.group(1), 16)), read(p)))
    shown = set(c for c in shown if 0x20 <= ord(c) <= 0xFFFF)
    missing = sorted(c for c in shown if ord(c) not in cmap)
    print("場面に出る字 %d、入らなかったもの %d" % (len(shown), len(missing)))
    for c in missing:
        print("  U+%04X" % ord(c))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
