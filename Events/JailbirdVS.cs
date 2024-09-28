using CommandSystem;
using Discord;
using MEC;
using Mirror;
using PFProject.Vote;
using PlayerRoles;
using PlayerRoles.Ragdolls;
using RemoteAdmin;
using System;
using System.Collections.Generic;
using System.Linq;
using Exiled.API.Features;
using System.Windows.Input;
using UnityEngine;
using ICommand = CommandSystem.ICommand;
using Player = Exiled.API.Features.Player;

namespace PFProject.Events
{
    [CommandHandler(typeof(RemoteAdminCommandHandler))]
    public class JailbirdVS : IGeneralVote, ICommand
    {
        public string VoteName => "jbvs";
        public string VoteNameChinese => "囚鸟大战";
        public string VoteDescription { get; } = "进行一场囚鸟大战!";
        public string VoteUsage { get; } = ".vote jbvs <团队数量> <生命值> [--no-showtime] [--showteams] [--showhealth]";
        public string Command { get; } = "jailbirdvs";
        public CoroutineHandle RunHandle { get; set; }

        public string[] Aliases { get; } = Array.Empty<string>();
        public string Description { get; } = "进行一场囚鸟大战!";

        public bool IsArgumentValid(ArraySegment<string> arguments, ICommandSender sender)
        {
            if (arguments.Count < 2)
                return false;
            try
            {
                int teams = int.Parse(arguments.At(0));
                if (teams < 1 || teams > 3)
                    return false;
                int.Parse(arguments.At(1));
            }
            catch (Exception ex)
            {
                return false;
            }
            return true;
        }
        public string FormatArgument(ArraySegment<string> arguments, ICommandSender sender)
        {
            return $"团队数量: {arguments.At(1)} | 生命值: {arguments.At(2)} HP";
        }
        public bool ExecuteVote(ArraySegment<string> arguments, ICommandSender sender, out string response)
        {
            if(GeneralExecute(arguments, sender,out response))
            {
                response = "[<color=green>Execute:S</color>]: 完成操作！";
                return true;
            }
            else
            {
                response = "[<color=red>Execute:E</color>]: " + response;
                return false;
            }
        }
        public bool GeneralExecute(ArraySegment<string> arguments, ICommandSender sender, out string response)
        {
            if (!IsArgumentValid(arguments, sender))
            {
                response = "错误的参数！";
                return false;
            }
            if (Timing.IsRunning(RunHandle))
            {
                response = "Jailbird VS已经运行了！";
                return false;
            }
            int health = 1000, teams = 2;
            bool showtime = true, showteams = false, showhealth = false;
            float waiting = 1f;
            if (arguments.Contains("--no-showtime"))
                showtime = false;
            if (arguments.Contains("--showteams"))
                showteams = true;
            if (arguments.Contains("--showhealth"))
            {
                showhealth = true;
                waiting = 0.5f;
            }
            teams = int.Parse(arguments.At(0));
            health = int.Parse(arguments.At(1));
            if (teams < 1 || teams > 3)
            {
                response = "错误的团队数量！";
                return false;
            }
            RunHandle = Timing.RunCoroutine(Coroutine(showtime, showteams, showhealth, waiting, teams,health));
            response = "启动成功！";
            return true;
        }
        public bool Execute(ArraySegment<string> arguments,ICommandSender sender,out string response)
        {
            return GeneralExecute(arguments, sender, out response);
        }
        public IEnumerator<float> Coroutine(bool showtime, bool showteams, bool showhealth, float waiting, int teams,int health)
        {

            if (teams == 1)
            {
                Exiled.API.Features.Server.FriendlyFire = true;
            }
            foreach (var p in Player.List)
            {
                p.Kill("准备囚鸟大战");
            }
            BasicRagdoll[] ragdolls = UnityEngine.Object.FindObjectsOfType<BasicRagdoll>();
            foreach (var r in ragdolls)
            {
                NetworkServer.Destroy(r.gameObject);
            }
            Log.Info("Allocating players");
            var list = Player.List.ToArray();
            Shuffle(list);
            var result = DistributeEvenly(list.ToList(), teams);
            int role = 0;
            for (int i = 0; i < result.Groups.Count; i++)
            {
                var t = result.Groups[i];
                var ex = result.ExtraElements[i] * health * 1.0f / result.Groups[i].Count;
                foreach (var p in t)
                {
                    RoleTypeId id = 0;
                    if (i == 0)
                    {
                        id = RoleTypeId.ChaosConscript;
                    }
                    else if (i == 1)
                    {
                        id = RoleTypeId.NtfCaptain;
                    }
                    else
                    {
                        id = RoleTypeId.Tutorial;
                    }
                    p.Role.Set(id);
                    p.ClearInventory();
                    p.EnableEffect(Exiled.API.Enums.EffectType.MovementBoost, 30);
                    p.Health = health - ex;
                    p.MaxHealth = health - ex;
                }
                role++;
            }
            Exiled.API.Features.Warhead.Detonate();
            Exiled.API.Features.Round.IsLocked = true;
            foreach (var i in Exiled.API.Features.Pickups.Pickup.List)
            {
                i.Destroy();
            }
            foreach (var player in Player.List)
            {
                player.Broadcast(5, $"<size=35><color=red>囚鸟大战</color></size> | <size=30>赛前预备 5s</size>", Broadcast.BroadcastFlags.Normal, true);
            }
            yield return Timing.WaitForSeconds(5f);
            long start = DateTimeOffset.Now.ToUnixTimeMilliseconds();
            while (true)
            {
                string broadcast_s = "<color=red><size=45>囚鸟大战</size></color>\n", hint_s = "";
                if (!Player.List.Any(x => x.IsAlive))
                {
                    broadcast_s += "<size=35><color=red>游戏结束</color>:<color=red>所有玩家都死了</color></size>";
                    foreach (var p in Player.List)
                    {
                        p.Broadcast(5, broadcast_s, Broadcast.BroadcastFlags.Normal, true);
                    }
                    yield return Timing.WaitForSeconds(5f);
                    break;
                }
                if (teams == 1)
                {

                    if (Player.List.Where((x) => x.IsAlive).Count() == 1)
                    {
                        broadcast_s += $"<size=35><color=red>游戏结束</color>:<color=yellow>{Player.List.Where((x) => x.IsAlive).First().Nickname}</color></color>胜利！</size>";
                        foreach (var p in Player.List)
                        {
                            p.Broadcast(5, broadcast_s, Broadcast.BroadcastFlags.Normal, true);
                        }
                        yield return Timing.WaitForSeconds(5f);
                        break;
                    }
                }
                if (teams != 1)
                {
                    if (Player.List.Where((x) => x.Role.Side == Exiled.API.Enums.Side.ChaosInsurgency).Count() == Player.List.Where((x) => x.IsAlive).Count())
                    {
                        broadcast_s += "<size=35><color=red>游戏结束</color>:<color=green>CI</color>胜利！</size>";
                        foreach (var p in Player.List)
                        {
                            p.Broadcast(5, broadcast_s, Broadcast.BroadcastFlags.Normal, true);
                        }
                        yield return Timing.WaitForSeconds(5f);
                        break;
                    }
                    else if (Player.List.Where((x) => x.Role.Side == Exiled.API.Enums.Side.Mtf).Count() == Player.List.Where((x) => x.IsAlive).Count())
                    {
                        broadcast_s += "<size=35><color=red>游戏结束</color>:<color=blue>MTF</color>胜利！</size>";
                        foreach (var p in Player.List)
                        {
                            p.Broadcast(5, broadcast_s, Broadcast.BroadcastFlags.Normal, true);
                        }
                        yield return Timing.WaitForSeconds(5f);
                        break;
                    }
                    else if (Player.List.Where((x) => x.Role.Side == Exiled.API.Enums.Side.Tutorial).Count() == Player.List.Where((x) => x.IsAlive).Count())
                    {
                        broadcast_s += "<size=35><color=red>游戏结束</color>:<color=#ffc0cb>Tutorial</color>胜利！</size>";
                        foreach (var p in Player.List)
                        {
                            p.Broadcast(5, broadcast_s, Broadcast.BroadcastFlags.Normal, true);
                        }
                        yield return Timing.WaitForSeconds(5f);
                        break;
                    }
                }
                if (showtime)
                {
                    broadcast_s += "<size=25><color=red>已用时间：</color></size><size=25><color=orange>" +
                        ((int)Math.Floor(Math.Abs((start - DateTimeOffset.Now.ToUnixTimeMilliseconds()) / 1000.0f))).ToString() + "s</color></size>\n";
                }
                if (showteams)
                {
                    if (teams == 1)
                    {
                        broadcast_s += $"<size=30><color=red>剩余人数</color>:<color=orange>{Player.List.Where((x) => x.IsAlive).Count()}</color></size>";
                    }
                    else if (teams == 2)
                    {
                        broadcast_s += $"<size=30><color=blue>MTF:{Player.List.Where((x) => x.Role.Side == Exiled.API.Enums.Side.Mtf).Count()}</color> | " +
                            $"<color=green>CI:{Player.List.Where((x) => x.Role.Side == Exiled.API.Enums.Side.ChaosInsurgency).Count()}</color></size>";
                    }
                    else
                    {
                        broadcast_s += $"<size=27><color=blue>MTF:{Player.List.Where((x) => x.Role.Side == Exiled.API.Enums.Side.Mtf).Count()}</color> | " +
                            $"<color=green>CI:{Player.List.Where((x) => x.Role.Side == Exiled.API.Enums.Side.ChaosInsurgency).Count()}</color> | " +
                            $"<color=#ffc0cb>T:{Player.List.Where((x) => x.Role.Side == Exiled.API.Enums.Side.Tutorial).Count()}</color></size> | ";

                    }
                    broadcast_s += "\n";
                }
                if (showhealth)
                {
                    float sum_mtf = 0, sum_ci = 0, sum_tutorial = 0;
                    foreach (var p in Player.List)
                    {
                        if (p.Role.Side == Exiled.API.Enums.Side.Mtf)
                            sum_mtf += p.Health;
                        else if (p.Role.Side == Exiled.API.Enums.Side.ChaosInsurgency)
                            sum_ci += p.Health;
                        else if (p.Role.Side == Exiled.API.Enums.Side.Tutorial)
                            sum_tutorial += p.Health;
                    }
                    string health_s = $"<size=30><color=blue>MTF：{sum_mtf}</color> | <color=green>CI：{sum_ci}</color>";
                    if (sum_tutorial > 0)
                    {
                        health_s += $" | <color=#ffc0cb>T：{sum_tutorial}</color></size>";
                    }
                    else
                    {
                        health_s += "</size>";
                    }
                    hint_s = health_s;
                }
                foreach (var p in Player.List)
                {
                    p.Broadcast(2, broadcast_s, Broadcast.BroadcastFlags.Normal, true);
                    if (showhealth && teams == 1)
                    {
                        if (p.IsAlive)
                        {
                            var players = Player.List.Where((x) => x.Id != p.Id && x.IsAlive);
                            float sum = 0;
                            foreach (var i in players)
                            {
                                sum += i.Health;
                            }
                            p.ShowHint($"<size=33>其余人员血量和：<color=orange>{((int)Math.Floor(sum))}</color></size>", 3);
                        }
                        else
                        {
                            var players = Player.List.Where((x) => x.IsAlive);
                            float sum = 0;
                            foreach (var i in players)
                            {
                                sum += i.Health;
                            }
                            p.ShowHint($"<size=33>剩余人员血量和：<color=orange>{sum}</color></size>", 3);
                        }
                    }
                    else if (!hint_s.IsEmpty())
                    {
                        p.ShowHint(hint_s, 3);
                    }
                    if (p.IsAlive)
                    {
                        if (!p.IsInventoryFull)
                        {
                            p.AddItem(ItemType.Jailbird);
                        }
                    }
                }
                yield return Timing.WaitForSeconds(waiting);
            }
            foreach (var p in Player.List)
            {
                p.Role.Set(RoleTypeId.Tutorial);
            }
            Exiled.API.Features.Warhead.Start();
            Exiled.API.Features.Warhead.Stop();
        }
        public class DistributionResult<T>
        {
            public List<List<T>> Groups { get; set; }
            public List<int> ExtraElements { get; set; }
        }

