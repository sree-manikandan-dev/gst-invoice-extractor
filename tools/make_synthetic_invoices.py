"""Generate synthetic GST invoice images with known values, for the eval set.

Each invoice is written as <name>.png plus <name>.json (the ground-truth label).
Some are deliberately degraded (rotation, blur, noise) so the eval is not too easy.

Usage:  pip install pillow  &&  python tools/make_synthetic_invoices.py
"""
import json
import random
from pathlib import Path

from PIL import Image, ImageDraw, ImageFilter, ImageFont

OUT = Path(__file__).resolve().parent.parent / "eval-data" / "synthetic"
CHARSET = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ"
FONT = "/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf"
FONT_BOLD = "/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf"


def gstin(state: int, pan: str) -> str:
    """Build a GSTIN with a correct mod-36 check character."""
    body = f"{state:02d}{pan}1Z"
    total = 0
    for i, ch in enumerate(body):
        product = CHARSET.index(ch) * (1 if i % 2 == 0 else 2)
        total += product // 36 + product % 36
    return body + CHARSET[(36 - total % 36) % 36]


SUPPLIERS = [
    ("Malabar Hardware Stores", 32, "AAKFM4521K"),
    ("Kochi Medicals Pvt Ltd", 32, "AACCK8812P"),
    ("Sree Durga Textiles", 32, "BBQPS5612H"),
    ("Chennai Office Supplies", 33, "AADFS7788Q"),
    ("Bengaluru Tech Traders", 29, "AABCT1332L"),
    ("Trivandrum Fuels", 32, "AAGFT2290M"),
]
BUYER = ("Your Company", 32, "ABCDE1234F")
RATES = [5, 12, 18, 28]


def font(size, bold=False):
    return ImageFont.truetype(FONT_BOLD if bold else FONT, size)


def make(index: int, rng: random.Random):
    name, s_state, s_pan = rng.choice(SUPPLIERS)
    supplier_gstin = gstin(s_state, s_pan)
    buyer_gstin = gstin(BUYER[1], BUYER[2])
    rate = rng.choice(RATES)
    taxable = round(rng.uniform(500, 50000), 2)
    tax = round(taxable * rate / 100, 2)
    intra = s_state == BUYER[1]
    cgst = sgst = round(tax / 2, 2) if intra else 0.0
    igst = 0.0 if intra else tax
    total = round(taxable + cgst + sgst + igst, 2)
    day, month = rng.randint(1, 28), rng.randint(1, 10)
    inv_no = f"{''.join(w[0] for w in name.split()[:2]).upper()}/26-27/{rng.randint(1, 9999):04d}"

    img = Image.new("RGB", (1240, 1754), "white")
    d = ImageDraw.Draw(img)
    y = 60
    d.text((60, y), name, font=font(40, True), fill="black"); y += 60
    d.text((60, y), f"GSTIN: {supplier_gstin}", font=font(26), fill="black"); y += 50
    d.text((60, y), "TAX INVOICE", font=font(34, True), fill="black"); y += 70
    d.text((60, y), f"Invoice No: {inv_no}", font=font(26), fill="black")
    d.text((720, y), f"Date: {day:02d}/{month:02d}/2026", font=font(26), fill="black"); y += 60
    d.text((60, y), f"Bill to: {BUYER[0]}", font=font(26), fill="black"); y += 40
    d.text((60, y), f"GSTIN: {buyer_gstin}", font=font(26), fill="black"); y += 80

    d.line((60, y, 1180, y), fill="black", width=2); y += 20
    d.text((60, y), "Description", font=font(24, True), fill="black")
    d.text((900, y), "Amount (Rs)", font=font(24, True), fill="black"); y += 40
    d.text((60, y), f"Goods @ {rate}% GST", font=font(24), fill="black")
    d.text((900, y), f"{taxable:,.2f}", font=font(24), fill="black"); y += 60
    d.line((60, y, 1180, y), fill="black", width=1); y += 20

    rows = [("Taxable Value", taxable)]
    rows += [(f"CGST @ {rate / 2:g}%", cgst), (f"SGST @ {rate / 2:g}%", sgst)] if intra else [(f"IGST @ {rate}%", igst)]
    for label, value in rows:
        d.text((560, y), label, font=font(24), fill="black")
        d.text((900, y), f"{value:,.2f}", font=font(24), fill="black"); y += 40
    d.text((560, y + 10), "Grand Total", font=font(28, True), fill="black")
    d.text((900, y + 10), f"{total:,.2f}", font=font(28, True), fill="black")

    # Degrade some images so the eval reflects messy real-world photos.
    if index % 3 == 1:
        img = img.rotate(rng.uniform(-4, 4), expand=True, fillcolor="white").filter(ImageFilter.GaussianBlur(1.2))
    if index % 3 == 2:
        img = img.resize((620, 877)).filter(ImageFilter.GaussianBlur(0.8))

    label = {
        "SupplierName": name, "SupplierGstin": supplier_gstin, "BuyerGstin": buyer_gstin,
        "InvoiceNumber": inv_no, "InvoiceDate": f"2026-{month:02d}-{day:02d}",
        "TaxableValue": taxable, "Cgst": cgst, "Sgst": sgst, "Igst": igst, "Total": total,
    }
    stem = f"synthetic-{index:02d}"
    img.convert("L").save(OUT / f"{stem}.png", optimize=True)
    (OUT / f"{stem}.json").write_text(json.dumps(label, indent=2) + "\n")


if __name__ == "__main__":
    OUT.mkdir(parents=True, exist_ok=True)
    rng = random.Random(42)  # fixed seed: same invoices every run
    for i in range(1, 11):
        make(i, rng)
    print(f"Wrote 10 invoices to {OUT}")
