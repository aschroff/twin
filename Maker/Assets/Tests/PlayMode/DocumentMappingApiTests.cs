using System.Collections;
using System.Collections.Generic;
using System.IO;
using Code;
using Code.AI;
using Code.AI.PromptGeneration;
using NoAPICalls;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// Sends a document to the LLM and checks what comes back can actually be applied to the twin:
/// the schema is accepted, the answer parses, and every name in it is one the app knows.
///
/// Calls the OpenAI API and needs a key in Assets/Tests/Helper/testsecrets.json - outside
/// NoAPICalls for that reason. The document is invented, so no patient data leaves the machine.
/// </summary>
public class DocumentMappingApiTests : TwinPaintTestBase
{
    /// <summary>Same pin as the AI component in the scene.</summary>
    private const string Model = "gpt-5.5-2026-04-23";

    /// <summary>Invented findings. Three that belong on the body, one that belongs to the
    /// patient as a whole and therefore has nowhere on the body to go.</summary>
    private const string TestDocument =
        "Test document, invented findings, not a real person.\n" +
        "1. Swelling of the left lower leg, from below the knee down to the ankle.\n" +
        "2. Burning pain on the front of the right forearm.\n" +
        "3. Reddened skin on both cheeks.\n" +
        "4. The patient reports being tired all day and sleeping badly.\n";

    /// <summary>A realistic report, in the repository so both this test and a hand test can use
    /// it. Fictional patient - see the notice on the document itself.</summary>
    private const string SampleReport = "Tests/Helper/lipoedema-report-sample.pdf";

    private static string DumpDir => Path.Combine(Application.temporaryCachePath, "DocumentPrompt");

    [UnityTest]
    public IEnumerator DocumentMapping_ComesBackWithNamesTheAppKnows()
    {
        if (!HasOpenAIApiKey)
        {
            Assert.Ignore("API key not configured. Create Assets/Tests/Helper/testsecrets.json from testsecrets.example.json");
            yield break;
        }

        yield return LoadLipEdemaTwin();

        var partManager = FindPartManager();
        var settingsManager = Object.FindObjectOfType<SettingsManager>(true);
        string prompt = DocumentPromptBuilder.Build(partManager, settingsManager);
        List<string> regionKeys = DocumentPromptBuilder.RegionKeys();

        // the document, as a file - anything that is not an image travels by upload
        string path = Path.Combine(Application.temporaryCachePath, "test_findings.txt");
        File.WriteAllText(path, TestDocument);

        var client = new OpenAIClient(OpenAIApiKey, timeout: 180);

        var upload = client.UploadFileAsync(path);
        while (!upload.IsCompleted) yield return null;
        Assert.IsNull(upload.Exception, "Uploading the document failed: " + upload.Exception?.Message);
        string fileId = upload.Result;
        Assert.IsNotEmpty(fileId);

        var allowedValues = new Dictionary<string, IEnumerable<string>>
        {
            { "paintings.regionKeys", regionKeys }
        };
        var request = client.RequestStructuredAsync<DocumentMapping>(
            prompt, Model, fileId: fileId, allowedValues: allowedValues);
        while (!request.IsCompleted) yield return null;
        Assert.IsNull(request.Exception, "The mapping request failed: " + request.Exception?.Message);

        DocumentMapping mapping = request.Result;
        Assert.IsNotNull(mapping, "No mapping came back.");

        Directory.CreateDirectory(DumpDir);
        File.WriteAllText(Path.Combine(DumpDir, "mapping.txt"), DocumentMappingText.Describe(mapping));
        Debug.Log("[Test] mapping:\n" + DocumentMappingText.Describe(mapping));

        // what the app has to be able to act on
        Assert.IsNotEmpty(mapping.Paintings, "The three findings on the body should produce paintings.");

        var knownTools = new List<string>();
        foreach (ToolInfo tool in ToolInventory.All()) knownTools.Add(tool.name);
        var knownGroups = new List<string>();
        foreach (PartManager.GroupData group in partManager.groups) knownGroups.Add(group.name);
        foreach (ProposedGroup proposed in mapping.NewGroups) knownGroups.Add(proposed.Name);

        foreach (ProposedPainting painting in mapping.Paintings)
        {
            Assert.IsNotEmpty(painting.RegionKeys, $"'{painting.FindingText}' names no body region.");
            foreach (string key in painting.RegionKeys)
            {
                CollectionAssert.Contains(regionKeys, key,
                    $"'{key}' is not a body region - the schema should have made that impossible.");
            }
            CollectionAssert.Contains(knownTools, painting.ToolName,
                $"'{painting.ToolName}' is not a tool of this app.");
            CollectionAssert.Contains(knownGroups, painting.Group,
                $"'{painting.Group}' is neither an existing group nor a proposed one.");
            Assert.IsNotEmpty(painting.Description, "A painting without a description cannot be applied.");
        }

        // the tiredness has nowhere on the body to go
        Assert.IsNotEmpty(mapping.PatientText,
            "What concerns the patient as a whole should come back as the patient text.");

        // a tool taken into use has to be one that was free
        var freeTools = new List<string>();
        foreach (ToolInfo tool in ToolInventory.Free()) freeTools.Add(tool.name);
        foreach (ProposedToolMeaning assignment in mapping.ToolAssignments)
        {
            CollectionAssert.Contains(freeTools, assignment.ToolName,
                $"'{assignment.ToolName}' already carries a meaning and must not be reassigned.");
        }
    }

