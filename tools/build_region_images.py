"""Localize six observed region sprites without changing pixels outside the title.

Inputs are extracted from the user's game; this tool does not crawl or modify it.
The user approved deterministic Pillow text editing. Font files are not bundled.
"""
import argparse
import hashlib
import json
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont, ImageChops

TITLES = ['프랭탕 지구', '에그조티 지구', '누아르 지구', '볼칸 지구', '이베르 지구', '아테네스 전역']
BOX = (65, 258, 332, 305)

def build(source, font, package, receipt):
    images = package / 'images'
    images.mkdir(parents=True, exist_ok=True)
    entries, records = [], []
    for number, title in enumerate(TITLES, 1):
        name = f'BtArea{number:02d}'
        original = Image.open(source / (name + '.png')).convert('RGBA')
        if original.size != (396, 396): raise ValueError('Unexpected source size: ' + name)
        edited = original.copy()
        x0,y0,x1,y1 = BOX
        edited.paste((255,255,255,0), BOX)
        scale = 4
        layer = Image.new('RGBA', ((x1-x0)*scale, (y1-y0)*scale))
        draw = ImageDraw.Draw(layer)
        face = ImageFont.truetype(str(font), 34*scale)
        draw.text(((198-x0)*scale, (282-y0)*scale), title, font=face, anchor='mm',
                  fill=(90,85,76,255), stroke_width=3*scale, stroke_fill=(255,255,255,255))
        edited.paste(layer.resize((x1-x0,y1-y0), Image.Resampling.LANCZOS), (x0,y0))
        check = edited.copy(); check.paste(original.crop(BOX), BOX)
        if ImageChops.difference(original, check).getbbox(alpha_only=False):
            raise ValueError('Pixels outside title changed: ' + name)
        import io
        buffer=io.BytesIO();edited.save(buffer,format='PNG',optimize=False)
        raw=buffer.getvalue();digest=hashlib.sha256(raw).hexdigest();file='images/'+digest+'.png'
        (package/file).write_bytes(raw)
        entries.append(dict(kind='sprite',name=name,width=396,height=396,file=file,sha256=digest,bytes=len(raw)))
        records.append(dict(name=name,title=title,source_sha256=hashlib.sha256((source/(name+'.png')).read_bytes()).hexdigest(),
                            output_sha256=digest,unchanged_outside_title=True))
    (package/'images.json').write_text(json.dumps(dict(schema='gc-images-v1',entries=entries),ensure_ascii=False,sort_keys=True,indent=2)+'\n',encoding='utf8')
    receipt.parent.mkdir(parents=True,exist_ok=True)
    receipt.write_text(json.dumps(dict(title_box=BOX,font_sha256=hashlib.sha256(font.read_bytes()).hexdigest(),
                                      pillow=Image.__version__,records=records),ensure_ascii=False,indent=2)+'\n',encoding='utf8')
    return entries

if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__)
    p.add_argument('--source',type=Path,required=True);p.add_argument('--font',type=Path,required=True)
    p.add_argument('--package',type=Path,required=True);p.add_argument('--receipt',type=Path,required=True)
    a=p.parse_args();print(json.dumps({'targets':len(build(a.source,a.font,a.package,a.receipt))}))
