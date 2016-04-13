/*
 * Tim Davis #1332245
 * Project Assignment #3
 * Admin.asmx
 * Admin role for the web crawler
 */

using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Web;
using System.Web.Services;
using System.Web.Script.Services;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Queue;
using Microsoft.WindowsAzure.Storage.Table;
using System.Threading;
using System.Web.Script.Serialization;
using System.Diagnostics;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.ServiceRuntime;
using System.IO;

namespace WebRole
{
    /// <summary>
    /// Admin role for the web crawler
    /// </summary>
    [WebService(Namespace = "http://tempuri.org/")]
    [WebServiceBinding(ConformsTo = WsiProfiles.BasicProfile1_1)]
    [System.ComponentModel.ToolboxItem(false)]
    // To allow this Web Service to be called from script, using ASP.NET AJAX, uncomment the following line. 
    [System.Web.Script.Services.ScriptService]
    public class Admin : System.Web.Services.WebService
    {
        // Constants
        private static string filepath = System.IO.Path.GetTempFileName();
        private const int maxSuggestions = 10;

        // Admin state
        static Trie trie;
        private static bool clearing = false;

        // Worker stats set to default settings
        private static string state = "idle";
        private static string tableSize = "0";
        private static string urlCount = "0";
        private static string last10Urls = "";
        private static string errors = "";

        // Trie stats set to default settings
        private static int trieCount = 0;
        private static string trieLast = "Trie not built yet.";

        // Queue and table set up
        private static CloudStorageAccount storageAccount = CloudStorageAccount.Parse(
                            ConfigurationManager.AppSettings["StorageConnectionString"]);
        private static CloudQueueClient queueClient = storageAccount.CreateCloudQueueClient();
        private static CloudTableClient tableClient = storageAccount.CreateCloudTableClient();

        // Queue and table references
        private CloudQueue adminQueue = queueClient.GetQueueReference("adminmessages");
        private CloudQueue urlQueue = queueClient.GetQueueReference("urls");
        private CloudTable pageTable = tableClient.GetTableReference("pages");
        private CloudTable statsTable = tableClient.GetTableReference("stats");

        // Performance counters
        private PerformanceCounter cpuCounter = 
            new PerformanceCounter("Processor", "% Processor Time", "_Total");
        private PerformanceCounter memCounter =
            new PerformanceCounter("Memory", "Available MBytes");

        // Cache
        private static Dictionary<string, string> cache = new Dictionary<string,string>();

        /// <summary>
        /// Downloads the wiki files containing titles
        /// </summary>
        [WebMethod]
        public string DownloadWiki()
        {
            CloudStorageAccount storageAccount = CloudStorageAccount.Parse(
                ConfigurationManager.AppSettings["StorageConnectionString"]);
            CloudBlobClient blobClient = storageAccount.CreateCloudBlobClient();
            CloudBlobContainer container = blobClient.GetContainerReference("wikifile");

            if (container.Exists())
            {
                foreach (IListBlobItem item in container.ListBlobs(null, false))
                {
                    if (item.GetType() == typeof(CloudBlockBlob))
                    {
                        CloudBlockBlob blob = (CloudBlockBlob)item;

                        using (var fileStream = System.IO.File.OpenWrite(filepath))
                        {
                            blob.DownloadToStream(fileStream);
                        }
                    }
                }
            }

            return "Download successful!";
        }

        /// <summary>
        /// Builds storage trie
        /// </summary>
        [WebMethod]
        public string BuildTrie()
        {
            trie = new Trie();
            StreamReader sr = new StreamReader(filepath);

            int counter = 0;
            float memUsage = memCounter.NextValue();
            string title = sr.ReadLine();

            try
            {
                while (title != null && memUsage > 50)
                {
                    trie.Add(title);
                    title = sr.ReadLine();
                    counter++; trieCount++;

                    if (counter == 1000)
                    {
                        counter = 0;
                        memUsage = memCounter.NextValue();
                    }
                }
            }
            catch (OutOfMemoryException)
            {
                return "Couldn't build the trie.";
            }

            trieLast = title;

            return "Trie built!";
        }

