# PowerShell Audio Generator for Red Slime Boss SFX
$outputDir = "d:\DATN\Assets\_Project\Bao\Sound\RedSlime"
if (!(Test-Path $outputDir)) {
    New-Item -ItemType Directory -Force -Path $outputDir | Out-Null
}

function Save-Wav {
    param(
        [string]$Path,
        [float[]]$Samples,
        [int]$SampleRate = 44100
    )
    
    $fs = [System.IO.File]::Create($Path)
    $bw = New-Object System.IO.BinaryWriter($fs)
    
    $numSamples = $Samples.Length
    $byteRate = $SampleRate * 2
    $dataChunkSize = $numSamples * 2
    $fileSize = 36 + $dataChunkSize
    
    # RIFF Header
    $bw.Write([System.Text.Encoding]::ASCII.GetBytes("RIFF"))
    $bw.Write([int32]$fileSize)
    $bw.Write([System.Text.Encoding]::ASCII.GetBytes("WAVE"))
    
    # fmt Chunk
    $bw.Write([System.Text.Encoding]::ASCII.GetBytes("fmt "))
    $bw.Write([int32]16)
    $bw.Write([int16]1)          # PCM format
    $bw.Write([int16]1)          # Mono
    $bw.Write([int32]$SampleRate)
    $bw.Write([int32]$byteRate)
    $bw.Write([int16]2)          # Block align
    $bw.Write([int16]16)         # 16-bit
    
    # data Chunk
    $bw.Write([System.Text.Encoding]::ASCII.GetBytes("data"))
    $bw.Write([int32]$dataChunkSize)
    
    foreach ($s in $Samples) {
        $clamped = [Math]::Max(-1.0, [Math]::Min(1.0, $s))
        $val = [int16]($clamped * 32767.0)
        $bw.Write($val)
    }
    
    $bw.Flush()
    $bw.Close()
    $fs.Close()
}

$sampleRate = 44100
$rand = New-Object System.Random(42)

function Gen-Hurt {
    $dur = 0.35
    $n = [int]($dur * $sampleRate)
    $samples = New-Object float[] $n
    for ($i = 0; $i -lt $n; $i++) {
        $t = $i / $sampleRate
        $progress = $t / $dur
        $env = [Math]::Pow(1.0 - $progress, 1.5)
        # Wet squishy pitch drop from 280Hz to 70Hz with frequency modulation
        $freq = 280.0 * [Math]::Exp(-6.0 * $progress) + 70.0
        $fm = 18.0 * [Math]::Sin(2.0 * [Math]::PI * 45.0 * $t)
        $sig = [Math]::Sin(2.0 * [Math]::PI * ($freq + $fm) * $t)
        # Add squelch noise burst at transient
        $noise = ($rand.NextDouble() * 2.0 - 1.0) * [Math]::Exp(-25.0 * $progress) * 0.4
        $samples[$i] = ($sig * 0.7 + $noise) * $env * 0.9
    }
    return $samples
}

function Gen-Death {
    $dur = 1.6
    $n = [int]($dur * $sampleRate)
    $samples = New-Object float[] $n
    for ($i = 0; $i -lt $n; $i++) {
        $t = $i / $sampleRate
        $progress = $t / $dur
        $env = [Math]::Pow(1.0 - $progress, 1.8)
        # Deep bubbling dissolution rumble 160Hz -> 35Hz
        $freq = 160.0 * [Math]::Exp(-2.5 * $progress) + 35.0
        $sig = [Math]::Sin(2.0 * [Math]::PI * $freq * $t)
        $sub = [Math]::Sin(2.0 * [Math]::PI * ($freq * 0.5) * $t) * 0.6
        $bubble = [Math]::Sin(2.0 * [Math]::PI * 18.0 * $t) * 0.3
        $noise = ($rand.NextDouble() * 2.0 - 1.0) * [Math]::Exp(-4.0 * $progress) * 0.35
        $samples[$i] = ($sig * 0.5 + $sub + $noise + $bubble) * $env * 0.85
    }
    return $samples
}

