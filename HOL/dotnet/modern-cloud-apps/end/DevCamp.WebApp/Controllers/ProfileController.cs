﻿using DevCamp.WebApp.Utils;
using DevCamp.WebApp.ViewModels;
using Microsoft.Owin.Security;
using Microsoft.Owin.Security.Cookies;
using Microsoft.Owin.Security.OpenIdConnect;
using Newtonsoft.Json;
using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;

namespace DevCamp.WebApp.Controllers
{
    //BASED ON THE SAMPLE https://azure.microsoft.com/en-us/documentation/articles/active-directory-v2-devquickstarts-dotnet-web/
    //AND https://github.com/microsoftgraph/aspnet-connect-rest-sample
    //AND https://github.com/microsoftgraph/aspnet-connect-sample <--DO NOT USE Uses MSAL Preview

    public class ProfileController : Controller
    {
        // The URL that auth should redirect to after a successful login.
        Uri loginRedirectUri => new Uri(Url.Action(nameof(Index), "Profile", null, Request.Url.Scheme));
        // The URL to redirect to after a logout.
        Uri logoutRedirectUri => new Uri(Url.Action(nameof(Index), "Profile", null, Request.Url.Scheme));

        public void SignIn()
        {
            if (!Request.IsAuthenticated)
            {
                // Signal OWIN to send an authorization request to Azure
                HttpContext.GetOwinContext().Authentication.Challenge(
                  new AuthenticationProperties { RedirectUri = "/" },
                  OpenIdConnectAuthenticationDefaults.AuthenticationType);
            }
        }

        public void SignOut()
        {
            if (Request.IsAuthenticated)
            {
                // Get the user's token cache and clear it
                string userObjId = System.Security.Claims.ClaimsPrincipal.Current
                  .FindFirst("http://schemas.microsoft.com/identity/claims/objectidentifier").Value;

                SessionTokenCache tokenCache = new SessionTokenCache(userObjId, HttpContext);
                tokenCache.Clear();
            }

            // Send an OpenID Connect sign-out request. 
            HttpContext.GetOwinContext().Authentication.SignOut(
              CookieAuthenticationDefaults.AuthenticationType);
            Response.Redirect("/");
        }

        [Authorize]
        //
        // GET: /UserProfile/
        public async Task<ActionResult> Index()
        {
            string userObjId = System.Security.Claims.ClaimsPrincipal.Current.FindFirst("http://schemas.microsoft.com/identity/claims/objectidentifier").Value;

            SessionTokenCache tokenCache = new SessionTokenCache(userObjId, HttpContext);
            string tenantId = System.Security.Claims.ClaimsPrincipal.Current.FindFirst("http://schemas.microsoft.com/identity/claims/tenantid").Value;
            string authority = string.Format(Settings.AAD_INSTANCE, tenantId, "");
            AuthHelper authHelper = new AuthHelper(authority, Settings.AAD_APP_ID, Settings.AAD_APP_SECRET, tokenCache);

            string accessToken = await authHelper.GetUserAccessToken(Url.Action("Index", "Profile", null, Request.Url.Scheme));
            UserProfileViewModel userProfile = new UserProfileViewModel();

            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.Accept.Clear();
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                // New code:
                HttpResponseMessage response = await client.GetAsync(Settings.GRAPH_CURRENT_USER_URL);
                if (response.IsSuccessStatusCode)
                {
                    string resultString = await response.Content.ReadAsStringAsync();

                    userProfile = JsonConvert.DeserializeObject<UserProfileViewModel>(resultString);
                }
            }

            return View(userProfile);
        }
    }
}