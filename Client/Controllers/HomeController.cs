using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;

namespace Client.Controllers
{
    public class HomeController : Controller
    {
        private readonly IHttpClientFactory _httpClientFactory;
        

        public HomeController(IHttpClientFactory factory)
        {
            _httpClientFactory = factory;
            
        }
        public IActionResult Index()
        {
            return View();
        }
        [Authorize]
        public async Task<IActionResult> Secret()
        {
            var token = await HttpContext.GetTokenAsync("access_token");
            var serverClient = _httpClientFactory.CreateClient();

            var refreshToken = await HttpContext.GetTokenAsync("refresh_token");

            serverClient.DefaultRequestHeaders.Add("Authorization",$"Bearer {token}");

            var serverResponse = await serverClient.GetAsync("https://localhost:44396/secret/index");
            await RefreshAccessToken();

            var apiClient = _httpClientFactory.CreateClient();

            token = await HttpContext.GetTokenAsync("access_token");
            apiClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");

            var apiResponse = await apiClient.GetAsync("https://localhost:44398/secret/index");

            return View();
        }


        private async Task<HttpResponseMessage> SecuredGetRequest(string url)
        {
            var token = await HttpContext.GetTokenAsync("access_token");

            var client = _httpClientFactory.CreateClient();

            client.DefaultRequestHeaders.Add("Authorization",$"Bearer {token}");

            return await client.GetAsync(url);
        }

        public async Task<HttpResponseMessage> RefreshAccessToken(Func<Task<HttpResponseMessage>> requestAction)
        {


            var refreshToken = await HttpContext.GetTokenAsync("refresh_token");

            var refreshTokenClient = _httpClientFactory.CreateClient();

            var requestData = new Dictionary<string, string>
            {
                ["grant_type"] = "refresh_token",
                ["refresh_token"] = refreshToken
            };

            var request = new HttpRequestMessage(HttpMethod.Post, "https://localhost:44396/oauth/token") {
                Content = new FormUrlEncodedContent(requestData)
            };

            var basicCredentials = "username:password";
            var encodedCredentials = Encoding.UTF8.GetBytes(basicCredentials);
            var base64Credentials = Convert.ToBase64String(encodedCredentials);
            
            request.Headers.Add("Authorization",$"Basic {base64Credentials}");

            var response = await refreshTokenClient.SendAsync(request);

            var responsString = await response.Content.ReadAsStringAsync();
            var responseData = JsonConvert.DeserializeObject<Dictionary<string,string>>(responsString);

            var newAccessToken = responseData.GetValueOrDefault("access_token");
            var newRefreshToken = responseData.GetValueOrDefault("refresh_token");

            var authInfo = await HttpContext.AuthenticateAsync("ClientCookie");

            authInfo.Properties.UpdateTokenValue("access_token", newAccessToken);
            authInfo.Properties.UpdateTokenValue("refresh_token", newRefreshToken);

            await HttpContext.SignInAsync("ClientCookie", authInfo.Principal, authInfo.Properties );

            return "";
        }
    }
}