function Gen-Split {
    $dur = 0.55
    $n = [int]($dur * $sampleRate)
    $samples = New-Object float[] $n
    for ($i = 0; $i -lt $n; $i++) {
        $t = $i / $sampleRate
        $progress = $t / $dur
        # Snappy pop transient then bubbly release
        $env = [Math]::Exp(-10.0 * $progress)
        $freq = 450.0 * [Math]::Exp(-12.0 * $progress) + 120.0
        $pop = [Math]::Sin(2.0 * [Math]::PI * $freq * $t)
        $squirt = [Math]::Sin(2.0 * [Math]::PI * 320.0 * $t + 5.0 * [Math]::Sin(2.0 * [Math]::PI * 80.0 * $t)) * [Math]::Exp(-7.0 * $progress) * 0.45
        $noise = ($rand.NextDouble() * 2.0 - 1.0) * [Math]::Exp(-20.0 * $progress) * 0.5
        $samples[$i] = ($pop * 0.6 + $squirt + $noise) * $env * 0.95
    }
    return $samples
}

function Gen-MergeGather {
    $dur = 0.8
    $n = [int]($dur * $sampleRate)
    $samples = New-Object float[] $n
    for ($i = 0; $i -lt $n; $i++) {
        $t = $i / $sampleRate
        $progress = $t / $dur
        # Rising suction energy
        $env = [Math]::Sin($progress * [Math]::PI * 0.5) * (1.0 - [Math]::Pow($progress, 6.0))
        $freq = 90.0 + 340.0 * [Math]::Pow($progress, 2.0)
        $fm = 25.0 * [Math]::Sin(2.0 * [Math]::PI * 15.0 * $t)
        $sig = [Math]::Sin(2.0 * [Math]::PI * ($freq + $fm) * $t)
        $sub = [Math]::Sin(2.0 * [Math]::PI * ($freq * 0.5) * $t) * 0.5
        $samples[$i] = ($sig * 0.6 + $sub) * $env * 0.85
    }
    return $samples
}

function Gen-MergeImpact {
    $dur = 0.9
    $n = [int]($dur * $sampleRate)
    $samples = New-Object float[] $n
    for ($i = 0; $i -lt $n; $i++) {
        $t = $i / $sampleRate
        $progress = $t / $dur
        $env = [Math]::Exp(-4.5 * $progress)
        # Heavy sub-bass boom + wet impact
        $freq = 220.0 * [Math]::Exp(-9.0 * $progress) + 42.0
        $bass = [Math]::Sin(2.0 * [Math]::PI * $freq * $t)
        $sub = [Math]::Sin(2.0 * [Math]::PI * 45.0 * $t) * 0.7 * [Math]::Exp(-3.0 * $progress)
        $snap = ($rand.NextDouble() * 2.0 - 1.0) * [Math]::Exp(-30.0 * $progress) * 0.6
        $samples[$i] = ($bass * 0.6 + $sub + $snap) * $env * 0.95
    }
    return $samples
}

function Gen-Melee {
    $dur = 0.38
    $n = [int]($dur * $sampleRate)
    $samples = New-Object float[] $n
    for ($i = 0; $i -lt $n; $i++) {
        $t = $i / $sampleRate
        $progress = $t / $dur
        # Whoosh swipe with squishy tone
        $env = [Math]::Sin($progress * [Math]::PI) * [Math]::Exp(-1.5 * $progress)
        $freq = 360.0 * [Math]::Exp(-6.0 * $progress) + 110.0
        $whoosh = ($rand.NextDouble() * 2.0 - 1.0) * 0.45
        $tonal = [Math]::Sin(2.0 * [Math]::PI * $freq * $t) * 0.55
        $samples[$i] = ($tonal + $whoosh) * $env * 0.9
    }
    return $samples
}

