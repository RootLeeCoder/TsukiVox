param(
    [string]$OutputPath = (Join-Path $PSScriptRoot "quest-tablet-ui-sketch.png")
)

Add-Type -AssemblyName System.Drawing.Common

$width = 1800
$height = 1420
$bitmap = [System.Drawing.Bitmap]::new($width, $height)
$graphics = [System.Drawing.Graphics]::FromImage($bitmap)
$graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$graphics.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::ClearTypeGridFit

function Color([string]$hex)
{
    return [System.Drawing.ColorTranslator]::FromHtml($hex)
}

function Brush([string]$hex)
{
    return [System.Drawing.SolidBrush]::new((Color $hex))
}

function Pen([string]$hex, [float]$thickness = 1)
{
    $pen = [System.Drawing.Pen]::new((Color $hex), $thickness)
    $pen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
    $pen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
    $pen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
    return $pen
}

function Font([float]$size, [System.Drawing.FontStyle]$style = [System.Drawing.FontStyle]::Regular)
{
    return [System.Drawing.Font]::new("Microsoft YaHei UI", $size, $style, [System.Drawing.GraphicsUnit]::Pixel)
}

function RoundedPath([float]$x, [float]$y, [float]$w, [float]$h, [float]$radius)
{
    $path = [System.Drawing.Drawing2D.GraphicsPath]::new()
    $diameter = $radius * 2
    $path.AddArc($x, $y, $diameter, $diameter, 180, 90)
    $path.AddArc($x + $w - $diameter, $y, $diameter, $diameter, 270, 90)
    $path.AddArc($x + $w - $diameter, $y + $h - $diameter, $diameter, $diameter, 0, 90)
    $path.AddArc($x, $y + $h - $diameter, $diameter, $diameter, 90, 90)
    $path.CloseFigure()
    return $path
}

function FillRounded([float]$x, [float]$y, [float]$w, [float]$h, [float]$radius, [string]$hex)
{
    $path = RoundedPath $x $y $w $h $radius
    $brush = Brush $hex
    $graphics.FillPath($brush, $path)
    $brush.Dispose()
    $path.Dispose()
}

function StrokeRounded([float]$x, [float]$y, [float]$w, [float]$h, [float]$radius, [string]$hex, [float]$thickness = 1)
{
    $path = RoundedPath $x $y $w $h $radius
    $pen = Pen $hex $thickness
    $graphics.DrawPath($pen, $path)
    $pen.Dispose()
    $path.Dispose()
}

function DrawText(
    [string]$text,
    [float]$x,
    [float]$y,
    [float]$w,
    [float]$h,
    [float]$size,
    [string]$hex,
    [System.Drawing.FontStyle]$style = [System.Drawing.FontStyle]::Regular,
    [System.Drawing.StringAlignment]$horizontal = [System.Drawing.StringAlignment]::Near,
    [System.Drawing.StringAlignment]$vertical = [System.Drawing.StringAlignment]::Near)
{
    $font = Font $size $style
    $brush = Brush $hex
    $format = [System.Drawing.StringFormat]::new()
    $format.Alignment = $horizontal
    $format.LineAlignment = $vertical
    $format.Trimming = [System.Drawing.StringTrimming]::EllipsisCharacter
    $format.FormatFlags = [System.Drawing.StringFormatFlags]::NoWrap
    $graphics.DrawString($text, $font, $brush, [System.Drawing.RectangleF]::new($x, $y, $w, $h), $format)
    $format.Dispose()
    $brush.Dispose()
    $font.Dispose()
}

