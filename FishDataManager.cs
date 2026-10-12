using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using StardewValley;
using StardewValley.GameData.Locations;
using StardewValley.ItemTypeDefinitions;

namespace FishTracker
{
    public class FishTrackData
    {
        public List<string> Regions = new List<string>();
        public List<string> MapMarkers = new List<string>();
        public List<string> DisplayLocations = new List<string>();
        public List<string> Seasons = new List<string>();
        public string Time = "Anytime";
        public string Weather = "Any Weather";
    }

    public class FishDataManager
    {
        public FishTrackData GetLocationsForFish(string targetFishId)
        {
            FishTrackData result = new FishTrackData();
            string qualifiedTarget = ItemRegistry.QualifyItemId(targetFishId);

            if (string.IsNullOrEmpty(qualifiedTarget)) return result;

            // Track whether seasons have been resolved from LocationData or GSQ
            bool foundLocationSeason = false;

            // 1. Handle Crab Pot items
            Item dummyItem = ItemRegistry.Create(qualifiedTarget);
            if (dummyItem != null && dummyItem.HasContextTag("fish_crab_pot"))
            {
                if (dummyItem.HasContextTag("fish_ocean"))
                {
                    result.MapMarkers.Add("Beach");
                    result.DisplayLocations.Add("Beach");
                }
                else
                {
                    result.MapMarkers.AddRange(new[] { "Town", "Forest", "Mountain" });
                    result.DisplayLocations.AddRange(new[] { "Town", "Forest", "Mountain" });
                }

                result.Seasons.Add("All Seasons");
                result.Time = "Anytime";
                result.Weather = "Any Weather";
                ProcessRegions(result);
                return result;
            }

            // 2. Handle algae and seaweed
            if (qualifiedTarget == "(O)152") { result.MapMarkers.Add("Beach"); result.DisplayLocations.Add("Beach"); result.Seasons.Add("All Seasons"); ProcessRegions(result); return result; }
            if (qualifiedTarget == "(O)153") { result.MapMarkers.AddRange(new[] { "Town", "Forest" }); result.DisplayLocations.AddRange(new[] { "Town", "Forest" }); result.Seasons.Add("All Seasons"); ProcessRegions(result); return result; }
            if (qualifiedTarget == "(O)157") { result.MapMarkers.AddRange(new[] { "UndergroundMine", "BugLand", "Sewer" }); result.DisplayLocations.AddRange(new[] { "Mines", "BugLand", "Sewer" }); result.Seasons.Add("All Seasons"); ProcessRegions(result); return result; }

            // 3. Handle special mine/caldera fish
            if (qualifiedTarget == "(O)162")
            {
                result.MapMarkers.AddRange(new[] { "UndergroundMine", "Caldera" });
                result.DisplayLocations.AddRange(new[] { "Mines (Floor 100)", "Volcano Caldera" });
                result.Seasons.Add("All Seasons");
                ProcessRegions(result);
                return result;
            }
            if (qualifiedTarget == "(O)161") { result.MapMarkers.Add("UndergroundMine"); result.DisplayLocations.Add("Mines (Floor 60)"); result.Seasons.Add("All Seasons"); ProcessRegions(result); return result; }
            if (qualifiedTarget == "(O)158") { result.MapMarkers.Add("UndergroundMine"); result.DisplayLocations.Add("Mines (Floor 20)"); result.Seasons.Add("All Seasons"); ProcessRegions(result); return result; }
            if (qualifiedTarget == "(O)156") { result.MapMarkers.Add("UndergroundMine"); result.DisplayLocations.Add("Mines (Floor 20, 60)"); result.Seasons.Add("All Seasons"); ProcessRegions(result); return result; }

            // 4. Scan location data and spawn conditions (1.6 LocationData)
            Dictionary<string, LocationData> allLocationData = DataLoader.Locations(Game1.content);
            foreach (var locEntry in allLocationData)
            {
                string locationName = locEntry.Key;
                LocationData data = locEntry.Value;

                if (locationName == "Temp" || locationName == "fishingGame") continue;
                if (data.Fish == null) continue;

                foreach (SpawnFishData fishSpawn in data.Fish)
                {
                    string qualifiedSpawn = ItemRegistry.QualifyItemId(fishSpawn.ItemId);
                    if (qualifiedSpawn == qualifiedTarget)
                    {
                        string mapMarker = locationName;
                        string displayText = locationName;

                        if (locationName.StartsWith("Farm")) { mapMarker = "Farm"; displayText = "Farm"; }
                        else if (locationName.StartsWith("Island"))
                        {
                            if (locationName == "IslandSouth" || locationName == "IslandWest" || locationName == "IslandSouthEast" || locationName == "IslandSouthEastCave") displayText = "Island Ocean";
                            else if (locationName == "IslandNorth") displayText = "Island River";
                            else displayText = locationName.Replace("Island", "Island ");
                        }
                        else if (locationName.StartsWith("Custom_"))
                        {
                            displayText = locationName.Replace("Custom_", "");
                            displayText = Regex.Replace(displayText, "([a-z])([A-Z])", "$1 $2");
                        }

                        if (!result.MapMarkers.Contains(mapMarker)) result.MapMarkers.Add(mapMarker);
                        if (!result.DisplayLocations.Contains(displayText)) result.DisplayLocations.Add(displayText);

                        // Read spawn season directly from Season property
                        if (fishSpawn.Season.HasValue)
                        {
                            string sName = fishSpawn.Season.Value.ToString();
                            if (!result.Seasons.Contains(sName)) result.Seasons.Add(sName);
                            foundLocationSeason = true;
                        }

                        // Parse weather and seasonal conditions from Game State Queries (GSQ)
                        if (!string.IsNullOrEmpty(fishSpawn.Condition))
                        {
                            string cond = fishSpawn.Condition.ToLower();

                            // Weather condition
                            if (cond.Contains("weather here sun") || cond.Contains("weather target sun") || cond.Contains("weather sun"))
                                result.Weather = "Sunny";
                            else if (cond.Contains("weather here rain") || cond.Contains("weather target rain") || cond.Contains("weather rain"))
                                result.Weather = "Rainy";

                            // Seasons defined in conditions (e.g. legendary fish)
                            if (cond.Contains("season here spring") || cond.Contains("season spring")) { if (!result.Seasons.Contains("Spring")) result.Seasons.Add("Spring"); foundLocationSeason = true; }
                            if (cond.Contains("season here summer") || cond.Contains("season summer")) { if (!result.Seasons.Contains("Summer")) result.Seasons.Add("Summer"); foundLocationSeason = true; }
                            if (cond.Contains("season here fall") || cond.Contains("season fall")) { if (!result.Seasons.Contains("Fall")) result.Seasons.Add("Fall"); foundLocationSeason = true; }
                            if (cond.Contains("season here winter") || cond.Contains("season winter")) { if (!result.Seasons.Contains("Winter")) result.Seasons.Add("Winter"); foundLocationSeason = true; }
                        }
                    }
                }
            }

            // 5. Fallback/supplementary lookup from Data/Fish
            string rawId = qualifiedTarget.Replace("(O)", "");
            Dictionary<string, string> fishDataDict = DataLoader.Fish(Game1.content);
            if (fishDataDict.TryGetValue(rawId, out string fishInfo))
            {
                string[] fields = fishInfo.Split('/');

                // Time ranges (field index 5)
                if (fields.Length > 5)
                {
                    string rawTime = fields[5];
                    if (rawTime == "both" || rawTime.Trim() == "") result.Time = "Anytime";
                    else
                    {
                        string[] times = rawTime.Split(' ');
                        List<string> formattedTimes = new List<string>();
                        for (int i = 0; i < times.Length - 1; i += 2)
                        {
                            if (int.TryParse(times[i], out int startTime) && int.TryParse(times[i + 1], out int endTime))
                                formattedTimes.Add($"{Game1.getTimeOfDayString(startTime)} - {Game1.getTimeOfDayString(endTime)}");
                        }
                        if (formattedTimes.Count > 0) result.Time = string.Join(", ", formattedTimes);
                    }
                }

                // Seasons (field index 6) - only if not already resolved from LocationData
                if (fields.Length > 6 && !foundLocationSeason)
                {
                    string rawSeasons = fields[6].Trim().ToLower();
                    if (rawSeasons.Contains("spring") && rawSeasons.Contains("summer") && rawSeasons.Contains("fall") && rawSeasons.Contains("winter"))
                    {
                        result.Seasons.Add("All Seasons");
                    }
                    else
                    {
                        string[] seasonArray = rawSeasons.Split(' ');
                        foreach (string s in seasonArray)
                        {
                            if (string.IsNullOrWhiteSpace(s)) continue;
                            string capitalized = char.ToUpper(s[0]) + s.Substring(1);
                            if (!result.Seasons.Contains(capitalized)) result.Seasons.Add(capitalized);
                        }
                    }
                }

                // Weather (field index 7)
                if (fields.Length > 7)
                {
                    string rawWeather = fields[7].Trim().ToLower();
                    if (rawWeather == "sunny" || rawWeather == "sun") result.Weather = "Sunny";
                    else if (rawWeather == "rainy" || rawWeather == "rain") result.Weather = "Rainy";
                }
            }

            if (result.Seasons.Count == 0) result.Seasons.Add("Unknown");

            ProcessRegions(result);
            return result;
        }

        private void ProcessRegions(FishTrackData data)
        {
            bool hasValley = false;
            bool hasIsland = false;

            foreach (string loc in data.MapMarkers)
            {
                if (loc.StartsWith("Island") || loc == "Caldera") hasIsland = true;
                else hasValley = true;
            }

            if (hasValley) data.Regions.Add("Stardew Valley");
            if (hasIsland) data.Regions.Add("Ginger Island");
        }
    }
}