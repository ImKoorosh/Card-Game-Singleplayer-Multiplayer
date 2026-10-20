#!/usr/bin/env python3
"""
Generates the 2D card artwork (512x512 PNG, one per card) used on the card faces in game.
Requires Pillow:  pip install pillow
Usage:            python3 make_card_art.py [output_folder]     (default: ../Resources/CardArt)
File names match the CardData asset names (Card_Vanguard.png ...), which is how the game finds them.
(The folder is named "Art~" so Unity ignores it.)
"""
import math, os, sys
from PIL import Image, ImageDraw, ImageFilter

S = 1024                      # drawn at 2x, saved at 512x512 (smooth edges)
OUT = (38, 30, 44, 255)       # outline colour
OW = 13                       # outline width


def c(r, g, b, a=255): return (r, g, b, a)
def mix(a, b, t): return tuple(int(a[i] * (1 - t) + b[i] * t) for i in range(3)) + (255,)
def light(col, t=0.35): return mix(col, (255, 255, 255), t)
def dark(col, t=0.35): return mix(col, (0, 0, 0), t)


GOLD = c(245, 200, 70); STEEL = c(178, 186, 204); WOOD = c(130, 88, 50); SKIN = c(240, 200, 165)
BLADE = c(225, 230, 242)


class Art:
    def __init__(self, inner, outer):
        grad = Image.radial_gradient("L").resize((S, S), Image.BILINEAR)   # 0 centre -> 255 edge
        self.im = Image.composite(Image.new("RGBA", (S, S), outer), Image.new("RGBA", (S, S), inner), grad)
        self.d = ImageDraw.Draw(self.im)
        self.soft(lambda d: d.ellipse((190, 880, 834, 965), fill=(0, 0, 0, 120)), blur=18)   # ground shadow

    # --- blended (semi transparent / blurred) drawing ---
    def soft(self, fn, blur=0):
        layer = Image.new("RGBA", (S, S), (0, 0, 0, 0))
        fn(ImageDraw.Draw(layer))
        if blur: layer = layer.filter(ImageFilter.GaussianBlur(blur))
        self.im = Image.alpha_composite(self.im, layer)
        self.d = ImageDraw.Draw(self.im)

    def glow(self, cx, cy, rx, ry, color, blur=40):
        self.soft(lambda d: d.ellipse((cx - rx, cy - ry, cx + rx, cy + ry), fill=color), blur)

    # --- outlined shapes ---
    def ell(self, cx, cy, rx, ry, fill, ow=OW):
        self.d.ellipse((cx - rx - ow, cy - ry - ow, cx + rx + ow, cy + ry + ow), fill=OUT)
        self.d.ellipse((cx - rx, cy - ry, cx + rx, cy + ry), fill=fill)

    def rr(self, x0, y0, x1, y1, r, fill, ow=OW):
        self.d.rounded_rectangle((x0 - ow, y0 - ow, x1 + ow, y1 + ow), r + ow, fill=OUT)
        self.d.rounded_rectangle((x0, y0, x1, y1), r, fill=fill)

    def poly(self, pts, fill, ow=OW):
        pts = [tuple(p) for p in pts]
        self.d.line(pts + [pts[0]], fill=OUT, width=2 * ow, joint="curve")
        self.d.polygon(pts, fill=fill)

    def stroke(self, pts, w, fill, ow=OW):
        pts = [tuple(p) for p in pts]
        for width, colr in ((w + 2 * ow, OUT), (w, fill)):
            self.d.line(pts, fill=colr, width=int(width), joint="curve")
            r = width / 2
            for (x, y) in (pts[0], pts[-1]):
                self.d.ellipse((x - r, y - r, x + r, y + r), fill=colr)

    def star(self, cx, cy, ro, ri, n, fill, rot=0.0, ow=OW):
        pts = []
        for i in range(n * 2):
            r = ro if i % 2 == 0 else ri
            ang = rot + math.pi * i / n
            pts.append((cx + r * math.cos(ang), cy + r * math.sin(ang)))
        self.poly(pts, fill, ow)

    def save(self, folder, name):
        self.d.rounded_rectangle((8, 8, S - 9, S - 9), 110, outline=OUT, width=16)
        im = self.im.resize((512, 512), Image.LANCZOS)
        mask = Image.new("L", (512, 512), 0)
        ImageDraw.Draw(mask).rounded_rectangle((0, 0, 511, 511), 58, fill=255)
        im.putalpha(mask)
        im.save(os.path.join(folder, name + ".png"))


