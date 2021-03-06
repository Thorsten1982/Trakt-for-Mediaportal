﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using TraktPlugin.TraktAPI.DataStructures;
using TraktPlugin.TraktHandlers;
using TraktPlugin.GUI;

namespace TraktPlugin.TraktAPI
{
    #region Enumerables
    /// <summary>
    /// List of Scrobble States
    /// </summary>
    public enum TraktScrobbleStates
    {
        watching,
        scrobble,
        cancelwatching
    }

    /// <summary>
    /// List of Sync Modes
    /// </summary>
    public enum TraktSyncModes
    {
        library,
        seen,
        unlibrary,
        unseen,
        watchlist,
        unwatchlist
    }

    /// <summary>
    /// List of Clearing Modes
    /// </summary>
    public enum TraktClearingModes
    {
        all,
        movies,
        episodes
    }

    /// <summary>
    /// List of Rate Types
    /// </summary>
    public enum TraktRateType
    {
        episode,
        show,
        movie
    }

    /// <summary>
    /// List of Rate Values
    /// </summary>
    public enum TraktRateValue
    {
        love,
        hate
    }

    public enum ConnectionState
    {
        Connected,
        Connecting,
        Disconnected,
        Invalid,
        Pending
    }

    #endregion

    /// <summary>
    /// Object that communicates with the Trakt API
    /// </summary>
    public class TraktAPI
    {
        #region Scrobbling

        /// <summary>
        /// Sends Scrobble data to Trakt
        /// </summary>
        /// <param name="scrobbleData">The Data to send</param>
        /// <param name="status">The mode to send it as</param>
        /// <returns>The response from Trakt</returns>
        public static TraktResponse ScrobbleMovieState(TraktMovieScrobble scrobbleData, TraktScrobbleStates status)
        {
            //If we are cancelling a scrobble we don't need data
            if (status != TraktScrobbleStates.cancelwatching)
            {
                // check that we have everything we need
                // server can accept title if movie id is not supplied
                if (scrobbleData == null)
                {
                    TraktResponse error = new TraktResponse
                    {
                        Error = "Not enough information to send to server",
                        Status = "failure"
                    };
                    return error;
                }
            }
            TraktLogger.Info(string.Format("{0} '{1}'", status, scrobbleData.Title));

            // serialize Scrobble object to JSON and send to server
            string response = Transmit(string.Format(TraktURIs.ScrobbleMovie, status.ToString()), scrobbleData.ToJSON());

            // return success or failure
            return response.FromJSON<TraktResponse>();
        }

        /// <summary>
        /// Sends Scrobble data to Trakt
        /// </summary>
        /// <param name="scrobbleData">The Data to send</param>
        /// <param name="status">The mode to send it as</param>
        /// <returns>The response from Trakt</returns>
        public static TraktResponse ScrobbleEpisodeState(TraktEpisodeScrobble scrobbleData, TraktScrobbleStates status)
        {
            // check that we have everything we need
            // server can accept title if movie id is not supplied
            if (status != TraktScrobbleStates.cancelwatching)
            {
                if (scrobbleData == null)
                {
                    TraktResponse error = new TraktResponse
                    {
                        Error = "Not enough information to send to server",
                        Status = "failure"
                    };
                    return error;
                }
                TraktLogger.Info(string.Format("{0} '{1}'", status, scrobbleData.Title));
            }
            
            // serialize Scrobble object to JSON and send to server
            string response = Transmit(string.Format(TraktURIs.ScrobbleShow, status.ToString()), scrobbleData.ToJSON());

            // return success or failure
            return response.FromJSON<TraktResponse>();
        }

        #endregion

        #region Syncing

        /// <summary>
        /// Sends movie sync data to Trakt
        /// </summary>
        /// <param name="syncData">The sync data to send</param>
        /// <param name="mode">The sync mode to use</param>
        /// <returns>The response from trakt</returns>
        public static TraktMovieSyncResponse SyncMovieLibrary(TraktMovieSync syncData, TraktSyncModes mode)
        {
            // check that we have everything we need
            // server can accept title/year if imdb id is not supplied
            if (syncData == null || syncData.MovieList.Count == 0)
            {
                TraktMovieSyncResponse error = new TraktMovieSyncResponse
                {
                    Error = "Not enough information to send to server",
                    Status = "failure"
                };
                return error;
            }

            // serialize Scrobble object to JSON and send to server
            string response = Transmit(string.Format(TraktURIs.SyncMovieLibrary, mode.ToString()), syncData.ToJSON());

            // Log how many movies were inserted, skipped, already exist and movie list of failures
            TraktLogger.Debug("Response: {0}", response);

            // return success or failure
            return response.FromJSON<TraktMovieSyncResponse>();
        }

