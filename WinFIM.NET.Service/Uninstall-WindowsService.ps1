<#
.SYNOPSIS
    This Powershell script stops and uninstalls the Windows Service for WinFIM.NET

.NOTES
    Author: WinFIM.NET
    Written for Powershell 5.1 and later
#>

#Requires -RunAsAdministrator
$ServiceName = "WinFIM.NET.Service"

$Service = Get-Service $ServiceName -ErrorAction SilentlyContinue
if($Service) {
    if ($Service.Status -eq "Running") {
        write-host "Stopping Windows Service $ServiceName..."
        Stop-Service -Name $ServiceName
    }
    write-host "Removing Windows Service $ServiceName..."
    sc.exe delete $ServiceName
}
else {
  write-host "Skipping - Windows Service $ServiceName not detected"
} 