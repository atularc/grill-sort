"""Sounds taken from the client's (iKame's) own games, from the screen recordings the user shared on 2026-11-22
(Downloads/Ikame sfx and splasg screen refernce/). User: our synthesized serve and win sounds were "frustrating" next to
these games. These are iKame's assets, used in iKame's own game; ask iKame for the original files to replace these.

Only moments where the game's music was silent (or far below the sound) are usable; measured signal / background:
  * win   = Skewer Jam level-complete jingle (11.20.49.mp4 @ 8.85-11.0 s; the music stops for it)
  * serve = Screwdom 3D "Well Done" reward chime, first 0.75 s (11.10.39.mp4 @ 20.95 s; ~15 dB above the music)
Skewer Jam's own serve sound sits under loud music (~2 dB) and could not be extracted.

Each clip: high-pass 90 Hz, spectral gating against a music-only stretch next to it (removes what is left of the music),
trim, short fades, loudness-matched. Sources copied to ArtSource/Audio/iKameRef/; outputs Assets/GrillSort/Audio/SFX/
win.ogg + serve.ogg (the synthesized versions are kept as win_synth.ogg / serve_synth.ogg by make_audio.py).

Usage (repo root): python -I Tools/ArtPipeline/extract_ref_sfx.py
"""
import os
import shutil
import subprocess

import numpy as np

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
REF = os.path.join(os.path.expanduser("~"), "Downloads", "Ikame sfx and splasg screen refernce")
KEEP = os.path.join(ROOT, "ArtSource", "Audio", "iKameRef")
SFX = os.path.join(ROOT, "Assets", "GrillSort", "Audio", "SFX")
SR = 44100

CLIPS = {
    # name: (video, start, end, noise_start, noise_end, peak dB)
    "win": ("WhatsApp Video 2026-11-22 at 11.20.49.mp4", 8.85, 11.0, 11.05, 11.9, -2),
    "serve": ("WhatsApp Video 2026-11-22 at 11.10.39.mp4", 20.95, 21.7, 20.2, 20.85, -4),
}


def decode(path, t0, t1):
    raw = subprocess.run(["ffmpeg", "-v", "error", "-ss", str(t0), "-t", str(t1 - t0), "-i", path,
                          "-ac", "1", "-ar", str(SR), "-f", "f32le", "-"], capture_output=True, check=True).stdout
    return np.frombuffer(raw, np.float32).copy()


def stft(x, n=2048, hop=512):
    w = np.hanning(n)
    frames = [np.fft.rfft(x[i:i + n] * w) for i in range(0, max(1, len(x) - n), hop)]
    return np.array(frames), w


def istft(F, w, length, n=2048, hop=512):
    y = np.zeros(length + n); norm = np.zeros(length + n)
    for k, f in enumerate(F):
        i = k * hop
        y[i:i + n] += np.fft.irfft(f, n) * w; norm[i:i + n] += w * w
    return (y / np.maximum(norm, 1e-6))[:length]


def clean(sig, noise):
    F, w = stft(sig)
    N, _ = stft(noise)
    prof = np.percentile(np.abs(N), 80, axis=0) * 1.4          # music level per frequency
    mag = np.abs(F)
    gain = np.clip((mag - prof) / np.maximum(mag, 1e-9), .08, 1.0)  # gate, never fully silent (no "underwater" artefacts)
    # smooth the gain over time so it doesn't flutter
    for k in range(1, len(gain)): gain[k] = np.maximum(gain[k], gain[k - 1] * .6)
    y = istft(F * gain, w, len(sig))
    Y = np.fft.rfft(y); f = np.fft.rfftfreq(len(y), 1 / SR); Y[f < 90] = 0
    return np.fft.irfft(Y, len(y))


def tidy(x, peak_db):
    fi, fo = int(.004 * SR), int(.12 * SR)
    x[:fi] *= np.linspace(0, 1, fi); x[-fo:] *= np.linspace(1, 0, fo)
    return x / np.abs(x).max() * 10 ** (peak_db / 20)


def main():
    os.makedirs(KEEP, exist_ok=True)
    for name, (vid, t0, t1, n0, n1, pk) in CLIPS.items():
        src = os.path.join(REF, vid)
        sig = decode(src, t0, t1); noise = decode(src, n0, n1)
        y = tidy(clean(sig, noise), pk)
        raw = os.path.join(KEEP, f"{name}_raw.wav")
        subprocess.run(["ffmpeg", "-v", "error", "-y", "-f", "f32le", "-ar", str(SR), "-ac", "1", "-i", "-", raw],
                       input=sig.astype(np.float32).tobytes(), check=True)
        out = os.path.join(SFX, name + ".ogg")
        subprocess.run(["ffmpeg", "-v", "error", "-y", "-f", "f32le", "-ar", str(SR), "-ac", "1", "-i", "-",
                        "-c:a", "libvorbis", "-q:a", "6", out], input=y.astype(np.float32).tobytes(), check=True)
        print("wrote", os.path.relpath(out, ROOT), f"{len(y) / SR:.2f}s (raw copy: {os.path.relpath(raw, ROOT)})")


if __name__ == "__main__":
    main()
