using System;
using System.Diagnostics;
using System.Speech.Synthesis;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Questlog.Services;

/// <summary>
/// Speaks assistant replies using the Windows built-in TTS engine (System.Speech).
/// Configured to explicitly bind to the default audio output device and play asynchronously.
/// </summary>
public class TtsService : IDisposable
{
    private readonly SpeechSynthesizer _synth;
    private bool _disposed;
    private bool _isInitialized;

    public TtsService(System.Net.Http.HttpClient? _ = null)
    {
        _synth = new SpeechSynthesizer();
        try
        {
            // Explicitly route to default Windows audio playback device (speakers/headphones)
            _synth.SetOutputToDefaultAudioDevice();

            // Enumerate and select the most natural female voice available
            VoiceInfo? bestVoice = null;
            var installedVoices = _synth.GetInstalledVoices();
            Debug.WriteLine($"[TTS] Found {installedVoices.Count} installed voice(s):");

            foreach (var voice in installedVoices)
            {
                if (!voice.Enabled) continue;
                var info = voice.VoiceInfo;
                Debug.WriteLine($"[TTS] - Voice: {info.Name} ({info.Culture}, {info.Gender}, {info.Age})");

                if (bestVoice == null) bestVoice = info;
                if (info.Gender == VoiceGender.Female && info.Age != VoiceAge.Child)
                {
                    bestVoice = info;
                    break;
                }
            }

            if (bestVoice != null)
            {
                _synth.SelectVoice(bestVoice.Name);
                Debug.WriteLine($"[TTS] Selected voice: {bestVoice.Name}");
            }

            _synth.Rate = 1;     // -10 slowest to 10 fastest; 1 = conversational
            _synth.Volume = 100;  // 100% volume
            _isInitialized = true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[TTS] Initialization warning: {ex.Message}");
        }
    }

    /// <summary>
    /// Speak text asynchronously. Cancels any in-progress speech first.
    /// </summary>
    public Task SpeakAsync(string text, string voice = "nova")
    {
        if (_disposed || !_isInitialized || string.IsNullOrWhiteSpace(text))
            return Task.CompletedTask;

        // Clean out markdown and emojis so speech synthesizer pronounces cleanly
        var clean = Regex.Replace(text, @"[*_`#~\[\]\(\)>]", " ");
        clean = Regex.Replace(clean, @"[^\w\s,\.!?'\-]", " ");
        clean = Regex.Replace(clean, @"\s{2,}", " ").Trim();

        if (clean.Length == 0) return Task.CompletedTask;

        try
        {
            Debug.WriteLine($"[TTS] Speaking ({clean.Length} chars): {clean}");
            _synth.SpeakAsyncCancelAll();
            _synth.SpeakAsync(clean);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[TTS] Speak error: {ex.Message}");
        }

        return Task.CompletedTask;
    }

    /// <summary>Stop any currently playing speech immediately.</summary>
    public void Stop()
    {
        if (_disposed || !_isInitialized) return;
        try
        {
            _synth.SpeakAsyncCancelAll();
        }
        catch { }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        try
        {
            _synth.SpeakAsyncCancelAll();
            _synth.Dispose();
        }
        catch { }
    }
}
