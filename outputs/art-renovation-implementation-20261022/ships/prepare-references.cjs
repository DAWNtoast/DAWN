const fs=require('fs'),path=require('path');
const sharp=require('C:/Users/Admin/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/sharp');
const dir=__dirname+'/references';
(async()=>{fs.mkdirSync(dir,{recursive:true});for(const id of ['gunship','occult','legendary_curse']){const src=id.startsWith('legendary')?'D:/Game/UnityClient/Assets/_Project/Resources/ShipArtV2/Special/'+id+'.png':'D:/Game/UnityClient/Assets/_Project/Resources/PortArt/ShipDossiers/ship_'+id+'_dossier_v1.png';await sharp(src).resize({width:768,height:1152,fit:'inside',withoutEnlargement:true}).flatten({background:'#dce6e8'}).jpeg({quality:88}).toFile(dir+'/'+id+'.jpg');}
await sharp('D:/Game/UnityClient/Assets/_Project/Resources/NavalTactics/tactical_ship_v1.png').resize({width:512,height:768,fit:'inside'}).flatten({background:'#dce6e8'}).jpeg({quality:88}).toFile(dir+'/orientation.jpg');console.log(dir);})().catch(e=>{console.error(e);process.exit(1)});
