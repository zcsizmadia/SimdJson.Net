cmake_minimum_required(VERSION 3.15)

# CMP0091 NEW: honour CMAKE_MSVC_RUNTIME_LIBRARY instead of patching flags.
# Must be set before the first project() call.
cmake_policy(SET CMP0091 NEW)

project(SimdJsonNative LANGUAGES CXX C)

# ─── C++ standard ────────────────────────────────────────────────────────────
set(CMAKE_CXX_STANDARD 17)
set(CMAKE_CXX_STANDARD_REQUIRED ON)
set(CMAKE_POSITION_INDEPENDENT_CODE ON)

# ─── MSVC CRT selection (must happen before FetchContent) ────────────────────
# Use the STATIC CRT (/MT / /MTd) for every target in this project, including
# the simdjson static library pulled in via FetchContent.  Both the archive and
# the bridge DLL must agree on the CRT; mixing /MD and /MT causes LNK2038.
if(MSVC)
    set(CMAKE_MSVC_RUNTIME_LIBRARY
        "MultiThreaded$<$<CONFIG:Debug>:Debug>"
        CACHE STRING "MSVC runtime library" FORCE)
endif()

# ─── 1. Fetch simdjson ───────────────────────────────────────────────────────
# simdjson is fetched at configure time and compiled as a STATIC library that
# gets fully linked into (and hidden inside) our SHARED bridge library.
include(FetchContent)
FetchContent_Declare(
    simdjson
    GIT_REPOSITORY https://github.com/simdjson/simdjson.git
    GIT_TAG        v4.6.3        # matches SimdJsonVersion in Directory.Build.props
    GIT_SHALLOW    TRUE
)

# Tell simdjson's own CMake to produce a static archive only.
set(BUILD_SHARED_LIBS         OFF CACHE BOOL "" FORCE)  # make simdjson build static
set(SIMDJSON_DEVELOPER_MODE   OFF CACHE BOOL "" FORCE)
# Enable the experimental incomplete-JSON parsing API used by SimdJsonNative_ParseAllowIncompleteJson.
add_compile_definitions(SIMDJSON_EXPERIMENTAL_ALLOW_INCOMPLETE_JSON)

FetchContent_MakeAvailable(simdjson)

# ─── 2. Bridge shared library ────────────────────────────────────────────────
add_library(SimdJsonNative SHARED src/simdjson_native.cpp)

# Expose only simdjson_native.h symbols; hide all simdjson internals.
target_include_directories(SimdJsonNative
    PUBLIC  $<BUILD_INTERFACE:${CMAKE_CURRENT_SOURCE_DIR}/include>
    PRIVATE ${simdjson_SOURCE_DIR}/include
)

# Link simdjson statically into the bridge .so / .dll.
target_link_libraries(SimdJsonNative PRIVATE simdjson)

# ─── 3. Platform-specific flags ──────────────────────────────────────────────
if(MSVC)
    # /EHsc  – standard C++ exception handling
    # /W4    – high warning level
    # /GL    – whole-program optimisation (link with /LTCG below)
    # CRT is already set to /MT|/MTd via CMAKE_MSVC_RUNTIME_LIBRARY above.
    target_compile_options(SimdJsonNative PRIVATE
        /W4 /EHsc
        $<$<CONFIG:Release>:/GL>
    )
    target_link_options(SimdJsonNative PRIVATE $<$<CONFIG:Release>:/LTCG>)

    if(CMAKE_SIZEOF_VOID_P EQUAL 4)
        # x86: at minimum SSE2 (simdjson falls back gracefully on older CPUs
        # via runtime dispatch, so this is just the compile-time floor).
        target_compile_options(SimdJsonNative PRIVATE /arch:SSE2)
    endif()
else()
    # GCC / Clang on Linux, macOS, Android …
    target_compile_options(SimdJsonNative PRIVATE
        -Wall -Wextra
        # Hide all symbols by default; only BRIDGE_API ones are exported.
        -fvisibility=hidden
        -fvisibility-inlines-hidden
    )

    if(APPLE)
        # macOS: use -dead_strip instead of --exclude-libs
        target_link_options(SimdJsonNative PRIVATE -dead_strip)
    else()
        # Linux / Android: discard unreferenced sections and hide
        # all symbols pulled in from the static simdjson archive.
        target_link_options(SimdJsonNative PRIVATE
            -Wl,--exclude-libs,ALL
            -Wl,--as-needed
        )
    endif()
endif()

# ─── 4. Output naming ────────────────────────────────────────────────────────
# Remove the default "lib" prefix on Linux/macOS so the output is
# always SimdJsonNative.dll / SimdJsonNative.so / SimdJsonNative.dylib.
set_target_properties(SimdJsonNative PROPERTIES
    PREFIX      ""
    OUTPUT_NAME "SimdJsonNative"
)

# ─── 5. Install rules ────────────────────────────────────────────────────────
# DESTINATION "." means files land directly in CMAKE_INSTALL_PREFIX with no
# bin/ or lib/ subdirectory.  In CI, --prefix is set to
#   SimdJson.Net/runtimes/<runtimeId>/native
# so the DLL/so/dylib ends up exactly where the NuGet layout expects it.
install(TARGETS SimdJsonNative
    RUNTIME DESTINATION .   # .dll on Windows
    LIBRARY DESTINATION .   # .so / .dylib on Linux / macOS
)
