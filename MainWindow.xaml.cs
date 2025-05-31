using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using MapleRoll_Server_UI_.Net.IO;
using System.Runtime.CompilerServices;
using System.Windows.Interop;
using System.Text.Json;
using System.Net.WebSockets;
using System.Configuration;

namespace MapleRoll_Server_UI_
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private HttpListener _webSocketListener;
        public static bool messageAll = false;
        public static bool messageGroup = false;
        public static bool messageUser = true;
        public static bool sendStartUpMessage = false;
        public string startUpMessage;

        public static List<Client> users = new List<Client>();
        public static List<Group> groups = new List<Group>();
        static TcpListener listener;
        private static int port = int.Parse( ConfigurationManager.AppSettings["Port"]);
        public string TestIPaddress = ConfigurationManager.AppSettings["TestIPAddress"];
        public static MainWindow mainWindow;
        public string IPaddress = ConfigurationManager.AppSettings["IPAddress"];
        public Client selectedUser;
        public Group selectedGroup;


        //public static MainWindow mainWindow;
        public MainWindow()
        {
            InitializeComponent();
            Loaded += Window_Loaded;

        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            mainWindow = this;
            StartServer();
            
        }
        private void StartServer()
        {

            try
            {
                users = new List<Client>();
                _webSocketListener = new HttpListener();
                _webSocketListener.Prefixes.Add($"{IPaddress}:{port}/"); 
                _webSocketListener.Start();
                Task.Run(() => AcceptWebSocketClients());
                string serverEndpoint = GetServerEndpoint(port); 
                Application.Current.Dispatcher.Invoke(() => mainWindow.SendMessageToConsole($"[{DateTime.Now}]: MapleRoll WebSocket Server is running at {serverEndpoint}.", 1));
            }
            catch (HttpListenerException ex)
            {
                Application.Current.Dispatcher.Invoke(() =>
                    mainWindow.SendMessageToConsole($"[{DateTime.Now}]: Failed to start. Reason: {ex.Message}. Ensure the application is running with sufficient privileges.", 20));
            }
            catch (Exception ex)
            {
                mainWindow.SendMessageToConsole($"[{DateTime.Now}]: Unexpected error: {ex.Message}", 20);
            }


        }
        public static string GetServerEndpoint(int port)
        {
            // Get all local IP addresses
            string[] localIPs = Dns.GetHostAddresses(Dns.GetHostName())
                                   .Where(ip => ip.AddressFamily == AddressFamily.InterNetwork)
                                   .Select(ip => ip.ToString())
                                   .ToArray();

            // Include localhost and any resolved IPs
            string endpoint = string.Join(", ", localIPs.Select(ip => $"ws://{ip}:{port}"));
           

            return endpoint;
        }
    
        private async Task AcceptWebSocketClients()
        {
            while (true)
            {
                var context = await _webSocketListener.GetContextAsync();
                if (context.Request.IsWebSocketRequest)
                {
                    var wsContext = await context.AcceptWebSocketAsync(null);
                    string remoteEndPoint = context.Request.RemoteEndPoint?.ToString() ?? "Unknown";
                    var client = new Client(wsContext.WebSocket, remoteEndPoint);
                    MainWindow.users.Add(client); // Use the static reference to users
                    MainWindow.mainWindow.SendMessageToConsole($"[{DateTime.Now}]: New WebSocket client connected from {remoteEndPoint}.", 1); // Correct reference
                    _ = client.Process(); // Handle in background
                }
                else
                {
                    context.Response.StatusCode = 400;
                    context.Response.Close();
                }
            }
        }

        private async Task Server()
        {
            try
            {
                while (true)
                {
                    try
                    {
                        var context = await _webSocketListener.GetContextAsync();
                        if (context.Request.IsWebSocketRequest)
                        {
                            var wsContext = await context.AcceptWebSocketAsync(null);
                            string remoteEndPoint = context.Request.RemoteEndPoint?.ToString() ?? "Unknown";
                            var client = new Client(wsContext.WebSocket, remoteEndPoint); // Pass WebSocket and remoteEndPoint
                            MainWindow.users.Add(client);
                            MainWindow.mainWindow.SendMessageToConsole($"[{DateTime.Now}]: New WebSocket client connected from {remoteEndPoint}.", 1);
                            _ = client.Process(); // Process client in the background
                        }
                        else
                        {
                            context.Response.StatusCode = 400;
                            context.Response.Close();
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[{DateTime.Now}]: Exception trying to AcceptWebSocket: {ex.Message}");
                        Application.Current.Dispatcher.Invoke(() =>
                            MainWindow.mainWindow.SendMessageToConsole($"[{DateTime.Now}]: Exception trying to AcceptWebSocket: {ex.Message}", 1));
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{DateTime.Now}]: {ex.Message}");
                MainWindow.mainWindow.SendMessageToConsole($"[{DateTime.Now}]: Exception {ex.Message}", 1);
            }
        }

        public static void AddClientToGroup(Client client, string groupId)
        {
            if (GroupExists(groupId))
            {
                Group group = new Group();
                group = GetGroupById(groupId, "AddClientToGroup()");
                group.users.Add(client);
                Console.WriteLine($"[{DateTime.Now}]: Added {client.Username} to Group{groupId} Count is {group.users.Count}");
                mainWindow.SendMessageToConsole($"[{DateTime.Now}]: Added {client.Username} to Group{groupId} Count is {group.users.Count}", 1);
                mainWindow.BuildGroupsListView();
            }
            else if (!GroupExists(groupId))
            {
                Group group = new Group();
                group.groupID = groupId;
                group.dateCreated = DateTime.Now.ToString();
                group.mainWindow = mainWindow;
                groups.Add(group);
                group.users.Add(client);
                mainWindow.BuildGroupsListView();
                Console.WriteLine($"[{DateTime.Now}]: Created Group{groupId} Count is {group.users.Count}");
            }
            BroadcastConnectionToGroup(groupId, client);
        }
        public static bool GroupExists(string _groupID)
        {
            // Console.WriteLine("Checking if Group Exists.");
            for (int i = 0; i < groups.Count; i++)
            {
                if (groups[i].groupID == _groupID)
                {
                    //Console.WriteLine("Group Exists.");
                    return true;
                }
            }
            // Console.WriteLine("Group Doesnt Exist.");
            return false;
        }
        public static bool UserExists(string uid)
        {
            foreach (var item in users)
            {
                if (item.UID.ToString() == uid)
                {
                    return true;
                }
            }
            return false;
        }
        public static Client SelectUserByID(string uid)
        {
            foreach (var user in users)
            {
                if (user.UID.ToString() == uid)
                {
                    return user;
                }
            }
            return null;

        }
        public static Group GetGroupById(string groupId, string reason = "")
        {
            Console.WriteLine($"Getting group with Id {groupId} requested by {reason}.");
            Group group = new Group();
            Console.WriteLine($"Checking the {groups.Count} current total groups for group {groupId}");
            for (int i = 0; i < groups.Count; i++)
            {
                if (groups[i].groupID == groupId)
                {

                    group = groups[i];
                     Console.WriteLine($"Group {group.groupID} Found with {group.users.Count} members.");
                    return group;

                }
            }
           Console.WriteLine($"[{DateTime.Now}]: Could not find Group with ID {group.groupID}.");
            mainWindow.SendMessageToConsole($"[{DateTime.Now}]: Could not find Group with ID {group.groupID}.", 1);
            return group;
        }

        public static int RollaRandomNumber()
        {
            Random rand = new Random();
            int roll = rand.Next(1, 101);
            return roll;
        }

        public static async Task ProccessRollForGroup(string groupID, string uid)
        {
            int roll = RollaRandomNumber();
            await BroadcastRollToGroup(groupID, uid, roll.ToString());

         
        }

        public static async Task ProccessGreedRollForGroup(string groupID, string username)
        {
            int roll = RollaRandomNumber();
            await BroadcastGreedRollToGroup(groupID, username, roll.ToString());


        }


        public static async Task ProccessNeedRollForGroup(string groupID, string uid)
        {

            int roll = RollaRandomNumber();
            await BroadcastNeedRollToGroup(groupID, uid, roll.ToString());


        }


        public static async Task BroadcastRollToGroup(string groupID, string uid, string roll)
        {
            Group group = GetGroupById(groupID);
            Client user = group.GetGroupMemberByUid(uid);

            if (group == null || group.users.Count == 0)
            {
                mainWindow.SendMessageToConsole($"[{DateTime.Now}]: No users in group {groupID} to send connection message.", 1);
                return;
            }

            var message = new
                {
                    Type = "roll",
                    GroupID = groupID,
                    UID = uid,
                    Roll = roll
                };

            string jsonMessage = JsonSerializer.Serialize(message);

            var sendTasks = group.users.Select(async _user =>
            {
                try
                {
                    await _user.SendMessageAsync(jsonMessage);
                    mainWindow.SendMessageToConsole($"[{DateTime.Now}]: Roll [{roll}] by [{user.Username}] in Group {groupID} UID: {uid}", 1);


                }
                catch (Exception ex)
                {
                    mainWindow.SendMessageToConsole($"[{DateTime.Now}]: Failed to send Roll message to [{user.Username}]. Error: {ex.Message}", 1);
                }
            });

            await Task.WhenAll(sendTasks);


            
        }


        public static async Task BroadcastNeedRollToGroup(string groupID, string uid, string roll)
        {
            Group group = GetGroupById(groupID);

            if (group == null || group.users.Count == 0)
            {
                mainWindow.SendMessageToConsole($"[{DateTime.Now}]: No users in group {groupID} to send connection message.", 1);
                return;
            }


            var message = new
                {
                    Type = "need",
                    GroupID = groupID,
                    UID = uid,
                    Roll = roll
                };

            string jsonMessage = JsonSerializer.Serialize(message);
            var sendTasks = group.users.Select(async _user =>
            {
                try
                {
                    await _user.SendMessageAsync(jsonMessage);
                    mainWindow.SendMessageToConsole($"[{DateTime.Now}]: Need Roll [{roll}] by [{_user.Username}] in Group {groupID} UID: {uid}", 15);


                }
                catch (Exception ex)
                {
                    mainWindow.SendMessageToConsole($"[{DateTime.Now}]: Failed to send Need Roll message to [{_user.Username}]. Error: {ex.Message}", 1);
                }
            });

            await Task.WhenAll(sendTasks);

           
        }


        public static async Task BroadcastGreedRollToGroup(string groupID, string uid, string roll)
        {
            Group group = GetGroupById(groupID);

            if (group == null || group.users.Count == 0)
            {
                mainWindow.SendMessageToConsole($"[{DateTime.Now}]: No users in group {groupID} to send connection message.", 1);
                return;
            }


            var message = new
                {
                    Type = "greed",
                    GroupID = groupID,
                    UID = uid,
                    Roll = roll
                };

            string jsonMessage = JsonSerializer.Serialize(message);
            var sendTasks = group.users.Select(async _user =>
            {
                try
                {
                    await _user.SendMessageAsync(jsonMessage);
                    mainWindow.SendMessageToConsole($"[{DateTime.Now}]: Greed Roll [{roll}] by [{_user.Username}] in Group {groupID} UID: {uid}", 15);


                }
                catch (Exception ex)
                {
                    mainWindow.SendMessageToConsole($"[{DateTime.Now}]: Failed to send Greed Roll message to [{_user.Username}]. Error: {ex.Message}", 1);
                }
            });

            await Task.WhenAll(sendTasks);

            
        }

        public static async Task BroadcastConnectionToGroup(string groupId, Client user)
        {
            Group group = GetGroupById(groupId);

            mainWindow.BuildUsersListView();
            mainWindow.BuildGroupsListView();

            if (group == null || group.users.Count == 0)
            {
                mainWindow.SendMessageToConsole($"[{DateTime.Now}]: No users in group {groupId} to send connection message.", 1);
                return;
            }


            var message = new
            {
            Type = "connection",
            GroupID = groupId,
            UID = user.UID.ToString(),
            Username = user.Username

            };

            string jsonMessage = JsonSerializer.Serialize(message);

            var sendTasks = group.users.Select(async _user =>
            {
                if (_user.Username != user.Username)
                {
                    try
                    {
                        await _user.SendMessageAsync(jsonMessage);
                        mainWindow.SendMessageToConsole($"[{DateTime.Now}]: Connection message sent to [{_user.Username}] in Group {groupId}.", 1);
                    }
                    catch (Exception ex)
                    {
                        mainWindow.SendMessageToConsole($"[{DateTime.Now}]: Failed to send connection message to [{_user.Username}]. Error: {ex.Message}", 1);
                    }
                }
            });

            await Task.WhenAll(sendTasks);
        }

        public static async Task BroadcastConnectionsToUser(string groupId, Client user)
        {
            Group group = GetGroupById(groupId, "BroadcastConnectionToUser()");

            if (group == null || user.ClientSocket.State != WebSocketState.Open)
            {
                mainWindow.SendMessageToConsole($"[{DateTime.Now}]: Cannot send connection message. User or group not valid.", 1);
                return;
            }

            foreach(Client client in group.users)
            {
               
                    var message = new
                    {
                        Type = "UserConnection",
                        GroupID = groupId,
                        UID = client.UID.ToString(),
                        Username = client.Username
                    };
                    string jsonMessage = JsonSerializer.Serialize(message);
                    try
                    {
                        await user.SendMessageAsync(jsonMessage);
                        mainWindow.SendMessageToConsole($"[{DateTime.Now}]: Connection message sent to user [{user.Username}] in Group {groupId}.", 1);
                    }
                    catch (Exception ex)
                    {
                        mainWindow.SendMessageToConsole($"[{DateTime.Now}]: Failed to send connection message to [{user.Username}]. Error: {ex.Message}", 1);
                    }

                

            }
       



            //mainWindow.BuildUsersListView();
            //mainWindow.BuildGroupsListView();
        }

        public static async Task BroadcastBadGroupIDToUser(string groupId, Client user)
        {
            Group group = GetGroupById(groupId, "BroadcastBadGroupIDToUser()");

            if (group == null || user.ClientSocket.State != WebSocketState.Open)
            {
                mainWindow.SendMessageToConsole($"[{DateTime.Now}]: Cannot send connection message. User or group not valid.", 1);
                return;
            }

            

                var message = new
                {
                    Type = "badgroupid",
                    GroupID = groupId,
                    UID = user.UID.ToString(),
                    Username = user.Username
                };
                string jsonMessage = JsonSerializer.Serialize(message);
                try
                {
                    await user.SendMessageAsync(jsonMessage);
                    mainWindow.SendMessageToConsole($"[{DateTime.Now}]: Bad Group ID message sent to user [{user.Username}] in Group {groupId}.", 1);
                }
                catch (Exception ex)
                {
                    mainWindow.SendMessageToConsole($"[{DateTime.Now}]: Failed to send connection message to [{user.Username}]. Error: {ex.Message}", 1);
                }



            




            //mainWindow.BuildUsersListView();
            //mainWindow.BuildGroupsListView();
        }


        public static async Task BroadcastMessageToGroup(string groupId, string msg, string uid)
        {
            Group group = GetGroupById(groupId);

            if (group == null || group.users.Count == 0)
            {
                mainWindow.SendMessageToConsole($"[{DateTime.Now}]: No users in group {groupId} to send messages.", 5);
                return;
            }

            var message = new
            {
                Type = "message",
                GroupID = groupId,
                Message = msg,
                UID = uid
            };

            string jsonMessage = JsonSerializer.Serialize(message);

            var sendTasks = group.users.Select(async user =>
            {
                try
                {
                    await user.SendMessageAsync(jsonMessage);
                    mainWindow.SendMessageToConsole($"[{DateTime.Now}]: Sending Message [{msg}] to [{user.Username}] in Group {groupId}", 5);
                }
                catch (Exception ex)
                {
                    mainWindow.SendMessageToConsole($"[{DateTime.Now}]: Failed to send message to [{user.Username}] in Group {groupId}. Error: {ex.Message}", 1);
                }
            });

            await Task.WhenAll(sendTasks);
        }

              

        public static async Task BroadcastRPSToGroup(string groupId, string msg)
        {
            Group group = GetGroupById(groupId);

            if (group == null || group.users.Count == 0)
            {
                mainWindow.SendMessageToConsole($"[{DateTime.Now}]: No users in group {groupId} to send RPS messages.", 17);
                return;
            }

            var message = new
            {
                Type = "rps",
                GroupID = groupId,
                Message = msg
            };

            string jsonMessage = JsonSerializer.Serialize(message);

            var sendTasks = group.users.Select(async user =>
            {
                try
                {
                    await user.SendMessageAsync(jsonMessage);
                    mainWindow.SendMessageToConsole($"[{DateTime.Now}]: Sending RPS Message [{msg}] to [{user.Username}] in Group {groupId}", 17);
                }
                catch (Exception ex)
                {
                    mainWindow.SendMessageToConsole($"[{DateTime.Now}]: Failed to send RPS message to [{user.Username}] in Group {groupId}. Error: {ex.Message}", 20);
                }
            });

            await Task.WhenAll(sendTasks);
        }

        public static async Task BroadcastFlipToGroup(string groupId, string uid, string msg)
        {
            Group group = GetGroupById(groupId);

            if (group == null || group.users.Count == 0)
            {
                mainWindow.SendMessageToConsole($"[{DateTime.Now}]: No users in group {groupId} to send Flip messages.", 16);
                return;
            }

            var message = new
            {
                Type = "flip",
                GroupID = groupId,
                UID = uid,
                Message = msg
            };

            string jsonMessage = JsonSerializer.Serialize(message);

            var sendTasks = group.users.Select(async user =>
            {
                try
                {
                    await user.SendMessageAsync(jsonMessage);
                    mainWindow.SendMessageToConsole($"[{DateTime.Now}]: Sending Flip Message [{msg}] to [{user.Username}] in Group {groupId}", 16);
                }
                catch (Exception ex)
                {
                    mainWindow.SendMessageToConsole($"[{DateTime.Now}]: Failed to send Flip message to [{user.Username}] in Group {groupId}. Error: {ex.Message}", 20);
                }
            });

            await Task.WhenAll(sendTasks);
        }



        public static async Task BrodcastEndRollVoteToGroup(string groupId, string uid)
        {
            Group group = GetGroupById(groupId);

            if (group == null || group.users.Count == 0)
            {
                mainWindow.SendMessageToConsole($"[{DateTime.Now}]: No users in group {groupId} to send End Roll Vote.", 12);
                return;
            }

            var message = new
            {
                Type = "endroll",
                GroupID = groupId,
                UID = uid
            };

            string jsonMessage = JsonSerializer.Serialize(message);

            var sendTasks = group.users.Select(async user =>
            {
                try
                {
                    await user.SendMessageAsync(jsonMessage);
                    mainWindow.SendMessageToConsole($"[{DateTime.Now}]: Sending End Roll Vote for user [{uid}] to [{user.Username}] in Group {groupId}", 12);
                }
                catch (Exception ex)
                {
                    mainWindow.SendMessageToConsole($"[{DateTime.Now}]: Failed to send End Roll Vote to [{user.Username}] in Group {groupId}. Error: {ex.Message}", 1);
                }
            });

            await Task.WhenAll(sendTasks);
        }

        public static async Task BrodcastEndNeedRollVoteToGroup(string groupId, string uid)
        {
            Group group = GetGroupById(groupId);

            if (group == null || group.users.Count == 0)
            {
                mainWindow.SendMessageToConsole($"[{DateTime.Now}]: No users in group {groupId} to send End Need/Greed Roll Vote messages.", 18);
                return;
            }

            var message = new
                {
                    Type = "endneed",
                    GroupID = groupId,
                    UID = uid
                };

            string jsonMessage = JsonSerializer.Serialize(message);

            var sendTasks = group.users.Select(async user =>
            {
                try
                {
                    await user.SendMessageAsync(jsonMessage);
                    mainWindow.SendMessageToConsole($"[{DateTime.Now}]: Sending End Need/Greed Roll Vote for user [{uid}] to [{user.Username}] in Group {groupId}", 18);
                }
                catch (Exception ex)
                {
                    mainWindow.SendMessageToConsole($"[{DateTime.Now}]: Failed to send End Need/Greed Roll Vote to [{user.Username}]. Error: {ex.Message}", 20);
                }
            });

            await Task.WhenAll(sendTasks);
        }

      


        public static async Task BrodcastDisconnectToGroup(string groupId, string uid, string username)
        {
            Group group = GetGroupById(groupId);

            if (group == null || group.users.Count == 0)
            {
                mainWindow.SendMessageToConsole($"[{DateTime.Now}]: No users in group {groupId} to notify of disconnect.", 1);
                return;
            }

            var message = new
            {
                Type = "disconnection",
                GroupID = groupId,
                UID = uid,
                Username = username
            };

            string jsonMessage = JsonSerializer.Serialize(message);

            var sendTasks = group.users.Select(async user =>
            {
                try
                {
                    await user.SendMessageAsync(jsonMessage);
                    mainWindow.SendMessageToConsole($"[{DateTime.Now}]: Notifying [{user.Username}] of disconnect in Group ID {groupId}.", 1);
                }
                catch (Exception ex)
                {
                    mainWindow.SendMessageToConsole($"[{DateTime.Now}]: Failed to notify [{user.Username}] of disconnect in Group ID {groupId}. Error: {ex.Message}", 20);
                }
            });

            await Task.WhenAll(sendTasks);
        }


        public static async Task BrodcastPrivateMessageToUser(string groupId, string msg, string senderUid, string receiverUid)
        {
            Group group = GetGroupById(groupId);

            foreach (var user in group.users)
            {
                if (user.UID.ToString() == receiverUid)
                {
                    var message = new
                    {
                        Type = "private",
                        GroupID = groupId,
                        SenderUID = senderUid,
                        ReceiverUID = receiverUid,
                        Message = msg
                    };

                    string json = JsonSerializer.Serialize(message);
                    await user.SendMessageAsync(json);

                    mainWindow.SendMessageToConsole($"[{DateTime.Now}]: Sending Private Message [{msg}] to [{user.Username}] in Group {groupId}", 5);
                    return;
                }
            }

            mainWindow.SendMessageToConsole($"[{DateTime.Now}]: Broadcast Private Message receiver not found", 5);
        }

        private void rtb_MessageGroup_Click(object sender, RoutedEventArgs e)
        {
            messageAll = false;
            messageGroup = true;
            messageUser = false;
        }
        private void rtb_MessageUser_Click(object sender, RoutedEventArgs e)
        {
            messageAll = false;
            messageGroup = false;
            messageUser = true;
        }
        private void rtb_MessageAll_Click(object sender, RoutedEventArgs e)
        {
            messageAll = true;
            messageGroup = false;
            messageUser = false;
        }

        public void SendMessageToConsole(string message, int color)
        {

            switch (color)
            {
                //System
                case 1:
                    this.Dispatcher.Invoke((() =>
                    {
                        Run run = new Run(message);
                        run.Foreground = (SolidColorBrush)new BrushConverter().ConvertFrom("#ffffff");
                        rtb_Console.Document.Blocks.Add(new Paragraph(run));
                        rtb_Console.ScrollToEnd();
                    }));
                    break;

                //Winning

                case 7:
                    this.Dispatcher.Invoke((() =>
                    {
                        Run run = new Run(message);
                        run.Foreground = (SolidColorBrush)new BrushConverter().ConvertFrom("#40ff00");
                        rtb_Console.Document.Blocks.Add(new Paragraph(run));
                        rtb_Console.ScrollToEnd();
                    }));
                    break;

                //Message 
                case 5:
                    this.Dispatcher.Invoke((() =>
                    {
                        Run run = new Run(message);
                        run.Foreground = (SolidColorBrush)new BrushConverter().ConvertFrom("#00ddff");
                        rtb_Console.Document.Blocks.Add(new Paragraph(run));
                        rtb_Console.ScrollToEnd();
                    }));
                    break;

                //Roll
                case 6:
                    this.Dispatcher.Invoke((() =>
                    {
                        Run run = new Run(message);
                        run.Foreground = (SolidColorBrush)new BrushConverter().ConvertFrom("#ff00ff");
                        rtb_Console.Document.Blocks.Add(new Paragraph(run));
                        rtb_Console.ScrollToEnd();
                    }));
                    break;

                //needGreed
                case 15:
                    this.Dispatcher.Invoke((() =>
                    {
                        Run run = new Run(message);
                        run.Foreground = (SolidColorBrush)new BrushConverter().ConvertFrom("#ffa200");
                        rtb_Console.Document.Blocks.Add(new Paragraph(run));
                        rtb_Console.ScrollToEnd();
                    }));

                    break;

                //RPS
                case 17:
                    this.Dispatcher.Invoke((() =>
                    {
                        Run run = new Run(message);
                        run.Foreground = (SolidColorBrush)new BrushConverter().ConvertFrom("#9000ff");
                        rtb_Console.Document.Blocks.Add(new Paragraph(run));
                        rtb_Console.ScrollToEnd();
                    }));
                    break;
                //Flip
                case 16:
                    this.Dispatcher.Invoke((() =>
                    {
                        Run run = new Run(message);
                        run.Foreground = (SolidColorBrush)new BrushConverter().ConvertFrom("#005eff");
                        rtb_Console.Document.Blocks.Add(new Paragraph(run));
                        rtb_Console.ScrollToEnd();
                    }));
                    break;

                case 20:
                    this.Dispatcher.Invoke((() =>
                    {
                        Run run = new Run(message);
                        run.Foreground = (SolidColorBrush)new BrushConverter().ConvertFrom("#ff0000");
                        rtb_Console.Document.Blocks.Add(new Paragraph(run));
                        rtb_Console.ScrollToEnd();
                    }));
                    break;
            }

        }
        public string SetStartupMessage()
        {
          
                if (this.Dispatcher.Invoke(() => !string.IsNullOrEmpty(txb_StartMessage.Text)))
                {
                    sendStartUpMessage = true;
                    this.Dispatcher.Invoke(() => startUpMessage = txb_StartMessage.Text);
                    return startUpMessage;
                }
                else
                {
                    sendStartUpMessage = false;
                    startUpMessage = "";
                    return null;
                }
       
        }

       

        public void BuildUsersListView()
        {

            this.Dispatcher.Invoke(() => lst_Users.Items.Clear());
            if(users.Count > 0) {
                foreach (var user in users)
                {
                    this.Dispatcher.Invoke(() =>
                    {
                        {
                            TextBox textBox = new TextBox();
                            textBox.FontWeight = FontWeights.Bold;
                            textBox.Visibility = Visibility.Visible;
                            textBox.Width = 100;
                            textBox.Height = 20;
                            textBox.Name = user.Username;
                            textBox.Text = $"{user.Username}";
                            textBox.TextWrapping = TextWrapping.NoWrap;
                            textBox.VerticalContentAlignment = VerticalAlignment.Center;
                            textBox.HorizontalContentAlignment = HorizontalAlignment.Center;
                            textBox.Background = (SolidColorBrush)new BrushConverter().ConvertFrom("#0560f2");
                            textBox.Foreground = new SolidColorBrush(Colors.White);
                            textBox.FontSize = 9;
                            textBox.IsTabStop = false;
                            textBox.Tag = "Users";
                            textBox.IsReadOnly = true;
                            textBox.Uid = user.UID.ToString();
                            textBox.MouseDoubleClick += TextBoxUserMouseDown;
                            lst_Users.Items.Add(textBox);
                           // Console.WriteLine($"Users count {users.Count.ToString()}");
                        }
                    });

                    this.Dispatcher.Invoke(() => lbl_CurrentUsersCount.Content = users.Count.ToString());

                }
            }
        }

        public void BuildGroupsListView()
        {
            this.Dispatcher.Invoke(() => lst_Groups.Items.Clear());

            foreach (var group in groups)
            {
                this.Dispatcher.Invoke(() =>
                {
                    {
                        TextBox textBox = new TextBox();
                        textBox.FontWeight = FontWeights.Bold;

                        textBox.Visibility = Visibility.Visible;
                        textBox.Width = 100;
                        textBox.Height = 20;
                        //textBox.Name = group.groupID;
                        textBox.Text = group.groupID;
                        textBox.TextWrapping = TextWrapping.NoWrap;
                        textBox.VerticalContentAlignment = VerticalAlignment.Center;
                        textBox.HorizontalContentAlignment = HorizontalAlignment.Center;
                        textBox.Background = (SolidColorBrush)new BrushConverter().ConvertFrom("#9000ff");
                        textBox.Foreground = new SolidColorBrush(Colors.White);
                        textBox.FontSize = 9;
                        textBox.IsTabStop = false;
                        textBox.Tag = "Groups";
                        textBox.IsReadOnly = true;
                        textBox.MouseDoubleClick += TextBoxGroupMouseDown;
                        lst_Groups.Items.Add(textBox);
                    }
                });


            }
            this.Dispatcher.Invoke(() => lbl_CurrentGroupsCount.Content = groups.Count.ToString());
           

        }
        public void BuildSelectedGroupsUserListView(string groupID)
        {

            this.Dispatcher.Invoke(() => lst_SelectedGroup.Items.Clear());
            Group group = GetGroupById(groupID, "Build Selected Group ListView");
            this.Dispatcher.Invoke(() => lbl_SelectedGroupUserCount.Content = group.users.Count.ToString());

            foreach (var user in group.users)
            {
                
                this.Dispatcher.Invoke(() => 
                {
                    {
                        TextBox textBox = new TextBox();
                        textBox.FontWeight = FontWeights.Bold;

                        textBox.Visibility = Visibility.Visible;
                        textBox.Width = 100;
                        textBox.Height = 20;
                        textBox.Name = user.Username;
                        textBox.Text = user.Username;
                        textBox.TextWrapping = TextWrapping.NoWrap;
                        textBox.VerticalContentAlignment = VerticalAlignment.Center;
                        textBox.HorizontalContentAlignment = HorizontalAlignment.Center;
                        textBox.Background = (SolidColorBrush)new BrushConverter().ConvertFrom("#ff008c");
                        textBox.Foreground = new SolidColorBrush(Colors.White);
                        textBox.FontSize = 9;
                        textBox.IsTabStop = false;
                        textBox.Tag = "GroupUser";
                        textBox.IsReadOnly = true;
                        textBox.Uid = user.UID.ToString();
                        textBox.MouseDoubleClick += TextBoxUserMouseDown;
                        lst_SelectedGroup.Items.Add(textBox);
                    }
                });
                
            }
            this.Dispatcher.Invoke(() => lbl_SelectedGroupTitle.Content = $"Group {group.groupID} Users");
            //Console.WriteLine($"group.users.Count- {group.users.Count.ToString()}");
            

        }

        private void TextBoxUserMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 1)
            {

                    TextBox clickedTextBox = sender as TextBox;
                     var uid = clickedTextBox.Uid;
                    selectedUser= SelectUserByID(uid);
                    UpdateSelectedUser();
                    //Console.WriteLine($"User {selectedUser.Username} in Group selectedUser.GroupID clicked.uid");
            }
        }

        private void TextBoxGroupMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 1)
            {
                    TextBox clickedTextBox = sender as TextBox;
                    var groupID = clickedTextBox.Text;
                    selectedGroup = GetGroupById(groupID);
                    this.Dispatcher.Invoke(() => { txb_MessagingGroupIDInput.Text = groupID; });
                    BuildSelectedGroupsUserListView(groupID);
            }
        }

        private void UpdateSelectedUser()
        {
            this.Dispatcher.Invoke(() =>
            {
                lbl_SelectedUID.Content = selectedUser.UID.ToString();
                lbl_SelectedUserIP.Content = selectedUser.RemoteEndPoint; // Updated to use the new property
                lbl_SelectedUserName.Content = selectedUser.Username;
                lbl_SelectedUserGroupID.Content = selectedUser.GroupID;
                txb_MessagingUserIDInput.Text = selectedUser.UID.ToString();
            });
        }

        private async void btn_SendMessage_Click(object sender, RoutedEventArgs e)
        {
            if (messageAll)
            {
                try
                {
                    var message = new
                    {
                        Type = "message_all",
                        Message = txb_ConsoleInput.Text
                    };

                    string jsonMessage = JsonSerializer.Serialize(message);

                    var sendTasks = users.Select(async user =>
                    {
                        try
                        {
                            await user.SendMessageAsync(jsonMessage);
                            mainWindow.SendMessageToConsole($"[{DateTime.Now}]: Sending Message [{txb_ConsoleInput.Text}] to [{user.Username}] in All Groups", 20);
                        }
                        catch (Exception ex)
                        {
                            mainWindow.SendMessageToConsole($"[{DateTime.Now}]: Failed to send message to [{user.Username}]. Error: {ex.Message}", 1);
                        }
                    });

                    await Task.WhenAll(sendTasks);
                }
                catch (Exception ex)
                {
                    mainWindow.SendMessageToConsole($"[{DateTime.Now}]: {ex.Message}", 1);
                }
            }
            else if (messageGroup)
            {
                try
                {
                    if (!string.IsNullOrEmpty(txb_MessagingGroupIDInput.Text))
                    {
                        if (GroupExists(txb_MessagingGroupIDInput.Text))
                        {
                            Group group = GetGroupById(txb_MessagingGroupIDInput.Text);

                            var message = new
                            {
                                Type = "message_group",
                                GroupID = txb_MessagingGroupIDInput.Text,
                                Message = txb_ConsoleInput.Text
                            };

                            string jsonMessage = JsonSerializer.Serialize(message);

                            var sendTasks = group.users.Select(async user =>
                            {
                                try
                                {
                                    await user.SendMessageAsync(jsonMessage);
                                    mainWindow.SendMessageToConsole($"[{DateTime.Now}]: Sending Message [{txb_ConsoleInput.Text}] to [{user.Username}] in Group {txb_MessagingGroupIDInput.Text}", 20);
                                }
                                catch (Exception ex)
                                {
                                    mainWindow.SendMessageToConsole($"[{DateTime.Now}]: Failed to send message to [{user.Username}]. Error: {ex.Message}", 1);
                                }
                            });

                            await Task.WhenAll(sendTasks);
                        }
                    }
                }
                catch (Exception ex)
                {
                    mainWindow.SendMessageToConsole($"[{DateTime.Now}]: {ex.Message}", 1);
                }
            }
            else if (messageUser)
            {
                try
                {
                    if (!string.IsNullOrEmpty(txb_MessagingUserIDInput.Text))
                    {
                        if (UserExists(txb_MessagingUserIDInput.Text))
                        {
                            Client user = SelectUserByID(txb_MessagingUserIDInput.Text);

                            var message = new
                            {
                                Type = "message_user",
                                UID = txb_MessagingUserIDInput.Text,
                                Message = txb_ConsoleInput.Text
                            };

                            string jsonMessage = JsonSerializer.Serialize(message);

                            try
                            {
                                await user.SendMessageAsync(jsonMessage);
                                mainWindow.SendMessageToConsole($"[{DateTime.Now}]: Sending Message [{txb_ConsoleInput.Text}] to [{user.Username}]", 20);
                            }
                            catch (Exception ex)
                            {
                                mainWindow.SendMessageToConsole($"[{DateTime.Now}]: Failed to send message to [{user.Username}]. Error: {ex.Message}", 1);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    mainWindow.SendMessageToConsole($"[{DateTime.Now}]: {ex.Message}", 1);
                }
            }
        }


        private async Task KickSelectedUser()
        {
            if (selectedUser != null && selectedUser.ClientSocket.State == WebSocketState.Open)
            {
                // Notify the user they have been kicked
                var message = new
                {
                    Type = "kick_user",
                    Message = "You have been kicked from the server."
                };

                string json = JsonSerializer.Serialize(message);
                await selectedUser.SendMessageAsync(json);

                // Close the WebSocket connection
                await selectedUser.ClientSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Kicked from server", CancellationToken.None);

                // Optionally remove the user from your internal data structures
                MainWindow.users.Remove(selectedUser);
            }
        }

        public async Task KickSelectedGroup()
        {
            if (selectedGroup == null || selectedGroup.users.Count == 0)
            {
                mainWindow.SendMessageToConsole($"[{DateTime.Now}]: No users in the selected group to kick.", 20);
                return;
            }

            var kickMessage = new
            {
                type = "kick_group",
                message = "This group was kicked from the server."
            };

            string jsonMessage = JsonSerializer.Serialize(kickMessage);

            var sendTasks = selectedGroup.users.Select(async user =>
            {
                try
                {
                    if (user.ClientSocket.State == WebSocketState.Open)
                    {
                        await user.SendMessageAsync(jsonMessage);
                        await user.ClientSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Group kicked from server", CancellationToken.None);
                        mainWindow.SendMessageToConsole($"[{DateTime.Now}]: User [{user.Username}] kicked from the group.", 20);
                    }
                }
                catch (Exception ex)
                {
                    mainWindow.SendMessageToConsole($"[{DateTime.Now}]: Failed to kick user [{user.Username}] from the group. Error: {ex.Message}", 20);
                }
            });

            await Task.WhenAll(sendTasks);

            
            groups.Remove(selectedGroup);
        }


        private void btn_KickSelectedUser_Click(object sender, RoutedEventArgs e)
        {
            if (selectedUser != null)
            {
                KickSelectedUser();
            }
        }

        private void btn_AllGroupsKickGroup_Click(object sender, RoutedEventArgs e)
        {
            KickSelectedGroup();
        }

        public void MigrateGroupAfterKickVote(string groupID)
        {
            selectedGroup= GetGroupById(groupID);
            MigrateGroup();


        }

        public async Task MigrateGroup()
        {
            // Generate a new group ID
            var GroupID = Client.GenerateNewGroupID(6);
            while (GroupExists(GroupID))
            {
                GroupID = Client.GenerateNewGroupID(6);
            }

            Group group = new Group
            {
                groupID = GroupID
            };
            groups.Remove(selectedGroup);

            if (selectedGroup.users.Count == 0)
            {
                mainWindow.SendMessageToConsole($"[{DateTime.Now}]: No users in the selected group to migrate.", 20);
                return;
            }

            var migrationMessage = new
            {
                type = "group_migration",
                newGroupId = GroupID
            };

            string jsonMessage = JsonSerializer.Serialize(migrationMessage);

            var sendTasks = selectedGroup.users.Select(async user =>
            {
                try
                {
                    user.GroupID = GroupID;
                    group.users.Add(user);
                    await user.SendMessageAsync(jsonMessage);
                    mainWindow.SendMessageToConsole($"[{DateTime.Now}]: User [{user.Username}] migrated to new Group {GroupID}.", 20);
                }
                catch (Exception ex)
                {
                    mainWindow.SendMessageToConsole($"[{DateTime.Now}]: Failed to migrate user [{user.Username}] to new Group {GroupID}. Error: {ex.Message}", 20);
                }
            });

            await Task.WhenAll(sendTasks);

            groups.Add(group);
            selectedGroup.users.Clear();
            selectedGroup = group;

            // Update the UI with the new group ID
            Application.Current.Dispatcher.Invoke(() =>
            {
                mainWindow.txb_MessagingGroupIDInput.Text = group.groupID;
            });

            BuildGroupsListView();
        }


        private void btn_AllGroupsNewGroupID_Click(object sender, RoutedEventArgs e)
        {
            MigrateGroup();
        }
    }
}