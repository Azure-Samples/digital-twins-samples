using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Identity.Client;
using Microsoft.Rest;
using ADTApi;
using System.Threading.Tasks;
using System.Linq;

namespace DigitalTwinsSample
{
    class AuthHelper
    {
        string[] scopes;
        string AdtInstanceUrl;
        string ClientId;
        string TenantId;
        private AzureDigitalTwinsAPI client;
        public AuthHelper (string[] _scopes, string _AdtInstanceUrl, string _ClientId, string _TenantId)
        {
            this.scopes = _scopes;
            this.AdtInstanceUrl = _AdtInstanceUrl;
            this.ClientId = _ClientId;
            this.TenantId = _TenantId;
        }

        public async Task Authorize()
        {
            AuthenticationResult authResult = null;
            var app = PublicClientApplicationBuilder.Create(ClientId)
                .WithTenantId(TenantId)
                .WithDefaultRedirectUri()
                .Build(); ;
            var accounts = await app.GetAccountsAsync();
            try
            {
                authResult = await app.AcquireTokenSilent(scopes, accounts.FirstOrDefault()).ExecuteAsync();
            }
            catch (MsalUiRequiredException ex)
            {
                System.Diagnostics.Debug.WriteLine($"MsalUiRequiredException: {ex.Message}");
                authResult = await app.AcquireTokenInteractive(scopes).ExecuteAsync();
            }

            if (authResult != null)
            {
                Log($"Authorization successful: {authResult.Account.Username}");

                try
                {
                    TokenCredentials tk = new TokenCredentials(authResult.AccessToken);
                    client = new AzureDigitalTwinsAPI(tk);
                    client.BaseUri = new Uri(AdtInstanceUrl);
                    Log($"Client connection created.");
                }
                catch (Exception e)
                {
                    Log($"Client creation failed:");
                    Log(e.Message);
                }
            }
        }

        public AzureDigitalTwinsAPI getClient()
        {
            return client;
        }
        public void Log(string s)
        {
            Console.WriteLine(s);
        }
    }
}