class Person:
    """A front-facing cartoon humanoid. Coordinates via at(dx, dy) are relative to the feet (dy negative = up)."""
    def __init__(self, a, cx=512, gy=925, s=1.0, hw=1.0, torso=c(90, 140, 230), legs=c(60, 60, 80), skin=SKIN):
        self.a, self.cx, self.gy, self.s, self.hw = a, cx, gy, s, hw
        self.torso, self.legs, self.skin = torso, legs, skin
        self.r = 108 * s

    def at(self, dx, dy): return (self.cx + dx * self.s, self.gy + dy * self.s)

    def legs_(self):
        for sx in (-1, 1):
            x, y = self.at(sx * 62 * self.hw, -190)
            w = 40 * self.s * self.hw
            self.a.rr(x - w, y, x + w, self.gy, 26 * self.s, self.legs)
            self.a.rr(x - w - 6 * self.s, self.gy - 48 * self.s, x + w + 6 * self.s, self.gy + 6 * self.s, 20 * self.s, dark(self.legs, 0.5))

    def torso_(self):
        x0, y0 = self.at(-125 * self.hw, -470); x1, y1 = self.at(125 * self.hw, -170)
        self.a.rr(x0, y0, x1, y1, 50 * self.s, self.torso)

    def arm(self, side, hand, color=None, w=54):
        sh = self.at(side * 120 * self.hw, -430)
        self.a.stroke([sh, hand], w * self.s, color or self.torso)
        self.a.ell(hand[0], hand[1], 36 * self.s, 36 * self.s, self.skin, ow=10)

    def head(self, eyes=True, smile=True):
        hx, hy = self.at(0, -575); r = self.r
        self.a.ell(hx, hy, r, r * 1.02, self.skin)
        if eyes: self.eyes()
        if smile:
            self.a.d.arc((hx - r * 0.35, hy + r * 0.05, hx + r * 0.35, hy + r * 0.55), 20, 160, fill=OUT, width=max(6, int(9 * self.s)))

    def eyes(self, dy=-2):
        hx, hy = self.at(0, -575)
        for sx in (-1, 1):
            ex, ey = hx + sx * self.r * 0.36, hy + self.r * 0.02 + dy
            self.a.d.ellipse((ex - 14 * self.s, ey - 20 * self.s, ex + 14 * self.s, ey + 20 * self.s), fill=OUT)
            self.a.d.ellipse((ex - 5 * self.s, ey - 12 * self.s, ex + 2 * self.s, ey - 4 * self.s), fill=(255, 255, 255, 255))


# ------------------------------------------------------------------ characters

def vanguard():
    a = Art(c(130, 175, 255), c(28, 46, 110)); p = Person(a, torso=c(70, 120, 225), legs=c(72, 78, 100))
    p.legs_(); p.torso_()
    a.rr(*p.at(-125, -255), *p.at(125, -205), 10, GOLD)
    a.stroke([p.at(240, -320), p.at(335, -810)], 46, BLADE)                 # sword
    a.stroke([p.at(165, -400), p.at(305, -430)], 28, GOLD)
    p.arm(1, p.at(250, -355))
    p.arm(-1, p.at(-215, -380))
    a.poly([p.at(-345, -490), p.at(-150, -490), p.at(-150, -300), p.at(-248, -185), p.at(-345, -300)], c(150, 160, 190))   # shield
    a.poly([p.at(-315, -455), p.at(-180, -455), p.at(-180, -310), p.at(-248, -235), p.at(-315, -310)], c(70, 120, 225), ow=8)
    a.ell(*p.at(-248, -360), 38 * p.s, 38 * p.s, GOLD)
    p.head(eyes=False, smile=False)
    hx, hy = p.at(0, -575); r = p.r
    a.ell(hx, hy - 10, r * 1.12, r * 1.06, STEEL)                            # helmet
    a.rr(hx - r * 0.7, hy - 22, hx + r * 0.7, hy + 22, 14, c(30, 30, 45), ow=8)
    a.rr(hx - 14, hy - 95, hx + 14, hy + 95, 8, STEEL, ow=8)
    a.poly([(hx - 25, hy - r * 1.0), (hx + 25, hy - r * 1.65), (hx + 140, hy - r * 1.45), (hx + 75, hy - r * 0.95)], c(70, 120, 225))
    return a, "Card_Vanguard"


def sprinter():
    a = Art(c(255, 225, 120), c(150, 70, 30))
    a.soft(lambda d: [d.line([(40, y), (250 + i * 30, y)], fill=(255, 255, 255, 130), width=12) for i, y in enumerate((260, 380, 500, 620, 740))], blur=2)
    for cx in (350, 672):
        p = Person(a, cx=cx, gy=905, s=0.62, torso=c(60, 60, 78), legs=c(190, 60, 60))
        p.legs_(); p.torso_()
        a.stroke([p.at(225, -330), p.at(340, -590)], 34, BLADE)
        p.arm(1, p.at(232, -335), w=60); p.arm(-1, p.at(-200, -300), w=60)
        hx, hy = p.at(0, -575); r = p.r
        a.ell(hx, hy, r * 1.2, r * 1.14, c(45, 45, 60))
        a.ell(hx, hy + 12 * p.s, r * 0.8, r * 0.68, SKIN, ow=9)
        p.eyes(dy=6)
        a.rr(hx - r * 0.78, hy + 28 * p.s, hx + r * 0.78, hy + r * 0.66, 14, c(200, 60, 60), ow=8)    # red mask
        a.poly([(hx - r * 0.8, hy - r * 0.6), (hx, hy - r * 1.55), (hx + r * 0.8, hy - r * 0.6)], c(45, 45, 60))
    return a, "Card_Sprinter"


