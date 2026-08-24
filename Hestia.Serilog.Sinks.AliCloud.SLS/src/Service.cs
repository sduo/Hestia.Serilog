using Aliyun.Api.LogService.Infrastructure.Serialization.Protobuf;
using Google.Protobuf;
using K4os.Compression.LZ4;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Serilog.Events;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using static Hestia.Serilog.Sinks.AliCloud.SLS.Utility;
using Log = Aliyun.Api.LogService.Infrastructure.Serialization.Protobuf.Log;

namespace Hestia.Serilog.Sinks.AliCloud.SLS
{
    //https://github.com/aliyun/aliyun-log-dotnetcore-sdk/blob/master/Aliyun.Api.LogService/Infrastructure/Protocol/Http/HttpRequestMessageBuilder.cs#L51

    public class LogServiceChainSink(string name, IServiceProvider services) : ChainSink
    {
        private readonly IHttpClientFactory Http = services.GetService<IHttpClientFactory>();
        private readonly IConfigurationSection Configuration = services.GetService<IConfiguration>().GetSection($"SLS:{name}");


        public Func<IReadOnlyDictionary<string, string>> TagFactory { get; init; } = null;

        public Func<string> ShardFactory { get; init; } = null;

        public Func<LogEvent, IReadOnlyDictionary<string,string>> ContentFactory { get; init; } = (@event)=> {
            var content = new Dictionary<string, string>() {
                { Fields.Timestamp, @event.Timestamp.ToString("yyyy-MM-dd HH:mm:ss.fff zzz", CultureInfo.InvariantCulture) },
                { Fields.Level, @event.Level.ToString() },
                { Fields.Template, @event.MessageTemplate.Text },
                { Fields.Message, @event.RenderMessage() },
                { Fields.Properties, Serilog.Utility.RenderLogEventPropertiesToJson(@event.Properties)  },
                { Fields.Exception, @event.Exception?.ToString() ?? string.Empty },
                { Fields.TraceId, @event.TraceId?.ToHexString() ?? string.Empty },
                { Fields.SpanId, @event.SpanId?.ToHexString() ?? string.Empty  }
            };
#if NET8_0_OR_GREATER
            return content.AsReadOnly();
#else
            return new ReadOnlyDictionary<string,string>(content);
#endif
        };


        private static StringBuilder GenerateSignSource(HttpRequestMessage request)
        {
            var list = new List<string>()
            {
                request.Method.Method,
                (request.Content.Headers.TryGetValues(Headers.ContentMD5, out var values) ? values.FirstOrDefault() : null) ?? string.Empty,
                request.Content?.Headers.ContentType.MediaType ?? string.Empty,
                (request.Headers.Date ?? DateTimeOffset.Now).ToString("r"), /* RFC 822 format */
            };

            list.AddRange(request.Headers
                .Concat(request.Content?.Headers ?? Enumerable.Empty<KeyValuePair<string, IEnumerable<string>>>())
                .Where(x => x.Key.StartsWith("x-log") || x.Key.StartsWith("x-acs"))
                .Select(x => new KeyValuePair<string, string>(x.Key.ToLower(), x.Value.SingleOrDefault() /* Fault tolerance */))
                .Where(x => !string.IsNullOrEmpty(x.Value)) // Remove empty header
                .OrderBy(x => x.Key)
                .Select(x => $"{x.Key}:{x.Value}")
            );

            list.Add(request.RequestUri.OriginalString);

            var source = new StringBuilder(string.Join('\n', list));
            
            return source;
        }


        private static bool Filter(LogEvent log, string endpoint)
        {            
            if(log is null) { return false; }
            if (log.Properties.TryGetValue("Uri", out var uri) || log.Properties.TryGetValue("RequestUri", out uri))
            {
                if(((uri as ScalarValue)?.Value as string)?.StartsWith(endpoint, StringComparison.OrdinalIgnoreCase) == true) { return false; }
            }
            return true;
        }

