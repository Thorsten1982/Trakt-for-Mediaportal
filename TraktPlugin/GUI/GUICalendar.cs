﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using MediaPortal.Configuration;
using MediaPortal.GUI.Library;
using Action = MediaPortal.GUI.Library.Action;
using MediaPortal.Util;
using TraktPlugin.TraktAPI;
using TraktPlugin.TraktAPI.DataStructures;

namespace TraktPlugin.GUI
{
    public class GUICalendar : GUIWindow
    {
        #region Skin Controls

        [SkinControl(2)]
        protected GUIButtonControl viewButton = null;

        [SkinControl(3)]
        protected GUIButtonControl startDateButton = null;

        [SkinControl(50)]
        protected GUIFacadeControl Facade = null;

        [SkinControlAttribute(60)]
        protected GUIImage FanartBackground = null;

        [SkinControlAttribute(61)]
        protected GUIImage FanartBackground2 = null;

        [SkinControlAttribute(62)]
        protected GUIImage loadingImage = null;

        #endregion

        #region Enums

        enum FacadeMovement
        {
            Up,
            Down
        }

        enum CalendarType
        {
            MyShows,
            Premieres,
            AllShows
        }

        enum ContextMenuItem
        {
            View,
            StartDate,
            MarkAsWatched,
            AddShowToWatchList,
            AddEpisodeToWatchList,
            RemoveShowFromWatchList,
            RemoveEpisodeFromWatchList,
            AddToLibrary,
            RemoveFromLibrary,
            Rate,
            Shouts,
            Trailers,
            WatchlistFilter
        }

        enum TrailerSite
        {
            IMDb,
            YouTube
        }

        enum StartDates
        {
            Today,
            Yesterday,
            OneWeekAgo,
            TwoWeeksAgo,
            OneMonthAgo
        }

        #endregion

        #region Constructor

        public GUICalendar()
        {
            backdrop = new ImageSwapper();
            backdrop.PropertyOne = "#Trakt.Calendar.Fanart.1";
            backdrop.PropertyTwo = "#Trakt.Calendar.Fanart.2";
        }

        #endregion

        #region Private Variables

        bool StopDownload { get; set; }
        FacadeMovement LastMoved { get; set; }
        CalendarType CurrentCalendar { get; set; }
        StartDates CurrentStartDate { get; set; }
        int CurrentWeekDays = 7;
        int PreviousSelectedIndex;
        int PreviousCalendarDayCount;
        bool IsCached = false;
        ImageSwapper backdrop;
        DateTime LastRequest = new DateTime();        

        #endregion

        #region Private Properties

        IEnumerable<TraktCalendar> TraktCalendarMyShows
        {
            get
            {
                if (_CalendarMyShows == null || LastRequest < DateTime.UtcNow.Subtract(new TimeSpan(0, TraktSettings.WebRequestCacheMinutes, 0)))
                {
                    _CalendarMyShows = TraktAPI.TraktAPI.GetCalendarForUser(TraktSettings.Username, GetStartDate().ToString("yyyyMMdd"), GetDaysForward());
                    LastRequest = DateTime.UtcNow;
                    IsCached = false;
                }
                return _CalendarMyShows;
            }
        }
        static IEnumerable<TraktCalendar> _CalendarMyShows = null;

        IEnumerable<TraktCalendar> TraktCalendarPremieres
        {
            get
            {
                if (_CalendarPremieres == null || LastRequest < DateTime.UtcNow.Subtract(new TimeSpan(0, TraktSettings.WebRequestCacheMinutes, 0)))
                {
                    _CalendarPremieres = TraktAPI.TraktAPI.GetCalendarPremieres(GetStartDate().ToString("yyyyMMdd"), GetDaysForward());
                    LastRequest = DateTime.UtcNow;
                    IsCached = false;
                }
                return _CalendarPremieres;
            }
        }
        private IEnumerable<TraktCalendar> _CalendarPremieres = null;

        IEnumerable<TraktCalendar> TraktCalendarAllShows
        {
            get
            {
                if (_CalendarAllShows == null || LastRequest < DateTime.UtcNow.Subtract(new TimeSpan(0, TraktSettings.WebRequestCacheMinutes, 0)))
                {
                    _CalendarAllShows = TraktAPI.TraktAPI.GetCalendarShows(GetStartDate().ToString("yyyyMMdd"), GetDaysForward());
                    LastRequest = DateTime.UtcNow;
                    IsCached = false;
                }
                return _CalendarAllShows;
            }
        }
        private IEnumerable<TraktCalendar> _CalendarAllShows = null;

        #endregion

        #region Base Overrides

        public override int GetID
        {
            get
            {
                return 87259;
            }
        }

        public override bool Init()
        {
            return Load(GUIGraphicsContext.Skin + @"\Trakt.Calendar.xml");
        }

        protected override void OnPageLoad()
        {
            // Requires Login
            if (!GUICommon.CheckLogin()) return;

            // Clear GUI properties
            ClearProperties();

            // Init Properties
            InitProperties();

            // Load Calendar
            LoadCalendar();
        }

        protected override void OnPageDestroy(int new_windowId)
        {
            PreviousSelectedIndex = Facade.SelectedListItemIndex;
            switch (CurrentCalendar)
            {
                case CalendarType.MyShows:
                    if (_CalendarMyShows != null) PreviousCalendarDayCount = _CalendarMyShows.Count();
                    break;

                case CalendarType.Premieres:
                    if (_CalendarPremieres != null) PreviousCalendarDayCount = _CalendarPremieres.Count();
                    break;

                case CalendarType.AllShows:
                    if (_CalendarAllShows != null) PreviousCalendarDayCount = _CalendarAllShows.Count();
                    break;
            }

            StopDownload = true;
            ClearProperties();

            // save current view
            TraktSettings.DefaultCalendarView = (int)CurrentCalendar;
            TraktSettings.DefaultCalendarStartDate = (int)CurrentStartDate;

            base.OnPageDestroy(new_windowId);
        }

