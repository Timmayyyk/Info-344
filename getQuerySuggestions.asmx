/*
 * Tim Davis #1332245
 * Project Assignment #2
 * getQuerySuggestions.asmx
 * Handles building and searching the trie for query suggestion
 */

using Microsoft.WindowsAzure.ServiceRuntime;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Web;
using System.Web.Script.Serialization;
using System.Web.Script.Services;
using System.Web.Services;

namespace WebRole
{
    /// <summary>
    /// Summary description for getQuerySuggestions
    /// </summary>
    [WebService(Namespace = "http://tempuri.org/")]
    [WebServiceBinding(ConformsTo = WsiProfiles.BasicProfile1_1)]
    [System.ComponentModel.ToolboxItem(false)]
    // To allow this Web Service to be called from script, using ASP.NET AJAX, uncomment the following line. 
    [System.Web.Script.Services.ScriptService]
    public class getQuerySuggestions : System.Web.Services.WebService
    {
        const string azureLocalResourceNameFromServiceDefinition = "SomeLocationForCache";
        private const int maxSuggestions = 10;

        static Trie trie;
        private PerformanceCounter mp;

        /// <summary>
        /// Downloads the wiki files containing titles
        /// </summary>
        [WebMethod]
        public void downloadWiki()
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

                        var azureLocalResource = RoleEnvironment.GetLocalResource(
                            azureLocalResourceNameFromServiceDefinition);
                       
                        var filepath = azureLocalResource.RootPath + "WikiTitles.txt";
                        using (var fileStream = System.IO.File.OpenWrite(filepath))
                        {
                            blob.DownloadToStream(fileStream);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Builds storage trie
        /// </summary>
        [WebMethod]
        public void buildTrie()
        {
            trie = new Trie();
            this.mp = new PerformanceCounter("Memory", "Available MBytes");
            var azureLocalResource = RoleEnvironment.GetLocalResource(
                            azureLocalResourceNameFromServiceDefinition);

            var filepath = azureLocalResource.RootPath + "WikiTitles.txt";
            StreamReader sr = new StreamReader(filepath);

            int counter = 0;
            float memUsage = mp.NextValue();
            string title = sr.ReadLine();

            try
            {
                while (title != null && memUsage > 50)
                {
                    trie.Add(title);
                    title = sr.ReadLine();
                    counter++;

                    if (counter == 1000)
                    {
                        counter = 0;
                        memUsage = mp.NextValue();
                    }
                }
            }
            catch (OutOfMemoryException)
            {
                // Do nothing
            }

            mp.Close();
            sr.Close();
        }

        /// <summary>
        /// Searches storage trie
        /// </summary>
        [WebMethod]
        [ScriptMethod(ResponseFormat = ResponseFormat.Json)]
        public string searchTrie(string prefix)
        {
            List<string> prefixedWords = trie.FindWords(prefix, maxSuggestions);

            return new JavaScriptSerializer().Serialize(prefixedWords);
        }
    }
}
