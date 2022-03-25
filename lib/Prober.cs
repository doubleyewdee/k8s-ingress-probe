using System.Net;
using k8s;

public sealed class Prober : IDisposable
{
    private readonly HttpClientHandler nonValidatingHandler = new() { ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator };
    private readonly HttpClient httpClient;
    private readonly ClusterView clusterView = new();

    public bool Healthy { get; private set; }
    public string BaseUri { get; init; }
    public string DefaultHost { get; init; } = "defaultHost";
    public int IngressMissingResultCode { get; init; } = 530;
    public IEnumerable<IngressPath> IngressPaths { get { return this.clusterView.IngressPaths; } }

    public Prober(string baseUri)
    {
        this.BaseUri = baseUri;
        this.Healthy = false;

        this.httpClient = new(this.nonValidatingHandler);
    }

    public async Task<bool> Probe(Kubernetes kubernetesClient)
    {
        var allSuccessful = true;
        await this.clusterView.Update(kubernetesClient);

        foreach (var ingPath in this.clusterView.IngressPaths)
        {
            var svc = ingPath.Service;

            string testUri = $"{this.BaseUri}{ingPath.Path}";

            Console.Write($"Ingress {ingPath.Namespace}/{ingPath.Name} probe -> {testUri} ... ");
            var status = await this.Inspect(testUri, string.IsNullOrEmpty(ingPath.Host) ? this.DefaultHost : ingPath.Host);

            // we're taking a big logic reduction here (which kind of makes the probe lookups not immediately helpful)
            // in the interests of safety. later on we should really want to look more closely at result codes to prove
            // they were handled in the way we'd expect...
            if (status != null && (int)status != this.IngressMissingResultCode)
            {
                Console.WriteLine($"success ({(int)status})");
            }
            else
            {
                Console.WriteLine("failure");
                Console.Error.WriteLine($"{testUri} failed");
                allSuccessful = false;
                // we don't break here because we want to test every probe on each run
            }
        }
        
        this.Healthy = allSuccessful;
        return this.Healthy;
    }

    public void Dispose()
    {
        this.httpClient.Dispose();
    }

    private async Task<HttpStatusCode?> Inspect(string uri, string hostHeaderValue)
    {
        try
        {
            var request = new HttpRequestMessage { RequestUri = new Uri(uri), Method = HttpMethod.Get, };
            request.Headers.Host = hostHeaderValue;
            var response = await this.httpClient.SendAsync(request);
            return response.StatusCode;
        }
        catch (HttpRequestException ex)
        {
            Console.Error.WriteLine($"{uri} for host {hostHeaderValue} failed: {ex.Message}");
            return null;
        }
    }
}