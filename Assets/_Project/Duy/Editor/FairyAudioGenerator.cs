using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Công cụ tạo các file âm thanh WAV chuẩn cho Tinh linh (Fairy):
/// - fairy_fly_loop.wav: Âm thanh bay lơ lửng ma thuật (Loop).
/// - fairy_fire_ambient.wav: Âm thanh ngọn lửa bùng cháy ma thuật (Loop).
/// - fairy_water_ambient.wav: Âm thanh nước róc rách & bong bóng nước (Loop).
/// - fairy_swap_switch.wav: Âm thanh chuyển đổi tinh linh khi bấm phím F (One-shot).
/// - fairy_death_sizzle.wav: Âm thanh khi tinh linh trúng bẫy lửa tử trận (One-shot).
/// - fairy_respawn_chime.wav: Âm thanh chuông tiên khi tinh linh hồi sinh (One-shot).
/// </summary>
[InitializeOnLoad]
public static class FairyAudioGenerator
{
    private const string AudioFolder = "Assets/_Project/Duy/Audio/Fairy";

    static FairyAudioGenerator()
    {
        EditorApplication.delayCall += EnsureFairyAudioFiles;
    }

    [MenuItem("Tools/Duy/Tạo Âm Thanh Tinh Linh (Fairy SFX)")]
    public static void GenerateAllFairyAudio()
    {
        EnsureFairyAudioFiles(true);
    }

    public static void EnsureFairyAudioFiles()
    {
        EnsureFairyAudioFiles(false);
    }

    private static void EnsureFairyAudioFiles(bool forceRegenerate)
    {
        string fullFolderPath = Path.Combine(Application.dataPath, "_Project/Duy/Audio/Fairy");
        if (!Directory.Exists(fullFolderPath))
        {
            Directory.CreateDirectory(fullFolderPath);
        }

        string flyPath = Path.Combine(fullFolderPath, "fairy_fly_loop.wav");
        string firePath = Path.Combine(fullFolderPath, "fairy_fire_ambient.wav");
        string waterPath = Path.Combine(fullFolderPath, "fairy_water_ambient.wav");
        string swapPath = Path.Combine(fullFolderPath, "fairy_swap_switch.wav");
        string deathPath = Path.Combine(fullFolderPath, "fairy_death_sizzle.wav");
        string respawnPath = Path.Combine(fullFolderPath, "fairy_respawn_chime.wav");

        bool generatedAny = false;

        if (forceRegenerate || !File.Exists(flyPath))
        {
            GenerateFlyLoopWav(flyPath);
            generatedAny = true;
        }

        if (forceRegenerate || !File.Exists(firePath))
        {
            GenerateFireAmbientWav(firePath);
            generatedAny = true;
        }

        if (forceRegenerate || !File.Exists(waterPath))
        {
            GenerateWaterAmbientWav(waterPath);
            generatedAny = true;
        }

        if (forceRegenerate || !File.Exists(swapPath))
        {
            GenerateSwapSwitchWav(swapPath);
            generatedAny = true;
        }

        if (forceRegenerate || !File.Exists(deathPath))
        {
            GenerateDeathSizzleWav(deathPath);
            generatedAny = true;
        }

        if (forceRegenerate || !File.Exists(respawnPath))
        {
            GenerateRespawnChimeWav(respawnPath);
            generatedAny = true;
        }

        if (generatedAny)
        {
            AssetDatabase.Refresh();
            ConfigureAudioClipSettings();
            Debug.Log("[FairyAudioGenerator] Đã tạo thành công bộ âm thanh Tinh linh tại: " + AudioFolder);
        }
    }

    private static void ConfigureAudioClipSettings()
    {
        string[] clips = new string[]
        {
            AudioFolder + "/fairy_fly_loop.wav",
            AudioFolder + "/fairy_fire_ambient.wav",
            AudioFolder + "/fairy_water_ambient.wav",
            AudioFolder + "/fairy_swap_switch.wav",
            AudioFolder + "/fairy_death_sizzle.wav",
            AudioFolder + "/fairy_respawn_chime.wav"
        };

        foreach (string clipPath in clips)
        {
            AudioImporter importer = AssetImporter.GetAtPath(clipPath) as AudioImporter;
            if (importer != null)
            {
                AudioImporterSampleSettings settings = importer.defaultSampleSettings;
                settings.loadType = AudioClipLoadType.DecompressOnLoad;
                settings.compressionFormat = AudioCompressionFormat.PCM;
                importer.defaultSampleSettings = settings;
                importer.forceToMono = true;
                importer.SaveAndReimport();
            }
        }
    }

