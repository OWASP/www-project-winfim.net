#Depending on the operating system of the host machines(s) that will build or run the containers, the image specified in the FROM statement may need to be changed.
#For more information, please see https://aka.ms/containercompat


# A Dockerfile to build winfim.net as a Windows container image which can be used in Docker / Kubernetes.

# This Dockerfile was designed to be run from Visual Studio using the "Docker Compose" project, select the Release profile for this dockerfile to build properly
# To build this Docker image from a commandline instead, replace the below copy command with: COPY  . C:\\Tools\WinFIM.NET
# docker build --tag winfim.net:1.0.0 --tag winfim.net:latest .

# Sample Command to run this Docker image in docker:
# docker run --name winfim.net_sample --volume "C:\:C:\host:ro" --rm -it winfim.net:1.0.0

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
RUN     sc.exe config "WinFIM.NET` Service" start=delayed-auto

# Install fluent-bit for logging to Docker stdout
RUN     [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12; \
        $serverAddress = \"1.1.1.1\"; \
        write-host "Setting DNS Client server address to $serverAddress"; \
        $networkInterfaceIndex = Get-NetAdapter | Select-Object -ExpandProperty InterfaceIndex; \
        Set-DnsClientServerAddress -InterfaceIndex $networkInterfaceIndex -ServerAddress $serverAddress; \
        $targetDirName=\"C:\Tools\fluent-bit\"; \
        $DownloadUrl=\"https://fluentbit.io/releases/1.9/fluent-bit-1.9.7-win64.zip\"; \
        $DownloadFileName=\"fluent-bit-1.9.7-win64.zip\"; \
        $TempDir=Join-Path \"$Env:Temp\" \"fluent-bit\"; \
        New-Item -Type Directory -Path \"$TempDir\"; \
        New-Item -Type Directory -Path \"$targetDirName\"; \
        Write-Host "Downloading file $DownloadFileName to: $targetDirName"; \
        $DownloadedFilePath = Join-Path \"$TempDir\" \"$DownloadFileName\"; \
        Invoke-WebRequest $DownloadUrl -OutFile \"$DownloadedFilePath\"; \
        Expand-Archive -LiteralPath \"$DownloadedFilePath\" -DestinationPath \"$TempDir\"; \
        Move-Item \"$TempDir\fluent-bit-1.9.7-win64\bin\*\" \"$targetDirName\" -Force; \
        Move-Item \"$TempDir\fluent-bit-1.9.7-win64\conf\*\" \"$targetDirName\" -Force; \
        remove-item \"$TempDir\" -Force -Recurse;

# Install vim for editing files directly in container
RUN     [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12; \
        $serverAddress = \"1.1.1.1\"; \
        write-host "Setting DNS Client server address to $serverAddress"; \
        $networkInterfaceIndex = Get-NetAdapter | Select-Object -ExpandProperty InterfaceIndex; \
        Set-DnsClientServerAddress -InterfaceIndex $networkInterfaceIndex -ServerAddress $serverAddress; \
        $targetDirName=\"C:\Tools\vim\"; \
        $DownloadUrl = \"https://github.com/vim/vim-win32-installer/releases/download/v9.0.0292/gvim_9.0.0292_x64_signed.zip\"; \
        $DownloadFileName = \"gvim_9.0.0292_x64_signed.zip\"; \
        $TempDir = Join-Path \"$Env:Temp\" \"vim\"; \
        New-Item -Type Directory -Path \"$TempDir\"; \
        New-Item -Type Directory -Path \"$TargetDirName\"; \
        Write-Host "Downloading file $DownloadFileName to: $TargetDirName"; \
        $DownloadedFilePath = Join-Path \"$TempDir\" \"$DownloadFileName\"; \
        Invoke-WebRequest $DownloadUrl -OutFile \"$DownloadedFilePath\"; \
        Expand-Archive -LiteralPath \"$DownloadedFilePath\" -DestinationPath \"$TempDir\"; \
        Move-Item \"$TempDir\vim\vim90\vim.exe\" \"$TargetDirName\" -Force; \
        [Environment]::SetEnvironmentVariable(\"Path\", $env:Path + \";$targetDirName\", [EnvironmentVariableTarget]::Machine); \
        remove-item \"$TempDir\" -Force -Recurse;

CMD     Write-Host "WinFIM.net container"; \
        Write-Host "Starting fluent-bit"; \
        c:\tools\fluent-bit\fluent-bit.exe -c c:\tools\fluent-bit\fluent-bit.conf
