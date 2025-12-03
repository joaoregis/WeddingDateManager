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

            if (e.Button == OpenMenuKey)
            {
                bool isWeddingPending = Game1.getAllFarmers().Any(f => f.friendshipData.Values.Any(fd => fd.Status == FriendshipStatus.Engaged && fd.WeddingDate != null));

                if (isWeddingPending)
                {
                    this.Monitor.Log("Abrindo menu de casamento...", LogLevel.Debug);
                    Game1.activeClickableMenu = new WeddingMenu(this.Helper);
                }
                else
                {
                    Game1.addHUDMessage(new HUDMessage("Nenhum casamento marcado.", HUDMessage.error_type));
                }
            }
        }
    }
}