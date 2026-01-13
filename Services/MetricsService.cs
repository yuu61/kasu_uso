using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Prometheus;

namespace kasu_uso.Services;

public class MetricsService : IDisposable
{
    private const string DefaultMetricsFilePath = "metrics_state.json";
    private readonly string _metricsFilePath;
    private readonly object _persistLock = new();
    private readonly ILogger<MetricsService> _logger;
    private readonly Timer _autoSaveTimer;
    private bool _disposed;

    // ボット/クローラー判定用の正規表現パターン
    private static readonly Regex BotPattern = new(
        @"(bot|crawl|spider|slurp|bingpreview|facebookexternalhit|linkedinbot|twitterbot|whatsapp|telegrambot|discordbot|applebot|yandex|baidu|duckduck|sogou|exabot|ia_archiver|googlebot|chrome-lighthouse|headless|phantomjs|selenium|puppeteer|playwright)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>
    /// User-Agentがボット/クローラーかどうかを判定する
    /// </summary>
    public static bool IsBot(string? userAgent)
    {
        if (string.IsNullOrWhiteSpace(userAgent))
            return true; // User-Agentがない場合はボットとみなす

        return BotPattern.IsMatch(userAgent);
    }

    // セッション関連メトリクス
    private readonly Gauge _activeSessionsGauge = Metrics
        .CreateGauge("kasu_uso_active_sessions_total", "現在のアクティブセッション数");

    private readonly Counter _totalSessionsCounter = Metrics
        .CreateCounter("kasu_uso_sessions_total", "総セッション数");

    // OpenAI API関連メトリクス
    private readonly Counter _openaiApiCallsCounter = Metrics
        .CreateCounter("kasu_uso_openai_api_calls_total", "OpenAI API呼び出し回数", new[] { "status", "model" });

    private readonly Histogram _openaiApiDuration = Metrics
        .CreateHistogram("kasu_uso_openai_api_duration_seconds", "OpenAI API応答時間",
            new HistogramConfiguration
            {
                Buckets = Histogram.LinearBuckets(0.1, 0.1, 20) // 0.1秒から2秒まで0.1秒刻み
            });

    private readonly Counter _openaiApiErrorsCounter = Metrics
        .CreateCounter("kasu_uso_openai_api_errors_total", "OpenAI APIエラー回数", new[] { "error_type" });

    // ユーザー操作関連メトリクス
    private readonly Counter _messagesSentCounter = Metrics
        .CreateCounter("kasu_uso_messages_sent_total", "送信されたメッセージ数");

    private readonly Counter _generateButtonClicksCounter = Metrics
        .CreateCounter("kasu_uso_generate_button_clicks_total", "生成ボタンクリック回数");

    // アプリケーションパフォーマンス関連メトリクス
    private readonly Histogram _pageLoadTime = Metrics
        .CreateHistogram("kasu_uso_page_load_duration_seconds", "ページ読み込み時間");

    private readonly Counter _errorCounter = Metrics
        .CreateCounter("kasu_uso_errors_total", "アプリケーションエラー回数", new[] { "error_type" });

    // ビジネスメトリクス
    private readonly Counter _monthSelectionCounter = Metrics
        .CreateCounter("kasu_uso_month_selections_total", "月選択回数", new[] { "month" });

    private readonly Counter _shareButtonClicksCounter = Metrics
        .CreateCounter("kasu_uso_share_button_clicks_total", "シェアボタンクリック回数", new[] { "platform" });

    // セッション管理
    private readonly HashSet<string> _activeSessions = new();
    private readonly object _sessionLock = new();

    // 永続化用の内部カウンター
    private long _totalSessionsValue;
    private long _messagesSentValue;
    private long _generateButtonClicksValue;
    private readonly Dictionary<string, long> _openaiApiCallsValues = new();
    private readonly Dictionary<string, long> _openaiApiErrorsValues = new();
    private readonly Dictionary<string, long> _errorValues = new();
    private readonly Dictionary<string, long> _monthSelectionValues = new();
    private readonly Dictionary<string, long> _shareButtonClicksValues = new();

    // 許可されたエラータイプのリスト（ラベルカーディナリティ制御用）
    private static readonly HashSet<string> AllowedErrorTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "api_key_load_error",
        "missing_api_key",
        "http_400", "http_401", "http_403", "http_404", "http_429", "http_500", "http_502", "http_503",
        "timeout",
        "unknown_error",
        "validation_error",
        "network_error"
    };

