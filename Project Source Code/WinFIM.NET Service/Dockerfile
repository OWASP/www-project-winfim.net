# A Dockerfile to build winfim.net as a Windows container image which can be used in Docker / Kubernetes.

# Depending on the operating system of the host machines(s) that will build or run the containers, the image specified in the FROM statement may need to be changed.
# For more information, please see https://aka.ms/containercompat

# This Dockerfile cam be built from Visual Studio or from a commandline.
# To build from Visual Studio, using the "Docker Compose" project, select the Release profile for this dockerfile to build properly
# To build this Docker image from a commandline, you can run this sample command:
#   docker build --tag winfim.net:1.0.0 .

# Sample Command to run this Docker image in docker:
# docker run --name winfim --volume "C:\:C:\host:ro" --rm -it winfim.net:latest

# Indicates that the windowsservercore image will be used as the base image.
FROM mcr.microsoft.com/dotnet/framework/runtime:4.8-windowsservercore-ltsc2019

ARG     source

LABEL   org.opencontainers.image.url="https://github.com/OWASP/www-project-winfim.net"

WORKDIR C:\\Tools\\WinFIM.NET

COPY    ${source:-obj/Docker/publish} .

SHELL   ["powershell", "$ErrorActionPreference = 'Stop'; $ProgressPreference = 'SilentlyContinue';"]

# Install WinFIM.NET service. 
# Note the required ` character before the space in the service name and BinaryPathName
RUN     New-Service -Name "WinFIM.NET` Service" -BinaryPathName "c:\tools\WinFIM.NET\WinFIM.NET` Service.exe"

# Set the WinFIM.NET service to delayed start, to give time for the host directories to be recognized by the container
# RUN   sc.exe config "WinFIM.NET` Service" start=delayed-auto

# Restart the Windows service if it fails, where:
#    The Failure count is reset to 0 after 30 seconds
#    Restart the failed service after 5 seconds.
RUN     sc.exe failure "WinFIM.NET` Service" reset= 30 actions= restart/5000

RUN     .\Update-DockerImage.ps1"; \
        Remove-Item "Update-DockerImage.ps1"

CMD     Write-Host "WinFIM.net container"; \
        Write-Host "Starting fluent-bit"; \
        c:\tools\fluent-bit\fluent-bit.exe -c c:\tools\fluent-bit\fluent-bit.conf