    private static void GenerateFlyLoopWav(string path)
    {
        const int sampleRate = 44100;
        const float duration = 2.5f;
        int numSamples = (int)(duration * sampleRate);
        float[] samples = new float[numSamples];
        System.Random rnd = new System.Random(101);

        for (int i = 0; i < numSamples; i++)
        {
            float t = (float)i / sampleRate;
            float flutter = 1.0f + 0.18f * Mathf.Sin(2.0f * Mathf.PI * 5.5f * t);
            float s1 = Mathf.Sin(2.0f * Mathf.PI * 340.0f * t) * 0.32f;
            float s2 = Mathf.Sin(2.0f * Mathf.PI * 680.0f * t) * 0.20f;
            float s3 = Mathf.Sin(2.0f * Mathf.PI * 1020.0f * t) * 0.12f;
            float sparkle = Mathf.Sin(2.0f * Mathf.PI * 1760.0f * t + Mathf.Sin(2.0f * Mathf.PI * 3.0f * t)) * 0.10f;
            float breeze = ((float)rnd.NextDouble() * 2.0f - 1.0f) * 0.04f;

            // Đảm bảo loop tròn viền không bị click
            float loopEnvelope = Mathf.Sin((float)i / numSamples * Mathf.PI);
            samples[i] = (s1 + s2 + s3 + sparkle + breeze) * flutter;
        }

        WritePcmWav(path, samples, sampleRate);
    }

    private static void GenerateFireAmbientWav(string path)
    {
        const int sampleRate = 44100;
        const float duration = 3.0f;
        int numSamples = (int)(duration * sampleRate);
        float[] samples = new float[numSamples];
        System.Random rnd = new System.Random(202);

        for (int i = 0; i < numSamples; i++)
        {
            float t = (float)i / sampleRate;
            float flameRumble = Mathf.Sin(2.0f * Mathf.PI * 150.0f * t + Mathf.Sin(2.0f * Mathf.PI * 11.0f * t) * 1.5f) * 0.26f;
            float flameMid = Mathf.Sin(2.0f * Mathf.PI * 300.0f * t + Mathf.Cos(2.0f * Mathf.PI * 7.0f * t)) * 0.18f;
            float hiss = ((float)rnd.NextDouble() * 2.0f - 1.0f) * 0.14f * (1.0f + 0.3f * Mathf.Sin(2.0f * Mathf.PI * 4.0f * t));

            float crackle = 0f;
            if (rnd.NextDouble() < 0.0038)
            {
                crackle = ((float)rnd.NextDouble() * 2.0f - 1.0f) * 0.65f;
            }

            samples[i] = flameRumble + flameMid + hiss + crackle;
        }

        WritePcmWav(path, samples, sampleRate);
    }

    private static void GenerateWaterAmbientWav(string path)
    {
        const int sampleRate = 44100;
        const float duration = 3.0f;
        int numSamples = (int)(duration * sampleRate);
        float[] samples = new float[numSamples];
        System.Random rnd = new System.Random(303);

        for (int i = 0; i < numSamples; i++)
        {
            float t = (float)i / sampleRate;
            float streamLow = Mathf.Sin(2.0f * Mathf.PI * 220.0f * t + Mathf.Sin(2.0f * Mathf.PI * 6.5f * t)) * 0.20f;
            float bubbleF1 = 450.0f + 350.0f * Mathf.Sin(2.0f * Mathf.PI * 4.0f * t);
            float bubbleF2 = 700.0f + 400.0f * Mathf.Cos(2.0f * Mathf.PI * 3.2f * t);
            float bubble1 = Mathf.Sin(2.0f * Mathf.PI * bubbleF1 * t) * 0.16f * Mathf.Max(0.0f, Mathf.Sin(2.0f * Mathf.PI * 8.0f * t));
            float bubble2 = Mathf.Sin(2.0f * Mathf.PI * bubbleF2 * t) * 0.14f * Mathf.Max(0.0f, Mathf.Cos(2.0f * Mathf.PI * 6.0f * t));
            float waterNoise = ((float)rnd.NextDouble() * 2.0f - 1.0f) * 0.06f;

            samples[i] = streamLow + bubble1 + bubble2 + waterNoise;
        }

        WritePcmWav(path, samples, sampleRate);
    }