function DrawIcon([string]$name, [float]$cx, [float]$cy, [float]$size, [string]$hex, [float]$thickness = 3)
{
    $pen = Pen $hex $thickness
    $left = $cx - $size * 0.5
    $top = $cy - $size * 0.5
    $right = $cx + $size * 0.5
    $bottom = $cy + $size * 0.5

    switch ($name)
    {
        "replay"
        {
            $graphics.DrawArc($pen, $left + 2, $top + 3, $size - 6, $size - 6, -60, 295)
            $graphics.DrawLines($pen, [System.Drawing.PointF[]]@(
                [System.Drawing.PointF]::new($left + 2, $top + $size * 0.42),
                [System.Drawing.PointF]::new($left + 2, $top + 3),
                [System.Drawing.PointF]::new($left + $size * 0.38, $top + 6)))
        }
        "skip-back"
        {
            $graphics.DrawLine($pen, $left + 3, $top + 3, $left + 3, $bottom - 3)
            $graphics.DrawPolygon($pen, [System.Drawing.PointF[]]@(
                [System.Drawing.PointF]::new($right - 3, $top + 4),
                [System.Drawing.PointF]::new($left + 8, $cy),
                [System.Drawing.PointF]::new($right - 3, $bottom - 4)))
        }
        "pause"
        {
            $graphics.DrawLine($pen, $cx - $size * 0.22, $top + 2, $cx - $size * 0.22, $bottom - 2)
            $graphics.DrawLine($pen, $cx + $size * 0.22, $top + 2, $cx + $size * 0.22, $bottom - 2)
        }
        "skip-forward"
        {
            $graphics.DrawPolygon($pen, [System.Drawing.PointF[]]@(
                [System.Drawing.PointF]::new($left + 3, $top + 4),
                [System.Drawing.PointF]::new($right - 8, $cy),
                [System.Drawing.PointF]::new($left + 3, $bottom - 4)))
            $graphics.DrawLine($pen, $right - 3, $top + 3, $right - 3, $bottom - 3)
        }
        "mic"
        {
            $graphics.DrawArc($pen, $cx - $size * 0.18, $top + 1, $size * 0.36, $size * 0.55, 180, 180)
            $graphics.DrawLine($pen, $cx - $size * 0.18, $top + $size * 0.28, $cx - $size * 0.18, $top + $size * 0.5)
            $graphics.DrawLine($pen, $cx + $size * 0.18, $top + $size * 0.28, $cx + $size * 0.18, $top + $size * 0.5)
            $graphics.DrawArc($pen, $cx - $size * 0.32, $top + $size * 0.36, $size * 0.64, $size * 0.46, 0, 180)
            $graphics.DrawLine($pen, $cx, $top + $size * 0.82, $cx, $bottom - 1)
            $graphics.DrawLine($pen, $cx - $size * 0.22, $bottom - 1, $cx + $size * 0.22, $bottom - 1)
        }
        "queue"
        {
            for ($index = 0; $index -lt 3; $index += 1)
            {
                $lineY = $top + 5 + $index * $size * 0.27
                $graphics.DrawLine($pen, $left + 2, $lineY, $right - 7, $lineY)
            }
            $graphics.DrawLine($pen, $right - 3, $top + 3, $right - 3, $bottom - 5)
            $graphics.DrawEllipse($pen, $right - 9, $bottom - 9, 9, 7)
        }
        "settings"
        {
            $graphics.DrawEllipse($pen, $cx - $size * 0.32, $cy - $size * 0.32, $size * 0.64, $size * 0.64)
            $graphics.DrawEllipse($pen, $cx - $size * 0.10, $cy - $size * 0.10, $size * 0.20, $size * 0.20)
            for ($index = 0; $index -lt 8; $index += 1)
            {
                $angle = $index * [Math]::PI / 4
                $x1 = $cx + [Math]::Cos($angle) * $size * 0.34
                $y1 = $cy + [Math]::Sin($angle) * $size * 0.34
                $x2 = $cx + [Math]::Cos($angle) * $size * 0.47
                $y2 = $cy + [Math]::Sin($angle) * $size * 0.47
                $graphics.DrawLine($pen, $x1, $y1, $x2, $y2)
            }
        }
        "back"
        {
            $graphics.DrawLines($pen, [System.Drawing.PointF[]]@(
                [System.Drawing.PointF]::new($right - 4, $top + 2),
                [System.Drawing.PointF]::new($left + 4, $cy),
                [System.Drawing.PointF]::new($right - 4, $bottom - 2)))
        }
        "chevron"
        {
            $graphics.DrawLines($pen, [System.Drawing.PointF[]]@(
                [System.Drawing.PointF]::new($left + 5, $top + 3),
                [System.Drawing.PointF]::new($right - 4, $cy),
                [System.Drawing.PointF]::new($left + 5, $bottom - 3)))
        }
        "copy"
        {
            $graphics.DrawRectangle($pen, $left + 7, $top + 7, $size - 8, $size - 8)
            $graphics.DrawRectangle($pen, $left + 1, $top + 1, $size - 8, $size - 8)
        }
        "close"
        {
            $graphics.DrawLine($pen, $left + 3, $top + 3, $right - 3, $bottom - 3)
            $graphics.DrawLine($pen, $right - 3, $top + 3, $left + 3, $bottom - 3)
        }
        "volume"
        {
            $graphics.DrawPolygon($pen, [System.Drawing.PointF[]]@(
                [System.Drawing.PointF]::new($left + 2, $cy - $size * 0.15),
                [System.Drawing.PointF]::new($left + $size * 0.30, $cy - $size * 0.15),
                [System.Drawing.PointF]::new($cx, $top + 3),
                [System.Drawing.PointF]::new($cx, $bottom - 3),
                [System.Drawing.PointF]::new($left + $size * 0.30, $cy + $size * 0.15),
                [System.Drawing.PointF]::new($left + 2, $cy + $size * 0.15)))
            $graphics.DrawArc($pen, $cx + 1, $top + $size * 0.20, $size * 0.38, $size * 0.60, -60, 120)
        }
    }

    $pen.Dispose()
}

