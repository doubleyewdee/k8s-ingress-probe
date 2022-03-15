using k8s;

sealed class IngressPath {
    public string Name { get; init; } = string.Empty;
    public string Host { get; init; } = string.Empty;
    public string Path { get; init; } = string.Empty;
    public string BaseUri { get => $"https://{this.Host}{this.Path}"; }
    public string Namespace { get; init; } = string.Empty;
    public Service Service { get; init; }

    public static IEnumerable<IngressPath> AllFromNamespace(Kubernetes client, string ns, string defaultHost)
    {
        var knownServices = new Dictionary<string, Service>();

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

                    if (!knownServices.TryGetValue(svc, out var service)) {
                        service = Service.FromCluster(client, svc, ns);
                        if (service == null) {
                            Console.Error.WriteLine($"Could not find candidate service {svc} for ingress {ingObj.Metadata.Name}");
                            continue;
                        }
                        knownServices[svc] = service;
                    }

                    yield return new IngressPath { Name = ingObj.Metadata.Name, Host = host, Path = path, Service = service, Namespace = ns };
                }
            }
        }
    }

    public override string ToString() => $"{this.BaseUri} -> {this.Service}";
}
