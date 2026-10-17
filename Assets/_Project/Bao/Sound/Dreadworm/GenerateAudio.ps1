$csharpSource = @"
using System;
using System.IO;
using System.Text;

public static class DreadwormCinematicAudioEngine
{
    private const int SampleRate = 44100;
    private static readonly Random Rand = new Random(4242);

    public static void GenerateAll(string outputDir)
    {
        Directory.CreateDirectory(outputDir);

        SaveWav(Path.Combine(outputDir, "sfx_dreadworm_engage_roar.wav"), GenEngageRoar());
        SaveWav(Path.Combine(outputDir, "sfx_dreadworm_hurt.wav"), GenHurt());
        SaveWav(Path.Combine(outputDir, "sfx_dreadworm_phase_transition.wav"), GenPhaseTransition());
        SaveWav(Path.Combine(outputDir, "sfx_dreadworm_death.wav"), GenDeath());
        SaveWav(Path.Combine(outputDir, "sfx_dreadworm_parried_stagger.wav"), GenParriedStagger());

        SaveWav(Path.Combine(outputDir, "sfx_dreadworm_slam_windup.wav"), GenSlamWindup());
        SaveWav(Path.Combine(outputDir, "sfx_dreadworm_slam_impact.wav"), GenSlamImpact());
        SaveWav(Path.Combine(outputDir, "sfx_dreadworm_heavy_slam_impact.wav"), GenHeavySlamImpact());
        SaveWav(Path.Combine(outputDir, "sfx_dreadworm_charge_windup.wav"), GenChargeWindup());
        SaveWav(Path.Combine(outputDir, "sfx_dreadworm_charge_dash.wav"), GenChargeDash());
        SaveWav(Path.Combine(outputDir, "sfx_dreadworm_charge_drift.wav"), GenChargeDrift());

        SaveWav(Path.Combine(outputDir, "sfx_dreadworm_ice_arrow_charge.wav"), GenProjectileCharge());
        SaveWav(Path.Combine(outputDir, "sfx_dreadworm_ice_arrow_fire.wav"), GenProjectileFire());
        SaveWav(Path.Combine(outputDir, "sfx_dreadworm_ice_arrow_hit.wav"), GenProjectileHit());

        SaveWav(Path.Combine(outputDir, "sfx_dreadworm_sphere_spawn.wav"), GenSphereSpawn());
        SaveWav(Path.Combine(outputDir, "sfx_dreadworm_sphere_sweep.wav"), GenSphereSweep());
        SaveWav(Path.Combine(outputDir, "sfx_dreadworm_sphere_impact.wav"), GenSphereImpact());

        SaveWav(Path.Combine(outputDir, "sfx_dreadworm_spine_charge_pulse.wav"), GenSpineChargePulse());
        SaveWav(Path.Combine(outputDir, "sfx_dreadworm_spine_erupt.wav"), GenSpineErupt());

        SaveWav(Path.Combine(outputDir, "sfx_dreadworm_sanctuary_windup.wav"), GenSanctuaryWindup());
        SaveWav(Path.Combine(outputDir, "sfx_dreadworm_sanctuary_shield_up.wav"), GenSanctuaryShieldUp());
        SaveWav(Path.Combine(outputDir, "sfx_dreadworm_sanctuary_shield_hit.wav"), GenSanctuaryShieldHit());
        SaveWav(Path.Combine(outputDir, "sfx_dreadworm_sanctuary_shield_break.wav"), GenSanctuaryShieldBreak());

        SaveWav(Path.Combine(outputDir, "sfx_dreadworm_icicle_telegraph.wav"), GenAerialTelegraph());
        SaveWav(Path.Combine(outputDir, "sfx_dreadworm_icicle_impact.wav"), GenAerialImpact());
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  PROFESSIONAL DSP & SYNTHESIS CORE (NO DISTORTION / NO CRACKLE / FULL BODY)
    // ══════════════════════════════════════════════════════════════════════════

    // Brown Noise Generator (Âm thanh trầm ấm, tự nhiên, không rác tai như White Noise)
    private static float brownNoiseState = 0f;
    private static float GenBrownNoise()
    {
        float white = (float)(Rand.NextDouble() * 2.0 - 1.0);
        brownNoiseState = (brownNoiseState + (0.04f * white)) / 1.04f;
        return brownNoiseState * 3.5f;
    }

    // Pink Noise Generator
    private static float b0 = 0, b1 = 0, b2 = 0, b3 = 0, b4 = 0, b5 = 0, b6 = 0;
    private static float GenPinkNoise()
    {
        float white = (float)(Rand.NextDouble() * 2.0 - 1.0);
        b0 = 0.99886f * b0 + white * 0.0555179f;
        b1 = 0.99332f * b1 + white * 0.0750759f;
        b2 = 0.96900f * b2 + white * 0.1538520f;
        b3 = 0.86650f * b3 + white * 0.3104856f;
        b4 = 0.55000f * b4 + white * 0.5329522f;
        b5 = -0.7616f * b5 - white * 0.0168980f;
        float pink = b0 + b1 + b2 + b3 + b4 + b5 + b6 + white * 0.5362f;
        b6 = white * 0.115926f;
        return pink * 0.12f;
    }

    // Smooth Low-Pass Filter (One-pole IIR Filter)
    private static float[] ApplyLowPassFilter(float[] input, float cutoffHz)
    {
        float[] output = new float[input.Length];
        float dt = 1.0f / SampleRate;
        float rc = 1.0f / (2.0f * (float)Math.PI * cutoffHz);
        float alpha = dt / (rc + dt);
        float prev = 0f;

        for (int i = 0; i < input.Length; i++)
        {
            prev = prev + alpha * (input[i] - prev);
            output[i] = prev;
        }
        return output;
    }

    // Smooth Sub-Bass Punch Wave (Sine Frequency Sweep with Smooth Curve)
    private static float GenSubThud(float t, float duration, float startHz = 160f, float endHz = 38f)
    {
        float p = t / duration;
        if (p >= 1.0f) return 0f;
        float env = (float)Math.Pow(1.0 - p, 2.2);
        float currentFreq = (startHz - endHz) * (float)Math.Exp(-12.0 * p) + endHz;
        float wave = (float)Math.Sin(2.0 * Math.PI * currentFreq * t);
        float subWave = (float)Math.Sin(Math.PI * currentFreq * t) * 0.5f;
        return (wave + subWave) * env;
    }

    // Beast Vocal / Throat Resonance (Formant Throat Sweep)
    private static float GenBeastThroat(float t, float duration, float basePitch = 75f, float growlDepth = 25f)
    {
        float p = t / duration;
        if (p >= 1.0f) return 0f;
        float env = (float)Math.Sin(Math.PI * Math.Pow(p, 0.45));

        float tremolo = (float)Math.Sin(2.0 * Math.PI * 18.0 * t);
        float currentPitch = basePitch + (growlDepth * tremolo) - (15.0f * p);

        float f1 = (float)Math.Sin(2.0 * Math.PI * currentPitch * t);
        float f2 = (float)Math.Sin(2.0 * Math.PI * (currentPitch * 2.02f) * t) * 0.6f;
        float f3 = (float)Math.Sin(2.0 * Math.PI * (currentPitch * 3.01f) * t) * 0.35f;
        float throatNoise = GenBrownNoise() * 0.3f;

        return (f1 + f2 + f3 + throatNoise) * env;
    }

    // Impact Transient Crack (Soft & Punchy, No Harsh Clicks)
    private static float GenPunchTransient(float t, float duration, float centerHz = 1200f)
    {
        float p = t / duration;
        if (p >= 1.0f) return 0f;
        float env = (float)Math.Exp(-45.0 * t);
        float sine = (float)Math.Sin(2.0 * Math.PI * centerHz * Math.Exp(-18.0 * t) * t);
        float noise = GenPinkNoise() * 0.8f;
        return (sine * 0.7f + noise * 0.5f) * env;
    }

    // Ambient Reverb Tail / Atmosphere Diffusion
    private static float[] MasterStudio(float[] raw, float reverbWet = 0.28f, int delayMs1 = 45, int delayMs2 = 90)
    {
        int d1 = (int)((delayMs1 / 1000.0) * SampleRate);
        int d2 = (int)((delayMs2 / 1000.0) * SampleRate);
        float[] buf = new float[raw.Length];

        for (int i = 0; i < raw.Length; i++)
        {
            float dry = raw[i];
            float e1 = (i >= d1) ? raw[i - d1] * 0.35f : 0f;
            float e2 = (i >= d2) ? raw[i - d2] * 0.18f : 0f;
            buf[i] = dry * (1.0f - reverbWet) + (e1 + e2) * reverbWet;
        }

        // Low-pass the entire mix at 10.5kHz to remove any harsh digital hiss
        buf = ApplyLowPassFilter(buf, 10500f);

        // Find True Peak
        float peak = 0.0001f;
        for (int i = 0; i < buf.Length; i++)
        {
            float a = Math.Abs(buf[i]);
            if (a > peak) peak = a;
        }

        // Soft-Knee Brickwall Limiter to -0.8 dBFS (0.91 max peak, 0% clipping)
        float targetPeak = 0.91f;
        float gain = targetPeak / Math.Max(peak, 0.0001f);

        for (int i = 0; i < buf.Length; i++)
        {
            float sample = buf[i] * gain;
            // Smooth ArcTan soft compression
            buf[i] = (float)(Math.Atan(sample * 1.1) / Math.Atan(1.1)) * targetPeak;
        }

        return buf;
    }

    private static void SaveWav(string path, float[] rawSamples)
    {
        float[] samples = MasterStudio(rawSamples);
        using (FileStream fs = File.Create(path))
        using (BinaryWriter bw = new BinaryWriter(fs))
        {
            int numSamples = samples.Length;
            int byteRate = SampleRate * 2;
            int dataChunkSize = numSamples * 2;
            int fileSize = 36 + dataChunkSize;

            bw.Write(Encoding.ASCII.GetBytes("RIFF"));
            bw.Write(fileSize);
            bw.Write(Encoding.ASCII.GetBytes("WAVE"));

            bw.Write(Encoding.ASCII.GetBytes("fmt "));
            bw.Write(16);
            bw.Write((short)1);
            bw.Write((short)1); // Mono
            bw.Write(SampleRate);
            bw.Write(byteRate);
            bw.Write((short)2);
            bw.Write((short)16);

            bw.Write(Encoding.ASCII.GetBytes("data"));
            bw.Write(dataChunkSize);

            for (int i = 0; i < samples.Length; i++)
            {
                short val = (short)(Math.Max(-1f, Math.Min(1f, samples[i])) * 32767.0f);
                bw.Write(val);
            }
        }
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  25 MASTER QUALITY DREADWORM SOUNDS
    // ══════════════════════════════════════════════════════════════════════════

    // 1. ENGAGE ROAR (Gầm Uy Lực Mở Màn Trận Đấu)
    private static float[] GenEngageRoar()
    {
        float dur = 2.2f;
        int n = (int)(dur * SampleRate);
        float[] s = new float[n];

        for (int i = 0; i < n; i++)
        {
            float t = i / (float)SampleRate;
            float thud = GenSubThud(t, dur, 130f, 35f) * 0.75f;
            float throat = GenBeastThroat(t, dur, 68f, 22f) * 1.2f;
            float wind = GenBrownNoise() * (float)Math.Sin(Math.PI * (t / dur)) * 0.4f;
            s[i] = thud + throat + wind;
        }
        return s;
    }

    // 2. HURT (Trúng Đòn: Tiếng Va Chạm Thịt Giáp & Gầm Đau Đớn)
    private static float[] GenHurt()
    {
        float dur = 0.45f;
        int n = (int)(dur * SampleRate);
        float[] s = new float[n];

        for (int i = 0; i < n; i++)
        {
            float t = i / (float)SampleRate;
            float p = t / dur;
            float snap = GenPunchTransient(t, dur, 1100f) * 0.9f;
            float body = GenSubThud(t, dur, 190f, 45f) * 0.85f;
            float grunt = (float)Math.Sin(2.0 * Math.PI * (85f - 30f * p) * t) * (float)Math.Exp(-10.0 * p) * 0.6f;
            s[i] = snap + body + grunt;
        }
        return s;
    }

    // 3. PHASE TRANSITION (Thức Tỉnh Cuồng Nộ: Gầm Rền Vang & Nổ Năng Lượng)
    private static float[] GenPhaseTransition()
    {
        float dur = 2.8f;
        int n = (int)(dur * SampleRate);
        float[] s = new float[n];

        for (int i = 0; i < n; i++)
        {
            float t = i / (float)SampleRate;
            float p = t / dur;
            float rise = (float)Math.Sin(2.0 * Math.PI * (45f + 140f * p * p) * t) * (float)Math.Min(1.0, t / 0.8f) * 0.6f;
            float sub = GenSubThud(t, dur, 150f, 30f) * 0.8f;
            float roar = GenBeastThroat(t, dur, 85f, 30f) * 1.3f;
            float atmosphere = GenBrownNoise() * (float)Math.Sin(Math.PI * p) * 0.5f;
            s[i] = rise + sub + roar + atmosphere;
        }
        return s;
    }

    // 4. DEATH (Tử Trận: Tiếng Gầm Hấp Hối & Đổ Sụp Xuống Đất)
    private static float[] GenDeath()
    {
        float dur = 3.0f;
        int n = (int)(dur * SampleRate);
        float[] s = new float[n];

        for (int i = 0; i < n; i++)
        {
            float t = i / (float)SampleRate;
            float p = t / dur;
            float groan = (float)Math.Sin(2.0 * Math.PI * (75f - 35f * p) * t) * (float)Math.Pow(1.0 - p, 1.4) * 0.9f;
            float bodyFall = (t > 1.2f) ? GenSubThud(t - 1.2f, 1.8f, 120f, 30f) * 1.1f : 0f;
            float dust = (t > 1.2f) ? GenBrownNoise() * (float)Math.Exp(-3.0 * (t - 1.2f)) * 0.45f : 0f;
            s[i] = groan + bodyFall + dust;
        }
        return s;
    }

    // 5. PARRIED STAGGER (Bị Parry: Keng Kim Loại Trong Trẻo & Choáng Váng)
    private static float[] GenParriedStagger()
    {
        float dur = 0.85f;
        int n = (int)(dur * SampleRate);
        float[] s = new float[n];

        for (int i = 0; i < n; i++)
        {
            float t = i / (float)SampleRate;
            float p = t / dur;
            // Bell / Metal Ding Tone (In-tune, no harsh noise)
            float ding1 = (float)Math.Sin(2.0 * Math.PI * 1850.0 * t) * (float)Math.Exp(-12.0 * t) * 0.6f;
            float ding2 = (float)Math.Sin(2.0 * Math.PI * 3700.0 * t) * (float)Math.Exp(-22.0 * t) * 0.35f;
            float subWobble = (float)Math.Sin(2.0 * Math.PI * (60.0 + 8.0 * Math.Sin(2.0 * Math.PI * 12.0 * t)) * t) * (float)Math.Exp(-4.0 * t) * 0.5f;
            s[i] = ding1 + ding2 + subWobble;
        }
        return s;
    }

    // 6. SLAM WINDUP (Gồng Cuộn Mình Nện Đất)
    private static float[] GenSlamWindup()
    {
        float dur = 0.55f;
        int n = (int)(dur * SampleRate);
        float[] s = new float[n];

        for (int i = 0; i < n; i++)
        {
            float t = i / (float)SampleRate;
            float p = t / dur;
            float whoosh = GenBrownNoise() * (float)Math.Pow(p, 2.5) * 0.7f;
            float risePitch = (float)Math.Sin(2.0 * Math.PI * (45.0 + 95.0 * p * p) * t) * p * 0.55f;
            s[i] = whoosh + risePitch;
        }
        return s;
    }

    // 7. SLAM IMPACT (Nện Đất Uy Lực: Sub Nặng & Sóng Chấn Động)
    private static float[] GenSlamImpact()
    {
        float dur = 0.85f;
        int n = (int)(dur * SampleRate);
        float[] s = new float[n];

        for (int i = 0; i < n; i++)
        {
            float t = i / (float)SampleRate;
            float snap = GenPunchTransient(t, dur, 850f) * 0.95f;
            float sub = GenSubThud(t, dur, 175f, 32f) * 1.3f;
            float debris = GenBrownNoise() * (float)Math.Exp(-7.0 * t) * 0.5f;
            s[i] = snap + sub + debris;
        }
        return s;
    }

    // 8. HEAVY SLAM IMPACT (Cú Đập Cực Đại: Nổ Rung Đấu Trường)
    private static float[] GenHeavySlamImpact()
    {
        float dur = 1.3f;
        int n = (int)(dur * SampleRate);
        float[] s = new float[n];

        for (int i = 0; i < n; i++)
        {
            float t = i / (float)SampleRate;
            float snap = GenPunchTransient(t, dur, 650f) * 1.2f;
            float sub = GenSubThud(t, dur, 140f, 25f) * 1.6f;
            float rumble = GenBrownNoise() * (float)Math.Exp(-3.5 * t) * 0.65f;
            s[i] = snap + sub + rumble;
        }
        return s;
    }

    // 9. CHARGE WINDUP (Cào Đất Lấy Đà)
    private static float[] GenChargeWindup()
    {
        float dur = 0.65f;
        int n = (int)(dur * SampleRate);
        float[] s = new float[n];

        for (int i = 0; i < n; i++)
        {
            float t = i / (float)SampleRate;
            float p = t / dur;
            float scrape = GenPinkNoise() * (float)Math.Pow(p, 2.0) * 0.5f;
            float growl = GenBeastThroat(t, dur, 65f, 15f) * p * 0.8f;
            s[i] = scrape + growl;
        }
        return s;
    }

    // 10. CHARGE DASH (Lao Húc Xé Gió Thần Tốc)
    private static float[] GenChargeDash()
    {
        float dur = 0.95f;
        int n = (int)(dur * SampleRate);
        float[] s = new float[n];

        for (int i = 0; i < n; i++)
        {
            float t = i / (float)SampleRate;
            float p = t / dur;
            float rush = GenBrownNoise() * (float)Math.Sin(Math.PI * p) * 0.85f;
            float engine = (float)Math.Sin(2.0 * Math.PI * (55.0 + 35.0 * Math.Sin(Math.PI * p)) * t) * (float)Math.Sin(Math.PI * p) * 0.6f;
            s[i] = rush + engine;
        }
        return s;
    }

    // 11. CHARGE DRIFT (Phanh Rê Cát Đổi Hướng)
    private static float[] GenChargeDrift()
    {
        float dur = 0.75f;
        int n = (int)(dur * SampleRate);
        float[] s = new float[n];

        for (int i = 0; i < n; i++)
        {
            float t = i / (float)SampleRate;
            float p = t / dur;
            float gravel = GenPinkNoise() * (float)Math.Pow(1.0 - p, 1.8) * 0.7f;
            float sub = GenSubThud(t, dur, 110f, 40f) * 0.6f;
            s[i] = gravel + sub;
        }
        return s;
    }

    // 12. PROJECTILE CHARGE (Tụ Năng Lượng Phi Đạn)
    private static float[] GenProjectileCharge()
    {
        float dur = 0.55f;
        int n = (int)(dur * SampleRate);
        float[] s = new float[n];

        for (int i = 0; i < n; i++)
        {
            float t = i / (float)SampleRate;
            float p = t / dur;
            float hum = (float)Math.Sin(2.0 * Math.PI * (220.0 + 380.0 * p * p) * t) * p * 0.65f;
            float sub = (float)Math.Sin(2.0 * Math.PI * (75.0 + 60.0 * p) * t) * p * 0.4f;
            s[i] = hum + sub;
        }
        return s;
    }

    // 13. PROJECTILE FIRE (Bắn Phi Đạn Vút Đi)
    private static float[] GenProjectileFire()
    {
        float dur = 0.4f;
        int n = (int)(dur * SampleRate);
        float[] s = new float[n];

        for (int i = 0; i < n; i++)
        {
            float t = i / (float)SampleRate;
            float p = t / dur;
            float zip = (float)Math.Sin(2.0 * Math.PI * (880.0 * Math.Exp(-14.0 * p) + 160.0) * t) * (float)Math.Exp(-8.0 * p) * 0.85f;
            float air = GenPinkNoise() * (float)Math.Exp(-12.0 * p) * 0.4f;
            s[i] = zip + air;
        }
        return s;
    }

    // 14. PROJECTILE HIT (Phi Đạn Nổ Mục Tiêu)
    private static float[] GenProjectileHit()
    {
        float dur = 0.55f;
        int n = (int)(dur * SampleRate);
        float[] s = new float[n];

        for (int i = 0; i < n; i++)
        {
            float t = i / (float)SampleRate;
            float p = t / dur;
            float pop = GenPunchTransient(t, dur, 1400f) * 0.85f;
            float thud = GenSubThud(t, dur, 220f, 50f) * 0.9f;
            s[i] = pop + thud;
        }
        return s;
    }

    // 15. SPHERE SPAWN (Sinh Cầu Năng Lượng)
    private static float[] GenSphereSpawn()
    {
        float dur = 0.8f;
        int n = (int)(dur * SampleRate);
        float[] s = new float[n];

        for (int i = 0; i < n; i++)
        {
            float t = i / (float)SampleRate;
            float p = t / dur;
            float pulse = (float)Math.Sin(2.0 * Math.PI * (180.0 + 80.0 * Math.Sin(2.0 * Math.PI * 8.0 * t)) * t) * (float)Math.Sin(Math.PI * p) * 0.7f;
            float sub = GenSubThud(t, dur, 160f, 40f) * 0.6f;
            s[i] = pulse + sub;
        }
        return s;
    }

    // 16. SPHERE SWEEP (Cầu Năng Lượng Quét Qua Đấu Trường)
    private static float[] GenSphereSweep()
    {
        float dur = 1.1f;
        int n = (int)(dur * SampleRate);
        float[] s = new float[n];

        for (int i = 0; i < n; i++)
        {
            float t = i / (float)SampleRate;
            float p = t / dur;
            float osc = (float)Math.Sin(2.0 * Math.PI * (110.0 + 35.0 * Math.Sin(2.0 * Math.PI * 4.0 * t)) * t) * (float)Math.Sin(Math.PI * p) * 0.75f;
            float wind = GenBrownNoise() * (float)Math.Sin(Math.PI * p) * 0.45f;
            s[i] = osc + wind;
        }
        return s;
    }

    // 17. SPHERE IMPACT (Cầu Năng Lượng Phát Nổ Sóng Xung Kích)
    private static float[] GenSphereImpact()
    {
        float dur = 1.1f;
        int n = (int)(dur * SampleRate);
        float[] s = new float[n];

        for (int i = 0; i < n; i++)
        {
            float t = i / (float)SampleRate;
            float pop = GenPunchTransient(t, dur, 750f) * 1.1f;
            float sub = GenSubThud(t, dur, 160f, 30f) * 1.4f;
            float boom = GenBrownNoise() * (float)Math.Exp(-4.5 * t) * 0.55f;
            s[i] = pop + sub + boom;
        }
        return s;
    }

    // 18. SPINE CHARGE PULSE (Tụ Lực Gai Sống Lưng)
    private static float[] GenSpineChargePulse()
    {
        float dur = 0.45f;
        int n = (int)(dur * SampleRate);
        float[] s = new float[n];

        for (int i = 0; i < n; i++)
        {
            float t = i / (float)SampleRate;
            float p = t / dur;
            float chime = (float)Math.Sin(2.0 * Math.PI * (340.0 + 260.0 * p) * t) * (float)Math.Sin(Math.PI * p) * 0.6f;
            s[i] = chime;
        }
        return s;
    }

    // 19. SPINE ERUPT (Bắn Loạt Gai Nhọn Xung Quanh)
    private static float[] GenSpineErupt()
    {
        float dur = 0.85f;
        int n = (int)(dur * SampleRate);
        float[] s = new float[n];

        for (int i = 0; i < n; i++)
        {
            float t = i / (float)SampleRate;
            float snap = GenPunchTransient(t, dur, 1200f) * 0.9f;
            float sub = GenSubThud(t, dur, 180f, 40f) * 0.95f;
            float flutter = GenPinkNoise() * (float)Math.Exp(-6.0 * t) * 0.45f;
            s[i] = snap + sub + flutter;
        }
        return s;
    }

    // 20. SANCTUARY WINDUP (Tạo Trận Địa Phòng Thủ)
    private static float[] GenSanctuaryWindup()
    {
        float dur = 1.0f;
        int n = (int)(dur * SampleRate);
        float[] s = new float[n];

        for (int i = 0; i < n; i++)
        {
            float t = i / (float)SampleRate;
            float p = t / dur;
            float aura = (float)Math.Sin(2.0 * Math.PI * (140.0 + 180.0 * p * p) * t) * p * 0.65f;
            float sub = (float)Math.Sin(2.0 * Math.PI * 65.0 * t) * p * 0.4f;
            s[i] = aura + sub;
        }
        return s;
    }

    // 21. SANCTUARY SHIELD UP (Kích Hoạt Khiên Năng Lượng)
    private static float[] GenSanctuaryShieldUp()
    {
        float dur = 0.9f;
        int n = (int)(dur * SampleRate);
        float[] s = new float[n];

        for (int i = 0; i < n; i++)
        {
            float t = i / (float)SampleRate;
            float chime1 = (float)Math.Sin(2.0 * Math.PI * 520.0 * t) * (float)Math.Exp(-5.0 * t) * 0.5f;
            float chime2 = (float)Math.Sin(2.0 * Math.PI * 1040.0 * t) * (float)Math.Exp(-8.0 * t) * 0.35f;
            float sub = GenSubThud(t, dur, 150f, 45f) * 0.8f;
            s[i] = chime1 + chime2 + sub;
        }
        return s;
    }

    // 22. SANCTUARY SHIELD HIT (Khiên Chặn Đòn Đánh)
    private static float[] GenSanctuaryShieldHit()
    {
        float dur = 0.45f;
        int n = (int)(dur * SampleRate);
        float[] s = new float[n];

        for (int i = 0; i < n; i++)
        {
            float t = i / (float)SampleRate;
            float pulse = (float)Math.Sin(2.0 * Math.PI * 440.0 * t) * (float)Math.Exp(-14.0 * t) * 0.7f;
            float sub = GenSubThud(t, dur, 180f, 60f) * 0.7f;
            s[i] = pulse + sub;
        }
        return s;
    }

    // 23. SANCTUARY SHIELD BREAK (Khiên Vỡ Tan Giải Phóng Năng Lượng)
    private static float[] GenSanctuaryShieldBreak()
    {
        float dur = 1.1f;
        int n = (int)(dur * SampleRate);
        float[] s = new float[n];

        for (int i = 0; i < n; i++)
        {
            float t = i / (float)SampleRate;
            float shatter1 = (float)Math.Sin(2.0 * Math.PI * 1650.0 * t) * (float)Math.Exp(-9.0 * t) * 0.6f;
            float shatter2 = (float)Math.Sin(2.0 * Math.PI * 2850.0 * t) * (float)Math.Exp(-14.0 * t) * 0.35f;
            float sub = GenSubThud(t, dur, 190f, 35f) * 1.1f;
            float debris = GenPinkNoise() * (float)Math.Exp(-5.0 * t) * 0.45f;
            s[i] = shatter1 + shatter2 + sub + debris;
        }
        return s;
    }

    // 24. AERIAL TELEGRAPH (Cảnh Báo Vật Thể Rơi Từ Trời Cao)
    private static float[] GenAerialTelegraph()
    {
        float dur = 0.8f;
        int n = (int)(dur * SampleRate);
        float[] s = new float[n];

        for (int i = 0; i < n; i++)
        {
            float t = i / (float)SampleRate;
            float p = t / dur;
            float dropPitch = (float)Math.Sin(2.0 * Math.PI * (1100.0 * Math.Exp(-4.0 * p) + 180.0) * t) * (float)Math.Sin(Math.PI * p) * 0.65f;
            float wind = GenPinkNoise() * (float)Math.Sin(Math.PI * p) * 0.35f;
            s[i] = dropPitch + wind;
        }
        return s;
    }

    // 25. AERIAL IMPACT (Thiên Thạch Nện Xuống Đất: Nổ Rền Vang)
    private static float[] GenAerialImpact()
    {
        float dur = 1.3f;
        int n = (int)(dur * SampleRate);
        float[] s = new float[n];

        for (int i = 0; i < n; i++)
        {
            float t = i / (float)SampleRate;
            float snap = GenPunchTransient(t, dur, 700f) * 1.1f;
            float sub = GenSubThud(t, dur, 160f, 30f) * 1.5f;
            float dust = GenBrownNoise() * (float)Math.Exp(-3.5 * t) * 0.6f;
            s[i] = snap + sub + dust;
        }
        return s;
    }
}
"@

Add-Type -TypeDefinition $csharpSource -Language CSharp
$outputDirectory = "d:\DATN\Assets\_Project\Bao\Sound\Dreadworm"
[DreadwormCinematicAudioEngine]::GenerateAll($outputDirectory)
Write-Host "Studio-grade Dreadworm Audio generation complete! Total 25 sounds created."