        /// <summary>
        /// Add/Remove show to/from watchlist
        /// </summary>
        /// <param name="syncData">The sync data to send</param>
        /// <param name="mode">The sync mode to use</param>
        /// <returns>The response from trakt</returns>
        public static TraktResponse SyncShowWatchList(TraktShowSync syncData, TraktSyncModes mode)
        {
            // check that we have everything we need            
            if (syncData == null || syncData.Shows.Count == 0)
            {
                TraktResponse error = new TraktResponse
                {
                    Error = "Not enough information to send to server",
                    Status = "failure"
                };
                return error;
            }

            // serialize Scrobble object to JSON and send to server
            string response = Transmit(string.Format(TraktURIs.SyncShowWatchList, mode.ToString()), syncData.ToJSON());           
            TraktLogger.Debug("Response: {0}", response);

            // return success or failure
            return response.FromJSON<TraktResponse>();
        }

        /// <summary>
        /// Add/Remove episode to/from watchlist
        /// </summary>
        /// <param name="syncData">The sync data to send</param>
        /// <param name="mode">The sync mode to use</param>
        /// <returns>The response from trakt</returns>
        public static TraktResponse SyncEpisodeWatchList(TraktEpisodeSync syncData, TraktSyncModes mode)
        {
            // check that we have everything we need            
            if (syncData == null || syncData.EpisodeList.Count == 0)
            {
                TraktResponse error = new TraktResponse
                {
                    Error = "Not enough information to send to server",
                    Status = "failure"
                };
                return error;
            }

            // serialize Scrobble object to JSON and send to server
            string response = Transmit(string.Format(TraktURIs.SyncEpisodeWatchList, mode.ToString()), syncData.ToJSON());
            TraktLogger.Debug("Response: {0}", response);

            // return success or failure
            return response.FromJSON<TraktResponse>();
        }

        /// <summary>
        /// Sends episode sync data to Trakt
        /// </summary>
        /// <param name="syncData">The sync data to send</param>
        /// <param name="mode">The sync mode to use</param>
        public static TraktResponse SyncEpisodeLibrary(TraktEpisodeSync syncData, TraktSyncModes mode)
        {
            // check that we have everything we need
            // server can accept title/year if imdb id is not supplied
            if (syncData == null || string.IsNullOrEmpty(syncData.SeriesID))
            {
                TraktResponse error = new TraktResponse
                {
                    Error = "Not enough information to send to server",
                    Status = "failure"
                };
                return error;
            }

            // serialize Scrobble object to JSON and send to server
            string response = Transmit(string.Format(TraktURIs.SyncEpisodeLibrary, mode.ToString()), syncData.ToJSON());

            // return success or failure
            return response.FromJSON<TraktResponse>();
        }

        #endregion

        #region Trakt Library Calls

        /// <summary>
        /// Gets the trakt movie library for a user
        /// </summary>
        /// <param name="user">The user to get</param>
        /// <returns>The trakt movie library</returns>
        public static IEnumerable<TraktLibraryMovies> GetMovieCollectionForUser(string user)
        {
            TraktLogger.Info("Getting user {0}'s movie collection from trakt", user);
            //Get the library
            string moviesForUser = Transmit(string.Format(TraktURIs.UserMoviesCollection, user), GetUserAuthentication());
            TraktLogger.Debug("Response: {0}", moviesForUser);
            //hand it on
            // if we timeout we will return an error response
            TraktResponse response = moviesForUser.FromJSON<TraktResponse>();
            if (response.Error != null) return null;
            return moviesForUser.FromJSONArray<TraktLibraryMovies>();
        }

        /// <summary>
        /// Gets all movies for a user from trakt, including movies not in collection
        /// </summary>
        /// <param name="user">The user to get</param>
        /// <returns>The trakt movie library</returns>
        public static IEnumerable<TraktLibraryMovies> GetAllMoviesForUser(string user)
        {
            TraktLogger.Info("Getting user {0}'s movies from trakt", user);
            //Get the library
            string moviesForUser = Transmit(string.Format(TraktURIs.UserMoviesAll, user), GetUserAuthentication());
            TraktLogger.Debug("Response: {0}", moviesForUser);
            //hand it on
            // if we timeout we will return an error response
            TraktResponse response = moviesForUser.FromJSON<TraktResponse>();
            if (response.Error != null) return null;
            return moviesForUser.FromJSONArray<TraktLibraryMovies>();
        }

