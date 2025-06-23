namespace MawMedia.Services.Tests;

public class TestFixture
    :  IAsyncLifetime
{
    public ValueTask InitializeAsync()
    {
        Console.WriteLine("startup");

        return ValueTask.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        Console.WriteLine("teardown");

        return ValueTask.CompletedTask;
    }
}
