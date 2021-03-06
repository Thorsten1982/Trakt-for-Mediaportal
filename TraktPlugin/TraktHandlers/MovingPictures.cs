﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Cornerstone.Database.CustomTypes;
using MediaPortal.Plugins.MovingPictures;
using MediaPortal.Plugins.MovingPictures.Database;
using MediaPortal.Plugins.MovingPictures.MainUI;
using TraktPlugin.TraktAPI;
using TraktPlugin.TraktAPI.DataStructures;
using System.Timers;
using MediaPortal.Player;
using System.Reflection;
using System.ComponentModel;
using Cornerstone.Database.Tables;

namespace TraktPlugin.TraktHandlers
{
    /// <summary>
    /// Support for MovingPictures
    /// </summary>
    class MovingPictures : ITraktHandler
    {
        Timer traktTimer;
        DBMovieInfo currentMovie;
        bool SyncInProgress;
        public static MoviePlayer player = null;
        private static IEnumerable<TraktMovie> recommendations;
        private static IEnumerable<TraktWatchListMovie> watchList;
        private static DateTime recommendationsAndWatchListAge;

        public static DBSourceInfo tmdbSource;

        public MovingPictures(int priority)
        {
            Priority = priority;
            TraktLogger.Debug("Adding Hooks to Moving Pictures Database");
            MovingPicturesCore.DatabaseManager.ObjectInserted += new Cornerstone.Database.DatabaseManager.ObjectAffectedDelegate(DatabaseManager_ObjectInserted);
            MovingPicturesCore.DatabaseManager.ObjectUpdated += new Cornerstone.Database.DatabaseManager.ObjectAffectedDelegate(DatabaseManager_ObjectUpdated);
            MovingPicturesCore.DatabaseManager.ObjectDeleted += new Cornerstone.Database.DatabaseManager.ObjectAffectedDelegate(DatabaseManager_ObjectDeleted);
        }

        #region ITraktHandler

        public string Name { get { return "Moving Pictures"; } }
        public int Priority { get; set; }
        
