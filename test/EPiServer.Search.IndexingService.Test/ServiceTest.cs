﻿using EPiServer.Data;
using EPiServer.Framework;
using EPiServer.Framework.Initialization;
using EPiServer.Search.Internal;
using EPiServer.Search.Queries;
using EPiServer.Search.Queries.Lucene;
using EPiServer.ServiceLocation;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.ServiceModel;
using System.ServiceModel.Description;
using System.ServiceModel.Web;
using System.Text;
using System.Threading;
using Xunit;


namespace EPiServer.Search.IndexingService
{
    [Collection(IntegrationTestCollection.Name)]
    public class ServiceTest : IDisposable
    {
        private readonly SearchHandler _searchHandler;
        private readonly RequestQueueHandler _requestQueueHandler;

        public ServiceTest()
        {
            _searchHandler = ServiceLocator.Current.GetInstance<SearchHandler>();
            _requestQueueHandler = ServiceLocator.Current.GetInstance<RequestQueueHandler>();
        }

        public void Dispose()
        {
            _requestQueueHandler.TruncateQueue();
        }

        [Fact]
        public void SH_DisplayTextMaxLengthTest()
        {
            string id1 = "1";
            //Setup services
            Uri baseAddress1 = null;
            ServiceHost sh1 = null;
            SetupIndexingServiceHost(out baseAddress1, out sh1);
            sh1.Open();

            try
            {
                //Reset indexes
                ResetAllIndexes();

                _requestQueueHandler.TruncateQueue();

                string s = "All work and no play makes jack a dull boy "; //43 chars
                StringBuilder sb = new StringBuilder();
                for (int i = 0; i < 100; i++)
                {
                    sb.Append(s);
                }

                sb.Append("This shold be searchable metadata");


                //Add an item
                IndexRequestItem item = new IndexRequestItem(id1, IndexAction.Add);
                item.Title = "Header test";
                item.DisplayText = sb.ToString();

                _searchHandler.UpdateIndex(item);

                _requestQueueHandler.ProcessQueue();

                FieldQuery fq = new FieldQuery("\"searchable metadata\"");
                SearchResults res = _searchHandler.GetSearchResults(fq, 1, 10);
                Assert.Equal(1, res.TotalHits);
                Assert.Equal(500, res.IndexResponseItems[0].DisplayText.Length);
                Assert.False(res.IndexResponseItems[0].DisplayText.Contains("searchable metadata"));

                _requestQueueHandler.TruncateQueue();

            }
            finally
            {
                sh1.Close();
            }
        }

        [Fact]
        public void SH_RequestResponseItemEqualityTest()
        {
            string id1 = "1";
            string id2 = "2";
            //Setup services
            Uri baseAddress1 = null;
            ServiceHost sh1 = null;
            SetupIndexingServiceHost(out baseAddress1, out sh1);
            sh1.Open();

            try
            {
                //Reset indexes
                ResetAllIndexes();

                //Add an item
                IndexRequestItem item = new IndexRequestItem(id1, IndexAction.Add);
                item.Title = "Header test";
                item.DisplayText = "Body test";
                item.Created = DateTime.Now;
                item.Modified = DateTime.Now;
                item.Uri = new Uri("http://www.google.com");
                item.Culture = "sv-SE";
                item.Authors.Add("me");
                item.Authors.Add("my self");
                item.Metadata = "Detta är ju massa meta data som man kan hålla på med";
                item.Categories.Add("cat1");
                item.Categories.Add("cat2");
                item.AccessControlList.Add("group1");
                item.AccessControlList.Add("group2");
                item.VirtualPathNodes.Add("vp1");
                item.VirtualPathNodes.Add("vp2");

                _searchHandler.UpdateIndex(item);

                _requestQueueHandler.ProcessQueue();

                AssertEqualToSearchResult(item, null);

                // Add item to another index with same data
                item = new IndexRequestItem(id1, IndexAction.Add);
                item.Title = "Header test";
                item.DisplayText = "Body test";
                item.Created = DateTime.Now;
                item.Modified = DateTime.Now;
                item.Uri = new Uri("http://www.google.com");
                item.Culture = "sv";
                item.Authors.Add("me");
                item.Authors.Add("my self");
                item.Metadata = "Detta är ju massa meta data som man kan hålla på med";
                item.Categories.Add("cat1");
                item.Categories.Add("cat2");
                item.AccessControlList.Add("group1");
                item.AccessControlList.Add("group2");
                item.VirtualPathNodes.Add("vp1");
                item.VirtualPathNodes.Add("vp2");

                item.NamedIndex = "testindex2";

                _searchHandler.UpdateIndex(item);

                _requestQueueHandler.ProcessQueue();

                AssertEqualToSearchResult(item, "testindex2");

                // Add item with no data to default index
                item = new IndexRequestItem(id2, IndexAction.Add);
                _searchHandler.UpdateIndex(item);

                _requestQueueHandler.ProcessQueue();

                AssertEqualToSearchResult(item, null);

                //Add an item other index with no data
                item = new IndexRequestItem(id2, IndexAction.Add);
                item.NamedIndex = "testindex2";
                _searchHandler.UpdateIndex(item);

                _requestQueueHandler.ProcessQueue();

                AssertEqualToSearchResult(item, "testindex2");

            }
            finally
            {
                sh1.Close();
            }
        }

        [Fact]
        public void SH_AllFieldMatchTest()
        {
            string id1 = "1";
            //Setup services
            Uri baseAddress1 = null;
            ServiceHost sh1 = null;
            SetupIndexingServiceHost(out baseAddress1, out sh1);
            sh1.Open();

            try
            {
                //Reset indexes
                ResetAllIndexes();

                //Add an item
                IndexRequestItem item = new IndexRequestItem(id1, IndexAction.Add);
                item.Title = "Header test";
                item.DisplayText = "Body test";
                item.Created = DateTime.Now;
                item.Modified = DateTime.Now;
                item.Uri = new Uri("http://www.google.com");
                item.Culture = "sv";
                item.Metadata = "Detta är ju massa meta data som man kan hålla på med";
                item.ItemType = "EPiServer.Search.IndexItem, EPiServer.Search";

                _searchHandler.UpdateIndex(item);

                _requestQueueHandler.ProcessQueue();

                FieldQuery q1 = new FieldQuery("Header test", Field.Title);
                FieldQuery q2 = new FieldQuery("Body test", Field.DisplayText);
                FieldQuery q3 = new FieldQuery("sv", Field.Culture);
                FieldQuery q4 = new FieldQuery("EPiServer.Search.IndexItem, EPiServer.Search", Field.ItemType);

                GroupQuery gq = new GroupQuery(LuceneOperator.AND);
                gq.QueryExpressions.Add(q1);
                gq.QueryExpressions.Add(q2);
                gq.QueryExpressions.Add(q3);
                gq.QueryExpressions.Add(q4);

                SearchResults res = _searchHandler.GetSearchResults(gq, 1, 10);
                Assert.Equal(1, res.TotalHits);

                gq = new GroupQuery(LuceneOperator.OR);
                gq.QueryExpressions.Add(q1);
                gq.QueryExpressions.Add(q2);
                gq.QueryExpressions.Add(q3);
                gq.QueryExpressions.Add(q4);

                res = _searchHandler.GetSearchResults(gq, 1, 10);
                Assert.Equal(1, res.TotalHits);

                AssertEqualToSearchResult(item, null);

                //Add an item to another index
                item = new IndexRequestItem(id1, IndexAction.Add);
                item.NamedIndex = "testindex2";
                _searchHandler.UpdateIndex(item);

                _requestQueueHandler.ProcessQueue();

                AssertEqualToSearchResult(item, "testindex2");

            }
            finally
            {
                sh1.Close();
            }
        }

        [Fact]
        public void SH_RangeSearchTest()
        {
            string id1 = "1";
            string id2 = "2";
            string id3 = "3";
            //Setup services
            Uri baseAddress1 = null;
            ServiceHost sh1 = null;
            SetupIndexingServiceHost(out baseAddress1, out sh1);
            sh1.Open();

            try
            {
                //Reset indexes
                ResetAllIndexes();

                IndexRequestItem item1 = new IndexRequestItem(id1, IndexAction.Add);
                item1.Title = "Header test";
                item1.DisplayText = "Body test";
                item1.Created = new DateTime(2010, 2, 3);
                item1.Modified = new DateTime(2010, 4, 2);
                _searchHandler.UpdateIndex(item1);

                IndexRequestItem item2 = new IndexRequestItem(id2, IndexAction.Add);
                item2.Title = "Header test";
                item2.DisplayText = "Body test";
                item2.Created = new DateTime(2009, 7, 18);
                item2.Modified = new DateTime(2009, 7, 8);
                _searchHandler.UpdateIndex(item2);

                IndexRequestItem item3 = new IndexRequestItem(id3, IndexAction.Add);
                item3.Title = "Header test";
                item3.DisplayText = "Body test";
                item3.Created = new DateTime(2009, 7, 18);
                item3.Modified = new DateTime(2009, 7, 8);
                _searchHandler.UpdateIndex(item3);

                _requestQueueHandler.ProcessQueue();

                RangeQuery r1 = new RangeQuery("20100101000000", "20100601000000", Field.Created, false);
                SearchResults res = _searchHandler.GetSearchResults(r1, 1, 10);
                Assert.Equal(1, res.TotalHits);

                r1 = new RangeQuery("20090101000000", "20100601000000", Field.Created, false);
                res = _searchHandler.GetSearchResults(r1, 1, 10);
                Assert.Equal(3, res.TotalHits);

                r1 = new RangeQuery("20100203000000", "20100402000000", Field.Created, true);
                res = _searchHandler.GetSearchResults(r1, 1, 10);
                Assert.Equal(1, res.TotalHits);

                r1 = new RangeQuery("20100203000000", "20100402000000", Field.Created, false);
                res = _searchHandler.GetSearchResults(r1, 1, 10);
                Assert.Equal(0, res.TotalHits);
            }
            finally
            {
                sh1.Close();
            }
        }

        [Fact]
        public void SH_ReferenceDataSearchTest()
        {
            string id1 = "1";
            string id2 = "ref1";
            string id3 = "2";

            //Setup services
            Uri baseAddress1 = null;
            ServiceHost sh1 = null;
            SetupIndexingServiceHost(out baseAddress1, out sh1);
            sh1.Open();

            try
            {
                //Reset indexes
                ResetAllIndexes();

                // Add items
                IndexRequestItem item = new IndexRequestItem(id1, IndexAction.Add);
                item.Title = "This is the header for id1 in default index";
                item.DisplayText = "This is the body for id1 in default index";
                item.Created = DateTime.Now.AddDays(-2);
                item.Metadata = "This is metadata for id1";
                _searchHandler.UpdateIndex(item);

                IndexRequestItem item2 = new IndexRequestItem(id3, IndexAction.Add);
                item2.Title = "This is the header for id2 in default index";
                item2.DisplayText = "This is the body for id2 in default index";
                item2.Created = DateTime.Now.AddDays(-2);
                item2.Metadata = "This is metadata for id2";
                _searchHandler.UpdateIndex(item2);

                _requestQueueHandler.ProcessQueue();

                // Search default field
                FieldQuery expr1 = new FieldQuery("\"header for id1\"");
                FieldQuery expr2 = new FieldQuery("\"body for id1\"");
                FieldQuery expr3 = new FieldQuery("\"metadata for id1\"");
                GroupQuery gq = new GroupQuery(LuceneOperator.AND);
                gq.QueryExpressions.Add(expr1);
                gq.QueryExpressions.Add(expr2);
                gq.QueryExpressions.Add(expr3);
                SearchResults results = _searchHandler.GetSearchResults(gq, 1, 20);
                Assert.Equal(1, results.IndexResponseItems.Count);

                AssertIndexItemEquality(item, results.IndexResponseItems[0]);

                // Search default field with not comitted reference data No hits
                expr1 = new FieldQuery("\"header for id1\"");
                expr2 = new FieldQuery("\"body for id1\"");
                expr3 = new FieldQuery("\"metadata for id1\"");
                FieldQuery expr5 = new FieldQuery("\"metadata for refItem\"");
                gq = new GroupQuery(LuceneOperator.AND);
                gq.QueryExpressions.Add(expr1);
                gq.QueryExpressions.Add(expr2);
                gq.QueryExpressions.Add(expr3);
                gq.QueryExpressions.Add(expr5);
                results = _searchHandler.GetSearchResults(gq, 1, 20);
                Assert.Equal(0, results.IndexResponseItems.Count);

                // Add reference data to item
                IndexRequestItem refItem = new IndexRequestItem(id2, IndexAction.Add);
                refItem.Title = "This is the header for refItem in default index";
                refItem.DisplayText = "This is the body for refItem in default index";
                refItem.Created = DateTime.Now.AddDays(-1);
                refItem.Metadata = "This is metadata for refItem";
                refItem.ReferenceId = id1;
                _searchHandler.UpdateIndex(refItem);

                _requestQueueHandler.ProcessQueue();

                Thread.Sleep(2000); // let reference data be merged in service

                // Search for reference data
                expr1 = new FieldQuery("\"header for id1\"");
                expr2 = new FieldQuery("\"body for id1\"");
                expr3 = new FieldQuery("\"metadata for id1\"");
                expr5 = new FieldQuery("\"metadata for refItem\"");
                FieldQuery expr6 = new FieldQuery("\"header for refItem\"");
                FieldQuery expr7 = new FieldQuery("\"body for refItem\"");
                FieldQuery expr8 = new FieldQuery("\"metadata for refItem\"");
                gq = new GroupQuery(LuceneOperator.AND);
                gq.QueryExpressions.Add(expr1);
                gq.QueryExpressions.Add(expr2);
                gq.QueryExpressions.Add(expr3);
                gq.QueryExpressions.Add(expr5);
                gq.QueryExpressions.Add(expr6);
                gq.QueryExpressions.Add(expr7);
                gq.QueryExpressions.Add(expr8);
                results = _searchHandler.GetSearchResults(gq, 1, 20);

                Assert.Equal(1, results.IndexResponseItems.Count);
                Assert.Equal("1", results.IndexResponseItems[0].Id);

                AssertIndexItemEquality(item, results.IndexResponseItems[0]);

            }
            finally
            {
                sh1.Close();
            }
        }

