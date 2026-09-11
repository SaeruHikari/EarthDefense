"""Bake deterministic, periodic 3D cloud density noise (no downloaded assets).

Run with NumPy/Pillow Python, then run tools/bake_cloud_volume_texture.ps1.
The packer is a C# tool scene that folds this RGBA8 output into the shader's packed RG8
layout (R shape, G erosion and detail combined) and verifies every saved Texture3D slice.
RGBA is linear scalar data: R shape, G erosion, B detail, A coarse variation.
The volume contains independently varying density at EVERY height; it is not a
surface height map. Coordinates and byte order are Z,Y,X,RGBA (X contiguous).
"""
from pathlib import Path
import hashlib
import json
import numpy as np
from PIL import Image, ImageDraw

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / 'assets/earth/clouds/volume'
ART = ROOT / 'artifacts'
SIZE = 96
SEED = 261214


def fade(t):
    return t*t*t*(t*(t*6.0-15.0)+10.0)


def perlin(size, frequency, seed):
    """Periodic gradient noise. All lattice lookups wrap on three axes."""
    rng = np.random.default_rng(seed)
    gradients = rng.normal(size=(frequency, frequency, frequency, 3)).astype(np.float32)
    gradients /= np.maximum(np.linalg.norm(gradients, axis=-1, keepdims=True), 1e-6)
    p = (np.arange(size, dtype=np.float32)+0.5)/size*frequency
    cell = np.floor(p).astype(np.int32)
    f = p-cell
    z,y,x = np.meshgrid(cell, cell, cell, indexing='ij')
    tz,ty,tx = np.meshgrid(f, f, f, indexing='ij')
    fx,fy,fz = fade(tx),fade(ty),fade(tz)
    output = np.zeros((size,size,size), dtype=np.float32)
    for dz in (0,1):
        for dy in (0,1):
            for dx in (0,1):
                g = gradients[(z+dz)%frequency, (y+dy)%frequency, (x+dx)%frequency]
                dot = g[...,0]*(tx-dx)+g[...,1]*(ty-dy)+g[...,2]*(tz-dz)
                output += dot*(fx if dx else 1-fx)*(fy if dy else 1-fy)*(fz if dz else 1-fz)
    return output


def cellular(size, frequency, seed):
    """Smooth rounded feature unions and F1 cells from periodic Worley points.

    Features stay inside their cells; surrounding 27 cells are sufficient for
    nearest-distance search. Soft Gaussian sums avoid pointed F1 cone maxima
    in the main shape channel. F1 is retained for irregular erosion detail.
    """
    rng = np.random.default_rng(seed)
    feature = rng.uniform(.08,.92,size=(frequency,frequency,frequency,3)).astype(np.float32)
    p = (np.arange(size,dtype=np.float32)+.5)/size*frequency
    cell = np.floor(p).astype(np.int32)
    f = p-cell
    z,y,x = np.meshgrid(cell,cell,cell,indexing='ij')
    tz,ty,tx = np.meshgrid(f,f,f,indexing='ij')
    minimum = np.full((size,size,size), 10., dtype=np.float32)
    union = np.zeros((size,size,size),dtype=np.float32)
    for dz in (-1,0,1):
        for dy in (-1,0,1):
            for dx in (-1,0,1):
                q = feature[(z+dz)%frequency,(y+dy)%frequency,(x+dx)%frequency]
                d2 = (tx-dx-q[...,0])**2+(ty-dy-q[...,1])**2+(tz-dz-q[...,2])**2
                np.minimum(minimum,d2,out=minimum)
                union += np.exp(-d2*4.0)
    return 1-np.exp(-union*.9), 1-np.clip(np.sqrt(minimum)/1.08,0,1)


def normalize(field):
    # Soft tails, wide usable midrange; no binary thresholding during baking.
    lo,hi = np.quantile(field, [.003,.997])
    return np.clip((field-lo)/max(hi-lo,1e-6),0,1).astype(np.float32)


