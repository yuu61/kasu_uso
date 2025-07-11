using kasu_uso.Services;
using Microsoft.AspNetCore.Mvc;

namespace kasu_uso.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MetricsInfoController : ControllerBase
{
    private readonly MetricsService _metricsService;

    public MetricsInfoController(MetricsService metricsService)
    {
        _metricsService = metricsService;
    }

    [HttpGet]
    public IActionResult GetMetricsInfo()
    {
        var metricsInfo = _metricsService.GetMetricsInfo();
        return Ok(new
        {
            message = "以下のPrometheusメトリクスが利用可能です。/metricsエンドポイントで確認できます。",
            metrics = metricsInfo,
            endpoints = new
            {
                prometheus_metrics = "/metrics",
                metrics_info = "/api/metricsinfo"
            }
        });
    }
}