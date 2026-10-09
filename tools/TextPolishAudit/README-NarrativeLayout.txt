Narrative text layout audit (temporary Editor tool)

Import TextPolishNarrativeLayoutAudit.cs only into the isolated validation project's Editor assembly. No production source, normal scene, asset or PlayerPrefs is modified by this tool. Run with a stopped Editor, not Play Mode.

Entry:
  -executeMethod ShatteredSail.Editor.TextPolishNarrativeLayoutAudit.RunFromCommandLine
  -textLayoutReport D:/Game/Artifacts/TextPolish20261008/Attempt01/narrative-layout.json

The report must be a new absolute .json path under Artifacts; existing reports are never overwritten. Any missing template/catalogue entry or failed layout writes failure details and throws, so the batch command fails.

Coverage: 150 expanded component Description entries + 15 shared component StoryBody chapters + 12 ship Lore + 12 ship Specialty + 40 treasure Description + 40 treasure Story = 269 records.

The tool creates disposable production EditorPreview scenes and obtains six actual production Text layouts. Component Story is opened through the real dossier page button; ship Story uses the existing ConfigureEditorShipyard page API. A one-item warehouse preview inventory, held only in memory, exposes the production Warehouse Collection Hint. Catalogue text is then bound to the actual labels, retaining the exact production rectangle, font, alignment, line spacing, overflow and best-fit configuration. It does not synthesize replacement UI or arbitrarily change font sizes.

Each record reports full text/tail, length, authored font size, best-fit range, actual rectangle, preferredHeight, and two generation checks (real 1920x1080 and 1280x720 Camera/RenderTexture targets, with the enabled production CanvasScaler verified). Each check records production-selected font size, full versus visible character/vertex counts, effective preferred height, and glyph bounds. The complete reference freezes the production-selected font size and allows overflow; it cannot shrink to hide a clipped last line. Raised CJK glyphs must stay below the preceding sibling paragraph or parent boundary; bottom bounds remain within the paragraph; horizontal bounds use the actual paper/panel, adjacent controls and active clipping masks, matching the existing dossier layout acceptance approach. Zero-area sentinel quads are ignored in glyph bounds.

These are text layout checks, not screenshots or a claim of full scene visual acceptance. Existing 36-character TavernDossierLayoutTests remain the character dossier coverage. PlayerPrefs setters, PlayerPrefs.Save, AssetDatabase.SaveAssets and scene-save APIs are never called by this tool; production preview lifecycles are guarded by non-playing mode. Temporary scenes/hosts are disposed on completion and construction failure.

Static compile evidence: Artifacts/TextPolish20261008/static-presentation/NarrativeLayout-Editor.rsp and NarrativeLayout-v3-compile.log, full current Editor sources plus this temporary tool. Unity execution is coordinated by root.

Attempt01 audit evidence is retained unchanged. Its 189 initial false results mixed glyph side-bearing overhang at the batch-default 640x360 surface with a tool reflection bug (warehouseStorageOpen is a property). Version 2 fixes field/property resolution, uses real supported 1080p/720p targets and visible surface horizontal bounds. It retains complete tail/mesh, production font size, preferred height, and vertical neighbour/bottom checks. A v2 failure still requires diagnosis; no production text was changed to force acceptance.

Attempt02/narrative-layout.json also remains unchanged: all 269 items were measured, but 40 treasure stories at 720p failed the tool's font-unit check. Native fontSizeUsedForBestFit returned raster size 10 at Canvas scale 2/3; v2 incorrectly treated it as logical size 10 and scaled its complete reference again. Version 3 records rawBestFitFontSize separately and resolves the reference's logical fontSize only by testing the authored min..max range against the actual production mesh: character/vertex counts and every vertex position must match (0.0001-pixel tolerance). It then retains that same logical size for the complete overflow reference. No matching size is a failure, not permission to choose a smaller font. Both full-tail/mesh and actual surface/vertical checks still apply. The v2 best-fit reference did not prove actual 720p glyph bounds; v3 is the required corrected evidence.
