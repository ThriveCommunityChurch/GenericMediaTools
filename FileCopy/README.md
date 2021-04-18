# File Copy
This tool is meant to be an automation tool for our video recording process that takes place multiple times each week. Currently we are using [RoboCopy](https://docs.microsoft.com/en-us/windows-server/administration/windows-commands/robocopy) via Powershell in order to transfer raw recordings from OBS to the [NAS](https://en.wikipedia.org/wiki/Network-attached_storage). This tool will help prevent us from ever having to open Powershell since this should just run as a background task on the host machine (Windows). 

## Generic Requirements
- Must be able to run silently in the background (no manual triggering should be required)
- Must be able to delete files _after_ they are successfully copied to the configured directory, in order to save space on the host machine
- Must be able to log errors / informational messages to a log file that can be accessed later in the event an error occurs
- Must be able to handle files that are being accessed by other processes (ie. during an OBS recording)
- Should not run continuiously which would be a waste of system resources (CPU cycles), and should be able to be configured how often the process is fired.
