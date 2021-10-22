# KubernetesNetworkMonitoringController

## Controller definition (from K8s documentation)

A controller tracks at least one Kubernetes resource type. These objects have a spec field that represents the desired state. The controller for that resource are responsible for making the current state come closer to that desired state. 

The controller might carry the action out itself; more commonly, in Kubernetes, a controller will send messages to the API server that have useful side effects. You'll see examples of this below."

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

&#x1F7E7; Create specific controller logic for K8s object REPLICA SET

&#x1F7E5; Create specific controller logic for K8s object JOB

&#x1F7E6; &#x1F7E5; Create unit tests

---

&#x1F7E9; - Done
&#x1F7E7; - In progress
&#x1F7E6; - Nice to have
&#x1F7E5; - To be done

# Functionality

#TODO describe functionality

<img alt="Kubernetes probes placement" src="/docs/resources/event_handler_flowchart.jpg"/>

---

Note: It is good to know that our project has dependency and use [kubernetes-client/csharp](https://github.com/kubernetes-client/csharp) to communicate with K8s API. It's inspired with this client source code and uses generic approach and extension methods as a functional elements.

# Usage

#TODO describe usage

## Requirements

- K8s cluster up and running
- kubectl or another similar tool to be able to deploy YAML files
- (optional) "cluster:admin" role credentials to be able to modify authorization rules etc. if needed
