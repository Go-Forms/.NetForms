"""«Злой csproj»: звук. Голоса (RHVoice, см. story.py), шумы и музыка синтезируются здесь же — без
чужих сэмплов. Вход: build/timeline.json; выход: build/soundtrack.wav (48 кГц, стерео).
"""

import json
import os
import wave
from functools import lru_cache

import numpy as np
from scipy import signal

import story

SR = 48000
HERE = os.path.dirname(os.path.abspath(__file__))
BUILD = os.path.join(HERE, "build")


# ─── основа ───────────────────────────────────────────────────────────────────────────────
def tt(dur):
    return np.arange(int(dur * SR)) / SR


def rng(seed):
    return np.random.default_rng(seed)


def lp(x, f, order=2):
    return signal.sosfilt(signal.butter(order, min(f / (SR / 2), 0.99), "low", output="sos"), x)


def hp(x, f, order=2):
    return signal.sosfilt(signal.butter(order, f / (SR / 2), "high", output="sos"), x)


def bp(x, f1, f2, order=2):
    return signal.sosfilt(signal.butter(order, [f1 / (SR / 2), min(f2 / (SR / 2), 0.99)], "band", output="sos"), x)


def expdec(dur, k, attack=0.002):
    t = tt(dur)
    e = np.exp(-t * k)
    na = max(1, int(attack * SR))
    e[:na] *= np.linspace(0, 1, na)
    return e


def bell_env(n, a=0.2, r=0.3):
    e = np.ones(n)
    na, nr = int(n * a), int(n * r)
    if na:
        e[:na] = np.linspace(0, 1, na)
    if nr:
        e[-nr:] = np.linspace(1, 0, nr)
    return e


def note(name):
    names = {"C": 0, "C#": 1, "D": 2, "D#": 3, "E": 4, "F": 5, "F#": 6, "G": 7, "G#": 8, "A": 9, "A#": 10, "B": 11}
    n, o = name[:-1], int(name[-1])
    return 440.0 * 2 ** ((names[n] + 12 * (o + 1) - 69) / 12)


@lru_cache(maxsize=None)
def ir(dur, k, seed=3):
    """Синтетическая импульсная характеристика комнаты."""
    r = rng(seed)
    n = int(dur * SR)
    e = np.exp(-np.arange(n) / SR * k)
    return np.stack([lp(r.standard_normal(n), 6000) * e, lp(r.standard_normal(n), 6000) * e]) * 0.12


def reverb(x, dur=0.8, k=7.0, wet=0.2):
    """Моно → стерео с хвостом."""
    h = ir(dur, k)
    L = signal.fftconvolve(x, h[0])[: len(x) + len(h[0]) - 1]
    R = signal.fftconvolve(x, h[1])[: len(x) + len(h[1]) - 1]
    dry = np.zeros_like(L)
    dry[: len(x)] = x
    return np.stack([dry + wet * L, dry + wet * R])


class Bus:
    def __init__(self, dur):
        self.x = np.zeros((2, int((dur + 3) * SR)))

    def add(self, t, sig, gain=1.0, pan=0.0):
        i = int(round(t * SR))
        if i < 0:
            sig = sig[..., -i:]
            i = 0
        if sig.ndim == 1:
            l, r = np.cos((pan + 1) * np.pi / 4), np.sin((pan + 1) * np.pi / 4)
            sig = np.stack([sig * l * 1.414, sig * r * 1.414])
        n = min(sig.shape[1], self.x.shape[1] - i)
        if n > 0:
            self.x[:, i:i + n] += sig[:, :n] * gain


# ─── шумы ─────────────────────────────────────────────────────────────────────────────────
def sfx_key(seed, loud=1.0):
    r = rng(seed)
    n = int(0.09 * SR)
    click = bp(r.standard_normal(n), 2200, 7000) * expdec(0.09, 380)
    f = r.uniform(150, 230)
    thock = np.sin(2 * np.pi * f * tt(0.09)) * expdec(0.09, 70) * 0.6 + lp(r.standard_normal(n), 900) * expdec(0.09, 90) * 0.5
    up = np.zeros(n)
    k = int(0.055 * SR)
    up[k:] = bp(r.standard_normal(n - k), 2500, 7000) * expdec((n - k) / SR, 500) * 0.35
    return (click * 0.6 + thock + up) * 0.5 * loud


def sfx_space(seed):
    return sfx_key(seed, 0.9) + lp(sfx_key(seed + 1, 0.6), 600)


def sfx_enter(seed):
    s = sfx_key(seed, 1.3)
    return s + np.pad(lp(sfx_key(seed + 7, 0.8), 1500), (0, 0))


