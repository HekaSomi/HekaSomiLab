"""Maintainer-only Linux rebuild. End users receive the compiled engine and model.

Requires Python 3.12+, CMake (or `python3 -m pip install --target artifacts/build-tools cmake`),
GNU make and objdump. Downloads are pinned and SHA-256 checked before extraction.
"""
from pathlib import Path
from concurrent.futures import ThreadPoolExecutor
import hashlib, json, os, shutil, subprocess, tarfile, urllib.request

ROOT = Path(__file__).resolve().parents[1]
WORK = ROOT / 'artifacts/upstream'
ASSETS = ROOT / 'assets/whisper'
WORK.mkdir(parents=True, exist_ok=True)
ASSETS.mkdir(parents=True, exist_ok=True)
DOWNLOADS = {
    'toolchain.tar.xz': ('https://github.com/mstorsjo/llvm-mingw/releases/download/20261104/llvm-mingw-20261104-ucrt-ubuntu-22.04-x86_64.tar.xz', '919380403de87dca3c581c2053230cd298af5e891286628ba0613b7fed370926'),
    'source.tar.gz': ('https://api.github.com/repos/ggml-org/whisper.cpp/tarball/b5454', 'be49b097b23e24283c1070e6df34337134757e9be05a6525e3c526d943051adb'),
    'linux.tar.gz': ('https://github.com/ggml-org/whisper.cpp/releases/download/b5454/whisper-bin-ubuntu-x64.tar.gz', 'a72becf15d7917f990f6313867a52638b82b7f9ef237fb0c980dac56a135781c'),
    'ggml-base-q5_1.bin': ('https://huggingface.co/ggerganov/whisper.cpp/resolve/5359861c739e955e79d9a303bcbc70fb988958b1/ggml-base-q5_1.bin', '422f1ae452ade6f30a004d7e5c6a43195e4433bc370bf23fac9cc591f01a8898'),
}
# Model checksum is the upstream LFS SHA-256, not the repository commit hash.

def digest(path):
    with path.open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()

def download(item):
    name, (url, expected) = item
    path = WORK / name
    if not path.exists() or digest(path) != expected:
        with urllib.request.urlopen(url, timeout=90) as response, path.open('wb') as target:
            shutil.copyfileobj(response, target)
    actual = digest(path)
    if actual != expected:
        raise RuntimeError(f'Checksum mismatch for {name}: {actual}')
    print(f'Verified {name}', flush=True)

with ThreadPoolExecutor(max_workers=4) as pool:
    list(pool.map(download, DOWNLOADS.items()))

for name, folder in [('source.tar.gz', 'source'), ('toolchain.tar.xz', 'toolchain'), ('linux.tar.gz', 'linux')]:
    target = WORK / folder
    if not target.exists():
        target.mkdir()
        with tarfile.open(WORK / name) as archive:
            archive.extractall(target, filter='data')
source = next((WORK / 'source').iterdir())
toolchain = next((WORK / 'toolchain').iterdir())
common = source / 'examples/common.cpp'
if '#include <algorithm>' not in common.read_text():
    common.write_text('#include <algorithm>\n' + common.read_text())
cmake = shutil.which('cmake') or str(ROOT / 'artifacts/build-tools/cmake/data/bin/cmake')
build = WORK / 'windows-static'
options = ['BUILD_SHARED_LIBS=OFF', 'GGML_NATIVE=OFF', 'GGML_OPENMP=OFF', 'GGML_AVX=OFF', 'GGML_AVX2=OFF', 'GGML_AVX512=OFF', 'GGML_FMA=OFF', 'GGML_F16C=OFF', 'WHISPER_BUILD_TESTS=OFF', 'WHISPER_SDL2=OFF', 'CMAKE_EXE_LINKER_FLAGS=-static']
command = [cmake, '-S', str(source), '-B', str(build), '-DCMAKE_BUILD_TYPE=Release', '-DCMAKE_SYSTEM_NAME=Windows', '-DCMAKE_SYSTEM_PROCESSOR=AMD64', f'-DCMAKE_C_COMPILER={toolchain}/bin/x86_64-w64-mingw32-clang', f'-DCMAKE_CXX_COMPILER={toolchain}/bin/x86_64-w64-mingw32-clang++', f'-DCMAKE_RC_COMPILER={toolchain}/bin/x86_64-w64-mingw32-windres'] + ['-D'+x for x in options]
subprocess.run(command, check=True)
subprocess.run([cmake, '--build', str(build), '--target', 'whisper-cli', '-j', '4'], check=True)
engine = ASSETS / 'windows-x64/whisper-cli.exe'
model = ASSETS / 'models/ggml-base-q5_1.bin'
engine.parent.mkdir(parents=True, exist_ok=True)
model.parent.mkdir(parents=True, exist_ok=True)
shutil.copy2(build / 'bin/whisper-cli.exe', engine)
shutil.copy2(WORK / 'ggml-base-q5_1.bin', model)
imports = [line.split('DLL Name:')[1].strip() for line in subprocess.check_output(['objdump','-p',str(engine)], text=True).splitlines() if 'DLL Name:' in line]
if any(not (name.lower().startswith('api-ms-win-') or name.lower() in {'kernel32.dll', 'advapi32.dll', 'user32.dll', 'bcrypt.dll', 'msvcrt.dll', 'ntdll.dll'}) for name in imports):
    raise RuntimeError(f'Unexpected non-system DLL dependency: {imports}')
licenses = ASSETS / 'licenses'
licenses.mkdir(exist_ok=True)
shutil.copy2(source / 'LICENSE', licenses / 'whisper.cpp-LICENSE.txt')
with urllib.request.urlopen('https://raw.githubusercontent.com/openai/whisper/main/LICENSE') as response:
    (licenses / 'Whisper-model-MIT.txt').write_bytes(response.read())
for file in toolchain.rglob('*'):
    if file.is_file() and ('license' in file.name.lower() or file.name.lower().startswith('copying')) and file.stat().st_size < 2_000_000:
        target = licenses / 'toolchain' / file.relative_to(toolchain)
        target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(file, target)
manifest = {'version': 'whisper.cpp-v1.9.5-hekasomi-static-1', 'modelName': 'Whisper Base multilingual (Q5_1)', 'executable': 'windows-x64/whisper-cli.exe', 'model': 'models/ggml-base-q5_1.bin', 'sha256': {'windows-x64/whisper-cli.exe': digest(engine), 'models/ggml-base-q5_1.bin': digest(model)}}
(ASSETS / 'bundle.json').write_text(json.dumps(manifest, indent=2)+'\n')
(ASSETS / 'static-build.json').write_text(json.dumps({'tag': 'b5454', 'stableVersion': 'v1.9.5', 'compiler': 'LLVM-MinGW 20261104 UCRT', 'configuration': options, 'patch': 'Added #include <algorithm> to examples/common.cpp.', 'downloads': DOWNLOADS, 'directWindowsImports': imports, 'exeSha256': digest(engine)}, indent=2)+'\n')
print('Bundled Windows engine and multilingual model ready.', flush=True)
