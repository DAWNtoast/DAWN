const fs=require('fs'),path=require('path'),crypto=require('crypto');
const root='D:/Game', out=root+'/Artifacts/ArtPolishBatch1_20261012';
const audit=JSON.parse(fs.readFileSync(root+'/Artifacts/ArtQualityAudit20261012/optimization-list.json','utf8'));
const sourcePaths=[...new Set(audit.rows.filter(r=>r.priority==='P1').flatMap(r=>r.files))];
if(sourcePaths.length!==31)throw new Error('Expected 31 production PNGs in first batch');
const extra=['UnityClient/Assets/_Project/Presentation/Runtime/LegacyDemoArtCatalog.cs','UnityClient/Assets/_Project/Tests/EditMode/ArtCatalogLazyLoadingTests.cs'];
const hash=p=>crypto.createHash('sha256').update(fs.readFileSync(p)).digest('hex');
fs.mkdirSync(out+'/originals',{recursive:true});
const manifestPath=out+'/source-baseline.json';
if(fs.existsSync(manifestPath)){console.log('Baseline exists; retaining original backup.');process.exit(0);}
const records=[];
for(const relative of [...sourcePaths.flatMap(p=>[p,p+'.meta']),...extra]){
 const source=root+'/'+relative,destination=out+'/originals/'+relative;
 fs.mkdirSync(path.dirname(destination),{recursive:true});
 if(fs.existsSync(destination)&&hash(destination)!==hash(source))throw new Error('Backup collision: '+relative);
 fs.copyFileSync(source,destination);
 records.push({relative,source,destination,sha256:hash(source)});
}
fs.writeFileSync(manifestPath,JSON.stringify({createdAtUtc:new Date().toISOString(),scope:'P1 only; 25 equipment masters, 4 event atlases / 80 cells, 2 icon crop repairs. No build/package.',records},null,2));
console.log(JSON.stringify({backedUp:records.length,pngs:sourcePaths.length,out}));
