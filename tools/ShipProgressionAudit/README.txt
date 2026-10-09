Run from PowerShell:
powershell -ExecutionPolicy Bypass -File D:/Game/Tools/ShipProgressionAudit/Summarize.ps1

Optional -Root <artifact root> -Output <report directory>.
Reads bounded UTF-8 snapshots of existing strict-actions.jsonl logs. Never starts Unity or writes Assets/save files.
Automatically includes Baseline-NoSwap/Continue/Finale and all Production-NoSwap/OneSwap/TwoSwap suffix segments.
Outputs UTF-8 summary.txt, branches.csv, segments.csv, transactions.csv, voyage-outcomes.csv, warnings.txt.
Re-run as live logs grow. Missing branches remain unstarted; unfinished logs remain undecided.
Operator stopped at port is an intentional pause with an earned checkpoint, even if finally writes RUN ENDED.
Continue/suffix folders join the original branch automatically. Each segment preserves its stop reason.
The latest segment supplies branch completion/objectives; paid transactions and voyage outcomes accumulate across segments.
OperatorPauses/ResumedSegments count pauses and explicit resume markers; they do not assert unseen save fields are identical.
Market-sale revenue includes earned cargo and is not a matched-freight profit calculation.

Parser/classification self-test (synthetic logs stay under Artifacts):
powershell -ExecutionPolicy Bypass -File D:/Game/Tools/ShipProgressionAudit/SelfTest.ps1
