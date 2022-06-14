using System;
using System.IO;
using System.Threading;
using Discord;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Calendar.v3;
using Google.Apis.Calendar.v3.Data;
using Google.Apis.Services;
using Google.Apis.Util.Store;

// https://discord.com/developers/docs/game-sdk/sdk-starter-guide

namespace GoogleCalendarDiscordRichPresence
{
    static class Program
    {
        /* Global instance of the scopes required by this quickstart.
         If modifying these scopes, delete your previously saved token.json/ folder. */
        static string[] Scopes = { CalendarService.Scope.CalendarReadonly };
        static string ApplicationName = "GoogleCalendarDiscordRichPresence";

        static Discord.Discord _discord;

        static void Main(string[] args)
        {
            Console.WriteLine("Hello World!");

            // Use your client ID from Discord's developer site.
            string clientId = Environment.GetEnvironmentVariable("DISCORD_CLIENT_ID");
            if (clientId == null)
            {
                clientId = "980199157388156938";
            }

            Console.WriteLine(clientId);
            _discord = new Discord.Discord(Int64.Parse(clientId), (UInt64)CreateFlags.Default);
            _discord.SetLogHook(LogLevel.Info, (level, message) => { Console.WriteLine("Log[{0}] {1}", level, message); });

            UserManager userManager = _discord.GetUserManager();
            userManager.OnCurrentUserUpdate += () => Console.WriteLine(userManager.GetCurrentUser().Username);

            Console.WriteLine($"Current locale: {_discord.GetApplicationManager().GetCurrentLocale()}");

            ActivityManager activityManager = _discord.GetActivityManager();

            Activity activity = new()
            {
                // Details = "details text",
                Timestamps =
                {
                    End = 6,
                },
                Instance = false,
            };

            Console.WriteLine(activity);

            activityManager.UpdateActivity(activity, result =>
            {
                Console.WriteLine("Update Activity {0}", result);

                Console.WriteLine(_discord.GetUserManager().GetCurrentUser().Username);
            });

            GoogleCalendarMain();

            while (true)
            {
                _discord.RunCallbacks();

                Thread.Sleep(50);
            }
        }

        // Based on https://developers.google.com/calendar/api/quickstart/dotnet
        static void GoogleCalendarMain()
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
                CalendarService service = new(new BaseClientService.Initializer
                {
                    HttpClientInitializer = credential,
                    ApplicationName = ApplicationName
                });

                // Define parameters of request.
                EventsResource.ListRequest request = service.Events.List("primary");
                request.TimeMin = DateTime.Now;
                request.ShowDeleted = false;
                request.SingleEvents = true;
                request.MaxResults = 10;
                request.OrderBy = EventsResource.ListRequest.OrderByEnum.StartTime;

                // List events.
                Events events = request.Execute();
                Console.WriteLine("Upcoming events:");
                if (events.Items == null || events.Items.Count == 0)
                {
                    Console.WriteLine("No upcoming events found.");
                    return;
                }

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
            catch (FileNotFoundException e)
            {
                Console.WriteLine(e.Message);
            }
        }
    }
}