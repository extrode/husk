#:package Extrode.Pare
// Writes <name>.svelte.ts.stripped.js for each .svelte.ts with Pare 0.6.0's strip: the text Husk
// hands compileModule, so reference.mjs can compile the same bytes under Node. With no arguments,
// beside each tests/fixtures source; with <root> <out>, under <out> at the source's path below <root>.
// Usage, from the repo root: dotnet run scripts/node-reference/strip-fixtures.cs [<root> <out>]
using Extrode.Pare;

string root = args.Length > 0 ? args[0] : Path.Combine("tests", "fixtures");
string? output = args.Length > 1 ? args[1] : null;
string[] skipped = ["node_modules", ".git", ".svelte-kit", "build", "dist"];
int failed = 0;
foreach (var path in Directory.EnumerateFiles(root, "*.svelte.ts", SearchOption.AllDirectories)
             .Where(p => !Path.GetRelativePath(root, p).Split(Path.DirectorySeparatorChar).Any(skipped.Contains))
             .Order(StringComparer.Ordinal))
{
    string? stripped = TypeStripper.Strip(File.ReadAllText(path), out var diagnostics);
    if (stripped is null)
    {
        foreach (var d in diagnostics)
            Console.Error.WriteLine($"{path}: {d.Code}");
        failed++;
        continue;
    }
    string target = (output is null ? path : Path.Combine(output, Path.GetRelativePath(root, path))) + ".stripped.js";
    Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(target))!);
    File.WriteAllText(target, stripped);
    Console.WriteLine($"stripped {path}");
}
// A corpus may hold TypeScript Pare cannot strip; reference.mjs records those as `strip` rows.
return failed == 0 || output is not null ? 0 : 1;
