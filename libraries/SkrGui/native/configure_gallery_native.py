import pathlib,re,json,hashlib,argparse
root=pathlib.Path(__file__).resolve().parents[1]
args=argparse.ArgumentParser();args.add_argument('--engine',default='D:/Code/ExtremeEngine-CppSLJIT');engine=pathlib.Path(args.parse_args().engine).resolve()
cpp=engine/'engine/modules/gui/samples/gallery_common/src/gallery_svg.cpp'
symbols=sorted(set(re.findall(r'\b(nng_\w+)\s*\(',cpp.read_text()))|{'nng_version'})
native=root/'native/gallery';native.mkdir(exist_ok=True)
(native/'exports.def').write_text('LIBRARY skrgui_gallery_native\nEXPORTS\n'+'\n'.join(' '+s for s in symbols)+'\n')
(native/'crt_bridge.c').write_text('''#include <stdlib.h>
#include <errno.h>
__declspec(dllexport) float skrgui_strtof(const char* text, char** end, int* range_error) {
 errno=0; float result=strtof(text,end); *range_error=(errno==ERANGE);return result;
}
''')
lib=engine/'build/.build/clang-cl/Windows-X64-debug/nng.lib'
(native/'CMakeLists.txt').write_text(f'''cmake_minimum_required(VERSION 3.24)
project(SkrGuiGalleryNative LANGUAGES C)
add_library(skrgui_gallery_native SHARED crt_bridge.c exports.def)
target_link_libraries(skrgui_gallery_native PRIVATE "{lib.as_posix()}" ws2_32 mswsock advapi32)
set_target_properties(skrgui_gallery_native PROPERTIES RUNTIME_OUTPUT_DIRECTORY "${{CMAKE_CURRENT_SOURCE_DIR}}/../artifacts/win-x64")
''')
(root/'native/gallery-native-manifest.json').write_text(json.dumps({'package':'NNG','version':'1.11.0','source_package_rule':'engine/packages/nng/build.cs','archive':str(lib),'archive_sha256':hashlib.sha256(lib.read_bytes()).hexdigest(),'exports':symbols,'contains_skrgui_cpp':False},indent=2))
print(len(symbols),'nng ABI exports')
