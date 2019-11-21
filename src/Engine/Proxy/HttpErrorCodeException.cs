using System;
using System.Net;

namespace Helium.Engine.Proxy
{
    public class HttpErrorCodeException : Exception
    {
        public HttpErrorCodeException(HttpStatusCode errorCode) {
            ErrorCode = errorCode;
        }

        public HttpStatusCode ErrorCode { get; }
    }
}