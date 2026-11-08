# Performance audit: 2026-11-08

Minimal Editor had measurable room for improvement. The changes in this audit
reduce Console processing and temporary allocations while preserving snapshot
validation, metadata, logger ownership, and Play Mode lifecycle behavior.
They do not establish an absolute optimum across every workload or platform.

## Results

The table uses medians from nine timed samples per case. Managed scan fixtures
exclude Unity's native row reading and restoration; native cases include them.
All allocation figures are managed bytes allocated, not retained or native memory.

| Workload | Before | After | Time reduction | Allocations before / after |
| --- | ---: | ---: | ---: | ---: |
| Native Console: 10,000 rows, no match | 4.935200 ms | 3.654100 ms | 26.0% | 2,400,152 / 1,920,152 B |
| Native Console: 10,000 long stacks, cleanup stress test | 332.552500 ms | 152.422300 ms | 54.2% | 568,880,896 / 243,920,368 B |
| Managed scan: 10,000 short stacks, cleanup | 9.956100 ms | 3.199600 ms | 67.9% | 11,120,200 / 4,320,200 B |
| Package duplicate: 1,000 imported assets | 0.106523 ms | 0.053192 ms | 50.1% | 56,912 / 928 B |
| Menu wildcard match | 0.000036 ms | 0.000012 ms | 67.2% | 40 / 0 B |

The native cleanup stress case contains long stacks captured through the command
runner, including an explicit test helper frame. It is not a prediction for
ordinary short application stacks. Its remaining cost is still substantial:
processing a very large Console can stall the Editor even after this improvement.
The native fixture verifies both row counts and removal of the helper frame.

The first optimized managed run is used throughout these tables. A second run
produced 0.471 ms versus 0.477 ms for the 10,000-row no-match scan, and 3.162 ms
versus 3.200 ms for short-stack cleanup. Small nanosecond-scale differences in
unchanged functions are not treated as improvements. Raw samples, including
outliers, are retained alongside the report.

## Changes kept

- Cache the string-field classification for Console rows and compile integer
  readers once, avoiding per-row boxing. Reflection remains the fallback when
  a runtime cannot create the optimized accessor, including enum-valued fields.
- Copy modified snapshots with a cached shallow-copy delegate. Retain the
  field-copy fallback, and keep every metadata field and the original row intact.
- Walk stack text by character ranges. Avoid arrays of split lines, temporary
  message prefixes, and separate stack substrings. Preserve CRLF handling,
  leading/trailing blank-line behavior, Unicode offsets, and message text.
- Allocate the retained-row list only when a row survives filtering. A fully
  suppressed managed scan no longer allocates an array sized to all input rows.
- Reuse consecutive imported package names while checking Asset Store duplicates.
  Preserve package boundaries, mixed packages, case rules, and embedded-package
  visibility. The measured 1,000-asset case falls from roughly 57 KB to 1 KB.
- Match wildcard menu prefixes directly without allocating a substring.

## Alternatives and remaining work

| Area | Evidence and decision |
| --- | --- |
| Idle callbacks | Console and status-bar time guards allocate 0 B in the benchmark. Keep the existing bounded polling. Removing these guards would not address active scan costs. |
| Unchanged Console | Repeated unchanged snapshots allocate 0 B and skip row reading. Preserve both version and count checks; a count-only or append-only cache can miss replacement rows. |
| Snapshot copying | The direct candidate benchmark compares reflection copying with a shallow-copy delegate. The delegate allocates 72 B rather than 264 B per current Unity row. Keep the guarded fast path. |
| Integer reflection | The direct candidate benchmark compares boxed field reads with a compiled integer reader. The compiled path allocates 0 B rather than 24 B per read. |
| Regex construction | Interpreted regexes are cheaper to construct, while compiled regexes match faster in the measured repeated workload. Keep compiled rules and their timeout. Settings changes, not individual logs, rebuild rules. |
| Contains versus regex | Both are measured with 1, 10, and 100 rules. Use Contains for literal matching. Regex semantics cannot generally be replaced with literal matching. |
| Many rules | Last-hit and no-match costs grow with rule count. Combining regexes or adding a multi-pattern matcher would need separate Unicode, timeout, and mixed-rule equivalence tests. No rewrite is justified for the common small rule sets here. |
| Native Console calls | Native reads and restoration remain a significant part of full scan time. Moving Unity calls to workers or assuming old rows cannot change would violate the current safety contract. A future time-sliced design needs its own consistency model. |
| Native menu titles | A 12-root native menu is measured separately. At the existing 0.2-second interval its cost is small. Caching mutable native titles would need invalidation for renames and replacement items. |
| Status bar | Active application is measured separately from the idle guard. The expensive lookup runs at a 15-second fallback interval, so caching window objects is unlikely to pay for additional lifecycle complexity. |
| Window title | The title callback runs on title changes. Its measured allocation is event-driven, not per Editor frame. |
| Folder rules | 5, 50, and 500 hidden rules are measured. Path checks allocate 0 B; native GUID-to-path resolution allocates and has a separate measurement. A trie or a per-refresh GUID cache is a candidate for projects with hundreds of rules, not a measured improvement in this change. |
| Toolbar UI and preferences | Inspectors, preferences, toolbar refresh hooks, and GUI layout were reviewed for continuous work. Integration tests cover behavior, but full repaint/layout cost was not isolated in this audit. |
| FMOD and optional integrations | FMOD was not installed. The optional Console toolbar lookup's cached path is measured, but real optional integration performance is not established. |
| Startup, native memory, and rendering | Cold startup, peak memory, native allocation, GPU rendering, and whole-Editor frame rate are not measured by this harness. No claims are made for them. |

