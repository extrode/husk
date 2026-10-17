#:package Extrode.Pare
// Writes <name>.svelte.ts.stripped.js beside each tests/fixtures .svelte.ts, with Pare 0.6.0's strip:
// the text Husk hands compileModule, so reference.mjs can compile the same bytes under Node.
// Usage, from the repo root: dotnet run scripts/node-reference/strip-fixtures.cs
using Extrode.Pare;

int failed = 0;
foreach (var path in Directory.GetFiles(Path.Combine("tests", "fixtures"), "*.svelte.ts", SearchOption.AllDirectories).Order(StringComparer.Ordinal))
{
    string? stripped = TypeStripper.Strip(File.ReadAllText(path), out var diagnostics);
    if (stripped is null)
    {
        foreach (var d in diagnostics)
            Console.Error.WriteLine($"{path}: {d.Code}");
        failed++;
        continue;
    }
    File.WriteAllText(path + ".stripped.js", stripped);
    Console.WriteLine($"stripped {path}");
}
return failed == 0 ? 0 : 1;
