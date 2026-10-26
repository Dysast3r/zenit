# Genera el ícono de la app y las imágenes del repositorio con el motor de WPF.
#   src/Zenit/Assets/icon.ico   multi-resolución 16–256 (cada tamaño dibujado a su escala)
#   src/Zenit/Assets/logo.png   256 px para mostrar dentro de la app
#   docs/icon.png                      1024 px
#   docs/banner.png                    1280×640, vista previa social de GitHub
# Identidad: la "Z" con el vértice del cénit en degradado cian → azul sobre azul marino.
# Uso: powershell -ExecutionPolicy Bypass -File tools/generate-icon.ps1
Add-Type -AssemblyName PresentationCore, PresentationFramework, WindowsBase

$root = Split-Path $PSScriptRoot -Parent
$assets = Join-Path $root 'src\Zenit\Assets'
$docs = Join-Path $root 'docs'
New-Item -ItemType Directory -Force -Path $assets, $docs | Out-Null

function C([string]$hex) { [System.Windows.Media.ColorConverter]::ConvertFromString($hex) }

function Radial([string]$hex, [double]$cx, [double]$cy, [double]$r) {
    $b = New-Object System.Windows.Media.RadialGradientBrush
    $b.Center = New-Object System.Windows.Point $cx, $cy
    $b.GradientOrigin = $b.Center
    $b.RadiusX = $r; $b.RadiusY = $r
    $b.GradientStops.Add((New-Object System.Windows.Media.GradientStop (C $hex), 0))
    $transparent = C $hex; $transparent.A = 0
    $b.GradientStops.Add((New-Object System.Windows.Media.GradientStop $transparent, 1))
    $b
}

function Linear([string[]]$colors, [double]$angle = 90) {
    $b = New-Object System.Windows.Media.LinearGradientBrush
    $rad = $angle * [Math]::PI / 180
    $b.StartPoint = New-Object System.Windows.Point (0.5 - [Math]::Cos($rad) / 2), (0.5 - [Math]::Sin($rad) / 2)
    $b.EndPoint = New-Object System.Windows.Point (0.5 + [Math]::Cos($rad) / 2), (0.5 + [Math]::Sin($rad) / 2)
    for ($i = 0; $i -lt $colors.Count; $i++) {
        $b.GradientStops.Add((New-Object System.Windows.Media.GradientStop (C $colors[$i]), ($i / [Math]::Max(1, $colors.Count - 1))))
    }
    $b
}

# Superelipse (forma "squircle" de los íconos de iOS)
function Squircle([double]$x, [double]$y, [double]$w, [double]$h, [double]$n = 5) {
    $points = New-Object System.Windows.Media.PointCollection
    for ($i = 0; $i -lt 720; $i++) {
        $t = $i / 720 * 2 * [Math]::PI
        $c = [Math]::Cos($t); $s = [Math]::Sin($t)
        $px = [Math]::Sign($c) * [Math]::Pow([Math]::Abs($c), 2 / $n)
        $py = [Math]::Sign($s) * [Math]::Pow([Math]::Abs($s), 2 / $n)
        $points.Add((New-Object System.Windows.Point ($x + $w / 2 + $px * $w / 2), ($y + $h / 2 + $py * $h / 2)))
    }
    $figure = New-Object System.Windows.Media.PathFigure
    $figure.StartPoint = $points[0]
    $figure.Segments.Add((New-Object System.Windows.Media.PolyLineSegment $points, $true))
    $figure.IsClosed = $true
    $geometry = New-Object System.Windows.Media.PathGeometry
    $geometry.Figures.Add($figure)
    $geometry
}

function Box($parent, [double]$x, [double]$y, [double]$w, [double]$h, [double]$radius, $brush) {
    $b = New-Object System.Windows.Controls.Border
    $b.Width = $w; $b.Height = $h
    $b.CornerRadius = New-Object System.Windows.CornerRadius $radius
    $b.Background = $brush
    [System.Windows.Controls.Canvas]::SetLeft($b, $x)
    [System.Windows.Controls.Canvas]::SetTop($b, $y)
    [void]$parent.Children.Add($b)
    $b
}