        static DistributionResult<T> DistributeEvenly<T>(List<T> data, int groupCount)
        {
            List<List<T>> groups = new List<List<T>>();
            List<int> extraElements = new List<int>();

            int minGroupSize = data.Count / groupCount;  // 每组的最小数量
            int extra = data.Count % groupCount;         // 多出来的元素数量

            // 初始化每个分组
            for (int i = 0; i < groupCount; i++)
            {
                groups.Add(new List<T>());
            }

            // 循环分配元素
            for (int i = 0; i < data.Count; i++)
            {
                groups[i % groupCount].Add(data[i]);
            }

            // 计算多余元素
            for (int i = 0; i < groupCount; i++)
            {
                if (groups[i].Count > minGroupSize)
                {
                    extraElements.Add(1);  // 该组有一个多余的元素
                }
                else
                {
                    extraElements.Add(0);  // 该组没有多余的元素
                }
            }

            return new DistributionResult<T>
            {
                Groups = groups,
                ExtraElements = extraElements
            };
        }
        public static void Shuffle(Player[] array)
        {
            System.Random rand = new System.Random();
            for (int i = array.Length - 1; i > 0; i--)
            {
                int j = rand.Next(0, i + 1); // 随机生成一个范围内的索引
                                             // 交换 array[i] 和 array[j]
                Player temp = array[i];
                array[i] = array[j];
                array[j] = temp;
            }
        }
    }
}
