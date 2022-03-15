using k8s;
using Microsoft.IdentityModel.Tokens;

sealed class Service
{
    public string Name { get; init; } = string.Empty;
    public string Namespace { get; init; } = string.Empty;
    public string ProbePath { get; init; } = string.Empty;

    public override string ToString() => $"{this.Namespace}/{this.Name}";

    public static Service? FromCluster(Kubernetes client, string serviceName, string ns)
    {
        var svc = client.ReadNamespacedService(serviceName, ns);
        if (svc.Spec.Selector == null)
        {
            Console.Error.WriteLine($"Service {ns}/{serviceName} has no selector, ignoring");
            return null;
        }

        foreach (var deployment in client.ListNamespacedDeployment(ns).Items)
        {
            var deploymentLabels = deployment.Spec.Template.Metadata.Labels;
            if (deploymentLabels == null) continue;
            var match = true;
            foreach (var (selectorLabel, selectorValue) in svc.Spec.Selector)
            {
                if (deploymentLabels.TryGetValue(selectorLabel, out var deploymentValue) && selectorValue != deploymentValue)
                {
                    match = false;
                    break;
                }
            }
            if (!match) continue;

            // we pick the first container with either a liveness or readiness probe which might be kinda bogus
            // but I'm lazy
            foreach (var container in deployment.Spec.Template.Spec.Containers)
            {
                var probe = container.LivenessProbe ?? container.ReadinessProbe;
                if (probe == null || probe.HttpGet == null || string.IsNullOrEmpty(probe.HttpGet.Path)) continue;

                return new Service { Name = serviceName, Namespace = ns, ProbePath = probe.HttpGet.Path };
            }
        }

        return null;
    }
}