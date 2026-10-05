# Draws workshop/preview.png: a title card with a mock of the PDA panel, in the PDA's colours.
# Placeholder until there's an in-game screenshot; the Workshop needs preview.png < 1 MB.
# Needs Pillow:  python3 make_preview.py
from PIL import Image, ImageDraw, ImageFont

W, H = 1024, 576
BG, PANEL = (8, 16, 20), (12, 30, 36)
CYAN = (32, 168, 186)          # lblFilter / checkbox text colour
DIM = (20, 90, 100)
BOLD = "/usr/share/fonts/truetype/dejavu/DejaVuSansCondensed-Bold.ttf"
REG = "/usr/share/fonts/truetype/dejavu/DejaVuSansCondensed.ttf"
f = lambda path, size: ImageFont.truetype(path, size)

im = Image.new("RGB", (W, H), BG)
d = ImageDraw.Draw(im)
for y in range(0, H, 4):                       # faint scanlines
    d.line([(0, y), (W, y)], fill=(10, 20, 25))

d.text((56, 70), "ENHANCED", font=f(BOLD, 78), fill=CYAN)
d.text((56, 150), "ORDERS", font=f(BOLD, 78), fill=CYAN)
d.text((60, 258), "Pick an area. Tick what you want.", font=f(REG, 28), fill=CYAN)
d.text((60, 296), "Confirm. Nothing else gets queued.", font=f(REG, 28), fill=CYAN)
for i, tag in enumerate(["UNINSTALL", "REPAIR", "HAUL"]):
    x = 60 + i * 150
    d.rectangle([x, 370, x + 136, 408], outline=CYAN, width=2, fill=PANEL)
    d.text((x + 68, 389), tag, font=f(BOLD, 20), fill=CYAN, anchor="mm")
d.text((60, 440), "+ saved selections  ·  install-menu groups", font=f(REG, 22), fill=DIM)
d.text((60, 470), "+ repair condition thresholds", font=f(REG, 22), fill=DIM)

# Mock PDA panel
x0, y0, x1, y1 = 600, 60, 970, 516
d.rectangle([x0, y0, x1, y1], fill=PANEL, outline=DIM, width=2)
def box(x, y, on):
    d.rectangle([x, y, x + 20, y + 20], outline=CYAN, width=2, fill=CYAN if on else PANEL)
rows = [("18 object(s) ready to uninstall", None, 0),
        ("Pick area", True, 0),
        ("HULL", None, 0), ("Floor Tile  x12", True, 1), ("Wall  x8", False, 1),
        ("POWR", None, 0), ("Conduit  x6", True, 1),
        ("FURN", None, 0), ("Bunk  x2", False, 1)]
y = y0 + 22
for text, on, indent in rows:
    x = x0 + 20 + indent * 28
    if on is None and text.isupper():
        box(x, y, False); d.text((x + 32, y - 2), text, font=f(BOLD, 21), fill=CYAN)
    elif on is None:
        d.text((x, y - 2), text, font=f(REG, 21), fill=CYAN)
    else:
        box(x, y, on); d.text((x + 32, y - 2), text, font=f(REG, 21), fill=CYAN)
    y += 40
d.rectangle([x0 + 20, y1 - 62, x1 - 110, y1 - 22], outline=CYAN, width=2, fill=(18, 52, 60))
d.text(((x0 + 20 + x1 - 110) // 2, y1 - 42), "UNINSTALL 18 SELECTED", font=f(BOLD, 18), fill=CYAN, anchor="mm")
d.rectangle([x1 - 100, y1 - 62, x1 - 20, y1 - 22], outline=CYAN, width=2, fill=PANEL)
d.text((x1 - 60, y1 - 42), "CANCEL", font=f(BOLD, 18), fill=CYAN, anchor="mm")

im.save("preview.png", optimize=True)
print("preview.png", im.size)
