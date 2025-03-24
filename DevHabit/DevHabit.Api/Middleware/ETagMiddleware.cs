using System.Security.Cryptography;
using System.Text;
using DevHabit.Api.Services;

namespace DevHabit.Api.Middleware;

public sealed class ETagMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, InMemoryETagStore eTagStore)
    {
        if (CanSkipETag(context))
        {
            await next(context);

            return;
        }

        string resourceUri = context.Request.Path.Value!;
        string ifNoneMatch = context.Request.Headers.IfNoneMatch.FirstOrDefault()?.Replace("\"", "");

        Stream originalStream = context.Response.Body;
        using var memoryStream = new MemoryStream();
        context.Response.Body = memoryStream;
        
        await next(context);

        if (IsETaggableResponse(context))
        {
            memoryStream.Position = 0;
            
            byte[] body = await GetResponseBytes(memoryStream);
            string eTag = GenerateETag(body);
            
            eTagStore.SetETag(resourceUri, eTag);
            context.Response.Headers.ETag = $"\"{eTag}\"";
            context.Response.Body = originalStream;

            if (context.Request.Method == HttpMethods.Get && ifNoneMatch == eTag)
            {
                context.Response.StatusCode = StatusCodes.Status304NotModified;
                context.Response.ContentLength = 0;
                return;
            }
        }

        memoryStream.Position = 0;
        await memoryStream.CopyToAsync(originalStream);
    }

    private string GenerateETag(byte[] body)
    {
        byte[] bytes = SHA512.HashData(body);
        return Convert.ToHexString(bytes);
    }

    private static async Task<byte[]> GetResponseBytes(MemoryStream memoryStream)
    {
        using var reader = new StreamReader(memoryStream, leaveOpen: true);
        memoryStream.Position = 0;

        string body = await reader.ReadToEndAsync();

        return Encoding.UTF8.GetBytes(body);
    }

    private bool IsETaggableResponse(HttpContext context)
    {
        return context.Response.StatusCode == StatusCodes.Status200OK &&
                (context.Response.Headers.ContentType
                    .FirstOrDefault()?
                    .Contains("json", StringComparison.OrdinalIgnoreCase) ?? false);
    }

    private bool CanSkipETag(HttpContext context)
    {
        return context.Request.Method == HttpMethods.Post || 
                context.Request.Method == HttpMethods.Delete;
    }
}
