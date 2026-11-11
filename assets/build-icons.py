"""Build Windows icon and MSIX tile images from the Figma export."""

from pathlib import Path

from PIL import Image


assets = Path(__file__).resolve().parent
dark_mark = Image.open(assets / "rintas-mark.png").convert("RGBA")
light_mark = Image.open(assets / "rintas-mark-1.png").convert("RGBA")
output = assets / "generated"
output.mkdir(exist_ok=True)


def pixel_size(mark: Image.Image, size: int) -> Image.Image:
    return mark.resize((size, size), Image.Resampling.NEAREST)


for size, filename in (
    (44, "Square44x44Logo.png"),
    (50, "StoreLogo.png"),
    (150, "Square150x150Logo.png"),
):
    pixel_size(dark_mark, size).save(output / filename)

icon_sizes = (16, 24, 32, 48, 64, 128, 256)
for mark, filename in (
    (dark_mark, "RintasDark.ico"),
    (light_mark, "RintasLight.ico"),
    (light_mark, "Rintas.ico"),
):
    frames = [pixel_size(mark, size) for size in icon_sizes]
    frames[-1].save(
        output / filename,
        format="ICO",
        sizes=[(size, size) for size in icon_sizes],
        append_images=frames[:-1],
        bitmap_format="bmp",
    )

print(f"Ikon Rintas siap di {output}")
