"""
Zenit promo - banda sonora original (sintetizada desde cero, libre de regalias).

Synthwave / electronica moderna a 120 BPM, 15.0 s, 44.1 kHz estereo.
Estructura (1 compas = 2 s, 1 pulso = 0.5 s):
  0-2 s   Intro: pad Am9 con filtro abriendose + swell de ruido, "bloops" de la isla
  2-12 s  Drop: kick 4/4, clap en 2 y 4, hats, sub bass con sidechain, arpegio
          F - C - G - Am - F/G ; whooshes en los cambios de pagina (4.5, 6.0, 7.5)
  10-12 s Riser + redoble de caja; silencio de tension en 11.875
  12 s    Impacto final sobre Cadd9, campanitas, cola de reverb, fade out 0.8 s

Uso:  python music.py salida.wav
"""
import sys
import numpy as np
from scipy.signal import butter, sosfilt, fftconvolve
from scipy.ndimage import minimum_filter1d, uniform_filter1d

SR = 44100
DUR = 15.0
N = int(SR * DUR)
BPM = 120
BEAT = 60 / BPM
RNG = np.random.default_rng(20261026)


def mtof(m):
    return 440.0 * 2 ** ((m - 69) / 12)


def buf():
    return np.zeros((N, 2))


def place(bus, sig, t0, gain=1.0, pan=0.0):
    """Add mono or stereo signal into bus at time t0 (s)."""
    i0 = int(round(t0 * SR))
    if sig.ndim == 1:
        l = np.cos((pan + 1) * np.pi / 4)
        r = np.sin((pan + 1) * np.pi / 4)
        sig = np.stack([sig * l * 1.414, sig * r * 1.414], axis=1)
    if i0 < 0:
        sig = sig[-i0:]
        i0 = 0
    n = min(len(sig), N - i0)
    if n > 0:
        bus[i0:i0 + n] += sig[:n] * gain


def filt(x, fc, btype="low", order=2):
    sos = butter(order, fc, btype, fs=SR, output="sos")
    return sosfilt(sos, x, axis=0)


def tv_filter(x, fc_fn, btype="low", order=2, block=256):
    """Time-varying Butterworth filter (block-wise coefficient update)."""
    y = np.zeros_like(x)
    zi = None
    for s in range(0, len(x), block):
        fc = fc_fn((s + block / 2) / SR)
        if btype == "band":
            lo, hi = fc
            fc = [max(20, lo), min(SR / 2 * 0.95, hi)]
        else:
            fc = float(np.clip(fc, 20, SR / 2 * 0.95))
        sos = butter(order, fc, btype, fs=SR, output="sos")
        if zi is None:
            zi = np.zeros((sos.shape[0], 2) + x.shape[1:])
        y[s:s + block], zi = sosfilt(sos, x[s:s + block], axis=0, zi=zi)
    return y


def polyblep(ph, dt):
    out = np.zeros_like(ph)
    m = ph < dt
    t = ph[m] / dt
    out[m] = t + t - t * t - 1
    m = ph > 1 - dt
    t = (ph[m] - 1) / dt
    out[m] = t * t + t + t + 1
    return out


def saw(freq, n, phase0=0.0):
    dt = freq / SR
    ph = (phase0 + np.cumsum(np.full(n, dt))) % 1.0
    return 2 * ph - 1 - polyblep(ph, dt)


def env_adsr(n, a, d, s, r, sus_len):
    t = np.arange(n) / SR
    e = np.where(t < a, t / max(a, 1e-4), 1.0)
    e = np.where((t >= a) & (t < a + d), 1 - (1 - s) * (t - a) / max(d, 1e-4), e)
    e = np.where((t >= a + d), s, e)
    rel = t >= sus_len
    e = np.where(rel, s * np.exp(-(t - sus_len) / max(r, 1e-4)), e)
    return e


# ----------------------------------------------------------------- timeline
CHORDS = [  # (start, end, pad notes, bass midi)
    (0.0, 2.0, [57, 60, 64, 71], 33),   # Am9
    (2.0, 4.0, [53, 57, 60, 64], 29),   # Fmaj7
    (4.0, 6.0, [55, 60, 64, 67], 36),   # C
    (6.0, 8.0, [55, 59, 62, 67], 31),   # G
    (8.0, 10.0, [57, 60, 64, 71], 33),  # Am9
    (10.0, 11.0, [53, 57, 60, 64], 29), # Fmaj7
    (11.0, 12.0, [55, 59, 62, 67], 31), # G
    (12.0, 15.0, [48, 55, 60, 64, 67, 74], 36),  # Cadd9 (resolucion)
]


def kick_times():
    return [2.0 + i * BEAT for i in range(20)]  # 2.0 .. 11.5


