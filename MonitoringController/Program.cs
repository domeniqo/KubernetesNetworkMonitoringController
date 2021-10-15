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

        private static async void PodEventHandler(WatchEventType type, V1Pod pod)
        {
            try
            {
                if (type == WatchEventType.Added || type == WatchEventType.Modified)
                {
                    if (pod.IsMonitored())
                    {
                        bool containerNeeded = true;
                        if (pod.Spec.HasContainer())
                        {
                            Console.WriteLine(pod.Name() + ": already has monitoring container");
                            if (pod.Status.Phase == "Running" && pod.Labels()["csirt.muni.cz/monitoringState"] == "init")
                            {
                                pod.Labels()["csirt.muni.cz/monitoringState"] = "monitoring";
                                try
                                {
                                    client.ReplaceNamespacedPod(pod, pod.Name(), pod.Namespace());
                                    Console.WriteLine("updating csirt.muni.cz/monitoringState label");
                                }
                                catch (Exception e)
                                {
                                    Console.WriteLine("Cannot update pod. " + e.Message);
                                }
                            }
                            containerNeeded = false;
                        }
                        if (pod.HasController())
                        {
                            Console.WriteLine(pod.Name() + ": is controlled by other resource");
                            containerNeeded = false;
                        }
                        if (pod.Metadata.DeletionTimestamp != null)
                        {
                            Console.WriteLine(pod.Name() + ": is terminating");
                            containerNeeded = false;
                        }
                        if (pod.Status.Phase != "Running")
                        {
                            Console.WriteLine(pod.Name() + ": is not in stable running state");
                            containerNeeded = false;
                        }
                        if (containerNeeded)
                        {
                            //- update is not enough, because changes of containers of existing pod is illegal, delete and create new pod instead
                            //- we do not care if this call is realy completed, therefor "fire and forget" approach is used
                            Console.WriteLine("deleting existing pod " + pod.Name());
                            client.DeleteNamespacedPodAsync(pod.Name(), pod.Namespace());
                            
                            pod.Spec.AddContainer(k8s.Yaml.LoadFromFileAsync<V1Container>("ContainerTemplates/ipfixprobe.yaml").Result);
                            Console.WriteLine("adding label to " + pod.Name());
                            pod.Labels()["csirt.muni.cz/monitoringState"] = "init";
                            V1Pod newPod = new V1Pod();
                            newPod.Metadata = new V1ObjectMeta();
                            newPod.Metadata.Annotations = pod.Annotations();
                            newPod.Metadata.Name = pod.Name() + "-monitored";
                            newPod.Metadata.Labels = pod.Labels();
                            newPod.Metadata.NamespaceProperty = pod.Namespace();
                            newPod.Spec = pod.Spec;
                            newPod.Spec.ImagePullSecrets = new List<V1LocalObjectReference> { new V1LocalObjectReference("regcred") };
                            Console.WriteLine("creating new pod");
                            try
                            {
                                var p = client.CreateNamespacedPodAsync(newPod, pod.Namespace());
                                Console.WriteLine("pod created " + (await p).Name());
                            }
                            catch (Exception e)
                            {
                                Console.WriteLine("Cannot create pod " + e.Message);
                            }

                        }
                    }
                    else
                    {
                        if (pod.Spec.HasContainer())
                        {
                            bool deleteContainer = true;
                            if (pod.Metadata.DeletionTimestamp != null)
                            {
                                Console.WriteLine(pod.Name() + ": is terminating");
                                deleteContainer = false;
                            }
                            if (pod.Status.Phase != "Running")
                            {
                                Console.WriteLine(pod.Name() + ":  is not in stable Running state");
                                deleteContainer = false;
                            }
                            if (deleteContainer)
                            {
                                //- update is not enough, because changes of containers of existing pod is illegal, delete and create new pod instead
                                //- we do not care if this call is realy completed, therefor "fire and forget" approach is used
                                Console.WriteLine("deleting existing pod " + pod.Name());
                                client.DeleteNamespacedPodAsync(pod.Name(), pod.Namespace());

                                V1Pod newPod = new V1Pod();
                                newPod.Metadata = new V1ObjectMeta();
                                newPod.Metadata.Annotations = pod.Annotations();
                                newPod.Metadata.Name = pod.Name() + "-not-monitored";
                                newPod.Metadata.Labels = pod.Labels();
                                if (newPod.Labels().ContainsKey("csirt.muni.cz/monitoringState"))
                                {
                                    newPod.Labels().Remove("csirt.muni.cz/monitoringState");
                                }
                                newPod.Metadata.NamespaceProperty = pod.Namespace();
                                newPod.Spec = pod.Spec;
                                newPod.Spec.Containers = newPod.Spec.Containers.Except(pod.Spec.Containers.Where(container => container.Name.Contains("csirt-probe"))).ToList();
                                if (newPod.Spec.ImagePullSecrets.Contains(new V1LocalObjectReference("regcred")))
                                {
                                    newPod.Spec.ImagePullSecrets.Remove(newPod.Spec.ImagePullSecrets.First(or => or.Name == "regcred"));
                                }
                                Console.WriteLine("creating new pod without probes");
                                try
                                {
                                    var p = client.CreateNamespacedPodAsync(newPod, pod.Namespace());
                                    Console.WriteLine("pod created " + (await p).Name());
                                }
                                catch (Exception e)
                                {
                                    Console.WriteLine("Cannot create pod " + e.Message);
                                }
                            }
                        }
                        else
                        {
                            Console.WriteLine(pod.Name() + ": not monitored pod");
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("something went wrong " + e.Message + "\n" + e.StackTrace);
            }

        }

        private static void Main(string[] args)
        {
            KubernetesClientConfiguration config;
            if (KubernetesClientConfiguration.IsInCluster())
            {
                config = KubernetesClientConfiguration.InClusterConfig();
            }
            else
            {   
                config = KubernetesClientConfiguration.BuildConfigFromConfigFile("admin.conf");
            }

            client = new Kubernetes(config);

            var podlistResp = client.ListNamespacedPodWithHttpMessagesAsync("default", watch: true);

            using (podlistResp.Watch<V1Pod, V1PodList>(PodEventHandler))
            {
                Console.WriteLine("press ctrl + c to stop watching");

                var ctrlc = new ManualResetEventSlim(false);
                Console.CancelKeyPress += (sender, eventArgs) => ctrlc.Set();
                ctrlc.Wait();
            }
        }


    }
}
