!include "MUI2.nsh"
!include "FileFunc.nsh"

# General configuration
!define PRODUCT_NAME "TimeFly"
!define PRODUCT_DESCRIPTION "TimeFly - Digital Art Focus Tracker"
!define PRODUCT_VERSION "0.3.0"
!define PRODUCT_PUBLISHER "chiraitori"
!define PRODUCT_WEB_SITE "https://github.com/chiraitori/timeflytracking"
!define PRODUCT_DIR_REGKEY "Software\Microsoft\Windows\CurrentVersion\App Paths\TimeFly.App.exe"
!define PRODUCT_UNINST_KEY "Software\Microsoft\Windows\CurrentVersion\Uninstall\${PRODUCT_NAME}"
!define PRODUCT_UNINST_ROOT_KEY "HKLM"

# Custom Branding (replaces default Nullsoft Install System text)
BrandingText "Wet Nilou"

# Installer settings
Name "${PRODUCT_NAME} ${PRODUCT_VERSION}"
OutFile "TimeFly-Setup-x64.exe"
InstallDir "$PROGRAMFILES64\TimeFly"
InstallDirRegKey ${PRODUCT_UNINST_ROOT_KEY} "${PRODUCT_DIR_REGKEY}" ""
ShowInstDetails show
ShowUnInstDetails show
RequestExecutionLevel admin
SetCompressor /SOLID lzma

# Modern UI Configuration
!define MUI_ABORTWARNING
!define MUI_ICON "TimeFly.App\Assets\app_icon.ico"
!define MUI_UNICON "TimeFly.App\Assets\app_icon.ico"

# Custom Artist Artwork Branding for Installer
!define MUI_HEADERIMAGE
!define MUI_HEADERIMAGE_RIGHT
!define MUI_HEADERIMAGE_BITMAP "TimeFly.App\Assets\installer_header.bmp"
!define MUI_HEADERIMAGE_UNBITMAP "TimeFly.App\Assets\installer_header.bmp"
!define MUI_WELCOMEFINISHPAGE_BITMAP "TimeFly.App\Assets\installer_sidebar.bmp"
!define MUI_UNWELCOMEFINISHPAGE_BITMAP "TimeFly.App\Assets\installer_sidebar.bmp"

# Pages
!insertmacro MUI_PAGE_WELCOME
!insertmacro MUI_PAGE_DIRECTORY
!insertmacro MUI_PAGE_INSTFILES

# Finish page
!define MUI_FINISHPAGE_RUN "$INSTDIR\TimeFly.App.exe"
!define MUI_FINISHPAGE_RUN_TEXT "Launch TimeFly"
!insertmacro MUI_PAGE_FINISH

# Uninstaller pages
!insertmacro MUI_UNPAGE_CONFIRM
!insertmacro MUI_UNPAGE_INSTFILES

!insertmacro MUI_LANGUAGE "English"

# Check if TimeFly is running using 100% Native Win32 API (ZERO CMD / ZERO Terminal flash)
Function .onInit
    FindWindow $0 "" "TimeFly"
    IntCmp $0 0 notRunning

    MessageBox MB_YESNO|MB_ICONQUESTION "TimeFly is currently running.$\r$\n$\r$\nWould you like the installer to close it automatically to continue setup?" IDYES closeProcess IDNO abortSetup

    closeProcess:
        # Gracefully send WM_CLOSE (16)
        SendMessage $0 16 0 0
        Sleep 400

        # Terminate process if still running
        System::Call 'user32::GetWindowThreadProcessId(i r0, *i .r1)'
        IntCmp $1 0 doneKill
        System::Call 'kernel32::OpenProcess(i 1, i 0, i r1) i .r2'
        IntCmp $2 0 doneKill
        System::Call 'kernel32::TerminateProcess(i r2, i 0)'
        System::Call 'kernel32::CloseHandle(i r2)'
    doneKill:
        Sleep 300
        Goto notRunning

    abortSetup:
        Abort

    notRunning:
FunctionEnd

