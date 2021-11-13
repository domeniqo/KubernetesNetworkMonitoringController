from mcr.microsoft.com/dotnet/sdk:5.0 as builder
RUN git clone https://github.com/domeniqo/KubernetesNetworkMonitoringController.git
WORKDIR KubernetesNetworkMonitoringController/src/MonitoringController
RUN dotnet build

from mcr.microsoft.com/dotnet/runtime:5.0 as finalImage
COPY --from=builder /KubernetesNetworkMonitoringController/src/MonitoringController/bin/Debug/net5.0/ /app
WORKDIR app
#COPY admin.conf #this is needed if you run controller outside K8s cluster you are connecting to, you can add 'admin.conf' also to source folder of the project and it will be copied automatically (check 'MonitoringController.csproj' file)
CMD dotnet MonitoringController.dll
