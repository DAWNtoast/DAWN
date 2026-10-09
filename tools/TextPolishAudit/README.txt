TextPolishPreviewSync — temporary isolated-project scene text synchronizer

Import TextPolishPreviewSync.cs (+meta) into the isolated project's existing
Assets/_Project/Editor directory, beside the partial GameplayPreviewSceneBuilder.
Do not install this tool in the main project. It explicitly rejects execution
when Application.dataPath belongs to D:/Game/UnityClient.

Entry point:
  ShatteredSail.Editor.TextPolishPreviewSync.SyncFromCommandLine

Required absolute-path arguments:
  -textPolishArtifactsPath D:/Game/Artifacts/TextPolish20261008
  -textPolishOutputPath D:/Game/Artifacts/TextPolish20261008/PreviewSync01

The artifacts directory must contain four JSON arrays:
  root-text-replacements.json
  ui-text-replacements.json
  crew-text-replacements.json
  item-text-replacements.json
Each element has string fields {"old": ..., "new": ...}. Empty old/null new
is rejected; an empty new is allowed. Duplicate identical pairs coalesce.
Same old with different new is a hard failure before any writes.

Output must be a fresh/empty child of the artifacts directory. A prior run,
backup or report is never overwritten. Optional -textPolishValidateOnly makes
the same full plan and backups/report without changing scene bytes.

Scope and guarantees:
* Scans exactly the audited 189 Assets/_Project/Scenes/**/*.unity files.
* Only legacy Unity UI Text components (the actual Text script GUID plus
  m_FontData) qualify. Hidden/inactive Text is included. TMP/custom component
  data, object names, transforms, images, styles, scripts and callbacks do not.
* Does not open or save scenes through Unity, rebuild objects or refresh assets.
* Parses m_Text value spans including double/single quoted and plain multiline
  YAML. Handles Unity Unicode escapes, \xB7, \0, literal whitespace, escaped
  line continuations and unescaped YAML folding. Unsupported block scalars or
  malformed input fail before writes. No block scalar was found in this audit.
* Exact whole-value mappings apply even for short labels. Otherwise a single
  left-to-right scan chooses the longest old at each position; old shorter
  than eight UTF-16 characters never substitutes inside a longer value.
  Newly emitted text is not rescanned.
* For a dossier containing Tavern Crew Detail Name, the displayed name must
  resolve to a current crew. Within the same Tavern Crew Detail ancestor, the
  tool sets Tavern Crew Biography using the actual private FormatCrewBiography
  method and Tavern Crew Story using the actual StoryFor(crew).Body. This fixes
  stale authored biographies/chapters that predate the replacement catalog.
  Missing/ambiguous crew ownership is a hard failure, not a broad guess.
* Re-parses the planned output and verifies exact intended text. Masking only
  each UI m_Text value span must leave byte-identical UTF-8, including BOM and
  every non-value character. Unchanged m_Text is left byte-identical too.
* All scenes are prepared before any writes. Every scene gets a CreateNew
  before/Assets/_Project/Scenes/... byte backup. Each changed source is checked
  against its original again before write and read back for exact verification.
* report.json includes each scene's backup path, before/after SHA-256, masked
  non-text SHA-256, Text counts, changed fields, mapped occurrences and forced
  crew fields. A partial I/O failure records status=failed and verified writes;
  originals remain in the backups. No main-project sync is performed here.

Validation performed outside Unity:
* Actual entire Editor assembly plus this tool passed static csc compilation.
* Artifact-only standalone .NET harness runs the same parser, replacement and
  189-scene planning methods, with actual extracted encoder/formatter methods.
  Its Unity shell/JSON adapter is stubbed; crew strings are read from current
  source. This validates parsing and byte invariants, not Unity rendering or
  runtime JsonUtility. The root agent owns the real Unity invocation.
* Harness and read-only scene reports are under
  Artifacts/TextPolish20261008/static/parser-harness.
