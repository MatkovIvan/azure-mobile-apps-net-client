using System;
using System.Collections.ObjectModel;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.MobileServices;
using Microsoft.WindowsAzure.MobileServices.SQLiteStore;
using Microsoft.WindowsAzure.MobileServices.Sync;
using Newtonsoft.Json;

namespace ConsoleApp1
{
    [DataTable("OfflineReady")]
    class OfflineReadyItem
    {
        [JsonProperty(PropertyName = "id")]
        public string Id { get; set; }

        [Version]
        public string Version { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        public override string ToString() => $"[Id={Id}, Version={Version}, Name={Name}]";
    }

    class Program
    {
        MobileServiceClient client;
        MobileServiceSQLiteStore store;

        IMobileServiceSyncTable<OfflineReadyItem> table;

        Program()
        {
            client = new MobileServiceClient("https://dihei-e2e-app.azurewebsites.net", new LogHttpHandler());
            store = new MobileServiceSQLiteStore("localstore.db");

            Log("Defining the table on the local store");
            store.DefineTable<OfflineReadyItem>();
            client.SyncContext.InitializeAsync(store);
            Log("Initialized the store and sync context");

            table = client.GetSyncTable<OfflineReadyItem>();
        }

        async Task ListAsync()
        {
            Log($"List items in the local table");
            var items = await table.ToListAsync();
            Log($"Items count: {0}", items.Count);
            foreach (var i in items)
                Log($"  {i}");
        }

        async Task PrepareAsync()
        {
            var item = new OfflineReadyItem { Name = "24" };
            await table.InsertAsync(item);
            Log("Inserted: {0}", item);

            item.Name = "42";
            await table.UpdateAsync(item);
            Log("Updated: {0}", item);

            //await table.DeleteAsync(item);
            //Log("Deleted: {0}", item);
            // OK

            //await table.PurgeAsync(force: true);
            //Log("Purged");
            // OK

            await store.DeleteAsync("OfflineReady", new[] { item.Id });
            Log("ITEM DATA DELETED FROM STORE: {0}", item);
        }

        async Task SyncAsync()
        {
            ReadOnlyCollection<MobileServiceTableOperationError> syncErrors = null;
            try
            {
                Log("Pushing changes to the server");
                await client.SyncContext.PushAsync();
                Log("Push done");

                //Log("Pulling the data into the local table");
                //await table.PullAsync("allTestItems", table.CreateQuery());
            }
            catch (MobileServicePushFailedException e)
            {
                Log("PushFailedException: {0}\n" +
                    "\tStatus={1}\n" +
                    "\tErrors={2}\n" +
                    "\tInnerException={3}",
                    e.Message, e.PushResult?.Status, e.PushResult?.Errors.Count, e.InnerException);

                syncErrors = e.PushResult?.Errors;
            }
            if (syncErrors != null)
                foreach (var error in syncErrors)
                {
                    await error.CancelAndDiscardItemAsync();
                    Log("Error executing sync operation. Item: {0} ({1}). Operation discarded.", error.TableName, error.Item["id"]);
                }
        }

        static void Main(string[] args)
        {
            var program = new Program();
            program.PrepareAsync().Wait();
            program.ListAsync().Wait();
            program.SyncAsync().Wait();
            Console.ReadLine();
        }

        #region Logging

        static void Log(string s, params object[] args) => Console.WriteLine($"[{DateTime.Now}] {s}", args);

        class LogHttpHandler : DelegatingHandler
        {
            protected async override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                Log("  >>> {0} {1}\n{2}", request.Method, request.RequestUri, request.Content?.ReadAsStringAsync().Result);
                var response = await base.SendAsync(request, cancellationToken);
                Log("  <<< {0} {1}\n{2}", (int)response.StatusCode, response.ReasonPhrase, response.Content?.ReadAsStringAsync().Result);
                return response;
            }
        }

        #endregion
    }
}
