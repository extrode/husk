using System.Text;
using System.Text.Json;

namespace Extrode.Husk.Compiler;

/// <summary>Entry points for <c>Extrode.Husk.AspNetCore</c>, which loads this assembly into its own
/// load context from the build package's folder and binds these by reflection: only strings and
/// bools cross, so no type is shared between the contexts.</summary>
public static class Development
{
    /// <summary>Compiles one source as the build does. Returns JSON:
    /// <c>{ "outputs": { "Styled.svelte.js": "...", ... }, "warnings": [...], "error": null }</c>,
    /// each diagnostic <c>{ "code", "message", "line" (1-based), "column" (0-based) }</c>, and
    /// <c>outputs</c> empty when <c>error</c> is set.</summary>
    public static string Compile(string sveltePath, string projectDirectory, string sourcePath, string source, bool dev)
    {
        var result = SvelteCompiler.Shared(sveltePath).Compile(new CompileInput(SvelteCompiler.Filename(projectDirectory, sourcePath), source), dev);
        using var buffer = new MemoryStream();
        using (var json = new Utf8JsonWriter(buffer))
        {
            json.WriteStartObject();
            json.WriteStartObject("outputs");
            if (result.Succeeded)
                foreach (var (extension, text) in CompiledFiles.Texts(sourcePath, result))
                    json.WriteString(CompiledFiles.Name(sourcePath, extension), text);
            json.WriteEndObject();
            json.WriteStartArray("warnings");
            foreach (var warning in result.Warnings)
                Write(json, warning);
            json.WriteEndArray();
            json.WritePropertyName("error");
            if (result.Error is { } error)
                Write(json, error);
            else
                json.WriteNullValue();
            json.WriteEndObject();
        }
        return Encoding.UTF8.GetString(buffer.ToArray());
    }

    private static void Write(Utf8JsonWriter json, CompileDiagnostic d)
    {
        json.WriteStartObject();
        json.WriteString("code", d.Code);
        json.WriteString("message", d.Message);
        json.WriteNumber("line", d.Line);
        json.WriteNumber("column", d.Column);
        json.WriteEndObject();
    }
}
