﻿using System;
using System.Linq;

using Raven.Client.Documents.Indexes;

namespace BitShuva.Chavah.Models.Indexes
{
    /// <summary>
    /// Raven index for searching through a user's likes. Used from the /mylikes page.
    /// </summary>
    public class Likes_SongSearch : AbstractIndexCreationTask<Like, Likes_SongSearch.Result>
    {
        public Likes_SongSearch()
        {
            Map = likes => from like in likes
                           where like.Status == LikeStatus.Like
                           let song = LoadDocument<Song>(like.SongId)
                           let album = LoadDocument<Album>(song.AlbumId)
                           select new Result
                           {
                               SongId = like.SongId,
                               UserId = like.UserId,
                               Date = like.Date,
                               Name = song.Name,
                               Artist = song.Artist,
                               Album = song.Album,
                               HebrewName = song.HebrewName
                           };

            Index(r => r.Name, FieldIndexing.Search);
            Index(r => r.Artist, FieldIndexing.Search);
            Index(r => r.Album, FieldIndexing.Search);
            Index(r => r.HebrewName, FieldIndexing.Search);
        }

        public class Result
        {
            public DateTime Date { get; set; }
            public string UserId { get; set; } = string.Empty;
            public string SongId { get; set; } = string.Empty;
            public string Name { get; set; } = string.Empty;
            public string Artist { get; set; } = string.Empty;
            public string Album { get; set; } = string.Empty;
            public string HebrewName { get; set; } = string.Empty;
        }
    }
}