        /// <summary>
        /// Gets the trakt episode library for a user
        /// </summary>
        /// <param name="user">The user to get</param>
        /// <returns>The trakt episode library</returns>
        public static IEnumerable<TraktLibraryShow> GetLibraryEpisodesForUser(string user)
        {
            TraktLogger.Info("Getting user {0}'s 'library' episodes from trakt", user);
            string showsForUser = Transmit(string.Format(TraktURIs.UserEpisodesCollection, user), GetUserAuthentication());
            TraktLogger.Debug("Response: {0}", showsForUser);            
            // if we timeout we will return an error response
            TraktResponse response = showsForUser.FromJSON<TraktResponse>();
            if (response.Error != null) return null;
            return showsForUser.FromJSONArray<TraktLibraryShow>();
        }

        /// <summary>
        /// Gets the trakt watched/seen episodes for a user
        /// </summary>
        /// <param name="user">The user to get</param>
        /// <returns>The trakt episode library</returns>
        public static IEnumerable<TraktLibraryShow> GetWatchedEpisodesForUser(string user)
        {
            TraktLogger.Info("Getting user {0}'s 'watched/seen' episodes from trakt", user);
            string showsForUser = Transmit(string.Format(TraktURIs.UserWatchedEpisodes, user), GetUserAuthentication());
            TraktLogger.Debug("Response: {0}", showsForUser);
            // if we timeout we will return an error response
            TraktResponse response = showsForUser.FromJSON<TraktResponse>();
            if (response.Error != null) return null;
            return showsForUser.FromJSONArray<TraktLibraryShow>();
        }

        public static IEnumerable<TraktLibraryShow> GetUnSeenEpisodesForUser(string user)
        {
            TraktLogger.Info("Getting user {0}'s 'unseen' episodes from trakt", user);
            string showsForUser = Transmit(string.Format(TraktURIs.UserEpisodesUnSeen, user), GetUserAuthentication());
            TraktLogger.Debug("Response: {0}", showsForUser);
            // if we timeout we will return an error response
            TraktResponse response = showsForUser.FromJSON<TraktResponse>();
            if (response.Error != null) return null;
            return showsForUser.FromJSONArray<TraktLibraryShow>();
        }

        #endregion

        #region Rating

        /// <summary>
        /// Sends episode rate data to Trakt
        /// </summary>
        /// <param name="episode">The Trakt rate data to send</param>
        /// <returns>The response from Trakt</returns>
        public static TraktRateResponse RateEpisode(TraktRateEpisode episode)
        {
            if (episode == null) return null;
            string response = Transmit(string.Format(TraktURIs.RateItem, TraktRateType.episode.ToString()), episode.ToJSON());
            return response.FromJSON<TraktRateResponse>();
        }

        /// <summary>
        /// Sends series rate data to Trakt
        /// </summary>
        /// <param name="episode">The Trakt rate data to send</param>
        /// <returns>The response from Trakt</returns>
        public static TraktRateResponse RateSeries(TraktRateSeries series)
        {
            if (series == null) return null;
            string response = Transmit(string.Format(TraktURIs.RateItem, TraktRateType.show.ToString()), series.ToJSON());
            return response.FromJSON<TraktRateResponse>();
        }

        /// <summary>
        /// Sends movie rate data to Trakt
        /// </summary>
        /// <param name="episode">The Trakt rate data to send</param>
        /// <returns>The response from Trakt</returns>
        public static TraktRateResponse RateMovie(TraktRateMovie movie)
        {
            string response = Transmit(string.Format(TraktURIs.RateItem, TraktRateType.movie.ToString()), movie.ToJSON());
            return response.FromJSON<TraktRateResponse>();
        }

        #endregion

        #region User

        public static TraktUserProfile GetUserProfile(string user)
        {
            string response = Transmit(string.Format(TraktURIs.UserProfile, user), GetUserAuthentication());
            return response.FromJSON<TraktUserProfile>();
        }