Function un.onInit
    FindWindow $0 "" "TimeFly"
    IntCmp $0 0 notRunning

    MessageBox MB_YESNO|MB_ICONQUESTION "TimeFly is currently running.$\r$\n$\r$\nWould you like to close it to proceed with uninstallation?" IDYES closeProcess IDNO abortUninstall

    closeProcess:
        SendMessage $0 16 0 0
        Sleep 400

        System::Call 'user32::GetWindowThreadProcessId(i r0, *i .r1)'
        IntCmp $1 0 doneKill
        System::Call 'kernel32::OpenProcess(i 1, i 0, i r1) i .r2'
        IntCmp $2 0 doneKill
        System::Call 'kernel32::TerminateProcess(i r2, i 0)'
        System::Call 'kernel32::CloseHandle(i r2)'
    doneKill:
        Sleep 300
        Goto notRunning

    abortUninstall:
        Abort

    notRunning:
FunctionEnd

Section "MainSection" SEC01
    SetOutPath "$INSTDIR"
    SetOverwrite on

    # Copy files from publish output
    File /r "publish\TimeFly\*.*"

    # Start Menu Shortcuts
    CreateDirectory "$SMPROGRAMS\TimeFly"
    CreateShortcut "$SMPROGRAMS\TimeFly\TimeFly.lnk" "$INSTDIR\TimeFly.App.exe" "" "$INSTDIR\TimeFly.App.exe" 0
    CreateShortcut "$SMPROGRAMS\TimeFly\Uninstall TimeFly.lnk" "$INSTDIR\uninstall.exe" "" "$INSTDIR\uninstall.exe" 0

    # Desktop Shortcut
    CreateShortcut "$DESKTOP\TimeFly.lnk" "$INSTDIR\TimeFly.App.exe" "" "$INSTDIR\TimeFly.App.exe" 0

    # Write Uninstaller
    WriteUninstaller "$INSTDIR\uninstall.exe"

    # Registry entries for Add/Remove Programs
    WriteRegStr ${PRODUCT_UNINST_ROOT_KEY} "${PRODUCT_UNINST_KEY}" "DisplayName" "${PRODUCT_NAME}"
    WriteRegStr ${PRODUCT_UNINST_ROOT_KEY} "${PRODUCT_UNINST_KEY}" "UninstallString" "$INSTDIR\uninstall.exe"
    WriteRegStr ${PRODUCT_UNINST_ROOT_KEY} "${PRODUCT_UNINST_KEY}" "DisplayIcon" "$INSTDIR\TimeFly.App.exe"
    WriteRegStr ${PRODUCT_UNINST_ROOT_KEY} "${PRODUCT_UNINST_KEY}" "DisplayVersion" "${PRODUCT_VERSION}"
    WriteRegStr ${PRODUCT_UNINST_ROOT_KEY} "${PRODUCT_UNINST_KEY}" "URLInfoAbout" "${PRODUCT_WEB_SITE}"
    WriteRegStr ${PRODUCT_UNINST_ROOT_KEY} "${PRODUCT_UNINST_KEY}" "Publisher" "${PRODUCT_PUBLISHER}"
SectionEnd

Section "Uninstall"
    # Close running instance silently if any
    nsExec::Exec 'taskkill.exe /F /IM TimeFly.App.exe /T'
    Sleep 200

    # Remove Shortcuts
    Delete "$DESKTOP\TimeFly.lnk"
    Delete "$SMPROGRAMS\TimeFly\TimeFly.lnk"
    Delete "$SMPROGRAMS\TimeFly\Uninstall TimeFly.lnk"
    RMDir "$SMPROGRAMS\TimeFly"

    # Remove Installed Files
    RMDir /r "$INSTDIR"

    # Remove Registry Keys
    DeleteRegKey ${PRODUCT_UNINST_ROOT_KEY} "${PRODUCT_UNINST_KEY}"
    DeleteRegKey ${PRODUCT_UNINST_ROOT_KEY} "${PRODUCT_DIR_REGKEY}"
    SetAutoClose true
SectionEnd
