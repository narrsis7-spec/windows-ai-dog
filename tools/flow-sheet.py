"""Pack keyed Flow frames into a v9 runtime sheet (444px cells, 4 per row, ground at y=405).

8 frames give a 1776x888 sheet, 16 frames (jump) a 1776x1776 sheet. One scale for every
frame so the dog never changes size. Usage:
python flow-sheet.py <rgba_dir> <out.png> <targetHeight|xScale> <targetCenterX> <frame> [<frame> ...]
Frames are 1-based file numbers (f001.png ...). The height is head-top to ground;
"x0.4838" gives the scale directly so clips share one dog size.

Horizontal: by default each frame's torso (rows 380-460 of the source) is centred, for
tracking-camera clips (walk, run). FLOW_X=fixed uses the torso of the clip's first video
frame (standing pose) for all frames, for locked-camera clips where the dog stays put.
Vertical: the lowest paw of the selected frames sits on y=405. FLOW_GROUND=frame grounds
each frame on its own lowest point instead (jump: the app adds the lift itself);
FLOW_GROUND=first uses the clip's first video frame.
"""
import os,sys,pathlib
import numpy as np
from PIL import Image
src=pathlib.Path(sys.argv[1]);out=sys.argv[2];size=sys.argv[3];centerX=float(sys.argv[4]);picks=[int(v) for v in sys.argv[5:]]
if len(picks) not in (8,16):raise SystemExit('need 8 or 16 frames')
frames=[Image.open(src/f'f{i:03d}.png').convert('RGBA') for i in picks]
alpha=lambda im:im.getchannel('A').point(lambda v:255 if v>32 else 0)
boxes=[alpha(f).getbbox() for f in frames]
top=min(b[1] for b in boxes);ground=max(b[3] for b in boxes)
scale=float(size[1:]) if size.startswith('x') else float(size)/(ground-top)
def torso(f):
    xs=np.nonzero((np.asarray(f)[380:460,:,3]>128).any(axis=0))[0];return (xs.min()+xs.max())/2
fixedTorso=torso(Image.open(src/'f001.png').convert('RGBA')) if os.environ.get('FLOW_X')=='fixed' else None
perFrameGround=os.environ.get('FLOW_GROUND')=='frame'
# FLOW_GROUND=first: the standing first video frame sets the ground, so a treat on the floor or
# paws stretched toward the camera do not lift the whole clip.
if os.environ.get('FLOW_GROUND')=='first':ground=alpha(Image.open(src/'f001.png').convert('RGBA')).getbbox()[3]
# FLOW_GROUND=<number>: an explicit source ground line, e.g. the final standing pose of a stop clip.
elif (os.environ.get('FLOW_GROUND') or '').isdigit():ground=int(os.environ['FLOW_GROUND'])
sheet=Image.new('RGBA',(1776,444*len(frames)//4))
for i,f in enumerate(frames):
    s=f.resize((round(f.width*scale),round(f.height*scale)),Image.Resampling.LANCZOS)
    big=Image.new('RGBA',(444,444))
    x0=round(centerX-(fixedTorso if fixedTorso is not None else torso(f))*scale)
    y0=round(405-(boxes[i][3] if perFrameGround else ground)*scale)
    # Paste with a possibly negative offset by cropping the scaled frame first.
    cx0,cy0=max(0,-x0),max(0,-y0)
    big.alpha_composite(s.crop((cx0,cy0,min(s.width,cx0+444-max(0,x0)),min(s.height,cy0+444-max(0,y0)))),(max(0,x0),max(0,y0)))
    sheet.alpha_composite(big,(i%4*444,i//4*444))
sheet.save(out)
b=[alpha(sheet.crop((i%4*444,i//4*444,i%4*444+444,i//4*444+444))).getbbox() for i in range(len(frames))]
print('scale',round(scale,4),'cellBoxes',b)
