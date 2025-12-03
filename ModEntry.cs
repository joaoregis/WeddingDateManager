using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using System.Linq;

namespace WeddingDateManager
{
    public class ModEntry : Mod
    {
        // Define a tecla de atalho (F9)
        private readonly SButton OpenMenuKey = SButton.F9;

        public override void Entry(IModHelper helper)
        {
            // Inscreve no evento de input de botões
            helper.Events.Input.ButtonPressed += OnButtonPressed;
        }

        private void OnButtonPressed(object? sender, ButtonPressedEventArgs e)
        {
            // "Context guards": só roda se o save estiver carregado e o player livre (não em cutscene)
            if (!Context.IsWorldReady || !Context.IsPlayerFree)
                return;

            if (e.Button == OpenMenuKey)
            {
                // Acessa o estado global do time (Multiplayer)
                // gettingMarried é um NetBool que indica se há casamento pendente
                // Agora varremos TODOS os fazendeiros (incluindo offline) para ver se alguém tem casamento marcado
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