        [Fact]
        [Trait("Category", "PDF")]
        public void SH_DataUriUpdateTest()
        {
            string id0 = "id0";
            //string id1 = "id1";
            //string id2 = "id2";
            //Setup services
            Uri baseAddress1 = null;
            ServiceHost sh1 = null;
            SetupIndexingServiceHost(out baseAddress1, out sh1);
            sh1.Open();

            string appPath = AppDomain.CurrentDomain.BaseDirectory;
            if (System.Web.Hosting.HostingEnvironment.IsHosted)
            {
                appPath = System.Web.Hosting.HostingEnvironment.ApplicationPhysicalPath;
            }

            string testDocumentPath0 = Path.Combine(appPath, @"Resources\TestFile.txt");
            string testDocumentPath1 = Path.Combine(appPath, @"Resources\test.pdf");
            string testDocumentPath2 = Path.Combine(appPath, @"Resources\test.doc");
            string testDocumentPath3 = Path.Combine(appPath, @"Resources\test.docx");
            string testDocumentPath4 = Path.Combine(appPath, @"Resources\test big.pdf");


            try
            {
                _requestQueueHandler.TruncateQueue();

                //Reset indexes
                ResetAllIndexes();

                //Add an item with text file
                IndexRequestItem item = new IndexRequestItem(id0, IndexAction.Add);
                item.DataUri = new Uri(testDocumentPath0);

                _searchHandler.UpdateIndex(item);

                _requestQueueHandler.ProcessQueue();

                Thread.Sleep(7000); //Wait for task queue in indexing service to finish

                FieldQuery fe = new FieldQuery("\"simple text file\"");
                SearchResults results = _searchHandler.GetSearchResults(fe, 1, 20);
                Assert.Equal(1, results.IndexResponseItems.Count);
                IndexResponseItem resultItem = results.IndexResponseItems[0];

                // Update the item with another data uri

                //Add an item with text file
                item = new IndexRequestItem(id0, IndexAction.Update);
                item.DataUri = new Uri(testDocumentPath1);

                _searchHandler.UpdateIndex(item);

                _requestQueueHandler.ProcessQueue();

                Thread.Sleep(5000); //Wait for task queue in indexing service to finish

                fe = new FieldQuery("\"plain and simple text file that we try to index\"");
                results = _searchHandler.GetSearchResults(fe, 1, 20);
                Assert.Equal(0, results.IndexResponseItems.Count);

                fe = new FieldQuery("\"test pdf document\"");
                results = _searchHandler.GetSearchResults(fe, 1, 20);
                Assert.Equal(1, results.IndexResponseItems.Count);

                // Update the item with no data uri
                item = new IndexRequestItem(id0, IndexAction.Update);
                _searchHandler.UpdateIndex(item);

                _requestQueueHandler.ProcessQueue();

                Thread.Sleep(5000); //Wait for task queue in indexing service to finish

                fe = new FieldQuery("\"test pdf document\"");
                results = _searchHandler.GetSearchResults(fe, 1, 20);
                Assert.Equal(0, results.IndexResponseItems.Count);

            }
            finally
            {
                sh1.Close();
            }
        }

        [Fact]
        [Trait("Category", "PDF")]
        public void SH_DataUriAddPDFTest()
        {
            string id0 = "id0";

            //Setup services
            Uri baseAddress1 = null;
            ServiceHost sh1 = null;
            SetupIndexingServiceHost(out baseAddress1, out sh1);
            sh1.Open();

            string appPath = AppDomain.CurrentDomain.BaseDirectory;
            if (System.Web.Hosting.HostingEnvironment.IsHosted)
            {
                appPath = System.Web.Hosting.HostingEnvironment.ApplicationPhysicalPath;
            }

            string testDocumentPath0 = Path.Combine(appPath, @"Resources\test.pdf");

            try
            {
                _requestQueueHandler.TruncateQueue();

                //Reset indexes
                ResetAllIndexes();

                //Add an item with text file
                IndexRequestItem item = new IndexRequestItem(id0, IndexAction.Add);
                item.Title = "Text Header test";
                item.DisplayText = "Text Body test";
                item.Created = DateTime.Now;
                item.Modified = DateTime.Now;
                item.Uri = new Uri("http://www.google.com");
                item.Culture = "sv";
                item.Metadata = "This is meta data";
                item.DataUri = new Uri(testDocumentPath0);

                _searchHandler.UpdateIndex(item);

                _requestQueueHandler.ProcessQueue();

                Thread.Sleep(5000); //Wait for task queue in indexing service to finish

                FieldQuery fe = new FieldQuery("\"test pdf document\"");
                SearchResults results = _searchHandler.GetSearchResults(fe, 1, 20);
                Assert.Equal(1, results.IndexResponseItems.Count);
            }
            finally
            {
                sh1.Close();
            }
        }

        [Fact]
        [Trait("Category", "PDF")]
        public void SH_DataUriAddBigPDFTest()
        {
            string id0 = "id0";

            //Setup services
            Uri baseAddress1 = null;
            ServiceHost sh1 = null;
            SetupIndexingServiceHost(out baseAddress1, out sh1);
            sh1.Open();

            string appPath = AppDomain.CurrentDomain.BaseDirectory;
            if (System.Web.Hosting.HostingEnvironment.IsHosted)
            {
                appPath = System.Web.Hosting.HostingEnvironment.ApplicationPhysicalPath;
            }

            string testDocumentPath0 = Path.Combine(appPath, @"Resources\test big.pdf");

            try
            {
                _requestQueueHandler.TruncateQueue();

                //Reset indexes
                ResetAllIndexes();

                //Add an item with text file
                IndexRequestItem item = new IndexRequestItem(id0, IndexAction.Add);
                item.Title = "Text Header test";
                item.DisplayText = "Text Body test";
                item.Created = DateTime.Now;
                item.Modified = DateTime.Now;
                item.Uri = new Uri("http://www.google.com");
                item.Culture = "sv";
                item.Metadata = "This is meta data";
                item.DataUri = new Uri(testDocumentPath0);

                _searchHandler.UpdateIndex(item);

                _requestQueueHandler.ProcessQueue();

                Thread.Sleep(20000); //Wait for task queue in indexing service to finish

                FieldQuery fe = new FieldQuery("\"StarCommunity is actually a module of Required Framework Components\"");
                SearchResults results = _searchHandler.GetSearchResults(fe, 1, 20);
                Assert.Equal(1, results.IndexResponseItems.Count);
            }
            finally
            {
                sh1.Close();
            }
        }

        [Fact]
        public void SH_DataUriAddWordDocXTest()
        {
            string id0 = "id0";

            //Setup services
            Uri baseAddress1 = null;
            ServiceHost sh1 = null;
            SetupIndexingServiceHost(out baseAddress1, out sh1);
            sh1.Open();

            string appPath = AppDomain.CurrentDomain.BaseDirectory;
            if (System.Web.Hosting.HostingEnvironment.IsHosted)
            {
                appPath = System.Web.Hosting.HostingEnvironment.ApplicationPhysicalPath;
            }

            string testDocumentPath0 = Path.Combine(appPath, @"Resources\test.docx");

            try
            {
                _requestQueueHandler.TruncateQueue();

                //Reset indexes
                ResetAllIndexes();

                //Add an item with text file
                IndexRequestItem item = new IndexRequestItem(id0, IndexAction.Add);
                item.Title = "Text Header test";
                item.DisplayText = "Text Body test";
                item.Created = DateTime.Now;
                item.Modified = DateTime.Now;
                item.Uri = new Uri("http://www.google.com");
                item.Culture = "sv";
                item.Metadata = "This is meta data";
                item.DataUri = new Uri(testDocumentPath0);

                _searchHandler.UpdateIndex(item);

                _requestQueueHandler.ProcessQueue();

                Thread.Sleep(5000); //Wait for task queue in indexing service to finish

                FieldQuery fe = new FieldQuery("\"This is a test word document\"");
                SearchResults results = _searchHandler.GetSearchResults(fe, 1, 20);
                Assert.Equal(1, results.IndexResponseItems.Count);
            }
            finally
            {
                sh1.Close();
            }
        }

        [Fact]
        public void SH_DataUriAddWordDocTest()
        {
            string id0 = "id0";

            //Setup services
            Uri baseAddress1 = null;
            ServiceHost sh1 = null;
            SetupIndexingServiceHost(out baseAddress1, out sh1);
            sh1.Open();

            string appPath = AppDomain.CurrentDomain.BaseDirectory;
            if (System.Web.Hosting.HostingEnvironment.IsHosted)
            {
                appPath = System.Web.Hosting.HostingEnvironment.ApplicationPhysicalPath;
            }

            string testDocumentPath0 = Path.Combine(appPath, @"Resources\test.doc");

            try
            {
                _requestQueueHandler.TruncateQueue();

                //Reset indexes
                ResetAllIndexes();

                //Add an item with text file
                IndexRequestItem item = new IndexRequestItem(id0, IndexAction.Add);
                item.Title = "Text Header test";
                item.DisplayText = "Text Body test";
                item.Created = DateTime.Now;
                item.Modified = DateTime.Now;
                item.Uri = new Uri("http://www.google.com");
                item.Culture = "sv";
                item.Metadata = "This is meta data";
                item.DataUri = new Uri(testDocumentPath0);

                _searchHandler.UpdateIndex(item);

                _requestQueueHandler.ProcessQueue();

                Thread.Sleep(5000); //Wait for task queue in indexing service to finish

                FieldQuery fe = new FieldQuery("\"test word 2007\"");
                SearchResults results = _searchHandler.GetSearchResults(fe, 1, 20);
                Assert.Equal(1, results.IndexResponseItems.Count);
            }
            finally
            {
                sh1.Close();
            }
        }

        [Fact]
        public void SH_DataUriAddTextFileTest()
        {
            string id0 = "id0";

            //Setup services
            Uri baseAddress1 = null;
            ServiceHost sh1 = null;
            SetupIndexingServiceHost(out baseAddress1, out sh1);
            sh1.Open();

            string appPath = AppDomain.CurrentDomain.BaseDirectory;
            if (System.Web.Hosting.HostingEnvironment.IsHosted)
            {
                appPath = System.Web.Hosting.HostingEnvironment.ApplicationPhysicalPath;
            }

            string testDocumentPath0 = Path.Combine(appPath, @"Resources\TestFile.txt");

            try
            {
                _requestQueueHandler.TruncateQueue();

                //Reset indexes
                ResetAllIndexes();

                //Add an item with text file
                IndexRequestItem item = new IndexRequestItem(id0, IndexAction.Add);
                item.Title = "Text Header test";
                item.DisplayText = "Text Body test";
                item.Created = DateTime.Now;
                item.Modified = DateTime.Now;
                item.Uri = new Uri("http://www.google.com");
                item.Culture = "sv";
                item.Metadata = "This is meta data";
                item.DataUri = new Uri(testDocumentPath0);

                _searchHandler.UpdateIndex(item);

                _requestQueueHandler.ProcessQueue();

                Thread.Sleep(5000); //Wait for task queue in indexing service to finish

                FieldQuery fe = new FieldQuery("\"plain and simple text file that we try to index\"");
                SearchResults results = _searchHandler.GetSearchResults(fe, 1, 20);
                Assert.Equal(1, results.IndexResponseItems.Count);
                IndexResponseItem resultItem = results.IndexResponseItems[0];
            }
            finally
            {
                sh1.Close();
            }
        }

        [Fact]
        [Trait("Category", "PDF")]
        public void SH_DataUriWithReferenceTest()
        {
            string id0 = "id0";
            string id1 = "id1";

            //Setup services
            Uri baseAddress1 = null;
            ServiceHost sh1 = null;
            SetupIndexingServiceHost(out baseAddress1, out sh1);
            sh1.Open();

            string appPath = AppDomain.CurrentDomain.BaseDirectory;
            if (System.Web.Hosting.HostingEnvironment.IsHosted)
            {
                appPath = System.Web.Hosting.HostingEnvironment.ApplicationPhysicalPath;
            }

            string testDocumentPath0 = Path.Combine(appPath, @"Resources\test.pdf");

            try
            {
                _requestQueueHandler.TruncateQueue();

                //Reset indexes
                ResetAllIndexes();

                //Add an item with a pdf file
                IndexRequestItem item1 = new IndexRequestItem(id0, IndexAction.Add);
                item1.Title = "Text Header test";
                item1.DisplayText = "Text Body test";
                item1.DataUri = new Uri(testDocumentPath0);

                _searchHandler.UpdateIndex(item1);

                //Add an item referencing id1
                IndexRequestItem item2 = new IndexRequestItem(id1, IndexAction.Add);
                item2.Title = "Text Header test reference item";
                item2.DisplayText = "Text Body test";
                item2.ReferenceId = id0;

                _searchHandler.UpdateIndex(item2);

                _requestQueueHandler.ProcessQueue();

                Thread.Sleep(5000); //Wait for task queue in indexing service to finish

                FieldQuery fe = new FieldQuery("\"test pdf document\"");
                SearchResults results = _searchHandler.GetSearchResults(fe, 1, 20);
                Assert.Equal(1, results.IndexResponseItems.Count);
                Assert.Equal(id0, results.IndexResponseItems[0].Id);

                fe = new FieldQuery("\"test reference item\"");
                results = _searchHandler.GetSearchResults(fe, 1, 20);
                Assert.Equal(1, results.IndexResponseItems.Count);
                Assert.Equal(id0, results.IndexResponseItems[0].Id);

                // Update reference item
                IndexRequestItem item3 = new IndexRequestItem(id1, IndexAction.Update);
                item3.Title = "Text Header test reference item update";
                item3.DisplayText = "Text Body test";
                item3.ReferenceId = id0;

                _searchHandler.UpdateIndex(item3);

                _requestQueueHandler.ProcessQueue();

                fe = new FieldQuery("\"test reference item update\"");
                results = _searchHandler.GetSearchResults(fe, 1, 20);
                Assert.Equal(1, results.IndexResponseItems.Count);
                Assert.Equal(id0, results.IndexResponseItems[0].Id);

            }
            finally
            {
                sh1.Close();
            }
        }

