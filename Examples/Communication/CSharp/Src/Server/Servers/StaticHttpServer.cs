using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Net;
using Firefly;
using Firefly.Streaming;
using BaseSystem;

namespace Server
{
    /// <summary>
    /// 本类的所有非继承的公共成员均是线程安全的。
    /// </summary>
    public class StaticHttpServer : IServer
    {
        private ExternalHttpServer Inner;
        public IServerContext ServerContext { get; private set; }
        private Action<Action> QueueUserWorkItem;
        private int ReadBufferSize;


        /// <summary>只能在启动前修改，以保证线程安全</summary>
        public int MaxBadCommands { get; set; }

        /// <summary>只能在启动前修改，以保证线程安全</summary>
        public String[] Bindings { get; set; }
        /// <summary>只能在启动前修改，以保证线程安全</summary>
        public int? SessionIdleTimeout { get; set; }
        /// <summary>只能在启动前修改，以保证线程安全</summary>
        public int? MaxConnections { get; set; }
        /// <summary>只能在启动前修改，以保证线程安全</summary>
        public int? MaxConnectionsPerIP { get; set; }
        /// <summary>只能在启动前修改，以保证线程安全</summary>
        public int? MaxUnauthenticatedPerIP { get; set; }
        /// <summary>只能在启动前修改，以保证线程安全</summary>
        public String ServiceVirtualPath { get; set; }
        /// <summary>只能在启动前修改，以保证线程安全</summary>
        public String PhysicalPath { get; set; }
        /// <summary>只能在启动前修改，以保证线程安全</summary>
        public String[] Indices { get; set; }
        /// <summary>只能在启动前修改，以保证线程安全</summary>
        public Boolean EnableClientRewrite { get; set; }
        /// <summary>只能在启动前修改，以保证线程安全</summary>
        public Func<String, Optional<String>> RelativePathTranslator { get; set; }

        public StaticHttpServer(IServerContext sc, Action<Action> QueueUserWorkItem, int ReadBufferSize = 8 * 1024)
        {
            ServerContext = sc;
            this.QueueUserWorkItem = QueueUserWorkItem;
            this.ReadBufferSize = ReadBufferSize;
            this.Indices = new String[] { };
        }