function DrawIconButton([string]$name, [float]$x, [float]$y, [float]$size, [string]$background, [string]$border, [string]$foreground)
{
    FillRounded $x $y $size $size 7 $background
    StrokeRounded $x $y $size $size 7 $border 1.4
    DrawIcon $name ($x + $size * 0.5) ($y + $size * 0.5) ($size * 0.38) $foreground 2.8
}

function DrawToggle([float]$x, [float]$y, [bool]$enabled, [string]$activeHex = "#F2B56B")
{
    $background = if ($enabled) { "#3A2A1B" } else { "#1C2524" }
    $border = if ($enabled) { $activeHex } else { "#33403F" }
    FillRounded $x $y 58 30 15 $background
    StrokeRounded $x $y 58 30 15 $border 1.2
    $knobX = if ($enabled) { $x + 31 } else { $x + 5 }
    $brush = Brush $(if ($enabled) { $activeHex } else { "#75827F" })
    $graphics.FillEllipse($brush, $knobX, $y + 4, 22, 22)
    $brush.Dispose()
}

$pageBrush = Brush "#080B0B"
$graphics.FillRectangle($pageBrush, 0, 0, $width, $height)
$pageBrush.Dispose()

# Board header
DrawText "TsukiVox Quest · 沉浸式茶几 UI" 64 40 700 28 15 "#3DE5BD" ([System.Drawing.FontStyle]::Bold)
DrawText "居中点歌平板改版草图" 64 70 720 50 34 "#F2F6F5" ([System.Drawing.FontStyle]::Bold)
DrawText "参考 VRSing WebXR 沉浸模式；默认只呈现唱歌所需信息，工程诊断按需打开。" 990 58 740 56 16 "#8E9C99" ([System.Drawing.FontStyle]::Regular) ([System.Drawing.StringAlignment]::Far) ([System.Drawing.StringAlignment]::Center)

# Main scene heading
DrawText "01 · 空间摆位与默认页" 64 136 520 32 19 "#F2F6F5" ([System.Drawing.FontStyle]::Bold)
DrawText "杯子全部移除 · 平板、Canvas 与茶几中心线统一为 x = 0" 1040 136 690 32 14 "#8E9C99" ([System.Drawing.FontStyle]::Regular) ([System.Drawing.StringAlignment]::Far)

# Main scene background
$sceneBrush = Brush "#070909"
$graphics.FillRectangle($sceneBrush, 64, 178, 1666, 610)
$sceneBrush.Dispose()
$linePen = Pen "#263332" 1
$graphics.DrawLine($linePen, 64, 178, 1730, 178)
$graphics.DrawLine($linePen, 64, 788, 1730, 788)
$linePen.Dispose()

