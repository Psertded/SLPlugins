using CommandSystem;
using System;
using Exiled.API.Features;
using MEC;
using Exiled.Permissions.Extensions;
using System.Collections.Generic;
using System.Linq;
using RemoteAdmin;
using ICommand = CommandSystem.ICommand;
using System.Collections;
using System.Reflection;

namespace PFProject.Vote
{
    public interface IGeneralVote
    {
        string VoteName { get; }
        string VoteNameChinese { get; }
        string VoteDescription { get; }
        string VoteUsage { get; }
        CoroutineHandle RunHandle { get; }
        
        bool IsArgumentValid(ArraySegment<string> arguments, ICommandSender sender);
        string FormatArgument(ArraySegment<string> arguments, ICommandSender sender);
        bool ExecuteVote(ArraySegment<string> arguments, ICommandSender sender, out string result);
    }
    [CommandHandler(typeof(GameConsoleCommandHandler))]
    [CommandHandler(typeof(ClientCommandHandler))]
    public class GeneralVoteCommand : ICommand
    {
        public string Command { get; set; } = "vote";
        public string[] Aliases { get; set; } = new string[] { "投票" };
        public string Description { get; set; } = "投票功能";
        public static Dictionary<int, bool> votelist = new();
        CoroutineHandle handle;
        public static bool IsAccept() => (SumAccept() > SumReject()) && SumAccept() >= Player.List.Count / 2;
        public static List<IGeneralVote> generalVotes = new List<IGeneralVote>();
        public static void ReloadList()
        {
            generalVotes = GetImplementingClasses<IGeneralVote>()
                .Select(type =>
                {
                    try
                    {
                        return (IGeneralVote)Activator.CreateInstance(type);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error creating instance of {type.Name}: {ex.Message}");
                        return null;
                    }
                })
                .Where(instance => instance != null)
                .ToList();
        }
        public static Type[] GetImplementingClasses<T>()
        {
            var interfaceType = typeof(T);
            var classes = Assembly.GetExecutingAssembly().GetTypes()
                .Where(t => interfaceType.IsAssignableFrom(t) && t.IsClass)
                .ToArray();

            return classes;
        }
        public static int SumReject()
        {
            int sum = 0;
            foreach(var i in votelist)
            {
                if (i.Value == false)
                    sum++;
            }
            return sum;
        }
        public static int SumAccept()
        {
            int sum = 0;
            foreach (var i in votelist)
            {
                if (i.Value == true)
                    sum++;
            }
            return sum;
        }
        public static IEnumerator<float> CoroutineVote(ArraySegment<string> arguments,ICommandSender sender,IGeneralVote vote)
        {
            string player_s = "玩家 ";
            Player player = null;
            if (sender is not PlayerCommandSender)
                player_s = "服务器";
            else
            {
                player = Player.Get(sender);
                player_s += player.Nickname;
            }
            int time = 30;
            if (Player.List.Count() == 1)
            {
                time = 0;
            }
            while (time > 0)
            {
                time--;
                foreach (var p in Player.List)
                {
                    string hint_s = $"<pos=-20%><size=32>{(player_s)} 请求了{vote.VoteNameChinese}</size>\n<size=30> " +
                    $"[参数： {vote.FormatArgument(arguments, sender)}]</size>\n\n<size=31>当前投票<color=red" +
                    $">{((votelist.ContainsKey(p.Id) && votelist[p.Id] == false)?"[拒绝]":"拒绝")}</color> {SumReject()}" +
                    $" | <color=green>{((votelist.ContainsKey(p.Id) && votelist[p.Id] == true)?"[接受]":"接受")}</color> " +
                    $"{SumAccept()}人数：\n | 剩余时间：{time}</size>\n<size=27>控制台输入.vote y<color=green>接受</color> " +
                    $"/ .vote n<color=red>拒绝</color></size></pos>";
                    p.ShowHint(hint_s, 3);
                }
                yield return Timing.WaitForSeconds(1f);
            }
            if (IsAccept() || Player.List.Count == 1)
            {
                string response;
                bool resp = vote.ExecuteVote(arguments, sender, out response);
                if (resp)
                {
                    yield return Timing.WaitUntilDone(vote.RunHandle);
                }
                if (player != null) player.SendConsoleMessage($"[<color={((resp)?"green":"red")}>{((resp)?"S":"E")}" +
                    $"</color>]: 投票执行结果：{response}", "green");
            }
            yield break;
        }
        public bool Execute(ArraySegment<string> arguments, ICommandSender sender, out string response)
        {
            if (arguments.Count < 1)
            {
                response = "用法: .vote [功能] [功能参数列表]\n或者 .vote [y/n]投票！\n使用.vote feature_list查看所有功能！";
                return false;
            }
            if (arguments.At(0) == "reload")
            {
                if (sender.CheckPermission("vote.reload"))
                {
                    ReloadList();
                    response = "成功重新加载！";
                    return true;
                }
                else
                {
                    response = "权限不足！";
                    return false;
                }
            }
            if (sender is PlayerCommandSender)
            {
                int id = Player.Get(sender).Id;
                if (!votelist.ContainsKey(id))
                {
                    if (arguments.At(0).ToLower() == "n")
                    {
                        votelist[id] = false;
                    }
                    if (arguments.At(0).ToLower() == "y")
                    {
                        votelist[id] = true;
                    }
                }
                else if (arguments.At(0).ToLower() == "y" && arguments.At(0).ToLower() == "n")
                {
                    response = $"您已经投过票了！当前投票:{(votelist[id] ? "<color=green>已通过</color>" : "<color=red>未通过</color>")}";
                    return false;
                }
            }
            if (Timing.IsRunning(handle))
            {
                response = "当前有投票正在进行，请稍后再试！";
                return false;
            }
            Player player = Player.Get(sender);
            if (arguments.At(0) == "feature_list")
            {
                response = "可用的功能列表：\n";
                foreach(var p in generalVotes)
                {
                    response += $"<color=yellow>{p.VoteNameChinese}</color>[<color=orange>{p.VoteName}</color>] - {p.VoteDescription}\n";
                }
                if (player == null || (player.CheckPermission("vote.reload")))
                    response += "\n<color=red>reload</color> - 重载功能列表\n";
                return true;
            }
            votelist.Clear();
            foreach (var p in generalVotes)
            {
                if (arguments.At(0) == p.VoteName)
                {
                    var skippedArray = new ArraySegment<string>(arguments.Skip(1).ToArray());
                    if (p.IsArgumentValid(skippedArray,sender))
                    {
                        handle = Timing.RunCoroutine(CoroutineVote(skippedArray, sender, p));
                        response = $"[<color=green>S</color>]: 成功发起投票！ 格式化后的参数： \"{p.FormatArgument(skippedArray, sender)}\"";
                        return true;
                    }
                    else
                    {
                        response = $"[<color=orange>W</color>]: 参数错误：用法：{p.VoteUsage}";
                        return false;
                    }
                }
            }
            response = "[<color=red>E</color>]: 没有找到对应的功能！";
            return false;
        }
    }
}
