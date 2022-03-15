using k8s;
using k8s.Models;

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
        var testUri = ingPath.BaseUri + ingPath.Service.ProbePath;
        Console.WriteLine($"Ingress {ingPath.Namespace}/{ingPath.Name} probe -> {testUri}");
    }
}
