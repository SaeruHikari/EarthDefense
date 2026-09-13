"""Build only pinned third-party sources; no SkrGui C++ runtime is linked."""
from pathlib import Path
import argparse, re, json, hashlib

parser = argparse.ArgumentParser()
parser.add_argument('--engine', default='D:/Code/ExtremeEngine-CppSLJIT')
args = parser.parse_args()
engine = Path(args.engine).resolve()
native = Path(__file__).resolve().parent
packages = engine / 'engine/packages'
specs = [
    ('freetype', 'text-render/freetype', '2.14.3'),
    ('harfbuzz', 'text-render/harfbuzz', '14.2.1'),
    ('icu', 'text-render/icu', '72.1'),
    ('png', 'file-processing/libpng', '1.6.58'),
    ('zlib', 'runtime-libraries/zlib', '1.3.2'),
    ('tess', 'runtime-libraries/libtess2', '1.0.3'),
]
all_sources = []
manifest = {'source_commit': '611561f81534354c8c1618dc27c4782cd9277cbf', 'contains_skrgui_cpp': False, 'packages': []}
for name, relative, version in specs:
    root = packages / relative
    build = (root / 'build.cs').read_text(encoding='utf-8')
    build = re.sub(r'/\*.*?\*/', '', build, flags=re.S)
    build = re.sub(r'//[^\n]*', '', build)
    patterns = re.findall(r'"([^"\n]+\.(?:c|cc|cpp))"', build)
    sources = []
    for pattern in patterns:
        if name == 'freetype' and pattern in ('freetype/src/base/ftsystem.c', 'freetype/src/base/ftdebug.c'):
            continue
        if name == 'png' and '/arm/' in pattern:
            continue
        pattern = pattern.replace('**.cpp', '**/*.cpp')
        files = sorted(root.glob(pattern)) if '*' in pattern else [root / pattern]
        for file in files:
            if not file.is_file(): raise FileNotFoundError(file)
            if file not in sources: sources.append(file)
    if not sources: raise RuntimeError('No source files for ' + name)
    all_sources += sources
    manifest['packages'].append({'name': name, 'version': version, 'build_rule_sha256': hashlib.sha256((root/'build.cs').read_bytes()).hexdigest(),
        'sources': [{'path': str(f.relative_to(engine)).replace('\\','/'), 'sha256': hashlib.sha256(f.read_bytes()).hexdigest()} for f in sources]})

includes = [
    'text-render/freetype/freetype/include',
    'text-render/harfbuzz/harfbuzz',
    'text-render/icu/icu4c/common', 'text-render/icu/icu4c/i18n', 'text-render/icu/data',
    'file-processing/libpng/1.6.58/include', 'file-processing/libpng/1.6.58/src',
    'runtime-libraries/zlib/1.3.2/include', 'runtime-libraries/zlib/1.3.2/src',
    'runtime-libraries/libtess2/1.0.3/Include', 'runtime-libraries/libtess2/1.0.3/Source',
]
definitions = ['FT2_BUILD_LIBRARY','FT_CONFIG_OPTION_SYSTEM_ZLIB','FT_CONFIG_OPTION_USE_PNG',
    'U_I18N_IMPLEMENTATION','U_COMMON_IMPLEMENTATION','U_STATIC_IMPLEMENTATION',
    'HAVE_FREETYPE','HAVE_ICU','HAVE_ICU_BUILTIN','HAVE_OT',
    'PNG_MIPS_MSA_OPT=0','PNG_MIPS_MMI_OPT=0','PNG_POWERPC_VSX_OPT=0','PNG_LOONGARCH_LSX_OPT=0','PNG_RISCV_RVV_OPT=0',
    'PNG_ARM_NEON_OPT=0','PNG_INTEL_SSE_OPT=1','ZLIB_BUILD','_CRT_SECURE_NO_WARNINGS',
    '_CRT_SECURE_NO_DEPRECATE','_CRT_NONSTDC_NO_DEPRECATE']
quote = lambda p: '"' + str(p).replace('\\','/') + '"'
cmake = ['cmake_minimum_required(VERSION 3.24)', 'project(SkrGuiPinnedNative LANGUAGES C CXX)',
    'set(CMAKE_C_STANDARD 11)', 'set(CMAKE_CXX_STANDARD 20)', 'set(CMAKE_WINDOWS_EXPORT_ALL_SYMBOLS ON)',
    'add_library(skrgui_native SHARED']
cmake += ['  '+quote(f) for f in all_sources]
cmake += [')', 'target_include_directories(skrgui_native PRIVATE']
cmake += ['  '+quote(packages/f) for f in includes]
cmake += [')', 'target_compile_definitions(skrgui_native PRIVATE ' + ' '.join(definitions) + ')',
    'target_compile_options(skrgui_native PRIVATE /utf-8 /wd4005 /wd4267 /wd4244 /wd4804 /wd4805)',
    'if(CMAKE_CXX_COMPILER_ID MATCHES "Clang")',
    'target_compile_options(skrgui_native PRIVATE -Wno-macro-redefined -Wno-deprecated-declarations)', 'endif()',
    'set_target_properties(skrgui_native PROPERTIES RUNTIME_OUTPUT_DIRECTORY "${CMAKE_CURRENT_SOURCE_DIR}/artifacts/win-x64" LIBRARY_OUTPUT_DIRECTORY "${CMAKE_CURRENT_SOURCE_DIR}/artifacts/win-x64")',
    'add_custom_command(TARGET skrgui_native POST_BUILD COMMAND ${CMAKE_COMMAND} -E copy_if_different ' + quote(packages/'text-render/icu/data/icudt72l.dat') + ' "$<TARGET_FILE_DIR:skrgui_native>/icudt72l.dat")']
(native/'CMakeLists.txt').write_text('\n'.join(cmake)+'\n', encoding='utf-8')
(native/'source-manifest.json').write_text(json.dumps(manifest,indent=2)+'\n', encoding='utf-8')
print(f'Configured {len(all_sources)} exact package source files; no SkrGui C++ sources.')