def ranger():
    a = Art(c(150, 235, 160), c(25, 80, 50)); p = Person(a, torso=c(70, 140, 80), legs=c(105, 80, 52))
    a.rr(*p.at(-200, -540), *p.at(-105, -250), 18, c(110, 75, 45))                      # quiver
    for i in range(3):
        a.stroke([p.at(-180 + i * 34, -540), p.at(-170 + i * 34, -640)], 12, WOOD, ow=7)
        a.ell(*p.at(-170 + i * 34, -650), 14, 20, c(220, 70, 70), ow=6)
    p.legs_(); p.torso_()
    bow = [p.at(270 + 95 * (1 - t * t), -470 + t * 320) for t in [i / 7 - 1 for i in range(15)]]
    a.stroke(bow, 26, WOOD)
    a.stroke([p.at(270, -790), p.at(120, -470), p.at(270, -150)], 6, (245, 245, 235), ow=4)
    a.stroke([p.at(120, -470), p.at(440, -470)], 14, WOOD, ow=8)
    a.poly([p.at(430, -505), p.at(500, -470), p.at(430, -435)], STEEL, ow=8)
    p.arm(1, p.at(365, -470)); p.arm(-1, p.at(120, -470))
    p.head()
    hx, hy = p.at(0, -575); r = p.r
    a.soft(lambda d: None)
    a.poly([(hx - r * 1.05, hy + r * 0.2), (hx - r * 0.95, hy - r * 0.6), (hx, hy - r * 1.6), (hx + r * 0.95, hy - r * 0.6), (hx + r * 1.05, hy + r * 0.2),
            (hx + r * 0.72, hy - r * 0.15), (hx + r * 0.6, hy - r * 0.7), (hx - r * 0.6, hy - r * 0.7), (hx - r * 0.72, hy - r * 0.15)], c(55, 120, 70))
    return a, "Card_Ranger"


def bulwark():
    a = Art(c(175, 185, 205), c(45, 55, 80)); stone = c(132, 134, 146); ds = dark(stone, 0.25)
    for sx in (-1, 1):
        a.stroke([(512 + sx * 255, 470), (512 + sx * 335, 690)], 150, stone)
        a.rr(512 + sx * 135 - 100, 700, 512 + sx * 135 + 100, 930, 36, ds)
    a.rr(262, 330, 762, 770, 64, stone)
    for sx in (-1, 1): a.ell(512 + sx * 340, 770, 105, 95, ds)
    a.glow(512, 530, 190, 190, (90, 200, 255, 190), blur=45)
    a.ell(512, 530, 72, 72, c(130, 230, 255)); a.ell(512, 530, 34, 34, c(235, 252, 255), ow=5)
    a.rr(400, 175, 624, 360, 44, stone)
    for sx in (-1, 1):
        a.glow(512 + sx * 55, 268, 40, 30, (90, 220, 255, 230), blur=12)
        a.d.ellipse((512 + sx * 55 - 26, 268 - 18, 512 + sx * 55 + 26, 268 + 18), fill=c(140, 235, 255))
    a.d.line([(300, 400), (350, 470), (320, 540)], fill=OUT, width=10); a.d.line([(720, 420), (670, 500), (700, 590)], fill=OUT, width=10)
    a.ell(330, 368, 62, 30, c(86, 158, 76), ow=8); a.ell(690, 352, 48, 24, c(86, 158, 76), ow=8); a.ell(440, 172, 40, 18, c(86, 158, 76), ow=7)
    return a, "Card_Bulwark"


def wisp():
    a = Art(c(200, 245, 255), c(40, 100, 170))
    a.glow(512, 520, 330, 290, (120, 235, 255, 170), blur=75)
    for sx in (-1, 1):
        a.poly([(512 + sx * 100, 470), (512 + sx * 400, 250), (512 + sx * 455, 375), (512 + sx * 435, 470), (512 + sx * 380, 560), (512 + sx * 300, 575), (512 + sx * 140, 560)], c(236, 249, 255))
        for k in range(3):
            a.d.line([(512 + sx * 160, 500 + k * 18), (512 + sx * (390 - k * 45), 335 + k * 75)], fill=c(175, 212, 235), width=9)
    a.poly([(440, 640), (584, 640), (540, 760), (512, 880), (490, 760)], c(130, 228, 250))
    a.ell(512, 520, 155, 152, c(112, 228, 252))
    a.glow(470, 470, 70, 50, (255, 255, 255, 170), blur=25)
    for sx in (-1, 1):
        a.ell(512 + sx * 58, 505, 26, 34, OUT, ow=2); a.d.ellipse((512 + sx * 58 - 10, 488, 512 + sx * 58 + 4, 506), fill=(255, 255, 255, 255))
    a.d.arc((462, 530, 562, 600), 20, 160, fill=OUT, width=10)
    a.soft(lambda d: [d.arc((60 + k * 30, 640 + k * 25, 520, 900), 160, 280, fill=(255, 255, 255, 170), width=12) for k in range(2)] +
           [d.arc((500, 640 + k * 25, 964 - k * 30, 900), 260, 20, fill=(255, 255, 255, 170), width=12) for k in range(2)], blur=1)
    return a, "Card_GaleWisp"


def emberseer():
    a = Art(c(255, 170, 90), c(60, 20, 90)); p = Person(a, torso=c(122, 62, 172), skin=SKIN)
    robe = c(122, 62, 172)
    a.stroke([p.at(290, -150), p.at(310, -650)], 28, WOOD)
    ox, oy = p.at(310, -705)
    a.glow(ox, oy, 170, 170, (255, 140, 40, 200), blur=50)
    for dx, h in ((-40, 95), (0, 135), (40, 85)):
        a.poly([(ox + dx - 26, oy - 20), (ox + dx, oy - 20 - h), (ox + dx + 26, oy - 20)], c(255, 120, 40), ow=8)
    a.ell(ox, oy, 62, 62, c(255, 190, 70))
    a.poly([p.at(-105, -470), p.at(105, -470), p.at(215, 0), p.at(-215, 0)], robe)
    a.rr(*p.at(-110, -300), *p.at(110, -255), 10, GOLD)
    p.arm(1, p.at(300, -450), color=robe); p.arm(-1, p.at(-190, -300), color=robe)
    p.head(smile=False)
    hx, hy = p.at(0, -575); r = p.r
    a.poly([(hx - r * 0.8, hy + r * 0.15), (hx + r * 0.8, hy + r * 0.15), (hx + r * 0.3, hy + r * 1.1), (hx, hy + r * 1.6), (hx - r * 0.3, hy + r * 1.1)], c(245, 245, 250))
    a.ell(hx, hy - r * 0.78, r * 1.5, r * 0.3, robe)
    a.poly([(hx - r * 0.88, hy - r * 0.7), (hx + r * 0.25, hy - r * 2.2), (hx + r * 0.88, hy - r * 0.7)], robe)
    a.rr(hx - r * 0.9, hy - r * 0.98, hx + r * 0.9, hy - r * 0.72, 8, GOLD, ow=8)
    return a, "Card_Emberseer"


