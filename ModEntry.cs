using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Menus;
using StardewValley.ItemTypeDefinitions;

namespace FishTracker
{
    public class ModEntry : Mod
    {
        private FishDataManager dataManager;
        private string trackedFishId = null;
        private FishTrackData trackedData = null;
        private ModConfig Config;

        // Custom UI indicator textures
        private Texture2D iconRegion;
        private Texture2D iconLocation;
        private Texture2D iconSeason;
        private Texture2D iconWeather;
        private Texture2D iconTime;

        private Dictionary<string, Vector2> valleyOffsets = new Dictionary<string, Vector2>()
        {
            { "Farm", new Vector2(380, 350) }, { "Town", new Vector2(585, 474) }, { "Beach", new Vector2(655, 643) },
            { "Forest", new Vector2(122, 442) }, { "Mountain", new Vector2(680, 139) }, { "UndergroundMine", new Vector2(651, 104) },
            { "Desert", new Vector2(-36, 63) }, { "Woods", new Vector2(18, 383) }, { "Sewer", new Vector2(262, 596) },
            { "BugLand", new Vector2(515, 460) }, { "WitchSwamp", new Vector2(529, 14) }, { "Submarine", new Vector2(630, 655) },
            { "NightMarket", new Vector2(670, 655) }
        };

        private Dictionary<string, Vector2> islandOffsets = new Dictionary<string, Vector2>()
        {
            { "IslandSouth", new Vector2(495, 527) }, { "IslandWest", new Vector2(94, 527) }, { "IslandNorth", new Vector2(449, 282) },
            { "IslandSouthEast", new Vector2(651, 486) }, { "IslandSouthEastCave", new Vector2(651, 486) }, { "Caldera", new Vector2(467, 109) }
        };

        public override void Entry(IModHelper helper)
        {
            this.Config = this.Helper.ReadConfig<ModConfig>();
            this.dataManager = new FishDataManager();

            // Load UI indicator textures from mod assets
            this.iconRegion = this.Helper.ModContent.Load<Texture2D>("assets/region.png");
            this.iconLocation = this.Helper.ModContent.Load<Texture2D>("assets/location.png");
            this.iconSeason = this.Helper.ModContent.Load<Texture2D>("assets/season.png");
            this.iconWeather = this.Helper.ModContent.Load<Texture2D>("assets/weather.png");
            this.iconTime = this.Helper.ModContent.Load<Texture2D>("assets/time.png");

            helper.Events.GameLoop.GameLaunched += this.OnGameLaunched;
            helper.Events.Input.ButtonPressed += this.OnButtonPressed;
            helper.Events.Display.RenderedActiveMenu += this.OnRenderedActiveMenu;
            helper.Events.Display.MenuChanged += this.OnMenuChanged;
        }

        private void OnGameLaunched(object sender, GameLaunchedEventArgs e)
        {
            var configMenu = this.Helper.ModRegistry.GetApi<IGenericModConfigMenuApi>("spacechase0.GenericModConfigMenu");
            if (configMenu is null) return;

            configMenu.Register(this.ModManifest, () => this.Config = new ModConfig(), () => this.Helper.WriteConfig(this.Config));

            configMenu.AddKeybind(this.ModManifest, () => this.Config.TrackerKey, val => this.Config.TrackerKey = val,
                () => this.Helper.Translation.Get("config.tracker-key.name"), () => this.Helper.Translation.Get("config.tracker-key.desc"));

            configMenu.AddBoolOption(this.ModManifest, () => this.Config.RequireCaughtFish, val => this.Config.RequireCaughtFish = val,
                () => this.Helper.Translation.Get("config.require-caught.name"), () => this.Helper.Translation.Get("config.require-caught.desc"));

            configMenu.AddBoolOption(this.ModManifest, () => this.Config.ShowFishPreview, val => this.Config.ShowFishPreview = val,
                () => this.Helper.Translation.Get("config.show-preview.name"), () => this.Helper.Translation.Get("config.show-preview.desc"));

            configMenu.AddBoolOption(this.ModManifest, () => this.Config.HideTimeInfo, val => this.Config.HideTimeInfo = val,
                () => this.Helper.Translation.Get("config.hide-time.name"), () => this.Helper.Translation.Get("config.hide-time.desc"));

            configMenu.AddNumberOption(this.ModManifest, () => this.Config.PreviewScale, val => this.Config.PreviewScale = val,
                () => this.Helper.Translation.Get("config.preview-scale.name"), () => this.Helper.Translation.Get("config.preview-scale.desc"), 1.0f, 6.0f, 0.5f);
        }