        /// <summary>
        /// Searches storage trie
        /// </summary>
        [WebMethod]
        [ScriptMethod(ResponseFormat = ResponseFormat.Json)]
        public string SearchTrie(string prefix)
        {
            try
            {
                List<string> prefixedWords = trie.FindWords(prefix, maxSuggestions);

                return new JavaScriptSerializer().Serialize(prefixedWords);
            }
            catch
            {
                return "";
            }
        }

        /// <summary>
        /// References the trie, easy to ping with Uptime Robot
        /// </summary>
        [WebMethod]
        public void KeepAlive()
        {
            List<string> words = trie.FindWords("apple", 0);
        }

        // Handles searching for urls/titles
        [WebMethod]
        [ScriptMethod(ResponseFormat = ResponseFormat.Json)]
        public string Search(string searchInput)
        {
            searchInput = searchInput.ToLower().Trim();

            if (cache.ContainsKey(searchInput))
            {
                return cache[searchInput];
            }
            else
            {
                var results = new List<Page>();

                string[] searchWords = searchInput.Split(' ');
                foreach (string word in searchWords)
                {

                    TableQuery<Page> query = new TableQuery<Page>()
                        .Where(TableQuery.GenerateFilterCondition("PartitionKey", QueryComparisons.Equal, word));

                    foreach (Page entity in pageTable.ExecuteQuery(query))
                    {
                        results.Add(entity);
                    }
                }

                string result = new JavaScriptSerializer().Serialize(
                    results.GroupBy(x => x.RowKey)
                    .Select(x => new Tuple<string, int, string, string>(
                        HttpUtility.UrlDecode(x.Key), x.ToList().Count, x.First().Title, x.First().Date))
                    .OrderByDescending(x => x.Item2)
                    .ThenByDescending(x => x.Item4)
                    .Take(20)
                    .ToArray());

                if (cache.Count >= 100)
                {
                    cache.Clear();
                }
                cache.Add(searchInput, result);

                return result;
            }
        }


        #region DASHBOARD METHODS

        // Sends admin message requesting to start crawling
        // Returns confirmation that request was sent
        [WebMethod]
        [ScriptMethod(ResponseFormat = ResponseFormat.Json)]
        public string StartCrawling()
        {
            // stop user from trying to do anything while crawler is clearing
            if (!clearing)
            {
                // Create needed queues if they don't exist
                adminQueue.CreateIfNotExists();
                urlQueue.CreateIfNotExists();

                // Create tables if they don't exist
                pageTable.CreateIfNotExists();
                statsTable.CreateIfNotExists();

                // Send request message
                CloudQueueMessage start = new CloudQueueMessage("start");
                adminQueue.AddMessage(start);

                return "Start request sent.";
            }
            else
            {
                return "Please wait until crawler is done clearing.";
            }
        }

        // Sends admin message requesting to stop crawling
        // Returns confirmation that request was sent
        [WebMethod]
        public string StopCrawling()
        {
            // Create needed queues if they don't exist
            adminQueue.CreateIfNotExists();
            urlQueue.CreateIfNotExists();

            // Send request message
            CloudQueueMessage stop = new CloudQueueMessage("stop");
            adminQueue.AddMessage(stop);

            return "Stop request sent.";
        }

        // Refreshes the crawler stats
        [WebMethod]
        public void RefreshStats()
        {
            try
            {
                TableQuery<Statistic> query = new TableQuery<Statistic>();
                foreach (Statistic entity in statsTable.ExecuteQuery(query))
                {
                    if (entity.Stat.Equals("state"))
                    {
                        state = entity.Value;
                    }
                    else if (entity.Stat.Equals("tablesize"))
                    {
                        tableSize = entity.Value;
                    }
                    else if (entity.Stat.Equals("urlcount"))
                    {
                        urlCount = entity.Value;
                    }
                    else if (entity.Stat.Equals("last10urls"))
                    {
                        last10Urls = entity.Value;
                    }
                    else if (entity.Stat.Equals("errors"))
                    {
                        errors = entity.Value;
                    }
                }
            }
            catch
            {
                // Don't break
            }
        }