function Shape($parent, [string]$data, $brush) {
    $p = New-Object System.Windows.Shapes.Path
    $p.Data = [System.Windows.Media.Geometry]::Parse($data)
    $p.Fill = $brush
    [void]$parent.Children.Add($p)
    $p
}

# El símbolo en un lienzo de 100 × 98 unidades: vértice, barra superior, diagonal y barra inferior.
# Se dibuja la diagonal primero para que las dos barras queden por encima, como en la marca.
$MarkW = 100; $MarkH = 98
function New-Mark([double]$size) {
    $mark = New-Object System.Windows.Controls.Canvas
    $mark.Width = $MarkW; $mark.Height = $MarkH
    $mark.LayoutTransform = New-Object System.Windows.Media.ScaleTransform ($size / $MarkW), ($size / $MarkW)

    [void](Shape $mark 'M 53.2,41.9 L 83.4,41.9 L 30.2,98 L 0,98 Z' (Linear @('#0B5BF2', '#1279FF', '#1E8EFF') 120))
    $bottom = Shape $mark 'M 47.4,79.6 L 80.2,79.6 Q 83.6,79.6 85.9,82.2 L 100,98 L 30.2,98 Z' (Linear @('#0846D9', '#0F7BFF', '#1FE4FF') 0)
    $shade = New-Object System.Windows.Media.Effects.DropShadowEffect
    $shade.Color = C '#020818'; $shade.BlurRadius = 6; $shade.ShadowDepth = 1.5; $shade.Direction = 160; $shade.Opacity = 0.45
    $bottom.Effect = $shade
    [void](Shape $mark 'M 6.8,33.9 L 73.4,33.9 Q 77.4,33.9 79.8,36.8 L 83.4,41.9 L 77.5,48.1 L 22.2,48.1 Q 18.6,48.1 16.4,45.4 Z' (Linear @('#24F2FF', '#16B4FF', '#0B6FFF') 0))
    [void](Shape $mark 'M 54.6,0 L 72.9,29.7 Q 47,9 32.7,29.7 Z' (Linear @('#2BF0FF', '#12A6FF', '#0A6CFF') 70))
    $mark
}

# Dibuja el ícono en un Canvas de S×S. $detail: full (≥48 px), medium (32 px) o tiny (≤24 px).
function New-IconCanvas([double]$S, [string]$detail) {
    $canvas = New-Object System.Windows.Controls.Canvas
    $canvas.Width = $S; $canvas.Height = $S

    $inset = if ($detail -eq 'tiny') { 0 } else { $S * 0.03 }
    $shape = Squircle $inset $inset ($S - 2 * $inset) ($S - 2 * $inset)

    # Fondo: azul marino profundo con un halo azul detrás del símbolo
    $bg = New-Object System.Windows.Controls.Grid
    $bg.Width = $S; $bg.Height = $S
    $bg.Clip = $shape
    foreach ($brush in @(
            (Linear @('#0C1B36', '#060C1C', '#03060F') 90),
            (Radial '#552B7BFF' 0.5 0.55 0.6),
            (Radial '#2222D3EE' 0.15 0.0 0.6),
            (Linear @('#12FFFFFF', '#00FFFFFF', '#00FFFFFF') 90))) {
        $r = New-Object System.Windows.Shapes.Rectangle
        $r.Fill = $brush
        [void]$bg.Children.Add($r)
    }
    [void]$canvas.Children.Add($bg)

    $rim = New-Object System.Windows.Shapes.Path
    $rim.Data = $shape
    $rim.Stroke = Linear @('#5538BDF8', '#1A2563EB') 90
    $rim.StrokeThickness = [Math]::Max(0.6, $S * 0.008)
    [void]$canvas.Children.Add($rim)

    # A menor tamaño el símbolo ocupa más superficie para seguir legible
    $ms = $S * $(switch ($detail) { 'tiny' { 0.74 } 'medium' { 0.66 } default { 0.58 } })
    $mx = ($S - $ms) / 2
    $my = ($S - $ms * $MarkH / $MarkW) / 2

    if ($detail -ne 'tiny') {
        # Resplandor azul del símbolo, como en la presentación de marca
        $glow = New-Mark $ms
        $blur = New-Object System.Windows.Media.Effects.BlurEffect
        $blur.Radius = $S * 0.07
        $glow.Effect = $blur
        $glow.Opacity = 0.55
        [System.Windows.Controls.Canvas]::SetLeft($glow, $mx); [System.Windows.Controls.Canvas]::SetTop($glow, $my)
        [void]$canvas.Children.Add($glow)
    }

    $mark = New-Mark $ms
    [System.Windows.Controls.Canvas]::SetLeft($mark, $mx); [System.Windows.Controls.Canvas]::SetTop($mark, $my)
    [void]$canvas.Children.Add($mark)

    $canvas
}

