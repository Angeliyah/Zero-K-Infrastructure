﻿using System;
using System.Linq;
using LobbyClient;
using PlasmaShared;
using ZkData;

namespace ZkLobbyServer {
    public class NewsListManager
    {
        private ZkLobbyServer server;
        private NewsList cachedNewsList;

        public NewsListManager(ZkLobbyServer zkLobbyServer)
        {
            server = zkLobbyServer;
            CacheNewsList();
        }

        private void CacheNewsList()
        {
            using (var db = new ZkDataContext())
            {
                cachedNewsList = new NewsList()
                {
                    NewsItems = db.News.OrderByDescending(x => x.Created).Take(10).ToList().Select(x => new NewsItem
                    {
                        Time = x.Created,
                        Header = x.Title,
                        Text = x.LobbyPlaintext,
                        Image = x.ThumbRelativeUrl,
                        Url = $"{GlobalConst.BaseSiteUrl}/Forum/Thread/{x.ForumThreadID}" // not very nice hardcode..
                    }).ToList()
                };
            }
        }

        public NewsList GetCurrentNewsList() => cachedNewsList;

        public void OnNewsChanged()
        {
            CacheNewsList();
            server.Broadcast(cachedNewsList);
        }
    }
}