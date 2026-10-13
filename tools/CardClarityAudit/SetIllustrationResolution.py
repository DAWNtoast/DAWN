"""Set the isolated comparison (or reviewed source) card illustration budget."""
import argparse
import hashlib
import json
import re
from pathlib import Path

parser = argparse.ArgumentParser()
parser.add_argument("project")
parser.add_argument("size", type=int, choices=[512, 1024])
parser.add_argument("--report", required=True)
args = parser.parse_args()
project = Path(args.project).resolve()
allowed = [Path("D:/Game/tmp/card-clarity-20261013").resolve(), Path("D:/Game/UnityClient").resolve()]
if project not in allowed:
    raise SystemExit("Only the explicit comparison and source projects are supported.")
report = Path(args.report).resolve()
if not report.is_relative_to(Path("D:/Game/Artifacts/CardClarity20261013").resolve()) or report.exists():
    raise SystemExit("Report must be new and inside this task's artifact folder.")
editor = project / "Assets/_Project/Editor/CardSchoolAssetImport.cs"
text = editor.read_text(encoding="utf-8-sig")
if "WesternRoot" not in text:
    text = text.replace('private const string Root = "Assets/_Project/Resources/CardSchools/";', 'private const string Root = "Assets/_Project/Resources/CardSchools/";\n        private const string WesternRoot = "Assets/_Project/Resources/CardArt/Western/";\n        private const int IllustrationMaxTextureSize = 1024;\n\n        public override uint GetVersion() => 1;')
    text = text.replace('if (!assetPath.StartsWith(Root, StringComparison.Ordinal)) return;', 'if (!assetPath.StartsWith(Root, StringComparison.Ordinal) &&\n                !assetPath.StartsWith(WesternRoot, StringComparison.Ordinal)) return;')
    text = text.replace('// A 512-wide illustration is comfortably above its 174px card window,\n            // including a 2x canvas; 132 distinct pictures need not remain full-size.\n            importer.maxTextureSize = frame ? 1024 : 512;', '// Preserve fine illustration detail for native 4K card faces and previews.\n            // Frame dimensions and the existing BC7 compression remain unchanged.\n            importer.maxTextureSize = frame ? 1024 : IllustrationMaxTextureSize;')
text, count = re.subn(r'IllustrationMaxTextureSize = (512|1024);', f'IllustrationMaxTextureSize = {args.size};', text)
if count != 1 or 'frame ? 1024 : IllustrationMaxTextureSize' not in text:
    raise SystemExit("Unexpected importer layout; no changes applied.")
records = []
for relative in ["Assets/_Project/Resources/CardArt/Western", "Assets/_Project/Resources/CardSchools/Art"]:
    for image in sorted((project / relative).glob("*.png")):
        meta = image.with_name(image.name + ".meta")
        original = meta.read_bytes()
        source = original.decode("utf-8-sig")
        modified, settings_count = re.subn(r'(?m)^(\s*maxTextureSize: )(?:512|1024)\s*$', lambda m: m.group(1) + str(args.size), source)
        if settings_count < 2:
            raise SystemExit(f"Unexpected texture metadata: {meta}")
        records.append((meta, modified, {"path":str(meta.relative_to(project)),"oldSha256":hashlib.sha256(original).hexdigest(),"settings":settings_count}))
if len(records) != 216:
    raise SystemExit(f"Expected 216 individual source illustrations, found {len(records)}")
editor.write_text(text, encoding="utf-8", newline="\n")
for meta, modified, record in records:
    meta.write_text(modified, encoding="utf-8", newline="\n")
    record["newSha256"] = hashlib.sha256(meta.read_bytes()).hexdigest()
report.write_text(json.dumps({"project":str(project),"illustrationMaxTextureSize":args.size,"illustrations":len(records),"textures":[r for _,_,r in records]}, indent=2),encoding="utf-8")
print(f"Configured {len(records)} illustrations at {args.size}; source PNGs and frame settings untouched.")