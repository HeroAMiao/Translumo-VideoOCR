@echo off

setlocal EnableDelayedExpansion

set "componentsPath=%~dp0ext_components"

if NOT exist %componentsPath% (
	powershell Invoke-WebRequest https://github.com/Danily07/Translumo/releases/download/v.0.8.5/_Components-v.0.8.0.zip -OutFile "%~dp0components.zip"
	powershell Expand-Archive "%~dp0components.zip" -DestinationPath !componentsPath!
	del "%~dp0components.zip"
	
	powershell Invoke-WebRequest https://github.com/ffbinaries/ffbinaries-prebuilt/releases/download/v4.2.1/ffmpeg-4.2.1-win-64.zip -OutFile "%~dp0ffmpeg.zip"
    	powershell Expand-Archive "%~dp0ffmpeg.zip" -DestinationPath !componentsPath!
    	del "%~dp0ffmpeg.zip"
	
	powershell Invoke-WebRequest https://github.com/ffbinaries/ffbinaries-prebuilt/releases/download/v4.2.1/ffprobe-4.2.1-win-64.zip -OutFile "%~dp0ffprobe.zip"
    	powershell Expand-Archive "%~dp0ffprobe.zip" -DestinationPath !componentsPath!
    	del "%~dp0ffprobe.zip"
)

 
ren "%componentsPath%\models\tesseract" tessdata
set "targetPaths[0]=%1python\"
set "targetPaths[1]=%1models\easyocr\"
set "targetPaths[2]=%1models\tessdata\"
set "targetPaths[3]=%1models\prediction\"
set "targetPaths[4]=%1x64\ffmpeg.exe"
set "targetPaths[5]=%1x64\ffprobe.exe"

set "inputBinariesPaths[0]=%componentsPath%\python"
set "inputBinariesPaths[1]=%componentsPath%\models\easyocr"
set "inputBinariesPaths[2]=%componentsPath%\models\tessdata"
set "inputBinariesPaths[3]=%componentsPath%\models\prediction"
set "inputBinariesPaths[4]=%componentsPath%\ffmpeg.exe"
set "inputBinariesPaths[5]=%componentsPath%\ffprobe.exe"

for %%i in (0,1,2,3) do (
	if NOT exist !targetPaths[%%i]! (
		xcopy /s !inputBinariesPaths[%%i]! !targetPaths[%%i]!
	)
)
if NOT exist %1x64\ (
    mkdir %1x64\
)


for %%i in (4,5) do (
    copy !inputBinariesPaths[%%i]! !targetPaths[%%i]!
)