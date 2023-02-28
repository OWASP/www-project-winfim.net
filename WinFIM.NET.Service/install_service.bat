%windir%\Microsoft.NET\Framework64\v4.0.30319\InstallUtil.exe "WinFIM.NET.Service.exe"
sc.exe failure "WinFIM.NET.Service" reset= 30 actions= restart/5000