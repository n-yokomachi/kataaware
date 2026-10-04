# -*- coding: utf-8 -*-
"""同じ顔の三人（主人公・片割れ・場面 6 の過去の主人公）の顔のテクスチャを、リアル系のアニメ寄りに描き直す。

Unity の RocketboxAnimeFace（unity/Assets/Editor/Rocketbox/RocketboxAnimeFace.cs）と組で使う。オーナーが 2026-10-05 に採用
（検証は unity/Assets/Editor/Study/FaceAnime.cs）。

その人の Painted/Head_self.png（組み立ての描き直し BuildRocketboxProtagonist.Paint が書く絵。髪の色・口元の黒子・鼻の手入れ・唇の色を済ませた絵）の上に描き重ねる。
UV の並びはそのまま。手でなぞる代わりに、模型の上の位置（RocketboxAnimeFace.ExportMaps が書く地図）と、
目の玉の球（瞼の縁は、目の玉の中心から 15.8 mm の球と頭の面が交わる所）から部品の場所を決め、形（楕円・曲線・ぼかし）で塗る。

- 肌: 毛穴・しみ・赤みを均す（細かいむらを減らし、色むらを大きくぼかした色へ寄せ、焼き込まれた陰を浅く）。
  赤みと黄みの少ない色へ寄せる（Lab で彩度を下げ・黄みを抑える。体・胸元・膝から下にも同じ変換を掛けて、継ぎ目を揃える）。
  明るさを上げるのは顎から上だけ（首と胸まで上げると、モニターの映り込みで首と胸が白く平らになった）
- 目頭の上から眉頭の下の、元の絵の灰色がかった眼窩の影を肌へ戻す。鼻の穴の塊を、まわりの肌の暖かい暗さへ寄せてぼかし、鼻先の下半分の帯を明るくする
- 目元: 上瞼の際のアイライン（目尻で太く、目尻の先へ跳ね上げる）とまつ毛、二重の線、睫毛の際の薄いローズブラウンの影、下瞼の際の細い線、涙袋、目頭の赤み
- 眉: 元の眉を周りの肌で埋め、少し太く低く、眉頭の淡い・眉尻の細い眉を描き直す（茶色の髪の片割れは眉も茶色）
- 頬の紅（薄く。オーナー「チークはもっと薄く」）、鼻筋の明るみ、頬の下の陰、唇のグラデーション（唇の合わせ目ほど濃く、縁はぼかす）と下唇の明るみ
- 目の玉の絵: 虹彩を 1.36 倍の大きさの澄んだ琥珀にし、瞳孔とはっきりした光の点を描く（左右の目で同じ絵を使い、UV は裏返っていない。光の点は左右の目で同じ側に出る）
- 髪の房の絵の上のまつ毛を濃く太く
- 黒子は元の絵から写し戻す。片割れは模型ごと裏返して組むので、同じ絵のまま本人の右に来る

書くもの（その人の Painted/。元の絵は読むだけで書き換えない）:
- Head_anime.png（512）・Head_anime_mask.png（R 肌（顔は 1、首から下は 0.5）・G 髪・B 艶。線形）
- Hair_anime.png（512 RGBA）・Hair_anime_mask.png
- Body_anime.png と、あれば Chest_anime.png・Legs_anime.png（肌の色だけ同じ変換）と、それぞれの _mask.png

手順:
    Unity: HalfAware/Rocketbox/Anime face: export maps（地図を unity/Temp/AnimeFace/<人>/ に書く）
    py -3.12 tools/make-anime-face.py [人の名前 ...]（既定は三人。numpy・scipy・PIL が要る）
    Unity: HalfAware/Rocketbox/Anime face: apply（マテリアルを AnimeSkin にし、頭のメッシュに顔の比率を焼く）
"""
import os
import sys
import numpy as np
from PIL import Image
from scipy import ndimage

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(HERE)
UNITY = os.path.join(ROOT, 'unity')

# 顔の三人（RocketboxPerson.AnimeFace）
PEOPLE = ['Face14_Hair14_BodySports02', 'Face14_Hair14_MadeDress', 'Face14_Hair14_GardenWear']

# 人ごとに描く場所（main が人ごとに置き直す）
SRC = OUT = WORK = None

N = 1024          # 描く大きさ（書くときに 512 へ縮める）
PPM = 2.0         # 顔の前の 1 mm の画素（1024 で）。地図で測ると 0.50〜0.54 mm/px
EYE_RADIUS = 0.0158   # 目の玉の中心から瞼の縁まで（m）

# ---- 値の表（ここを詰める） ---------------------------------------------------

BASE_LOOK = dict(
    # 肌
    detail_keep=0.22,        # 細かいむら（4 px より細かい）を残す割合
    chroma_even=0.75,        # 色むらを大きくぼかした色へ寄せる量
    flatten=0.30,            # 焼き込まれた陰（目の下・ほうれい線・顎の下）を浅くする量
    tone_lift=0.08,          # 明るさ L の倍率から 1 を引いた値
    tone_chroma=0.74,        # 彩度の倍率
    tone_yellow=0.74,        # 黄み b の倍率
    # 目元（mm）
    liner_inner=1.6, liner_outer=3.0, wing_len=4.2, wing_rise=0.42,
    liner_colour=(0.055, 0.035, 0.035),
    lash_len=1.8,
    crease_off=2.6, crease_alpha=0.40, crease_colour=(0.70, 0.50, 0.47),
    shadow_reach=2.6, shadow_alpha=0.30, shadow_colour=(0.88, 0.66, 0.62),
    socket_lift=0.85,        # 目頭の上から眉頭の下の、元の絵の灰色がかった影を肌へ戻す量
    socket_bright=7.0,       # そこを形の陰のぶん明るくする量（L）
    nostril_soft=0.75,       # 鼻の穴の暗い所を、まわりの肌の暖かい暗さへ寄せる量
    under_nose=0.0,          # 鼻の下を向いた面を明るくする量
    nose_band=0.30,          # 鼻先の下半分から鼻の穴の高さの帯を明るくする量（形の陰の帯を和らげる）
    lower_alpha=0.50, lower_colour=(0.30, 0.19, 0.17),
    bag_alpha=0.10, bag_shadow=0.08,
    # 眉
    brow_colour=(0.16, 0.115, 0.105), brow_inner=4.0, brow_alpha=0.88, brow_straight=0.35, brow_drop=1.4,
    # 頬・鼻・唇
    contour=0.45,
    blush_alpha=0.10, blush_colour=(0.97, 0.56, 0.58), nose_blush=0.06,   # チークは薄く（オーナー、2026-10-05）
    nose_light=0.08,
    lip_colour=(0.86, 0.38, 0.43), lip_alpha=0.45, lip_gloss=0.30, lip_upper=6.6, lip_lower=7.2,
    # 目の玉
    iris_scale=1.36, pupil=0.36,
)
LOOK = BASE_LOOK


