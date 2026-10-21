"""Key a Flow clip shot on a plain light-grey studio background into RGBA frames.

Alpha comes from colour distance to the background: the caramel coat is far from
neutral grey (chroma), dark eyes/nose are far in luminance, while the soft floor
shadow is neutral and only moderately darker, so it stays transparent.
Usage: python flow-matte.py <frames_dir> <out_dir>
"""
import sys,pathlib
import numpy as np
from PIL import Image,ImageFilter

src,out=pathlib.Path(sys.argv[1]),pathlib.Path(sys.argv[2]);out.mkdir(parents=True,exist_ok=True)
files=sorted(src.glob('*.png'))
first=np.asarray(Image.open(files[0]).convert('RGB')).astype(float)
# Background colour from the four frame corners (the watermark corner excluded).
corners=np.concatenate([first[:40,:40].reshape(-1,3),first[:40,-40:].reshape(-1,3),first[-40:,:40].reshape(-1,3)])
bg=np.median(corners,axis=0)
def ramp(v,lo,hi):return np.clip((v-lo)/(hi-lo),0,1)
for f in files:
    px=np.asarray(Image.open(f).convert('RGB')).astype(float)
    lum=px.mean(axis=2);chroma=px.max(axis=2)-px.min(axis=2)
    a=np.maximum(ramp(chroma-(bg.max()-bg.min()),10,38),ramp(bg.mean()-lum,70,120))
    # The Flow mark sits in the bottom-right corner, away from the dog; it is never part of the sprite.
    h,w=a.shape;a[int(h*.72):,int(w*.85):]=0
    # Drop isolated specks: keep pixels near a solid part of the subject.
    solid=np.asarray(Image.fromarray((a>0.6).astype('uint8')*255).filter(ImageFilter.MinFilter(3)).filter(ImageFilter.MaxFilter(15)))>0
    a=a*solid
    # Remove the grey background mixed into semi-transparent fur edges.
    al=np.clip(a,1e-3,1)[...,None];rgb=np.clip((px-(1-al)*bg)/al,0,255)
    Image.fromarray(np.dstack([rgb,a*255]).astype('uint8'),'RGBA').save(out/f.name)
print('bg',bg.round(1).tolist(),'frames',len(files))
