using k8s;
using k8s.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MonitoringController
{
    class PodsController
    {
        private IKubernetes client;

        public PodsController(IKubernetes client)
        {
            this.client = client;
        }

        public async void KubeEventHandler(WatchEventType type, V1Pod pod)
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
                            client.DeleteNamespacedPodAsync(pod.Name(), pod.Namespace(), gracePeriodSeconds: 0);

                            pod.Spec.AddContainer(k8s.Yaml.LoadFromFileAsync<V1Container>("ContainerTemplates/ipfixprobe.yaml").Result);
                            Console.WriteLine("adding label to " + pod.Name());
                            pod.Labels()["csirt.muni.cz/monitoringState"] = "init";
                            V1Pod newPod = new V1Pod();
                            newPod.Metadata = new V1ObjectMeta();
                            newPod.Metadata.Annotations = pod.Annotations();
                            newPod.Metadata.Name = pod.Name() + "-monitored";
                            newPod.Metadata.Labels = pod.Labels();
                            newPod.Labels()["csirt.muni.cz/originPodName"] = pod.Name();
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
                                var podDeletion = client.DeleteNamespacedPodAsync(pod.Name(), pod.Namespace(), gracePeriodSeconds: 0);

                                V1Pod newPod = new V1Pod();
                                newPod.Metadata = new V1ObjectMeta();
                                newPod.Metadata.Annotations = pod.Annotations();
                                if (pod.Labels().ContainsKey("csirt.muni.cz/originPodName"))
                                {
                                    newPod.Metadata.Name = pod.Labels()["csirt.muni.cz/originPodName"];
                                }
                                else
                                {
                                    newPod.Metadata.Name = pod.Name() + "-not-monitored";
                                }
                                newPod.Metadata.Labels = pod.Labels();
                                if (newPod.Labels().ContainsKey("csirt.muni.cz/monitoringState"))
                                {
                                    newPod.Labels().Remove("csirt.muni.cz/monitoringState");
                                }
                                if (newPod.Labels().ContainsKey("csirt.muni.cz/originPodName"))
                                {
                                    newPod.Labels().Remove("csirt.muni.cz/originPodName");
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
                                    await podDeletion;
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
    }
}
