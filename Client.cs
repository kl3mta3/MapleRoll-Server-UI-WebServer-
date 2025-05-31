using MapleRoll_Server_UI_.Net.IO;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Net.WebSockets;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using System.Text.Json;

namespace MapleRoll_Server_UI_
{
    public class Client
    {

        public string Username { get; set; }
        public string GroupID { get; set; }
        public Guid UID { get; set; }
        public Group group { get; set; }
        public string RemoteEndPoint { get; private set; }
        public MainWindow program { get; set; }

        public WebSocket ClientSocket { get; private set; }



        public Client(WebSocket socket, string remoteEndPoint)
        {
            ClientSocket = socket;
            UID = Guid.NewGuid();
            program = MainWindow.mainWindow;
            RemoteEndPoint = remoteEndPoint;
        }

        public async Task Process()
        {
            var buffer = new byte[1024];
            while (ClientSocket.State == WebSocketState.Open)
            {
                try
                {
                    var result = await ClientSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                    if (result.MessageType == WebSocketMessageType.Text)
                    {
                        string message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                        HandleMessage(message);
                    }
                    else if (result.MessageType == WebSocketMessageType.Close)
                    {
                        await ClientSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);
                        MainWindow.users.Remove(this);
                        group.users.Remove(this);
                        if (group.users.Count <= 0)
                        {
                            MainWindow.groups.Remove(group);

                        }
                        else
                        {
                            await MainWindow.BrodcastDisconnectToGroup(this.GroupID, this.UID.ToString(), this.Username);
                            program.SendMessageToConsole($"[{DateTime.Now}]: Client Disconnected.", 1);
                            program.BuildUsersListView();
                            program.BuildGroupsListView();

                        }
                    }
                }
                catch (Exception ex)
                {
                    program.SendMessageToConsole($"[{DateTime.Now}]: Error: {ex.Message}", 1);
                    MainWindow.users.Remove(this);
                    group.users.Remove(this);
                    if (group.users.Count <= 0)
                    {
                        MainWindow.groups.Remove(group);

                    }
                    else
                    {
                        await MainWindow.BrodcastDisconnectToGroup(this.GroupID, this.UID.ToString(), this.Username);
                        program.SendMessageToConsole($"[{DateTime.Now}]: Client Disconnected.", 1);
                        program.BuildUsersListView();
                        program.BuildGroupsListView();

                    }
                    break;
                }
            }
        }

        private T ParseJson<T>(string json)
        {
            try
            {
                return JsonSerializer.Deserialize<T>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
            }
            catch (JsonException ex)
            {
                program.SendMessageToConsole($"[{DateTime.Now}]: JSON parsing error: {ex.Message}", 1);
                throw;
            }
        }

        public class BaseMessage
        {
            public string Type { get; set; }
        }

        private void HandleMessage(string json)
        {
            try
            {
                var baseMessage = ParseJson<BaseMessage>(json);
                program.SendMessageToConsole($"[{DateTime.Now}]: Incoming Message of Type:{baseMessage}", 1);

                switch (baseMessage.Type.ToLower())
                {
                    case "message":
                        HandleIncomingMessage(json);
                        break;
                    case "connection":
                        HandleConnectionMessage(json);
                        break;
                    case "disconnect":
                        HandleDisconnectMessage(json);
                        break;
                    case "roll":
                        HandleRollRequest(json);
                        break;
                    case "rollpass":
                        HandleRollPassRequest(json);
                        break;
                    case "needgreedpass":
                        HandleNeedGreedPassRequest(json);
                        break;
                    case "endroll":
                        HandleRollEndRequest(json);
                        break;
                    case "greed":
                        HandleGreedRoll(json);
                        break;
                    case "need":
                        HandleNeedRoll(json);
                        break;
                    case "endneed":
                        HandleRollEndNeedRequest(json);
                        break;
                    case "flip":
                        HandleFlipMessage(json);
                        break;
                    case "rps":
                        HandleRPSMessage(json);
                        break;
                    case "kick":
                        HandleKickVote(json);
                        break;
                    case "private":
                        HandlePrivateMessage(json);
                        break;
                    case "test":
                        HandleTestMessage(json);
                        break;

                    default:
                        program.SendMessageToConsole($"[{DateTime.Now}]: Unknown message type: {baseMessage.Type}.", 1);
                        break;
                }
            }
            catch (Exception ex)
            {
                program.SendMessageToConsole($"[{DateTime.Now}]: Error handling message: {ex.Message}", 1);
            }
        }


