"""Renders the application icon: a Latham-style horn of spheres coiling inward.

Pure Python (no dependencies). Writes AppIcon.ico (16-256 px) and AppIcon.png (256 px)
into src/Morphogen.App/Assets.

    python tools/make_icon.py
"""

import colorsys
import math
import os
import struct
import zlib

SIZE = 1536  # master render size; divisible by every icon size
ICON_SIZES = [256, 128, 64, 48, 32, 24, 16]
OUT_DIR = os.path.join(os.path.dirname(__file__), "..", "src", "Morphogen.App", "Assets")


def normalize(v):
    length = math.sqrt(sum(c * c for c in v))
    return tuple(c / length for c in v)


def build_horn():
    """Returns spheres (x, y, z, radius, rgb) along a tapering 3D spiral, in object space."""
    spheres = []
    count = 46
    turns = 1.7
    tilt = math.radians(38)
    for i in range(count):
        t = i / (count - 1)
        theta = t * turns * 2 * math.pi
        path_radius = math.exp(-1.55 * t * turns)
        x = path_radius * math.cos(theta)
        y = path_radius * math.sin(theta)
        z = 0.55 * (1 - path_radius)  # rises as it coils, like a shell
        # Tilt toward the viewer so the coil reads as 3D.
        y, z = y * math.cos(tilt) - z * math.sin(tilt), y * math.sin(tilt) + z * math.cos(tilt)
        radius = 0.36 * path_radius + 0.02
        hue = (0.07 - 0.27 * t) % 1.0  # orange -> pink -> violet
        value = 1.0 if i % 2 == 0 else 0.82  # ribbed segments
        rgb = colorsys.hsv_to_rgb(hue, 0.78, value)
        spheres.append((x, y, z, radius, rgb))
    return spheres


def fit(spheres, margin):
    """Scales and centres the spheres into pixel space with the given fractional margin."""
    min_x = min(s[0] - s[3] for s in spheres)
    max_x = max(s[0] + s[3] for s in spheres)
    min_y = min(s[1] - s[3] for s in spheres)
    max_y = max(s[1] + s[3] for s in spheres)
    span = max(max_x - min_x, max_y - min_y)
    scale = SIZE * (1 - 2 * margin) / span
    cx, cy = (min_x + max_x) / 2, (min_y + max_y) / 2
    return [
        (SIZE / 2 + (x - cx) * scale, SIZE / 2 - (y - cy) * scale, z * scale, r * scale, rgb)
        for x, y, z, r, rgb in spheres
    ]


def rounded_rect_coverage(px, py, size, corner):
    """Anti-aliased coverage of a rounded square filling the image."""
    half = size / 2
    dx = max(abs(px - half) - (half - corner), 0)
    dy = max(abs(py - half) - (half - corner), 0)
    d = math.hypot(dx, dy) - corner
    return min(max(0.5 - d, 0.0), 1.0)


def render():
    pixels = [[0.0, 0.0, 0.0, 0.0] for _ in range(SIZE * SIZE)]
    corner = SIZE * 0.2
    top, bottom = (0.10, 0.11, 0.24), (0.07, 0.26, 0.32)
    for py in range(SIZE):
        f = py / (SIZE - 1)
        base = [top[c] + (bottom[c] - top[c]) * f for c in range(3)]
        row = py * SIZE
        for px in range(SIZE):
            cov = rounded_rect_coverage(px + 0.5, py + 0.5, SIZE, corner)
            if cov <= 0:
                continue
            # Soft glow behind the form.
            g = math.exp(-((px - SIZE * 0.5) ** 2 + (py - SIZE * 0.48) ** 2) / (2 * (SIZE * 0.28) ** 2)) * 0.22
            pixels[row + px] = [base[0] + g * 0.9, base[1] + g * 0.6, base[2] + g * 0.8, cov]

    light = normalize((-0.45, 0.65, 0.62))
    half_vec = normalize((light[0], light[1], light[2] + 1))
    zbuf = [-1e30] * (SIZE * SIZE)
    for sx, sy, sz, r, rgb in fit(build_horn(), 0.12):
        for py in range(max(0, int(sy - r)), min(SIZE, int(sy + r) + 2)):
            for px in range(max(0, int(sx - r)), min(SIZE, int(sx + r) + 2)):
                dx, dy = (px + 0.5 - sx) / r, -(py + 0.5 - sy) / r
                d2 = dx * dx + dy * dy
                if d2 >= 1:
                    continue
                nz = math.sqrt(1 - d2)
                depth = sz + nz * r
                idx = py * SIZE + px
                if depth <= zbuf[idx]:
                    continue
                zbuf[idx] = depth
                n = (dx, dy, nz)
                diffuse = max(0.0, n[0] * light[0] + n[1] * light[1] + n[2] * light[2])
                spec = max(0.0, n[0] * half_vec[0] + n[1] * half_vec[1] + n[2] * half_vec[2]) ** 48
                rim = (1 - nz) ** 3
                out = []
                for c in range(3):
                    v = rgb[c] * (0.16 + 0.9 * diffuse) + 0.75 * spec + rim * (0.25, 0.75, 0.9)[c] * 0.55
                    out.append(v)
                # Edge anti-aliasing against what is already there.
                edge = min(1.0, (1 - math.sqrt(d2)) * r)
                prev = pixels[idx]
                pixels[idx] = [prev[c] + (out[c] - prev[c]) * edge for c in range(3)] + [max(prev[3], edge)]
    return pixels


def downsample(pixels, size):
    factor = SIZE // size
    out = bytearray()
    area = factor * factor
    for y in range(size):
        out.append(0)  # PNG filter type: none
        for x in range(size):
            acc = [0.0, 0.0, 0.0, 0.0]
            for sy in range(y * factor, (y + 1) * factor):
                row = sy * SIZE
                for sx in range(x * factor, (x + 1) * factor):
                    p = pixels[row + sx]
                    a = p[3]
                    acc[0] += p[0] * a
                    acc[1] += p[1] * a
                    acc[2] += p[2] * a
                    acc[3] += a
            alpha = acc[3] / area
            for c in range(3):
                v = acc[c] / acc[3] if acc[3] > 0 else 0
                out.append(int(round(min(max(v, 0.0), 1.0) ** (1 / 1.15) * 255)))
            out.append(int(round(alpha * 255)))
    return bytes(out)


def png(size, raw):
    def chunk(kind, data):
        return struct.pack(">I", len(data)) + kind + data + struct.pack(">I", zlib.crc32(kind + data) & 0xFFFFFFFF)

    header = struct.pack(">IIBBBBB", size, size, 8, 6, 0, 0, 0)
    return b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", header) + chunk(b"IDAT", zlib.compress(raw, 9)) + chunk(b"IEND", b"")


def ico(images):
    header = struct.pack("<HHH", 0, 1, len(images))
    offset = 6 + 16 * len(images)
    entries, data = b"", b""
    for size, blob in images:
        dim = 0 if size >= 256 else size
        entries += struct.pack("<BBBBHHII", dim, dim, 0, 0, 1, 32, len(blob), offset)
        offset += len(blob)
        data += blob
    return header + entries + data


def main():
    pixels = render()
    images = [(s, png(s, downsample(pixels, s))) for s in ICON_SIZES]
    os.makedirs(OUT_DIR, exist_ok=True)
    with open(os.path.join(OUT_DIR, "AppIcon.ico"), "wb") as f:
        f.write(ico(images))
    with open(os.path.join(OUT_DIR, "AppIcon.png"), "wb") as f:
        f.write(images[0][1])


if __name__ == "__main__":
    main()
