# -*- coding: utf-8 -*-
"""Alien Invasion の Steam プレビュー画像を、原発Mod(NuclearMeltdown)のスタイルに寄せて生成する。
構成: ヒーロー画像(夜のトライポッド) + 大タイトル + UFOアイコン + ハザード帯 + ラベル付きサムネ列。"""
import os
from PIL import Image, ImageDraw, ImageFont, ImageFilter

WS = r"C:\Users\omone\Desktop\G\エイリアン襲来プロジェクト\docs\workshop"
HERO = os.path.join(WS, "screenshot-2-night-tripod.png")
THUMBS = [
    (os.path.join(WS, "screenshot-1-mothership.png"),    "1 UFO"),
    (os.path.join(WS, "screenshot-3-tripod-highway.png"), "2 TRIPODS"),
    (os.path.join(WS, "screenshot-4-street-attack.png"),  "3 LASERS"),
    (os.path.join(WS, "screenshot-2-night-tripod.png"),   "4 NIGHT"),
]
OUT = os.path.join(WS, "preview.png")

W = H = 1024
FILM_TOP = 764           # サムネ列の上端
DIVIDER_H = 30           # ハザード帯の高さ

def load_font(names, size):
    for n in names:
        p = os.path.join(r"C:\Windows\Fonts", n)
        if os.path.exists(p):
            return ImageFont.truetype(p, size)
    return ImageFont.load_default()

f_title = load_font(["ariblk.ttf", "impact.ttf", "arialbd.ttf"], 106)
f_small = load_font(["arialbd.ttf", "arial.ttf"], 34)
f_label = load_font(["arialbd.ttf", "arial.ttf"], 26)

def cover_top(img, w, h, zoom=1.0):
    """w×hをカバーするよう拡大し、上端を保持・水平中央でクロップ(下部のHUD文字を切り落とす)。"""
    iw, ih = img.size
    scale = max(w / iw, h / ih) * zoom
    nw, nh = int(iw * scale + 0.5), int(ih * scale + 0.5)
    r = img.resize((nw, nh), Image.LANCZOS)
    left = (nw - w) // 2
    return r.crop((left, 0, left + w, h))

def cover_center(img, w, h):
    iw, ih = img.size
    scale = max(w / iw, h / ih)
    nw, nh = int(iw * scale + 0.5), int(ih * scale + 0.5)
    r = img.resize((nw, nh), Image.LANCZOS)
    left = (nw - w) // 2
    top = (nh - h) // 2
    return r.crop((left, top, left + w, top + h))

canvas = Image.new("RGB", (W, H), (8, 8, 12))

# --- ヒーロー画像(上部) ---
hero_h = FILM_TOP - DIVIDER_H  # 帯の上まで
hero = cover_top(Image.open(HERO).convert("RGB"), W, hero_h, zoom=1.12)
canvas.paste(hero, (0, 0))

# --- タイトル可読性のための暗色グラデーション(左〜下を暗く) ---
ov = Image.new("RGBA", (W, hero_h), (0, 0, 0, 0))
od = ImageDraw.Draw(ov)
for y in range(hero_h):
    # 下ほど暗く
    a = int(150 * max(0.0, (y - hero_h * 0.42) / (hero_h * 0.58)))
    od.line([(0, y), (W, y)], fill=(3, 4, 10, min(a, 165)))
for x in range(W):
    # 左ほど暗く(タイトルは左寄せ)
    a = int(120 * max(0.0, (W * 0.55 - x) / (W * 0.55)))
    od.line([(x, int(hero_h * 0.40)), (x, hero_h)], fill=(3, 4, 10, min(a, 130)))
canvas.paste(Image.alpha_composite(canvas.crop((0, 0, W, hero_h)).convert("RGBA"), ov).convert("RGB"), (0, 0))

draw = ImageDraw.Draw(canvas)

# --- UFOアイコン(原発の放射能マークの位置に相当) ---
icx, icy = 100, 600
glow = Image.new("RGBA", (W, H), (0, 0, 0, 0))
gd = ImageDraw.Draw(glow)
gd.ellipse([icx-40, icy-30, icx+40, icy+30], fill=(255, 50, 30, 120))   # 赤い発光
glow = glow.filter(ImageFilter.GaussianBlur(14))
canvas.paste(Image.alpha_composite(canvas.convert("RGBA"), glow).convert("RGB"), (0, 0))
draw = ImageDraw.Draw(canvas)
# ドーム(上) → 円盤(下)
draw.ellipse([icx-40, icy-42, icx+40, icy+8],  fill=(70, 72, 84), outline=(20, 20, 26), width=3)
draw.ellipse([icx-92, icy-16, icx+92, icy+22], fill=(44, 46, 56), outline=(16, 16, 22), width=4)
# 中央の赤い光と黄色いリングの示唆
draw.ellipse([icx-14, icy-8, icx+14, icy+8], fill=(255, 70, 45), outline=(255, 200, 60), width=3)
draw.ellipse([icx-60, icy-3, icx-44, icy+7], fill=(210, 40, 30))
draw.ellipse([icx+44, icy-3, icx+60, icy+7], fill=(210, 40, 30))

# --- タイトル文字 ---
tx = 205
GREEN = (128, 236, 58)
draw.text((tx+2, 462), "Cities: Skylines Mod", font=f_small, fill=(235, 235, 240),
          stroke_width=3, stroke_fill=(0, 0, 0))
draw.text((tx, 496), "ALIEN", font=f_title, fill=(255, 255, 255),
          stroke_width=6, stroke_fill=(0, 0, 0))
draw.text((tx, 602), "INVASION", font=f_title, fill=GREEN,
          stroke_width=6, stroke_fill=(0, 0, 0))

# --- ハザード帯(黄×黒の斜めストライプ) ---
dy0 = hero_h
band = Image.new("RGB", (W, DIVIDER_H), (242, 194, 0))
bd = ImageDraw.Draw(band)
sw = 34
for i in range(-DIVIDER_H, W + DIVIDER_H, sw * 2):
    bd.polygon([(i, DIVIDER_H), (i + sw, DIVIDER_H), (i + sw + DIVIDER_H, 0), (i + DIVIDER_H, 0)],
               fill=(15, 15, 18))
canvas.paste(band, (0, dy0))

# --- サムネ列(ラベル付き) ---
pad, gap = 12, 10
n = len(THUMBS)
tw = (W - 2 * pad - (n - 1) * gap) // n
th = H - FILM_TOP - 10
ty = FILM_TOP + 2
for i, (path, label) in enumerate(THUMBS):
    x = pad + i * (tw + gap)
    cell = cover_center(Image.open(path).convert("RGB"), tw, th)
    canvas.paste(cell, (x, ty))
    draw.rectangle([x, ty, x + tw - 1, ty + th - 1], outline=(230, 230, 235), width=2)
    # 下部のラベル黒帯
    lb_h = 34
    draw.rectangle([x, ty + th - lb_h, x + tw, ty + th], fill=(10, 10, 12))
    wlab = draw.textlength(label, font=f_label)
    draw.text((x + (tw - wlab) / 2, ty + th - lb_h + 4), label, font=f_label, fill=(245, 245, 245))

canvas.save(OUT, "PNG")
print("saved", OUT, canvas.size)
