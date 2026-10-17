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

    [Theory]
    [InlineData("libc")]
    [InlineData("ClearScriptV8.dll")]
    [InlineData("Other.osx-arm64.dylib")]
    public void Other_libraries_are_left_to_the_default_probe(string name) =>
        Assert.Null(NativeProbe.Candidate("base", name));
}
