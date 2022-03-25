using k8s;

const string KubeConfigEnvVar = "KUBECONFIG";
const string DestinationHostnameEnvVar = "DESTINATION_HOSTNAME";
const string IgnoreFailureEnvVar = "IGNORE_FAILURE";

KubernetesClientConfiguration kubernetesConfig;
var kubeConfigFile = Environment.GetEnvironmentVariable(KubeConfigEnvVar);
if (kubeConfigFile != null)
{
    Console.WriteLine($"Using ${KubeConfigEnvVar} environment variable value {kubeConfigFile}");
    kubernetesConfig = KubernetesClientConfiguration.BuildConfigFromConfigFile(kubeConfigFile);
}
else
{
    Console.WriteLine($"Assuming in-cluster execution since ${KubeConfigEnvVar} environment variable is not set");
    kubernetesConfig = KubernetesClientConfiguration.InClusterConfig();
}

using var client = new Kubernetes(kubernetesConfig);
using var prober = new Prober($"https://{Environment.GetEnvironmentVariable(DestinationHostnameEnvVar)}");
using var updater = new Timer(async _ => await prober.Probe(client), null, TimeSpan.Zero, TimeSpan.FromSeconds(60));

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.MapGet("/dump", async context => {
    context.Response.ContentType = "text/plain";
    context.Response.StatusCode = 200;
    foreach (var ingPath in prober.IngressPaths)
    {
        await context.Response.WriteAsync($"{ingPath.Namespace}/{ingPath.Name} -> {ingPath}\n");
    }
});

app.MapGet("/healthz", async context => {
    context.Response.ContentType = "text/plain";
    if (prober.Healthy || Environment.GetEnvironmentVariable(IgnoreFailureEnvVar) != null) {
        context.Response.StatusCode = 200;
        await context.Response.WriteAsync("OK");
    } else {
        context.Response.StatusCode = 503;
        await context.Response.WriteAsync("One or more unhealthy probes");
    }
});

app.Run();
