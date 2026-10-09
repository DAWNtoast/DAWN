from pathlib import Path
root=Path('D:/Game')
source=(root/'Tools/VoyageHeaderCargoAudit/VoyageHeaderCargoCapture.cs').read_text(encoding='utf-8-sig')
source=source.replace('VoyageHeaderCargoCapture','TextPolishCargoCapture')
source=source.replace('new[] { "Navigation", "Encounter", "CargoBasic", "CargoSpecial", "CargoSelected" }','new[] { "CargoWater", "CargoGem", "CargoCompass", "WarehouseWater", "WarehouseGem", "WarehouseRelic" }')
source=source.replace('new Vector2Int(1920,1080), new Vector2Int(1280,720), new Vector2Int(3440,1440)','new Vector2Int(1920,1080), new Vector2Int(1280,720)')
source=source.replace('report.frames.Count != 15','report.frames.Count != 12').replace('15 verified production UI captures','12 verified production cargo-description captures')
source=source.replace('scope = "Disposable production Navigation/Encounter previews and Basic/Special/Selected voyage cargo UI at three render targets. " +', 'scope = "Read-only cargo and warehouse item description previews at two production scaler targets. All 15 descriptions checked against their actual text rectangle. " +')
source=source.replace('if (state != "CargoBasic") PreviewButton(controller,"Voyage Cargo Special Tab").onClick.Invoke();\n            if (state == "CargoSelected")', 'if (state != "CargoWater") PreviewButton(controller,"Voyage Cargo Special Tab").onClick.Invoke();\n            if (state.StartsWith("Cargo", StringComparison.Ordinal))')
source=source.replace('PreviewButton(controller,"Voyage Cargo Item gem").onClick.Invoke();', 'PreviewButton(controller,"Voyage Cargo Item " + (state == "CargoWater" ? "water" : state == "CargoCompass" ? "ancientcompass" : "gem")).onClick.Invoke();')
source=source.replace('CoreGameplayPreviewScreen screen = frame.state == "Encounter" ? CoreGameplayPreviewScreen.Encounter : CoreGameplayPreviewScreen.Navigation;', 'CoreGameplayPreviewScreen screen = frame.state.StartsWith("Warehouse", StringComparison.Ordinal) ? CoreGameplayPreviewScreen.Warehouse : CoreGameplayPreviewScreen.Navigation;')
source=source.replace('if (frame.state.StartsWith("Cargo", StringComparison.Ordinal)) PrepareCargoView(controller, frame.state);', '''if (frame.state.StartsWith("Cargo", StringComparison.Ordinal)) PrepareCargoView(controller, frame.state);
                else PrepareWarehouseView(controller, frame.state);''')
source=source.replace('frame.renderingDisplaySize = canvas.renderingDisplaySize;', 'CheckCargoDescriptions(canvas);\n                frame.renderingDisplaySize = canvas.renderingDisplaySize;')
anchor='        private static Button PreviewButton'
extra='''        private static void PrepareWarehouseView(CoreGameplayDemoController controller, string state)
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            Type type = typeof(CoreGameplayDemoController);
            type.GetProperty("warehouseStorageOpen", flags).SetValue(controller, true);
            type.GetField("warehouseRareStorageOpen", flags).SetValue(controller, state != "WarehouseWater");
            type.GetMethod("Render", flags).Invoke(controller, null);
            string key = state == "WarehouseWater" ? "water" : state == "WarehouseRelic" ? "relic" : "gem";
            string before = CargoSnapshot(controller.Session);
            object quote = type.GetMethod("CreateWarehouseCargoQuote", flags, null, new[] {typeof(string),typeof(bool)}, null)
                .Invoke(controller, new object[] {key, true});
            type.GetMethod("BuildWarehouseCargoActions", flags).Invoke(controller, new[] {quote});
            if (CargoSnapshot(controller.Session) != before) throw new InvalidOperationException("Warehouse preview changed cargo.");
        }

        private static void CheckCargoDescriptions(Canvas canvas)
        {
            Text label = canvas.GetComponentsInChildren<Text>(false).Single(t => t.name == "Voyage Cargo Item Description" || t.name == "Warehouse Item Description");
            TextGenerationSettings settings = label.GetGenerationSettings(label.rectTransform.rect.size);
            settings.resizeTextForBestFit = false;
            using (var generator = new TextGenerator())
                foreach (var item in CoreWarehouseItemCatalog.All)
                {
                    float height = generator.GetPreferredHeight(item.Description, settings) / label.pixelsPerUnit;
                    if (height > label.rectTransform.rect.height + 1f)
                        throw new InvalidOperationException("Description clipped: " + item.Key + " " + height + "/" + label.rectTransform.rect.height);
                }
        }

'''
source=source.replace(anchor,extra+anchor)
(root/'Tools/TextPolishAudit/TextPolishCargoCapture.cs').write_text(source,encoding='utf-8')
print('Created temporary 12-frame description capture, with every cargo description measured.')
