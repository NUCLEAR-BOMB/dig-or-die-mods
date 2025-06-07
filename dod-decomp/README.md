
# `DigOrDie.dll` Decompilation

In `Dig or Die/DigOrDie_Data/Plugins` exists file `DigOrDie.dll`.
This library is used by the game to calculate force, light, electricity and liquid mechanics extremely fast using multithreaded native code.

`DigOrDie.dll` is a 32-bit PE32 dynamic library that was compiled using `Microsoft Visual C/C++ (19.16.27026)` and linker `Microsoft Linker (14.16.27026)` (provided by [**Detect It Easy**](https://github.com/horsicq/Detect-It-Easy)).
Checksum SHA512: `280ed13e02518a1011c62fa4c059bbf2f6067d58ffcae669b325deac49a02bd960e428c8823e18932b01dda420f69b7182e2eb0cf9e855a24ba8e970d40f366e`.

The goal of this project is to reverse engineer and basically decompile `DigOrDie.dll` into readable C++ code.
It targets the **functional matching** accuracy (basically, we're not trying to produce extract bytes or instructions that are in original `.dll`).
Though, this means that the final result may have some deviations from original that are very hard to detect.

## Exported functions

| Function                   | When used                                       | What does                                                                               |
| -------------------------- | ----------------------------------------------- | --------------------------------------------------------------------------------------- |
| `DllClose`                 | On application exit                             | Stops all threads                                                                       |
| `DllGetSaveOffset`         | In save file saving and loading                 | Pseudo-randomly generates offsets for serializing/deserializing save file world data    |
| `DllInit`                  | On application start                            | Initializes 4 threads and seeds random number generator                                 |
| `DllProcessElectricity`    | On simulation update                            | Processes electricity interactions in the world                                         |
| `DllProcessForces`         | On simulation update                            | Processes force mechanic interactions                                                   |
| `DllProcessLightingSquare` | On simulation update                            | Processes lighting interactions in the world                                            |
| `DllProcessWaterMT`        | On simulation update                            | Processes all liquids in the world                                                      |
| `DllResetSimu`             | On world creation/loading                       | Prepares state for simulation                                                           |
| `DllSetCallbacks`          | On application start                            | Prepares functions for printing to console and calculating electricity procured by item |
| `GetBestSpawnPoint`        | On need to calculate a spawn point for a player | Computes best spawn point for player                                                    |

## Notes

- Uses WinAPI to create, manage and synchronize threads.
- The game **always** uses 4 threads but the library supports up to 32 threads (contains an array of 32 elements of object thread state).
- A lot of weird generated code:
  - Uses of division/modulo identity operations which basically does nothing except reducing performance.
  - Uses of signed integers where unsigned would make more sense and improve performance in some cases.
  - Internal functions have unconventional calling conversion (optimization by MSVC?).
- Tracks a lot of global state. Some even are initialized and updated from innocent functions `DllProcessWaterMT` and `DllProcessLightingSquare`.

# Building

## Prerequisites

- [CMake](https://cmake.org/download/) 4.0+.
- Visual Studio or clang-cl compiler supporting C++20.

## Building the library

> [!NOTE]
> If your Dig or Die installed in non-default location (`%ProgramFiles(x86)%/Steam/steamapps/common/Dig or Die`), you can provide
> the `-D GAME_DATA_PATH={path to Dig or Die installation}` option to automatically replace the game's `DigOrDie.dll` with a new compiled one.

> [!IMPORTANT]
> The game **only** allows 32-bit `DigOrDie.dll`, but the project can be compiled as 64-bit.
> If you want to use a reverse engineered `DigOrDie.dll` elsewhere remove the `-A Win32` option.

To generate a CMake project run this command in `dod-decomp/` folder where `CMakeLists.txt` is located:
> [!NOTE]
> Provide the `-T ClangCL` option to use clang-cl compiler instead of MSVC.
```bash
cmake -B build -A Win32
```

Run this to open Visual Studio for this project:
```bash
cmake --open build
```

Or, you can also build it though command line:
```bash
cmake --build build
```

After building is completed, the original `DigOrDie.dll` will be overwritten with the newly compiled `.dll` if `GAME_DATA_PATH` variable points to valid folder
(the message will be displayed to console).
