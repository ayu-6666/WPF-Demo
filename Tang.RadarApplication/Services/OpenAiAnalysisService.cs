using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Tang.RadarApplication.Models;
using System.Collections.Generic;

namespace Tang.RadarApplication.Services
{
    public sealed class OpenAiAnalysisService
    {
        private readonly HttpClient client = new HttpClient();
        public async Task<string> AnalyzeAsync(IEnumerable<RadarTarget> targets)
        {
            var key = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
            if (string.IsNullOrWhiteSpace(key)) return "未配置 OPENAI_API_KEY，当前显示本地雷达数据。";
            var rows = string.Join("\n", System.Linq.Enumerable.Select(targets, x => $"{x.Id}: distance={x.DistanceKm:F2}km azimuth={x.AzimuthDegree:F1}deg speed={x.SpeedKmh:F1}km/h"));
            var body = JsonConvert.SerializeObject(new { model = "gpt-4.1-mini", input = "请用中文简短分析以下雷达目标态势，只做辅助说明，不做安全决策：\n" + rows });
            using (var request = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/responses"))
            { request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", key); request.Content = new StringContent(body, Encoding.UTF8, "application/json"); using (var response = await client.SendAsync(request).ConfigureAwait(false)) { var text = await response.Content.ReadAsStringAsync().ConfigureAwait(false); return response.IsSuccessStatusCode ? text : "OpenAI 请求失败：" + response.StatusCode; } }
        }
    }
}
