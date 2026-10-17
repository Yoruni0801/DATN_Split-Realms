using UnityEngine;
using UnityEditor;
using System.IO;
using System;
using System.Reflection;

public class AudioTrimmerWindow : EditorWindow
{
    public enum AppMode { TrimSingle, CombineTwo }
    private AppMode currentMode = AppMode.TrimSingle;

    public enum CombineType { Append, Mix }
    private CombineType combineType = CombineType.Append;

    private AudioClip sourceClip1;
    private float startTime1 = 0f;
    private float endTime1 = 0f;

    private AudioClip sourceClip2;
    private float startTime2 = 0f;
    private float endTime2 = 0f;

    private AudioClip previewClip;

    private Vector2 scrollPos;

    private Color headerColor = new Color(0.2f, 0.2f, 0.22f);
    private Color accentColor = new Color(0.3f, 0.6f, 0.9f);

    [MenuItem("Tools/Duy/Audio Trimmer")]
    public static void ShowWindow()
    {
        AudioTrimmerWindow window = GetWindow<AudioTrimmerWindow>("Audio Trimmer");
        window.minSize = new Vector2(350, 450);
    }

    private void OnDisable()
    {
        StopPreview();

        if (previewClip != null)
        {
            DestroyImmediate(previewClip);
            previewClip = null;
        }
    }

    private void OnGUI()
    {
        DrawHeader();

        scrollPos = EditorGUILayout.BeginScrollView(scrollPos);
        EditorGUILayout.BeginVertical(new GUIStyle { padding = new RectOffset(15, 15, 15, 15) });

        DrawModeSelection();

        if (currentMode == AppMode.TrimSingle)
        {
            DrawFileSelection(ref sourceClip1, ref startTime1, ref endTime1, "1. CHỌN TẬP TIN");

            if (sourceClip1 != null)
            {
                DrawTrimmingSection(sourceClip1, ref startTime1, ref endTime1, "2. CÀI ĐẶT KHOẢNG CẮT");
                DrawPreviewSection();
                DrawActionSection("3. XUẤT FILE", "CẮT & LƯU THÀNH WAV");
            }
            else
            {
                EditorGUILayout.Space(20);
                EditorGUILayout.HelpBox("Vui lòng gắn một AudioClip vào để bắt đầu cắt và chỉnh sửa.", MessageType.Info);
            }
        }
        else
        {
            DrawFileSelection(ref sourceClip1, ref startTime1, ref endTime1, "1. CHỌN TẬP TIN 1");
            if (sourceClip1 != null)
                DrawTrimmingSection(sourceClip1, ref startTime1, ref endTime1, "CÀI ĐẶT KHOẢNG CẮT FILE 1");

            DrawFileSelection(ref sourceClip2, ref startTime2, ref endTime2, "2. CHỌN TẬP TIN 2");
            if (sourceClip2 != null)
                DrawTrimmingSection(sourceClip2, ref startTime2, ref endTime2, "CÀI ĐẶT KHOẢNG CẮT FILE 2");

            if (sourceClip1 != null && sourceClip2 != null)
            {
                DrawCombineOptions();
                DrawPreviewSection();
                DrawActionSection("4. XUẤT FILE", "KẾT HỢP & LƯU THÀNH WAV");
            }
            else
            {
                EditorGUILayout.Space(20);
                EditorGUILayout.HelpBox("Vui lòng gắn đủ hai AudioClip để bắt đầu kết hợp.", MessageType.Info);
            }
        }

        EditorGUILayout.EndVertical();
        EditorGUILayout.EndScrollView();
    }

    private void DrawHeader()
    {
        Rect headerRect = GUILayoutUtility.GetRect(0, 50);
        EditorGUI.DrawRect(headerRect, headerColor);

        GUIStyle titleStyle = new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize = 18,
            alignment = TextAnchor.MiddleLeft,
            normal = { textColor = Color.white }
        };

        Rect titleRect = new Rect(headerRect.x + 15, headerRect.y, headerRect.width - 30, headerRect.height);

        GUIContent titleContent = new GUIContent(
            " AUDIO TRIMMER",
            EditorGUIUtility.IconContent("AudioClip Icon").image
        );

        EditorGUI.LabelField(titleRect, titleContent, titleStyle);

