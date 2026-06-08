; CapsShow NSIS Installer Script
; Encoding: UTF-8 BOM
; 由 build-release.ps1 调用，请勿直接手动运行（需先完成 dotnet publish）

; ---------------------------
; 1. Settings & Includes
; ---------------------------
Unicode true

!include "MUI2.nsh"
!include "LogicLib.nsh"
!include "nsDialogs.nsh"
!include "FileFunc.nsh"

RequestExecutionLevel user

; ---------------------------
; 2. Application Definitions
; ---------------------------
!define APP_NAME        "CapsShow"
!define APP_EXE         "CapsShow.exe"
!define APP_PUBLISHER   "CapsShow"

; APP_VERSION 由 build-release.ps1 通过 /DAPP_VERSION=x.x.x 传入
!ifndef APP_VERSION
    !define APP_VERSION "1.0.0"
!endif

; 发布产物目录，由 build-release.ps1 通过 /DSOURCE_DIR=... 传入
!ifndef SOURCE_DIR
    !define SOURCE_DIR "D:\workspace\my\CapsShow\publish"
!endif

!ifndef DIST_DIR
    !define DIST_DIR "D:\workspace\my\CapsShow\dist"
!endif

!define OUT_FILE        "${DIST_DIR}\${APP_NAME}-Setup-v${APP_VERSION}.exe"
!define DISPLAY_NAME    "${APP_NAME}"

!define REG_KEY         "Software\${APP_NAME}"
!define UNINSTALL_KEY   "Software\Microsoft\Windows\CurrentVersion\Uninstall\${APP_NAME}"
!define RUN_KEY         "Software\Microsoft\Windows\CurrentVersion\Run"

; ---------------------------
; 3. Installer Attributes
; ---------------------------
Name "${DISPLAY_NAME} ${APP_VERSION}"
OutFile "${OUT_FILE}"
InstallDir "$LOCALAPPDATA\Programs\${APP_NAME}"
InstallDirRegKey HKCU "${REG_KEY}" "InstallDir"

ShowInstDetails   show
ShowUnInstDetails show

VIProductVersion "${APP_VERSION}.0"
VIAddVersionKey  "ProductName"     "${DISPLAY_NAME}"
VIAddVersionKey  "CompanyName"     "${APP_PUBLISHER}"
VIAddVersionKey  "FileDescription" "${DISPLAY_NAME} Installer"
VIAddVersionKey  "FileVersion"     "${APP_VERSION}"
VIAddVersionKey  "ProductVersion"  "${APP_VERSION}"
VIAddVersionKey  "LegalCopyright"  "Copyright (c) ${APP_PUBLISHER}"

!define MUI_ICON       "main.ico"
!define MUI_UNICON     "main.ico"
!define MUI_ABORTWARNING
!define MUI_FINISHPAGE_RUN "$INSTDIR\${APP_EXE}"
!define MUI_FINISHPAGE_RUN_TEXT "启动 ${APP_NAME}"

; ---------------------------
; 4. Pages
; ---------------------------
!insertmacro MUI_PAGE_WELCOME
!insertmacro MUI_PAGE_DIRECTORY

Var Dialog
Var CheckDesktop
Var CheckStartMenu
Var CheckStartup
Page custom OptionsPageCreate OptionsPageLeave

!insertmacro MUI_PAGE_INSTFILES
!insertmacro MUI_PAGE_FINISH

!insertmacro MUI_UNPAGE_CONFIRM
!insertmacro MUI_UNPAGE_INSTFILES
!insertmacro MUI_UNPAGE_FINISH

; ---------------------------
; 5. Language
; ---------------------------
!insertmacro MUI_LANGUAGE "SimpChinese"

; ---------------------------
; 6. Custom Options Page
; ---------------------------
Function OptionsPageCreate
    !insertmacro MUI_HEADER_TEXT "安装选项" "选择要创建的快捷方式和启动选项"

    nsDialogs::Create 1018
    Pop $Dialog
    ${If} $Dialog == error
        Abort
    ${EndIf}

    ${NSD_CreateCheckBox} 0 0 100% 12u "创建桌面快捷方式"
    Pop $CheckDesktop
    ${NSD_SetState} $CheckDesktop ${BST_CHECKED}

    ${NSD_CreateCheckBox} 0 15u 100% 12u "创建开始菜单文件夹"
    Pop $CheckStartMenu
    ${NSD_SetState} $CheckStartMenu ${BST_CHECKED}

    ${NSD_CreateCheckBox} 0 30u 100% 12u "开机自动启动"
    Pop $CheckStartup
    ${NSD_SetState} $CheckStartup ${BST_UNCHECKED}

    nsDialogs::Show
