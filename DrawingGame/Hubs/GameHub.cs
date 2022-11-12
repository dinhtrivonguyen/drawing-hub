using Microsoft.AspNetCore.SignalR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DrawingGame.Hubs
{
    public class GameHub:Hub
    {
        static Dictionary<string, string> users = new Dictionary<string, string>();
        static Dictionary<string, string> guessed = new Dictionary<string, string>();
        static string drawer = string.Empty;
        public async Task SetProfile(string nickName)
        {
            lock (users)
            {
                if (!users.ContainsKey(this.Context.ConnectionId))
                    users.Add(this.Context.ConnectionId, nickName);
                else
                    users[this.Context.ConnectionId] = nickName;
            }
            await this.Clients.Others.SendAsync("usersChanged", users);
        }
        public Dictionary<string,string> GetUsers()
        {
            return users;
        }
        static Random r = new Random();
        static string[] Things = new string[] { "apple", "phone", "cucumber", "walking", "music", "laptop", "beer", "tree", "face mask", "table", "tablet", "projector", "running", "mountain", "lake", "water", "person", "cute", "dog", "cat", "lynx", "bear" };
        static List<string> ValidResponse = new List<string>();
        public async Task StartGame()
        {
            var rand = -1;
            lock (r)
            {
                rand = r.Next(0, users.Keys.Count);
            }
            var connectionId = users.Keys.ToArray()[rand];
            await this.Clients.All.SendAsync("clearChat");
            await this.Clients.All.SendAsync("clearCanvas");
            await this.Clients.Client(connectionId).SendAsync("draw!");
            drawer = users[connectionId];
            lock (r)
            {
                rand = r.Next(0, Things.Length);
            }
            var validThing = Things[rand];
            ValidResponse.Clear();
            ValidResponse.Add(validThing);
            ValidResponse.Add(validThing.ToLower());
            ValidResponse.Add(validThing.ToUpper());
            var NormalizedValidThing = validThing.RemoveDiacritics();
            ValidResponse.Add(NormalizedValidThing);
            ValidResponse.Add(NormalizedValidThing.ToLower());
            ValidResponse.Add(NormalizedValidThing.ToUpper());

            guessed = new Dictionary<string, string>();

            await this.Clients.Client(connectionId).SendAsync("chatMessage", $"Draw {validThing}");
            await this.Clients.AllExcept(connectionId).SendAsync("guess!");
            await this.Clients.AllExcept(connectionId).SendAsync("chatMessage", $"{drawer} is drawing!");
        }
        public async Task ProcessMessage(string msg)
        {
            if (ValidResponse.Contains(msg) && !guessed.ContainsKey(this.Context.ConnectionId))
            {
                await this.Clients.Caller.SendAsync("chatMessage", "You guessed right!");
                await this.Clients.Others.SendAsync("chatMessage", $"{users[this.Context.ConnectionId]} guessed right!");
                guessed.Add(this.Context.ConnectionId, users[this.Context.ConnectionId]);
                await this.Clients.All.SendAsync("guessed", $"{users[this.Context.ConnectionId]}");
                if(guessed.Count >= users.Count - 1)
                {
                    await this.Clients.All.SendAsync("chatMessage", "Everyone guessed right! New game starts in 5 seconds.");
                    Thread.Sleep(5000);
                    await this.Clients.All.SendAsync("restart", users);
                    await StartGame();
                }
            }
            else if (ValidResponse.Contains(msg))
            {
                return;
            }
            else
            {
                await this.Clients.All.SendAsync("chatMessage", $"{users[this.Context.ConnectionId]}: {msg}");
            }
        }
        public async Task SendLines(Point[] points)
        {
            await this.Clients.Others.SendAsync("drawLines", points);
        }
        public override Task OnDisconnectedAsync(Exception exception)
        {
            lock (users)
            {
                if (users.ContainsKey(this.Context.ConnectionId))
                    users.Remove(this.Context.ConnectionId);
            }
            return base.OnDisconnectedAsync(exception);
        }
    }
    public struct Point
    {
        public int X { get; set; }
        public int Y { get; set; }
    }
    public static class StringExtensionMethods //replace unnecessary characters
    {
        static Dictionary<char, char> replaceDict = new Dictionary<char, char>();

        static StringExtensionMethods()
        {
            var chars = "říšžťčýůňúěďáéó"; //příliš žluťoučký kůň úpěl ďábelské ódy
            var charsReplace = "risztcyunuedaeo";
            for (int i = 0; i < chars.Length; i++)
            {
                replaceDict.Add(chars[i], charsReplace[i]);
            }
        }

        public static string RemoveDiacritics(this string text)
        {
            StringBuilder sb = new StringBuilder(text);
            for (int i = 0; i < sb.Length; i++)
            {
                if (replaceDict.ContainsKey(sb[i]))
                {
                    sb[i] = replaceDict[sb[i]];
                }
            }
            return sb.ToString();
        }
    }
}
