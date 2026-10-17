using System.Collections;
using Microsoft.Build.Framework;

namespace Extrode.Husk.Tests;

/// <summary>Records what a task logs, as MSBuild would print it.</summary>
public sealed class FakeBuildEngine : IBuildEngine
{
    public List<BuildErrorEventArgs> Errors { get; } = [];
    public List<BuildWarningEventArgs> Warnings { get; } = [];
    public List<string> Messages { get; } = [];

    public bool ContinueOnError => false;
    public int LineNumberOfTaskNode => 0;
    public int ColumnNumberOfTaskNode => 0;
    public string ProjectFileOfTaskNode => "test.proj";

    public void LogErrorEvent(BuildErrorEventArgs e) => Errors.Add(e);
    public void LogWarningEvent(BuildWarningEventArgs e) => Warnings.Add(e);
    public void LogMessageEvent(BuildMessageEventArgs e) => Messages.Add(e.Message ?? "");
    public void LogCustomEvent(CustomBuildEventArgs e) { }

    public bool BuildProjectFile(string projectFileName, string[] targetNames, IDictionary globalProperties, IDictionary targetOutputs) =>
        throw new NotSupportedException();

    public static string Line(BuildErrorEventArgs e) => $"{e.File}({e.LineNumber},{e.ColumnNumber}): error {e.Code}: {e.Message}";
    public static string Line(BuildWarningEventArgs e) => $"{e.File}({e.LineNumber},{e.ColumnNumber}): warning {e.Code}: {e.Message}";
}
