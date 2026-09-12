import io
import struct
import sys
from pathlib import Path
from PIL import Image, ImageDraw

RED = (153, 27, 27)
WHITE = (255, 255, 255)
SUPERSAMPLE = 4
CANVAS = 120.0


def draw_icon(size):
    big = size * SUPERSAMPLE
    unit = big / CANVAS
    canvas = Image.new("RGBA", (big, big), (0, 0, 0, 0))
    draw = ImageDraw.Draw(canvas)

    def rounded(x, y, w, h, radius, color):
        draw.rounded_rectangle([x * unit, y * unit, (x + w) * unit, (y + h) * unit],
                               radius=radius * unit, fill=color)

    rounded(0, 0, 120, 120, 26, RED + (255,))
    rounded(27, 22, 7, 26, 3, WHITE + (255,))
    rounded(36, 22, 7, 26, 3, WHITE + (255,))
    rounded(45, 22, 7, 26, 3, WHITE + (255,))
    rounded(27, 42, 25, 14, 6, WHITE + (255,))
    rounded(33, 50, 13, 48, 6, WHITE + (255,))
    rounded(69, 22, 25, 38, 12, WHITE + (255,))
    rounded(75, 52, 13, 46, 6, WHITE + (255,))

    return canvas.resize((size, size), Image.LANCZOS)


def save_ico(path, sizes):
    frames = []
    for size in sizes:
        buffer = io.BytesIO()
        draw_icon(size).save(buffer, format="PNG")
        frames.append((size, buffer.getvalue()))

    offset = 6 + 16 * len(frames)
    directory = b""
    payload = b""
    for size, png in frames:
        dimension = 0 if size >= 256 else size
        directory += struct.pack("<BBBBHHII", dimension, dimension, 0, 0, 1, 32, len(png), offset)
        payload += png
        offset += len(png)

    path.write_bytes(struct.pack("<HHH", 0, 1, len(frames)) + directory + payload)


def main(repo_root=None):
    root = Path(repo_root) if repo_root else Path(__file__).resolve().parent.parent
    assets = root / "desktop" / "GastronomyApp.Desktop" / "Assets"
    branding = root / "branding"
    assets.mkdir(parents=True, exist_ok=True)

    save_ico(assets / "app.ico", [16, 24, 32, 48, 64, 128, 256])
    draw_icon(512).save(branding / "gastronomy-icon-512.png")

    print(f"Wrote the GastronomyApp icon under {root.resolve()}")


if __name__ == "__main__":
    main(sys.argv[1] if len(sys.argv) > 1 else None)
