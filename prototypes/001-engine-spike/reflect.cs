#:property PublishAot=false
#:property TreatWarningsAsErrors=false
// Lists ClearScript.V8's Compile overloads, flags and settings, to avoid guessing its API.
using System.Reflection;

var asm = Assembly.LoadFrom(args[0]);
Assembly.LoadFrom(Path.Combine(Path.GetDirectoryName(args[0])!, "ClearScript.Core.dll"));
var t = asm.GetType("Microsoft.ClearScript.V8.V8ScriptEngine")!;
foreach (var m in t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly).Where(m => m.Name is "Compile" or "Evaluate"))
    Console.WriteLine($"{m.Name}({string.Join(", ", m.GetParameters().Select(p => p.ParameterType.Name + " " + p.Name))})");
foreach (var c in t.GetConstructors()) Console.WriteLine($"ctor({string.Join(", ", c.GetParameters().Select(p => p.ParameterType.Name + " " + p.Name))})");
foreach (var n in new[] { "V8ScriptEngineFlags", "V8CacheKind", "V8CacheResult", "V8GlobalFlags" })
{
    var e = asm.GetType("Microsoft.ClearScript.V8." + n);
    Console.WriteLine(n + ": " + (e is null ? "missing" : string.Join(" ", Enum.GetNames(e))));
}
var rc = asm.GetType("Microsoft.ClearScript.V8.V8RuntimeConstraints")!;
Console.WriteLine("V8RuntimeConstraints: " + string.Join(" ", rc.GetProperties().Select(p => p.Name)));
var s = asm.GetType("Microsoft.ClearScript.V8.V8Settings");
Console.WriteLine("V8Settings: " + (s is null ? "missing" : string.Join(" ", s.GetProperties(BindingFlags.Public | BindingFlags.Static).Select(p => p.Name))));
