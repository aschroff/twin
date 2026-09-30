/// <summary>
/// The address of a test in the process landscape (`Assets/Tests/PROCESS_LANDSCAPE.md`).
/// </summary>
/// <remarks>
/// <para>Every test carries exactly one of these as its NUnit category, and
/// <c>TestCategoriesGuardTests</c> fails the build when one carries none or more than one. The
/// names are constants rather than free text so that a typo is a compile error instead of a test
/// that quietly drops out of the map.</para>
///
/// <para>The category says which part of the app a test belongs to. It does <b>not</b> say what a
/// test costs — that is what the folders are for, and only they are a promise:
/// <c>EditMode/</c> and <c>PlayMode/NoAPICalls/</c> never call an external service,
/// <c>PlayMode/APICalls/</c> needs a key and spends tokens.</para>
///
/// <para>Run one process across all folders:
/// <code>
/// unity cmd run_tests --mode PlayMode --filter_type category --filter P04_look_at_the_twin
/// </code>
/// The filter matches the whole name — no prefixes, no patterns.</para>
/// </remarks>
public static class Processes
{
    /// <summary>create · name · save as copy · open · list · delete · reset the app</summary>
    public const string ManageTwins = "P01_manage_twins";

    /// <summary>pick a tool · paint freehand · pick a body region · place · undo and redo</summary>
    public const string MarkUpTheBody = "P02_mark_up_the_body";

    /// <summary>create · name · select · hide and show · delete · which part sits in which group</summary>
    public const string OrganiseIntoGroups = "P03_organise_into_groups";

    /// <summary>turn · move · zoom · store a view · activate a stored view · Shape</summary>
    public const string LookAtTheTwin = "P04_look_at_the_twin";

    /// <summary>describe a part · report on a version · document to twin · screenshots · skin</summary>
    public const string DescribeAndReport = "P05_describe_and_report";

    /// <summary>export a zip · import a zip · versions · server sync</summary>
    public const string ExchangeTwins = "P06_exchange_twins";

    /// <summary>settings · language · start and quit</summary>
    public const string AppFrame = "P07_app_frame";

    /// <summary>
    /// No business-visible flow of its own: the key lookup, the schema builder for structured
    /// model answers, the language-model client. Deliberately outside the landscape rather than
    /// squeezed into a process it does not belong to.
    /// </summary>
    public const string Technical = "T00_technical";
}

/// <summary>
/// What a person does in one sitting, across several processes. A chain checks the transitions
/// and what survives them; anything a process test already checks it does not claim again.
/// </summary>
public static class Chains
{
    /// <summary>P01 · P02 · P03</summary>
    public const string NewTwinFirstParts = "K01_new_twin_first_parts";

    /// <summary>P01 · P06 · P04 · P03</summary>
    public const string OpenAndAddToATwin = "K02_open_and_add_to_a_twin";

    /// <summary>P06 · P02 · P01</summary>
    public const string ReceiveATwin = "K03_receive_a_twin";

    /// <summary>P05 · P03 · P02</summary>
    public const string DocumentBecomesATwin = "K04_document_becomes_a_twin";

    /// <summary>P05 · P03</summary>
    public const string ReportOnAVersion = "K05_report_on_a_version";
}