# ---- 色 -----------------------------------------------------------------------

def to_lin(c):
    c = np.clip(c, 0, 1)
    return np.where(c <= 0.04045, c / 12.92, ((c + 0.055) / 1.055) ** 2.4)


def to_srgb(c):
    c = np.clip(c, 0, 1)
    return np.where(c <= 0.0031308, c * 12.92, 1.055 * np.power(c, 1 / 2.4) - 0.055)


M_XYZ = np.array([[0.4124, 0.3576, 0.1805], [0.2126, 0.7152, 0.0722], [0.0193, 0.1192, 0.9505]])
WHITE = np.array([0.95047, 1.0, 1.08883])


def to_lab(rgb):
    xyz = to_lin(rgb) @ M_XYZ.T / WHITE
    f = np.where(xyz > 0.008856, np.cbrt(xyz), 7.787 * xyz + 16 / 116)
    L = 116 * f[..., 1] - 16
    a = 500 * (f[..., 0] - f[..., 1])
    b = 200 * (f[..., 1] - f[..., 2])
    return np.stack([L, a, b], -1)


def from_lab(lab):
    fy = (lab[..., 0] + 16) / 116
    fx = fy + lab[..., 1] / 500
    fz = fy - lab[..., 2] / 200
    f = np.stack([fx, fy, fz], -1)
    xyz = np.where(f > 0.2069, f ** 3, (f - 16 / 116) / 7.787) * WHITE
    lin = xyz @ np.linalg.inv(M_XYZ).T
    return to_srgb(lin)


def lum(rgb):
    return rgb @ np.array([0.299, 0.587, 0.114])


def smooth(e0, e1, x):
    t = np.clip((x - e0) / (e1 - e0), 0, 1)
    return t * t * (3 - 2 * t)


def blur(a, s):
    if a.ndim == 3:
        return np.stack([ndimage.gaussian_filter(a[..., i], s) for i in range(a.shape[-1])], -1)
    return ndimage.gaussian_filter(a, s)


def masked_blur(a, w, s):
    """w の重みでぼかす（重みの無い所の値を混ぜない）"""
    if a.ndim == 3:
        num = blur(a * w[..., None], s)
        den = blur(w, s)[..., None]
    else:
        num = blur(a * w, s)
        den = blur(w, s)
    return num / np.maximum(den, 1e-6)


def tone(rgb, k=None, lift=None):
    """肌の色の変換。明るく、赤みと黄みを抑える（色は頭・胸元・腕に同じに掛ける）。
    明るさを上げるのは顔だけ（lift は画素ごとの 0〜1。None なら 0）。首から下まで明るくすると、モニターの映り込みで首と胸が白く平らになった"""
    k = k or LOOK
    lab = to_lab(rgb)
    # 明るさは掛け算で上げる（100 へ寄せると暗い所ほど持ち上がり、顎の下の陰が薄れて顎と首の境が消えた）
    if lift is not None:
        lab[..., 0] = np.minimum(lab[..., 0] * (1 + k['tone_lift'] * lift), 96)
    lab[..., 1] *= k['tone_chroma']
    lab[..., 2] *= k['tone_chroma'] * k['tone_yellow']
    return np.clip(from_lab(lab), 0, 1)


def mix(img, colour, alpha):
    return img + (np.asarray(colour) - img) * alpha[..., None]


def multiply(img, colour, alpha):
    return img * (1 + (np.asarray(colour) - 1) * alpha[..., None])


def screen(img, colour, alpha):
    c = np.asarray(colour) * alpha[..., None]
    return 1 - (1 - img) * (1 - c)


# ---- 筆（柔らかい円を並べて線を描く） ------------------------------------------

def stroke(shape, pts, radii, feather=0.8, alphas=None):
    """点の列 pts（行, 列）に、半径 radii の柔らかい円を置いた重なり（最大）。alphas があれば点ごとの濃さ"""
    out = np.zeros(shape, np.float32)
    for i, (r, c) in enumerate(pts):
        rad = radii[i]
        a = 1.0 if alphas is None else alphas[i]
        if rad <= 0 or a <= 0:
            continue
        R = int(np.ceil(rad + feather + 1))
        r0, r1 = max(0, int(r) - R), min(shape[0], int(r) + R + 2)
        c0, c1 = max(0, int(c) - R), min(shape[1], int(c) + R + 2)
        yy, xx = np.mgrid[r0:r1, c0:c1]
        d = np.sqrt((yy + 0.5 - r) ** 2 + (xx + 0.5 - c) ** 2)
        v = smooth(rad + feather, rad - feather, d) * a
        out[r0:r1, c0:c1] = np.maximum(out[r0:r1, c0:c1], v)
    return out


# ---- 読む ---------------------------------------------------------------------

def load_rgb(path, n=None):
    im = Image.open(path).convert('RGB')
    if n is not None and im.size[0] != n:
        im = im.resize((n, n), Image.LANCZOS)
    return np.asarray(im, np.float32) / 255


def save_rgb(rgb, path, n=512):
    im = Image.fromarray((np.clip(rgb, 0, 1) * 255 + 0.5).astype(np.uint8), 'RGB')
    if im.size[0] != n:
        im = im.resize((n, n), Image.LANCZOS)
    im.save(path)


def save_rgba(rgba, path, n=512):
    im = Image.fromarray((np.clip(rgba, 0, 1) * 255 + 0.5).astype(np.uint8), 'RGBA')
    if im.size[0] != n:
        im = im.resize((n, n), Image.LANCZOS)
    im.save(path)


def anchors():
    a = {}
    for line in open(os.path.join(WORK, 'anchors.txt'), encoding='utf-8'):
        p = line.split()
        if len(p) >= 4 and not p[0].startswith('eyeUv'):
            a[p[0]] = np.array([float(p[1]), float(p[2]), float(p[3])])
    return a


# ---- 頭 -----------------------------------------------------------------------

