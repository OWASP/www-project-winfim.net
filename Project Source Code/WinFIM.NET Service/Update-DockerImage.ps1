<#
    .SYNOPSIS
        This Powershell script updates the WinFIM.NET docker image.
        It does the following:
        - Downloads / installs / configures Fluent-bit to pipe logs to STDOUT which can be read by Docker logs (Example docker command: docker logs winfim)
        - Downloads / installs vim  to enable editing of files directly in a running container
        - replace contents of monlist.txt with paths relevant to a WinFIM.NET Docker container monitoring it's host
        - Configures WinFIM.NET to not save to Windows Event logs - Fluent-bit is used instead
        - Has a function to import root certs - useful for Fluent-bit output plugins, with the Azure root cert as an example. Tested using the Fluent-Bit azure_blob plugin

    .NOTES
        Author: WinFIM.NET
#>

# Powershell runtime options
$ErrorActionPreference = "Stop" # Stop script on first error

# functions
function New-Directory {
    param ($TargetDirName)
    if (-Not(Test-Path -Path "$TargetDirName")) {
        Write-Host "Creating directory: $TargetDirName"
        New-Item -Type Directory -Path $TargetDirName | Out-Null
    }
}

function Remove-Directory {
    param ($TargetDirName)
    if (Test-Path -Path "$TargetDirName") {
        Write-Host -Level INFO "Deleting directory: $TargetDirName"
        remove-item "$TargetDirName" -Force -Recurse
    }
}

function Set-WindowsPath {
    param ($TargetDirName, [ValidateSet('User', 'Machine')] $Scope)
    $oldPath = [System.Environment]::GetEnvironmentVariable("Path", $Scope)
    $oldPathArray = ($oldPath) -split ";"
    if ($oldPathArray -Contains "$TargetDirName") {
        write-host "Skipping Adding directory $TargetDirName to $Scope Path - already exists"
    }
    else {
        write-host "Adding directory $TargetDirName to $Scope Path"
        $newPath = $oldPath, $TargetDirName -join ";" -replace ";+", ";"
        [System.Environment]::SetEnvironmentVariable("Path", $newPath, $Scope)
        $env:Path = [System.Environment]::GetEnvironmentVariable("Path", "User"), [System.Environment]::GetEnvironmentVariable("Path", "Machine") -join ";"
    }
}

function Set-DnsServer {
    # Temporarily set the DNS Server while building the image to enable downloading files from the Internet
    $serverAddress = "1.1.1.1"
    write-host "Setting DNS Client server address to $serverAddress"
    $networkInterfaceIndex = Get-NetAdapter | Select-Object -ExpandProperty InterfaceIndex
    Set-DnsClientServerAddress -InterfaceIndex $networkInterfaceIndex -ServerAddress $serverAddress
}

