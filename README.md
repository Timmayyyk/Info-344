# Search Engine Infrastructure .net Cloud App
This search engine utilizes a web crawler (WorkerRole.cs) that crawls XML sitemaps, validating the URLS and using the Azure Cloud Queue to pass and store the url/title combinations into an Azure Cloud Table.  A search page (index.html) gives users the ability to search titles that have been stored in the table.  A Trie structure (Trie.cs) is utilized to quickly (almost instantly), efficiently, and asynchronously provide 10 query suggestions to the user every time they type a letter in the search box.

All of this together provides an experience familiar to Google.