using System;
using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using System.Text;
using System.Web;

namespace Hestia.Serilog.Sinks.AliCloud.SLS.Tests
{
    [ExcludeFromCodeCoverage]
    internal sealed class Original
    {
        // https://github.com/aliyun/aliyun-log-dotnetcore-sdk/blob/master/Aliyun.Api.LogService/Infrastructure/Protocol/Http/HttpRequestMessageBuilder.cs#L415
        internal static string CalculateContentMd5(byte[] content)
        {
            using var hasher = MD5.Create();
            var hash = hasher.ComputeHash(content);
            return BitConverter.ToString(hash).Replace("-", string.Empty);
        }

        // https://github.com/aliyun/aliyun-log-dotnetcore-sdk/blob/master/Aliyun.Api.LogService/Infrastructure/Protocol/Http/HttpRequestMessageBuilder.cs#L318
        internal static string ComputeSignature(byte[] key, byte[] content)
        {
            using var hasher = new HMACSHA1(key);
            var sign = hasher.ComputeHash(content);
            return Convert.ToBase64String(sign);
        }

        // https://github.com/aliyun/aliyun-log-dotnetcore-sdk/blob/master/Aliyun.Api.LogService/Infrastructure/Protocol/Http/HttpRequestMessageBuilder.cs#L447
        internal static string encodeUrl(string value, Encoding encoding)
        {
            if (value == null) { return ""; }
            string encoded = HttpUtility.UrlEncode(value, encoding);
            return encoded.Replace("+", "%20").Replace("*", "%2A").Replace("~", "%7E").Replace("/", "%2F");
        }
    }
}