        /// <summary>
        /// Returns a list of Friends and their user profiles
        /// </summary>
        /// <param name="user">username of person to retrieve friend s list</param>
        public static IEnumerable<GUIFriendItem> GetUserFriends(string user)
        {
            string response = Transmit(string.Format(TraktURIs.UserFriends, user), GetUserAuthentication());
            return response.FromJSONArray<GUIFriendItem>();
        }
        
        /// <summary>
        /// Returns list of episodes in Users Calendar
        /// </summary>
        /// <param name="user">username of person to get Calendar</param>
        public static IEnumerable<TraktCalendar> GetCalendarForUser(string user)
        {
            // 7-Days from Today
            // All Dates should be in PST (GMT-8)
            DateTime dateNow = DateTime.UtcNow.Subtract(new TimeSpan(8, 0, 0));
            return GetCalendarForUser(user, dateNow.ToString("yyyyMMdd"), "7");
        }

        /// <summary>
        /// Returns list of episodes in Users Calendar
        /// </summary>
        /// <param name="user">username of person to get Calendar</param>
        /// <param name="startDate">Start Date of calendar in form yyyyMMdd (GMT-8hrs)</param>
        /// <param name="days">Number of days to return in calendar</param>
        public static IEnumerable<TraktCalendar> GetCalendarForUser(string user, string startDate, string days)
        {
            string userCalendar = Transmit(string.Format(TraktURIs.UserCalendarShows, user, startDate, days), GetUserAuthentication());
            return userCalendar.FromJSONArray<TraktCalendar>();

        }

        public static IEnumerable<TraktCalendar> GetCalendarPremieres()
        {
            // 7-Days from Today
            // All Dates should be in PST (GMT-8)
            DateTime dateNow = DateTime.UtcNow.Subtract(new TimeSpan(8, 0, 0));
            return GetCalendarPremieres(dateNow.ToString("yyyyMMdd"), "7");

        }

        /// <summary>
        /// Returns list of episodes in the Premieres Calendar
        /// </summary>        
        /// <param name="startDate">Start Date of calendar in form yyyyMMdd (GMT-8hrs)</param>
        /// <param name="days">Number of days to return in calendar</param>
        public static IEnumerable<TraktCalendar> GetCalendarPremieres(string startDate, string days)
        {
            string premieres = Transmit(string.Format(TraktURIs.CalendarPremieres, startDate, days), GetUserAuthentication());
            return premieres.FromJSONArray<TraktCalendar>();
        }

        public static IEnumerable<TraktCalendar> GetCalendarShows()
        {
            DateTime dateNow = DateTime.UtcNow.Subtract(new TimeSpan(8, 0, 0));
            return GetCalendarShows(dateNow.ToString("yyyyMMdd"), "7");
        }

        public static IEnumerable<TraktCalendar> GetCalendarShows(string startDate, string days)
        {
            string premieres = Transmit(string.Format(TraktURIs.CalendarAllShows, startDate, days), GetUserAuthentication());
            return premieres.FromJSONArray<TraktCalendar>();
        }

        /// <summary>
        /// Returns list of the 100 last watched episodes by a user
        /// </summary>
        /// <param name="user">username of person to get watched history</param>        
        public static IEnumerable<TraktWatchedEpisode> GetUserEpisodeWatchedHistory(string user)
        {
            string watchedEpisodes = Transmit(string.Format(TraktURIs.UserEpisodeWatchedHistory, user), GetUserAuthentication());
            return watchedEpisodes.FromJSONArray<TraktWatchedEpisode>();
        }

        /// <summary>
        /// Returns list of the 100 last watched movies by a user
        /// </summary>
        /// <param name="user">username of person to get watched history</param>        
        public static IEnumerable<TraktWatchedMovie> GetUserMovieWatchedHistory(string user)
        {
            string watchedMovies = Transmit(string.Format(TraktURIs.UserMovieWatchedHistory, user), GetUserAuthentication());
            return watchedMovies.FromJSONArray<TraktWatchedMovie>();
        }

        #endregion

        #region Friends

        /// <summary>
        /// Returns a list of Friends for current user
        /// </summary>        
        public static IEnumerable<GUIFriendItem> GetFriends()
        {
            string response = Transmit(TraktURIs.Friends, GetUserAuthentication());
            return response.FromJSONArray<GUIFriendItem>();
        }

