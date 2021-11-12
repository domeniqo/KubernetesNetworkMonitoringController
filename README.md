# KubernetesNetworkMonitoringController

## Controller definition (from K8s documentation)

A controller tracks at least one Kubernetes resource type. These objects have a spec field that represents the desired state. The controller for that resource are responsible for making the current state come closer to that desired state. 

The controller might carry the action out itself; more commonly, in Kubernetes, a controller will send messages to the API server that have useful side effects."

## Project purpose and goals

KubernetesNetworkMonitoringController is basically K8s API client which tracks multiple resources written in C#. This project is part of the open-source project which should come with complex solution for monitoring network traffic of applications running in Kubernetes cluster covered by research of [CSIRT MUNI](https://csirt.muni.cz/).

Along with this project, network probe and collector are developed. Probe gets relevant information from K8s API and sends it to collector together with captured network for further processing. YAML files used in this repository will be mainly using container images with network probe described below.

### Used network probe and collector

- Original probe [CESNET/ipfixprobe](https://github.com/CESNET/ipfixprobe)
- Original collector [CESNET/ipfixcol2](https://github.com/CESNET/ipfixcol2)
- Original probe + kubernetes plugin + configuration of collector [domeniqo/ipfixprobe](https://github.com/domeniqo/ipfixprobe)

# Actual status

&#x1F7E9; Create base controller for K8s object with metadata

&#x1F7E9; Create specific controller logic for K8s object POD

&#x1F7E9; Create specific controller logic for K8s object DEPLOYMENT

&#x1F7E9; &#x1F7E7; Test in cluster

&#x1F7E7; Update configuration of monitoring based on external source

&#x1F7E7; Create specific controller logic for K8s object REPLICA SET

&#x1F7E5; Create specific controller logic for K8s object JOB

&#x1F7E5; &#x1F7E6; Create unit tests

---

&#x1F7E9; - Done
&#x1F7E7; - In progress
&#x1F7E6; - Nice to have
&#x1F7E5; - To be done

# Functionality

There are basically two main approaches you can use to communicate with K8s API and react on particular events:
1. [Register a webhook](https://kubernetes.io/docs/reference/access-authn-authz/extensible-admission-controllers/) and wait for the invocation from K8s API prior create/update/delete of K8s objects. 
2. Actively listen and react on changes in K8s cluster. This functionality can be achieved with [K8s API client](https://kubernetes.io/docs/reference/using-api/client-libraries/).

Both options use so-called [Controller pattern](https://kubernetes.io/docs/concepts/architecture/controller/) to manage K8s objects and manipulate them. Our solution acts as an API client and actively listens and reacts to changes in K8s cluster in real-time. We are using one of the officially supported client libraries written in C# ([kubernetes-client/csharp](https://github.com/kubernetes-client/csharp)). On top of that we are building the application which can add/remove additional containers with network flows exporters to PODs to be able to monitor the network traffic inside these PODs. Based on metadata of particular K8s objects we decide what should be done with the K8s object and update it accordingly. Controller decisions are based on object.metadata.labels or on object.metadata.annotations values. Full list with description of used metadata is shown in the following table. 

<table>
    <thead>
        <tr>
            <th>Object.Metadata.Labels.Key</th>
            <th>Value</th>
            <th>Description</th>
        </tr>
    </thead>
    <tbody>
        <tr>
            <td rowspan=2>csirt.muni.cz/monitoring</td>
            <td>enabled</td>
            <td rowspan=2>User defined value. Defines whether containers defined by K8s object should be monitored or not. When this label is not provided at all, it's the same as Disabled option.</td>
        </tr>
        <tr>
            <td>disabled</td>
        </tr>
        <tr>
            <td rowspan=2>csirt.muni.cz/monitoringState</td>
            <td>init</td>
            <td rowspan=2>Used for internal state recognition.</td>
        </tr>
        <tr>
            <td>monitoring</td>
        </tr>
        <tr>
            <td>csirt.muni.cz/originPodName</td>
            <td>[name]</td>
            <td>Internal usage. Since PODs are immutable objects in K8s, this is used by PodsController in the following way: when monitoring on POD object is enabled original pod is deleted and new one with postfix '-monitored' is created. When monitoring is disabled on this POD, this value is used to create a new POD with the name it had before monitoring. However, user should consider using PODs as standalone objects and use some <a href="https://kubernetes.io/docs/concepts/workloads/pods/#workload-resources-for-managing-pods">workload resource</a> to manage PODs.</td>
        </tr>
    </tbody>
</table>
<table>
    <thead>
        <tr>
            <th>Object.Metadata.Annotations.Key</th>
            <th>Value</th>
            <th>Description</th>
        </tr>
    </thead>
    <tbody>
        <tr>
            <td>csirt.muni.cz/containerTemplate</td>
            <td>[name]</td>
            <td>Name of the template to be used. Templates define what monitoring container and other PODs spec fields to be applied. This is basically configuration for any other container you want to add as part of the POD.spec field. Check predefined templates <a href="src/MonitoringController/ContainerTemplates">here</a></td>
        </tr>
    </tbody>
</table>

Basic idea of workflow when event from K8s API is received is shown in the picture below.

<img alt="Kubernetes probes placement" src="/docs/resources/event_handler_flowchart.jpg"/>

# Usage

Since the whole concept of controller is focused on automatization of the process, user inputs are not needed for controller to run. It reacts to events from K8s API and makes decisions on its own. The only thing what user needs to do is to deploy this program somehow and ensure it has right configuration to be able to connect to K8s API and has permissions to read, update and delete objects in K8s API.

## Running the controller

### Requirements

- K8s cluster up and running
- kubectl or another similar tool to be able to deploy YAML files
- (optional) "cluster:admin" role credentials to be able to modify authorization rules etc. if needed

### InCluster deployment
#TODO build docker image

#TODO yaml file with user with permissions

### On-site deployment
#TODO describe what is needed

## Enabling monitoring
This applies for all already supported objects:

All you need to do is to set label in the object metadata (external system, GUI tools, "kubectl edit" etc.).

```
apiVersion: v1
kind: Pod
metadata:
  ...
  labels:
    csirt.muni.cz/monitoring: enabled
    ...
  ...
...
```

## Disabling monitoring
This applies for all already supported objects:

```
apiVersion: v1
kind: Pod
metadata:
  ...
  labels:
    csirt.muni.cz/monitoring: disabled
    ...
  ...
...
```

Also, all objects which does not contain this label are checked whether they contain monitoring container and other elements managed by controller during processing, so disabling may be as simple as removing this label completely from K8s object.
