using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace NCMS.Backend.Core.Context
{
    public interface IRequestContext
    {
        /// <summary>
        /// Gets the remote IP address of the client making the request.
        /// </summary>
        string? IpAddress { get; }

        /// <summary>
        /// Gets the User-Agent header from the request.
        /// </summary>
        string? UserAgent { get; }

        /// <summary>
        /// Gets a client identifier from the X-Client-Id header, or a default value.
        /// </summary>
        string ClientId { get; }

        /// <summary>
        /// Gets the origin URL (scheme + host + path base) of the current request.
        /// </summary>
        string? Origin { get; }
    }
}