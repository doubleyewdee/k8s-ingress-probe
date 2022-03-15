using System.Net;

static class Prober
{
    private static readonly HttpClient client = new HttpClient();

    public static async Task<HttpStatusCode?> Inspect(string uri)
    {
        try
        {
            var response = await client.GetAsync(uri);
            return response.StatusCode;
        }
        catch (HttpRequestException ex)
        {
            Console.Error.WriteLine($"{uri} failed: {ex.Message}");
            return null;
        }
    }
}