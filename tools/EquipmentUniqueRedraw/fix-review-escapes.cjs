const fs=require('fs');
const p='D:/Game/tools/EquipmentUniqueRedraw/review.cjs';
let s=fs.readFileSync(p,'utf8');
s=s.replaceAll("'\\n'","'\\\\n'").replaceAll("'\\n\\n结构设计：'","'\\\\n\\\\n结构设计：'");
s=s.replace(".replaceAll('<','\\u003c')",".replaceAll('<',String.fromCharCode(92)+'u003c')");
fs.writeFileSync(p,s);
