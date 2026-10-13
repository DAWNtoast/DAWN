'use strict';
const fs=require('node:fs');
const file='D:/Game/Artifacts/TavernRedraw20261012/layout-v2.json';
const layout=JSON.parse(fs.readFileSync(file,'utf8'));
const scale=1920/layout.sourceWidth;
const overlaps=(a,b)=>a[0]<b[0]+b[2]&&a[0]+a[2]>b[0]&&a[1]<b[1]+b[3]&&a[1]+a[3]>b[1];
const errors=[];
for(const p of layout.ports){
 for(let i=0;i<6;i++){
  const b=p.bounds[i];
  if(b[0]<0||b[1]<31||b[0]+b[2]>1672||b[1]+b[3]>876)errors.push(p.id+' '+i+' outside visible crop');
  for(let j=i+1;j<6;j++)if(overlaps(b,p.bounds[j]))errors.push(p.id+' hotspots '+i+'/'+j);
  const [x,y]=p.nameCenters[i];
  for(const [type,w,h,cy] of [['name',220,34,y],['status',205,23,y-28/scale]]){
   const r=[x-w/scale/2,cy-h/scale/2,w/scale,h/scale];
   if(r[0]<0||r[1]<31||r[0]+r[2]>1672||r[1]+r[3]>876)errors.push(p.id+' '+i+' '+type+' outside visible crop');
   for(let j=0;j<6;j++)if(j!==i&&overlaps(r,p.bounds[j]))errors.push(p.id+' '+i+' '+type+' covers '+j);
  }
 }
}
console.log(JSON.stringify({ports:layout.ports.length,hotspots:36,errors},null,2));
if(errors.length)process.exitCode=1;