function Gen-MeleeHit {
    $dur = 0.32
    $n = [int]($dur * $sampleRate)
    $samples = New-Object float[] $n
    for ($i = 0; $i -lt $n; $i++) {
        $t = $i / $sampleRate
        $progress = $t / $dur
        $env = [Math]::Exp(-12.0 * $progress)
        $freq = 320.0 * [Math]::Exp(-14.0 * $progress) + 75.0
        $thud = [Math]::Sin(2.0 * [Math]::PI * $freq * $t)
        $slap = ($rand.NextDouble() * 2.0 - 1.0) * [Math]::Exp(-35.0 * $progress) * 0.65
        $samples[$i] = ($thud * 0.65 + $slap) * $env * 0.95
    }
    return $samples
}

function Gen-SlamJump {
    $dur = 0.6
    $n = [int]($dur * $sampleRate)
    $samples = New-Object float[] $n
    for ($i = 0; $i -lt $n; $i++) {
        $t = $i / $sampleRate
        $progress = $t / $dur
        # Rising launch whoosh + spring release
        $env = [Math]::Sin($progress * [Math]::PI)
        $freq = 80.0 + 260.0 * [Math]::Pow($progress, 1.8)
        $sig = [Math]::Sin(2.0 * [Math]::PI * $freq * $t)
        $whoosh = ($rand.NextDouble() * 2.0 - 1.0) * 0.4
        $samples[$i] = ($sig * 0.6 + $whoosh) * $env * 0.9
    }
    return $samples
}

function Gen-SlamImpact {
    $dur = 1.2
    $n = [int]($dur * $sampleRate)
    $samples = New-Object float[] $n
    for ($i = 0; $i -lt $n; $i++) {
        $t = $i / $sampleRate
        $progress = $t / $dur
        $env = [Math]::Exp(-3.8 * $progress)
        # Deep earthquake shockwave boom
        $freq = 180.0 * [Math]::Exp(-8.0 * $progress) + 38.0
        $boom = [Math]::Sin(2.0 * [Math]::PI * $freq * $t)
        $sub = [Math]::Sin(2.0 * [Math]::PI * 42.0 * $t) * 0.8 * [Math]::Exp(-2.0 * $progress)
        $crack = ($rand.NextDouble() * 2.0 - 1.0) * [Math]::Exp(-35.0 * $progress) * 0.75
        $samples[$i] = ($boom * 0.6 + $sub + $crack) * $env * 0.95
    }
    return $samples
}

function Gen-SpikeErupt {
    $dur = 0.48
    $n = [int]($dur * $sampleRate)
    $samples = New-Object float[] $n
    for ($i = 0; $i -lt $n; $i++) {
        $t = $i / $sampleRate
        $progress = $t / $dur
        $env = [Math]::Exp(-7.0 * $progress)
        # Stone crack + sharp pierce
        $freq = 420.0 * [Math]::Exp(-10.0 * $progress) + 110.0
        $pierce = [Math]::Sin(2.0 * [Math]::PI * $freq * $t)
        $crunch = ($rand.NextDouble() * 2.0 - 1.0) * [Math]::Exp(-15.0 * $progress) * 0.6
        $samples[$i] = ($pierce * 0.55 + $crunch) * $env * 0.9
    }
    return $samples
}

function Gen-DashWindup {
    $dur = 0.32
    $n = [int]($dur * $sampleRate)
    $samples = New-Object float[] $n
    for ($i = 0; $i -lt $n; $i++) {
        $t = $i / $sampleRate
        $progress = $t / $dur
        $env = [Math]::Pow($progress, 1.5)
        # High-tension coil charging
        $freq = 140.0 + 380.0 * [Math]::Pow($progress, 2.5)
        $buzz = [Math]::Sin(2.0 * [Math]::PI * $freq * $t) * [Math]::Sin(2.0 * [Math]::PI * 60.0 * $t)
        $samples[$i] = $buzz * $env * 0.85
    }
    return $samples
}

