﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TraktPlugin.TraktAPI
{
    /// <summary>
    /// List of URIs for the Trakt API
    /// </summary>
    public static class TraktURIs
    {
        public const string ApiKey = "0edee4275d03fe72117e3f69a28815939b082548";
        public const string ScrobbleShow = @"http://api.trakt.tv/show/{0}/" + ApiKey;
        public const string ScrobbleMovie = @"http://api.trakt.tv/movie/{0}/" + ApiKey;
        public const string UserWatchedEpisodes = @"http://api.trakt.tv/user/library/shows/watched.json/" + ApiKey + @"/{0}";
        public const string UserWatchedMovies = @"http://api.trakt.tv/user/watched/movies.json/" + ApiKey + @"/{0}";
        public const string UserProfile = @"http://api.trakt.tv/user/profile.json/" + ApiKey + @"/{0}";
        public const string SeriesOverview = @"http://api.trakt.tv/show/summary.json/" + ApiKey + @"/{0}";
        public const string UserEpisodesUnSeen = @"http://api.trakt.tv/user/library/shows/unseen.json/" + ApiKey + @"/{0}";
        public const string UserEpisodesCollection = @"http://api.trakt.tv/user/library/shows/collection.json/" + ApiKey + @"/{0}";
        public const string UserMoviesCollection = @"http://api.trakt.tv/user/library/movies/collection.json/" + ApiKey + @"/{0}";
        public const string UserMoviesAll = @"http://api.trakt.tv/user/library/movies/all.json/" + ApiKey + @"/{0}";
        public const string SyncEpisodeLibrary = @"http://api.trakt.tv/show/episode/{0}/" + ApiKey;
        public const string SyncShowWatchList = @"http://api.trakt.tv/show/{0}/" + ApiKey;
        public const string SyncEpisodeWatchList = @"http://api.trakt.tv/show/episode/{0}/" + ApiKey;
        public const string SyncMovieLibrary = @"http://api.trakt.tv/movie/{0}/" + ApiKey;
        public const string SyncMovieWatchList = @"http://api.trakt.tv/movie/watchlist/" + ApiKey;
        public const string UserCalendarShows = @"http://api.trakt.tv/user/calendar/shows.json/" + ApiKey + @"/{0}/{1}/{2}";
        public const string CalendarPremieres = @"http://api.trakt.tv/calendar/premieres.json/" + ApiKey + @"/{0}/{1}";
        public const string CalendarAllShows = @"http://api.trakt.tv/calendar/shows.json/" + ApiKey + @"/{0}/{1}";
        public const string UserFriends = @"http://api.trakt.tv/user/friends.json/" + ApiKey + @"/{0}";
        public const string UserFriendsExtended = @"http://api.trakt.tv/user/friends.json/" + ApiKey + @"/{0}/extended";
        public const string RateItem = @"http://api.trakt.tv/rate/{0}/" + ApiKey;
        public const string TrendingMovies = @"http://api.trakt.tv/movies/trending.json/" + ApiKey;
        public const string TrendingShows = @"http://api.trakt.tv/shows/trending.json/" + ApiKey;
        public const string UserMovieRecommendations = @"http://api.trakt.tv/recommendations/movies/" + ApiKey;
        public const string UserShowsRecommendations = @"http://api.trakt.tv/recommendations/shows/" + ApiKey;
        public const string UserMovieWatchList = @"http://api.trakt.tv/user/watchlist/movies.json/" + ApiKey + @"/{0}";
        public const string UserShowsWatchList = @"http://api.trakt.tv/user/watchlist/shows.json/" + ApiKey + @"/{0}";
        public const string UserEpisodesWatchList = @"http://api.trakt.tv/user/watchlist/episodes.json/" + ApiKey + @"/{0}";
        public const string CreateAccount = @"http://api.trakt.tv/account/create/" + ApiKey;
        public const string TestAccount = @"http://api.trakt.tv/account/test/" + ApiKey;
        public const string UserEpisodeWatchedHistory = @"http://api.trakt.tv/user/watched/episodes.json/" + ApiKey + @"/{0}";
        public const string UserMovieWatchedHistory = @"http://api.trakt.tv/user/watched/movies.json/" + ApiKey + @"/{0}";
        public const string Friends = @"http://api.trakt.tv/friends/all/" + ApiKey;
        public const string FriendRequests = @"http://api.trakt.tv/friends/requests/" + ApiKey;
        public const string FriendAdd = @"http://api.trakt.tv/friends/add/" + ApiKey;
        public const string FriendApprove = @"http://api.trakt.tv/friends/approve/" + ApiKey;
        public const string FriendDeny = @"http://api.trakt.tv/friends/deny/" + ApiKey;
        public const string FriendDelete = @"http://api.trakt.tv/friends/delete/" + ApiKey;
        public const string SearchUsers = @"http://api.trakt.tv/search/users.json/" + ApiKey + @"/{0}";
        public const string MovieShouts = @"http://api.trakt.tv/movie/shouts.json/" + ApiKey + @"/{0}";
        public const string ShowShouts = @"http://api.trakt.tv/show/shouts.json/" + ApiKey + @"/{0}";
        public const string EpisodeShouts = @"http://api.trakt.tv/show/episode/shouts.json/" + ApiKey + @"/{0}/{1}/{2}";
        public const string DismissMovieRecommendation = @"http://api.trakt.tv/recommendations/movies/dismiss/" + ApiKey;
        public const string DismissShowRecommendation = @"http://api.trakt.tv/recommendations/shows/dismiss/" + ApiKey;
    }
}