function Get-FluentBit {
    # Download a popular opensource log processor and forwarder called Fluent Bit
    param ($TargetDirName)
    [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
    $fullVersion = "2.0.4"
    $regexPatternMajorMinorVersion = [Regex]::new('^(\d+).(\d+)') # Pattern match example: matches "2.0" from "2.0.4"
    $majorMinorVersion = $regexPatternMajorMinorVersion.Matches($fullVersion).Value
    $DownloadUrl = "https://fluentbit.io/releases/${majorMinorVersion}/fluent-bit-${fullVersion}-win64.zip"
    $DownloadFileName = "fluent-bit-${fullVersion}-win64.zip"
    $TempDir = Join-Path "$Env:Temp" "fluent-bit"
    Remove-Directory $TempDir
    New-Directory $TempDir
    New-Directory $TargetDirName
    Write-Host "Downloading file $DownloadFileName to temp directory: $TempDir"
    $DownloadedFilePath = Join-Path "$TempDir" "$DownloadFileName"
    Invoke-WebRequest $DownloadUrl -OutFile "$DownloadedFilePath"
    Expand-Archive -LiteralPath "$DownloadedFilePath" -DestinationPath $TempDir
    Write-Host "Moving extracted FluentBit files to: $TargetDirName"
    Move-Item $TempDir\fluent-bit-${fullVersion}-win64\bin\* $TargetDirName -Force
    Move-Item $TempDir\fluent-bit-${fullVersion}-win64\conf\* $TargetDirName -Force
    Remove-Directory $TempDir
}
function Get-Vim {
    # Download a popular opensource text editor called VIM so users can edit text based files inside the container
    param ($TargetDirName)
    [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
    $version = "9.0.0000"
    $DownloadUrl = "https://github.com/vim/vim-win32-installer/releases/download/v${version}/gvim_${version}_x64_signed.zip"
    $DownloadFileName = "gvim_${version}_x64_signed.zip"
    $TempDir = Join-Path "$Env:Temp" "vim"
    Remove-Directory $TempDir
    New-Directory $TempDir
    New-Directory $TargetDirName
    Write-Host "Downloading file $DownloadFileName to: $TargetDirName"
    $DownloadedFilePath = Join-Path "$TempDir" "$DownloadFileName"
    Invoke-WebRequest $DownloadUrl -OutFile "$DownloadedFilePath"
    Expand-Archive -LiteralPath "$DownloadedFilePath" -DestinationPath $TempDir
    # Remove-Item $DownloadedFilePath
    # Get-ChildItem $TempDir\vim\vim90\*.* -Recurse | Move-Item -Destination $TargetDirName -Force
    Move-Item $TempDir\vim\vim90\vim.exe $TargetDirName -Force
    Set-WindowsPath -TargetDirName $TargetDirName -Scope Machine
    Remove-Directory $TempDir
}

function Update-FluentBitConf {
    param ($TargetDirName)
    $TargetFilePath = Join-Path "$TargetDirName" "fluent-bit.conf"
    Write-Host "Updating file $TargetFilePath"
    $fluentBitConf = @'
[SERVICE]
    flush        1
    daemon       Off
    log_level    info
    parsers_file parsers.conf
    plugins_file plugins.conf
    http_server  Off
    http_listen  0.0.0.0
    http_port    2020
    storage.metrics on

[INPUT]
    Name        tail
    Path        C:\Tools\WinFIM.NET\*.log
    db          C:\Tools\fluent-bit\fluent-bit.db

[OUTPUT]
    name  stdout
    match *

'@
    write-output $fluentBitConf > $TargetFilePath
    Write-Host "Converting $TargetFilePath to Linux line endings"
    ((Get-Content $TargetFilePath) -join "`n") + "`n" | Set-Content -NoNewline $TargetFilePath
}

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

function Update-WinFimAppConfig {
    # set App.Config setting "is_log_to_windows_eventlog" to False
    $targetFile = "$PSScriptRoot\WinFIM.NET Service.exe.config"
    $xmlContent = [xml](Get-Content -Path $targetFile)
    if (($node = $xmlContent.SelectSingleNode("//configuration/applicationSettings/WinFIM.NET_Service.Properties.Settings/setting[@name = 'is_log_to_windows_eventlog']"))) {
        if ($node.Value -ne "False") { 
            $node.value = "False"
            $xmlContent.Save($targetFile)
        }
    }
    
}

function Import-RootCerts {
    # Example: Import the Azure root cert for the built in Fluent-bit Azure plugins to be able to upload to Azure. Add more root certs if using different Fluent-bit plugin(s) that need them.
    # The $AzureBaltimoreCyberTrustRoot link is from site: https://learn.microsoft.com/en-us/azure/security/fundamentals/azure-ca-details
    $AzureBaltimoreCyberTrustRoot = "https://crt.sh/?d=76"
    Invoke-WebRequest -URI $AzureBaltimoreCyberTrustRoot -OutFile "C:\AzureBaltimoreCyberTrustRoot.cer"
    Import-Certificate -FilePath C:\AzureBaltimoreCyberTrustRoot.cer -CertStoreLocation Cert:\LocalMachine\Root\
}

# main program
Set-DnsServer
Get-FluentBit           -TargetDirName "C:\Tools\fluent-bit"
Update-FluentBitConf    -TargetDirName "C:\Tools\fluent-bit"
Get-Vim                 -TargetDirName "C:\Tools\vim"
Update-monlist          -TargetDirName "C:\Tools\WinFIM.NET"
Update-WinFimAppConfig
Import-RootCerts
