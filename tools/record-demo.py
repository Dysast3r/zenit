"""Graba el recorrido de la isla (modo --demo tour, solo en Debug) y lo guarda como GIF para el README.

El recorrido se muestra sobre un fondo propio, así que no aparece nada del escritorio.
Requiere Pillow.  Uso:
    python tools/record-demo.py src/Zenit/bin/Debug/net10.0-windows10.0.22621.0/Zenit.exe docs/media/zenit-demo.gif
"""
import ctypes
import subprocess
import sys
import time
from ctypes import wintypes

from PIL import Image, ImageGrab

FPS = 15
SECONDS = 21.5
WIDTH = 640

ctypes.windll.shcore.SetProcessDpiAwareness(2)  # coordenadas en píxeles físicos
user32 = ctypes.windll.user32


def find_window(pid: int, title: str) -> int:
    found = []

    @ctypes.WINFUNCTYPE(wintypes.BOOL, wintypes.HWND, wintypes.LPARAM)
    def callback(hwnd, _):
        owner = wintypes.DWORD()
        user32.GetWindowThreadProcessId(hwnd, ctypes.byref(owner))
        buffer = ctypes.create_unicode_buffer(64)
        user32.GetWindowTextW(hwnd, buffer, 64)
        if owner.value == pid and buffer.value == title and user32.IsWindowVisible(hwnd):
            found.append(hwnd)
        return True

    user32.EnumWindows(callback, 0)
    return found[0] if found else 0


def main() -> None:
    exe, out = sys.argv[1], sys.argv[2]
    process = subprocess.Popen([exe, "--demo", "tour"])
    try:
        hwnd = 0
        for _ in range(50):
            time.sleep(0.1)
            hwnd = find_window(process.pid, "Zenit")
            if hwnd:
                break
        if not hwnd:
            raise SystemExit("No se encontró la ventana de la isla")

        # Se espera a que el fondo neutro del recorrido esté visible: así nunca se graba el escritorio.
        time.sleep(1.0)
        rect = wintypes.RECT()
        user32.GetWindowRect(hwnd, ctypes.byref(rect))
        # La isla expandida ocupa los ~240 px superiores de la ventana (en unidades lógicas).
        scale = (rect.right - rect.left) / 560
        bbox = (rect.left, rect.top, rect.right, rect.top + int(250 * scale))

        frames = []
        start = time.perf_counter()
        while time.perf_counter() - start < SECONDS:
            tick = time.perf_counter()
            frames.append(ImageGrab.grab(bbox=bbox, all_screens=True))
            time.sleep(max(0.0, 1 / FPS - (time.perf_counter() - tick)))
    finally:
        process.kill()

    height = round(frames[0].height * WIDTH / frames[0].width)
    scaled = [f.convert("RGB").resize((WIDTH, height), Image.LANCZOS) for f in frames]

    # Cada cuadro lleva su propia paleta (así las carátulas conservan su color), pero se calcula con una
    # franja de referencia debajo: grises y los colores fijos de la interfaz (temporizador, cronómetro,
    # reuniones, batería). Si no, el fondo azul se queda con casi todos los colores y el texto pequeño
    # pierde el suyo: el blanco sale azulado y el naranja, gris.
    ramp = Image.new("RGB", (WIDTH, height // 3))
    ramp.paste(Image.linear_gradient("L").rotate(90, expand=True).resize((WIDTH, height // 6)).convert("RGB"), (0, 0))
    accents = [(0xFF, 0x9F, 0x0A), (0x30, 0xD1, 0x58), (0x0A, 0x84, 0xFF), (0xFF, 0x45, 0x3A)]
    block = WIDTH // len(accents)
    for i, color in enumerate(accents):
        ramp.paste(color, (i * block, height // 6, (i + 1) * block, height // 3))
    palette_frames = []
    for frame in scaled:
        canvas = Image.new("RGB", (WIDTH, height + ramp.height))
        canvas.paste(frame, (0, 0))
        canvas.paste(ramp, (0, height))
        quantized = canvas.quantize(colors=256, method=Image.Quantize.MEDIANCUT)
        palette_frames.append(quantized.crop((0, 0, WIDTH, height)))
    palette_frames[0].save(out, save_all=True, append_images=palette_frames[1:],
                           duration=round(1000 / FPS), loop=0, optimize=True, disposal=1)
    print(f"{out}: {len(frames)} cuadros, {WIDTH}x{height}")


if __name__ == "__main__":
    main()
