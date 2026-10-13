"""Runs each approved edit once; completed API images are never re-requested."""
import argparse,concurrent.futures,json,os,subprocess,time
from pathlib import Path
ROOT=Path('D:/Game')
OUT=ROOT/'Artifacts/BackgroundClarity4K20261013'
API=ROOT/'tools/BackgroundClarity4K20261013/Invoke-GeminiEnhancement.ps1'
EXPORT=ROOT/'tools/BackgroundClarity4K20261013/Export-PreserveFrame4K.ps1'
manifest=json.loads((OUT/'manifest.json').read_text(encoding='utf-8'))
parser=argparse.ArgumentParser();parser.add_argument('--workers',type=int,default=3);args=parser.parse_args()
assert 1<=args.workers<=3
assert manifest['approvedGroups']==['A01','A02','A03','A06'] and len(manifest['items'])==19
def command(script,args):
 childenv=os.environ.copy()
 # The bundled Python inherits PowerShell 7's module path. Explicit Windows
 # PowerShell 5.1 subprocesses need their own built-in script modules (Get-FileHash).
 childenv['PSModulePath']=os.path.join(os.environ['SystemRoot'],'System32','WindowsPowerShell','v1.0','Modules')
 result=subprocess.run(['powershell','-NoProfile','-ExecutionPolicy','Bypass','-File',str(script)]+args,
                       env=childenv,capture_output=True,text=True,creationflags=subprocess.CREATE_NO_WINDOW)
 return result.returncode, result.stdout.strip(), result.stderr.strip()
def job(item):
 name=item['name'];meta=Path(item['requestedOutput']+'.metadata.json')
 if not meta.exists():
  print('REQUEST '+name,flush=True)
  code,stdout,stderr=command(API,['-PromptFile',item['prompt'],'-OutputPath',item['requestedOutput'],
                                '-ReferenceImages',item['backup'],'-Resolution','4K','-AspectRatio','16:9','-TimeoutSeconds','180'])
  if code:return {'name':name,'status':'api_failed','exitCode':code,'message':stderr}
 data=json.loads(meta.read_text(encoding='utf-8-sig'))
 if not data['success']:return {'name':name,'status':'api_failed_previous_attempt','error':data.get('errorClassification')}
 if not Path(item['finalOutput']).exists():
  code,stdout,stderr=command(EXPORT,['-InputPath',data['output']['path'],'-OutputPath',item['finalOutput']])
  if code:return {'name':name,'status':'export_failed','exitCode':code,'message':stderr}
 print('READY '+name+' '+str(data['output']['width'])+'x'+str(data['output']['height'])+' -> 3840x2160',flush=True)
 return {'name':name,'status':'ready_for_review','apiMetadata':str(meta),'output':item['finalOutput']}
results=[]
with concurrent.futures.ThreadPoolExecutor(max_workers=args.workers) as pool:
 for done in concurrent.futures.as_completed([pool.submit(job,item) for item in manifest['items']]):
  result=done.result();results.append(result)
  (OUT/'generation-progress.json').write_text(json.dumps({'completed':len(results),'total':19,'items':results},ensure_ascii=False,indent=2),encoding='utf-8')
  print('PROGRESS '+str(len(results))+'/19 '+result['status'],flush=True)
print('GENERATION COMPLETE '+str(sum(r['status']=='ready_for_review' for r in results))+'/19',flush=True)
