using k8s;

public sealed class IngressPath {
    public string Namespace { get; init; }
    public string Name { get; init; }
    public string Host { get; init; } = string.Empty;
    public string Path { get; init; } = string.Empty;
    public string BaseUri { get => $"https://{this.Host}{this.Path}"; }
    internal Service Service { get; init; }

    IngressPath(string ns, string name, Service service)
    {
        this.Namespace = ns;
        this.Name = name;
        this.Service = service;
    }

    public static IEnumerable<IngressPath> AllFromNamespace(IKubernetes client, string ns)
    {
        var knownServices = new Dictionary<string, Service>();

        foreach (var ingObj in client.ListNamespacedIngress(ns).Items)
        {
            foreach (var rule in ingObj.Spec.Rules)
            {
                var host = rule.Host ?? string.Empty;
                if (host.StartsWith('*')) continue; // wildcard hosts may or may not be backed by wildcard DNS, ignore them for now
                foreach (var rulePath in rule.Http.Paths)
                {
                    var path = rulePath.Path;
                    // if it smells like a regex we don't want it
                    if (path.IndexOfAny(new char[] {'*', '?', '+', '[', ']'}) != -1)
                    {
                        Console.WriteLine($"Ignoring ingress {ns}/{ingObj.Metadata.Name} path {path} as it looks like a regex");
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

                    var ingressPath = new IngressPath(ns, ingObj.Metadata.Name, service) { Host = host, Path = path };
                    Console.WriteLine($"Generated ingress {ingressPath}");
                    yield return ingressPath;
                }
            }
        }
    }

    public override string ToString() => $"{this.BaseUri} -> {this.Service}";
}
