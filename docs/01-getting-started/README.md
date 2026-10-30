# Getting started

The short version: add three packages, put a component under `wwwroot`, call `AddHusk()`, run `dotnet watch`. [Your first app](first-app.md) does it step by step and ends with a working page.

## Requirements

- .NET SDK 10.
- A build machine that is Windows, macOS or glibc Linux, on x64 or arm64. Otherwise the build stops with [HUSK001](../03-reference/error-codes.md).
- An ASP.NET Core web project.

## Packages

| Package | Reference as |
|---|---|
| `Extrode.Pare.Build` | `PrivateAssets="all"`, directly |
| `Extrode.Husk.Build` | `PrivateAssets="all"`, directly |
| `Extrode.Husk.AspNetCore` | normal reference |

All three are referenced directly because NuGet imports build targets only for direct references, and Husk's build runs inside Pare's. Leave out `Extrode.Pare.Build` and the build stops with HUSK002.