def main():
    OUT.mkdir(parents=True,exist_ok=True)
    ART.mkdir(parents=True,exist_ok=True)
    print('Baking periodic 96^3 cloud shape and erosion...',flush=True)
    p4 = perlin(SIZE,4,SEED)
    p8 = perlin(SIZE,8,SEED+1)
    p16 = perlin(SIZE,16,SEED+2)
    billow6,cell6 = cellular(SIZE,6,SEED+10)
    billow12,cell12 = cellular(SIZE,12,SEED+11)
    _,cell24 = cellular(SIZE,24,SEED+12)
    fbm = normalize(p4*.65+p8*.25+p16*.10)
    shape = normalize(fbm*.68+normalize(billow6*.75+billow12*.25)*.32)
    erosion = normalize(cell12*.72+cell24*.28)
    detail = normalize(perlin(SIZE,20,SEED+21)*.42+cell24*.58)
    variation = normalize(perlin(SIZE,3,SEED+31)*.72+perlin(SIZE,6,SEED+32)*.28)
    data = np.rint(np.stack((shape,erosion,detail,variation),axis=-1)*255).astype(np.uint8)
    raw = data.tobytes(order='C')
    raw_path = ART/'cloud_noise_3d.rgba8'
    raw_path.write_bytes(raw)
    stats = {'generator':'tools/bake_cloud_volume_noise.py','seed':SEED,'size':[SIZE,SIZE,SIZE],
             'format':'RGBA8 linear data','mipmaps':False,'raw_bytes':len(raw),
             'gpu_mib_without_mipmaps':len(raw)/1024**2,'raw_sha256':hashlib.sha256(raw).hexdigest(),
             'axis_order':'Z,Y,X,RGBA; X is contiguous; repeat in XYZ','channels':{}}
    for i,name in enumerate(('R_shape','G_erosion','B_detail','A_variation')):
        c = data[...,i].astype(np.float32)/255.
        axis_steps=[]
        for axis in range(3):
            ordinary=np.abs(np.diff(c,axis=axis)).mean()
            seam=np.abs(np.take(c,0,axis=axis)-np.take(c,-1,axis=axis)).mean()
            axis_steps.append({'axis':'ZYX'[axis],'adjacent_mean':float(ordinary),'wrap_mean':float(seam),
                               'wrap_to_interior_ratio':float(seam/max(ordinary,1e-6))})
        stats['channels'][name]={'min':float(c.min()),'max':float(c.max()),'mean':float(c.mean()),
            'std':float(c.std()),'percentiles_1_10_25_50_75_90_99':np.quantile(c,[.01,.1,.25,.5,.75,.9,.99]).tolist(),
            'fraction_in_025_075':float(((c>=.25)&(c<=.75)).mean()),'continuity':axis_steps}
    # A sampled periodic field is not supposed to duplicate first/last voxels:
    # they are separated by one voxel across the wrap, like any adjacent pair.
    assert all(.12<s['std']<.30 for s in stats['channels'].values())
    assert all(.6<c['wrap_to_interior_ratio']<1.5 for s in stats['channels'].values() for c in s['continuity'])
    (OUT/'NOISE_STATS.json').write_text(json.dumps(stats,indent=2)+'\n',encoding='utf-8')
    atlas=Image.new('RGB',(SIZE*8, SIZE*4+20),(12,15,20))
    for channel in range(4):
        for frame in range(8):
            z=int((frame+.5)*SIZE/8)
            slice_image=Image.fromarray(data[z,:,:,channel]).convert('RGB')
            atlas.paste(slice_image,(frame*SIZE,channel*SIZE+20))
    ImageDraw.Draw(atlas).text((5,4),'R shape | G erosion | B detail | A variation; rows, 8 Z slices',(230,235,240))
    atlas.save(ART/'cloud-volume-noise-slices.png')
    print(json.dumps({k:{'mean':v['mean'],'std':v['std']} for k,v in stats['channels'].items()},indent=2))
    print('Raw volume:',raw_path,'bytes:',len(raw),flush=True)


if __name__=='__main__':
    main()