def sfx_slam(seed):
    """Злой удар по клавише: щелчок, удар по столу, дребезг предметов."""
    r = rng(seed)
    d = 0.35
    n = int(d * SR)
    body = np.sin(2 * np.pi * (70 + 30 * np.exp(-tt(d) * 30)) * tt(d)) * expdec(d, 16)
    crack = bp(r.standard_normal(n), 1500, 8000) * expdec(d, 160)
    rattle = bp(r.standard_normal(n), 800, 3000) * expdec(d, 12) * (0.5 + 0.5 * np.sign(np.sin(2 * np.pi * 38 * tt(d)))) * 0.25
    ping = sum(np.sin(2 * np.pi * r.uniform(1800, 4200) * tt(d)) * expdec(d, r.uniform(25, 45)) * 0.08 for _ in range(3))
    key = np.pad(sfx_key(seed, 1.6), (0, n - int(0.09 * SR)))
    return (body * 0.9 + crack * 0.8 + rattle + ping + key) * 0.8


def sfx_safe(seed=11):
    """Ctrl+S — дверь сейфа: металлический удар, засов, гул."""
    d = 3.0
    t = tt(d)
    r = rng(seed)
    partials = [(62, 1.0, 1.4), (97, 0.8, 2.2), (143, 0.6, 3.0), (211, 0.5, 3.6), (278, 0.35, 4.4), (356, 0.3, 5.0), (512, 0.2, 6.5), (733, 0.12, 8.0)]
    metal = sum(a * np.sin(2 * np.pi * f * t * (1 + 0.002 * np.sin(2 * np.pi * 3 * t))) * np.exp(-t * k) for f, a, k in partials)
    boom = np.sin(2 * np.pi * (48 - 18 * (1 - np.exp(-t * 4))) * t) * np.exp(-t * 3.5) * 1.3
    hit = lp(r.standard_normal(len(t)), 2500) * np.exp(-t * 30) * 0.9
    latch = np.zeros(len(t))
    for when, g in [(0.32, 0.6), (0.4, 0.45)]:
        i = int(when * SR)
        m = int(0.03 * SR)
        latch[i:i + m] += bp(r.standard_normal(m), 1500, 6000) * expdec(0.03, 200) * g
    x = (metal * 0.35 + boom + hit + latch)
    st = reverb(x, 2.0, 2.2, 0.35)
    return st / np.abs(st).max() * 0.95


def sfx_cart(seed, dur):
    r = rng(seed)
    n = int(dur * SR)
    brown = np.cumsum(r.standard_normal(n))
    brown = hp(brown, 25)
    brown = lp(brown / (np.abs(brown).max() + 1e-9), 260)
    am = 0.6 + 0.4 * np.sin(2 * np.pi * 8.5 * tt(dur))
    rattle = np.zeros(n)
    for _ in range(int(dur * 14)):
        i = r.integers(0, n - 2000)
        m = 900
        rattle[i:i + m] += bp(r.standard_normal(m), 2000, 6000) * expdec(m / SR, 300) * r.uniform(0.05, 0.2)
    squeak = np.zeros(n)
    for _ in range(int(dur)):
        i = r.integers(0, max(1, n - 12000))
        m = 9000
        f = r.uniform(1900, 2400) + 150 * np.sin(2 * np.pi * 7 * tt(m / SR))
        squeak[i:i + m] += np.sin(2 * np.pi * np.cumsum(f) / SR) * bell_env(m, 0.3, 0.5) * 0.05
    return (brown * am * 2.2 + rattle + squeak) * bell_env(n, 0.15, 0.2)


def sfx_thunk(seed):
    r = rng(seed)
    d = 0.3
    return (np.sin(2 * np.pi * 95 * tt(d)) * expdec(d, 25) + lp(r.standard_normal(int(d * SR)), 600) * expdec(d, 40) * 0.7) * 0.8


def sfx_typewriter(seed, dur):
    r = rng(seed)
    out = np.zeros(int((dur + 0.6) * SR))
    t = 0.0
    k = 0
    while t < dur:
        i = int(t * SR)
        m = int(0.06 * SR)
        s = bp(r.standard_normal(m), 1500, 6000) * expdec(0.06, 160) + np.sin(2 * np.pi * 2600 * tt(0.06)) * expdec(0.06, 90) * 0.2
        out[i:i + m] += s * 0.55
        t += r.uniform(0.055, 0.1)
        k += 1
    i = int(dur * SR)
    m = int(0.6 * SR)
    out[i:i + m] += (np.sin(2 * np.pi * 2093 * tt(0.6)) + 0.4 * np.sin(2 * np.pi * 5230 * tt(0.6))) * expdec(0.6, 6) * 0.25
    return out


def sfx_boom(seed):
    d = 2.2
    t = tt(d)
    r = rng(seed)
    x = np.sin(2 * np.pi * (70 - 36 * (1 - np.exp(-t * 3))) * t) * np.exp(-t * 2.2) + lp(r.standard_normal(len(t)), 900) * np.exp(-t * 14) * 0.7
    st = reverb(x, 2.0, 2.5, 0.3)
    return st / np.abs(st).max() * 0.9


