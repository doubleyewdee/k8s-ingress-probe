# k8s-ingress-probe
A toy app that finds ingresses in Kubernetes clusters and tries to probe their liveness endpoints externally

This is just demo code and mostly not useful, but also not very proprietary and could maybe be useful for someone else to toy with.

## Background

My work team (Azure ML) runs k8s clusters with a whole buttload of ingresses. When our ingress controller can't match a path
to a backing service, we route to a default backend which spits out a specific 5xx error (an unambiguous error code we've chosen that indicates the server has no idea what to do with the provided URI). We can thus use a combination of ingress objects + services + deployments to come up with a rudimentary view of the world where we expect certain URIs to produce specific results.

For cases where the URI starts with the same prefix as the liveness probe (e.g. https://eastus2.api.azureml.ms/fooservice/keepalive -> a service with a liveness probe of /fooservice/keepalive) we can expect a 2xx back. For cases where these don't match (meaning the probe isn't accessible via the ingress) we can expect the service to get the traffic and *not* 5xx it (could be anything else, though!). So if we get a 5xx back we can conclude that somehow our ingress controller isn't routing queries the way we expect.

## Reasoning

We've recently observed cases where nginx-ingress-controller boots and has an incomplete/incorrect view of the world. This sample code provides the scaffolding to build a watchdog that could monitor our ingress controller pods and determine if they appear to be unhealthy.
