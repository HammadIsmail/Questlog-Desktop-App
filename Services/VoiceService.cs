using System;
using System.Diagnostics;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using NAudio.Wave;

namespace Questlog.Services;

/// <summary>
/// State of the voice session.
/// </summary>
public enum VoiceState
{
    Idle,
    Connecting,
    Listening,
    Processing,
    Error
}

/// <summary>
/// Manages microphone capture and AssemblyAI streaming transcription.
/// Uses NAudio for PCM capture, a Channel-based sequential send loop for reliable WebSocket streaming,
/// and System.Net.WebSockets.ClientWebSocket for AssemblyAI Streaming v3.
/// </summary>
public class VoiceService : IDisposable
{
    private readonly ApiClient _apiClient;

    private ClientWebSocket? _socket;
    private WaveInEvent? _waveIn;
    private CancellationTokenSource? _cts;
    private Channel<byte[]>? _audioChannel;
    private VoiceState _state = VoiceState.Idle;

    // 16 kHz, 16-bit, mono — required by AssemblyAI streaming v3
    private const int SampleRate = 16000;
    private const int BitsPerSample = 16;
    private const int Channels = 1;

    public event Action<string>? TranscriptPartialReceived;
    public event Action<string>? TranscriptFinalReceived;
    public event Action<VoiceState>? StateChanged;
    public event Action<string>? ErrorOccurred;

    public VoiceState State
    {
        get => _state;
        private set
        {
            _state = value;
            StateChanged?.Invoke(value);
        }
    }

    public bool IsActive => _state is VoiceState.Connecting or VoiceState.Listening or VoiceState.Processing;

    public VoiceService(ApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    /// <summary>
    /// Fetch a short-lived token, open the AssemblyAI WebSocket, and start streaming mic audio.
    /// </summary>
    public async Task StartSessionAsync()
    {
        if (IsActive) return;

        State = VoiceState.Connecting;
        _cts = new CancellationTokenSource();

        try
        {
            // Verify microphone is available
            if (WaveInEvent.DeviceCount == 0)
            {
                State = VoiceState.Error;
                ErrorOccurred?.Invoke("No recording microphone detected. Please connect a microphone.");
                return;
            }

            Debug.WriteLine($"[VoiceService] Found {WaveInEvent.DeviceCount} audio input device(s). Using default input.");

            // 1. Obtain short-lived token from backend (keeps primary API key secure)
            var token = await _apiClient.GetVoiceTokenAsync();
            if (string.IsNullOrWhiteSpace(token))
            {
                State = VoiceState.Error;
                ErrorOccurred?.Invoke("Failed to obtain voice token. Is the backend running?");
                return;
            }

            // 2. Open WebSocket to AssemblyAI Streaming v3
            _socket = new ClientWebSocket();
            var wsUri = new Uri($"wss://streaming.assemblyai.com/v3/ws?token={token}&sample_rate={SampleRate}&encoding=pcm_s16le&formatted_finals=true");
            Debug.WriteLine($"[VoiceService] Connecting to AssemblyAI WebSocket: {wsUri}");
            await _socket.ConnectAsync(wsUri, _cts.Token);
            Debug.WriteLine("[VoiceService] Connected to AssemblyAI Streaming v3!");

            State = VoiceState.Listening;

            // 3. Create audio queue for sequential sending (prevents socket race conditions)
            _audioChannel = Channel.CreateUnbounded<byte[]>(new UnboundedChannelOptions
            {
                SingleReader = true,
                SingleWriter = true
            });

            // 4. Start send & receive background loops
            _ = Task.Run(() => ReceiveLoopAsync(_cts.Token), _cts.Token);
            _ = Task.Run(() => SendAudioLoopAsync(_cts.Token), _cts.Token);

            // 5. Start NAudio microphone capture
            _waveIn = new WaveInEvent
            {
                WaveFormat = new WaveFormat(SampleRate, BitsPerSample, Channels),
                BufferMilliseconds = 100
            };
            _waveIn.DataAvailable += OnAudioDataAvailable;
            _waveIn.StartRecording();
            Debug.WriteLine("[VoiceService] Microphone recording started.");
        }
        catch (OperationCanceledException)
        {
            State = VoiceState.Idle;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[VoiceService Error] {ex}");
            State = VoiceState.Error;
            ErrorOccurred?.Invoke($"Voice session error: {ex.Message}");
            await CleanupAsync();
        }
    }

    /// <summary>Stop recording and close the WebSocket gracefully.</summary>
    public async Task StopSessionAsync()
    {
        if (!IsActive) return;

        try
        {
            _waveIn?.StopRecording();
        }
        catch { }

        try
        {
            // Send terminate session command per AssemblyAI v3 protocol
            if (_socket?.State == WebSocketState.Open)
            {
                var endMsg = JsonSerializer.Serialize(new { type = "Terminate" });
                var bytes = Encoding.UTF8.GetBytes(endMsg);
                await _socket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None);

                // Short delay to allow remaining final transcript to arrive
                await Task.Delay(500);
                await _socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Session ended", CancellationToken.None);
            }
        }
        catch { /* best-effort cleanup */ }
        finally
        {
            _cts?.Cancel();
            await CleanupAsync();
            State = VoiceState.Idle;
        }
    }

