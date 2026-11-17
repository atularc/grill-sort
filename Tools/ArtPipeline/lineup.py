# Builds Grill-Food-Lineup.png: every food sprite with its name.
import os
from PIL import Image, ImageDraw, ImageFont
R=".."; F=ImageFont.truetype("fonts/Fredoka-Bold.ttf",34)
names=sorted(f[:-4] for f in os.listdir(f"{R}/food"))
cols=6; cell=300; rows=(len(names)+cols-1)//cols
S=Image.new("RGBA",(cols*cell,rows*cell+20),(252,236,214,255)); d=ImageDraw.Draw(S)
for i,n in enumerate(names):
    x=(i%cols)*cell; y=(i//cols)*cell+10
    d.rounded_rectangle((x+12,y+8,x+cell-12,y+cell-8),28,fill=(255,248,238))
    im=Image.open(f"{R}/food/{n}.png").resize((220,220),Image.LANCZOS)
    S.alpha_composite(im,(x+40,y+18))
    d.text((x+cell//2,y+cell-36),n.capitalize(),font=F,fill=(92,46,18),anchor="mm")
S.convert("RGB").save(f"{R}/Grill-Food-Lineup.png",optimize=True); print("lineup",S.size)
