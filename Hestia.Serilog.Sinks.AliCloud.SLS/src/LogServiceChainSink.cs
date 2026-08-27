using Google.Protobuf;
using Hestia.Core;
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
using System.Threading.Tasks;
using static Hestia.Serilog.Sinks.AliCloud.SLS.Utility;
using ProtobufLog = Aliyun.Api.LogService.Infrastructure.Serialization.Protobuf.Log;
using ProtobufLogGroup = Aliyun.Api.LogService.Infrastructure.Serialization.Protobuf.LogGroup;
using ProtobufLogTag = Aliyun.Api.LogService.Infrastructure.Serialization.Protobuf.LogTag;
using ProtobufLogContent = Aliyun.Api.LogService.Infrastructure.Serialization.Protobuf.Log.Types.Content;


namespace Hestia.Serilog.Sinks.AliCloud.SLS
{
    //https://github.com/aliyun/aliyun-log-dotnetcore-sdk/blob/master/Aliyun.Api.LogService/Infrastructure/Protocol/Http/HttpRequestMessageBuilder.cs#L51

    public sealed class LogServiceChainSink(string name, IServiceProvider services, ChainSink next=null) : ChainSink(next)
    {
        public LogServiceChainSink(string name, IServiceProvider services):this (name, services, new LocalFileChainSink()) { }
        public LogServiceChainSink(IServiceProvider services) : this(null, services) { }
        private readonly IHttpClientFactory Http = services.GetService<IHttpClientFactory>();
        private readonly IConfigurationSection Configuration = services.GetService<IConfiguration>().GetSection(string.IsNullOrEmpty(name) ? "SLS" : $"SLS:{name}");

        private IReadOnlyDictionary<string, string> FixedTags { get; init; } = null;

        public Func<IConfigurationSection, LogEvent, Log> LogBuilder { get; init; } = (configuration, @event) => {
            var tags = new Dictionary<string, string>();
            if( @event.Properties.TryGetValue(Properties.SourceContext, out var src))
            {
                var source = (src as ScalarValue)?.Value as string;
                if(!string.IsNullOrEmpty(source)) { tags.Add(TagKeys.SourceContext, source); }                
            }  

            var content = new Dictionary<string, string>() {
                { ContentKeys.Timestamp, @event.Timestamp.ToString(configuration.GetValue($"format:{ContentKeys.Timestamp}","yyyy-MM-dd HH:mm:ss.fff zzz"), CultureInfo.InvariantCulture) },
                { ContentKeys.Level, @event.Level.ToString() },
                { ContentKeys.Template, @event.MessageTemplate.Text },
                { ContentKeys.Message, @event.RenderMessage() },
                { ContentKeys.Properties, Serilog.Utility.RenderLogEventPropertiesToJson(@event.Properties)  },
                { ContentKeys.Exception, @event.Exception?.ToString() ?? string.Empty },
                { ContentKeys.TraceId, @event.TraceId?.ToHexString() ?? string.Empty },
                { ContentKeys.SpanId, @event.SpanId?.ToHexString() ?? string.Empty  }
            };
            return new Log()
            {
                Shard = null,
                Tags = tags.Count == 0 ? null : tags,
                Timestamp = (uint)@event.Timestamp.ToUnixTimeSeconds(),
#if NET8_0_OR_GREATER
                Contents = content.AsReadOnly()
#else
                Contents = new ReadOnlyDictionary<string, string>(content)
#endif
            };
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
            if (log.Properties.TryGetValue(Properties.Uri, out var uri) || log.Properties.TryGetValue(Properties.RequestUri, out uri))
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

            var logs = events.Where(x => Filter(x, endpoint)).Select(x=> LogBuilder?.Invoke(Configuration,x)).Where(x=>x is not null).ToLookup(x=>x.BuildLookupKey());
            if (logs.Count==0) { return; }            
            var topic = Configuration.GetValue("topic", string.Empty);
            var source = Configuration.GetValue("source", string.Empty);

            var errors = new List<Exception>(logs.Count);

            foreach(var batch in logs)
            {
                try
                {
                    var shard = batch.FirstOrDefault().Shard;
                    var tags = batch.FirstOrDefault().Tags;
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
                    var group = new ProtobufLogGroup
                    {
                        // https://github.com/aliyun/aliyun-log-dotnetcore-sdk/issues/14
                        Topic = topic, // Empty is allowed, but not null.
                        Source = source, // Empty is allowed, but not null.
                        LogTags = {
                            tags?.Union(FixedTags).Where(x=>!string.IsNullOrEmpty(x.Key)).Select(x=> new ProtobufLogTag() {
                                Key = x.Key,
                                Value = x.Value ?? string.Empty // Empty is allowed, but not null.                           
                            }) ?? []
                        },
                        Logs = {
                        batch.Select(x => new ProtobufLog {
                            Time = x.Timestamp,
                            Contents = {
                                x.Contents?.Where(x=>!string.IsNullOrEmpty(x.Key)).Select(x=> new ProtobufLogContent{
                                    Key = x.Key,
                                    Value = x.Value ?? string.Empty
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
                catch (Exception ex)
                {
                    errors.Add(ex);
                }                
            }

            if (errors.Count > 0)
            {
                throw new AggregateException(errors);
            }
        }
    }
}