    private void OnAudioDataAvailable(object? sender, WaveInEventArgs e)
    {
        if (_audioChannel == null || _cts?.IsCancellationRequested == true || e.BytesRecorded <= 0)
            return;

        // Copy audio buffer to prevent data corruption from NAudio buffer reuse
        var chunk = new byte[e.BytesRecorded];
        Array.Copy(e.Buffer, 0, chunk, 0, e.BytesRecorded);
        _audioChannel.Writer.TryWrite(chunk);
    }

    private async Task SendAudioLoopAsync(CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested && _socket?.State == WebSocketState.Open && _audioChannel != null)
            {
                if (await _audioChannel.Reader.WaitToReadAsync(ct))
                {
                    while (_audioChannel.Reader.TryRead(out var chunk))
                    {
                        if (_socket.State != WebSocketState.Open) break;
                        await _socket.SendAsync(new ArraySegment<byte>(chunk), WebSocketMessageType.Binary, true, ct);
                    }
                }
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            Debug.WriteLine($"[VoiceService Audio Send Error] {ex.Message}");
        }
    }

    private async Task ReceiveLoopAsync(CancellationToken ct)
    {
        var buffer = new byte[16384];
        var sb = new StringBuilder();

        try
        {
            while (_socket?.State == WebSocketState.Open && !ct.IsCancellationRequested)
            {
                sb.Clear();
                WebSocketReceiveResult result;
                do
                {
                    result = await _socket.ReceiveAsync(new ArraySegment<byte>(buffer), ct);
                    if (result.MessageType == WebSocketMessageType.Close) return;
                    sb.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));
                }
                while (!result.EndOfMessage);

                ParseTranscript(sb.ToString());
            }
        }
        catch (OperationCanceledException) { /* expected on stop */ }
        catch (Exception ex)
        {
            Debug.WriteLine($"[VoiceService Receive Error] {ex.Message}");
            State = VoiceState.Error;
            ErrorOccurred?.Invoke($"Receive error: {ex.Message}");
        }
    }

    private void ParseTranscript(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            string? msgType = null;
            if (root.TryGetProperty("type", out var tProp))
                msgType = tProp.GetString();
            else if (root.TryGetProperty("message_type", out var mtProp))
                msgType = mtProp.GetString();

            if (string.IsNullOrEmpty(msgType)) return;

            Debug.WriteLine($"[AssemblyAI WS] Type={msgType}");

            // AssemblyAI v3: SpeechStarted event
            if (msgType.Equals("SpeechStarted", StringComparison.OrdinalIgnoreCase))
            {
                State = VoiceState.Listening;
                return;
            }

            // AssemblyAI v3: Turn event
            if (msgType.Equals("Turn", StringComparison.OrdinalIgnoreCase))
            {
                string transcript = "";
                if (root.TryGetProperty("transcript", out var trProp))
                    transcript = trProp.GetString() ?? "";
                if (string.IsNullOrWhiteSpace(transcript) && root.TryGetProperty("utterance", out var utProp))
                    transcript = utProp.GetString() ?? "";

                if (string.IsNullOrWhiteSpace(transcript)) return;

                bool endOfTurn = root.TryGetProperty("end_of_turn", out var eotProp) && eotProp.GetBoolean();

                if (!endOfTurn)
                {
                    State = VoiceState.Listening;
                    TranscriptPartialReceived?.Invoke(transcript);
                }
                else
                {
                    State = VoiceState.Processing;
                    TranscriptFinalReceived?.Invoke(transcript);
                }
                return;
            }

            // Fallback for legacy v2 or other event formats
            string text = "";
            if (root.TryGetProperty("text", out var textProp))
                text = textProp.GetString() ?? "";
            else if (root.TryGetProperty("transcript", out var trProp2))
                text = trProp2.GetString() ?? "";

            if (msgType.Equals("PartialTranscript", StringComparison.OrdinalIgnoreCase) ||
                msgType.Equals("partial_transcript", StringComparison.OrdinalIgnoreCase))
            {
                if (!string.IsNullOrWhiteSpace(text))
                {
                    State = VoiceState.Listening;
                    TranscriptPartialReceived?.Invoke(text);
                }
            }
            else if (msgType.Equals("FinalTranscript", StringComparison.OrdinalIgnoreCase) ||
                     msgType.Equals("final_transcript", StringComparison.OrdinalIgnoreCase) ||
                     msgType.Equals("TurnComplete", StringComparison.OrdinalIgnoreCase))
            {
                if (!string.IsNullOrWhiteSpace(text))
                {
                    State = VoiceState.Processing;
                    TranscriptFinalReceived?.Invoke(text);
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[AssemblyAI Parse Error] {ex.Message}");
        }
    }

    private async Task CleanupAsync()
    {
        if (_waveIn != null)
        {
            _waveIn.DataAvailable -= OnAudioDataAvailable;
            _waveIn.Dispose();
            _waveIn = null;
        }

        if (_audioChannel != null)
        {
            _audioChannel.Writer.TryComplete();
            _audioChannel = null;
        }

        if (_socket != null)
        {
            _socket.Dispose();
            _socket = null;
        }

        await Task.CompletedTask;
    }

    public void Dispose()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _waveIn?.Dispose();
        _socket?.Dispose();
    }
}
