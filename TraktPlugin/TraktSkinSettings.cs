﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;
using MediaPortal.GUI.Library;

namespace TraktPlugin.GUI
{
    static class TraktSkinSettings
    {
        public static string CurrentSkin { get { return GUIGraphicsContext.Skin; } }
        public static string PreviousSkin { get; set; }

        public static int PosterMainOverlayPosX { get; set; }
        public static int PosterMainOverlayPosY { get; set; }
        public static int PosterRatingOverlayPosX { get; set; }
        public static int PosterRatingOverlayPosY { get; set; }

        public static int EpisodeThumbMainOverlayPosX { get; set; }
        public static int EpisodeThumbMainOverlayPosY { get; set; }
        public static int EpisodeThumbRatingOverlayPosX { get; set; }
        public static int EpisodeThumbRatingOverlayPosY { get; set; }

        public static void Init()
        {
            // Import Skin Settings
            string xmlSkinSettings = GUIGraphicsContext.Skin + @"\Trakt.SkinSettings.xml";
            Load(xmlSkinSettings);

            // Remember last skin used incase we need to reload
            PreviousSkin = CurrentSkin;
        }

        /// <summary>
        /// Reads all Skin Settings
        /// </summary>
        /// <param name="filename"></param>
        public static void Load(string filename)
        {
            // Check if File Exist
            if (!System.IO.File.Exists(filename))
            {
                TraktLogger.Warning("Trakt Skin Settings does not exist!");
                return;
            }

            XmlDocument doc = new XmlDocument();
            try
            {
                doc.Load(filename);
            }
            catch (XmlException e)
            {
                TraktLogger.Error("Cannot Load skin settings xml file!: {0}", e.Message);                
                return;
            }

            // Read and Import Skin Settings            
            GetOverlayPositions(doc);            
        }

        /// <summary>
        /// Get Position of overlays to add on posters in thumbs
        /// </summary>
        /// <param name="doc"></param>
        private static void GetOverlayPositions(XmlDocument doc)
        {
            TraktLogger.Info("Loading Settings for Overlay positions");

            int posx = 0;
            int posy = 0;

            // Load Main Overlay Positions
            XmlNode node = null;
            PosterMainOverlayPosX = 178;
            node = doc.DocumentElement.SelectSingleNode("/settings/mainoverlayicons/posters/posx");
            if (node != null)
            {
                int.TryParse(node.InnerText, out posx);
                PosterMainOverlayPosX = posx;
            }
            PosterMainOverlayPosY = 0;
            node = doc.DocumentElement.SelectSingleNode("/settings/mainoverlayicons/posters/posy");
            if (node != null)
            {
                int.TryParse(node.InnerText, out posy);
                PosterMainOverlayPosY = posy;
            }

            node = null;
            EpisodeThumbMainOverlayPosX = 278;
            node = doc.DocumentElement.SelectSingleNode("/settings/mainoverlayicons/episodethumbs/posx");
            if (node != null)
            {
                int.TryParse(node.InnerText, out posx);
                EpisodeThumbMainOverlayPosX = posx;
            }
            EpisodeThumbMainOverlayPosY = 0;
            node = doc.DocumentElement.SelectSingleNode("/settings/mainoverlayicons/episodethumbs/posy");
            if (node != null)
            {
                int.TryParse(node.InnerText, out posy);
                EpisodeThumbMainOverlayPosY = posy;
            }

            // Load Rating Overlay Positions
            PosterRatingOverlayPosX = 178;
            node = doc.DocumentElement.SelectSingleNode("/settings/ratingoverlayicons/posters/posx");
            if (node != null)
            {
                int.TryParse(node.InnerText, out posx);
                PosterRatingOverlayPosX = posx;
            }
            PosterRatingOverlayPosY = 0;
            node = doc.DocumentElement.SelectSingleNode("/settings/ratingoverlayicons/posters/posy");
            if (node != null)
            {
                int.TryParse(node.InnerText, out posy);
                PosterRatingOverlayPosY = posy;
            }

            EpisodeThumbRatingOverlayPosX = 278;
            node = doc.DocumentElement.SelectSingleNode("/settings/ratingoverlayicons/episodethumbs/posx");
            if (node != null)
            {
                int.TryParse(node.InnerText, out posx);
                EpisodeThumbRatingOverlayPosX = posx;
            }
            EpisodeThumbRatingOverlayPosY = 0;
            node = doc.DocumentElement.SelectSingleNode("/settings/ratingoverlayicons/episodethumbs/posy");
            if (node != null)
            {
                int.TryParse(node.InnerText, out posy);
                EpisodeThumbRatingOverlayPosY = posy;
            }
        }

    }
}