        protected override void OnClicked(int controlId, GUIControl control, Action.ActionType actionType)
        {
            // wait for any background action to finish
            if (GUIBackgroundTask.Instance.IsBusy) return;

            switch (controlId)
            {
                // Facade
                case (50):
                    if (actionType == Action.ActionType.ACTION_SELECT_ITEM)
                    {
                        GUIListItem item = Facade.SelectedListItem as GUIListItem;

                        // Is a group header
                        if (item !=null && item.IsFolder)
                        {
                            switch (CurrentCalendar)
                            {
                                case CalendarType.MyShows:
                                    // previous call may have timedout
                                    if (_CalendarMyShows != null)
                                    {
                                        PreviousCalendarDayCount = _CalendarMyShows.Count();
                                        PreviousSelectedIndex = Facade.SelectedListItemIndex;
                                        CurrentWeekDays += 7;
                                        _CalendarMyShows = null;
                                    }
                                    break;

                                case CalendarType.Premieres:
                                    if (_CalendarPremieres != null)
                                    {
                                        PreviousCalendarDayCount = _CalendarPremieres.Count();
                                        PreviousSelectedIndex = Facade.SelectedListItemIndex;
                                        CurrentWeekDays += 7;
                                        _CalendarPremieres = null;
                                    }
                                    break;

                                case CalendarType.AllShows:
                                    if (_CalendarAllShows != null)
                                    {
                                        PreviousCalendarDayCount = _CalendarAllShows.Count();
                                        PreviousSelectedIndex = Facade.SelectedListItemIndex;
                                        CurrentWeekDays += 7;
                                        _CalendarAllShows = null;
                                    }
                                    break;
                            }

                            // load next 7 days in calendar
                            LoadCalendar();
                        }

                        // Is an episode
                        if (item != null && !item.IsFolder)
                        {
                            CheckAndPlayEpisode();
                        }
                    }
                    break;

                // View Button
                case (2):
                    ShowViewMenu();
                    break;

                // Start Date Button
                case (3):
                    ShowStartDateMenu();
                    break;

                default:
                    break;
            }
            base.OnClicked(controlId, control, actionType);
        }

        public override void OnAction(Action action)
        {
            switch (action.wID)
            {
                case Action.ActionType.ACTION_PLAY:
                case Action.ActionType.ACTION_MUSIC_PLAY:
                    CheckAndPlayEpisode();
                    break;

                case Action.ActionType.ACTION_PREVIOUS_MENU:
                    break;

                case Action.ActionType.ACTION_MOVE_UP:
                case Action.ActionType.ACTION_PAGE_UP:
                    LastMoved = FacadeMovement.Up;                    
                    break;

                case Action.ActionType.ACTION_MOVE_DOWN:
                case Action.ActionType.ACTION_PAGE_DOWN:
                    LastMoved = FacadeMovement.Down;
                    break;
            }
            base.OnAction(action);
        }

        protected override void OnShowContextMenu()
        {
            IDialogbox dlg = (IDialogbox)GUIWindowManager.GetWindow((int)GUIWindow.Window.WINDOW_DIALOG_MENU);
            if (dlg == null) return;

            dlg.Reset();
            dlg.SetHeading(GUIUtils.PluginName());

            TraktCalendar.TraktEpisodes episodeItem = Facade.SelectedListItem.TVTag as TraktCalendar.TraktEpisodes;
            if (episodeItem == null) return;

            // Create Views Menu Item
            GUIListItem listItem = new GUIListItem(Translation.ChangeView);
            dlg.Add(listItem);
            listItem.ItemId = (int)ContextMenuItem.View;

            // Start Date
            listItem = new GUIListItem(Translation.StartDate + "...");
            dlg.Add(listItem);
            listItem.ItemId = (int)ContextMenuItem.StartDate;

            // Rate
            listItem = new GUIListItem(Translation.Rate + "...");
            dlg.Add(listItem);
            listItem.ItemId = (int)ContextMenuItem.Rate;

            // Mark As Watched
            if (episodeItem.Episode.Plays == 0)
            {
                listItem = new GUIListItem(Translation.MarkAsWatched);
                dlg.Add(listItem);
                listItem.ItemId = (int)ContextMenuItem.MarkAsWatched;
            }

            // Add/Remove Show Watch List
            if (!episodeItem.Show.InWatchList)
            {
                listItem = new GUIListItem(Translation.AddShowToWatchList);
                dlg.Add(listItem);
                listItem.ItemId = (int)ContextMenuItem.AddShowToWatchList;
            }
            else
            {
                listItem = new GUIListItem(Translation.RemoveShowFromWatchList);
                dlg.Add(listItem);
                listItem.ItemId = (int)ContextMenuItem.RemoveShowFromWatchList;
            }

            // Add/Remove Episode Watch List
            if (!episodeItem.Episode.InWatchList)
            {
                listItem = new GUIListItem(Translation.AddEpisodeToWatchList);
                dlg.Add(listItem);
                listItem.ItemId = (int)ContextMenuItem.AddEpisodeToWatchList;
            }
            else
            {
                listItem = new GUIListItem(Translation.RemoveEpisodeFromWatchList);
                dlg.Add(listItem);
                listItem.ItemId = (int)ContextMenuItem.RemoveEpisodeFromWatchList;
            }

            // Add/Remove Libary
            if (!episodeItem.Episode.InCollection)
            {
                listItem = new GUIListItem(Translation.AddToLibrary);
                dlg.Add(listItem);
                listItem.ItemId = (int)ContextMenuItem.AddToLibrary;
            }
            else
            {
                listItem = new GUIListItem(Translation.RemoveFromLibrary);
                dlg.Add(listItem);
                listItem.ItemId = (int)ContextMenuItem.RemoveFromLibrary;
            }

            // Shouts
            listItem = new GUIListItem(Translation.Shouts + "...");
            dlg.Add(listItem);
            listItem.ItemId = (int)ContextMenuItem.Shouts;

            #if MP12
            if (TraktHelper.IsOnlineVideosAvailableAndEnabled)
            {
                listItem = new GUIListItem(Translation.Trailers);
                dlg.Add(listItem);
                listItem.ItemId = (int)ContextMenuItem.Trailers;
            }
            #endif

            // Watch List Filter
            if (CurrentCalendar == CalendarType.MyShows)
            {
                if (TraktSettings.CalendarHideTVShowsInWatchList)
                    listItem = new GUIListItem(Translation.ShowTVShowsInWatchlist);
                else
                    listItem = new GUIListItem(Translation.HideTVShowsInWatchlist);
                
                dlg.Add(listItem);
                listItem.ItemId = (int)ContextMenuItem.WatchlistFilter;
            }

            // Show Context Menu
            dlg.DoModal(GUIWindowManager.ActiveWindow);
            if (dlg.SelectedId < 0) return;

            switch (dlg.SelectedId)
            {
                case ((int)ContextMenuItem.View):
                    ShowViewMenu();
                    break;

                case ((int)ContextMenuItem.StartDate):
                    ShowStartDateMenu();
                    break;

                case ((int)ContextMenuItem.Shouts):
                    GUIShouts.ShoutType = GUIShouts.ShoutTypeEnum.episode;
                    GUIShouts.EpisodeInfo = new EpisodeShout
                    {
                        TVDbId = episodeItem.Show.Tvdb,
                        IMDbId = episodeItem.Show.Imdb,
                        Title = episodeItem.Show.Title,
                        SeasonIdx = episodeItem.Episode.Season.ToString(),
                        EpisodeIdx = episodeItem.Episode.Number.ToString()
                    };
                    GUIShouts.Fanart = episodeItem.Show.Images.FanartImageFilename;
                    GUIWindowManager.ActivateWindow((int)TraktGUIWindows.Shouts);
                    break;

                case ((int)ContextMenuItem.Rate):
                    RateEpisode(episodeItem);
                    OnEpisodeSelected(Facade.SelectedListItem, Facade);
                    ((Facade.SelectedListItem as GUITraktCalendarListItem).Item as TraktImage).NotifyPropertyChanged("EpisodeImages");
                    break;

                case ((int)ContextMenuItem.MarkAsWatched):
                    MarkEpisodeAsWatched(episodeItem);
                    episodeItem.Episode.Plays = 1;
                    OnEpisodeSelected(Facade.SelectedListItem, Facade);
                    ((Facade.SelectedListItem as GUITraktCalendarListItem).Item as TraktImage).NotifyPropertyChanged("EpisodeImages");
                    break;

                case ((int)ContextMenuItem.AddShowToWatchList):
                    AddShowToWatchList(episodeItem);
                    episodeItem.Show.InWatchList = true;
                    OnEpisodeSelected(Facade.SelectedListItem, Facade);
                    GUIWatchListShows.ClearCache(TraktSettings.Username);
                    break;

                case ((int)ContextMenuItem.AddEpisodeToWatchList):
                    AddEpisodeToWatchList(episodeItem);
                    episodeItem.Episode.InWatchList = true;
                    OnEpisodeSelected(Facade.SelectedListItem, Facade);
                    ((Facade.SelectedListItem as GUITraktCalendarListItem).Item as TraktImage).NotifyPropertyChanged("EpisodeImages");
                    GUIWatchListShows.ClearCache(TraktSettings.Username);
                    break;

                case ((int)ContextMenuItem.RemoveShowFromWatchList):
                    RemoveShowFromWatchList(episodeItem);
                    episodeItem.Show.InWatchList = false;
                    OnEpisodeSelected(Facade.SelectedListItem, Facade);
                    GUIWatchListShows.ClearCache(TraktSettings.Username);
                    break;

                case ((int)ContextMenuItem.RemoveEpisodeFromWatchList):
                    RemoveEpisodeFromWatchList(episodeItem);
                    episodeItem.Episode.InWatchList = false;
                    OnEpisodeSelected(Facade.SelectedListItem, Facade);
                    ((Facade.SelectedListItem as GUITraktCalendarListItem).Item as TraktImage).NotifyPropertyChanged("EpisodeImages");
                    GUIWatchListShows.ClearCache(TraktSettings.Username);
                    break;

                case ((int)ContextMenuItem.AddToLibrary):
                    AddEpisodeToLibrary(episodeItem);
                    episodeItem.Episode.InCollection = true;
                    OnEpisodeSelected(Facade.SelectedListItem, Facade);
                    ((Facade.SelectedListItem as GUITraktCalendarListItem).Item as TraktImage).NotifyPropertyChanged("EpisodeImages");
                    break;

                case ((int)ContextMenuItem.RemoveFromLibrary):
                    RemoveEpisodeFromLibrary(episodeItem);
                    episodeItem.Episode.InCollection = false;
                    OnEpisodeSelected(Facade.SelectedListItem, Facade);
                    ((Facade.SelectedListItem as GUITraktCalendarListItem).Item as TraktImage).NotifyPropertyChanged("EpisodeImages");
                    break;

                #if MP12
                case ((int)ContextMenuItem.Trailers):
                    if (episodeItem != null) ShowTrailersMenu(episodeItem);
                    break;
                #endif

                case ((int)ContextMenuItem.WatchlistFilter):
                    TraktSettings.CalendarHideTVShowsInWatchList = !TraktSettings.CalendarHideTVShowsInWatchList;
                    LoadCalendar();
                    break;

                default:
                    break;
            }

            base.OnShowContextMenu();
        }