def lancer():
    a = Art(c(255, 200, 130), c(120, 50, 20)); p = Person(a, torso=c(235, 130, 60), legs=c(90, 75, 70))
    a.soft(lambda d: [d.line([(40, y), (220 + i * 25, y)], fill=(255, 255, 255, 120), width=12) for i, y in enumerate((520, 620, 720, 820))], blur=2)
    a.poly([p.at(-150, -470), p.at(150, -470), p.at(300, -60), p.at(-300, -60)], c(200, 70, 40))   # cape
    p.legs_(); p.torso_()
    a.rr(*p.at(-125, -255), *p.at(125, -205), 10, GOLD)
    a.stroke([p.at(-300, -150), p.at(360, -730)], 30, c(150, 105, 60))                              # lance
    a.poly([p.at(355, -660), p.at(440, -830), p.at(295, -765)], STEEL)
    a.poly([p.at(300, -600), p.at(40, -560), p.at(130, -660), p.at(270, -670)], c(255, 130, 50), ow=9)   # pennant
    p.arm(1, p.at(150, -350)); p.arm(-1, p.at(-90, -300))
    p.head(eyes=True, smile=False)
    hx, hy = p.at(0, -575); r = p.r
    a.poly([(hx - r * 1.1, hy + r * 0.3), (hx - r * 1.05, hy - r * 0.3), (hx, hy - r * 1.15), (hx + r * 1.05, hy - r * 0.3), (hx + r * 1.1, hy + r * 0.3),
            (hx + r * 0.8, hy + r * 0.1), (hx + r * 0.75, hy - r * 0.35), (hx - r * 0.75, hy - r * 0.35), (hx - r * 0.8, hy + r * 0.1)], STEEL)
    a.poly([(hx - 20, hy - r * 1.1), (hx + 30, hy - r * 1.75), (hx + 150, hy - r * 1.5), (hx + 80, hy - r * 1.05)], c(255, 130, 50))
    return a, "Card_Lancer"


def powder_imp():
    a = Art(c(255, 150, 90), c(90, 15, 20)); p = Person(a, s=0.92, torso=c(215, 70, 55), legs=c(60, 40, 45), skin=c(225, 90, 70))
    bx, by = p.at(-140, -400)
    a.ell(bx, by, 140 * p.s, 140 * p.s, c(45, 45, 52)); a.glow(bx - 40, by - 50, 40, 26, (255, 255, 255, 110), blur=12)
    a.stroke([(bx - 10, by - 135), (bx - 50, by - 215), (bx - 120, by - 235)], 12, c(200, 170, 110), ow=7)
    a.star(bx - 130, by - 245, 56, 23, 8, c(255, 225, 90), ow=7); a.glow(bx - 130, by - 245, 90, 90, (255, 190, 60, 160), blur=30)
    p.legs_(); p.torso_()
    p.arm(1, p.at(230, -320), w=58); p.arm(-1, p.at(-210, -330), w=58)
    p.head(eyes=False, smile=False)
    hx, hy = p.at(0, -575); r = p.r
    for sx in (-1, 1):
        a.poly([(hx + sx * r * 0.45, hy - r * 0.75), (hx + sx * r * 0.95, hy - r * 1.5), (hx + sx * r * 0.85, hy - r * 0.6)], c(250, 240, 210), ow=9)
        a.poly([(hx + sx * r * 0.95, hy - r * 0.05), (hx + sx * r * 1.65, hy - r * 0.45), (hx + sx * r * 1.0, hy + r * 0.35)], c(225, 90, 70), ow=9)
        ex = hx + sx * r * 0.36
        a.d.ellipse((ex - 26, hy - r * 0.2 - 26, ex + 26, hy - r * 0.2 + 26), fill=(255, 255, 255, 255)); a.d.ellipse((ex - 10, hy - r * 0.2 - 10, ex + 10, hy - r * 0.2 + 10), fill=OUT)
        a.d.line([(ex - 34, hy - r * 0.2 - 44 + sx * 10), (ex + 34, hy - r * 0.2 - 44 - sx * 10)], fill=OUT, width=12)
    a.poly([(hx - r * 0.55, hy + r * 0.2), (hx + r * 0.55, hy + r * 0.2), (hx + r * 0.42, hy + r * 0.62), (hx - r * 0.42, hy + r * 0.62)], c(60, 20, 25), ow=8)
    for k in range(-2, 3): a.poly([(hx + k * 34 - 14, hy + r * 0.2), (hx + k * 34 + 14, hy + r * 0.2), (hx + k * 34, hy + r * 0.38)], (255, 255, 255, 255), ow=3)
    return a, "Card_PowderImp"


