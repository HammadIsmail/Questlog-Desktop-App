using System;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
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
/// Uses NAudio for PCM capture and System.Net.WebSockets.ClientWebSocket
/// for the AssemblyAI Streaming v3 WebSocket — no extra WebSocket NuGet needed.
/// </summary>
public class VoiceService : IDisposable
{
    private readonly ApiClient _apiClient;

    private ClientWebSocket? _socket;
    private WaveInEvent? _waveIn;
    private CancellationTokenSource? _cts;
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
            // 1. Obtain short-lived token from our backend (keeps master key safe)
            var token = await _apiClient.GetVoiceTokenAsync();
            if (string.IsNullOrWhiteSpace(token))
            {
                State = VoiceState.Error;
                ErrorOccurred?.Invoke("Failed to obtain voice token. Is the backend running?");
                return;
            }

            // 2. Open WebSocket to AssemblyAI Streaming v3
            _socket = new ClientWebSocket();
            var wsUri = new Uri($"wss://streaming.assemblyai.com/v3/ws?token={token}&sample_rate={SampleRate}&encoding=pcm_s16le");
            await _socket.ConnectAsync(wsUri, _cts.Token);

            State = VoiceState.Listening;

            // 3. Start receive loop (runs on background task)
            _ = Task.Run(() => ReceiveLoopAsync(_cts.Token), _cts.Token);

            // 4. Start NAudio microphone capture
            _waveIn = new WaveInEvent
            {
                WaveFormat = new WaveFormat(SampleRate, BitsPerSample, Channels),
                BufferMilliseconds = 100
            };
            _waveIn.DataAvailable += OnAudioDataAvailable;
            _waveIn.StartRecording();
        }
        catch (OperationCanceledException)
        {
            State = VoiceState.Idle;
        }
        catch (Exception ex)
        {
            State = VoiceState.Error;
            ErrorOccurred?.Invoke($"Voice session error: {ex.Message}");
            await CleanupAsync();
        }
    }

    /// <summary>Stop recording and close the WebSocket gracefully.</summary>
    public async Task StopSessionAsync()
    {
        if (!IsActive) return;

        _waveIn?.StopRecording();

        try
        {
            // Send end-of-stream signal per AssemblyAI protocol
            if (_socket?.State == WebSocketState.Open)
            {
                var endMsg = JsonSerializer.Serialize(new { terminate_session = true });
                var bytes = Encoding.UTF8.GetBytes(endMsg);
                await _socket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None);

                // Short wait for final transcript before closing
                await Task.Delay(800);
                await _socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Session ended", CancellationToken.None);
            }
        }
        catch { /* best-effort */ }
        finally
        {
            _cts?.Cancel();
            await CleanupAsync();
            State = VoiceState.Idle;
        }
    }

    private void OnAudioDataAvailable(object? sender, WaveInEventArgs e)
    {
        if (_socket?.State != WebSocketState.Open || _cts?.IsCancellationRequested == true)
            return;

        // Send raw PCM bytes directly (binary frame) — AssemblyAI accepts binary PCM
        var segment = new ArraySegment<byte>(e.Buffer, 0, e.BytesRecorded);
        // Fire-and-forget on the socket (must be sequential; use a queue in production)
        _ = _socket.SendAsync(segment, WebSocketMessageType.Binary, true, _cts!.Token);
    }

    private async Task ReceiveLoopAsync(CancellationToken ct)
    {
        var buffer = new byte[8192];
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

            // AssemblyAI v3 sends { "type": "partial_transcript" | "final_transcript", "text": "…" }
            if (!root.TryGetProperty("type", out var typeProp)) return;
            var type = typeProp.GetString();
            var text = root.TryGetProperty("text", out var textProp) ? textProp.GetString() ?? "" : "";

            if (type == "partial_transcript" && !string.IsNullOrWhiteSpace(text))
            {
                State = VoiceState.Listening;
                TranscriptPartialReceived?.Invoke(text);
            }
            else if (type == "final_transcript" && !string.IsNullOrWhiteSpace(text))
            {
                State = VoiceState.Processing;
                TranscriptFinalReceived?.Invoke(text);
            }
        }
        catch { /* ignore malformed frames */ }
    }

    private async Task CleanupAsync()
    {
        if (_waveIn != null)
        {
            _waveIn.DataAvailable -= OnAudioDataAvailable;
            _waveIn.Dispose();
            _waveIn = null;
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