        #endregion

        #region Private Methods

        private TraktEpisodeSync CreateEpisodeSyncData(TraktCalendar.TraktEpisodes episode)
        {
            List<TraktEpisodeSync.Episode> episodeList = new List<TraktEpisodeSync.Episode>();
            episodeList.Add(new TraktEpisodeSync.Episode { EpisodeIndex = episode.Episode.Number.ToString(), SeasonIndex = episode.Episode.Season.ToString() });

            TraktEpisodeSync syncData = new TraktEpisodeSync
            {
                EpisodeList = episodeList,
                IMDBID = episode.Show.Imdb,
                SeriesID = episode.Show.Tvdb,
                Title = episode.Show.Title,
                Year = episode.Show.Year.ToString(),
                UserName = TraktSettings.Username,
                Password = TraktSettings.Password
            };

            return syncData;
        }

        private TraktShowSync CreateShowSyncData(TraktCalendar.TraktEpisodes episode)
        {
            List<TraktShowSync.Show> shows = new List<TraktShowSync.Show>();

            TraktShowSync.Show show = new TraktShowSync.Show
            {
                Title = episode.Show.Title,
                TVDBID = episode.Show.Tvdb,
                Year = episode.Show.Year
            };
            shows.Add(show);

            TraktShowSync syncData = new TraktShowSync
            {  
                Shows = shows,
                UserName = TraktSettings.Username,
                Password = TraktSettings.Password
            };

            return syncData;
        }

        private void AddShowToWatchList(TraktCalendar.TraktEpisodes episode)
        {
            Thread syncThread = new Thread(delegate(object obj)
            {
                TraktAPI.TraktAPI.SyncShowWatchList(CreateShowSyncData(obj as TraktCalendar.TraktEpisodes), TraktSyncModes.watchlist);
            })
            {
                IsBackground = true,
                Name = "Adding Show to Watch List"
            };

            syncThread.Start(episode);
        }

        private void AddEpisodeToWatchList(TraktCalendar.TraktEpisodes episode)
        {
            Thread syncThread = new Thread(delegate(object obj)
            {
                TraktAPI.TraktAPI.SyncEpisodeWatchList(CreateEpisodeSyncData(obj as TraktCalendar.TraktEpisodes), TraktSyncModes.watchlist);
            })
            {
                IsBackground = true,
                Name = "Adding Episode to Watch List"
            };

            syncThread.Start(episode);
        }

        private void RemoveShowFromWatchList(TraktCalendar.TraktEpisodes episode)
        {
            Thread syncThread = new Thread(delegate(object obj)
            {
                TraktAPI.TraktAPI.SyncShowWatchList(CreateShowSyncData(obj as TraktCalendar.TraktEpisodes), TraktSyncModes.unwatchlist);
            })
            {
                IsBackground = true,
                Name = "Removing Show from Watch List"
            };

            syncThread.Start(episode);
        }

        private void RemoveEpisodeFromWatchList(TraktCalendar.TraktEpisodes episode)
        {
            Thread syncThread = new Thread(delegate(object obj)
            {
                TraktAPI.TraktAPI.SyncEpisodeWatchList(CreateEpisodeSyncData(obj as TraktCalendar.TraktEpisodes), TraktSyncModes.unwatchlist);
            })
            {
                IsBackground = true,
                Name = "Removing Episode from Watch List"
            };

            syncThread.Start(episode);
        }