def sidechain_env():
    t = np.arange(N) / SR
    g = np.ones(N)
    for kt in kick_times():
        m = t >= kt
        d = t[m] - kt
        g[m] *= 1 - 0.65 * np.exp(-d / 0.11)
    return g


# ----------------------------------------------------------------- instruments
def pad_bus():
    bus = buf()
    for (t0, t1, notes, _) in CHORDS:
        intro = t0 == 0.0
        final = t0 == 12.0
        length = (t1 - t0) + (0.25 if not final else 0.0)
        n = int((length + (2.5 if final else 0.6)) * SR)
        a = 1.4 if intro else 0.04
        e = env_adsr(n, a, 0.3, 0.85, 0.35 if not final else 1.2, length)
        for note in notes:
            f = mtof(note)
            for v, det in enumerate([-0.11, -0.05, 0.0, 0.05, 0.11]):
                ph0 = RNG.random()
                s = saw(f * 2 ** (det / 12), n, ph0) * e
                pan = [-0.8, -0.4, 0.0, 0.4, 0.8][v]
                place(bus, s, t0 - (0.0 if intro else 0.02), gain=0.05 * (1.5 if intro else (1.35 if final else 1.0)), pan=pan)

    def fc(t):
        if t < 2.0:
            return 650 * (2600 / 650) ** ((t / 2.0) ** 1.4)
        if t < 12.0:
            return 2600 + 900 * np.sin(2 * np.pi * (t - 2) / 8)
        return 4200 * np.exp(-(t - 12) / 4)

    bus = tv_filter(bus, fc, "low", 2)
    bus = filt(bus, 120, "high", 2)
    return bus


def bass_bus():
    bus = buf()
    t_step = BEAT / 2  # corcheas
    for (t0, t1, _, root) in CHORDS:
        if t0 < 2.0 or t0 >= 12.0:
            continue
        k = 0
        tt = t0
        while tt < t1 - 1e-6:
            if tt >= 11.5:
                break
            n = int(t_step * 0.95 * SR)
            f = mtof(root + (12 if k % 2 == 1 else 0))
            tv = np.arange(n) / SR
            e = np.minimum(1, tv / 0.004) * np.exp(-tv / 0.22)
            sub = np.sin(2 * np.pi * mtof(root) * tv)
            mid = filt(saw(f, n), 900, "low", 2)
            s = (0.9 * sub + 0.35 * mid) * e
            s = np.tanh(1.6 * s) / 1.2
            place(bus, s, tt, gain=0.42)
            tt += t_step
            k += 1
    # cola grave en el final
    n = int(3.0 * SR)
    tv = np.arange(n) / SR
    s = np.sin(2 * np.pi * mtof(36) * tv) * np.exp(-tv / 1.1) * np.minimum(1, tv / 0.01)
    place(bus, s, 12.0, gain=0.38)
    return bus


def arp_bus():
    bus = buf()
    pattern = [0, 1, 2, 3, 2, 1, 2, 3]
    step = BEAT / 4
    for (t0, t1, notes, _) in CHORDS:
        if t0 < 2.0 or t0 >= 12.0:
            continue
        tt = t0
        i = 0
        while tt < t1 - 1e-6 and tt < 11.0:
            note = notes[pattern[i % len(pattern)] % len(notes)] + 12
            n = int(0.28 * SR)
            tv = np.arange(n) / SR
            e = np.minimum(1, tv / 0.002) * np.exp(-tv / 0.09)
            s = saw(mtof(note), n) * 0.6 + saw(mtof(note) * 1.004, n) * 0.4
            s = filt(s * e, 2400 + 1200 * (i % 4 == 0), "low", 2)
            acc = 1.0 if i % 4 == 0 else 0.7
            place(bus, s, tt, gain=0.13 * acc, pan=0.25 * np.sin(i * 1.3))
            tt += step
            i += 1
    # campanitas finales (FM bell) sobre el logo
    bell_notes = [84, 88, 91, 95, 98]
    for j, m in enumerate(bell_notes):
        t0 = 12.25 + j * step
        n = int(1.6 * SR)
        tv = np.arange(n) / SR
        f = mtof(m)
        mod = np.sin(2 * np.pi * f * 2.76 * tv) * 2.2 * np.exp(-tv / 0.25)
        s = np.sin(2 * np.pi * f * tv + mod) * np.exp(-tv / 0.55) * np.minimum(1, tv / 0.002)
        place(bus, s, t0, gain=0.07, pan=-0.6 + 0.3 * j)
    # ping-pong delay 3/16
    d = int(BEAT * 0.75 * SR)
    out = bus.copy()
    fb = 0.38
    src = bus.mean(axis=1)
    for k in range(1, 6):
        sh = np.zeros(N)
        if k * d < N:
            sh[k * d:] = src[:N - k * d]
        ch = k % 2
        out[:, ch] += sh * (fb ** k)
    return out


