"""Extract analysis frames from every downloaded Sort Streak video (research only).

For each video in index.csv:
  frames/<src>_L<level>_start.png   phone screen at t=2.5 s, scaled up (starting board)
  frames/<src>_L<level>_sheetNN.png contact sheets: one frame every 2.5 s, 10 per row, 2 rows per sheet
"""
import csv, os, subprocess, sys

FF = r"C:\Users\zod\AppData\Local\Microsoft\WinGet\Packages\Gyan.FFmpeg_Microsoft.Winget.Source_8wekyb3d8bbwe\ffmpeg-8.1.2-full_build\bin\ffmpeg.exe"
HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(HERE, "frames")
os.makedirs(OUT, exist_ok=True)
CROP = {"A": "crop=240:480:200:0", "B": "crop=368:480:243:0,scale=240:-2"}
STEP = 2.5
PER_SHEET = 20


def run(args):
    subprocess.run([FF, "-v", "error", "-y", *args], check=True)


def main():
    rows = list(csv.DictReader(open(os.path.join(HERE, "index.csv"), encoding="utf-8")))
    for r in rows:
        src, lv, secs = r["src"], int(r["level"]), float(r["seconds"] or 0)
        vid = os.path.join(HERE, "vid", r["file"])
        tag = f"{src}_L{lv:03d}"
        crop = CROP[src] if lv > 0 else CROP[src]
        start = os.path.join(OUT, f"{tag}_start.png")
        if not os.path.exists(start):
            run(["-ss", "2.5", "-i", vid, "-frames:v", "1", "-vf", crop + ",scale=480:-2", "-update", "1", start])
        n = int(secs // STEP) + 1
        sheets = (n + PER_SHEET - 1) // PER_SHEET
        if os.path.exists(os.path.join(OUT, f"{tag}_sheet{sheets:02d}.png")):
            continue
        run(["-i", vid, "-vf", f"fps=1/{STEP},{crop},tile=10x2:padding=4:color=white",
             os.path.join(OUT, f"{tag}_sheet%02d.png")])
        print(tag, n, "frames", sheets, "sheets", flush=True)


if __name__ == "__main__":
    main()