        private void AddEpisodeToLibrary(TraktCalendar.TraktEpisodes episode)
        {
            Thread syncThread = new Thread(delegate(object obj)
            {
                TraktAPI.TraktAPI.SyncEpisodeLibrary(CreateEpisodeSyncData(obj as TraktCalendar.TraktEpisodes), TraktSyncModes.library);
            })
            {
                IsBackground = true,
                Name = "Add Episode to Library"
            };

            syncThread.Start(episode);
        }

        private void RemoveEpisodeFromLibrary(TraktCalendar.TraktEpisodes episode)
        {
            Thread syncThread = new Thread(delegate(object obj)
            {
                TraktAPI.TraktAPI.SyncEpisodeLibrary(CreateEpisodeSyncData(obj as TraktCalendar.TraktEpisodes), TraktSyncModes.unlibrary);
            })
            {
                IsBackground = true,
                Name = "Remove Episode From Library"
            };

            syncThread.Start(episode);
        }

        private void MarkEpisodeAsWatched(TraktCalendar.TraktEpisodes episode)
        {
            Thread syncThread = new Thread(delegate(object obj)
            {
                TraktAPI.TraktAPI.SyncEpisodeLibrary(CreateEpisodeSyncData(obj as TraktCalendar.TraktEpisodes), TraktSyncModes.seen);
            })
            {
                IsBackground = true,
                Name = "Mark Episode as Watched"
            };

            syncThread.Start(episode);
        }

        private void RateEpisode(TraktCalendar.TraktEpisodes episode)
        {
            // default rating to love if not already set
            TraktRateEpisode rateObject = new TraktRateEpisode
            {
                SeriesID = episode.Show.Tvdb,
                Title = episode.Show.Title,
                Year = episode.Show.Year.ToString(),
                Episode = episode.Episode.Number.ToString(),
                Season = episode.Episode.Season.ToString(),
                Rating = episode.Episode.Rating,
                UserName = TraktSettings.Username,
                Password = TraktSettings.Password
            };

            string prevRating = episode.Episode.Rating;
            episode.Episode.Rating = GUIUtils.ShowRateDialog<TraktRateEpisode>(rateObject);

            // if previous rating not equal to current rating then 
            // update skin properties to reflect changes so we dont
            // need to re-request from server
            if (prevRating != episode.Episode.Rating)
            {
                if (prevRating == "false")
                {
                    episode.Episode.Ratings.Votes++;
                    if (episode.Episode.Rating == "love")
                        episode.Episode.Ratings.LovedCount++;
                    else
                        episode.Episode.Ratings.HatedCount++;
                }

                if (prevRating == "love")
                {
                    episode.Episode.Ratings.LovedCount--;
                    episode.Episode.Ratings.HatedCount++;
                }

                if (prevRating == "hate")
                {
                    episode.Episode.Ratings.LovedCount++;
                    episode.Episode.Ratings.HatedCount--;
                }

                episode.Episode.Ratings.Percentage = (int)Math.Round(100 * (episode.Episode.Ratings.LovedCount / (float)episode.Episode.Ratings.Votes));
            }
        }

        private void CheckAndPlayEpisode()
        {
            GUIListItem selectedItem = Facade.SelectedListItem as GUIListItem;
            if (selectedItem == null) return;

            var episode = selectedItem.TVTag as TraktCalendar.TraktEpisodes;
            if (episode == null) return;

            int seriesid = Convert.ToInt32(episode.Show.Tvdb);
            int seasonidx = episode.Episode.Season;
            int episodeidx = episode.Episode.Number;

            GUICommon.CheckAndPlayEpisode(seriesid, seasonidx, episodeidx);
        }

        #if MP12
        private void ShowTrailersMenu(TraktCalendar.TraktEpisodes episodeItem)
        {
            IDialogbox dlg = (IDialogbox)GUIWindowManager.GetWindow((int)GUIWindow.Window.WINDOW_DIALOG_MENU);
            dlg.Reset();
            dlg.SetHeading(Translation.Trailer);

            foreach (TrailerSite site in Enum.GetValues(typeof(TrailerSite)))
            {
                string menuItem = Enum.GetName(typeof(TrailerSite), site);
                GUIListItem pItem = new GUIListItem(menuItem);
                dlg.Add(pItem);
            }

            dlg.DoModal(GUIWindowManager.ActiveWindow);

            if (dlg.SelectedLabel >= 0)
            {
                string siteUtil = string.Empty;
                string searchParam = string.Empty;

                switch (dlg.SelectedLabelText)
                {
                    case ("IMDb"):
                        siteUtil = "IMDb Movie Trailers";
                        if (!string.IsNullOrEmpty(episodeItem.Show.Imdb))
                            // Exact search
                            searchParam = episodeItem.Show.Imdb;
                        else
                            searchParam = episodeItem.Show.Title;
                        break;

                    case ("YouTube"):
                        siteUtil = "YouTube";
                        searchParam = episodeItem.Show.Title;
                        break;
                }

                string loadingParam = string.Format("site:{0}|search:{1}|return:Locked", siteUtil, searchParam);
                // Launch OnlineVideos Trailer search
                GUIWindowManager.ActivateWindow((int)ExternalPluginWindows.OnlineVideos, loadingParam);
            }
        }
        #endif

        private void ShowStartDateMenu()
        {
            IDialogbox dlg = (IDialogbox)GUIWindowManager.GetWindow((int)GUIWindow.Window.WINDOW_DIALOG_MENU);
            if (dlg == null) return;

            dlg.Reset();
            dlg.SetHeading(Translation.StartDate);

            foreach (int value in Enum.GetValues(typeof(StartDates)))
            {
                StartDates type = (StartDates)Enum.Parse(typeof(StartDates), value.ToString());
                string label = GetStartDateName(type);

                // Create new item
                GUIListItem listItem = new GUIListItem(label);
                listItem.ItemId = value;

                // Set selected if current
                if (type == CurrentStartDate) listItem.Selected = true;

                // Add new item to context menu
                dlg.Add(listItem);
            }

            dlg.DoModal(GUIWindowManager.ActiveWindow);
            if (dlg.SelectedId <= 0) return;

            // Set new Selection            
            CurrentStartDate = (StartDates)Enum.GetValues(typeof(StartDates)).GetValue(dlg.SelectedLabel);
            SetCurrentStartDate();

            // Reset Views and Apply
            CurrentWeekDays = 7;
            PreviousSelectedIndex = 0;
            PreviousCalendarDayCount = 0;
            _CalendarMyShows = null;
            _CalendarPremieres = null;
            _CalendarAllShows = null;

            LoadCalendar();
        }

