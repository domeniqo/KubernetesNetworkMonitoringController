using k8s;
using k8s.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MonitoringController
{
    public abstract class BaseController<T> : IController<T> where T : IKubernetesObject<V1ObjectMeta> 
    {
        protected IKubernetes client;

        protected BaseController(IKubernetes client)
        {
            this.client = client;
        }

        public abstract void InitMonitoring();

        public abstract void CheckAndUpdate();

        public abstract void DeinitMonitoring();

        public abstract void EventHandler(WatchEventType eventType, T resource);
    }
}
