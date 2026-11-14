using System.Net;

namespace NERO.AI;

public sealed class GeminiApiException : Exception
{
    public GeminiApiException(HttpStatusCode statusCode, string message) : base(message)
    {
        StatusCode = statusCode;
    }

    public HttpStatusCode StatusCode { get; }
}