## Method

- Unity `7000.0.0a16` (`0745a91df592`), CoreCLR `.NET 10.0.10-dev`.
- macOS `27.2`, Apple M2 Max; Unity CLI `1.0.0-beta.17`, Pipeline `0.10.0-exp.1`.
- Final comparisons use one isolated visible Editor with Minimal Editor embedded,
  the package's bundled Harmony, and test tooling. Other workspace packages are
  absent. No preferences are saved by the benchmarks.
- 73 distinct cases, including controls and candidate implementations: 54 managed
  cases, 9 native Console cases, and 10 additional native/event/path cases.
- Managed cases use four warmup calls, adaptive batch sizes targeting at least
  12 ms (capped at 262,144 iterations), and nine measured batches. CPU time uses
  `Stopwatch`; allocations use `GC.GetAllocatedBytesForCurrentThread`.
- Native cases use one warmup scan and nine prepared samples. Seeding and checking
  results are outside timing. Each scan uses the real reflected native API.
- Native stack fixtures emit real warnings through a non-inlined test helper.
  Unity's add-entry API does not preserve stack offsets, so inserting text alone
  is not a valid native cleanup benchmark. An initial invalid fixture was
  rejected and its measurements are excluded.
- Baseline product files came from commit `7fbdb16cebbe94fb458d98dc2907af3158340444`. The same harness
  was used with the changed product files. Only the isolated fixture was switched
  to the baseline. The working checkout retained its changes.
- See [source hashes](Benchmarks/2026-11-08/source-hashes.json) for the exact
  optimized source and assembly definitions. These are local results, not a
  cross-platform release certification or a statistical confidence interval.

## Verification

The final isolated embedded-package run passed **229/229 tests**, with no failures,
skips, or inconclusive results, after successful Unity compilation. Every C# file
in the fixture matched the working package byte for byte. Coverage includes:

- 600 deterministic stack-text examples with mixed line endings, blank lines,
  Unicode, and message prefixes; snapshot metadata and reflection fallbacks.
- Package-name deduplication across mixed separators, similar prefixes, and
  non-adjacent names; ordinal menu-prefix behavior.
- Existing incomplete-snapshot, concurrent-change, restoration-failure,
  logger-ownership, regex-timeout, and native reflection integration regressions.
- Six Play sessions without domain reload, with scene reload on and off. The new
  accessor delegates and descriptors remain the same objects across transitions.

During setup, the headless workspace run passed 228/229 tests but did not deliver
the native Console-change notification. That test passed in the visible fixture.
The first visible fixture used an external local dependency and passed 228/229;
the toolbar test expects an embedded package. Embedding the package produced the
final green result. A benchmark-only title-constructor compilation error was
corrected before final measurements. These earlier runs are not the final gate.

The shared workspace's visible Editor stalled in Project Icon's native icon
update according to an OS process sample. That task-owned instance was stopped;
final performance and integration results come from the isolated Editor.

Unity 2021.3, Unity 2022.3, Unity 6, Windows, Linux, and real FMOD/Assistant
integrations were not tested. Only Unity `7000.0.0a16` was installed. The package's
minimum Unity version, licenses, binary contents, and importer constraints were
not changed.

Final verification also passed a fresh one-shot compilation in the shared
workspace, Markdown lint, package dry-run packaging, source-hash comparisons,
and diff whitespace checks. The compile log contains no C# compiler errors and
ends with Unity's successful batch-exit marker. Task-owned Editors were stopped,
and the disposable fixture and its generated warning logs were removed.

