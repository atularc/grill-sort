"""Game audio (2026-11-21, user: "add proper sound effects and background music ... decent and so satisfying").

All sources are CC0 (see ArtSource/Audio/LICENSES.md). This script:
  * trims leading silence, adds tiny fades, and peak-levels every effect to its own target (so nothing pokes out),
  * synthesizes the grill sizzle (one-shot) and a seamless sizzle ambience loop (no CC0 pack had a good sizzle),
  * levels the background music to a calm loudness.
Writes NEW files to Assets/GrillSort/Audio/SFX/*.ogg and Assets/GrillSort/Audio/Music/*.ogg (the old synthesized
placeholders in Assets/GrillSort/Audio/*.wav stay as backups).

Usage (repo root): python -I Tools/ArtPipeline/make_audio.py      (always overwrites its own outputs)
Needs ffmpeg with libvorbis on PATH.
"""
import os
import subprocess

import numpy as np

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
SRC = os.path.join(ROOT, "ArtSource", "Audio")
SFX = os.path.join(ROOT, "Assets", "GrillSort", "Audio", "SFX")
MUS = os.path.join(ROOT, "Assets", "GrillSort", "Audio", "Music")
SR = 44100

# game name -> (source in ArtSource/Audio/Kenney, peak dBFS). Quieter targets for the frequent sounds.
EFFECTS = {
    # pick + drop are SYNTHESIZED (see pop_sound): the user found the pluck / soft-thud versions bad on every move
    "invalid": ("error_004.ogg", -8),         # can't drop there: short soft buzz
    # "click" is synthesized (ikame_click): matched to the client's games, see below
    "star": ("glass_002.ogg", -5),            # win stars (pitched up per star) + sparkle
    "whoosh": ("maximize_008.ogg", -7),       # skewer flies to the customer, mascot hops off
    "pop": ("drop_002.ogg", -6),              # bubbles / logo landing
    "coin": ("handleCoins.ogg", -6),          # reward coins
    "win": ("jingles_STEEL03.ogg", -3),       # level complete: steel drums C C C5 (rising)
    "serve": ("jingles_STEEL10.ogg", -5),     # order served: steel drums D -> G
    "fail": ("jingles_STEEL07.ogg", -6),      # no moves: steel drums E D# D (gentle "wah-wah")
}
# background music (user pick 2026-11-21): "Which Brand of Mustard Shall I Buy?" (CC0). Its file ends in a fade-out,
# so it is cut at LOOP_END (123.15 s: the point where the music repeats its opening, found by matching the spectrum
# against the first 4 s, 0.93 correlation) and the seam is crossfaded, so it loops with no dip or pause.
# The other candidates (buy_something, apple_cider, two_left_socks, chill_lofi, funked_up) stay in ArtSource/Audio/Music.
MUSIC_SRC, MUSIC_OUT, LOOP_END, LOOP_XFADE = "mustard_bossa.ogg", "bgm_main.ogg", 123.15, .05
MUSIC_LUFS = -20


def decode(path):
    raw = subprocess.run(["ffmpeg", "-v", "error", "-i", path, "-ac", "1", "-ar", str(SR), "-f", "f32le", "-"],
                         capture_output=True, check=True).stdout
    return np.frombuffer(raw, np.float32).copy()


def encode(x, path, quality=5, channels=1):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    x = np.clip(x, -1, 1).astype(np.float32)
    subprocess.run(["ffmpeg", "-v", "error", "-y", "-f", "f32le", "-ar", str(SR), "-ac", str(channels), "-i", "-",
                    "-c:a", "libvorbis", "-q:a", str(quality), path], input=x.tobytes(), check=True)
    print("wrote", os.path.relpath(path, ROOT), f"{len(x) / SR / channels:.2f}s")


