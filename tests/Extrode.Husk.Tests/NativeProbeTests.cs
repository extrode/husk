using Extrode.Husk.Compiler;

namespace Extrode.Husk.Tests;

public sealed class NativeProbeTests
{
    [Theory]
    [InlineData("ClearScriptV8.osx-arm64.dylib", "osx-arm64")]
    [InlineData("ClearScriptV8.win-x64.dll", "win-x64")]
    [InlineData("ClearScriptV8.linux-arm64.so", "linux-arm64")]
    public void The_rid_folder_comes_from_the_library_name(string name, string rid) =>
        Assert.Equal(Path.Combine("base", "runtimes", rid, "native", name), NativeProbe.Candidate("base", name));

    [Fact]
    public void The_native_package_is_looked_for_beside_the_husk_package_in_the_cache()
    {
        string cache = Path.Combine("home", ".nuget", "packages");
        string dir = Path.Combine(cache, "extrode.husk.build", "0.1.0", "tasks", "net10.0");
        Assert.Equal(
            Path.Combine(cache, "microsoft.clearscript.v8.native.osx-arm64", "7.5.1.1", "runtimes", "osx-arm64", "native", "ClearScriptV8.osx-arm64.dylib"),
            NativeProbe.PackageCandidate(dir + Path.DirectorySeparatorChar, "ClearScriptV8.osx-arm64.dylib", new Version(7, 5, 1, 1)));
    }

    [Theory]
    [InlineData("base")]
    [InlineData("a/b/net10.0")]
    public void A_folder_outside_a_tasks_layout_has_no_package_candidate(string dir) =>
        Assert.Null(NativeProbe.PackageCandidate(dir, "ClearScriptV8.osx-arm64.dylib", new Version(7, 5, 1, 1)));

    [Fact]
    public void A_library_that_is_not_clearscripts_has_no_package_candidate() =>
        Assert.Null(NativeProbe.PackageCandidate(Path.Combine("c", "p", "v", "tasks", "net10.0"), "libc", new Version(7, 5, 1, 1)));

    [Theory]
    [InlineData("libc")]
    [InlineData("ClearScriptV8.dll")]
    [InlineData("Other.osx-arm64.dylib")]
    public void Other_libraries_are_left_to_the_default_probe(string name) =>
        Assert.Null(NativeProbe.Candidate("base", name));

    [Fact]
    public void The_development_app_looks_for_the_process_native_beside_the_compiler_then_in_the_cache()
    {
        string v8 = Path.Combine(Path.GetDirectoryName(typeof(Extrode.Husk.Compiler.SvelteCompiler).Assembly.Location)!, "ClearScript.V8.dll");
        string cache = Path.Combine(Path.GetTempPath(), "husk-native-" + Guid.NewGuid().ToString("N"));
        string dir = Path.Combine(cache, "extrode.husk.build", "1.0.0", "tasks", "net10.0");
        Directory.CreateDirectory(dir);
        try
        {
            File.Copy(v8, Path.Combine(dir, "ClearScript.V8.dll"));
            string compiler = Path.Combine(dir, "Extrode.Husk.Compiler.dll");

            string expected = Assert.IsType<string>(Extrode.Husk.AspNetCore.CompilerBridge.MissingNative(compiler));
            Assert.StartsWith(Path.Combine(cache, "microsoft.clearscript.v8.native."), expected, StringComparison.Ordinal);
            Assert.Contains(Path.Combine("7.5.1.1", "runtimes"), expected, StringComparison.Ordinal);

            Directory.CreateDirectory(Path.GetDirectoryName(expected)!);
            File.WriteAllText(expected, "");
            Assert.Null(Extrode.Husk.AspNetCore.CompilerBridge.MissingNative(compiler));
        }
        finally
        {
            Directory.Delete(cache, recursive: true);
        }
    }
}
