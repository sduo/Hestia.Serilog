using Hestia.Security;
using Serilog.Events;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net.Http.Headers;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Web;
using static Hestia.Serilog.Utility;

namespace Hestia.Serilog.Sinks.AliCloud.SLS
{
    public sealed class Utility
    {
        // https://github.com/aliyun/aliyun-log-dotnetcore-sdk/blob/master/Aliyun.Api.LogService/Infrastructure/Protocol/Http/LogHeaders.cs
        public sealed class Headers
        {
            public const string BodyRawSize = "x-log-bodyrawsize";
            public const string ApiVersion = "x-log-apiversion";
            public const string CompressType = "x-log-compresstype";
            public const string SignatureMethod = "x-log-signaturemethod";
            public const string ContentMD5 = "Content-MD5";
            public static readonly MediaTypeHeaderValue MimeProtobuf = new ("application/x-protobuf");
        }        
        internal static class Fields
        {
            public const string Timestamp = "timestamp";
            public const string Level = "level";
            public const string TraceId = "trace_id";
            public const string SpanId = "span_id";
            public const string Message = "message";
            public const string Template = "template";
            public const string Properties = "properties";
            public const string Exception = "exception";
        }       
        public const string SDK = "hestia.serilog.sls" /* "log-dotnetcore-sdk" */;
        public static readonly string Version  = Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        public const string ApiVersion = "0.6.0";
        public const string Compress = "lz4";
        public const string Signature = "hmac-sha1";
#if NET8_0_OR_GREATER
        public static readonly IReadOnlyDictionary<char, string> EncodeMap = new Dictionary<char, string>() { { '+', "%20" }, { '*', "%2A" }, { '~', "%7E" }, { '/', "%2F" } }.AsReadOnly();
#else
        public static readonly IReadOnlyDictionary<char, string> EncodeMap = new ReadOnlyDictionary<char, string>(new Dictionary<char, string>() { { '+', "%20" }, { '*', "%2A" }, { '~', "%7E" }, { '/', "%2F" } });
#endif
        public static string Md5(byte[] content) => (content?.Length > 0) ? Convert.ToHexString(HASH.MD5(content)) : string.Empty;

        public static string HMAC_SHA1(byte[] key, byte[] content) => (key?.Length > 0 && content?.Length > 0) ? Convert.ToBase64String(MAC.HMAC_SHA1(key, content)) : string.Empty;

        public static string EncodeURI(string content, Encoding encoding = null) => string.IsNullOrEmpty(content) ? string.Empty : HttpUtility.UrlEncode(content, encoding ?? Encoding.UTF8).AsEnumerable().Aggregate(new StringBuilder(), (sb, c) => { if (EncodeMap.TryGetValue(c, out var m)) { return sb.Append(m); } else { return sb.Append(c); } }).ToString();

        public static Dictionary<string, string> ParseQueryString(string querystring)
        {
            if (string.IsNullOrEmpty(querystring))
            {
                return [];
            }
            var nvc = HttpUtility.ParseQueryString(querystring);
            return nvc.AllKeys.SelectMany((k)=> { return nvc.GetValues(k); }, (k,v)=> { return new KeyValuePair<string, string>(k, v); }).ToDictionary(kv=>kv.Key, kv=>kv.Value);
        }
    }
}
