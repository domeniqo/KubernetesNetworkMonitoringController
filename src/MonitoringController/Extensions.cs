using k8s.Models;
using k8s;
using System.Collections.Generic;
using System.Linq;
using System;
using System.Threading.Tasks;

namespace MonitoringController
{
    public static class ModelExtensions
    {
        public static bool IsMonitored(this IMetadata<V1ObjectMeta> obj)
        {
            string value = string.Empty;
            obj.Labels()?.TryGetValue("csirt.muni.cz/monitoring", out value);
            return (value == "enabled");
        }

        public static async Task<V1Container> GetMonitoringContainerAsync(this IMetadata<V1ObjectMeta> obj)
        {
            string containerTemplateName = string.Empty;
            if (obj.Annotations()?.TryGetValue("csirt.muni.cz/containerTemplate", out containerTemplateName) == true)
            {
                string path = "ContainerTemplates/" + containerTemplateName + ".yaml";
                try
                {
                    return await k8s.Yaml.LoadFromFileAsync<V1Container>(path);
                }
                catch(Exception e)
                {
                    Console.WriteLine("Could not load container template. Does it exist? Path: " + path);
                    Console.WriteLine("Exception message: " + e.Message);
                    return null;
                }
            }
            else
            {
                Console.WriteLine("Could not find annotation 'csirt.muni.cz/containerTemplate' in given resource");
                return null;
            }
        }

        public static bool HasController(this V1Pod pod)
        {
            return pod.OwnerReferences()?.Any(owner => owner.Controller == true) == true;
        }

        public static bool HasContainer(this V1PodSpec podSpec)
        {
            return podSpec.Containers?.Any(cont => cont.Name.Contains("csirt-probe")) == true;
        }

        public static bool HasContainer(this V1Deployment deployment)
        {
            return deployment.Spec.Template.Spec.HasContainer();
        }

        public static void AddContainer(this V1PodSpec spec, V1Container container)
        {
            spec.Containers.Add(container);
        }
    }
}