/*
 * Tim Davis #1332245
 * Project Assignment #2
 * Page.cs
 * Contains the Page class, which is a table entity
 */

using Microsoft.WindowsAzure.Storage.Table;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web;

namespace WebRole
{
    class Page : TableEntity
    {
        public Page(string url, string title)
        {
            // Encode urls
            string encodedUrl = HttpUtility.UrlEncode(url);

            this.PartitionKey = encodedUrl;
            this.RowKey = "title";

            this.URL = url;
            this.Title = title;
        }

        public Page() { }

        public string URL { get; set; }

        public string Title { get; set; }
    }
}
