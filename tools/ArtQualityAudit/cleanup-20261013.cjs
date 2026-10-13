// Exact user-approved retirement scope. This is not a generic missing-file filter.
const fs = require('fs');
const path = require('path');
const project = 'D:/Game';
const manifestPath = path.join(__dirname, 'retired-assets-20261013.json');
const manifest = JSON.parse(fs.readFileSync(manifestPath, 'utf8'));
const approvedGroups = new Set(['A01', 'A02', 'A03', 'A04', 'B01', 'I01', 'I02', 'I03', 'I04', 'I05', 'I06']);
const groups = manifest.groups;
if (groups.length !== approvedGroups.size || new Set(groups.map(group => group.id)).size !== approvedGroups.size || groups.some(group => !approvedGroups.has(group.id))) throw new Error('Unexpected approved cleanup groups');
const retired = new Set(groups.flatMap(group => group.files.map(file => file.path)));
const retiredMedia = new Set(groups.flatMap(group => group.files.filter(file => file.kind === 'media').map(file => file.path)));
if (groups.reduce((count, group) => count + group.files.length, 0) !== 104 || retired.size !== 104 || retiredMedia.size !== 61) throw new Error('Unexpected approved cleanup file scope');
const normalize = file => String(file).replace(/\\/g, '/').replace(/^D:\/Game\//i, '').replace(/^Assets\/_Project\//, 'UnityClient/Assets/_Project/');
const isRetired = file => retired.has(normalize(file));
function validateInventory(inventory) {
  const files = [...inventory.images.map(image => 'UnityClient/Assets/_Project/' + image.relative), ...inventory.archived.map(file => 'UnityClient/Assets/_Project/' + file)];
  const stale = files.filter(isRetired);
  if (stale.length) throw new Error('Inventory still includes approved retired files; rerun inventory.cjs: ' + stale.join(', '));
  const missing = files.filter(file => !fs.existsSync(path.join(project, file)));
  if (missing.length) throw new Error('Inventory source files missing: ' + missing.join(', '));
  if (new Set(files).size !== files.length) throw new Error('Duplicate source paths in inventory');
}
module.exports = { isRetired, retiredMedia, validateInventory };
