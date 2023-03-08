<#
    .SYNOPSIS
    A Powershell script to build and run the WinFIM.NET docker image

    .EXAMPLE
    From Windows Powershell command prompt:
        PS> ./Build-DockerImage.ps1

    .NOTES
        Author: WinFIM.NET
        Written for Powershell 5.1 and later
        Requirements:
            Docker Desktop
                 - Installation from Windows 10+:  winget install --id=Docker.DockerDesktop  -e
                 - more info: https://www.docker.com/products/docker-desktop/

#>

# Build the Docker image
$imageName = "winfim.net"
$tag = "latest"
Write-Host "Building Docker image ${ImageName}:${tag}"

docker build -f .\WinFIM.NET.Service\Dockerfile --force-rm --tag ${ImageName}:${tag} .

Write-Host "To run a container from this image, type:"
Write-Host "docker run --name winfim --volume ""C:\:C:\host:ro"" --rm -it ${ImageName}:${tag} powershell"

Write-Host "Finished"
