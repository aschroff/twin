using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

/// <summary>
/// Editor tooling to run PlayMode tests from automation (e.g. MCP) and write the
/// results to a file, surviving the domain reload that entering play mode causes.
/// The [InitializeOnLoad] static constructor re-registers the result callback after
/// every domain reload, so RunFinished always fires and the result file gets written.
/// </summary>
[InitializeOnLoad]
public static class TemplatePoCRunner
{
    private static string ResultsPath => Path.Combine(Application.dataPath, "..", "Temp", "TemplatePoCResults.json");

    static TemplatePoCRunner()
    {
        var api = ScriptableObject.CreateInstance<TestRunnerApi>();
        api.RegisterCallbacks(new ResultWriter());
    }

    [MenuItem("Tools/Template PoC/Run PlayMode Test")]
    public static void StartRun()
    {
        Run("NoAPICalls.ProgrammaticPaintingTests.PaintTemplateParts_ThreeRegions");
    }

    [MenuItem("Tools/Template PoC/Run SaveTwin Baseline Test")]
    public static void StartBaselineRun()
    {
        Run("NoAPICalls.SaveTwinPlayModeTests.SaveButton_OpensSaveMode");
    }

    private const string Gen = "TemplateLibraryTools.TemplateLibraryGenerator.";

    [MenuItem("Tools/Template Library/Batch 01 Torso Front")]
    public static void GenBatch01() { Run(Gen + "Batch01_TorsoFront"); }

    [MenuItem("Tools/Template Library/Batch 02 Torso Back")]
    public static void GenBatch02() { Run(Gen + "Batch02_TorsoBack"); }

    [MenuItem("Tools/Template Library/Batch 03 Arms Front")]
    public static void GenBatch03() { Run(Gen + "Batch03_ArmsFront"); }

    [MenuItem("Tools/Template Library/Batch 04 Arms Back")]
    public static void GenBatch04() { Run(Gen + "Batch04_ArmsBack"); }

    [MenuItem("Tools/Template Library/Batch 05 Legs Front")]
    public static void GenBatch05() { Run(Gen + "Batch05_LegsFront"); }

    [MenuItem("Tools/Template Library/Batch 06 Legs Back")]
    public static void GenBatch06() { Run(Gen + "Batch06_LegsBack"); }

    [MenuItem("Tools/Template Library/Batch 07 Head Neck")]
    public static void GenBatch07() { Run(Gen + "Batch07_HeadNeck"); }

    [MenuItem("Tools/Template Library/Batch 08 Hands")]
    public static void GenBatch08() { Run(Gen + "Batch08_Hands"); }

    [MenuItem("Tools/Template Library/Batch 09 Extended Misc")]
    public static void GenBatch09() { Run(Gen + "Batch09_ExtendedMisc"); }

    [MenuItem("Tools/Template Library/Run Batches 01+02")]
    public static void GenBatch0102() { Run(Gen + "Batch01_TorsoFront", Gen + "Batch02_TorsoBack"); }

    [MenuItem("Tools/Template Library/Run Diagnose")]
    public static void GenDiagnose() { Run(Gen + "Batch00_Diagnose"); }

    [MenuItem("Tools/Template Library/Run PartTemplateService Tests")]
    public static void RunServiceTests()
    {
        Run("NoAPICalls.PartTemplateServiceTests.PaintRegion_AddsPartToActiveGroup",
            "NoAPICalls.PartTemplateServiceTests.PaintRegion_UnknownRegion_ThrowsWithAvailableNames",
            "NoAPICalls.PartTemplateServiceTests.PaintRegion_WithTool_AppliesToolColorAndMetadata",
            "NoAPICalls.PartTemplateServiceTests.GetTemplateGroupNames_ListsAllArmRegions",
            "NoAPICalls.PartTemplateServiceTests.GetTemplateCatalog_ListsAllTwinsAndRegions",
            "NoAPICalls.PartTemplateServiceTests.PaintWithCurrentTool_UsesActiveTool_AndFallsBackToMarker",
            "NoAPICalls.PartTemplateServiceTests.LoadTwin_BindsCommandsToPaintableTexture",
            "NoAPICalls.PartTemplateServiceTests.SaveFileSize_GrowsLinearly_WithPartsInOneGroup",
            "NoAPICalls.PartTemplateServiceTests.LoadTwin_RelinksPartsToTheirGroups",
            "NoAPICalls.PartTemplateServiceTests.SavedTwin_ContainsNoPaintableTextureReferences");
    }

    private static void Run(params string[] testNames)
    {
        if (File.Exists(ResultsPath))
            File.Delete(ResultsPath);

        var api = ScriptableObject.CreateInstance<TestRunnerApi>();
        var filter = new Filter
        {
            testMode = TestMode.PlayMode,
            testNames = testNames
        };
        api.Execute(new ExecutionSettings(filter));
        Debug.Log("[TemplatePoCRunner] PlayMode test run started: " + string.Join(", ", testNames));
    }

    private class ResultWriter : ICallbacks
    {
        public void RunStarted(ITestAdaptor testsToRun) { }
        public void TestStarted(ITestAdaptor test) { }
        public void TestFinished(ITestResultAdaptor result) { }

        public void RunFinished(ITestResultAdaptor result)
        {
            var sb = new StringBuilder();
            sb.AppendLine("{");
            sb.AppendLine($"  \"status\": \"{result.TestStatus}\",");
            sb.AppendLine($"  \"passed\": {result.PassCount},");
            sb.AppendLine($"  \"failed\": {result.FailCount},");
            sb.AppendLine($"  \"skipped\": {result.SkipCount},");
            sb.AppendLine($"  \"duration\": {result.Duration.ToString(System.Globalization.CultureInfo.InvariantCulture)},");
            sb.AppendLine("  \"failures\": [");
            var first = true;
            AppendFailures(result, sb, ref first);
            sb.AppendLine();
            sb.AppendLine("  ]");
            sb.AppendLine("}");
            File.WriteAllText(ResultsPath, sb.ToString());
            Debug.Log($"[TemplatePoCRunner] Results written to {ResultsPath}");
        }

        private static void AppendFailures(ITestResultAdaptor result, StringBuilder sb, ref bool first)
        {
            if (result.HasChildren)
            {
                foreach (var child in result.Children)
                    AppendFailures(child, sb, ref first);
                return;
            }
            if (result.TestStatus != TestStatus.Failed)
                return;
            if (!first)
                sb.AppendLine(",");
            first = false;
            var message = Escape(result.Message) + "\\n" + Escape(result.StackTrace);
            sb.Append($"    {{ \"test\": \"{Escape(result.FullName)}\", \"message\": \"{message}\" }}");
        }

        private static string Escape(string value)
        {
            return string.IsNullOrEmpty(value)
                ? ""
                : value.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "");
        }
    }
}
