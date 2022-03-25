using k8s;

sealed class ClusterView
{
    private readonly List<IngressPath> ingressPaths = new();
    public IEnumerable<IngressPath> IngressPaths { get { return this.ingressPaths; }}

    public async Task<bool> Update(IKubernetes kubernetesClient)
    {
        this.ingressPaths.Clear();
        foreach (var ns in (await kubernetesClient.ListNamespaceAsync()).Items)
        {
            this.ingressPaths.AddRange(IngressPath.AllFromNamespace(kubernetesClient, ns.Metadata.Name));
        }

        return true;
    }
}