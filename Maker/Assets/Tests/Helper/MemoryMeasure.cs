using UnityEngine.Profiling;

/// <summary>
/// The few memory figures the tests need, and how to print them.
/// </summary>
/// <remarks>
/// <para>This lives in the test assembly on purpose. Measuring memory is something the tests do,
/// not something the app does, so none of it is shipped.</para>
///
/// <para>The three counters are added together because on iOS they all count against the same
/// per-process limit: what Unity holds, what the managed heap holds, and what the graphics driver
/// holds. The sum is a proxy, not the figure iOS actually kills on — but the <b>differences</b>
/// between two readings are reliable, and differences are what the tests assert on.</para>
///
/// <para>Deliberately not <see cref="UnityEngine.Texture.currentTextureMemory"/>: it leaves the
/// 8192-square paint targets out entirely, and was measured thirteen times low.</para>
/// </remarks>
public static class MemoryMeasure
{
    /// <summary>Everything that counts against the app's limit, in bytes.</summary>
    public static long Total()
    {
        return Profiler.GetTotalReservedMemoryLong()
             + Profiler.GetMonoHeapSizeLong()
             + Profiler.GetAllocatedMemoryForGraphicsDriver();
    }

    public static string Mb(long bytes)
    {
        return (bytes / 1048576f).ToString("0") + " MB";
    }

    public static string Signed(long bytes)
    {
        return (bytes / 1048576f).ToString("+0;-0;0") + " MB";
    }
}
