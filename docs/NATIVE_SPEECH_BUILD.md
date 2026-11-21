# Included speech engine and model

The app ships its speech assets in `assets/whisper`; the source ZIP includes identical files. No startup download is performed. `bundle.json` records the runtime paths and SHA-256 values. The app checks both files before first use each session; Setup can recheck them.

| Component | Pinned origin |
|---|---|
| Whisper.cpp | [ggml-org/whisper.cpp](https://github.com/ggml-org/whisper.cpp), release v1.9.5, build tag b5454, source d1be6fd |
| Model | [ggerganov/whisper.cpp](https://huggingface.co/ggerganov/whisper.cpp), revision 5359861c739e955e79d9a303bcbc70fb988958b1, ggml-base-q5_1.bin |
| Windows compiler | [LLVM-MinGW](https://github.com/mstorsjo/llvm-mingw/releases/tag/20261104), 20261104 UCRT Ubuntu 22.04 x86_64 toolchain |
| Model and engine license | MIT; complete notices in assets/whisper/licenses, with LLVM, libc++, unwind and MinGW notices |

The Windows CLI is cross-built from pinned upstream source with a statically linked C++ runtime, OpenMP disabled and CPU-specific AVX/FMA optimizations disabled. Its inspected imports are Windows system API/UCRT DLLs; no separate MSVC or MinGW runtime installer is required. GPU acceleration is disabled by the app. Source patch: explicitly include `<algorithm>` in `examples/common.cpp` for libc++.

Maintainers can reproduce the build on Linux using `python3 scripts/build-bundled-speech.py` after installing CMake (or the documented local pip target), GNU make and objdump. This maintainer operation downloads pinned source/toolchain/model artifacts and verifies their hashes before extraction. End users do not run it. `scripts/Prepare-Speech.ps1` only checks the already included package.

`static-build.json` records exact upstream download hashes, compilation flags and inspected imports. SHA-256 integrity is not code signing; this is an unsigned development release.

| Shipped file | SHA-256 |
|---|---|
| windows-x64/whisper-cli.exe | `d80d8e26fa486b4ca0a7a5d30273f49eb6012fd53e75d13f50a5a048ac18ac20` |
| models/ggml-base-q5_1.bin | `422f1ae452ade6f30a004d7e5c6a43195e4433bc370bf23fac9cc591f01a8898` |

Real inference with this exact model and the corresponding official Linux CLI recognized the sample phrase. The Windows binary was compiled and inspected, but could not be executed on the Linux build host. CI includes a Windows native sample test; it has not been run in a connected repository during this delivery. Native device and UI checks remain in WINDOWS_SMOKE_TEST.md.

Whisper recognizes multiple spoken languages and translates audio into English. It does not translate English audio into arbitrary target languages; those comparisons use the optional language model. Short/noisy clips and automatic language detection require review. The sample success is an integration check, not a quality benchmark.