function Gen-DashLaunch {
    $dur = 0.42
    $n = [int]($dur * $sampleRate)
    $samples = New-Object float[] $n
    for ($i = 0; $i -lt $n; $i++) {
        $t = $i / $sampleRate
        $progress = $t / $dur
        $env = [Math]::Sin($progress * [Math]::PI) * [Math]::Exp(-2.0 * $progress)
        # Supersonic bullet whoosh
        $freq = 520.0 * [Math]::Exp(-6.0 * $progress) + 160.0
        $whoosh = ($rand.NextDouble() * 2.0 - 1.0) * 0.55
        $tone = [Math]::Sin(2.0 * [Math]::PI * $freq * $t) * 0.45
        $samples[$i] = ($tone + $whoosh) * $env * 0.95
    }
    return $samples
}

function Gen-DashHit {
    $dur = 0.38
    $n = [int]($dur * $sampleRate)
    $samples = New-Object float[] $n
    for ($i = 0; $i -lt $n; $i++) {
        $t = $i / $sampleRate
        $progress = $t / $dur
        $env = [Math]::Exp(-9.0 * $progress)
        # Electric lightning slash
        $freq = 480.0 * [Math]::Exp(-12.0 * $progress) + 90.0
        $tone = [Math]::Sin(2.0 * [Math]::PI * $freq * $t)
        $spark = ($rand.NextDouble() * 2.0 - 1.0) * [Math]::Exp(-20.0 * $progress) * 0.7
        $samples[$i] = ($tone * 0.5 + $spark) * $env * 0.95
    }
    return $samples
}

function Gen-ShootWindup {
    $dur = 0.58
    $n = [int]($dur * $sampleRate)
    $samples = New-Object float[] $n
    for ($i = 0; $i -lt $n; $i++) {
        $t = $i / $sampleRate
        $progress = $t / $dur
        $env = [Math]::Pow($progress, 1.2) * (1.0 - [Math]::Pow($progress, 8.0))
        # Gurgling suction
        $freq = 110.0 + 190.0 * $progress
        $gurgle = [Math]::Sin(2.0 * [Math]::PI * $freq * $t + 6.0 * [Math]::Sin(2.0 * [Math]::PI * 25.0 * $t))
        $samples[$i] = $gurgle * $env * 0.85
    }
    return $samples
}

function Gen-ShootFire {
    $dur = 0.45
    $n = [int]($dur * $sampleRate)
    $samples = New-Object float[] $n
    for ($i = 0; $i -lt $n; $i++) {
        $t = $i / $sampleRate
        # 3 rapid spits at t=0, t=0.09, t=0.18
        $val = 0.0
        for ($p = 0; $p -lt 3; $p++) {
            $offset = $p * 0.09
            if ($t -ge $offset) {
                $dt = $t - $offset
                $pEnv = [Math]::Exp(-22.0 * $dt)
                $pFreq = 540.0 * [Math]::Exp(-18.0 * $dt) + 160.0
                $pTone = [Math]::Sin(2.0 * [Math]::PI * $pFreq * $dt)
                $pPop = ($rand.NextDouble() * 2.0 - 1.0) * [Math]::Exp(-45.0 * $dt) * 0.5
                $val += ($pTone * 0.6 + $pPop) * $pEnv
            }
        }
        $samples[$i] = [Math]::Min(1.0, [Math]::Max(-1.0, $val * 0.85))
    }
    return $samples
}

function Gen-ShootImpact {
    $dur = 0.35
    $n = [int]($dur * $sampleRate)
    $samples = New-Object float[] $n
    for ($i = 0; $i -lt $n; $i++) {
        $t = $i / $sampleRate
        $progress = $t / $dur
        $env = [Math]::Exp(-10.0 * $progress)
        $freq = 380.0 * [Math]::Exp(-14.0 * $progress) + 85.0
        $splash = ($rand.NextDouble() * 2.0 - 1.0) * [Math]::Exp(-18.0 * $progress) * 0.65
        $tone = [Math]::Sin(2.0 * [Math]::PI * $freq * $t) * 0.5
        $samples[$i] = ($tone + $splash) * $env * 0.9
    }
    return $samples
}