def cleric():
    a = Art(c(255, 250, 200), c(90, 140, 90)); p = Person(a, torso=c(250, 248, 240)); white = c(250, 248, 240)
    a.stroke([p.at(285, -150), p.at(300, -660)], 26, WOOD)
    ox, oy = p.at(300, -725)
    a.glow(ox, oy, 160, 160, (255, 235, 120, 210), blur=45); a.ell(ox, oy, 58, 58, c(255, 235, 140))
    a.poly([p.at(-105, -470), p.at(105, -470), p.at(205, 0), p.at(-205, 0)], white)
    a.poly([p.at(-30, -470), p.at(30, -470), p.at(60, 0), p.at(-60, 0)], GOLD, ow=8)
    a.rr(*p.at(-110, -330), *p.at(110, -290), 10, GOLD)
    p.arm(1, p.at(295, -450), color=white); p.arm(-1, p.at(-175, -330), color=white)
    a.ell(*p.at(0, -575), p.r * 1.06, p.r * 1.02, c(120, 80, 50), ow=0) if False else None
    p.head()
    hx, hy = p.at(0, -575); r = p.r
    a.poly([(hx - r * 1.05, hy + r * 0.2), (hx - r * 0.9, hy - r * 0.75), (hx, hy - r * 1.15), (hx + r * 0.9, hy - r * 0.75), (hx + r * 1.05, hy + r * 0.2),
            (hx + r * 0.85, hy - r * 0.35), (hx, hy - r * 0.68), (hx - r * 0.85, hy - r * 0.35)], c(240, 200, 120))   # hair
    a.glow(hx, hy - r * 1.45, r * 1.1, r * 0.4, (255, 225, 110, 200), blur=25)
    a.d.ellipse((hx - r * 0.95, hy - r * 1.75, hx + r * 0.95, hy - r * 1.15), outline=OUT, width=38)
    a.d.ellipse((hx - r * 0.95, hy - r * 1.75, hx + r * 0.95, hy - r * 1.15), outline=GOLD, width=22)
    def plus(d):
        for (x, y, s) in ((150, 380, 46), (880, 300, 38), (860, 640, 50), (130, 700, 36)):
            d.rounded_rectangle((x - s, y - s / 3.2, x + s, y + s / 3.2), 10, fill=(90, 230, 120, 235)); d.rounded_rectangle((x - s / 3.2, y - s, x + s / 3.2, y + s), 10, fill=(90, 230, 120, 235))
    a.soft(plus, blur=1)
    return a, "Card_DawnCleric"


def stormcaller():
    a = Art(c(150, 170, 255), c(20, 25, 70)); p = Person(a, torso=c(45, 75, 140)); robe = c(45, 75, 140)
    for (x, y, rx, ry) in ((330, 190, 170, 80), (512, 150, 200, 95), (700, 190, 170, 80), (470, 240, 240, 70)):
        a.ell(x, y, rx, ry, c(92, 100, 140), ow=10)
    a.stroke([p.at(290, -150), p.at(300, -640)], 26, WOOD)
    ox, oy = p.at(300, -705)
    a.glow(ox, oy, 170, 170, (150, 210, 255, 210), blur=50); a.ell(ox, oy, 56, 56, c(190, 235, 255))
    for ang in (-160, -110, -50, -5, 40):
        r = math.radians(ang)
        pts = [(ox + math.cos(r) * d + (14 if i % 2 else -14), oy + math.sin(r) * d) for i, d in enumerate((60, 105, 150, 195))]
        a.stroke(pts, 14, c(255, 250, 170), ow=6)
    a.poly([(a_ := p.at(-105, -470)), p.at(105, -470), p.at(215, 0), p.at(-215, 0)], robe)
    a.rr(*p.at(-110, -300), *p.at(110, -258), 10, c(120, 190, 255))
    p.arm(1, p.at(295, -420), color=robe); p.arm(-1, p.at(-190, -300), color=robe)
    p.head(smile=False)
    hx, hy = p.at(0, -575); r = p.r
    a.ell(hx, hy - r * 0.05, r * 1.22, r * 1.18, robe)
    a.ell(hx, hy + r * 0.08, r * 0.8, r * 0.72, SKIN, ow=9); p.eyes(dy=8)
    a.poly([(hx - r * 0.75, hy - r * 0.75), (hx, hy - r * 1.6), (hx + r * 0.75, hy - r * 0.75)], robe)
    return a, "Card_Stormcaller"


