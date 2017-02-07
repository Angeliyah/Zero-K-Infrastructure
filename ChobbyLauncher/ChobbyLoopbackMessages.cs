﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GameAnalyticsSDK.Net;

namespace ChobbyLauncher
{
    public class ChobbyMessageAttribute : Attribute {}
    
    [ChobbyMessage]
    public class OpenUrl
    {
        public string Url { get; set; }
    }

    [ChobbyMessage]
    public class OpenFolder
    {
        public string Folder { get; set; }
    }

    /// <summary>
    /// Restarts wrapper 
    /// </summary>
    [ChobbyMessage]
    public class Restart
    {
    }

    /// <summary>
    /// Flashes spring window
    /// </summary>
    [ChobbyMessage]
    public class Alert
    {
        public string Message { get; set; }
    }

    /// <summary>
    /// Sets text to speech volume
    /// </summary>
    [ChobbyMessage]
    public class TtsVolume
    {
        /// <summary>
        /// Min 0, Max 1
        /// </summary>
        public double Volume { get; set; }
    }

    /// <summary>
    /// Say a text. Name is used as a hint (hashed) for picking a voice
    /// </summary>
    [ChobbyMessage]
    public class TtsSay
    {
        public string Name {get; set; }
        public string Text { get; set; }
    }

    [ChobbyMessage]
    public class DownloadFile
    {
        public string FileType { get; set; }
        public string Name { get; set; }
    }

    [ChobbyMessage]
    public class DownloadFileDone
    {
        public string FileType { get; set; }
        public string Name { get; set; }
        public bool IsSuccess { get; set; }
    }


    [ChobbyMessage]
    public class SteamOnline
    {
        public string AuthToken { get; set; }
        public List<string> Friends { get; set; }
        public string FriendSteamID { get; set; }
        public string SuggestedName { get; set; }
    }


    [ChobbyMessage]
    public class SteamJoinFriend
    {
        public string FriendSteamID { get; set; }
    }


    [ChobbyMessage]
    public class SteamOpenOverlaySection
    {
        public SteamClientHelper.OverlayOption? Option { get; set; } = SteamClientHelper.OverlayOption.LobbyInvite;
    }


    [ChobbyMessage]
    public class SteamOpenOverlayWebsite
    {
        public string Url { get; set; }
    }

    [ChobbyMessage]
    public class SteamInviteFriendToGame
    {
        public string SteamID { get; set; }
    }


    [ChobbyMessage]
    public class SteamOverlayChanged
    {
        public bool IsActive { get; set; }
    }

    [ChobbyMessage]
    public class GaAddErrorEvent
    {
        public string Message { get; set; }

        public EGAErrorSeverity Severity { get; set; }
    }


    [ChobbyMessage]
    public class GaAddDesignEvent
    {
        public string EventID { get; set; }

        public double? Value { get; set; }
    }

    [ChobbyMessage]
    public class GaAddProgressionEvent
    {
        public EGAProgressionStatus Status { get; set; }

        public string Progression1 { get; set; }

        public string Progression2 { get; set; }

        public string Progression3 { get; set; }

        public double? Score { get; set; }
    }


}
