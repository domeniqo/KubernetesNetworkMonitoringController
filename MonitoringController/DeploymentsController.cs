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

        public override void CheckAndUpdate(V1Deployment resource)
        {
            throw new NotImplementedException();
        }

        public override void DeinitMonitoring(V1Deployment resource)
        {
            throw new NotImplementedException();
        }

        public override void InitMonitoring(V1Deployment resource)
        {
            throw new NotImplementedException();
        }

    }
}