def paint_head(debug=False):
    k = LOOK
    pos = np.fromfile(os.path.join(WORK, 'head_map_%d.bin' % N), np.float32).reshape(N, N, 4)[::-1]
    X, Y, Z, On = pos[..., 0], pos[..., 1], pos[..., 2], pos[..., 3] > 0.5
    lab_, n_ = ndimage.label(On)
    sizes = ndimage.sum(On, lab_, range(1, n_ + 1))
    main = lab_ == (np.argmax(sizes) + 1)
    A = anchors()
    hair512 = np.fromfile(os.path.join(WORK, 'head_hair_512.bin'), np.float32).reshape(512, 512)[::-1]
    hair = np.asarray(Image.fromarray(hair512).resize((N, N), Image.BILINEAR))
    base = load_rgb(os.path.join(SRC, 'Head_self.png'), N)
    img = base.copy()

    skin = main & (hair < 0.5)
    skinw = ndimage.gaussian_filter(skin.astype(np.float32), 1.0) * main
    front = main & (Z > 0.0)

    # 顔の部品の画素の位置（模型の上の点に一番近い、顔の前の画素）
    def at(p, mask=front):
        d = (X - p[0]) ** 2 + (Y - p[1]) ** 2 + 0.25 * (Z - p[2]) ** 2
        d = np.where(mask, d, 1e9)
        i = np.argmin(d)
        return np.array(np.unravel_index(i, d.shape), np.float32)

    # 目の縁（目の玉の中心からの距離で決める）
    eyes = {}
    for side in 'LR':
        C = A['eye' + side]
        d = np.sqrt((X - C[0]) ** 2 + (Y - C[1]) ** 2 + (Z - C[2]) ** 2)
        hole = ndimage.binary_opening(main & (d < EYE_RADIUS))
        lab2, n2 = ndimage.label(hole)
        s2 = ndimage.sum(hole, lab2, range(1, n2 + 1))
        hole = lab2 == (np.argmax(s2) + 1)
        ys, xs = np.where(hole)
        cols = np.arange(xs.min(), xs.max() + 1)
        top = np.array([ys[xs == c].min() if np.any(xs == c) else np.nan for c in cols], np.float32)
        bot = np.array([ys[xs == c].max() + 1 if np.any(xs == c) else np.nan for c in cols], np.float32)
        ok = ~np.isnan(top)
        top = np.interp(cols, cols[ok], top[ok])
        bot = np.interp(cols, cols[ok], bot[ok])
        top = ndimage.gaussian_filter1d(top, 1.5)
        bot = ndimage.gaussian_filter1d(bot, 1.5)
        # 目頭は鼻の側（画像の真ん中の列 512 に近い端）
        inner_first = abs(cols[0] - 512) < abs(cols[-1] - 512)
        eyes[side] = dict(hole=hole, cols=cols.astype(np.float32), top=top, bot=bot, inner_first=inner_first)

    # 部品のまわりを、肌を均す処理から外す
    protect = np.zeros((N, N), np.float32)
    mole = at(A['mole'])
    yy, xx = np.mgrid[0:N, 0:N]
    protect = np.maximum(protect, smooth(8, 5, np.hypot(yy - mole[0], xx - mole[1])))
    for e in eyes.values():
        protect = np.maximum(protect, ndimage.gaussian_filter(ndimage.binary_dilation(e['hole'], iterations=2).astype(np.float32), 1.0))

    # ---- 唇の形（肌を均す前に決める）----
    # 口の端の二点（口の端のボーンに一番近い顔の画素）と、その間の唇の合わせ目（元の絵で列ごとに一番暗い行）から、
    # 上唇と下唇の輪郭を形で決める。上唇の真ん中に山のくぼみ（唇の山）
    mL, mR = at(A['mouthL']), at(A['mouthR'])
    c0, c1 = sorted([mL[1], mR[1]])
    cm, hw = (c0 + c1) / 2, (c1 - c0) / 2
    Lb = lum(base)
    rmid = (mL[0] + mR[0]) / 2
    lcols = np.arange(int(c0) - 3, int(c1) + 4)
    seam = np.array([int(rmid) - 12 + np.argmin(Lb[int(rmid) - 12:int(rmid) + 12, c]) for c in lcols], np.float32)
    # 端の列は口の外の暗い所を拾うので、真ん中 8 割の列に 4 次の式を合わせて端へ延ばす
    mid = np.abs(lcols + 0.5 - cm) < 0.8 * hw
    seam = np.polyval(np.polyfit(lcols[mid], seam[mid], 4), lcols).astype(np.float32)
    sc = np.interp(xx + 0.5, lcols + 0.5, seam)
    tt = (xx + 0.5 - cm) / hw
    inside = np.clip(1 - tt * tt, 0, 1)
    up = sc - k['lip_upper'] * PPM * inside ** 0.55 * (1 - 0.16 * np.exp(-(tt / 0.13) ** 2))
    dn = sc + k['lip_lower'] * PPM * inside ** 0.75
    lips = (smooth(up - 1.5, up + 1.5, yy) * smooth(dn + 1.5, dn - 1.5, yy) * smooth(1.08, 0.98, np.abs(tt))).astype(np.float32)
    lip_seam = sc

    # ---- 元の眉を埋める ----
    # 元の眉の濃い所は髪と見なされている（頭の絵の髪の重み）。眉の所は肌に戻してから埋める
    brows = {}
    L0 = lum(img)
    bmask = {}
    for side in 'LR':
        bi, bo = A['browIn' + side], A['browOut' + side]
        zone = front & (np.abs(X - (bi[0] + bo[0]) / 2) < abs(bo[0] - bi[0]) / 2 + 0.013) & (Y > A['eye' + side][1] + 0.0095) & (Y < bi[1] + 0.014)
        ref = masked_blur(L0, zone.astype(np.float32), 10)
        dark = zone & (L0 < ref * 0.82)
        dark = ndimage.binary_opening(dark)
        lab3, n3 = ndimage.label(dark)
        if n3 == 0:
            continue
        s3 = ndimage.sum(dark, lab3, range(1, n3 + 1))
        big = np.argmax(s3) + 1
        # 一番大きい塊と、その近く（3 px 以内）の塊を眉とする
        near = ndimage.binary_dilation(lab3 == big, iterations=4)
        keep = [i for i in range(1, n3 + 1) if s3[i - 1] > 6 and np.any(near & (lab3 == i))]
        bm = np.isin(lab3, keep)
        bmask[side] = (bm, zone)
    for side, (bm, zone) in bmask.items():
        hair = np.where(ndimage.binary_dilation(bm, iterations=4) & zone, 0.0, hair).astype(np.float32)
    skin = main & (hair < 0.5)
    skinw = ndimage.gaussian_filter(skin.astype(np.float32), 1.0) * main
    for side, (bm, zone) in bmask.items():
        e = eyes[side]
        ys, xs = np.where(bm)
        w = np.maximum((masked_blur(L0, zone.astype(np.float32), 10) - L0)[bm], 1e-3)
        cols = np.arange(xs.min(), xs.max() + 1)
        cen = np.array([np.average(ys[xs == c], weights=w[xs == c]) if np.any(xs == c) else np.nan for c in cols])
        ok = ~np.isnan(cen)
        fit = np.polyfit(cols[ok], cen[ok], 2)
        brows[side] = dict(cols=cols, fit=fit, inner_first=e['inner_first'])
        fillmask = ndimage.binary_dilation(bm, iterations=3)
        support = (skin & ~fillmask).astype(np.float32)
        fill = masked_blur(img, support, 4)
        fill2 = masked_blur(img, support, 9)
        f = ndimage.gaussian_filter(fillmask.astype(np.float32), 1.2)
        img = mix(img, 0.5 * fill + 0.5 * fill2, f * main)

    # ---- 肌を均す ----
    face = front & skin
    F = ndimage.gaussian_filter((main & (Y > 1.505) & (hair < 0.5)).astype(np.float32), 5) * skinw
    amt = F * (1 - protect)
    amt_c = amt * (1 - lips)
    lab = to_lab(img)
    w = skinw
    low = masked_blur(lab, w, 4.0)
    detail = lab - low
    lab = lab - detail * (1 - k['detail_keep']) * amt[..., None]
    # 色むら（a, b）を大きくぼかした色へ
    chroma_low = masked_blur(lab[..., 1:], w, 14.0)
    lab[..., 1:] = lab[..., 1:] + (chroma_low - lab[..., 1:]) * (k['chroma_even'] * amt_c)[..., None]
    # 焼き込まれた陰を浅く
    Llow = masked_blur(lab[..., 0], w, 22.0)
    Lref = masked_blur(lab[..., 0], w, 70.0)
    lab[..., 0] = lab[..., 0] + np.clip(Lref - Llow, -2, 30) * k['flatten'] * amt
    img = np.clip(from_lab(lab), 0, 1)

    # ---- 肌の色 ----
    # 明るさを上げるのは顎から上（顎の下 1.5〜5 cm でなだらかに 0 へ）
    img = mix(img, tone(img, lift=smooth(1.475, 1.515, Y)), skinw * main * (1 - 0.6 * lips))

    # ---- 目頭の上の影を肌へ戻す ----
    # 元の絵の眼窩の灰色がかった影（目頭の上から眉頭の下）が、くま・疲れに見えた。
    # 上瞼の際から 1.8 mm より上、眉の下まで、目頭の側ほど強く、額の肌の明るさと色へ寄せる
    labs = to_lab(img)
    wide = masked_blur(labs, (skinw * front * (1 - protect)).astype(np.float32), 30.0)
    for side in 'LR':
        e = eyes[side]
        cols, top = e['cols'], e['top']
        n = len(cols)
        s_ = np.linspace(0, 1, n) if e['inner_first'] else np.linspace(1, 0, n)
        inner_c = cols[0] if e['inner_first'] else cols[-1]
        out_d = 1.0 if e['inner_first'] else -1.0
        cc = np.clip(xx, cols.min(), cols.max())
        topc = np.interp(cc, cols, top)
        sc_ = np.interp(cc, cols, s_)
        # 目頭の外（鼻の側）へ 6 mm までと、11 mm までなだらかに延ばす（鼻筋の脇の影まで）。目頭より鼻の側では、目頭の高さの 2 mm 下まで下ろす（目頭の脇の影も拾う）
        past = np.clip(-(xx - inner_c) * out_d, 0, None)
        ii = 0 if e['inner_first'] else n - 1
        corner_mid = (e['top'][ii] + e['bot'][ii]) / 2
        pw = smooth(0.0, 2.0 * PPM, past)
        above = (topc + pw * (corner_mid + 2.0 * PPM + 2.4 * PPM - topc)) - yy
        horiz = smooth(11.0 * PPM, 6.0 * PPM, past) * (1 - 0.55 * smooth(0.2, 0.8, sc_))
        b = brows.get(side)
        if b is not None:
            # 元の眉の真ん中の線から 2 mm 下（眉の下の縁）より下
            # 眉の列の外（眉頭より鼻の側）は、際から 8 mm 上まで
            browc = np.polyval(b['fit'], np.clip(xx, b['cols'].min(), b['cols'].max())) + 2.0 * PPM
            inb = (xx >= b['cols'].min()) & (xx <= b['cols'].max())
            cap = np.where(inb, smooth(0.0, 2.0 * PPM, yy - browc), smooth(9.0 * PPM, 7.0 * PPM, above))
            vert = smooth(1.2 * PPM, 2.4 * PPM, above) * cap
        else:
            vert = smooth(1.2 * PPM, 2.4 * PPM, above) * smooth(10 * PPM, 6 * PPM, above)
        w_s = ndimage.gaussian_filter(np.clip(vert * horiz, 0, 1) * front, 4.0) * skinw * k['socket_lift']
        dark = np.clip((wide[..., 0] - labs[..., 0]) / 10.0, 0, 1)
        labs[..., 0] += (wide[..., 0] - labs[..., 0]) * w_s * np.maximum(dark, 0.4)
        labs[..., 1:] += (wide[..., 1:] - labs[..., 1:]) * (w_s * 0.9)[..., None]
        # 眼窩のくぼみは形の陰でも暗くなるので、そのぶん絵を少し明るく、赤みを少し足す
        labs[..., 0] += w_s * k['socket_bright']
        labs[..., 1] += w_s * 2.0
    img = np.clip(from_lab(labs), 0, 1)

    # ---- 鼻の穴 ----
    # 鼻の下の穴と小鼻の脇の灰色がかった茶色の塊を、まわりの肌の暖かい暗さへ寄せ、縁をぼかす
    nz0 = A['nose']
    zone_n = front & (np.abs(X) < 0.016) & (Y > nz0[1] - 0.020) & (Y < nz0[1] + 0.004)
    zf = ndimage.gaussian_filter(zone_n.astype(np.float32), 3.0)
    # まわりの肌（暗い所を除いた明るい側）の色
    Ln = lum(img)
    med = masked_blur(Ln, zone_n.astype(np.float32), 9.0)
    ref = masked_blur(img, (zone_n & skin & (Ln > med * 0.97)).astype(np.float32), 9.0)
    dL = np.clip((lum(ref) - Ln) / 0.12, 0, 1)
    # 暗さは元の半分ほどに抑えた暖かい陰（灰に寄らないよう、赤みを少し残す）
    warm = ref * (1 - 0.10 * dL)[..., None] * np.array([1.0, 0.96, 0.94])
    soft_w = ndimage.gaussian_filter(dL * zf, 3.0)
    warm = blur(warm, 3.0)
    img = mix(img, warm, np.clip(soft_w * 2.5, 0, 1) * k['nostril_soft'])
    # 鼻の下を向いた面（鼻先の下・鼻の穴のまわり）は、形の陰で暗い帯になり、上唇との境で縁が硬く見えた。
    # 下を向いた面ほど絵を明るく暖かくして、陰の帯を周りの肌へ近づける（面の向きは地図の位置の差から求める）
    gy, gx = np.gradient(Y), np.gradient(X)
    du = np.stack([np.gradient(X, axis=1), np.gradient(Y, axis=1), np.gradient(Z, axis=1)], -1)
    dv = np.stack([np.gradient(X, axis=0), np.gradient(Y, axis=0), np.gradient(Z, axis=0)], -1)
    nrm = np.cross(du, dv)
    nrm /= np.maximum(np.linalg.norm(nrm, axis=-1, keepdims=True), 1e-9)
    # 顔の前の面で法線が前（+z）を向くよう、向きを揃える
    if np.median(nrm[front & (np.abs(X) < 0.02) & (np.abs(Y - nz0[1]) < 0.01), 2]) < 0:
        nrm = -nrm
    down = smooth(-0.05, -0.55, nrm[..., 1])
    zone_u = front & (np.abs(X) < 0.017) & (Y > nz0[1] - 0.022) & (Y < nz0[1] + 0.002)
    w_u = ndimage.gaussian_filter((down * zone_u).astype(np.float32), 3.0) * skinw
    # 鼻先の下半分から鼻の穴の高さ（鼻先の一番前の点から 1〜9 mm 下）の帯。正面から見て、ここが灰色がかった暗い帯になった
    tipz = np.where(front & (np.abs(X) < 0.004) & (np.abs(Y - nz0[1]) < 0.012), Z, -1.0)
    ytip = Y.flat[np.argmax(tipz)]
    band = np.exp(-((Y - (ytip - 0.005)) / 0.0045) ** 2) * smooth(0.018, 0.010, np.abs(X)) * front
    w_b = ndimage.gaussian_filter(band.astype(np.float32), 2.0) * skinw
    lift_n = k['under_nose'] * w_u + k['nose_band'] * w_b
    img = np.clip(img * (1 + lift_n)[..., None] * (1 - 0.05 * np.clip(lift_n * 3, 0, 1))[..., None] ** np.array([0.0, 1.0, 1.5]), 0, 1)

    # ---- 目元 ----
    for side in 'LR':
        e = eyes[side]
        cols, top, bot = e['cols'], e['top'], e['bot']
        n = len(cols)
        s = np.linspace(0, 1, n) if e['inner_first'] else np.linspace(1, 0, n)
        out_dir = 1.0 if (cols[-1] - cols[0]) * (1 if e['inner_first'] else -1) > 0 else -1.0
        # 目尻の角
        oi = n - 1 if e['inner_first'] else 0
        ii = 0 if e['inner_first'] else n - 1
        corner = np.array([(top[oi] + bot[oi]) / 2, cols[oi]])
        inner = np.array([(top[ii] + bot[ii]) / 2, cols[ii]])

        # 上瞼のぼかしの影（際から shadow_reach mm 上まで）
        reach = k['shadow_reach'] * PPM
        cc = np.clip(xx, cols.min(), cols.max())
        topc = np.interp(cc, cols, top)
        sc = np.interp(cc, cols, s)
        above = topc - yy
        hor = smooth(-3 * PPM, 0, (xx - cols.min()) * 1.0) * smooth(-3 * PPM, 0, (cols.max() - xx) * 1.0)
        # 目尻の外は少し広げる
        outer_ext = smooth(-4 * PPM, 0, out_dir * (xx - corner[1]) * -1 + 0)  # 1 inside
        a_sh = smooth(reach, 0, above) * smooth(-1.0, 1.5, above) * np.maximum(hor, 0) * smooth(0.0, 0.4, sc) * k['shadow_alpha'] * front
        img = multiply(img, k['shadow_colour'], a_sh)

        # 二重の線
        pts, rad, al = [], [], []
        for j in range(n):
            if s[j] < 0.06:
                continue
            off = (k['crease_off'] + 0.6 * s[j]) * PPM
            pts.append((top[j] - off, cols[j]))
            rad.append(0.45 * PPM * (0.7 + 0.3 * s[j]))
            al.append(smooth(0.06, 0.3, s[j]))
        # 目尻の先へ少し伸ばす
        last = np.array(pts[-1] if e['inner_first'] else pts[0])
        for t in np.linspace(0, 1, 8)[1:]:
            pts.append((last[0] + t * 1.0 * PPM, last[1] + out_dir * t * 2.2 * PPM))
            rad.append(0.4 * PPM * (1 - 0.5 * t))
            al.append(1 - t)
        a_cr = stroke((N, N), pts, rad, 0.9, al) * k['crease_alpha']
        img = multiply(img, k['crease_colour'], a_cr)

        # アイライン（上瞼の際の帯。目尻で太く）
        pts, rad = [], []
        for j in range(n):
            th = (k['liner_inner'] + (k['liner_outer'] - k['liner_inner']) * s[j] ** 1.4) * PPM
            # 目頭は細く絞る
            th *= 0.45 + 0.55 * smooth(0.0, 0.18, s[j])
            pts.append((top[j] - th / 2 + 0.8, cols[j]))
            rad.append(th / 2)
        # 跳ね上げ: 目尻の角から外へ、上へ
        th_end = k['liner_outer'] * PPM
        L_w = k['wing_len'] * PPM
        d = np.array([-k['wing_rise'], out_dir])
        d = d / np.linalg.norm(d)
        start = np.array([min(corner[0], top[oi] + th_end * 0.25), corner[1]])
        for t in np.linspace(0, 1, 24):
            p = start + d * L_w * t
            r_ = th_end / 2 * (1 - t) + 0.35 * t
            pts.append((p[0] - r_ + 0.5, p[1]))
            rad.append(r_)
        a_ln = stroke((N, N), pts, rad, 0.7)
        # まつ毛（際の上へ、外へ向けて）
        lpts, lrad = [], []
        rng = np.random.default_rng(7 if side == 'L' else 11)
        for j in range(0, n, 2):
            if s[j] < 0.15:
                continue
            th = (k['liner_inner'] + (k['liner_outer'] - k['liner_inner']) * s[j] ** 1.4) * PPM
            base_p = np.array([top[j] - th + 1.0, cols[j]])
            ang = np.deg2rad(15 + 45 * s[j] + rng.uniform(-8, 8))
            v = np.array([-np.cos(ang), out_dir * np.sin(ang)])
            ln = k['lash_len'] * PPM * (0.6 + 0.6 * s[j]) * rng.uniform(0.8, 1.1)
            for t in np.linspace(0, 1, 6):
                p = base_p + v * ln * t
                lpts.append((p[0], p[1]))
                lrad.append(0.55 * (1 - 0.6 * t))
        a_lash = stroke((N, N), lpts, lrad, 0.5) * 0.85
        a_ln = np.maximum(a_ln, a_lash)
        img = mix(img, k['liner_colour'], a_ln)

        # 下瞼の際の細い線（目尻側の 2/3）
        pts, rad, al = [], [], []
        for j in range(n):
            if s[j] < 0.25:
                continue
            pts.append((bot[j] + 0.4 * PPM - 0.6, cols[j]))
            rad.append(0.40 * PPM * (0.6 + 0.6 * s[j]))
            al.append(smooth(0.25, 0.55, s[j]))
        a_lo = stroke((N, N), pts, rad, 0.8, al) * k['lower_alpha']
        img = mix(img, k['lower_colour'], a_lo)

        # 涙袋（下瞼の下の明るみと、その下の淡い影）
        botc = np.interp(cc, cols, bot)
        below = yy - botc
        hor2 = smooth(0.08, 0.3, sc) * smooth(1.0, 0.8, sc) * np.maximum(hor, 0)
        a_bag = smooth(1.0 * PPM, 2.2 * PPM, below) * smooth(3.8 * PPM, 2.4 * PPM, below) * hor2 * k['bag_alpha'] * front
        img = screen(img, (1.0, 0.93, 0.92), a_bag)
        a_bs = smooth(3.0 * PPM, 3.8 * PPM, below) * smooth(5.0 * PPM, 3.9 * PPM, below) * hor2 * k['bag_shadow'] * front
        img = multiply(img, (0.86, 0.74, 0.72), a_bs)

        # 目頭の赤み
        a_in = stroke((N, N), [(inner[0], inner[1] - out_dir * 0.8 * PPM)], [0.9 * PPM], 1.2) * 0.45
        img = mix(img, (0.92, 0.58, 0.58), a_in)
        e['corner'] = corner
        e['out_dir'] = out_dir

    # ---- 眉を描き直す ----
    for side, b in brows.items():
        e = eyes[side]
        cols = b['cols']
        fit = b['fit']
        n = len(cols)
        c_in = cols[0] if b['inner_first'] else cols[-1]
        c_out = cols[-1] if b['inner_first'] else cols[0]
        out_dir = e['out_dir']
        # 眉頭を 0.8 mm 内へ、眉尻を 0.5 mm 短く
        c_in = c_in - out_dir * 0.8 * PPM
        c_out = c_out - out_dir * 0.5 * PPM
        cs = np.linspace(c_in, c_out, 120)
        curve = np.polyval(fit, cs)
        line = np.linspace(curve[0], curve[-1], len(cs))
        cen = curve * (1 - k['brow_straight']) + line * k['brow_straight'] + k['brow_drop'] * PPM
        ss = np.linspace(0, 1, len(cs))
        wdt = (k['brow_inner'] - 0.5 * ss - 2.4 * ss ** 3) * PPM
        al = (0.45 + 0.55 * smooth(0.0, 0.35, ss)) * (1 - 0.25 * smooth(0.8, 1.0, ss))
        a_br = stroke((N, N), list(zip(cen, cs)), wdt / 2, 1.0, al)
        # 毛の流れ（細い筋の濃淡）
        streak = 0.85 + 0.15 * np.sin((xx * 0.9 + yy * 2.3)) * np.sin(xx * 0.37)
        a_br = a_br * streak * k['brow_alpha'] * front
        img = mix(img, k['brow_colour'], a_br)

    # ---- 輪郭の陰（頬の下から顎の脇を少し暗くして、顔を細く見せる） ----
    for side in 'LR':
        sx = -1.0 if side == 'L' else 1.0
        cx = A['mouth' + side][0] + sx * 0.020
        r2 = ((X - cx) / 0.012) ** 2 + ((Y - (A['mouth' + side][1] + 0.004)) / 0.022) ** 2
        img = multiply(img, (0.86, 0.78, 0.78), np.exp(-r2 * 1.3) * k['contour'] * front * skinw)

    # ---- 頬の紅 ----
    for side in 'LR':
        c = A['cheek' + side]
        cx = c[0] + (0.002 if side == 'R' else -0.002)
        cy = c[1] + 0.002
        r2 = ((X - cx) / 0.018) ** 2 + ((Y - cy) / 0.011) ** 2
        a_bl = np.exp(-r2 * 1.2) * k['blush_alpha'] * front * skinw
        img = multiply(img, k['blush_colour'], a_bl)
    # 鼻の頭にも薄く
    nz = A['nose']
    r2 = (X / 0.006) ** 2 + ((Y - nz[1] + 0.002) / 0.005) ** 2
    img = multiply(img, k['blush_colour'], np.exp(-r2 * 1.5) * k['nose_blush'] * front)

    # ---- 鼻筋の明るみ ----
    eyeY = (A['eyeL'][1] + A['eyeR'][1]) / 2
    a_nl = smooth(0.0065, 0.0010, np.abs(X)) * smooth(nz[1] + 0.002, nz[1] + 0.008, Y) * smooth(eyeY + 0.002, eyeY - 0.004, Y) * front * (Z > 0.04)
    img = screen(img, (1.0, 0.97, 0.95), a_nl * k['nose_light'])

    # ---- 唇 ----
    # 合わせ目ほど濃いコーラルの桃色、縁へ向けて淡く（グラデーション）。輪郭は少しぼかす
    dist = np.abs(yy - lip_seam)
    half = np.where(yy < lip_seam, lip_seam - up, dn - lip_seam)
    g = 1 - smooth(0.0, 1.0, dist / np.maximum(half, 1.0))
    # 口の端へは色を薄く（口を小さく見せる）
    ends = smooth(0.95, 0.55, np.abs(tt))
    img = mix(img, k['lip_colour'], lips * (0.35 + 0.65 * g) * k['lip_alpha'] * (0.35 + 0.65 * ends))
    soft = ndimage.gaussian_filter(lips, 2.5)
    img = mix(img, k['lip_colour'], np.clip(soft - lips, 0, 1) * 0.30)
    # 合わせ目を締める
    img = multiply(img, (0.62, 0.36, 0.38), smooth(1.4 * PPM, 0.2 * PPM, dist) * smooth(1.02, 0.85, np.abs(tt)) * (np.abs(tt) < 1.05) * 0.55)
    # 下唇の明るみ（真ん中の少し下）
    hl_r = np.interp(cm, lcols + 0.5, seam) + 0.45 * k['lip_lower'] * PPM
    a_hl = np.exp(-(((yy - hl_r) / (1.1 * PPM)) ** 2 + ((xx - cm) / (3.2 * PPM)) ** 2)) * lips
    img = screen(img, (1.0, 0.92, 0.92), a_hl * 0.40)

    # ---- 黒子を元へ戻す ----
    dm = np.hypot(yy - mole[0], xx - mole[1])
    a_m = smooth(4.5, 2.5, dm) * (lum(base) < lum(img) - 0.08)
    a_m = ndimage.gaussian_filter(a_m.astype(np.float32), 0.6)
    img = mix(img, base, np.clip(a_m * 1.2, 0, 1))

    # ---- 目の玉の絵 ----
    eyeball = lab_ == lab_[int(round((1 - 0.0675) * N)), int(round(0.2634 * N))]
    face_img = img
    img = paint_eyeball(face_img, eyeball)

    # ---- 部位の絵 ----
    mask = np.zeros((N, N, 3), np.float32)
    # R: 肌。顔の肌は 1、首から下の肌は 0.5（体の肌。シェーダーが陰を深く当てる）。顎の下 1.5〜5 cm でなだらかに移す（胸元の面との継ぎ目で当て方を揃える）
    faceness = smooth(1.475, 1.515, Y)
    mask[..., 0] = np.clip(skinw * main * (0.5 + 0.5 * faceness), 0, 1)
    mask[..., 1] = np.clip(hair * On * (1 - mask[..., 0]), 0, 1)
    mask[..., 2] = np.clip(lips * k['lip_gloss'], 0, 1)
    mask[eyeball] = [0, 0, 0.85]

    # 島の外（縮めた絵やミップマップで縁に混ざる所）は、一番近い島の中の画素で埋める（元の絵の島の外の肌色が縁に橙の線で出たため）
    _, near = ndimage.distance_transform_edt(~On, return_indices=True)
    img = img[near[0], near[1]]
    mask = mask[near[0], near[1]]
    save_rgb(img, os.path.join(OUT, 'Head_anime.png'))
    save_rgb(mask, os.path.join(OUT, 'Head_anime_mask.png'))
    if debug:
        dbg = img.copy()
        for e in eyes.values():
            h = e['hole']
            dbg[h & ~ndimage.binary_erosion(h)] = [0, 1, 0]
        Image.fromarray((np.clip(dbg[200:480, 360:680], 0, 1) * 255).astype(np.uint8)).resize((960, 840), Image.NEAREST).save(os.path.join(WORK, 'dbg_face_paint.png'))
        Image.fromarray((np.clip(img[860:1020, 190:350], 0, 1) * 255).astype(np.uint8)).resize((480, 480), Image.NEAREST).save(os.path.join(WORK, 'dbg_eyeball_paint.png'))
        Image.fromarray((np.clip(img, 0, 1) * 255).astype(np.uint8)).save(os.path.join(WORK, 'dbg_head_1024.png'))
    return img