# Wordmark ZENIT geométrico: la E son tres barras sin asta, como en la marca.
function New-Wordmark([double]$H, $brush) {
    $t = $H * 0.17; $w = $H * 0.92; $gap = $H * 0.42; $d = $t * 1.35
    $letters = @(
        @("M 0,0 H $w V $t H 0 Z", "M 0,$($H - $t) H $w V $H H 0 Z", "M $($w - $d),$t L $w,$t L $d,$($H - $t) L 0,$($H - $t) Z"),
        @("M 0,0 H $w V $t H 0 Z", "M 0,$(($H - $t) / 2) H $w V $(($H + $t) / 2) H 0 Z", "M 0,$($H - $t) H $w V $H H 0 Z"),
        @("M 0,0 H $t V $H H 0 Z", "M $($w - $t),0 H $w V $H H $($w - $t) Z", "M 0,0 L $d,0 L $w,$H L $($w - $d),$H Z"),
        @("M 0,0 H $t V $H H 0 Z"),
        @("M 0,0 H $w V $t H 0 Z", "M $(($w - $t) / 2),0 H $(($w + $t) / 2) V $H H $(($w - $t) / 2) Z")
    )
    $widths = @($w, $w, $w, $t, $w)
    $word = New-Object System.Windows.Controls.Canvas
    $x = 0
    for ($i = 0; $i -lt $letters.Count; $i++) {
        $letter = New-Object System.Windows.Controls.Canvas
        foreach ($data in $letters[$i]) { [void](Shape $letter $data $brush) }
        [System.Windows.Controls.Canvas]::SetLeft($letter, $x)
        [void]$word.Children.Add($letter)
        $x += $widths[$i] + $gap
    }
    $word.Width = $x - $gap; $word.Height = $H
    $word
}

function Render($element, [int]$w, [int]$h) {
    $size = New-Object System.Windows.Size $w, $h
    $element.Measure($size)
    $element.Arrange((New-Object System.Windows.Rect $size))
    $element.UpdateLayout()
    $bitmap = New-Object System.Windows.Media.Imaging.RenderTargetBitmap $w, $h, 96, 96, ([System.Windows.Media.PixelFormats]::Pbgra32)
    $bitmap.Render($element)
    $bitmap
}

function To-Png($bitmap) {
    $encoder = New-Object System.Windows.Media.Imaging.PngBitmapEncoder
    $encoder.Frames.Add([System.Windows.Media.Imaging.BitmapFrame]::Create($bitmap))
    $ms = New-Object System.IO.MemoryStream
    $encoder.Save($ms)
    , $ms.ToArray()
}

function Save-Png($bitmap, [string]$path) { [System.IO.File]::WriteAllBytes($path, (To-Png $bitmap)) }

# ── Ícono .ico ──
$sizes = 16, 20, 24, 32, 40, 48, 64, 96, 128, 256
$images = foreach ($s in $sizes) {
    $detail = if ($s -le 24) { 'tiny' } elseif ($s -le 40) { 'medium' } else { 'full' }
    , (To-Png (Render (New-IconCanvas $s $detail) $s $s))
}
$icoPath = Join-Path $assets 'icon.ico'
$fs = [System.IO.File]::Create($icoPath)
$w = New-Object System.IO.BinaryWriter $fs
$w.Write([uint16]0); $w.Write([uint16]1); $w.Write([uint16]$sizes.Count)
$offset = 6 + 16 * $sizes.Count
for ($i = 0; $i -lt $sizes.Count; $i++) {
    $dim = if ($sizes[$i] -ge 256) { 0 } else { $sizes[$i] }
    $w.Write([byte]$dim); $w.Write([byte]$dim); $w.Write([byte]0); $w.Write([byte]0)
    $w.Write([uint16]1); $w.Write([uint16]32)
    $w.Write([uint32]$images[$i].Length); $w.Write([uint32]$offset)
    $offset += $images[$i].Length
}
foreach ($img in $images) { $w.Write($img) }
$w.Close()