        private void ShowViewMenu()
        {
            IDialogbox dlg = (IDialogbox)GUIWindowManager.GetWindow((int)GUIWindow.Window.WINDOW_DIALOG_MENU);
            if (dlg == null) return;

            dlg.Reset();
            dlg.SetHeading(Translation.Calendar);

            foreach (int value in Enum.GetValues(typeof(CalendarType)))
            {
                CalendarType type = (CalendarType)Enum.Parse(typeof(CalendarType), value.ToString());
                string label = GetCalendarTypeName(type);

                // Create new item
                GUIListItem listItem = new GUIListItem(label);
                listItem.ItemId = value;

                // Set selected if current
                if (type == CurrentCalendar) listItem.Selected = true;

                // Add new item to context menu
                dlg.Add(listItem);
            }

            dlg.DoModal(GUIWindowManager.ActiveWindow);
            if (dlg.SelectedId <= 0) return;

            // Set new Selection            
            CurrentCalendar = (CalendarType)Enum.GetValues(typeof(CalendarType)).GetValue(dlg.SelectedLabel);
            SetCurrentView();

            // Reset Views and Apply
            CurrentWeekDays = 7;
            PreviousSelectedIndex = 0;
            PreviousCalendarDayCount = 0;
            _CalendarMyShows = null;
            _CalendarPremieres = null;
            _CalendarAllShows = null;

            LoadCalendar();
        }

        private string GetStartDateName(StartDates date)
        {
            switch (date)
            {
                case StartDates.Today:
                    return Translation.DateToday;

                case StartDates.Yesterday:
                    return Translation.DateYesterday;

                case StartDates.OneWeekAgo:
                    return Translation.DateOneWeekAgo;

                case StartDates.TwoWeeksAgo:
                    return Translation.DateTwoWeeksAgo;

                case StartDates.OneMonthAgo:
                    return Translation.DateOneMonthAgo;

                default:
                    return Translation.DateToday;
            }
        }

        private string GetCalendarTypeName(CalendarType type)
        {
            switch (type)
            {
                case CalendarType.MyShows:
                    return Translation.CalendarMyShows;

                case CalendarType.Premieres:
                    return Translation.CalendarPremieres;

                case CalendarType.AllShows:
                    return Translation.CalendarAllShows;

                default:
                    return Translation.CalendarMyShows;
            }
        }

        private IEnumerable<TraktCalendar> GetCalendar()
        {
            GUIUtils.SetProperty("#Trakt.Items", string.Empty);

            switch (CurrentCalendar)
            {
                case CalendarType.MyShows:
                    return TraktCalendarMyShows;

                case CalendarType.Premieres:
                    return TraktCalendarPremieres;
     
                case CalendarType.AllShows:
                    return TraktCalendarAllShows;

                default:
                    return TraktCalendarMyShows;
            }
        }

        private void LoadCalendar()
        {
            GUIBackgroundTask.Instance.ExecuteInBackgroundAndCallback(() =>
            {
                return GetCalendar();
            },
            delegate(bool success, object result)
            {
                if (success)
                {
                    IEnumerable<TraktCalendar> calendar = result as IEnumerable<TraktCalendar>;
                    SendCalendarToFacade(calendar);
                }
            }, Translation.GettingCalendar, true);
        }

        private void SendCalendarToFacade(IEnumerable<TraktCalendar> calendar)
        {
            // check if we got a bad response
            if (calendar.Count() < PreviousCalendarDayCount)
            {
                GUIUtils.ShowNotifyDialog(GUIUtils.PluginName(), Translation.ErrorCalendar);
                // set defaults
                _CalendarMyShows = null;
                _CalendarPremieres = null;
                _CalendarAllShows = null;
                LastRequest = new DateTime();
                return;
            }

            // clear facade
            GUIControl.ClearControl(GetID, Facade.GetID);
           
            int itemCount = 0;
            List<TraktImage> showImages = new List<TraktImage>();

            // Add each days episodes to the list
            // Use Label3 of facade for Day/Group Idenitfier
            foreach (var day in calendar)
            {                 
                // apply watch list filter
                var episodes = day.Episodes;
                if (CurrentCalendar == CalendarType.MyShows)
                {
                    if (TraktSettings.CalendarHideTVShowsInWatchList)
                    {
                        episodes = day.Episodes.Where(e => !e.Show.InWatchList).ToList();
                    }
                }

                if (episodes.Count > 0)
                {
                    // add day header
                    GUIListItem item = new GUIListItem();
                    item.Label3 = DateTime.Parse(day.ToString()).ToLongDateString();
                    item.IconImage = "defaultTraktCalendar.png";
                    item.IconImageBig = "defaultTraktCalendarBig.png";
                    item.ThumbnailImage = "defaultTraktCalendarBig.png";
                    item.OnItemSelected += OnCalendarDateSelected;
                    Utils.SetDefaultIcons(item);
                    Facade.Add(item);
                 
                    foreach (var episode in episodes)
                    {
                        GUITraktCalendarListItem episodeItem = new GUITraktCalendarListItem(episode.ToString());

                        // add image for download
                        TraktImage images = new TraktImage
                        {
                            EpisodeImages = episode.Episode.Images,
                            ShowImages = episode.Show.Images
                        };
                        showImages.Add(images);

                        // extended skin properties
                        episode.Date = DateTime.Parse(day.ToString()).ToLongDateString();
                        episode.SelectedIndex = (itemCount + 1).ToString();

                        episodeItem.Item = images;
                        episodeItem.TVTag = episode;
                        episodeItem.ItemId = Int32.MaxValue - itemCount;
                        episodeItem.IconImage = "defaultTraktEpisode.png";
                        episodeItem.IconImageBig = "defaultTraktEpisodeBig.png";
                        episodeItem.ThumbnailImage = "defaultTraktEpisodeBig.png";
                        episodeItem.OnItemSelected += OnEpisodeSelected;
                        Utils.SetDefaultIcons(episodeItem);
                        Facade.Add(episodeItem);
                        itemCount++;
                    }
                }
            }

            // if nothing airing this week, then indicate to user
            if (!IsCached && (calendar.Count() == PreviousCalendarDayCount))
            {
                GUIListItem item = new GUIListItem();

                item.Label3 = Translation.NoEpisodesThisWeek;
                item.IconImage = "defaultTraktCalendar.png";
                item.IconImageBig = "defaultTraktCalendarBig.png";
                item.ThumbnailImage = "defaultTraktCalendarBig.png";
                item.OnItemSelected += OnCalendarDateSelected;
                Utils.SetDefaultIcons(item);
                Facade.Add(item);

                // Stay on Next Week Item
                if (PreviousSelectedIndex > 0)
                    PreviousSelectedIndex--;
            }

            // Add Next Week Item so user can fetch next weeks calendar
            GUIListItem nextItem = new GUIListItem(Translation.NextWeek);
            
            nextItem.IconImage = "traktNextWeek.png";
            nextItem.IconImageBig = "traktNextWeek.png";
            nextItem.ThumbnailImage = "traktNextWeek.png";
            nextItem.OnItemSelected += OnNextWeekSelected;
            nextItem.IsFolder = true;
            Facade.Add(nextItem);

            // Set Facade Layout
            Facade.SetCurrentLayout("List");
            GUIControl.FocusControl(GetID, Facade.GetID);

            // Set the first episode on calendar on initial request (Index 0 is a day header), 
            // Set last position if paging to next week
            if (!IsCached)
                Facade.SelectIndex(PreviousSelectedIndex + 1);
            else // If cached just set to last position
                Facade.SelectIndex(PreviousSelectedIndex);

            // set facade properties
            GUIUtils.SetProperty("#itemcount", itemCount.ToString());
            GUIUtils.SetProperty("#Trakt.Items", string.Format("{0} {1}", itemCount.ToString(), itemCount > 1 ? Translation.Episodes : Translation.Episode));

            // Download episode images Async and set to facade
            GetImages(showImages);
        }