        [Fact]
        [Trait("Category", "PDF")]
        public void SH_DataUriAsReferenceTest()
        {
            string id0 = "id0";
            string id1 = "id1";

            //Setup services
            Uri baseAddress1 = null;
            ServiceHost sh1 = null;
            SetupIndexingServiceHost(out baseAddress1, out sh1);
            sh1.Open();

            string appPath = AppDomain.CurrentDomain.BaseDirectory;
            if (System.Web.Hosting.HostingEnvironment.IsHosted)
            {
                appPath = System.Web.Hosting.HostingEnvironment.ApplicationPhysicalPath;
            }

            string testDocumentPath0 = Path.Combine(appPath, @"Resources\test.pdf");

            try
            {
                _requestQueueHandler.TruncateQueue();

                //Reset indexes
                ResetAllIndexes();

                //Add an item
                IndexRequestItem item1 = new IndexRequestItem(id0, IndexAction.Add);
                item1.Title = "Text title main item";
                item1.DisplayText = "Text Body test";

                _searchHandler.UpdateIndex(item1);

                //Add an item with a pdf file uri referencing id0
                IndexRequestItem item2 = new IndexRequestItem(id1, IndexAction.Add);
                item2.Title = "Text title test";
                item2.DisplayText = "Text Body test";
                item2.DataUri = new Uri(testDocumentPath0);
                item2.ReferenceId = id0; 

                _searchHandler.UpdateIndex(item2);
                
                _requestQueueHandler.ProcessQueue();

                Thread.Sleep(5000); //Wait for task queue in indexing service to finish

                FieldQuery fe = new FieldQuery("\"title main item\"");
                SearchResults results = _searchHandler.GetSearchResults(fe, 1, 20);
                Assert.Equal(1, results.IndexResponseItems.Count);
                Assert.Equal(id0, results.IndexResponseItems[0].Id);

                fe = new FieldQuery("\"test pdf document\"");
                results = _searchHandler.GetSearchResults(fe, 1, 20);
                Assert.Equal(1, results.IndexResponseItems.Count);
                Assert.Equal(id0, results.IndexResponseItems[0].Id);

                // Update reference item
                IndexRequestItem item3 = new IndexRequestItem(id1, IndexAction.Update);
                item3.Title = "Text title test reference item update";
                item3.DisplayText = "Text Body test";
                item3.DataUri = new Uri(testDocumentPath0);
                item3.ReferenceId = id0;

                _searchHandler.UpdateIndex(item3);

                _requestQueueHandler.ProcessQueue();

                Thread.Sleep(5000); //Wait for task queue in indexing service to finish

                fe = new FieldQuery("\"test reference item update\"");
                results = _searchHandler.GetSearchResults(fe, 1, 20);
                Assert.Equal(1, results.IndexResponseItems.Count);
                Assert.Equal(id0, results.IndexResponseItems[0].Id);
            }
            finally
            {
                sh1.Close();
            }
        }

        [Fact]
        public void SH_ACLTest()
        {
            string id1 = "1";
            //Setup services
            Uri baseAddress1 = null;
            ServiceHost sh1 = null;
            SetupIndexingServiceHost(out baseAddress1, out sh1);
            sh1.Open();

            try
            {

                //Reset indexes
                ResetAllIndexes();

                //Add an item
                IndexRequestItem item = new IndexRequestItem(id1, IndexAction.Add);
                item.Title = "Header test";
                item.DisplayText = "Body test";
                item.Created = DateTime.Now;
                item.Modified = DateTime.Now;
                item.Uri = new Uri("http://www.google.com");
                item.Culture = "sv";
                item.Metadata = "Detta är ju massa meta data som man kan hålla på med";
                item.AccessControlList.Add("G:me");
                item.AccessControlList.Add("U:myself");
                item.AccessControlList.Add("G:and irene");

                _searchHandler.UpdateIndex(item);

                _requestQueueHandler.ProcessQueue();

                FieldQuery fe = new FieldQuery(item.Id, Field.Id);

                AccessControlListQuery aclQuery = new AccessControlListQuery();
                aclQuery.Items.Add("G:me");

                GroupQuery gq1 = new GroupQuery(LuceneOperator.AND);
                gq1.QueryExpressions.Add(fe);
                gq1.QueryExpressions.Add(aclQuery);

                SearchResults results = _searchHandler.GetSearchResults(gq1, 1, 20);
                Assert.Equal(1, results.IndexResponseItems.Count);
                IndexResponseItem resultItem = results.IndexResponseItems[0];

                AssertIndexItemEquality(item, resultItem);

                //Group test
                FieldQuery fe2 = new FieldQuery(item.Id, Field.Id);
                AccessControlListQuery aclQuery2 = new AccessControlListQuery();
                aclQuery2.Items.Add("G:and irene");

                GroupQuery gq2 = new GroupQuery(LuceneOperator.AND);
                gq2.QueryExpressions.Add(fe2);
                gq2.QueryExpressions.Add(aclQuery2);

                results = _searchHandler.GetSearchResults(gq2, 1, 20);
                Assert.Equal(1, results.IndexResponseItems.Count);
                resultItem = results.IndexResponseItems[0];
                AssertIndexItemEquality(item, resultItem);

                //Groups and users test
                FieldQuery fe3 = new FieldQuery(item.Id, Field.Id);
                AccessControlListQuery aclQuery3 = new AccessControlListQuery();
                aclQuery3.Items.Add("G:me");
                aclQuery3.Items.Add("G:and irene");
                aclQuery3.Items.Add("U:myself");

                GroupQuery gq3 = new GroupQuery(LuceneOperator.AND);
                gq3.QueryExpressions.Add(fe3);
                gq3.QueryExpressions.Add(aclQuery3);

                results = _searchHandler.GetSearchResults(gq3, 1, 20);
                Assert.Equal(1, results.IndexResponseItems.Count);
                resultItem = results.IndexResponseItems[0];

                AssertEqualToSearchResult(item, null);

                //No access test
                FieldQuery fe4 = new FieldQuery(item.Id, Field.Id);
                AccessControlListQuery aclQuery4 = new AccessControlListQuery();
                aclQuery4.Items.Add("G:no access");

                GroupQuery gq4 = new GroupQuery(LuceneOperator.AND);
                gq4.QueryExpressions.Add(fe4);
                gq4.QueryExpressions.Add(aclQuery4);

                results = _searchHandler.GetSearchResults(gq4, 1, 20);
                Assert.Empty(results.IndexResponseItems);

                //Update the item
                item = new IndexRequestItem(id1, IndexAction.Update);
                item.Title = "Header test";
                item.DisplayText = "Body test";
                item.Created = DateTime.Now;
                item.Modified = DateTime.Now;
                item.Uri = new Uri("http://www.google.com");
                item.Culture = "sv";
                item.Metadata = "Detta är ju massa meta data som man kan hålla på med";
                item.AccessControlList.Add("G:me");
                item.AccessControlList.Add("U:myself");

                _searchHandler.UpdateIndex(item);

                _requestQueueHandler.ProcessQueue();

                // Check that "and irene" dont have access after update
                AccessControlListQuery aclQuery5 = new AccessControlListQuery();
                aclQuery5.Items.Add("G:and irene");
                FieldQuery fe5 = new FieldQuery(item.Id, Field.Id);
                GroupQuery gq5 = new GroupQuery(LuceneOperator.AND);
                gq5.QueryExpressions.Add(fe5);
                gq5.QueryExpressions.Add(aclQuery5);
                results = _searchHandler.GetSearchResults(gq2, 1, 20);
                Assert.Empty(results.IndexResponseItems);

                // Check inner operator "AND"
                AccessControlListQuery aclQuery6 = new AccessControlListQuery(LuceneOperator.AND);
                aclQuery6.Items.Add("G:me");
                aclQuery6.Items.Add("U:myself");
                results = _searchHandler.GetSearchResults(aclQuery6, 1, 20);
                Assert.Equal(1, results.IndexResponseItems.Count);
            }
            finally
            {
                sh1.Close();
            }
        }

        [Fact]
        public void SH_RemoveTest()
        {
            string id = Guid.NewGuid().ToString();
            //Setup services
            Uri baseAddress1 = null;
            ServiceHost sh1 = null;
            SetupIndexingServiceHost(out baseAddress1, out sh1);
            sh1.Open();


            try
            {
                //Reset indexes
                ResetAllIndexes();

                //Add two items
                IndexRequestItem item1 = new IndexRequestItem(id, IndexAction.Add);
                _searchHandler.UpdateIndex(item1);

                IndexRequestItem item2 = new IndexRequestItem(id, IndexAction.Add);
                item2.NamedIndex = "testindex2";
                _searchHandler.UpdateIndex(item2);

                _requestQueueHandler.ProcessQueue();

                AssertEqualToSearchResult(item1, null);
                AssertEqualToSearchResult(item2, "testindex2");

                //Remove item from default index
                IndexRequestItem item3 = new IndexRequestItem(id, IndexAction.Remove);
                _searchHandler.UpdateIndex(item3);

                _requestQueueHandler.ProcessQueue();

                //The item should be removed
                EscapedFieldQuery fe = new EscapedFieldQuery(id, Field.Id);
                SearchResults results = _searchHandler.GetSearchResults(fe, 1, 20);
                Assert.Equal(0, results.IndexResponseItems.Count);


                //Remove item from named index index
                IndexRequestItem item4 = new IndexRequestItem(id, IndexAction.Remove);
                item4.NamedIndex = "testindex2";
                _searchHandler.UpdateIndex(item4);

                _requestQueueHandler.ProcessQueue();

                //The item should be removed
                Collection<string> namedIndexes = new Collection<string>();
                fe = new EscapedFieldQuery(id, Field.Id);
                results = _searchHandler.GetSearchResults(fe, null, namedIndexes, 1, 20);
                Assert.Equal(0, results.IndexResponseItems.Count);
            }
            finally
            {
                sh1.Close();
            }
        }

        [Fact]
        public void SH_ReservedCharactersInIdTest()
        {
            string id = "~" + Guid.NewGuid().ToString() + "(";
            //Setup services
            Uri baseAddress1 = null;
            ServiceHost sh1 = null;
            SetupIndexingServiceHost(out baseAddress1, out sh1);
            sh1.Open();


            try
            {
                //Reset indexes
                ResetAllIndexes();

                //Add two items
                IndexRequestItem item1 = new IndexRequestItem(id, IndexAction.Add);
                _searchHandler.UpdateIndex(item1);

                IndexRequestItem item2 = new IndexRequestItem(id, IndexAction.Add);
                item2.NamedIndex = "testindex2";
                _searchHandler.UpdateIndex(item2);

                _requestQueueHandler.ProcessQueue();

                AssertEqualToSearchResult(item1, null);
                AssertEqualToSearchResult(item2, "testindex2");

                //Remove item from default index
                IndexRequestItem item3 = new IndexRequestItem(id, IndexAction.Remove);
                _searchHandler.UpdateIndex(item3);

                _requestQueueHandler.ProcessQueue();

                //The item should be removed
                EscapedFieldQuery fe = new EscapedFieldQuery(id, Field.Id);
                SearchResults results = _searchHandler.GetSearchResults(fe, 1, 20);
                Assert.Equal(0, results.IndexResponseItems.Count);


                //Remove item from named index index
                IndexRequestItem item4 = new IndexRequestItem(id, IndexAction.Remove);
                item4.NamedIndex = "testindex2";
                _searchHandler.UpdateIndex(item4);

                _requestQueueHandler.ProcessQueue();

                //The item should be removed
                Collection<string> namedIndexes = new Collection<string>();
                fe = new EscapedFieldQuery(id, Field.Id);
                results = _searchHandler.GetSearchResults(fe, null, namedIndexes, 1, 20);
                Assert.Equal(0, results.IndexResponseItems.Count);
            }
            finally
            {
                sh1.Close();
            }
        }

        [Fact]
        public void SH_UpdateTest()
        {
            string id = Guid.NewGuid().ToString();
            //Setup services
            Uri baseAddress1 = null;
            ServiceHost sh1 = null;
            SetupIndexingServiceHost(out baseAddress1, out sh1);
            sh1.Open();

            try
            {
                //Reset indexes
                ResetAllIndexes();

                //Add an item
                IndexRequestItem item1 = new IndexRequestItem(id, IndexAction.Add);
                item1.Title = "header test";
                _searchHandler.UpdateIndex(item1);

                _requestQueueHandler.ProcessQueue();

                AssertEqualToSearchResult(item1, null);

                //Update the item
                IndexRequestItem item2 = new IndexRequestItem(id, IndexAction.Update);
                item2.Title = "header test updated";
                _searchHandler.UpdateIndex(item2);

                _requestQueueHandler.ProcessQueue();

                AssertEqualToSearchResult(item2, null);
            }
            finally
            {
                sh1.Close();
            }
        }

        [Fact]
        public void SH_UpdateNamedIndexTest()
        {
            string id = Guid.NewGuid().ToString();
            //Setup services
            Uri baseAddress1 = null;
            ServiceHost sh1 = null;
            SetupIndexingServiceHost(out baseAddress1, out sh1);
            sh1.Open();

            try
            {
                //Reset indexes
                ResetAllIndexes();

                //Add an item
                IndexRequestItem item1 = new IndexRequestItem(id, IndexAction.Add);
                item1.Title = "header test";
                item1.DisplayText = "body test";
                item1.NamedIndex = "testindex3";
                _searchHandler.UpdateIndex(item1);

                _requestQueueHandler.ProcessQueue();

                AssertEqualToSearchResult(item1, "testindex3");

                //Update the item by including the update endpoint for the mockup service
                IndexRequestItem item2 = new IndexRequestItem(id, IndexAction.Update);
                item2.Title = "header test updated";
                item2.DisplayText = "body test updated";
                item2.NamedIndex = "testindex3";
                _searchHandler.UpdateIndex(item2);

                _requestQueueHandler.ProcessQueue();

                AssertEqualToSearchResult(item2, "testindex3");

            }
            finally
            {
                sh1.Close();
            }
        }

        [Fact]
        public void SH_PagingTest()
        {
            //Setup services
            Uri baseAddress1 = null;
            ServiceHost sh1 = null;
            SetupIndexingServiceHost(out baseAddress1, out sh1);
            sh1.Open();

            SearchSettings.Options.UseIndexingServicePaging = true;

            try
            {
                //Reset indexes
                ResetAllIndexes();

                CreateMultipleRequests();

                _requestQueueHandler.ProcessQueue();

                SearchResults results = null;

                //Get all items where any of the words exist in field: default and index: default
                FieldQuery expr = new FieldQuery("header");
                results = _searchHandler.GetSearchResults(expr, 1, 7);
                Assert.Equal(7, results.TotalHits);
                Assert.Equal(7, results.IndexResponseItems.Count);

                expr = new FieldQuery("header");
                results = _searchHandler.GetSearchResults(expr, 1, 3);
                Assert.Equal(7, results.TotalHits);
                Assert.Equal(3, results.IndexResponseItems.Count);

                expr = new FieldQuery("header");
                results = _searchHandler.GetSearchResults(expr, 2, 3);
                Assert.Equal(7, results.TotalHits);
                Assert.Equal(3, results.IndexResponseItems.Count);

                expr = new FieldQuery("header");
                results = _searchHandler.GetSearchResults(expr, 3, 3);
                Assert.Equal(7, results.TotalHits);
                Assert.Equal(1, results.IndexResponseItems.Count);
            }
            finally
            {

                sh1.Close();
            }
        }

