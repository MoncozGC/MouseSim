# MouseSim 应用图标生成器
# 设计：圆角方形蓝色渐变底 + 白色简约鼠标（带滚轮槽）+ 白色键盘（键位排布）
param(
    [string]$OutDir = "F:\CodeProject\PersonalSpace\mouse\tools\icon"
)

Add-Type -AssemblyName System.Drawing

$W = 256
$master = New-Object System.Drawing.Bitmap($W, $W)
$g = [System.Drawing.Graphics]::FromImage($master)
$g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias

function New-RoundRect([float]$x, [float]$y, [float]$w, [float]$h, [float]$r) {
    $p = New-Object System.Drawing.Drawing2D.GraphicsPath
    $p.AddArc($x, $y, $r, $r, 180, 90)
    $p.AddArc($x + $w - $r, $y, $r, $r, 270, 90)
    $p.AddArc($x + $w - $r, $y + $h - $r, $r, $r, 0, 90)
    $p.AddArc($x, $y + $h - $r, $r, $r, 90, 90)
    $p.CloseFigure()
    return $p
}

$accent1 = [System.Drawing.Color]::FromArgb(255, 0x5B, 0x9C, 0xF8)   # 亮蓝
$accent2 = [System.Drawing.Color]::FromArgb(255, 0x2B, 0x62, 0xC4)   # 深蓝
$white = [System.Drawing.Brushes]::White
$blue = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 0x2E, 0x6F, 0xDB))

# 1) 背景圆角方形 + 对角渐变
$bgPath = New-RoundRect 0 0 $W $W 58
$rect = New-Object System.Drawing.Rectangle(0, 0, $W, $W)
$grad = New-Object System.Drawing.Drawing2D.LinearGradientBrush($rect, $accent1, $accent2, 45.0)
$g.FillPath($grad, $bgPath)

# 2) 键盘（白色圆角横条 + 蓝色键位）
$kbPath = New-RoundRect 44 142 168 78 18
$g.FillPath($white, $kbPath)
# 第一排：4 个键
foreach ($kx in 58, 98, 138, 178) {
    $g.FillPath($blue, (New-RoundRect $kx 156 22 16 5))
}
# 第二排：左键 + 空格条 + 右键
$g.FillPath($blue, (New-RoundRect 58 182 26 22 5))
$g.FillPath($blue, (New-RoundRect 92 182 72 22 8))
$g.FillPath($blue, (New-RoundRect 172 182 26 22 5))

# 3) 鼠标（白色胶囊 + 蓝色滚轮槽 + 左右键分界线）
$msPath = New-RoundRect 95 28 66 96 33
$g.FillPath($white, $msPath)
# 滚轮槽
$slot = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 0x1F, 0x4E, 0xA8))
$g.FillPath($slot, (New-RoundRect 124 38 8 24 4))
# 左右键分界线（从顶部到 1/3 处）
$g.FillPath($slot, (New-RoundRect 127 28 5 28 2))

$g.Dispose()

# 导出 256 预览 PNG
$preview = Join-Path $OutDir "icon-preview.png"
$master.Save($preview, [System.Drawing.Imaging.ImageFormat]::Png)

# 生成多尺寸 ICO（PNG 压缩条目，Vista+ 全兼容）
$sizes = 16, 32, 48, 64, 128, 256
$pngBytes = @{}
foreach ($s in $sizes) {
    $bmp = New-Object System.Drawing.Bitmap($s, $s)
    $gg = [System.Drawing.Graphics]::FromImage($bmp)
    $gg.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $gg.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $gg.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $gg.DrawImage($master, 0, 0, $s, $s)
    $gg.Dispose()
    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    $pngBytes[$s] = $ms.ToArray()
}

$ico = New-Object System.IO.MemoryStream
$bw = New-Object System.IO.BinaryWriter($ico)
$bw.Write([UInt16]0); $bw.Write([UInt16]1); $bw.Write([UInt16]$sizes.Count)
$offset = 6 + 16 * $sizes.Count
foreach ($s in $sizes) {
    $dim = if ($s -ge 256) { 0 } else { $s }
    $bw.Write([byte]$dim); $bw.Write([byte]$dim)
    $bw.Write([byte]0); $bw.Write([byte]0)
    $bw.Write([UInt16]1); $bw.Write([UInt16]32)
    $bw.Write([UInt32]$pngBytes[$s].Length)
    $bw.Write([UInt32]$offset)
    $offset += $pngBytes[$s].Length
}
foreach ($s in $sizes) { $bw.Write($pngBytes[$s]) }
$bw.Flush()

$outIco = Join-Path $OutDir "app.ico"
[System.IO.File]::WriteAllBytes($outIco, $ico.ToArray())
Write-Host "OK: $preview"
Write-Host "OK: $outIco ($($ico.Length) bytes)"