def sfx_whoosh(seed, d=0.6):
    r = rng(seed)
    n = int(d * SR)
    x = r.standard_normal(n)
    out = np.zeros(n)
    seg = 1200
    for i in range(0, n, seg):
        k = i / n
        f = 300 + 3200 * np.sin(k * np.pi) ** 2
        out[i:i + seg] = bp(x[i:i + seg], f * 0.7, f * 1.3)
    return out * bell_env(n, 0.5, 0.45) * 0.5


def sfx_crack(seed):
    r = rng(seed)
    out = np.zeros(int(0.4 * SR))
    for when in (0.0, 0.07, 0.16, 0.2):
        i = int(when * SR)
        m = int(0.012 * SR)
        out[i:i + m] += hp(r.standard_normal(m), 2500) * expdec(0.012, 600) * 0.8
    return out


def sfx_error(seed):
    out = np.zeros(int(0.3 * SR))
    for j, when in enumerate((0.0, 0.14)):
        i = int(when * SR)
        m = int(0.09 * SR)
        sq = signal.square(2 * np.pi * (180 if j == 0 else 150) * tt(0.09)) * bell_env(m, 0.05, 0.3)
        out[i:i + m] += lp(sq, 1600) * 0.25
    return out


def bell(f, d, k=3.5, a=1.0):
    t = tt(d)
    return a * (np.sin(2 * np.pi * f * t) + 0.35 * np.sin(2 * np.pi * f * 2.76 * t) * np.exp(-t * 4) + 0.2 * np.sin(2 * np.pi * f * 5.4 * t) * np.exp(-t * 8)) * expdec(d, k, 0.003)


def sfx_success(seed):
    out = np.zeros(int(1.4 * SR))
    for when, f in ((0.0, note("C6")), (0.12, note("G6"))):
        i = int(when * SR)
        b = bell(f, 1.2, 4.0, 0.25)
        out[i:i + len(b)] += b
    return out


def sfx_notify(seed):
    out = np.zeros(int(1.4 * SR))
    for when, f in ((0.0, note("E6")), (0.14, note("B6"))):
        i = int(when * SR)
        b = bell(f, 1.1, 5.0, 0.22)
        out[i:i + len(b)] += b
    return out


def sfx_creak(seed):
    r = rng(seed)
    d = 0.55
    n = int(d * SR)
    x = r.standard_normal(n) * (0.4 + 0.6 * (np.sin(2 * np.pi * 34 * tt(d)) > 0.3))
    out = np.zeros(n)
    for f in (620, 910):
        out += bp(x, f * 0.95, f * 1.05)
    return out * bell_env(n, 0.3, 0.4) * 0.6


def sfx_sip(seed):
    r = rng(seed)
    d = 0.6
    n = int(d * SR)
    x = r.standard_normal(n)
    out = np.zeros(n)
    s = 1200
    for i in range(0, n, s):
        f = 900 + 1800 * (i / n)
        out[i:i + s] = bp(x[i:i + s], f * 0.8, f * 1.2)
    return out * bell_env(n, 0.2, 0.3) * (0.6 + 0.4 * np.sin(2 * np.pi * 18 * tt(d))) * 0.35


def sfx_scratch(seed):
    """Скретч пластинки: кофе замирает."""
    r = rng(seed)
    d = 0.55
    t = tt(d)
    f = 600 + 900 * np.sin(2 * np.pi * 5.5 * t) * np.exp(-t * 2) - 400 * t
    tone = signal.sawtooth(2 * np.pi * np.cumsum(f) / SR) * 0.3
    noise = bp(r.standard_normal(len(t)), 800, 5000) * 0.4
    return lp(tone + noise, 5000) * bell_env(len(t), 0.03, 0.4) * 0.7


def sfx_click(seed):
    r = rng(seed)
    out = np.zeros(int(0.12 * SR))
    for when, g in ((0.0, 1.0), (0.075, 0.6)):
        i = int(when * SR)
        m = int(0.01 * SR)
        out[i:i + m] += (hp(r.standard_normal(m), 3000) + 0.5 * np.sin(2 * np.pi * 2100 * tt(0.01))) * expdec(0.01, 700) * g
    return out * 0.45


def sfx_marker(seed, dur):
    r = rng(seed)
    n = int(dur * SR)
    t = tt(dur)
    f = 2600 + 400 * np.sin(2 * np.pi * 3 * t + r.uniform(0, 6))
    squeak = np.sin(2 * np.pi * np.cumsum(f) / SR) * (0.5 + 0.5 * np.sin(2 * np.pi * r.uniform(11, 19) * t)) ** 2
    scratch = bp(r.standard_normal(n), 1500, 5000) * 0.25
    gate = (np.sin(2 * np.pi * 2.3 * t + 1) > -0.6).astype(float)
    return (squeak * 0.12 + scratch * 0.3) * lp(gate, 30) * bell_env(n, 0.05, 0.1)


