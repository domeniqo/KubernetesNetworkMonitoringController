using System;
using System.Linq;
using System.Threading;
using k8s;
using k8s.Models;
using System.Collections.Generic;
using MonitoringController;

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
            if (type == WatchEventType.Added || type == WatchEventType.Modified)
            {
                if (pod.IsMonitored())
                {
                    if (!pod.Spec.HasContainer())
                    {
                        pod.Spec.AddContainer(k8s.Yaml.LoadFromFileAsync<V1Container>("ContainerTemplates/ipfixprobe.yaml").Result);
                        client.ReplaceNamespacedPod(pod, pod.Name(), pod.Namespace());
                    } 
                    else
                    {
                        Console.Write("already monitored pod: " + pod.Name());
                    }
                }
                else
                {
                    Console.WriteLine("not monitored pod: " + pod.Name());
                    //remove containers if any exists
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
