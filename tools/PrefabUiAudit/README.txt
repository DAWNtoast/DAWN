Port UI prefab/cache audit

Copy PortUiPerformanceProbe.cs into the isolated Unity project's existing
Assets/_Project/Tests/PlayMode directory. Do not import it into production.
It shares CoreGameplayDemoSceneTests' profile/checkpoint isolation helpers.
The same source compiles against the pre-optimization controller and the
optimized controller. Run each against a separate new output directory.

Required Unity arguments:
  -batchmode -projectPath <isolated project> -runTests -testPlatform PlayMode
  -testFilter PortUiPerformance_MeasureMarketWarehouseAndFacilityCallbacks
  -testResults <new XML path> -logFile <new log path>
  -portUiPerformanceOutput <new JSON path>

Optional screenshots:
  -navalVisualCapturePath <new capture directory>
This reuses NavalBattleVisualCapture and produces a 1920 x 1080 PNG after
each scenario, outside the timed section. A GPU is required for captures;
do not supply -nographics when capturing.
The probe waits 0.35 real-time seconds after all scenario samples and before
capture so the screen transition is fully opaque. This wait is not measured.
The exact probe used for the original timing JSON is preserved at
Artifacts/PrefabUiOptimization20261009/MeasuredPortUiPerformanceProbe.cs.

Optional comma-separated scenario selection:
  -portUiPerformanceScenarios warehouse_catalog_6_pages,market_storage_mode_3_changes,market_buy_sell_wood,facility_cycle_10_changes

Scenarios each have 3 warmups followed by 12 recorded samples:
* warehouse_catalog_6_pages: open the full collection catalog,
  click next three times and previous three times, returning to page 1.
* market_storage_mode_3_changes: cycle Ship -> Warehouse -> Ask -> Ship.
  The actual game displays buy and sell controls simultaneously; it has
  no buy/sell tab to switch.
* market_buy_sell_wood: buy one wood, sell that wood; reset fixture gold,
  cargo and the current month's purchased count outside each sample.
  Assert the real final asset values and exactly one recorded purchase.
* facility_cycle_10_changes: start at Market and click Next Port Facility
  ten times, visiting every facility and returning to Market.

Timing and allocation cover only Button.onClick.Invoke plus forced Canvas
layout. Lookup, assertions, fixture reset, waits, inventories and captures
are outside timing. One frame separates operations so destroyed baseline
objects are not left queued across the next callback. Deferred destruction
time and GPU work are not measured. Current-thread managed bytes are checked
with a known allocation; -1 indicates unavailable. This is not an FPS test.
pageReplacements is -1 for facility switches where changing page bodies is
expected. Header/backdrop replacements remain comparable across scenarios.

Permanent optimized behavior tests:
  Assets/_Project/Tests/PlayMode/PortChromeReuseTests.cs
  -testFilter PortChrome_
They use reflection to skip only when the pre-optimization implementation
lacks the cachedFacilityHeader field. After optimization they require the
real retained instances, correct navigation, active-page gold/port updates,
and exactly one navigation action after repeated header reuse.

Static validation (no Unity process launched by the audit author):
  Artifacts/PrefabUi20261009/static-probe/Probe.rsp
  Artifacts/PrefabUi20261009/static-probe/Chrome.rsp
Both compiled successfully against the existing isolated project's current
Bee assembly references and its full pre-optimization PlayMode test sources.
