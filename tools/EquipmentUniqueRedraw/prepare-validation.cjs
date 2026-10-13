const fs=require('fs');
let code=fs.readFileSync('D:/Game/tools/ArtPolishBatch1/ArtPolishBatch1Capture.cs','utf8').replace(/\r\n/g,'\n');
function replace(a,b){if(!code.includes(a))throw Error('Missing capture anchor '+a.slice(0,80));code=code.replace(a,b);}
code=code.replaceAll('ArtPolishBatch1Capture','EquipmentUniqueRedrawCapture');
replace('25 equipment dossiers, ten showroom pages, twelve event examples across four atlases, three tutorial examples.','150 equipment dossiers, 50 showroom pages and 38 inventory pages.');
replace('catalog.Entries.GroupBy(e => e.ArtSourceId).Select(g => g.First()).OrderBy(e => e.ArtSourceId).ToArray()','catalog.ExpandedEntries.OrderBy(e => e.Id).ToArray()');
replace('if (entries.Length != 25) throw new InvalidOperationException("Expected 25 art prototypes.");','if (entries.Length != 150 || entries.Select(e => e.ArtSourceId).Distinct().Count() != 150) throw new InvalidOperationException("Expected 150 independent equipment identities.");');
replace('page < 2','page < 10');
const events=code.indexOf('            for (int route = 0; route < 4;');
const smoke=code.indexOf('            bool smoke =',events);
if(events<0||smoke<0)throw Error('Missing frames anchors');
code=code.slice(0,events)+'            for (int page = 0; page < 38; page++) report.frames.Add(new Frame { state = "Inventory", id = "All", page = page, file = "inventory_" + page.ToString("D2") + ".png" });\n'+code.slice(smoke);
replace('f.state == "Detail" ? f.id == "bow_oak_ram" : f.state == "Shop" ? f.id == "Cannon" && f.page == 0 : f.cell == 0','f.state == "Detail" ? f.id == "plating_corkwave_liner" || f.id == "bow_oak_ram" : f.state == "Shop" ? f.id == "Cannon" && f.page == 0 : f.page == 0');
const assetStart=code.indexOf('                foreach (string route in new[]');
const assetEnd=code.indexOf('\n            }',assetStart);
code=code.slice(0,assetStart)+'                foreach (var entry in entries) VerifyAsset(report, "Assets/_Project/Resources/ComponentArt/" + entry.ArtSourceId + ".png", 512, 512, true);'+code.slice(assetEnd);
code=code.replaceAll('第一批美术实际界面验收','150件独立装备实际界面验收').replaceAll('第一批美术 · 实际界面验收','150件独立装备 · 实际界面验收').replace('25件部件详情、10页陈列、四类事件与教学示例。','150件部件详情、50页陈列、38页配件库存。');
replace('if (path.Contains("_event_sheet") && texture.mipmapCount <= 1)','if (texture.mipmapCount <= 1)');
replace('Event minification mipmaps missing: ','Equipment minification mipmaps missing: ');
replace('bool equipment = frame.state == "Shop" || frame.state == "Detail";','bool equipment = frame.state == "Shop" || frame.state == "Detail";');
replace('equipment ? CoreGameplayPreviewScreen.ComponentShop : CoreGameplayPreviewScreen.Encounter','equipment ? CoreGameplayPreviewScreen.ComponentShop : CoreGameplayPreviewScreen.Shipyard');
replace('else if (frame.state == "Tutorial") ConfigureTutorial(controller, frame);\n                else ConfigureEvent(controller, frame);','else ConfigureInventory(controller, frame, entries);');
replace('asset.Contains("/ModularShip/Layers/") || asset.Contains("_event_sheet")','asset.Contains("/ModularShip/Layers/") || asset.Contains("/ComponentArt/")');
const configEvent=code.indexOf('        private static void ConfigureEvent('),configFind=code.indexOf('        private static T Find<T>(',configEvent);
code=code.slice(0,configEvent)+`        private static void ConfigureInventory(CoreGameplayDemoController controller, Frame frame, ComponentArtEntry[] entries)
        {
            var catalog = (ComponentArtCatalog)typeof(CoreGameplayDemoController).GetField("componentArt", Private).GetValue(controller);
            int serial = 0;
            var inventory = new OwnedShipComponentInventory(catalog, () => "art-review-" + serial++);
            inventory.EnsureLoadout(0);
            foreach (var entry in entries) inventory.Grant(catalog.GetById(entry.Id), 1f);
            typeof(CoreGameplayDemoController).GetField("componentInventory", Private).SetValue(controller, inventory);
            typeof(CoreGameplayDemoController).GetField("shipyardInventoryPage", Private).SetValue(controller, frame.page);
            typeof(CoreGameplayDemoController).GetField("shipyardInventoryShowAll", Private).SetValue(controller, true);
            typeof(CoreGameplayDemoController).GetField("shipyardDossierTab", Private).SetValue(controller, ShipyardDossierTab.Equipment);
            typeof(CoreGameplayDemoController).GetField("shipyardDetailOpen", Private).SetValue(controller, true);
            typeof(CoreGameplayDemoController).GetField("selectedShipPreview", Private).SetValue(controller, 0);
            RectTransform panel = Find<RectTransform>(controller, "Shipyard Panel"); Clear(panel);
            Invoke(controller, "BuildShipyardDossier", panel, 0);
        }
`+code.slice(configFind);
replace('Debug.Log("ART_POLISH_BATCH1_CAPTURE_OK: ','Debug.Log("EQUIPMENT_UNIQUE_REDRAW_CAPTURE_OK: ');
fs.writeFileSync('D:/Game/tools/EquipmentUniqueRedraw/EquipmentUniqueRedrawCapture.cs',code);
console.log('Prepared equipment-only capture utility: 150 details + 50 shop pages + 38 actual inventory pages.');
