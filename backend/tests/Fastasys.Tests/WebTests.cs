namespace Fastasys.Tests;

public class WebTests(TestWebApplicationFactory factory) : IClassFixture<TestWebApplicationFactory>
{
    [Fact]
    public async Task GetHealth_ReturnsSuccessStatusCode()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = factory.CreateClient();
        var response = await client.GetAsync("/health", cancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