        [Fact]
        public void SH_DefaultFieldTest()
        {
            //Setup services
            Uri baseAddress1 = null;
            ServiceHost sh1 = null;
            SetupIndexingServiceHost(out baseAddress1, out sh1);
            sh1.Open();
            try
            {
                ResetAllIndexes();

                IndexRequestItem item = new IndexRequestItem("1", IndexAction.Add);
                item.Title = "testar lite svårt";
                item.DisplayText = "testing introtext";
                _searchHandler.UpdateIndex(item);

                item = new IndexRequestItem("2", IndexAction.Add);
                item.Title = "testing header2";
                item.DisplayText = "testar lite svårt";
                _searchHandler.UpdateIndex(item);

                item = new IndexRequestItem("3", IndexAction.Add);
                item.Title = "header3";
                item.DisplayText = "testar lite lätt";
                item.Metadata = "metadata med svårt innehåll";
                _searchHandler.UpdateIndex(item);

                _requestQueueHandler.ProcessQueue();

                FieldQuery q = new FieldQuery("testing");
                SearchResults res = _searchHandler.GetSearchResults(q, 1, 100);
                Assert.Equal(2, res.IndexResponseItems.Count);

                q = new FieldQuery("svårt");
                res = _searchHandler.GetSearchResults(q, 1, 100);
                Assert.Equal(3, res.IndexResponseItems.Count);
            }
            finally
            {
                sh1.Close();
            }
        }

        [Fact]
        public void SH_UpdateNonExistingItemShouldAdd()
        {
            //Setup services
            Uri baseAddress1 = null;
            ServiceHost sh1 = null;
            SetupIndexingServiceHost(out baseAddress1, out sh1);
            sh1.Open();
            try
            {
                ResetAllIndexes();

                IndexRequestItem item = new IndexRequestItem("1", IndexAction.Update);
                item.Title = "testar lite svårt";
                item.DisplayText = "testing introtext";
                _searchHandler.UpdateIndex(item);

                _requestQueueHandler.ProcessQueue();

                FieldQuery q = new FieldQuery("testing");
                SearchResults res = _searchHandler.GetSearchResults(q, 1, 100);
                Assert.Equal(1, res.IndexResponseItems.Count);
            }
            finally
            {
                sh1.Close();
            }
        }


        [Fact]
        public void SH_FieldExpressionsTest()
        {
            //Setup services
            Uri baseAddress1 = null;
            ServiceHost sh1 = null;
            SetupIndexingServiceHost(out baseAddress1, out sh1);
            sh1.Open();

            try
            {
                _requestQueueHandler.TruncateQueue();

                //Reset indexes
                ResetAllIndexes();

                CreateMultipleRequests();
                _requestQueueHandler.ProcessQueue();

                SearchResults results = null;

                //Get all items where any of the words exist in field: default and index: default
                FieldQuery expr = new FieldQuery("\"header for\"");
                results = _searchHandler.GetSearchResults(expr, 1, 20);
                Assert.Equal(7, results.IndexResponseItems.Count);

                GroupQuery gq = new GroupQuery(LuceneOperator.AND);
                FieldQuery fq1 = new FieldQuery("\"this is\"");
                FieldQuery fq2 = new FieldQuery("\"header for id3\"");
                gq.QueryExpressions.Add(fq1);
                gq.QueryExpressions.Add(fq2);
                results = _searchHandler.GetSearchResults(gq, 1, 20);
                Assert.Equal(1, results.IndexResponseItems.Count);

                gq = new GroupQuery(LuceneOperator.OR);
                fq1 = new FieldQuery("\"this is\"");
                fq2 = new FieldQuery("\"header for id3\"");
                gq.QueryExpressions.Add(fq1);
                gq.QueryExpressions.Add(fq2);
                results = _searchHandler.GetSearchResults(gq, 1, 20);
                Assert.Equal(7, results.IndexResponseItems.Count);

                expr = new FieldQuery("\"är data i body\"");
                results = _searchHandler.GetSearchResults(expr, 1, 20);
                Assert.Equal(1, results.IndexResponseItems.Count);

                expr = new FieldQuery("\"är Data i Body\"");
                results = _searchHandler.GetSearchResults(expr, 1, 20);
                Assert.Equal(1, results.IndexResponseItems.Count);

                expr = new FieldQuery("\"är Data i Body*\"");
                results = _searchHandler.GetSearchResults(expr, 1, 20);
                Assert.Equal(1, results.IndexResponseItems.Count);

                expr = new FieldQuery("\"är data i meta\"");
                results = _searchHandler.GetSearchResults(expr, 1, 20);
                Assert.Equal(1, results.IndexResponseItems.Count);

                expr = new FieldQuery("\"testas lite svårt\"");
                results = _searchHandler.GetSearchResults(expr, 1, 20);
                Assert.Equal(1, results.IndexResponseItems.Count);

                expr = new FieldQuery("\"testas lite svart\"");
                results = _searchHandler.GetSearchResults(expr, 1, 20);
                Assert.Equal(0, results.IndexResponseItems.Count);

                expr = new FieldQuery("svårt");
                results = _searchHandler.GetSearchResults(expr, 1, 20);
                Assert.Equal(1, results.IndexResponseItems.Count);

                //Get top 5 items where any of the words exist in field: default and index: default
                expr = new FieldQuery("\"header for\"");
                results = _searchHandler.GetSearchResults(expr, 1, 5);
                Assert.Equal(5, results.IndexResponseItems.Count);

                //Get all items where the exact phrase exist in field: default and index: default
                expr = new FieldQuery("\"header for id1\"");
                results = _searchHandler.GetSearchResults(expr, 1, 20);
                Assert.Equal(1, results.IndexResponseItems.Count);

                //Get all items where the exact phrase exist in field: header and index: default
                expr = new FieldQuery("\"header for id1\"", Field.Title);
                results = _searchHandler.GetSearchResults(expr, 1, 20);
                Assert.Equal(1, results.IndexResponseItems.Count);

                //Get all items where the exact phrase exist in field: body and index: default
                expr = new FieldQuery("\"header for\"", Field.DisplayText);
                results = _searchHandler.GetSearchResults(expr, 1, 20);
                Assert.Equal(0, results.IndexResponseItems.Count);

                //Get all items where any of the words exist in field: header and index: testindex2
                expr = new FieldQuery("\"header for\"", Field.Title);
                Collection<string> indexes1 = new Collection<string>();
                indexes1.Add("testindex2");
                results = _searchHandler.GetSearchResults(expr, null, indexes1, 1, 20);
                Assert.Equal(3, results.IndexResponseItems.Count);

                //Get all items where any of the words exist in field: default and index: testindex3
                expr = new FieldQuery("\"header for\"");
                Collection<string> indexes2 = new Collection<string>();
                indexes2.Add("testindex3");
                results = _searchHandler.GetSearchResults(expr, null, indexes2, 1, 20);
                Assert.Equal(4, results.IndexResponseItems.Count);

                expr = new FieldQuery("\"header for\"");
                Collection<string> indexes3 = new Collection<string>();
                indexes3.Add("testindex2");
                indexes3.Add("testindex3");
                results = _searchHandler.GetSearchResults(expr, null, indexes3, 1, 20);
                Assert.Equal(7, results.IndexResponseItems.Count);

                expr = new FieldQuery("Cms", Field.ItemType);
                results = _searchHandler.GetSearchResults(expr, 1, 100);
                Assert.Equal(1, results.IndexResponseItems.Count);

                //expr = new FieldQuery("Cm*", Field.Type);
                //results = _searchHandler.GetSearchResults(expr, 1, 100);
                //Assert.Equal(1, results.IndexResponseItems.Count);

                //expr = new FieldQuery("EPiServer.Common*", Field.Type);
                //results = _searchHandler.GetSearchResults(expr, 1, 100);
                //Assert.Equal(1, results.IndexResponseItems.Count);
            }
            finally
            {
                sh1.Close();
            }
        }

        [Fact]
        public void SH_TypeFieldSearchTest()
        {
            string id1 = "e00c464d-2d3c-4ad0-a4ad-356364313321";
            string id2 = "2";
            string id3 = "3";
            //Setup services
            Uri baseAddress1 = null;
            ServiceHost sh1 = null;
            SetupIndexingServiceHost(out baseAddress1, out sh1);
            sh1.Open();

            try
            {
                //Reset indexes
                ResetAllIndexes();

                //Add items to different indexes
                IndexRequestItem item1 = new IndexRequestItem(id1, IndexAction.Add);
                item1.ItemType = "CmsPage";
                _searchHandler.UpdateIndex(item1);

                IndexRequestItem item2 = new IndexRequestItem(id2, IndexAction.Add);
                item2.ItemType = "EPiServer.Common.Comment, EPiServer.Common";
                _searchHandler.UpdateIndex(item2);

                IndexRequestItem item3 = new IndexRequestItem(id3, IndexAction.Add);
                item3.ItemType = "Car.carpool";
                _searchHandler.UpdateIndex(item3);

                _requestQueueHandler.ProcessQueue();

                SearchResults results = null;

                FieldQuery expr = new FieldQuery("CmsPage", Field.ItemType);
                results = _searchHandler.GetSearchResults(expr, 1, 20);
                Assert.Equal(1, results.IndexResponseItems.Count);

                expr = new FieldQuery("Car.car*", Field.ItemType);
                results = _searchHandler.GetSearchResults(expr, 1, 20);
                Assert.Equal(1, results.IndexResponseItems.Count);

                expr = new FieldQuery("EPiServer.Common*", Field.ItemType);
                results = _searchHandler.GetSearchResults(expr, 1, 20);
                Assert.Equal(1, results.IndexResponseItems.Count);

                expr = new FieldQuery("EPiServer.Common", Field.ItemType);
                results = _searchHandler.GetSearchResults(expr, 1, 20);
                Assert.Equal(1, results.IndexResponseItems.Count);
            }
            finally
            {

                sh1.Close();
            }
        }

        [Fact]
        public void SH_FuzzySearchTest()
        {
            string id1 = "e00c464d-2d3c-4ad0-a4ad-356364313321";
            string id2 = "2";

            //Setup services
            Uri baseAddress1 = null;
            ServiceHost sh1 = null;
            SetupIndexingServiceHost(out baseAddress1, out sh1);
            sh1.Open();

            try
            {
                //Reset indexes
                ResetAllIndexes();

                //Add items to different indexes
                IndexRequestItem item1 = new IndexRequestItem(id1, IndexAction.Add);
                item1.DisplayText = "you may reconcider this";
                _searchHandler.UpdateIndex(item1);

                IndexRequestItem item2 = new IndexRequestItem(id2, IndexAction.Add);
                item2.DisplayText = "and you may reconcideration this as well";
                item2.NamedIndex = "testindex2";
                _searchHandler.UpdateIndex(item2);

                _requestQueueHandler.ProcessQueue();

                SearchResults results = null;

                FieldQuery expr = new FieldQuery("reconcider");
                results = _searchHandler.GetSearchResults(expr, 1, 20);
                Assert.Equal(1, results.IndexResponseItems.Count);

                Collection<string> namedIndexes = new Collection<string>();
                namedIndexes.Add("default");
                namedIndexes.Add("testindex2");

                //Get all items where any of the words exist in field: default and index: default
                expr = new FuzzyQuery("reconcider", Field.Default, 0.9f);
                results = _searchHandler.GetSearchResults(expr, null, namedIndexes, 1, 20);
                Assert.Equal(1, results.IndexResponseItems.Count);

                expr = new FuzzyQuery("reconcider", Field.Default, 0.1f);
                results = _searchHandler.GetSearchResults(expr, null, namedIndexes, 1, 20);
                Assert.Equal(2, results.IndexResponseItems.Count);

            }
            finally
            {

                sh1.Close();
            }
        }

        [Fact]
        public void SH_ProximitySearchTest()
        {
            string id1 = "e00c464d-2d3c-4ad0-a4ad-356364313321";
            string id2 = "2";
            string id3 = "3";

            //Setup services
            Uri baseAddress1 = null;
            ServiceHost sh1 = null;
            SetupIndexingServiceHost(out baseAddress1, out sh1);
            sh1.Open();

            try
            {
                //Reset indexes
                ResetAllIndexes();

                //Add items to different indexes

                IndexRequestItem item1 = new IndexRequestItem(id1, IndexAction.Add);
                item1.DisplayText = "test body for id1 in default index";
                _searchHandler.UpdateIndex(item1);

                IndexRequestItem item2 = new IndexRequestItem(id2, IndexAction.Add);
                item2.DisplayText = "test body for id2 in default index";
                _searchHandler.UpdateIndex(item2);

                IndexRequestItem item3 = new IndexRequestItem(id3, IndexAction.Add);
                item3.DisplayText = "test body for id3 in default index";
                item3.NamedIndex = "testindex2";
                _searchHandler.UpdateIndex(item3);

                _requestQueueHandler.ProcessQueue();

                //Assert.IsTrue(wh.WaitOne(10000)); //Timeout due to that the queue was never processed

                SearchResults results = null;

                FieldQuery expr = new ProximityQuery("\"body index\"", Field.Default, 1);
                results = _searchHandler.GetSearchResults(expr, 1, 20);
                Assert.Equal(0, results.IndexResponseItems.Count);

                expr = new ProximityQuery("\"body index\"", Field.Default, 4);
                results = _searchHandler.GetSearchResults(expr, 1, 20);
                Assert.Equal(2, results.IndexResponseItems.Count);

                Collection<string> namedIndexes = new Collection<string>();
                namedIndexes.Add("testindex2");

                expr = new ProximityQuery("\"body index\"", Field.Default, 4);
                results = _searchHandler.GetSearchResults(expr, null, namedIndexes, 1, 20);
                Assert.Equal(1, results.IndexResponseItems.Count);

                namedIndexes.Add("default");
                expr = new ProximityQuery("\"body index\"", Field.Default, 4);
                results = _searchHandler.GetSearchResults(expr, null, namedIndexes, 1, 20);
                Assert.Equal(3, results.IndexResponseItems.Count);
            }
            finally
            {

                sh1.Close();
            }
        }