        private void OnButtonPressed(object sender, ButtonPressedEventArgs e)
        {
            // DevTool: Press 'P' while viewing the Map to calculate cursor offset relative to map bounds
            if (e.Button == SButton.P)
            {
                if (Game1.activeClickableMenu is GameMenu gm && gm.currentTab == GameMenu.mapTab)
                {
                    if (gm.pages[GameMenu.mapTab] is MapPage mapPage)
                    {
                        int mouseX = (int)e.Cursor.ScreenPixels.X;
                        int mouseY = (int)e.Cursor.ScreenPixels.Y;

                        int mapBaseX = mapPage.xPositionOnScreen;
                        int mapBaseY = mapPage.yPositionOnScreen;

                        int offsetX = mouseX - mapBaseX;
                        int offsetY = mouseY - mapBaseY;

                        this.Monitor.Log($"[DevTool] Calculated Offset: new Vector2({offsetX}, {offsetY})", LogLevel.Alert);
                    }
                }
                return;
            }

            if (e.Button != this.Config.TrackerKey) return;

            if (Game1.activeClickableMenu is GameMenu gameMenu && gameMenu.pages[gameMenu.currentTab] is CollectionsPage collectionsPage && collectionsPage.currentTab == CollectionsPage.fishTab)
            {
                int mouseX = (int)e.Cursor.ScreenPixels.X;
                int mouseY = (int)e.Cursor.ScreenPixels.Y;
                var currentIcons = collectionsPage.collections[collectionsPage.currentTab][collectionsPage.currentPage];

                foreach (ClickableTextureComponent fishComponent in currentIcons)
                {
                    if (fishComponent.containsPoint(mouseX, mouseY))
                    {
                        string[] nameParts = fishComponent.name.Split(' ');
                        string hoveredFishId = nameParts[0];
                        bool isCaught = (nameParts.Length > 1 && nameParts[1] == "true");

                        if (this.Config.RequireCaughtFish && !isCaught) return;

                        this.Helper.Input.Suppress(e.Button);
                        this.trackedFishId = hoveredFishId;
                        this.trackedData = this.dataManager.GetLocationsForFish(hoveredFishId);
                        gameMenu.changeTab(GameMenu.mapTab);
                        break;
                    }
                }
            }
        }

