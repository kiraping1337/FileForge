# Third-party notices

FileForge uses the components listed below. Their licenses apply to those components, not to
FileForge's own code. Exact package versions are in the `.csproj` files and in
`frontend/package.json`.

This repository contains no third-party binaries. Docker images are built locally from official base
images and Alpine Linux packages. ImageMagick and FFmpeg are not linked into FileForge: they run as
separate programs in their own containers.

## Backend (.NET)

| Component                                   | License           |
|---------------------------------------------|-------------------|
| .NET runtime, ASP.NET Core                  | MIT               |
| Entity Framework Core                       | MIT               |
| Microsoft.Extensions.* packages             | MIT               |
| Npgsql Entity Framework Core provider       | PostgreSQL License |

## Tests

| Component                                   | License           |
|---------------------------------------------|-------------------|
| xUnit.net v3, xunit.runner.visualstudio     | Apache-2.0        |
| Microsoft.NET.Test.Sdk                      | MIT               |
| Microsoft.AspNetCore.Mvc.Testing            | MIT               |
| Microsoft.Extensions.TimeProvider.Testing   | MIT               |
| Testcontainers for .NET                     | MIT               |

## Frontend

| Component                                   | License           |
|---------------------------------------------|-------------------|
| Angular                                     | MIT               |
| RxJS                                        | Apache-2.0        |
| tslib                                       | 0BSD              |
| Archivo font (via Google Fonts)             | SIL OFL 1.1       |
| Big Shoulders Display font (via Google Fonts) | SIL OFL 1.1     |

## Runtime images and tools

| Component                                   | License           | Used in                     |
|---------------------------------------------|-------------------|-----------------------------|
| PostgreSQL                                  | PostgreSQL License | `postgres`                 |
| nginx                                       | BSD-2-Clause      | `web`                       |
| Node.js                                     | MIT               | `web` (build stage only)    |
| Docker CLI                                  | Apache-2.0        | `sandbox-runner`            |
| Alpine Linux                                | various, per package | base of most images      |

## Converters

| Component                                   | License                         |
|---------------------------------------------|---------------------------------|
| ImageMagick                                 | ImageMagick License (Apache-2.0 style) |
| libjpeg-turbo                               | IJG, BSD-3-Clause, zlib         |
| libpng                                      | libpng License                  |
| libwebp                                     | BSD-3-Clause                    |
| FFmpeg                                      | LGPL-2.1-or-later; the Alpine build includes GPL components, so that binary is GPL-2.0-or-later |
| x264 (H.264 encoder)                        | GPL-2.0-or-later                |
| libvpx (VP9 encoder)                        | BSD-3-Clause                    |
| Opus                                        | BSD-3-Clause                    |
| LAME (MP3 encoder)                          | LGPL-2.0-or-later               |
| libvorbis                                   | BSD-3-Clause                    |

The full license texts are published on each project's website and in its source repository.
