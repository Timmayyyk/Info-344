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
        public Page(string url, string title, string word, string date)
        {
            this.PartitionKey = word.ToLower();
            this.RowKey = HttpUtility.UrlEncode(url);

            this.Title = title;
            this.Date = date;
        }

        public Page() { }

        public string Title { get; set; }

        public string Date { get; set; }
    }
}
