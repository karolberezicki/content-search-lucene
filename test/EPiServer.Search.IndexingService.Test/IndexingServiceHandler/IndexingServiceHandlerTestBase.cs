﻿using EPiServer.Search.IndexingService.Helpers;
using Microsoft.Extensions.Logging;
using Moq;

namespace EPiServer.Search.IndexingService.Test.IndexingServiceHandler
{
    public class IndexingServiceHandlerTestBase
    {
        protected Mock<IFeedHelper> _feedHelperMock;
        protected Mock<ILuceneHelper> _luceneHelperMock;
        protected Mock<ICommonFunc> _commonFuncMock;
        protected Mock<IResponseExceptionHelper> _responseExceptionHelperMock;
        protected Mock<IDocumentHelper> _documentHelperMock;

        public IndexingServiceHandlerTestBase()
        {
            _feedHelperMock = new Mock<IFeedHelper>();
            _luceneHelperMock = new Mock<ILuceneHelper>();
            _commonFuncMock = new Mock<ICommonFunc>();
            _responseExceptionHelperMock = new Mock<IResponseExceptionHelper>();
            _documentHelperMock = new Mock<IDocumentHelper>();

            var logMock = new Mock<ILogger>();
            IndexingServiceSettings.IndexingServiceServiceLog = logMock.Object;
        }

        public EPiServer.Search.IndexingService.IndexingServiceHandler SetupMock()
        {
            return new IndexingService.IndexingServiceHandler(
                _feedHelperMock.Object,
                _luceneHelperMock.Object,
                _commonFuncMock.Object,
                _responseExceptionHelperMock.Object,
                _documentHelperMock.Object);
        }
    }
}
