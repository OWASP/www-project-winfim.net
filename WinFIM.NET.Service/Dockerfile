# A Dockerfile to build winfim.net as a Windows container image which can be used in Docker / Kubernetes.
# Docker Deskop should be in Windows mode before building. (& $Env:ProgramFiles\Docker\Docker\DockerCli.exe -SwitchWindowsEngine)

# This Dockerfile cam be built from Visual Studio.
# To build from Visual Studio, using the "Docker Compose" project, select the Release profile for this dockerfile to build properly

# To build this Docker image from a commandline instead, from the compiled directory:
#   Replace the below COPY command with: COPY  . C:\\Tools\WinFIM.NET
#   Replace the contents of the .dockerignore file with: Dockerfile
#   Then run this sample command:
#   docker build --tag winfim.net:latest .

# Sample commands to run this Docker image in docker:
# docker run --name winfim --volume "C:\:C:\host:ro" --net "Default Switch" --rm -it winfim.net:latest
# docker run --name winfim --volume "C:\:C:\host:ro" --net "Default Switch" --rm -it winfim.net:latest powershell

# Sample command to give this image a new tag:
# docker tag winfim.net:latest winfim.net:1.2.0-2019
# docker tag winfim.net:latest winfim.net:1.2.0-2022

# Depending on the operating system of the host machines(s) that will build or run the containers, the image specified in the FROM statement may need to be changed.
# For more information, please see https://aka.ms/containercompat
FROM mcr.microsoft.com/dotnet/framework/runtime:4.8-windowsservercore-ltsc2019
# FROM    mcr.microsoft.com/dotnet/framework/runtime:4.8-windowsservercore-ltsc2022

ARG     source

LABEL   org.opencontainers.image.url="https://github.com/OWASP/www-project-winfim.net"

WORKDIR C:\\Tools\\WinFIM.NET

COPY    ${source:-obj/Docker/publish} .

SHELL   ["powershell", "$ErrorActionPreference = 'Stop'; $ProgressPreference = 'SilentlyContinue';"]

# Install WinFIM.NET.Service. 
# Note the required ` character before the space in the service name and BinaryPathName
RUN     New-Service -Name "WinFIM.NET` Service" -BinaryPathName "c:\tools\WinFIM.NET\WinFIM.NET` Service.exe"

# Set the WinFIM.NET.Service to delayed start, to give time for the host directories to be recognized by the container
# RUN   sc.exe config "WinFIM.NET` Service" start=delayed-auto

# Restart the Windows service if it fails, where the Failure count is reset to 0 after 30 seconds, Restart the failed service after 5 seconds.
# Set startup to delayed so the service startup doesn't conflict with other starting processes, e.g. networking
RUN     sc.exe failure "WinFIM.NET` Service" reset= 30 actions= restart/5000
        # sc.exe config "WinFIM.NET` Service" start=delayed-auto

# Run a script to install Fluent-bit, VIM and replace monlist.txt with watched paths that make sense for a Docker container watching it's host's C drive
RUN     .\Update-DockerImage.ps1"; \
        Remove-Item "Update-DockerImage.ps1"

# Commands to launch on container startup. 
# Start the WinFIM.NET.Service with a short delay so there are no conflicts with other starting up processes, e.g. networking.
CMD     c:\tools\fluent-bit\fluent-bit.exe -c c:\tools\fluent-bit\fluent-bit.conf

# Cleanup
RUN     Stop-Service -Name "WinFIM.NET` Service"; \
        Remove-Item *.log; \
        Remove-Item fimdb.db*; \
        remove-item c:\tools\fluent-bit\fluent-bit.db*