        private void OnCalendarDateSelected(GUIListItem item, GUIControl parent)
        {
            // Skip over date to next/prev episode if a header (Date)
            if (LastMoved == FacadeMovement.Down)
            {
                Facade.OnAction(new Action(Action.ActionType.ACTION_MOVE_DOWN, 0, 0));
            }
            else
            {
                Facade.OnAction(new Action(Action.ActionType.ACTION_MOVE_UP, 0, 0));
                
                // if the current item is now the first item which is a header, then skip to end
                // we need to bypass the scroll delay so we are not stuck on the first item
                if (Facade.SelectedListItemIndex == 0)
                {
                    Facade.SelectedListItemIndex = Facade.Count - 1;
                }
            }
        }

        private void OnNextWeekSelected(GUIListItem item, GUIControl parent)
        {
            backdrop.Filename = string.Empty;
            ClearProperties();
        }

        private void OnEpisodeSelected(GUIListItem item, GUIControl parent)
        {
            TraktCalendar.TraktEpisodes episode = item.TVTag as TraktCalendar.TraktEpisodes;
            PublishEpisodeSkinProperties(episode);
            GUIImageHandler.LoadFanart(backdrop, episode.Show.Images.FanartImageFilename);
        }

        private void InitProperties()
        {
            // Fanart
            backdrop.GUIImageOne = FanartBackground;
            backdrop.GUIImageTwo = FanartBackground2;
            backdrop.LoadingImage = loadingImage;

            CurrentCalendar = (CalendarType)TraktSettings.DefaultCalendarView;
            SetCurrentView();

            CurrentStartDate = (StartDates)TraktSettings.DefaultCalendarStartDate;
            SetCurrentStartDate();

            // clear properties only if we need to
            if (LastRequest < DateTime.UtcNow.Subtract(new TimeSpan(0, TraktSettings.WebRequestCacheMinutes, 0)))
            {
                CurrentWeekDays = 7;
                PreviousSelectedIndex = 0;
                PreviousCalendarDayCount = 0;
                IsCached = false;
            }
            else // restore previous position on load
                IsCached = true;
        }

        /// <summary>
        /// Get number of days forward to request in calendar,
        /// takes into consideration the current anchor point
        /// </summary>
        private string GetDaysForward()
        {
            switch (CurrentStartDate)
            {
                case StartDates.Today:
                    return CurrentWeekDays.ToString();
                case StartDates.Yesterday:
                    return (CurrentWeekDays + 1).ToString();
                case StartDates.OneWeekAgo:
                    return (CurrentWeekDays + 7).ToString();
                case StartDates.TwoWeeksAgo:
                    return (CurrentWeekDays + 14).ToString();
                case StartDates.OneMonthAgo:
                    return (CurrentWeekDays + 31).ToString();
                default:
                    return CurrentWeekDays.ToString();
            }
        }

        /// <summary>
        /// Get Date Time for Calendar Anchor Point
        /// </summary>
        private DateTime GetStartDate()
        {
            switch (CurrentStartDate)
            {
                case StartDates.Today:
                    return DateTime.Now.Subtract(new TimeSpan(0, 0, 0, 0));
                case StartDates.Yesterday:
                    return DateTime.Now.Subtract(new TimeSpan(1, 0, 0, 0));
                case StartDates.OneWeekAgo:
                    return DateTime.Now.Subtract(new TimeSpan(7, 0, 0, 0));
                case StartDates.TwoWeeksAgo:
                    return DateTime.Now.Subtract(new TimeSpan(14, 0, 0, 0));
                case StartDates.OneMonthAgo:
                    return DateTime.Now.Subtract(new TimeSpan(31, 0, 0, 0));
                default:
                    return DateTime.Now.Subtract(new TimeSpan(0, 0, 0, 0));
            }
        }

        private void SetCurrentStartDate()
        {
            // Set current start date in button label
            if (startDateButton != null)
                startDateButton.Label = Translation.StartDate + ": " + GetStartDateName(CurrentStartDate);
        }

        private void SetCurrentView()
        {
            // Set current view in button label
            if (viewButton != null)
                viewButton.Label = Translation.View + ": " + GetCalendarTypeName(CurrentCalendar);

            SetProperty("#Trakt.Calendar.Type", CurrentCalendar.ToString());
            switch (CurrentCalendar)
            {
                case CalendarType.MyShows:
                    SetProperty("#Trakt.CurrentView", Translation.CalendarMyShows);
                    break;
                case CalendarType.Premieres:
                    SetProperty("#Trakt.CurrentView", Translation.CalendarPremieres);
                    break;
                case CalendarType.AllShows:
                    SetProperty("#Trakt.CurrentView", Translation.CalendarAllShows);
                    break;
            }            
        }

        private void SetProperty(string property, string value)
        {
            string propertyValue = string.IsNullOrEmpty(value) ? "N/A" : value;
            GUIUtils.SetProperty(property, propertyValue);
        }

