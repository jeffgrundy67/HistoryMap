"""Step 1. Download NPGallery photo PDFs for each place in places.json ([{"ref": "..."}, ...]).
Lists https://npgallery.nps.gov/AssetDetail/NRIS/{ref}, downloads every non-"Text" PDF to pdf/, records results in fetch.json (resumable).
Usage: python fetch.py"""
import json,re,os,sys,requests,time
from concurrent.futures import ThreadPoolExecutor
UA={'User-Agent':'Mozilla/5.0'}
top=json.load(open('places.json'))
os.makedirs('pdf',exist_ok=True)
res=json.load(open('fetch.json')) if os.path.exists('fetch.json') else {}
def one(x):
    ref=x['ref']
    if ref in res and res[ref].get('done'): return ref,res[ref]
    r={'assets':[]}
    try:
        h=requests.get(f'https://npgallery.nps.gov/AssetDetail/NRIS/{ref}',headers=UA,timeout=60).text
        guids=list(dict.fromkeys(re.findall(r'GetAsset/([0-9a-fA-F-]{36})',h)))
        for g in guids:
            url=f'https://npgallery.nps.gov/GetAsset/{g}'
            with requests.get(url,headers=UA,timeout=120,stream=True) as s:
                cd=s.headers.get('content-disposition',''); ct=s.headers.get('content-type','')
                fn=(re.findall(r'filename=([^;]+)',cd) or [''])[0].strip('"')
                a={'guid':g,'url':url,'filename':fn,'ctype':ct,'status':s.status_code,'len':s.headers.get('content-length')}
                if s.status_code==200 and 'pdf' in ct and 'text' not in fn.lower():
                    p=f'pdf/{ref}_{g[:8]}.pdf'
                    with open(p,'wb') as f:
                        for c in s.iter_content(1<<16): f.write(c)
                    a['path']=p
                r['assets'].append(a)
        r['done']=True
    except Exception as e:
        r['err']=str(e)
    return ref,r
with ThreadPoolExecutor(6) as ex:
    for ref,r in ex.map(one,top):
        res[ref]=r
        print(ref,[(a['filename'],a.get('len')) for a in r['assets']],r.get('err',''),flush=True)
json.dump(res,open('fetch.json','w'),indent=1)