def drum_bus():
    bus = buf()
    # kick
    for kt in kick_times():
        n = int(0.45 * SR)
        tv = np.arange(n) / SR
        fr = 46 + 120 * np.exp(-tv / 0.032)
        ph = 2 * np.pi * np.cumsum(fr) / SR
        s = np.sin(ph) * np.exp(-tv / 0.26)
        click = filt(RNG.standard_normal(n) * np.exp(-tv / 0.003), 1500, "high") * 0.35
        s = np.tanh(2.2 * (s + click)) * 0.9
        place(bus, s, kt, gain=0.95)
    # clap/snare en 2 y 4
    clap_times = [2.0 + BEAT + i * 2 * BEAT for i in range(10)]
    clap_times = [c for c in clap_times if c < 11.0]
    for ct in clap_times:
        n = int(0.35 * SR)
        tv = np.arange(n) / SR
        nz = RNG.standard_normal(n)
        e = np.zeros(n)
        for off in (0.0, 0.011, 0.022):
            m = tv >= off
            e[m] += np.exp(-(tv[m] - off) / (0.008 if off < 0.02 else 0.085))
        s = filt(nz * e, [900, 5200], "band", 2) * 0.9
        tone = np.sin(2 * np.pi * 190 * tv) * np.exp(-tv / 0.05) * 0.4
        place(bus, s + tone, ct, gain=0.30)
    # redoble 11.0 -> 11.875
    tt = 11.0
    while tt < 11.86:
        prog = (tt - 11.0) / 0.875
        stepl = BEAT / 2 if prog < 0.25 else (BEAT / 4 if prog < 0.6 else BEAT / 8)
        n = int(0.15 * SR)
        tv = np.arange(n) / SR
        s = filt(RNG.standard_normal(n), [700, 6000], "band", 2) * np.exp(-tv / 0.06)
        s += np.sin(2 * np.pi * 200 * tv) * np.exp(-tv / 0.04) * 0.3
        place(bus, s, tt, gain=0.12 + 0.3 * prog ** 1.5)
        tt += stepl
    # hats
    step = BEAT / 4
    i = 0
    tt = 2.0
    while tt < 11.0:
        off = (i % 4) == 2
        n = int((0.16 if off else 0.05) * SR)
        tv = np.arange(n) / SR
        s = filt(RNG.standard_normal(n), 7500, "high", 2) * np.exp(-tv / (0.06 if off else 0.018))
        place(bus, s, tt, gain=0.16 if off else 0.07, pan=0.2 if off else -0.15)
        tt += step
        i += 1
    # hats suaves del outro
    tt = 12.5
    while tt < 14.6:
        n = int(0.05 * SR)
        tv = np.arange(n) / SR
        s = filt(RNG.standard_normal(n), 8000, "high", 2) * np.exp(-tv / 0.02)
        place(bus, s, tt, gain=0.045 * (1 - (tt - 12.5) / 2.5), pan=0.3)
        tt += BEAT / 2
    return bus


