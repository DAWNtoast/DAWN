Code structure review and Windows release, 2026-10-16.

The reusable desktop release workflow is tools/ReleaseBuild/DesktopRelease.ps1.
Validation and build use the same isolated source snapshot. Historical release
scripts and their original evidence remain available in their dated directories.

The completed Windows release uses frozen snapshot Release04 and version 0.3.20261016.3.
Its completed full-suite outputs are FinalEdit04 and FinalPlay04 under
Artifacts/CodeStructureRelease20261016. Each RunTests invocation must supply
-SourceManifest with Release04/source-manifest.csv. Verify.ps1 requires both
full suites, their unchanged XML files, and matching before/after input hashes.
Release04 full-suite verification passed at 2026-10-16T21:16:06.2331163Z.
FinalEdit04: 5,584 total; 5,539 passed; 0 failed; 45 skipped; exit 0; 143.573 s.
FinalPlay04: 524 total; 516 passed; 0 failed; 8 skipped; exit 0; 2,037.981 s.
Combined: 6,055 passed and 53 retired/opt-in skips. Both suites recorded PASS
input validation before and after execution. These are complete reruns against
Release04, not a merged total from earlier snapshots or targeted reruns.
Windows x86_64 / Mono build succeeded in 110.955 s with 0 errors and 0 warnings.
All 880 resource images were referenced in the actual build log. The packaged
player smoke passed with exit 0 in 7.410 s, confirming version 0.3.20261016.3
and production identity ShatteredSail / Shattered Sail without changing saves.
Package and Seal passed; all 178 ZIP files matched their source SHA256 hashes.

Frozen source inputs: 4,371 files; isolated project inputs: 4,373 files.
Source manifest SHA256:
A0E9AE9283F4A1EC638DA1552E66B6F57426982181213F73C81D959C5CFB7EAF
Isolated project input manifest SHA256:
6FB18250404685DCBC7694B3BA47636E9E2B683AB75EECBD6BC598882B91EE85
FinalEdit04/results.xml SHA256:
EC110A0F38EE8C7E577DC4CDAF7AF757C116EA7E332FF5C20E70B16E0F73139A
FinalPlay04/results.xml SHA256:
FB935A84FB029A6509354D553374187CBC88F85C70C46A7630003B8322C93C5D

Order: Snapshot -> full EditMode -> full PlayMode -> Verify.ps1 -> Windows ->
Smoke -> add the package's release notes -> Package -> Seal.
Release.ps1 exposes the named build stages. Verify.ps1 takes -EditRun and
-PlayRun as absolute paths to the two preserved result directories.
Pass -SnapshotName Release04 explicitly to Release.ps1 and Verify.ps1.
Do not create the Windows output directory in advance; the builder owns it.

Available Windows executable:
Builds/第七港口-代码结构优化20261016/Windows/第七港口.exe
Available ZIP:
Builds/第七港口-代码结构优化20261016/第七港口-Windows-20261016-代码结构优化.zip
ZIP bytes: 1,797,504,413; uncompressed bytes: 1,985,901,107.
ZIP SHA256:
4F12AAD332503EBA9518EA1BA42085921FCBAFDB7C18B59B3280FBF747C0B855
Executable SHA256:
82EFD5A8C297A5046F6BB72C240DAB8972E64003851935EBA0A46AD121CE0305
Final seal: Builds/第七港口-代码结构优化20261016/release-manifest.json (PASS).
Extract the complete ZIP and run Windows/第七港口.exe; retain its adjacent files.
Scope, exclusions and final results:
docs/CodeStructureRelease20261016.txt

Existing source snapshots, test runs and players are never overwritten.
For a future version, call the reusable scripts with new evidence, test-output
and release directories and an explicit version. The dated wrapper records
this release's exact paths; it is not an overwrite-in-place build command.
