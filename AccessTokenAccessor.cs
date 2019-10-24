using System;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using Microsoft.IdentityModel.Clients.ActiveDirectory;

namespace StorageAccountSync
{
    /// <summary>
    /// This class provides authentication helpers when using various Azure clients, such as KeyVaultClient
    /// </summary>
    public static class AccessTokenAccessor
    {
        public static async Task<string> Get(string authority, string resource, string scope, string clientId, X509Certificate2 certificate)
        {
            if (authority == null) throw new ArgumentNullException(nameof(authority));
            if (resource == null) throw new ArgumentNullException(nameof(resource));
            if (scope == null) throw new ArgumentNullException(nameof(scope));
            if (clientId == null) throw new ArgumentNullException(nameof(clientId));
            if (certificate == null) throw new ArgumentNullException(nameof(certificate));

            IClientAssertionCertificate credential = GetCredential(clientId, certificate);
            var authenticationContext = new AuthenticationContext(authority, null);
            var authenticationResult = await authenticationContext.AcquireTokenAsync(resource, credential);
            return authenticationResult.AccessToken;
        }

        public static async Task<string> Get(string authority, string resource, string scope, string clientId, string clientSecret)
        {
            if (authority == null) throw new ArgumentNullException(nameof(authority));
            if (resource == null) throw new ArgumentNullException(nameof(resource));
            if (scope == null) throw new ArgumentNullException(nameof(scope));
            if (clientId == null) throw new ArgumentNullException(nameof(clientId));
            if (clientSecret == null) throw new ArgumentNullException(nameof(clientSecret));

            ClientCredential credential = GetCredential(clientId, clientSecret);
            var authenticationContext = new AuthenticationContext(authority, null);
            var authenticationResult = await authenticationContext.AcquireTokenAsync(resource, credential);
            return authenticationResult.AccessToken;
        }

        private static IClientAssertionCertificate GetCredential(string clientId, X509Certificate2 certificate) => new ClientAssertionCertificate(clientId, certificate);

        private static ClientCredential GetCredential(string clientId, string clientSecret) => new ClientCredential(clientId, clientSecret);
    }
}
