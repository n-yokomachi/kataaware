# -*- coding: utf-8 -*-
"""場面 8（共用ガレージ）のコンクリートの足音を、場面 2 の足音から作り直す。

素材は Kenney RPG Audio の足音（CC0 1.0）で、`Assets/Audio/Step1`〜`Step5.wav` と
同じ出どころ。あちらは濡れた石畳と土の上で、測ると 2.5kHz 以上が 700Hz 以下より
11〜15dB 弱い。柔らかい地面を踏んだ音で、裸のコンクリートの上で鳴らすと
床が土に聞こえる。

コンクリートを踏む音は、低い唸りではなく高い打ちつけになる。ここでやるのは 3 つ。

  - 190Hz から下を落とす。土の含む深い唸りを抜く
  - 1.2kHz から上を持ち上げる。靴底が硬い面を打つ帯域がここ
  - 尾は少しだけ詰める。土は踏んだ足の下で崩れて長く尾を引くが、コンクリートは引かない。
    ガレージの反響は素材に焼かず、AudioReverbFilter に任せる

**二度「軽い」と差し戻された。** 一度目は 360Hz から下を丸ごと落として打音を 2.6 倍にしていた。
二度目は落とす端を 190Hz へ下げ、素のまま残す量を倍にした。それでも変わらなかった。

理由ははっきりしている。**素材に低い成分が無い。** 元は Kenney の柔らかい足音で、
低い帯域そのものが録れていない。残す・削るをどう振っても、無いものは出てこない。
重さを出すには足すしかない。

そこで低い打ちを合成して重ねる。92Hz の正弦を 55ms で減衰させ、頭の 12ms だけ
35% 上へ振る。踏み込んだ瞬間に床へ掛かる重さがこれにあたる。靴底の打ちつけ（高い方）は
素材が持っているので、二つを重ねると「硬い面を重い人が踏んだ」音になる

こちらでは音を聴けないので、採否はオーナーが聴いて決める。判断できるのは
帯域の偏りだけで、LICENSES.md の「息の選び分け」と同じやり方になる。

    python tools/make-steps.py
"""

import array
import math
import os
import wave

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(HERE)
AUDIO = os.path.join(ROOT, 'unity', 'Assets', 'Audio')

# 素材。**5 つ全部は使わない。**
#
# Step1〜5 の偏りを測ると -10.9 / -11.7 / -12.2 / -14.0 / -15.5dB で、
# 同じ「柔らかい足音」の中でも高域の量が 5dB 開いている。高域の少ない Step4 は、
# どれだけ持ち上げても素材に無いものは出てこず、加工すると雑音だけが増える。
#
# 明るい 3 つ（Step2 / Step5 / Step1）を打ちつけとして使い、Step3 を 1 つだけ
# 混ぜる。4 つ目が鈍いのは不具合ではない。人は毎歩きれいに踏まないので、
# 踵を擦った一歩が混じっていた方が、同じ音の繰り返しに聞こえにくい
SOURCES = ['Step2', 'Step5', 'Step1', 'Step3']

# 落とす下の端。Hz
RUMBLE = 190.0
# 持ち上げる上の端。Hz。靴底がコンクリートを打つ帯域
SLAP = 1200.0
# 持ち上げる量。倍。**上げ過ぎると軽くなる。** 2.6 では打音だけが立って体重が乗らなかった
SLAP_GAIN = 1.5
# 素のまま残す量。倍。0 にすると打音だけの薄い音になる
BODY_GAIN = 0.80

# ---- 足す低い打ち ------------------------------------------------------
#
# **ここが重さの出どころ。** 素材には無いので合成する。
# 量を上げ過ぎると足音ではなく太鼓になるので、頂点に対する割合で置く。

# 基音。Hz。人が踏んだときに床が返す帯域
THUMP_HZ = 92.0
# 減衰の時定数。秒。長くすると床が鳴り続けて体育館になる。0.055 では尾が残り過ぎた
THUMP_DECAY = 0.048
# 打音の頂点に対する量。倍。**重さはここで決まる。**
# 0.30 で低い側が素の +12dB、0.50 で +16dB、0.62 で +18dB。
# 上げ過ぎると足音ではなく太鼓になる。重いと言われたらここから下げる
THUMP_GAIN = 0.50
# 頭で基音を上へ振る割合。踏み込みの出足が出る
THUMP_SWEEP = 0.35
# 上へ振ったぶんが戻るまで。秒
THUMP_BEND = 0.012
# 尾の減衰の時定数。秒
TAIL = 0.062
# 減衰を掛け始めるまで。秒。頭の打ちつけはそのまま通す
ATTACK = 0.014
# 書き出す長さ。秒
LENGTH = 0.200
# 頂点。dBFS。素材の Step1〜5 と揃える
PEAK_DB = -6.0


