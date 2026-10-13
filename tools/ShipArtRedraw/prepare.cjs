const fs = require('fs');
const path = require('path');
const sharp = require('C:/Users/Admin/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/sharp');
const root = 'D:/Game/Artifacts/ShipArtRedraw20261012';
const resources = 'D:/Game/UnityClient/Assets/_Project/Resources';
const groups = [
  { key: 'roster', source: 'PortArt/ship_roster_sheet.png', cols: 2, rows: 2, contacts: [.19,.17,.21,.23], descriptions: ['teal-trimmed two-mast cream-sailed light sloop with green pennants, carved golden bow', 'large three-mast pirate galleon with red and black torn sails, ivory skull and crossed bones emblem, red pennants, dark timber and gold trim, two broadside gun decks', 'three-mast navy galleon with blue sails and gold compass emblems, blue gold pennants, dark blue hull with gold trim and gunports', 'three-mast occult vessel with ragged violet sails, mint crescent emblem, purple pennants, dark curled hull carvings and mint lanterns'] },
  { key: 'regional', source: 'LegacyDemo/ship_sprite_sheet.png', cols: 4, rows: 2, contacts: [.085,.095,.095,.10,.255,.22,.225,.255], descriptions: ['small one-mast swift cutter, cream triangular sails, tiny black skull flag at stern and red mast pennant, blue-trimmed wooden hull', 'large three-mast cream square-sailed armed merchant galleon, red pennants, small black skull stern flag, warm timber and golden trim, broadside cannons', 'broad two-mast cargo brig with patched cream sails, red and green pennants, blue-trimmed timber hull, cargo barrels and crates', 'two-mast cream-sailed rugged pirate brig with black skull pennants and red bunting, reinforced spiked bow', 'spectral two-mast ghost sloop with tattered pale mint sails, glowing turquoise translucent weathered hull and green pennant', 'heavy three-mast cream square-sailed navy warship, blue gold pennants, blue hull bands and bright gold ornament, many broadside guns', 'low broad cargo barge with a long red canopy over open cargo deck, short central mast and red pennant, warm timber golden trim, hanging side barrels', 'three-mast dark occult vessel with torn violet sails and mint eye emblem, purple pennants, coiled black hull carvings and mint lanterns'] }
];
(async () => {
  if (fs.existsSync(path.join(root, 'manifest.json'))) throw new Error('This redraw has already been prepared; keep its original references and backups.');
  for (const dir of ['references', 'originals', 'generated', 'prompts', 'preview']) fs.mkdirSync(path.join(root, dir), {recursive:true});
  const entries = [];
  for (const group of groups) {
    const input = path.join(resources, group.source), meta = await sharp(input).metadata();
    fs.copyFileSync(input, path.join(root, 'originals', group.key + '.png'));
    fs.copyFileSync(input + '.meta', path.join(root, 'originals', group.key + '.png.meta'));
    for (let cell=0;cell<group.cols*group.rows;cell++) {
      const col=cell%group.cols, row=Math.floor(cell/group.cols);
      const left=Math.round(col*meta.width/group.cols), top=Math.round(row*meta.height/group.rows);
      const width=Math.round((col+1)*meta.width/group.cols)-left, height=Math.round((row+1)*meta.height/group.rows)-top;
      const name=group.key+'_'+cell;
      const file=path.join(root,'references',name+'.png');
      await sharp(input).extract({left,top,width,height}).png().toFile(file);
      entries.push({name,group:group.key,cell,cols:group.cols,rows:group.rows,source:group.source,reference:file,width,height,contact:group.contacts[cell],description:group.descriptions[cell]});
    }
  }
  fs.writeFileSync(path.join(root,'manifest.json'),JSON.stringify({root,starter:path.join(resources,'ModularShip/light_sloop_base.png'),entries},null,2));
  console.log('Prepared '+entries.length+' source cells at '+root);
})();