    public MetricsService(ILogger<MetricsService> logger, IConfiguration configuration)
    {
        _logger = logger;

        // 設定からファイルパスを読み込み、未設定の場合はデフォルト値を使用
        var configPath = configuration["Metrics:StatePath"];
        if (!string.IsNullOrWhiteSpace(configPath))
        {
            _metricsFilePath = Path.IsPathRooted(configPath)
                ? configPath
                : Path.Combine(AppContext.BaseDirectory, configPath);
        }
        else
        {
            _metricsFilePath = Path.Combine(AppContext.BaseDirectory, DefaultMetricsFilePath);
        }

        LoadState();

        // 5分ごとに自動保存（異常終了時のデータ損失を軽減）
        _autoSaveTimer = new Timer(_ => SaveStateInternal(), null, TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));
    }

    // セッション関連メソッド
    public void IncrementActiveSession(string sessionId)
    {
        lock (_sessionLock)
        {
            if (_activeSessions.Add(sessionId))
            {
                _activeSessionsGauge.Set(_activeSessions.Count);
                _totalSessionsCounter.Inc();
                Interlocked.Increment(ref _totalSessionsValue);
            }
        }
    }

    public void DecrementActiveSession(string sessionId)
    {
        lock (_sessionLock)
        {
            if (_activeSessions.Remove(sessionId))
            {
                _activeSessionsGauge.Set(_activeSessions.Count);
            }
        }
    }

    // OpenAI API関連メソッド
    public void RecordOpenAiApiCall(string status, string model, double durationSeconds)
    {
        _openaiApiCallsCounter.WithLabels(status, model).Inc();
        _openaiApiDuration.Observe(durationSeconds);

        var key = $"{status}|{model}";
        lock (_persistLock)
        {
            _openaiApiCallsValues.TryGetValue(key, out var current);
            _openaiApiCallsValues[key] = current + 1;
        }
    }

    public void RecordOpenAiApiError(string errorType)
    {
        _openaiApiErrorsCounter.WithLabels(errorType).Inc();

        lock (_persistLock)
        {
            _openaiApiErrorsValues.TryGetValue(errorType, out var current);
            _openaiApiErrorsValues[errorType] = current + 1;
        }
    }

    // ユーザー操作関連メソッド
    public void IncrementMessagesSent()
    {
        _messagesSentCounter.Inc();
        Interlocked.Increment(ref _messagesSentValue);
    }

    public void IncrementGenerateButtonClicks()
    {
        _generateButtonClicksCounter.Inc();
        Interlocked.Increment(ref _generateButtonClicksValue);
    }

    // アプリケーションパフォーマンス関連メソッド
    public void RecordPageLoadTime(double durationSeconds)
    {
        _pageLoadTime.Observe(durationSeconds);
    }

    public void RecordError(string errorType)
    {
        // ラベルカーディナリティ制御: 許可されたエラータイプのみ記録
        var normalizedErrorType = AllowedErrorTypes.Contains(errorType) ? errorType : "unknown_error";

        _errorCounter.WithLabels(normalizedErrorType).Inc();

        lock (_persistLock)
        {
            _errorValues.TryGetValue(normalizedErrorType, out var current);
            _errorValues[normalizedErrorType] = current + 1;
        }
    }

    // ビジネスメトリクス関連メソッド
    public void RecordMonthSelection(string month)
    {
        _monthSelectionCounter.WithLabels(month).Inc();

        lock (_persistLock)
        {
            _monthSelectionValues.TryGetValue(month, out var current);
            _monthSelectionValues[month] = current + 1;
        }
    }

    public void RecordShareButtonClick(string platform)
    {
        _shareButtonClicksCounter.WithLabels(platform).Inc();

        lock (_persistLock)
        {
            _shareButtonClicksValues.TryGetValue(platform, out var current);
            _shareButtonClicksValues[platform] = current + 1;
        }
    }

    // 利用可能なメトリクス情報を取得
    public Dictionary<string, string> GetMetricsInfo()
    {
        return new Dictionary<string, string>
        {
            ["セッション"] = "kasu_uso_active_sessions_total, kasu_uso_sessions_total",
            ["OpenAI API"] = "kasu_uso_openai_api_calls_total, kasu_uso_openai_api_duration_seconds, kasu_uso_openai_api_errors_total",
            ["ユーザー操作"] = "kasu_uso_messages_sent_total, kasu_uso_generate_button_clicks_total",
            ["パフォーマンス"] = "kasu_uso_page_load_duration_seconds, kasu_uso_errors_total",
            ["ビジネス"] = "kasu_uso_month_selections_total, kasu_uso_share_button_clicks_total"
        };
    }

    /// <summary>
    /// 保存されたメトリクス状態をファイルから読み込み、カウンターを復元する
    /// </summary>
    private void LoadState()
    {
        try
        {
            if (!File.Exists(_metricsFilePath))
            {
                _logger.LogInformation("メトリクス状態ファイルが存在しません: {Path}", _metricsFilePath);
                return;
            }

            var json = File.ReadAllText(_metricsFilePath);
            var state = JsonSerializer.Deserialize<MetricsState>(json);

            if (state == null)
                return;

            // 単純なカウンターを復元（Inc(value)で一括増加）
            _totalSessionsValue = state.TotalSessions;
            if (state.TotalSessions > 0)
                _totalSessionsCounter.Inc(state.TotalSessions);

            _messagesSentValue = state.MessagesSent;
            if (state.MessagesSent > 0)
                _messagesSentCounter.Inc(state.MessagesSent);

            _generateButtonClicksValue = state.GenerateButtonClicks;
            if (state.GenerateButtonClicks > 0)
                _generateButtonClicksCounter.Inc(state.GenerateButtonClicks);

            // ラベル付きカウンターを復元
            if (state.OpenAiApiCalls != null)
            {
                foreach (var kvp in state.OpenAiApiCalls)
                {
                    _openaiApiCallsValues[kvp.Key] = kvp.Value;
                    var parts = kvp.Key.Split('|');
                    if (parts.Length == 2 && kvp.Value > 0)
                        _openaiApiCallsCounter.WithLabels(parts[0], parts[1]).Inc(kvp.Value);
                }
            }

            if (state.OpenAiApiErrors != null)
            {
                foreach (var kvp in state.OpenAiApiErrors)
                {
                    _openaiApiErrorsValues[kvp.Key] = kvp.Value;
                    if (kvp.Value > 0)
                        _openaiApiErrorsCounter.WithLabels(kvp.Key).Inc(kvp.Value);
                }
            }

            if (state.Errors != null)
            {
                foreach (var kvp in state.Errors)
                {
                    _errorValues[kvp.Key] = kvp.Value;
                    if (kvp.Value > 0)
                        _errorCounter.WithLabels(kvp.Key).Inc(kvp.Value);
                }
            }

            if (state.MonthSelections != null)
            {
                foreach (var kvp in state.MonthSelections)
                {
                    _monthSelectionValues[kvp.Key] = kvp.Value;
                    if (kvp.Value > 0)
                        _monthSelectionCounter.WithLabels(kvp.Key).Inc(kvp.Value);
                }
            }

            if (state.ShareButtonClicks != null)
            {
                foreach (var kvp in state.ShareButtonClicks)
                {
                    _shareButtonClicksValues[kvp.Key] = kvp.Value;
                    if (kvp.Value > 0)
                        _shareButtonClicksCounter.WithLabels(kvp.Key).Inc(kvp.Value);
                }
            }

            _logger.LogInformation("メトリクス状態を復元しました: {Path}", _metricsFilePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "メトリクス状態の読み込みに失敗しました: {Path}", _metricsFilePath);
        }
    }

    /// <summary>
    /// 現在のメトリクス状態をファイルに保存する（公開API）
    /// </summary>
    public void SaveState()
    {
        SaveStateInternal();
        _logger.LogInformation("メトリクス状態を保存しました: {Path}", _metricsFilePath);
    }

    /// <summary>
    /// 現在のメトリクス状態をファイルに保存する（内部実装、定期保存用）
    /// </summary>
    private void SaveStateInternal()
    {
        if (_disposed) return;

        try
        {
            MetricsState state;
            lock (_persistLock)
            {
                state = new MetricsState
                {
                    TotalSessions = _totalSessionsValue,
                    MessagesSent = _messagesSentValue,
                    GenerateButtonClicks = _generateButtonClicksValue,
                    OpenAiApiCalls = new Dictionary<string, long>(_openaiApiCallsValues),
                    OpenAiApiErrors = new Dictionary<string, long>(_openaiApiErrorsValues),
                    Errors = new Dictionary<string, long>(_errorValues),
                    MonthSelections = new Dictionary<string, long>(_monthSelectionValues),
                    ShareButtonClicks = new Dictionary<string, long>(_shareButtonClicksValues)
                };
            }

            var json = JsonSerializer.Serialize(state, new JsonSerializerOptions { WriteIndented = true });

            // 原子的書き込み: 一時ファイルに書き込んでからリネーム
            var tempPath = _metricsFilePath + ".tmp";
            File.WriteAllText(tempPath, json);
            File.Move(tempPath, _metricsFilePath, overwrite: true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "メトリクス状態の保存に失敗しました: {Path}", _metricsFilePath);
        }
    }

    /// <summary>
    /// リソースを解放する
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _autoSaveTimer.Dispose();

        // 終了時に最終保存
        try
        {
            SaveStateInternal();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "終了時のメトリクス保存に失敗しました");
        }
    }
}

/// <summary>
/// メトリクス状態の永続化用クラス
/// </summary>
public class MetricsState
{
    public long TotalSessions { get; set; }
    public long MessagesSent { get; set; }
    public long GenerateButtonClicks { get; set; }
    public Dictionary<string, long>? OpenAiApiCalls { get; set; }
    public Dictionary<string, long>? OpenAiApiErrors { get; set; }
    public Dictionary<string, long>? Errors { get; set; }
    public Dictionary<string, long>? MonthSelections { get; set; }
    public Dictionary<string, long>? ShareButtonClicks { get; set; }
}