# 虹彩の色（琥珀。台詞の原稿の独白に「琥珀色の目」）: 上の暗い色・下の明るい色・瞳のまわりの明るい輪・縁の暗い輪・下の縁の照り返し
IRIS = dict(deep=np.array([0.30, 0.13, 0.04]), bright=np.array([0.96, 0.66, 0.22]),
            collar=(1.0, 0.85, 0.55), limbal=np.array([0.10, 0.05, 0.03]), glow=(1.0, 0.80, 0.45))


def paint_eyeball(img, island):
    """目の玉の絵を描き直す。虹彩を大きく澄んだ琥珀に、瞳孔と光の点。白目は滑らかに"""
    pal = IRIS
    k = LOOK
    cy, cx = (1 - 0.0675) * N, 0.2634 * N
    R0 = 0.0171 * N
    R = R0 * k['iris_scale']
    yy, xx = np.mgrid[0:N, 0:N].astype(np.float32)
    dy, dx = yy + 0.5 - cy, xx + 0.5 - cx
    r = np.hypot(dx, dy)
    ang = np.arctan2(dy, dx)
    out = img.copy()

    # 白目: 元の明るさの大きな流れだけ残し、血管と細かいむらを消す。上の縁は瞼の影で少し灰に
    white = np.array([0.90, 0.89, 0.885])
    shade = smooth(R * 1.2, R * 2.9, r)
    top_shadow = smooth(-R * 0.2, -R * 1.6, dy) * 0.38
    sclera = white * (1 - 0.34 * shade)[..., None] * (1 - top_shadow)[..., None]
    sclera = sclera * np.array([1.0, 0.985, 0.99])
    a_scl = smooth(R * 3.0, R * 2.4, r) * island
    out = mix(out, sclera, a_scl)

    # 虹彩: 上は暗く（瞼の影）、下は明るい琥珀。縁の暗い輪、瞳のまわりの明るい輪、放射の筋
    t = np.clip(r / R, 0, 1.5)
    vert = smooth(-R, R, dy)                      # 0 上 → 1 下
    deep, bright = pal['deep'], pal['bright']
    col = deep + (bright - deep) * (0.15 + 0.85 * vert ** 1.2)[..., None]
    rays = 0.5 + 0.5 * np.sin(ang * 23 + np.sin(ang * 7) * 1.5) * np.sin(ang * 11 + 0.7)
    col = col * (0.88 + 0.12 * rays * smooth(0.4, 0.8, t))[..., None]
    collar = np.exp(-((t - 0.52) / 0.10) ** 2) * 0.25
    col = screen(col, pal['collar'], collar)
    limbal = smooth(0.78, 1.0, t)
    col = col * (1 - 0.72 * limbal)[..., None] + pal['limbal'] * (0.72 * limbal)[..., None]
    # 上の影（瞼の落とす影）
    col = col * (1 - 0.45 * smooth(-0.15 * R, -0.95 * R, dy) * smooth(1.05, 0.6, t))[..., None]
    a_iris = smooth(1.04, 0.97, t) * island
    out = mix(out, col, a_iris)
    # 瞳孔
    a_p = smooth(k['pupil'] + 0.04, k['pupil'] - 0.03, t) * island
    out = mix(out, (0.035, 0.022, 0.02), a_p)
    # 光の点: 大きいのを左上に、小さいのを右下に（絵の左は本人の右。左右の目で同じ側）
    def spot(px, py, rx, ry, a):
        d2 = ((xx + 0.5 - (cx + px * R)) / (rx * R)) ** 2 + ((yy + 0.5 - (cy + py * R)) / (ry * R)) ** 2
        return smooth(1.0, 0.55, np.sqrt(d2)) * a * island
    out = mix(out, (1.0, 1.0, 0.98), spot(-0.36, -0.38, 0.24, 0.20, 0.95))
    out = mix(out, (1.0, 0.98, 0.95), spot(0.40, 0.40, 0.10, 0.09, 0.85))
    # 下の縁の照り返し（澄んだ感じ）
    out = screen(out, pal['glow'], np.exp(-(((t - 0.80) / 0.10) ** 2)) * vert ** 3 * 0.35 * a_iris)
    return out


