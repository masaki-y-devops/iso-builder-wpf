## Overview

When you drag and drop the file folder (socrial possible), you can compress it to `Backup_ (today's date).zip``,

I tried to create an app that outputs it to the desktop as "Backup_ (today's date). iso" as it is.

Created using AI (Gemini) support. **Implementation → Problems occurred → Ask questions and crush**, I was able to turn at a relatively high speed.

## Why did you make it?

In addition to the internal hard drive, USB HDD, for file backup,

**Used by buying and using optical discs (BD-R, some M-DISCs) with their advantages in non-rewrite properties.

Previously, ImgBurn (https://portableapps.com/apps/music_video/imgburn-portable) and [BurnAware] (https://burnaware.com/) created ISO manually,

When the directory hierarchy deepened, ISO creation itself was possible, but when burning to the disk, there were many errors and many empty disks were was wasted.

So I had no choice,** I manually put it into ZIP and made it ISO, but this is very troublesome. **

The output destination is a desktop, so if you do these routine tasks in one stop after D & D, you can do these routine tasks in one stop, you can just prepare and burn an empty disk, and you have realized some automation**.

## How to use

It is for x86-64 only.

. NET 10 runtime is required. Download here → [Microsoft official] (https://aka.ms/dotnet/1.0/windowsdeskstop-runtime-win-x64.exe)

1. Please download from the Release page and start it in any directory.

2. After starting, drag and drop the file folder you want to process with Don at once.

→ The progress bar is up and `Backup_YYYYmmdd.zip` is created on the desktop while the file folder structure is still there.

→ After that, based on the ZIP, `Backup_YYmmdd.iso` will also be created on the desktop.

→ Done! After that, cook it and bake it on a physical disk with ImgBurn.

## What I considered and what I was hooked

### Assignment issues for each step of the progress bar.

Since it is difficult to obtain a progress of ISO creation, I made it a step up to ZIP creation and occupied up to 99%. The remaining one percent was allocated to ISO creation.

###`Functional problem solving when implementing "CancellationToken``".

If you do not split the check frequency, the cancellation token check will not be turned around until the ZIP creation is completed, and the cancellation process cannot be done even if you press the button on the GUI.

Therefore, for the purpose of monitoring the issuance of detailed cancellation tokens, the ZIP creation is divided into a loop every 8KB, and the check is turned every time the loop is turned.

### (sorberly) I was hooked for a small hour without noticing that the file name and the label name of the ISO file are different

I was thinking about the flow of assigning a file name with an independent method (including commented out such as `GetSaveZipPath``),

Since the extension is unnecessary for the label name, it is necessary to remove the part at the time of assignment, so there was a feeling of overturning the main end.

Therefore, in order to prevent the protrusion of the method, the label name is specified as ``DateTime.Now:yyyyMMdd``, and settled on the approach of assigning it directly.

### Desktop notifications on completion.

Utilizing the Windows11 standard notification center.

### Separate source files by partial class. I tried to divide it by screen and logic

Initially, it was described only in `MainWindow.xaml.cs``, scrolling increased and readability was greatly reduced.

Therefore, the save name generation method, ZIP creation method, and ISO creation method are separated as ``MainWindow.xaml.Core.cs``.

By using the ``partial's method to divide the same class, I was able to experience the practice of maintaining maintainability.
