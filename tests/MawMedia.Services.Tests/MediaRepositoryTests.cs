namespace MawMedia.Services.Tests;

public class MediaRepositoryTests
{
    readonly TestFixture _fixture;

    public MediaRepositoryTests(TestFixture fixture)
    {
        ArgumentNullException.ThrowIfNull(fixture);

        _fixture = fixture;
    }

    [Fact]
    public void Test1()
    {
        Assert.True(true);
    }
}