FunctionEnd

Function OptionsPageLeave
    ; 桌面快捷方式
    ${NSD_GetState} $CheckDesktop $R0
    ${If} $R0 == ${BST_CHECKED}
        SetOutPath "$INSTDIR"
        CreateShortcut "$DESKTOP\${APP_NAME}.lnk" "$INSTDIR\${APP_EXE}"
    ${EndIf}

    ; 开始菜单
    ${NSD_GetState} $CheckStartMenu $R0
    ${If} $R0 == ${BST_CHECKED}
        CreateDirectory "$SMPROGRAMS\${APP_NAME}"
        SetOutPath "$INSTDIR"
        CreateShortcut "$SMPROGRAMS\${APP_NAME}\${APP_NAME}.lnk" "$INSTDIR\${APP_EXE}"
        CreateShortcut "$SMPROGRAMS\${APP_NAME}\卸载 ${APP_NAME}.lnk" "$INSTDIR\uninstaller.exe"
    ${EndIf}

    ; 开机启动
    ${NSD_GetState} $CheckStartup $R0
    ${If} $R0 == ${BST_CHECKED}
        WriteRegStr HKCU "${RUN_KEY}" "${APP_NAME}" '"$INSTDIR\${APP_EXE}"'
    ${EndIf}
FunctionEnd

; ---------------------------
; 7. Installation Section
; ---------------------------
Section "Install" SecInstall
    SetOutPath "$INSTDIR"
    SetOverwrite on

    ; 写入卸载信息（控制面板"程序和功能"可见）
    WriteRegStr   HKCU "${UNINSTALL_KEY}" "DisplayName"     "${DISPLAY_NAME}"
    WriteRegStr   HKCU "${UNINSTALL_KEY}" "DisplayVersion"  "${APP_VERSION}"
    WriteRegStr   HKCU "${UNINSTALL_KEY}" "Publisher"       "${APP_PUBLISHER}"
    WriteRegStr   HKCU "${UNINSTALL_KEY}" "InstallLocation" "$INSTDIR"
    WriteRegStr   HKCU "${UNINSTALL_KEY}" "DisplayIcon"     "$INSTDIR\${APP_EXE}"
    WriteRegStr   HKCU "${UNINSTALL_KEY}" "UninstallString" '"$INSTDIR\uninstaller.exe"'
    WriteRegDWORD HKCU "${UNINSTALL_KEY}" "NoModify"        1
    WriteRegDWORD HKCU "${UNINSTALL_KEY}" "NoRepair"        1

    WriteRegStr HKCU "${REG_KEY}" "InstallDir" "$INSTDIR"

    ; 递归复制所有发布产物
    File /r "${SOURCE_DIR}\*.*"

    ; 生成卸载程序
    WriteUninstaller "$INSTDIR\uninstaller.exe"

    ; 记录安装大小
    ${GetSize} "$INSTDIR" "/S=0K" $0 $1 $2
    IntFmt $0 "0x%08X" $0
    WriteRegDWORD HKCU "${UNINSTALL_KEY}" "EstimatedSize" "$0"
SectionEnd

; ---------------------------
; 8. Uninstaller Section
; ---------------------------
Section "Uninstall"
    ; 删除安装目录中的所有文件
    RMDir /r "$INSTDIR"

    ; 删除快捷方式
    Delete "$DESKTOP\${APP_NAME}.lnk"
    Delete "$SMPROGRAMS\${APP_NAME}\*.*"
    RMDir  "$SMPROGRAMS\${APP_NAME}"

    ; 清理注册表
    DeleteRegKey   HKCU "${UNINSTALL_KEY}"
    DeleteRegKey   HKCU "${REG_KEY}"
    DeleteRegValue HKCU "${RUN_KEY}" "${APP_NAME}"
SectionEnd