def warden():
    a = Art(c(200, 245, 255), c(25, 70, 130)); p = Person(a, hw=1.15, torso=c(135, 190, 235), legs=c(75, 110, 160))
    def flakes(d):
        for (x, y, s) in ((140, 330, 46), (880, 280, 40), (900, 620, 52), (120, 640, 38), (720, 140, 30)):
            for k in range(3):
                r = math.radians(60 * k); d.line([(x - s * math.cos(r), y - s * math.sin(r)), (x + s * math.cos(r), y + s * math.sin(r))], fill=(255, 255, 255, 235), width=9)
    a.soft(flakes, blur=1)
    p.legs_(); p.torso_()
    a.rr(*p.at(-140, -260), *p.at(140, -210), 10, c(225, 240, 255))
    a.stroke([p.at(255, -250), p.at(320, -640)], 34, WOOD)                                   # hammer
    a.rr(*p.at(190, -800), *p.at(470, -625), 26, c(200, 230, 255))
    a.poly([p.at(330, -800), p.at(380, -895), p.at(430, -800)], c(150, 230, 255), ow=9)
    p.arm(1, p.at(258, -300), w=64)
    a.ell(*p.at(-235, -350), 150 * p.s, 150 * p.s, c(120, 175, 225))                          # round shield
    a.ell(*p.at(-235, -350), 105 * p.s, 105 * p.s, c(70, 120, 185), ow=8)
    sx, sy = p.at(-235, -350)
    for k in range(3):
        r = math.radians(60 * k); a.d.line([(sx - 80 * math.cos(r), sy - 80 * math.sin(r)), (sx + 80 * math.cos(r), sy + 80 * math.sin(r))], fill=(240, 250, 255, 255), width=12)
    p.arm(-1, p.at(-140, -330), w=64)
    p.head(eyes=True, smile=False)
    hx, hy = p.at(0, -575); r = p.r
    a.poly([(hx - r * 1.1, hy + r * 0.3), (hx - r * 1.05, hy - r * 0.3), (hx, hy - r * 1.15), (hx + r * 1.05, hy - r * 0.3), (hx + r * 1.1, hy + r * 0.3),
            (hx + r * 0.8, hy + r * 0.1), (hx + r * 0.75, hy - r * 0.3), (hx - r * 0.75, hy - r * 0.3), (hx - r * 0.8, hy + r * 0.1)], c(150, 205, 240))
    for sx_ in (-1, 1):
        a.poly([(hx + sx_ * r * 0.95, hy - r * 0.7), (hx + sx_ * r * 1.6, hy - r * 1.55), (hx + sx_ * r * 1.3, hy - r * 0.4)], c(245, 240, 225), ow=9)
    return a, "Card_FrostWarden"


def drummer():
    a = Art(c(255, 205, 120), c(110, 45, 15)); p = Person(a, torso=c(215, 120, 50), legs=c(95, 62, 40))
    def waves(d):
        for k in range(3):
            d.arc((20 + k * 40, 330 + k * 30, 300 - k * 10, 780 - k * 30), 130, 230, fill=(255, 240, 170, 200), width=14)
            d.arc((724 + k * 10, 330 + k * 30, 1004 - k * 40, 780 - k * 30), -50, 50, fill=(255, 240, 170, 200), width=14)
    a.soft(waves, blur=1)
    p.legs_(); p.torso_()
    a.ell(*p.at(0, -300), 175 * p.s, 135 * p.s, c(150, 100, 60))                               # drum body
    a.ell(*p.at(0, -320), 160 * p.s, 120 * p.s, c(255, 235, 195), ow=9)
    a.d.ellipse((p.at(0, -320)[0] - 160 * p.s, p.at(0, -320)[1] - 120 * p.s, p.at(0, -320)[0] + 160 * p.s, p.at(0, -320)[1] + 120 * p.s), outline=c(240, 130, 50), width=22)
    a.stroke([p.at(235, -470), p.at(115, -330)], 18, WOOD, ow=8); a.stroke([p.at(-235, -470), p.at(-115, -330)], 18, WOOD, ow=8)
    p.arm(1, p.at(240, -480), w=58); p.arm(-1, p.at(-240, -480), w=58)
    for sx in (-1, 1): a.ell(*p.at(sx * 240, -500), 26, 26, c(245, 200, 120), ow=8)
    p.head(smile=False)
    hx, hy = p.at(0, -575); r = p.r
    a.d.arc((hx - r * 0.4, hy + r * 0.05, hx + r * 0.4, hy + r * 0.6), 10, 170, fill=OUT, width=10)
    a.poly([(hx - r * 1.1, hy + r * 0.25), (hx - r * 1.05, hy - r * 0.3), (hx, hy - r * 1.15), (hx + r * 1.05, hy - r * 0.3), (hx + r * 1.1, hy + r * 0.25),
            (hx + r * 0.85, hy - r * 0.05), (hx - r * 0.85, hy - r * 0.05)], c(150, 105, 65))
    a.poly([(hx - 14, hy - r * 1.1), (hx - 70, hy - r * 1.7), (hx + 70, hy - r * 1.7), (hx + 14, hy - r * 1.1)], c(240, 120, 45))
    return a, "Card_DrumCaptain"


def pebbleling():
    a = Art(c(200, 190, 175), c(75, 65, 60)); stone = c(140, 135, 130)
    a.poly([(300, 560), (360, 420), (480, 360), (620, 380), (720, 470), (740, 640), (690, 790), (560, 850), (400, 830), (310, 720)], stone)
    for sx in (-1, 1):
        a.ell(512 + sx * 170, 850, 70, 52, dark(stone, 0.2)); a.stroke([(512 + sx * 240, 560), (512 + sx * 320, 700)], 70, stone)
        a.ell(512 + sx * 325, 720, 56, 50, dark(stone, 0.2))
    a.glow(512, 690, 110, 100, (90, 200, 255, 190), blur=35); a.ell(512, 690, 45, 45, c(130, 230, 255), ow=9)
    for sx in (-1, 1):
        a.ell(512 + sx * 82, 520, 52, 58, c(255, 255, 255), ow=9); a.ell(512 + sx * 76, 530, 24, 28, OUT, ow=0)
    a.d.arc((452, 560, 572, 640), 20, 160, fill=OUT, width=10)
    a.ell(430, 380, 70, 30, c(90, 160, 80), ow=8); a.ell(610, 372, 48, 22, c(90, 160, 80), ow=8)
    return a, "Card_Pebbleling"