        /// <summary>
        /// Returns a list of Friend requests for current user
        /// </summary>        
        public static IEnumerable<GUIFriendItem> GetFriendRequests()
        {
            string response = Transmit(TraktURIs.FriendRequests, GetUserAuthentication());
            return response.FromJSONArray<GUIFriendItem>();
        }

        public static TraktResponse FriendApprove(TraktFriend friend)
        {
            string response = Transmit(TraktURIs.FriendApprove, friend.ToJSON());
            return response.FromJSON<TraktResponse>();
        }

        public static TraktResponse FriendAdd(TraktFriend friend)
        {
            string response = Transmit(TraktURIs.FriendAdd, friend.ToJSON());
            return response.FromJSON<TraktResponse>();
        }

        public static TraktResponse FriendDeny(TraktFriend friend)
        {
            string response = Transmit(TraktURIs.FriendDeny, friend.ToJSON());
            return response.FromJSON<TraktResponse>();
        }

        public static TraktResponse FriendDelete(TraktFriend friend)
        {
            string response = Transmit(TraktURIs.FriendDelete, friend.ToJSON());
            return response.FromJSON<TraktResponse>();
        }

        #endregion

        #region Trending

        public static IEnumerable<TraktTrendingMovie> GetTrendingMovies()
        {
            string response = Transmit(TraktURIs.TrendingMovies, GetUserAuthentication());
            return response.FromJSONArray<TraktTrendingMovie>();
        }

        public static IEnumerable<TraktTrendingShow> GetTrendingShows()
        {
            string response = Transmit(TraktURIs.TrendingShows, GetUserAuthentication());
            return response.FromJSONArray<TraktTrendingShow>();
        }

        #endregion

        #region Recommendations

        public static TraktResponse DismissMovieRecommendation(TraktMovieSlug movie)
        {
            string response = Transmit(TraktURIs.DismissMovieRecommendation, movie.ToJSON());
            return response.FromJSON<TraktResponse>();
        }

        public static TraktResponse DismissShowRecommendation(TraktShowSlug show)
        {
            string response = Transmit(TraktURIs.DismissShowRecommendation, show.ToJSON());
            return response.FromJSON<TraktResponse>();
        }


        public static IEnumerable<TraktMovie> GetRecommendedMovies()
        {
            string response = Transmit(TraktURIs.UserMovieRecommendations, GetUserAuthentication());
            return response.FromJSONArray<TraktMovie>();
        }

        public static IEnumerable<TraktShow> GetRecommendedShows()
        {
            string response = Transmit(TraktURIs.UserShowsRecommendations, GetUserAuthentication());
            return response.FromJSONArray<TraktShow>();
        }

        #endregion

        #region Watch List

        public static IEnumerable<TraktWatchListMovie> GetWatchListMovies(string user)
        {
            string response = Transmit(string.Format(TraktURIs.UserMovieWatchList, user), GetUserAuthentication());
            return response.FromJSONArray<TraktWatchListMovie>();
        }

        public static IEnumerable<TraktWatchListShow> GetWatchListShows(string user)
        {
            string response = Transmit(string.Format(TraktURIs.UserShowsWatchList, user), GetUserAuthentication());
            return response.FromJSONArray<TraktWatchListShow>();
        }

        public static IEnumerable<TraktWatchListEpisode> GetWatchListEpisodes(string user)
        {
            string response = Transmit(string.Format(TraktURIs.UserEpisodesWatchList, user), GetUserAuthentication());
            return response.FromJSONArray<TraktWatchListEpisode>();
        }

        #endregion

        #region Account

        public static TraktResponse CreateAccount(TraktAccount account)
        {
            string response = Transmit(TraktURIs.CreateAccount, account.ToJSON());
            return response.FromJSON<TraktResponse>();
        }

        public static TraktResponse TestAccount(TraktAccount account)
        {
            string response = Transmit(TraktURIs.TestAccount, account.ToJSON());
            return response.FromJSON<TraktResponse>();
        }

        #endregion

        #region Search

        /// <summary>
        /// Returns a list of users found using search term
        /// </summary>        
        public static IEnumerable<GUIFriendItem> SearchForFriends(string searchTerm)
        {
            string response = Transmit(string.Format(TraktURIs.SearchUsers, searchTerm), GetUserAuthentication());
            return response.FromJSONArray<GUIFriendItem>();
        }

        #endregion

        #region Shouts

