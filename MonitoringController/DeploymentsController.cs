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

        public override void EventHandler(WatchEventType eventType, V1Deployment resource)
        {
            throw new NotImplementedException();
        }

        public override void CheckAndUpdate()
        {
            throw new NotImplementedException();
        }

        public override void DeinitMonitoring()
        {
            throw new NotImplementedException();
        }

        public override void InitMonitoring()
        {
            throw new NotImplementedException();
        }
    }
}