def scrapper():
    a = Art(c(180, 235, 130), c(40, 70, 35)); p = Person(a, s=0.92, torso=c(120, 85, 50), legs=c(80, 62, 40), skin=c(120, 195, 85))
    p.legs_(); p.torso_()
    a.stroke([p.at(250, -300), p.at(370, -640)], 38, BLADE)
    p.arm(1, p.at(255, -305), w=60); p.arm(-1, p.at(-200, -300), w=60)
    p.head(eyes=False, smile=False)
    hx, hy = p.at(0, -575); r = p.r
    for sx in (-1, 1):
        a.poly([(hx + sx * r * 0.85, hy - r * 0.1), (hx + sx * r * 2.0, hy - r * 0.75), (hx + sx * r * 1.0, hy + r * 0.45)], c(120, 195, 85), ow=9)
        ex = hx + sx * r * 0.38
        a.d.ellipse((ex - 28, hy - r * 0.1 - 30, ex + 28, hy - r * 0.1 + 30), fill=c(255, 240, 120)); a.d.ellipse((ex - 9, hy - r * 0.1 - 15, ex + 9, hy - r * 0.1 + 15), fill=OUT)
    a.poly([(hx - r * 0.6, hy + r * 0.25), (hx + r * 0.6, hy + r * 0.25), (hx + r * 0.45, hy + r * 0.62), (hx - r * 0.45, hy + r * 0.62)], c(70, 30, 25), ow=8)
    for k in (-1, 1): a.poly([(hx + k * 32 - 13, hy + r * 0.25), (hx + k * 32 + 13, hy + r * 0.25), (hx + k * 32, hy + r * 0.42)], (255, 255, 255, 255), ow=3)
    a.poly([(hx - r * 1.05, hy - r * 0.15), (hx - r * 0.7, hy - r * 0.95), (hx, hy - r * 1.25), (hx + r * 0.7, hy - r * 0.95), (hx + r * 1.05, hy - r * 0.15),
            (hx + r * 0.6, hy - r * 0.45), (hx - r * 0.6, hy - r * 0.45)], c(110, 80, 50))
    return a, "Card_Scrapper"


def bolt_tower():
    a = Art(c(255, 185, 110), c(70, 30, 45)); stone = c(150, 150, 165)
    a.poly([(330, 900), (390, 420), (634, 420), (694, 900)], stone)
    for k, y in enumerate(range(480, 900, 70)):
        a.d.line([(372 + k * 2, y), (652 - k * 2, y)], fill=OUT, width=8)
        for x in range(430 + (k % 2) * 40, 620, 80): a.d.line([(x, y), (x, y + 70)], fill=OUT, width=8)
    a.rr(340, 380, 684, 470, 14, dark(stone, 0.15))
    for x in (350, 470, 590): a.rr(x, 320, x + 84, 400, 10, stone, ow=10)
    a.ell(512, 330, 120, 100, c(90, 92, 110)); a.ell(512, 330, 70, 60, c(255, 150, 70), ow=10)
    a.stroke([(570, 330), (800, 280)], 70, c(60, 62, 76))
    a.glow(850, 270, 100, 80, (255, 220, 100, 210), blur=30)
    a.star(850, 270, 80, 34, 8, c(255, 235, 120), ow=8)
    a.rr(450, 640, 574, 900, 60, c(70, 45, 35))
    a.stroke([(512, 260), (512, 130)], 12, WOOD, ow=7); a.poly([(520, 130), (650, 165), (520, 200)], c(235, 80, 70), ow=8)
    return a, "Card_BoltTower"


def den():
    a = Art(c(190, 240, 130), c(35, 75, 45)); wood = c(140, 95, 55)
    a.rr(250, 520, 774, 910, 24, wood)
    for x in range(310, 774, 80): a.d.line([(x, 530), (x, 900)], fill=dark(wood, 0.5), width=8)
    a.poly([(180, 560), (512, 270), (844, 560), (780, 560), (512, 345), (244, 560)], c(90, 150, 70))
    a.poly([(200, 580), (512, 290), (824, 580), (770, 580), (512, 360), (254, 580)], c(110, 175, 80), ow=10)
    a.rr(420, 640, 604, 910, 70, c(28, 20, 22), ow=10)
    a.glow(512, 790, 70, 90, (255, 235, 90, 130), blur=25)
    for sx in (-1, 1): a.d.ellipse((512 + sx * 34 - 16, 760, 512 + sx * 34 + 16, 802), fill=c(255, 235, 90))
    a.stroke([(800, 560), (800, 240)], 14, WOOD, ow=8); a.poly([(806, 250), (930, 290), (806, 330)], c(120, 195, 85), ow=9)
    for x in (300, 700): a.rr(x - 38, 600, x + 38, 690, 10, c(250, 215, 110), ow=9)
    return a, "Card_ScrapperDen"


def ember_burst():
    a = Art(c(255, 170, 70), c(75, 15, 20))
    a.glow(512, 520, 420, 380, (255, 120, 30, 200), blur=70)
    a.star(512, 520, 400, 210, 12, c(230, 70, 35), rot=0.1)
    a.star(512, 520, 320, 160, 12, c(255, 140, 40), rot=0.35, ow=10)
    a.star(512, 520, 220, 110, 10, c(255, 215, 80), rot=0.2, ow=9)
    a.ell(512, 520, 100, 100, c(255, 250, 215), ow=8)
    a.soft(lambda d: [d.ellipse((x - r, y - r, x + r, y + r), fill=(255, 210, 90, 240)) for (x, y, r) in ((150, 230, 16), (870, 250, 20), (120, 760, 14), (900, 740, 18), (300, 110, 12), (720, 100, 14))], blur=2)
    return a, "Card_EmberBurst"