    private static void GenerateSwapSwitchWav(string path)
    {
        const int sampleRate = 44100;
        const float duration = 0.85f;
        int numSamples = (int)(duration * sampleRate);
        float[] samples = new float[numSamples];
        float[] notes = new float[] { 523.25f, 659.25f, 783.99f, 1046.50f, 1318.51f };

        for (int i = 0; i < numSamples; i++)
        {
            float t = (float)i / sampleRate;
            float env = Mathf.Exp(-3.5f * t);
            float whoosh = Mathf.Sin(2.0f * Mathf.PI * (240.0f + 850.0f * t) * t) * 0.35f * Mathf.Exp(-6.0f * t);
            float chimes = 0f;

            for (int k = 0; k < notes.Length; k++)
            {
                float noteTime = t - (k * 0.045f);
                if (noteTime >= 0)
                {
                    float noteEnv = Mathf.Exp(-5.5f * noteTime);
                    chimes += Mathf.Sin(2.0f * Mathf.PI * notes[k] * noteTime) * 0.15f * noteEnv;
                }
            }

            samples[i] = (whoosh + chimes) * env;
        }

        WritePcmWav(path, samples, sampleRate);
    }

    private static void GenerateDeathSizzleWav(string path)
    {
        const int sampleRate = 44100;
        const float duration = 0.8f;
        int numSamples = (int)(duration * sampleRate);
        float[] samples = new float[numSamples];
        System.Random rnd = new System.Random(505);

        for (int i = 0; i < numSamples; i++)
        {
            float t = (float)i / sampleRate;
            float env = Mathf.Exp(-4.0f * t);
            float dropFreq = Mathf.Max(50.0f, 500.0f * (1.0f - t * 1.5f));
            float dropTone = Mathf.Sin(2.0f * Mathf.PI * dropFreq * t) * 0.32f;
            float sizzle = ((float)rnd.NextDouble() * 2.0f - 1.0f) * 0.45f * Mathf.Exp(-3.0f * t);

            samples[i] = (dropTone + sizzle) * env;
        }

        WritePcmWav(path, samples, sampleRate);
    }

    private static void GenerateRespawnChimeWav(string path)
    {
        const int sampleRate = 44100;
        const float duration = 1.2f;
        int numSamples = (int)(duration * sampleRate);
        float[] samples = new float[numSamples];
        float[] respawnNotes = new float[] { 440.0f, 554.37f, 659.25f, 880.0f };

        for (int i = 0; i < numSamples; i++)
        {
            float t = (float)i / sampleRate;
            float env = Mathf.Exp(-2.8f * t);
            float chimes = 0f;

            for (int k = 0; k < respawnNotes.Length; k++)
            {
                float noteTime = t - (k * 0.06f);
                if (noteTime >= 0)
                {
                    float noteEnv = Mathf.Exp(-3.5f * noteTime);
                    chimes += Mathf.Sin(2.0f * Mathf.PI * respawnNotes[k] * noteTime) * 0.18f * noteEnv;
                }
            }

            samples[i] = chimes * env;
        }

        WritePcmWav(path, samples, sampleRate);
    }

    private static void WritePcmWav(string filePath, float[] samples, int sampleRate)
    {
        using (FileStream stream = File.Create(filePath))
        using (BinaryWriter writer = new BinaryWriter(stream))
        {
            int byteRate = sampleRate * 2;
            int dataLength = samples.Length * 2;
            int chunkSize = 36 + dataLength;

            // RIFF header
            writer.Write(Encoding.ASCII.GetBytes("RIFF"));
            writer.Write(chunkSize);
            writer.Write(Encoding.ASCII.GetBytes("WAVE"));

            // fmt subchunk
            writer.Write(Encoding.ASCII.GetBytes("fmt "));
            writer.Write(16); // subchunk1Size (16 for PCM)
            writer.Write((short)1); // AudioFormat (1 = PCM)
            writer.Write((short)1); // NumChannels (1 = Mono)
            writer.Write(sampleRate);
            writer.Write(byteRate);
            writer.Write((short)2); // BlockAlign (NumChannels * BitsPerSample / 8)
            writer.Write((short)16); // BitsPerSample

            // data subchunk
            writer.Write(Encoding.ASCII.GetBytes("data"));
            writer.Write(dataLength);

            foreach (float sample in samples)
            {
                float clamped = Mathf.Clamp(sample, -1.0f, 1.0f);
                short shortVal = (short)(clamped * 32767.0f);
                writer.Write(shortVal);
            }
        }
    }
}
