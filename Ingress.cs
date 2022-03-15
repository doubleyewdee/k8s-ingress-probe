using k8s;

sealed class IngressPath {
    public string Host { get; private set; } = string.Empty;
    public string Path { get; private set; } = string.Empty;
    public string Namespace { get; private set; } = string.Empty;
    public string ServiceName { get; private set; } = string.Empty;

    public static IEnumerable<IngressPath> AllFromNamespace(Kubernetes client, string ns, string defaultHost)
    {
        foreach (var ingObj in client.ListNamespacedIngress(ns).Items)
        {
            foreach (var rule in ingObj.Spec.Rules)
            {
                var host = rule.Host ?? defaultHost;
                if (host.StartsWith('*')) host = "blorp" + host[1..]; // wildcard into ... something
                foreach (var rulePath in rule.Http.Paths)
                {
                    var path = rulePath.Path;
                    // if it smells like a regex we don't want it
                    if (path.IndexOfAny(new char[] {'*', '?', '+', '[', ']'}) != -1) {
                        continue;
                    }
                    var svc = rulePath.Backend.Service.Name;

                    yield return new IngressPath { Host = host, Path = path, ServiceName = svc, Namespace = ns };
                }
            }
        }
    }

    public override string ToString() => $"https://{this.Host}{this.Path} -> {this.Namespace}/{this.ServiceName}";
}
