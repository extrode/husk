# Contributing

Thanks for looking. Husk is small and opinionated, so it helps to talk before you build something big: open an issue first.

## Set up

You need the .NET 10 SDK. Clone the repo next to [Pare](https://github.com/extrode/pare) if you plan to change the Pare side.

```sh
dotnet build Husk.slnx
dotnet test --solution Husk.slnx
```

The integration tests run real builds and real browsers (Chrome, Firefox, Safari on macOS), so they are slow. The unit tests take seconds.

## Rules that matter

- **Tests with every change.** A bug fix has a test that fails without the fix.
- **No Node or Deno at build time** in anything Husk ships. Node may appear in research.
- **No new package** without discussing it first.
- **C# sources are ASCII.**
- Branch off `dev`; `main` is for releases. Conventional commit prefixes (`feat:`, `fix:`, `docs:`).

## Licensing

By contributing you agree your work is licensed under the project's license (ISL-P 1.2 with ISL-OE 1.2, see `LICENSE.md`).