## Reproduce

Enable this package's tests in an isolated project and discover its installed
Pipeline command schemas. With the tested CLI/Pipeline version, run from that
project's root:

```sh
unity --format json command script recompile --project-path "$PWD"
unity --format json command script recompile_status --project-path "$PWD"
unity --format json command script eval --code 'return MinimalEditor.Tests.Editor.Console.MinimalEditorBenchmarks.Run("Library/MinimalEditorBenchmarks/managed.json");' --timeout 180 --project-path "$PWD"
unity --format json command script eval --code 'return MinimalEditor.Tests.Editor.Console.MinimalEditorBenchmarks.RunAdditional("Library/MinimalEditorBenchmarks/additional.json");' --timeout 180 --project-path "$PWD"
```

Inspect compilation status before running benchmarks. For native Console tests,
use a disposable visible Editor: the following deliberately clears and replaces
that Editor's Console contents and emits many warning rows. Do not run it in an
Editor containing logs you want to preserve.

```sh
unity --format json command script eval --code 'return MinimalEditor.Tests.Editor.Console.MinimalEditorBenchmarks.RunNativeConsole("Library/MinimalEditorBenchmarks/native.json", true);' --timeout 180 --project-path "$PWD"
```

The harness lives in the existing Console test assembly and is invoked explicitly.
It does not run during normal EditMode test execution. The additional native-menu
case uses the existing Menu Item test fixture and skips it when that module is
unavailable. Recreate the same module configuration for comparisons.

## Complete managed measurements

Times are microseconds per operation; allocation columns are bytes per operation.
The no-op harness and candidate cases are controls, not product end-to-end costs.

| Case | Before µs | After µs | Before B | After B |
| --- | ---: | ---: | ---: | ---: |
| `harness/empty` | 0.002 | 0.002 | 0 | 0 |
| `logger/contains/0/miss` | 0.004 | 0.003 | 0 | 0 |
| `logger/contains/0/log-bypass` | 0.004 | 0.003 | 0 | 0 |
| `logger/contains/1/miss` | 0.015 | 0.012 | 0 | 0 |
| `logger/contains/1/log-bypass` | 0.004 | 0.003 | 0 | 0 |
| `logger/contains/1/first-hit` | 0.016 | 0.013 | 0 | 0 |
| `logger/contains/1/last-hit` | 0.016 | 0.016 | 0 | 0 |
| `logger/regex/1/miss` | 0.042 | 0.041 | 0 | 0 |
| `logger/contains/10/miss` | 0.104 | 0.103 | 0 | 0 |
| `logger/contains/10/log-bypass` | 0.004 | 0.004 | 0 | 0 |
| `logger/contains/10/first-hit` | 0.016 | 0.016 | 0 | 0 |
| `logger/contains/10/last-hit` | 0.173 | 0.177 | 0 | 0 |
| `logger/regex/10/miss` | 0.367 | 0.367 | 0 | 0 |
| `logger/contains/100/miss` | 0.947 | 0.948 | 0 | 0 |
| `logger/contains/100/log-bypass` | 0.004 | 0.004 | 0 | 0 |
| `logger/contains/100/first-hit` | 0.016 | 0.016 | 0 | 0 |
| `logger/contains/100/last-hit` | 1.578 | 1.574 | 0 | 0 |
| `logger/regex/100/miss` | 4.390 | 4.428 | 0 | 0 |
| `logger/formatted-warning` | 0.063 | 0.063 | 64 | 64 |
| `stack/clean-20-lines` | 0.336 | 0.338 | 0 | 0 |
| `stack/dirty-41-lines` | 4.145 | 2.467 | 12,472 | 7,536 |
| `regex/build-compiled` | 8.261 | 8.321 | 9,000 | 9,000 |
| `regex/build-interpreted` | 1.271 | 1.280 | 1,688 | 1,688 |
| `regex/compiled-match` | 0.034 | 0.030 | 0 | 0 |
| `regex/interpreted-match` | 0.085 | 0.087 | 0 | 0 |
| `console/100/unchanged` | 0.011 | 0.010 | 0 | 0 |
| `console/100/changed-no-match` | 12.457 | 5.670 | 5,656 | 856 |
| `console/100/stack-cleanup` | 87.220 | 33.211 | 111,392 | 43,392 |
| `console/100/all-hidden` | 4.994 | 2.852 | 3,376 | 120 |
| `console/100/cleanup-disabled` | 10.117 | 4.459 | 3,256 | 856 |
| `console/1000/unchanged` | 0.011 | 0.009 | 0 | 0 |
| `console/1000/changed-no-match` | 122.821 | 47.038 | 56,056 | 8,056 |
| `console/1000/stack-cleanup` | 858.325 | 279.548 | 1,112,192 | 432,192 |
| `console/1000/all-hidden` | 48.506 | 27.432 | 32,184 | 128 |
| `console/1000/cleanup-disabled` | 96.142 | 44.019 | 32,056 | 8,056 |
| `console/10000/unchanged` | 0.011 | 0.009 | 0 | 0 |
| `console/10000/changed-no-match` | 1217.863 | 476.909 | 560,056 | 80,056 |
| `console/10000/stack-cleanup` | 9956.100 | 3199.600 | 11,120,200 | 4,320,200 |
| `console/10000/all-hidden` | 474.041 | 266.427 | 320,184 | 128 |
| `console/10000/cleanup-disabled` | 1008.550 | 443.753 | 320,056 | 80,056 |
| `idle/console-update-no-rules` | 0.033 | 0.029 | 0 | 0 |
| `console/native-count` | 0.211 | 0.204 | 72 | 72 |
| `candidate/snapshot-reflected-copy` | 0.251 | 0.235 | 264 | 264 |
| `candidate/snapshot-memberwise-copy` | 0.046 | 0.041 | 72 | 72 |
| `candidate/mode-reflection` | 0.012 | 0.011 | 24 | 24 |
| `candidate/mode-compiled-getter` | 0.002 | 0.002 | 0 | 0 |
| `packages/duplicate/0-assets` | 1.604 | 1.354 | 912 | 928 |
| `packages/duplicate/100-assets` | 12.309 | 6.529 | 6,512 | 928 |
| `packages/duplicate/1000-assets` | 106.523 | 53.192 | 56,912 | 928 |
| `packages/non-store` | 0.044 | 0.038 | 0 | 0 |
| `folders/path-check-current-settings` | 0.009 | 0.008 | 0 | 0 |
| `idle/status-bar-poll` | 0.035 | 0.029 | 0 | 0 |
| `menus/wildcard-match` | 0.036 | 0.012 | 40 | 0 |
| `console/optional-toolbar-cached` | 0.009 | 0.007 | 0 | 0 |

