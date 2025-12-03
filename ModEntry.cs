using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;

namespace WeddingDateManager
{
    public class ModEntry : Mod
    {
        private readonly SButton OpenMenuKey = SButton.F9;

        public override void Entry(IModHelper helper)
        {
            helper.Events.Input.ButtonPressed += OnButtonPressed;
        }

        private void OnButtonPressed(object? sender, ButtonPressedEventArgs e)
        {
            if (!Context.IsWorldReady || !Context.IsPlayerFree)
                return;

            if (e.Button == this.OpenMenuKey)
            {
                bool isWeddingPending = Game1.getAllFarmers().Any(f => f.friendshipData.Values.Any(fd => fd.Status == FriendshipStatus.Engaged && fd.WeddingDate != null));
                bool teamGettingMarried = false;

                try
                {
                    var teamObj = Game1.player.team;

                    var friendshipDataField = this.Helper.Reflection.GetField<object>(teamObj, "friendshipData", false);
                    if (friendshipDataField != null)
                    {
                        dynamic friendshipData = friendshipDataField.GetValue();
                        this.Monitor.Log($"[Debug] Team.friendshipData found. Checking contents...", LogLevel.Info);

                        foreach (var key in friendshipData.Keys)
                        {
                            var friendship = friendshipData[key];
                            var status = friendship.Status;
                            var weddingDate = friendship.WeddingDate;

                            this.Monitor.Log($"[Debug] Pair: {key} | Status: {status} | WeddingDate: {weddingDate}", LogLevel.Info);

                            if (status.ToString() == "Engaged" && weddingDate != null)
                            {
                                this.Monitor.Log($"[Debug] FOUND ENGAGED PAIR IN TEAM DATA! Date: {weddingDate}", LogLevel.Alert);
                                teamGettingMarried = true;
                            }
                        }
                    }
                    else
                    {
                        this.Monitor.Log("[Debug] Team.friendshipData field NOT found.", LogLevel.Warn);
                    }
                }
                catch (Exception ex)
                {
                    this.Monitor.Log($"[Debug] Reflection error inspecting friendshipData: {ex.Message}", LogLevel.Error);
                }

                if (!isWeddingPending && teamGettingMarried)
                {
                    isWeddingPending = true;
                    this.Monitor.Log("Player-Player wedding detected via Team.friendshipData!", LogLevel.Info);
                }
                else
                {
                    this.Monitor.Log($"Wedding Check: Friendship={isWeddingPending}, Team.gettingMarried={teamGettingMarried}", LogLevel.Trace);
                }

                if (isWeddingPending)
                {
                    Game1.activeClickableMenu = new WeddingMenu(this.Helper);
                }
                else
                {
                    Game1.showRedMessage("Nenhum casamento marcado.");
                }
            }
        }
    }
}