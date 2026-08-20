#!/usr/bin/env python3
"""Generates src/ScreenSwitch/Resources/app.ico.

The icon is a white two-way arrow on a blue rounded square: the arrow reads as "send this
across" at every size, including the 16px the notification area actually uses. Each size is
drawn at 4x and downsampled so the diagonals come out smooth.

Usage: python3 tools/make_icon.py
"""

from pathlib import Path

from PIL import Image, ImageDraw

SIZES = [16, 20, 24, 32, 48, 64, 128, 256]
SUPERSAMPLE = 4

BACKGROUND_TOP = (37, 99, 235)     # blue-600
BACKGROUND_BOTTOM = (14, 165, 233)  # sky-500
FOREGROUND = (255, 255, 255, 255)


def vertical_gradient(size, top, bottom):
    """A square filled with a vertical top->bottom colour ramp."""
    image = Image.new("RGB", (1, size))
    for y in range(size):
        blend = y / max(1, size - 1)
        image.putpixel((0, y), tuple(
            round(top[channel] + (bottom[channel] - top[channel]) * blend)
            for channel in range(3)
        ))
    return image.resize((size, size), Image.NEAREST)


def rounded_mask(size, radius):
    mask = Image.new("L", (size, size), 0)
    ImageDraw.Draw(mask).rounded_rectangle([0, 0, size - 1, size - 1], radius=radius, fill=255)
    return mask


def draw_arrow(draw, size, y_center, thickness, tail, head, pointing_right):
    """One arrow: a rounded shaft from `tail` to `head` plus a triangular head at `head`."""
    half = thickness / 2
    head_half = thickness * 1.35
    head_length = thickness * 1.5

    shaft_end = head - head_length if pointing_right else head + head_length
    draw.rounded_rectangle(
        [min(tail, shaft_end), y_center - half, max(tail, shaft_end), y_center + half],
        radius=half,
        fill=FOREGROUND,
    )
    draw.polygon(
        [
            (head, y_center),
            (shaft_end, y_center - head_half),
            (shaft_end, y_center + head_half),
        ],
        fill=FOREGROUND,
    )


def render(size):
    canvas = size * SUPERSAMPLE

    background = vertical_gradient(canvas, BACKGROUND_TOP, BACKGROUND_BOTTOM).convert("RGBA")
    background.putalpha(rounded_mask(canvas, radius=round(canvas * 0.22)))

    layer = Image.new("RGBA", (canvas, canvas), (0, 0, 0, 0))
    draw = ImageDraw.Draw(layer)

    thickness = canvas * 0.095
    draw_arrow(draw, canvas, canvas * 0.37, thickness, canvas * 0.22, canvas * 0.80, pointing_right=True)
    draw_arrow(draw, canvas, canvas * 0.63, thickness, canvas * 0.78, canvas * 0.20, pointing_right=False)

    return Image.alpha_composite(background, layer).resize((size, size), Image.LANCZOS)


def main():
    target = Path(__file__).resolve().parent.parent / "src" / "ScreenSwitch" / "Resources" / "app.ico"
    target.parent.mkdir(parents=True, exist_ok=True)

    frames = [render(size) for size in SIZES]
    frames[-1].save(target, format="ICO", sizes=[(size, size) for size in SIZES])
    print(f"wrote {target} ({target.stat().st_size} bytes)")


if __name__ == "__main__":
    main()
