﻿using log4net;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace EPiServer.Search.IndexingService
{
    public interface IIndexingServiceSettings
    {
        void Init();
        static ILog IndexingServiceServiceLog { get; set; }
    }
}
