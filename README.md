# ORCA for .NET

A managed wrapper for Appeon PowerBuilder's ORCA API (`pborc.dll`) and a command line tool
built on it that converts in both directions:

```
PBL  ──export──▶  <library>.pbl.src/  ──build──▶  PBL + PBD + EXE
                        ▲                │
                        └──── verify ────┘
```

```
src/Orca/       class library — P/Invoke wrapper for the whole documented ORCA surface
src/OrcaCli/    command line tool — see src/OrcaCli/README.md for the full manual
```

The CLI has its own documentation: **[`src/OrcaCli/README.md`](src/OrcaCli/README.md)**.

## Why the wrapper is x86-only

`pborc.dll` is a 32-bit DLL (PE machine `0x014C`). A 64-bit host cannot load it, and the
.NET loader reports `BadImageFormatException` at the first call. Both projects therefore pin
`<PlatformTarget>x86</PlatformTarget>`; a 32-bit app host is produced automatically. If you
need a 64-bit consumer, it must talk to a 32-bit helper process instead.

## Pointing at PowerBuilder

Two paths are configurable, and **both** matter:

| Setting | Default |
|---|---|
| `pborc.dll` | `C:\Program Files (x86)\Appeon\PowerBuilder 25.0\IDE\pborc.dll` |
| runtime folder | `C:\Program Files (x86)\Appeon\Common\PowerBuilder\Runtime 25.0.0.3711` |

`pborc.dll` sits in the IDE folder, but its dependencies (`pbvm.dll`, `pbshr.dll`, …) live in
the runtime folder. `OrcaRuntime.Initialize(dllPath, runtimePath)` prepends the runtime folder
to the process `PATH`, calls `SetDefaultDllDirectories` + `AddDllDirectory` +
`SetDllDirectory`, and installs a `NativeLibrary` resolver that loads the configured absolute
`pborc.dll`. Precedence: `--dll`/`--runtime` → `ORCA_DLL_PATH`/`ORCA_RUNTIME_PATH` →
`appsettings.json` → defaults.

## The command line tool

```
orca.cmd --help
orca.cmd info --check
orca.cmd export --pbl PB_DEMO_PROJ\APP_SRC\tlfi_docuscan.pbl
orca.cmd verify
orca.cmd build --pbd --exe PB_DEMO_PROJ\BUILD_OUTPUT\tlfi_docuscan.exe
orca.cmd list --src PB_DEMO_PROJ\TARGET_OUTPUT --json
```

| Command | Purpose |
|---|---|
| `export` | PBL → `.sr?` files, in the Library painter's mirrored `<pbl file name>.src` layout |
| `build` | sources → PBL, PBD, EXE (two-session bootstrap, rebuild, round-trip check) |
| `list` | list a library's objects, or a source tree's files |
| `verify` | library vs. sources, byte for byte; exit `2` on drift |
| `info` | resolved paths, DLL version, `--check` proves ORCA loads |

Exit codes: `0` success, `1` usage/ORCA error, `2` differences, `3` build failure. Options,
JSON schemas, CI recipes and troubleshooting are in
[`src/OrcaCli/README.md`](src/OrcaCli/README.md).

## Results on the bundled demo

Export of `PB_DEMO_PROJ/APP_SRC/tlfi_docuscan.pbl` (5 objects):

| entry | type | result vs. the GUI export |
|---|---|---|
| `tlfi_docuscan` | Application | identical (3 090 B) |
| `dw_device` | DataWindow | identical (31 138 B) |
| `dw_createjobrequest` | DataWindow | identical (22 554 B) |
| `dw_createjobresponse` | DataWindow | identical (9 850 B) |
| `w_main` | Window | differs — 34 586 B vs 39 996 B |

The PBL's stored `w_main` is not the revision that was exported through the GUI: it declares
an extra control (`cb_1`) with its own `clicked` event and stores its sections in a different
order. Not an export bug — importing the GUI-exported `w_main` and exporting it again
reproduces that file.

Build from `PB_DEMO_PROJ/TARGET_OUTPUT/tlfi_docuscan.pbl.src`:

| artefact | size |
|---|---|
| `BUILD_OUTPUT/tlfi_docuscan.pbl` | 175 616 B |
| `BUILD_OUTPUT/tlfi_docuscan.pbd` | 71 680 B (`--pbd`) |
| `BUILD_OUTPUT/tlfi_docuscan.exe` | 247 296 B (`--exe`) |
| round-trip | **5 / 5 objects byte-identical**, exit 0 |

