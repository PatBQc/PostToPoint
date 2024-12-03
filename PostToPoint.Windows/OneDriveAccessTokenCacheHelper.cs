using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using Microsoft.Identity.Client;
using Microsoft.Identity.Client.Extensions.Msal;

namespace PostToPoint.Windows
{
    public class OneDriveAccessTokenCacheHelper
    {
        private static readonly string CacheFileName = "msal_cache.bin";
        private static readonly string CacheDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), ".msalcache");

        public static async Task EnableSerialization(IPublicClientApplication app)
        {
            var storageProperties = new StorageCreationPropertiesBuilder(CacheFileName, CacheDir)
                .Build();

            var cacheHelper = await MsalCacheHelper.CreateAsync(storageProperties);
            cacheHelper.RegisterCache(app.UserTokenCache);
        }

        public static async Task ClearCache(IPublicClientApplication app)
        {
            var accounts = await app.GetAccountsAsync();
            foreach (var account in accounts)
            {
                await app.RemoveAsync(account);
            }
        }
    }
}
