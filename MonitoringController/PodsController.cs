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

        #region public
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
                                Console.WriteLine("(API REQUEST) " + pod.Name() + ": updating csirt.muni.cz/monitoringState label");
                                try
                                {
                                    //awaiting even this returns void, because if operations would throw exception, we want to catch them
                                    await client.ReplaceNamespacedPodAsync(pod, pod.Name(), pod.Namespace())
                                        .ContinueWith(task => Console.WriteLine("(API RESPONSE) " + task.GetAwaiter().GetResult().Name() + ": pod updated"));
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
                            Console.WriteLine("(API REQUEST) " + pod.Name() + ": deleting existing pod");
                            var deletionTask = client.DeleteNamespacedPodAsync(pod.Name(), pod.Namespace(), gracePeriodSeconds: 0)
                                .ContinueWith(task => Console.WriteLine("(API RESPONSE) " + task.GetAwaiter().GetResult().Name() + ": pod deleted"));

                            V1Pod newPod = GenerateApplicablePod(pod);

                            Console.WriteLine(newPod.Name() + ": adding monitoring container");
                            newPod.Spec.AddContainer(k8s.Yaml.LoadFromFileAsync<V1Container>("ContainerTemplates/ipfixprobe.yaml").Result);
                            
                            Console.WriteLine(newPod.Name() + ": adding labels");
                            newPod.Labels()["csirt.muni.cz/monitoringState"] = "init";
                            newPod.Labels()["csirt.muni.cz/originPodName"] = pod.Name();

                            var newName = pod.Name() + "-monitored";
                            Console.WriteLine(newName + ": changing name. Old name: " +  pod.Name());
                            newPod.Metadata.Name = newName;

                            Console.WriteLine(newPod.Name() + ": adding image pull secrets");
                            newPod.Spec.ImagePullSecrets = new List<V1LocalObjectReference> { new V1LocalObjectReference("regcred") };
                            
                            Console.WriteLine("(API REQUEST) " + newPod.Name() + ": creating new pod");
                            try
                            {
                                //awaiting even this returns void, because if operations would throw exception, we want to catch them
                                await deletionTask;
                                await client.CreateNamespacedPodAsync(newPod, pod.Namespace())
                                    .ContinueWith(task => Console.WriteLine("(API RESPONSE) " + task.GetAwaiter().GetResult().Name() + ": pod created"));
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
                                Console.WriteLine("(API REQUEST)deleting existing pod " + pod.Name());
                                var deletionTask = client.DeleteNamespacedPodAsync(pod.Name(), pod.Namespace(), gracePeriodSeconds: 0)
                                    .ContinueWith(task => Console.WriteLine("(API RESPONSE) " + task.GetAwaiter().GetResult().Name() + ": pod deleted"));

                                V1Pod newPod = GenerateApplicablePod(pod);

                                if (newPod.Labels().ContainsKey("csirt.muni.cz/originPodName"))
                                {
                                    newPod.Metadata.Name = newPod.Labels()["csirt.muni.cz/originPodName"];
                                }
                                else
                                {
                                    newPod.Metadata.Name = newPod.Name() + "-not-monitored";
                                }
                                Console.WriteLine(newPod.Name() + ": updating name of pod. Old name: " + pod.Name());

                                Console.WriteLine(newPod.Name() + ": updating labels of pod");
                                if (newPod.Labels().ContainsKey("csirt.muni.cz/monitoringState"))
                                {
                                    newPod.Labels().Remove("csirt.muni.cz/monitoringState");
                                }
                                if (newPod.Labels().ContainsKey("csirt.muni.cz/originPodName"))
                                {
                                    newPod.Labels().Remove("csirt.muni.cz/originPodName");
                                }

                                Console.WriteLine(newPod.Name() + ": removing all monitoring containers ('csirt-probe' in container name)");
                                newPod.Spec.Containers = newPod.Spec.Containers.Except(pod.Spec.Containers.Where(container => container.Name.Contains("csirt-probe"))).ToList();
                                if (newPod.Spec.ImagePullSecrets.Contains(new V1LocalObjectReference("regcred")))
                                {
                                    newPod.Spec.ImagePullSecrets.Remove(newPod.Spec.ImagePullSecrets.First(or => or.Name == "regcred"));
                                }
                                Console.WriteLine("(API REQUEST) " + newPod.Name() + ": creating new pod without probes");
                                try
                                {
                                    //awaiting even this returns void, because if operations would throw exception, we want to catch them
                                    await deletionTask;
                                    await client.CreateNamespacedPodAsync(newPod, pod.Namespace()) 
                                        .ContinueWith(task => Console.WriteLine("(API RESPONSE) " + task.GetAwaiter().GetResult().Name() + ": pod created"));
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
                Console.WriteLine(pod.Name() + "Error during k8s event handling (PodsController):\n" + e.Message + "\n" + e.StackTrace);
            }
        }
        #endregion

        #region private
        /***
         * This method should generate POD, which is applicable and can be used as configuration for creating new POD request to k8s API.
         * The main purpose is that you cannot send request to create new POD containing some fields you optained when getting resource from API (i.e. Pod.Metadata.creationTimestamp).
         * */
        private V1Pod GenerateApplicablePod(V1Pod original)
        {
            V1Pod newPod = new V1Pod();
            newPod.Metadata = new V1ObjectMeta();
            newPod.Metadata.Name = original.Name();
            newPod.Metadata.Annotations = original.Annotations();
            newPod.Metadata.Labels = original.Labels();
            newPod.Metadata.NamespaceProperty = original.Namespace();
            newPod.Spec = original.Spec;
            return newPod;
        }
        #endregion
    }
}
