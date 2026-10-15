
!include "MUI2.nsh"
!include "FileFunc.nsh"

Name "Mail2EInvoice"
OutFile "Mail2EInvoice_Setup.exe"
InstallDir "$PROGRAMFILES64\Mail2EInvoice"
RequestExecutionLevel admin

Page directory
Page instfiles

UninstPage uninstConfirm
UninstPage instfiles

Section "Install"

  SetOutPath "$INSTDIR"
  
  ; --- Stop existing service (if installed) ---
  DetailPrint "Stopping existing service (if running)..."
  ExecWait 'sc stop Mail2EInvoice'

  ; Optional: wait a moment to ensure shutdown
  Sleep 2000

  ; --- Files ---
  CreateDirectory "$INSTDIR"
  CreateDirectory "$INSTDIR\Example"
  CreateDirectory "$INSTDIR\Example\Input"
  CreateDirectory "$INSTDIR\Example\Output"
  CreateDirectory "$INSTDIR\Example\Backup"
  CreateDirectory "$INSTDIR\Example\Error"
  SetOutPath "$INSTDIR"
  File /r "Mail2EInvoice\bin\Release\net10.0\win-x64\publish\*.*"

  ; Only copy Configuration.json if NOT exists
  IfFileExists "$INSTDIR\Configuration.json" skip_settings
    SetOutPath "$INSTDIR"
    File "Configuration.json"
  skip_settings:

  ; --- Shortcut (Icon equivalent) ---
  ;CreateShortcut "$INSTDIR\WhiteBox Configurator.lnk" "$INSTDIR\Configuration\WhiteBox.Configurator.exe"
  
  ; --- Install Dotnet Runtime ---
  ExecWait '"$INSTDIR\Runtime\dotnet-runtime-10-win-x64.exe" /install /quiet /norestart'

  ; --- Install service ---
  DetailPrint "Creating Windows service..."
  ExecWait 'sc create Mail2EInvoice binPath= "$INSTDIR\Mail2EInvoice.exe" start= auto'
  ExecWait 'sc description Mail2EInvoice "Verarbeitet E-Mails (EML-Dateien) und bereitet Anhänge zur Weiterverarbeitung auf."'

  ; --- Start service ---
  DetailPrint "Starting service..."
  ExecWait 'sc start Mail2EInvoice'

  ; --- Uninstaller ---
  WriteUninstaller "$INSTDIR\Uninstall.exe"

SectionEnd


Section "Uninstall"

  ; Stop service
  DetailPrint "Stopping service..."
  ExecWait 'sc stop Mail2EInvoice'

  ; Delete service
  DetailPrint "Deleting service..."
  ExecWait 'sc delete Mail2EInvoice'

  ; Remove installed files
  RMDir /r "$INSTDIR"

SectionEnd
