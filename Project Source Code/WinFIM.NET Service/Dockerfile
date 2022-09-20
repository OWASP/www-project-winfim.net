#Depending on the operating system of the host machines(s) that will build or run the containers, the image specified in the FROM statement may need to be changed.
#For more information, please see https://aka.ms/containercompat


# A Dockerfile to build winfim.net as a Windows container image which can be used in Docker / Kubernetes.

# This Dockerfile was designed to be run from Visual Studio using the "Docker Compose" project, select the Release profile for this dockerfile to build properly
# To build this Docker image from a commandline instead, replace the below copy command with: COPY  . C:\\Tools\WinFIM.NET
# docker build --tag winfim.net:1.0.0 --tag winfim.net:latest .

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
# RUN     sc.exe config "WinFIM.NET` Service" start=delayed-auto

RUN     .\Update-DockerImage.ps1"; \
        Remove-Item "Update-DockerImage.ps1"

CMD     Write-Host "WinFIM.net container"; \
        Write-Host "Starting fluent-bit"; \
        c:\tools\fluent-bit\fluent-bit.exe -c c:\tools\fluent-bit\fluent-bit.conf
