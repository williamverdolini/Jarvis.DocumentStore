using Jarvis.Framework.Shared.ReadModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Jarvis.DocumentStore.Core.ReadModel
{
    public class TenantStatsReadModel : AbstractReadModel<string>
    {
        public Int32 Documents { get; set; }

        public Int32 Handles { get; set; }

        public Int32 Files { get; set; }

        public Int64 DocumentSize { get; set; }
    }
}