# Screen and room surround
$columnBrush = Brush "#1B0D12"
$graphics.FillRectangle($columnBrush, 500, 205, 70, 230)
$graphics.FillRectangle($columnBrush, 1230, 205, 70, 230)
$columnBrush.Dispose()
$frameBrush = Brush "#040606"
$graphics.FillRectangle($frameBrush, 605, 205, 590, 225)
$frameBrush.Dispose()
$screenBrush = Brush "#152427"
$graphics.FillRectangle($screenBrush, 624, 224, 552, 183)
$screenBrush.Dispose()
$brassPen = Pen "#B77A3F" 5
$graphics.DrawLine($brassPen, 570, 435, 1230, 435)
$brassPen.Dispose()

# Coffee table, deliberately empty except for centered tablet
$tableBrush = Brush "#0B1A1A"
$graphics.FillPolygon($tableBrush, [System.Drawing.PointF[]]@(
    [System.Drawing.PointF]::new(230, 418),
    [System.Drawing.PointF]::new(1570, 418),
    [System.Drawing.PointF]::new(1770, 788),
    [System.Drawing.PointF]::new(30, 788)))
$tableBrush.Dispose()
$tablePen = Pen "#B77A3F" 7
$graphics.DrawLines($tablePen, [System.Drawing.PointF[]]@(
    [System.Drawing.PointF]::new(230, 418),
    [System.Drawing.PointF]::new(1570, 418),
    [System.Drawing.PointF]::new(1770, 788)))
$graphics.DrawLine($tablePen, 30, 788, 230, 418)
$tablePen.Dispose()

$axisPen = Pen "#234D45" 1.5
$axisPen.DashStyle = [System.Drawing.Drawing2D.DashStyle]::Dash
$graphics.DrawLine($axisPen, 900, 182, 900, 785)
$axisPen.Dispose()
DrawText "茶几 / 平板中心线" 912 755 180 22 11 "#4E8C80"

# Physical tablet shell and shadow
FillRounded 291 393 1218 384 10 "#050707"
FillRounded 300 378 1200 382 9 "#171F1F"
StrokeRounded 300 378 1200 382 9 "#3A4745" 2
FillRounded 326 402 1148 332 5 "#0B1111"
StrokeRounded 326 402 1148 332 5 "#31413F" 1.3

# Tablet header
$dividerPen = Pen "#1B2524" 1
$graphics.DrawLine($dividerPen, 326, 458, 1474, 458)
$graphics.DrawLine($dividerPen, 326, 612, 1474, 612)
$graphics.DrawLine($dividerPen, 326, 686, 1474, 686)
$dividerPen.Dispose()

$moonPen = Pen "#8FFFE4" 1.8
$graphics.DrawEllipse($moonPen, 352, 417, 30, 30)
$graphics.DrawArc($moonPen, 360, 421, 18, 20, 65, 210)
$moonPen.Dispose()
DrawText "TsukiVox" 393 414 180 24 18 "#F2F6F5" ([System.Drawing.FontStyle]::Bold)
DrawText "月读声域" 393 438 180 18 11 "#8E9C99"

$dotBrush = Brush "#3DE5BD"
$graphics.FillEllipse($dotBrush, 1120, 427, 8, 8)
$dotBrush.Dispose()
DrawText "点歌服务已连接" 1136 417 155 32 13 "#B9C4C2" ([System.Drawing.FontStyle]::Regular) ([System.Drawing.StringAlignment]::Near) ([System.Drawing.StringAlignment]::Center)
DrawIconButton "queue" 1308 410 40 "#0B1111" "#263332" "#F2F6F5"
DrawIconButton "settings" 1362 410 40 "#0B1111" "#263332" "#F2F6F5"
FillRounded 1337 402 18 18 9 "#3DE5BD"
DrawText "3" 1337 402 18 18 10 "#03130F" ([System.Drawing.FontStyle]::Bold) ([System.Drawing.StringAlignment]::Center) ([System.Drawing.StringAlignment]::Center)

# Now playing
DrawText "正在播放 · 第 2 / 5 首" 360 478 480 26 13 "#3DE5BD" ([System.Drawing.FontStyle]::Bold)
DrawText "春日影" 360 507 640 58 38 "#F2F6F5" ([System.Drawing.FontStyle]::Bold)
DrawText "Bilibili · 播放中" 360 565 520 24 14 "#8E9C99"
$waveBrush = Brush "#3DE5BD"
$barHeights = @(18, 42, 64, 31, 52, 24, 39, 15)
for ($index = 0; $index -lt $barHeights.Count; $index += 1)
{
    $barHeight = $barHeights[$index]
    $graphics.FillRectangle($waveBrush, 1190 + $index * 18, 535 - $barHeight * 0.5, 6, $barHeight)
}
$waveBrush.Dispose()

