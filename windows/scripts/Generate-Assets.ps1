param()

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$assetRoot = Join-Path $PSScriptRoot '..\Assets'
$sourcePath = Join-Path $assetRoot 'icon-source.png'
if (-not (Test-Path -LiteralPath $sourcePath)) {
  throw "Missing canonical icon: $sourcePath"
}

$source = [System.Drawing.Image]::FromFile($sourcePath)
try {
  $targets = @(
    @{ Name = 'Square44x44Logo.scale-200.png'; Width = 88; Height = 88; Icon = 88 },
    @{ Name = 'Square150x150Logo.scale-200.png'; Width = 300; Height = 300; Icon = 300 },
    @{ Name = 'Square44x44Logo.targetsize-24_altform-unplated.png'; Width = 24; Height = 24; Icon = 24 },
    @{ Name = 'Square44x44Logo.targetsize-48_altform-lightunplated.png'; Width = 48; Height = 48; Icon = 48 },
    @{ Name = 'StoreLogo.png'; Width = 50; Height = 50; Icon = 50 },
    @{ Name = 'LockScreenLogo.scale-200.png'; Width = 48; Height = 48; Icon = 48 },
    @{ Name = 'Wide310x150Logo.scale-200.png'; Width = 620; Height = 300; Icon = 260 },
    @{ Name = 'SplashScreen.scale-200.png'; Width = 1240; Height = 600; Icon = 420 }
  )

  foreach ($target in $targets) {
    $bitmap = [System.Drawing.Bitmap]::new($target.Width, $target.Height, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    try {
      $graphics.Clear([System.Drawing.Color]::Transparent)
      $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
      $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
      $size = $target.Icon
      $x = [int](($target.Width - $size) / 2)
      $y = [int](($target.Height - $size) / 2)
      $graphics.DrawImage($source, [System.Drawing.Rectangle]::new($x, $y, $size, $size))
      $bitmap.Save((Join-Path $assetRoot $target.Name), [System.Drawing.Imaging.ImageFormat]::Png)
    }
    finally {
      $graphics.Dispose()
      $bitmap.Dispose()
    }
  }
}
finally {
  $source.Dispose()
}
