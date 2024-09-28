using MEC;
using AutoEvent;
using PFProject.Vote;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AutoEvent.Interfaces;
using CommandSystem;
using RemoteAdmin;
using Exiled.API.Features;
using UnityEngine;
using YamlDotNet.Core.Tokens;
using AutoEvent.API;

namespace PFProject.Events
{
    public class AutoEvent:IGeneralVote
    {
        public string VoteName => "ev";
        public string VoteNameChinese => "小游戏";
        public string VoteDescription { get; } = "游玩小游戏";
        public string VoteUsage { get; } = ".vote ev [list/run/stop，list不需要投票！]";
        public CoroutineHandle RunHandle { get; set; }
        public bool IsArgumentValid(ArraySegment<string> arguments,ICommandSender sender)
        {
            if(arguments.Count < 1)
            {
                return false;
            }
            if(arguments.At(0) == "stop")
            {
                if (global::AutoEvent.AutoEvent.ActiveEvent == null)
                    return false;
                return true;
            }
            if(arguments.At(0) == "start" || arguments.At(0) == "run" || arguments.At(0) == "play")
            {
                if (arguments.Count < 2)
                    return false;
                if(global::AutoEvent.AutoEvent.ActiveEvent != null)
                {
                    return false;
                }
                Event ev = Event.GetEvent(arguments.At(1));
                if (ev == null)
                    return false;
                return true;
            }
            else if(arguments.At(0) == "list")
            {
                if(sender is PlayerCommandSender)
                {
                    Player pl = Player.Get(sender);
                    pl.SendConsoleMessage("以下是小游戏列表：(下方报错无需理会!)","green");
                    foreach(var ev in Event.Events)
                    {
                        string tag = string.Empty;
                        if (ev is IEventTag itag)
                        {
                            tag = $"<color={itag.TagInfo.Color}>[{itag.TagInfo.Name}]</color> ";
                        }
                        pl.SendConsoleMessage($"<color=white>{ev.Name}</color> {tag}[<color=yellow>{ev.CommandName}</color>]: <color=white>{ev.Description}</color>", "green");
                    }
                    pl.SendConsoleMessage("以上是小游戏列表：(下方报错无需理会!)","green");
                    pl.SendConsoleMessage("如需游玩，请使用：.vote ev run [游戏英文名]","green");
                    return false;
                }
            }
            return true;
        }
        public string FormatArgument(ArraySegment<string> arguments,ICommandSender sender)
        {
            if (arguments.Count < 1)
                return "";
            if (arguments.At(0) == "stop")
                return $"停止小游戏 \"{(global::AutoEvent.AutoEvent.ActiveEvent).Name}\"";
            if (arguments.Count > 1 && (arguments.At(0) == "start" || arguments.At(0) == "run" || arguments.At(0) == "play"))
                return $"游玩小游戏 \"{(Event.GetEvent(arguments.At(1))).Name}\"";
            return "";
        }
        public bool ExecuteVote(ArraySegment<string> arguments, ICommandSender sender, out string response)
        {
            if (arguments.Count < 1)
            {
                response = "参数错误！用法：.vote ev [list/run/stop，list不需要投票！]";
                return false;
            }
            if (arguments.At(0) == "stop")
            {
                if (global::AutoEvent.AutoEvent.ActiveEvent == null)
                {
                    response = "小游戏没有运行！";
                    return false;
                }
                else
                {
                    global::AutoEvent.AutoEvent.ActiveEvent.StopEvent();
                    foreach (var pl in Player.List)
                    {
                        pl.Role.Set(PlayerRoles.RoleTypeId.Spectator);
                    }
                    response = "所有玩家已被杀死，且小游戏马上会结束!";
                    return true;
                }
            }
            if (arguments.At(0) == "start" || arguments.At(0) == "run" || arguments.At(0) == "play")
            {
                var old_arguments = arguments;
                arguments = new ArraySegment<string>(arguments.Skip(1).ToArray());
                if (global::AutoEvent.AutoEvent.ActiveEvent != null)
                {
                    response = $"小游戏 {(global::AutoEvent.AutoEvent.ActiveEvent.Name)} 正在运行！";
                    return false;
                }
                Event ev = Event.GetEvent(arguments.At(0));
                if (ev == null)
                {
                    response = $"没有找到小游戏{arguments.At(0)} ";
                    return false;
                }

                string conf = "";
                EventConfig? config = null;
                if (arguments.Count >= 2)
                {
                    if (!ev.TryGetPresetName(arguments.At(1), out string presetName))
                    {
                        response = $"找不到预设\"{arguments.At(1)}\"";
                        return false;
                    }
                    if (!ev.SetConfig(arguments.At(1)))
                    {
                        response = $"无法设置预设 \"{presetName}\",这可能是由于一个错误!";
                        return false;
                    }
                }

                if (!(ev is IEventMap map && !string.IsNullOrEmpty(map.MapInfo.MapName) && map.MapInfo.MapName.ToLower() != "none"))
                {
                    DebugLogger.LogDebug("No map has been specified for this event!", LogLevel.Warn, true);
                }
                else if (!Extensions.IsExistsMap(map.MapInfo.MapName))
                {
                    response = $"你需要一张地图文件{map.MapInfo.MapName}才能继续游戏！请联系服务器管理员！";
                    return false;
                }

                Round.IsLocked = true;

                if (!Round.IsStarted)
                {
                    Round.Start();

                    Timing.CallDelayed(2f, () => {

                        foreach (Player player in Player.List)
                        {
                            player.ClearInventory();
                        }

                        ev.StartEvent();
                        global::AutoEvent.AutoEvent.ActiveEvent = ev;
                    });
                }
                else
                {
                    ev.StartEvent();
                    global::AutoEvent.AutoEvent.ActiveEvent = ev;
                }

                response = $"小游戏{ev.Name}成功启动！";
                return true;
                arguments = old_arguments;
            }
            response = "找不到对应的操作！（这个样例只复制了几个简单的指令！）";
            return false;
        }
    }
}
