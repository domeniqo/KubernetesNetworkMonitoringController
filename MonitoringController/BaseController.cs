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

        public virtual void EventHandler(WatchEventType eventType, T resource)
        {
            try
            {
                //we do not care about other WatchEventType
                if (eventType == WatchEventType.Added || eventType == WatchEventType.Modified)
                {
                    //not valid preconditions
                    if (!PreconditionsChecks(resource))
                    {
                        Console.WriteLine(resource.Name() + " :initial conditions are not met");
                        return;
                    }

                    //check if label csirt.muni.cz/monitoring is set to 'enabled'
                    if (resource.IsMonitored())
                    {
                        if (HasContainer(resource))
                        {
                            Console.WriteLine(resource.Name() + ": already has monitoring container");
                            CheckAndUpdate(resource);
                        }
                        else
                        {
                            InitMonitoring(resource);
                        }

                    }
                    else
                    {
                        //check if pod has monitoring container, but should not have according to label csirt.muni.cz/monitoring
                        if (HasContainer(resource))
                        {
                            DeinitMonitoring(resource);
                        }
                        else
                        {
                            Console.WriteLine(resource.Name() + ": is not monitored");
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(resource.Name() + "Error during k8s event handling:\n" + e.Message + "\n" + e.StackTrace);
            }
        }

        /***
         * Returns false => should not continue further processing
           additional checks specific to particular resource should be added to overriden method in child classes
         */
        protected virtual bool PreconditionsChecks(T resource)
        {
            // Resource is not terminating, this is quiet strong common precondition. we do not care about resource when it's already terminating.
            return resource.Metadata.DeletionTimestamp == null;
        }

        protected abstract bool HasContainer(T resource);

        public abstract void InitMonitoring(T resource);

        public abstract void CheckAndUpdate(T resource);

        public abstract void DeinitMonitoring(T resource);
    }
}
