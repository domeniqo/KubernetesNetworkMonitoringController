using k8s.Models;
using k8s;
using System.Collections.Generic;
using System.Linq;

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

        public static bool HasController(this V1Pod pod)
        {
            return pod.OwnerReferences()?.Any(owner => owner.Controller == true) == true;
        }

        public static bool HasContainer(this V1PodSpec spec)
        {
            return spec.Containers?.Any(cont => cont.Name.Contains("csirt-probe")) == true;
        }

        public static void AddContainer(this V1PodSpec spec, V1Container container)
        {
            spec.Containers.Add(container);
        }
    }
}