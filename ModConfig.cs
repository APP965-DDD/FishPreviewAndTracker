using StardewModdingAPI;

namespace FishTracker
{
    public class ModConfig
    {
        // Keybind to trigger tracking and switch to the map tab (default: M)
        public SButton TrackerKey { get; set; } = SButton.M;

        // If true, fish that haven't been caught yet cannot be tracked (prevents spoilers)
        public bool RequireCaughtFish { get; set; } = false;

        // Toggle in-game preview icon during the fishing minigame
        public bool ShowFishPreview { get; set; } = true;

        // Hide time of day availability in the tracker UI
        public bool HideTimeInfo { get; set; } = false;

        // Scale factor for the hooked fish preview icon (range: 1.0 - 6.0)
        public float PreviewScale { get; set; } = 4.0f;
    }
}