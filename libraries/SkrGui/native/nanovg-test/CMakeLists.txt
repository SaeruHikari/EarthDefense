cmake_minimum_required(VERSION 3.20)
project(SkrGuiNanoVGTest LANGUAGES CXX)
add_library(skrgui_nanovg_test SHARED "D:/Code/ExtremeEngine-CppSLJIT/engine/packages/nanovg/nanovg/nanovg.cpp")
set_target_properties(skrgui_nanovg_test PROPERTIES CXX_STANDARD 20 WINDOWS_EXPORT_ALL_SYMBOLS ON RUNTIME_OUTPUT_DIRECTORY "${CMAKE_CURRENT_SOURCE_DIR}/../artifacts/win-x64")
