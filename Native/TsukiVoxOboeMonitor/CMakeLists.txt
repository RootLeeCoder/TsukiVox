cmake_minimum_required(VERSION 3.22)

project(TsukiVoxOboeMonitor LANGUAGES CXX)

set(CMAKE_CXX_STANDARD 17)
set(CMAKE_CXX_STANDARD_REQUIRED ON)
set(CMAKE_CXX_EXTENSIONS OFF)

set(OBOE_BUILD_TESTS OFF CACHE BOOL "" FORCE)
set(OBOE_BUILD_EXAMPLES OFF CACHE BOOL "" FORCE)
set(OBOE_BUILD_APPS OFF CACHE BOOL "" FORCE)
set(OBOE_BUILD_SHARED OFF CACHE BOOL "" FORCE)

set(OBOE_SOURCE_DIR "${CMAKE_CURRENT_SOURCE_DIR}/third_party/oboe")
if(NOT EXISTS "${OBOE_SOURCE_DIR}/CMakeLists.txt")
    message(FATAL_ERROR "Oboe source was not found. Run build.ps1 to fetch google/oboe tag 1.10.0.")
endif()

add_subdirectory("${OBOE_SOURCE_DIR}" "${CMAKE_CURRENT_BINARY_DIR}/oboe")

add_library(tsukivox_oboe_monitor SHARED
    src/tsukivox_oboe_monitor.cpp
)

target_link_libraries(tsukivox_oboe_monitor
    PRIVATE
        oboe
        c++_shared
        android
        log
)

target_compile_options(tsukivox_oboe_monitor
    PRIVATE
        -Wall
        -Wextra
        -Wpedantic
)
