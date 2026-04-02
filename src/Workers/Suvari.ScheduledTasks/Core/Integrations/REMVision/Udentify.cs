using Newtonsoft.Json;
using System.Globalization;
using System.Net.Http.Headers;

namespace Suvari.ScheduledTasks.Core.Integrations.REMVision;

public class Udentify
{
    private static readonly HttpClient _http = new HttpClient { Timeout = TimeSpan.FromSeconds(300) };
    private const string _apiBaseUrl = "https://api.udentify.co/";

    private readonly int    _brandId;
    private readonly string _accessToken;

    public Udentify(string username, string password, int brandId)
    {
        _brandId = brandId;

        using var req = new HttpRequestMessage(HttpMethod.Post, $"{_apiBaseUrl}Token")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "password",
                ["username"]   = username,
                ["password"]   = password
            })
        };

        using var resp = _http.Send(req);
        resp.EnsureSuccessStatusCode();

        _accessToken = JsonConvert.DeserializeObject<TokenResponse>(ReadContent(resp)).Access_token;
    }

    public List<StoreResponse> GetStores()
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, $"{_apiBaseUrl}Brand/{_brandId}/Stores");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);

        using var resp = _http.Send(req);
        resp.EnsureSuccessStatusCode();

        return JsonConvert.DeserializeObject<BaseResponse<List<StoreResponse>>>(ReadContent(resp)).Data;
    }

    public LineEnteranceCountResponse GetLineEntranceCount(int storeId, DateTime startDate, DateTime endDate, int tzOffset)
    {
        var sdate = startDate.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
        var edate = endDate.ToString("dd/MM/yyyy",   CultureInfo.InvariantCulture);
        var stime = startDate.ToString("HH:mm",      CultureInfo.InvariantCulture);
        var etime = endDate.ToString("HH:mm",         CultureInfo.InvariantCulture);
        var url   = $"{_apiBaseUrl}Store/{storeId}/LineEntranceCount?sdate={sdate}&edate={edate}&stime={stime}&etime={etime}&filter=0&tzoffset={tzOffset}";

        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);

        using var resp = _http.Send(req);
        resp.EnsureSuccessStatusCode();

        return JsonConvert.DeserializeObject<LineEnteranceCountResponse>(ReadContent(resp));
    }

    private static string ReadContent(HttpResponseMessage response)
    {
        using var stream = response.Content.ReadAsStream();
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
