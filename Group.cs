using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.RightsManagement;
using System.Text;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Security.Cryptography;
using MapleRoll_Server_UI_.Net.IO;
using System.Net.WebSockets;
using System.Text.Json;

namespace MapleRoll_Server_UI_
{
    public class Group
    {
        public string groupID { get; set; }
        public string dateCreated { get; set; }
        public MainWindow mainWindow { get; set; }
        public List<Client> users = new List<Client>();
        //public  Client userVotedToKick;
        public List<KickVote> kickVotes = new List<KickVote>();

        public Group()
        {

            users = new List<Client>();



        }

        public void ResetKick()
        {
            kickVotes.Clear();
        }

        public bool KickVoteExists(Client user)
        {
            foreach (KickVote voteKick in kickVotes)
            {
                if (voteKick.userToKick.UID == user.UID)
                {
                    return true;

                }

            }
            return false;
        }

        public KickVote GetVoteKickByKickedUser(Client user)
        {


            foreach (KickVote kick in kickVotes)
            {
                if (kick.userToKick.UID == user.UID)
                {
                    return kick;
                }

            }
            return null;

        }

        public Client GetGroupMemberByUid(string uid)
        {
            Console.WriteLine("GetGroupMemberByUid()");
            foreach (Client user in users)
            {
                if (user.UID.ToString() == uid)
                {
                    Console.WriteLine("GetGroupMemberByUid(): User Found");
                    return user;

                }

            }
            Console.WriteLine("GetGroupMemberByUid(): User NOT Found");
            return null;
        }

        public void ProcessKickVote(string toKickUid, string voterUid)
        {
            //Console.WriteLine("Start Processing KickVote");
            Client votedToKick = GetGroupMemberByUid(toKickUid);
            //Console.WriteLine($"Voted to Kick {votedToKick.Username}");
            Client voter = GetGroupMemberByUid(voterUid);

            KickVote kickVote = new KickVote();

            if (!KickVoteExists(votedToKick))
            {
                kickVote.userToKick = votedToKick;
                kickVote.ID = toKickUid;
                kickVotes.Add(kickVote);
                //Console.WriteLine("Kick vote doesnt exist Created Vote");
            }
            if (KickVoteExists(votedToKick))
            {
                //Console.WriteLine("Kick vote exist");
                kickVote = GetVoteKickByKickedUser(votedToKick);

                //Console.WriteLine("Aquired Existing vote");
                if (!kickVote.UserHasVoted(voter.UID.ToString()))
                {
                    kickVote.Votes.Add(voter);
                    // Console.WriteLine("User has not voted.");


                    //Console.WriteLine($"Votes to kick user are {kickVote.Votes.Count}  needed votes {(users.Count) * .6}");
                    if (kickVote.Votes.Count >= (users.Count) * .6)
                    {
                        KickUser(kickVote);

                    }


                }
                else
                {
                    //Console.WriteLine("User has  voted. Exiting");
                    return;
                }
            }
        }

        public async Task KickUser(KickVote kick)
        {
            Client votedToKick = GetGroupMemberByUid(kick.ID);
            Group group = new Group
            {
                groupID = groupID
            };

            // Rebuild group without the kicked user
            foreach (var usr in users)
            {
                if (usr.UID.ToString() != kick.ID)
                {
                    group.users.Add(usr);
                }
            }

            string kickedUserName = votedToKick.Username;

            // Notify the kicked user
            var kickedMessage = new
            {
                Type = "kicked",
                GroupID = kick.ID,
                Message = "You have been kicked from the group."
            };

            string kickedJson = JsonSerializer.Serialize(kickedMessage);
            await votedToKick.SendMessageAsync(kickedJson);

            // Close the WebSocket connection for the kicked user
            if (votedToKick.ClientSocket.State == WebSocketState.Open)
            {
                await votedToKick.ClientSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "You have been kicked from the group.", CancellationToken.None);
            }

            // Notify the remaining group members
            foreach (var user in group.users)
            {
                var notificationMessage = new
                {
                    Type = "group_notification",
                    GroupID = group.groupID,
                    Message = $"{kickedUserName} has been kicked from the group."
                };

                string notificationJson = JsonSerializer.Serialize(notificationMessage);
                await user.SendMessageAsync(notificationJson);
            }

            // Update UI and migrate the group
            mainWindow.selectedGroup = group;
            mainWindow.MigrateGroup();
        }

        public class KickVote()
        {
            public Client userToKick;
            public string ID = "";
            public List<Client> Votes = new List<Client>();


            public bool UserHasVoted(string uid)
            {
                foreach (var vote in Votes)
                {
                    if (vote.UID.ToString() == uid)
                    {
                        return true;
                    }
                }
                return false;
            }
        }
    }
}
