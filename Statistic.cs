/*
 * Tim Davis #1332245
 * Project Assignment #2
 * Statistic.cs
 * Contains the Statistic class, which is a table entity
 */

using Microsoft.WindowsAzure.Storage.Table;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WebRole
{
    class Statistic : TableEntity
    {
        public Statistic(string stat, string value)
        {
            this.PartitionKey = stat;
            this.RowKey = "";

            this.Stat = stat;
            this.Value = value;
        }

        public Statistic() { }

        public string Stat { get; set; }

        public string Value { get; set; }
    }
}
