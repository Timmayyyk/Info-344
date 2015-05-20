/*
 * Tim Davis #1332245
 * Project Assignment #3
 * WorkerRole.cs
 * Worker role for the web crawler
 */

using System;
using System.Xml;
using System.IO;
using System.Text;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.WindowsAzure;
using Microsoft.WindowsAzure.Diagnostics;
using Microsoft.WindowsAzure.ServiceRuntime;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Queue;
using Microsoft.WindowsAzure.Storage.Table;
using HtmlAgilityPack;

namespace WorkerRole
{
    public class WorkerRole : RoleEntryPoint
    {
        // Global constants
        private const int ACCEPTABLE_YEAR = 2015;
        private const int ACCEPTABLE_MONTH = 4;

        // Worker role state
        private static string state = "idle";

        // Queue and table set up
        private static CloudStorageAccount storageAccount = CloudStorageAccount.Parse(
            CloudConfigurationManager.GetSetting("StorageConnectionString"));
        private static CloudQueueClient queueClient = storageAccount.CreateCloudQueueClient();
        private static CloudTableClient tableClient = storageAccount.CreateCloudTableClient();

        // Queue and table references
        private CloudQueue adminQueue = queueClient.GetQueueReference("adminmessages");
        private CloudQueue urlQueue = queueClient.GetQueueReference("urls");
        private CloudTable pageTable = tableClient.GetTableReference("pages");
        private CloudTable statsTable = tableClient.GetTableReference("stats");

        // Disallow list
        private static List<string> disallows = new List<string>();

        // Url set to check for duplicates
        private static HashSet<string> crawledUrls = new HashSet<string>();

        // Stats
        private static int tableSize = 0;
        private static int urlCount = 0;
        private static Queue<string> last10urls = new Queue<string>();
        private static List<string> errors = new List<string>();

        // Testing
        private static int errorCount = 0;

        public override void Run()
        {
            while (true)
            {
                // Sleep 50ms
                Thread.Sleep(50);

                // Get and remove Admin messages from adminQueue (start/stop/etc)
                try
                {
                    adminQueue.CreateIfNotExists();
                    CloudQueueMessage adminMessage = adminQueue.GetMessage();
                    adminQueue.DeleteMessage(adminMessage);
                    string admin = adminMessage.AsString;

                    // Handle Admin messages
                    if (admin.Equals("start"))
                    {
                        state = "loading";
                    }
                    else if (admin.Equals("stop"))
                    {
                        state = "idle";
                    }
                    else
                    {
                        // ??
                    }
                } 
                catch (Exception)
                {
                    // Don't crash if there aren't any admin messages
                }

                // Update state in stats table
                UpdateState();

                // Check what state we're in
                if (state.Equals("idle")) 
                {
                    // Worker is idle, state = idle
                    // Do nothing
                }
                else if (state.Equals("loading"))
                {
                    // Worker is loading, state = loading
                    string cnn = "http://www.cnn.com/robots.txt";
                    string bleacher = "http://bleacherreport.com/robots.txt";
                    HandleLoading(cnn, true);
                    HandleLoading(bleacher, false);


                    // Change state to crawling
                    state = "crawling";

                    // Create needed table if it doesn't exist
                    pageTable.CreateIfNotExists();
                }
                else if (state.Equals("crawling"))
                {
                    HandleCrawling();
                }
                else
                {
                    // state = ?
                    throw new Exception("Something went wrong!");
                }
            }
        }

        // Handles the worker role in loading state
        // Specifically, handles robots.txt
        public void HandleLoading(string robotsUrl, bool isCnn)
        {
            System.Net.WebClient wc = new System.Net.WebClient();
            string webData = wc.DownloadString(robotsUrl);
            string[] dataLines = webData.Split('\n');

            foreach (string line in dataLines)
            {
                string[] parts = line.Split(' ');

                if (parts[0].Equals("Sitemap:"))
                {
                    string xmlUrl = parts[1];

                    // If it isn't cnn sitemap, make sure we only go into the nba sitemap
                    if (isCnn || xmlUrl.Contains("nba"))
                    {
                        HandleLoadingHelp(xmlUrl);
                    }
                }
                else if (parts[0].Equals("Disallow:"))
                {
                    string disallow = parts[1];
                    disallows.Add(disallow);
                }
            }
        }

