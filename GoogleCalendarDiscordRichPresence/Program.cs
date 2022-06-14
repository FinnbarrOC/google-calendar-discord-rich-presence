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
        static Int64 DiscordClientId = 980199157388156938;  // Use your client ID from Discord's developer site.
        
        /* Global instance of the scopes required by this quickstart.
         If modifying these scopes, delete your previously saved token.json/ folder. */
        static string[] Scopes = { CalendarService.Scope.CalendarReadonly };
        static string ApplicationName = "GoogleCalendarDiscordRichPresence";

        static Discord.Discord _discord;
        static ActivityManager _activityManager;

        static CalendarService _calendarService;
        
        static void Main(string[] args)
        {
            InitDiscordGameSdk();
            
            InitGoogleCalendarApi();
            
            Timer checkCalendarTimer = new(_ => UpdateStatusFromCalendar(), null, 
                TimeSpan.Zero, TimeSpan.FromMinutes(1));

            while (true)
            {
                _discord.RunCallbacks();

                Thread.Sleep(50);
            }
        }

        static void InitDiscordGameSdk()
        {
            _discord = new Discord.Discord(DiscordClientId, (UInt64)CreateFlags.Default);
            _discord.SetLogHook(LogLevel.Info, (level, message) => { Console.WriteLine("Log[{0}] {1}", level, message); });
            
            _activityManager = _discord.GetActivityManager();
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
            Activity activity = new()
            {
                // Details = "details text",
                Timestamps =
                {
                    End = ((DateTimeOffset)GetNextFreeTime()).ToUnixTimeSeconds(),
                },
                Instance = false,
            };
            
            _activityManager.UpdateActivity(activity, result =>
            {
                Console.WriteLine("Update Activity {0}", result);
            });
        }

        static DateTime GetNextFreeTime()
        {
            // Define parameters of request.
            EventsResource.ListRequest request = _calendarService.Events.List("primary");
            request.TimeMin = DateTime.Now;
            request.ShowDeleted = false;
            request.SingleEvents = true;
            request.MaxResults = 10;
            request.OrderBy = EventsResource.ListRequest.OrderByEnum.StartTime;
            
            Events events = request.Execute();
            
            if (events.Items == null || events.Items.Count == 0)
            {
                return DateTime.UtcNow;
            }
            
            // TODO: if the next meeting hasn't started yet, don't mark user as in a meeting
            // TODO: go through all meetings and find end of all meetings if they flow into each other

            return events.Items[0].End.DateTime.GetValueOrDefault();

            foreach (Event eventItem in events.Items)
            {
                string when = eventItem.Start.DateTime.ToString();
                if (String.IsNullOrEmpty(when))
                {
                    when = eventItem.Start.Date;
                }

                Console.WriteLine("{0} ({1})", eventItem.Summary, when);
            }
        }
    }
}