        public void SyncLibrary()
        {
            TraktLogger.Info("Moving Pictures Starting Sync");
            SyncInProgress = true;

            //Get all movies in our local database
            List<DBMovieInfo> MovieList = DBMovieInfo.GetAll();

            // Get TMDb Data Provider
            tmdbSource = DBSourceInfo.GetAll().Find(s => s.ToString() == "themoviedb.org");

            //Remove any blocked movies
            MovieList.RemoveAll(movie => TraktSettings.BlockedFolders.Any(f => movie.LocalMedia[0].FullPath.ToLowerInvariant().Contains(f.ToLowerInvariant())));
            MovieList.RemoveAll(movie => TraktSettings.BlockedFilenames.Contains(movie.LocalMedia[0].FullPath));

            #region Skipped Movies Check
            // Remove Skipped Movies from previous Sync
            if (TraktSettings.SkippedMovies != null)
            {
                // allow movies to re-sync again after 7-days in the case user has addressed issue ie. edited movie or added to themoviedb.org
                if (TraktSettings.SkippedMovies.LastSkippedSync.FromEpoch() > DateTime.UtcNow.Subtract(new TimeSpan(7, 0, 0, 0)))
                {
                    if (TraktSettings.SkippedMovies.Movies != null)
                    {
                        TraktLogger.Info("Skipping {0} movies due to invalid data or movies don't exist on http://themoviedb.org. Next check will be {1}.", TraktSettings.SkippedMovies.Movies.Count, TraktSettings.SkippedMovies.LastSkippedSync.FromEpoch().Add(new TimeSpan(7, 0, 0, 0)));
                        foreach (var movie in TraktSettings.SkippedMovies.Movies)
                        {
                            TraktLogger.Info("Skipping movie, Title: {0}, Year: {1}, IMDb: {2}", movie.Title, movie.Year, movie.IMDBID);
                            MovieList.RemoveAll(m => (m.Title == movie.Title) && (m.Year.ToString() == movie.Year) && (m.ImdbID == movie.IMDBID));
                        }
                    }
                }
                else
                {
                    if (TraktSettings.SkippedMovies.Movies != null) TraktSettings.SkippedMovies.Movies.Clear();
                    TraktSettings.SkippedMovies.LastSkippedSync = DateTime.UtcNow.ToEpoch();
                }
            }
            #endregion

            TraktLogger.Info("{0} movies available to sync in MovingPictures database", MovieList.Count.ToString());

            //Get the movies that we have watched
            List<DBMovieInfo> SeenList = MovieList.Where(m => m.ActiveUserSettings.WatchedCount > 0).ToList();

            TraktLogger.Info("{0} watched movies available to sync in MovingPictures database", SeenList.Count.ToString());

            //Get all movies we have in our library including movies in users collection            
            IEnumerable<TraktLibraryMovies> traktMoviesAll = TraktAPI.TraktAPI.GetAllMoviesForUser(TraktSettings.Username);
            if (traktMoviesAll == null)
            {
                SyncInProgress = false;
                TraktLogger.Error("Error getting movies from trakt server, cancelling sync.");
                return;
            }
            TraktLogger.Info("{0} movies in trakt library", traktMoviesAll.Count().ToString());

            #region Movies to Sync to Collection
            //Filter out a list of movies we have already sync'd in our collection
            List<TraktLibraryMovies> NoLongerInOurCollection = new List<TraktLibraryMovies>();
            List<DBMovieInfo> moviesToSync = new List<DBMovieInfo>(MovieList);
            foreach (TraktLibraryMovies tlm in traktMoviesAll)
            {
                bool notInLocalCollection = true;
                foreach (DBMovieInfo movie in MovieList.Where(m => BasicHandler.GetProperMovieImdbId(m.ImdbID) == tlm.IMDBID || (GetTmdbID(m) == tlm.TMDBID) || (string.Compare(m.Title, tlm.Title, true) == 0 && m.Year.ToString() == tlm.Year)))
                {
                    //If the users IMDB ID is empty and we have matched one then set it
                    if(!String.IsNullOrEmpty(tlm.IMDBID) && (String.IsNullOrEmpty(movie.ImdbID) || movie.ImdbID.Length != 9))
                    {
                        TraktLogger.Info("Movie '{0}' inserted IMDBID '{1}'", movie.Title, tlm.IMDBID);
                        movie.ImdbID = tlm.IMDBID;
                        movie.Commit();
                    }
                    
                    // If it is watched in Trakt but not Moving Pictures update
                    // skip if movie is watched but user wishes to have synced as unseen locally
                    if (tlm.Plays > 0 && !tlm.UnSeen && movie.ActiveUserSettings.WatchedCount == 0)
                    {
                        TraktLogger.Info("Movie '{0}' is watched on Trakt, updating database", movie.Title);
                        movie.ActiveUserSettings.WatchedCount = 1;
                        movie.Commit();
                    }

                    // mark movies as unseen if watched locally
                    if (tlm.UnSeen && movie.ActiveUserSettings.WatchedCount > 0)
                    {
                        TraktLogger.Info("Movie '{0}' is unseen on Trakt, updating database", movie.Title);
                        movie.ActiveUserSettings.WatchedCount = 0;
                        movie.ActiveUserSettings.Commit();
                    }

                    notInLocalCollection = false;

                    //filter out if its already in collection
                    if (tlm.InCollection)
                    {
                        moviesToSync.RemoveAll(m => (BasicHandler.GetProperMovieImdbId(m.ImdbID) == tlm.IMDBID) || (GetTmdbID(m) == tlm.TMDBID) || (string.Compare(m.Title, tlm.Title, true) == 0 && m.Year.ToString() == tlm.Year));
                    }
                    break;
                }

                if (notInLocalCollection && tlm.InCollection)
                    NoLongerInOurCollection.Add(tlm);
            }
            #endregion

            #region Movies to Sync to Seen Collection
            // Filter out a list of movies already marked as watched on trakt
            // also filter out movie marked as unseen so we dont reset the unseen cache online
            List<DBMovieInfo> watchedMoviesToSync = new List<DBMovieInfo>(SeenList);
            foreach (TraktLibraryMovies tlm in traktMoviesAll.Where(t => t.Plays > 0 || t.UnSeen))
            {
                foreach (DBMovieInfo watchedMovie in SeenList.Where(m => BasicHandler.GetProperMovieImdbId(m.ImdbID) == tlm.IMDBID || (GetTmdbID(m) == tlm.TMDBID) || (string.Compare(m.Title, tlm.Title, true) == 0 && m.Year.ToString() == tlm.Year)))
                {
                    //filter out
                    watchedMoviesToSync.Remove(watchedMovie);
                }
            }
            #endregion

            //Send Library/Collection
            TraktLogger.Info("{0} movies need to be added to Library", moviesToSync.Count.ToString());
            foreach (DBMovieInfo m in moviesToSync)
                TraktLogger.Info("Sending movie to trakt library, Title: {0}, Year: {1}, IMDb: {2}, TMDb: {3}", m.Title, m.Year.ToString(), m.ImdbID, GetTmdbID(m));

            if (moviesToSync.Count > 0)
            {
                TraktMovieSyncResponse response = TraktAPI.TraktAPI.SyncMovieLibrary(CreateSyncData(moviesToSync), TraktSyncModes.library);
                BasicHandler.InsertSkippedMovies(response);
                TraktAPI.TraktAPI.LogTraktResponse(response);
            }

            //Send Seen
            TraktLogger.Info("{0} movies need to be added to SeenList", watchedMoviesToSync.Count.ToString());
            foreach (DBMovieInfo m in watchedMoviesToSync)
                TraktLogger.Info("Sending movie to trakt as seen, Title: {0}, Year: {1}, IMDb: {2}, TMDb: {3}", m.Title, m.Year.ToString(), m.ImdbID, GetTmdbID(m));

            if (watchedMoviesToSync.Count > 0)
            {
                TraktMovieSyncResponse response = TraktAPI.TraktAPI.SyncMovieLibrary(CreateSyncData(watchedMoviesToSync), TraktSyncModes.seen);
                BasicHandler.InsertSkippedMovies(response);
                TraktAPI.TraktAPI.LogTraktResponse(response);
            }

            //Dont clean library if more than one movie plugin installed
            if (TraktSettings.KeepTraktLibraryClean && TraktSettings.MoviePluginCount == 1)
            {
                //Remove movies we no longer have in our local database from Trakt
                foreach (var m in NoLongerInOurCollection)
                    TraktLogger.Info("Removing from Trakt Collection {0}", m.Title);

                TraktLogger.Info("{0} movies need to be removed from Trakt Collection", NoLongerInOurCollection.Count.ToString());

                if (NoLongerInOurCollection.Count > 0)
                {
                    //Then remove from library
                    TraktMovieSyncResponse response = TraktAPI.TraktAPI.SyncMovieLibrary(BasicHandler.CreateMovieSyncData(NoLongerInOurCollection), TraktSyncModes.unlibrary);
                    TraktAPI.TraktAPI.LogTraktResponse(response);
                }
            }

            IEnumerable<TraktWatchListMovie> traktWatchListMovies = null;
            IEnumerable<TraktMovie> traktRecommendationMovies = null;

            //Get it once to speed things up
            if (TraktSettings.MovingPicturesCategories || TraktSettings.MovingPicturesFilters)
            {
                TraktLogger.Debug("Retrieving watchlist from trakt");
                traktWatchListMovies = TraktAPI.TraktAPI.GetWatchListMovies(TraktSettings.Username);

                TraktLogger.Debug("Retrieving recommendations from trakt");
                traktRecommendationMovies = TraktAPI.TraktAPI.GetRecommendedMovies();
            }

            //Moving Pictures Categories
            if (TraktSettings.MovingPicturesCategories && traktWatchListMovies != null && traktRecommendationMovies != null)
                UpdateMovingPicturesCategories(traktRecommendationMovies, traktWatchListMovies);
            else
                RemoveMovingPicturesCategories();

            //Moving Pictures Filters
            if (TraktSettings.MovingPicturesFilters && traktWatchListMovies != null && traktRecommendationMovies != null)
                UpdateMovingPicturesFilters(traktRecommendationMovies, traktWatchListMovies);
            else
                RemoveMovingPicturesFilters();

            SyncInProgress = false;
            TraktLogger.Info("Moving Pictures Sync Completed");

        }

