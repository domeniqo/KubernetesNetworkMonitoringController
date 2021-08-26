using System;
using System.Linq;
using System.Threading;
using k8s;
using k8s.Models;

namespace watch
{
    internal class Program
    {
        private static IKubernetes client;

        private static void addContainer(V1Pod pod)
        {
            foreach (var owner in pod.OwnerReferences())
            {
                if (owner.Controller == true)
                {
                    if (owner.Kind == "ReplicaSet")
                    {
                        var replicaSet = client.ReadNamespacedReplicaSet(owner.Name, pod.Namespace());
                        addContainer(replicaSet);
                    }
                    else if (owner.Kind == "Deployment")
                    {
                        var deployment = client.ReadNamespacedDeployment(owner.Name, pod.Namespace());
                        addContainer(deployment);
                    }
                    else
                    {
                        Console.WriteLine("This kind of controller is not supported for pod. Controller kind,name: " + owner.Kind + "," + owner.Name);
                    }
                }
            }

            if (pod.OwnerReferences().All(owner => owner.Controller != true))
            {
                // pod is standalone, delete and create new pod with desired spec
                Console.WriteLine("deleting and creating stanalone pod");
            }
        }

        private static void addContainer(V1ReplicaSet rs)
        {
            foreach (var owner in rs.OwnerReferences())
            {
                if (owner.Controller == true)
                {
                    if (owner.Kind == "Deployment")
                    {
                        var deployment = client.ReadNamespacedDeployment(owner.Name, rs.Namespace());
                        addContainer(deployment);
                    }
                    else
                    {
                        Console.WriteLine("This kind of controller is not supported for replicaSet. Controller kind,name: " + owner.Kind + "," + owner.Name);
                    }
                }
            }

            if (rs.OwnerReferences().All(owner => owner.Controller != true))
            {
                // rs is standalone - change spec template
                Console.WriteLine("deleting and creating standlalone replicaSet");
            }
        }

        private static void addContainer(V1Deployment deployment)
        {
            bool hasController = false;
            deployment.OwnerReferences().ToList().ForEach(owner =>
            {
                if (owner.Controller == true)
                {
                    Console.WriteLine("Unknown controller for deployment. Controller kind,name: " + owner.Kind + "," + owner.Name);
                    hasController = true;
                }
            });
            if (!hasController)
            {
                // change deployment spec template
                Console.WriteLine("updating deployment spec to add monitoring container");
            }
        }

        private static void EventHandler(WatchEventType type, V1Pod pod)
        {
            Console.WriteLine(pod.Name() + " - " + type + "\n");
            if (type == WatchEventType.Added)
            {
                string monitoringValue;
                if (pod.Metadata.Labels.TryGetValue("csirt.muni.cz/monitoring", out monitoringValue)
                    && monitoringValue == "enabled"
                    && !pod.Metadata.Labels.ContainsKey("csirt.muni.cz/monitoringState"))
                {
                    // add container and update labels to initialization
                    pod.Metadata.Labels.Add("csirt.muni.cz/monitoringState", "initialization");
                    try
                    {
                        client.ReplaceNamespacedPod(pod, pod.Name(), pod.Namespace());
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(e.Message);
                    }

                    Console.WriteLine("want to add container to new created pod");
                }
            }

            if (type == WatchEventType.Modified)
            {
                string monitoringValue;
                string monitoringState;
                if (pod.Metadata.Labels.TryGetValue("csirt.muni.cz/monitoring", out monitoringValue))
                {
                    if (!pod.Metadata.Labels.ContainsKey("csirt.muni.cz/monitoringState"))
                    {
                        // adding container
                        addContainer(pod);
                        pod.Metadata.Labels.Add("csirt.muni.cz/monitoringState", "initialization");
                        try
                        {
                            client.ReplaceNamespacedPod(pod, pod.Name(), pod.Namespace());
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine(e.Message);
                        }

                        Console.WriteLine("want to add container to modified pod");
                    }

                    if (monitoringValue == "enabled"
                        && pod.Metadata.Labels.TryGetValue("csirt.muni.cz/monitoringState", out monitoringState)
                        && monitoringState == "initialization")
                    {
                        // check wether monitoring container is running properly and set monitoring label
                        Console.WriteLine("checking if container is running properly");
                    }

                    if (monitoringValue == "enabled"
                        && pod.Metadata.Labels.TryGetValue("csirt.muni.cz/monitoringState", out monitoringState)
                        && monitoringState == "monitoring")
                    {
                        // check wether all requirements are fullfilled (should be without change)
                        Console.WriteLine("checking if container is running properly");
                    }

                    if (monitoringValue == "disabled")
                    {
                        pod.Labels().Remove("csirt.muni.cz/monitoringState");
                        try
                        {
                            client.ReplaceNamespacedPod(pod, pod.Name(), pod.Namespace());
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine(e.Message);
                        }

                        Console.WriteLine("user decided to turn monitoring off - removing container and label");
                        // remove monitoring container and update labels
                    }
                }
                else
                {
                    // check if eventually our container is not running and labels set (user updated pod in a way he removed
                    // csirt.muni.cz/monitoring label completely insted of using "disabled" option)
                    Console.WriteLine("csirt.muni.cz/monitoring label not set, but maybe it's user fault - checking actual state and disabling monitoring if enabled");
                }
            }
        }

        private static void Main(string[] args)
        {
            var config = KubernetesClientConfiguration.InClusterConfig();

            client = new Kubernetes(config);

            var podlistResp = client.ListNamespacedPodWithHttpMessagesAsync("default", watch: true);

            using (podlistResp.Watch<V1Pod, V1PodList>(EventHandler))
            {
                Console.WriteLine("press ctrl + c to stop watching");

                var ctrlc = new ManualResetEventSlim(false);
                Console.CancelKeyPress += (sender, eventArgs) => ctrlc.Set();
                ctrlc.Wait();
            }
        }
    }
}