def sfx_wipe(seed):
    r = rng(seed)
    d = 0.9
    n = int(d * SR)
    return lp(r.standard_normal(n), 2500) * (0.5 + 0.5 * np.sin(2 * np.pi * 13 * tt(d))) * bell_env(n, 0.1, 0.3) * 0.35


def sfx_open(seed):
    out = np.zeros(int(2.5 * SR))
    w = sfx_whoosh(seed, 0.5) * 0.5
    out[: len(w)] += w
    for j, nm in enumerate(("C5", "E5", "G5", "C6")):
        b = bell(note(nm), 2.0, 2.2, 0.16)
        i = int((0.25 + j * 0.07) * SR)
        out[i:i + len(b)] += b[: len(out) - i]
    return out


def sfx_steps(seed, dur):
    r = rng(seed)
    out = np.zeros(int((dur + 0.3) * SR))
    t = 0.0
    while t < dur:
        i = int(t * SR)
        m = int(0.12 * SR)
        out[i:i + m] += (lp(r.standard_normal(m), 400) * expdec(0.12, 40) + np.sin(2 * np.pi * 80 * tt(0.12)) * expdec(0.12, 50) * 0.5) * 0.35
        t += 0.46 + r.uniform(-0.03, 0.03)
    return out


def sfx_mug(seed):
    d = 0.5
    t = tt(d)
    return (np.sin(2 * np.pi * 1780 * t) * np.exp(-t * 28) * 0.3 + np.sin(2 * np.pi * 2950 * t) * np.exp(-t * 40) * 0.15
            + np.sin(2 * np.pi * 130 * t) * np.exp(-t * 40) * 0.5)


def sfx_logo(seed):
    out = np.zeros(int(3.0 * SR))
    for j, nm in enumerate(("C6", "E6", "G6", "C7", "E7")):
        b = bell(note(nm), 2.2, 2.0, 0.12)
        i = int(j * 0.08 * SR)
        out[i:i + len(b)] += b[: len(out) - i]
    return out


def make_sfx(e, idx):
    n = e["name"]
    seed = 1000 + idx
    if n == "key":
        return sfx_key(seed)
    if n == "space":
        return sfx_space(seed)
    if n == "enter":
        return sfx_enter(seed)
    if n == "slam":
        return sfx_slam(seed)
    if n == "safe":
        return sfx_safe()
    if n == "cart":
        return sfx_cart(seed, e.get("dur", 2.5))
    if n == "thunk":
        return sfx_thunk(seed)
    if n == "typewriter":
        return sfx_typewriter(seed, e.get("dur", 1.5))
    if n == "boom":
        return sfx_boom(seed)
    if n == "whoosh":
        return sfx_whoosh(seed)
    if n == "crack":
        return sfx_crack(seed)
    if n == "error":
        return sfx_error(seed)
    if n == "success":
        return sfx_success(seed)
    if n == "notify":
        return sfx_notify(seed)
    if n == "creak":
        return sfx_creak(seed)
    if n == "sip":
        return sfx_sip(seed)
    if n == "scratch":
        return sfx_scratch(seed)
    if n == "click":
        return sfx_click(seed)
    if n == "marker":
        return sfx_marker(seed, e.get("dur", 1.0))
    if n == "wipe":
        return sfx_wipe(seed)
    if n == "open":
        return sfx_open(seed)
    if n == "steps":
        return sfx_steps(seed, e.get("dur", 2.0))
    if n == "mug":
        return sfx_mug(seed)
    if n == "logo":
        return sfx_logo(seed)
    raise ValueError(n)


SFX_LEVEL = {"key": 0.55, "space": 0.5, "enter": 0.6, "slam": 0.62, "safe": 1.0, "cart": 0.5, "thunk": 0.7,
             "typewriter": 0.5, "boom": 0.75, "whoosh": 0.5, "crack": 0.6, "error": 0.5, "success": 0.7, "notify": 0.8,
             "creak": 0.35, "sip": 0.6, "scratch": 0.8, "click": 0.6, "marker": 0.6, "wipe": 0.6, "open": 0.8,
             "steps": 0.6, "mug": 0.7, "logo": 0.8}


