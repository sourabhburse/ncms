using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace NCMS.Backend.Shared.Quota
{
    public enum QuotaResource
    {
        ApiCalls,
        StorageBytes,
        Users,
        ActiveFeatureFlags
    }
}