        /// <summary>
        /// Return a list of shouts for a movie
        /// </summary>
        /// <param name="title">The movie search term, either (title-year seperate spaces with '-'), imdbid, tmdbid</param>    
        public static IEnumerable<TraktShout> GetMovieShouts(string title)
        {
            string response = Transmit(string.Format(TraktURIs.MovieShouts, title), GetUserAuthentication());
            return response.FromJSONArray<TraktShout>();
        }

        /// <summary>
        /// Return a list of shouts for a show
        /// </summary>
        /// <param name="title">The show search term, either (title seperate spaces with '-'), imdbid, tvdbid</param>    
        public static IEnumerable<TraktShout> GetShowShouts(string title)
        {
            string response = Transmit(string.Format(TraktURIs.ShowShouts, title), GetUserAuthentication());
            return response.FromJSONArray<TraktShout>();
        }

        /// <summary>
        /// Return a list of shouts for a episode
        /// </summary>
        /// <param name="title">The episode search term, either (title seperate spaces with '-'), imdbid, tmdbid</param>
        /// <param name="season">The episode index</param>
        /// <param name="indexc">The season index</param>
        public static IEnumerable<TraktShout> GetEpisodeShouts(string title, string season, string episode)
        {
            string response = Transmit(string.Format(TraktURIs.EpisodeShouts, title, season, episode), GetUserAuthentication());
            return response.FromJSONArray<TraktShout>();
        }

        #endregion

        #region Helpers

        /// <summary>
        /// Gets a User Authentication object
        /// </summary>       
        /// <returns>The User Authentication json string</returns>
        private static string GetUserAuthentication()
        {
            return new TraktAuthentication { Username = TraktSettings.Username, Password = TraktSettings.Password }.ToJSON();
        }

        /// <summary>
        /// Communicates to and from Trakt
        /// </summary>
        /// <param name="address">The URI to use</param>
        /// <param name="data">The Data to send</param>
        /// <returns>The response from Trakt</returns>
        private static string Transmit(string address, string data)
        {
            if (!string.IsNullOrEmpty(data))
            {
                TraktLogger.Debug("Post: {0} Address: {1}", data, address);
            }

            try
            {
                ServicePointManager.Expect100Continue = false;
                WebClient client = new WebClient();
                client.Encoding = Encoding.UTF8;
                client.Headers.Add("user-agent", TraktSettings.UserAgent);
                return client.UploadString(address, data);
            }
            catch (WebException e)
            {
                // something bad happened e.g. invalid login
                TraktResponse error = new TraktResponse
                {
                    Status = "failure",
                    Error = e.Message
                };
                return error.ToJSON();
            }
        }

