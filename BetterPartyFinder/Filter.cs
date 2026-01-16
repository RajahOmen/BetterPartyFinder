using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Game.Gui.PartyFinder.Types;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;

namespace BetterPartyFinder;

public class Filter : IDisposable
{
    private Plugin Plugin { get; }

    internal Filter(Plugin plugin)
    {
        Plugin = plugin;

        Plugin.PartyFinderGui.ReceiveListing += ReceiveListing;
    }

    public void Dispose()
    {
        Plugin.PartyFinderGui.ReceiveListing -= ReceiveListing;
    }

    private void ReceiveListing(IPartyFinderListing listing, IPartyFinderListingEventArgs args)
    {
        args.Visible = args.Visible && ListingVisible(listing);
    }

    private unsafe bool ListingVisible(IPartyFinderListing listing)
    {
        var instance = AgentLookingForGroup.Instance();
        if (instance->SearchAreaTab == 1 && Plugin.Config.DisableInWorld)
        {
            Plugin.Log.Verbose("Disabled in world tab.");
            return true;
        }

        if (instance->SearchAreaTab == 2 && Plugin.Config.DisableInPrivate)
        {
            Plugin.Log.Verbose("Disabled in private tab.");
            return true;
        }

        // get the current preset or mark all pfs as visible
        var selectedId = Plugin.Config.SelectedPreset;
        if (selectedId == null || !Plugin.Config.Presets.TryGetValue(selectedId.Value, out var filter))
        {
            Plugin.Log.Verbose("early exit 1");
            return true;
        }

        // check max item level
        if (!filter.AllowHugeItemLevel && Sheets.MaxItemLevel > 0 && listing.MinimumItemLevel > Sheets.MaxItemLevel)
        {
            Plugin.Log.Verbose("early exit 2");
            return false;
        }

        // filter based on duty whitelist/blacklist
        if (filter.Duties.Count > 0 && listing.DutyType == DutyType.Normal)
        {
            var inList = filter.Duties.Contains(listing.RawDuty);
            // ReSharper disable once SwitchStatementHandlesSomeKnownEnumValuesWithDefault
            switch (filter.DutiesMode)
            {
                case ListMode.Blacklist when inList:
                case ListMode.Whitelist when !inList:
                    return false;
            }
        }

        // filter based on item level range
        if (filter.MinItemLevel != null && listing.MinimumItemLevel < filter.MinItemLevel)
        {
            Plugin.Log.Verbose("early exit 3");
            return false;
        }

        if (filter.MaxItemLevel != null && listing.MinimumItemLevel > filter.MaxItemLevel)
        {
            Plugin.Log.Verbose("early exit 4");
            return false;
        }

        // filter based on restrictions
        // make sure the listing doesn't contain any of the toggled off search areas
        if (((listing.SearchArea ^ filter.SearchArea) & ~filter.SearchArea) > 0)
        {
            Plugin.Log.Verbose("early exit 5");
            return false;
        }

        if (listing.LootRules == 0 && !filter.NoLootRule)
        {
            Plugin.Log.Verbose("early exit 6");
            return false;
        }

        if (!listing[filter.LootRule])
        {
            Plugin.Log.Verbose("early exit 7");
            return false;
        }

        if (((listing.DutyFinderSettings ^ filter.DutyFinderSettings) & ~filter.DutyFinderSettings) > 0) {
            Plugin.Log.Verbose("early exit 8");
            return false;
        }

        if (!listing[filter.Conditions])
        {
            Plugin.Log.Verbose("early exit 9");
            return false;
        }

        if (!listing[filter.Objectives])
        {
            Plugin.Log.Verbose("early exit 10");
            return false;
        }

        //filter based on keywords
        if (filter.Keywords.Count() > 0 && listing.Description.TextValue.Length > 0)
        {
            if (!filter.Keywords.CheckDescription(listing.Description.TextValue))
            {
                Plugin.Log.Verbose("early exit 11");
                return false;
            }
        }

        // filter based on category (slow)
        if (!filter.Categories.Any(category => category.ListingMatches(listing)))
        {
            Plugin.Log.Verbose("LISTINGMATCHES WAS FALSE");
            return false;
        }

        // filter based on jobs (slow?)
        if (filter.Jobs.Count > 0 && !listing[SearchAreaFlags.AllianceRaid])
        {
            var slots = listing.Slots.ToArray();
            var present = listing.RawJobsPresent.ToArray();

            // create a list of sets containing the slots each job is able to join
            var jobs = new HashSet<int>[filter.Jobs.Count];
            for (var i = 0; i < jobs.Length; i++)
                jobs[i] = [];

            for (var idx = 0; idx < filter.Jobs.Count; idx++)
            {
                var wanted = filter.Jobs[idx];

                for (var i = 0; i < listing.SlotsAvailable; i++)
                {
                    // if the slot is already full or the job can't fit into it, skip
                    if (present[i] != 0 || !slots[i][wanted])
                        continue;

                    // check for one player per job
                    if (listing[SearchAreaFlags.OnePlayerPerJob])
                    {
                        // make sure at least one job in the wanted set isn't taken
                        foreach (var possibleJob in Enum.GetValues<JobFlags>())
                        {
                            if (!wanted.HasFlag(possibleJob))
                                continue;

                            var job = possibleJob.ClassJob(Plugin.Data);
                            if (job is null)
                                continue;

                            if (present.Contains((byte) job.Value.RowId))
                                continue;

                            jobs[idx].Add(i);
                            break;
                        }
                    }
                    else
                    {
                        // not one player per job
                        jobs[idx].Add(i);
                    }
                }

                // if this job couldn't match any slot, can't join the party
                if (jobs[idx].Count == 0)
                    return false;
            }

            // ensure the number of total slots with possibles joins is at least the number of jobs
            // note that this doesn't make sure it's joinable, see below
            var numSlots = jobs
                .Aggregate((acc, x) => acc.Union(x).ToHashSet())
                .Count;

            if (numSlots < jobs.Length)
                return false;

            // loop through each unique pair of jobs
            for (var i = 0; i < jobs.Length; i++)
            {
                // ReSharper disable once LoopCanBeConvertedToQuery
                for (var j = 0; j < jobs.Length; j++)
                {
                    if (i >= j)
                        continue;

                    var a = jobs[i];
                    var b = jobs[j];

                    // check if the slots either job can join have overlap
                    if (a.Intersect(b).Count() != 1)
                        continue;

                    // if there is overlap, check the difference between the sets
                    // if there is no difference, the party can't be joined
                    // note that if the overlap is more than one slot, we don't need to check
                    if (!a.Except(b).Any())
                        return false;
                }
            }
        }

        // filter based on player
        if (filter.Players.Count > 0)
            if (filter.Players.Any(info => info.Name == listing.Name.TextValue && info.World == listing.HomeWorld.Value.RowId))
                return false;

        return true;
    }
}