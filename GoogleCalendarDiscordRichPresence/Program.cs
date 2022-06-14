using System;
using System.IO;
using System.Threading;
using Discord;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Calendar.v3;
using Google.Apis.Calendar.v3.Data;
using Google.Apis.Services;
using Google.Apis.Util.Store;

// References:
// https://discord.com/developers/docs/game-sdk/sdk-starter-guide
// https://developers.google.com/calendar/api/quickstart/dotnet

namespace GoogleCalendarDiscordRichPresence
{
    static class Program
    {
        static readonly Int64 _discordClientId = 980199157388156938;  // Use your client ID from Discord's developer site.
        
        /* Global instance of the scopes required by this quickstart.
         If modifying these scopes, delete your previously saved token.json/ folder. */
        static readonly string[] Scopes = { CalendarService.Scope.CalendarReadonly };
        static readonly string ApplicationName = "GoogleCalendarDiscordRichPresence";

        static Discord.Discord _discord;
        static ActivityManager _activityManager;

        static CalendarService _calendarService;

        static readonly int _runCallbacksIntervalMs = 50;
        static readonly int _refreshDiscordStatusIntervalMs = 60000;
        static int _refreshDiscordStatusElapsedMs = 0;

        static bool _hasDiscordInstance = false;
        
        static void Main(string[] args)
        {
            InitDiscordGameSdk();
            
            InitGoogleCalendarApi();

            try
            {
                while (true)
                {
                    if (_hasDiscordInstance)
                    {
                        _discord.RunCallbacks();
                    }
                    
                    _refreshDiscordStatusElapsedMs += _runCallbacksIntervalMs;

                    if (_refreshDiscordStatusElapsedMs >= _refreshDiscordStatusIntervalMs)
                    {
                        UpdateStatusFromCalendar();
                    
                        _refreshDiscordStatusElapsedMs = 0;
                    }
                
                    Thread.Sleep(_runCallbacksIntervalMs);
                }
            }
            catch (Exception e)
            {
                Console.Error.WriteLine(e);
            }
        }

        static void InitDiscordGameSdk()
        {
            _discord = new Discord.Discord(_discordClientId, (UInt64)CreateFlags.Default);
            _discord.SetLogHook(LogLevel.Info, (level, message) => { Console.WriteLine("Log[{0}] {1}", level, message); });
            
            _activityManager = _discord.GetActivityManager();

            _hasDiscordInstance = true;
        }

        static void InitGoogleCalendarApi()
        {
            try
            {
                UserCredential credential;
                // Load client secrets.
                using (FileStream stream = new("GoogleCalendarCredentials.json", FileMode.Open, FileAccess.Read))
                {
                    /* The file token.json stores the user's access and refresh tokens, and is created
                     automatically when the authorization flow completes for the first time. */
                    string credPath = "token.json";
                    credential = GoogleWebAuthorizationBroker.AuthorizeAsync(
                        GoogleClientSecrets.FromStream(stream).Secrets,
                        Scopes,
                        "user",
                        CancellationToken.None,
                        new FileDataStore(credPath, true)).Result;
                    Console.WriteLine("Credential file saved to: " + credPath);
                }

                // Create Google Calendar API service.
                _calendarService = new(new BaseClientService.Initializer
                {
                    HttpClientInitializer = credential,
                    ApplicationName = ApplicationName
                });
            }
            catch (FileNotFoundException e)
            {
                Console.Error.WriteLine(e.Message);
            }
        }
        
        static void UpdateStatusFromCalendar()
        {
            DateTime nextFreeTime = GetNextFreeTime();
            
            if (nextFreeTime == default)
            {
                if (_hasDiscordInstance)
                {
                    _discord.Dispose();
                    _hasDiscordInstance = false;
                }
            }
            else
            {
                if (!_hasDiscordInstance)
                {
                    InitDiscordGameSdk();
                }
                
                Activity activity = new()
                {
                    // Details = "details text",
                    Timestamps =
                    {
                        End = ((DateTimeOffset)nextFreeTime).ToUnixTimeSeconds(),
                    },
                    Instance = false,
                };
                
                _activityManager.UpdateActivity(activity, _ => { });
            }
        }

        static DateTime GetNextFreeTime()
        {
            // Define parameters of request.
            EventsResource.ListRequest request = _calendarService.Events.List("primary");
            request.TimeMin = DateTime.Now;
            request.SingleEvents = true;
            request.MaxResults = 10;
            request.OrderBy = EventsResource.ListRequest.OrderByEnum.StartTime;
            
            Events events = request.Execute();
            
            if (events.Items == null || events.Items.Count == 0)
            {
                return default;  // Calendar is empty, so user is not in a meeting
            }
            
            if (events.Items[0].Start.DateTime.HasValue && events.Items[0].Start.DateTime.Value > DateTime.Now)
            {
                return default;  // The next upcoming meeting hasn't started yet, so user is not in a meeting
            }
            
            DateTime soonestEndTime = events.Items[0].Start.DateTime.GetValueOrDefault();
            
            // If the end of one event is the same as the start time of the next event, then find final end time
            foreach (Event eventItem in events.Items)
            {
                if (eventItem.Start.DateTime.GetValueOrDefault() == soonestEndTime)
                {
                    soonestEndTime = eventItem.End.DateTime.GetValueOrDefault();
                }
                else
                {
                    break;
                }
            }

            return soonestEndTime;
        }
    }
}