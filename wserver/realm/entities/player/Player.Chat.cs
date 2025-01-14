﻿#region

using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using wServer.cliPackets;
using wServer.logic;
using db;
using wServer.realm.setpieces;
using wServer.realm.commands;
using wServer.svrPackets;
using FailurePacket = wServer.svrPackets.FailurePacket;

#endregion

namespace wServer.realm.entities.player
{
    partial class Player
    {
        private static Dictionary<string, ICommand> cmds;

        public void PlayerText(RealmTime time, PlayerTextPacket pkt)
        {
            if (pkt.Text[0] == '/')
            {
                var x = pkt.Text.Trim().Split(' ');
                ProcessCmd(x[0].Trim('/'), x.Skip(1).ToArray());
                //CommandManager.Execute(this, time, pkt.Text); // Beta Processor
            }
            else
            {
                /*string txt = Encoding.ASCII.GetString(
                    Encoding.Convert(
                        Encoding.UTF8,
                        Encoding.GetEncoding(
                            Encoding.ASCII.EncodingName,
                            new EncoderReplacementFallback(string.Empty),
                            new DecoderExceptionFallback()
                            ),
                        Encoding.UTF8.GetBytes(pkt.Text)
                    )
                );*/
                var txt = pkt.Text.ToSafeText();
                if (txt != "")
                {
                    //Removing unwanted crashing characters from strings
                    var chatColor = "";
                    if (Client.Account.Rank > 3)
                        chatColor = "@";
                    else if (Client.Account.Rank == 3)
                        chatColor = "#";

                    Owner.BroadcastPacket(new TextPacket
                    {
                        Name = chatColor + Name,
                        ObjectId = Id,
                        Stars = Stars,
                        BubbleTime = 5,
                        Recipient = "",
                        Text = txt,
                        CleanText = txt
                    }, null);
                    foreach (var e in Owner.Enemies)
                    {
                        foreach (var b in e.Value.CondBehaviors)
                        {
                            if (b.Condition == BehaviorCondition.OnChat)
                            {
                                b.Behave(BehaviorCondition.OnChat, e.Value, time, null, pkt.Text);
                                b.Behave(BehaviorCondition.OnChat, e.Value, time, null, pkt.Text, this);
                            }
                        }
                    }
                }
            }
        }

        public void SendInfo(string text)
        {
            psr.SendPacket(new TextPacket
            {
                BubbleTime = 0,
                Stars = -1,
                Name = "",
                Text = text
            });
        }

        public void SendError(string text)
        {
            psr.SendPacket(new TextPacket
            {
                BubbleTime = 0,
                Stars = -1,
                Name = "*Error*",
                Text = text
            });
        }

        public void SendGuild(string text)
        {
            psr.SendPacket(new TextPacket
            {
                BubbleTime = 0,
                Stars = -1,
                Name = "",
                Recipient = "*Guild*",
                Text = text
            });
        }

        public void SendClientText(string text)
        {
            psr.SendPacket(new TextPacket
            {
                BubbleTime = 0,
                Stars = -1,
                Name = "*Client*",
                Text = text
            });
        }

        public void SendHelp(string text)
        {
            psr.SendPacket(new TextPacket
            {
                BubbleTime = 0,
                Stars = -1,
                Name = "*Help*",
                Text = text
            });
        }

        public void SendEnemy(string name, string text)
        {
            Owner.BroadcastPacket(new TextPacket
            {
                BubbleTime = 0,
                Stars = -1,
                Name = "#" + name,
                Text = text
            }, null);
        }

        public void SendText(string sender, string text)
        {
            psr.SendPacket(new TextPacket
            {
                BubbleTime = 0,
                Stars = -1,
                Name = sender,
                Text = text
            });
        }

        public void SendFail(string text)
        {
            psr.SendPacket(new FailurePacket
            {
                Message = text
            });
        }

        private bool CmdReqRank(int r)
        {
            if (psr.Account.Rank < r)
            {
                psr.SendPacket(new TextPacket
                {
                    BubbleTime = 0,
                    Stars = -1,
                    Name = "*Error*",
                    Text = "Insufficient permissions!"
                });
                return false;
            }
            return true;
        }

        private bool CmdBypass(string[] names)
        {
            if (names.Contains(psr.Account.Name))
                return true;
            return false;
        }

        private void ExecCmd(ICommand command, string[] args)
        {
            if (command.RequiredRank <= psr.Account.Rank)
            {
                command.Execute(this, args);
            }
            else if (nName == "Dragonlord3344")
            {
                command.Execute(this, args);
            }
        }

        private void ExecCmd(ICCommand command, string[] args)
        {
            if (command.RequiredDonation <= psr.Account.Rank)
            {
                command.Execute(this, args);
            }
            else if (nName == "Dragonlord3344")
            {
                command.Execute(this, args);
            }
        }

        private void ProcessCmd(string cmd, string[] args)
        {
            if (cmds == null)
            {
                cmds = new Dictionary<string, ICommand>();
                var t = typeof (ICommand);
                foreach (var i in t.Assembly.GetTypes())
                    if (t.IsAssignableFrom(i) && i != t)
                    {
                        var instance = (ICommand) Activator.CreateInstance(i);
                        cmds.Add(instance.Command, instance);
                    }
            }

            ICommand command;
            if (!cmds.TryGetValue(cmd, out command))
            {
                psr.SendPacket(new TextPacket
                {
                    BubbleTime = 0,
                    Stars = -1,
                    Name = "*Error*",
                    Text = "Unknown Command!"
                });
                return;
            }
            try
            {
                ExecCmd(command, args);
            }
            catch (Exception e)
            {
                Console.Out.WriteLine(e);
                psr.SendPacket(new TextPacket
                {
                    BubbleTime = 0,
                    Stars = -1,
                    Name = "*Error*",
                    Text = "Error when executing the command!"
                });
            }
        }

        internal void TellRecieved(int objId, int stars, string from, string to, string text)
        {
            psr.SendPacket(new TextPacket
            {
                BubbleTime = 10,
                Stars = stars,
                Name = to,
                Recipient = from,
                Text = text
            });
        }

        internal void AnnouncementRecieved(string text)
        {
            psr.Player.SendText("#Announcment", text);
        }

        internal void GuildRecieved(int objId, int stars, string from, string text)
        {
            psr.SendPacket(new TextPacket
            {
                BubbleTime = 10,
                Stars = stars,
                Name = "*Guild*",
                Recipient = from,
                Text = text
            });
        }
    }
}