        [Fact]
        public void SH_TermBoostSearchTest()
        {
            string id1 = "e00c464d-2d3c-4ad0-a4ad-356364313321";
            string id2 = "2";
            string id3 = "3";

            //Setup services
            Uri baseAddress1 = null;
            ServiceHost sh1 = null;
            SetupIndexingServiceHost(out baseAddress1, out sh1);
            sh1.Open();

            try
            {
                //Reset indexes
                ResetAllIndexes();

                //Add items
                IndexRequestItem item1 = new IndexRequestItem(id1, IndexAction.Add);
                item1.Title = "test header for id1 in default index";
                item1.DisplayText = "test body for id1 in default index";
                _searchHandler.UpdateIndex(item1);

                IndexRequestItem item2 = new IndexRequestItem(id2, IndexAction.Add);
                item2.Title = "test header for id2 in default index";
                item2.DisplayText = "test body for id2 in default index";
                _searchHandler.UpdateIndex(item2);

                IndexRequestItem item3 = new IndexRequestItem(id3, IndexAction.Add);
                item3.Title = "test header for id3 in default index";
                item3.DisplayText = "test body for id3 in default index";
                _searchHandler.UpdateIndex(item3);

                _requestQueueHandler.ProcessQueue();

                //Assert.IsTrue(wh.WaitOne(10000)); //Timeout due to that the queue was never processed

                SearchResults results = null;

                TermBoostQuery t1 = new TermBoostQuery("\"for id2\"", 20);

                TermBoostQuery t2 = new TermBoostQuery("\"for id3\"", 3);

                FieldQuery f1 = new FieldQuery("header");

                GroupQuery g = new GroupQuery(LuceneOperator.OR);
                g.QueryExpressions.Add(t1);
                g.QueryExpressions.Add(t2);
                g.QueryExpressions.Add(f1);

                results = _searchHandler.GetSearchResults(g, 1, 20);
                Assert.Equal(3, results.TotalHits);
                Assert.Equal(id2, results.IndexResponseItems[0].Id);
                Assert.Equal(id3, results.IndexResponseItems[1].Id);
                Assert.Equal(id1, results.IndexResponseItems[2].Id);

                t1 = new TermBoostQuery("\"for id2\"", 3);

                t2 = new TermBoostQuery("\"for id3\"", 20);

                f1 = new FieldQuery("header");

                g = new GroupQuery(LuceneOperator.OR);
                g.QueryExpressions.Add(t1);
                g.QueryExpressions.Add(t2);
                g.QueryExpressions.Add(f1);

                results = _searchHandler.GetSearchResults(g, 1, 20);
                Assert.Equal(3, results.TotalHits);
                Assert.Equal(id3, results.IndexResponseItems[0].Id);
                Assert.Equal(id2, results.IndexResponseItems[1].Id);
                Assert.Equal(id1, results.IndexResponseItems[2].Id);


            }
            finally
            {
                sh1.Close();
            }
        }


        [Fact]
        public void SH_CategoriesSearchTest()
        {
            string id1 = "e00c464d-2d3c-4ad0-a4ad-356364313321";
            string id2 = "2";

            //Setup services
            Uri baseAddress1 = null;
            ServiceHost sh1 = null;
            SetupIndexingServiceHost(out baseAddress1, out sh1);
            sh1.Open();

            try
            {
                //Reset indexes
                ResetAllIndexes();

                IndexRequestItem item1 = new IndexRequestItem(id1, IndexAction.Add);
                item1.Categories.Add("tag1");
                item1.Categories.Add("tag2");
                item1.Categories.Add("tag1 tag2");
                _searchHandler.UpdateIndex(item1);

                IndexRequestItem item2 = new IndexRequestItem(id2, IndexAction.Add);
                item2.Categories.Add("entity3/cars");
                item2.Categories.Add("tag1");
                _searchHandler.UpdateIndex(item2);

                _requestQueueHandler.ProcessQueue();

                //Assert.IsTrue(wh.WaitOne(10000)); //Timeout due to that the queue was never processed

                SearchResults results = null;

                Thread.Sleep(3000);

                CategoryQuery categoriesQuery1 = new CategoryQuery(LuceneOperator.AND);
                categoriesQuery1.Items.Add("tag1");

                results = _searchHandler.GetSearchResults(categoriesQuery1, 1, 20);
                Assert.Equal(2, results.TotalHits);

                CategoryQuery categoriesQuery2 = new CategoryQuery(LuceneOperator.AND);
                categoriesQuery2.Items.Add("tag2");
                results = _searchHandler.GetSearchResults(categoriesQuery2, 1, 20);
                Assert.Equal(1, results.TotalHits);

                CategoryQuery categoriesQuery3 = new CategoryQuery(LuceneOperator.AND);
                categoriesQuery3.Items.Add("tag1 tag2");
                results = _searchHandler.GetSearchResults(categoriesQuery3, 1, 20);
                Assert.Equal(1, results.TotalHits);

                GroupQuery group = new GroupQuery(LuceneOperator.OR);
                group.QueryExpressions.Add(categoriesQuery1);
                results = _searchHandler.GetSearchResults(group, 1, 20);
                Assert.Equal(2, results.TotalHits);

                group.QueryExpressions.Add(categoriesQuery3);
                results = _searchHandler.GetSearchResults(group, 1, 20);
                Assert.Equal(2, results.TotalHits);
            }
            finally
            {

                sh1.Close();
            }
        }

        [Fact]
        public void SH_VirtualPathSearchTest()
        {
            string id1 = Guid.NewGuid().ToString();
            string id2 = Guid.NewGuid().ToString();
            string id3 = Guid.NewGuid().ToString();
            string id4 = Guid.NewGuid().ToString();
            string id5 = Guid.NewGuid().ToString();
            string id6 = Guid.NewGuid().ToString();

            //Setup services
            Uri baseAddress1 = null;
            ServiceHost sh1 = null;
            SetupIndexingServiceHost(out baseAddress1, out sh1);
            sh1.Open();

            try
            {
                //Reset indexes
                ResetAllIndexes();

                IndexRequestItem item1 = new IndexRequestItem(id1, IndexAction.Add);
                item1.Title = "testing header";
                item1.Metadata = "testing metadata";
                item1.VirtualPathNodes.Add("Node 1");
                item1.VirtualPathNodes.Add("node 1_1");
                item1.VirtualPathNodes.Add("node 1_2");
                _searchHandler.UpdateIndex(item1);

                IndexRequestItem item2 = new IndexRequestItem(id2, IndexAction.Add);
                item2.Title = "testing header";
                item2.Metadata = "testing metadata";
                item2.VirtualPathNodes.Add("node2");
                item2.VirtualPathNodes.Add("node 1_1");
                _searchHandler.UpdateIndex(item2);

                IndexRequestItem item3 = new IndexRequestItem(id3, IndexAction.Add);
                item3.Title = "testing header";
                item3.Metadata = "testing metadata";
                item3.VirtualPathNodes.Add("Node 1");
                item3.VirtualPathNodes.Add("node 1_3");
                item3.VirtualPathNodes.Add("node 1_4");
                _searchHandler.UpdateIndex(item3);

                IndexRequestItem item4 = new IndexRequestItem(id4, IndexAction.Add);
                item4.Title = "the fourth item header4";
                item4.VirtualPathNodes.Add("Node 1");
                item4.VirtualPathNodes.Add("node 1_3");
                item4.VirtualPathNodes.Add("node 1_4");
                _searchHandler.UpdateIndex(item4);

                IndexRequestItem item5 = new IndexRequestItem(id5, IndexAction.Add);
                item5.Title = "the item header5";
                item5.VirtualPathNodes.Add("Node 1");
                item5.VirtualPathNodes.Add("node 1_3");
                item5.VirtualPathNodes.Add("node 1_5");
                _searchHandler.UpdateIndex(item5);

                string node1 = Guid.NewGuid().ToString();
                string node2 = Guid.NewGuid().ToString();

                IndexRequestItem item6 = new IndexRequestItem(id6, IndexAction.Add);
                item6.Title = "the item header6";
                item6.VirtualPathNodes.Add(node1);
                item6.VirtualPathNodes.Add(node2);
                _searchHandler.UpdateIndex(item6);

                _requestQueueHandler.ProcessQueue();

                SearchResults results = null;

                // Make sure we get 2 hits for "node 1"
                VirtualPathQuery vpq1 = new VirtualPathQuery();
                vpq1.VirtualPathNodes.Add("Node 1");
                FieldQuery fq1 = new FieldQuery("testing header");
                GroupQuery gq1 = new GroupQuery(LuceneOperator.AND);
                gq1.QueryExpressions.Add(fq1);
                gq1.QueryExpressions.Add(vpq1);

                results = _searchHandler.GetSearchResults(gq1, 1, 20);
                Assert.Equal(2, results.TotalHits);

                // Make sure we get 2 hits for "node 1/node 1_1"
                VirtualPathQuery vpq2 = new VirtualPathQuery();
                vpq2.VirtualPathNodes.Add("Node 1");
                vpq2.VirtualPathNodes.Add("node 1_1");
                FieldQuery fq2 = new FieldQuery("testing header");
                GroupQuery gq2 = new GroupQuery(LuceneOperator.AND);
                gq2.QueryExpressions.Add(fq2);
                gq2.QueryExpressions.Add(vpq2);

                results = _searchHandler.GetSearchResults(gq2, 1, 20);
                Assert.Equal(1, results.TotalHits);

                // Make sure we get 1 hit for "node 1/node 1_1/node 1_2"
                VirtualPathQuery vpq3 = new VirtualPathQuery();
                vpq3.VirtualPathNodes.Add("Node 1");
                vpq3.VirtualPathNodes.Add("node 1_1");
                vpq3.VirtualPathNodes.Add("node 1_2");
                FieldQuery fq3 = new FieldQuery("testing header");
                GroupQuery gq3 = new GroupQuery(LuceneOperator.AND);
                gq3.QueryExpressions.Add(fq3);
                gq3.QueryExpressions.Add(vpq3);

                results = _searchHandler.GetSearchResults(gq3, 1, 20);
                Assert.Equal(1, results.TotalHits);
                IndexItemBase resultItem = results.IndexResponseItems[0];
                AssertIndexItemEquality(item1, resultItem);

                // Make sure we get 2 hits for "node 1/node 1_3/node 1_4"
                VirtualPathQuery vpq4 = new VirtualPathQuery();
                vpq4.VirtualPathNodes.Add("Node 1");
                vpq4.VirtualPathNodes.Add("node 1_3");
                vpq4.VirtualPathNodes.Add("node 1_4");

                results = _searchHandler.GetSearchResults(vpq4, 1, 20);
                Assert.Equal(2, results.TotalHits);

                // Make sure we get 0 hits for "node 1_1" because its not a starting node
                VirtualPathQuery vpq5 = new VirtualPathQuery();
                vpq5.VirtualPathNodes.Add("node 1_1");

                results = _searchHandler.GetSearchResults(vpq5, 1, 20);
                Assert.Equal(0, results.TotalHits);

                // Make sure we get 3 hits for "node 1/node 1_3/"
                VirtualPathQuery vpq6 = new VirtualPathQuery();
                vpq6.VirtualPathNodes.Add("Node 1");
                vpq6.VirtualPathNodes.Add("node 1_3");

                results = _searchHandler.GetSearchResults(vpq6, 1, 20);
                Assert.Equal(3, results.TotalHits);

                // Make sure we get 1 hit for guid formatted nodes (id6)
                VirtualPathQuery vpq7 = new VirtualPathQuery();
                vpq7.VirtualPathNodes.Add(node1);

                results = _searchHandler.GetSearchResults(vpq7, 1, 20);
                Assert.Equal(1, results.TotalHits);

            }
            finally
            {

                sh1.Close();
            }
        }

        [Fact]
        public void SH_VirtualPathUpdateTest()
        {
            string id1 = Guid.NewGuid().ToString();
            string id2 = Guid.NewGuid().ToString();
            string id3 = Guid.NewGuid().ToString();
            string id4 = Guid.NewGuid().ToString();
            string id5 = Guid.NewGuid().ToString();

            //Setup services
            Uri baseAddress1 = null;
            ServiceHost sh1 = null;
            SetupIndexingServiceHost(out baseAddress1, out sh1);
            sh1.Open();

            try
            {
                //Reset indexes
                ResetAllIndexes();

                // id1
                IndexRequestItem item = new IndexRequestItem(id1, IndexAction.Add);
                item.Title = "testing header";
                item.DisplayText = "testing introtext";
                item.VirtualPathNodes.Add(id1);
                _searchHandler.UpdateIndex(item);

                // id1/id2
                item = new IndexRequestItem(id2, IndexAction.Add);
                item.Title = "testing header";
                item.DisplayText = "testing introtext";
                item.VirtualPathNodes.Add(id1);
                item.VirtualPathNodes.Add(id2);
                _searchHandler.UpdateIndex(item);

                // id1/id3
                item = new IndexRequestItem(id3, IndexAction.Add);
                item.Title = "testing header";
                item.DisplayText = "testing introtext";
                item.VirtualPathNodes.Add(id1);
                item.VirtualPathNodes.Add(id3);
                _searchHandler.UpdateIndex(item);

                // id1/id3/id4
                item = new IndexRequestItem(id4, IndexAction.Add);
                item.Title = "testing header";
                item.DisplayText = "testing introtext";
                item.VirtualPathNodes.Add(id1);
                item.VirtualPathNodes.Add(id3);
                item.VirtualPathNodes.Add(id4);
                _searchHandler.UpdateIndex(item);

                // id1/id3/id4/id5
                item = new IndexRequestItem(id5, IndexAction.Add);
                item.Title = "testing header for id5";
                item.DisplayText = "testing introtext for id5";
                item.Metadata = "testing metadata for id5";
                item.VirtualPathNodes.Add(id1);
                item.VirtualPathNodes.Add(id3);
                item.VirtualPathNodes.Add(id4);
                item.VirtualPathNodes.Add(id5);
                _searchHandler.UpdateIndex(item);

                // id1/id3/id4/id5
                item = new IndexRequestItem(id5, IndexAction.Add);
                item.Title = "testing header for id5";
                item.DisplayText = "testing introtext for id5";
                item.Metadata = "testing metadata for id5";
                item.NamedIndex = "testindex2";
                item.VirtualPathNodes.Add(id1);
                item.VirtualPathNodes.Add(id3);
                item.VirtualPathNodes.Add(id4);
                item.VirtualPathNodes.Add(id5);
                _searchHandler.UpdateIndex(item);

                _requestQueueHandler.ProcessQueue();

                // id1/id3/id4
                VirtualPathQuery vpq = new VirtualPathQuery();
                vpq.VirtualPathNodes.Add(id1);
                vpq.VirtualPathNodes.Add(id3);
                vpq.VirtualPathNodes.Add(id4);

                SearchResults results = _searchHandler.GetSearchResults(vpq, 1, 20);
                Assert.Equal(2, results.TotalHits);

                // Test OR and AND
                VirtualPathQuery vpqOr1 = new VirtualPathQuery();
                vpqOr1.VirtualPathNodes.Add(id1);
                vpqOr1.VirtualPathNodes.Add(id3);
                vpqOr1.VirtualPathNodes.Add(id4);

                VirtualPathQuery vpqOr2 = new VirtualPathQuery();
                vpqOr2.VirtualPathNodes.Add(id4);

                GroupQuery gVpqOr = new GroupQuery(LuceneOperator.OR);
                gVpqOr.QueryExpressions.Add(vpqOr1);
                gVpqOr.QueryExpressions.Add(vpqOr2);

                results = _searchHandler.GetSearchResults(gVpqOr, 1, 20);
                Assert.Equal(2, results.TotalHits);

                gVpqOr = new GroupQuery(LuceneOperator.AND);
                gVpqOr.QueryExpressions.Add(vpqOr1);
                gVpqOr.QueryExpressions.Add(vpqOr2);

                results = _searchHandler.GetSearchResults(gVpqOr, 1, 20);
                Assert.Equal(0, results.TotalHits);

                //UPDATE id3 from id1/id3 -> id1/id2/id3
                item = new IndexRequestItem(id3, IndexAction.Update);
                item.AutoUpdateVirtualPath = true;
                item.Title = "testing header";
                item.DisplayText = "testing introtext";
                item.VirtualPathNodes.Add(id1);
                item.VirtualPathNodes.Add(id2);
                item.VirtualPathNodes.Add(id3);
                _searchHandler.UpdateIndex(item);

                _requestQueueHandler.ProcessQueue();

                // id1/id3/id4. No results for the old path
                vpq = new VirtualPathQuery();
                vpq.VirtualPathNodes.Add(id1);
                vpq.VirtualPathNodes.Add(id3);
                vpq.VirtualPathNodes.Add(id4);

                results = _searchHandler.GetSearchResults(vpq, 1, 20);
                Assert.Equal(0, results.TotalHits);

                // id1/id2/id3/id4. Should get results for the new path
                vpq = new VirtualPathQuery();
                vpq.VirtualPathNodes.Add(id1);
                vpq.VirtualPathNodes.Add(id2);
                vpq.VirtualPathNodes.Add(id3);
                vpq.VirtualPathNodes.Add(id4);

                Collection<string> namedIndexes = new Collection<string>();
                namedIndexes.Add("default");
                namedIndexes.Add("testindex2");
                results = _searchHandler.GetSearchResults(vpq, null, namedIndexes, 1, 20);
                Assert.Equal(3, results.TotalHits);

                //Check that autoupdated content is still searchable. 
                FieldQuery fq = new FieldQuery("\"metadata for id5\"");
                GroupQuery gq = new GroupQuery(LuceneOperator.AND);
                gq.QueryExpressions.Add(fq);
                gq.QueryExpressions.Add(vpq);
                results = _searchHandler.GetSearchResults(gq, 1, 20);
                Assert.Equal(1, results.TotalHits);

            }
            finally
            {

                sh1.Close();
            }
        }

