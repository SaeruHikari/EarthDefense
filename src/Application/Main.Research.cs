using Godot;
using System;
using System.Linq;
using Earthward.Domain;
using Earthward.Presentation;
namespace Earthward.Application;

public partial class Main
{
    private string _researchWindowFocus = "";

    public void FocusResearchWindow(string id)
    {
        _researchWindowFocus = id;
        RefreshGraph();
        if (ResearchGraph.Node(id).Count > 0) ResearchGraph.FocusNode(id);
    }


    public bool ResearchSidebarOpen() => DockOpen && Tab == "tech";

    private bool ResearchSidebarPresent() => ResearchSidebarOpen() || (_researchTween?.IsRunning() == true && _researchWidth > RailWidth + .5f);

    public Rect2 ResearchTargetRect()
    {
        float width = Math.Min(760, Math.Max(540, WorldSize.X * .5f));
        width = Math.Min(width, WorldSize.X - 220);
        return new(WorldSize.X - width - 18, 87, width, WorldSize.Y - 172);
    }

    public Rect2 ResearchCurrentRect() => new(WorldSize.X - _researchWidth - 18, 87, _researchWidth, WorldSize.Y - 172);

    private void LayoutResearchSidebar(bool force = false)
    {
        if (ResearchGraph == null)
            return;
        bool open = ResearchSidebarOpen();
        float target = open ? ResearchTargetRect().Size.X : RailWidth;
        if (force || open != _lastResearchLayout || Math.Abs(target - _researchTargetWidth) > .5f)
        {
            _lastResearchLayout = open;
            _researchTargetWidth = target;
            _researchTween?.Kill();
            if (Math.Abs(target - _researchWidth) < .5f)
                _researchWidth = target;
            else
            {
                double duration = ResearchRollDuration * Math.Clamp(Math.Abs(target - _researchWidth) / Math.Max(1, ResearchTargetRect().Size.X - RailWidth), .25, 1);
                _researchTween = CreateTween().SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.InOut);
                _researchTween.TweenMethod(Callable.From<float>(w => { _researchWidth = w; PositionResearchCanvas(); QueueRedraw(); }), _researchWidth, target, duration);
            }
        }
        PositionResearchCanvas();
    }

    private void PositionResearchCanvas()
    {
        var current = ResearchCurrentRect();
        var target = ResearchTargetRect();
        _researchClip.Position = current.Position + new Vector2(10, 99);
        _researchClip.Size = (current.Size - new Vector2(20, 111)).Max(Vector2.One);
        ResearchGraph.Position = target.Position - current.Position;
        ResearchGraph.Size = (target.Size - new Vector2(20, 111)).Max(Vector2.One);
    }

    public void OpenResearchSidebar()
    {
        ExitSpectator();
        CancelBuildSelection();
        CancelResourceUpgrade();
        SolarNavOpen = false;
        DockOpen = true;
        Tab = "tech";
        Modal = "";
        CloseCombatSettings(false);
        LayoutResearchSidebar(true);
        RefreshGraph();
        if (LocalShieldGuidePending && ResearchGraph.Node("D_N4").Count > 0)
            ResearchGraph.FocusNode("D_N4");
    }

    private void RefreshGraph()
    {
        var nodes = Game.GraphNodes(_researchWindowFocus);
        string[] branches = { "K", "M", "L", "I", "D", "C" };
        foreach (var node in nodes)
        {
            string id = node.S("id");
            var status = Game.GetGroupStatus(id);
            string branch = node.S("branch");
            node["branch_id"] = branch;
            node["branch_index"] = Array.IndexOf(branches, branch);
            node["stage"] = 1;
            node["is_independent"] = true;
            node["level"] = status.L("level");
            node["max"] = status.L("max", 1);
            node["available"] = status.B("can_purchase");
            node["requirement_details"] = status.List("requirement_details");
            node["preview"] = status.S("preview");
            node["cost_text"] = CostText(status.Map("cost"));
            node["lock_reason"] = status.S("lock_reason");

        }
        ResearchGraph.SetNodes(nodes);
        _graphDirty = false;
        _graphRefreshDelay = .15;
    }

    public void PurchaseGraph(string id)
    {
        var status = Game.GetGroupStatus(id);
        bool success = Game.PurchaseGroup(id);
        if (success)
        {
            ShowNotice("研究完成 · " + status.S("name", id));
            if (status.B("is_successor") || id.Contains("_R")) _researchWindowFocus = "";
            Sounds.PlaySound("research");
            SaveIfSafe();
        }
        else
        {
            ShowNotice(status.S("lock_reason", "科研或外星科技点不足，检查前置研究"));
            Sounds.PlaySound("error");
        }
        RefreshGraph();
    }
}