        private void ClearProperties()
        {
            GUIUtils.SetProperty("#Trakt.Calendar.Selected.Date", string.Empty);
            GUIUtils.SetProperty("#selectedindex", string.Empty);

            GUIUtils.SetProperty("#Trakt.Show.Imdb", string.Empty);
            GUIUtils.SetProperty("#Trakt.Show.Tvdb", string.Empty);
            GUIUtils.SetProperty("#Trakt.Show.TvRage", string.Empty);
            GUIUtils.SetProperty("#Trakt.Show.Title", string.Empty);
            GUIUtils.SetProperty("#Trakt.Show.Url", string.Empty);
            GUIUtils.SetProperty("#Trakt.Show.AirDay", string.Empty);
            GUIUtils.SetProperty("#Trakt.Show.AirTime", string.Empty);
            GUIUtils.SetProperty("#Trakt.Show.Certification", string.Empty);
            GUIUtils.SetProperty("#Trakt.Show.Country", string.Empty);
            GUIUtils.SetProperty("#Trakt.Show.FirstAired", string.Empty);
            GUIUtils.SetProperty("#Trakt.Show.Network", string.Empty);
            GUIUtils.SetProperty("#Trakt.Show.Overview", string.Empty);
            GUIUtils.SetProperty("#Trakt.Show.Runtime", string.Empty);
            GUIUtils.SetProperty("#Trakt.Show.Year", string.Empty);
            GUIUtils.SetProperty("#Trakt.Show.InWatchList", string.Empty);
            GUIUtils.SetProperty("#Trakt.Show.Rating", string.Empty);
            GUIUtils.SetProperty("#Trakt.Show.Ratings.Icon", string.Empty);
            GUIUtils.SetProperty("#Trakt.Show.Ratings.HatedCount", string.Empty);
            GUIUtils.SetProperty("#Trakt.Show.Ratings.LovedCount", string.Empty);
            GUIUtils.SetProperty("#Trakt.Show.Ratings.Percentage", string.Empty);
            GUIUtils.SetProperty("#Trakt.Show.Ratings.Votes", string.Empty);
            GUIUtils.SetProperty("#Trakt.Show.FanartImageFilename", string.Empty);
            GUIUtils.SetProperty("#Trakt.Episode.Number", string.Empty);
            GUIUtils.SetProperty("#Trakt.Episode.Season", string.Empty);
            GUIUtils.SetProperty("#Trakt.Episode.FirstAired", string.Empty);
            GUIUtils.SetProperty("#Trakt.Episode.Title", string.Empty);
            GUIUtils.SetProperty("#Trakt.Episode.Url", string.Empty);
            GUIUtils.SetProperty("#Trakt.Episode.Overview", string.Empty);
            GUIUtils.SetProperty("#Trakt.Episode.Runtime", string.Empty);
            GUIUtils.SetProperty("#Trakt.Episode.InWatchList", string.Empty);
            GUIUtils.SetProperty("#Trakt.Episode.InCollection", string.Empty);
            GUIUtils.SetProperty("#Trakt.Episode.Plays", string.Empty);
            GUIUtils.SetProperty("#Trakt.Episode.Watched", string.Empty);
            GUIUtils.SetProperty("#Trakt.Episode.Rating", string.Empty);
            GUIUtils.SetProperty("#Trakt.Episode.Ratings.Icon", string.Empty);
            GUIUtils.SetProperty("#Trakt.Episode.Ratings.HatedCount", string.Empty);
            GUIUtils.SetProperty("#Trakt.Episode.Ratings.LovedCount", string.Empty);
            GUIUtils.SetProperty("#Trakt.Episode.Ratings.Percentage", string.Empty);
            GUIUtils.SetProperty("#Trakt.Episode.Ratings.Votes", string.Empty);
            GUIUtils.SetProperty("#Trakt.Episode.EpisodeImageFilename", string.Empty);
        }

        private void PublishEpisodeSkinProperties(TraktCalendar.TraktEpisodes episode)
        {
            SetProperty("#Trakt.Calendar.Selected.Date", episode.Date);
            SetProperty("#selectedindex", episode.SelectedIndex);

            SetProperty("#Trakt.Show.Imdb", episode.Show.Imdb);
            SetProperty("#Trakt.Show.Tvdb", episode.Show.Tvdb);
            SetProperty("#Trakt.Show.TvRage", episode.Show.TvRage);
            SetProperty("#Trakt.Show.Title", episode.Show.Title);
            SetProperty("#Trakt.Show.Url", episode.Show.Url);
            SetProperty("#Trakt.Show.AirDay", episode.Show.AirDay);
            SetProperty("#Trakt.Show.AirTime", episode.Show.AirTime);
            SetProperty("#Trakt.Show.Certification", episode.Show.Certification);
            SetProperty("#Trakt.Show.Country", episode.Show.Country);
            SetProperty("#Trakt.Show.FirstAired", episode.Show.FirstAired.FromEpoch().ToShortDateString());
            SetProperty("#Trakt.Show.Network", episode.Show.Network);
            SetProperty("#Trakt.Show.Overview", string.IsNullOrEmpty(episode.Show.Overview) ? Translation.NoShowSummary : episode.Show.Overview);
            SetProperty("#Trakt.Show.Runtime", episode.Show.Runtime.ToString());
            SetProperty("#Trakt.Show.Year", episode.Show.Year.ToString());
            SetProperty("#Trakt.Show.InWatchList", episode.Show.InWatchList.ToString());
            SetProperty("#Trakt.Show.Rating", episode.Show.Rating);
            SetProperty("#Trakt.Show.Ratings.Icon", (episode.Show.Ratings.LovedCount > episode.Show.Ratings.HatedCount) ? "love" : "hate");
            SetProperty("#Trakt.Show.Ratings.HatedCount", episode.Show.Ratings.HatedCount.ToString());
            SetProperty("#Trakt.Show.Ratings.LovedCount", episode.Show.Ratings.LovedCount.ToString());
            SetProperty("#Trakt.Show.Ratings.Percentage", episode.Show.Ratings.Percentage.ToString());
            SetProperty("#Trakt.Show.Ratings.Votes", episode.Show.Ratings.Votes.ToString());
            SetProperty("#Trakt.Show.FanartImageFilename", episode.Show.Images.FanartImageFilename);
            SetProperty("#Trakt.Episode.Number", episode.Episode.Number.ToString());
            SetProperty("#Trakt.Episode.Season", episode.Episode.Season.ToString());
            SetProperty("#Trakt.Episode.FirstAired", episode.Episode.FirstAired.FromEpoch().ToShortDateString());
            SetProperty("#Trakt.Episode.Title", string.IsNullOrEmpty(episode.Episode.Title) ? string.Format("{0} {1}", Translation.Episode, episode.Episode.Number.ToString()) : episode.Episode.Title);
            SetProperty("#Trakt.Episode.Url", episode.Episode.Url);
            SetProperty("#Trakt.Episode.Overview", string.IsNullOrEmpty(episode.Episode.Overview) ? Translation.NoEpisodeSummary : episode.Episode.Overview);
            SetProperty("#Trakt.Episode.Runtime", episode.Episode.Runtime.ToString());
            SetProperty("#Trakt.Episode.InWatchList", episode.Episode.InWatchList.ToString());
            SetProperty("#Trakt.Episode.InCollection", episode.Episode.InCollection.ToString());
            SetProperty("#Trakt.Episode.Plays", episode.Episode.Plays.ToString());
            SetProperty("#Trakt.Episode.Watched", (episode.Episode.Plays > 0).ToString());
            SetProperty("#Trakt.Episode.Rating", episode.Episode.Rating);
            SetProperty("#Trakt.Episode.Ratings.Icon", (episode.Episode.Ratings.LovedCount > episode.Episode.Ratings.HatedCount) ? "love" : "hate");
            SetProperty("#Trakt.Episode.Ratings.HatedCount", episode.Episode.Ratings.HatedCount.ToString());
            SetProperty("#Trakt.Episode.Ratings.LovedCount", episode.Episode.Ratings.LovedCount.ToString());
            SetProperty("#Trakt.Episode.Ratings.Percentage", episode.Episode.Ratings.Percentage.ToString());
            SetProperty("#Trakt.Episode.Ratings.Votes", episode.Episode.Ratings.Votes.ToString());
            SetProperty("#Trakt.Episode.EpisodeImageFilename", episode.Episode.Images.EpisodeImageFilename);
        }

