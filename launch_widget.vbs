Set fso = CreateObject("Scripting.FileSystemObject")
Set shell = CreateObject("Shell.Application")

scriptDir = fso.GetParentFolderName(WScript.ScriptFullName)
appPath = fso.BuildPath(scriptDir, "pc_vitals_widget.pyw")

' Launch pythonw.exe as Administrator (required on Windows to access CPU thermal MSR sensors)
shell.ShellExecute "pythonw.exe", """" & appPath & """", scriptDir, "runas", 1