# Five immersive controls, centered like VRSing
$buttonSize = 68
$buttonGap = 20
$buttonStart = 700
DrawIconButton "replay" $buttonStart 615 $buttonSize "#0C1313" "#32524C" "#F2F6F5"
DrawIconButton "skip-back" ($buttonStart + 88) 615 $buttonSize "#8FFFE4" "#8FFFE4" "#03130F"
DrawIconButton "pause" ($buttonStart + 176) 608 82 "#3DE5BD" "#3DE5BD" "#03130F"
DrawIconButton "skip-forward" ($buttonStart + 278) 615 $buttonSize "#0C1313" "#32524C" "#F2F6F5"
DrawIconButton "mic" ($buttonStart + 366) 615 $buttonSize "#3A2A1B" "#A97848" "#FFD6A1"
DrawText "上一首" ($buttonStart + 80) 688 84 22 11 "#8FFFE4" ([System.Drawing.FontStyle]::Bold) ([System.Drawing.StringAlignment]::Center)

# Voice summary footer
$warmBrush = Brush "#F2B56B"
$graphics.FillEllipse($warmBrush, 360, 711, 9, 9)
$warmBrush.Dispose()
DrawText "麦克风已开启" 382 699 170 30 14 "#F2F6F5" ([System.Drawing.FontStyle]::Bold) ([System.Drawing.StringAlignment]::Near) ([System.Drawing.StringAlignment]::Center)
DrawText "人声 · KTV" 536 699 140 30 13 "#8E9C99" ([System.Drawing.FontStyle]::Regular) ([System.Drawing.StringAlignment]::Near) ([System.Drawing.StringAlignment]::Center)
DrawText "调整人声" 1290 699 110 30 13 "#BCC8C5" ([System.Drawing.FontStyle]::Regular) ([System.Drawing.StringAlignment]::Far) ([System.Drawing.StringAlignment]::Center)
DrawIcon "chevron" 1414 714 14 "#BCC8C5" 2

# Main callouts
FillRounded 92 686 190 68 2 "#0B1111"
$accentPen = Pen "#3DE5BD" 3
$graphics.DrawLine($accentPen, 92, 686, 92, 754)
$accentPen.Dispose()
DrawText "同一中心锚点" 107 696 160 22 13 "#F2F6F5" ([System.Drawing.FontStyle]::Bold)
DrawText "机身与 UI 不再错位" 107 721 160 22 11 "#A8B5B2"

FillRounded 1518 686 184 68 2 "#0B1111"
$warmPen = Pen "#F2B56B" 3
$graphics.DrawLine($warmPen, 1518, 686, 1518, 754)
$warmPen.Dispose()
DrawText "清空茶几表面" 1533 696 154 22 13 "#F2F6F5" ([System.Drawing.FontStyle]::Bold)
DrawText "移除两只杯子" 1533 721 154 22 11 "#A8B5B2"

# Secondary states heading
DrawText "02 · 次级页面与隐藏诊断" 64 828 600 34 19 "#F2F6F5" ([System.Drawing.FontStyle]::Bold)
DrawText "默认页不显示 IP、URL、延迟、后端或缓存信息" 1070 828 660 34 14 "#8E9C99" ([System.Drawing.FontStyle]::Regular) ([System.Drawing.StringAlignment]::Far)

# Voice state panel
DrawText "人声页" 64 874 200 26 15 "#F2F6F5" ([System.Drawing.FontStyle]::Bold)
DrawText '从主页“调整人声”进入' 460 874 360 26 12 "#8E9C99" ([System.Drawing.FontStyle]::Regular) ([System.Drawing.StringAlignment]::Far)
FillRounded 64 906 810 382 8 "#171F1F"
StrokeRounded 64 906 810 382 8 "#364241" 1.5
FillRounded 80 922 778 350 4 "#0B1111"
$dividerPen = Pen "#1B2524" 1
$graphics.DrawLine($dividerPen, 80, 984, 858, 984)
$dividerPen.Dispose()
DrawIconButton "back" 98 934 38 "#0B1111" "#263332" "#F2F6F5"
DrawText "人声" 152 934 180 38 18 "#F2F6F5" ([System.Drawing.FontStyle]::Bold) ([System.Drawing.StringAlignment]::Near) ([System.Drawing.StringAlignment]::Center)

