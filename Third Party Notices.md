# Third Party Notices

This package includes package-owned builds of third-party software. Those
components remain licensed under their own terms.

## Harmony

| Field | Value |
| --- | --- |
| Component | Harmony 2.4.1 for Mono; Harmony 2.4.2 for CoreCLR |
| Mono binary | `ThirdParty/Essentials.MinimalEditor.Harmony.dll` (.NET Framework 4.7.2) |
| CoreCLR binary | `ThirdParty/CoreCLR/Essentials.MinimalEditor.Harmony.dll` (.NET 10) |
| Project | [pardeike/Harmony](https://github.com/pardeike/Harmony) |
| License | MIT License |
| Copyright | Copyright (c) Andreas Pardeike |

Unity 7 and newer select the CoreCLR build. Earlier Editors retain the Mono
build. Both importers are Editor-only and use complementary version constraints,
so only one build is loaded. The Package Manager and Project Folders modules
share these binaries.

The CoreCLR binary is the unmodified `net10.0/0Harmony.dll` from the official
[Harmony 2.4.2 release](https://github.com/pardeike/Harmony/releases/tag/v2.4.2.0),
renamed to retain this package's explicit assembly reference. The release archive
is `Harmony-Fat.2.4.2.0.zip`; its SHA-256 matches the digest published with the
GitHub release asset.

| File | SHA-256 |
| --- | --- |
| `Harmony-Fat.2.4.2.0.zip` | `a5fc5f9d9640b927d786a0527faa18bf7aa776788235140c59e9b73de87a7774` |
| CoreCLR binary | `fd77b88724f4104440df0cf979a851d35eec75ea3a7e86297d04abe47c71aff6` |

### MIT License

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
