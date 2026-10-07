Add-Type -AssemblyName System.Drawing
$bmp = New-Object System.Drawing.Bitmap 'C:\Users\sahib\.gemini\antigravity\brain\9aac1139-86e3-448a-859e-99116fd3a7f9\kanbancards_logo_1791380531696.jpg'
$bmp.MakeTransparent([System.Drawing.Color]::White)
$icoStream = New-Object System.IO.FileStream('C:\VS Code\KanbanCards\AppIcon.ico', [System.IO.FileMode]::Create)
$icon = [System.Drawing.Icon]::FromHandle($bmp.GetHicon())
$icon.Save($icoStream)
$icoStream.Close()
$bmp.Dispose()