Save-Png (Render (New-IconCanvas 256 'full') 256 256) (Join-Path $assets 'logo.png')
Save-Png (Render (New-IconCanvas 1024 'full') 1024 1024) (Join-Path $docs 'icon.png')

# ── Banner para GitHub (1280×640) ──
$font = New-Object System.Windows.Media.FontFamily 'Segoe UI Variable Display, Segoe UI'
$banner = New-Object System.Windows.Controls.Canvas
$banner.Width = 1280; $banner.Height = 640
$bgGrid = New-Object System.Windows.Controls.Grid
$bgGrid.Width = 1280; $bgGrid.Height = 640
foreach ($brush in @(
        (Linear @('#0A1730', '#040814', '#02040B') 90),
        (Radial '#992563EB' 0.0 0.0 0.65),
        (Radial '#6622D3EE' 1.0 0.15 0.5),
        (Radial '#552B7BFF' 0.24 0.62 0.38))) {
    $r = New-Object System.Windows.Shapes.Rectangle
    $r.Fill = $brush
    [void]$bgGrid.Children.Add($r)
}
[void]$banner.Children.Add($bgGrid)

# Círculos guía tenues detrás del símbolo, como en la lámina de construcción
foreach ($ring in @(@(300, 0.10), @(420, 0.06))) {
    $e = New-Object System.Windows.Shapes.Ellipse
    $e.Width = $ring[0]; $e.Height = $ring[0]
    $e.Stroke = New-Object System.Windows.Media.SolidColorBrush (C '#38BDF8')
    $e.StrokeThickness = 1; $e.Opacity = $ring[1]
    [System.Windows.Controls.Canvas]::SetLeft($e, 270 - $ring[0] / 2); [System.Windows.Controls.Canvas]::SetTop($e, 380 - $ring[0] / 2)
    [void]$banner.Children.Add($e)
}

# Notch colgando del borde superior, como en la app
$notch = New-Object System.Windows.Shapes.Path
$notchW = 470; $notchH = 64; $ear = 14; $nx = (1280 - $notchW) / 2
$right = $nx + $notchW
$notch.Data = [System.Windows.Media.Geometry]::Parse(
    "M $($nx - $ear),0 L $($right + $ear),0 A $ear,$ear 0 0 0 $right,$ear L $right,$($notchH - 30) A 30,30 0 0 1 $($right - 30),$notchH L $($nx + 30),$notchH A 30,30 0 0 1 $nx,$($notchH - 30) L $nx,$ear A $ear,$ear 0 0 0 $($nx - $ear),0 Z")
$notch.Fill = New-Object System.Windows.Media.SolidColorBrush (C '#000000')
$notchGlow = New-Object System.Windows.Media.Effects.DropShadowEffect
$notchGlow.Color = C '#2B7BFF'; $notchGlow.BlurRadius = 40; $notchGlow.ShadowDepth = 0; $notchGlow.Opacity = 0.75
$notch.Effect = $notchGlow
[void]$banner.Children.Add($notch)
[void](Box $banner ($nx + 18) 12 40 40 11 (Linear @('#22D3EE', '#2563EB', '#1E3A8A') 45))
$song = New-Object System.Windows.Controls.TextBlock
$song.Text = 'Blinding Lights'; $song.Foreground = [System.Windows.Media.Brushes]::White
$song.FontFamily = $font; $song.FontSize = 17; $song.FontWeight = [System.Windows.FontWeights]::SemiBold
[System.Windows.Controls.Canvas]::SetLeft($song, $nx + 72); [System.Windows.Controls.Canvas]::SetTop($song, 12)
[void]$banner.Children.Add($song)
$artist = New-Object System.Windows.Controls.TextBlock
$artist.Text = 'The Weeknd'; $artist.Foreground = New-Object System.Windows.Media.SolidColorBrush (C '#A3FFFFFF')
$artist.FontFamily = $font; $artist.FontSize = 14
[System.Windows.Controls.Canvas]::SetLeft($artist, $nx + 72); [System.Windows.Controls.Canvas]::SetTop($artist, 33)
[void]$banner.Children.Add($artist)
$eq = @(0.4, 0.75, 0.55, 0.3)
for ($i = 0; $i -lt 4; $i++) {
    $bh = 30 * $eq[$i]
    [void](Box $banner ($nx + $notchW - 52 + $i * 8) (32 - $bh / 2) 5 $bh 2.5 (Linear @('#FFFFFF', '#7DD3FC') 90))
}