        Rect lineRect = new Rect(0, headerRect.yMax - 2, position.width, 2);
        EditorGUI.DrawRect(lineRect, accentColor);
    }

    private void DrawModeSelection()
    {
        EditorGUILayout.LabelField("CHẾ ĐỘ", EditorStyles.boldLabel);
        EditorGUILayout.BeginVertical("HelpBox");
        EditorGUILayout.Space(5);
        
        EditorGUI.BeginChangeCheck();
        currentMode = (AppMode)EditorGUILayout.EnumPopup("Chế độ", currentMode);
        if (EditorGUI.EndChangeCheck())
        {
            StopPreview();
        }

        EditorGUILayout.Space(5);
        EditorGUILayout.EndVertical();
        EditorGUILayout.Space(10);
    }

    private void DrawCombineOptions()
    {
        EditorGUILayout.LabelField("3. TÙY CHỌN KẾT HỢP", EditorStyles.boldLabel);
        EditorGUILayout.BeginVertical("HelpBox");
        EditorGUILayout.Space(5);
        
        EditorGUI.BeginChangeCheck();
        combineType = (CombineType)EditorGUILayout.EnumPopup("Kiểu kết hợp", combineType);
        if (EditorGUI.EndChangeCheck())
        {
            StopPreview();
        }
        
        if (combineType == CombineType.Append)
            EditorGUILayout.LabelField("Nối tiếp File 2 sau File 1", EditorStyles.miniLabel);
        else
            EditorGUILayout.LabelField("Chồng (Mix) File 2 lên File 1", EditorStyles.miniLabel);

        EditorGUILayout.Space(5);
        EditorGUILayout.EndVertical();
        EditorGUILayout.Space(10);
    }

    private void DrawFileSelection(ref AudioClip clip, ref float start, ref float end, string title)
    {
        EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
        EditorGUILayout.BeginVertical("HelpBox");
        EditorGUILayout.Space(5);

        EditorGUI.BeginChangeCheck();

        clip = (AudioClip)EditorGUILayout.ObjectField(
            "Audio File",
            clip,
            typeof(AudioClip),
            false
        );

        if (EditorGUI.EndChangeCheck())
        {
            StopPreview();

            if (previewClip != null)
            {
                DestroyImmediate(previewClip);
                previewClip = null;
            }

            if (clip != null)
            {
                start = 0f;
                end = clip.length;
            }
            else
            {
                start = 0f;
                end = 0f;
            }
        }

        if (clip != null)
        {
            EditorGUILayout.LabelField(
                $"Định dạng: {clip.frequency}Hz | {clip.channels} kênh | {clip.length:F2}s",
                EditorStyles.miniLabel
            );
        }

        EditorGUILayout.Space(5);
        EditorGUILayout.EndVertical();
        EditorGUILayout.Space(10);
    }

    private void DrawTrimmingSection(AudioClip clip, ref float start, ref float end, string title)
    {
        EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
        EditorGUILayout.BeginVertical("HelpBox");
        EditorGUILayout.Space(10);

        EditorGUI.BeginChangeCheck();

        EditorGUILayout.MinMaxSlider(ref start, ref end, 0f, clip.length);

        EditorGUILayout.Space(5);

        EditorGUILayout.BeginHorizontal();

        EditorGUILayout.BeginVertical();
        EditorGUILayout.LabelField("Bắt đầu (s)", EditorStyles.miniLabel);
        start = EditorGUILayout.FloatField(start);
        EditorGUILayout.EndVertical();

        EditorGUILayout.Space(10);

        EditorGUILayout.BeginVertical();
        EditorGUILayout.LabelField("Kết thúc (s)", EditorStyles.miniLabel);
        end = EditorGUILayout.FloatField(end);
        EditorGUILayout.EndVertical();

        EditorGUILayout.EndHorizontal();

        if (EditorGUI.EndChangeCheck())
        {
            start = Mathf.Clamp(start, 0f, clip.length);
            end = Mathf.Clamp(end, 0f, clip.length);

            if (end < start)
            {
                end = start;
            }

            StopPreview();
        }

        EditorGUILayout.Space(10);

        float duration = end - start;

        GUI.color = Color.cyan;
        EditorGUILayout.LabelField($"Thời lượng đoạn này: {duration:F3} giây", EditorStyles.boldLabel);
        GUI.color = Color.white;

        EditorGUILayout.Space(5);
        EditorGUILayout.EndVertical();
        EditorGUILayout.Space(10);
    }

    private void DrawPreviewSection()
    {
        EditorGUILayout.LabelField(currentMode == AppMode.TrimSingle ? "3. NGHE THỬ" : "NGHE THỬ KẾT QUẢ", EditorStyles.boldLabel);
        EditorGUILayout.BeginVertical("HelpBox");
        EditorGUILayout.Space(5);

        EditorGUILayout.BeginHorizontal();

        GUI.backgroundColor = new Color(0.7f, 1f, 0.7f);

        if (GUILayout.Button(
                new GUIContent(" Play", EditorGUIUtility.IconContent("PlayButton").image),
                GUILayout.Height(35)
            ))
        {
            PlayPreview();
        }

        GUI.backgroundColor = new Color(1f, 0.7f, 0.7f);

        if (GUILayout.Button(
                new GUIContent(" Stop", EditorGUIUtility.IconContent("preAudioLoopOff").image),
                GUILayout.Height(35)
            ))
        {
            StopPreview();
        }

        GUI.backgroundColor = Color.white;

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(5);
        EditorGUILayout.HelpBox(
            "Nếu bấm Play không nghe, hãy chọn AudioClip trong Project rồi đổi Import Settings: Load Type = Decompress On Load, Preload Audio Data = On.",
            MessageType.Info
        );

        EditorGUILayout.EndVertical();
        EditorGUILayout.Space(10);
    }

    private void DrawActionSection(string title, string btnText)
    {
        EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
        EditorGUILayout.BeginVertical("HelpBox");
        EditorGUILayout.Space(10);

        GUI.backgroundColor = accentColor;

        if (GUILayout.Button(btnText, GUILayout.Height(45)))
        {
            TrimAndSaveAudio();
        }

        GUI.backgroundColor = Color.white;

        EditorGUILayout.Space(10);
        EditorGUILayout.EndVertical();
    }

    private float[] GetTrimmedData(AudioClip clip, float start, float end, out int channels, out int freq)
    {
        channels = clip.channels;
        freq = clip.frequency;

        int startSample = Mathf.FloorToInt(start * freq * channels);
        int endSample = Mathf.FloorToInt(end * freq * channels);
        int totalSampleData = clip.samples * channels;

        startSample = Mathf.Clamp(startSample, 0, totalSampleData);
        endSample = Mathf.Clamp(endSample, 0, totalSampleData);

        int sampleCount = endSample - startSample;
        if (sampleCount <= 0) return null;

        float[] originalData = new float[totalSampleData];
        if (!clip.GetData(originalData, 0))
        {
            return null;
        }

        float[] trimmedData = new float[sampleCount];
        Array.Copy(originalData, startSample, trimmedData, 0, sampleCount);
        return trimmedData;
    }

    private float[] StandardizeChannels(float[] data, int currentChannels, int targetChannels)
    {
        if (currentChannels == targetChannels) return data;

        int samples = data.Length / currentChannels;
        float[] newData = new float[samples * targetChannels];

        if (currentChannels == 1 && targetChannels == 2)
        {
            for (int i = 0; i < samples; i++)
            {
                newData[i * 2] = data[i];     // L
                newData[i * 2 + 1] = data[i]; // R
            }
        }
        else if (currentChannels == 2 && targetChannels == 1)
        {
            for (int i = 0; i < samples; i++)
            {
                newData[i] = (data[i * 2] + data[i * 2 + 1]) / 2f;
            }
        }
        else
        {
            return data;
        }
        return newData;
    }

    private float[] CombineAudioData(float[] data1, int ch1, float[] data2, int ch2, CombineType type, int targetChannels)
    {
        float[] stdData1 = StandardizeChannels(data1, ch1, targetChannels);
        float[] stdData2 = StandardizeChannels(data2, ch2, targetChannels);

        if (type == CombineType.Append)
        {
            float[] combined = new float[stdData1.Length + stdData2.Length];
            Array.Copy(stdData1, 0, combined, 0, stdData1.Length);
            Array.Copy(stdData2, 0, combined, stdData1.Length, stdData2.Length);
            return combined;
        }
        else // Mix
        {
            int maxLength = Math.Max(stdData1.Length, stdData2.Length);
            float[] combined = new float[maxLength];

            for (int i = 0; i < maxLength; i++)
            {
                float v1 = i < stdData1.Length ? stdData1[i] : 0f;
                float v2 = i < stdData2.Length ? stdData2[i] : 0f;
                
                combined[i] = Mathf.Clamp(v1 + v2, -1f, 1f);
            }
            return combined;
        }
    }

    private void PlayPreview()
    {
        StopPreview();

        if (currentMode == AppMode.TrimSingle)
        {
            if (sourceClip1 == null) return;
            float[] data = GetTrimmedData(sourceClip1, startTime1, endTime1, out int ch, out int freq);
            if (data == null)
            {
                Debug.LogError("Không lấy được dữ liệu audio. Hãy kiểm tra Import Settings.");
                return;
            }
            CreateAndPlayPreview(data, ch, freq, sourceClip1.name);
        }
        else
        {
            if (sourceClip1 == null || sourceClip2 == null) return;
            float[] data1 = GetTrimmedData(sourceClip1, startTime1, endTime1, out int ch1, out int freq1);
            float[] data2 = GetTrimmedData(sourceClip2, startTime2, endTime2, out int ch2, out int freq2);

            if (data1 == null || data2 == null)
            {
                Debug.LogError("Không lấy được dữ liệu audio từ một trong hai file.");
                return;
            }

            int finalChannels = Math.Max(ch1, ch2);
            int finalFreq = Math.Max(freq1, freq2);

            if (freq1 != freq2)
            {
                Debug.LogWarning("Hai file có tần số lấy mẫu (frequency) khác nhau. File kết quả sẽ dùng tần số cao hơn, tốc độ có thể bị ảnh hưởng.");
            }

            float[] combinedData = CombineAudioData(data1, ch1, data2, ch2, combineType, finalChannels);
            CreateAndPlayPreview(combinedData, finalChannels, finalFreq, "Combined");
        }
    }

    private void CreateAndPlayPreview(float[] data, int channels, int freq, string clipName)
    {
        if (previewClip != null)
        {
            DestroyImmediate(previewClip);
            previewClip = null;
        }

        int previewSamples = data.Length / channels;
        if (previewSamples <= 0) return;

        previewClip = AudioClip.Create("Preview_" + clipName, previewSamples, channels, freq, false);
        previewClip.SetData(data, 0);

        PlayClipInternal(previewClip);
    }

    private void StopPreview()
    {
        StopAllClipsInternal();
    }

    private void TrimAndSaveAudio()
    {
        float[] finalData = null;
        int finalChannels = 1;
        int finalFreq = 44100;
        string defaultName = "Audio_Result";

        if (currentMode == AppMode.TrimSingle)
        {
            if (sourceClip1 == null)
            {
                EditorUtility.DisplayDialog("Lỗi", "Chưa chọn AudioClip!", "OK");
                return;
            }

            finalData = GetTrimmedData(sourceClip1, startTime1, endTime1, out finalChannels, out finalFreq);
            if (finalData == null)
            {
                EditorUtility.DisplayDialog("Lỗi", "Không thể lấy dữ liệu audio. Kiểm tra Import Settings.", "OK");
                return;
            }
            defaultName = sourceClip1.name + "_Trimmed";
        }
        else
        {
            if (sourceClip1 == null || sourceClip2 == null)
            {
                EditorUtility.DisplayDialog("Lỗi", "Chưa chọn đủ 2 AudioClip!", "OK");
                return;
            }

            float[] data1 = GetTrimmedData(sourceClip1, startTime1, endTime1, out int ch1, out int freq1);
            float[] data2 = GetTrimmedData(sourceClip2, startTime2, endTime2, out int ch2, out int freq2);

            if (data1 == null || data2 == null)
            {
                EditorUtility.DisplayDialog("Lỗi", "Không thể lấy dữ liệu audio. Kiểm tra Import Settings.", "OK");
                return;
            }

            finalChannels = Math.Max(ch1, ch2);
            finalFreq = Math.Max(freq1, freq2);
            finalData = CombineAudioData(data1, ch1, data2, ch2, combineType, finalChannels);
            defaultName = sourceClip1.name + "_Combined";
        }

        if (finalData == null || finalData.Length == 0)
        {
            EditorUtility.DisplayDialog("Lỗi", "Không có dữ liệu audio để lưu!", "OK");
            return;
        }

        int finalSamples = finalData.Length / finalChannels;
        AudioClip resultClip = AudioClip.Create(defaultName, finalSamples, finalChannels, finalFreq, false);
        resultClip.SetData(finalData, 0);

        string path = EditorUtility.SaveFilePanelInProject("Lưu Audio mới", defaultName, "wav", "Lưu đoạn audio kết quả");

        if (!string.IsNullOrEmpty(path))
        {
            SaveAsWav(path, resultClip);
            AssetDatabase.Refresh();
            EditorUtility.DisplayDialog("Thành công", "Đã lưu audio tại:\n" + path, "OK");
        }

        DestroyImmediate(resultClip);
    }
    private void SaveAsWav(string filepath, AudioClip clip)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(filepath));

        using (FileStream fileStream = new FileStream(filepath, FileMode.Create))
        using (BinaryWriter writer = new BinaryWriter(fileStream))
        {
            float[] samples = new float[clip.samples * clip.channels];
            clip.GetData(samples, 0);

            short[] intData = new short[samples.Length];

            for (int i = 0; i < samples.Length; i++)
            {
                float clampedSample = Mathf.Clamp(samples[i], -1f, 1f);
                intData[i] = (short)(clampedSample * short.MaxValue);
            }

            int byteRate = clip.frequency * clip.channels * 2;
            int subChunk2Size = intData.Length * 2;
            int chunkSize = 36 + subChunk2Size;

            writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
            writer.Write(chunkSize);
            writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVE"));

            writer.Write(System.Text.Encoding.ASCII.GetBytes("fmt "));
            writer.Write(16);
            writer.Write((short)1);
            writer.Write((short)clip.channels);
            writer.Write(clip.frequency);
            writer.Write(byteRate);
            writer.Write((short)(clip.channels * 2));
            writer.Write((short)16);

            writer.Write(System.Text.Encoding.ASCII.GetBytes("data"));
            writer.Write(subChunk2Size);

            foreach (short dataPoint in intData)
            {
                writer.Write(dataPoint);
            }
        }
    }

    private void PlayClipInternal(AudioClip clip)
    {
        if (clip == null) return;

        Assembly unityEditorAssembly = typeof(AudioImporter).Assembly;
        Type audioUtilClass = unityEditorAssembly.GetType("UnityEditor.AudioUtil");

        if (audioUtilClass == null)
        {
            Debug.LogError("Không tìm thấy UnityEditor.AudioUtil.");
            return;
        }

        BindingFlags flags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

        MethodInfo method = audioUtilClass.GetMethod(
            "PlayPreviewClip",
            flags,
            null,
            new Type[] { typeof(AudioClip), typeof(int), typeof(bool) },
            null
        );

        if (method != null)
        {
            method.Invoke(null, new object[] { clip, 0, false });
            return;
        }

        method = audioUtilClass.GetMethod(
            "PlayClip",
            flags,
            null,
            new Type[] { typeof(AudioClip), typeof(int), typeof(bool) },
            null
        );

        if (method != null)
        {
            method.Invoke(null, new object[] { clip, 0, false });
            return;
        }

        method = audioUtilClass.GetMethod(
            "PlayClip",
            flags,
            null,
            new Type[] { typeof(AudioClip) },
            null
        );

        if (method != null)
        {
            method.Invoke(null, new object[] { clip });
            return;
        }

        Debug.LogError("Không tìm thấy hàm PlayPreviewClip hoặc PlayClip trong UnityEditor.AudioUtil.");
    }

    private void StopAllClipsInternal()
    {
        Assembly unityEditorAssembly = typeof(AudioImporter).Assembly;
        Type audioUtilClass = unityEditorAssembly.GetType("UnityEditor.AudioUtil");

        if (audioUtilClass == null)
        {
            Debug.LogError("Không tìm thấy UnityEditor.AudioUtil.");
            return;
        }

        BindingFlags flags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

        MethodInfo method = audioUtilClass.GetMethod("StopAllPreviewClips", flags);

        if (method != null)
        {
            method.Invoke(null, null);
            return;
        }

        method = audioUtilClass.GetMethod("StopAllClips", flags);

        if (method != null)
        {
            method.Invoke(null, null);
            return;
        }

        Debug.LogError("Không tìm thấy hàm StopAllPreviewClips hoặc StopAllClips trong UnityEditor.AudioUtil.");
    }
}