        public bool Scrobble(String filename)
        {
            StopScrobble();
            List<DBMovieInfo> searchResults = (from m in DBMovieInfo.GetAll() where (from path in m.LocalMedia select path.FullPath).ToList().Contains(filename) select m).ToList();
            if (searchResults.Count == 1)
            {
                //Create timer
                currentMovie = searchResults[0];
                TraktLogger.Info(string.Format("Found playing movie {0}", currentMovie.Title));
                ScrobbleHandler(currentMovie, TraktScrobbleStates.watching);
                traktTimer = new Timer();
                traktTimer.Interval = 900000;
                traktTimer.Elapsed += new ElapsedEventHandler(traktTimer_Elapsed);
                traktTimer.Start();
                return true;
            }
            else if (searchResults.Count == 0)
                TraktLogger.Debug("Playback started but Movie not found");
            else
                TraktLogger.Debug("Multiple movies found for filename something is up!");
            return false;
        }

        public void StopScrobble()
        {
            if (traktTimer != null)
                traktTimer.Stop();
            
            if (currentMovie != null)
            {
                ScrobbleHandler(currentMovie, TraktScrobbleStates.cancelwatching);
                currentMovie = null;
            }
        }

        #endregion

        #region Scrobbling

        /// <summary>
        /// Ticker for Scrobbling
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void traktTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            ScrobbleHandler(currentMovie, TraktScrobbleStates.watching);
        }

        /// <summary>
        /// Scrobbles a given movie
        /// </summary>
        /// <param name="movie">Movie to Scrobble</param>
        /// <param name="state">Scrobbling mode to use</param>
        private void ScrobbleHandler(DBMovieInfo movie, TraktScrobbleStates state)
        {
            TraktLogger.Debug("Scrobbling Movie {0}", movie.Title);
            // MovingPictures stores duration in milliseconds, g_Player reports in seconds
            Double currentPosition = g_Player.CurrentPosition;
            Double duration = movie.ActualRuntime / 1000;

            Double percentageCompleted = duration != 0.0 ? (currentPosition / duration * 100) : 0.0;
            TraktLogger.Debug(string.Format("Percentage of {0} is {1}", movie.Title, percentageCompleted.ToString()));

            //Create Scrobbling Data
            TraktMovieScrobble scrobbleData = CreateScrobbleData(movie);

            if (scrobbleData != null)
            {
                // duration is reported in minutes
                scrobbleData.Duration = Convert.ToInt32(duration / 60).ToString();
                scrobbleData.Progress = Convert.ToInt32(percentageCompleted).ToString();
                BackgroundWorker scrobbler = new BackgroundWorker();
                scrobbler.DoWork += new DoWorkEventHandler(scrobbler_DoWork);
                scrobbler.RunWorkerCompleted += new RunWorkerCompletedEventHandler(scrobbler_RunWorkerCompleted);
                scrobbler.RunWorkerAsync(new MovieScrobbleAndMode { MovieScrobble = scrobbleData, ScrobbleState = state });
            }
        }

        /// <summary>
        /// BackgroundWorker code to scrobble movie state
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void scrobbler_DoWork(object sender, DoWorkEventArgs e)
        {
            MovieScrobbleAndMode data = e.Argument as MovieScrobbleAndMode;
            e.Result = TraktAPI.TraktAPI.ScrobbleMovieState(data.MovieScrobble, data.ScrobbleState);
        }

