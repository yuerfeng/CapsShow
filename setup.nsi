!include "MUI2.nsh"

!define APP_NAME "CapsShow"
!define APP_VERSION "1.0.0"
!define COMPANY_NAME "YourCompany"
!define SOURCE_DIR "D:\workspace\net\CapsShow\bin\Release\net8.0-windows\win-x64\publish"

Name "${APP_NAME}"
OutFile "CapsShow setup.exe"
InstallDir "$PROGRAMFILES\${APP_NAME}"
InstallDirRegKey HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\${APP_NAME}" "InstallLocation"

RequestExecutionLevel admin

!insertmacro MUI_PAGE_WELCOME
!insertmacro MUI_PAGE_DIRECTORY
!insertmacro MUI_PAGE_INSTFILES
!insertmacro MUI_PAGE_FINISH

!insertmacro MUI_UNPAGE_CONFIRM
!insertmacro MUI_UNPAGE_INSTFILES
!insertmacro MUI_UNPAGE_FINISH

!insertmacro MUI_LANGUAGE "SimpChinese"

Section "Install"
    SetOutPath "$INSTDIR"
    File /r "${SOURCE_DIR}\*.*"

    CreateShortCut "$DESKTOP\CapsShow.lnk" "$INSTDIR\CapsShow.exe"
    CreateDirectory "$SMPROGRAMS\CapsShow"
    CreateShortCut "$SMPROGRAMS\CapsShow\CapsShow.lnk" "$INSTDIR\CapsShow.exe"
    CreateShortCut "$SMPROGRAMS\CapsShow\卸载 CapsShow.lnk" "$INSTDIR\Uninstall.exe"

    WriteUninstaller "$INSTDIR\Uninstall.exe"

    WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\${APP_NAME}" "DisplayName" "${APP_NAME}"
    WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\${APP_NAME}" "UninstallString" "$INSTDIR\Uninstall.exe"
    WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\${APP_NAME}" "DisplayVersion" "${APP_VERSION}"
    WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\${APP_NAME}" "Publisher" "${COMPANY_NAME}"
    WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\${APP_NAME}" "InstallLocation" "$INSTDIR"

    WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Run" "${APP_NAME}" '"$INSTDIR\CapsShow.exe"'
SectionEnd

Section "Uninstall"
    Delete "$DESKTOP\CapsShow.lnk"
    Delete "$SMPROGRAMS\CapsShow\CapsShow.lnk"
    Delete "$SMPROGRAMS\CapsShow\卸载 CapsShow.lnk"
    RMDir "$SMPROGRAMS\CapsShow"

    RMDir /r "$INSTDIR"

    DeleteRegKey HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\${APP_NAME}"
    DeleteRegValue HKCU "Software\Microsoft\Windows\CurrentVersion\Run" "${APP_NAME}"
SectionEnd
