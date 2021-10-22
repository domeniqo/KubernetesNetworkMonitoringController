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

        protected override bool HasContainer(V1Deployment resource)
        {
            return resource.HasContainer();
        }

        public override async void CheckAndUpdate(V1Deployment resource)
        {
            if (resource.Status.Replicas != resource.Status.ReadyReplicas)
            {
                return;
            }
            if (resource.Labels()?["csirt.muni.cz/monitoringState"] == "init")
            {
                resource.Labels()["csirt.muni.cz/monitoringState"] = "monitoring";
                Console.WriteLine("(API REQUEST) " + resource.Name() + ": updating csirt.muni.cz/monitoringState label");
                try
                {
                    //awaiting even this returns void, because if operations would throw exception, we want to catch them
                    await client.ReplaceNamespacedDeploymentAsync(resource, resource.Name(), resource.Namespace())
                        .ContinueWith(task => Console.WriteLine("(API RESPONSE) " + task.GetAwaiter().GetResult().Name() + ": deployment updated"));
                }
                catch (Exception e)
                {
                    Console.WriteLine("Cannot update deployment. " + e.Message);
                }
            }
        }

        public override async void DeinitMonitoring(V1Deployment resource)
        {
            if (resource.Labels().ContainsKey("csirt.muni.cz/monitoringState"))
            {
                resource.Labels().Remove("csirt.muni.cz/monitoringState");
            }
            resource.Spec.Template.Spec.Containers = resource.Spec.Template.Spec.Containers.Except(resource.Spec.Template.Spec.Containers.Where(container => container.Name.Contains("csirt-probe"))).ToList();
            if (resource.Spec.Template.Spec.ImagePullSecrets.Contains(new V1LocalObjectReference("regcred")))
            {
                resource.Spec.Template.Spec.ImagePullSecrets.Remove(resource.Spec.Template.Spec.ImagePullSecrets.First(or => or.Name == "regcred"));
            }
            Console.WriteLine("(API REQUEST) " + resource.Name() + ": updating csirt.muni.cz/monitoringState label");
            try
            {
                //awaiting even this returns void, because if operations would throw exception, we want to catch them
                await client.ReplaceNamespacedDeploymentAsync(resource, resource.Name(), resource.Namespace())
                    .ContinueWith(task => Console.WriteLine("(API RESPONSE) " + task.GetAwaiter().GetResult().Name() + ": deployment monitoring deinitialized"));
            }
            catch (Exception e)
            {
                Console.WriteLine("Cannot finish deinitialization of deployment monitoring. " + e.Message);
            }
        }

        public override async void InitMonitoring(V1Deployment resource)
        {
            var loadContainerTask = k8s.Yaml.LoadFromFileAsync<V1Container>("ContainerTemplates/ipfixprobe.yaml");
            resource.Labels()["csirt.muni.cz/monitoringState"] = "init";

            resource.Spec.Template.Spec.ImagePullSecrets = new List<V1LocalObjectReference> { new V1LocalObjectReference("regcred") };

            resource.Spec.Template.Spec.AddContainer(await loadContainerTask);

            Console.WriteLine("(API REQUEST) " + resource.Name() + " :initialization of monitoring");
            try
            {
                //awaiting even this returns void, because if operations would throw exception, we want to catch them
                await client.ReplaceNamespacedDeploymentAsync(resource, resource.Name(), resource.Namespace())
                    .ContinueWith(task => Console.WriteLine("(API RESPONSE) " + task.GetAwaiter().GetResult().Name() + " :initialization successful"));
            }
            catch(Exception e)
            {
                Console.WriteLine("Cannot initialize deployment monitoring. " + e.Message);
            }
        }

    }
}
