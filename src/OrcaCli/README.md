# orca-cli

Command line front end for **ORCA**, Appeon PowerBuilder's library API (`pborc.dll`).

It converts in both directions and verifies the result:

```
PBL  ──export──▶  <library>.pbl.src/  ──build──▶  PBL + PBD + EXE
                        ▲                │
                        └──── verify ────┘
```

| Command | Purpose |
|---|---|
| [`export`](#export) | Export a library to `.sr?` source files, in the Library painter's mirrored layout |
| [`build`](#build) | Rebuild libraries (and optionally PBDs/EXE) from exported sources |
| [`list`](#list) | List a library's objects, or the files of a source tree |
| [`verify`](#verify) | Compare a library against its sources; exit 2 on drift |
| [`info`](#info) | Show the resolved configuration; `--check` proves ORCA loads |

* [Requirements](#requirements)
* [Install](#install)
* [Synopsis](#synopsis)
* [Exit codes](#exit-codes)
* [Configuration](#configuration)
* [Source layout](#source-layout)
* [JSON output](#json-output)
* [CI recipes](#ci-recipes)
* [Troubleshooting](#troubleshooting)

## Requirements

- **.NET 8** desktop runtime (the tool targets `net8.0`).
- **PowerBuilder 25.0** installed: `pborc.dll` in the IDE folder and the matching runtime
  folder. Both paths are configurable.
- **x86 only.** `pborc.dll` is a 32-bit DLL; the tool is built as a 32-bit executable, so a
  64-bit host is not possible. `orca-cli info` prints the process width it is running in.
- PowerBuilder itself must be **closed** while the tool runs. ORCA uses the same PowerBuilder
  compiler, which is not reentrant; running both at once can corrupt libraries.

## Install

The tool is part of the `ORCA.Net` repository:

```
build.cmd                 REM builds src\Orca\Orca.csproj and src\OrcaCli\OrcaCli.csproj
orca.cmd <command> ...    REM wrapper around src\OrcaCli\bin\Release\net8.0\orca-cli.exe
```

You can also call the executable directly, or copy `orca-cli.exe`, `Orca.dll`,
`orca-cli.runtimeconfig.json` and `appsettings.json` next to each other anywhere.

## Synopsis

```
orca-cli [global options] <command> [command options]

Global options:
  -h, --help       show the overview, or one command's help together with a command name
  -V, --version    print the version
  -v, --verbose    show per-object detail (imports, exports, differences)
  -q, --quiet      only warnings and errors

Commands: build, export, info, list, verify
```

`orca-cli help <command>` and `orca-cli <command> --help` are equivalent.
Options may be written `--name value` or `--name=value`; `-p`, `-s`, `-o` are shorthands for
`--pbl`, `--src`, `--out`; `--` ends option parsing.

## Exit codes

| Code | Meaning |
|---|---|
| `0` | success |
| `1` | usage error, missing configuration, or an ORCA failure |
| `2` | `verify` found differences (also used by `build` when its round-trip check fails) |
| `3` | `build` failed with compile or link errors |

## Configuration

Three layers, first match wins:

1. command line — `--dll`, `--runtime`, `--encoding`;
2. environment — `ORCA_DLL_PATH`, `ORCA_RUNTIME_PATH`;
3. `appsettings.json` — next to the executable, then (overriding) in the current directory.

```json
{
  "Orca": {
    "DllPath": "C:\\Program Files (x86)\\Appeon\\PowerBuilder 25.0\\IDE\\pborc.dll",
    "RuntimePath": "C:\\Program Files (x86)\\Appeon\\Common\\PowerBuilder\\Runtime 25.0.0.3711"
  },
  "Demo": {
    "PblPath": "PB_DEMO_PROJ\\APP_SRC\\tlfi_docuscan.pbl",
    "OutputDirectory": "PB_DEMO_PROJ\\TARGET_OUTPUT",
    "BuildSourceDirectory": "PB_DEMO_PROJ\\TARGET_OUTPUT",
    "BuildPblPath": "PB_DEMO_PROJ\\BUILD_OUTPUT\\tlfi_docuscan.pbl",
    "Encoding": "Unicode"
  }
}
```

Relative paths are resolved against the project root: the nearest ancestor directory that
contains `PB_DEMO_PROJ`, else the current directory. The section may be called `Demo` or
`Project`.

`pborc.dll` sits in the IDE folder, but its dependencies (`pbvm.dll`, `pbshr.dll`, …) live in
the runtime folder. `OrcaRuntime.Initialize` prepends the runtime folder to the process `PATH`,
calls `SetDefaultDllDirectories` + `AddDllDirectory` + `SetDllDirectory`, and installs a
`NativeLibrary` resolver that loads the configured absolute `pborc.dll`.

## Source layout

The Library painter records **which objects belong to which PBL** by exporting each library
into a folder named after the library file plus `.src`:

```
PB_DEMO_PROJ/TARGET_OUTPUT/
  tlfi_docuscan.pbl.src/
    tlfi_docuscan.sra
    dw_device.srd
    dw_createjobrequest.srd
    dw_createjobresponse.srd
    w_main.srw
```

`export` produces that layout by default, and `build` consumes it: one folder becomes one PBL,
and the objects inside it are imported into exactly that PBL. Pass `--layout flat` to
`export` (or point `--src` at a flat folder) when you want a single-library dump instead.

## export

```
orca-cli export --pbl <file.pbl> [--out <dir>] [--layout pbl|flat] [--encoding <enc>]
                [--no-headers] [--binary] [--no-bom] [--dry-run] [--json]
```

| Option | Meaning |
|---|---|
| `--pbl <file>` | library to export; default `PblPath` |
| `--out <dir>` | parent output folder; default `OutputDirectory`, else the library's folder |
| `--layout <kind>` | `pbl` (default) writes into `<pbl file name>.src`, `flat` writes into `--out` |
| `--flat` | shorthand for `--layout flat` |
| `--no-headers` | omit `$PBExportHeader$` / `$PBExportComments$` lines |
| `--binary` | append embedded binary components (OLE …) |
| `--no-bom` | do not prepend a UTF-16 BOM |
| `--dry-run` | export into memory and report, write nothing |

The export itself uses `PBORCA_LibraryDirectory` to enumerate entries and
`PBORCA_LibraryEntryExportEx` to fetch each source, growing the buffer on
`PBORCA_BUFFERTOOSMALL`. File names are `<entry>.sr?` (`.sra` application, `.srw` window,
`.srd` datawindow, `.srf` function, `.srm` menu, `.srs` structure, `.sru` user object, `.srq`
query, `.srp` pipeline, `.srj` project).

```console
$ orca-cli export --pbl app.pbl --out sources
Library : D:\work\app.pbl
Output  : D:\work\sources\app.pbl.src
Encoding: Unicode, headers
Entries : 5
Exported 5 object(s), 101218 bytes

$ orca-cli export --pbl app.pbl --layout flat --verbose
```

## build

```
orca-cli build --src <dir> [--out <dir>] [--pbl <file.pbl>] [--app <name>]
               [--rebuild full|incremental|migrate|3pass] [--no-rebuild]
               [--pbd] [--exe <file>] [--icon <ico>] [--pbr <pbr>] [--comment <text>]
               [--no-verify] [--json]
```

| Option | Meaning |
|---|---|
| `--src <dir>` | folder containing `*.pbl.src` folders, or a flat `.sr?` folder; default `BuildSourceDirectory` |
| `--out <dir>` | output folder for the libraries |
| `--pbl <file>` | flat layout: the library to create; also determines the output folder |
| `--app <name>` | Application object name; default from the `.sra` file |
| `--rebuild <type>` | `full` (default), `incremental`, `migrate`, `3pass` |
| `--no-rebuild` | skip `PBORCA_ApplicationRebuild` |
| `--pbd` | also build a PBD for every library |
| `--exe <file>` | also build an executable |
| `--icon <file>` | icon for the EXE; a 32×32 placeholder is generated when omitted |
| `--pbr <file>` | PowerBuilder resource file for the EXE |
| `--comment <text>` | comment for the created PBLs |
| `--no-verify` | skip the round-trip comparison |

What happens, in order:

1. every `*.pbl.src` folder becomes one PBL (name = folder name minus `.src`); the library
   holding the `.sra` is placed first in the library list;
2. **bootstrap session**: create the libraries, set the list, `SessionSetCurrentAppl(NULL,
   NULL)` and import the Application object — compile errors are expected here because its
   `open`/`close` events reference windows that do not exist yet;
3. **main session**: set the list and the real application, import every other object into its
   own PBL (definitions before datawindows, datawindows before windows);
4. **re-import the Application object**, now that its windows exist, then
   `PBORCA_ApplicationRebuild`. Without step 4 the rebuild keeps failing with
   `C0015: Undefined variable: w_main`;
5. optionally `PBORCA_DynamicLibraryCreate` and `PBORCA_ExecutableCreate`. With `--exe` the
   application library goes into the EXE and the other libraries must already be PBDs, so
   those are built first and `iPBDFlags` marks them;
6. unless `--no-verify`, every rebuilt library is exported again and compared byte for byte
   with the sources.

```console
$ orca-cli build --src sources --out build --pbd --exe build\app.exe
Sources : D:\work\sources (1 libraries)
Output  : D:\work\build
App     : app in app.pbl
Objects : 5
Import  : app (Application) — bootstrap, compile deferred
Import  : app (Application) — application
Rebuild : Full
Library : D:\work\build\app.pbl (175616 bytes)
PBD     : D:\work\build\app.pbd (71680 bytes)
EXE     : D:\work\build\app.exe (247296 bytes)
Verify  : 5 object(s) — 5 identical, 0 different, 0 missing
```

## list

```
orca-cli list --pbl <file.pbl> [--json]
orca-cli list --src <dir> [--json]
```

`--pbl` opens the library through ORCA (`PBORCA_LibraryDirectory`) and prints name, type,
size and comments. `--src` reads the exported tree instead and needs no PowerBuilder
installation at all, which makes it usable on a build agent that only has the sources.

## verify

```
orca-cli verify --pbl <file.pbl> [--src <dir>] [--json]
```

Re-exports each source file from the library and compares bytes. Objects present in the
library but absent from the tree are reported as `extra`. Defaults for `--src`, in order: the
`sibling folder <pbl file name>.src`, then the configured `OutputDirectory`.

Exit `0` when everything matches, `2` when anything differs, is missing or extra — suitable as
a gate in CI:

```console
$ orca-cli verify --pbl app.pbl --src sources
Library : D:\work\app.pbl
Sources : D:\work\sources\app.pbl.src
w_main                   Window       differs (expected 39996 bytes, got 34586)
Result  : 5 source file(s) — 4 identical, 1 different, 0 missing, 0 extra
$ echo $LASTEXITCODE
2
```

## info

```
orca-cli info [--check] [--json]
```

Prints the tool version, process width, the resolved `pborc.dll` and runtime folder (with
existence and the DLL's file version), the encoding and which `appsettings.json` files were
read. `--check` loads the DLL and opens/closes an ORCA session, which is the quickest way to
prove a machine is set up correctly.

## JSON output

Every command accepts `--json`. The document goes to **stdout**; progress and errors go to
stderr, so `orca-cli ... --json | jq` is safe.

```jsonc
// export
{ "library": "…", "output": "…", "encoding": "Unicode", "layout": "pbl",
  "dryRun": false, "count": 5, "bytes": 101218,
  "entries": [ { "name": "w_main", "type": "Window", "bytes": 34586, "file": "w_main.srw" } ] }

// list --pbl        { "library", "comment", "count", "entries": [ { "name","type","sizeBytes","comments","createdUtc" } ] }
// list --src        { "sources", "libraries": [ { "library","directory","files":[…] } ] }
// verify            { "library","source","sources","identical","different","missing","extra",
//                      "differences": [ { "entry","type","status","detail" } ] }
// info              { "version","process","dll":{…},"runtime":{…},"encoding","config":[…],"check":{…} }
// build             { "sources","output","application","libraries":[…],"pbds":[…],"executable","verify":{…} }
```

## CI recipes

```yaml
# fail the build when a PBL and its sources disagree
- run: orca-cli verify --pbl app.pbl --src sources

# regenerate the sources, then fail if they changed unexpectedly
- run: orca-cli export --pbl app.pbl --out sources
- run: git diff --exit-code sources

# rebuild everything from source and prove the round trip
- run: orca-cli build --src sources --out build --pbd --exe build/app.exe
```

## Troubleshooting

| Symptom | Cause |
|---|---|
| `BadImageFormatException` or "could not load" | the process is 64-bit, or the runtime folder is wrong; run `orca-cli info --check` |
| `ORCA ExecutableCreate failed: -1 (InvalidParameters)` | `--icon` pointed at a missing file (the tool generates one when omitted) |
| `'app' failed to compile` / `C0015: Undefined variable: w_main` | the Application object was not re-imported after the windows — the `build` command does this automatically |
| `-4 PBORCA_BADLIBRARY` | a library path does not exist or is not a PBL |
| `-13 PBORCA_CURRAPPLNOTSET` | import/build called without a current application |
| `-22 … -26` | source-control connection failures (`Scc*` functions, used by the library, not by these commands) |
| libraries get corrupted | PowerBuilder was open while ORCA ran |

## Limits

- The tool covers the library, compile, query and build functions of ORCA. Source-control
  (`Scc*`) and Windows Forms deployment are available in the `Orca` library but are not
  exposed as commands.
- ORCA cannot read `.pbt` targets, which is why library membership lives in the
  `<pbl>.src` folders.
- Only workspaces are supported by PowerBuilder's API, never solutions.
