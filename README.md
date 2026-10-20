## Overview

This creates simple zip archive to preserve original file structure.

## How to use

It is for x86-64 only.

.NET 10 runtime is required. 

To intstall, Run below on cmd.exe as admin:

```cmd
winget install Microsoft.DotNet.DesktopRuntime.10
```

1. Please download executables from the Release page and save, then launch it.

2. After starting, drag and drop the file folder you want to process.

→ The progress bar is up and `Backup_YYYYmmdd.zip` is created on the desktop while the file folder structure is still there.

→ After that, based on the ZIP, `Backup_YYmmdd.iso` will also be created on the desktop.