function Gen-DualWarning {
    $dur = 0.55
    $n = [int]($dur * $sampleRate)
    $samples = New-Object float[] $n
    for ($i = 0; $i -lt $n; $i++) {
        $t = $i / $sampleRate
        $progress = $t / $dur
        # High-tech laser chirp warning sweep
        $env = [Math]::Sin($progress * [Math]::PI)
        $freq = 600.0 + 800.0 * [Math]::Sin(2.0 * [Math]::PI * 12.0 * $t)
        $laser = [Math]::Sin(2.0 * [Math]::PI * $freq * $t)
        $samples[$i] = $laser * $env * 0.8
    }
    return $samples
}

function Gen-DualLaunch {
    $dur = 0.48
    $n = [int]($dur * $sampleRate)
    $samples = New-Object float[] $n
    for ($i = 0; $i -lt $n; $i++) {
        $t = $i / $sampleRate
        $progress = $t / $dur
        $env = [Math]::Sin($progress * [Math]::PI) * [Math]::Exp(-1.2 * $progress)
        # Twin supersonic dash whoosh
        $freq = 620.0 * [Math]::Exp(-5.0 * $progress) + 140.0
        $whoosh = ($rand.NextDouble() * 2.0 - 1.0) * 0.6
        $tone = [Math]::Sin(2.0 * [Math]::PI * $freq * $t) * 0.45
        $samples[$i] = ($tone + $whoosh) * $env * 0.95
    }
    return $samples
}

function Gen-DualClash {
    $dur = 1.1
    $n = [int]($dur * $sampleRate)
    $samples = New-Object float[] $n
    for ($i = 0; $i -lt $n; $i++) {
        $t = $i / $sampleRate
        $progress = $t / $dur
        $env = [Math]::Exp(-4.0 * $progress)
        # Titanic guillotine clash
        $freq = 320.0 * [Math]::Exp(-8.0 * $progress) + 50.0
        $metal = [Math]::Sin(2.0 * [Math]::PI * 880.0 * $t) * 0.3 * [Math]::Exp(-12.0 * $progress)
        $boom = [Math]::Sin(2.0 * [Math]::PI * $freq * $t) * 0.6
        $sub = [Math]::Sin(2.0 * [Math]::PI * 45.0 * $t) * 0.7 * [Math]::Exp(-2.5 * $progress)
        $crack = ($rand.NextDouble() * 2.0 - 1.0) * [Math]::Exp(-30.0 * $progress) * 0.75
        $samples[$i] = ($boom + $metal + $sub + $crack) * $env * 0.95
    }
    return $samples
}

function Gen-DualParried {
    $dur = 0.75
    $n = [int]($dur * $sampleRate)
    $samples = New-Object float[] $n
    for ($i = 0; $i -lt $n; $i++) {
        $t = $i / $sampleRate
        $progress = $t / $dur
        $env = [Math]::Exp(-5.5 * $progress)
        # Sharp resonant metallic parry ping
        $ping1 = [Math]::Sin(2.0 * [Math]::PI * 1760.0 * $t) * 0.5
        $ping2 = [Math]::Sin(2.0 * [Math]::PI * 2640.0 * $t) * 0.35
        $ping3 = [Math]::Sin(2.0 * [Math]::PI * 3520.0 * $t) * 0.2
        $click = ($rand.NextDouble() * 2.0 - 1.0) * [Math]::Exp(-50.0 * $progress) * 0.5
        $samples[$i] = ($ping1 + $ping2 + $ping3 + $click) * $env * 0.9
    }
    return $samples
}

