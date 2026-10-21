"""Install the Flow-generated candidate sheets as the runtime v9 sprites.

Candidates come from tools/flow-matte.py + tools/flow-sheet.py
(see README.md). All clips share one scale and the
same base image, so no per-clip coat colour correction or code-drawn tail wag is needed.
Usage: python import-flow-sprites.py <candidate_dir>
"""
import sys,json,hashlib,shutil,pathlib
r=pathlib.Path(__file__).resolve().parents[1];src=pathlib.Path(sys.argv[1]);out=r/'assets/sprites-v9'
# Playback rates match each clip's real timing (8 picks over one wag/stride at 24fps video);
# travel is the planted-paw speed at app scale so feet do not slide.
fps={'idle':13,'walk':8,'run':11,'jump':12,'treat':5,'headtilt':6,'sit':6,'lie':6}
travel={'idle':0,'walk':66,'run':150}
# Gait frames closest to the standing idle pose (alpha difference); a stop waits for one.
settle={'walk':[2,7],'run':[0,7]}
a=json.loads((out/'atlas.json').read_text(encoding='utf-8'))
for name,c in a['actions'].items():
    shutil.copyfile(src/f'{name}-candidate.png',out/c['image'])
    c['sha256']=hashlib.sha256((out/c['image']).read_bytes()).hexdigest()
    c['defaultFps']=fps[name];c['coatColorGain']=[1.0,1.0,1.0];c.pop('stationaryTailWag',None)
    if name in travel:c['travelPixelsPerSecond']=travel[name]
    if name in settle:c['settleFrames']=settle[name]
# Optional run slow-down clip (16 frames, 4x4 cells) played when a run ends.
stop=src/'runstop-candidate.png'
if stop.exists():
    shutil.copyfile(stop,out/'runstop.png')
    a['actions']['run']['stop']={'image':'runstop.png','width':1776,'height':1776,'defaultFps':8,
        'sha256':hashlib.sha256((out/'runstop.png').read_bytes()).hexdigest(),
        'frames':[{'index':i,'sourceRect':{'x':i%4*444,'y':i//4*444,'width':444,'height':444},'anchor':{'x':222,'y':405}} for i in range(16)]}
(out/'atlas.json').write_text(json.dumps(a,indent=2),encoding='utf-8')
print({n:c['sha256'][:12] for n,c in a['actions'].items()})