        private void GetImages(List<TraktImage> itemsWithThumbs)
        {
            StopDownload = false;

            // split the downloads in 5+ groups and do multithreaded downloading
            int groupSize = (int)Math.Max(1, Math.Floor((double)itemsWithThumbs.Count / 5));
            int groups = (int)Math.Ceiling((double)itemsWithThumbs.Count() / groupSize);

            for (int i = 0; i < groups; i++)
            {
                List<TraktImage> groupList = new List<TraktImage>();
                for (int j = groupSize * i; j < groupSize * i + (groupSize * (i + 1) > itemsWithThumbs.Count ? itemsWithThumbs.Count - groupSize * i : groupSize); j++)
                {
                    groupList.Add(itemsWithThumbs[j]);
                }

                new Thread(delegate(object o)
                {
                    List<TraktImage> items = (List<TraktImage>)o;
                    foreach (TraktImage item in items)
                    {
                        #region Episode Image
                        // stop download if we have exited window
                        if (StopDownload) break;

                        string remoteThumb = item.EpisodeImages.Screen;
                        string localThumb = item.EpisodeImages.EpisodeImageFilename;

                        if (!string.IsNullOrEmpty(remoteThumb) && !string.IsNullOrEmpty(localThumb))
                        {
                            if (GUIImageHandler.DownloadImage(remoteThumb, localThumb))
                            {
                                // notify that image has been downloaded
                                item.NotifyPropertyChanged("EpisodeImages");
                            }
                        }
                        #endregion

                        #region Fanart
                        // stop download if we have exited window
                        if (StopDownload) break;
                        if (!TraktSettings.DownloadFanart) continue;

                        string remoteFanart = item.ShowImages.Fanart;
                        string localFanart = item.ShowImages.FanartImageFilename;

                        if (!string.IsNullOrEmpty(remoteFanart) && !string.IsNullOrEmpty(localFanart))
                        {
                            if (GUIImageHandler.DownloadImage(remoteFanart, localFanart))
                            {
                                // notify that image has been downloaded
                                item.NotifyPropertyChanged("ShowImages");
                            }
                        }
                        #endregion
                    }
                    #if !MP12
                    // refresh the facade so thumbnails get displayed
                    // this is not needed in MP 1.2 Beta
                    GUIWindowManager.SendThreadMessage(new GUIMessage(GUIMessage.MessageType.GUI_MSG_REFRESH, GUIWindowManager.ActiveWindow, 0, 50, 0, 0, null));
                    #endif
                })
                {
                    IsBackground = true,
                    Name = "Trakt Episode Image Downloader " + i.ToString()
                }.Start(groupList);
            }
        }

        #endregion

        #region Public Methods

        public static void ClearCache()
        {
            _CalendarMyShows = null;
        }

        #endregion
    }

    public class TraktImage : INotifyPropertyChanged
    {
        public TraktEpisode.ShowImages EpisodeImages { get; set; }
        public TraktShow.ShowImages ShowImages { get; set; }

        public event PropertyChangedEventHandler PropertyChanged;
        public void NotifyPropertyChanged(string propertyName)
        {
            if (PropertyChanged != null)
                PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
        }

    }

    public class GUITraktCalendarListItem : GUIListItem
    {
        public GUITraktCalendarListItem(string strLabel) : base(strLabel) { }

        public object Item
        {
            get { return _Item; }
            set
            {
                _Item = value;
                INotifyPropertyChanged notifier = value as INotifyPropertyChanged;
                if (notifier != null) notifier.PropertyChanged += (s, e) =>
                {
                    if (s is TraktImage && e.PropertyName == "EpisodeImages")
                        SetImageToGui((s as TraktImage).EpisodeImages.EpisodeImageFilename);
                    if (s is TraktImage && e.PropertyName == "ShowImages")
                        UpdateCurrentSelection();
                };
            }
        } protected object _Item;

        /// <summary>
        /// Loads an Image from memory into a facade item
        /// </summary>
        /// <param name="imageFilePath">Filename of image</param>
        protected void SetImageToGui(string imageFilePath)
        {
            if (string.IsNullOrEmpty(imageFilePath)) return;

            // determine the overlay to add to poster
            TraktCalendar.TraktEpisodes episode = TVTag as TraktCalendar.TraktEpisodes;
            MainOverlayImage mainOverlay = MainOverlayImage.None;

            if (episode.Episode.InWatchList)
                mainOverlay = MainOverlayImage.Watchlist;
            else if (episode.Episode.Plays > 0)
                mainOverlay = MainOverlayImage.Seenit;

            // add additional overlay if applicable
            if (episode.Episode.InCollection)
                mainOverlay |= MainOverlayImage.Library;

            RatingOverlayImage ratingOverlay = RatingOverlayImage.None;

            if (episode.Episode.Rating == "love")
                ratingOverlay = RatingOverlayImage.Love;
            else if (episode.Episode.Rating == "hate")
                ratingOverlay = RatingOverlayImage.Hate;

            // get a reference to a MediaPortal Texture Identifier
            string suffix = mainOverlay.ToString().Replace(", ", string.Empty) + Enum.GetName(typeof(RatingOverlayImage), ratingOverlay);
            string texture = GUIImageHandler.GetTextureIdentFromFile(imageFilePath, suffix);

            // build memory image, resize thumbnail incase its a fanart
            Image memoryImage = null;
            if (mainOverlay != MainOverlayImage.None || ratingOverlay != RatingOverlayImage.None)
            {
                memoryImage = GUIImageHandler.DrawOverlayOnEpisodeThumb(imageFilePath, mainOverlay, ratingOverlay, new Size(400, 225));
                if (memoryImage == null) return;

                // load texture into facade item
                if (GUITextureManager.LoadFromMemory(memoryImage, texture, 0, 0, 0) > 0)
                {
                    ThumbnailImage = texture;
                    IconImage = texture;
                    IconImageBig = texture;
                }
            }
            else
            {
                ThumbnailImage = imageFilePath;
                IconImage = imageFilePath;
                IconImageBig = imageFilePath;
            }

            // if selected and is current window force an update of thumbnail
            UpdateCurrentSelection();
        }

        protected void UpdateCurrentSelection()
        {
            GUICalendar window = GUIWindowManager.GetWindow(GUIWindowManager.ActiveWindow) as GUICalendar;
            if (window != null)
            {
                GUIListItem selectedItem = GUIControl.GetSelectedListItem(87259, 50);
                if (selectedItem == this)
                {
                    GUIWindowManager.SendThreadMessage(new GUIMessage(GUIMessage.MessageType.GUI_MSG_ITEM_SELECT, GUIWindowManager.ActiveWindow, 0, 50, ItemId, 0, null));
                }
            }
        }
    }
}