Splitting the same objects into two folders (`tlfi_docuscan.pbl.src` with the application and
the datawindows, `app_ui.pbl.src` with `w_main.srw`) is also honoured: two PBLs are created,
`w_main` lands in `app_ui.pbl`, `app_ui.pbd` is the only dynamic library, the EXE links it
(`iPBDFlags` = `{0, 1}`), and the round-trip is still 5 / 5 identical.

Building the GUI set instead also compiles and rebuilds cleanly, but its `w_main` re-exports
with the control property blocks in a different order: same length, same content, different
sequence — PowerBuilder normalises source order on import. That is why the ORCA-produced set
is the one that round-trips byte-exactly.

`PB_DEMO_PROJ/GUI_EXPORT_REFERENCE/` keeps the untouched flat GUI export for comparison:

```
orca.cmd verify --pbl PB_DEMO_PROJ\APP_SRC\tlfi_docuscan.pbl --src PB_DEMO_PROJ\GUI_EXPORT_REFERENCE
```

## Library surface

`OrcaSession` wraps every documented function, grouped as in the ORCA guide:

| Area | Members |
|---|---|
| session | `Open`, `ConfigureSession`, `SetDebug`, `SetLibraryList`, `SetCurrentApplication`, `UseTemporaryApplication`, `GetErrorMessage`, `Dispose` |
| library | `CreateLibrary`, `DeleteLibrary`, `SetLibraryComment`, `GetLibrary`, `GetEntryInfo`, `CopyEntry`, `MoveEntry`, `DeleteEntry`, `ExportEntryBytes`, `ExportEntryText`, `ExportEntryToFile` |
| compile | `ImportEntry`, `ImportEntries`, `RegenerateEntry`, `Rebuild` |
| query | `QueryAncestors`, `QueryReferences` |
| build | `SetExecutableInfo`, `CreateDynamicLibrary`, `CreateExecutable`, `DeployWinFormsProject` |
| source control | `GetSourceControlProperties`, `ConnectSourceControl`, `ConnectSourceControlOffline`, `SetSourceControlPassword`, `SetSourceControlTarget`, `ExcludeLibrariesFromRefresh`, `RefreshSourceControlTarget`, `GetLatestVersion`, `ResetRevisionNumber`, `CloseSourceControl` |

Failures throw `OrcaException` carrying the raw `OrcaResult` code plus the text from
`PBORCA_SessionGetError`. `LastCompileErrors` keeps the messages the compiler callback
produced.

Types (`OrcaEntryType`, `OrcaEncoding`, `OrcaClobber`, `OrcaRebuildType`, `OrcaBuildFlags`,
`OrcaSourceControlTargetFlags`) use the numeric values from `pborca.h`; `PBORCA_TYPE` is
declared in a fixed order there, which is why the enum is not alphabetical.

Interop layouts are checked at `OrcaSession.Open` against the x86 sizes implied by
`pborca.h` (for example `PBORCA_DIRENTRY` = 528 bytes, `PBORCA_SCC` = 2 572 bytes), so a
future PowerBuilder version that changes a structure fails loudly instead of corrupting
memory.

## Building

```
build.cmd
```

`build.cmd` prefers `%ProgramFiles(x86)%\dotnet\dotnet.exe`. On this machine the x64 SDK
(`10.0.102`) is missing its workload MSBuild folders
(`Sdks\Microsoft.NET.SDK.Workload*Locator`), which makes `dotnet restore` on the solution
fail with no diagnostic; the x86 SDK `8.0.407` builds the projects correctly. Restore is
therefore done per project and the solution is built with `--no-restore -m:1`.

Requirements:

- .NET SDK 8.0.4xx, or a newer SDK that still ships the `net8.0` targeting pack;
- PowerBuilder 25.0 installed (runtime + `pborc.dll`) for actually running.

Building with Visual Studio is supported through `Orca.Net.sln`.

## Operational notes

- PowerBuilder must be closed while ORCA runs — the compiler is not reentrant, and using
  both at once can corrupt PBLs.
- One library list and one current application per session. To work on another target,
  close the session and open a new one.
- Library-management and export calls need neither a library list nor a current
  application; import, compile, query and build calls need both.
- `ImportEntry` deletes an existing entry of the same name before compiling, and a failed
  import still leaves the object in the library. That is what makes the bootstrap →
  re-import sequence work.
- The Library painter tracks library membership through `<pbl file name>.src` folders; the
  export reproduces that mapping and the build consumes it, so a multi-PBL target keeps its
  objects in the right libraries without a `.pbt`.
