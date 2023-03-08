<#
.SYNOPSIS
    This Powershell script installs the Windows Service for WinFIM.NET

.NOTES
    Author: WinFIM.NET
    Written for Powershell 5.1 and later. (later versions of Powershell have better tools)
#>

#Requires -RunAsAdministrator
$ErrorActionPreference = "Stop" # Stop on 1st error
$ServiceName = "WinFIM.NET.Service"
$Description = "File Integrity Monitoring service for Windows"
$BinaryPathName = [System.IO.Path]::Combine($PSScriptRoot, "WinFIM.NET.Service.exe")

if(-Not(Test-Path $BinaryPathName)) {
    write-Error "Cannot install Windows service ${ServiceName} - path $BinaryPathName does not exist"
}
$Service = Get-Service $ServiceName -ErrorAction SilentlyContinue
if (-Not($Service)) {
    $params = @{
        Name = $ServiceName
        BinaryPathName = $BinaryPathName
        Description = $Description
    }
    write-host "Registering Windows service ${ServiceName}" -ForegroundColor Green
    $Service = New-Service @params
}
else {
    write-host "Windows service ${ServiceName} already exists" -ForegroundColor Yellow
}
write-host "Configuring Windows Service $ServiceName to restart upon failure..."
sc.exe failure $ServiceName reset= 30 actions= restart/5000
if ($targetService.Status -ne "Running") {
    write-host "Starting Windows Service $ServiceName..."
    Start-Service -Name $ServiceName
}
Get-Service -name $ServiceName
