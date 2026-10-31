# Getting started

The short version: add three packages, put a component under `wwwroot`, call `AddHusk()`, run `dotnet watch`. [Your first app](first-app.md) does it step by step and ends with a working page.

## Requirements

- .NET SDK 10.
- A build machine that is Windows, macOS or glibc Linux, on x64 or arm64. Otherwise the build stops with [HUSK001](../03-reference/error-codes.md).
- An ASP.NET Core web project.

## Packages

| Package | Reference as |
|---|---|
| `Extrode.Husk.Build` | `PrivateAssets="all"`, directly |
| `Extrode.Husk.AspNetCore` | normal reference |

Husk's build runs inside Pare's. `Extrode.Husk.Build` brings `Extrode.Pare.Build` in; an app that already references it directly keeps working, at 0.7.2 or later (an older one is NU1605).