# Símbolo con su resplandor
$glow = New-Mark 250
$glowBlur = New-Object System.Windows.Media.Effects.BlurEffect
$glowBlur.Radius = 40
$glow.Effect = $glowBlur; $glow.Opacity = 0.6
[System.Windows.Controls.Canvas]::SetLeft($glow, 145); [System.Windows.Controls.Canvas]::SetTop($glow, 258)
[void]$banner.Children.Add($glow)
$mark = New-Mark 250
[System.Windows.Controls.Canvas]::SetLeft($mark, 145); [System.Windows.Controls.Canvas]::SetTop($mark, 258)
[void]$banner.Children.Add($mark)

$word = New-Wordmark 70 ([System.Windows.Media.Brushes]::White)
[System.Windows.Controls.Canvas]::SetLeft($word, 470); [System.Windows.Controls.Canvas]::SetTop($word, 262)
[void]$banner.Children.Add($word)
$subtitle = New-Object System.Windows.Controls.TextBlock
$subtitle.Text = 'La isla dinámica para Windows'
$subtitle.Foreground = New-Object System.Windows.Media.SolidColorBrush (C '#D9FFFFFF')
$subtitle.FontFamily = $font; $subtitle.FontSize = 28
[System.Windows.Controls.Canvas]::SetLeft($subtitle, 468); [System.Windows.Controls.Canvas]::SetTop($subtitle, 360)
[void]$banner.Children.Add($subtitle)

$chips = New-Object System.Windows.Controls.StackPanel
$chips.Orientation = 'Horizontal'
foreach ($label in 'Spotify · TIDAL · YouTube Music', 'Clima', 'Monedas', 'Código abierto · MIT') {
    $chip = New-Object System.Windows.Controls.Border
    $chip.CornerRadius = New-Object System.Windows.CornerRadius 18
    $chip.Background = New-Object System.Windows.Media.SolidColorBrush (C '#1A60A5FA')
    $chip.BorderBrush = New-Object System.Windows.Media.SolidColorBrush (C '#4038BDF8')
    $chip.BorderThickness = New-Object System.Windows.Thickness 1
    $chip.Padding = New-Object System.Windows.Thickness 16, 7, 16, 8
    $chip.Margin = New-Object System.Windows.Thickness 0, 0, 10, 0
    $text = New-Object System.Windows.Controls.TextBlock
    $text.Text = $label; $text.Foreground = [System.Windows.Media.Brushes]::White
    $text.FontFamily = $font; $text.FontSize = 17
    $chip.Child = $text
    [void]$chips.Children.Add($chip)
}
[System.Windows.Controls.Canvas]::SetLeft($chips, 468); [System.Windows.Controls.Canvas]::SetTop($chips, 425)
[void]$banner.Children.Add($chips)
$credit = New-Object System.Windows.Controls.TextBlock
$credit.FontFamily = $font; $credit.FontSize = 20
$creditBy = New-Object System.Windows.Documents.Run 'por '
$creditBy.Foreground = New-Object System.Windows.Media.SolidColorBrush (C '#99FFFFFF')
$creditName = New-Object System.Windows.Documents.Run 'Danidev.cl'
$creditName.Foreground = New-Object System.Windows.Media.SolidColorBrush (C '#7DD3FC')
$creditName.FontWeight = [System.Windows.FontWeights]::SemiBold
$credit.Inlines.Add($creditBy); $credit.Inlines.Add($creditName)
[System.Windows.Controls.Canvas]::SetLeft($credit, 470); [System.Windows.Controls.Canvas]::SetTop($credit, 490)
[void]$banner.Children.Add($credit)

Save-Png (Render $banner 1280 640) (Join-Path $docs 'banner.png')
Write-Host "Listo: $icoPath, logo.png, docs/icon.png, docs/banner.png"