        public void HandleTestMessage(string json)
        {
            var data = ParseJson<TestMessage>(json);
            string groupID = data.GroupID;
            string uID= data.UID;
            string message= data.Message;
            program.SendMessageToConsole($"[{DateTime.Now}]: TEST MESSAGE RECIEVED FROM USER:{uID} IN GROUP:{groupID}.  Message:{message}", 1);

        }

        public class TestMessage
        {

            public string Type { get; set; }
            public string GroupID { get; set; }
            public string UID { get; set; }
            public string Message { get; set; }

        }

        //Message request

        private void HandleIncomingMessage(string json)
        {
            var data = ParseJson<IncomingMessage>(json);
            string groupID = data.GroupID;
            string uid = data.UID;
            MainWindow.BroadcastMessageToGroup(groupID, data.Message, uid);
            program.SendMessageToConsole($"[{DateTime.Now}]: Message received from group {groupID}: {data.Message}.", 5);
        }

        public class IncomingMessage
        {
            public string Type { get; set; }
            public string GroupID { get; set; }
            public string UID { get; set; }
            public string Message { get; set; }
        }


        //roll request


        private void HandleRollRequest(string json)
        {
            var data = ParseJson<RollRequest>(json);
            string groupID = data.GroupID;
            string uid = data.UID;

            MainWindow.ProccessRollForGroup(groupID, uid);
            program.SendMessageToConsole($"[{DateTime.Now}]: User {data.UID} submitted Roll Request from group {groupID}.", 6);
        }

        public class RollRequest
        {
            public string Type { get; set; }
            public string GroupID { get; set; }
            public string UID { get; set; }
        }

        //roll pass request
        private void HandleRollPassRequest(string json)
        {
            var data = ParseJson<RollPassRequest>(json);
            string groupID = data.GroupID;
            string uid = data.UID;
            int roll = 0;
           
            MainWindow.BroadcastRollToGroup(groupID, uid, roll.ToString());
            program.SendMessageToConsole($"[{DateTime.Now}]: User {data.UID} submitted Roll Pass Request from group {groupID}.", 6);
        }

        public class RollPassRequest
        {
            public string Type { get; set; }
            public string GroupID { get; set; }
            public string UID { get; set; }
        }
        //needGreed pass request
        private void HandleNeedGreedPassRequest(string json)
        {
            var data = ParseJson<NeedGreedPassRequest>(json);
            string groupID = data.GroupID;
            string uid = data.UID;
            int roll = 0;

            MainWindow.BroadcastGreedRollToGroup(groupID, uid, roll.ToString());
            program.SendMessageToConsole($"[{DateTime.Now}]: User {data.UID} submitted Need Greed Pass Request from group {groupID}.", 6);
        }

        public class NeedGreedPassRequest
        {
            public string Type { get; set; }
            public string GroupID { get; set; }
            public string UID { get; set; }
        }


        // Disconnect request

        private void HandleDisconnectMessage(string json)
        {

            var data = ParseJson<DisconnectMessage>(json);
            string groupID = data.GroupID;
            string uid= data.UID;
            Group group= MainWindow.GetGroupById(groupID);
            Client user = group.GetGroupMemberByUid(uid);
            //Console.WriteLine($"Disconnect message from {user.Username} in group {group.groupID} it now has {group.users.Count} members.");
            group.users.Remove(user);
            MainWindow.users.Remove(user);

            if (group.users.Count <= 0)
            {
                MainWindow.groups.Remove(group);
                program.SendMessageToConsole($"[{DateTime.Now}]: User {user.Username} Client Disconnected . Group empty Removing group.", 1);
                program.BuildGroupsListView();
            }
            else
            {
                MainWindow.BrodcastDisconnectToGroup(user.GroupID, uid, user.Username);
                program.SendMessageToConsole($"[{DateTime.Now}]:User {user.Username} Client Disconnected from group {groupID}.", 1);
                //program.BuildUsersListView();

            }
               // program.BuildGroupsListView();
                program.BuildUsersListView();
        }