function Gen-PinballCharge {
    $dur = 0.25
    $n = [int]($dur * $sampleRate)
    $samples = New-Object float[] $n
    for ($i = 0; $i -lt $n; $i++) {
        $t = $i / $sampleRate
        $progress = $t / $dur
        $env = [Math]::Pow($progress, 1.5)
        # Hyper spring squash
        $freq = 120.0 + 320.0 * [Math]::Pow($progress, 2.0)
        $spring = [Math]::Sin(2.0 * [Math]::PI * $freq * $t)
        $samples[$i] = $spring * $env * 0.8
    }
    return $samples
}

function Gen-PinballBounce {
    $dur = 0.32
    $n = [int]($dur * $sampleRate)
    $samples = New-Object float[] $n
    for ($i = 0; $i -lt $n; $i++) {
        $t = $i / $sampleRate
        $progress = $t / $dur
        $env = [Math]::Exp(-9.0 * $progress)
        # Rubbery boing ricochet
        $freq = 240.0 + 380.0 * [Math]::Exp(-12.0 * $progress)
        $fm = 40.0 * [Math]::Sin(2.0 * [Math]::PI * 30.0 * $t)
        $boing = [Math]::Sin(2.0 * [Math]::PI * ($freq + $fm) * $t)
        $pop = ($rand.NextDouble() * 2.0 - 1.0) * [Math]::Exp(-35.0 * $progress) * 0.4
        $samples[$i] = ($boing * 0.7 + $pop) * $env * 0.95
    }
    return $samples
}

function Gen-PinballLand {
    $dur = 0.3
    $n = [int]($dur * $sampleRate)
    $samples = New-Object float[] $n
    for ($i = 0; $i -lt $n; $i++) {
        $t = $i / $sampleRate
        $progress = $t / $dur
        $env = [Math]::Exp(-11.0 * $progress)
        $freq = 200.0 * [Math]::Exp(-14.0 * $progress) + 65.0
        $thud = [Math]::Sin(2.0 * [Math]::PI * $freq * $t)
        $rub = ($rand.NextDouble() * 2.0 - 1.0) * [Math]::Exp(-22.0 * $progress) * 0.45
        $samples[$i] = ($thud * 0.65 + $rub) * $env * 0.9
    }
    return $samples
}

function Gen-LaserCharge {
    $dur = 0.3
    $n = [int]($dur * $sampleRate)
    $samples = New-Object float[] $n
    for ($i = 0; $i -lt $n; $i++) {
        $t = $i / $sampleRate
        $progress = $t / $dur
        $env = [Math]::Pow($progress, 1.8)
        # Powering up geometric frequency
        $freq = 180.0 + 850.0 * [Math]::Pow($progress, 2.5)
        $laser = [Math]::Sin(2.0 * [Math]::PI * $freq * $t)
        $samples[$i] = $laser * $env * 0.85
    }
    return $samples
}

function Gen-LaserLoop {
    $dur = 1.0
    $n = [int]($dur * $sampleRate)
    $samples = New-Object float[] $n
    for ($i = 0; $i -lt $n; $i++) {
        $t = $i / $sampleRate
        # Continuous pulsating electronic hum
        $env = 1.0
        if ($t -lt 0.05) { $env = $t / 0.05 }
        if ($t -gt 0.95) { $env = (1.0 - $t) / 0.05 }
        $f1 = [Math]::Sin(2.0 * [Math]::PI * 440.0 * $t) * 0.4
        $f2 = [Math]::Sin(2.0 * [Math]::PI * 880.0 * $t) * 0.25
        $f3 = [Math]::Sin(2.0 * [Math]::PI * 1320.0 * $t) * 0.15
        $pulse = [Math]::Sin(2.0 * [Math]::PI * 14.0 * $t) * 0.2
        $samples[$i] = ($f1 + $f2 + $f3 + $pulse) * $env * 0.85
    }
    return $samples
}

