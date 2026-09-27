# -*- coding: utf-8 -*-
"""收官批图标：3 件专属遗物（256px，atlas+outline+大图）+ 2 个缺失 power 图标（64px）。
配色沿用雪豹口径：雪白 / 冰蓝阴影 / 深藏青描边（扁平矢量、硬边着色）。"""
from PIL import Image, ImageDraw
import os

ROOT = r"d:\sts2mod\lingdawnpao"
IMG = os.path.join(ROOT, "images")
WHITE = (240, 244, 250, 255)
ICE = (140, 190, 225, 255)
ICE_DARK = (70, 120, 160, 255)
NAVY = (24, 44, 66, 255)
CREAM = (232, 220, 198, 255)


def save(im, rel):
    path = os.path.join(IMG, rel)
    os.makedirs(os.path.dirname(path), exist_ok=True)
    im.save(path)
    print("gen:", rel)


def make_relic(name, painter):
    size = 256
    im = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    painter(ImageDraw.Draw(im), size)
    save(im, f"atlases/relic_atlas.sprites/{name}.png")
    save(im.copy(), f"atlases/relic_outline_atlas.sprites/{name}.png")
    save(im.copy(), f"relics/{name}.png")
    save(im.copy(), f"packed/relics/{name}.png")
    for folder in ("relic_atlas", "relic_outline_atlas"):
        tres = (
            '[gd_resource type="AtlasTexture" load_steps=2 format=3]\n\n'
            f'[ext_resource type="Texture2D" path="res://images/atlases/{folder}.sprites/{name}.png" id="1"]\n\n'
            "[resource]\natlas = ExtResource(\"1\")\n"
            f"region = Rect2(0, 0, {size}, {size})\n"
        )
        with open(os.path.join(IMG, "atlases", folder + ".sprites", name + ".tres"), "w", encoding="utf-8") as f:
            f.write(tres)
    print("gen: relic", name)


def make_power(name, painter):
    size = 64
    im = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    painter(ImageDraw.Draw(im), size)
    save(im, f"atlases/power_atlas.sprites/{name}.png")
    tres = (
        '[gd_resource type="AtlasTexture" load_steps=2 format=3]\n\n'
        f'[ext_resource type="Texture2D" path="res://images/atlases/power_atlas.sprites/{name}.png" id="1"]\n\n'
        "[resource]\natlas = ExtResource(\"1\")\n"
        f"region = Rect2(0, 0, {size}, {size})\n"
    )
    with open(os.path.join(IMG, "atlases/power_atlas.sprites", name + ".tres"), "w", encoding="utf-8") as f:
        f.write(tres)
    print("gen: power", name)


def paint_cozy_pelt(d, s):
    """暖绒：一块叠起的绒毯（三层圆角毯 + 雪点）。"""
    w = 16
    # 底层毯
    d.rounded_rectangle([40, 130, 216, 200], radius=34, fill=ICE, outline=NAVY, width=w)
    # 中层
    d.rounded_rectangle([56, 84, 200, 148], radius=32, fill=WHITE, outline=NAVY, width=w)
    # 顶层卷边
    d.rounded_rectangle([72, 46, 184, 100], radius=28, fill=CREAM, outline=NAVY, width=w)
    # 雪点
    for cx, cy in [(96, 160), (128, 168), (160, 158)]:
        d.ellipse([cx - 8, cy - 8, cx + 8, cy + 8], fill=WHITE, outline=NAVY, width=4)


def paint_frostbell(d, s):
    """霜铃：铃身（冰蓝）+ 白色雪粒 + 顶部挂环。"""
    w = 14
    d.ellipse([108, 28, 148, 60], outline=NAVY, width=w)  # 挂环
    d.polygon([(70, 190), (186, 190), (166, 92), (90, 92)], fill=ICE, outline=NAVY, width=w)  # 铃身
    d.rounded_rectangle([62, 178, 194, 204], radius=12, fill=WHITE, outline=NAVY, width=10)  # 铃口
    d.ellipse([116, 196, 140, 220], fill=ICE, outline=NAVY, width=8)  # 铃舌
    # 高光与雪粒
    d.line([104, 108, 96, 160], fill=WHITE, width=10)
    for cx, cy in [(196, 70), (56, 84), (206, 120)]:
        d.ellipse([cx - 6, cy - 6, cx + 6, cy + 6], fill=WHITE)


def paint_snowball_pouch(d, s):
    """雪球袋：束口布袋 + 露出的雪球。"""
    w = 14
    d.polygon([(64, 96), (192, 96), (222, 200), (34, 200)], fill=ICE_DARK, outline=NAVY, width=w)  # 袋身
    d.rounded_rectangle([88, 60, 168, 104], radius=16, fill=ICE, outline=NAVY, width=w)  # 束口
    d.ellipse([94, 28, 162, 92], fill=WHITE, outline=NAVY, width=w)  # 雪球
    d.arc([110, 44, 146, 78], 200, 340, fill=ICE, width=8)  # 雪球纹理
    d.line([128, 96, 128, 60], fill=NAVY, width=8)  # 束绳


def paint_swarm_grant(d, s):
    """蜂群伏击授能：三道汇聚的爪痕（三斜线 + 汇聚箭头感）。"""
    w = 9
    for i, x in enumerate((14, 30, 46)):
        d.line([x, 52, x + 10, 12], fill=WHITE if i != 1 else ICE, width=w)
    d.polygon([(20, 52), (36, 52), (28, 40)], fill=ICE)


def paint_vape(d, s):
    """电子烟：烟杆 + 呼出云雾。"""
    w = 7
    d.rounded_rectangle([22, 26, 34, 54], radius=5, fill=ICE, outline=NAVY, width=w)
    d.rounded_rectangle([24, 12, 32, 26], radius=3, fill=ICE_DARK, outline=NAVY, width=4)
    d.ellipse([34, 26, 56, 44], fill=WHITE, outline=NAVY, width=5)  # 云雾
    d.ellipse([46, 34, 62, 50], fill=WHITE, outline=NAVY, width=5)


if __name__ == "__main__":
    make_relic("cozy_pelt", paint_cozy_pelt)
    make_relic("frostbell", paint_frostbell)
    make_relic("snowball_pouch", paint_snowball_pouch)
    make_power("swarm_ambush_grant_power", paint_swarm_grant)
    make_power("vape_power", paint_vape)
    print("done.")
