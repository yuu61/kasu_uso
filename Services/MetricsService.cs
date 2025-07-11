using Prometheus;

namespace kasu_uso.Services;

public class MetricsService
{
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

    // セッション関連メソッド
    public void IncrementActiveSession(string sessionId)
    {
        lock (_sessionLock)
        {
            if (_activeSessions.Add(sessionId))
            {
                _activeSessionsGauge.Set(_activeSessions.Count);
                _totalSessionsCounter.Inc();
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
    }

    public void RecordOpenAiApiError(string errorType)
    {
        _openaiApiErrorsCounter.WithLabels(errorType).Inc();
    }

    // ユーザー操作関連メソッド
    public void IncrementMessagesSent()
    {
        _messagesSentCounter.Inc();
    }

    public void IncrementGenerateButtonClicks()
    {
        _generateButtonClicksCounter.Inc();
    }

    // アプリケーションパフォーマンス関連メソッド
    public void RecordPageLoadTime(double durationSeconds)
    {
        _pageLoadTime.Observe(durationSeconds);
    }

    public void RecordError(string errorType)
    {
        _errorCounter.WithLabels(errorType).Inc();
    }

    // ビジネスメトリクス関連メソッド
    public void RecordMonthSelection(string month)
    {
        _monthSelectionCounter.WithLabels(month).Inc();
    }

    public void RecordShareButtonClick(string platform)
    {
        _shareButtonClicksCounter.WithLabels(platform).Inc();
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
}