# ─── голоса ───────────────────────────────────────────────────────────────────────────────
def lpc_whisper(x):
    """Шёпот: огибающая спектра речи (LPC) + шумовое возбуждение вместо голосовых связок."""
    order, n, hop = 22, int(0.03 * SR), int(0.01 * SR)
    win = np.hanning(n)
    out = np.zeros(len(x) + n)
    r = rng(5)
    for i in range(0, len(x) - n, hop):
        fr = x[i:i + n] * win
        ac = np.correlate(fr, fr, "full")[n - 1:n + order]
        if ac[0] < 1e-8:
            continue
        a = np.zeros(order + 1)
        a[0] = 1.0
        err = ac[0]
        for k in range(1, order + 1):
            acc = ac[k] + np.dot(a[1:k], ac[k - 1:0:-1])
            kk = -acc / err
            a[1:k + 1] = a[1:k + 1] + kk * a[k - 1::-1][:k]
            err *= 1 - kk * kk
            if err <= 0:
                break
        g = np.sqrt(max(err, 0) / n)
        exc = r.standard_normal(n) * g
        out[i:i + n] += signal.lfilter([1.0], a, exc) * win
    return hp(out[: len(x)], 250) * 1.4


def voice(line):
    sr, x = story.read_wav(os.path.join(HERE, line["wav"]))
    x = story.trim(x, sr)
    x = signal.resample_poly(x, SR // sr, 1) if SR % sr == 0 else signal.resample(x, int(len(x) * SR / sr))
    x = x / (np.sqrt(np.mean(x ** 2)) + 1e-9) * 0.1
    st = line.get("style")
    if st == "robot":
        t = np.arange(len(x)) / SR
        x = x * (0.62 + 0.38 * np.sin(2 * np.pi * 55 * t))
        x = bp(x, 260, 3600)
        d = int(0.007 * SR)
        x = x + 0.45 * np.concatenate([np.zeros(d), x[:-d]])
        x = x / (np.sqrt(np.mean(x ** 2)) + 1e-9) * 0.058
    elif st == "whisper":
        # шёпот со следом голоса — чтобы слова оставались разборчивыми
        x = lpc_whisper(x) + 0.3 * lp(x, 1400)
        x = x / (np.sqrt(np.mean(x ** 2)) + 1e-9) * 0.07
    elif st == "teeth":
        x = hp(x, 220)
        x = np.tanh(x * 5) / 5 * 1.6
        x = x / (np.sqrt(np.mean(x ** 2)) + 1e-9) * 0.095
    return x


# ─── музыка ───────────────────────────────────────────────────────────────────────────────
@lru_cache(maxsize=None)
def pluck(f, d=1.0, bright=1.0):
    t = tt(d)
    x = sum((1 / h ** 1.3) * np.sin(2 * np.pi * f * h * t) * np.exp(-t * (2.2 + h * 1.6 / bright)) for h in range(1, 9))
    x *= expdec(d, 0.0, 0.004)
    return x * 0.3


@lru_cache(maxsize=None)
def marimba(f, d=0.8):
    t = tt(d)
    return (np.sin(2 * np.pi * f * t) + 0.25 * np.sin(2 * np.pi * f * 4 * t) * np.exp(-t * 12)) * expdec(d, 6, 0.002) * 0.3


@lru_cache(maxsize=None)
def bass(f, d=0.5):
    t = tt(d)
    x = np.sin(2 * np.pi * f * t) + 0.3 * np.sin(2 * np.pi * 2 * f * t) + 0.1 * np.sin(2 * np.pi * 3 * f * t)
    return x * bell_env(len(t), 0.02, 0.35) * 0.35


@lru_cache(maxsize=None)
def pad(freqs, d, attack=0.35):
    t = tt(d)
    x = np.zeros(len(t))
    for f in freqs:
        for det in (-0.004, 0.0, 0.005):
            ff = f * (1 + det)
            x += sum((1 / h) * np.sin(2 * np.pi * ff * h * t + h) for h in range(1, 7))
    x = lp(x, 1800)
    x *= bell_env(len(t), attack, 0.35)
    return x / (np.abs(x).max() + 1e-9) * 0.3


@lru_cache(maxsize=None)
def hat(seed=1, d=0.05):
    return hp(rng(seed).standard_normal(int(d * SR)), 7000) * expdec(d, 90) * 0.12


@lru_cache(maxsize=None)
def kick(d=0.35):
    t = tt(d)
    return np.sin(2 * np.pi * (45 + 80 * np.exp(-t * 30)) * t) * np.exp(-t * 9) * 0.9


CHORDS_A = [["C4", "E4", "G4", "C5"], ["A3", "C4", "E4", "A4"], ["F3", "A3", "C4", "F4"], ["G3", "B3", "D4", "G4"]]
ROOTS_A = ["C2", "A1", "F1", "G1"]
MELODY_A = [["E5", None, "G5", None, "E5", "D5", "C5", None], ["C5", None, "E5", None, "A4", None, None, None],
            ["A4", None, "C5", None, "F5", "E5", "D5", None], ["D5", None, None, "B4", "G4", None, None, None]]


def theme_office(bus, t0, t1, gain=1.0, melody=True, soft=False, fade_in=0.3, fade_out=0.8):
    """Утренняя офисная тема: щипковые арпеджио C–Am–F–G, бас, шейкер, маримба."""
    beat = 60 / 112
    tmp = Bus(t1 - t0 + 1)
    bar = 0
    t = 0.0
    while t < t1 - t0:
        ch = CHORDS_A[bar % 4]
        for k, idx in enumerate([0, 2, 1, 3, 2, 1, 3, 2]):
            tmp.add(t + k * beat / 2, pluck(note(ch[idx]), 1.0, 0.7 if soft else 1.0), 0.8, 0.25 if k % 2 else -0.25)
        if not soft:
            r = ROOTS_A[bar % 4]
            tmp.add(t, bass(note(r), beat * 1.6), 1.0)
            tmp.add(t + 2 * beat, bass(note(r), beat * 1.2), 0.8)
            tmp.add(t + 3.5 * beat, bass(note(r) * 1.5, beat * 0.5), 0.6)
            for k in range(8):
                if k % 2:
                    tmp.add(t + k * beat / 2, hat(k), 1.0, 0.3)
        if melody and (bar // 4) % 2 == 1:
            for k, nm in enumerate(MELODY_A[bar % 4]):
                if nm:
                    tmp.add(t + k * beat / 2, marimba(note(nm)), 0.9, 0.1)
        bar += 1
        t += 4 * beat
    x = tmp.x[:, : int((t1 - t0) * SR)]
    n = x.shape[1]
    e = np.ones(n)
    fi, fo = int(fade_in * SR), int(fade_out * SR)
    if fi:
        e[:fi] = np.linspace(0, 1, fi)
    if fo:
        e[-fo:] *= np.linspace(1, 0, fo)
    bus.add(t0, x * e, gain)


def theme_montage(bus, t0, t1, hits, gain=1.0):
    """Пять стадий: пульс в ля миноре, бочка, хэт, удар на каждой смене стадии."""
    beat = 60 / 126
    tmp = Bus(t1 - t0 + 1)
    prog = [("A1", ["A3", "C4", "E4"]), ("F1", ["F3", "A3", "C4"]), ("D2", ["D3", "F3", "A3"]), ("E1", ["E3", "G#3", "B3"])]
    t = 0.0
    bar = 0
    while t < t1 - t0:
        root, ch = prog[bar % 4]
        for k in range(8):
            tmp.add(t + k * beat / 2, bass(note(root) * (2 if k % 2 else 1), beat * 0.45), 0.9)
        for k in range(4):
            tmp.add(t + k * beat, kick(), 0.8)
        for k in range(16):
            tmp.add(t + k * beat / 4, hat(k), 0.6 if k % 2 else 0.9, 0.35)
        tmp.add(t, pad(tuple(note(n) for n in ch), 4 * beat, 0.1), 0.35)
        bar += 1
        t += 4 * beat
    x = tmp.x[:, : int((t1 - t0) * SR)]
    for h in hits:
        tmp2 = sfx_boom(77)
        i = int((h - t0) * SR)
        n = min(tmp2.shape[1], x.shape[1] - i)
        if n > 0:
            x[:, i:i + n] += tmp2[:, :n] * 0.5
    n = x.shape[1]
    e = np.ones(n)
    e[: int(0.2 * SR)] = np.linspace(0, 1, int(0.2 * SR))
    e[-int(0.6 * SR):] *= np.linspace(1, 0, int(0.6 * SR))
    bus.add(t0, x * e, gain)


def pad_at(bus, t0, t1, notes, gain, attack=0.3):
    d = t1 - t0
    if d <= 0.05:
        return
    p = pad(tuple(note(n) for n in notes), round(d, 3), attack)
    bus.add(t0, p, gain, -0.1)
    bus.add(t0 + 0.013, p, gain * 0.8, 0.15)


def celesta_arp(bus, t0, t1, notes, step, gain):
    t = t0
    i = 0
    while t < t1:
        bus.add(t, marimba(note(notes[i % len(notes)]), 0.9), gain, 0.3 if i % 2 else -0.3)
        t += step
        i += 1


# ─── атмосфера ────────────────────────────────────────────────────────────────────────────
def room_tone(d, seed=9, level=1.0):
    r = rng(seed)
    n = int(d * SR)
    pink = lp(r.standard_normal(n), 500) + 0.3 * lp(r.standard_normal(n), 2000)
    hum = 0.2 * np.sin(2 * np.pi * 50 * tt(d)) + 0.08 * np.sin(2 * np.pi * 100 * tt(d))
    return (pink * 0.02 + hum * 0.004) * level


def birds(d, seed=21):
    r = rng(seed)
    out = np.zeros(int(d * SR))
    t = r.uniform(0.3, 1.0)
    while t < d - 1:
        for k in range(r.integers(2, 5)):
            dd = r.uniform(0.06, 0.13)
            tt_ = tt(dd)
            f0 = r.uniform(3000, 5200)
            f = f0 + r.uniform(-1500, 1500) * (tt_ / dd)
            ch = np.sin(2 * np.pi * np.cumsum(f) / SR) * bell_env(len(tt_), 0.2, 0.5)
            i = int((t + k * r.uniform(0.1, 0.18)) * SR)
            out[i:i + len(ch)] += ch * 0.03
        t += r.uniform(1.2, 2.8)
    return out


def snore(d, seed=31):
    r = rng(seed)
    out = np.zeros(int(d * SR))
    t = 0.2
    while t < d - 3:
        m = int(1.3 * SR)
        rattle = 0.5 + 0.5 * np.sign(np.sin(2 * np.pi * 32 * tt(1.3)))
        inh = lp(r.standard_normal(m), 700) * lp(rattle, 200) * bell_env(m, 0.5, 0.4) * 0.12
        i = int(t * SR)
        out[i:i + m] += inh
        m2 = int(1.1 * SR)
        exh = lp(r.standard_normal(m2), 450) * bell_env(m2, 0.1, 0.7) * 0.05
        j = int((t + 1.5) * SR)
        out[j:j + m2] += exh
        t += 3.6
    return out


def clock_ticks(d, period, seed=41):
    out = np.zeros(int(d * SR))
    t = 0.0
    k = 0
    while t < d:
        f = 2100 if k % 2 == 0 else 1650
        m = int(0.04 * SR)
        s = (np.sin(2 * np.pi * f * tt(0.04)) * expdec(0.04, 120) + hp(rng(seed + k).standard_normal(m), 3000) * expdec(0.04, 300) * 0.5) * 0.12
        i = int(t * SR)
        out[i:i + m] += s[: len(out) - i]
        t += period(t) if callable(period) else period
        k += 1
    return out


# ─── сведение ─────────────────────────────────────────────────────────────────────────────
def main():
    with open(os.path.join(BUILD, "timeline.json")) as f:
        TL = json.load(f)
    M = TL["marks"]
    D = TL["duration"]
    sc = {s["id"]: s for s in TL["scenes"]}
    voices, sfx, music, amb = Bus(D), Bus(D), Bus(D), Bus(D)

    # голоса (+ лёгкая «комната» в офисе)
    for l in TL["lines"]:
        x = voice(l)
        pan = story.VOICES[l["who"]]["pan"]
        if l.get("style") in ("robot",):
            voices.add(l["t0"], reverb(x, 0.9, 5, 0.25), 1.0)
        elif l["t0"] < sc["s2"]["start"] or sc["s5"]["start"] <= l["t0"] < sc["s6"]["start"]:
            st = reverb(x, 0.5, 9, 0.12)
            l_, r_ = np.cos((pan + 1) * np.pi / 4) * 1.414, np.sin((pan + 1) * np.pi / 4) * 1.414
            voices.add(l["t0"], np.stack([st[0] * l_, st[1] * r_]), 1.0)
        else:
            voices.add(l["t0"], x, 1.0, pan * 0.5)

    # шумы
    for i, e in enumerate(TL["sfx"]):
        s = make_sfx(e, i)
        pan = 0.0
        if e["name"] in ("cart", "steps"):
            pan = -0.2
        sfx.add(e["t"], s, SFX_LEVEL[e["name"]] * e.get("gain", 1.0), pan)

    # атмосфера
    def span(a, b, sig, g=1.0, fade=0.3):
        n = sig.shape[-1]
        env = bell_env(n, min(0.45, fade / max(0.01, b - a)), min(0.45, fade / max(0.01, b - a)))
        amb.add(a, sig * env, g)

    s1, s2, s3, s4, s5, s6 = (sc[k] for k in ("s1", "s2", "s3", "s4", "s5", "s6"))
    span(s1["start"], s2["end"], room_tone(s2["end"] - s1["start"], 9), 1.0)
    span(M["s3.denial"], M["s3.accept"], clock_ticks(M["s3.accept"] - M["s3.denial"], 0.5), 1.0, 0.1)
    span(M["s3.accept"], s3["end"], clock_ticks(s3["end"] - M["s3.accept"], lambda t: 0.6 + t * 0.12), 1.0, 0.2)
    span(M["s3.accept"], M["s4.silence"], room_tone(M["s4.silence"] - M["s3.accept"], 12, 0.6), 1.0, 0.4)
    span(M["s4.window"], s4["end"], room_tone(s4["end"] - M["s4.window"], 13, 0.6), 1.0, 0.6)
    span(s5["start"], s5["end"], room_tone(s5["end"] - s5["start"], 14, 0.9) + birds(s5["end"] - s5["start"]), 1.0, 0.6)
    span(s5["start"], M["s5.check"], snore(M["s5.check"] - s5["start"]), 1.0, 0.4)
    span(M["s5.coffee"], s5["end"], snore(s5["end"] - M["s5.coffee"], 33), 1.0, 0.4)

    # музыка
    theme_office(music, s1["start"] + 0.2, M["s1.pause"], 0.4)
    theme_office(music, M["s1.l2"], s1["end"], 0.45, melody=False, fade_in=0.8, fade_out=0.9)
    celesta_arp(music, M["s2.ok"], M["s2.ok"] + 0.6, ["C5", "E5", "G5", "C6"], 0.12, 0.8)
    theme_office(music, M["s2.lean"], M["s2.freeze"], 0.45, melody=False, fade_in=0.1, fade_out=0.05)
    pad_at(music, M["s2.term3"], s2["end"], ["A2", "E3", "A3"], 0.35, 0.3)
    music.add(M["s2.l3"] - 0.3, marimba(note("E4"), 1.2), 0.6)
    music.add(M["s2.l3"] + 0.4, marimba(note("C4"), 1.5), 0.6)
    theme_montage(music, M["s3.denial"], M["s3.accept"], [M["s3.anger"], M["s3.bargain"], M["s3.depr"]], 0.5)
    pad_at(music, M["s3.accept"], s3["end"], ["A2", "C3", "E3", "B3"], 0.32, 0.4)
    pad_at(music, s4["start"], M["s4.chat"], ["A2", "E3", "G3", "C4"], 0.3, 0.3)
    pad_at(music, M["s4.chat"], M["s4.edit"], ["F2", "C3", "A3", "E4"], 0.3, 0.25)
    celesta_arp(music, M["s4.page"], M["s4.l1"], ["C5", "E5", "A5", "E5", "G5", "E5"], 0.36, 0.45)
    pad_at(music, M["s4.edit"], M["s4.save"], ["D2", "A2", "D3", "F3"], 0.34, 0.15)
    pad_at(music, M["s4.save"] + 1.2, M["s4.silence"], ["A1", "E2"], 0.25, 0.2)
    pad_at(music, M["s4.window"], s4["end"] + 0.4, ["C3", "G3", "C4", "E4", "D5"], 0.42, 0.15)
    celesta_arp(music, M["s4.window"] + 0.4, s4["end"] - 0.5, ["C5", "G5", "E5", "D6", "C6", "G5"], 0.3, 0.35)
    theme_office(music, s5["start"] + 0.3, M["s5.check"], 0.4, fade_in=1.0, fade_out=0.6)
    theme_office(music, M["s5.check"], M["s5.two"], 0.5, melody=False, soft=True, fade_in=0.4, fade_out=0.4)
    music.add(M["s5.f2"] + 0.1, sfx_success(5), 0.6)
    theme_office(music, M["s5.m2"], s5["end"], 0.45, fade_in=0.4, fade_out=1.0)
    for a, b, ch in ((s6["start"], M["s6.titr1"], ["A2", "E3", "C4"]), (M["s6.titr1"], M["s6.calm"], ["F2", "C3", "A3"]),
                     (M["s6.calm"], M["s6.titr2"], ["C3", "G3", "E4"]), (M["s6.titr2"], M["s6.logo"], ["G2", "D3", "B3"]),
                     (M["s6.logo"], s6["end"], ["C3", "G3", "C4", "E4", "G4"])):
        pad_at(music, a, b + 0.3, ch, 0.5, 0.25)
    celesta_arp(music, M["s6.final"], M["s6.fade"], ["C5", "E5", "G5", "C6", "G5", "E5"], 0.3, 0.5)

    # приглушить музыку и атмосферу под речью
    v = np.abs(voices.x).sum(0)
    env = lp(v, 4)
    env = env / (env.max() + 1e-9)
    duck = 1 - 0.55 * np.clip(env * 6, 0, 1)
    # полная тишина перед окном «Склада»
    quiet = np.ones(voices.x.shape[1])
    a, b = int(M["s4.silence"] * SR), int(M["s4.window"] * SR)
    quiet[a:b] = 0.0
    ramp = int(0.08 * SR)
    quiet[a - ramp:a] = np.linspace(1, 0, ramp)

    master = voices.x * 1.0 + sfx.x * 0.9 + (music.x * 0.6 + amb.x) * duck * quiet
    master = master[:, : int((D + 0.5) * SR)]
    peak = np.abs(master).max()
    master = np.tanh(master / peak * 1.25) / np.tanh(1.25) * 0.93
    out = (np.clip(master.T, -1, 1) * 32767).astype(np.int16)
    path = os.path.join(BUILD, "soundtrack.wav")
    with wave.open(path, "w") as w:
        w.setnchannels(2)
        w.setsampwidth(2)
        w.setframerate(SR)
        w.writeframes(out.tobytes())
    rms = np.sqrt(np.mean(master ** 2))
    print(f"soundtrack.wav: {out.shape[0] / SR:.1f} с, RMS {20 * np.log10(rms):.1f} dBFS")


if __name__ == "__main__":
    main()
