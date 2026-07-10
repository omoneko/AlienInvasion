# -*- coding: utf-8 -*-
"""Alien Invasion の Steam プレビュー画像を、原発Mod(NuclearMeltdown)のスタイルに寄せて生成する。
2パネル構成: 上=UFO(母船)+大タイトル / 黄黒ハザード帯 / 下=トライポッドの市街地襲撃バナー。"""
import os
from PIL import Image, ImageDraw, ImageFont, ImageFilter

WS = r"C:\Users\omone\Desktop\G\エイリアン襲来プロジェクト\docs\workshop"
HERO = os.path.join(WS, "cover-ufo.png")        # 上パネル: UFO母船
BANNER = os.path.join(WS, "cover-tripods.png")  # 下パネル: トライポッド襲撃
OUT = os.path.join(WS, "preview.png")

W = H = 1024
HERO_H = 616          # 上パネル(UFO)の高さ
BAND_H = 30           # ハザード帯

def load_font(names, size):
    for n in names:
        p = os.path.join(r"C:\Windows\Fonts", n)
        if os.path.exists(p):
            return ImageFont.truetype(p, size)
    return ImageFont.load_default()

f_title = load_font(["ariblk.ttf", "impact.ttf", "arialbd.ttf"], 106)
f_small = load_font(["arialbd.ttf", "arial.ttf"], 34)
f_chip = load_font(["arialbd.ttf", "arial.ttf"], 28)

def cover(img, w, h, bias="center"):
    """w×hをカバーするよう拡大し、bias('center'|'top')でクロップ。"""
    iw, ih = img.size
    scale = max(w / iw, h / ih)
    nw, nh = int(iw * scale + 0.5), int(ih * scale + 0.5)
    r = img.resize((nw, nh), Image.LANCZOS)
    left = (nw - w) // 2
    top = 0 if bias == "top" else (nh - h) // 2
    return r.crop((left, top, left + w, top + h))

canvas = Image.new("RGB", (W, H), (8, 8, 12))

# --- 上パネル: UFO母船 ---
hero = cover(Image.open(HERO).convert("RGB"), W, HERO_H, bias="center")
canvas.paste(hero, (0, 0))

# タイトル可読性のための暗色グラデーション(左下を暗く)
ov = Image.new("RGBA", (W, HERO_H), (0, 0, 0, 0))
od = ImageDraw.Draw(ov)
for y in range(HERO_H):
    a = int(160 * max(0.0, (y - HERO_H * 0.40) / (HERO_H * 0.60)))
    od.line([(0, y), (W, y)], fill=(3, 4, 10, min(a, 175)))
for x in range(W):
    a = int(130 * max(0.0, (W * 0.6 - x) / (W * 0.6)))
    od.line([(x, int(HERO_H * 0.38)), (x, HERO_H)], fill=(3, 4, 10, min(a, 140)))
canvas.paste(Image.alpha_composite(canvas.crop((0, 0, W, HERO_H)).convert("RGBA"), ov).convert("RGB"), (0, 0))

# --- 下パネル: トライポッド襲撃バナー ---
banner = cover(Image.open(BANNER).convert("RGB"), W, H - (HERO_H + BAND_H), bias="top")
canvas.paste(banner, (0, HERO_H + BAND_H))

# --- ハザード帯(黄×黒の斜めストライプ) ---
band = Image.new("RGB", (W, BAND_H), (242, 194, 0))
bd = ImageDraw.Draw(band)
sw = 34
for i in range(-BAND_H, W + BAND_H, sw * 2):
    bd.polygon([(i, BAND_H), (i + sw, BAND_H), (i + sw + BAND_H, 0), (i + BAND_H, 0)], fill=(15, 15, 18))
canvas.paste(band, (0, HERO_H))

draw = ImageDraw.Draw(canvas)

# --- UFOアイコン ---
icx, icy = 100, 496
glow = Image.new("RGBA", (W, H), (0, 0, 0, 0))
gd = ImageDraw.Draw(glow)
gd.ellipse([icx-40, icy-30, icx+40, icy+30], fill=(255, 50, 30, 120))
glow = glow.filter(ImageFilter.GaussianBlur(14))
canvas = Image.alpha_composite(canvas.convert("RGBA"), glow).convert("RGB")
draw = ImageDraw.Draw(canvas)
draw.ellipse([icx-40, icy-42, icx+40, icy+8],  fill=(70, 72, 84), outline=(20, 20, 26), width=3)
draw.ellipse([icx-92, icy-16, icx+92, icy+22], fill=(44, 46, 56), outline=(16, 16, 22), width=4)
draw.ellipse([icx-14, icy-8, icx+14, icy+8], fill=(255, 70, 45), outline=(255, 200, 60), width=3)
draw.ellipse([icx-60, icy-3, icx-44, icy+7], fill=(210, 40, 30))
draw.ellipse([icx+44, icy-3, icx+60, icy+7], fill=(210, 40, 30))

# --- タイトル文字 ---
tx = 205
GREEN = (128, 236, 58)
draw.text((tx+2, 352), "Cities: Skylines Mod", font=f_small, fill=(235, 235, 240),
          stroke_width=3, stroke_fill=(0, 0, 0))
draw.text((tx, 386), "ALIEN", font=f_title, fill=(255, 255, 255),
          stroke_width=6, stroke_fill=(0, 0, 0))
draw.text((tx, 492), "INVASION", font=f_title, fill=GREEN,
          stroke_width=6, stroke_fill=(0, 0, 0))

# --- 下バナーのラベルチップ ---
chip = "TRIPOD ASSAULT"
cw = draw.textlength(chip, font=f_chip)
cx, cy = 18, H - 46
draw.rectangle([cx, cy, cx + cw + 24, cy + 36], fill=(10, 10, 12))
draw.rectangle([cx, cy, cx + 6, cy + 36], fill=(242, 194, 0))
draw.text((cx + 16, cy + 5), chip, font=f_chip, fill=(245, 245, 245))

canvas.save(OUT, "PNG")
print("saved", os.path.basename(OUT), canvas.size)