        [Fact]
        public void SH_AddUpdateReadWriteMultipleThreadTest()
        {
            //Setup services
            Uri baseAddress1 = null;
            ServiceHost sh1 = null;
            SetupIndexingServiceHost(out baseAddress1, out sh1);
            sh1.Open();

            int numItems = 100;

            Collection<string> ids = new Collection<string>();
            for (int i = 0; i < numItems; i++)
            {
                ids.Add(Guid.NewGuid().ToString());
            }

            try
            {
                //Reset indexes
                ResetAllIndexes();

                // Start add thread
                Thread writeThread = new Thread(new ThreadStart(() =>
                {
                    for (int i = 0; i < numItems; i++)
                    {
                        _searchHandler.UpdateIndex(new IndexRequestItem(ids[i], IndexAction.Add));
                        _requestQueueHandler.ProcessQueue();
                    }
                }));

                writeThread.Start();

                // Start update thread
                Thread updateThread = new Thread(new ThreadStart(() =>
                {
                    for (int i = 0; i < numItems; i++)
                    {
                        _searchHandler.UpdateIndex(new IndexRequestItem(ids[i], IndexAction.Update));
                        _requestQueueHandler.ProcessQueue();
                    }
                }));

                updateThread.Start();

                // Start write thread
                Thread readThread = new Thread(new ThreadStart(() =>
                {
                    for (int i = 0; i < numItems; i++)
                    {
                        FieldQuery fq = new FieldQuery("test search in default index");
                        _searchHandler.GetSearchResults(fq, 1, 20);

                        fq = new FieldQuery(ids[i], Field.Id);
                        _searchHandler.GetSearchResults(fq, 1, 20);
                    }
                }));

                readThread.Start();

                writeThread.Join();
                readThread.Join();
                updateThread.Join();

                // Assert that all items are added
                for (int i = 0; i < numItems; i++)
                {
                    FieldQuery fq = new FieldQuery(ids[i], Field.Id);
                    SearchResults results = _searchHandler.GetSearchResults(fq, 1, 20);
                    Assert.Equal(1, results.IndexResponseItems.Count);
                }

            }
            finally
            {
                sh1.Close();
            }
        }

        [Fact]
        public void SH_AlotOfAddUpdateAndRemovesTest()
        {
            //Setup services
            Uri baseAddress1 = null;
            ServiceHost sh1 = null;
            SetupIndexingServiceHost(out baseAddress1, out sh1);
            sh1.Open();

            try
            {
                //Reset indexes
                ResetAllIndexes();

                IndexRequestItem item1 = null; ;
                IndexRequestItem item2 = null;
                IndexRequestItem item3 = null;

                for (int i = 0; i < 500; i++)
                {
                    string id = Guid.NewGuid().ToString();

                    IndexRequestItem item = new IndexRequestItem(id, IndexAction.Add);
                    item = new IndexRequestItem(id, IndexAction.Update);
                    item = new IndexRequestItem(id, IndexAction.Remove);

                    //Store the first, middle and last item for assert
                    if (i == 0)
                        item1 = item;
                    if (i == 250)
                        item2 = item;
                    if (i == 499)
                        item3 = item;

                    _searchHandler.UpdateIndex(item);
                }

                _requestQueueHandler.ProcessQueue();

                // Assert that items are removed
                FieldQuery fe = new FieldQuery(item1.Id, Field.Id);
                SearchResults results = _searchHandler.GetSearchResults(fe, 1, 20);
                Assert.Equal(0, results.IndexResponseItems.Count);

                fe = new FieldQuery(item2.Id, Field.Id);
                results = _searchHandler.GetSearchResults(fe, 1, 20);
                Assert.Equal(0, results.IndexResponseItems.Count);
                fe = new FieldQuery(item3.Id, Field.Id);

                results = _searchHandler.GetSearchResults(fe, 1, 20);
                Assert.Equal(0, results.IndexResponseItems.Count); 

            }
            finally
            {
                sh1.Close();
            }
        }

        [Fact]
        public void SH_AddTest()
        {
            //Setup services
            Uri baseAddress1 = null;
            ServiceHost sh1 = null;
            SetupIndexingServiceHost(out baseAddress1, out sh1);
            sh1.Open();

            try
            {
                //Reset indexes
                ResetAllIndexes();

                string id;
                
                id = Guid.NewGuid().ToString();
                IndexRequestItem item1 = new IndexRequestItem(id, IndexAction.Add);
                item1.DisplayText = "Hello World";
                _searchHandler.UpdateIndex(item1);

                id = Guid.NewGuid().ToString();
                IndexRequestItem item2 = new IndexRequestItem(id, IndexAction.Add);
                item2.DisplayText = "Hello\x1bTest";
                _searchHandler.UpdateIndex(item2);

                _requestQueueHandler.ProcessQueue();

                AssertEqualToSearchResult(item1, null);
                AssertEqualToSearchResult(item2, null);
            }
            finally
            {
                sh1.Close();
            }
        }

        [Fact]
        public void SH_AlotOfAddTest()
        {
            //Setup services
            Uri baseAddress1 = null;
            ServiceHost sh1 = null;
            SetupIndexingServiceHost(out baseAddress1, out sh1);
            sh1.Open();

            try
            {
                //Reset indexes
                ResetAllIndexes();

                IndexRequestItem item1 = null; ;
                IndexRequestItem item2 = null;
                IndexRequestItem item3 = null;

                for (int i = 0; i < 500; i++)
                {
                    string id = Guid.NewGuid().ToString();

                    IndexRequestItem item = new IndexRequestItem(id, IndexAction.Add);

                    //Store the first, middle and last item for assert
                    if (i == 0)
                        item1 = item;
                    if (i == 250)
                        item2 = item;
                    if (i == 499)
                        item3 = item;

                    _searchHandler.UpdateIndex(item);
                }

                _requestQueueHandler.ProcessQueue();

                AssertEqualToSearchResult(item1, null);
                AssertEqualToSearchResult(item2, null);
                AssertEqualToSearchResult(item3, null);

            }
            finally
            {
                sh1.Close();
            }
        }

        [Fact]
        public void SH_TypeFieldTest()
        {
            string id = "1";
            //Setup services
            Uri baseAddress1 = null;
            ServiceHost sh1 = null;
            SetupIndexingServiceHost(out baseAddress1, out sh1);
            sh1.Open();

            try
            {
                //Reset indexes
                ResetAllIndexes();

                IndexRequestItem item = new IndexRequestItem(id, IndexAction.Add);
                item.Title = "testing header";
                item.DisplayText = "testing introtext";
                item.ItemType = "EPiServer.Common.Comment, EPiServer.Common";
                _searchHandler.UpdateIndex(item);

                _requestQueueHandler.ProcessQueue();

                FieldQuery q = new FieldQuery("EPiServer.Common*", Field.ItemType);
                SearchResults res = _searchHandler.GetSearchResults(q, 1, 100);

                Assert.Equal(1, res.IndexResponseItems.Count);
            }
            finally
            {
                sh1.Close();
            }
        }

        [Fact]
        public void SH_CultureFieldTest()
        {
            string id = "1";
            //Setup services
            Uri baseAddress1 = null;
            ServiceHost sh1 = null;
            SetupIndexingServiceHost(out baseAddress1, out sh1);
            sh1.Open();

            try
            {
                _requestQueueHandler.TruncateQueue();

                //Reset indexes
                ResetAllIndexes();

                IndexRequestItem item = new IndexRequestItem(id, IndexAction.Add);
                item.Title = "testing header";
                item.DisplayText = "testing introtext";
                item.Culture = "sv-SE";
                _searchHandler.UpdateIndex(item);

                _requestQueueHandler.ProcessQueue();

                FieldQuery q = new FieldQuery("sv-SE", Field.Culture);
                SearchResults res = _searchHandler.GetSearchResults(q, 1, 100);
                Assert.Equal(1, res.IndexResponseItems.Count);

                q = new FieldQuery("sv*", Field.Culture);
                res = _searchHandler.GetSearchResults(q, 1, 100);
                Assert.Equal(1, res.IndexResponseItems.Count);
            }
            finally
            {
                sh1.Close();
            }
        }

        [Fact]
        public void SH_AccessDeniedTest()
        {
            string id = "1";
            //Setup services
            Uri baseAddress1 = null;
            ServiceHost sh1 = null;
            SetupIndexingServiceHost(out baseAddress1, out sh1);
            sh1.Open();

            try
            {
                //Reset indexes
                ResetAllIndexes();

                IndexRequestItem item = new IndexRequestItem(id, IndexAction.Add);
                _searchHandler.UpdateIndex(item, "deniedService");

                _requestQueueHandler.ProcessQueue();

                FieldQuery q = new FieldQuery(id, Field.Id);
                SearchResults res = _searchHandler.GetSearchResults(q, "deniedService", null, 1, 100);
                Assert.Equal(0, res.IndexResponseItems.Count);
            }
            finally
            {
                sh1.Close();
            }
        }

        [Fact]
        public void SH_DefaultFieldSearchAfterAdd()
        {
            string id = "1";
            //Setup services
            Uri baseAddress1 = null;
            ServiceHost sh1 = null;
            SetupIndexingServiceHost(out baseAddress1, out sh1);
            sh1.Open();

            try
            {
                //Reset indexes
                ResetAllIndexes();

                IndexRequestItem item = new IndexRequestItem(id, IndexAction.Add);
                item.Title = "The title field";
                item.DisplayText = "The display text";
                item.Metadata = "The metadata field";

                _searchHandler.UpdateIndex(item);

                _requestQueueHandler.ProcessQueue();

                FieldQuery q = new FieldQuery("title");
                SearchResults res = _searchHandler.GetSearchResults(q, 1, 100);
                Assert.Equal(1, res.IndexResponseItems.Count);

                q = new FieldQuery("display");
                res = _searchHandler.GetSearchResults(q, 1, 100);
                Assert.Equal(1, res.IndexResponseItems.Count);

                q = new FieldQuery("metadata");
                res = _searchHandler.GetSearchResults(q, 1, 100);
                Assert.Equal(1, res.IndexResponseItems.Count);
            }
            finally
            {
                sh1.Close();
            }
        }

        [Fact]
        public void SH_DefaultFieldSearchAfterVirtualPathUpdate()
        {
            string id = "1";
            //Setup services
            Uri baseAddress1 = null;
            ServiceHost sh1 = null;
            SetupIndexingServiceHost(out baseAddress1, out sh1);
            sh1.Open();

            try
            {
                //Reset indexes
                ResetAllIndexes();

                IndexRequestItem item = new IndexRequestItem(id, IndexAction.Add);
                item.Title = "The title field";
                item.DisplayText = "The display text";
                item.Metadata = "The metadata field";
                item.VirtualPathNodes.Add("node");

                _searchHandler.UpdateIndex(item);

                _requestQueueHandler.ProcessQueue();

                FieldQuery q = new FieldQuery("title");
                SearchResults res = _searchHandler.GetSearchResults(q, 1, 100);
                Assert.Equal(1, res.IndexResponseItems.Count);

                q = new FieldQuery("display");
                res = _searchHandler.GetSearchResults(q, 1, 100);
                Assert.Equal(1, res.IndexResponseItems.Count);

                q = new FieldQuery("metadata");
                res = _searchHandler.GetSearchResults(q, 1, 100);
                Assert.Equal(1, res.IndexResponseItems.Count);

                //Update
                item = new IndexRequestItem(id, IndexAction.Update);
                item.Title = "The title field";
                item.DisplayText = "The display text";
                item.Metadata = "The metadata field";
                item.VirtualPathNodes.Add("nodeupdate");

                _searchHandler.UpdateIndex(item);

                _requestQueueHandler.ProcessQueue();

                q = new FieldQuery("title");
                res = _searchHandler.GetSearchResults(q, 1, 100);
                Assert.Equal(1, res.IndexResponseItems.Count);

                q = new FieldQuery("display");
                res = _searchHandler.GetSearchResults(q, 1, 100);
                Assert.Equal(1, res.IndexResponseItems.Count);

                q = new FieldQuery("metadata");
                res = _searchHandler.GetSearchResults(q, 1, 100);
                Assert.Equal(1, res.IndexResponseItems.Count);

            }
            finally
            {
                sh1.Close();
            }
        }