# Voice rows
DrawText "麦克风" 116 1010 150 26 14 "#F2F6F5" ([System.Drawing.FontStyle]::Bold)
DrawText "开启后可听到实时返听" 116 1036 190 20 11 "#8E9C99"
FillRounded 330 1023 390 8 4 "#222C2B"
FillRounded 330 1023 255 8 4 "#3DE5BD"
DrawToggle 762 1011 $true

DrawText "返听音量" 116 1080 150 26 14 "#F2F6F5" ([System.Drawing.FontStyle]::Bold)
DrawText "建议先低后高" 116 1106 150 20 11 "#8E9C99"
FillRounded 330 1093 390 8 4 "#222C2B"
FillRounded 330 1093 225 8 4 "#3DE5BD"
$knobBrush = Brush "#8FFFE4"
$graphics.FillEllipse($knobBrush, 545, 1087, 20, 20)
$knobBrush.Dispose()
DrawIcon "volume" 790 1097 26 "#8E9C99" 2

DrawText "人声效果" 116 1154 150 26 14 "#F2F6F5" ([System.Drawing.FontStyle]::Bold)
DrawText "四档预设直接选择" 116 1180 170 20 11 "#8E9C99"
FillRounded 330 1152 486 56 7 "#0E1515"
StrokeRounded 330 1152 486 56 7 "#263332" 1
$segmentWidth = 118
$segmentLabels = @("原声", "KTV", "强效", "柔和")
for ($index = 0; $index -lt $segmentLabels.Count; $index += 1)
{
    $segmentX = 336 + $index * $segmentWidth
    if ($index -eq 1)
    {
        FillRounded $segmentX 1158 112 44 4 "#3DE5BD"
        DrawText $segmentLabels[$index] $segmentX 1158 112 44 13 "#03130F" ([System.Drawing.FontStyle]::Bold) ([System.Drawing.StringAlignment]::Center) ([System.Drawing.StringAlignment]::Center)
    }
    else
    {
        DrawText $segmentLabels[$index] $segmentX 1158 112 44 13 "#AEB9B7" ([System.Drawing.FontStyle]::Bold) ([System.Drawing.StringAlignment]::Center) ([System.Drawing.StringAlignment]::Center)
    }
}

# Diagnostics state panel
DrawText "诊断抽屉" 926 874 240 26 15 "#F2F6F5" ([System.Drawing.FontStyle]::Bold)
DrawText "设置 → 诊断与支持；默认关闭" 1325 874 405 26 12 "#8E9C99" ([System.Drawing.FontStyle]::Regular) ([System.Drawing.StringAlignment]::Far)
FillRounded 926 906 804 382 8 "#171F1F"
StrokeRounded 926 906 804 382 8 "#364241" 1.5
FillRounded 942 922 772 350 4 "#07100F"

# Dimmed home behind drawer
FillRounded 970 992 230 12 3 "#172120"
FillRounded 970 1020 318 30 3 "#172120"
FillRounded 970 1068 270 12 3 "#172120"
$scrimBrush = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(132, 0, 0, 0))
$graphics.FillRectangle($scrimBrush, 942, 922, 772, 350)
$scrimBrush.Dispose()

# Drawer
$drawerBrush = Brush "#101717"
$graphics.FillRectangle($drawerBrush, 1190, 922, 524, 350)
$drawerBrush.Dispose()
$drawerPen = Pen "#31403E" 1.5
$graphics.DrawLine($drawerPen, 1190, 922, 1190, 1272)
$drawerPen.Dispose()
DrawText "诊断与支持" 1212 935 260 42 18 "#F2F6F5" ([System.Drawing.FontStyle]::Bold) ([System.Drawing.StringAlignment]::Near) ([System.Drawing.StringAlignment]::Center)
DrawIconButton "close" 1656 934 38 "#101717" "#263332" "#F2F6F5"
$drawerDivider = Pen "#1B2524" 1
$graphics.DrawLine($drawerDivider, 1190, 984, 1714, 984)
$graphics.DrawLine($drawerDivider, 1212, 1052, 1692, 1052)
$graphics.DrawLine($drawerDivider, 1212, 1108, 1692, 1108)
$graphics.DrawLine($drawerDivider, 1212, 1164, 1692, 1164)
$drawerDivider.Dispose()

