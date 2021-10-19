using System;
using System.Linq;
using System.Threading;
using k8s;
using k8s.Models;
using System.Collections.Generic;

namespace MonitoringController
{
    internal class Program
    {
        private static IKubernetes client;

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
            PodsController podsController = new PodsController(client);

            var podlistResp = client.ListNamespacedPodWithHttpMessagesAsync("default", watch: true);

            using (podlistResp.Watch<V1Pod, V1PodList>(podsController.EventHandler))
            {
                Console.WriteLine("press ctrl + c to stop watching");

                var ctrlc = new ManualResetEventSlim(false);
                Console.CancelKeyPress += (sender, eventArgs) => ctrlc.Set();
                ctrlc.Wait();
            }
        }


    }
}