def snowflake(a, cx, cy, size, w, fill):
    for k in range(6):
        ang = math.radians(60 * k)
        ex, ey = cx + size * math.cos(ang), cy + size * math.sin(ang)
        a.stroke([(cx, cy), (ex, ey)], w, fill, ow=8)
        for t in (0.45, 0.72):
            bx, by = cx + size * t * math.cos(ang), cy + size * t * math.sin(ang)
            for s in (-1, 1):
                b = ang + s * math.radians(50)
                a.stroke([(bx, by), (bx + size * 0.28 * math.cos(b), by + size * 0.28 * math.sin(b))], w * 0.7, fill, ow=7)


def frost_field():
    a = Art(c(190, 240, 255), c(20, 60, 130))
    a.glow(512, 640, 440, 200, (120, 210, 255, 210), blur=40)
    a.ell(512, 660, 400, 170, c(185, 228, 250), ow=10)
    a.ell(512, 660, 300, 120, c(215, 243, 255), ow=8)
    for (x, y, h, w) in ((230, 640, 300, 80), (310, 720, 210, 66), (790, 640, 320, 84), (710, 730, 220, 66), (512, 600, 120, 50)):
        a.poly([(x - w, y), (x, y - h), (x + w, y)], c(140, 215, 250), ow=10)
    snowflake(a, 512, 400, 230, 26, c(245, 252, 255))
    return a, "Card_FrostField"


def verdant_pulse():
    a = Art(c(190, 255, 170), c(20, 85, 50))
    for k, rr_ in enumerate((420, 320, 220)):
        a.d.ellipse((512 - rr_, 600 - rr_ * 0.45, 512 + rr_, 600 + rr_ * 0.45), outline=OUT, width=30)
        a.d.ellipse((512 - rr_, 600 - rr_ * 0.45, 512 + rr_, 600 + rr_ * 0.45), outline=c(110 + k * 30, 230, 120 + k * 20), width=16)
    a.glow(512, 450, 230, 230, (150, 255, 160, 190), blur=55)
    leaf = [(512, 160), (650, 260), (700, 420), (600, 580), (512, 660), (424, 580), (324, 420), (374, 260)]
    a.poly(leaf, c(90, 205, 95))
    a.poly([(512, 200), (620, 290), (512, 640), (404, 290)], c(130, 235, 120), ow=0)
    a.d.line([(512, 190), (512, 660)], fill=OUT, width=14)
    for y in (300, 390, 480):
        a.d.line([(512, y + 40), (600, y)], fill=OUT, width=9); a.d.line([(512, y + 40), (424, y)], fill=OUT, width=9)
    def plus(d):
        for (x, y, s) in ((170, 360, 48), (860, 330, 40), (800, 780, 46), (200, 790, 36)):
            d.rounded_rectangle((x - s, y - s / 3.2, x + s, y + s / 3.2), 10, fill=(255, 255, 255, 240)); d.rounded_rectangle((x - s / 3.2, y - s, x + s / 3.2, y + s), 10, fill=(255, 255, 255, 240))
    a.soft(plus, blur=1)
    return a, "Card_VerdantPulse"


def thunder_snare():
    a = Art(c(175, 160, 255), c(25, 15, 70))
    a.ell(512, 690, 380, 120, c(110, 100, 170), ow=10)
    a.d.ellipse((512 - 280, 690 - 80, 512 + 280, 690 + 80), outline=c(255, 235, 120), width=14)
    a.glow(512, 560, 300, 360, (255, 240, 130, 130), blur=60)
    for (x, y, rx, ry) in ((300, 210, 200, 95), (512, 160, 240, 110), (730, 215, 200, 95), (520, 260, 300, 80)):
        a.ell(x, y, rx, ry, c(88, 92, 135), ow=10)
    a.poly([(570, 250), (385, 560), (500, 560), (430, 870), (670, 480), (550, 480), (650, 250)], c(255, 242, 130), ow=15)
    a.poly([(575, 290), (440, 540), (540, 540), (470, 780), (620, 500), (520, 500), (600, 290)], c(255, 255, 220), ow=0)
    a.soft(lambda d: [d.ellipse((x - r, y - r, x + r, y + r), fill=(255, 245, 160, 240)) for (x, y, r) in ((200, 640, 14), (830, 620, 16), (300, 820, 12), (740, 830, 14))], blur=2)
    return a, "Card_ThunderSnare"


CARDS = [vanguard, sprinter, ranger, bulwark, wisp, emberseer, lancer, powder_imp, cleric, stormcaller, warden, drummer,
         pebbleling, scrapper, bolt_tower, den, ember_burst, frost_field, verdant_pulse, thunder_snare]

if __name__ == "__main__":
    here = os.path.dirname(os.path.abspath(__file__))
    folder = sys.argv[1] if len(sys.argv) > 1 else os.path.join(here, "..", "Resources", "CardArt")
    os.makedirs(folder, exist_ok=True)
    for fn in CARDS:
        art, name = fn()
        art.save(folder, name)
        print("wrote", name)
