using k8s;
using k8s.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MonitoringController
{
    class PodsController : BaseController<V1Pod>
    {
        #region public

        public PodsController(IKubernetes client) : base(client) { }

        public override async void InitMonitoring(V1Pod pod)
        {
            var gettingContainer = pod.GetMonitoringContainerAsync();

            V1Pod newPod = GenerateApplicablePod(pod);


            Console.WriteLine(newPod.Name() + ": adding labels");
            newPod.Labels()["csirt.muni.cz/monitoringState"] = "init";
            newPod.Labels()["csirt.muni.cz/originPodName"] = pod.Name();

            var newName = pod.Name() + "-monitored";
            Console.WriteLine(newName + ": changing name. Origin name: " + pod.Name());
            newPod.Metadata.Name = newName;

            Console.WriteLine(newPod.Name() + ": adding image pull secrets");
            newPod.Spec.ImagePullSecrets = new List<V1LocalObjectReference> { new V1LocalObjectReference("regcred") };

            Console.WriteLine(newPod.Name() + ": adding monitoring container");
            newPod.Spec.AddContainer(await gettingContainer);
            try
            {
                //- update is not enough, because changes of containers of existing pod is illegal, create new and delete origin pod instead
                await CreatePodAsync(newPod);
                await DeletePodAsync(pod);
            }
            catch
            {
                Console.WriteLine(newPod.Name() + ": could not finish pod monitoring initialization");
            }

        }

        public override async void CheckAndUpdate(V1Pod pod)
        {
            if (pod.Labels()?["csirt.muni.cz/monitoringState"] == "init")
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
        }

        public override async void DeinitMonitoring(V1Pod pod)
        {
            V1Pod newPod = GenerateApplicablePod(pod);

            if (newPod.Labels().ContainsKey("csirt.muni.cz/originPodName"))
            {
                newPod.Metadata.Name = newPod.Labels()["csirt.muni.cz/originPodName"];
            }
            else
            {
                newPod.Metadata.Name = newPod.Name() + "-not-monitored";
            }
            Console.WriteLine(newPod.Name() + ": updating name of pod. Origin name: " + pod.Name());

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

            try
            {
                //- update is not enough, because changes of containers of existing pod is illegal, create new and delete origin pod instead
                await CreatePodAsync(newPod);
                await DeletePodAsync(pod);
            }
            catch
            {
                Console.WriteLine(newPod.Name() + ": could not finish pod monitoring deinitialization");
            }

        }

        #endregion

        #region protected
        protected override bool PreconditionsChecks(V1Pod resource)
        {
            //we do not care about pods which are not running
            return base.PreconditionsChecks(resource) && resource.Status.Phase == "Running" && !resource.HasController();
        }

        protected override bool HasContainer(V1Pod resource)
        {
            return resource.Spec.HasContainer();
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

        private async Task DeletePodAsync(V1Pod pod)
        {
            try
            {
                Console.WriteLine("(API REQUEST) " + pod.Name() + ": deleting existing pod");
                //awaiting even this returns void, because if operations would throw exception, we want to catch them
                await client.DeleteNamespacedPodAsync(pod.Name(), pod.Namespace(), gracePeriodSeconds: 0)
                    .ContinueWith(task => Console.WriteLine("(API RESPONSE) " + task.GetAwaiter().GetResult().Name() + ": pod deleted"));
            }
            catch (Exception e)
            {
                Console.WriteLine("Cannot delete pod " + e.Message);
            }
        }

        private async Task CreatePodAsync(V1Pod pod)
        {
            try
            {
                Console.WriteLine("(API REQUEST) " + pod.Name() + ": creating new pod");
                //awaiting even this returns void, because if operations would throw exception, we want to catch them
                await client.CreateNamespacedPodAsync(pod, pod.Namespace())
                    .ContinueWith(task => Console.WriteLine("(API RESPONSE) " + task.GetAwaiter().GetResult().Name() + ": pod created"));
            }
            catch (Exception e)
            {
                Console.WriteLine("Cannot create pod " + e.Message);
                throw e;
            }
        }
        #endregion
    }
}