        private void OnRenderedActiveMenu(object sender, RenderedActiveMenuEventArgs e)
        {
            // Feature 1: Preview hooked fish beside the BobberBar
            if (this.Config.ShowFishPreview && Game1.activeClickableMenu is BobberBar bobberBar)
            {
                string fishId = bobberBar.whichFish;
                if (!string.IsNullOrEmpty(fishId))
                {
                    ParsedItemData itemData = ItemRegistry.GetDataOrErrorItem(fishId);
                    Vector2 fishDrawPosition = new Vector2(bobberBar.xPositionOnScreen + 180, bobberBar.yPositionOnScreen + 150);

                    float scale = this.Config.PreviewScale;
                    int boxSize = (int)(16 * scale) + 32;

                    IClickableMenu.drawTextureBox(e.SpriteBatch, (int)fishDrawPosition.X - 16, (int)fishDrawPosition.Y - 16, boxSize, boxSize, Color.White);
                    e.SpriteBatch.Draw(itemData.GetTexture(), fishDrawPosition, itemData.GetSourceRect(), Color.White, 0f, Vector2.Zero, scale, SpriteEffects.None, 1f);
                }
            }

            // Feature 2: Fish tracker overlay on the World Map
            else if (Game1.activeClickableMenu is GameMenu gameMenu && gameMenu.currentTab == GameMenu.mapTab)
            {
                if (this.trackedFishId != null && this.trackedData != null && gameMenu.pages[GameMenu.mapTab] is MapPage mapPage)
                {
                    int mapBaseX = mapPage.xPositionOnScreen;
                    int mapBaseY = mapPage.yPositionOnScreen;
                    ParsedItemData fishData = ItemRegistry.GetDataOrErrorItem(this.trackedFishId);

                    string currentRegion = mapPage.mapRegion?.Id ?? "Valley";
                    Dictionary<string, Vector2> activeOffsets = null;

                    // Match current map region view
                    if (currentRegion == "Valley")
                    {
                        activeOffsets = this.valleyOffsets;
                    }
                    else if (currentRegion == "GingerIsland" || currentRegion == "Island")
                    {
                        activeOffsets = this.islandOffsets;
                    }

                    // Render fish icons on map locations
                    if (activeOffsets != null)
                    {
                        foreach (string locationName in this.trackedData.MapMarkers)
                        {
                            if (activeOffsets.TryGetValue(locationName, out Vector2 offset))
                            {
                                Vector2 finalDrawPos = new Vector2(mapBaseX + offset.X, mapBaseY + offset.Y);
                                e.SpriteBatch.Draw(fishData.GetTexture(), finalDrawPos, fishData.GetSourceRect(), Color.White, 0f, Vector2.Zero, 3f, SpriteEffects.None, 1f);
                            }
                        }
                    }

                    // Render info box
                    string fishName = fishData.DisplayName;
                    string regionText = string.Join(", ", this.trackedData.Regions);
                    string rawLocationText = string.Join(", ", this.trackedData.DisplayLocations);
                    if (string.IsNullOrEmpty(rawLocationText)) rawLocationText = "Unknown";
                    string seasonText = string.Join(", ", this.trackedData.Seasons);
                    string weatherText = this.trackedData.Weather;
                    string timeText = this.trackedData.Time;

                    int maxTextWidth = 380;
                    string wrappedLocationText = Game1.parseText(rawLocationText, Game1.smallFont, maxTextWidth);

                    Vector2 nameSize = Game1.dialogueFont.MeasureString(fishName);
                    Vector2 regionSize = Game1.smallFont.MeasureString(regionText);
                    Vector2 locationSize = Game1.smallFont.MeasureString(wrappedLocationText);
                    Vector2 seasonSize = Game1.smallFont.MeasureString(seasonText);
                    Vector2 weatherSize = Game1.smallFont.MeasureString(weatherText);
                    Vector2 timeSize = Game1.smallFont.MeasureString(timeText);

                    int boxX = 32;
                    int boxY = 32;
                    int boxWidth = 450;

                    int boxHeight = 20 + (int)nameSize.Y + 15 + (int)regionSize.Y + 10 + (int)locationSize.Y + 10 + (int)seasonSize.Y + 10 + (int)weatherSize.Y + 20;
                    if (!this.Config.HideTimeInfo) boxHeight += 10 + (int)timeSize.Y;

                    IClickableMenu.drawTextureBox(e.SpriteBatch, boxX, boxY, boxWidth, boxHeight, Color.White);

                    int currentY = boxY + 20;
                    int iconSize = 24;

                    // Fish item icon and title
                    e.SpriteBatch.Draw(fishData.GetTexture(), new Vector2(boxX + 20, currentY), fishData.GetSourceRect(), Color.White, 0f, Vector2.Zero, 2f, SpriteEffects.None, 1f);
                    Utility.drawTextWithShadow(e.SpriteBatch, fishName, Game1.dialogueFont, new Vector2(boxX + 60, currentY), Game1.textColor);
                    currentY += (int)nameSize.Y + 15;

                    // Region
                    e.SpriteBatch.Draw(this.iconRegion, new Rectangle(boxX + 20, currentY, iconSize, iconSize), Color.White);
                    Utility.drawTextWithShadow(e.SpriteBatch, regionText, Game1.smallFont, new Vector2(boxX + 55, currentY), Game1.textColor);
                    currentY += (int)regionSize.Y + 10;

                    // Location
                    e.SpriteBatch.Draw(this.iconLocation, new Rectangle(boxX + 20, currentY, iconSize, iconSize), Color.White);
                    Utility.drawTextWithShadow(e.SpriteBatch, wrappedLocationText, Game1.smallFont, new Vector2(boxX + 55, currentY), Game1.textColor);
                    currentY += (int)locationSize.Y + 10;

                    // Season
                    e.SpriteBatch.Draw(this.iconSeason, new Rectangle(boxX + 20, currentY, iconSize, iconSize), Color.White);
                    Utility.drawTextWithShadow(e.SpriteBatch, seasonText, Game1.smallFont, new Vector2(boxX + 55, currentY), Game1.textColor);
                    currentY += (int)seasonSize.Y + 10;

                    // Weather
                    e.SpriteBatch.Draw(this.iconWeather, new Rectangle(boxX + 20, currentY, iconSize, iconSize), Color.White);
                    Utility.drawTextWithShadow(e.SpriteBatch, weatherText, Game1.smallFont, new Vector2(boxX + 55, currentY), Game1.textColor);
                    currentY += (int)weatherSize.Y + 10;

                    // Time of day
                    if (!this.Config.HideTimeInfo)
                    {
                        e.SpriteBatch.Draw(this.iconTime, new Rectangle(boxX + 20, currentY, iconSize, iconSize), Color.White);
                        Utility.drawTextWithShadow(e.SpriteBatch, timeText, Game1.smallFont, new Vector2(boxX + 55, currentY), Game1.textColor);
                    }
                }
            }
        }

        private void OnMenuChanged(object sender, MenuChangedEventArgs e)
        {
            if (e.NewMenu == null)
            {
                if (this.trackedFishId != null)
                {
                    this.trackedFishId = null;
                    this.trackedData = null;
                }
            }
        }
    }
}