        /// <summary>
        /// Clears our library on Trakt as best as the api lets us
        /// </summary>
        /// <param name="mode">What to remove from Trakt</param>
        internal static void ClearLibrary(TraktClearingModes mode, ProgressDialog progressDialog, bool clearSeen)
        {
            progressDialog.Title = "Clearing Library";
            progressDialog.CancelMessage = "Attempting to Cancel";
            progressDialog.Maximum = 100;
            progressDialog.Value = 0;
            progressDialog.Line1 = "Clearing your Trakt Library";
            progressDialog.ShowDialog(ProgressDialog.PROGDLG.Modal, ProgressDialog.PROGDLG.NoMinimize, ProgressDialog.PROGDLG.NoTime);

            //Movies
            if (mode == TraktClearingModes.all || mode == TraktClearingModes.movies)
            {
                TraktLogger.Info("Removing Movies from Trakt");
                TraktLogger.Info("NOTE: WILL NOT REMOVE SCROBBLED MOVIES DUE TO API LIMITATION");
                progressDialog.Line2 = "Getting movies for user";
                List<TraktLibraryMovies> movies = GetAllMoviesForUser(TraktSettings.Username).ToList();

                var syncData = BasicHandler.CreateMovieSyncData(movies);
                TraktResponse response = null;

                if (clearSeen)
                {
                    TraktLogger.Info("First removing movies from seen");
                    progressDialog.Line2 = "Setting seen movies as unseen";
                    response = SyncMovieLibrary(syncData, TraktSyncModes.unseen);
                    LogTraktResponse(response);
                }

                TraktLogger.Info("Now removing movies from library");
                progressDialog.Line2 = "Removing movies from library";
                response = SyncMovieLibrary(syncData, TraktSyncModes.unlibrary);
                LogTraktResponse(response);

                TraktLogger.Info("Removed all movies possible, some manual clean up may be required");
            }
            if(mode == TraktClearingModes.all)
                progressDialog.Value = 15;
            if (progressDialog.HasUserCancelled)
            {
                TraktLogger.Info("Cancelling Library Clearing");
                progressDialog.CloseDialog();
                return;
            }
            //Episodes
            if (mode == TraktClearingModes.all || mode == TraktClearingModes.episodes)
            {
                TraktLogger.Info("Removing Shows from Trakt");
                TraktLogger.Info("NOTE: WILL NOT REMOVE SCROBBLED SHOWS DUE TO API LIMITATION");

                if (clearSeen)
                {
                    TraktLogger.Info("First removing shows from seen");
                    progressDialog.Line2 = "Getting Watched Episodes from Trakt";
                    var watchedEpisodes = GetWatchedEpisodesForUser(TraktSettings.Username);
                    if (watchedEpisodes != null)
                    {
                        foreach (var series in watchedEpisodes.ToList())
                        {
                            TraktLogger.Info("Removing '{0}' from seen", series.ToString());
                            progressDialog.Line2 = string.Format("Setting {0} as unseen", series.ToString());
                            TraktResponse response = SyncEpisodeLibrary(BasicHandler.CreateEpisodeSyncData(series), TraktSyncModes.unseen);
                            LogTraktResponse(response);
                            System.Threading.Thread.Sleep(500);
                            if (progressDialog.HasUserCancelled)
                            {
                                TraktLogger.Info("Cancelling Library Clearing");
                                progressDialog.CloseDialog();
                                return;
                            }
                        }
                    }
                }
                progressDialog.Value = 85;
                TraktLogger.Info("Now removing shows from library");
                progressDialog.Line2 = "Getting Library Episodes from Trakt";
                var libraryEpisodes = GetLibraryEpisodesForUser(TraktSettings.Username);
                if (libraryEpisodes != null)
                {
                    foreach (var series in libraryEpisodes.ToList())
                    {
                        TraktLogger.Info("Removing '{0}' from library", series.ToString());
                        progressDialog.Line2 = string.Format("Removing {0} from library", series.ToString());
                        TraktResponse response = SyncEpisodeLibrary(BasicHandler.CreateEpisodeSyncData(series), TraktSyncModes.unlibrary);
                        LogTraktResponse(response);
                        System.Threading.Thread.Sleep(500);
                        if (progressDialog.HasUserCancelled)
                        {
                            TraktLogger.Info("Cancelling Library Clearing");
                            progressDialog.CloseDialog();
                            return;
                        }
                    }
                }
                TraktLogger.Info("Removed all shows possible, some manual clean up may be required");
            }
            progressDialog.Value = 100;
            progressDialog.CloseDialog();
        }

        /// <summary>
        /// Logs the result of Trakt api call
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="response"></param>
        public static void LogTraktResponse<T>(T response)
        {
            try
            {
                if (response == null || (response as TraktResponse).Status == null)
                {
                    TraktLogger.Error("Response from server was unexpected.");
                    return;
                }
                // check response error status
                if ((response as TraktResponse).Status != "success")
                {
                    if ((response as TraktResponse).Error == "The remote server returned an error: (401) Unauthorized.")
                    {
                        // handle unauthorized (GUI notification)
                        //GUIUtils.ShowNotifyDialog(GUIUtils.PluginName(), Translation.UnAuthorized);
                        // log it
                        TraktLogger.Error("401 Unauthorized, Please check your Username and Password");
                    }else
                        TraktLogger.Error((response as TraktResponse).Error);
                }
                else
                {
                    // success
                    if (!string.IsNullOrEmpty((response as TraktResponse).Message))
                    {
                        TraktLogger.Info("Response: {0}", (response as TraktResponse).Message);
                    }
                    else
                    {
                        // no message returned on movie sync success
                        if ((response is TraktMovieSyncResponse))
                        {
                            string message = "Response: Movies Inserted: {0}, Movies Already Exist: {1}, Movies Skipped: {2}";
                            TraktLogger.Info(message, (response as TraktMovieSyncResponse).Inserted, (response as TraktMovieSyncResponse).AlreadyExist, (response as TraktMovieSyncResponse).Skipped);
                        }
                    }
                }
            }
            catch (Exception)
            {
                TraktLogger.Info("Response: {0}", "Failed to interpret response from server");
            }
        }

        #endregion
    }
}
