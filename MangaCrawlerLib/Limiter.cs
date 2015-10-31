using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Diagnostics;

namespace MangaCrawlerLib
{
    internal static class Limiter
    {
        #region Priority
        internal enum Priority
        {
            Chapters = 1,
            Series = 2,
            Pages = 3,
            Image = 4,
            Miniature = 5
        }
        #endregion

        #region Limit
        private class Limit : IDisposable
        {
            public readonly Priority Priority;
            public readonly AutoResetEvent Event;
            public readonly Server Server;
            public readonly ulong LimiterOrder;

            public Limit(Priority a_priority, Server server, ulong limiterOrder)
            {
                Event = new AutoResetEvent(false);
                Priority = a_priority;
                Server = server;
                LimiterOrder = limiterOrder;
            }

            public override string ToString()
            {
                return $"Server: {Server.ID}, Priority: {Priority}, LimitOrder: {LimiterOrder}";
            }

            public void Dispose()
            {
                Event.Dispose();
            }
        }
        #endregion

        private static readonly List<Limit> Limits = new List<Limit>();
        private static readonly AutoResetEvent LoopEvent = new AutoResetEvent(true);

        private const int LoopSleepMs = 500;
        private const int WaitSleepMs = 500;

        private static readonly Dictionary<Server, int> ServerConnections = new Dictionary<Server, int>();
        private static readonly Dictionary<Server, bool> OneChapterPerServer = new Dictionary<Server, bool>();
        private static int _connections;

        static Limiter()
        {
            foreach (var server in DownloadManager.Instance.Servers)
            {
                ServerConnections[server] = 0;
                OneChapterPerServer[server] = false;
            }

            var loopThread = new Thread(Loop)
            {
                Name = "Limiter",
                IsBackground = true
            };
            loopThread.Start();

        }

        public static void BeginChapter(Chapter chapter)
        {
            Aquire(chapter.Server, chapter.Token, Priority.Pages, chapter.LimiterOrder);
        }

        public static void AquireMiniature(Serie serie)
        {
            Aquire(serie.Server, CancellationToken.None, Priority.Miniature, serie.LimiterOrder);
        }

        public static void Aquire(Server server)
        {
            Aquire(server, CancellationToken.None, Priority.Series, server.LimiterOrder);
        }

        public static void Aquire(Serie serie)
        {
            Aquire(serie.Server, CancellationToken.None, Priority.Chapters, serie.LimiterOrder);
        }

        public static void Aquire(Chapter chapter)
        {
            Aquire(chapter.Server, chapter.Token, Priority.Image, chapter.LimiterOrder);
        }

        public static void Aquire(Page page)
        {
            Aquire(page.Server, page.Chapter.Token, Priority.Image, page.LimiterOrder);
        }

        private static void Aquire(Server server, CancellationToken cancellationToken, Priority priority, ulong limiterOrder)
        {
            using (var limit = new Limit(priority, server, limiterOrder))
            {
                lock (Limits)
                {
                    Limits.Add(limit);
                }
                LoopEvent.Set();

                while (!limit.Event.WaitOne(WaitSleepMs))
                {
                    lock (Limits)
                    {
                        if (!cancellationToken.IsCancellationRequested) continue;
                        Limits.Remove(limit);
                        cancellationToken.ThrowIfCancellationRequested();
                    }
                }
            }
        }

        private static void Loop()
        {
            for (; ; )
            {
                LoopEvent.WaitOne(LoopSleepMs);

                lock (Limits)
                {
                    for (; ; )
                    {
                        var limit = GetLimit();

                        if (limit == null)
                        {
                            break;
                        }
                        if (limit.Priority == Priority.Pages)
                        {
                            Debug.Assert(!OneChapterPerServer[limit.Server]);
                            OneChapterPerServer[limit.Server] = true;
                        }
                        else
                        {
                            if (limit.Priority == Priority.Image)
                                Debug.Assert(OneChapterPerServer[limit.Server]);
                                
                            _connections++;
                            ServerConnections[limit.Server]++;

                            Debug.Assert(_connections <=
                                         DownloadManager.Instance.MangaSettings.MaximumConnections);
                            Debug.Assert(ServerConnections[limit.Server] <=
                                         limit.Server.Crawler.MaxConnectionsPerServer);
                        }

                        limit.Event.Set();
                    }
                }
            }
        }

        public static void EndChapter(Chapter chapter)
        {
            lock (Limits)
            {
                Debug.Assert(OneChapterPerServer[chapter.Server]);
                OneChapterPerServer[chapter.Server] = false;
            }

            LoopEvent.Set();
        }

        public static void Release(Serie serie)
        {
            Release(serie.Server);
        }

        public static void Release(Page page)
        {
            Release(page.Server);
        }

        public static void Release(Chapter chapter)
        {
            Release(chapter.Server);
        }

        public static void Release(Server server)   
        {
            lock (Limits)
            {
                _connections--;
                ServerConnections[server]--;

                Debug.Assert(_connections >= 0);
                Debug.Assert(ServerConnections[server] >= 0);
            }

            LoopEvent.Set();
        }

        private static Limit GetLimit()
        {
            Limit candidate = null;

            var candidates1 = from limit in Limits
                              where limit.Priority != Priority.Pages
                              where ServerConnections[limit.Server] <
                                 limit.Server.Crawler.MaxConnectionsPerServer
                              orderby limit.Priority, limit.LimiterOrder
                              select limit;

            var candidates2 = from limit in Limits
                              where limit.Priority == Priority.Pages
                              where !OneChapterPerServer[limit.Server]
                              orderby limit.LimiterOrder
                              select limit;

            if (_connections < DownloadManager.Instance.MangaSettings.MaximumConnections)
                candidate = candidates1.FirstOrDefault();

            if (candidate == null)
                candidate = candidates2.FirstOrDefault();

            if (candidate != null)
                Limits.Remove(candidate);

            return candidate;
        }
    }
}