        // Helps handle the worker role in loading state
        // Specifically, handles xml pages, recursively searching them
        public void HandleLoadingHelp(string xmlUrl)
        {
            try
            {
                using (System.Net.WebClient wc = new System.Net.WebClient())
                {
                    string xmlString = wc.DownloadString(xmlUrl);
                    using (XmlReader reader = XmlReader.Create(new StringReader(xmlString)))
                    {
                        // Check if "lastmod" exists in the xml
                        bool hasLastMod = false;
                        if (xmlString.Contains("lastmod"))
                        {
                            hasLastMod = true;
                        }

                        while (reader.Read())
                        {
                            // Get the reader to the next <loc> tag, read the value inside it
                            reader.ReadToFollowing("loc");
                            reader.Read();
                            string url = reader.Value;

                            // Cnn sitemaps have lastMod, Bleacherreport doesn't
                            string lastMod = null; int year = 0; int month = 0;
                            if (hasLastMod)
                            {
                                // Get the reader to the next <lastmod> tag, read the value inside it
                                reader.ReadToFollowing("lastmod");
                                reader.Read();
                                lastMod = reader.Value;

                                char[] dateDelimiters = { '-', 'T' };
                                string[] dateParts = lastMod.Split(dateDelimiters);

                                year = Int32.Parse(dateParts[0]);
                                month = Int32.Parse(dateParts[1]);
                            }

                            // If this url was modified at least on or after the acceptable date
                            if (!hasLastMod || (year >= ACCEPTABLE_YEAR && month >= ACCEPTABLE_MONTH)) {
                                // Check url format
                                if (url.EndsWith(".xml"))
                                {
                                    // url is an xml sitemap url
                                    HandleLoadingHelp(url);
                                }
                                else
                                {
                                    // url is an html url
                                    CloudQueueMessage htmlMessage = new CloudQueueMessage(url);
                                    urlQueue.AddMessage(htmlMessage);
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception)
            {
                errors.Add("Loading error (url failed): " + xmlUrl);
                UpdateErrors();
                errorCount++;
                // Don't crash
            }
        }

        // Handles the worker role in crawling state
        // Specifically, handles html pages (one per HandleCrawling() call)
        public void HandleCrawling()
        {
            // Get and remove URL message from urlQueue
            CloudQueueMessage urlMessage = urlQueue.GetMessage();
            urlQueue.DeleteMessage(urlMessage);
            string htmlUrl = urlMessage.AsString;

            // Update url count
            UpdateUrlCount();

            // Check if this url is unique for our table
            if (!crawledUrls.Contains(htmlUrl))
            {
                crawledUrls.Add(htmlUrl);

                var Webget = new HtmlWeb();
                var doc = Webget.Load(htmlUrl);

                // Get page title
                HtmlNode titleNode = doc.DocumentNode.SelectSingleNode("//title");
                string title = titleNode.InnerHtml;

                // Store (url, pagetitle) in table storage
                InsertTable(htmlUrl, title);

                // Get urls in page
                foreach (HtmlNode urlNode in doc.DocumentNode.SelectNodes("//a//@href"))
                {
                    // Get url from each node
                    string newUrl = urlNode.Attributes["href"].Value;

                    // Check url for cnn or bleacherreport
                    if (newUrl.Contains("cnn.com") || newUrl.Contains("bleacherreport.com"))
                    {
                        // Ignore already queued/visited urls, break if url contains a disallow
                        foreach (string disallow in disallows)
                        {
                            if (newUrl.Contains(disallow))
                            {
                                return;
                            }
                        }

                        // If url is allowed, store it into urlQueue and add it to urls set
                        CloudQueueMessage htmlMessage = new CloudQueueMessage(newUrl);
                        urlQueue.AddMessage(htmlMessage);
                    }
                }
            }
        }

        // Inserts a Page table entity into the storage table
        public void InsertTable(string url, string title)
        {
            pageTable.CreateIfNotExists();
            // Create and insert table entity
            Page page = new Page(url, title);
            TableOperation insertOperation = TableOperation.Insert(page);
            pageTable.Execute(insertOperation);

            // Update table size and last 10 urls
            UpdateTableSize();
            UpdateLast10Urls(url);
        }

        // Change state in stats table
        public void UpdateState()
        {
            try
            {
                statsTable.CreateIfNotExists();
                Statistic stateStat = new Statistic("state", state);
                TableOperation replace = TableOperation.InsertOrReplace(stateStat);
                statsTable.Execute(replace);
            }
            catch
            {
                // Don't break
            }
        }

        // Change table size in stats table
        public void UpdateTableSize()
        {
            tableSize++;
            statsTable.CreateIfNotExists();
            Statistic tableSizeStat = new Statistic("tablesize", tableSize.ToString());
            TableOperation replace = TableOperation.InsertOrReplace(tableSizeStat);
            statsTable.Execute(replace);
        }

        // Change url count in stats table
        public void UpdateUrlCount()
        {
            urlCount++;
            statsTable.CreateIfNotExists();
            Statistic urlCountStat = new Statistic("urlcount", urlCount.ToString());
            TableOperation replace = TableOperation.InsertOrReplace(urlCountStat);
            statsTable.Execute(replace);
        }

        // Change last 10 urls in stats table
        public void UpdateLast10Urls(string url)
        {
            last10urls.Enqueue(url);
            while (last10urls.Count > 10)
            {
                last10urls.Dequeue();
            }

            string list = string.Join(" | ", last10urls.ToArray());

            statsTable.CreateIfNotExists();
            Statistic last10urlsStat = new Statistic("last10urls", list);
            TableOperation replace = TableOperation.InsertOrReplace(last10urlsStat);
            statsTable.Execute(replace);
        }
        
        // Change errors in stats table
        public void UpdateErrors()
        {
            statsTable.CreateIfNotExists();

            string list = string.Join(" | ", errors.ToArray());

            Statistic errorsStat = new Statistic("errors", list);
            TableOperation replace = TableOperation.InsertOrReplace(errorsStat);
            statsTable.Execute(replace);
        }


        // Default
        private readonly CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
        private readonly ManualResetEvent runCompleteEvent = new ManualResetEvent(false);

        // Default
        public override bool OnStart()
        {
            // Set the maximum number of concurrent connections
            ServicePointManager.DefaultConnectionLimit = 12;

            // For information on handling configuration changes
            // see the MSDN topic at http://go.microsoft.com/fwlink/?LinkId=166357.

            bool result = base.OnStart();

            Trace.TraceInformation("WorkerRole has been started");

            return result;
        }

        // Default
        public override void OnStop()
        {
            Trace.TraceInformation("WorkerRole is stopping");

            this.cancellationTokenSource.Cancel();
            this.runCompleteEvent.WaitOne();

            base.OnStop();

            Trace.TraceInformation("WorkerRole has stopped");
        }

        // Default
        private async Task RunAsync(CancellationToken cancellationToken)
        {
            // TODO: Replace the following with your own logic.
            while (!cancellationToken.IsCancellationRequested)
            {
                Trace.TraceInformation("Working");
                await Task.Delay(1000);
            }
        }
    }
}