## Complete native Console measurements

| Case | Before µs | After µs | Before B | After B |
| --- | ---: | ---: | ---: | ---: |
| `native-console/100/changed-no-match` | 47.700 | 40.400 | 24,152 | 19,352 |
| `native-console/100/stack-cleanup` | 2555.700 | 1287.700 | 5,689,160 | 2,439,560 |
| `native-console/100/all-hidden` | 45.900 | 36.100 | 22,744 | 19,488 |
| `native-console/1000/changed-no-match` | 500.800 | 355.500 | 240,152 | 192,152 |
| `native-console/1000/stack-cleanup` | 22223.500 | 14042.200 | 56,888,360 | 24,392,360 |
| `native-console/1000/all-hidden` | 387.900 | 362.600 | 224,352 | 192,296 |
| `native-console/10000/changed-no-match` | 4935.200 | 3654.100 | 2,400,152 | 1,920,152 |
| `native-console/10000/stack-cleanup` | 332552.500 | 152422.300 | 568,880,896 | 243,920,368 |
| `native-console/10000/all-hidden` | 4244.200 | 3783.300 | 2,240,352 | 1,920,296 |

## Additional unchanged paths

These are measurements of the final code, not claimed before/after improvements.

| Case | µs per operation | B per operation |
| --- | ---: | ---: |
| `folders/5-rules/miss` | 0.025 | 0 |
| `folders/5-rules/last-hit` | 0.033 | 0 |
| `folders/50-rules/miss` | 0.220 | 0 |
| `folders/50-rules/last-hit` | 0.308 | 0 |
| `folders/500-rules/miss` | 1.410 | 0 |
| `folders/500-rules/last-hit` | 3.270 | 0 |
| `folders/native-guid-to-path` | 0.445 | 40 |
| `status-bar/active-apply` | 50.406 | 264 |
| `title/update-callback` | 1.112 | 176 |
| `menus/native-12-roots` | 1.027 | 576 |

## Raw data

- [Managed baseline](Benchmarks/2026-11-08/baseline.json)
- [Managed optimized](Benchmarks/2026-11-08/optimized.json)
- [Managed optimized repeat](Benchmarks/2026-11-08/optimized-repeat.json)
- [Native Console baseline](Benchmarks/2026-11-08/native-baseline.json)
- [Native Console optimized](Benchmarks/2026-11-08/native-optimized.json)
- [Additional paths](Benchmarks/2026-11-08/additional-final.json)