        [Fact]
        public void SH_AddReferenceTest()
        {
            string id1 = "1";
            string id2 = "2";
            string id3 = "3";
            //Setup services
            Uri baseAddress1 = null;
            ServiceHost sh1 = null;
            SetupIndexingServiceHost(out baseAddress1, out sh1);
            sh1.Open();

            try
            {
                _requestQueueHandler.TruncateQueue();

                //Reset indexes
                ResetAllIndexes();

                // Add the main item
                IndexRequestItem item = new IndexRequestItem(id1, IndexAction.Add);
                item.Title = "The title field";
                item.DisplayText = "The display text";
                item.Metadata = "The metadata field";

                _searchHandler.UpdateIndex(item);

                _requestQueueHandler.ProcessQueue();

                FieldQuery q = new FieldQuery("title AND display AND metadata");
                SearchResults res = _searchHandler.GetSearchResults(q, 1, 100);
                Assert.Equal(1, res.IndexResponseItems.Count);

                // Add a reference item to main item
                IndexRequestItem refItem = new IndexRequestItem(id2, IndexAction.Add);
                refItem.Title = "referencetitle";
                refItem.DisplayText = "referencedisplay";
                refItem.Metadata = "referencemetadata";
                refItem.ReferenceId = id1;

                _searchHandler.UpdateIndex(refItem);

                _requestQueueHandler.ProcessQueue();

                // Make sure the old test still works
                q = new FieldQuery("title AND display AND metadata");
                res = _searchHandler.GetSearchResults(q, 1, 100);
                Assert.Equal(1, res.IndexResponseItems.Count);
                Assert.Equal(id1, res.IndexResponseItems[0].Id);

                // Make sure that we can search reference data and get the main item back
                q = new FieldQuery("referencetitle AND referencedisplay AND referencemetadata");
                res = _searchHandler.GetSearchResults(q, 1, 100);
                Assert.Equal(1, res.IndexResponseItems.Count);
                Assert.Equal(id1, res.IndexResponseItems[0].Id);

                // Add another reference item to main item
                IndexRequestItem refItem2 = new IndexRequestItem(id3, IndexAction.Add);
                refItem2.Title = "referencetitle second";
                refItem2.DisplayText = "referencedisplay second";
                refItem2.Metadata = "referencemetadata second";
                refItem2.ReferenceId = id1;

                _searchHandler.UpdateIndex(refItem2);

                _requestQueueHandler.ProcessQueue();

                // Make sure the old test still works
                q = new FieldQuery("title AND display AND metadata");
                res = _searchHandler.GetSearchResults(q, 1, 100);
                Assert.Equal(1, res.IndexResponseItems.Count);
                Assert.Equal(id1, res.IndexResponseItems[0].Id);

                // Make sure that we still can search the first reference data and get the main item back
                q = new FieldQuery("referencetitle AND referencedisplay AND referencemetadata");
                res = _searchHandler.GetSearchResults(q, 1, 100);
                Assert.Equal(1, res.IndexResponseItems.Count);
                Assert.Equal(id1, res.IndexResponseItems[0].Id);

                // Make sure that we can search second reference data and get the main item back
                q = new FieldQuery("\"referencetitle second\" AND \"referencedisplay second\" AND \"referencemetadata second\"");
                res = _searchHandler.GetSearchResults(q, 1, 100);
                Assert.Equal(1, res.IndexResponseItems.Count);
                Assert.Equal(id1, res.IndexResponseItems[0].Id);
            }
            finally
            {
                sh1.Close();
            }
        }

        [Fact]
        public void SH_AddReferenceWithMissingMainItemTest()
        {
            string id1 = "1";
            string id2 = "2";
            string id3 = "3";
            //Setup services
            Uri baseAddress1 = null;
            ServiceHost sh1 = null;
            SetupIndexingServiceHost(out baseAddress1, out sh1);
            sh1.Open();

            try
            {
                _requestQueueHandler.TruncateQueue();

                //Reset indexes
                ResetAllIndexes();
                
                // Add a reference item to a non-existing main item
                IndexRequestItem refItem = new IndexRequestItem(id2, IndexAction.Add);
                refItem.Title = "referencetitle";
                refItem.DisplayText = "referencedisplay";
                refItem.Metadata = "referencemetadata";
                refItem.ReferenceId = id1;

                _searchHandler.UpdateIndex(refItem);

                _requestQueueHandler.ProcessQueue();

                // THEN, add the main item
                IndexRequestItem item = new IndexRequestItem(id1, IndexAction.Add);
                item.Title = "The title field";
                item.DisplayText = "The display text";
                item.Metadata = "The metadata field";

                _searchHandler.UpdateIndex(item);

                _requestQueueHandler.ProcessQueue();

                FieldQuery q = new FieldQuery("title AND display AND metadata");
                SearchResults res = _searchHandler.GetSearchResults(q, 1, 100);
                Assert.Equal(1, res.IndexResponseItems.Count);

                // Make sure the old test still works
                q = new FieldQuery("title AND display AND metadata");
                res = _searchHandler.GetSearchResults(q, 1, 100);
                Assert.Equal(1, res.IndexResponseItems.Count);
                Assert.Equal(id1, res.IndexResponseItems[0].Id);

                // Make sure that we can search reference data and get the main item back
                q = new FieldQuery("referencetitle AND referencedisplay AND referencemetadata");
                res = _searchHandler.GetSearchResults(q, 1, 100);
                Assert.Equal(1, res.IndexResponseItems.Count);
                Assert.Equal(id1, res.IndexResponseItems[0].Id);

                // Add another reference item to main item
                IndexRequestItem refItem2 = new IndexRequestItem(id3, IndexAction.Add);
                refItem2.Title = "referencetitle second";
                refItem2.DisplayText = "referencedisplay second";
                refItem2.Metadata = "referencemetadata second";
                refItem2.ReferenceId = id1;

                _searchHandler.UpdateIndex(refItem2);

                _requestQueueHandler.ProcessQueue();

                // Make sure the old test still works
                q = new FieldQuery("title AND display AND metadata");
                res = _searchHandler.GetSearchResults(q, 1, 100);
                Assert.Equal(1, res.IndexResponseItems.Count);
                Assert.Equal(id1, res.IndexResponseItems[0].Id);

                // Make sure that we still can search the first reference data and get the main item back
                q = new FieldQuery("referencetitle AND referencedisplay AND referencemetadata");
                res = _searchHandler.GetSearchResults(q, 1, 100);
                Assert.Equal(1, res.IndexResponseItems.Count);
                Assert.Equal(id1, res.IndexResponseItems[0].Id);

                // Make sure that we can search second reference data and get the main item back
                q = new FieldQuery("\"referencetitle second\" AND \"referencedisplay second\" AND \"referencemetadata second\"");
                res = _searchHandler.GetSearchResults(q, 1, 100);
                Assert.Equal(1, res.IndexResponseItems.Count);
                Assert.Equal(id1, res.IndexResponseItems[0].Id);
            }
            finally
            {
                sh1.Close();
            }
        }

        [Fact]
        public void SH_AssertReferenceDataUpdate()
        {
            string id1 = "1";
            string id2 = "2";
            string id3 = "3";
            //Setup services
            Uri baseAddress1 = null;
            ServiceHost sh1 = null;
            SetupIndexingServiceHost(out baseAddress1, out sh1);
            sh1.Open();

            try
            {
                _requestQueueHandler.TruncateQueue();

                // Reset indexes
                ResetAllIndexes();

                // Add main item
                IndexRequestItem item1 = new IndexRequestItem(id1, IndexAction.Add);
                item1.Title = "The maintitle field";
                item1.DisplayText = "The maindisplay text";
                item1.Metadata = "The mainmetadata field";

                // Add first reference item
                IndexRequestItem item2 = new IndexRequestItem(id2, IndexAction.Add);
                item2.Title = "The first reference title field";
                item2.DisplayText = "The first reference display text";
                item2.Metadata = "The first reference metadata field";
                item2.ReferenceId = id1;

                // Add second reference item
                IndexRequestItem item3 = new IndexRequestItem(id3, IndexAction.Add);
                item3.Title = "The second reference title field";
                item3.DisplayText = "The second reference display text";
                item3.Metadata = "The second reference metadata field";
                item3.ReferenceId = id1;

                _searchHandler.UpdateIndex(item1);
                _searchHandler.UpdateIndex(item2);
                _searchHandler.UpdateIndex(item3);

                _requestQueueHandler.ProcessQueue();

                // Update first reference
                item2 = new IndexRequestItem(id2, IndexAction.Update);
                item2.Title = "The first updated reference title field";
                item2.DisplayText = "The first updated reference display text";
                item2.Metadata = "The first updated reference metadata field";
                //item2.ReferenceId = id1;

                _searchHandler.UpdateIndex(item2);

                _requestQueueHandler.ProcessQueue();

                // Make sure that id1 and id3 is still searchable
                FieldQuery q = new FieldQuery("maintitle AND \"second reference\"");
                SearchResults r = _searchHandler.GetSearchResults(q, 1, 10);
                Assert.Equal(1, r.TotalHits);
                Assert.Equal(1, r.IndexResponseItems.Count);

                // Make sure that id2 has been updated
                q = new FieldQuery("\"first updated reference\"");
                r = _searchHandler.GetSearchResults(q, 1, 10);
                Assert.Equal(1, r.TotalHits);
                Assert.Equal(1, r.IndexResponseItems.Count);
                Assert.Equal(id1, r.IndexResponseItems[0].Id);
            }
            finally
            {
                sh1.Close();
            }
        }

        [Fact]
        public void SH_AssertReferenceDataRemoval()
        {
            string id1 = "1";
            string id2 = "2";
            string id3 = "3";
            //Setup services
            Uri baseAddress1 = null;
            ServiceHost sh1 = null;
            SetupIndexingServiceHost(out baseAddress1, out sh1);
            sh1.Open();

            try
            {
                _requestQueueHandler.TruncateQueue();

                // Reset indexes
                ResetAllIndexes();

                // Add main item
                IndexRequestItem item1 = new IndexRequestItem(id1, IndexAction.Add);
                item1.Title = "The maintitle field";
                item1.DisplayText = "The maindisplay text";
                item1.Metadata = "The mainmetadata field";

                // Add first reference item
                IndexRequestItem item2 = new IndexRequestItem(id2, IndexAction.Add);
                item2.Title = "The first reference title field";
                item2.DisplayText = "The first reference display text";
                item2.Metadata = "The first reference metadata field";
                item2.ReferenceId = id1;

                // Add second reference item
                IndexRequestItem item3 = new IndexRequestItem(id3, IndexAction.Add);
                item3.Title = "The second reference title field";
                item3.DisplayText = "The second reference display text";
                item3.Metadata = "The second reference metadata field";
                item3.ReferenceId = id1;

                _searchHandler.UpdateIndex(item1);
                _searchHandler.UpdateIndex(item2);
                _searchHandler.UpdateIndex(item3);

                _requestQueueHandler.ProcessQueue();

                // Remove second reference
                IndexRequestItem delItem = new IndexRequestItem(id2, IndexAction.Remove);
                //delItem.ReferenceId = id1;
                _searchHandler.UpdateIndex(delItem);

                _requestQueueHandler.ProcessQueue();

                // Make sure that id1 and id3 is still searchable
                FieldQuery q = new FieldQuery("maintitle AND \"second reference\"");
                SearchResults r = _searchHandler.GetSearchResults(q, 1, 10);
                Assert.Equal(1, r.TotalHits);
                Assert.Equal(1, r.IndexResponseItems.Count);

                // Make sure that id2 is not searchable
                q = new FieldQuery("\"first reference\"");
                r = _searchHandler.GetSearchResults(q, 1, 10);
                Assert.Equal(0, r.TotalHits);
                Assert.Equal(0, r.IndexResponseItems.Count);

                // Remove main item
                IndexRequestItem mainItem = new IndexRequestItem(id1, IndexAction.Remove);
                _searchHandler.UpdateIndex(mainItem);

                _requestQueueHandler.ProcessQueue();

                // Make sure that id3 is not searchable
                q = new FieldQuery("\"second reference\"");
                r = _searchHandler.GetSearchResults(q, 1, 10);
                Assert.Equal(0, r.TotalHits);
                Assert.Equal(0, r.IndexResponseItems.Count);

            }
            finally
            {
                sh1.Close();
            }
        }

        [Fact]
        public void SH_PublicationEndFieldTest()
        {
            //Setup services
            Uri baseAddress1 = null;
            ServiceHost sh1 = null;
            SetupIndexingServiceHost(out baseAddress1, out sh1);
            sh1.Open();

            try
            {
                // Reset indexes
                ResetAllIndexes();

                IndexRequestItem item = new IndexRequestItem("1", IndexAction.Add);
                item.Title = "testing header";
                item.DisplayText = "testing introtext";
                item.PublicationEnd = DateTime.Now.AddSeconds(-1);
                _searchHandler.UpdateIndex(item);

                item = new IndexRequestItem("2", IndexAction.Add);
                item.Title = "testing header2";
                item.DisplayText = "testing introtext2";
                item.PublicationEnd = DateTime.Now.AddMinutes(5);
                _searchHandler.UpdateIndex(item);

                item = new IndexRequestItem("3", IndexAction.Add);
                item.Title = "testing header3";
                item.DisplayText = "testing introtext3";
                _searchHandler.UpdateIndex(item);

                _requestQueueHandler.ProcessQueue();


                FieldQuery q = new FieldQuery("testing", Field.Title);
                SearchResults res = _searchHandler.GetSearchResults(q, 1, 100);
                Assert.Equal(2, res.IndexResponseItems.Count);
                Assert.Equal(2, res.IndexResponseItems.Count<IndexResponseItem>(iri => (iri.Id == "2" || iri.Id == "3") &&
                                                                 (iri.PublicationEnd == null || iri.PublicationEnd > DateTime.Now)));
            }
            finally
            {
                sh1.Close();
            }
        }

        [Fact]
        public void SH_PublicationStartFieldTest()
        {
            //Setup services
            Uri baseAddress1 = null;
            ServiceHost sh1 = null;
            SetupIndexingServiceHost(out baseAddress1, out sh1);
            sh1.Open();

            try
            {
                // Reset indexes
                ResetAllIndexes();

                IndexRequestItem item = new IndexRequestItem("1", IndexAction.Add);
                item.Title = "testing header";
                item.DisplayText = "testing introtext";
                item.PublicationStart = DateTime.Now.AddMinutes(5);

                // Check that difference is less than a second (10 million ticks)
                Assert.True(Math.Abs(DateTime.Now.AddMinutes(5).Ticks-item.PublicationStart.Value.Ticks) < 10000*1000); 

                _searchHandler.UpdateIndex(item);

                item = new IndexRequestItem("2", IndexAction.Add);
                item.Title = "testing header2";
                item.DisplayText = "testing introtext2";
                item.PublicationStart = DateTime.Now;
                _searchHandler.UpdateIndex(item);

                item = new IndexRequestItem("3", IndexAction.Add);
                item.Title = "testing header3";
                item.DisplayText = "testing introtext3";
                _searchHandler.UpdateIndex(item);

                _requestQueueHandler.ProcessQueue();


                FieldQuery q = new FieldQuery("testing", Field.Title);
                SearchResults res = _searchHandler.GetSearchResults(q, 1, 100);
                Assert.Equal(2, res.IndexResponseItems.Count);
                Assert.Equal(2, res.IndexResponseItems.Count<IndexResponseItem>(iri => (iri.Id == "2" || iri.Id == "3") &&
                                                                                    (iri.PublicationStart == null || iri.PublicationStart <= DateTime.Now)));
            }
            finally
            {
                sh1.Close();
            }
        }

