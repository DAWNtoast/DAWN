# Runtime boundaries and desktop release

`Core` owns deterministic game rules and has `noEngineReferences: true`.
`Content` supplies authored identities and descriptions. `Persistence` contains
serialized save contracts. `Presentation` adapts those rules to Unity UI,
input, audio and rendering. `Editor` owns import checks and player builds.

The runtime controller remains a partial class so existing scene and prefab
references keep their GUIDs. Its six Unity lifecycle entry points now live in
`Presentation/Runtime/CoreGameplayLifecycle.cs`: startup, enable, frame update,
pause, disable and destruction. Feature-specific operations remain in their
existing files; `CoreGameplayResourceMemory.cs` owns generated visual cleanup
and deferred unloading. Preserve lifecycle call order when adding a subsystem.

Market inventory access uses the basic-item subset of `CoreWarehouseItemCatalog`.
That boundary matters: warehouse-only treasure, relics and cargo must not become
ordinary market supply items. Keep market display labels and unsupported-item
behavior compatible when extending the catalog.

`WindowsReleaseBuilder` and `MacReleaseBuilder` retain their public batch entry
points and delegate shared policy to `DesktopReleaseBuilder`. Pass
`-releaseOutput` with a fully qualified path in a new/empty directory and
`-releaseVersion` with a numeric release version. Both targets use the same two
player scenes, asset metadata checks, save identity and strict build settings.
Player settings are restored in `finally`; macOS additionally restores its
architecture setting. Omitting the version retains the historical default for
old callers, so new release automation should always supply it explicitly.

`tools/ReleaseBuild/DesktopRelease.ps1` snapshots source into an existing isolated
project, verifies checks against that snapshot, builds, runs the packaged-player
smoke check and verifies every ZIP entry. It does not rewrite the C# builder to
set a version. `tools/ReleaseBuild/RunTests.ps1` uses graphics for both suites,
because some EditMode tests render real UI. Validation uses a separate save
identity. The retired `UIPreviewsForMe` directory must remain empty and is never
copied, regenerated or refreshed by this workflow.