def fx_bus():
    bus = buf()
    # bloops de la isla (punto 0.45 s, pildora 0.95 s)
    for t0, f0, f1 in ((0.45, 620, 980), (0.95, 420, 760)):
        n = int(0.35 * SR)
        tv = np.arange(n) / SR
        fr = f0 + (f1 - f0) * (1 - np.exp(-tv / 0.03))
        s = np.sin(2 * np.pi * np.cumsum(fr) / SR) * np.exp(-tv / 0.09) * np.minimum(1, tv / 0.003)
        place(bus, s, t0, gain=0.16)
    # swell de intro hacia el drop
    n = int(2.0 * SR)
    tv = np.arange(n) / SR
    nz = RNG.standard_normal((n, 2))
    sw = tv_filter(nz, lambda t: 300 * (9000 / 300) ** (t / 2.0), "low", 2)
    sw *= ((tv / 2.0) ** 2.5)[:, None]
    place(bus, sw, 0.0, gain=0.10)
    # whooshes de cambio de pagina y colapso
    for t0 in (4.45, 5.95, 7.45, 8.95):
        n = int(0.45 * SR)
        tv = np.arange(n) / SR
        nz = RNG.standard_normal((n, 2))
        w = tv_filter(nz, lambda t: (500 + 5000 * np.sin(np.pi * min(t / 0.45, 1)), 1400 + 9000 * np.sin(np.pi * min(t / 0.45, 1))), "band", 1)
        w *= (np.sin(np.pi * np.clip(tv / 0.45, 0, 1)) ** 2)[:, None]
        place(bus, w, t0 - 0.05, gain=0.12)
    # click del boton "siguiente" (3.5 s) y "pop" al despegar (11.0 s)
    for t0, f in ((3.5, 1800), (11.0, 900)):
        n = int(0.08 * SR)
        tv = np.arange(n) / SR
        s = np.sin(2 * np.pi * f * tv) * np.exp(-tv / 0.012)
        place(bus, s, t0, gain=0.12)
    # riser 10 -> 11.875
    t0, t1 = 10.0, 11.875
    n = int((t1 - t0) * SR)
    tv = np.arange(n) / SR
    p = tv / (t1 - t0)
    nz = RNG.standard_normal((n, 2))
    r = tv_filter(nz, lambda t: (300 * 30 ** (t / (t1 - t0)), 900 * 12 ** (t / (t1 - t0))), "band", 1)
    r *= (p ** 2.2)[:, None]
    fr = mtof(55) * 2 ** (2 * p)
    tone = np.sin(2 * np.pi * np.cumsum(fr) / SR) * p ** 2 * 0.25
    r[:, 0] += tone
    r[:, 1] += tone
    fade = np.minimum(1, (len(tv) - np.arange(n)) / (0.01 * SR))
    r *= fade[:, None]
    place(bus, r, t0, gain=0.22)
    # impacto 12.0
    n = int(3.0 * SR)
    tv = np.arange(n) / SR
    fr = 38 + 110 * np.exp(-tv / 0.05)
    boom = np.sin(2 * np.pi * np.cumsum(fr) / SR) * np.exp(-tv / 0.9)
    boom = np.tanh(2.5 * boom) * 0.8
    place(bus, boom, 12.0, gain=0.9)
    crash = filt(RNG.standard_normal((n, 2)), 3500, "high", 2) * np.exp(-tv / 1.1)[:, None]
    place(bus, crash, 12.0, gain=0.055)
    return bus


def reverb(x, seconds=2.4, decay=0.55, seed=7):
    rng = np.random.default_rng(seed)
    n = int(seconds * SR)
    tv = np.arange(n) / SR
    ir = rng.standard_normal((n, 2)) * np.exp(-tv / decay)[:, None]
    ir = filt(ir, 6000, "low", 1)
    ir[: int(0.012 * SR)] = 0  # pre-delay
    ir /= np.sqrt((ir ** 2).sum(axis=0))
    out = np.stack([fftconvolve(x[:, c], ir[:, c])[:N] for c in range(2)], axis=1)
    return out


def limiter(x, ceiling):
    peak = np.abs(x).max(axis=1)
    g = np.minimum(1.0, ceiling / np.maximum(peak, 1e-9))
    look = int(0.005 * SR)
    g = minimum_filter1d(g, size=2 * look + 1)
    g = uniform_filter1d(g, size=look)
    g = np.minimum(g, ceiling / np.maximum(peak, 1e-9))
    return x * g[:, None]


def main(out_path):
    import pyloudnorm as pyln
    sc = sidechain_env()[:, None]
    pad = pad_bus()
    bass = bass_bus()
    arp = arp_bus()
    drums = drum_bus()
    fx = fx_bus()
    t = np.arange(N) / SR
    sc_pad = np.where(t[:, None] >= 2.0, sc, 1.0)

    dry = pad * sc_pad * 1.0 + bass * sc * 1.0 + arp * sc_pad * 0.9 + drums + fx
    send = pad * 0.35 + arp * 0.45 + drums * 0.06 + fx * 0.22
    wet = reverb(send)
    mix = dry + wet * 0.55
    mix = filt(mix, 28, "high", 2)

    # fades
    fi = np.minimum(1, t / 0.01)
    fo = np.clip((DUR - t) / 0.8, 0, 1)
    fo = 0.5 - 0.5 * np.cos(np.pi * fo)
    mix *= (fi * fo)[:, None]

    meter = pyln.Meter(SR)
    lufs = meter.integrated_loudness(mix)
    mix *= 10 ** ((-14.0 - lufs) / 20)
    mix = np.tanh(mix * 1.05) / 1.05  # saturacion suave
    ceiling = 10 ** (-1.2 / 20)
    mix = limiter(mix, ceiling)
    mix = np.clip(mix, -ceiling, ceiling)
    lufs2 = meter.integrated_loudness(mix)
    peak_db = 20 * np.log10(np.abs(mix).max())
    print(f"LUFS integrado: {lufs2:.2f}  pico: {peak_db:.2f} dBFS  duracion: {len(mix)/SR:.3f} s")

    import wave
    pcm = (mix * 32767).astype(np.int16)
    with wave.open(out_path, "wb") as w:
        w.setnchannels(2)
        w.setsampwidth(2)
        w.setframerate(SR)
        w.writeframes(pcm.tobytes())


if __name__ == "__main__":
    main(sys.argv[1] if len(sys.argv) > 1 else "zenit-music.wav")
