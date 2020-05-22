using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Runtime.Serialization.Formatters.Binary;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using DiscordRPC;
using DiscordRPC.Logging;

namespace a2services
{
    class main
    {
        static void Main(string[] args) => new main().MainAsync().GetAwaiter().GetResult();

        private DiscordSocketClient Client;
        private CommandService Commands;

        HoldServices Services = new HoldServices();

        private async Task MainAsync()
        {
            if(File.Exists("services.a2"))
            {
                Services = ((HoldServices)ByteArrayToObject(File.ReadAllBytes("services.a2")));
                foreach (var item in Services.services)
                {
                    item.isWorking = false;
                }
            }

            Client = new DiscordSocketClient(new DiscordSocketConfig
            {
                LogLevel = Discord.LogSeverity.Debug
            });

            Commands = new CommandService(new CommandServiceConfig
            {
                CaseSensitiveCommands = true,
                DefaultRunMode = RunMode.Async,
                LogLevel = Discord.LogSeverity.Debug
            });

            string token = "";
            await Client.LoginAsync(Discord.TokenType.Bot, token);

            await Client.SetActivityAsync(new Game("Managing services", ActivityType.CustomStatus));
            await Client.StartAsync();

            Client.MessageReceived += Client_MessageReceived;

            new Thread(new ThreadStart(checkServices)).Start();

            Console.WriteLine("a2ron.space bot ready :)");

            await Task.Delay(-1);
        }

        private async Task Client_MessageReceived(SocketMessage arg)
        {
            if (arg.Author.Id == 713204556020056136)
                return;

            List<string> carg = new List<string>();
            using (StringReader reader = new StringReader(arg.Content.Replace(" ", "\n")))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    carg.Add(line);
                }
            }

            if(arg.Content == "!cmd")
            {
                await arg.Channel.SendMessageAsync(arg.Author.Mention + " La syntaxe attendu pour la commande : `!s _nom du service_ _statu_s_ _port enregistrement_ _code identification_`.");
                return;
            }

            if (arg.Content == "!services")
            {
                await arg.Channel.SendMessageAsync(arg.Author.Mention + " Le fichier `services.a2` contient " + Services.services.Count + " services.");
                return;
            }

            if (arg.Content == "!dlservices")
            {
                if(File.Exists("services.a2"))
                    await arg.Channel.SendFileAsync("services.a2");
                return;
            }

            if (arg.Content == "!help")
            {
                await arg.Channel.SendMessageAsync(arg.Author.Mention + " Voici tous les commandes :\n-s\n-cmd\n-services\n-dlservices\n-clear\n-source\n");
                return;
            }

            if(arg.Content == "!source")
            {
                await arg.Channel.SendMessageAsync(arg.Author.Mention + " Mon code source est disponible ici : https://github.com/space2a/a2ronbot");
                return;
            }

            if(arg.Author.Id == 215221044904984576)
            {
                if (carg[0] == "!clear")
                {
                    Console.WriteLine("HERE!!");
                    if (carg.Count != 2)
                    { await arg.Channel.SendMessageAsync(arg.Author.Mention + " Impossible de traiter votre demande, le nombre d'arguments attendu était de 1, il était ici de " + (carg.Count - 1) + "."); return; }
                    if (carg[1] == "CODE")
                    {
                        Services.services.Clear();
                        Services.services = new List<Service>();
                        File.WriteAllBytes("services.a2", ObjectToByteArray(Services)); //SAVING THE FILE
                        await arg.Channel.SendMessageAsync(arg.Author.Mention + " Le fichier `services.a2` et ma variable ont étés vidés.");
                    }
                    else { await arg.Channel.SendMessageAsync(arg.Author.Mention + " Impossible de traiter votre demande, le code d'identification n'était pas valide."); return; }

                    return;
                }

                if (carg[0] == "!s")
                {
                    
                    if (carg.Count != 5)
                    { await arg.Channel.SendMessageAsync(arg.Author.Mention + " Impossible de traiter votre demande, le nombre d'arguments attendu était de 4, il était ici de " + (carg.Count - 1) + "."); return; }
                    if (carg[4] != "CODE")
                    { await arg.Channel.SendMessageAsync(arg.Author.Mention + " Impossible de traiter votre demande, le code d'identification n'était pas valide."); return; }
                    string status = "";
                    uint color = 0x0;
                    Console.WriteLine(carg[3]);
                    if (carg[2] == "a") { status = "ajouté"; color = 0x6D12BE; }
                    else if (carg[2] == "s") { status = "supprimé"; color = 0xBE1212; }
                    else { await arg.Channel.SendMessageAsync(arg.Author.Mention + " Impossible de traiter votre demande, le status était invalid."); return; }

                    int port = 0;
                    if (!int.TryParse(carg[3], out port)) { await arg.Channel.SendMessageAsync(arg.Author.Mention + " Impossible de traiter votre demande, le port d'enregistrement était invalid."); return; }

                    var channel = Client.GetChannel() as IMessageChannel;

                    if (carg[2] == "s")
                    {
                        int index = 0;
                        for (int i = 0; i < Services.services.Count; i++)
                        {
                            if (Services.services[i].Name == carg[1].ToLower()) { index = i; break; }
                        }
                        Services.services.RemoveAt(0);
                    }
                    else
                    {
                        bool go = true;
                        for (int i = 0; i < Services.services.Count; i++)
                        {
                            if(Services.services[i].Name == carg[1].ToLower())
                            {
                                await arg.Channel.SendMessageAsync(arg.Author.Mention + " Impossible de traiter votre demande, ce service existe déjà."); go = false; break;
                            }
                        }
                        if (!go) { return; }
                        Services.services.Add(new Service() { Name = carg[1].ToLower(), port = port });
                    }
                    File.WriteAllBytes("services.a2", ObjectToByteArray(Services)); //SAVING THE FILE

                    
                    SendEmbedMessage(channel, carg[1].ToLower(), status, color);
                    return;
                }
            }