def tidy(x, peak_db):
    """Trim leading silence, 3 ms fade-in, 25 ms fade-out, peak-normalise."""
    thr = 10 ** (-50 / 20) * max(1e-6, np.abs(x).max())
    nz = np.nonzero(np.abs(x) > thr)[0]
    if len(nz):
        x = x[max(0, nz[0] - int(.002 * SR)):nz[-1] + int(.01 * SR)]
    fi, fo = int(.003 * SR), min(len(x) // 3, int(.025 * SR))
    x[:fi] *= np.linspace(0, 1, fi)
    x[-fo:] *= np.linspace(1, 0, fo)
    return x / max(1e-6, np.abs(x).max()) * 10 ** (peak_db / 20)


def shaped_noise(n, lo, hi, rng, tilt=0.0):
    """Noise band-limited to lo..hi Hz with a gentle tilt, by FFT (circular, so it loops seamlessly)."""
    spec = np.fft.rfft(rng.standard_normal(n))
    f = np.fft.rfftfreq(n, 1 / SR)
    mask = np.clip((f - lo) / (lo * .5), 0, 1) * np.clip((hi - f) / (hi * .3), 0, 1)
    mask *= (np.maximum(f, 1) / 4000) ** tilt
    y = np.fft.irfft(spec * mask, n)
    return y / np.abs(y).max()


def sizzle(seconds, rng, loop=False):
    """Grill sizzle: a fizzy hiss that flutters, plus fat crackles and spits of different sizes."""
    n = int(seconds * SR)
    hiss = shaped_noise(n, 2500, 14000, rng, tilt=-.4)
    flutter = shaped_noise(n, .5, 18, rng)                       # slow random wobble of the hiss
    hiss *= .55 + .45 * (flutter * .5 + .5)
    crack = np.zeros(n)
    t = 0.0
    rate = 55.0                                                   # crackles per second
    while True:
        t += rng.exponential(1 / rate)
        i = int(t * SR)
        if i >= n:
            break
        ln = int(rng.uniform(.002, .012) * SR)
        burst = rng.standard_normal(ln) * np.exp(-np.linspace(0, rng.uniform(4, 9), ln))
        amp = rng.choice([.25, .4, .7, 1.0], p=[.45, .3, .17, .08])  # mostly small ticks, some big spits
        j = min(n, i + ln)
        crack[i:j] += burst[:j - i] * amp
    # crackles are bright clicks: high-pass them by FFT
    cs = np.fft.rfft(crack); f = np.fft.rfftfreq(n, 1 / SR)
    crack = np.fft.irfft(cs * np.clip((f - 1200) / 1200, 0, 1), n)
    crack /= max(1e-6, np.abs(crack).max())
    y = hiss * .5 + crack * .65
    if not loop:
        t = np.arange(n) / SR
        env = np.minimum(1, t / .015) * np.where(t < .18, 1, np.exp(-(t - .18) / .42))
        y *= env
    return y / np.abs(y).max()


def pop_sound(f0, f1, dur, body=1.0, tick=0.0, seed=0):
    """A round, bubbly pop: a sine that glides f0 -> f1 (exponential), soft 2 ms attack, smooth decay, a hint of
    the 2nd harmonic for warmth and an optional tiny filtered tick on the attack. Clean and never harsh."""
    rng = np.random.default_rng(seed)
    n = int(dur * SR); t = np.arange(n) / SR
    f = f0 * (f1 / f0) ** np.minimum(1, t / (dur * .55))
    ph = 2 * np.pi * np.cumsum(f) / SR
    env = np.minimum(1, t / .002) * np.exp(-t / (dur * .28))
    y = (np.sin(ph) + .18 * np.sin(2 * ph)) * env * body
    if tick > 0:
        k = int(.004 * SR)
        click = rng.standard_normal(k) * np.exp(-np.linspace(0, 6, k))
        cs = np.fft.rfft(click, n); fr = np.fft.rfftfreq(n, 1 / SR)
        click = np.fft.irfft(cs * np.exp(-((fr - 3000) / 1800) ** 2), n)
        y += click / max(1e-6, np.abs(click).max()) * tick
    fo = int(.01 * SR); y[-fo:] *= np.linspace(1, 0, fo)
    return y / np.abs(y).max()


# ---- iKame-style taps (2026-11-22, user: "we need same click sfx and other sfx" as the client's games).
# Measured on their screen recordings (FishUp!, Skewer Jam, Screwdom 3D, Mahjong; music + compression make the sounds
# impossible to lift cleanly, so they are rebuilt from the measurements):
#   * FishUp! UI button: a soft, warm DOUBLE tap "tok-tok", the 2nd 50 ms after the 1st, brightness ~800 Hz (4 of 4 taps)
#   * Skewer Jam tap / drop: a mellow wooden "tock" with a ~420 Hz tone (G#4-A4); a dropped stack lands as one tock per
#     piece ~125 ms apart (the drop cascade)
#   * all of them are warm and short (no bright digital pings)
def tock(f, dur=.11, body=1.0, click=.35, bright=2.6, seed=0):
    """Wood-block style tap: two decaying partials (f, f x bright) + a soft filtered click, no glide."""
    rng = np.random.default_rng(seed)
    n = int(dur * SR); t = np.arange(n) / SR
    # short tonal body (the references ring only ~30 ms) under a crisp broadband click (up to ~5 kHz)
    env = np.minimum(1, t / .0012) * np.exp(-t / (dur * .13))
    y = np.sin(2 * np.pi * f * t) * env * body + .3 * np.sin(2 * np.pi * f * bright * t) * np.exp(-t / (dur * .06))
    k = int(.004 * SR)
    c = np.zeros(n); c[:k] = rng.standard_normal(k) * np.exp(-np.linspace(0, 6, k))
    cs = np.fft.rfft(c); fr = np.fft.rfftfreq(n, 1 / SR)
    c = np.fft.irfft(cs * np.exp(-((fr - 2600) / 2600) ** 2), n)
    y += c / max(1e-6, np.abs(c).max()) * click
    fo = int(.008 * SR); y[-fo:] *= np.linspace(1, 0, fo)
    return y / np.abs(y).max()


def ikame_click():
    """FishUp!-style UI tap: tok (higher) + tok (lower) 50 ms apart, warm."""
    a = tock(760, .07, body=.6, click=.9, bright=2.1, seed=3)
    b = tock(560, .08, body=.6, click=.8, bright=2.1, seed=4) * .9
    gap = int(.05 * SR)
    y = np.zeros(gap + len(b)); y[:len(a)] += a; y[gap:] += b
    return y / np.abs(y).max()


def main():
    rng = np.random.default_rng(7)
    # pick: a light upward "bloop" (lifting); drop: a round, slightly lower "plop" + soft tick (landing), 3 sizes
    # Skewer Jam-style wooden tocks (the earlier soft "bloop" pops: pop_sound, kept for reference)
    encode(tock(470, .09, click=.6, seed=1) * 10 ** (-8 / 20), os.path.join(SFX, "pick.ogg"))
    for i, f in enumerate([415, 405, 428]):
        encode(tock(f, .12, click=.55, seed=10 + i) * 10 ** (-7 / 20), os.path.join(SFX, f"drop_{i}.ogg"))
    encode(ikame_click() * 10 ** (-7 / 20), os.path.join(SFX, "click.ogg"))
    for name, (src, peak) in EFFECTS.items():
        encode(tidy(decode(os.path.join(SRC, "Kenney", src)), peak), os.path.join(SFX, name + ".ogg"))
    encode(sizzle(1.5, rng) * 10 ** (-4 / 20), os.path.join(SFX, "sizzle.ogg"))
    encode(sizzle(8.0, rng, loop=True) * 10 ** (-10 / 20), os.path.join(SFX, "sizzle_loop.ogg"))
    # music: stereo, loudness-normalised, decent Vorbis quality
    os.makedirs(MUS, exist_ok=True)
    raw = subprocess.run(["ffmpeg", "-v", "error", "-i", os.path.join(SRC, "Music", MUSIC_SRC), "-ar", str(SR), "-ac", "2",
                          "-af", f"loudnorm=I={MUSIC_LUFS}:TP=-2:LRA=11", "-f", "f32le", "-"], capture_output=True, check=True).stdout
    st = np.frombuffer(raw, np.float32).reshape(-1, 2).copy()
    L, c = int(LOOP_END * SR), int(LOOP_XFADE * SR)
    loop = st[:L].copy()
    ramp = np.linspace(0, 1, c)[:, None]
    loop[:c] = st[:c] * ramp + st[L:L + c] * (1 - ramp)   # the start blends in from what follows the loop end
    encode(loop.reshape(-1), os.path.join(MUS, MUSIC_OUT), quality=4, channels=2)


if __name__ == "__main__":
    main()
