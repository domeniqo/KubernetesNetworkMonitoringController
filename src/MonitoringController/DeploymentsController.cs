using k8s;
using k8s.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MonitoringController
{
    public class DeploymentsController : BaseController<V1Deployment>
    {
        public DeploymentsController(IKubernetes client) : base(client) { }

        protected override bool HasContainer(V1Deployment deployment)
        {
            return deployment.HasContainer();
        }

        public override async void CheckAndUpdate(V1Deployment deployment)
        {
            if (deployment.Status.Replicas != deployment.Status.ReadyReplicas)
            {
                return;
            }
            if (deployment.Labels()?["csirt.muni.cz/monitoringState"] == "init")
            {
                deployment.Labels()["csirt.muni.cz/monitoringState"] = "monitoring";
                Console.WriteLine("(API REQUEST) " + deployment.Name() + ": updating csirt.muni.cz/monitoringState label");
                try
                {
                    //awaiting even this returns void, because if operations would throw exception, we want to catch them
                    await client.ReplaceNamespacedDeploymentAsync(deployment, deployment.Name(), deployment.Namespace())
                        .ContinueWith(task => Console.WriteLine("(API RESPONSE) " + task.GetAwaiter().GetResult().Name() + ": deployment updated"));
                }
                catch (Exception e)
                {
                    Console.WriteLine("Cannot update deployment. " + e.Message);
                }
            }
        }

        public override async void DeinitMonitoring(V1Deployment deployment)
        {
            if (deployment.Labels().ContainsKey("csirt.muni.cz/monitoringState"))
            {
                deployment.Labels().Remove("csirt.muni.cz/monitoringState");
            }
            deployment.Spec.Template.Spec.Containers = deployment.Spec.Template.Spec.Containers.Except(deployment.Spec.Template.Spec.Containers.Where(container => container.Name.Contains("csirt-probe"))).ToList();
            if (deployment.Spec.Template.Spec.ImagePullSecrets.Contains(new V1LocalObjectReference("regcred")))
            {
                deployment.Spec.Template.Spec.ImagePullSecrets.Remove(deployment.Spec.Template.Spec.ImagePullSecrets.First(or => or.Name == "regcred"));
            }
            Console.WriteLine("(API REQUEST) " + deployment.Name() + ": updating csirt.muni.cz/monitoringState label");
            try
            {
                //awaiting even this returns void, because if operations would throw exception, we want to catch them
                await client.ReplaceNamespacedDeploymentAsync(deployment, deployment.Name(), deployment.Namespace())
                    .ContinueWith(task => Console.WriteLine("(API RESPONSE) " + task.GetAwaiter().GetResult().Name() + ": deployment monitoring deinitialized"));
            }
            catch (Exception e)
            {
                Console.WriteLine("Cannot finish deinitialization of deployment monitoring. " + e.Message);
            }
        }

        public override async void InitMonitoring(V1Deployment deployment)
        {
            var loadContainerTask = deployment.GetMonitoringContainerTemplateAsync();
            var loadPodTask = deployment.GetMonitoringPodTemplateAsync();

            deployment.Labels()["csirt.muni.cz/monitoringState"] = "init";

            deployment.Spec.Template.Spec.ImagePullSecrets = new List<V1LocalObjectReference> { new V1LocalObjectReference("regcred") };
            if ((await loadPodTask) != null)
            {
                deployment.Spec.Template.Spec.MergeWith(loadPodTask.Result.Spec);
            }
            else 
            { 
                deployment.Spec.Template.Spec.AddContainer(await loadContainerTask);
            }

            Console.WriteLine("(API REQUEST) " + deployment.Name() + " :initialization of monitoring");
            try
            {
                //awaiting even this returns void, because if operations would throw exception, we want to catch them
                await client.ReplaceNamespacedDeploymentAsync(deployment, deployment.Name(), deployment.Namespace())
                    .ContinueWith(task => Console.WriteLine("(API RESPONSE) " + task.GetAwaiter().GetResult().Name() + " :initialization successful"));
            }
            catch(Exception e)
            {
                Console.WriteLine("Cannot initialize deployment monitoring. " + e.Message);
            }
        }

    }
}
