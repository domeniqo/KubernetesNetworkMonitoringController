using k8s;
using k8s.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MonitoringController
{
    public interface IController<T> where T : IKubernetesObject<V1ObjectMeta>
    {
        /***
         * Ensures that T k8s object will have monitoring container added to the pod(s).
         * */
        void InitMonitoring();

        /***
         * Checks whether k8s object is in good condition and performs action to update it accordingly to the actual state
         * (set labels, update monitoring container parameters etc.).
         */
        void CheckAndUpdate();

        /***
         * Ensures that all labels, containers and every aspect which this controller is working with during lifetime 
         * of the k8s object is removed from its specification and updates it accordingly.
         */
        void DeinitMonitoring();

        /***
         * Main event handler receiving events from k8s cluster API.
         */
        void EventHandler(WatchEventType eventType, T resource);
    }
}