# ---- 髪の房（まつ毛） -----------------------------------------------------------

def paint_hair():
    im = np.asarray(Image.open(os.path.join(SRC, 'Hair.png')).convert('RGBA'), np.float32) / 255
    rgb, a = im[..., :3].copy(), im[..., 3].copy()
    n = a.shape[0]
    # まつ毛の帯（512 の絵の左下）。上のまつ毛（濃い二つ）は太く、下のまつ毛は少しだけ
    upper = [(389, 441, 38, 67), (446, 500, 91, 120)]
    lower = [(386, 439, 102, 126), (448, 500, 37, 61)]
    lash = np.zeros_like(a, bool)
    for (r0, r1, c0, c1), grow in [(b, 1) for b in upper] + [(b, 0) for b in lower]:
        sub = a[r0:r1, c0:c1]
        if grow > 0:
            g = ndimage.grey_dilation(sub, size=(grow + 1, grow + 1))
            g = np.maximum(sub, g * 0.75)
            a[r0:r1, c0:c1] = np.clip(g * 1.1, 0, 1)
        rgb[r0:r1, c0:c1] = rgb[r0:r1, c0:c1] * 0.35 + np.array([0.02, 0.015, 0.015]) * 0.65
        lash[r0:r1, c0:c1] = True
    out = np.concatenate([rgb, a[..., None]], -1)
    save_rgba(out, os.path.join(OUT, 'Hair_anime.png'), n)
    mask = np.zeros((n, n, 3), np.float32)
    mask[..., 1] = 1.0
    mask[lash] = [0, 0, 0]
    save_rgb(mask, os.path.join(OUT, 'Hair_anime_mask.png'), n)


