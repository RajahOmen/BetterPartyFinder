using Dalamud.Game.Gui.PartyFinder.Types;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Bindings.ImGui;

namespace BetterPartyFinder.Windows.Main;

public partial class MainWindow
{
    private bool Save;

    private void DrawRestrictionsTab(ConfigurationFilter filter)
    {
        using var table = ImRaii.Table("CategoryTable", 2, ImGuiTableFlags.BordersInnerV);
        if (!table.Success)
            return;

        Save = false;

        ImGui.TableSetupColumn("##Show");
        ImGui.TableSetupColumn("##Hide");

        ImGui.TableNextColumn();
        Helper.TextColored(ImGuiColors.HealerGreen, "Show:");
        ImGui.Separator();

        ImGui.TableNextColumn();
        Helper.TextColored(ImGuiColors.ParsedOrange, "Hide:");
        ImGui.Separator();

        filter[ObjectiveFlags.Practice] = DrawRestrictionEntry("Practice", filter[ObjectiveFlags.Practice]);
        filter[ObjectiveFlags.DutyCompletion] = DrawRestrictionEntry("Duty Completion", filter[ObjectiveFlags.DutyCompletion]);
        filter[ObjectiveFlags.Loot] = DrawRestrictionEntry("Loot", filter[ObjectiveFlags.Loot]);

        DrawSeparator();

        filter[ConditionFlags.None] = DrawRestrictionEntry("No Completion Requirement", filter[ConditionFlags.None]);
        filter[ConditionFlags.DutyIncomplete] = DrawRestrictionEntry("Duty Incomplete", filter[ConditionFlags.DutyIncomplete]);
        filter[ConditionFlags.DutyComplete] = DrawRestrictionEntry("Duty Complete", filter[ConditionFlags.DutyComplete]);
        filter[ConditionFlags.DutyCompleteWeeklyRewardUnclaimed] = DrawRestrictionEntry("Weekly Reward Unclaimed", filter[ConditionFlags.DutyCompleteWeeklyRewardUnclaimed]);

        DrawSeparator();

        filter[DutyFinderSettingsFlags.UndersizedParty] = DrawRestrictionEntry("Undersized Party", filter[DutyFinderSettingsFlags.UndersizedParty]);
        filter[DutyFinderSettingsFlags.MinimumItemLevel] = DrawRestrictionEntry("Minimum Item Level", filter[DutyFinderSettingsFlags.MinimumItemLevel]);
        filter[DutyFinderSettingsFlags.SilenceEcho] = DrawRestrictionEntry("Silence Echo", filter[DutyFinderSettingsFlags.SilenceEcho]);

        DrawSeparator();

        filter.NoLootRule = DrawRestrictionEntry("No Loot Rule", filter.NoLootRule);
		filter[LootRuleFlags.GreedOnly] = DrawRestrictionEntry("Greed Only", filter[LootRuleFlags.GreedOnly]);
        filter[LootRuleFlags.Lootmaster] = DrawRestrictionEntry("Lootmaster", filter[LootRuleFlags.Lootmaster]);

        DrawSeparator();

        filter[SearchAreaFlags.DataCentre] = DrawRestrictionEntry("Data Centre Parties", filter[SearchAreaFlags.DataCentre]);
        filter[SearchAreaFlags.World] = DrawRestrictionEntry("World-Local Parties", filter[SearchAreaFlags.World]);
        filter[SearchAreaFlags.OnePlayerPerJob] = DrawRestrictionEntry("One Player Per Job", filter[SearchAreaFlags.OnePlayerPerJob]);

        if (Save)
            Plugin.Config.Save();
    }

    private bool DrawRestrictionEntry(string name, bool state)
    {
        ImGui.TableNextRow();
        ImGui.TableSetColumnIndex(state ? 0 : 1);

        if (ImGui.Selectable(name))
        {
            state = !state;
            Save = true;
        }

        return state;
    }

    private void DrawSeparator()
    {
        ImGui.TableNextRow();

        ImGui.TableNextColumn();
        ImGui.Separator();
        ImGui.TableNextColumn();
        ImGui.Separator();
    }
}