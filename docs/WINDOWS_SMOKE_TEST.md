# Windows acceptance checks — version 0.3

These checks require Windows 11 x64 and actual devices. Status is Pending because the build host was Linux. Use a clean extracted folder; keep the previous version for comparison. Record hardware, Windows version, results and any reproduction steps.

| Check | Expected result | Status |
|---|---|---|
| Start with no .NET SDK, Whisper or Ollama installed | App opens on Start; included speech is available | Pending |
| Try sample, with networking disabled | Real transcript contains “Please do not cancel my reservation” | Pending |
| Extract into a path containing spaces | Sample runs successfully | Pending |
| Run without Visual C++ redistributable installed | Included speech engine uses Windows system components | Pending |
| Record, stop, play, transcribe | Default microphone selected; level/timer update; transcript is editable | Pending |
| Allow recording to reach two minutes | Stops and prepares audio automatically | Pending |
| No microphone / microphone permission denied | Actionable message; file import remains available | Pending |
| Import 44.1/48 kHz stereo WAV and MP3 | Automatic conversion; original hash unchanged | Pending |
| Unsupported, empty, oversized or overlong audio | Clear error; no partial imported copy retained | Pending |
| Transcribe non-English speech and translate it to English | Distinct original/English outputs; bilingual review of quality | Pending |
| Cancel active inference | Child process ends, temporary output removed, UI remains usable | Pending |
| Copy and save transcript | Text matches reviewed output | Pending |
| Damage a copied model; Check speech package | Integrity error with recovery guidance | Pending |
| Optional Ollama unavailable / no models / connected | Helpful setup state; Start continues to work | Pending |
| New personality; switch profile or close with unsaved text | Discard prompt or block; answer is not silently lost | Pending |
| Record an interview without Ollama | Included speech transcribes; encrypted evidence can be saved | Pending |
| IPIP responses, approval and revision | Skip/1–5 choices; incomplete domains remain unscored | Pending |
| Revoke draft / approved / already revoked | Only approved version can transition to revoked | Pending |
| Compare, export, inspect full translation, reopen history | Results/reviews load; open-folder shortcut works | Pending |
| Reopen a saved workspace from 0.1 | Same user's encrypted profiles, runs and reviews remain readable | Pending |
| Close/reopen, delete construct, review retained exports | Data lifecycle matches README | Pending |
| Approve a profile; accept or decline the optional test invitation | Participation opens with that approved version, or normal editing continues | Pending |
| Complete practice test; skip a question; seal twice | Incomplete answers rejected; one frozen session per version/instrument; resume existing session | Pending |
| Preview each condition; change provider or model | Exact shared context visible; fresh sharing confirmation required | Pending |
| Compare through each live provider with an authorized developer key | Real responses recorded, actual model ID retained when returned; no keys in reports and no human answers in inference requests | Pending |
| Cancel mid-test; resume the same settings | Completed results retained; only missing questions requested | Pending |
| Export task; import valid, tampered and duplicate answer files | Valid result accepted once; wrong packet/invalid choice/overwrite rejected | Pending |
| Revoke profile during an active request | Late result rejected; no more inference starts for that version | Pending |
| Import synthetic fixture | 50% match, 100% coverage; explicitly unverified/synthetic; no global average inferred | Pending |
| Connect to staging platform with read-only Lab token | Authorized reports loaded; matching cohort checked; no global aggregate below minimum cohort size | Pending |
| Remove server record; fetch newer complete snapshot | Removed record disappears from online snapshot; independent file imports remain clearly labeled | Pending |
| Large fonts, keyboard navigation, narrow window and long option text | Participation controls remain usable without clipping or inaccessible actions | Pending |

Use consenting test data. A native smoke pass verifies usability and integration, not psychological fidelity or translation quality. Human acceptance remains separate from engineering checks.
