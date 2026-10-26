"""
Renderiza el motion graphic de Zenit cuadro a cuadro con Chromium headless
y lo codifica a MP4 (H.264 + AAC) con el ffmpeg incluido en imageio-ffmpeg.

Uso:
  python render.py --fmt 16x9 --audio musica.wav --out ../../docs/media/zenit-promo-16x9.mp4
  python render.py --fmt 9x16 --stills 1.0 3.0 6.5 --stills-dir C:/tmp/frames   (solo cuadros PNG)
  python render.py --fmt 16x9 --poster ../../docs/media/zenit-promo-16x9.png --poster-t 13.9
"""
import argparse
import mimetypes
import subprocess
import sys
import time
from pathlib import Path

from playwright.sync_api import sync_playwright

HERE = Path(__file__).resolve().parent
REPO = HERE.parent.parent
HOST = "http://zenit.local"
FPS = 30
DUR = 15.0


def open_page(p, fmt):
    w, h = (1080, 1920) if fmt == "9x16" else (1920, 1080)
    browser = p.chromium.launch(args=["--force-color-profile=srgb", "--disable-gpu-vsync"])
    page = browser.new_page(viewport={"width": w, "height": h}, device_scale_factor=1)

    def handle(route):
        rel = route.request.url[len(HOST):].split("?")[0].lstrip("/")
        f = (REPO / rel).resolve()
        if REPO in f.parents and f.is_file():
            ctype = mimetypes.guess_type(str(f))[0] or "application/octet-stream"
            route.fulfill(status=200, body=f.read_bytes(), headers={"content-type": ctype})
        else:
            route.fulfill(status=404, body="not found")

    page.route(f"{HOST}/**", handle)
    page.goto(f"{HOST}/marketing/motion/index.html?fmt={fmt}")
    page.evaluate("window.ready")
    return browser, page, (w, h)


def shot(page, w, h):
    return page.screenshot(type="png", clip={"x": 0, "y": 0, "width": w, "height": h}, animations="disabled")


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--fmt", choices=["16x9", "9x16"], default="16x9")
    ap.add_argument("--out")
    ap.add_argument("--audio")
    ap.add_argument("--samples", type=int, default=0, help="sub-cuadros de motion blur (0 = automatico: 6, o 16 en movimientos rapidos)")
    ap.add_argument("--stills", type=float, nargs="*")
    ap.add_argument("--stills-dir", default=".")
    ap.add_argument("--poster")
    ap.add_argument("--poster-t", type=float, default=13.9)
    a = ap.parse_args()

    import imageio_ffmpeg
    ffmpeg = imageio_ffmpeg.get_ffmpeg_exe()

    with sync_playwright() as p:
        browser, page, (w, h) = open_page(p, a.fmt)

        if a.stills:
            out_dir = Path(a.stills_dir)
            out_dir.mkdir(parents=True, exist_ok=True)
            for t in a.stills:
                f = round(t * FPS)
                page.evaluate(f"window.renderFrame({f}, {{samples: {a.samples}}})")
                path = out_dir / f"{a.fmt}_{t:05.2f}.png"
                path.write_bytes(shot(page, w, h))
                print("still", path)

        if a.poster:
            f = round(a.poster_t * FPS)
            page.evaluate(f"window.renderFrame({f}, {{samples: 1}})")
            Path(a.poster).parent.mkdir(parents=True, exist_ok=True)
            Path(a.poster).write_bytes(shot(page, w, h))
            print("poster", a.poster)

        if a.out:
            Path(a.out).parent.mkdir(parents=True, exist_ok=True)
            cmd = [ffmpeg, "-y", "-loglevel", "error",
                   "-f", "image2pipe", "-framerate", str(FPS), "-c:v", "png", "-i", "-"]
            if a.audio:
                cmd += ["-i", a.audio]
            cmd += ["-map", "0:v"] + (["-map", "1:a"] if a.audio else [])
            cmd += ["-vf", "scale=out_color_matrix=bt709:out_range=tv,format=yuv420p",
                    "-c:v", "libx264", "-preset", "slow", "-crf", "18", "-profile:v", "high",
                    "-color_primaries", "bt709", "-color_trc", "bt709", "-colorspace", "bt709", "-color_range", "tv",
                    "-r", str(FPS)]
            if a.audio:
                cmd += ["-c:a", "aac", "-b:a", "192k", "-ar", "44100"]
            cmd += ["-t", f"{DUR:.3f}", "-movflags", "+faststart", a.out]
            proc = subprocess.Popen(cmd, stdin=subprocess.PIPE)
            n = int(round(DUR * FPS))
            t0 = time.time()
            for f in range(n):
                page.evaluate(f"window.renderFrame({f}, {{samples: {a.samples}}})")
                proc.stdin.write(shot(page, w, h))
                if f % 30 == 0:
                    el = time.time() - t0
                    print(f"[{a.fmt}] frame {f}/{n}  {el:.0f}s", flush=True)
            proc.stdin.close()
            rc = proc.wait()
            if rc != 0:
                sys.exit(f"ffmpeg fallo con codigo {rc}")
            print("video", a.out)

        browser.close()


if __name__ == "__main__":
    main()