        [Fact]
        public void SH_PublicationStartAndEndFieldTest()
        {
            //Setup services
            Uri baseAddress1 = null;
            ServiceHost sh1 = null;
            SetupIndexingServiceHost(out baseAddress1, out sh1);
            sh1.Open();

            try
            {
                // Reset indexes
                ResetAllIndexes();

                IndexRequestItem item = new IndexRequestItem("1", IndexAction.Add);
                item.Title = "testing header";
                item.DisplayText = "testing introtext";
                item.PublicationStart = DateTime.Now.AddMinutes(5);
                item.PublicationEnd = DateTime.Now.AddMinutes(20);
                _searchHandler.UpdateIndex(item);

                item = new IndexRequestItem("2", IndexAction.Add);
                item.Title = "testing header2";
                item.DisplayText = "testing introtext2";
                item.PublicationStart = DateTime.Now.AddMinutes(-20);
                item.PublicationEnd = DateTime.Now.AddMinutes(-5);
                _searchHandler.UpdateIndex(item);

                item = new IndexRequestItem("3", IndexAction.Add);
                item.Title = "testing header3";
                item.DisplayText = "testing introtext3";
                item.PublicationStart = DateTime.Now.AddMinutes(-20);
                item.PublicationEnd = DateTime.Now.AddMinutes(5);
                _searchHandler.UpdateIndex(item);

                _requestQueueHandler.ProcessQueue();


                FieldQuery q = new FieldQuery("testing", Field.Title);
                SearchResults res = _searchHandler.GetSearchResults(q, 1, 100);
                Assert.Equal(1, res.IndexResponseItems.Count);
                Assert.Equal("3", res.IndexResponseItems[0].Id);
            }
            finally
            {
                sh1.Close();
            }
        }

        [Fact]
        public void SH_ItemStatusTest()
        {
            //Setup services
            Uri baseAddress1 = null;
            ServiceHost sh1 = null;
            SetupIndexingServiceHost(out baseAddress1, out sh1);
            sh1.Open();

            try
            {
                // Reset indexes
                ResetAllIndexes();

                IndexRequestItem item = new IndexRequestItem("1", IndexAction.Add);
                item.Title = "testing header";
                item.DisplayText = "testing introtext";
                item.ItemStatus = ItemStatus.Approved;
                _searchHandler.UpdateIndex(item);

                item = new IndexRequestItem("2", IndexAction.Add);
                item.Title = "testing header2";
                item.DisplayText = "testing introtext2";
                item.ItemStatus = ItemStatus.Pending;
                _searchHandler.UpdateIndex(item);

                item = new IndexRequestItem("3", IndexAction.Add);
                item.Title = "testing header3";
                item.DisplayText = "testing introtext3";
                item.ItemStatus = ItemStatus.Removed;
                _searchHandler.UpdateIndex(item);

                item = new IndexRequestItem("4", IndexAction.Add);
                item.Title = "testing header4";
                item.DisplayText = "testing introtext4";
                _searchHandler.UpdateIndex(item);

                _requestQueueHandler.ProcessQueue();

                IQueryExpression q = new ItemStatusQuery(ItemStatus.Approved);
                SearchResults res = _searchHandler.GetSearchResults(q, 1, 100);
                Assert.Equal(2, res.IndexResponseItems.Count);
                Assert.Equal(2, res.IndexResponseItems.Count<IndexResponseItem>(iri => (iri.Id == "1" || iri.Id == "4")));

                q = new ItemStatusQuery(ItemStatus.Pending);
                res = _searchHandler.GetSearchResults(q, 1, 100);
                Assert.Equal(1, res.IndexResponseItems.Count);
                Assert.Equal("2", res.IndexResponseItems[0].Id);

                q = new ItemStatusQuery(ItemStatus.Removed);
                res = _searchHandler.GetSearchResults(q, 1, 100);
                Assert.Equal(1, res.IndexResponseItems.Count);
                Assert.Equal("3", res.IndexResponseItems[0].Id);

                q = new ItemStatusQuery(ItemStatus.Approved | ItemStatus.Pending);
                res = _searchHandler.GetSearchResults(q, 1, 100);
                Assert.Equal(3, res.IndexResponseItems.Count);
                Assert.Equal(3, res.IndexResponseItems.Count<IndexResponseItem>(iri => (iri.Id == "1" || iri.Id == "2" || iri.Id == "4")));

            }
            finally
            {
                sh1.Close();
            }
        }

        [Fact]
        public void SH_WildcardQueryCaseInsensitivityOnStandardFieldsTest()
        {
            //Setup services
            Uri baseAddress1 = null;
            ServiceHost sh1 = null;
            SetupIndexingServiceHost(out baseAddress1, out sh1);
            sh1.Open();

            try
            {
                // Reset indexes
                ResetAllIndexes();

                IndexRequestItem item = new IndexRequestItem("1", IndexAction.Add);
                item.Title = "Testing"; // title is made into lower case by the analyzer and should be handled as case-insensitive
                _searchHandler.UpdateIndex(item);

                _requestQueueHandler.ProcessQueue();


                IQueryExpression q = new FieldQuery("TEST*"); // a wildcard query for this field should work even when case does not match
                SearchResults res = _searchHandler.GetSearchResults(q, 1, 100);

                Assert.Equal(1, res.IndexResponseItems.Count);


                IQueryExpression q2 = new FieldQuery("test*"); // a wildcard query for this field should work even when case does not match
                SearchResults res2 = _searchHandler.GetSearchResults(q2, 1, 100);

                Assert.Equal(1, res2.IndexResponseItems.Count);
            }
            finally
            {
                sh1.Close();
            }
        }


        [Fact]
        public void SH_WildcardQueryCaseSensitivityOnSpecialFieldsTest()
        {
            //Setup services
            Uri baseAddress1 = null;
            ServiceHost sh1 = null;
            SetupIndexingServiceHost(out baseAddress1, out sh1);
            sh1.Open();

            try
            {
                // Reset indexes
                ResetAllIndexes();

                IndexRequestItem item = new IndexRequestItem("Testing", IndexAction.Add); // id is left as-is by the analyzer (no change to casing)
                _searchHandler.UpdateIndex(item);

                _requestQueueHandler.ProcessQueue();


                IQueryExpression q = new FieldQuery("TEST*", Field.Id); // a wildcard query for this field should work only when case matches
                SearchResults res = _searchHandler.GetSearchResults(q, 1, 100);

                Assert.Equal(0, res.IndexResponseItems.Count);


                IQueryExpression q2 = new FieldQuery("test*", Field.Id); // a wildcard query for this field should work only when case matches
                SearchResults res2 = _searchHandler.GetSearchResults(q2, 1, 100);

                Assert.Equal(0, res2.IndexResponseItems.Count);


                IQueryExpression q3 = new FieldQuery("Test*", Field.Id); // a wildcard query for this field should work only when case matches
                SearchResults res3 = _searchHandler.GetSearchResults(q3, 1, 100);

                Assert.Equal(1, res3.IndexResponseItems.Count);

            }
            finally
            {
                sh1.Close();
            }
        }


        [Fact]
        public void CMSSearchReIndexable_WhenTheNameIndexIsDefined_ShouldReturnIt()
        {
            Assert.True(ServiceLocator.Current.GetAllInstances<IReIndexable>().Any(i => i.NamedIndex == "testnameindex"));
        }

        [Fact]
        public void CMSSearchReIndexable_WhenNamedIndexingServiceIsDefined_ShouldReturnIt()
        {
            Assert.True(ServiceLocator.Current.GetAllInstances<IReIndexable>().Any(i => i.NamedIndexingService == "testnameservice"));
        }
        #region Helper methods


        private void AssertEqualToSearchResult(IndexRequestItem item, string namedIndex)
        {
            Collection<string> namedIndexes = new Collection<string>();
            namedIndexes.Add(namedIndex);
            EscapedFieldQuery fe = new EscapedFieldQuery(item.Id, Field.Id);
            SearchResults results = _searchHandler.GetSearchResults(fe, null, namedIndexes, 1, 20);
            Assert.Equal(1, results.IndexResponseItems.Count);
            IndexResponseItem resultItem = results.IndexResponseItems[0];

            AssertIndexItemEquality(item, resultItem);
        }

        private void AssertIndexItemEquality(IndexItemBase item1, IndexItemBase item2)
        {
            Assert.Equal(item1.Id, item2.Id);
            Assert.Equal(item1.Title, item2.Title);
            Assert.Equal(item1.DisplayText, item2.DisplayText);
            Assert.Equal(item1.Created.ToString(), item2.Created.ToString());
            Assert.Equal(item1.Modified.ToString(), item2.Modified.ToString());
            Assert.Equal(item1.ItemType, item2.ItemType);
            Assert.Equal(item1.Culture, item2.Culture);
            Assert.Equal(item1.Uri?.ToString() ?? "", item2.Uri?.ToString() ?? "");
            Assert.Equal(item1.DataUri?.ToString() ?? "", item2.DataUri?.ToString() ?? "");
            Assert.Equal(item1.ReferenceId, item2.ReferenceId);
            Assert.Equal(item1.BoostFactor, item2.BoostFactor);
            //Assert.Equal(item1.MetaData, item2.MetaData, "MetaData not equal"); // Not in response

            string expectedNamedIndex = item1.NamedIndex;
            if (item1.NamedIndex == null || item1.NamedIndex == "")
                expectedNamedIndex = "default";

            Assert.Equal(expectedNamedIndex, item2.NamedIndex);

            Assert.Equal(item1.AccessControlList, item2.AccessControlList);
            Assert.Equal(item1.Categories, item2.Categories);
            Assert.Equal(item1.VirtualPathNodes.Select(x => x.Replace(" ", "")), item2.VirtualPathNodes);
            Assert.Equal(item1.Authors, item2.Authors);
        }

        private void ResetAllIndexes()
        {
            Collection<string> indexes = _searchHandler.GetNamedIndexes();
            foreach (string name in indexes)
                _searchHandler.ResetIndex(name);
        }

        public static void SetupIndexingServiceHost(out Uri baseAddress, out ServiceHost sh)
        {
            sh = new WebServiceHost(typeof(IndexingService));
            baseAddress = sh.BaseAddresses[0]; // From application config

            ServiceMetadataBehavior smb = sh.Description.Behaviors.Find<ServiceMetadataBehavior>();
            if (smb == null)
            {
                smb = new ServiceMetadataBehavior();
                smb.HttpGetEnabled = true;
                sh.Description.Behaviors.Add(smb);
            }
            else
            {
                smb.HttpGetEnabled = false;
            }
            ServiceDebugBehavior sdb = sh.Description.Behaviors.Find<ServiceDebugBehavior>();
            if (sdb == null)
            {
                sdb = new ServiceDebugBehavior();
                sdb.IncludeExceptionDetailInFaults = true;
                sh.Description.Behaviors.Add(sdb);
            }
            else
            {
                sdb.IncludeExceptionDetailInFaults = true;
            }
        }

        private void CreateMultipleRequests()
        {
            string id1 = Guid.NewGuid().ToString();
            string id2 = Guid.NewGuid().ToString();
            string id3 = Guid.NewGuid().ToString();
            string id4 = Guid.NewGuid().ToString();
            string id5 = Guid.NewGuid().ToString();
            string id6 = Guid.NewGuid().ToString();
            string id7 = Guid.NewGuid().ToString();

            //Add items to different indexes
            IndexRequestItem item = new IndexRequestItem(id1, IndexAction.Add);
            item.Title = "This is the header for id1 in default index";
            item.DisplayText = "Detta är data i body delen";
            item.Metadata = "Detta är data i meta data delen som testas lite svårt";
            item.ItemType = "EPiServer.Common.Comment, EPiServer.Common";
            _searchHandler.UpdateIndex(item);

            item = new IndexRequestItem(id2, IndexAction.Add);
            item.Title = "This is the header for id2 in default index";
            item.ItemType = "Cms";
            _searchHandler.UpdateIndex(item);

            item = new IndexRequestItem(id3, IndexAction.Add);
            item.Title = "This is the header for id3 in default index";
            item.DisplayText = "This is the intro text for id3 in default index";
            _searchHandler.UpdateIndex(item);

            item = new IndexRequestItem(id4, IndexAction.Add);
            item.Title = "This is the header for id4 in default index";
            _searchHandler.UpdateIndex(item);

            item = new IndexRequestItem(id5, IndexAction.Add);
            item.Title = "This is the header for id5 in default index";
            _searchHandler.UpdateIndex(item);

            item = new IndexRequestItem(id6, IndexAction.Add);
            item.Title = "This is the header for id6 in default index";
            _searchHandler.UpdateIndex(item);

            item = new IndexRequestItem(id7, IndexAction.Add);
            item.Title = "This is the header for id7 in default index";
            _searchHandler.UpdateIndex(item);

            item = new IndexRequestItem(id1, IndexAction.Add);
            item.Title = "This is the header for id1 in testindex2";
            item.NamedIndex = "testindex2";
            _searchHandler.UpdateIndex(item);

            item = new IndexRequestItem(id2, IndexAction.Add);
            item.Title = "This is the header for id2 in testindex2";
            item.NamedIndex = "testindex2";
            _searchHandler.UpdateIndex(item);

            item = new IndexRequestItem(id3, IndexAction.Add);
            item.Title = "This is the header for id3 in testindex2";
            item.NamedIndex = "testindex2";
            _searchHandler.UpdateIndex(item);

            item = new IndexRequestItem(id4, IndexAction.Add);
            item.Title = "This is the header for id4 in testindex3";
            item.NamedIndex = "testindex3";
            _searchHandler.UpdateIndex(item);

            item = new IndexRequestItem(id5, IndexAction.Add);
            item.Title = "This is the header for id5 in testindex3";
            item.NamedIndex = "testindex3";
            _searchHandler.UpdateIndex(item);

            item = new IndexRequestItem(id6, IndexAction.Add);
            item.Title = "This is the header for id6 in testindex3";
            item.NamedIndex = "testindex3";
            _searchHandler.UpdateIndex(item);

            item = new IndexRequestItem(id7, IndexAction.Add);
            item.Title = "This is the header for id7 in testindex3";
            item.NamedIndex = "testindex3";
            _searchHandler.UpdateIndex(item);
        }

        #endregion
    }
}