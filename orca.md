# ORCA API — Condensed Reference

Condensed from `orca.pdf` (*Appeon PowerBuilder® 2025 — ORCA Guide*, 108 pages).
Covers every documented `PBORCA_*` function, its callback, its structures, and — the
part that matters — **when** you must call it and what state must exist first.

---

## Contents

1. [What ORCA is](#1-what-orca-is)
2. [Rules that shape every call](#2-rules-that-shape-every-call)
3. [The shape of an ORCA program](#3-the-shape-of-an-orca-program)
4. [Prerequisites: which calls need what](#4-prerequisites-which-calls-need-what)
5. [Task → function index](#5-task--function-index)
6. [Session functions](#6-session-functions)
7. [Configuration](#7-configuration)
8. [Library functions](#8-library-functions)
9. [Import / compile functions](#9-import--compile-functions)
10. [Query functions](#10-query-functions)
11. [Build functions (PBD, DLL, EXE, WinForms)](#11-build-functions-pbd-dll-exe-winforms)
12. [Source control (SCC) functions](#12-source-control-scc-functions)
13. [Enumerations and flags](#13-enumerations-and-flags)
14. [Structures](#14-structures)
15. [Callbacks](#15-callbacks)
16. [Return codes](#16-return-codes)
17. [Recipes](#17-recipes)
18. [Pitfalls checklist](#18-pitfalls-checklist)
19. [Obsolete / removed API](#19-obsolete--removed-api)

---

## 1. What ORCA is

ORCA (Open Repository CASE API / "Open Library API") is the programmatic equivalent of
PowerBuilder's Library painter plus parts of the Application and Project painters. A C/C++
program calls exported `PBORCA_*` functions in **`PBORC.dll`** to create and manage PBLs,
import and compile objects, query dependencies, and build PBDs/DLLs/EXEs.

Development files ship with PowerBuilder under
`%AppeonInstallPath%\PowerBuilder 25.0\SDK\ORCA`:

- `PBORCA.H` — declarations, enums, structures, return codes
- `PBORCA.LIB` — import library

Typical uses: OrcaScript, CASE tools, class libraries, documentation/analysis tools,
text-search-and-replace across a library, source-control bridges, and rebuilding targets
from source-controlled source.

What it **cannot** do: ORCA works with a PowerBuilder **workspace** only — never a
PowerBuilder **solution**.

---

## 2. Rules that shape every call

| Rule | Consequence |
|---|---|
| The PowerBuilder compiler is **not reentrant** | PowerBuilder must be **closed** while ORCA runs. Close PB → run ORCA → restart PB. Running both can **corrupt PBLs**. |
| Everything is `WINAPI` / `__stdcall`; callbacks are `CALLBACK` / `__stdcall` | Calling convention must match on both sides. |
| ANSI and Unicode clients are both supported | Code with `_TEXT()`/`TCHAR` from `tchar.h` (`Shared/Appeon/PowerBuilder/cgen/h`); build with `/D_UNICODE` for Unicode. |
| A session is a handle (`HPBORCA`, `LPVOID`) | Every call takes it. Open once, close once; no per-call context. |
| Library list and current application can be set **once per session** | To switch app or library list: `PBORCA_SessionClose` → `PBORCA_SessionOpen` → set again. |
| Order is reversed vs. the IDE | IDE: pick application, then library search path. ORCA: **library list first**, then current application. |
| Callbacks use pointers | Functions whose callbacks deliver *information* (directory, hierarchy, references) are effectively C/C++-only and the callback is **required**. Error callbacks are optional (pass `0`). |
| Strings passed to callbacks are owned by ORCA | Copy anything you keep; use them only inside the callback. |
| `pbcPara` argument | Reserved. **Always `NULL`.** |
| Session close frees everything | Not closing leaks memory (no data loss — a session holds no connection). |

---

## 3. The shape of an ORCA program

```
PBORCA_SessionOpen                     -> hORCA
  PBORCA_ConfigureSession              (optional but do it: encoding, clobber, headers)
  PBORCA_SessionSetLibraryList         (only if you will compile / query / build)
  PBORCA_SessionSetCurrentAppl         (after the library list)
    ... work: import, export, copy, query, build ...
PBORCA_SessionClose
```

Any library-management or source-control work can happen between open and close without a
library list or current application.

---

## 4. Prerequisites: which calls need what

**A — Session only** (callable right after `SessionOpen`):

`ConfigureSession`, `SetDebug`, `SessionGetError`, all `Library*` functions,
all `Scc*` functions, `SccResetRevisionNumber`.

**B — Library list + current application required:**

| Needing B | Why |
|---|---|
| `CompileEntryImport`, `CompileEntryImportList`, `CompileEntryRegenerate` | compiler resolves references through the library list |
| `ApplicationRebuild` | compiles every library on the list |
| `ObjectQueryHierarchy`, `ObjectQueryReference` | resolve ancestors/references across the list |
| `ExecutableCreate`, `DynamicLibraryCreate` | build from ORCA's library list |
| `SetExeInfo` | sets version info for a later build |
| `DeployWinFormProject` | deploys the target's project object |

**C — SCC shortcuts:** `SccSetTarget` performs an implicit
`SessionSetLibraryList` + `SessionSetCurrentAppl`. If you use it, **do not** call those two
yourself in that session.

Exception: during **bootstrapping** you pass `NULL` library and application names to
`SessionSetCurrentAppl` so ORCA substitutes a temporary application object.

---

## 5. Task → function index

| You want to… | Call |
|---|---|
| open / close a session | `SessionOpen`, `SessionClose` |
| set encoding, clobber, export headers | `ConfigureSession` |
| turn the DEBUG directive on/off | `SetDebug` (or `ConfigureSession`) |
| explain a failure | `SessionGetError` |
| set library search path / current app | `SessionSetLibraryList`, `SessionSetCurrentAppl` |
| create / delete a PBL | `LibraryCreate`, `LibraryDelete` |
| change a PBL comment | `LibraryCommentModify` |
| list objects in a PBL | `LibraryDirectory` |
| inspect one object | `LibraryEntryInformation` |
| copy / move / delete an object | `LibraryEntryCopy`, `LibraryEntryMove`, `LibraryEntryDelete` |
| export source | `LibraryEntryExport`, `LibraryEntryExportEx` |
| import + compile one object | `CompileEntryImport` |
| import + compile many objects | `CompileEntryImportList` |
| recompile an existing object | `CompileEntryRegenerate` |
| rebuild the whole application | `ApplicationRebuild` |
| list ancestors / references | `ObjectQueryHierarchy`, `ObjectQueryReference` |
| build a PBD or PB DLL | `DynamicLibraryCreate` |
| build an EXE | `ExecutableCreate` (after `SetExeInfo` if you want version info) |
| deploy a Windows Forms target | `DeployWinFormProject` |
| connect to source control | `SccConnect`, `SccConnectOffline`, `SccGetConnectProperties` |
| pull a target and set it up | `SccSetTarget`, `SccExcludeLibraryList`, `SccRefreshTarget` |
| fetch loose (non-PBL) files | `SccGetLatestVersion` |
| fix revision metadata after external edits | `SccResetRevisionNumber` |
| disconnect | `SccClose`, then `SessionClose` |

## 6. Session functions

### `PBORCA_SessionOpen`
```c
HPBORCA PBORCA_SessionOpen(void);
```
**When** — always first, before any other ORCA call. **Returns** the session handle, or `0`
on failure (only when memory is exhausted). Keeping a session open costs nothing, so open
it and leave it open while you need it.

### `PBORCA_SessionClose`
```c
void PBORCA_SessionClose(HPBORCA hORCASession);
```
**When** — last, after `SccClose` in SCC programs. Frees all session resources and the
executable version info set by `SetExeInfo`; skipping it leaks memory. No return value.

### `PBORCA_SessionSetLibraryList`
```c
INT PBORCA_SessionSetLibraryList(HPBORCA h, LPTSTR *pLibNames, INT iNumberOfLibs);
```
**When** — after `SessionOpen`, **before** `SessionSetCurrentAppl`, and before anything that
compiles, queries, or builds. Once per session.

- `pLibNames` — array of pointers to fully qualified PBL file names.
- Search order is array order, exactly like the IDE's library search path; the first match
  wins when resolving references.

**Returns** `0 PBORCA_OK`, `-1 PBORCA_INVALIDPARMS`, `-4 PBORCA_BADLIBRARY` (bad name or a
listed library does not exist).

### `PBORCA_SessionSetCurrentAppl`
```c
INT PBORCA_SessionSetCurrentAppl(HPBORCA h, LPTSTR lpszApplLibName, LPTSTR lpszApplName);
```
**When** — immediately after the library list, before compiling/querying/building. Once per
session. The application library **must already be in the library list**.

- Passing `NULL` for both names is the **bootstrap** mode: ORCA installs an internal default
  application object so you can import your own Application object into an empty library.

**Returns** `0`, `-1`, `-2 PBORCA_DUPOPERATION` (already set), `-3` (library missing),
`-4`, `-5` (library list not set), `-6` (library not in list).

### `PBORCA_SessionGetError`
```c
void PBORCA_SessionGetError(HPBORCA h, LPTSTR lpszErrorBuffer, INT iErrorBufferSize);
```
**When** — any time a call returns a non-zero code and you want the full text. The numeric
code is always sufficient for logic; this gives the human-readable message. Buffer of
`PBORCA_MSGBUFFER` (256) is the documented size. Fills `""` when there is no current error.
No return value.

---

## 7. Configuration

### `PBORCA_ConfigureSession`
```c
INT PBORCA_ConfigureSession(HPBORCA h, PPBORCA_CONFIG_SESSION pSessionConfig);
```
**When** — immediately after `SessionOpen` (that is the documented placement), and again
anytime you want to change behaviour. Settings persist for the session or until the next
call.

**Critical:** the whole struct is applied each time — there is no partial update. Zero the
struct and set **every** field on every call, or you silently reset the ones you omit.

| Field | Meaning |
|---|---|
| `eClobber` | overwrite policy for `LibraryEntryExport`, `LibraryEntryExportEx`, `DynamicLibraryCreate`, `ExecutableCreate`, `LibraryDelete` |
| `eExportEncoding` | `PBORCA_UNICODE` (default for Unicode clients), `PBORCA_ANSI_DBCS` (default for ANSI clients), `PBORCA_UTF8`, `PBORCA_HEXASCII` |
| `bExportHeaders` | emit the two `$PBExportHeader$` / `$PBExportComments$` lines; default `FALSE` |
| `bExportIncludeBinary` | append the object's binary component (OLE etc.) to the export; default `FALSE`; **recommended for new development** |
| `bExportCreateFile` | `TRUE` writes to a file in `pExportDirectory` instead of the caller's buffer; default `FALSE` |
| `pExportDirectory` | target directory when `bExportCreateFile` is `TRUE` |
| `eImportEncoding` | encoding expected in `lpszEntrySyntax` for subsequent imports |
| `bDebug` | value of the DEBUG conditional-compilation directive for compiler-invoking calls |

**Returns** `0`, or `-1` (session not open / null config pointer).

### `PBORCA_SetDebug`
```c
INT PBORCA_SetDebug(HPBORCA h, BOOL bDebug);
```
**When** — any time after `SessionOpen`, if you did not set `bDebug` in `ConfigureSession`
(default is `TRUE`) or need to flip it mid-session. Affects
`ApplicationRebuild`, `CompileEntryImport`, `CompileEntryImportList`,
`CompileEntryRegenerate`, `SccGetLatestVersion`, `SccRefreshTarget`.

**Returns** `0`, `-1` (invalid handle).

**Trap:** setDebug does **not** mark objects as needing recompilation. Consequence:
`PBORCA_INCREMENTAL_REBUILD` will not rebuild an object whose only change is DEBUG status —
avoid incremental rebuild (and incremental `SccRefreshTarget`) on targets that contain
`DEBUG` conditional compilation blocks.

```c
typedef struct pborca_configsession {
   PBORCA_ENUM_FILEWRITE_OPTION eClobber;
   PBORCA_ENCODING  eExportEncoding;
   BOOL             bExportHeaders;
   BOOL             bExportIncludeBinary;
   BOOL             bExportCreateFile;
   LPTSTR           pExportDirectory;
   PBORCA_ENCODING  eImportEncoding;
   BOOL             bDebug;
   PVOID            filler2, filler3, filler4;   // reserved
} PBORCA_CONFIG_SESSION, FAR *PPBORCA_CONFIG_SESSION;
```

---

## 8. Library functions

All of these work with **no library list and no current application**, and they operate on
files you name explicitly (they do not consult the session search path).

### `PBORCA_LibraryCreate`
```c
INT PBORCA_LibraryCreate(HPBORCA h, LPTSTR lpszLibraryName, LPTSTR lpszLibraryComments);
```
**When** — to create an empty PBL on disk, e.g. before bootstrapping or rebuilding a work
library from scratch. Add objects afterwards with `CompileEntryImport*`
(needs library list) or `LibraryEntryCopy` (does not).

**Returns** `0`, `-1`, `-4`, `-7`, `-8` (already exists), `-9` (invalid name).

### `PBORCA_LibraryDelete`
```c
INT PBORCA_LibraryDelete(HPBORCA h, LPTSTR lpszLibraryName);
```
**When** — to remove a PBL file. To delete a **read-only** PBL you must first set
`eClobber = PBORCA_CLOBBER_ALWAYS` via `ConfigureSession`.

**Returns** `0`, `-1`, `-4`, `-7`.

### `PBORCA_LibraryCommentModify`
```c
INT PBORCA_LibraryCommentModify(HPBORCA h, LPTSTR lpszLibName, LPTSTR lpszLibComments);
```
**When** — to change the comment shown for a library (Library ▸ Properties).

**Returns** `0`, `-1`, `-3` (library not found), `-4`, `-7`.

### `PBORCA_LibraryDirectory`
```c
INT PBORCA_LibraryDirectory(HPBORCA h, LPTSTR lpszLibName, LPTSTR lpszLibComments,
                            INT iCmntsBuffLen, PBORCA_LISTPROC pListProc, LPVOID pUserData);
```
**When** — to enumerate a library: comments plus one callback per entry. The only way to
discover what is inside a PBL.

- `lpszLibComments` — caller buffer that receives the library comment.
- `iCmntsBuffLen` — buffer length **in TCHARs**; use `PBORCA_MAXCOMMENT + 1`
  (the guide's prose says `PBORCA_MAXCOMMENTS + 1` — the header constant is
  `PBORCA_MAXCOMMENT`).
- `pListProc` — **required**; called once per entry with a `PBORCA_DIRENTRY`.
- `pUserData` — your accumulator. Two standard patterns: one big buffer that you `realloc`
  on overflow, or a linked list whose head you pass as user data.

**Returns** `0`, `-1`, `-4`, `-7`.

### `PBORCA_LibraryEntryInformation`
```c
INT PBORCA_LibraryEntryInformation(HPBORCA h, LPTSTR lpszLibraryName, LPTSTR lpszEntryName,
                                   PBORCA_TYPE otEntryType, PPBORCA_ENTRYINFO pEntryInfo);
```
**When** — to get comments, create time, object size and **source size** for one entry;
typically first, so you can size the export buffer. Pass `otEntryType = PBORCA_BINARY` to
test whether an entry has a binary (OLE) component — legacy technique, still supported.

**Returns** `0`, `-1`, `-3`, `-4`, `-7`.

**Size caveat:** `lSourceSize` is **always the Unicode byte count**, regardless of client or
`eExportEncoding`, and it grows when `bExportHeaders` / `bExportIncludeBinary` are enabled
(unless the type is `PBORCA_BINARY`). For a non-Unicode export you must estimate yourself:
all-ANSI source ≈ `lSourceSize / 2 + 1`; Unicode needs `+2` for the terminator.

### `PBORCA_LibraryEntryCopy`
```c
INT PBORCA_LibraryEntryCopy(HPBORCA h, LPTSTR lpszSourceLibName, LPTSTR lpszDestLibName,
                            LPTSTR lpszEntryName, PBORCA_TYPE otEntryType);
```
**When** — to copy one entry between libraries, including between libraries outside the
current application. **One call handles both components**: source, then binary if present
(unlike import, which needs two calls).

**Returns** `0`, `-1`, `-3`, `-4`, `-7`.

### `PBORCA_LibraryEntryMove`
```c
INT PBORCA_LibraryEntryMove(HPBORCA h, LPTSTR lpszSourceLibName, LPTSTR lpszDestLibName,
                            LPTSTR lpszEntryName, PBORCA_TYPE otEntryType);
```
**When** — same as copy, but the source entry is removed. Also moves the binary component in
the same call.

**Returns** `0`, `-1`, `-3`, `-4`, `-7`.

> Neither copy nor move invokes the compiler: the copied PCODE is whatever was in the source
> PBL, so its DEBUG-directive state is unknown. Use `CompileEntryRegenerate` if that matters.

### `PBORCA_LibraryEntryDelete`
```c
INT PBORCA_LibraryEntryDelete(HPBORCA h, LPTSTR lpszLibName, LPTSTR lpszEntryName,
                              PBORCA_TYPE otEntryType);
```
**When** — to remove one object from a library. No library list needed.

**Returns** `0`, `-1`, `-3`, `-4`, `-7`.

### `PBORCA_LibraryEntryExport`
```c
INT PBORCA_LibraryEntryExport(HPBORCA h, LPTSTR lpszLibraryName, LPTSTR lpszEntryName,
                              PBORCA_TYPE otEntryType, LPTSTR lpszExportBuffer,
                              LONG lExportBufferSize);
```
**When** — to read an object's source, either into your buffer or (with
`bExportCreateFile = TRUE`) into `<entry>.sr?` in `pExportDirectory`. No library list needed.

Behaviour is entirely driven by the current `PBORCA_CONFIG_SESSION`:

- `bExportCreateFile = TRUE` → `lpszExportBuffer` may be `NULL` and
  `lExportBufferSize` is ignored.
- `bExportHeaders = TRUE` → the two `$PBExport…$` lines are written first.
- `bExportIncludeBinary = TRUE` → binary component appended (recommended).
- `otEntryType = PBORCA_BINARY` → exports just the hex-ASCII binary section; this mode
  **ignores** `bExportHeaders` and `bExportIncludeBinary`.
- Encoding comes from `eExportEncoding`.
- CR/LF is appended to every exported line.
- If the buffer is **exactly** the required length the call succeeds but **does not
  NUL-terminate**; allocate extra bytes and you get a terminator.

**Returns** `0`, `-1`, `-3`, `-4`, `-7`, `-10 PBORCA_BUFFERTOOSMALL`,
`-33 PBORCA_DBCSERROR` (Unicode→ANSI/DBCS conversion failed — wrong locale).

**Overwrite policy when the export file exists:**

| `eClobber` | writable file | read-only file |
|---|---|---|
| `PBORCA_NOCLOBBER` | `PBORCA_OBJEXISTS` | `PBORCA_OBJEXISTS` |
| `PBORCA_CLOBBER` | `PBORCA_OK` | `PBORCA_OBJEXISTS` |
| `PBORCA_CLOBBER_ALWAYS` | `PBORCA_OK` | `PBORCA_OK` |
| `PBORCA_CLOBBER_DECIDED_BY_SYSTEM` | `PBORCA_OK` | `PBORCA_OBJEXISTS` (legacy behaviour) |

`PBORCA_OBJEXISTS` from an export means "existing file was not overwritten".

### `PBORCA_LibraryEntryExportEx`
```c
INT PBORCA_LibraryEntryExportEx(HPBORCA h, LPTSTR lpszLibraryName, LPTSTR lpszEntryName,
                                PBORCA_TYPE otEntryType, LPTSTR lpszExportBuffer,
                                LONG lExportBufferSize, LONG *plReturnSize);
```
**When** — exactly when you need the **actual byte count** produced after encoding
conversion (e.g. you want to write the buffer to a socket/file, or verify truncation).
Behaves identically to `LibraryEntryExport`, plus `*plReturnSize`.

**Returns** same codes as `LibraryEntryExport`.

## 9. Import / compile functions

**All require the library list + current application** (section 4, group B). Source may be
either component-free text or text plus an embedded binary section.

Common notes:

- `lEntrySyntaxBuffSize` / `*pEntrySyntaxBuffSizes` are **byte counts**, never character
  counts — a Unicode buffer is `_tcslen(s) * 2`.
- The import encoding must match the bytes you pass. Set `eImportEncoding` in
  `PBORCA_CONFIG_SESSION` and call `ConfigureSession` first. Defaults: ANSI/DBCS client →
  `PBORCA_ANSI_DBCS`, Unicode client → `PBORCA_UNICODE`. ORCA does **not** sniff the file;
  your program must (BOM `FEFF` → Unicode, `EFBBBF` → UTF8, `HA` prefix → HEXASCII,
  otherwise ANSI/DBCS).
- Export header lines (`$PBExportHeader$…`) in the source are **ignored**; the entry name and
  comments you pass are authoritative.

### `PBORCA_CompileEntryImport`
```c
INT PBORCA_CompileEntryImport(HPBORCA h, LPTSTR lpszLibraryName, LPTSTR lpszEntryName,
                              PBORCA_TYPE otEntryType, LPTSTR lpszComments,
                              LPTSTR lpszEntrySyntax, LONG lEntrySyntaxBuffSize,
                              PBORCA_ERRPROC pCompErrorProc, LPVOID pUserData);
```
**When** — to place one object's source into a library and compile it. The library must be on
the session's library list.

- `otEntryType` — any `PBORCA_TYPE`, including `PBORCA_BINARY`.
- `lpszComments` — stored as the entry comment.
- `pCompErrorProc` — `0` to ignore errors (you still get the return code).

**Objects with embedded binary data (OLE, …) need two calls.** First the text (type =
real object type, e.g. `PBORCA_WINDOW`), then the binary section:

```c
PBORCA_CompileEntryImport(h, lib, "w_edit_connect", PBORCA_WINDOW, "comment",
                          (LPTSTR)pReadBuffer, iSourceSize, fpError, NULL);
PBORCA_CompileEntryImport(h, lib, "w_edit_connect", PBORCA_BINARY, NULL,
                          (LPTSTR)pBinStart, iBinarySize, fpError, NULL);
```

Split the exported file at the marker string
`Start of PowerBuilder Binary Data Section`; everything before it is source, everything from
it on is binary.

**Returns** `0`, `-1`, `-4` (bad library / could not save), `-6` (library not in list),
`-9` (name violates PowerBuilder naming rules), `-11` (compile error),
`-13` (current application not set).

**Two behaviours to design around:**

1. **A failed import still leaves the object in the library**, possibly needing edits. Minor
   errors → openable in its painter. Severe errors → export, fix, re-import.
2. **An import deletes the existing entry of the same name before importing.** If the new
   import then fails, the old object is already gone. Back up or export first.

### `PBORCA_CompileEntryImportList`
```c
INT PBORCA_CompileEntryImportList(HPBORCA h, LPTSTR *pLibraryNames, LPTSTR *pEntryNames,
                                  PBORCA_TYPE *otEntryTypes, LPTSTR *pComments,
                                  LPTSTR *pEntrySyntaxBuffers, LONG *pEntrySyntaxBuffSizes,
                                  INT iNumberOfEntries, PBORCA_ERRPROC pCompErrorProc,
                                  LPVOID pUserData);
```
**When** — to import several related objects in one pass (a window, its menu, a user object it
uses). This is the call used for bootstrapping.

- Parallel arrays, all `iNumberOfEntries` long; index *n* of every array describes the same
  object.
- `pComments` — a comment string per object, stored as the entry comment.
- **Order matters:** ancestors before descendants; a user object before anything that refers
  to it. ORCA imports all entries, compiles each type definition, then — if nothing failed —
  compiles all objects in all listed libraries.
- Mutual references still need `ApplicationRebuild` afterwards.

**Returns** `0`, `-1`, `-4`, `-6`, `-7`, `-9`, `-11`, `-13`. Same two caveats as
`CompileEntryImport` (object stays on failure; old entry deleted first).

### `PBORCA_CompileEntryRegenerate`
```c
INT PBORCA_CompileEntryRegenerate(HPBORCA h, LPTSTR lpszLibraryName, LPTSTR lpszEntryName,
                                  PBORCA_TYPE otEntryType, PBORCA_ERRPROC pCompErrorProc,
                                  LPVOID pUserData);
```
**When** — to recompile one object already in a library: after `SetDebug` changed, after a
copy/move left stale PCODE, or to migrate a single entry. Fix errors by exporting, editing,
re-importing.

**Returns** `0`, `-1`, `-3` (object not found), `-4`, `-5` (no library list), `-6`, `-7`,
`-11`.

### `PBORCA_ApplicationRebuild`
```c
INT PBORCA_ApplicationRebuild(HPBORCA h, PBORCA_REBLD_TYPE eRebldType,
                              PBORCA_ERRPROC pCompErrProc, LPVOID pUserData);
```
**When** — after importing a batch, to resolve dependencies and **circular references**
(it compiles in multiple passes). Compiles every object in every library on the list.

| `eRebldType` | Effect |
|---|---|
| `PBORCA_INCREMENTAL_REBUILD` | rebuilds what changed since the last build (and what depends on it) |
| `PBORCA_FULL_REBUILD` | rebuilds everything |
| `PBORCA_MIGRATE` | upgrades objects from an earlier PowerBuilder version, then rebuilds |
| `PBORCA_3PASS` | multi-pass compile (available as an enum value) |

**Returns** `0`, `-1`, `-11`, `-13`.

**Trap:** incremental rebuild will not catch a change that is only in DEBUG-directive status
(see `SetDebug`).

---

## 10. Query functions

**Both require the library list + current application**, and both need a **required**
callback — they are the only way to get the data out.

### `PBORCA_ObjectQueryHierarchy`
```c
INT PBORCA_ObjectQueryHierarchy(HPBORCA h, LPTSTR lpszLibraryName, LPTSTR lpszEntryName,
                                PBORCA_TYPE otEntryType, PBORCA_HIERPROC pHierarchyProc,
                                LPVOID pUserData);
```
**When** — to walk an object's ancestor chain. **Only `PBORCA_WINDOW`, `PBORCA_MENU` and
`PBORCA_USEROBJECT` are legal**; anything else is an invalid parameter.

Callback receives `PBORCA_HIERARCHY { LPTSTR lpszAncestorName; }` once per ancestor.

**Returns** `0`, `-1`, `-3`, `-4`, `-5`, `-6`, `-7`, `-9`,
`-14 PBORCA_OBJHASNOANCS` (object has no ancestors).

### `PBORCA_ObjectQueryReference`
```c
INT PBORCA_ObjectQueryReference(HPBORCA h, LPTSTR lpszLibraryName, LPTSTR lpszEntryName,
                                PBORCA_TYPE otEntryType, PBORCA_REFPROC pRefProc,
                                LPVOID pUserData);
```
**When** — to find what an object depends on (impact analysis before deleting or moving an
object; building a dependency graph). Accepts all object types.

Callback receives `PBORCA_REFERENCE { lpszLibraryName; lpszEntryName; otEntryType;
otEntryRefType; }` once per referenced object.

**Returns** `0`, `-1`, `-3`, `-4`, `-5`, `-6`, `-9`, `-15 PBORCA_OBJHASNOREFS`.

---

## 11. Build functions (PBD, DLL, EXE, WinForms)

**All require the library list + current application.** Build order is always:
**PBDs/DLLs first, EXE second** — `ExecutableCreate` only links against libraries you already
built.

### `PBORCA_DynamicLibraryCreate`
```c
INT PBORCA_DynamicLibraryCreate(HPBORCA h, LPTSTR lpszLibraryName, LPTSTR lpszPBRName,
                                LONG lFlags, LPVOID pbcPara = NULL);
```
**When** — to turn one PBL into a PBD (Pcode) or a PowerBuilder DLL (machine code), before
building the EXE that will use it.

- `lpszPBRName` — resource file to embed, or `0` if the app has none.
- `lFlags` — `0` gives native Pcode; the machine-code constants below give a DLL.
- **Output naming:** same directory and base name as the PBL, extension swapped —
  `C:\DIR\PROG.PBL` → `C:\DIR\PROG.PBD` (Pcode) or `C:\DIR\PROG.DLL` (machine code).

| `eClobber` | existing PBD/DLL |
|---|---|
| `PBORCA_NOCLOBBER` | fails, whatever the file attributes |
| `PBORCA_CLOBBER` / `PBORCA_CLOBBER_DECIDED_BY_SYSTEM` | succeeds if read-write, fails if read-only |
| `PBORCA_CLOBBER_ALWAYS` | always succeeds |

**Returns** `0`, `-1`, `-4`, `-17 PBORCA_PBDCREATERROR`.

### `PBORCA_ExecutableCreate`
```c
INT PBORCA_ExecutableCreate(HPBORCA h, LPTSTR lpszExeName, LPTSTR lpszIconName,
                            LPTSTR lpszPBRName, PBORCA_LNKPROC pLinkErrProc,
                            LPVOID pUserData, INT *iPBDFlags, INT iNumberOfPBDFlags,
                            LONG lFlags, LPVOID pbcPara = NULL);
```
**When** — last, to produce the EXE from ORCA's library list and current application.

- `lpszIconName` — icon file that **must already exist**.
- `lpszPBRName` — resource file that **must already exist**, or `0`.
- `iPBDFlags` — one element **per library in the library list, same order**:
  `0` = build that library's objects into the EXE, `1` = that library is already a
  PBD/PB DLL and must be linked (not embedded). Every `1` needs a
  `DynamicLibraryCreate` call first.
- `iNumberOfPBDFlags` — count of libraries (must equal the list length).
- `lFlags` — `0` = Pcode; `PBORCA_X64` = 64-bit; machine-code bits ORed together.
- `pLinkErrProc` — `0` allowed; callback receives `PBORCA_LINKERR { lpszMessageText }`.

| `eClobber` | existing EXE |
|---|---|
| `PBORCA_NOCLOBBER` / `PBORCA_CLOBBER_DECIDED_BY_SYSTEM` | fails, whatever the attributes |
| `PBORCA_CLOBBER` | succeeds if read-write, fails if read-only |
| `PBORCA_CLOBBER_ALWAYS` | always succeeds |

**Returns** `0`, `-1`, `-5` (no library list), `-12 PBORCA_LINKERROR`,
`-13` (no current application).

### `PBORCA_SetExeInfo`
```c
INT PBORCA_SetExeInfo(HPBORCA h, PBORCA_EXEINFO *pExeInfo);
```
**When** — after `SessionOpen` + `SessionSetLibraryList` + `SessionSetCurrentAppl`, and
**before** `ExecutableCreate` (also applies to machine-code DLLs). Optional: without it the
EXE simply carries no version resource.

- Strings are copied into ORCA's control block, so you may free them immediately.
- Version numbers must be four comma-separated integers: `"12,0,0,0001"`.
- The data is dropped by `SessionClose`, so every new session must call `SetExeInfo` again.

**Returns** `0`, `-1` (null `pExeInfo` or null handle).

```c
typedef struct pborca_exeinfo {
   LPTSTR lpszCompanyName, lpszProductName, lpszDescription, lpszCopyright;
   LPTSTR lpszFileVersion, lpszFileVersionNum;
   LPTSTR lpszProductVersion, lpszProductVersionNum;
   LPTSTR lpszManifestInfo;          // header only — missing from the guide's listing
} PBORCA_EXEINFO;
```

### `PBORCA_DeployWinFormProject`
```c
INT PBORCA_DeployWinFormProject(HPBORCA h, LPTSTR lpszLibraryName, LPTSTR lpszProjectName,
                                LPTSTR lpszIconFileName, PBORCA_DOTNETPROC pDotNetProc,
                                LPVOID pUserData);
```
**When** — to generate, compile and deploy a **Windows Forms** target's assemblies from a
project object. Requires library list and current application.

- Messages arrive through `pDotNetProc` in fixed order: **all errors, then all warnings, then
  all unsupported-feature messages**.
- `PBORCA_DOTNET_MESSAGE { eMessageType; lpszMessageText; }`; types are
  `PBORCA_ERROR_MESSAGE`, `PBORCA_WARNING_MESSAGE`, `PBORCA_UNSUPPORTED_FEATURE`.
- The DEBUG directive here comes from the target's project object, **not** from `bDebug`.

**Returns** `0`, `-1`, `-4` (required DLL could not load), `-5` (library list required),
`-13` (current application required), `-19`/`-20`/`-21` (component-builder create/init/build).

### Machine-code flags for `lFlags`

| Constant | Meaning |
|---|---|
| (0) / `PBORCA_P_CODE` | native Pcode EXE/PBD |
| `PBORCA_MACHINE_CODE` | machine code (native unless 16-bit bit set) |
| `PBORCA_MACHINE_CODE_NATIVE` | native machine code |
| `PBORCA_MACHINE_CODE_OPT` / `_OPT_SPEED` | optimize |
| `PBORCA_MACHINE_CODE_OPT_SPACE` | optimize for size |
| `PBORCA_TRACE_INFO` | include trace information |
| `PBORCA_ERROR_CONTEXT` | include script name + line number on errors |
| `PBORCA_NEW_VISUAL_STYLE_CONTROLS` | XP-style visual controls |
| `PBORCA_X64` | 64-bit output (EXE) |
| `PBORCA_MACHINE_DEFAULT` | shortcut = `MACHINE_CODE \| MACHINE_CODE_OPT_SPEED` |
| `PBORCA_MACHINE_DEBUG` | shortcut = `MACHINE_CODE \| TRACE_INFO \| ERROR_CONTEXT` |

Typical: `PBORCA_MACHINE_CODE_NATIVE | PBORCA_MACHINE_CODE_OPT_SPEED | PBORCA_TRACE_INFO |
PBORCA_ERROR_CONTEXT`.
Obsolete bits (16-bit `PBORCA_MACHINE_CODE_16` / `PBORCA_P_CODE_16`, `PBORCA_OPEN_SERVER`,
PocketBuilder `PBORCA_PK_*`) exist only for legacy code — do not use.

---

## 12. Source control (SCC) functions

The SCC API of PowerBuilder 8+ is wrapped here. A connection creates a runtime environment
(runtime engine, object manager, PowerScript compiler, storage manager) that stays alive until
`SccClose`. **One target per connection:** to process a second target, `SccClose` →
`SessionClose` → `SessionOpen` → connect again.

### `PBORCA_SccGetConnectProperties`
```c
INT PBORCA_SccGetConnectProperties(HPBORCA h, LPTSTR pWorkspaceFile, PBORCA_SCC *pConfig);
```
**When** — before connecting, to obtain the connection properties already stored in a
PowerBuilder **workspace** (`.pbw`). Fills `szProviderName`, `szUserID`, `szProject`,
`szLocalProjPath`, `szAuxPath`, `szLogFile`, `lAppend` — everything `SccConnect` needs.
`szAuxPath` is what allows a **silent** connection (no provider dialog). You may override any
field afterwards, or skip this call and populate `PBORCA_SCC` manually.

**Returns** `0`, `-3` (workspace file not found).

### `PBORCA_SccConnect`
```c
INT PBORCA_SccConnect(HPBORCA h, PBORCA_SCC *pConfig);
```
**When** — to open the SCC project and create the runtime environment. Takes a preallocated,
usually zeroed `PBORCA_SCC`.

**Returns** `0`, `-22 PBORCA_SCCFAILURE`, `-23 PBORCA_REGREADERROR`,
`-24 PBORCA_SCCLOADDLLFAILED`, `-25 PBORCA_SCCINITFAILED`,
`-26 PBORCA_OPENPROJFAILED`.

### `PBORCA_SccConnectOffline`
```c
INT PBORCA_SccConnectOffline(HPBORCA h, PBORCA_SCC *pConfig);
```
**When** — to refresh/rebuild with **no network**, assuming every needed object is already on
the local project path (e.g. refresh while docked, build later on a laptop). Instantiates the
ORCA SCC class but never talks to the provider.

Three hard preconditions, each with its own error code:

| Requirement | Error if violated |
|---|---|
| later `SccSetTarget` must include `PBORCA_SCC_IMPORTONLY` | `-30 PBORCA_IMPORTONLY_REQ` |
| a prior `SccGetConnectProperties` call (needed for the `.PBC` checkout data) | `-31 PBORCA_GETCONNECT_REQ` |
| the workspace's `.PBC` file must exist | `-32 PBORCA_PBCFILE_REQ` |

**Returns** also `-22`, `-23`, `-24`, `-25`, `-26`.

### `PBORCA_SccSetTarget`
```c
INT PBORCA_SccSetTarget(HPBORCA h, LPTSTR pTargetFile, LONG lFlags,
                        PBORCA_SETTGTPROC pSetTgtProc, LPVOID pUserData);
```
**When** — right after connecting: retrieves the target (`.pbt`) from source control, then
performs an **implicit `SessionSetLibraryList` + `SessionSetCurrentAppl`**. Do **not** call
those two yourself in an SCC session.

- Creates missing target directories/libraries in the local project path.
- Calls `pSetTgtProc` once per library in the list (`PBORCA_SETTARGET { lpszLibraryName; }`)
  — your chance to recognise shared libraries and exclude them.
- After it, the session is fully set up, so you can build PBDs/EXEs without closing and
  reopening — much cheaper than the `SccClose` → re-init path.

Flags (`lFlags`, ORed):

| Flag | Effect |
|---|---|
| `PBORCA_SCC_OUTOFDATE` | only refresh what is out of sync (with the local path when `IMPORTONLY` is set, with the repository otherwise) |
| `PBORCA_SCC_REFRESH_ALL` | refresh every target library (mutually exclusive with `OUTOFDATE`) |
| `PBORCA_SCC_IMPORTONLY` | everything needed already exists locally — import from the local path only; **required** after `SccConnectOffline` |
| `PBORCA_SCC_EXCLUDE_CHECKOUT` | never overwrite objects currently checked out; enables unattended batch refreshes (checkout status from the provider, or from the `.PBC` when offline) |

### `PBORCA_SccExcludeLibraryList`
```c
INT PBORCA_SccExcludeLibraryList(HPBORCA h, LPTSTR *pLibNames, INT iNumberOfLibs);
```
**When** — between `SccSetTarget` and `SccRefreshTarget`, to skip shared PBLs you know were
already refreshed. Excluded libraries are **not** refreshed but **are** still used in the
rebuild that follows.

### `PBORCA_SccRefreshTarget`
```c
INT PBORCA_SccRefreshTarget(HPBORCA h, PBORCA_REBLD_TYPE eRebldType);
```
**When** — after the target and exclusions are set. Gets the latest source for every object
in the target libraries, imports it, and compiles it — i.e. the whole refresh+build in one
call.

| `eRebldType` | Effect |
|---|---|
| `PBORCA_FULL_REBUILD` | full rebuild |
| `PBORCA_INCREMENTAL_REBUILD` | incremental rebuild |
| `PBORCA_MIGRATE` | upgrade + full rebuild |

Avoid `INCREMENTAL_REBUILD` when DEBUG-directive differences are the only change.

### `PBORCA_SccGetLatestVersion`
```c
INT PBORCA_SccGetLatestVer(HPBORCA h, LONG nFiles, LPTSTR *ppFileNames);
```
**When** — to pull **loose files that are not PBL entries** but belong to the application:
`.bmp`, `.jpg`, `.ico`, `.doc`, `.hlp`, `.htm`, `.jsp`, `.pbr`, etc. Give fully qualified
file names.

**Returns** `0`, `-22`.

### `PBORCA_SccResetRevisionNumber`
```c
INT PBORCA_SccResetRevisionNumber(HPBORCA h, LPTSTR lpszLibraryName, LPTSTR lpszEntryName,
                                  PBORCA_TYPE otEntryType, LPTSTR lpszRevisionNum);
```
**When** — your program modified an object's source **outside** the provider and the PBL's
revision metadata must be corrected so a later diff/status is not wrong. Works with or
without an SCC connection, and the PBL need not be in the library list — it edits metadata in
the **local PBL only**, never the repository.

Use it when:

- you imported a known revision with `CompileEntryImport` → pass that revision string;
- the revision is unknown → pass `NULL` (clears the stored revision number);
- you performed a "manual check-in" (export → commit via the provider) → get the new revision
  from the provider and pass it.

`otEntryType` accepts all types including `PBORCA_BINARY`. `lpszEntryName` has no `.sr?`
extension. Only meaningful with providers implementing `SccQueryInfoEx`.

**Returns** `0`, `-1` (null library/entry), `-7` (PBL not openable read/write).

### `PBORCA_SccClose`
```c
INT PBORCA_SccClose(HPBORCA h);
```
**When** — when done with SCC, **before** `SessionClose`. Calls `SCCUninitialize` to drop the
provider connection.

## 13. Enumerations and flags

```c
typedef enum pborca_type {          // object / entry type — order is fixed by pborca.h
   PBORCA_APPLICATION   = 0,
   PBORCA_DATAWINDOW    = 1,
   PBORCA_FUNCTION      = 2,
   PBORCA_MENU          = 3,
   PBORCA_QUERY         = 4,
   PBORCA_STRUCTURE     = 5,
   PBORCA_USEROBJECT    = 6,
   PBORCA_WINDOW        = 7,
   PBORCA_PIPELINE      = 8,
   PBORCA_PROJECT       = 9,
   PBORCA_PROXYOBJECT   = 10,
   PBORCA_BINARY        = 11
} PBORCA_TYPE;

typedef enum pborca_encoding {      // export / import source encoding
   PBORCA_UNICODE, PBORCA_UTF8, PBORCA_HEXASCII, PBORCA_ANSI_DBCS
} PBORCA_ENCODING;

typedef enum pborca_clobber {       // overwrite policy
   PBORCA_NOCLOBBER,                // never overwrite
   PBORCA_CLOBBER,                  // overwrite if not write-protected
   PBORCA_CLOBBER_ALWAYS,           // overwrite even write-protected
   PBORCA_CLOBBER_DECIDED_BY_SYSTEM // legacy (pre-PB10) behaviour
} PBORCA_ENUM_FILEWRITE_OPTION;

typedef enum pborca_rebldtype {     // rebuild flavour
   PBORCA_FULL_REBUILD, PBORCA_INCREMENTAL_REBUILD,
   PBORCA_MIGRATE, PBORCA_3PASS
} PBORCA_REBLD_TYPE;

typedef enum pborca_dotnet_msgtype {
   PBORCA_ERROR_MESSAGE, PBORCA_WARNING_MESSAGE, PBORCA_UNSUPPORTED_FEATURE
} PBORCA_DOTNET_MSGTYPE;
```

Notes: `PBORCA_BINARY` is a **component**, not a standalone object — it only makes sense as a
second import/export call or as a probe in `LibraryEntryInformation`.
`PBORCA_HEXASCII` is the printable hex form PowerBuilder uses for embedded binary sections.

---

## 14. Structures

| Structure | Passed to | Fields |
|---|---|---|
| `PBORCA_CONFIG_SESSION` | `ConfigureSession` | see §7 |
| `PBORCA_ENTRYINFO` | `LibraryEntryInformation` | `szComments[PBORCA_MAXCOMMENT+1]`, `lCreateTime`, `lObjectSize`, `lSourceSize` |
| `PBORCA_DIRENTRY` | directory callback | `szComments`, `lCreateTime`, `lEntrySize` (source + compiled), `lpszEntryName`, `otEntryType` |
| `PBORCA_COMPERR` | compile-error callback | `iLevel`, `lpszMessageNumber`, `lpszMessageText`, `iColumnNumber`, `iLineNumber` |
| `PBORCA_HIERARCHY` | hierarchy callback | `lpszAncestorName` |
| `PBORCA_REFERENCE` | reference callback | `lpszLibraryName`, `lpszEntryName`, `otEntryType`, `otEntryRefType` |
| `PBORCA_LINKERR` | link-error callback | `lpszMessageText` |
| `PBORCA_BLDERR` | obsolete build callback | `lpszMessageText` |
| `PBORCA_SCC` | `SccConnect`, `SccConnectOffline`, `SccGetConnectProperties` | see below |
| `PBORCA_SCCSETTARGET` | `SccSetTarget` callback | `lpszLibraryName` |
| `PBORCA_EXEINFO` | `SetExeInfo` | see §11 |
| `PBORCA_DOTNET_MESSAGE` | `DeployWinFormProject` callback | `eMessageType`, `lpszMessageText` |

**`PBORCA_COMPERR.iLevel`** — severity ladder; a single failure can produce several callback
calls, starting with context lines:

| Value | Meaning |
|---|---|
| 0 | context info (object name, script name) |
| 1 | `CM_INFORMATION_LEVEL` |
| 2 | `CM_OBSOLETE_LEVEL` |
| 3 | `CM_WARNING_LEVEL` |
| 4 | `CM_ERROR_LEVEL` |
| 5 | `CM_FATAL_LEVEL` |
| 6 | `CM_DBWARNING_LEVEL` |

**`PBORCA_SCC`**

```c
typedef struct pborca_scc {
   HWND   hWnd;                                    // parent window, usually NULL
   TCHAR  szProviderName[PBORCA_SCC_NAME_LEN + 1]; // 32 chars
   LONG   lCapabilities;                           // returned by SccConnect
   TCHAR  szUserID[PBORCA_SCC_USER_LEN + 1];       // 32 chars
   TCHAR  szProject[PBORCA_SCC_PATH_LEN + 1];      // 301 chars
   TCHAR  szLocalProjPath[PBORCA_SCC_PATH_LEN + 1];
   TCHAR  szAuxPath[PBORCA_SCC_PATH_LEN + 1];      // provider-specific; enables silent connect
   TCHAR  szLogFile[PBORCA_SCC_PATH_LEN + 1];
   LPTEXTOUTPROC  fpSccMsgHandler;                 // SCC provider messages
   PBORCA_BLDPROC fpOrcaMsgHandler;                // ORCA SCC messages
   LONG   lCommentLen;                             // returned by SccConnect
   LONG   lAppend;                                 // 1 append, 0 overwrite log
   LPVOID pCommBlk;                                // reserved
   LONG   lDeleteTempFiles;                        // clean the local project path when done
   BOOL   bDeletePblFlag;                          // also delete the generated PBLs
} PBORCA_SCC;
```

`SccGetConnectProperties` fills provider, user, project, local path, aux path, log file and
append flag; `SccConnect` fills `lCapabilities` and `lCommentLen`. The struct is 2 572 bytes
on x86.

---

## 15. Callbacks

Every callback is `void (CALLBACK *)(structure*, LPVOID userData)`, i.e. `__stdcall`, and
returns nothing. Pattern: allocate user data → pass pointer with function pointer → ORCA calls
it 0..n times → after the call returns, read your user data.

| Callback type | Used by | Delivers | Required? |
|---|---|---|---|
| `PBORCA_ERRPROC` | `ApplicationRebuild`, `CompileEntryImport`, `CompileEntryImportList`, `CompileEntryRegenerate` | `PBORCA_COMPERR` per compile message | optional (`0`) |
| `PBORCA_LISTPROC` | `LibraryDirectory` | `PBORCA_DIRENTRY` per entry | **required** |
| `PBORCA_HIERPROC` | `ObjectQueryHierarchy` | `PBORCA_HIERARCHY` per ancestor | **required** |
| `PBORCA_REFPROC` | `ObjectQueryReference` | `PBORCA_REFERENCE` per reference | **required** |
| `PBORCA_LNKPROC` | `ExecutableCreate` | `PBORCA_LINKERR` per link error | optional (`0`) |
| `PBORCA_BLDPROC` | obsolete `BuildProject*` | `PBORCA_BLDERR` | optional |
| `PBORCA_SETTGTPROC` | `SccSetTarget` | `PBORCA_SCCSETTARGET` per library | optional (`NULL`) |
| `PBORCA_DOTNETPROC` | `DeployWinFormProject` | `PBORCA_DOTNET_MESSAGE` | required for diagnostics |

Cross-language note: `HPBORCA` is `LPVOID`; function pointers must be cast to the declared
type, e.g. `fpError = (PBORCA_ERRPROC)ErrorProc;`.

Example of the canonical user-data accumulator and callback (abridged from the guide):

```c
typedef struct ORCA_UserDataInfo {
   LPBYTE lpszBuffer;     // message storage
   DWORD  dwCallCount;    // messages so far
   DWORD  dwBufferSize;   // capacity
   DWORD  dwBufferOffset; // write cursor
} ORCA_USERDATAINFO, FAR *PORCA_USERDATAINFO;

void CALLBACK ErrorProc(PPBORCA_COMPERR lpErr, LPVOID lpUserData)
{
   PORCA_USERDATAINFO d = (PORCA_USERDATAINFO)lpUserData;
   d->dwCallCount++;
   /* append lpErr->lpszMessageText + "\r\n" at d->lpszBuffer + d->dwBufferOffset,
      growing the buffer when it would overflow */
}
```

---

## 16. Return codes

Defined in `PBORCA.H`. `INT` return values; `0` is success, everything else is negative.

| Code | Constant | Meaning |
|---|---|---|
| 0 | `PBORCA_OK` | success |
| -1 | `PBORCA_INVALIDPARMS` | invalid parameter list |
| -2 | `PBORCA_DUPOPERATION` | duplicate operation (e.g. current app already set) |
| -3 | `PBORCA_OBJNOTFOUND` | object not found |
| -4 | `PBORCA_BADLIBRARY` | bad library name / library not found / could not save |
| -5 | `PBORCA_LIBLISTNOTSET` | library list not set |
| -6 | `PBORCA_LIBNOTINLIST` | library not in library list |
| -7 | `PBORCA_LIBIOERROR` | library I/O error |
| -8 | `PBORCA_OBJEXISTS` | object exists |
| -9 | `PBORCA_INVALIDNAME` | name violates PowerBuilder naming rules |
| -10 | `PBORCA_BUFFERTOOSMALL` | buffer too small |
| -11 | `PBORCA_COMPERROR` | compile error |
| -12 | `PBORCA_LINKERROR` | link error |
| -13 | `PBORCA_CURRAPPLNOTSET` | current application not set |
| -14 | `PBORCA_OBJHASNOANCS` | object has no ancestors |
| -15 | `PBORCA_OBJHASNOREFS` | object has no references |
| -16 | `PBORCA_PBDCOUNTERROR` | invalid number of PBDs |
| -17 | `PBORCA_PBDCREATERROR` | PBD create error |
| -18 | `PBORCA_CHECKOUTERROR` | source management error (obsolete) |
| -19 | `PBORCA_CBCREATEERROR` | could not instantiate ComponentBuilder |
| -20 | `PBORCA_CBINITERROR` | ComponentBuilder Init failed |
| -21 | `PBORCA_CBBUILDERROR` | ComponentBuilder BuildProject failed |
| -22 | `PBORCA_SCCFAILURE` | could not connect to source control |
| -23 | `PBORCA_REGREADERROR` | could not read registry |
| -24 | `PBORCA_SCCLOADDLLFAILED` | could not load DLL |
| -25 | `PBORCA_SCCINITFAILED` | could not initialize SCC connection |
| -26 | `PBORCA_OPENPROJFAILED` | could not open SCC project |
| -27 | `PBORCA_TARGETNOTFOUND` | target file not found |
| -28 | `PBORCA_TARGETREADERR` | unable to read target file |
| -29 | `PBORCA_GETINTERFACEERROR` | unable to access SCC interface |
| -30 | `PBORCA_IMPORTONLY_REQ` | offline connect requires `IMPORTONLY` |
| -31 | `PBORCA_GETCONNECT_REQ` | offline connect requires `SccGetConnectProperties` with `EXCLUDE_CHECKOUT` |
| -32 | `PBORCA_PBCFILE_REQ` | offline + `EXCLUDE_CHECKOUT` requires the `.PBC` file |
| -33 | `PBORCA_DBCSERROR` | locale error converting Unicode → ANSI/DBCS |

When you need the text behind a code, call `SessionGetError` immediately — the session keeps
only the current error.

---

## 17. Recipes

### 17.1 Import one source file (with optional OLE payload)

1. `SessionOpen`
2. `ConfigureSession` — zero the struct, set `eImportEncoding` to match the file (sniff BOM:
   `FEFF` → `PBORCA_UNICODE`, `EFBBBF` → `PBORCA_UTF8`, `HA` → `PBORCA_HEXASCII`, else
   `PBORCA_ANSI_DBCS`), plus `eClobber`, `bDebug`.
3. (If needed) `LibraryDelete` + `LibraryCreate` for the work PBL.
4. `SessionSetLibraryList` (the work PBL must be in it) → `SessionSetCurrentAppl`.
5. Locate `Start of PowerBuilder Binary Data Section` in the file, if present, and split.
6. `CompileEntryImport` (source part), then a second `CompileEntryImport` with
   `PBORCA_BINARY` (binary part) if any.
7. `SessionClose`.

### 17.2 Export every object of a PBL to disk

1. `SessionOpen`
2. `ConfigureSession`: `bExportCreateFile = TRUE`, `pExportDirectory`, `eExportEncoding`,
   `bExportHeaders = TRUE`, `bExportIncludeBinary = TRUE`, `eClobber = PBORCA_CLOBBER`.
3. `LibraryDirectory` with a callback that records each `lpszEntryName`/`otEntryType`.
4. For each recorded entry: `LibraryEntryExport(h, lib, name, type, NULL, 0)` — ORCA writes
   `<name>.sr?` into the export directory.
5. `SessionClose`.

No library list or current application needed anywhere in this recipe.

### 17.3 Build PBDs and then the EXE

1. `SessionOpen`, `ConfigureSession` (`eClobber` decides overwrite behaviour).
2. `SessionSetLibraryList` (order = application PBL first, then libraries).
3. `SessionSetCurrentAppl`.
4. `SetExeInfo` (optional but do it before building).
5. `DynamicLibraryCreate` for every library that should ship separately (Pcode → `.pbd`,
   machine code → `.dll`); `lFlags = 0` for Pcode, or machine-code flags for a DLL.
6. `ExecutableCreate` with `iPBDFlags[i] = 1` exactly for the libraries built in step 5 and
   `0` for libraries to embed; `lFlags` = `0` (Pcode), `PBORCA_X64`, or machine-code
   combination.
7. `SessionClose`.

Skeleton:

```c
int pbdFlags[4] = { 0, 0, 1, 1 };            // last two libraries are separate PBDs
for (i = 2; i < 4; i++)
    PBORCA_DynamicLibraryCreate(h, libs[i], 0, 0, NULL);

lBuild = PBORCA_MACHINE_CODE_NATIVE | PBORCA_MACHINE_CODE_OPT_SPEED;
rc = PBORCA_ExecutableCreate(h, "c:\\app\\process.exe", "c:\\app\\process.ico",
                             "c:\\app\\process.pbr", fpLinkProc, lpUserData,
                             (INT *)pbdFlags, 4, lBuild, NULL);
```

### 17.4 Bootstrap a new application from source

Session 1 (empty library, temporary application):

1. `SessionOpen`
2. `LibraryCreate` — the new PBL
3. `SessionSetLibraryList` — that PBL only
4. `SessionSetCurrentAppl(h, NULL, NULL)` — ORCA substitutes a default application
5. `CompileEntryImportList` — **only the Application object**. Importing anything else now
   fails or miscompiles, because dependents see the temporary application (globals, etc.)
6. `SessionClose`

Session 2 (real application):

1. `SessionOpen`
2. `LibraryCreate` any additional PBLs (or create them earlier — just don't import yet)
3. `SessionSetLibraryList` — the bootstrap PBL plus the new ones
4. `SessionSetCurrentAppl` — the Application object you just imported
5. `CompileEntryImportList` / `CompileEntryImport` for everything else
6. **Re-import the Application object now that the windows exist** — its open/close events
   reference them, so the bootstrap import ends in `-11 PBORCA_COMPERROR` and stores broken
   PCODE. Replacing the object that is currently the application is allowed (the import
   deletes the old entry and compiles the new one against the complete library).
7. `ApplicationRebuild` (`PBORCA_FULL_REBUILD`) to settle dependencies
8. `SessionClose`

> Verified against PowerBuilder 25: without step 6 the rebuild keeps reporting
> `C0015: Undefined variable: w_main` for the application's `open` event, even with
> `PBORCA_3PASS`. `CompileEntryRegenerate` on the Application object does not help either —
> the import must be re-run. With step 6 the rebuild is clean and a full export of the
> rebuilt library is byte-identical to the sources.

### 17.5 Refresh and rebuild from source control

Online:

1. `SessionOpen`
2. `SccGetConnectProperties` (workspace `.pbw`) → `PBORCA_SCC`
3. `SccConnect`
4. `SccSetTarget` with `PBORCA_SCC_OUTOFDATE | PBORCA_SCC_EXCLUDE_CHECKOUT` (plus
   `PBORCA_SCC_IMPORTONLY` only if the local path is already current)
5. optionally `SccExcludeLibraryList` for shared PBLs
6. `SccRefreshTarget(PBORCA_FULL_REBUILD)` — refresh + import + compile
7. build PBDs/EXEs now if wanted (the session already has a library list and application)
8. `SccClose`, `SessionClose`

Offline (no network):

1. `SessionOpen`
2. `SccGetConnectProperties` — **mandatory** (`-31` otherwise), also supplies the `.PBC`
3. `SccConnectOffline`
4. `SccSetTarget` with `PBORCA_SCC_IMPORTONLY | PBORCA_SCC_OUTOFDATE |
   PBORCA_SCC_EXCLUDE_CHECKOUT`
5. `SccRefreshTarget(PBORCA_FULL_REBUILD)`
6. `SccClose`, `SessionClose`

### 17.6 Edit one object in place

`SessionOpen` → (library list + current application) → `LibraryEntryInformation`
(size + binary probe) → `LibraryEntryExport`/`Ex` → modify text → `CompileEntryImport`
(replace; remember the old entry is deleted first) → if binary data exists, second
`CompileEntryImport` with `PBORCA_BINARY` → if the PBL is under SCC and you tracked the
revision, `SccResetRevisionNumber` → `SessionClose`.

### 17.7 Rebuild a library from scratch (mirror)

`LibraryDelete` → `LibraryCreate` → `SessionSetLibraryList` (including the new PBL) →
`SessionSetCurrentAppl` → `CompileEntryImportList` (ancestors first) →
`ApplicationRebuild(PBORCA_FULL_REBUILD)` → `SessionClose`.
This is the standard "work PBL from source files" pattern.

**Which object goes into which PBL?** ORCA has no concept of a target: it only knows the
library list you hand it, so the mapping must be stored next to the sources. The Library
painter does exactly that — it exports each library into a folder named
`<pbl file name>.src` (`tlfi_docuscan.pbl.src/`), one folder per library in the list. When you
round-trip through ORCA, recreate the same structure and import each folder's files into the
PBL named by the folder; the application's library goes first in the library list. Deriving
membership from a `.pbt` is not an option with ORCA — it cannot read targets.

---

## 18. Pitfalls checklist

- **PowerBuilder must not be running.** The compiler is single-instance; concurrent use can
  corrupt PBLs.
- **One attempt per session.** Library list and current application are write-once — a second
  `SessionSetCurrentAppl` returns `PBORCA_DUPOPERATION`. New target ⇒ new session.
- **Import is destructive on collision.** Existing entry is deleted before the new one is
  compiled; a failed import leaves you without the old object.
- **Import failures still create entries.** Check the return code, don't assume atomicity.
- **Pass byte counts, not character counts**, for all syntax-buffer sizes.
- **Set `eImportEncoding` to the truth.** ORCA does not detect encoding; wrong setting
  produces garbled source or `-33`.
- **`lSourceSize` is Unicode-based** even for ANSI clients — budget the export buffer
  yourself (+ headers, + binary, + NUL).
- **Exact-size export buffers are not NUL-terminated.**
- **`eClobber` semantics differ per function** (`LibraryDelete` needs `CLOBBER_ALWAYS` for
  read-only; `NOCLOBBER` and `DECIDED_BY_SYSTEM` behave the same for EXE creation).
- **Copy/move don't compile.** PCODE may be stale relative to the current `bDebug`; regenerate
  when it matters.
- **Incremental rebuild ignores DEBUG-only changes** — same for
  `SccRefreshTarget(INCREMENTAL)`.
- **`SetExeInfo` info dies with the session** — set it in every session that builds.
- **PBD/DLL before EXE**, and `iPBDFlags` must line up 1:1 with the library list order.
- **Don't mix SCC setup with manual setup:** after `SccSetTarget`, never call
  `SessionSetLibraryList`/`SessionSetCurrentAppl` yourself.
- **One target per SCC connection** — close, reopen a session to process another.
- **`SessionClose` is not optional** — memory leaks otherwise.
- **Workspaces, not solutions** — a PowerBuilder solution is out of scope.
- **Strings from callbacks are transient** — copy them.
- `pbcPara` is reserved: always `NULL`.

---

## 19. Obsolete / removed API

**Obsolete (still present, EAServer-only, unsupported since PowerBuilder 2017):**

- `PBORCA_BuildProject` — deploy an EAServer component per the project object.
- `PBORCA_BuildProjectEx` — same, overriding server name/port (does not override server
  profile values).
- `PBORCA_BuildProjectWithOverrides` — same, forcing server profile + project overrides,
  requires login ID and password.
- `PBORCA_BLDERR` structure and its callback — for the above.
- Obsolete code-generation bits: 16-bit (`PBORCA_MACHINE_CODE_16`, `PBORCA_P_CODE_16`),
  `PBORCA_OPEN_SERVER`, PocketBuilder `PBORCA_PK_*`.
- `PBORCA_CHECKOUTERROR` (-18) — old source management error.

**Removed in PowerBuilder 9** (must be deleted from existing ORCA programs; replaced by the
`PBORCA_Scc*` family):

- `PBORCA_CheckOutEntry`
- `PBORCA_CheckInEntry`
- `PBORCA_ListCheckOutEntries`

**Documentation inconsistencies to be aware of** (the guide contradicts itself, and in places
disagrees with the shipped `pborca.h`):

- `SccSetPassword` is declared in `pborca.h` as
  `INT SccSetPassword(HPBORCA, LPTSTR pPassword)` and listed in the overview table, but the
  guide never documents it. It sets the password property before `SccConnect`.
- The overview table omits `SccResetRevisionNumber`, which *is* documented in full.
- `LibraryDirectory`'s recommended comment-buffer length is written `PBORCA_MAXCOMMENTS + 1`
  in prose while the header constant used everywhere else is `PBORCA_MAXCOMMENT` (value
  `255`; `PBORCA_MSGBUFFER` is `256`).
- `SessionOpen`'s "See also" references `PBORCA_SetCurrentAppl`; the real name is
  `PBORCA_SessionSetCurrentAppl`.
- `CompileEntryImport`'s printed syntax omits the `LPTSTR` before `lpszComments`, and
  `LibraryEntryExportEx`'s omits the comma before `plReturnSize`.
- The guide's structure tables are simplified. The header is authoritative and differs:
  `PBORCA_REFERENCE` carries a fourth field `otEntryRefType`; `PBORCA_EXEINFO` carries
  `lpszManifestInfo`; `PBORCA_SCC` holds `LONG lCapabilities` (not a pointer), an extra
  `fpOrcaMsgHandler`, `lCommentLen`, and trailing `lDeleteTempFiles` /
  `bDeletePblFlag` flags. On x86 the real sizes are `PBORCA_DIRENTRY` 528,
  `PBORCA_ENTRYINFO` 524, `PBORCA_CONFIG_SESSION` 44, `PBORCA_REFERENCE` 16,
  `PBORCA_SCC` 2572 bytes.
- The header also declares members the guide never mentions:
  `PBORCA_ApplicationRebuildEx`, `PBORCA_SccConnectOfflineToSourcControl`,
  `PBORCA_SetAutobuildLogProc` and `PBORCA_ImportPBeXtension`.
- `PBORCA_SessionOpen` is the **Unicode** session; ANSI clients are mapped to
  `PBORCA_SessionOpenA` by the header (`#define PBORCA_SessionOpen PBORCA_SessionOpenA` when
  neither `UNICODE` nor `_UNICODE` is defined). There are no per-function `W`/`A` variants —
  the string width of every later call follows the variant used to open the session.