        /// <summary>
        /// End point for BackgroundWorker to send result to log
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void scrobbler_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            TraktResponse response = e.Result as TraktResponse;
            TraktAPI.TraktAPI.LogTraktResponse(response);
        }

        #endregion

        #region MovingPicturesHooks

        /// <summary>
        /// Fired when an objected is removed from the Moving Pictures Database
        /// </summary>
        /// <param name="obj"></param>
        private void DatabaseManager_ObjectDeleted(Cornerstone.Database.Tables.DatabaseTable obj)
        {
            if (TraktSettings.AccountStatus != ConnectionState.Connected) return;

            //If we have removed a movie from Moving Pictures we want to update Trakt library
            if (obj.GetType() == typeof(DBMovieInfo))
            {
                //Only remove from collection if the user wants us to
                if (TraktSettings.KeepTraktLibraryClean)
                {
                    //A Movie was removed from the database update trakt
                    DBMovieInfo insertedMovie = (DBMovieInfo)obj;
                    SyncMovie(CreateSyncData(insertedMovie), TraktSyncModes.unlibrary);
                }
            }
        }

        /// <summary>
        /// Fired when an object is updated in the Moving Pictures Database
        /// </summary>
        /// <param name="obj"></param>
        private void DatabaseManager_ObjectUpdated(Cornerstone.Database.Tables.DatabaseTable obj)
        {
            if (TraktSettings.AccountStatus != ConnectionState.Connected) return;

            //If it is user settings for a movie
            if (obj.GetType() == typeof(DBUserMovieSettings))
            {
                DBUserMovieSettings userMovieSettings = (DBUserMovieSettings)obj;
                DBMovieInfo movie = userMovieSettings.AttachedMovies[0];

                // don't do anything if movie is blocked
                if (TraktSettings.BlockedFilenames.Contains(movie.LocalMedia[0].FullPath) || TraktSettings.BlockedFolders.Any(f => movie.LocalMedia[0].FullPath.ToLowerInvariant().Contains(f.ToLowerInvariant())))
                {
                    TraktLogger.Info("Movie {0} is on the blocked list so we didn't update Trakt", movie.Title);
                    return;
                }

                // if we are syncing, we maybe manually setting state from trakt
                // in this case we dont want to resend to trakt
                if (SyncInProgress) return;

                //We check the watched flag and update Trakt respectfully
                //ignore if movie is the current movie being scrobbled, this will be set to watched automatically
                if (userMovieSettings.WatchCountChanged && movie != currentMovie)
                {
                    if (userMovieSettings.WatchedCount == 0)
                    {
                        SyncMovie(CreateSyncData(movie), TraktSyncModes.unseen);
                    }
                    else
                    {
                        SyncMovie(CreateSyncData(movie), TraktSyncModes.seen);
                        RemoveMovieFromFiltersAndCategories(movie);
                    }
                }
                
                //We will update the Trakt rating of the Movie
                //TODO: Create a user setting for what they want to define as love/hate
                if (userMovieSettings.RatingChanged && userMovieSettings.UserRating > 0)
                {
                    if (userMovieSettings.UserRating >= 4)
                    {
                        RateMovie(CreateRateData(movie, TraktRateValue.love.ToString()));
                    }
                    else if (userMovieSettings.UserRating <= 2)
                    {
                        RateMovie(CreateRateData(movie, TraktRateValue.hate.ToString()));
                    }
                }

                // reset user flags for watched/ratings
                userMovieSettings.WatchCountChanged = false;
                userMovieSettings.RatingChanged = false;
            }
        }

        /// <summary>
        /// Fired when an object is inserted in the Moving Pictures Database
        /// </summary>
        /// <param name="obj"></param>
        private void DatabaseManager_ObjectInserted(Cornerstone.Database.Tables.DatabaseTable obj)
        {
            if (TraktSettings.AccountStatus != ConnectionState.Connected) return;

            if (obj.GetType() == typeof(DBWatchedHistory))
            {
                //A movie has been watched push that out.
                DBWatchedHistory watchedEvent = (DBWatchedHistory)obj;
                if (!TraktSettings.BlockedFilenames.Contains(watchedEvent.Movie.LocalMedia[0].FullPath) && !TraktSettings.BlockedFolders.Any(f => watchedEvent.Movie.LocalMedia[0].FullPath.ToLowerInvariant().Contains(f.ToLowerInvariant())))
                {
                    ScrobbleHandler(watchedEvent.Movie, TraktScrobbleStates.scrobble);
                    RemoveMovieFromFiltersAndCategories(watchedEvent.Movie);
                }
                else
                    TraktLogger.Info("Movie {0} was found as blocked so did not scrobble", watchedEvent.Movie.Title);
            }
            else if (obj.GetType() == typeof(DBMovieInfo))
            {
                //A Movie was inserted into the database update trakt
                DBMovieInfo insertedMovie = (DBMovieInfo)obj;
                if (!TraktSettings.BlockedFilenames.Contains(insertedMovie.LocalMedia[0].FullPath) && !TraktSettings.BlockedFolders.Any(f => insertedMovie.LocalMedia[0].FullPath.ToLowerInvariant().Contains(f.ToLowerInvariant())))
                {
                    SyncMovie(CreateSyncData(insertedMovie), TraktSyncModes.library);
                    UpdateCategoriesAndFilters();
                }
                else
                    TraktLogger.Info("Newly inserted movie, {0}, was found on our block list so wasn't added to Trakt", insertedMovie.Title);
            }
        }

        #endregion
        
        #region SyncingMovieData

        /// <summary>
        /// Syncs Movie data in another thread
        /// </summary>
        /// <param name="syncData">Data to sync</param>
        /// <param name="mode">The Syncing mode to use</param>
        private void SyncMovie(TraktMovieSync syncData, TraktSyncModes mode)
        {
            BackgroundWorker moviesync = new BackgroundWorker();
            moviesync.DoWork += new DoWorkEventHandler(moviesync_DoWork);
            moviesync.RunWorkerCompleted += new RunWorkerCompletedEventHandler(moviesync_RunWorkerCompleted);
            moviesync.RunWorkerAsync(new MovieSyncAndMode { SyncData = syncData, Mode = mode });
        }

        /// <summary>
        /// Work Handler for Syncing Data in a seperate thread
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void moviesync_DoWork(object sender, DoWorkEventArgs e)
        {
            //Get the sync data
            MovieSyncAndMode data = e.Argument as MovieSyncAndMode;
            //performt the sync
            e.Result = TraktAPI.TraktAPI.SyncMovieLibrary(data.SyncData, data.Mode);
        }

        /// <summary>
        /// Records the result of the Movie Sync to the Log
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void moviesync_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            TraktResponse response = e.Result as TraktResponse;
            TraktAPI.TraktAPI.LogTraktResponse(response);
        }

        #endregion

        #region MovieRating
        private void RateMovie(TraktRateMovie rateData)
        {
            BackgroundWorker rateMovie = new BackgroundWorker();
            rateMovie.DoWork += new DoWorkEventHandler(rateMovie_DoWork);
            rateMovie.RunWorkerCompleted += new RunWorkerCompletedEventHandler(rateMovie_RunWorkerCompleted);
            rateMovie.RunWorkerAsync(rateData);
        }

        void rateMovie_DoWork(object sender, DoWorkEventArgs e)
        {
            TraktRateMovie data = (TraktRateMovie)e.Argument;
            e.Result = TraktAPI.TraktAPI.RateMovie(data);
        }

        void rateMovie_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            TraktRateResponse response = (TraktRateResponse)e.Result;
            TraktAPI.TraktAPI.LogTraktResponse(response);
        }
        #endregion

        #region DataCreators

        /// <summary>
        /// Creates Sync Data based on a List of DBMovieInfo objects
        /// </summary>
        /// <param name="Movies">The movies to base the object on</param>
        /// <returns>The Trakt Sync data to send</returns>
        public static TraktMovieSync CreateSyncData(List<DBMovieInfo> Movies)
        {
            string username = TraktSettings.Username;
            string password = TraktSettings.Password;

            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
                return null;

            List<TraktMovieSync.Movie> moviesList = (from m in Movies
                                                select new TraktMovieSync.Movie
                                                {
                                                    IMDBID = m.ImdbID,
                                                    TMDBID = GetTmdbID(m),
                                                    Title = m.Title,
                                                    Year = m.Year.ToString()
                                                }).ToList();

            TraktMovieSync syncData = new TraktMovieSync
            {
                UserName = username,
                Password = password,
                MovieList = moviesList
            };
            return syncData;
        }
               
        /// <summary>
        /// Creates Sync Data based on a single DBMovieInfo object
        /// </summary>
        /// <param name="Movie">The movie to base the object on</param>
        /// <returns>The Trakt Sync data to send</returns>
        public static TraktMovieSync CreateSyncData(DBMovieInfo Movie)
        {
            string username = TraktSettings.Username;
            string password = TraktSettings.Password;

            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
                return null;

            List<TraktMovieSync.Movie> moviesList = new List<TraktMovieSync.Movie>();
            moviesList.Add(new TraktMovieSync.Movie
            {
                IMDBID = Movie.ImdbID,
                TMDBID = GetTmdbID(Movie),
                Title = Movie.Title,
                Year = Movie.Year.ToString()
            });

            TraktMovieSync syncData = new TraktMovieSync
            {
                UserName = username,
                Password = password,
                MovieList = moviesList
            };
            return syncData;
        }

        /// <summary>
        /// Creates Scrobble data based on a DBMovieInfo object
        /// </summary>
        /// <param name="movie">The movie to base the object on</param>
        /// <returns>The Trakt scrobble data to send</returns>
        public static TraktMovieScrobble CreateScrobbleData(DBMovieInfo movie)
        {
            string username = TraktSettings.Username;
            string password = TraktSettings.Password;

            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
                return null;

            TraktMovieScrobble scrobbleData = new TraktMovieScrobble
            {
                Title = movie.Title,
                Year = movie.Year.ToString(),
                IMDBID = movie.ImdbID,
                TMDBID = GetTmdbID(movie),
                PluginVersion = TraktSettings.Version,
                MediaCenter = "Mediaportal",
                MediaCenterVersion = Assembly.GetEntryAssembly().GetName().Version.ToString(),
                MediaCenterBuildDate = String.Empty,
                UserName = username,
                Password = password
            };
            return scrobbleData;
        }

        public static TraktRateMovie CreateRateData(DBMovieInfo movie, String rating)
        {
            string username = TraktSettings.Username;
            string password = TraktSettings.Password;

            if (String.IsNullOrEmpty(username) || String.IsNullOrEmpty(password))
                return null;

            TraktRateMovie rateData = new TraktRateMovie
            {
                Title = movie.Title,
                Year = movie.Year.ToString(),
                IMDBID = movie.ImdbID,
                TMDBID = GetTmdbID(movie),
                UserName = username,
                Password = password,
                Rating = rating
            };
            return rateData;
        }

        #endregion

        #region Other Private Methods

        private static void UpdateMovingPicturesCategories(IEnumerable<TraktMovie> traktRecommendationMovies, IEnumerable<TraktWatchListMovie> traktWatchListMovies)
        {
            if (!TraktSettings.MovingPicturesCategories)
                return;

            TraktLogger.Info("Updating Moving Pictures Categories");

            DBNode<DBMovieInfo> traktNode = null;

            if (TraktSettings.MovingPicturesCategoryId == -1)
            {
                CreateMovingPictureCategories();
            }

            if (TraktSettings.MovingPicturesCategoryId != -1)
            {
                TraktLogger.Debug("Retrieving node from Moving Pictures Database");
                traktNode = MovingPicturesCore.DatabaseManager.Get<DBNode<DBMovieInfo>>(TraktSettings.MovingPicturesCategoryId);
            }

            if (traktNode != null)
            {
                TraktLogger.Debug("Removing all children nodes");
                traktNode.Children.Clear();

                TraktLogger.Debug("Adding nodes");
                traktNode.Children.AddRange(CreateNodes(traktRecommendationMovies, traktWatchListMovies));
                traktNode.Children.ForEach(new Action<DBNode<DBMovieInfo>>(n => n.Parent = traktNode));
                MovingPicturesCore.Settings.CategoriesMenu.Commit();
            }
            else
            {
                TraktLogger.Error("Trakt Node is null, can't continue making categories");
            }
        }

        private static void UpdateMovingPicturesFilters(IEnumerable<TraktMovie> traktRecommendationMovies, IEnumerable<TraktWatchListMovie> traktWatchListMovies)
        {
            if (!TraktSettings.MovingPicturesFilters)
                return;

            TraktLogger.Info("Updating Moving Pictures Filters");

            DBNode<DBMovieInfo> traktNode = null;

            if (TraktSettings.MovingPicturesFiltersId == -1)
            {
                CreateMovingPictureFilters();
            }

            if (TraktSettings.MovingPicturesFiltersId != -1)
            {
                TraktLogger.Debug("Retrieving node from Moving Pictures Database");
                traktNode = MovingPicturesCore.DatabaseManager.Get<DBNode<DBMovieInfo>>(TraktSettings.MovingPicturesFiltersId);
            }

            if (traktNode != null)
            {
                TraktLogger.Debug("Removing all children nodes");
                traktNode.Children.Clear();

                TraktLogger.Debug("Adding nodes");
                traktNode.Children.AddRange(CreateNodes(traktRecommendationMovies, traktWatchListMovies));
                traktNode.Children.ForEach(new Action<DBNode<DBMovieInfo>>(n => n.Parent = traktNode));
                MovingPicturesCore.Settings.FilterMenu.Commit();
            }
            else
            {
                TraktLogger.Error("Trakt Node is null, can't continue making filters");
            }
        }

        private static IEnumerable<DBNode<DBMovieInfo>> CreateNodes(IEnumerable<TraktMovie> traktRecommendationMovies, IEnumerable<TraktWatchListMovie> traktWatchListMovies)
        {
            #region WatchList
            TraktLogger.Debug("Creating the watchlist node");
            var watchlistNode = new DBNode<DBMovieInfo> {Name = "${" + GUI.Translation.WatchList + "}"};

            var watchlistSettings = new DBMovieNodeSettings();
            watchlistNode.AdditionalSettings = watchlistSettings;
            
            TraktLogger.Debug("Getting the Movie's from Moving Pictures");
            var movieList = DBMovieInfo.GetAll();


            TraktLogger.Debug("Creating the watchlist filter");
            var watchlistFilter = new DBFilter<DBMovieInfo>();
            foreach (var movie in traktWatchListMovies.Select(traktmovie => movieList.Find(m => m.ImdbID.CompareTo(traktmovie.Imdb) == 0)).Where(movie => movie != null))
            {
                TraktLogger.Debug("Adding {0} to watchlist", movie.Title);
                watchlistFilter.WhiteList.Add(movie);
            }
            
            if (watchlistFilter.WhiteList.Count == 0)
            {
                TraktLogger.Debug("Nothing in watchlist, Blacklisting everything");
                watchlistFilter.BlackList.AddRange(movieList);
            }

            watchlistNode.Filter = watchlistFilter;


            #endregion

            #region Recommendations
            TraktLogger.Debug("Creating the recommendations node");
            var recommendationsNode = new DBNode<DBMovieInfo> {Name = "${" + GUI.Translation.Recommendations + "}"};

            var recommendationsSettings = new DBMovieNodeSettings();
            recommendationsNode.AdditionalSettings = recommendationsSettings;

            TraktLogger.Debug("Creating the recommendations filter");
            var recommendationsFilter = new DBFilter<DBMovieInfo>();
            foreach (var movie in traktRecommendationMovies.Select(traktMovie => movieList.Find(m => m.ImdbID.CompareTo(traktMovie.Imdb) == 0)).Where(movie => movie != null))
            {
                TraktLogger.Debug("Adding {0} to recommendations", movie.Title);
                recommendationsFilter.WhiteList.Add(movie);
            }
            
            if (recommendationsFilter.WhiteList.Count == 0)
            {
                TraktLogger.Debug("Nothing in recommendation list, Blacklisting everything");
                recommendationsFilter.BlackList.AddRange(movieList);
            }

            recommendationsNode.Filter = recommendationsFilter;

            #endregion

            return new DBNode<DBMovieInfo>[] { watchlistNode, recommendationsNode };
        }

        private static void RemoveMovieFromFiltersAndCategories(DBMovieInfo movie)
        {
            TraktLogger.Info("Removing {0} from filters and categories", movie.Title);

            #region Categories
            if (TraktSettings.MovingPicturesCategoryId != -1)
            {
                var rootNode = MovingPicturesCore.DatabaseManager.Get<DBNode<DBMovieInfo>>(TraktSettings.MovingPicturesCategoryId);
                if(rootNode != null)
                {
                    RemoveMovieFromNode(movie, rootNode);
                }
                else
                    TraktLogger.Error("Couldn't find the categories node");
            }
            else
                TraktLogger.Debug("Categories not created, skipping");
            #endregion

            #region Filters
            if (TraktSettings.MovingPicturesFiltersId != -1)
            {
                var rootNode = MovingPicturesCore.DatabaseManager.Get<DBNode<DBMovieInfo>>(TraktSettings.MovingPicturesFiltersId);
                if (rootNode != null)
                {
                    RemoveMovieFromNode(movie, rootNode);
                }
                else
                    TraktLogger.Error("Couldn't find the filters node");
            }
            else
                TraktLogger.Debug("Filters not created, skipping");
            #endregion

            TraktLogger.Info("Finished removing from filters and categories");
        }

        private static void RemoveMovieFromNode(DBMovieInfo movie, DBNode<DBMovieInfo> rootNode)
        {
            foreach (var node in rootNode.Children)
            {
                node.Filter.WhiteList.Remove(movie);
                if (node.Filter.WhiteList.Count == 0)
                    node.Filter.BlackList.AddRange(DBMovieInfo.GetAll());
                node.Commit();
            }
        }

        #endregion

        #region Other Public Methods

        public void DisposeEvents()
        {
            TraktLogger.Debug("Removing Hooks from Moving Pictures Database");
            MovingPicturesCore.DatabaseManager.ObjectInserted -= new Cornerstone.Database.DatabaseManager.ObjectAffectedDelegate(DatabaseManager_ObjectInserted);
            MovingPicturesCore.DatabaseManager.ObjectUpdated -= new Cornerstone.Database.DatabaseManager.ObjectAffectedDelegate(DatabaseManager_ObjectUpdated);
            MovingPicturesCore.DatabaseManager.ObjectDeleted -= new Cornerstone.Database.DatabaseManager.ObjectAffectedDelegate(DatabaseManager_ObjectDeleted);
        }

        public static string GetTmdbID(DBMovieInfo movie)
        {
            if (tmdbSource == null) return null;

            string id = movie.GetSourceMovieInfo(tmdbSource).Identifier;
            if (id == null || id.Trim() == string.Empty) return null;
            return id;
        }

        public static bool FindMovieID(string title, int year, string imdbid, ref int? movieID)
        {
            // get all movies in local database
            List<DBMovieInfo> movies = DBMovieInfo.GetAll();

            // try find a match
            DBMovieInfo movie = movies.Find(m => BasicHandler.GetProperMovieImdbId(m.ImdbID) == imdbid || (string.Compare(m.Title, title, true) == 0 && m.Year == year));
            if (movie == null) return false;

            movieID = movie.ID;
            return true;
        }

        public static void PlayMovie(int? movieID)
        {
            if (movieID == null) return;

            // get all movies in local database
            List<DBMovieInfo> movies = DBMovieInfo.GetAll();

            // try find a match
            DBMovieInfo movie = movies.Find(m => m.ID == movieID);

            if (movie == null) return;
            PlayMovie(movie);
        }

        public static void PlayMovie(DBMovieInfo movie)
        {
            if (player == null) player = new MoviePlayer(new MovingPicturesGUI());
            player.Play(movie);
        }

        public static void CreateMovingPictureCategories()
        {
            if (!TraktSettings.MovingPicturesCategories)
                return;

            TraktLogger.Debug("Checking if Category has already been created");
            if (TraktSettings.MovingPicturesCategoryId == -1)
            {
                TraktLogger.Debug("Category not created so let's create it");
                DBNode<DBMovieInfo> traktNode = new DBNode<DBMovieInfo>();
                traktNode.Name = "${Trakt}";

                DBMovieNodeSettings nodeSettings = new DBMovieNodeSettings();
                traktNode.AdditionalSettings = nodeSettings;

                TraktLogger.Debug("Setting the sort position to {0}", (MovingPicturesCore.Settings.CategoriesMenu.RootNodes.Count + 1).ToString());
                //Add it at the end
                traktNode.SortPosition = MovingPicturesCore.Settings.CategoriesMenu.RootNodes.Count + 1;

                TraktLogger.Debug("Adding to Root Node");
                MovingPicturesCore.Settings.CategoriesMenu.RootNodes.Add(traktNode);

                TraktLogger.Debug("Committing");
                MovingPicturesCore.Settings.CategoriesMenu.Commit();

                TraktLogger.Debug("Saving the ID {0}", traktNode.ID.ToString());
                TraktSettings.MovingPicturesCategoryId = (int)traktNode.ID;
                TraktSettings.saveSettings();

            }
            else
            {
                TraktLogger.Debug("Category has already been created");
            }
        }

        public static void UpdateMovingPicturesCategories()
        {
            if (!TraktSettings.MovingPicturesCategories || TraktSettings.AccountStatus != ConnectionState.Connected)
                return;

            TraktLogger.Debug("Retrieving watchlist from trakt");
            IEnumerable<TraktWatchListMovie> traktWatchListMovies = TraktAPI.TraktAPI.GetWatchListMovies(TraktSettings.Username);

            TraktLogger.Debug("Retrieving recommendations from trakt");
            IEnumerable<TraktMovie> traktRecommendationMovies = TraktAPI.TraktAPI.GetRecommendedMovies();

            UpdateMovingPicturesCategories(traktRecommendationMovies, traktWatchListMovies);
        }

        public static void RemoveMovingPicturesCategories()
        {
            if (TraktSettings.MovingPicturesCategories)
                return;

            TraktLogger.Info("Removing Moving Pictures Categories");

            if (TraktSettings.MovingPicturesCategoryId != -1)
            {
                DBNode<DBMovieInfo> traktNode = MovingPicturesCore.DatabaseManager.Get<DBNode<DBMovieInfo>>(TraktSettings.MovingPicturesCategoryId);

                if (traktNode != null)
                {
                    TraktLogger.Debug("Removing Categories from Moving Pictures");
                    MovingPicturesCore.Settings.CategoriesMenu.RootNodes.Remove(traktNode);
                    MovingPicturesCore.Settings.CategoriesMenu.RootNodes.Commit();
                }
                else
                {
                    TraktLogger.Error("Trakt Node is null!, it's already been removed");
                }

                TraktLogger.Debug("Removing setting");
                TraktSettings.MovingPicturesCategoryId = -1;
                TraktSettings.saveSettings();
            }
            else
            {
                TraktLogger.Debug("We don't have a record of the id!");
            }
        }

        public static void CreateMovingPictureFilters()
        {
            if (!TraktSettings.MovingPicturesFilters)
                return;

            TraktLogger.Debug("Checking if Filter has already been created");
            if (TraktSettings.MovingPicturesFiltersId == -1)
            {
                TraktLogger.Debug("Filter not created so let's create it");
                DBNode<DBMovieInfo> traktNode = new DBNode<DBMovieInfo>();
                traktNode.Name = "${Trakt}";

                DBMovieNodeSettings nodeSettings = new DBMovieNodeSettings();
                traktNode.AdditionalSettings = nodeSettings;

                TraktLogger.Debug("Setting the sort position to {0}", (MovingPicturesCore.Settings.FilterMenu.RootNodes.Count + 1).ToString());
                //Add it at the end
                traktNode.SortPosition = MovingPicturesCore.Settings.FilterMenu.RootNodes.Count + 1;

                TraktLogger.Debug("Adding to Root Node");
                MovingPicturesCore.Settings.FilterMenu.RootNodes.Add(traktNode);

                TraktLogger.Debug("Committing");
                MovingPicturesCore.Settings.FilterMenu.Commit();

                TraktLogger.Debug("Saving the ID {0}", traktNode.ID.ToString());
                TraktSettings.MovingPicturesFiltersId = (int)traktNode.ID;
                TraktSettings.saveSettings();

            }
            else
            {
                TraktLogger.Debug("Category has already been created");
            }
        }

        public static void UpdateMovingPicturesFilters()
        {
            if (!TraktSettings.MovingPicturesFilters || TraktSettings.AccountStatus != ConnectionState.Connected)
                return;

            TraktLogger.Debug("Retrieving watchlist from trakt");
            IEnumerable<TraktWatchListMovie> traktWatchListMovies = TraktAPI.TraktAPI.GetWatchListMovies(TraktSettings.Username);

            TraktLogger.Debug("Retrieving recommendations from trakt");
            IEnumerable<TraktMovie> traktRecommendationMovies = TraktAPI.TraktAPI.GetRecommendedMovies();

            UpdateMovingPicturesFilters(traktRecommendationMovies, traktWatchListMovies);
        }

        public static void RemoveMovingPicturesFilters()
        {
            if (TraktSettings.MovingPicturesFilters)
                return;

            TraktLogger.Info("Removing Moving Pictures Filters");

            if (TraktSettings.MovingPicturesFiltersId != -1)
            {
                DBNode<DBMovieInfo> traktNode = MovingPicturesCore.DatabaseManager.Get<DBNode<DBMovieInfo>>(TraktSettings.MovingPicturesFiltersId);

                if (traktNode != null)
                {
                    TraktLogger.Debug("Removing Filters from Moving Pictures");
                    MovingPicturesCore.Settings.FilterMenu.RootNodes.Remove(traktNode);
                    MovingPicturesCore.Settings.FilterMenu.RootNodes.Commit();
                }
                else
                {
                    TraktLogger.Error("Trakt Node is null!, it's already been removed");
                }

                TraktLogger.Debug("Removing setting");
                TraktSettings.MovingPicturesFiltersId = -1;
                TraktSettings.saveSettings();
            }
            else
            {
                TraktLogger.Debug("We don't have a record of the id!");
            }
        }

        public static void UpdateCategoriesAndFilters()
        {
            var bw = new BackgroundWorker();
            bw.DoWork += delegate(object sender, DoWorkEventArgs args)
                             {
                                 if (!TraktSettings.MovingPicturesCategories && !TraktSettings.MovingPicturesFilters)
                                     return;

                                 TraktLogger.Info("Updating Categories and/or Filters");
                                 if (recommendations == null || watchList == null || (DateTime.Now - recommendationsAndWatchListAge) > TimeSpan.FromMinutes(5))
                                 {
                                     recommendations = TraktAPI.TraktAPI.GetRecommendedMovies();
                                     watchList = TraktAPI.TraktAPI.GetWatchListMovies(TraktSettings.Username);
                                     recommendationsAndWatchListAge = DateTime.Now;
                                 }
                                 if(recommendations == null || watchList == null)
                                 {
                                     TraktLogger.Error("Recommendations or Watchlist were null so updating filters failed");
                                     return;
                                 }
                                 UpdateMovingPicturesCategories(recommendations, watchList);
                                 UpdateMovingPicturesFilters(recommendations, watchList);
                                 TraktLogger.Info("Finished updating filters");
                             };
            bw.RunWorkerAsync();
        }

        #endregion
    }
}