        // Returns the worker's state
        [WebMethod]
        [ScriptMethod(ResponseFormat = ResponseFormat.Json)]
        public string GetWorkerState()
        {
            RefreshStats();
            return state;
        }

        // Returns CPU state
        [WebMethod]
        [ScriptMethod(ResponseFormat = ResponseFormat.Json)]
        public string GetCPUState()
        {
            return "" + this.cpuCounter.NextValue();
        }

        // Returns Memory state
        [WebMethod]
        [ScriptMethod(ResponseFormat = ResponseFormat.Json)]
        public string GetMemoryState()
        {
            return "" + this.memCounter.NextValue();
        }

        // Returns url queue size
        [WebMethod]
        [ScriptMethod(ResponseFormat = ResponseFormat.Json)]
        public string GetUrlQueueSize()
        {
            urlQueue.CreateIfNotExists();
            urlQueue.FetchAttributes();
            string size = "" + urlQueue.ApproximateMessageCount;
            return size;
        }

        // Returns the number of urls in the pages table
        [WebMethod]
        [ScriptMethod(ResponseFormat = ResponseFormat.Json)]
        public string GetTableSize()
        {
            return tableSize;
        }

        [WebMethod]
        [ScriptMethod(ResponseFormat = ResponseFormat.Json)]
        public string GetNumberUrlsCrawled()
        {
            return urlCount;
        }

        // Returns the last 10 urls crawled
        [WebMethod]
        [ScriptMethod(ResponseFormat = ResponseFormat.Json)]
        public string GetLast10Urls()
        {
            return last10Urls;
        }

        // Returns errors
        [WebMethod]
        [ScriptMethod(ResponseFormat = ResponseFormat.Json)]
        public string GetErrors()
        {
            return errors;
        }

        // Stops worker role, and clears queues and tables
        // Returns confirmation that everything was cleared
        [WebMethod]
        [ScriptMethod(ResponseFormat = ResponseFormat.Json)]
        public string ClearEverything()
        {
            if (!clearing) {
                StopCrawling();
                ClearQueues();

                // Sleep 10 seconds to make sure worker gets stop message
                Thread.Sleep(10000);

                ClearTables();

                return "Everything has been stopped and cleared.";
            }
            else
            {
                return "Please wait until crawler is done clearing.";
            }
        }

        // Clears the pages table
        // Returns confirmation that table was cleared
        [WebMethod]
        public string ClearTables()
        {
            clearing = true;

            // Create needed tables if they don't exist
            statsTable.CreateIfNotExists();
            pageTable.CreateIfNotExists();

            // Delete the tables
            statsTable.Delete();
            pageTable.Delete();

            // Reset stats to defaults for when the stats table isn't filled
            state = "idle"; tableSize = "0"; urlCount = "0"; last10Urls = ""; errors = "";

            while (true)
            {
                try
                {
                    statsTable.CreateIfNotExists();
                    pageTable.CreateIfNotExists();
                    break;
                }
                catch (StorageException e)
                {
                    if (e.RequestInformation.HttpStatusCode == 409)
                    {
                        Thread.Sleep(1000);
                    }
                    else
                    {
                        throw;
                    }
                }
            }

            clearing = false;
            return "Tables have been cleared.";
        }

        public string ClearQueues()
        {
            adminQueue.Clear();
            urlQueue.Clear();

            return "";
        }

        [WebMethod]
        [ScriptMethod(ResponseFormat = ResponseFormat.Json)]
        public string GetTrieCount()
        {
            return "" + trieCount;
        }

        [WebMethod]
        [ScriptMethod(ResponseFormat = ResponseFormat.Json)]
        public string GetTrieLast()
        {
            return trieLast;
        }

        #endregion
    }
}