    /// <summary>
    /// The same thing through the app: the process uploads, calls, and puts the proposal on the
    /// review screen. Uses the key the app itself resolves (see ApiKeys), so this also proves the
    /// app has one.
    /// </summary>
    [UnityTest]
    public IEnumerator UploadFlow_PutsTheProposalOnTheReviewScreen()
    {
        if (!HasOpenAIApiKey)
        {
            Assert.Ignore("API key not configured.");
            yield break;
        }

        yield return LoadLipEdemaTwin();
        yield return ClickButtonByPath("Canvas/Save UI/Top/GameObject/Back Button");
        yield return WaitForModeActive("Main");

        var ai = Object.FindObjectOfType<Code.AI.AI>(true);
        Assert.IsTrue(ai.hasApiKey,
            "The app found no API key. Put it into " + ApiKeys.SearchedPlaces());

        var process = Object.FindObjectOfType<DocumentUploadProcess>(true);

        // a real PDF, which is what a user picks - and a different upload path from a text file
        string path = Path.Combine(Application.dataPath, SampleReport);
        Assert.IsTrue(File.Exists(path), $"The sample report is missing: {path}");

        yield return ClickButtonByPath("Canvas/Main UI/Bottom/Upload/Icon");
        yield return WaitForModeActive("Upload");

        process.ShowPicked(path, null);
        yield return WaitForModeActive("UploadReview");

        float waited = 0f;
        while (process.lastMapping == null && waited < 120f)
        {
            waited += Time.deltaTime;
            yield return null;
        }
        Assert.IsNotNull(process.lastMapping, $"No mapping arrived within {waited:F0}s.");

        var review = Object.FindObjectOfType<DocumentReviewManager>(true);
        string shown = review.GetShownText();
        StringAssert.Contains("lipoedema-report-sample.pdf", shown);
        StringAssert.Contains("FINDINGS ON THE BODY", shown);
        StringAssert.Contains("ABOUT THE PATIENT", shown);

        // the report describes findings on both sides, so at least one has to name two regions
        bool bothSides = false;
        foreach (ProposedPainting painting in process.lastMapping.Paintings)
        {
            if (painting.RegionKeys != null && painting.RegionKeys.Count > 1) bothSides = true;
        }
        Assert.IsTrue(bothSides, "A report full of symmetrical findings produced no multi-region painting.");

        Directory.CreateDirectory(DumpDir);
        File.WriteAllText(Path.Combine(DumpDir, "report-mapping.txt"), shown);
        Debug.Log($"[Test] the review screen after {waited:F0}s:\n{shown}");
    }
}