            if(arg.Content.StartsWith("!") && arg.Content.Length > 1)
                await arg.Channel.SendMessageAsync(arg.Author.Mention + " Impossible de traiter votre demande, commande inconnue.");
        }

        void SendEmbedMessage(IMessageChannel channel, string nom, string status, uint color)
        {
            var builder = new EmbedBuilder()
            .WithTitle("Information à propos d'un service")
            .WithDescription("Le service **" + nom + "**.a2ron.space à été " + status + ".")
            .WithUrl("https://discordapp.com")
            .WithColor(new Color(color))
            .WithTimestamp(DateTimeOffset.FromUnixTimeMilliseconds(1590112324527))
            .WithFooter(footer => {
                footer
                    .WithText("a2ron.space")
                    .WithIconUrl("https://cdn.discordapp.com/embed/avatars/0.png");
            })
            .WithThumbnailUrl("http://www.a2ron.space/bot.png");

            var embed = builder.Build();
            channel.SendMessageAsync(
                null,
                embed: embed)
                .ConfigureAwait(false);
        }

        void checkServices()
        {
            while (true)
            {
                for (int i = 0; i < Services.services.Count; i++)
                {
                    Service service = Services.services[i];

                    TcpClient client = null;
                    NetworkStream stream = null;
                    try
                    {

                        client = new TcpClient(service.Addr, service.port);

                        stream = client.GetStream();


                        byte[] data = new byte[255];
                        client.ReceiveTimeout = 5000;
                        if (client.Connected)
                        {
                            if (!service.isWorking)
                                SendEmbedMessage(Client.GetChannel() as IMessageChannel, service.Name, "démarré", 0x19C7E6);
                            service.isWorking = true;
                            client.Close();
                        }
                    }
                    catch (Exception)
                    {
                        if (service.isWorking)
                            SendEmbedMessage(Client.GetChannel() as IMessageChannel, service.Name, "stoppé", 0xBEBE12);
                        service.isWorking = false;
                    }
                }

                Thread.Sleep(5000);
            }

        }

        object ByteArrayToObject(byte[] arrBytes)
        {
            try
            {
                MemoryStream memStream = new MemoryStream();
                BinaryFormatter binForm = new BinaryFormatter();
                memStream.Write(arrBytes, 0, arrBytes.Length);
                memStream.Seek(0, SeekOrigin.Begin);
                object obj = (object)binForm.Deserialize(memStream);

                return obj;
            }
            catch (Exception) { return null; }
        }

        byte[] ObjectToByteArray(object obj)
        {
            try
            {
                if (obj == null)
                    return null;

                BinaryFormatter bf = new BinaryFormatter();
                MemoryStream ms = new MemoryStream();
                bf.Serialize(ms, obj);

                return ms.ToArray();
            }
            catch (Exception) { return null; }
        }

    }

    [Serializable]
    public class Service
    {
        public string Name = "";
        public string Addr = "";
        public int port = 0;
        public bool isWorking = false;
    }

    [Serializable]
    public class HoldServices
    {
        public List<Service> services = new List<Service>();
    }


}
