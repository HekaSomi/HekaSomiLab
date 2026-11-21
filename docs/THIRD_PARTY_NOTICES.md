# Third-party notices

Application source is supplied for the HekaSomi project; this delivery does not select an open-source distribution license for your company's application. Third-party components retain their own licenses.

| Component | Use | Upstream |
|---|---|---|
| .NET / WPF | Runtime and desktop framework | https://github.com/dotnet/runtime and https://github.com/dotnet/wpf |
| CommunityToolkit.Mvvm | Observable state and commands | https://github.com/CommunityToolkit/dotnet |
| NAudio | Windows recording/playback | https://github.com/naudio/NAudio |
| Microsoft.Data.Sqlite.Core | SQLite access | https://github.com/dotnet/efcore |
| SQLitePCLRaw | Native SQLite binding/configuration | https://github.com/ericsink/SQLitePCL.raw |
| SQLite | Embedded database | https://sqlite.org/copyright.html |
| xUnit / Microsoft.NET.Test.Sdk | Source-project tests only | https://github.com/xunit/xunit and https://github.com/microsoft/vstest |
| IPIP | Public-domain questionnaire items | https://ipip.ori.org/newPermission.htm |
| FFmpeg / Flite | Generated synthetic demo speech; engines are not bundled | https://ffmpeg.org/ and https://github.com/festvox/flite |

Package-supplied license files and metadata are copied into `docs/licenses`. The Windows package also redistributes the .NET runtime. Source and binary licenses should be reviewed when preparing a public signed release. Whisper.cpp and the Whisper Base multilingual Q5_1 model are bundled. Their MIT licenses and static-toolchain notices are in `assets/whisper/licenses`. No Ollama server or large language model is included. See [NATIVE_SPEECH_BUILD.md](NATIVE_SPEECH_BUILD.md) for provenance.