        protected override void Write(IReadOnlyCollection<LogEvent> events)
        {
            var ak = Configuration.GetValue<string>("ak", null);
            if(string.IsNullOrEmpty(ak)) {  throw new ArgumentException("ak is required"); }
            
            var sk = Configuration.GetValue<string>("sk", null);
            if (string.IsNullOrEmpty(sk)) { throw new ArgumentException("sk is required"); }           
            
            var store = Configuration.GetValue<string>("store");
            if (string.IsNullOrEmpty(store)) { throw new ArgumentException("store is required"); }

            var endpoint = Configuration.GetValue<string>("endpoint");
            if (string.IsNullOrEmpty(endpoint)) { throw new ArgumentException("endpoint is required"); }

            var logs = events.Where(x => Filter(x, endpoint));
            if (!logs.Any()) { return; }            
            var topic = Configuration.GetValue("topic", string.Empty);
            var source = Configuration.GetValue("source", string.Empty);

            var shard = ShardFactory?.Invoke() ?? null;
            var path = new StringBuilder($"/logstores/{store}/shards");
            var query = new Dictionary<string, string>();
            if (string.IsNullOrEmpty(shard))
            {
                path.Append("/lb");
            }
            else
            {
                path.Append("/route");
                query.Add("key", shard);
            }
            var url = query.Count == 0 ? path.ToString() : string.Join('?', path.ToString(), string.Join('&', query.Select(kv => string.Join('=', kv.Key, kv.Value))));

            using var request = new HttpRequestMessage(HttpMethod.Post, url);
            #region HttpRequestMessageBuilder
            #region ParseUri
            #endregion
            #region FillDefaultHeaders
            request.Headers.Date = DateTimeOffset.Now;
            request.Headers.UserAgent.Add(new ProductInfoHeaderValue(SDK, Utility.Version));
            request.Headers.Add(Headers.ApiVersion, ApiVersion);
            #endregion
            #region Content
            var group = new LogGroup
            {
                // https://github.com/aliyun/aliyun-log-dotnetcore-sdk/issues/14
                Topic = topic, // Empty is allowed, but not null.
                Source = source, // Empty is allowed, but not null.
                LogTags = {
                    TagFactory?.Invoke()?.Select(x=> new LogTag()
                    {
                        Key = x.Key,
                        Value = x.Value ?? string.Empty // Empty is allowed, but not null.
                    }) ?? []
                },
                Logs = {
                    logs.Select(x => new Log {
                        Time = (uint)x.Timestamp.ToUnixTimeSeconds(),
                        Contents = {
                            ContentFactory?.Invoke(x).Where(x=>!string.IsNullOrEmpty(x.Key)).ToDictionary(x=>x.Key, x=>x.Value).Select(kv=> new Log.Types.Content{
                                Key = kv.Key, Value = kv.Value ?? string.Empty
                            })
                        }
                    }) ?? []
                }
            };
            #endregion
            #region Serialize
            var serialized = group.ToByteArray();
            request.Headers.Add(Headers.BodyRawSize, serialized.Length.ToString());
            #endregion
            #region Compress
            request.Headers.Add(Headers.CompressType, Compress);
            byte[] compressed = new byte[LZ4Codec.MaximumOutputSize(serialized.Length)];
            int length = LZ4Codec.Encode(serialized, 0, serialized.Length, compressed, 0, compressed.Length, LZ4Level.L00_FAST);
            Array.Resize(ref compressed, length);
            #endregion
            #endregion
            #region SendRequestAsync
            #region Authenticate            
            #endregion
            #region Sign
            request.Headers.Add(Headers.SignatureMethod, Signature);
            #endregion
            #region Build
            request.Content = new ByteArrayContent(compressed);
            request.Content.Headers.ContentType = Headers.MimeProtobuf;
            request.Content.Headers.ContentLength = compressed.Length;
            var md5 = Md5(compressed);
            request.Content.Headers.Add(Headers.ContentMD5, md5);
            var src = GenerateSignSource(request);
            if (query.Count > 0)
            {
                src.Append('?');
                src.Append(string.Join('&', query.OrderBy(x => x.Key).Select(x => $"{x.Key}={x.Value}")));
            }
            var signature = HMAC_SHA1(Encoding.UTF8.GetBytes(sk), Encoding.UTF8.GetBytes(src.ToString()));
            request.Headers.Authorization = new AuthenticationHeaderValue("LOG", $"{ak}:{signature}");
            var uri = string.Concat(endpoint, query.Count == 0 ? path.ToString() : string.Join('?', path.ToString(), string.Join('&', query.OrderBy(kv => kv.Key).Select(kv => string.Join('=', EncodeURI(kv.Key), EncodeURI(kv.Value))))));
            request.RequestUri = new Uri(uri, UriKind.Absolute);
            #endregion
            #endregion
            using var rpc = Http.CreateClient();
            using var response = rpc.Send(request);
            response.EnsureSuccessStatusCode();
        }
    }
}