# ---- 胸元と体（肌の色だけ） -----------------------------------------------------

def skin_weight(rgb):
    """肌らしさ（色相 5〜45 度、彩度 0.15〜0.75、明るさ 0.22 以上）"""
    mx = rgb.max(-1)
    mn = rgb.min(-1)
    sat = (mx - mn) / np.maximum(mx, 1e-4)
    r, g, b = rgb[..., 0], rgb[..., 1], rgb[..., 2]
    hue = np.degrees(np.arctan2(np.sqrt(3) * (g - b), 2 * r - g - b)) % 360
    w = smooth(0.10, 0.18, sat) * smooth(0.85, 0.72, sat) * smooth(0.18, 0.26, mx)
    w *= smooth(0, 6, hue) * smooth(50, 40, hue)
    w *= (r > g) & (g >= b * 0.85)
    return ndimage.gaussian_filter(w.astype(np.float32), 1.0)


def paint_skin_only(name):
    """体・胸元・膝から下の絵の肌の色だけを、頭と同じ変換にする（明るさは上げない。首から下は頭の絵も上げない）"""
    path = os.path.join(SRC, name + '.png')
    if not os.path.exists(path):
        return False
    rgb = load_rgb(path)
    w = skin_weight(rgb)
    out = mix(rgb, tone(rgb), w)
    save_rgb(out, os.path.join(OUT, name + '_anime.png'), rgb.shape[0])
    mask = np.zeros_like(rgb)
    # R: 体の肌は 0.5（顔の肌は 1。シェーダーが体の肌の陰を深く当てる）
    mask[..., 0] = 0.5 * w
    save_rgb(mask, os.path.join(OUT, name + '_anime_mask.png'), rgb.shape[0])
    return True