def read(name):
    """モノラル 16bit で読む。ステレオなら左だけ取る"""
    with wave.open(os.path.join(AUDIO, name + '.wav'), 'rb') as w:
        ch, width, rate, count = w.getnchannels(), w.getsampwidth(), w.getframerate(), w.getnframes()
        raw = w.readframes(count)
    if width != 2:
        raise ValueError(name + ' は 16bit ではない')
    a = array.array('h')
    a.frombytes(raw)
    if ch == 2:
        a = array.array('h', [a[i] for i in range(0, len(a), 2)])
    return [s / 32768.0 for s in a], rate


def one_pole(sig, rate, hz):
    """一次の低域通過。戻すのは通した側"""
    k = 2.0 * math.pi * hz / rate
    k = k / (k + 1.0)
    out = []
    y = 0.0
    for v in sig:
        y += k * (v - y)
        out.append(y)
    return out


def shape(sig, rate):
    """唸りを抜いて打音を持ち上げ、尾を詰める"""
    deep = one_pole(sig, rate, RUMBLE)
    body = [sig[i] - deep[i] for i in range(len(sig))]
    soft = one_pole(body, rate, SLAP)
    slap = [body[i] - soft[i] for i in range(len(body))]

    n = min(len(sig), int(LENGTH * rate))
    out = []
    hold = int(ATTACK * rate)
    fade = int(0.006 * rate)
    for i in range(n):
        v = BODY_GAIN * body[i] + SLAP_GAIN * slap[i]
        if i > hold:
            v *= math.exp(-(i - hold) / (TAIL * rate))
        # 末尾は必ず 0 へ落とす。途中で切ると再生のたびに小さな click が付く
        if i > n - fade:
            v *= (n - i) / float(fade)
        out.append(v)
    return out


def thump(n, rate, peak):
    """体重の乗る低い打ち。素材に無いので合成して重ねる"""
    out = []
    phase = 0.0
    for i in range(n):
        # 頭だけ基音を上へ振る。真っ直ぐな正弦だと「ぼ」で、振ると「どす」になる
        hz = THUMP_HZ * (1.0 + THUMP_SWEEP * math.exp(-i / (THUMP_BEND * rate)))
        phase += 2.0 * math.pi * hz / rate
        out.append(math.sin(phase) * math.exp(-i / (THUMP_DECAY * rate)) * peak * THUMP_GAIN)
    return out


def weigh(sig, rate):
    """低い打ちを重ねる。頭を揃えるので、打ちつけと同時に床へ重さが掛かる"""
    peak = max(abs(v) for v in sig)
    if peak <= 0.0:
        return sig
    low = thump(len(sig), rate, peak)
    return [sig[i] + low[i] for i in range(len(sig))]


def deep(sig, rate):
    """低い側の実効値。300Hz より下。dB。重さが増えたかを測る"""
    soft = one_pole(sig, rate, 300.0)
    power = sum(v * v for v in soft) / max(1, len(soft))
    return 10.0 * math.log10(power + 1e-12)


def level(sig, db):
    """頂点を揃える"""
    peak = max(abs(v) for v in sig)
    if peak <= 0.0:
        return sig
    gain = (10.0 ** (db / 20.0)) / peak
    return [v * gain for v in sig]


def bias(sig, rate):
    """帯域の偏り。2.5kHz より上が 700Hz より下より何 dB 強いか。dB"""
    deep = one_pole(sig, rate, 700.0)
    soft = one_pole(sig, rate, 2500.0)
    low = sum(v * v for v in deep)
    high = sum((sig[i] - soft[i]) ** 2 for i in range(len(sig)))
    return 10.0 * math.log10((high + 1e-12) / (low + 1e-12))


def write(name, sig, rate):
    a = array.array('h', [max(-32768, min(32767, int(round(v * 32767.0)))) for v in sig])
    with wave.open(os.path.join(AUDIO, name + '.wav'), 'wb') as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(rate)
        w.writeframes(a.tobytes())


def main():
    for i, src in enumerate(SOURCES):
        sig, rate = read(src)
        thin = level(shape(sig, rate), PEAK_DB)
        made = level(weigh(thin, rate), PEAK_DB)
        name = 'Concrete%d' % (i + 1)
        write(name, made, rate)
        print('%s -> %s  %.3f 秒  低い側 %+.1fdB -> %+.1fdB  偏り %+.1fdB'
              % (src, name, len(made) / float(rate),
                 deep(thin, rate), deep(made, rate), bias(made, rate)))


if __name__ == '__main__':
    main()
