### WinFIM.NET Information
WinFIM.NET - File Integrity Monitoring For Windows

There are plenty of commercial tools to do file integrity monitoring (FIM). But, for freeware / Open Source, especially for Windows, it seems not much options.

A small Windows Service named [“WinFIM.NET”](https://github.com/OWASP/www-project-winfim.net) was developed trying to fill up this gap.

### Downloads or Social Links
* Developer's blog: [https://redblueteam.wordpress.com/](https://redblueteam.wordpress.com/)

### Change Log
#### 2022-10-26 v1.2.0

- on initial run, do not warn if base paths are not detected

- Bumped assembly to 1.2.0.0, setup project and docker image versions to 1.2.0

- New switch: is_log_to_windows_eventlog - default value is true

- New switch: is_capture_remote_connection_status - default value is false

- check directory owner

- Performance improvement - only checks file ownership once per cycle

- log to file uses JSON formatter by default

- Updated nuget packages

- Cleared issues found by Resharper

- logs a max of 32768 characters to windows event log per reported bug

#### 2020-03-11 [Initial release](https://redblueteam.wordpress.com/2020/03/11/winfim-net-windows-file-integrity-monitoring/).

