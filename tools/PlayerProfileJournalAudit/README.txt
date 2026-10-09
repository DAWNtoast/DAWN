ProfileSaveJournal controlled process-interruption audit

Run.ps1 builds an isolated Windows Development Player under D:/Game/tmp, with
company/product ShatteredSailAudit/Optimization20261008. Runtime workers use
-batchmode -nographics: this is a persistence audit, not a rendering benchmark.

The runner injects stop hooks only into isolated source copies, restores both
source files in finally, and records source/binary hashes. Production assets
do not contain these hooks. Each process has a unique report path and verified
PID, mode, company, product and stop status. Finished probes deliberately wait
for termination through the runner's retained process handle. The runner checks
the OS process inventory after termination before starting the next worker.
These are controlled terminations, not orderly Unity shutdowns or power-loss tests.

Cases: immediately after durable journal creation; after chart fields are queued;
after the final preferences flush; and interruption while replaying recovery.
Each case inspects raw disk preferences separately, calls the production recovery
before scene Awake, repeats recovery in another process, performs a completed
control save, then verifies that completed save after another restart.
All 100 typed protected preferences and 20 readable profile/derived values are
compared against their exact pre-SaveProgress group or completed control group.
The explicit seeded fixture is not evidence of earned/no-GM gameplay progression.

Verified run: Artifacts/SequentialOptimization20261008/02-Stability/
ProfileJournal-headless/summary.json (4/4).
Earlier ProfileJournal* diagnostic attempts are retained. Native ExitProcess
proved unreliable on this host; that helper strategy was removed after owned
process cleanup. Earlier failures must not be interpreted as production failures.

prepare_probe.py and prepare_runner.py are the source generators. Regenerate
both C#/PowerShell outputs after changing either generator, then validate syntax
and build/run anew. ExistingBuild verifies original executable/DLL hashes and
uses an existing diagnostic build in place; always use a fresh output directory.