function Gen-LaserBurst {
    $dur = 0.55
    $n = [int]($dur * $sampleRate)
    $samples = New-Object float[] $n
    for ($i = 0; $i -lt $n; $i++) {
        $t = $i / $sampleRate
        $progress = $t / $dur
        $env = [Math]::Exp(-6.5 * $progress)
        $freq = 780.0 * [Math]::Exp(-8.0 * $progress) + 120.0
        $tone = [Math]::Sin(2.0 * [Math]::PI * $freq * $t)
        $shatter = ($rand.NextDouble() * 2.0 - 1.0) * [Math]::Exp(-16.0 * $progress) * 0.55
        $samples[$i] = ($tone * 0.55 + $shatter) * $env * 0.9
    }
    return $samples
}

Write-Host "Generating Red Slime audio clips..."

Save-Wav (Join-Path $outputDir "sfx_redslime_hurt.wav") (Gen-Hurt)
Save-Wav (Join-Path $outputDir "sfx_redslime_death.wav") (Gen-Death)
Save-Wav (Join-Path $outputDir "sfx_redslime_split.wav") (Gen-Split)
Save-Wav (Join-Path $outputDir "sfx_redslime_merge_gather.wav") (Gen-MergeGather)
Save-Wav (Join-Path $outputDir "sfx_redslime_merge_impact.wav") (Gen-MergeImpact)
Save-Wav (Join-Path $outputDir "sfx_redslime_melee.wav") (Gen-Melee)
Save-Wav (Join-Path $outputDir "sfx_redslime_melee_hit.wav") (Gen-MeleeHit)
Save-Wav (Join-Path $outputDir "sfx_redslime_slam_jump.wav") (Gen-SlamJump)
Save-Wav (Join-Path $outputDir "sfx_redslime_slam_impact.wav") (Gen-SlamImpact)
Save-Wav (Join-Path $outputDir "sfx_redslime_spike_erupt.wav") (Gen-SpikeErupt)
Save-Wav (Join-Path $outputDir "sfx_redslime_dash_windup.wav") (Gen-DashWindup)
Save-Wav (Join-Path $outputDir "sfx_redslime_dash_launch.wav") (Gen-DashLaunch)
Save-Wav (Join-Path $outputDir "sfx_redslime_dash_hit.wav") (Gen-DashHit)
Save-Wav (Join-Path $outputDir "sfx_redslime_shoot_windup.wav") (Gen-ShootWindup)
Save-Wav (Join-Path $outputDir "sfx_redslime_shoot_fire.wav") (Gen-ShootFire)
Save-Wav (Join-Path $outputDir "sfx_redslime_shoot_impact.wav") (Gen-ShootImpact)
Save-Wav (Join-Path $outputDir "sfx_redslime_dual_warning.wav") (Gen-DualWarning)
Save-Wav (Join-Path $outputDir "sfx_redslime_dual_launch.wav") (Gen-DualLaunch)
Save-Wav (Join-Path $outputDir "sfx_redslime_dual_clash.wav") (Gen-DualClash)
Save-Wav (Join-Path $outputDir "sfx_redslime_dual_parried.wav") (Gen-DualParried)
Save-Wav (Join-Path $outputDir "sfx_redslime_pinball_charge.wav") (Gen-PinballCharge)
Save-Wav (Join-Path $outputDir "sfx_redslime_pinball_bounce.wav") (Gen-PinballBounce)
Save-Wav (Join-Path $outputDir "sfx_redslime_pinball_land.wav") (Gen-PinballLand)
Save-Wav (Join-Path $outputDir "sfx_redslime_laser_charge.wav") (Gen-LaserCharge)
Save-Wav (Join-Path $outputDir "sfx_redslime_laser_loop.wav") (Gen-LaserLoop)
Save-Wav (Join-Path $outputDir "sfx_redslime_laser_burst.wav") (Gen-LaserBurst)

Write-Host "Successfully generated 26 Red Slime audio clips!"
