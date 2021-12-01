using System;
using System.Collections.ObjectModel;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace EPiServer.Search.IndexingService.Test.Helpers.LuceneHelper
{
    [Trait(nameof(EPiServer.Search.IndexingService.Helpers.LuceneHelper), nameof(EPiServer.Search.IndexingService.Helpers.LuceneHelper.Remove))]
    public class RemoveTest : LuceneHelperTestBase
    {
        [Fact]
        public void RemoveFeedItemModelNamedIndex_WhenFeedItemIdNotEqualIgnoreItemId_ShouldRunRemove()
        {
            var logMock = new Mock<ILogger>();
            IndexingServiceSettings.IndexingServiceServiceLog = logMock.Object;
            var namedIndexMock = new Mock<NamedIndex>("testindex1");
            var dir1 = Lucene.Net.Store.FSDirectory.Open(new System.IO.DirectoryInfo(string.Format(@"c:\fake\App_Data\{0}\Main", Guid.NewGuid())));
            namedIndexMock.SetupGet(x => x.Directory).Returns(() => dir1);

            var classInstant = SetupMock();
            classInstant.Remove(new FeedItemModel() { Id = "1" }, namedIndexMock.Object);
        }

        [Fact]
        public void RemoveFeedItemModelNamedIndex_WhenFeedItemIdNotEqualIgnoreItemId_ShouldRunGetAutoUpdateVirtualPathValue()
        {
            var logMock = new Mock<ILogger>();
            IndexingServiceSettings.IndexingServiceServiceLog = logMock.Object;
            var namedIndexMock = new Mock<NamedIndex>("testindex1");
            var dir1 = Lucene.Net.Store.FSDirectory.Open(new System.IO.DirectoryInfo(string.Format(@"c:\fake\App_Data\{0}\Main", Guid.NewGuid())));
            namedIndexMock.SetupGet(x => x.Directory).Returns(() => dir1);

            _feedHelperMock.Setup(x => x.GetAutoUpdateVirtualPathValue(It.IsAny<FeedItemModel>())).Returns(true);

            var feed = new FeedItemModel() { Id = "1" };
            feed.ElementExtensions.Add(IndexingServiceSettings.SyndicationItemElementNameVirtualPath, new Collection<string>() { });

            var classInstant = SetupMock();
            classInstant.Remove(feed, namedIndexMock.Object);
        }

        [Fact]
        public void RemoveStringNamedIndexBool_ShouldWork()
        {
            var logMock = new Mock<ILogger>();
            IndexingServiceSettings.IndexingServiceServiceLog = logMock.Object;

            var namedIndexMock = new Mock<NamedIndex>("testindex1");
            var dir1 = Lucene.Net.Store.FSDirectory.Open(new System.IO.DirectoryInfo(string.Format(@"c:\fake\App_Data\{0}\Main", Guid.NewGuid())));
            namedIndexMock.SetupGet(x => x.Directory).Returns(() => dir1);

            var classInstant = SetupMock();
            classInstant.Remove("1", namedIndexMock.Object, false);
        }
    }
}
