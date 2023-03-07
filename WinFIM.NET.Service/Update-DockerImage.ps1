<#
    .SYNOPSIS
        This Powershell script updates the WinFIM.NET docker image.
        It does the following:
        - replace contents of monlist.txt with paths relevant to a WinFIM.NET Docker container monitoring it's host

    .NOTES
        Author: WinFIM.NET
#>

# Powershell runtime options
$ErrorActionPreference = "Stop" # Stop script on first error

# functions
function Update-MonList {
    param ($TargetDirName)
    $TargetFilePath = Join-Path "$TargetDirName" "monlist.txt"
    Write-Host "Updating file $TargetFilePath"
    $monListTxt = @'
C:\host\autoexec.bat
C:\host\boot.ini
C:\host\config.sys
C:\host\Program` Files\Microsoft` Security` Client\msseces.exe
C:\host\Windows\explorer.exe
C:\host\Windows\regedit.exe
C:\host\windows\system.ini
C:\host\Windows\System32\userinit.exe
C:\host\windows\win.ini
C:\host\test
'@
    write-output $monListTxt > $TargetFilePath
}

# main program
Update-monlist          -TargetDirName "C:\app"
