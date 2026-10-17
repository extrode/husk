namespace Extrode.Husk.Tests;

public sealed class AssemblyTests
{
    [Fact]
    public void The_three_assemblies_are_referenced()
    {
        string[] names = ["Extrode.Husk.Compiler", "Extrode.Husk.Build", "Extrode.Husk.AspNetCore"];
        foreach (var name in names)
            Assert.True(File.Exists(Path.Combine(AppContext.BaseDirectory, name + ".dll")), name);
    }
}