DrawText "系统状态" 1212 996 130 22 13 "#F2F6F5" ([System.Drawing.FontStyle]::Bold)
DrawText "摘要优先" 1212 1020 130 18 10 "#8E9C99"
$chipLabels = @("应用 正常", "音频 正常", "视频 正常")
for ($index = 0; $index -lt $chipLabels.Count; $index += 1)
{
    $chipX = 1398 + $index * 96
    FillRounded $chipX 1005 88 30 4 "#10201D"
    StrokeRounded $chipX 1005 88 30 4 "#2D4943" 1
    DrawText $chipLabels[$index] $chipX 1005 88 30 10 "#B9D8D1" ([System.Drawing.FontStyle]::Bold) ([System.Drawing.StringAlignment]::Center) ([System.Drawing.StringAlignment]::Center)
}

DrawText "点歌服务" 1212 1064 180 22 13 "#F2F6F5" ([System.Drawing.FontStyle]::Bold)
DrawText "已连接 · 192.168.31.191" 1212 1086 240 18 10 "#8E9C99"
DrawIcon "chevron" 1678 1080 14 "#AEB9B7" 2

DrawText "显示视频调试信息" 1212 1124 240 22 13 "#F2F6F5" ([System.Drawing.FontStyle]::Bold)
DrawText "临时叠加到大屏幕" 1212 1146 240 18 10 "#8E9C99"
DrawToggle 1628 1119 $false "#3DE5BD"

DrawText "原始详情" 1212 1180 180 22 13 "#F2F6F5" ([System.Drawing.FontStyle]::Bold)
DrawText "后端、延迟、URL、缓存与采样信息" 1212 1202 320 18 10 "#8E9C99"
DrawIcon "chevron" 1678 1193 14 "#AEB9B7" 2

FillRounded 1212 1226 480 34 5 "#0B1A17"
StrokeRounded 1212 1226 480 34 5 "#3A7468" 1
DrawIcon "copy" 1366 1243 16 "#8FFFE4" 1.8
DrawText "复制完整诊断信息" 1382 1226 180 34 12 "#8FFFE4" ([System.Drawing.FontStyle]::Bold) ([System.Drawing.StringAlignment]::Center) ([System.Drawing.StringAlignment]::Center)

# Color system legend
$legendY = 1340
$legend = @(
    @("#3DE5BD", "薄荷绿", "播放、连接与射线焦点"),
    @("#F2B56B", "暖黄色", "仅表示麦克风正在工作"),
    @("#202A29", "石墨灰", "机身与普通操作层级"),
    @("#EF7474", "红色", "仅用于真实错误或危险操作")
)
for ($index = 0; $index -lt $legend.Count; $index += 1)
{
    $legendX = 70 + $index * 430
    $swatchBrush = Brush $legend[$index][0]
    $graphics.FillRectangle($swatchBrush, $legendX, $legendY + 7, 14, 14)
    $swatchBrush.Dispose()
    DrawText $legend[$index][1] ($legendX + 26) $legendY 105 28 12 "#F2F6F5" ([System.Drawing.FontStyle]::Bold) ([System.Drawing.StringAlignment]::Near) ([System.Drawing.StringAlignment]::Center)
    DrawText $legend[$index][2] ($legendX + 126) $legendY 265 28 11 "#8E9C99" ([System.Drawing.FontStyle]::Regular) ([System.Drawing.StringAlignment]::Near) ([System.Drawing.StringAlignment]::Center)
}

$graphics.Dispose()
$directory = Split-Path -Parent $OutputPath
if (-not (Test-Path -LiteralPath $directory))
{
    New-Item -ItemType Directory -Path $directory -Force | Out-Null
}

$bitmap.Save($OutputPath, [System.Drawing.Imaging.ImageFormat]::Png)
$bitmap.Dispose()
Write-Output $OutputPath
