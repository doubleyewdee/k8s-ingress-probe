using System.Security.Permissions;
using k8s;

const string KubeConfigEnvVar = "KUBECONFIG";

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

var defaultHost = args.Length > 0 ? args[0] : "defaultHost";


foreach (var nsObj in client.ListNamespace().Items)
{
    var ns = nsObj.Metadata.Name;
    foreach (var ingPath in IngressPath.AllFromNamespace(client, ns, defaultHost))
    {
        var svc = ingPath.Service;

        string testUri;
        ProbeExpectation expectedResult;
        // If the service's probe URL starts with the ingress path we should expect that we're going to be able to
        // externally query its probe, and that should work.
        // If we don't have an obvious path to the probe, we don't know what to expect (except not a 5xx error)
        if (svc.ProbePath.StartsWith(ingPath.Path))
        {
            expectedResult = ProbeExpectation.Success;
            testUri = $"https://{ingPath.Host}{svc.ProbePath}";
        }
        else
        {
            expectedResult = ProbeExpectation.NotServerError;
            testUri = ingPath.BaseUri;
        }
        Console.Write($"Ingress {ingPath.Namespace}/{ingPath.Name} probe -> {testUri} ... ");
        var status = await Prober.Inspect(testUri);
        switch (status)
        {
            case var code when code != null && (int)code >= 200 && (int)code < (expectedResult == ProbeExpectation.Success ? 300 : 500):
                Console.WriteLine("success");
                break;
            default:
                Console.WriteLine();
                Console.Error.WriteLine($"{testUri} failed");
                break;
        }
    }
}

enum ProbeExpectation
{
    Success,
    NotServerError,
    Unknown,
}