        public class DisconnectMessage
        {

            public string Type { get; set; }
            public string GroupID { get; set; }
            public string UID { get; set; }

        }

        //connection message
        private void HandleConnectionMessage(string json)
        {
            var data = ParseJson<ConnectionMessage>(json);
            string groupID = data.GroupID;
            Username=data.Username;
            UID = Guid.NewGuid();
            if (groupID=="" || groupID==null)
            {
                // Generate a new group ID
                var _GroupID = Client.GenerateNewGroupID(6);
                while (MainWindow.GroupExists(_GroupID))
                {
                    _GroupID = Client.GenerateNewGroupID(6);

                }
                groupID = _GroupID;
                
                MainWindow.AddClientToGroup(this, groupID);
            }
            GroupID = groupID;
           
                if(!MainWindow.GroupExists(groupID))
                {

                    MainWindow.BroadcastBadGroupIDToUser(GroupID, this);


                    Console.WriteLine($"Bad group ID provided as {group.groupID}.");

                    if (group.users.Count <= 0)
                    {
                        MainWindow.groups.Remove(group);
                        program.BuildGroupsListView();
                        program.BuildUsersListView();
                    }
                    else
                    {
                        program.SendMessageToConsole($"[{DateTime.Now}]: Client Disconnected.", 1);
                        program.BuildUsersListView();

                    }

                    return;


                }

            //MainWindow.AddClientToGroup(this, GroupID);

            
            
            MainWindow.BroadcastConnectionToGroup(GroupID, this);
            MainWindow.BroadcastConnectionsToUser(GroupID,this);
            program.SendMessageToConsole($"[{DateTime.Now}]: User {this.Username} has Joined Group{GroupID}.", 6);
        }

        public class ConnectionMessage
        {
            public string Type { get; set; }
            public string GroupID { get; set; }
            public string Username { get; set; }



        }

        //end Roll
        private void HandleRollEndRequest(string json)
        {
            var data = ParseJson<RollEndRequest>(json);
            string groupID = data.GroupID;

            MainWindow.BrodcastEndRollVoteToGroup(groupID, data.UID);
            program.SendMessageToConsole($"[{DateTime.Now}]: User {data.UID} submitted Roll End Request from group {groupID}.", 1);
        }

        public class RollEndRequest
        {
            public string Type { get; set; }
            public string GroupID { get; set; }
            public string UID { get; set; }
        }

        //endneed Roll
        private void HandleRollEndNeedRequest(string json)
        {
            var data = ParseJson<RollEndNeedRequest>(json);
            string groupID = data.GroupID;
            Group group = MainWindow.GetGroupById(data.GroupID);
            Client user = group.GetGroupMemberByUid(data.UID);

            MainWindow.BrodcastEndNeedRollVoteToGroup(groupID, data.UID);
            program.SendMessageToConsole($"[{DateTime.Now}]: User {user.Username} submitted Need Roll End Request from group {groupID}.", 1);
        }

        public class RollEndNeedRequest
        {
            public string Type { get; set; }
            public string GroupID { get; set; }
            public string UID { get; set; }
        }

        //Greed Roll 
        private void HandleGreedRoll(string json)
        {
            var data = ParseJson<GreedRoll>(json);
            string groupID = data.GroupID;
            Group group = MainWindow.GetGroupById(data.GroupID);
            Client user = group.GetGroupMemberByUid(data.UID);

            MainWindow.ProccessGreedRollForGroup(groupID, data.UID);
            program.SendMessageToConsole($"[{DateTime.Now}]: User {user.Username} submitted Greed Roll Request from group {groupID}.", 15);
        }

        public class GreedRoll
        {
            public string Type { get; set; }
            public string GroupID { get; set; }
            public string UID { get; set; }
          
        }

        //Need Roll