# 人ごとの値（LOOK に重ねる）。茶色の髪の片割れは眉も茶色
OVERRIDES = {
    'Face14_Hair14_MadeDress': dict(brow_colour=(0.24, 0.16, 0.12), brow_alpha=0.82),
}


def run(person, debug=False):
    global SRC, OUT, WORK, LOOK
    SRC = OUT = os.path.join(UNITY, 'Assets', 'Models', 'rocketbox', person, 'Painted')
    WORK = os.path.join(UNITY, 'Temp', 'AnimeFace', person)
    if not os.path.exists(os.path.join(WORK, 'anchors.txt')):
        sys.exit('地図が無い（Unity で HalfAware/Rocketbox/Anime face: export maps）: ' + WORK)
    keep = LOOK
    LOOK = dict(BASE_LOOK, **OVERRIDES.get(person, {}))
    try:
        paint_head(debug)
        paint_hair()
        done = [n for n in ('Body', 'Chest', 'Legs') if paint_skin_only(n)]
    finally:
        LOOK = keep
    print(person, '書いた:', OUT, '（肌の色:', ', '.join(done) + '）')


if __name__ == '__main__':
    args = [a for a in sys.argv[1:] if not a.startswith('--')]
    for p in (args or PEOPLE):
        run(p, '--debug' in sys.argv)