        public void Start()
        {
            var Root = FileNameHandling.GetDirectoryPathWithTailingSeparator(FileNameHandling.GetAbsolutePath(PhysicalPath, Environment.CurrentDirectory));
            var Indices = this.Indices;

            var MimeTypes = new Dictionary<String, String>(StringComparer.OrdinalIgnoreCase);
            MimeTypes.Add("htm", "text/html");
            MimeTypes.Add("html", "text/html");
            MimeTypes.Add("shtml", "text/html");
            MimeTypes.Add("xml", "text/xml");
            MimeTypes.Add("css", "text/css");
            MimeTypes.Add("js", "application/x-javascript");
            MimeTypes.Add("txt", "text/plain");
            MimeTypes.Add("gif", "image/gif");
            MimeTypes.Add("jpg", "image/jpeg");
            MimeTypes.Add("jpeg", "image/jpeg");
            MimeTypes.Add("png", "image/png");
            MimeTypes.Add("tif", "image/tiff");
            MimeTypes.Add("tiff", "image/tiff");
            MimeTypes.Add("ico", "image/x-icon");
            MimeTypes.Add("mp3", "audio/mpeg");
            MimeTypes.Add("wav", "audio/wav");
            MimeTypes.Add("mid", "audio/midi");
            MimeTypes.Add("midi", "audio/midi");

            Func<String, String> GetMimeType = Extension =>
            {
                if (MimeTypes.ContainsKey(Extension)) { return MimeTypes[Extension]; }
                return "application/octet-stream";
            };

            Func<String, Int64, Optional<KeyValuePair<Int64, Int64>>> TryGetRange = (RangeStrs, FileLength) =>
            {
                // http://www.w3.org/Protocols/rfc2616/rfc2616-sec14.html

                Int64 Start = 0;
                Int64 Length = FileLength;
                var RangeStrsTrimmed = RangeStrs.Trim(' ');
                if (RangeStrsTrimmed.StartsWith("bytes="))
                {
                    RangeStrsTrimmed = RangeStrsTrimmed.Substring("bytes=".Length);
                }
                else
                {
                    return Optional<KeyValuePair<Int64, Int64>>.Empty;
                }
                var RangeStrsSplitted = RangeStrsTrimmed.Split(',');
                if (RangeStrsSplitted.Length != 1)
                {
                    return Optional<KeyValuePair<Int64, Int64>>.Empty;
                }
                var Parts = RangeStrsSplitted.Single().Trim(' ').Split('-').Select(p => p.Trim(' ')).ToArray();
                if (Parts.Length != 2)
                {
                    return Optional<KeyValuePair<Int64, Int64>>.Empty;
                }
                if (Parts[0] == "")
                {
                    // 不支持suffix-byte-range-spec = "-" suffix-length
                    return Optional<KeyValuePair<Int64, Int64>>.Empty;
                }
                if (!Int64.TryParse(Parts[0], out Start))
                {
                    return Optional<KeyValuePair<Int64, Int64>>.Empty;
                }
                if ((Start < 0) || (Start > FileLength))
                {
                    return Optional<KeyValuePair<Int64, Int64>>.Empty;
                }
                if (Parts[1] == "")
                {
                    Length = FileLength - Start;
                }
                else
                {
                    Int64 End;
                    if (!Int64.TryParse(Parts[1], out End))
                    {
                        return Optional<KeyValuePair<Int64, Int64>>.Empty;
                    }
                    if (End < Start)
                    {
                        return Optional<KeyValuePair<Int64, Int64>>.Empty;
                    }
                    Length = Math.Min(End + 1 - Start, FileLength - Start);
                }
                return new KeyValuePair<Int64, Int64>(Start, Length);
            };

            Action<String, HttpListenerContext, IPEndPoint, Action, Action<Exception>> RequestHandler = (RelativePath, a, e, OnSuccess, OnFailure) =>
            {
                if (a.Request.ContentLength64 > 0)
                {
                    a.Response.StatusCode = 400;
                    OnSuccess();
                    return;
                }

                if (RelativePathTranslator != null)
                {
                    var oRelativePath = RelativePathTranslator(RelativePath);
                    if (oRelativePath.OnNone)
                    {
                        a.Response.StatusCode = 404;
                        OnSuccess();
                        return;
                    }
                    RelativePath = oRelativePath.Value;
                }
                var Path = FileNameHandling.GetAbsolutePath(RelativePath, Root);
                if ((RelativePath != "") && !Path.StartsWith(Root))
                {
                    a.Response.StatusCode = 400;
                    OnSuccess();
                    return;
                }

                if (!File.Exists(Path))
                {
                    var Found = false;

                    var UnescapedPath = Uri.UnescapeDataString(Path);
                    if (File.Exists(UnescapedPath))
                    {
                        Path = UnescapedPath;
                        Found = true;
                    }

                    if (!Found)
                    {
                        foreach (var Index in Indices)
                        {
                            var IndexPath = FileNameHandling.GetPath(Path, Index);
                            if (File.Exists(IndexPath))
                            {
                                Path = IndexPath;
                                Found = true;
                                break;
                            }
                        }
                        foreach (var Index in Indices)
                        {
                            var IndexPath = FileNameHandling.GetPath(UnescapedPath, Index);
                            if (File.Exists(IndexPath))
                            {
                                Path = IndexPath;
                                Found = true;
                                break;
                            }
                        }
                    }

                    if (!Found && EnableClientRewrite)
                    {
                        foreach (var Index in Indices)
                        {
                            var IndexPath = FileNameHandling.GetPath(Root, Index);
                            if (File.Exists(IndexPath))
                            {
                                Path = IndexPath;
                                Found = true;
                                break;
                            }
                        }
                    }

                    if (!Found)
                    {
                        a.Response.StatusCode = 404;
                        OnSuccess();
                        return;
                    }
                }

                if (!Path.StartsWith(Root))
                {
                    a.Response.StatusCode = 400;
                    OnSuccess();
                    return;
                }

                var Buffer = new Byte[512 * 1024];
                using (var fs = Streams.OpenReadable(Path))
                {
                    var LastWriteTime = File.GetLastWriteTimeUtc(Path);
                    var LastWriteTimeStr = LastWriteTime.ToString("R");
                    var ETag = "\"" + Times.DateTimeUtcToString(LastWriteTime) + "\"";

                    var IfModifiedSinceStr = a.Request.Headers["If-Modified-Since"];
                    if ((IfModifiedSinceStr != null) && (LastWriteTimeStr == IfModifiedSinceStr))
                    {
                        a.Response.StatusCode = 304;
                        OnSuccess();
                        return;
                    }

                    var oRange = Optional<KeyValuePair<Int64, Int64>>.Empty;
                    var IfRangeStr = a.Request.Headers["If-Range"];
                    if ((IfRangeStr == null) || (ETag == IfRangeStr) || (LastWriteTimeStr == IfRangeStr))
                    {
                        var RangeStrs = a.Request.Headers["Range"];
                        if (RangeStrs != null)
                        {
                            oRange = TryGetRange(RangeStrs, fs.Length);
                        }
                    }

                    Int64 Start;
                    Int64 Length;
                    if (oRange.OnSome)
                    {
                        var Range = oRange.Value;
                        Start = Range.Key;
                        Length = Range.Value;
                        a.Response.StatusCode = 206;
                        a.Response.Headers.Add("Content-Range", "bytes {0}-{1}/{2}".Formats(Range.Key, Range.Key + Range.Value - 1, fs.Length));
                    }
                    else
                    {
                        Start = 0;
                        Length = fs.Length;
                        a.Response.StatusCode = 200;
                    }

                    a.Response.ContentLength64 = Length;
                    var Extension = FileNameHandling.GetExtendedFileName(Path);
                    a.Response.ContentType = GetMimeType(Extension);
                    if (Extension.Equals("txt", StringComparison.OrdinalIgnoreCase))
                    {
                        a.Response.ContentEncoding = System.Text.Encoding.UTF8;
                    }
                    a.Response.Headers.Add("Accept-Ranges", "bytes");
                    a.Response.Headers.Add("Last-Modified", LastWriteTimeStr);
                    a.Response.Headers.Add("ETag", ETag);
                    try
                    {
                        using (var ros = a.Response.OutputStream)
                        {
                            var Count = (Length + Buffer.Length - 1) / Buffer.Length;
                            fs.Position = Start;
                            for (int k = 0; k < Count; k += 1)
                            {
                                var ChunkSize = (int)(Math.Min(Buffer.Length, Length - Buffer.Length * k));
                                fs.Read(Buffer, 0, ChunkSize);
                                ros.Write(Buffer, 0, ChunkSize);
                            }
                        }
                    }
                    catch (System.Net.HttpListenerException)
                    {
                    }
                }

                OnSuccess();
            };

            Inner = new ExternalHttpServer(ServerContext, RequestHandler, QueueUserWorkItem, ReadBufferSize);
            Inner.Bindings = Bindings;
            Inner.SessionIdleTimeout = SessionIdleTimeout;
            Inner.MaxConnections = MaxConnections;
            Inner.MaxConnectionsPerIP = MaxConnectionsPerIP;
            Inner.MaxBadCommands = MaxBadCommands;
            Inner.MaxUnauthenticatedPerIP = MaxUnauthenticatedPerIP;
            Inner.ServiceVirtualPath = ServiceVirtualPath;

            Inner.Start();
        }

        public void Stop()
        {
            Inner.Stop();
        }

        public void Dispose()
        {
            Stop();
        }
    }
}
