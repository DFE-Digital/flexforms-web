namespace GovUK.Dfe.FlexForms.PlaywrightTests.Infrastructure;

/// <summary>
/// Lets only one application be created at a time across the whole test run.
/// </summary>
/// <remarks>
/// The API builds each reference as "{prefix}-{yyyyMMdd}-{n}", where n is one more than the newest
/// application created today, and stores it under a unique index. Two creates that overlap read the
/// same "newest" row, build the same reference, and the second insert fails. Fixtures run in parallel
/// (see AssemblySetup), so every create - through the API helpers or the "Start new application"
/// button - goes through this gate. Hold it only for the create itself, never for a whole test.
/// </remarks>
public static class ApplicationCreationGate
{
    private static readonly SemaphoreSlim Gate = new(1, 1);

    public static async Task<T> RunAsync<T>(Func<Task<T>> create)
    {
        await Gate.WaitAsync();
        try
        {
            return await create();
        }
        finally
        {
            Gate.Release();
        }
    }

    public static Task RunAsync(Func<Task> create) =>
        RunAsync(async () =>
        {
            await create();
            return true;
        });
}
