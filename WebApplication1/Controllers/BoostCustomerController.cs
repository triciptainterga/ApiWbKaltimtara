using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading.Tasks;

[ApiController]
[Route("api/boost")]
public class BoostCustomerController : ControllerBase
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private string _token = "";

    public BoostCustomerController(IHttpClientFactory httpClientFactory, IConfiguration configuration)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
    }

    [HttpGet("get-customer/{custMobile}")]
    public async Task<IActionResult> GetCustomerByMobile(string custMobile)
    {
        var client = _httpClientFactory.CreateClient();

        if (string.IsNullOrEmpty(_token))
        {
            _token = await GetToken(client);
            if (string.IsNullOrEmpty(_token))
                return Unauthorized("Login gagal.");
        }

        var result = await GetCustomerData(client, _token, custMobile);
        if (result.status == 401)
        {
            _token = await GetToken(client);
            if (string.IsNullOrEmpty(_token))
                return Unauthorized("Login ulang gagal.");

            result = await GetCustomerData(client, _token, custMobile);
        }

        return StatusCode(result.status, result.body);
    }

    [HttpGet("get-customer-email/{custEmail}")]
    public async Task<IActionResult> GetCustomerBycustEmail(string custEmail)
    {
        var client = _httpClientFactory.CreateClient();

        if (string.IsNullOrEmpty(_token))
        {
            _token = await GetToken(client);
            if (string.IsNullOrEmpty(_token))
                return Unauthorized("Login gagal.");
        }

        var result = await GetCustomerData(client, _token, custEmail);
        if (result.status == 401)
        {
            _token = await GetToken(client);
            if (string.IsNullOrEmpty(_token))
                return Unauthorized("Login ulang gagal.");

            result = await GetCustomerData(client, _token, custEmail);
        }

        return StatusCode(result.status, result.body);
    }

    private async Task<string> GetToken(HttpClient client)
    {
        var baseUrl = _configuration["BoostApi:BaseUrl"];
        var username = _configuration["BoostApi:Username"];
        var password = _configuration["BoostApi:Password"];

        var request = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/token");
        request.Content = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("username", username),
            new KeyValuePair<string, string>("password", password),
            new KeyValuePair<string, string>("grant_type", "password")
        });

        var response = await client.SendAsync(request);
        if (!response.IsSuccessStatusCode) return null;

        var content = await response.Content.ReadAsStringAsync();
        var json = JsonSerializer.Deserialize<JsonElement>(content);
        return json.GetProperty("access_token").GetString();
    }

    private async Task<(int status, string body)> GetCustomerData(HttpClient client, string token, string identifier)
    {
        var baseUrl = _configuration["BoostApi:BaseUrl"];
        var isEmail = identifier.Contains("@");
        var url = isEmail
            ? $"{baseUrl}/api/CRMCap/V1/GetCallCenterCustomerDataByEmail?custEmail={identifier}&formatData=json"
            : $"{baseUrl}/api/CRMCap/V1/GetCallCenterCustomerDataByMobile?custMobile={identifier}&formatData=json";

        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.SendAsync(request);
        var content = await response.Content.ReadAsStringAsync();
        return ((int)response.StatusCode, content);
    }
}