        private void HandleNeedRoll(string json)
        {
            var data = ParseJson<NeedRoll>(json);
            Group group = MainWindow.GetGroupById(data.GroupID);
            Client user = group.GetGroupMemberByUid(data.UID);

            MainWindow.ProccessNeedRollForGroup(data.GroupID, data.UID);
            program.SendMessageToConsole($"[{DateTime.Now}]: User {user.Username} submitted Need Roll Request from group {data.GroupID}.", 15);
        }

        public class NeedRoll
        {
            public string Type { get; set; }
            public string GroupID { get; set; }
            public string UID { get; set; }
            
        }



        //flip 
        private void HandleFlipMessage(string json)
        {
            var data = ParseJson<FlipMessage>(json);
            string groupID = data.GroupID;
            string uid=data.UID;

            MainWindow.BroadcastFlipToGroup(groupID,  uid, data.Message);
            program.SendMessageToConsole($"[{DateTime.Now}]: Flip received from group {groupID}: {data.Message}.", 16);
        }

        public class FlipMessage
        {
            public string Type { get; set; }
            public string GroupID { get; set; }
            public string Message { get; set; }
            public string Username {  get; set; }
            public string UID { get; set; }
        }

        //RPS 
        private void HandleRPSMessage(string json)
        {
            var data = ParseJson<RPSMessage>(json);
            string groupID = data.GroupID;

            MainWindow.BroadcastRPSToGroup(groupID, data.Message);
            program.SendMessageToConsole($"[{DateTime.Now}]: RPS Message received from group {groupID}: {data.Message}.", 17);
        }

        public class RPSMessage
        {
            public string Type { get; set; }
            public string GroupID { get; set; }
            public string Message { get; set; }
        }

        //Kick 

        private void HandleKickVote(string json)
        {
            var data = ParseJson<KickVote>(json);
            
            Group group = MainWindow.GetGroupById(data.GroupID);
            program.SendMessageToConsole($"[{DateTime.Now}]: User {data.VoterUsername} submitted to Kick User {data.TargetUsername} from Group {data.GroupID}.", 1);
            group.ProcessKickVote(data.TargetUsername, data.VoterUsername);
        }

        public class KickVote
        {
            public string Type { get; set; }
            public string GroupID { get; set; }
            public string VoterUsername { get; set; }
            public string TargetUsername { get; set; }
        }

        //Private Message

        private void HandlePrivateMessage(string json)
        {
            var data = ParseJson<PrivateMessage>(json);
            string groupID = data.GroupID;
            

            MainWindow.BrodcastPrivateMessageToUser(groupID, data.Message, data.SenderUID, data.ReceiverUID);
            program.SendMessageToConsole($"[{DateTime.Now}]: User {data.SenderUID} sent PM [{data.Message}] to User {data.ReceiverUID} in Group {groupID}.", 1);
        }

        public class PrivateMessage
        {
            public string Type { get; set; }
            public string GroupID { get; set; }
            public string SenderUID { get; set; }
            public string ReceiverUID { get; set; }
            public string Message { get; set; }
        }







        public async Task SendMessageAsync(string message)
        {
            if (ClientSocket.State == WebSocketState.Open)
            {
                var buffer = Encoding.UTF8.GetBytes(message);
                await ClientSocket.SendAsync(new ArraySegment<byte>(buffer), WebSocketMessageType.Text, true, CancellationToken.None);
            }
        }

        //private void Disconnect(Client client)
        //{
        //    //MainWindow.users.Remove(client);
        //    //group.users.Remove(client);
        //    // if(group.users.Count<=0)
        //    //{
        //    //    MainWindow.groups.Remove(group);

        //    //}
        //    // else
        //    //{
        //    //    await MainWindow.BrodcastDisconnectToGroup(client.GroupID, client.UID.ToString(), client.Username);
        //    //    program.SendMessageToConsole($"[{DateTime.Now}]: Client Disconnected.", 1);

        //    //}

         

        //}


        public static string GenerateNewGroupID(int length)
        {
            Random random = new Random();

            const string chars = "ACDEFGHJKLMNPQRTUVWXYZ234679";
            return new string(Enumerable.Repeat(chars, length).Select(s => s[random.Next(s.Length)]).ToArray());

        }

           
        
    }
}
