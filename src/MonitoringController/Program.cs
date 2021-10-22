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
            IController<V1Pod> podsController = new PodsController(client);
            IController<V1Deployment> deploymentsController = new DeploymentsController(client);

            var podlistResp = client.ListNamespacedPodWithHttpMessagesAsync("default", watch: true);
            var deploymetlistResp = client.ListNamespacedDeploymentWithHttpMessagesAsync("default", watch: true);

            using (podlistResp.Watch<V1Pod, V1PodList>(podsController.EventHandler))
            {
                using (deploymetlistResp.Watch<V1Deployment, V1DeploymentList>(deploymentsController.EventHandler))
                {

                    Console.WriteLine("press ctrl + c to stop watching");

                    var ctrlc = new ManualResetEventSlim(false);
                    Console.CancelKeyPress += (sender, eventArgs) => ctrlc.Set();
                    ctrlc.Wait();
                }
            }
        }
    }
}
